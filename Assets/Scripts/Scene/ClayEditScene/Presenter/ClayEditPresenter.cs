using Camera.Interface;
using ClayEditor;
using ClayEditor.Input.Interface;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using R3;
using SaveData;
using SaveData.Interface;
using Scene.ClayEditScene.Interface;
using Scene.Core.Interface;
using System;
using System.IO;
using System.Threading;
using UI.ClayEditor.View;
using UnityEngine;
using VContainer;

namespace Scene.ClayEditScene.Presenter
{
    public class ClayEditPresenter : IClayEditPresenter, IDisposable
    {
        private IClayMonsterSceneManager sceneManager;
        private IClayEditView clayEditView;
        private IClayModelExporter exporter;
        private ClayAutoRigController autoRigController;
        private SaveSlotView saveSlotView;
        private IClayInputProvider inputProvider;
        private IClayEditCameraView cameraView;
        private IClayEditEntryView entryView;
        private ClayEditRemakeLoadSlotView remakeLoadSlotView;
        private IClayEditEditorUiGate editorUiGate;
        private ClayEditSessionContext sessionContext;
        private ClayHistoryManager historyManager;

        private readonly CompositeDisposable disposables = new();

        private bool isExporting;

        [Inject]
        public void Construct(
            IClayMonsterSceneManager sceneManager,
            IClayEditView clayEditView,
            IClayModelExporter exporter,
            ClayAutoRigController autoRigController,
            SaveSlotView saveSlotView,
            IClayInputProvider inputProvider,
            IClayEditCameraView cameraView,
            IClayEditEntryView entryView,
            ClayEditRemakeLoadSlotView remakeLoadSlotView,
            IClayEditEditorUiGate editorUiGate,
            ClayEditSessionContext sessionContext,
            ClayHistoryManager historyManager)
        {
            this.sceneManager = sceneManager;
            this.clayEditView = clayEditView;
            this.exporter = exporter;
            this.autoRigController = autoRigController;
            this.saveSlotView = saveSlotView;
            this.inputProvider = inputProvider;
            this.cameraView = cameraView;
            this.entryView = entryView;
            this.remakeLoadSlotView = remakeLoadSlotView;
            this.editorUiGate = editorUiGate;
            this.sessionContext = sessionContext;
            this.historyManager = historyManager;
        }

        void IClayEditPresenter.Setup()
        {
            // イベントを登録する
            clayEditView.SubscribeOpenToModeSelectSceneWindowButtonClick(OpenToModeSelectSceneButton);
            clayEditView.SubscribeToModeSelectSceneButtonClick((cancel) => ToModeSelectSceneButton(cancel).Forget());
            clayEditView.SubscribeCancelToModeSelectSceneButtonClick(CancelToModeSelectSceneButton);

            entryView.OnNewCreateClicked
                .Subscribe(_ => BeginNewCreateFlow())
                .AddTo(disposables);

            entryView.OnRemakeClicked
                .Subscribe(_ => BeginRemakeFlow())
                .AddTo(disposables);

            remakeLoadSlotView.OnRemakeConfirmed
                .Subscribe(OnRemakeModelLoaded)
                .AddTo(disposables);

            remakeLoadSlotView.OnCancelled
                .Subscribe(_ =>
                {
                    entryView.Show();
                    SetEditorInteractable(false);
                })
                .AddTo(disposables);

            // セーブ完了を購読してシーン遷移する(プレイヤー保存のみ)
            saveSlotView.OnSaved
                .Subscribe(info =>
                {
                    if (info.Pool == ModelSavePool.Player)
                    {
                        OnModelSaved();
                    }
                })
                .AddTo(disposables);

            // 保存UIの開閉に合わせて、モデル操作とカメラ操作のロックを切り替える
            saveSlotView.IsSaveUiOpen
                .Subscribe(isOpen =>
                {
                    if (!sessionContext.IsEditorReady)
                    {
                        return;
                    }

                    SetEditorInteractable(!isOpen);
                })
                .AddTo(disposables);
        }

        void IClayEditPresenter.OnEnter()
        {
            clayEditView.Inisialize();
            sessionContext.Reset();
            remakeLoadSlotView.Hide();
            saveSlotView.HideForLeave();
            editorUiGate.SetEditorVisible(false);
            entryView.Hide();
            SetEditorInteractable(false);
        }

        /// <inheritdoc/>
        void IClayEditPresenter.OnEnterAfterFadeIn()
        {
            entryView.Show();
        }

        /// <inheritdoc/>
        void IClayEditPresenter.OnLeave()
        {
            remakeLoadSlotView.Hide();
            saveSlotView.HideForLeave();
            entryView.Hide();
            editorUiGate.SetEditorVisible(false);
            if (clayEditView?.CheckToModeSelectSceneWindow != null)
            {
                clayEditView.CheckToModeSelectSceneWindow.enabled = false;
            }

            SetEditorInteractable(false);
        }

        private void BeginNewCreateFlow()
        {
            sessionContext.BeginNewCreate();
            entryView.Hide();
            editorUiGate.SetEditorVisible(true);
            saveSlotView.ShowEditorChrome();
            SetEditorInteractable(true);
        }

        private void BeginRemakeFlow()
        {
            entryView.Hide();
            remakeLoadSlotView.Show();
        }

        private void OnRemakeModelLoaded(ClayEditRemakeSelection selection)
        {
            sessionContext.BeginRemake(selection.SlotIndex, selection.ModelName);
            historyManager.Clear();
            remakeLoadSlotView.Hide();
            editorUiGate.SetEditorVisible(true);
            saveSlotView.ShowEditorChrome();
            SetEditorInteractable(true);
        }

        void OpenToModeSelectSceneButton()
        {
            clayEditView.CheckToModeSelectSceneWindow.enabled = true;
        }
        async UniTask ToModeSelectSceneButton(CancellationToken cancellationToken)
        {
            SetInputBlocker(true);
            await OnSaveClicked(cancellationToken);
            SetInputBlocker(false);
            sceneManager.BackScene().Forget();
        }
        void CancelToModeSelectSceneButton()
        {
            clayEditView.CheckToModeSelectSceneWindow.enabled = false;
        }

        // セーブ完了時: 暗転してから前シーンへ戻り明転する
        void OnModelSaved()
        {
            ReturnAfterSaveAsync().Forget();
        }

        private async UniTaskVoid ReturnAfterSaveAsync()
        {
            await sceneManager.BackScene();
        }

        // 編集(モデル操作・カメラ操作)の受付を切り替える
        private void SetEditorInteractable(bool isInteractable)
        {
            // Undo/Redo/Delete/彫る/ブラシ操作などモデルへ影響する入力
            inputProvider.SetInputEnabled(isInteractable);

            // カメラの回転・ズーム・プリセット視点操作
            cameraView.SetCameraOperatable(isInteractable);
        }

        private void SetInputBlocker(bool isBlocked)
        {
            clayEditView.InputBlocker.SetActive(isBlocked);
        }

        private async UniTask OnSaveClicked(CancellationToken cancellationToken)
        {
            // 多重実行を防ぐ
            if (isExporting)
            {
                return;
            }

            // TODO: ボーンを生成する
            await ExportAsync(cancellationToken);
            autoRigController.ClearBones();
        }

        private async UniTask ExportAsync(CancellationToken cancellationToken)
        {
            Transform boneRoot = autoRigController.BoneRoot;
            SkinnedMeshRenderer runtimeRenderer = autoRigController.MeshRenderer;
            if (runtimeRenderer == null || boneRoot == null)
            {
                Debug.LogWarning("エクスポート対象が不足しています(runtimeRenderer / BoneRoot)");
                return;
            }

            isExporting = true;
            try
            {
                // 出力直前に、その時点の形状からボーンを生成し適用する
                autoRigController.RebuildSkeleton();

                string fileName = "TestData.glb";
                string filePath = Path.Combine(Application.persistentDataPath, fileName);

                bool success = await exporter.ExportToGlbAsync(runtimeRenderer, boneRoot, filePath, cancellationToken);
                if (success)
                {
                    Debug.Log($"モデルをエクスポートしました: {filePath}");
                }
                else
                {
                    Debug.LogError("モデルのエクスポートに失敗しました");
                }
            }
            finally
            {
                isExporting = false;
            }
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}