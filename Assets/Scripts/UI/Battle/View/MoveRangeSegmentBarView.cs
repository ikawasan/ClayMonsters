using UnityEngine;
using UnityEngine.UI;

namespace UI.Battle.View
{
    /// <summary>
    /// 攻撃の有効距離をBattleNpcのRangeSegmentsと同じ見た目で表示する
    /// 0からmaxDistanceの軸で射程に重なるセグメントだけ有効色にする
    /// </summary>
    public sealed class MoveRangeSegmentBarView : MonoBehaviour
    {
        private const string PrefabResourcePath = "UI/MoveRangeSegmentBar";

        public const int SegmentCount = 3;
        public const float DefaultMaxDistance = 10f;
        // BattleDistanceBandResolverの近中境界と揃える
        private const float CloseMaxDistance = 4f;
        private const float MidMaxDistance = 7f;
        public const float BattleBarWidth = 64f;
        public const float BattleBarHeight = 10f;
        public const float BattleSegmentWidth = 20f;
        public const float BattleSegmentHeight = 6f;
        public const float BattleSegmentSpacing = 2f;
        public static readonly Vector2 BattleAnchoredPosition = new Vector2(0f, 8f);
        public const float CompactBarWidth = 78f;
        public const float CompactBarHeight = 12f;
        public const float CompactSegmentWidth = 22f;
        public const float CompactSegmentHeight = 10f;
        public const float CompactSegmentSpacing = 4f;

        public static readonly Color RangeInColor = new Color(0.30f, 0.82f, 0.34f, 1f);
        public static readonly Color RangeOutColor = new Color(0.42f, 0.45f, 0.50f, 1f);

        private static readonly Color DefaultActiveColor = RangeInColor;
        private static readonly Color DefaultInactiveColor = RangeOutColor;
        private static readonly Color DefaultOutOfBandColor = RangeOutColor;
        private static readonly string[] BattleSegmentNames =
        {
            "Image",
            "Image (1)",
            "Image (2)"
        };

        [SerializeField] private Image[] rangeSegments;

        private Sprite rangeActiveSprite;
        private Sprite rangeInactiveSprite;

        /// <summary>
        /// BattleNpcのRangeSegmentsと同じUIを親配下に生成する
        /// </summary>
        public static MoveRangeSegmentBarView CreateBattleUi(
            Transform parent,
            Vector2? anchoredPosition = null)
        {
            MoveRangeSegmentBarView view = TryInstantiatePrefab(parent, anchoredPosition);
            if (view == null)
            {
                view = CreateProgrammatic(parent, compact: false, anchoredPosition);
            }

            return view;
        }

        /// <summary>
        /// 一覧向けのコンパクトな射程バーを親配下に生成する
        /// 見た目はBattleNpcと同じセグメントを使う
        /// </summary>
        public static MoveRangeSegmentBarView CreateCompact(Transform parent)
        {
            MoveRangeSegmentBarView view = TryInstantiatePrefab(parent, null);
            if (view == null)
            {
                view = CreateProgrammatic(parent, compact: true, null);
            }

            if (view == null || view.transform is not RectTransform rectTransform)
            {
                return view;
            }

            rectTransform.sizeDelta = new Vector2(CompactBarWidth, CompactBarHeight);
            return view;
        }

        private static MoveRangeSegmentBarView TryInstantiatePrefab(
            Transform parent,
            Vector2? anchoredPosition)
        {
            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, parent, false);
            var prefabView = instance.GetComponent<MoveRangeSegmentBarView>();
            if (prefabView == null)
            {
                Destroy(instance);
                return null;
            }

            prefabView.EnsureSprites();
            if (anchoredPosition.HasValue && prefabView.transform is RectTransform rectTransform)
            {
                rectTransform.anchoredPosition = anchoredPosition.Value;
            }

            return prefabView;
        }

        private static MoveRangeSegmentBarView CreateProgrammatic(
            Transform parent,
            bool compact,
            Vector2? anchoredPosition)
        {
            if (parent == null)
            {
                return null;
            }

            var rootObject = new GameObject(
                "RangeSegments",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement),
                typeof(MoveRangeSegmentBarView));
            rootObject.transform.SetParent(parent, false);

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0.5f);
            rootRect.anchorMax = new Vector2(0f, 0.5f);
            rootRect.pivot = new Vector2(0f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            if (compact)
            {
                rootRect.sizeDelta = new Vector2(CompactBarWidth, CompactBarHeight);
            }
            else
            {
                rootRect.sizeDelta = new Vector2(BattleBarWidth, BattleBarHeight);
                if (anchoredPosition.HasValue)
                {
                    rootRect.anchoredPosition = anchoredPosition.Value;
                }
                else
                {
                    rootRect.anchoredPosition = BattleAnchoredPosition;
                }
            }

            LayoutElement rootLayout = rootObject.GetComponent<LayoutElement>();
            rootLayout.flexibleWidth = 0f;
            rootLayout.flexibleHeight = 0f;
            if (compact)
            {
                rootLayout.minWidth = CompactBarWidth;
                rootLayout.preferredWidth = CompactBarWidth;
                rootLayout.minHeight = CompactBarHeight;
                rootLayout.preferredHeight = CompactBarHeight;
            }
            else
            {
                rootLayout.minWidth = BattleBarWidth;
                rootLayout.preferredWidth = BattleBarWidth;
                rootLayout.minHeight = BattleBarHeight;
                rootLayout.preferredHeight = BattleBarHeight;
            }

            HorizontalLayoutGroup layoutGroup = rootObject.GetComponent<HorizontalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = !compact;
            layoutGroup.childForceExpandHeight = !compact;
            if (compact)
            {
                layoutGroup.spacing = CompactSegmentSpacing;
                layoutGroup.padding = new RectOffset(0, 0, 0, 0);
            }
            else
            {
                layoutGroup.spacing = BattleSegmentSpacing;
                layoutGroup.padding = new RectOffset(3, 0, 0, 0);
            }

            float segmentWidth = compact ? CompactSegmentWidth : BattleSegmentWidth;
            float segmentHeight = compact ? CompactSegmentHeight : BattleSegmentHeight;
            var segments = new Image[SegmentCount];
            for (int i = 0; i < SegmentCount; i++)
            {
                string segmentName = i < BattleSegmentNames.Length
                    ? BattleSegmentNames[i]
                    : "Image_" + i;
                var segmentObject = new GameObject(
                    segmentName,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(LayoutElement));
                segmentObject.transform.SetParent(rootObject.transform, false);

                RectTransform segmentRect = segmentObject.GetComponent<RectTransform>();
                segmentRect.anchorMin = new Vector2(0f, 0.5f);
                segmentRect.anchorMax = new Vector2(0f, 0.5f);
                segmentRect.pivot = new Vector2(0f, 0.5f);
                segmentRect.sizeDelta = new Vector2(segmentWidth, segmentHeight);

                LayoutElement segmentLayout = segmentObject.GetComponent<LayoutElement>();
                segmentLayout.minWidth = segmentWidth;
                segmentLayout.preferredWidth = segmentWidth;
                segmentLayout.minHeight = segmentHeight;
                segmentLayout.preferredHeight = segmentHeight;

                Image segmentImage = segmentObject.GetComponent<Image>();
                segmentImage.raycastTarget = false;
                segments[i] = segmentImage;
            }

            MoveRangeSegmentBarView view = rootObject.GetComponent<MoveRangeSegmentBarView>();
            view.Bind(segments);
            view.EnsureSprites();
            return view;
        }

        /// <summary>
        /// 既存の射程セグメント画像をこのViewへ紐付ける
        /// </summary>
        public void Bind(Image[] segments)
        {
            rangeSegments = NormalizeSegmentArray(segments, transform);
            EnsureSprites();
        }

        /// <summary>
        /// 既存の射程セグメント画像へ表示を反映する
        /// </summary>
        public static void ApplySegments(
            Image[] segments,
            float rangeMin,
            float rangeMax,
            float maxDistance,
            bool usable,
            Sprite activeSprite,
            Sprite inactiveSprite,
            Color activeColor,
            Color inactiveColor,
            Color outOfBandColor,
            bool preserveSegmentHierarchy = false)
        {
            _ = usable;
            _ = inactiveColor;
            _ = activeSprite;
            _ = inactiveSprite;

            Sprite segmentSprite = GetFallbackSegmentSprite();
            Color inColor = activeColor;
            Color outColor = outOfBandColor;

            for (int i = 0; i < SegmentCount; i++)
            {
                Image image = GetSegmentImage(segments, i);
                if (image == null)
                {
                    continue;
                }

                if (preserveSegmentHierarchy && !image.gameObject.activeSelf)
                {
                    continue;
                }

                if (!preserveSegmentHierarchy)
                {
                    image.gameObject.SetActive(true);
                }

                image.enabled = true;
                image.sprite = segmentSprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;

                float lower = GetSegmentLower(i, maxDistance);
                float upper = GetSegmentUpper(i, maxDistance);
                bool inRange = maxDistance > 0f && rangeMin < upper && rangeMax > lower;
                image.color = inRange ? inColor : outColor;
            }
        }

        private static float GetSegmentLower(int segmentIndex, float maxDistance)
        {
            if (maxDistance <= 0f || segmentIndex <= 0)
            {
                return 0f;
            }

            if (segmentIndex == 1)
            {
                return Mathf.Min(CloseMaxDistance, maxDistance);
            }

            float closeMax = Mathf.Min(CloseMaxDistance, maxDistance);
            float midMax = Mathf.Min(MidMaxDistance, maxDistance);
            return Mathf.Max(midMax, closeMax);
        }

        private static float GetSegmentUpper(int segmentIndex, float maxDistance)
        {
            if (maxDistance <= 0f)
            {
                return 0f;
            }

            if (segmentIndex <= 0)
            {
                return Mathf.Min(CloseMaxDistance, maxDistance);
            }

            if (segmentIndex == 1)
            {
                float closeMax = Mathf.Min(CloseMaxDistance, maxDistance);
                float midMax = Mathf.Min(MidMaxDistance, maxDistance);
                return Mathf.Max(midMax, closeMax);
            }

            return maxDistance;
        }

        /// <summary>
        /// 未設定セグメントへ既定スプライトを割り当てる
        /// </summary>
        public static void ApplyDefaultSprites(
            Image[] segments,
            ref Sprite activeSprite,
            ref Sprite inactiveSprite,
            bool preserveSegmentHierarchy = false)
        {
            EnsureCatalogSprites(ref activeSprite, ref inactiveSprite);

            if (segments == null)
            {
                return;
            }

            for (int i = 0; i < SegmentCount; i++)
            {
                Image image = GetSegmentImage(segments, i);
                if (image == null)
                {
                    continue;
                }

                if (preserveSegmentHierarchy && !image.gameObject.activeSelf)
                {
                    continue;
                }

                ConfigureSegmentImage(image, GetFallbackSegmentSprite());
                if (!preserveSegmentHierarchy)
                {
                    image.gameObject.SetActive(true);
                }

                image.enabled = true;
                image.color = DefaultOutOfBandColor;
            }
        }

        /// <summary>
        /// 射程に応じてセグメント色を更新する
        /// </summary>
        public void Apply(
            float rangeMin,
            float rangeMax,
            float maxDistance,
            bool usable = true,
            bool preserveSegmentHierarchy = false)
        {
            ApplyStyled(
                rangeMin,
                rangeMax,
                maxDistance,
                usable,
                DefaultActiveColor,
                DefaultInactiveColor,
                DefaultOutOfBandColor,
                preserveSegmentHierarchy);
        }

        /// <summary>
        /// MoveButtonViewと同じ色指定で射程表示を更新する
        /// </summary>
        public void ApplyStyled(
            float rangeMin,
            float rangeMax,
            float maxDistance,
            bool usable,
            Color activeColor,
            Color inactiveColor,
            Color outOfBandColor,
            bool preserveSegmentHierarchy = false)
        {
            EnsureCatalogSprites(ref rangeActiveSprite, ref rangeInactiveSprite);
            ApplySegments(
                rangeSegments,
                rangeMin,
                rangeMax,
                maxDistance,
                usable,
                rangeActiveSprite,
                rangeInactiveSprite,
                activeColor,
                inactiveColor,
                outOfBandColor,
                preserveSegmentHierarchy);
        }

        /// <summary>
        /// 射程セグメント用スプライトを読み込む
        /// </summary>
        public void EnsureSprites(bool preserveSegmentHierarchy = false)
        {
            EnsureCatalogSprites(ref rangeActiveSprite, ref rangeInactiveSprite);
            ApplyDefaultSprites(
                rangeSegments,
                ref rangeActiveSprite,
                ref rangeInactiveSprite,
                preserveSegmentHierarchy);
        }

        private void Awake()
        {
            rangeSegments = NormalizeSegmentArray(rangeSegments, transform);
            EnsureSprites(preserveSegmentHierarchy: ShouldPreserveSegmentHierarchy());
        }

        private bool ShouldPreserveSegmentHierarchy()
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.name.Contains("ModelSaveConfirmView")
                    || current.name == "ConfirmSlotRow"
                    || current.name == "ConfirmContent")
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static void EnsureCatalogSprites(ref Sprite activeSprite, ref Sprite inactiveSprite)
        {
            if (activeSprite == null)
            {
                activeSprite = MoveCommandSpriteCatalog.LoadRangeActive();
            }

            if (inactiveSprite == null)
            {
                inactiveSprite = MoveCommandSpriteCatalog.LoadRangeInactive();
            }
        }

        private static void ConfigureSegmentImage(Image image, Sprite inactiveSprite)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = inactiveSprite ?? GetFallbackSegmentSprite();
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
        }

        private static Image GetSegmentImage(Image[] segments, int index)
        {
            if (segments == null || index < 0 || index >= segments.Length)
            {
                return null;
            }

            return segments[index];
        }

        private static Image[] NormalizeSegmentArray(Image[] segments, Transform root)
        {
            if (segments != null && segments.Length >= SegmentCount)
            {
                var normalized = new Image[SegmentCount];
                for (int i = 0; i < SegmentCount; i++)
                {
                    normalized[i] = segments[i];
                }

                return normalized;
            }

            if (root == null)
            {
                return segments;
            }

            var collected = new Image[SegmentCount];
            int collectedCount = 0;
            for (int i = 0; i < root.childCount && collectedCount < SegmentCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                Image image = child.GetComponent<Image>();
                if (image == null)
                {
                    continue;
                }

                collected[collectedCount] = image;
                collectedCount++;
            }

            if (collectedCount >= SegmentCount)
            {
                return collected;
            }

            return segments;
        }

        private static Sprite fallbackSegmentSprite;

        private static Sprite GetFallbackSegmentSprite()
        {
            if (fallbackSegmentSprite != null)
            {
                return fallbackSegmentSprite;
            }

            fallbackSegmentSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f));
            return fallbackSegmentSprite;
        }
    }
}
