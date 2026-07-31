namespace UI.SkillTree.Interface
{
    /// <summary>
    /// スキルツリー画面のPresenter契約
    /// </summary>
    public interface ISkillTreePresenter
    {
        /// <summary>
        /// 初期購読を設定する
        /// </summary>
        void Setup();

        /// <summary>
        /// 画面を表示する
        /// </summary>
        void Show();

        /// <summary>
        /// 画面を非表示にする
        /// </summary>
        void Hide();
    }
}
