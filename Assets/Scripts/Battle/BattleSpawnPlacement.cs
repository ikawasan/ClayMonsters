using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 戦闘モデルをスポーンTransformへ確実に配置する
    /// 回転反映とメッシュ下端の地面合わせを行う
    /// </summary>
    public static class BattleSpawnPlacement
    {
        /// <summary>
        /// 戦闘中モデルの統一スケール
        /// </summary>
        public const float BattleModelScale = 0.5f;

        /// <summary>
        /// 対戦紹介演出パラメータを調整した時点のモデルスケール
        /// </summary>
        public const float StagingAuthoredModelScale = 0.7f;

        /// <summary>
        /// 作者想定スケール基準の長さを現在の戦闘モデルスケールへ換算する
        /// </summary>
        /// <param name="authoredLength">StagingAuthoredModelScale時点の長さ</param>
        /// <returns>現在スケール向けの長さ</returns>
        public static float ScaleAuthoredLength(float authoredLength)
        {
            if (StagingAuthoredModelScale <= 1e-6f)
            {
                return authoredLength;
            }

            return authoredLength * (BattleModelScale / StagingAuthoredModelScale);
        }

        /// <summary>
        /// モデルをスポーンTransformへ配置する
        /// </summary>
        /// <param name="model">配置するモデル</param>
        /// <param name="spawn">基準Transform</param>
        public static void Apply(Transform model, Transform spawn)
        {
            if (model == null || spawn == null)
            {
                return;
            }

            ApplyAt(model, spawn.position, spawn.rotation, spawn.position.y, spawn);
        }

        /// <summary>
        /// 指定位置へ配置し地面高さへ下端を合わせる
        /// </summary>
        /// <param name="model">配置するモデル</param>
        /// <param name="worldPosition">ルートのワールド座標</param>
        /// <param name="worldRotation">ルートのワールド回転</param>
        /// <param name="groundY">足元を合わせるY座標</param>
        public static void ApplyAt(
            Transform model,
            Vector3 worldPosition,
            Quaternion worldRotation,
            float groundY)
        {
            ApplyAt(model, worldPosition, worldRotation, groundY, hierarchyParent: null);
        }

        /// <summary>
        /// 指定位置へ配置しHierarchy親も指定する
        /// </summary>
        /// <param name="model">配置するモデル</param>
        /// <param name="worldPosition">ルートのワールド座標</param>
        /// <param name="worldRotation">ルートのワールド回転</param>
        /// <param name="groundY">足元を合わせるY座標</param>
        /// <param name="hierarchyParent">破棄しやすいようぶら下げる親</param>
        public static void ApplyAt(
            Transform model,
            Vector3 worldPosition,
            Quaternion worldRotation,
            float groundY,
            Transform hierarchyParent)
        {
            if (model == null)
            {
                return;
            }

            if (hierarchyParent != null)
            {
                model.SetParent(hierarchyParent, true);
            }
            else
            {
                model.SetParent(null, true);
            }

            model.SetPositionAndRotation(worldPosition, worldRotation);
            model.localScale = Vector3.one * BattleModelScale;
            SnapBottomToGroundY(model, groundY);
        }

        /// <summary>
        /// ピボットずれを補正し見た目の中心を水平目標へ合わせる
        /// glTF由来モデルは原点が中心とは限らないため描画基準で寄せ直す
        /// </summary>
        /// <param name="model">対象モデル</param>
        /// <param name="targetCenter">合わせたい水平中心</param>
        public static void RecenterHorizontallyTo(Transform model, Vector3 targetCenter)
        {
            if (model == null
                || !BattleFieldFocusResolver.TryGetPosedModelBounds(model, out Bounds bounds))
            {
                return;
            }

            Vector3 offset = model.position - bounds.center;
            Vector3 corrected = targetCenter + offset;
            corrected.y = model.position.y;
            model.position = corrected;
        }

        /// <summary>
        /// モデル下端を指定Yへ合わせる
        /// </summary>
        /// <param name="model">対象モデル</param>
        /// <param name="groundY">地面Y座標</param>
        public static void SnapBottomToGroundY(Transform model, float groundY)
        {
            if (model == null || !TryGetVisualBottomY(model, out float bottomY))
            {
                return;
            }

            float deltaY = groundY - bottomY;
            if (Mathf.Approximately(deltaY, 0f))
            {
                return;
            }

            Vector3 position = model.position;
            position.y += deltaY;
            model.position = position;
        }

        /// <summary>
        /// モデルとその子から表示上の最下端Yを求める
        /// </summary>
        public static bool TryGetVisualBottomY(Transform model, out float bottomY)
        {
            bottomY = float.PositiveInfinity;
            bool found = false;

            if (model == null)
            {
                return false;
            }

            SkinnedMeshRenderer[] skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[i];
                if (skinnedRenderer == null || !skinnedRenderer.enabled)
                {
                    continue;
                }

                if (TryGetSkinnedBottomY(skinnedRenderer, out float skinnedBottomY))
                {
                    bottomY = Mathf.Min(bottomY, skinnedBottomY);
                    found = true;
                }
            }

            MeshRenderer[] meshRenderers = model.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer meshRenderer = meshRenderers[i];
                if (meshRenderer == null || !meshRenderer.enabled)
                {
                    continue;
                }

                bottomY = Mathf.Min(bottomY, meshRenderer.bounds.min.y);
                found = true;
            }

            return found && bottomY < float.PositiveInfinity;
        }

        private static bool TryGetSkinnedBottomY(SkinnedMeshRenderer skinnedRenderer, out float bottomY)
        {
            bottomY = float.PositiveInfinity;
            var bakedMesh = new Mesh();
            try
            {
                skinnedRenderer.BakeMesh(bakedMesh, true);
                Vector3[] vertices = bakedMesh.vertices;
                Transform rendererTransform = skinnedRenderer.transform;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float y = rendererTransform.TransformPoint(vertices[i]).y;
                    if (y < bottomY)
                    {
                        bottomY = y;
                    }
                }
            }
            finally
            {
                Object.Destroy(bakedMesh);
            }

            return bottomY < float.PositiveInfinity;
        }
    }
}
