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

        private readonly ReactiveProperty<bool> isGuideVisible = new(true);

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

        /// <inheritdoc />
        public void Initialize()
        {
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
            disposables.Dispose();
        }
    }
}