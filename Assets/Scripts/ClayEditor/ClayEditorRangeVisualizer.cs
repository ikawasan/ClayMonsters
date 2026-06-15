using UnityEngine;

namespace ClayEditor
{
    /// <summary>
    /// クレイエディターの有効範囲表示クラス
    /// </summary>
    public class ClayEditorRangeVisualizer : MonoBehaviour
    {
        public Color wireframeColor = Color.yellow;

        /// <summary>
        /// マーチングキューブの範囲枠を生成
        /// </summary>
        public void RefreshWireframe(int size, float scale)
        {
            Transform old = transform.Find("Wireframe_Root");
            if (old) DestroyImmediate(old.gameObject);

            GameObject wire = new GameObject("Wireframe_Root");
            wire.transform.SetParent(transform, false);
            LineRenderer lr = wire.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.startWidth = lr.endWidth = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = wireframeColor;
            lr.sortingOrder = -1;

            float s = (size * scale) * 0.5f;
            Vector3[] p = {
            new Vector3(-s, -s, -s), new Vector3(s, -s, -s), new Vector3(s, s, -s),
            new Vector3(-s, s, -s), new Vector3(-s, -s, -s), new Vector3(-s, -s, s),
            new Vector3(s, -s, s), new Vector3(s, s, s), new Vector3(-s, s, s),
            new Vector3(-s, -s, s), new Vector3(-s, s, s), new Vector3(-s, s, -s),
            new Vector3(s, s, -s), new Vector3(s, s, s), new Vector3(s, -s, s), new Vector3(s, -s, -s)
            };
            lr.positionCount = p.Length; lr.SetPositions(p);
        }
    }
}
