using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// トーナメントの参加者サムネスロット
    /// </summary>
    public sealed class NpcTournamentFighterSlotView : MonoBehaviour
    {
        private static readonly Color DefeatedDimmerColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color DefeatedThumbnailColor = new Color(0.45f, 0.45f, 0.45f, 1f);

        [SerializeField] private Image thumbnailImage;
        [SerializeField] private Image dimmerImage;
        [SerializeField] private Image outlineImage;
        [SerializeField] private Image maskImage;
        [SerializeField] private TMP_Text nameText;

        /// <summary>
        /// スロットRect
        /// </summary>
        public RectTransform RectTransform => transform as RectTransform;

        /// <summary>
        /// 現在のサムネスプライト
        /// </summary>
        public Sprite ThumbnailSprite => thumbnailImage != null ? thumbnailImage.sprite : null;

        private void Awake()
        {
            if (thumbnailImage == null || dimmerImage == null)
            {
                Debug.LogError(
                    "[NpcTournamentFighterSlotView] thumbnailImageかdimmerImageが未配線です",
                    this);
            }

            if (outlineImage == null)
            {
                Debug.LogError(
                    "[NpcTournamentFighterSlotView] outlineImageが未配線です",
                    this);
            }

            if (maskImage == null)
            {
                Debug.LogError(
                    "[NpcTournamentFighterSlotView] maskImageが未配線です",
                    this);
            }

            // 未設定スロットは枠もマスク絵も出さない
            ApplyContentVisuals(thumbnailImage != null ? thumbnailImage.sprite : null, thumbnailVisible: false);
            SetDefeated(false);
        }

        /// <summary>
        /// サムネと名前を設定する
        /// </summary>
        /// <param name="sprite">サムネ</param>
        /// <param name="displayName">名前</param>
        public void SetContent(Sprite sprite, string displayName)
        {
            if (nameText != null)
            {
                nameText.text = displayName ?? string.Empty;
            }

            ApplyContentVisuals(sprite, thumbnailVisible: sprite != null);
            SetDefeated(false);
        }

        /// <summary>
        /// 別スロットの内容をコピーする
        /// </summary>
        /// <param name="source">元</param>
        public void CopyFrom(NpcTournamentFighterSlotView source)
        {
            if (source == null)
            {
                return;
            }

            if (nameText != null && source.nameText != null)
            {
                nameText.text = source.nameText.text;
            }

            ApplyContentVisuals(source.ThumbnailSprite, thumbnailVisible: source.ThumbnailSprite != null);
            SetDefeated(false);
        }

        /// <summary>
        /// サムネと枠をセットで表示切替する
        /// 枠だけ残すと白四角の謎画像になるため同時に切り替える
        /// </summary>
        /// <param name="visible">表示するか</param>
        public void SetThumbnailVisible(bool visible)
        {
            bool show = visible && thumbnailImage != null && thumbnailImage.sprite != null;
            if (thumbnailImage != null)
            {
                thumbnailImage.enabled = show;
            }

            if (outlineImage != null)
            {
                outlineImage.enabled = show;
                outlineImage.raycastTarget = false;
            }

            if (maskImage != null)
            {
                maskImage.enabled = show;
                maskImage.raycastTarget = false;
            }
        }

        /// <summary>
        /// 敗北で暗くする
        /// </summary>
        /// <param name="defeated">敗北か</param>
        public void SetDefeated(bool defeated)
        {
            if (dimmerImage != null)
            {
                dimmerImage.enabled = defeated;
                dimmerImage.color = DefeatedDimmerColor;
                dimmerImage.raycastTarget = false;
            }

            if (thumbnailImage != null && !defeated)
            {
                thumbnailImage.color = Color.white;
            }
            else if (thumbnailImage != null && defeated)
            {
                thumbnailImage.color = DefeatedThumbnailColor;
            }
        }

        /// <summary>
        /// 敗北暗転をゆっくり再生する
        /// </summary>
        /// <param name="durationSeconds">秒</param>
        /// <param name="cancellationToken">キャンセル</param>
        public async UniTask AnimateDarkenAsync(
            float durationSeconds,
            CancellationToken cancellationToken)
        {
            if (dimmerImage != null)
            {
                dimmerImage.enabled = true;
                dimmerImage.raycastTarget = false;
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, durationSeconds);
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (dimmerImage != null)
                {
                    Color dimmer = DefeatedDimmerColor;
                    dimmer.a = DefeatedDimmerColor.a * t;
                    dimmerImage.color = dimmer;
                }

                if (thumbnailImage != null)
                {
                    thumbnailImage.color = Color.Lerp(Color.white, DefeatedThumbnailColor, t);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            SetDefeated(true);
        }

        private void ApplyContentVisuals(Sprite sprite, bool thumbnailVisible)
        {
            bool show = thumbnailVisible && sprite != null;
            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = sprite;
                thumbnailImage.enabled = show;
                thumbnailImage.color = Color.white;
                thumbnailImage.preserveAspect = false;
                thumbnailImage.type = Image.Type.Simple;
                thumbnailImage.maskable = true;
            }

            if (outlineImage != null)
            {
                // サムネと枠はセット空き時の白枠ゴースト防止
                outlineImage.enabled = show;
                outlineImage.raycastTarget = false;
            }

            if (maskImage != null)
            {
                // マスク形状用Imageは中身表示時だけ有効
                maskImage.enabled = show;
                maskImage.raycastTarget = false;
            }
        }
    }
}
