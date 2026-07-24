using ClayEditor.Rigging;
using System.Collections.Generic;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// モデルの色・部位数・頂点数からHP・攻撃力・防御力・速度・命中を算出する
    /// </summary>
    public static class ModelStatusCalculator
    {
        private const int ColorQuantizeLevels = 4;
        private const int MaxDistinctColorBuckets = 32;
        private const float MinVertexCount = 400f;
        private const float MaxVertexCount = 12000f;
        private const int MinSpeed = 6;
        private const int MaxSpeed = 18;

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
            int vertexCount = mesh.vertexCount;
            partAnalyzer.CountParts(bones, out int armCount, out int legCount, out _, out int backCount);

            return new ModelStatus
            {
                hp = CalculateHp(mesh),
                attack = CalculateAttack(armCount, legCount, backCount),
                defense = CalculateDefense(vertexCount),
                speed = CalculateSpeed(vertexCount),
                hit = CalculateHit(vertexCount)
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
            int hp = Mathf.RoundToInt(
                ModelStatusDefaults.DefaultHp
                + (balanceScore * 140f)
                + (varietyScore * 120f));

            return Mathf.Max(ModelStatusDefaults.DefaultHp, hp);
        }

        private static int CalculateAttack(int armCount, int legCount, int tailCount)
        {
            int limbCount = Mathf.Max(0, armCount) + Mathf.Max(0, legCount) + Mathf.Max(0, tailCount);
            float normalized = Mathf.Clamp01((limbCount - 1f) / 7f);
            int attack = Mathf.RoundToInt(
                Mathf.Lerp(ModelStatusDefaults.MinAttack, ModelStatusDefaults.MaxAttack, normalized));

            return Mathf.Clamp(attack, ModelStatusDefaults.MinAttack, ModelStatusDefaults.MaxAttack);
        }

        private static int CalculateDefense(int vertexCount)
        {
            float normalized = Mathf.InverseLerp(MinVertexCount, MaxVertexCount, vertexCount);
            int defense = Mathf.RoundToInt(
                Mathf.Lerp(ModelStatusDefaults.MinDefense, ModelStatusDefaults.MaxDefense, normalized));

            return Mathf.Clamp(defense, ModelStatusDefaults.MinDefense, ModelStatusDefaults.MaxDefense);
        }

        private static int CalculateSpeed(int vertexCount)
        {
            float normalized = Mathf.InverseLerp(MinVertexCount, MaxVertexCount, vertexCount);
            int speed = Mathf.RoundToInt(Mathf.Lerp(MaxSpeed, MinSpeed, normalized));
            return Mathf.Clamp(speed, MinSpeed, MaxSpeed);
        }

        private static int CalculateHit(int vertexCount)
        {
            float normalized = Mathf.InverseLerp(MinVertexCount, MaxVertexCount, vertexCount);
            int hit = Mathf.RoundToInt(
                Mathf.Lerp(ModelStatusDefaults.MinHit, ModelStatusDefaults.MaxHit, normalized));
            return Mathf.Clamp(hit, ModelStatusDefaults.MinHit, ModelStatusDefaults.MaxHit);
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
