using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using R3;
using Scene.ClayEditScene.Interface;
using UnityEngine;

namespace Scene.ClayEditScene.View
{
    /// <summary>
    /// ClayEdit入場時に新規作成と作り直しを選ばせるView
    /// </summary>
    public sealed class ClayEditEntryView : MonoBehaviour, IClayEditEntryView, ILanguageAwareUi
    {
        [Tooltip("ONのときフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [SerializeField] private Canvas entryCanvas;
        [SerializeField] private LHButton newCreateButton;
        [SerializeField] private LHButton remakeButton;

        private readonly Subject<Unit> newCreateSubject = new();
        private readonly Subject<Unit> remakeSubject = new();
        private LocalizedBakedTextApplier bakedLabelApplier;
        private string newCreateOriginal = "新規作成";
        private string remakeOriginal = "モンスターを作り直す";
        private bool sceneOriginalsCaptured;

        /// <inheritdoc />
        public Observable<Unit> OnNewCreateClicked => newCreateSubject;

        /// <inheritdoc />
        public Observable<Unit> OnRemakeClicked => remakeSubject;

        private void Awake()
        {
            Hide();
        }

        private void Start()
        {
            if (newCreateButton != null)
            {
                newCreateButton.SubscribeOnClick(() => newCreateSubject.OnNext(Unit.Default));
            }

            if (remakeButton != null)
            {
                remakeButton.SubscribeOnClick(() => remakeSubject.OnNext(Unit.Default));
            }

            CaptureSceneOriginals();
            ApplyLocalizedLabels();
            ValidateSceneLayout();
        }

        /// <inheritdoc />
        public void Show()
        {
            CaptureSceneOriginals();
            ApplyLocalizedLabels();
            if (entryCanvas != null)
            {
                entryCanvas.enabled = true;
            }

            gameObject.SetActive(true);
        }

        /// <inheritdoc />
        public void Hide()
        {
            if (entryCanvas != null)
            {
                entryCanvas.enabled = false;
            }
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
        }

        private void CaptureSceneOriginals()
        {
            if (sceneOriginalsCaptured)
            {
                return;
            }

            newCreateOriginal = SceneLocalizedLabel.Capture(newCreateButton, newCreateOriginal);
            remakeOriginal = SceneLocalizedLabel.Capture(remakeButton, remakeOriginal);
            sceneOriginalsCaptured = true;
        }

        private void ApplyLocalizedLabels()
        {
            CaptureSceneOriginals();
            if (bakedLabelApplier == null)
            {
                bakedLabelApplier = new LocalizedBakedTextApplier();
                // EntryPanel直下の未配線複製ボタン文言も同一原文で差し替える
                bakedLabelApplier.Register(GameTextKeys.TitleClayEdit, "エディット");
                bakedLabelApplier.Register(GameTextKeys.TitleClayEdit, "モンスターエディット");
                bakedLabelApplier.Register(GameTextKeys.ClayEditRemake, "作り直し");
                bakedLabelApplier.Register(GameTextKeys.ClayEditRemakeLong, remakeOriginal);
                bakedLabelApplier.Register(GameTextKeys.ClayEditNewCreate, "新規");
                bakedLabelApplier.Register(GameTextKeys.ClayEditNewCreate, newCreateOriginal);
                Transform root = entryCanvas != null ? entryCanvas.transform : transform;
                bakedLabelApplier.Capture(root);
            }

            bakedLabelApplier.Apply();
            LhButtonLabelUtility.SetLabel(
                newCreateButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.ClayEditNewCreate, newCreateOriginal));
            LhButtonLabelUtility.SetLabel(
                remakeButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.ClayEditRemakeLong, remakeOriginal));
        }

        private void ValidateSceneLayout()
        {
            if (!useSceneCanvasLayout)
            {
                return;
            }

            if (entryCanvas == null || newCreateButton == null || remakeButton == null)
            {
                Debug.LogError(
                    "[ClayEditEntryView] シーン上のUI参照が未設定です。HierarchyでUI参照を確認してください",
                    this);
            }
        }

        private void OnDestroy()
        {
            newCreateSubject.Dispose();
            remakeSubject.Dispose();
        }
    }
}
