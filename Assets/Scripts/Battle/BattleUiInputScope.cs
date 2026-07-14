using System;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Battle
{
    /// <summary>
    /// 戦闘中にUIのキーボードナビゲーションだけを止めA/D移動入力を通す
    /// </summary>
    public sealed class BattleUiInputScope : IDisposable
    {
        private InputAction navigateAction;
        private bool navigateWasEnabled;
        private bool sendNavigationWasEnabled = true;

        /// <summary>
        /// UIナビゲーション抑制を開始する
        /// </summary>
        public static BattleUiInputScope Suppress()
        {
            var scope = new BattleUiInputScope();
            scope.Acquire();
            return scope;
        }

        private void Acquire()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                sendNavigationWasEnabled = eventSystem.sendNavigationEvents;
                eventSystem.sendNavigationEvents = false;
                eventSystem.SetSelectedGameObject(null);
            }

            InputSystemUIInputModule module = eventSystem != null
                ? eventSystem.GetComponent<InputSystemUIInputModule>()
                : null;

            if (module?.actionsAsset == null)
            {
                return;
            }

            navigateAction = module.actionsAsset.FindAction("UI/Navigate");
            if (navigateAction != null)
            {
                navigateWasEnabled = navigateAction.enabled;
                navigateAction.Disable();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (navigateAction != null && navigateWasEnabled)
            {
                navigateAction.Enable();
            }

            navigateAction = null;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.sendNavigationEvents = sendNavigationWasEnabled;
            }
        }
    }
}
