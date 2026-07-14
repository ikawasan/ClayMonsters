using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.BattlePVPScene.Interface;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.BattlePVPScene.View
{
    /// <summary>
    /// 通信切断時のメッセージウィンドウとタイトル戻りボタン
    /// </summary>
    public sealed class BattlePvpDisconnectView : MonoBehaviour, IBattlePvpDisconnectView
    {
        [Tooltip("ONのときフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private Image panelBackground;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private LHButton titleReturnButton;

        private void Awake()
        {
            ValidateSceneLayout();
            titleReturnButton?.EnsureUiSoundFeedback();
            SetVisible(false);
        }

        /// <inheritdoc/>
        public void SetVisible(bool visible)
        {
            if (rootCanvas != null)
            {
                rootCanvas.enabled = visible;
            }

            gameObject.SetActive(visible);
        }

        /// <inheritdoc/>
        public async UniTask WaitReturnToTitleAsync(CancellationToken cancellationToken)
        {
            if (titleReturnButton == null)
            {
                await UniTask.WaitUntilCanceled(cancellationToken);
                return;
            }

            bool decided = false;

            void OnClick()
            {
                decided = true;
            }

            titleReturnButton.onClick.AddListener(OnClick);
            try
            {
                await UniTask.WaitUntil(() => decided, cancellationToken: cancellationToken);
            }
            finally
            {
                titleReturnButton.onClick.RemoveListener(OnClick);
            }
        }

        private void ValidateSceneLayout()
        {
            if (!useSceneCanvasLayout)
            {
                return;
            }

            if (rootCanvas == null
                || panelBackground == null
                || messageText == null
                || titleReturnButton == null)
            {
                Debug.LogError(
                    "[BattlePvpDisconnectView] シーン上のUI参照が未設定です。Tools/ClayMonsters/Migrate BattlePVP Auxiliary UIを実行してください",
                    this);
            }
        }
    }
}
