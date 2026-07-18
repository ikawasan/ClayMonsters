using Extensions;
using LighthouseExtends.UIComponent.Button;
using R3;
using Scene.ClayEditScene.Interface;
using UnityEngine;

namespace Scene.ClayEditScene.View
{
    /// <summary>
    /// ClayEdit入場時に新規作成と作り直しを選ばせるView
    /// </summary>
    public sealed class ClayEditEntryView : MonoBehaviour, IClayEditEntryView
    {
        [Tooltip("ONのときフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [SerializeField] private Canvas entryCanvas;
        [SerializeField] private LHButton newCreateButton;
        [SerializeField] private LHButton remakeButton;

        private readonly Subject<Unit> newCreateSubject = new();
        private readonly Subject<Unit> remakeSubject = new();

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

            ValidateSceneLayout();
        }

        /// <inheritdoc />
        public void Show()
        {
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
