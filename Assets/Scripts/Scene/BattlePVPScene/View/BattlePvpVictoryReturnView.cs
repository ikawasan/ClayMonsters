using Battle;
using Battle.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using System.Threading;
using UnityEngine;

namespace Scene.BattlePVPScene.View
{
    /// <summary>
    /// 通信対戦勝利後のタイトル戻りと再戦ボタンUI
    /// </summary>
    public sealed class BattlePvpVictoryReturnView : MonoBehaviour, IBattleDualVictoryReturnView
    {
        [Tooltip("ONのときフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private LHButton titleReturnButton;
        [SerializeField] private LHButton rematchButton;

        private void Awake()
        {
            ValidateSceneLayout();
            rematchButton?.EnsureUiSoundFeedback();
            titleReturnButton?.EnsureUiSoundFeedback();
            SetDualButtonsVisible(false);
        }

        /// <inheritdoc/>
        public void SetDualButtonsVisible(bool visible)
        {
            if (rootCanvas != null)
            {
                rootCanvas.enabled = visible;
            }

            if (titleReturnButton != null)
            {
                titleReturnButton.gameObject.SetActive(visible);
                titleReturnButton.interactable = visible;
            }

            if (rematchButton != null)
            {
                rematchButton.gameObject.SetActive(visible);
                rematchButton.interactable = visible;
            }
        }

        /// <inheritdoc/>
        public async UniTask<BattleVictoryReturnChoice> WaitVictoryReturnChoiceAsync(CancellationToken cancellationToken)
        {
            if (titleReturnButton == null && rematchButton == null)
            {
                await UniTask.WaitUntilCanceled(cancellationToken);
                return BattleVictoryReturnChoice.Title;
            }

            BattleVictoryReturnChoice choice = BattleVictoryReturnChoice.Title;
            bool decided = false;

            void OnTitleClick()
            {
                choice = BattleVictoryReturnChoice.Title;
                decided = true;
            }

            void OnRematchClick()
            {
                choice = BattleVictoryReturnChoice.Rematch;
                decided = true;
            }

            if (titleReturnButton != null)
            {
                titleReturnButton.onClick.AddListener(OnTitleClick);
            }

            if (rematchButton != null)
            {
                rematchButton.onClick.AddListener(OnRematchClick);
            }

            try
            {
                await UniTask.WaitUntil(() => decided, cancellationToken: cancellationToken);
                return choice;
            }
            finally
            {
                if (titleReturnButton != null)
                {
                    titleReturnButton.onClick.RemoveListener(OnTitleClick);
                }

                if (rematchButton != null)
                {
                    rematchButton.onClick.RemoveListener(OnRematchClick);
                }
            }
        }

        private void ValidateSceneLayout()
        {
            if (!useSceneCanvasLayout)
            {
                return;
            }

            if (rootCanvas == null || titleReturnButton == null || rematchButton == null)
            {
                Debug.LogError(
                    "[BattlePvpVictoryReturnView] シーン上のUI参照が未設定です。Tools/ClayMonsters/Migrate BattlePVP Auxiliary UIを実行してください",
                    this);
            }
        }
    }
}
