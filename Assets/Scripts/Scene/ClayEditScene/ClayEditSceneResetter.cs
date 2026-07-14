using ClayEditor;
using ClayEditor.Interface;
using ClayEditor.Rigging;
using GameData;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using VContainer;

namespace Scene.ClayEditScene
{
    /// <summary>
    /// ClayEdit シーンから別シーンへ遷移する際に、シーンの状態を初期化する。
    /// 造形メッシュ・ボクセル・Undo履歴・生成済みモデル・編集モードをリセットする。
    /// </summary>
    public sealed class ClayEditSceneResetter
    {
        private readonly IClaySceneContext context;
        private readonly ClayEditor.ClayEditor editor;
        private readonly ClayHistoryManager historyManager;
        private readonly ClayAutoRigger rigger;
        private readonly ClayEditSessionContext sessionContext;

        [Inject]
        public ClayEditSceneResetter(
            IClaySceneContext context,
            ClayEditor.ClayEditor editor,
            ClayHistoryManager historyManager,
            ClayAutoRigger rigger,
            ClayEditSessionContext sessionContext)
        {
            this.context = context;
            this.editor = editor;
            this.historyManager = historyManager;
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

            // Undo / Redo 履歴を破棄する
            historyManager.Clear();

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
