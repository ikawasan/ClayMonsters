namespace Scene.ClayEditScene.Interface
{
    /// <summary>
    /// ClayEditの編集UI表示を入場フロー完了まで制御する
    /// </summary>
    public interface IClayEditEditorUiGate
    {
        /// <summary>
        /// 編集UIの表示を切り替える
        /// </summary>
        /// <param name="isVisible">trueで表示</param>
        void SetEditorVisible(bool isVisible);
    }
}
