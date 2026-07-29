using System;
using Battle.Interface;
using UnityEngine.InputSystem;

namespace Battle.Input
{
    /// <summary>
    /// A/Dキーで戦闘移動を入力する
    /// D=接近・A=後退・Shift+A/D=ステップ移動
    /// </summary>
    public sealed class BattleKeyboardMovementInput : IBattlePlayerInput, IDisposable
    {
        private readonly InputAction approachAction;
        private readonly InputAction retreatAction;
        private readonly InputAction stepApproachAction;
        private readonly InputAction stepRetreatAction;
        private readonly InputAction knockbackAction;
        private readonly InputAction partRepairAction;
        private readonly InputAction[] attackActions = new InputAction[4];
        private int movementIntent;
        private int pendingStepIntent;
        private bool knockbackPressed;
        private int pendingAttackIndex = -1;

        /// <summary>
        /// ステップ入力が押されたときに通知する
        /// </summary>
        public event System.Action<int> StepPressed;

        /// <summary>
        /// 攻撃入力が押されたときに通知する
        /// </summary>
        public event System.Action<int> AttackPressed;

        public BattleKeyboardMovementInput()
        {
            approachAction = new InputAction("Approach", InputActionType.Button);
            approachAction.AddBinding("<Keyboard>/d");
            approachAction.AddBinding("<Keyboard>/rightArrow");

            retreatAction = new InputAction("Retreat", InputActionType.Button);
            retreatAction.AddBinding("<Keyboard>/a");
            retreatAction.AddBinding("<Keyboard>/leftArrow");

            stepApproachAction = CreateShiftModifiedAction(
                "StepApproach",
                "<Keyboard>/d",
                "<Keyboard>/rightArrow");
            stepRetreatAction = CreateShiftModifiedAction(
                "StepRetreat",
                "<Keyboard>/a",
                "<Keyboard>/leftArrow");

            knockbackAction = new InputAction("Knockback", InputActionType.Button);
            knockbackAction.AddBinding("<Keyboard>/space");

            partRepairAction = new InputAction("PartRepair", InputActionType.Button);
            partRepairAction.AddBinding("<Mouse>/rightButton");

            string[] attackKeys = { "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3", "<Keyboard>/4" };
            for (int i = 0; i < attackActions.Length; i++)
            {
                attackActions[i] = new InputAction($"Attack{i + 1}", InputActionType.Button);
                attackActions[i].AddBinding(attackKeys[i]);
                attackActions[i].Enable();
            }

            approachAction.Enable();
            retreatAction.Enable();
            stepApproachAction.Enable();
            stepRetreatAction.Enable();
            knockbackAction.Enable();
            partRepairAction.Enable();
        }

        /// <inheritdoc />
        public int MovementIntent => movementIntent;

        /// <inheritdoc />
        public bool IsHoldingPartRepair => partRepairAction.IsPressed();

        /// <inheritdoc />
        public void RefreshInput()
        {
            bool shiftHeld = IsShiftHeld();

            if (stepApproachAction.WasPressedThisFrame())
            {
                pendingStepIntent = -1;
                StepPressed?.Invoke(pendingStepIntent);
            }
            else if (stepRetreatAction.WasPressedThisFrame())
            {
                pendingStepIntent = 1;
                StepPressed?.Invoke(pendingStepIntent);
            }

            if (!shiftHeld)
            {
                // InputActionはEnable時点で既に押下中のキーを拾えないことがあるため
                // Keyboardの現在押下も併用する
                bool approach = approachAction.IsPressed() || IsApproachKeyHeld();
                bool retreat = retreatAction.IsPressed() || IsRetreatKeyHeld();

                if (approach && !retreat)
                {
                    movementIntent = -1;
                }
                else if (retreat && !approach)
                {
                    movementIntent = 1;
                }
                else
                {
                    movementIntent = 0;
                }
            }
            else
            {
                movementIntent = 0;
            }

            if (knockbackAction.WasPressedThisFrame())
            {
                knockbackPressed = true;
            }

            for (int i = 0; i < attackActions.Length; i++)
            {
                if (attackActions[i].WasPressedThisFrame())
                {
                    pendingAttackIndex = i;
                    AttackPressed?.Invoke(i);
                    break;
                }
            }
        }

        /// <inheritdoc />
        public int ConsumeAttackMoveIndex()
        {
            if (pendingAttackIndex < 0)
            {
                return -1;
            }

            int index = pendingAttackIndex;
            pendingAttackIndex = -1;
            return index;
        }

        /// <inheritdoc />
        public int ConsumeStepIntent()
        {
            if (pendingStepIntent == 0)
            {
                return 0;
            }

            int intent = pendingStepIntent;
            pendingStepIntent = 0;
            return intent;
        }

        /// <inheritdoc />
        public bool ConsumeKnockbackPressed()
        {
            if (!knockbackPressed)
            {
                return false;
            }

            knockbackPressed = false;
            return true;
        }

        /// <summary>
        /// ネットワーク同期用に現在の入力状態を取得する
        /// </summary>
        public BattleKeyboardInputState PeekInputState() =>
            new BattleKeyboardInputState(
                movementIntent,
                pendingStepIntent,
                knockbackPressed,
                pendingAttackIndex,
                partRepairAction.IsPressed());

        /// <inheritdoc />
        public void Dispose()
        {
            approachAction.Disable();
            retreatAction.Disable();
            stepApproachAction.Disable();
            stepRetreatAction.Disable();
            knockbackAction.Disable();
            partRepairAction.Disable();
            approachAction.Dispose();
            retreatAction.Dispose();
            stepApproachAction.Dispose();
            stepRetreatAction.Dispose();
            knockbackAction.Dispose();
            partRepairAction.Dispose();
            for (int i = 0; i < attackActions.Length; i++)
            {
                attackActions[i].Disable();
                attackActions[i].Dispose();
            }
        }

        private static InputAction CreateShiftModifiedAction(string actionName, params string[] bindings)
        {
            var action = new InputAction(actionName, InputActionType.Button);
            for (int i = 0; i < bindings.Length; i++)
            {
                action.AddCompositeBinding("OneModifier")
                    .With("Binding", bindings[i])
                    .With("modifier", "<Keyboard>/leftShift");
                action.AddCompositeBinding("OneModifier")
                    .With("Binding", bindings[i])
                    .With("modifier", "<Keyboard>/rightShift");
            }

            return action;
        }

        private static bool IsShiftHeld()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        }

        private static bool IsApproachKeyHeld()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
        }

        private static bool IsRetreatKeyHeld()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
        }
    }
}
