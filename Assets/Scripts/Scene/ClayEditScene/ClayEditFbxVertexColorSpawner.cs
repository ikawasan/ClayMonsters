using System.Threading;
using Camera.Interface;
using ClayEditor;
using ClayEditor.Input.Interface;
using Cysharp.Threading.Tasks;
using Scene.ClayEditScene.Interface;
using UI.ClayEditor.View;
using UnityEngine;
using VContainer;

namespace Scene.ClayEditScene
{
    /// <summary>
    /// ClayEditシーンでFBXを頂点カラー変換してスポーンする
    /// Editor専用機能でありROMビルドではコンポーネントを破棄する
    /// </summary>
    public sealed class ClayEditFbxVertexColorSpawner : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Source")]
        [Tooltip("Project上のFBXモデルまたはPrefab")]
        [SerializeField] private GameObject fbxModel;

        [Header("Spawn")]
        [Tooltip("一時メッシュの親未設定時はClayModel直下へ置く")]
        [SerializeField] private Transform spawnParent;

        [Tooltip("プレビュー表示に使う粘土マテリアル")]
        [SerializeField] private Material clayMaterial;

        [Tooltip("テクスチャが無い頂点の色")]
        [SerializeField] private Color defaultVertexColor = new Color(0.94f, 0.86f, 0.74f, 1f);

        [Tooltip("頂点カラー解像度用の分割深度大きいほどテクスチャに近く重い")]
        [Range(0, 6)]
        [SerializeField] private int colorSubdivisionDepth = 4;

        [Tooltip("UV辺がこの長さを超えたら分割する(1でテクスチャ全体)")]
        [SerializeField] private float maxUvEdgeLength = 0.04f;

        [Tooltip("ONのとき頂点カラー付きメッシュを残すOFFならボクセル化後に破棄する")]
        [SerializeField] private bool keepSpawnedMeshPreview;

        [Header("Orientation")]
        [Tooltip("親ローカルでのスポーン向き(Euler度)")]
        [SerializeField] private Vector3 spawnEulerAngles = Vector3.zero;

        [Tooltip("ONのとき境界ボックスからY-upへ自動補正するOFFならSpawn Euler Anglesのみ使う")]
        [SerializeField] private bool applyAutoOrientation;

        private ClayEditFbxVertexColorImporter importer;
        private ClayEditSessionContext sessionContext;
        private ClayHistoryManager historyManager;
        private IClayEditEntryView entryView;
        private IClayEditEditorUiGate editorUiGate;
        private SaveSlotView saveSlotView;
        private IClayInputProvider inputProvider;
        private IClayEditCameraView cameraView;
        private bool isSpawning;

        [Inject]
        public void Construct(
            ClayEditFbxVertexColorImporter importer,
            ClayEditSessionContext sessionContext,
            ClayHistoryManager historyManager,
            IClayEditEntryView entryView,
            IClayEditEditorUiGate editorUiGate,
            SaveSlotView saveSlotView,
            IClayInputProvider inputProvider,
            IClayEditCameraView cameraView)
        {
            this.importer = importer;
            this.sessionContext = sessionContext;
            this.historyManager = historyManager;
            this.entryView = entryView;
            this.editorUiGate = editorUiGate;
            this.saveSlotView = saveSlotView;
            this.inputProvider = inputProvider;
            this.cameraView = cameraView;
        }

        /// <summary>
        /// Inspector指定のFBXを頂点カラー変換してClayEditへスポーンする
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功した場合trueと空文字失敗時はfalseと理由</returns>
        public UniTask<(bool success, string errorMessage)> SpawnAsync(CancellationToken cancellationToken)
        {
            return SpawnFromModelAsync(fbxModel, cancellationToken);
        }

        /// <summary>
        /// 指定モデルを頂点カラー変換してClayEditへスポーンする
        /// </summary>
        /// <param name="modelAsset">FBXモデルまたはPrefab</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功した場合trueと空文字失敗時はfalseと理由</returns>
        public async UniTask<(bool success, string errorMessage)> SpawnFromModelAsync(
            GameObject modelAsset,
            CancellationToken cancellationToken)
        {
            if (isSpawning)
            {
                return (false, "変換処理中です");
            }

            if (!Application.isPlaying)
            {
                return (false, "Play Mode中のみ実行できます");
            }

            if (importer == null || sessionContext == null)
            {
                return (false, "VContainer注入が完了していません");
            }

            isSpawning = true;
            try
            {
                (bool success, string errorMessage) = await importer.TryImportAsync(
                    modelAsset,
                    spawnParent,
                    clayMaterial,
                    defaultVertexColor,
                    keepSpawnedMeshPreview,
                    colorSubdivisionDepth,
                    maxUvEdgeLength,
                    spawnEulerAngles,
                    applyAutoOrientation,
                    cancellationToken);
                if (!success)
                {
                    return (false, errorMessage);
                }

                BeginEditorSession();
                return (true, string.Empty);
            }
            finally
            {
                isSpawning = false;
            }
        }

        private void BeginEditorSession()
        {
            sessionContext.BeginNewCreate();
            historyManager?.Clear();
            entryView?.Hide();
            editorUiGate?.SetEditorVisible(true);
            saveSlotView?.ShowEditorChrome();
            inputProvider?.SetInputEnabled(true);
            cameraView?.SetCameraOperatable(true);
        }
#else
        private void Awake()
        {
            Destroy(this);
        }
#endif
    }
}
