using ClayEditor;
using ClayEditor.Interface;
using ClayEditor.Rigging;
using GameData;
using R3;
using System;
using VContainer;
using VContainer.Unity;

namespace UI.ClayEditor.ViewModel
{
    public sealed class ClayEditModeViewModel : IInitializable, IDisposable
    {
        private readonly IClaySceneContext context;
        private readonly ClayAutoRigController autoRigController;
        private readonly ClayVoxelEngine engine;
        private readonly CompositeDisposable disposables = new();

        /// <summary>
        /// 現在の編集モード
        /// </summary>
        public ReadOnlyReactiveProperty<EditModeType> CurrentMode => context.CurrentMode;

        /// <summary>
        /// 編集対象のモデルが選択されているか
        /// </summary>
        public ReadOnlyReactiveProperty<bool> HasModel { get; }

        /// <summary>
        /// 操作説明テキストを表示するか
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsGuideVisible => isGuideVisible;

        /// <summary>
        /// 成形ペイント中に編集UIを隠すか
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsEditorUiHidden => isEditorUiHidden;

        private readonly ReactiveProperty<bool> isGuideVisible = new(false);
        private readonly ReactiveProperty<bool> isEditorUiHidden = new(false);

        [Inject]
        public ClayEditModeViewModel(IClaySceneContext context, ClayAutoRigController autoRigController, ClayVoxelEngine engine)
        {
            this.context = context;
            this.autoRigController = autoRigController;
            this.engine = engine;

            HasModel = context.CurrentModel
                .Select(model => model != null)
                .ToReadOnlyReactiveProperty()
                .AddTo(disposables);
        }

        /// <summary>
        /// 操作説明テキストの表示状態を変更する
        /// </summary>
        /// <param name="visible">表示する場合true</param>
        public void SetGuideVisible(bool visible)
        {
            isGuideVisible.Value = visible;
        }

        /// <summary>
        /// 編集UIの非表示状態を変更する
        /// </summary>
        /// <param name="hidden">非表示にする場合true</param>
        public void SetEditorUiHidden(bool hidden)
        {
            isEditorUiHidden.Value = hidden;
        }

        /// <summary>
        /// 入場退出時に操作説明とUI非表示フラグを初期化する
        /// </summary>
        public void ResetUiVisibilityFlags()
        {
            isGuideVisible.Value = false;
            isEditorUiHidden.Value = false;
        }

        /// <inheritdoc />
        public void Initialize()
        {
            ResetUiVisibilityFlags();

            // モード遷移に応じて表示するメッシュを切り替える
            context.CurrentMode
                .Subscribe(OnModeChanged)
                .AddTo(disposables);
        }

        /// <summary>
        /// 編集モードを変更する
        /// </summary>
        /// <param name="mode">新しい編集モード</param>
        public void ChangeMode(EditModeType mode)
        {
            context.ChangeMode(mode);
        }

        private void OnModeChanged(EditModeType mode)
        {
            autoRigController.RestoreSculptEditState(mode);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            isGuideVisible.Dispose();
            isEditorUiHidden.Dispose();
            disposables.Dispose();
        }
    }
}