using ClayEditor.Rigging;
using LighthouseExtends.UIComponent.Button;
using Scene.TrainingScene.Domain;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 攻撃入れ替え候補1枠の表示
    /// シーン配置済みUIへ現在技情報を反映する
    /// </summary>
    public sealed class TrainingAttackSwapSlotView : MonoBehaviour
    {
        [Header("Button")]
        [Tooltip("このスロットを入れ替えるボタン")]
        [SerializeField] private LHButton selectButton;

        [Header("Attack")]
        [Tooltip("現在の技表示")]
        [SerializeField] private TrainingAttackSlotView currentAttackSlot;

        /// <summary>
        /// 入れ替え候補を表示する
        /// </summary>
        /// <param name="slotNumber">スロット番号</param>
        /// <param name="currentAttack">現在の攻撃</param>
        public void Apply(int slotNumber, MotionType currentAttack)
        {
            EnsureSelectButtonReference();
            gameObject.SetActive(true);
            currentAttackSlot?.Apply(slotNumber, currentAttack);
        }

        /// <summary>
        /// 表示を隠す
        /// </summary>
        public void Hide()
        {
            currentAttackSlot?.Clear();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 選択ボタンを返す
        /// </summary>
        public LHButton SelectButton
        {
            get
            {
                EnsureSelectButtonReference();
                return selectButton;
            }
        }

        private void EnsureSelectButtonReference()
        {
            if (selectButton != null)
            {
                return;
            }

            selectButton = GetComponent<LHButton>();
        }
    }
}
