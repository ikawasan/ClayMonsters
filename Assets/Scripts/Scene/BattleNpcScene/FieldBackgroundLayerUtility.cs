using UnityEngine;

namespace Scene.BattleNpcScene
{
    /// <summary>
    /// 背景輪郭用にFieldルートをFieldレイヤーへ揃える
    /// </summary>
    public static class FieldBackgroundLayerUtility
    {
        public const string LayerName = "Field";

        /// <summary>
        /// 指定Field配下をFieldレイヤーへ設定する
        /// </summary>
        /// <param name="field">対象Field</param>
        public static void Apply(GameObject field)
        {
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogWarning("[FieldBackgroundLayerUtility] Fieldレイヤーが未定義です");
                return;
            }

            if (field == null)
            {
                return;
            }

            SetLayerRecursively(field.transform, layer);
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }
    }
}
