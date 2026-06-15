using UnityEngine;
using VContainer;

namespace ClayEditor
{
    [RequireComponent(typeof(ClayVoxelEngine), typeof(ClayAutoRigger))]
    public class ClayEditor : MonoBehaviour
    {
        [Inject] private readonly ClayHistoryManager historyManager;
        [Inject] private readonly ClayVoxelEngine engine;
        [Inject] private readonly ClayAutoRigger rigger;
        [Inject] private readonly ClayEditorRangeVisualizer editorRangeVisualizer;

        [Header("Brush Settings")]
        [SerializeField] private float brushRadius = 2f;
        [SerializeField] private float brushStrength = 0.5f;
        [SerializeField] private float minBrushRadius = 0.1f;
        [SerializeField] private float maxBrushRadius = 20f;

        /// <summary>
        /// 現在のブラシ半径
        /// </summary>
        public float BrushRadius => brushRadius;

        void Start()
        {
            // モデル依存のセットアップ（ボーン構築・可視化・初回メッシュ生成）を行う
            if (rigger.Bones == null || rigger.Bones.Length == 0)
            {
                rigger.AutoSetup();
            }

            editorRangeVisualizer.RefreshWireframe(engine.size, engine.Scale);
            UpdateAll();
        }

        /// <summary>
        /// ブラシ半径を相対的に変更する
        /// </summary>
        /// <param name="delta">加算する半径量</param>
        public void ChangeBrushRadius(float delta)
        {
            brushRadius = Mathf.Clamp(brushRadius + delta, minBrushRadius, maxBrushRadius);
        }

        /// <summary>
        /// ワールド座標を中心にボクセルを加算 / 減算して造形する
        /// </summary>
        /// <param name="worldPos">造形する中心のワールド座標</param>
        /// <param name="isSubtract">trueなら削る、falseなら盛る</param>
        public void ModifyAtWorldPosition(Vector3 worldPos, bool isSubtract)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            float strength = isSubtract ? -brushStrength : brushStrength;

            engine.Modify(localPos, brushRadius, strength);
            UpdateAll();
        }

        /// <summary>
        /// 現在のボクセル状態を Undo 履歴に保存する
        /// </summary>
        public void SaveState()
        {
            historyManager.SaveState(engine.GetVoxelData());
        }

        /// <summary>
        /// 直前の状態へ戻す
        /// </summary>
        public void Undo()
        {
            if (historyManager.TryUndo(engine.GetVoxelData(), out float[] prevState))
            {
                engine.SetVoxelData(prevState);
                UpdateAll();
            }
        }

        /// <summary>
        /// 取り消した操作をやり直す
        /// </summary>
        public void Redo()
        {
            if (historyManager.TryRedo(engine.GetVoxelData(), out float[] nextState))
            {
                engine.SetVoxelData(nextState);
                UpdateAll();
            }
        }

        /// <summary>
        /// 造形メッシュを全消去する
        /// </summary>
        public void ClearMesh()
        {
            engine.ClearAllVoxels();
            UpdateAll();
        }

        private void UpdateAll()
        {
            var data = engine.GenerateMeshData();
            if (data.vertices == null || data.vertices.Length == 0)
            {
                var emptyData = new ClayVoxelEngine.MeshData
                {
                    vertices = new Vector3[0],
                    normals = new Vector3[0],
                    indices = new int[0]
                };
                engine.ApplyToRenderer(emptyData, new BoneWeight[0], rigger.BindPoses, rigger.Bones);
                return;
            }

            var weights = rigger.Calculate(data.vertices, transform);
            engine.ApplyToRenderer(data, weights, rigger.BindPoses, rigger.Bones);
        }
    }
}
