using ClayEditor.Input.Interface;
using R3;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace ClayEditor.Input
{
    /// <summary>
    /// 入力情報を提供するクラス
    /// </summary>
    public sealed class ClayInputProvider : IClayInputProvider, ITickable, IDisposable
    {
        // イベント用 Subject
        private readonly Subject<Unit> onUndo = new();
        private readonly Subject<Unit> onRedo = new();
        private readonly Subject<Unit> onDelete = new();
        private readonly Subject<Unit> onPrimaryPressed = new();
        private readonly Subject<Unit> onSecondaryPressed = new();
        private readonly Subject<float> onScroll = new();
        private readonly ReactiveProperty<bool> isPointerOverUI = new(false);

        // 入力受付の有効/無効
        private bool isInputEnabled = true;

        // スライダー操作中などポインタがUI外へ出てもオーバー扱いを維持する件数
        private int uiPointerCaptureCount;

        // UI操作の左クリックを離すまでオーバー扱いを維持する
        private bool holdUiOverUntilPrimaryRelease;

        /// <inheritdoc />
        public Observable<Unit> OnUndo => onUndo;

        /// <inheritdoc />
        public Observable<Unit> OnRedo => onRedo;

        /// <inheritdoc />
        public Observable<Unit> OnDelete => onDelete;

        /// <inheritdoc />
        public Observable<Unit> OnPrimaryPressed => onPrimaryPressed;

        /// <inheritdoc />
        public Observable<Unit> OnSecondaryPressed => onSecondaryPressed;

        /// <inheritdoc />
        public Observable<float> OnScroll => onScroll;

        /// <inheritdoc />
        public Observable<bool> OnPointerOverUIChanged => isPointerOverUI;

        /// <inheritdoc />
        public Vector2 PointerPosition { get; private set; }

        /// <inheritdoc />
        public bool IsPointerOverUI => isPointerOverUI.Value;

        /// <inheritdoc />
        public bool IsPrimaryHeld { get; private set; }

        /// <inheritdoc />
        public bool IsSecondaryHeld { get; private set; }

        /// <inheritdoc />
        public bool IsShiftPressed { get; private set; }

        /// <inheritdoc />
        public bool IsCtrlPressed { get; private set; }

        /// <inheritdoc />
        public bool IsAltPressed { get; private set; }

        // スクロールの反応値だがしきい値
        private const float ScrollThreshold = 0.01f;

        /// <inheritdoc />
        public void SetInputEnabled(bool isEnabled)
        {
            isInputEnabled = isEnabled;
        }

        /// <inheritdoc />
        public void BeginUiPointerCapture()
        {
            uiPointerCaptureCount++;
            holdUiOverUntilPrimaryRelease = false;
            if (uiPointerCaptureCount == 1)
            {
                isPointerOverUI.Value = true;
            }
        }

        /// <inheritdoc />
        public void EndUiPointerCapture()
        {
            if (uiPointerCaptureCount <= 0)
            {
                return;
            }

            uiPointerCaptureCount--;
            if (uiPointerCaptureCount > 0)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                // 離すまではペイント等へ遷移させない
                holdUiOverUntilPrimaryRelease = true;
                isPointerOverUI.Value = true;
                return;
            }

            holdUiOverUntilPrimaryRelease = false;
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
            {
                isPointerOverUI.Value = false;
            }
        }

        /// <inheritdoc />
        public void Tick()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || keyboard == null)
            {
                return;
            }

            // 入力が無効化されている間は、モデルへ影響する操作を一切受け付けない
            if (!isInputEnabled)
            {
                // 押下・修飾キーの状態をクリアして、無効化中に編集が継続しないようにする
                IsPrimaryHeld = false;
                IsSecondaryHeld = false;
                IsShiftPressed = false;
                IsCtrlPressed = false;
                IsAltPressed = false;
                return;
            }

            // 現在フレームの状態を更新
            bool isOverUi = uiPointerCaptureCount > 0
                || holdUiOverUntilPrimaryRelease
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());

            if (holdUiOverUntilPrimaryRelease && !mouse.leftButton.isPressed)
            {
                holdUiOverUntilPrimaryRelease = false;
                isOverUi = uiPointerCaptureCount > 0
                    || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            }

            isPointerOverUI.Value = isOverUi;
            PointerPosition = mouse.position.ReadValue();
            IsShiftPressed = keyboard.shiftKey.isPressed;
            IsCtrlPressed = keyboard.ctrlKey.isPressed;
            IsAltPressed = keyboard.altKey.isPressed;
            IsPrimaryHeld = mouse.leftButton.isPressed;
            IsSecondaryHeld = mouse.rightButton.isPressed;

            // UI 上にポインタがある状態は操作系の入力イベントを発火しない
            if (!IsPointerOverUI)
            {
                if (IsCtrlPressed && keyboard.zKey.wasPressedThisFrame)
                {
                    onUndo.OnNext(Unit.Default);
                }

                if (IsCtrlPressed && keyboard.yKey.wasPressedThisFrame)
                {
                    onRedo.OnNext(Unit.Default);
                }

                if (keyboard.deleteKey.wasPressedThisFrame)
                {
                    onDelete.OnNext(Unit.Default);
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > ScrollThreshold)
                {
                    onScroll.OnNext(scroll);
                }

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    onPrimaryPressed.OnNext(Unit.Default);
                }

                if (mouse.rightButton.wasPressedThisFrame)
                {
                    onSecondaryPressed.OnNext(Unit.Default);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            isPointerOverUI.Dispose();
            onUndo.Dispose();
            onRedo.Dispose();
            onDelete.Dispose();
            onPrimaryPressed.Dispose();
            onSecondaryPressed.Dispose();
            onScroll.Dispose();
        }
    }
}