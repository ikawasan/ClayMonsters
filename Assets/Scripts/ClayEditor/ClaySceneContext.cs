using ClayEditor.Interface;
using GameData;
using R3;
using System;

namespace ClayEditor
{
    /// <summary>
    /// 粘土編集シーンの実行時状態（編集モード / 選択モデル）を保持する
    /// </summary>
    public class ClaySceneContext : IClaySceneContext, IDisposable
    {
        /// <summary>
        /// 現在の編集モード（粘土編集 / ペイント など）
        /// </summary>
        public ReactiveProperty<EditModeType> CurrentMode { get; } = new(EditModeType.Clay);

        /// <summary>
        /// 現在選択・編集中のモデル
        /// 未選択時は Value が null
        /// </summary>
        public ReactiveProperty<ClayModel> CurrentModel { get; } = new(null);

        /// <inheritdoc />
        public void ChangeMode(EditModeType newMode)
        {
            CurrentMode.Value = newMode;
        }

        /// <inheritdoc />
        public void SetModel(ClayModel model)
        {
            CurrentModel.Value = model;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            CurrentMode.Dispose();
            CurrentModel.Dispose();
        }
    }
}
