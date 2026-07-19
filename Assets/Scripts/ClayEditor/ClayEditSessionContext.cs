namespace ClayEditor
{
    /// <summary>
    /// ClayEditシーン入場時の作成モードと作り直し対象スロットを保持する
    /// </summary>
    public sealed class ClayEditSessionContext
    {
        /// <summary>
        /// 入場フローが完了し編集UIを表示してよいか
        /// </summary>
        public bool IsEditorReady { get; private set; }

        /// <summary>
        /// 新規作成モードか
        /// </summary>
        public bool IsNewCreate => StartMode == ClayEditStartMode.NewCreate;

        /// <summary>
        /// 作り直しモードか
        /// </summary>
        public bool IsRemake => StartMode == ClayEditStartMode.Remake;

        /// <summary>
        /// 入場時の開始モード
        /// </summary>
        public ClayEditStartMode StartMode { get; private set; } = ClayEditStartMode.Pending;

        /// <summary>
        /// 作り直し対象のプレイヤースロット番号
        /// </summary>
        public int RemakeSlotIndex { get; private set; } = -1;

        /// <summary>
        /// 作り直し対象のモデル名
        /// </summary>
        public string RemakeModelName { get; private set; } = string.Empty;

        /// <summary>
        /// 新規作成フローを開始する
        /// </summary>
        public void BeginNewCreate()
        {
            StartMode = ClayEditStartMode.NewCreate;
            RemakeSlotIndex = -1;
            RemakeModelName = string.Empty;
            IsEditorReady = true;
        }

        /// <summary>
        /// 作り直しフローを開始する
        /// </summary>
        /// <param name="slotIndex">対象スロット番号</param>
        /// <param name="modelName">対象モデル名</param>
        public void BeginRemake(int slotIndex, string modelName)
        {
            StartMode = ClayEditStartMode.Remake;
            RemakeSlotIndex = slotIndex;
            RemakeModelName = modelName ?? string.Empty;
            IsEditorReady = true;
        }

        /// <summary>
        /// セッション状態を初期化する
        /// </summary>
        public void Reset()
        {
            StartMode = ClayEditStartMode.Pending;
            RemakeSlotIndex = -1;
            RemakeModelName = string.Empty;
            IsEditorReady = false;
        }
    }

    /// <summary>
    /// ClayEdit入場時の開始モード
    /// </summary>
    public enum ClayEditStartMode
    {
        Pending,
        NewCreate,
        Remake
    }
}
