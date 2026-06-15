using ClayEditor;
using ClayEditor.Interface;
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
        /// 粘土モード用の説明テキストを表示するか
        /// </summary>
        public ReadOnlyReactiveProperty<bool> ShowClayDescriptor { get; }

        [Inject]
        public ClayEditModeViewModel(IClaySceneContext context)
        {
            this.context = context;

            HasModel = context.CurrentModel
                .Select(model => model != null)
                .ToReadOnlyReactiveProperty()
                .AddTo(disposables);

            ShowClayDescriptor = Observable.CombineLatest(
                    context.CurrentMode,
                    context.CurrentModel,
                    (mode, model) => mode == EditModeType.Clay && model != null)
                .ToReadOnlyReactiveProperty()
                .AddTo(disposables);
        }

        /// <inheritdoc />
        public void Initialize()
        {
            // モード or モデルが変わったらモデルの Animator に対する操作を行う
            Observable.CombineLatest(
                    context.CurrentMode,
                    context.CurrentModel,
                    (mode, model) => (mode, model))
                .Where(state => state.model != null)
                .Subscribe(state => ApplyModeToModel(state.mode, state.model))
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

        private static void ApplyModeToModel(EditModeType mode, ClayModel model)
        {
            if (model.Animator == null)
            {
                return;
            }

            switch (mode)
            {
                case EditModeType.Paint:
                    model.Animator.SetTrigger("TPose");
                    break;
                case EditModeType.Animation:
                    model.Animator.SetTrigger("Idle");
                    break;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
