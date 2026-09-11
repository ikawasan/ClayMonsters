using ClayEditor.Rigging;
using System.Collections.Generic;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// モデルの色・部位・形状からHP・攻撃力・防御力・速度・命中を算出する
    /// 各ステは独立した指標で作成時0〜200帯へばらつかせる
    /// </summary>
    public static class ModelStatusCalculator
    {
        private const int ColorQuantizeLevels = 4;
        private const int MaxDistinctColorBuckets = 32;

        // 体積はlog補間用の想定粘土サイズ(ローカル空間)
        private const float MinVolume = 0.02f;
        private const float MaxVolume = 120f;

        // 非溶接メッシュ想定の三角面数帯(面数=vertexCount/3相当)
        private const float MinTriangleCount = 200f;
        private const float MaxTriangleCount = 80000f;

        // 攻撃スコア=腕1.6+脚1.0+尻尾0.7 の想定レンジ
        private const float MinAttackLimbScore = 0.5f;
        private const float MaxAttackLimbScore = 12f;

        // 縦横比(高さ/最大幅) で命中を散らす
        private const float MinVerticality = 0.45f;
        private const float MaxVerticality = 3.2f;

        private static readonly (Color color, StatusColorGroup group)[] Palette =
        {
            (new Color(1f, 0f, 0f), StatusColorGroup.Red),
            (new Color(0f, 1f, 0f), StatusColorGroup.Green),
            (new Color(0f, 0f, 1f), StatusColorGroup.Blue),
            (new Color(1f, 1f, 1f), StatusColorGroup.White),
            (new Color(0f, 0f, 0f), StatusColorGroup.Black)
        };

        private enum StatusColorGroup
        {
            Red,
            Green,
            Blue,
            White,
            Black
        }

        /// <summary>
        /// 編集中モデルからステータスを算出する
        /// </summary>
        /// <param name="renderer">スキンメッシュ</param>
        /// <param name="partAnalyzer">部位解析</param>
        /// <param name="bones">ボーン配列</param>
        /// <returns>算出したステータス</returns>
        public static ModelStatus Calculate(
            SkinnedMeshRenderer renderer,
            SkeletonPartAnalyzer partAnalyzer,
            Transform[] bones)
        {
            if (renderer?.sharedMesh == null || renderer.sharedMesh.vertexCount <= 0)
            {
                return ModelStatusDefaults.Create();
            }

            Mesh mesh = renderer.sharedMesh;
            ResolveShapeFeatures(
                mesh,
                out float volume,
                out float verticality,
                out int triangleCount);

            int armCount = 0;
            int legCount = 0;
            int backCount = 0;
            if (partAnalyzer != null)
            {
                partAnalyzer.CountParts(bones, out armCount, out legCount, out _, out backCount);
            }

            return new ModelStatus
            {
                hp = CalculateHp(mesh),
                attack = CalculateAttack(armCount, legCount, backCount),
                defense = CalculateDefense(volume, triangleCount),
                speed = CalculateSpeed(volume, triangleCount),
                hit = CalculateHit(verticality, armCount, legCount)
            };
        }

        private static int CalculateHp(Mesh mesh)
        {
            Color[] colors = mesh.colors;
            if (colors == null || colors.Length == 0)
            {
                return ModelStatusDefaults.DefaultHp;
            }

            var groupCounts = new int[5];
            var distinctColors = new HashSet<int>();
            int sampled = 0;

            for (int i = 0; i < colors.Length; i++)
            {
                Color color = colors[i];
                if (color.a <= 0.01f)
                {
                    continue;
                }

                sampled++;
                groupCounts[(int)ClassifyColor(color)]++;
                distinctColors.Add(QuantizeColor(color));
            }

            if (sampled == 0)
            {
                return ModelStatusDefaults.DefaultHp;
            }

            float balanceScore = CalculateBalanceScore(groupCounts, sampled);
            float varietyScore = Mathf.Clamp01(distinctColors.Count / (float)MaxDistinctColorBuckets);
            // 色の均衡と多様さでHPを散らす
            float colorScore = Mathf.Clamp01((balanceScore * 0.55f) + (varietyScore * 0.45f));
            // 単色寄りが底張りしすぎないよう中央に寄せすぎず広げる
            colorScore = Mathf.Pow(colorScore, 0.85f);
            return LerpStat(
                ModelStatusDefaults.MinHp,
                ModelStatusDefaults.MaxHp,
                colorScore);
        }

        private static int CalculateAttack(int armCount, int legCount, int tailCount)
        {
            // 腕を主指標にして脚尻尾は加点止まり上限張り付きを避ける
            float limbScore =
                (Mathf.Max(0, armCount) * 1.6f)
                + (Mathf.Max(0, legCount) * 1.0f)
                + (Mathf.Max(0, tailCount) * 0.7f);
            float normalized = NormalizeLog(limbScore, MinAttackLimbScore, MaxAttackLimbScore);
            // 2腕2脚=約5.2で中位少し上程度に留まる
            return LerpStat(
                ModelStatusDefaults.MinAttack,
                ModelStatusDefaults.MaxAttack,
                normalized);
        }

        private static int CalculateDefense(float volume, int triangleCount)
        {
            // 体積が大きいほど防御
            float volumeScore = NormalizeLog(volume, MinVolume, MaxVolume);
            // 面が多いほど表面が厚い扱い(密度)
            float densityScore = NormalizeLog(
                Mathf.Max(1, triangleCount),
                MinTriangleCount,
                MaxTriangleCount);
            float score = Mathf.Clamp01((volumeScore * 0.65f) + (densityScore * 0.35f));
            return LerpStat(
                ModelStatusDefaults.MinDefense,
                ModelStatusDefaults.MaxDefense,
                score);
        }

        private static int CalculateSpeed(float volume, int triangleCount)
        {
            // 小さいほど速く面が少ないほど速い(防御と逆相関寄りだが係数をずらして完全一致を避ける)
            float volumeScore = NormalizeLog(volume, MinVolume, MaxVolume);
            float densityScore = NormalizeLog(
                Mathf.Max(1, triangleCount),
                MinTriangleCount,
                MaxTriangleCount);
            float bulky = Mathf.Clamp01((volumeScore * 0.55f) + (densityScore * 0.45f));
            float score = 1f - bulky;
            return LerpStat(
                ModelStatusDefaults.MinSpeed,
                ModelStatusDefaults.MaxSpeed,
                score);
        }

        private static int CalculateHit(float verticality, int armCount, int legCount)
        {
            // 縦長・上半身寄りで命中を上げる
            float tallScore = NormalizeLinear(verticality, MinVerticality, MaxVerticality);
            float armBias = Mathf.Clamp01(Mathf.Max(0, armCount) / 6f);
            float legBias = Mathf.Clamp01(Mathf.Max(0, legCount) / 6f);
            // 脚過多は接地寄りで命中をやや抑え腕は加点
            float score = Mathf.Clamp01((tallScore * 0.7f) + (armBias * 0.35f) - (legBias * 0.15f));
            return LerpStat(
                ModelStatusDefaults.MinHit,
                ModelStatusDefaults.MaxHit,
                score);
        }

        private static void ResolveShapeFeatures(
            Mesh mesh,
            out float volume,
            out float verticality,
            out int triangleCount)
        {
            Bounds bounds = mesh.bounds;
            Vector3 size = bounds.size;
            float sx = Mathf.Max(0.001f, size.x);
            float sy = Mathf.Max(0.001f, size.y);
            float sz = Mathf.Max(0.001f, size.z);
            volume = sx * sy * sz;
            float maxWidth = Mathf.Max(sx, sz);
            verticality = sy / maxWidth;

            // 非溶接MCメッシュはvertexCount=三角面*3なので面数へ換算してレンジを現実に合わせる
            int rawVertices = Mathf.Max(0, mesh.vertexCount);
            if (mesh.triangles != null && mesh.triangles.Length >= 3)
            {
                triangleCount = mesh.triangles.Length / 3;
            }
            else
            {
                triangleCount = Mathf.Max(1, rawVertices / 3);
            }
        }

        private static int LerpStat(int min, int max, float t)
        {
            int value = Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01(t)));
            return Mathf.Clamp(value, min, max);
        }

        private static float NormalizeLog(float value, float min, float max)
        {
            float clamped = Mathf.Clamp(value, min, max);
            float logMin = Mathf.Log(min);
            float logMax = Mathf.Log(max);
            if (Mathf.Abs(logMax - logMin) < 0.0001f)
            {
                return 0.5f;
            }

            return Mathf.InverseLerp(logMin, logMax, Mathf.Log(clamped));
        }

        private static float NormalizeLinear(float value, float min, float max)
        {
            return Mathf.InverseLerp(min, max, value);
        }

        private static float CalculateBalanceScore(int[] groupCounts, int total)
        {
            int presentGroups = 0;
            for (int i = 0; i < groupCounts.Length; i++)
            {
                if (groupCounts[i] > 0)
                {
                    presentGroups++;
                }
            }

            if (presentGroups <= 1)
            {
                return 0f;
            }

            float entropy = 0f;
            for (int i = 0; i < groupCounts.Length; i++)
            {
                if (groupCounts[i] <= 0)
                {
                    continue;
                }

                float probability = groupCounts[i] / (float)total;
                entropy -= probability * Mathf.Log(probability, 2f);
            }

            float maxEntropy = Mathf.Log(presentGroups, 2f);
            if (maxEntropy <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(entropy / maxEntropy);
        }

        private static StatusColorGroup ClassifyColor(Color color)
        {
            StatusColorGroup nearest = StatusColorGroup.Red;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < Palette.Length; i++)
            {
                Color paletteColor = Palette[i].color;
                float dr = color.r - paletteColor.r;
                float dg = color.g - paletteColor.g;
                float db = color.b - paletteColor.b;
                float distance = (dr * dr) + (dg * dg) + (db * db);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = Palette[i].group;
                }
            }

            return nearest;
        }

        private static int QuantizeColor(Color color)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(color.r * ColorQuantizeLevels), 0, ColorQuantizeLevels);
            int g = Mathf.Clamp(Mathf.RoundToInt(color.g * ColorQuantizeLevels), 0, ColorQuantizeLevels);
            int b = Mathf.Clamp(Mathf.RoundToInt(color.b * ColorQuantizeLevels), 0, ColorQuantizeLevels);
            int span = ColorQuantizeLevels + 1;
            return ((r * span) + g) * span + b;
        }
    }
}
