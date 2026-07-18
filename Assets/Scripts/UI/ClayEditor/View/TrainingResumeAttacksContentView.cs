using ClayEditor.Rigging;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 育成確認画面とセーブスロット向け技構成表示
    /// 4枠固定のシーン配置UIへ攻撃情報を反映する
    /// </summary>
    public sealed class TrainingResumeAttacksContentView : MonoBehaviour
    {
        public const int AttackSlotCount = 4;

        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private TrainingAttackSlotView[] attackSlots = new TrainingAttackSlotView[AttackSlotCount];

        /// <summary>
        /// 技構成を表示する
        /// </summary>
        /// <param name="attacks">技一覧</param>
        public void Show(IReadOnlyList<MotionType> attacks)
        {
            if (attacks == null || attacks.Count == 0)
            {
                ShowEmpty();
                return;
            }

            if (headerText != null)
            {
                headerText.enabled = true;
                headerText.text = "▼技構成";
            }

            if (emptyText != null)
            {
                emptyText.enabled = false;
            }

            for (int i = 0; i < AttackSlotCount; i++)
            {
                TrainingAttackSlotView slotView = attackSlots != null && i < attackSlots.Length
                    ? attackSlots[i]
                    : null;
                if (slotView == null)
                {
                    continue;
                }

                if (i < attacks.Count)
                {
                    slotView.Apply(i + 1, attacks[i]);
                }
                else
                {
                    slotView.Clear();
                }
            }
        }

        /// <summary>
        /// 育成再開確認向けに4枠固定レイアウトで技構成を表示する
        /// 空枠もHierarchy上は残しLayoutGroupの再計算を避ける
        /// </summary>
        /// <param name="attacks">技一覧</param>
        public void ShowForResumeWindow(IReadOnlyList<MotionType> attacks)
        {
            const bool preserveLayoutSpace = true;

            if (headerText != null)
            {
                headerText.enabled = false;
            }

            if (emptyText != null)
            {
                emptyText.enabled = false;
            }

            if (attacks == null || attacks.Count == 0)
            {
                ClearSlots(preserveLayoutSpace);
                return;
            }

            for (int i = 0; i < AttackSlotCount; i++)
            {
                TrainingAttackSlotView slotView = attackSlots != null && i < attackSlots.Length
                    ? attackSlots[i]
                    : null;
                if (slotView == null)
                {
                    continue;
                }

                if (i < attacks.Count)
                {
                    slotView.Apply(i + 1, attacks[i], preserveLayoutSpace);
                }
                else
                {
                    slotView.Clear(preserveLayoutSpace);
                }
            }
        }

        /// <summary>
        /// セーブスロット行向けにヘッダーなしで技構成を表示する
        /// </summary>
        /// <param name="attacks">技一覧</param>
        public void ShowForSaveSlot(IReadOnlyList<MotionType> attacks)
        {
            if (attacks == null || attacks.Count == 0)
            {
                Clear();
                return;
            }

            if (headerText != null)
            {
                headerText.gameObject.SetActive(false);
            }

            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(false);
            }

            for (int i = 0; i < AttackSlotCount; i++)
            {
                TrainingAttackSlotView slotView = attackSlots != null && i < attackSlots.Length
                    ? attackSlots[i]
                    : null;
                if (slotView == null)
                {
                    continue;
                }

                if (i < attacks.Count)
                {
                    slotView.Apply(i + 1, attacks[i]);
                }
                else
                {
                    slotView.Clear();
                }
            }
        }

        /// <summary>
        /// 確認画面プレハブ向けに技構成を表示する
        /// 枠のHierarchyとRectTransformは維持する
        /// </summary>
        /// <param name="attacks">技一覧</param>
        public void ShowForConfirmPrefab(IReadOnlyList<MotionType> attacks)
        {
            if (headerText != null)
            {
                headerText.enabled = false;
            }

            if (emptyText != null)
            {
                emptyText.enabled = false;
            }

            if (attacks == null || attacks.Count == 0)
            {
                ClearForConfirmPrefab();
                return;
            }

            for (int i = 0; i < AttackSlotCount; i++)
            {
                TrainingAttackSlotView slotView = attackSlots != null && i < attackSlots.Length
                    ? attackSlots[i]
                    : null;
                if (slotView == null)
                {
                    continue;
                }

                if (i < attacks.Count)
                {
                    slotView.ApplyForConfirmPrefab(i + 1, attacks[i]);
                }
                else
                {
                    slotView.ClearForConfirmPrefab();
                }
            }
        }

        /// <summary>
        /// 確認画面プレハブ向けに表示内容をクリアする
        /// </summary>
        public void ClearForConfirmPrefab()
        {
            if (attackSlots == null)
            {
                return;
            }

            for (int i = 0; i < attackSlots.Length; i++)
            {
                attackSlots[i]?.ClearForConfirmPrefab();
            }
        }

        /// <summary>
        /// 表示内容をクリアする
        /// </summary>
        /// <param name="preserveLayoutSpace">trueのときLayoutGroup向けに枠を残す</param>
        public void Clear(bool preserveLayoutSpace = false)
        {
            if (headerText != null)
            {
                if (preserveLayoutSpace)
                {
                    headerText.enabled = false;
                }
                else
                {
                    headerText.gameObject.SetActive(false);
                }
            }

            if (emptyText != null)
            {
                if (preserveLayoutSpace)
                {
                    emptyText.enabled = false;
                }
                else
                {
                    emptyText.gameObject.SetActive(false);
                }
            }

            ClearSlots(preserveLayoutSpace);
        }

        private void ShowEmpty()
        {
            ClearSlots(preserveLayoutSpace: false);

            if (headerText != null)
            {
                headerText.enabled = false;
            }

            if (emptyText != null)
            {
                emptyText.enabled = true;
                emptyText.text = "▼技構成\nなし";
                return;
            }

            if (headerText != null)
            {
                headerText.enabled = true;
                headerText.text = "▼技構成\nなし";
            }
        }

        private void ClearSlots(bool preserveLayoutSpace)
        {
            if (attackSlots == null)
            {
                return;
            }

            for (int i = 0; i < attackSlots.Length; i++)
            {
                attackSlots[i]?.Clear(preserveLayoutSpace);
            }
        }
    }
}
