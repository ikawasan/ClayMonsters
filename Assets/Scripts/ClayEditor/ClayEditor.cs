using ClayEditor.Rigging;
using UnityEngine;
using VContainer;

namespace ClayEditor
{
    [RequireComponent(typeof(ClayVoxelEngine), typeof(ClayAutoRigger))]
    public class ClayEditor : MonoBehaviour
    {
        [Inject] private readonly ClayHistoryManager historyManager;
        [Inject] private readonly ClayVoxelEngine engine;
        [Inject] private readonly ClayEditorRangeVisualizer editorRangeVisualizer;

        [Header("Brush Settings")]
        [SerializeField] private float brushRadius = 2f;
        [SerializeField] private float brushStrength = 0.5f;
        [SerializeField] private float minBrushRadius = 0.1f;
        [SerializeField] private float maxBrushRadius = 20f;

        [Header("Performance")]
        [Tooltip("造形中にメッシュ表示を更新する間隔（フレーム数）大きいほど軽いが追従が粗くなる")]
        [SerializeField] private int shapeUpdateInterval = 3;

        // 造形中のメッシュ更新を間引くためのフレームカウンタ
        private int modifyFrameCounter;

        // 直近の造形でまだ表示へ反映していない変更があるか
        private bool pendingShapeUpdate;

        /// <summary>
        /// 現在のブラシ半径
        /// </summary>
        public float BrushRadius => brushRadius;

        /// <summary>
        /// レイキャスト深度と対象メッシュの基準Transform
        /// </summary>
        public Transform RaycastAnchor => engine.ClayModelTransform;

        void Start()
        {
            // ボーンは自動リギング（ClayAutoRigController）がアニメーションモード遷移時とエクスポート時に
            // 生成、設定するため、ここでのボーン構築は行わない
            editorRangeVisualizer.RefreshWireframe(engine.size, engine.Scale);
            engine.UpdateShapeFast(refreshColliders: true);
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
        /// ボクセル更新は毎フレーム行い、メッシュと当たり判定は shapeUpdateInterval フレームに1回へ間引く
        /// </summary>
        /// <param name="worldPos">造形する中心のワールド座標</param>
        /// <param name="isSubtract">true なら減算、false なら加算</param>
        public void ModifyAtWorldPosition(Vector3 worldPos, bool isSubtract)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            float strength = isSubtract ? -brushStrength : brushStrength;

            engine.Modify(localPos, brushRadius, strength);

            // 間引きながらメッシュ表示を更新する
            modifyFrameCounter++;
            int interval = Mathf.Max(shapeUpdateInterval, 1);
            if (modifyFrameCounter >= interval)
            {
                modifyFrameCounter = 0;
                pendingShapeUpdate = false;
                engine.UpdateShapeFast(refreshColliders: true);
            }
            else
            {
                pendingShapeUpdate = true;
            }
        }

        /// <summary>
        /// 造形ストロークの終了時などに呼び 間引きで未反映の最終形状を確実に表示へ反映する
        /// </summary>
        public void FlushShape()
        {
            modifyFrameCounter = 0;
            pendingShapeUpdate = false;
            engine.FlushShape();
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
                engine.FlushShape();
            }
        }

        /// <summary>
        /// 元に戻した操作をやり直す
        /// </summary>
        public void Redo()
        {
            if (historyManager.TryRedo(engine.GetVoxelData(), out float[] nextState))
            {
                engine.SetVoxelData(nextState);
                engine.FlushShape();
            }
        }

        /// <summary>
        /// 造形メッシュを全消去する
        /// </summary>
        public void ClearMesh()
        {
            engine.ClearAllVoxels();
            engine.FlushShape();
        }
    }
}