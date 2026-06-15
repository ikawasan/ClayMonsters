using UnityEngine;
using VContainer;

namespace ClayEditor.Paint
{
    /// <summary>
    /// 頂点カラーペイントクラス
    /// </summary>
    public class ClayPainter : MonoBehaviour
    {
        [Inject] private readonly ClayVoxelEngine engine;

        [Header("Paint Settings")]
        [SerializeField] private float brushRadius = 2f;
        [SerializeField] private float minBrushRadius = 0.1f;
        [SerializeField] private float maxBrushRadius = 20f;
        [SerializeField] private Color initialColor = Color.red;

        private Color currentColor;

        /// <summary>
        /// 現在のペイントブラシ半径
        /// </summary>
        public float BrushRadius => brushRadius;

        /// <summary>
        /// 現在のペイント色
        /// </summary>
        public Color CurrentColor => currentColor;

        private void Awake()
        {
            currentColor = initialColor;
        }

        /// <summary>
        /// ペイントに使用する色を設定する
        /// </summary>
        /// <param name="color">ペイントカラー</param>
        public void SetColor(Color color)
        {
            currentColor = color;
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
        /// ワールド座標を中心に現在の色でメッシュの頂点カラーを塗る
        /// </summary>
        /// <param name="worldPos">塗りの中心となるワールド座標</param>
        public void PaintAtWorldPosition(Vector3 worldPos)
        {
            engine.PaintVertices(worldPos, brushRadius, currentColor);
        }
    }
}

