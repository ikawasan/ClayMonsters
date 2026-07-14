namespace UI.ClayEditor.View
{
    /// <summary>
    /// ClayEdit作り直しで選択されたスロット情報
    /// </summary>
    public readonly struct ClayEditRemakeSelection
    {
        /// <summary>
        /// 選択されたスロット番号
        /// </summary>
        public int SlotIndex { get; }

        /// <summary>
        /// 選択されたモデル名
        /// </summary>
        public string ModelName { get; }

        /// <summary>
        /// 作り直し選択情報を生成する
        /// </summary>
        /// <param name="slotIndex">スロット番号</param>
        /// <param name="modelName">モデル名</param>
        public ClayEditRemakeSelection(int slotIndex, string modelName)
        {
            SlotIndex = slotIndex;
            ModelName = modelName ?? string.Empty;
        }
    }
}
