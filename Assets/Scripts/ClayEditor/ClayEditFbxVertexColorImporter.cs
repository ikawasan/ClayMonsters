#if UNITY_EDITOR
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace ClayEditor
{
    /// <summary>
    /// FBXモデルを頂点カラー付きメッシュへ変換し造形グリッドへ取り込むEditor専用サービス
    /// </summary>
    public sealed class ClayEditFbxVertexColorImporter
    {
        private readonly ClayVoxelEngine engine;

        [Inject]
        public ClayEditFbxVertexColorImporter(ClayVoxelEngine engine)
        {
            this.engine = engine;
        }

        /// <summary>
        /// FBXモデル資産を頂点カラー変換して造形へ取り込む
        /// </summary>
        /// <param name="modelAsset">FBXモデルまたはPrefab</param>
        /// <param name="spawnParent">一時オブジェクトの親</param>
        /// <param name="clayMaterial">プレビュー用マテリアル</param>
        /// <param name="defaultVertexColor">テクスチャが無い頂点の色</param>
        /// <param name="keepSpawnedMeshPreview">ONのとき頂点カラーメッシュを残す</param>
        /// <param name="colorSubdivisionDepth">色解像度用分割深度</param>
        /// <param name="maxUvEdgeLength">分割判定のUV辺長上限</param>
        /// <param name="spawnEulerAngles">親ローカルでのスポーン向き(度)</param>
        /// <param name="applyAutoOrientation">trueのときY-up自動補正を行う</param>
        /// <param name="importScale">フィット後の一様スケール倍率1が既定のフィットサイズ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功した場合trueと空文字失敗時はfalseと理由</returns>
        public async UniTask<(bool success, string errorMessage)> TryImportAsync(
            GameObject modelAsset,
            Transform spawnParent,
            Material clayMaterial,
            Color defaultVertexColor,
            bool keepSpawnedMeshPreview,
            int colorSubdivisionDepth,
            float maxUvEdgeLength,
            Vector3 spawnEulerAngles,
            bool applyAutoOrientation,
            float importScale,
            CancellationToken cancellationToken)
        {
            if (modelAsset == null)
            {
                return (false, "fbxModelが未設定です");
            }

            if (engine == null)
            {
                return (false, "ClayVoxelEngineが未注入です");
            }

            float safeImportScale = Mathf.Max(importScale, 1e-5f);
            Transform parent = spawnParent != null ? spawnParent : engine.ClayModelTransform;
            Quaternion spawnRotation = Quaternion.Euler(spawnEulerAngles);
            GameObject instanceRoot = null;
            GameObject previewRoot = null;
            Mesh bakedMesh = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                instanceRoot = Object.Instantiate(modelAsset, parent);
                instanceRoot.name = modelAsset.name + "_FbxSource";
                instanceRoot.transform.localPosition = Vector3.zero;
                instanceRoot.transform.localRotation = spawnRotation;
                instanceRoot.transform.localScale = Vector3.one;
                SetRenderersEnabled(instanceRoot, false);

                (bool bakeSuccess, Mesh bakedResult, string bakeError) =
                    await ClayFbxTextureToVertexColorBaker.TryBakeCombinedMeshAsync(
                        instanceRoot,
                        defaultVertexColor,
                        colorSubdivisionDepth,
                        maxUvEdgeLength,
                        cancellationToken);
                if (!bakeSuccess)
                {
                    return (false, bakeError);
                }

                bakedMesh = bakedResult;

                previewRoot = new GameObject(modelAsset.name + "_VertexColorSpawn");
                previewRoot.transform.SetParent(parent, false);
                previewRoot.transform.localPosition = Vector3.zero;
                previewRoot.transform.localRotation = spawnRotation;
                previewRoot.transform.localScale = Vector3.one;

                MeshFilter meshFilter = previewRoot.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = bakedMesh;
                MeshRenderer meshRenderer = previewRoot.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = CreateVertexColorPreviewMaterial(clayMaterial);
                meshRenderer.enabled = keepSpawnedMeshPreview;

                Color[] bakedColors = bakedMesh.colors;
                if (bakedColors == null || bakedColors.Length != bakedMesh.vertexCount)
                {
                    return (false, "ベイクメッシュに頂点カラーがありません");
                }

                return await engine.TryImportFromWorldMeshAsync(
                    bakedMesh,
                    engine.ClayModelTransform,
                    previewRoot.transform,
                    bakedColors,
                    cancellationToken,
                    applyAutoOrientation,
                    fitToGrid: true,
                    importScale: safeImportScale);
            }
            finally
            {
                if (instanceRoot != null)
                {
                    Object.Destroy(instanceRoot);
                }

                if (!keepSpawnedMeshPreview)
                {
                    if (previewRoot != null)
                    {
                        Object.Destroy(previewRoot);
                    }

                    if (bakedMesh != null)
                    {
                        Object.Destroy(bakedMesh);
                    }
                }
            }
        }

        private static Material CreateVertexColorPreviewMaterial(Material clayMaterial)
        {
            Shader vertexColorShader = Shader.Find("Custom/VertexColorUnlit");
            if (vertexColorShader != null)
            {
                var previewMaterial = new Material(vertexColorShader)
                {
                    name = "M_FbxVertexColorPreview",
                    hideFlags = HideFlags.HideAndDontSave
                };
                previewMaterial.SetColor("_BaseColor", Color.white);
                return previewMaterial;
            }

            if (clayMaterial != null)
            {
                var previewMaterial = new Material(clayMaterial)
                {
                    name = "M_FbxVertexColorPreviewClay",
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (previewMaterial.HasProperty("_BaseColor"))
                {
                    previewMaterial.SetColor("_BaseColor", Color.white);
                }

                if (previewMaterial.HasProperty("_VertexColorStrength"))
                {
                    previewMaterial.SetFloat("_VertexColorStrength", 1f);
                }

                if (previewMaterial.HasProperty("_AmbientColor"))
                {
                    previewMaterial.SetColor("_AmbientColor", Color.white);
                }

                if (previewMaterial.HasProperty("_LightSteps"))
                {
                    previewMaterial.SetFloat("_LightSteps", 1f);
                }

                return previewMaterial;
            }

            return null;
        }

        private static void SetRenderersEnabled(GameObject root, bool enabled)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = enabled;
            }
        }
    }
}
#endif
