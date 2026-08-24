using System.Collections.Generic;
using Extensions;
using UI.Battle.Interface;
using LighthouseExtends.UIComponent.Button;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UI.Battle.View
{
    /// <summary>
    /// リアルタイム戦闘UIの実装自他のHP・ガッツ・間合い・残り時間・技スロットと最後に触れた技名・決着を表示する
    /// </summary>
    public class BattleView : MonoBehaviour, IBattleView, Localization.ILanguageAwareUi
    {
        [Header("技コマンド")]
        [Tooltip("攻撃ボタン(最大4。威力/属性/ガッツ/距離バーを持つ)")]
        [SerializeField] private MoveButtonView[] moveButtons;

        [Tooltip("プレイヤー側の攻撃名表示")]
        [SerializeField] private TMP_Text moveNameText;

        [Tooltip("敵側の攻撃名表示")]
        [SerializeField] private TMP_Text enemyMoveNameText;

        [Tooltip("必要部位(属性)ごとの画像")]
        [SerializeField] private TargetPartIconEntry[] targetPartIcons;

        [Header("敵の技")]
        [Tooltip("敵の技ボタン(最大4。表示のみ。威力/属性/ガッツ/距離バーを持つ)")]
        [SerializeField] private MoveButtonView[] enemyMoveButtons;

        [Header("プレイヤー")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private Image playerHpFill;
        [SerializeField] private TMP_Text playerHpText;
        [SerializeField] private Image playerGutsFill;
        [SerializeField] private TMP_Text playerGutsText;
        [SerializeField] private Slider playerGutsSlider;

        [Header("敵")]
        [SerializeField] private TMP_Text enemyNameText;
        [SerializeField] private Image enemyHpFill;
        [SerializeField] private TMP_Text enemyHpText;
        [SerializeField] private Image enemyGutsFill;
        [SerializeField] private TMP_Text enemyGutsText;
        [SerializeField] private Slider enemyGutsSlider;

        [Header("間合い・時間")]
        [Tooltip("間合いゲージ(Image Type=Filled)。0=密着で満タン、最大間合いで空に近づく")]
        [SerializeField] private Image distanceFill;
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text combatHintText;
        [SerializeField] private TMP_Text timeText;

        [Tooltip("残り時間が少ない時の文字色")]
        [SerializeField] private Color timeWarningColor = new Color(1f, 0.3f, 0.3f);

        [Tooltip("残り時間警告に入る秒数")]
        [SerializeField] private float timeWarningSeconds = 10f;

        [Header("見た目")]
        [SerializeField] private float gaugeSmoothSpeed = 14f;
        // 乗算でもFillのハイライトが残るよう明度高めの粘土色にする
        [SerializeField] private Color playerHpHealthyColor = new Color(0.62f, 0.9f, 0.58f, 1f);
        [SerializeField] private Color playerHpWarningColor = new Color(0.98f, 0.86f, 0.42f, 1f);
        [SerializeField] private Color playerHpCriticalColor = new Color(0.98f, 0.48f, 0.42f, 1f);
        [SerializeField] private Color enemyHpHealthyColor = new Color(0.98f, 0.58f, 0.52f, 1f);
        [SerializeField] private Color enemyHpWarningColor = new Color(0.98f, 0.72f, 0.4f, 1f);
        [SerializeField] private Color enemyHpCriticalColor = new Color(0.92f, 0.38f, 0.34f, 1f);
        [SerializeField] private Color playerGutsFillColor = new Color(0.55f, 0.86f, 1f, 1f);
        [SerializeField] private Color enemyGutsFillColor = new Color(1f, 0.7f, 0.48f, 1f);
        [SerializeField] private Color distanceCloseColor = new Color(1f, 0.68f, 0.48f, 1f);
        [SerializeField] private Color distanceMidColor = new Color(0.98f, 0.9f, 0.48f, 1f);
        [SerializeField] private Color distanceFarColor = new Color(0.58f, 0.88f, 1f, 1f);
        [SerializeField] private Color distanceDefaultColor = new Color(0.82f, 0.92f, 1f, 1f);
        [SerializeField] private Color combatHintActiveColor = new Color(1f, 0.92f, 0.45f, 1f);
        [SerializeField] private float timeWarningPulseSpeed = 6f;

        [Header("決着")]
        [Tooltip("決着時に非表示にする戦闘HUDのルート")]
        [SerializeField] private GameObject[] battleHudRoots;
        [SerializeField] private TMP_Text resultText;

        private readonly Subject<int> moveSelectedSubject = new Subject<int>();

        private readonly Dictionary<MoveTargetPartId, Sprite> targetPartMap = new Dictionary<MoveTargetPartId, Sprite>();
        private float currentMaxDistance = 10f;
        private Color timeNormalColor = Color.white;
        private float lastDisplayedDistance = float.NaN;
        private string lastDisplayedBandName;
        private int lastDisplayedTimeSeconds = int.MinValue;
        private int lastPlayerHpShown = int.MinValue;
        private int lastPlayerMaxHpShown = int.MinValue;
        private int lastEnemyHpShown = int.MinValue;
        private int lastEnemyMaxHpShown = int.MinValue;
        private int lastPlayerGutsFloorShown = int.MinValue;
        private int lastPlayerMaxGutsFloorShown = int.MinValue;
        private int lastEnemyGutsFloorShown = int.MinValue;
        private int lastEnemyMaxGutsFloorShown = int.MinValue;
        private float lastDisplayedDistanceRounded = float.NaN;
        private string lastDistanceTextBand;

        private float displayPlayerHpFill;
        private float displayEnemyHpFill;
        private float displayPlayerGutsFill;
        private float displayEnemyGutsFill;
        private float displayDistanceFill;
        private float targetPlayerHpFill;
        private float targetEnemyHpFill;
        private float targetPlayerGutsFill;
        private float targetEnemyGutsFill;
        private float targetDistanceFill;
        private string currentDistanceBandName = string.Empty;
        private float lastTimeRemaining = float.MaxValue;
        private bool hasResult;
        private string cachedResultWinnerName = string.Empty;
        private bool gaugesNeedUpdate = true;
        private Color lastPlayerHpFillColor;
        private Color lastEnemyHpFillColor;
        private Color lastPlayerGutsFillColor;
        private Color lastEnemyGutsFillColor;
        private Color lastDistanceFillColor;
        private bool hasLastPlayerHpFillColor;
        private bool hasLastEnemyHpFillColor;
        private bool hasLastPlayerGutsFillColor;
        private bool hasLastEnemyGutsFillColor;
        private bool hasLastDistanceFillColor;
        private const float GaugeSettleEpsilon = 0.0008f;

        // HP/コスト/間合いバーがSliderで作られている場合Sliderが長さを制御する
        private Slider playerHpSlider;
        private Slider enemyHpSlider;
        private Slider distanceSlider;

        /// <summary>
        /// 破壊対象部位と画像の対応(インスペクタ設定用)
        /// </summary>
        [System.Serializable]
        private struct TargetPartIconEntry
        {
            public MoveTargetPartId targetPartId;
            public Sprite sprite;
        }

        /// <inheritdoc />
        public Observable<int> OnMoveSelected => moveSelectedSubject;

        private string[] playerMoveNames;
        private string[] enemyMoveNames;
        private bool hasLoggedMissingEnemyMoveNameText;

        private void Awake()
        {
            playerMoveNames = new string[moveButtons != null ? moveButtons.Length : 0];
            enemyMoveNames = new string[enemyMoveButtons != null ? enemyMoveButtons.Length : 0];

            if (enemyMoveNameText == null)
            {
                Debug.LogError(
                    "[BattleView] enemyMoveNameTextが未配線ですPrefab Modeで敵側AttackNameを接続してください",
                    this);
                hasLoggedMissingEnemyMoveNameText = true;
            }
            BuildSpriteMaps();

            if (timeText != null)
            {
                timeNormalColor = timeText.color;
            }

            if (combatHintText != null)
            {
                InputIconTmpUtility.ApplySpriteAsset(combatHintText);
            }

            if (moveButtons != null)
            {
                for (int i = 0; i < moveButtons.Length; i++)
                {
                    int index = i;
                    if (moveButtons[index] != null && moveButtons[index].Button != null)
                    {
                        moveButtons[index].Button.SubscribeOnClick(() =>
                        {
                            ShowMoveName(true, playerMoveNames, index);
                            moveSelectedSubject.OnNext(index);
                        });
                        AddHoverHandler(
                            moveButtons[index].Button.gameObject,
                            () => ShowMoveName(true, playerMoveNames, index),
                            moveButtons[index]);
                    }
                }
            }

            if (enemyMoveButtons != null)
            {
                for (int i = 0; i < enemyMoveButtons.Length; i++)
                {
                    int index = i;
                    if (enemyMoveButtons[index] != null && enemyMoveButtons[index].Button != null)
                    {
                        AddHoverHandler(
                            enemyMoveButtons[index].Button.gameObject,
                            () => ShowMoveName(false, enemyMoveNames, index),
                            enemyMoveButtons[index]);
                    }
                }
            }

            if (resultText != null)
            {
                resultText.enabled = false;
                hasResult = false;
            }

            playerHpSlider = ResolveFillSlider(playerHpFill);
            enemyHpSlider = ResolveFillSlider(enemyHpFill);
            distanceSlider = ResolveFillSlider(distanceFill);
            ConfigureGaugeSlider(playerHpSlider);
            ConfigureGaugeSlider(enemyHpSlider);
            ConfigureGaugeSlider(playerGutsSlider);
            ConfigureGaugeSlider(enemyGutsSlider);
            ConfigureGaugeSlider(distanceSlider);

            BattleHudVisualUtility.ApplyValueOutline(playerHpText);
            BattleHudVisualUtility.ApplyValueOutline(enemyHpText);
            BattleHudVisualUtility.ApplyValueOutline(playerGutsText);
            BattleHudVisualUtility.ApplyValueOutline(enemyGutsText);

            displayPlayerHpFill = targetPlayerHpFill = 1f;
            displayEnemyHpFill = targetEnemyHpFill = 1f;
            displayPlayerGutsFill = targetPlayerGutsFill = 0f;
            displayEnemyGutsFill = targetEnemyGutsFill = 0f;
            displayDistanceFill = targetDistanceFill = 0f;
        }

        private void Update()
        {
            bool needsTimePulse = timeText != null && lastTimeRemaining <= timeWarningSeconds;
            bool gaugesSettled =
                !gaugesNeedUpdate
                && Mathf.Abs(displayPlayerHpFill - targetPlayerHpFill) <= GaugeSettleEpsilon
                && Mathf.Abs(displayEnemyHpFill - targetEnemyHpFill) <= GaugeSettleEpsilon
                && Mathf.Abs(displayPlayerGutsFill - targetPlayerGutsFill) <= GaugeSettleEpsilon
                && Mathf.Abs(displayEnemyGutsFill - targetEnemyGutsFill) <= GaugeSettleEpsilon
                && Mathf.Abs(displayDistanceFill - targetDistanceFill) <= GaugeSettleEpsilon;
            if (gaugesSettled && !needsTimePulse)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            bool anyMoved = SmoothToward(ref displayPlayerHpFill, targetPlayerHpFill, gaugeSmoothSpeed, deltaTime)
                | SmoothToward(ref displayEnemyHpFill, targetEnemyHpFill, gaugeSmoothSpeed, deltaTime)
                | SmoothToward(ref displayPlayerGutsFill, targetPlayerGutsFill, gaugeSmoothSpeed, deltaTime)
                | SmoothToward(ref displayEnemyGutsFill, targetEnemyGutsFill, gaugeSmoothSpeed, deltaTime)
                | SmoothToward(ref displayDistanceFill, targetDistanceFill, gaugeSmoothSpeed, deltaTime);

            if (anyMoved || gaugesNeedUpdate)
            {
                ApplyGaugeVisuals();
                gaugesNeedUpdate = false;
            }

            UpdateTimePulse();
        }

        private static bool SmoothToward(ref float current, float target, float speed, float deltaTime)
        {
            float next = BattleHudVisualUtility.SmoothFill(current, target, speed, deltaTime);
            if (Mathf.Abs(next - target) <= GaugeSettleEpsilon)
            {
                next = target;
            }

            if (Mathf.Abs(next - current) <= 1e-6f)
            {
                return false;
            }

            current = next;
            return true;
        }

        private void ApplyGaugeVisuals()
        {
            if (playerHpFill != null)
            {
                Color color = BattleHudVisualUtility.ResolveHpFillColor(
                    displayPlayerHpFill,
                    playerHpHealthyColor,
                    playerHpWarningColor,
                    playerHpCriticalColor);
                ApplyRatioFill(
                    playerHpSlider,
                    playerHpFill,
                    displayPlayerHpFill,
                    color,
                    ref lastPlayerHpFillColor,
                    ref hasLastPlayerHpFillColor);
            }

            if (enemyHpFill != null)
            {
                Color color = BattleHudVisualUtility.ResolveHpFillColor(
                    displayEnemyHpFill,
                    enemyHpHealthyColor,
                    enemyHpWarningColor,
                    enemyHpCriticalColor);
                ApplyRatioFill(
                    enemyHpSlider,
                    enemyHpFill,
                    displayEnemyHpFill,
                    color,
                    ref lastEnemyHpFillColor,
                    ref hasLastEnemyHpFillColor);
            }

            if (playerGutsFill != null)
            {
                ApplyRatioFill(
                    playerGutsSlider,
                    playerGutsFill,
                    displayPlayerGutsFill,
                    playerGutsFillColor,
                    ref lastPlayerGutsFillColor,
                    ref hasLastPlayerGutsFillColor);
            }

            if (enemyGutsFill != null)
            {
                ApplyRatioFill(
                    enemyGutsSlider,
                    enemyGutsFill,
                    displayEnemyGutsFill,
                    enemyGutsFillColor,
                    ref lastEnemyGutsFillColor,
                    ref hasLastEnemyGutsFillColor);
            }

            if (distanceFill != null)
            {
                Color color = BattleHudVisualUtility.ResolveDistanceFillColor(
                    currentDistanceBandName,
                    distanceCloseColor,
                    distanceMidColor,
                    distanceFarColor,
                    distanceDefaultColor);
                ApplyRatioFill(
                    distanceSlider,
                    distanceFill,
                    displayDistanceFill,
                    color,
                    ref lastDistanceFillColor,
                    ref hasLastDistanceFillColor);
            }
        }

        // Slider付きはvalueで長さを制御しImage Type=FilledのときだけfillAmountも同期する
        private static void ApplyRatioFill(
            Slider slider,
            Image fill,
            float ratio,
            Color color,
            ref Color lastColor,
            ref bool hasLastColor)
        {
            ratio = Mathf.Clamp01(ratio);
            if (slider != null)
            {
                slider.SetValueWithoutNotify(ratio);
            }

            if (fill.type == Image.Type.Filled)
            {
                fill.fillAmount = ratio;
            }

            if (!hasLastColor || lastColor != color)
            {
                BattleHudVisualUtility.SetImageColor(fill, color);
                lastColor = color;
                hasLastColor = true;
            }
        }

        private static void ConfigureGaugeSlider(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        // Fill画像を包むSliderを探す
        private static Slider ResolveFillSlider(Image fill)
        {
            if (fill == null)
            {
                return null;
            }

            return fill.GetComponentInParent<Slider>(true);
        }

        private void UpdateTimePulse()
        {
            if (timeText == null || lastTimeRemaining > timeWarningSeconds)
            {
                return;
            }

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * timeWarningPulseSpeed);
            Color color = Color.Lerp(timeWarningColor, Color.white, pulse * 0.35f);
            timeText.color = color;
        }

        // Resourcesとインスペクタ設定からスプライト対応表を作る
        private void BuildSpriteMaps()
        {
            targetPartMap.Clear();
            foreach (MoveTargetPartId targetPartId in System.Enum.GetValues(typeof(MoveTargetPartId)))
            {
                Sprite sprite = MoveCommandSpriteCatalog.LoadTargetPartIcon(targetPartId);
                if (sprite != null)
                {
                    targetPartMap[targetPartId] = sprite;
                }
            }

            if (targetPartIcons != null)
            {
                foreach (TargetPartIconEntry entry in targetPartIcons)
                {
                    if (entry.sprite != null)
                    {
                        targetPartMap[entry.targetPartId] = entry.sprite;
                    }
                }
            }
        }

        // 破壊対象部位から画像を引く
        private Sprite GetTargetPart(MoveTargetPartId targetPartId)
        {
            if (targetPartMap.TryGetValue(targetPartId, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            return MoveCommandSpriteCatalog.LoadTargetPartIcon(targetPartId);
        }

        private Color GetTargetPartColor(MoveTargetPartId targetPartId)
        {
            return MoveTargetPartSpriteFactory.GetDisplayColor(targetPartId);
        }

        /// <inheritdoc />
        public void SetAttackName(bool isPlayer, string attackName)
        {
            TMP_Text target = isPlayer ? moveNameText : enemyMoveNameText;
            if (target == null)
            {
                if (!isPlayer && !hasLoggedMissingEnemyMoveNameText)
                {
                    Debug.LogError(
                        "[BattleView] enemyMoveNameTextが未配線ですPrefab Modeで敵側AttackNameを接続してください",
                        this);
                    hasLoggedMissingEnemyMoveNameText = true;
                }

                return;
            }

            target.text = string.IsNullOrEmpty(attackName) ? string.Empty : attackName;
        }

        // カーソルが最後に当たった技名を固定欄に表示する
        private void ShowMoveName(bool isPlayer, string[] names, int index)
        {
            if (names == null || index < 0 || index >= names.Length)
            {
                return;
            }

            string moveName = names[index];
            if (!string.IsNullOrEmpty(moveName))
            {
                SetAttackName(isPlayer, moveName);
            }
        }

        // ホバー(PointerEnter)と選択(Select)で技名表示を呼ぶ
        private static void AddHoverHandler(GameObject target, System.Action onHover, MoveButtonView moveButton = null)
        {
            if (target == null)
            {
                return;
            }

            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = target.AddComponent<EventTrigger>();
            }

            AddHoverEntry(trigger, EventTriggerType.PointerEnter, onHover);
            if (moveButton != null)
            {
                AddHoverEntry(trigger, EventTriggerType.PointerEnter, () => moveButton.SetHighlighted(true));
                AddHoverEntry(trigger, EventTriggerType.PointerExit, () => moveButton.SetHighlighted(false));
            }
        }

        private static void AddHoverEntry(EventTrigger trigger, EventTriggerType type, System.Action onHover)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => onHover());
            trigger.triggers.Add(entry);
        }

        /// <inheritdoc />
        public void InitializeUnits(string playerName, string enemyName)
        {
            if (playerNameText != null) playerNameText.text = playerName;
            if (enemyNameText != null) enemyNameText.text = enemyName;
        }

        /// <inheritdoc />
        public void PrepareForBattleInput()
        {
            SetBattleHudActive(true);

            if (resultText != null)
            {
                resultText.enabled = false;
                hasResult = false;
            }

            Navigation navigation = new Navigation { mode = Navigation.Mode.None };
            DisableNavigation(moveButtons, navigation);

            if (playerGutsSlider != null)
            {
                playerGutsSlider.interactable = false;
            }

            if (enemyGutsSlider != null)
            {
                enemyGutsSlider.interactable = false;
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private static void DisableNavigation(MoveButtonView[] buttons, Navigation navigation)
        {
            if (buttons == null)
            {
                return;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].Button != null)
                {
                    buttons[i].Button.navigation = navigation;
                }
            }
        }

        /// <inheritdoc />
        public void SetHp(bool isPlayer, int currentHp, int maxHp)
        {
            float ratio = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;

            if (isPlayer)
            {
                if (!Mathf.Approximately(targetPlayerHpFill, ratio))
                {
                    targetPlayerHpFill = ratio;
                    gaugesNeedUpdate = true;
                }

                if (playerHpText != null
                    && (lastPlayerHpShown != currentHp || lastPlayerMaxHpShown != maxHp))
                {
                    lastPlayerHpShown = currentHp;
                    lastPlayerMaxHpShown = maxHp;
                    playerHpText.text = $"{currentHp}/{maxHp}";
                }

                return;
            }

            if (!Mathf.Approximately(targetEnemyHpFill, ratio))
            {
                targetEnemyHpFill = ratio;
                gaugesNeedUpdate = true;
            }

            if (enemyHpText != null
                && (lastEnemyHpShown != currentHp || lastEnemyMaxHpShown != maxHp))
            {
                lastEnemyHpShown = currentHp;
                lastEnemyMaxHpShown = maxHp;
                enemyHpText.text = $"{currentHp}/{maxHp}";
            }
        }

        /// <inheritdoc />
        public void SetGuts(bool isPlayer, float guts, float maxGuts)
        {
            float ratio = maxGuts > 0f ? Mathf.Clamp01(guts / maxGuts) : 0f;
            int gutsFloor = Mathf.FloorToInt(guts);
            int maxFloor = Mathf.FloorToInt(maxGuts);

            if (isPlayer)
            {
                if (!Mathf.Approximately(targetPlayerGutsFill, ratio))
                {
                    targetPlayerGutsFill = ratio;
                    gaugesNeedUpdate = true;
                }

                if (playerGutsText != null
                    && (lastPlayerGutsFloorShown != gutsFloor || lastPlayerMaxGutsFloorShown != maxFloor))
                {
                    lastPlayerGutsFloorShown = gutsFloor;
                    lastPlayerMaxGutsFloorShown = maxFloor;
                    playerGutsText.text = $"{gutsFloor}/{maxFloor}";
                }

                return;
            }

            if (!Mathf.Approximately(targetEnemyGutsFill, ratio))
            {
                targetEnemyGutsFill = ratio;
                gaugesNeedUpdate = true;
            }

            if (enemyGutsText != null
                && (lastEnemyGutsFloorShown != gutsFloor || lastEnemyMaxGutsFloorShown != maxFloor))
            {
                lastEnemyGutsFloorShown = gutsFloor;
                lastEnemyMaxGutsFloorShown = maxFloor;
                enemyGutsText.text = $"{gutsFloor}/{maxFloor}";
            }
        }

        /// <inheritdoc />
        public void SetDistance(float distance, float maxDistance, string bandName)
        {
            currentMaxDistance = maxDistance > 0f ? maxDistance : currentMaxDistance;
            string safeBandName = string.IsNullOrEmpty(bandName) ? string.Empty : bandName;
            float rounded = Mathf.Round(distance * 10f) * 0.1f;
            float nextFill = maxDistance > 0f ? Mathf.Clamp01(1f - rounded / maxDistance) : 0f;

            bool bandChanged = lastDisplayedBandName != safeBandName;
            bool valueChanged = !Mathf.Approximately(lastDisplayedDistanceRounded, rounded);
            if (bandChanged || valueChanged)
            {
                lastDisplayedDistance = rounded;
                lastDisplayedDistanceRounded = rounded;
                lastDisplayedBandName = safeBandName;
                currentDistanceBandName = safeBandName;
                if (!Mathf.Approximately(targetDistanceFill, nextFill) || bandChanged)
                {
                    targetDistanceFill = nextFill;
                    gaugesNeedUpdate = true;
                }
            }

            if (distanceText == null)
            {
                return;
            }

            // roundedは上で同期済みなので帯/数値の変化時だけテキストを張り直す
            if (lastDistanceTextBand == safeBandName && !valueChanged)
            {
                return;
            }

            lastDistanceTextBand = safeBandName;

            if (string.IsNullOrEmpty(safeBandName))
            {
                distanceText.text = Localization.LocalizedText.Get(
                    Localization.GameTextKeys.BattleDistance,
                    "distance",
                    rounded.ToString("0.0"));
            }
            else
            {
                distanceText.text = $"{safeBandName} {rounded:0.0}";
            }
        }

        /// <inheritdoc />
        public void SetCombatHint(string hint)
        {
            if (combatHintText == null)
            {
                return;
            }

            string next = hint ?? string.Empty;
            if (combatHintText.text == next)
            {
                return;
            }

            combatHintText.text = next;
            combatHintText.color = string.IsNullOrEmpty(next)
                ? new Color(1f, 1f, 1f, 0.65f)
                : combatHintActiveColor;
        }

        /// <inheritdoc />
        public void SetTimeRemaining(float seconds)
        {
            int displaySeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            if (displaySeconds == lastDisplayedTimeSeconds && timeText != null)
            {
                Color targetColor = seconds <= timeWarningSeconds ? timeWarningColor : timeNormalColor;
                if (timeText.color == targetColor)
                {
                    return;
                }
            }

            lastDisplayedTimeSeconds = displaySeconds;
            lastTimeRemaining = seconds;

            if (timeText != null)
            {
                timeText.text = displaySeconds.ToString();
                timeText.color = seconds <= timeWarningSeconds ? timeWarningColor : timeNormalColor;
            }
        }

        /// <inheritdoc />
        public void SetPlayerMoves(IReadOnlyList<MoveDisplay> moves)
        {
            if (moveButtons == null)
            {
                return;
            }

            for (int i = 0; i < moveButtons.Length; i++)
            {
                bool hasMove = moves != null && i < moves.Count;
                MoveButtonView moveButton = moveButtons[i];
                if (moveButton != null)
                {
                    if (moveButton.gameObject.activeSelf != hasMove)
                    {
                        moveButton.gameObject.SetActive(hasMove);
                    }

                    if (hasMove)
                    {
                        MoveDisplay move = moves[i];
                        moveButton.Apply(
                            move,
                            GetTargetPart(move.TargetPartId),
                            GetTargetPartColor(move.TargetPartId),
                            currentMaxDistance);
                    }
                }

                if (playerMoveNames != null && i < playerMoveNames.Length)
                {
                    playerMoveNames[i] = hasMove ? moves[i].Name : null;
                }
            }
        }

        /// <inheritdoc />
        public void SetEnemyMoves(IReadOnlyList<MoveDisplay> moves)
        {
            if (enemyMoveButtons == null)
            {
                return;
            }

            for (int i = 0; i < enemyMoveButtons.Length; i++)
            {
                bool hasMove = moves != null && i < moves.Count;
                MoveButtonView moveButton = enemyMoveButtons[i];
                if (moveButton != null)
                {
                    if (moveButton.gameObject.activeSelf != hasMove)
                    {
                        moveButton.gameObject.SetActive(hasMove);
                    }

                    if (hasMove)
                    {
                        MoveDisplay move = moves[i];
                        moveButton.Apply(
                            move,
                            GetTargetPart(move.TargetPartId),
                            GetTargetPartColor(move.TargetPartId),
                            currentMaxDistance);
                    }
                }

                if (enemyMoveNames != null && i < enemyMoveNames.Length)
                {
                    enemyMoveNames[i] = hasMove ? moves[i].Name : null;
                }
            }
        }

        /// <inheritdoc />
        public void SetPlayerMoveRecasts(IReadOnlyList<float> ready01ByIndex)
        {
            ApplyMoveRecasts(moveButtons, ready01ByIndex);
        }

        /// <inheritdoc />
        public void SetEnemyMoveRecasts(IReadOnlyList<float> ready01ByIndex)
        {
            ApplyMoveRecasts(enemyMoveButtons, ready01ByIndex);
        }

        private static void ApplyMoveRecasts(MoveButtonView[] buttons, IReadOnlyList<float> ready01ByIndex)
        {
            if (buttons == null || ready01ByIndex == null)
            {
                return;
            }

            int count = Mathf.Min(buttons.Length, ready01ByIndex.Count);
            for (int i = 0; i < count; i++)
            {
                MoveButtonView moveButton = buttons[i];
                if (moveButton == null || !moveButton.gameObject.activeSelf)
                {
                    continue;
                }

                moveButton.SetRecastReadyRatio(ready01ByIndex[i]);
            }
        }

        /// <inheritdoc />
        public void PrepareForResultDisplay()
        {
            DisableMoveButtonInput(moveButtons);
            DisableMoveButtonInput(enemyMoveButtons);
            SetBattleHudActive(false);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        /// <inheritdoc />
        public void ShowResult(string winnerName)
        {
            if (resultText == null)
            {
                return;
            }

            cachedResultWinnerName = winnerName;
            hasResult = true;
            ApplyResultText();
            resultText.enabled = true;
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            // 言語切替後もHP/ガッツの輪郭を再付与する
            BattleHudVisualUtility.ApplyValueOutline(playerHpText);
            BattleHudVisualUtility.ApplyValueOutline(enemyHpText);
            BattleHudVisualUtility.ApplyValueOutline(playerGutsText);
            BattleHudVisualUtility.ApplyValueOutline(enemyGutsText);

            // 間合い帯文言を次のSetDistanceで張り直す
            lastDisplayedBandName = null;
            lastDistanceTextBand = null;
            lastDisplayedDistance = float.NaN;
            lastDisplayedDistanceRounded = float.NaN;

            if (hasResult)
            {
                ApplyResultText();
            }
        }

        private void ApplyResultText()
        {
            if (resultText == null)
            {
                return;
            }

            string resultLabel = string.IsNullOrEmpty(cachedResultWinnerName)
                ? Localization.LocalizedText.Get(Localization.GameTextKeys.BattleDraw)
                : Localization.LocalizedText.Get(
                    Localization.GameTextKeys.BattleVictory,
                    "winnerName",
                    cachedResultWinnerName);
            Localization.LocalizedFont.SetText(resultText, resultLabel);
        }

        /// <inheritdoc />
        public void ShowPartBreakLockMessage(int moveIndex)
        {
            if (moveButtons == null || moveIndex < 0 || moveIndex >= moveButtons.Length)
            {
                return;
            }

            MoveButtonView moveButton = moveButtons[moveIndex];
            if (moveButton == null)
            {
                return;
            }

            moveButton.ShowPartLockMessage();
        }

        private void SetBattleHudActive(bool isActive)
        {
            if (battleHudRoots == null)
            {
                return;
            }

            for (int i = 0; i < battleHudRoots.Length; i++)
            {
                if (battleHudRoots[i] != null)
                {
                    battleHudRoots[i].SetActive(isActive);
                }
            }
        }

        private static void DisableMoveButtonInput(MoveButtonView[] buttons)
        {
            if (buttons == null)
            {
                return;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                MoveButtonView moveButton = buttons[i];
                if (moveButton == null || moveButton.Button == null)
                {
                    continue;
                }

                moveButton.Button.interactable = false;
                moveButton.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }

        private void OnDestroy()
        {
            moveSelectedSubject.Dispose();
        }
    }
}