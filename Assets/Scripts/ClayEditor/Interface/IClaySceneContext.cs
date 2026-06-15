using GameData;
using R3;

namespace ClayEditor.Interface
{
    /// <summary>
    /// ClayEditシーンの編集状態を提供するインターフェース
    /// </summary>
    public interface IClaySceneContext
    {
        /// <summary>
        /// 現在の編集モード（粘土編集 / ペイント など）
        /// </summary>
        ReactiveProperty<EditModeType> CurrentMode { get; }

        /// <summary>
        /// 編集中のモデル
        /// </summary>
        ReactiveProperty<ClayModel> CurrentModel { get; }

        /// <summary>
        /// 編集モードを変更する
        /// </summary>
        /// <param name="newMode">新しい編集モード。</param>
        void ChangeMode(EditModeType newMode);

        /// <summary>
        /// 編集対象のモデルを差し替える
        /// </summary>
        /// <param name="model">新しく編集対象とするモデル</param>
        void SetModel(ClayModel model);
    }
}
