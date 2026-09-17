using ClayEditor;
using Extensions;
using GameData;
using R3;
using System;
using UI.ClayEditor.ViewModel;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// ClayEdit成形ブラシ形状の選択ボタン群を表示する
    /// </summary>
    public sealed class ClaySculptBrushShapeView : MonoBehaviour
    {
        [Serializable]
        private sealed class ShapeButtonEntry
        {
            public ClaySculptBrushShape shape;
            public Button button;
            public Image icon;
        }

        [Inject] private readonly ClaySculptBrushShapeContext brushShapeContext;
        [Inject] private readonly ClayEditModeViewModel modeViewModel;

        [SerializeField] private Canvas canvas;
        [SerializeField] private ShapeButtonEntry[] shapeButtons;
        [SerializeField] private Color selectedIconColor = Color.white;
        [SerializeField] private Color unselectedIconColor = new(0.72f, 0.72f, 0.72f, 0.92f);

        private void Start()
        {
            ValidateReferences();
            ApplyButtonIcons();
            BindButtons();
            RefreshSelectionVisual(brushShapeContext.Shape);

            brushShapeContext.CurrentShape
                .Subscribe(RefreshSelectionVisual)
                .AddTo(this);

            Observable.CombineLatest(
                    modeViewModel.CurrentMode,
                    modeViewModel.HasModel,
                    (mode, hasModel) => mode == EditModeType.Clay && hasModel)
                .Subscribe(isVisible => SetCanvasVisible(isVisible))
                .AddTo(this);
        }

        private void ValidateReferences()
        {
            if (canvas == null || shapeButtons == null || shapeButtons.Length == 0)
            {
                Debug.LogError(
                    "[ClaySculptBrushShapeView] 必須SerializeFieldが未配線ですHierarchyで接続してください",
                    this);
            }
        }

        private void ApplyButtonIcons()
        {
            if (shapeButtons == null)
            {
                return;
            }

            for (int i = 0; i < shapeButtons.Length; i++)
            {
                ShapeButtonEntry entry = shapeButtons[i];
                if (entry == null || entry.icon == null)
                {
                    continue;
                }

                Sprite icon = ClayEditPrimitiveResources.GetIcon(entry.shape);
                if (icon != null)
                {
                    entry.icon.sprite = icon;
                    entry.icon.preserveAspect = true;
                }
            }
        }

        private void BindButtons()
        {
            if (shapeButtons == null)
            {
                return;
            }

            for (int i = 0; i < shapeButtons.Length; i++)
            {
                ShapeButtonEntry entry = shapeButtons[i];
                if (entry == null || entry.button == null)
                {
                    continue;
                }

                ClaySculptBrushShape shape = entry.shape;
                entry.button.OnClickAsObservable()
                    .Subscribe(_ => brushShapeContext.SetShape(shape))
                    .AddTo(this);
            }
        }

        private void RefreshSelectionVisual(ClaySculptBrushShape selectedShape)
        {
            if (shapeButtons == null)
            {
                return;
            }

            for (int i = 0; i < shapeButtons.Length; i++)
            {
                ShapeButtonEntry entry = shapeButtons[i];
                if (entry == null || entry.icon == null)
                {
                    continue;
                }

                entry.icon.color = entry.shape == selectedShape
                    ? selectedIconColor
                    : unselectedIconColor;
            }
        }

        private void SetCanvasVisible(bool isVisible)
        {
            CanvasVisibilityUtility.SetCanvasEnabled(canvas, isVisible);
        }
    }
}
