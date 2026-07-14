using UI.Battle.Interface;
using LighthouseExtends.UIComponent.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Battle.View
{
    /// <summary>
    /// 1つの攻撃ボタンの表示部品威力・破壊部位画像・アイコン・必要ガッツ・有効距離セグメントを持つ
    /// </summary>
    public class MoveButtonView : MonoBehaviour
    {
        [Tooltip("クリック購読に使うボタン本体")]
        [SerializeField] private LHButton button;

        [Tooltip("威力の数値表示")]
        [SerializeField] private TMP_Text powerText;

        [Tooltip("破壊対象部位の画像")]
        [SerializeField] private Image attributeImage;

        [Tooltip("技アイコンの画像")]
        [SerializeField] private Image iconImage;

        [Tooltip("必要ガッツの数値表示")]
        [SerializeField] private TMP_Text gutsText;

        [Tooltip("命中率ラベル表示")]
        [SerializeField] private TMP_Text hitRateText;

        [Tooltip("有効距離を分割した距離セグメント画像(近・中・遠など)。射程に重なるものだけ有効化する)")]
        [SerializeField] private Image[] rangeSegments;

        [Header("見た目")]
        [SerializeField] private Image buttonBackground;
        [SerializeField] private Color usableBackgroundColor = new Color(0.18f, 0.24f, 0.34f, 0.92f);
        [SerializeField] private Color unusableBackgroundColor = new Color(0.12f, 0.13f, 0.16f, 0.72f);
        [SerializeField] private Color highlightBackgroundColor = new Color(0.28f, 0.36f, 0.5f, 0.98f);
        [SerializeField] private Color rangeActiveColor = MoveRangeSegmentBarView.RangeInColor;
        [SerializeField] private Color rangeInactiveColor = MoveRangeSegmentBarView.RangeOutColor;
        [SerializeField] private Color rangeOutOfBandColor = MoveRangeSegmentBarView.RangeOutColor;

        private bool isUsable;
        private bool isHighlighted;
        private Sprite rangeActiveSprite;
        private Sprite rangeInactiveSprite;
        private MoveRangeSegmentBarView rangeSegmentBarView;

        private void Awake()
        {
            if (buttonBackground == null && button != null)
            {
                buttonBackground = button.GetComponent<Image>();
            }

            DisableRaycastOnDecorations();
            EnsureRangeSegmentBarView();
            MoveRangeSegmentBarView.ApplyDefaultSprites(rangeSegments, ref rangeActiveSprite, ref rangeInactiveSprite);
        }

        /// <summary>
        /// 装飾用GraphicのRaycastを無効化しクリックをボタン本体へ通す
        /// PowerやCostの文字がボタンより手前にありクリックを遮るのを防ぐ
        /// </summary>
        private void DisableRaycastOnDecorations()
        {
            if (button == null)
            {
                return;
            }

            Graphic buttonGraphic = button.targetGraphic;
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic != null && graphic != buttonGraphic)
                {
                    graphic.raycastTarget = false;
                }
            }
        }

        private void EnsureRangeSegmentBarView()
        {
            if (rangeSegmentBarView != null)
            {
                return;
            }

            Transform rangeRoot = transform.Find("RangeSegments");
            if (rangeRoot == null && rangeSegments != null && rangeSegments.Length > 0 && rangeSegments[0] != null)
            {
                rangeRoot = rangeSegments[0].transform.parent;
            }

            if (rangeRoot == null)
            {
                return;
            }

            rangeSegmentBarView = rangeRoot.GetComponent<MoveRangeSegmentBarView>();
            if (rangeSegmentBarView == null)
            {
                rangeSegmentBarView = rangeRoot.gameObject.AddComponent<MoveRangeSegmentBarView>();
            }

            rangeSegmentBarView.Bind(rangeSegments);
        }

        /// <summary>
        /// 未設定の射程セグメントに既定スプライトを割り当てる
        /// </summary>
        public void ApplyDefaultRangeSprites()
        {
            MoveRangeSegmentBarView.ApplyDefaultSprites(rangeSegments, ref rangeActiveSprite, ref rangeInactiveSprite);
        }

        /// <summary>
        /// クリック購読やホバー対象に使うボタン本体
        /// </summary>
        public LHButton Button => button;

        /// <summary>
        /// ホバー強調のオンオフ
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            isHighlighted = highlighted;
            RefreshBackgroundColor();
        }

        /// <summary>
        /// 表示内容を更新する有効距離は0からmaxDistanceの軸で射程に重なるセグメントを有効化する
        /// </summary>
        public void Apply(
            in MoveDisplay move,
            Sprite icon,
            Sprite targetPartIcon,
            Color targetPartColor,
            float maxDistance)
        {
            if (powerText != null)
            {
                powerText.text = Mathf.RoundToInt(move.Power * 100f).ToString();
            }

            if (gutsText != null)
            {
                gutsText.text = Mathf.RoundToInt(move.GutsCost).ToString();
            }

            if (hitRateText != null)
            {
                hitRateText.text = string.IsNullOrEmpty(move.HitRateLabel) ? string.Empty : move.HitRateLabel;
            }

            SetSprite(iconImage, icon);
            ApplyTargetPartIcon(targetPartIcon, targetPartColor, move.TargetPartId);

            if (button != null)
            {
                button.interactable = move.Usable;
            }

            isUsable = move.Usable;
            SetVisualUsable(move.Usable);
            ApplyRangeSegments(move.RangeMin, move.RangeMax, maxDistance, move.Usable);
            RefreshBackgroundColor();
        }

        private void ApplyTargetPartIcon(Sprite sprite, Color color, MoveTargetPartId targetPartId)
        {
            if (attributeImage == null)
            {
                return;
            }

            if (targetPartId == MoveTargetPartId.None || sprite == null)
            {
                attributeImage.gameObject.SetActive(false);
                return;
            }

            attributeImage.gameObject.SetActive(true);
            attributeImage.sprite = sprite;
            attributeImage.color = color;
            attributeImage.preserveAspect = false;
            attributeImage.type = Image.Type.Simple;
        }

        private void SetVisualUsable(bool usable)
        {
            float alpha = usable ? 1f : 0.42f;
            SetImageAlpha(iconImage, alpha);
            SetImageAlpha(attributeImage, alpha);
            SetTextAlpha(powerText, alpha);
            SetTextAlpha(gutsText, alpha);
            SetTextAlpha(hitRateText, alpha);

            if (powerText != null)
            {
                powerText.color = usable
                    ? new Color(1f, 0.95f, 0.78f, alpha)
                    : new Color(0.72f, 0.74f, 0.78f, alpha);
            }

            if (gutsText != null)
            {
                gutsText.color = usable
                    ? new Color(0.72f, 0.9f, 1f, alpha)
                    : new Color(0.55f, 0.6f, 0.66f, alpha);
            }
        }

        private void RefreshBackgroundColor()
        {
            if (buttonBackground == null)
            {
                return;
            }

            Color target = unusableBackgroundColor;
            if (isUsable)
            {
                target = isHighlighted ? highlightBackgroundColor : usableBackgroundColor;
            }

            buttonBackground.color = target;
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static void SetTextAlpha(TMP_Text text, float alpha)
        {
            if (text == null)
            {
                return;
            }

            Color color = text.color;
            color.a = alpha;
            text.color = color;
        }

        private void ApplyRangeSegments(float rangeMin, float rangeMax, float maxDistance, bool usable)
        {
            EnsureRangeSegmentBarView();
            if (rangeSegmentBarView != null)
            {
                rangeSegmentBarView.ApplyStyled(
                    rangeMin,
                    rangeMax,
                    maxDistance,
                    usable,
                    rangeActiveColor,
                    rangeInactiveColor,
                    rangeOutOfBandColor);
                return;
            }

            MoveRangeSegmentBarView.ApplySegments(
                rangeSegments,
                rangeMin,
                rangeMax,
                maxDistance,
                usable,
                rangeActiveSprite,
                rangeInactiveSprite,
                rangeActiveColor,
                rangeInactiveColor,
                rangeOutOfBandColor);
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
