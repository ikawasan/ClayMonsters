using ClayEditor;
using ClayEditor.Interface;
using GameData;
using R3;
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace UI.ClayEditor.ViewModel
{
    /// <summary>
    /// モデル選択のためのViewModel
    /// </summary>
    public sealed class ModelTypeSelectionViewModel : IDisposable
    {
        private readonly IObjectResolver resolver;
        private readonly IClaySceneContext context;
        private readonly CompositeDisposable disposables = new();

        /// <summary>
        /// 編集対象のモデルが選択されているか
        /// </summary>
        public ReadOnlyReactiveProperty<bool> HasModel { get; }

        /// <summary>
        /// 現在の編集モード
        /// </summary>
        public ReadOnlyReactiveProperty<EditModeType> CurrentMode => context.CurrentMode;

        /// <summary>
        /// 選択UIを表示すべきか
        /// </summary>
        public ReadOnlyReactiveProperty<bool> ShowSelector { get; }

        [Inject]
        public ModelTypeSelectionViewModel(IObjectResolver resolver, IClaySceneContext context)
        {
            this.resolver = resolver;
            this.context = context;

            HasModel = context.CurrentModel
                .Select(model => model != null)
                .ToReadOnlyReactiveProperty()
                .AddTo(disposables);

            ShowSelector = Observable.CombineLatest(
                    context.CurrentModel.Select(model => model != null),
                    context.CurrentMode,
                    (hasModel, mode) => !hasModel || mode == EditModeType.ModelSelect)
                .ToReadOnlyReactiveProperty()
                .AddTo(disposables);
        }

        /// <summary>
        /// 指定プレハブを生成して編集対象モデルとして登録
        /// </summary>
        /// <param name="prefab">生成するモデルのプレハブ</param>
        /// <param name="parent">生成先の親 Transform</param>
        public void SelectModel(GameObject prefab, Transform parent)
        {
            if (prefab == null)
            {
                return;
            }

            GameObject spawnedModel = resolver.Instantiate(prefab, parent);

            // ClayModel を持たないプレハブは無効
            if (!spawnedModel.TryGetComponent(out ClayModel clayModel))
            {
                UnityEngine.Object.Destroy(spawnedModel);
                return;
            }

            // 既存モデルがあれば破棄する（型の切り替え）
            ClayModel previous = context.CurrentModel.Value;
            if (previous != null && previous != clayModel)
            {
                UnityEngine.Object.Destroy(previous.gameObject);
            }

            context.SetModel(clayModel);

            // 型選択モードから自動的にクレイ編集モードへ戻す
            context.ChangeMode(EditModeType.Clay);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
