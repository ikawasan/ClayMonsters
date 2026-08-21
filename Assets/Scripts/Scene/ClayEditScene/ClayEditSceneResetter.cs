using ClayEditor;
using ClayEditor.Interface;
using ClayEditor.Paint;
using ClayEditor.Rigging;
using GameData;
using TMPro;
using UI.ClayEditor.View;
using UI.ColorPicker;
using UnityEngine;
using VContainer;

namespace Scene.ClayEditScene
{
    /// <summary>
    /// ClayEdit シーンから別シーンへ遷移する際に、シーンの状態を初期化する。
    /// 造形メッシュ・ボクセル色・Undo/ペイント履歴・カラーピッカー・編集モードをリセットする。
    /// </summary>
    public sealed class ClayEditSceneResetter
    {
        private readonly IClaySceneContext context;
        private readonly ClayEditor.ClayEditor editor;
        private readonly ClayVoxelEngine engine;
        private readonly ClayHistoryManager historyManager;
        private readonly ClayPaintHistoryManager paintHistoryManager;
        private readonly ColorPicker colorPicker;
        private readonly ClayPainter clayPainter;
        private readonly ClayAutoRigger rigger;
        private readonly ClayEditSessionContext sessionContext;

        [Inject]
        public ClayEditSceneResetter(
            IClaySceneContext context,
            ClayEditor.ClayEditor editor,
            ClayVoxelEngine engine,
            ClayHistoryManager historyManager,
            ClayPaintHistoryManager paintHistoryManager,
            ColorPicker colorPicker,
            ClayPainter clayPainter,
            ClayAutoRigger rigger,
            ClayEditSessionContext sessionContext)
        {
            this.context = context;
            this.editor = editor;
            this.engine = engine;
            this.historyManager = historyManager;
            this.paintHistoryManager = paintHistoryManager;
            this.colorPicker = colorPicker;
            this.clayPainter = clayPainter;
            this.rigger = rigger;
            this.sessionContext = sessionContext;
        }

        /// <summary>
        /// ClayEdit シーンの状態を初期状態へ戻す。
        /// </summary>
        public void Reset()
        {
            sessionContext.Reset();
            CollapseModeDropdown();
            // 造形メッシュ・ボクセルを空に戻す
            editor.ClearMesh();

            // ペイント頂点色を既定へ戻す(密度クリア後も色バッファは残るため明示的に初期化)
            engine.ResetVoxelColors();

            // Undo / Redo 履歴を破棄する
            historyManager.Clear();
            paintHistoryManager.Clear();

            // ブラシ色とカラーピッカー表示を初期化する(再入場時に前回色が残らないようにする)
            clayPainter.ResetPaintState();
            colorPicker.ResetToColor(clayPainter.CurrentColor);

            // 自動生成したボーンを破棄する（固定モデル自体は破棄しない）
            ClayModel model = context.CurrentModel.Value;
            if (model != null && model.BoneRoot != null)
            {
                for (int i = model.BoneRoot.childCount - 1; i >= 0; i--)
                {
                    Object.Destroy(model.BoneRoot.GetChild(i).gameObject);
                }
            }

            // リガーのボーン参照をクリアする（破棄済みボーンを参照したままにしない）
            rigger.SetBones(new Transform[0]);

            // 編集モードを初期化する
            context.ChangeMode(EditModeType.Clay);
        }

        private static void CollapseModeDropdown()
        {
            ClayEditModeView modeView = Object.FindFirstObjectByType<ClayEditModeView>(FindObjectsInactive.Include);
            if (modeView == null)
            {
                return;
            }

            TMP_Dropdown dropdown = modeView.GetComponentInChildren<TMP_Dropdown>(true);
            if (dropdown != null)
            {
                ClayEditModeDropdownLayout.Collapse(dropdown);
            }

            ModelSaveSlotScrollListView.ExitFullscreenSelectionLayout();
        }
    }
}
