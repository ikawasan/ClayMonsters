using Scene.BattlePVPScene.Interface;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.BattlePVPScene.View
{
    /// <summary>
    /// 相手入力待ち時に画面下部へメッセージを表示する
    /// </summary>
    public sealed class BattlePvpOpponentWaitView : MonoBehaviour, IBattlePvpOpponentWaitView
    {
        [Tooltip("ONのときフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private Image bannerBackground;
        [SerializeField] private TMP_Text messageText;

        private void Awake()
        {
            ValidateSceneLayout();
            SetVisible(false);
        }

        /// <inheritdoc/>
        public void SetVisible(bool visible)
        {
            if (rootCanvas != null)
            {
                if (visible)
                {
                    // SceneFade(32000)より前面に出し暗転下に埋もれないようにする
                    rootCanvas.overrideSorting = true;
                    rootCanvas.sortingOrder = 32500;
                }

                rootCanvas.enabled = visible;
            }
        }

        private void ValidateSceneLayout()
        {
            if (!useSceneCanvasLayout)
            {
                return;
            }

            if (rootCanvas == null || bannerBackground == null || messageText == null)
            {
                Debug.LogError(
                    "[BattlePvpOpponentWaitView] シーン上のUI参照が未設定です。HierarchyでUI参照を確認してください",
                    this);
            }
        }
    }
}
