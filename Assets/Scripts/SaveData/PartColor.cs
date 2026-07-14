using ClayEditor.Rigging;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// 保存用の部位と色の対応DictionaryはシリアライズできないためListで保持する
    /// </summary>
    [System.Serializable]
    public struct PartColor
    {
        public BonePart part;
        public Color color;
    }
}