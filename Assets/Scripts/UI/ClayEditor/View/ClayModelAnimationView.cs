using ClayEditor;
using ClayEditor.Rigging;

using GameData;

using LighthouseExtends.UIComponent.Button;

using R3;

using R3.Triggers;

using UI.ClayEditor.ViewModel;

using UnityEngine;

using VContainer;



namespace UI.ClayEditor.View

{

    /// <summary>

    /// ClayEditで待機・走行・汎用攻撃をプレビューするView

    /// </summary>

    public class ClayModelAnimationView : MonoBehaviour

    {

        [Inject] private ClayAutoRigController autoRigController;

        [Inject] private ClayEditModeViewModel viewModel;

        [Inject] private SkeletonPartAnalyzer partAnalyzer;



        [SerializeField] private Canvas canvas;

        [SerializeField] private ProceduralMotionCharacter motionCharacter;

        [SerializeField] private LHButton runButton;

        [SerializeField] private LHButton attackButton;



        private void Start()

        {

            canvas.OnEnableAsObservable()

                .Subscribe(_ => TryPlayIdle())

                .AddTo(this);



            canvas.OnDisableAsObservable()

                .Subscribe(_ => StopMotion())

                .AddTo(this);



            autoRigController.OnSkeletonRebuilt

                .Subscribe(_ =>

                {

                    RefreshButtonStates();

                    TryPlayIdle();

                })

                .AddTo(this);



            viewModel.CurrentMode

                .Subscribe(mode =>

                {

                    if (mode != EditModeType.Animation)

                    {

                        StopMotion();

                    }

                    else

                    {

                        TryPlayIdle();

                    }

                })

                .AddTo(this);



            if (runButton != null)

            {

                runButton.onClick.AsObservable()

                    .Subscribe(_ => ToggleRun())

                    .AddTo(this);

            }



            if (attackButton != null)

            {

                attackButton.onClick.AsObservable()

                    .Subscribe(_ => PlayGenericAttack())

                    .AddTo(this);

            }



            RefreshButtonStates();

        }



        private bool CanDriveMotion()

        {

            return viewModel.CurrentMode.CurrentValue == EditModeType.Animation

                && canvas.enabled

                && canvas.gameObject.activeInHierarchy;

        }



        private void TryPlayIdle()

        {

            if (!CanDriveMotion() || !motionCharacter.IsReady)

            {

                return;

            }



            motionCharacter.Play(MotionType.Idle);

        }



        private void StopMotion()

        {

            if (motionCharacter == null || !motionCharacter.IsReady)

            {

                return;

            }



            motionCharacter.Play(MotionType.None);

        }



        private void ToggleRun()

        {

            if (!CanDriveMotion() || !motionCharacter.IsReady)

            {

                return;

            }



            MotionType runMotion = ClayEditMotionPreview.ResolveRunMotion(motionCharacter);

            if (motionCharacter.CurrentMotion == runMotion)

            {

                TryPlayIdle();

                return;

            }



            motionCharacter.Play(runMotion);

        }



        private void PlayGenericAttack()

        {

            if (!CanDriveMotion() || !motionCharacter.IsReady)

            {

                return;

            }



            MotionType? attack = ClayEditMotionPreview.ResolveGenericAttack(partAnalyzer, autoRigController.Bones);

            if (!attack.HasValue)

            {

                return;

            }



            motionCharacter.Play(attack.Value);

        }



        private void RefreshButtonStates()

        {

            bool isReady = motionCharacter != null && motionCharacter.IsReady;



            if (runButton != null)

            {

                runButton.interactable = isReady;

            }



            if (attackButton != null)

            {

                bool canAttack = isReady

                    && ClayEditMotionPreview.ResolveGenericAttack(partAnalyzer, autoRigController.Bones).HasValue;

                attackButton.interactable = canAttack;

            }

        }

    }

}

