using System;
using UnityEngine.Events;

namespace Scene.TitleScene.Interface
{
    /// <summary>
    /// 未配線時のCPU戦モード選択UIダミー
    /// </summary>
    public sealed class NullTitleNpcBattleMenuView : ITitleNpcBattleMenuView
    {
        /// <inheritdoc/>
        public bool IsConfigured => false;

        /// <inheritdoc/>
        public void ShowModeSelect()
        {
        }

        /// <inheritdoc/>
        public void ShowDifficultySelect()
        {
        }

        /// <inheritdoc/>
        public void Hide()
        {
        }

        /// <inheritdoc/>
        public IDisposable SubscribeTournamentButtonClick(UnityAction action) => EmptyDisposable.Instance;

        /// <inheritdoc/>
        public IDisposable SubscribeFreeBattleButtonClick(UnityAction action) => EmptyDisposable.Instance;

        /// <inheritdoc/>
        public IDisposable SubscribeModeBackButtonClick(UnityAction action) => EmptyDisposable.Instance;

        /// <inheritdoc/>
        public IDisposable SubscribeEasyButtonClick(UnityAction action) => EmptyDisposable.Instance;

        /// <inheritdoc/>
        public IDisposable SubscribeNormalButtonClick(UnityAction action) => EmptyDisposable.Instance;

        /// <inheritdoc/>
        public IDisposable SubscribeHardButtonClick(UnityAction action) => EmptyDisposable.Instance;

        /// <inheritdoc/>
        public IDisposable SubscribeVeryHardButtonClick(UnityAction action) => EmptyDisposable.Instance;

        /// <inheritdoc/>
        public IDisposable SubscribeDifficultyBackButtonClick(UnityAction action) => EmptyDisposable.Instance;

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();

            public void Dispose()
            {
            }
        }
    }
}
