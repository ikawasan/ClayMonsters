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
        [Tooltip("薄いメッシュ時に表示メッシュを更新する間隔フレーム数")]
        [SerializeField] private int shapeUpdateInterval = 2;
        [Tooltip("厚いメッシュ時にブラシ周辺差し替えを行う間隔フレーム数")]
        [SerializeField] private int denseShapeUpdateInterval = 2;
        [Tooltip("コライダーを焼き直す間隔フレーム数")]
        [SerializeField] private int colliderUpdateInterval = 4;

        private int shapeFrameCounter;
        private int colliderFrameCounter;
        private Vector3 lastModifyLocalPos;
        private float lastModifyLocalRadius;
        private bool hasLastModify;

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
        /// 既存の厚いメッシュ上ではブラシ周辺だけ差し替えて更新する
        /// </summary>
        /// <param name="worldPos">造形する中心のワールド座標</param>
        /// <param name="isSubtract">true なら減算、false なら加算</param>
        public void ModifyAtWorldPosition(Vector3 worldPos, bool isSubtract)
        {
            Transform meshTransform = engine.ClayModelTransform;
            Vector3 localPos = meshTransform.InverseTransformPoint(worldPos);
            float modelScale = meshTransform.lossyScale.x;
            float localRadius = modelScale > 0f ? brushRadius / modelScale : brushRadius;
            float strength = isSubtract ? -brushStrength : brushStrength;

            engine.Modify(localPos, localRadius, strength);
            lastModifyLocalPos = localPos;
            lastModifyLocalRadius = localRadius;
            hasLastModify = true;

            if (engine.IsDenseSculptMesh)
            {
                // ピンポイント: チャンク全体再生成せずブラシ周辺だけ本メッシュへ差し替える
                shapeFrameCounter++;
                int denseInterval = Mathf.Max(denseShapeUpdateInterval, 1);
                if (shapeFrameCounter >= denseInterval)
                {
                    shapeFrameCounter = 0;
                    engine.UpdateShapeFastNearBrush(localPos, localRadius, refreshColliders: false);
                }
            }
            else
            {
                engine.ClearBrushSculptPreview();

                shapeFrameCounter++;
                int meshInterval = Mathf.Max(shapeUpdateInterval, 1);
                if (shapeFrameCounter >= meshInterval)
                {
                    shapeFrameCounter = 0;
                    engine.UpdateShapeFast(refreshColliders: false);
                }
            }

            colliderFrameCounter++;
            int colliderInterval = Mathf.Max(colliderUpdateInterval, 1);
            if (colliderFrameCounter >= colliderInterval)
            {
                colliderFrameCounter = 0;
                engine.FlushChunkColliders();
            }
        }

        /// <summary>
        /// 造形ストロークの終了時などに呼び 最終形状とコライダーを確定する
        /// </summary>
        public void FlushShape()
        {
            shapeFrameCounter = 0;
            colliderFrameCounter = 0;
            if (hasLastModify)
            {
                // 部分差し替えの継ぎ目をストローク終了時に本更新で解消する
                engine.MarkBrushChunksDirty(lastModifyLocalPos, lastModifyLocalRadius);
                hasLastModify = false;
            }

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
