using System;
using System.Threading;
using Battle.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using Scene.Core.Interface;
using UnityEngine;

namespace Scene.BattleNpcScene
{
    /// <summary>
    /// シーン共通フェードを使ってBattleNpc内のキャンバス切替を行う
    /// </summary>
    public sealed class BattleCanvasTransition : IBattleCanvasTransition
    {
        private readonly ISceneFade sceneFade;

        /// <summary>
        /// シーン共通フェードを受け取る
        /// </summary>
        public BattleCanvasTransition(ISceneFade sceneFade)
        {
            this.sceneFade = sceneFade;
        }

        /// <inheritdoc />
        public UniTask FadeOutAsync(CancellationToken cancellationToken = default)
        {
            return sceneFade != null
                ? sceneFade.FadeOutAsync(cancellationToken)
                : UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public async UniTask FadeInAsync(CancellationToken cancellationToken = default)
        {
            if (sceneFade == null)
            {
                return;
            }

            await sceneFade.FadeInAsync(cancellationToken);
            ReleasePresentationInput();
        }

        /// <inheritdoc />
        public void ReleasePresentationInput()
        {
            if (sceneFade is Scene.Core.View.SceneFadeView fadeView)
            {
                fadeView.ReleaseInputBlock();
            }
        }

        /// <inheritdoc />
        public async UniTask SetCanvasEnabledAsync(
            Canvas canvas,
            bool enabled,
            CancellationToken cancellationToken = default,
            bool fadeOutBeforeChange = true,
            bool fadeInAfterChange = true)
        {
            if (sceneFade == null)
            {
                ApplyCanvasEnabled(canvas, enabled);
                return;
            }

            if (fadeOutBeforeChange)
            {
                await sceneFade.FadeOutAsync(cancellationToken);
            }

            ApplyCanvasEnabled(canvas, enabled);

            if (fadeInAfterChange)
            {
                await sceneFade.FadeInAsync(cancellationToken);
            }
        }

        /// <inheritdoc />
        public async UniTask SwitchCanvasAsync(Canvas from, Canvas to, CancellationToken cancellationToken = default)
        {
            if (sceneFade == null)
            {
                ApplyCanvasEnabled(from, false);
                ApplyCanvasEnabled(to, true);
                return;
            }

            await sceneFade.FadeOutAsync(cancellationToken);
            ApplyCanvasEnabled(from, false);
            ApplyCanvasEnabled(to, true);
            await sceneFade.FadeInAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async UniTask FadeOutAndRunAsync(Action apply, CancellationToken cancellationToken = default)
        {
            if (sceneFade != null)
            {
                await sceneFade.FadeOutAsync(cancellationToken);
            }

            apply?.Invoke();
        }

        private static void ApplyCanvasEnabled(Canvas canvas, bool enabled)
        {
            CanvasVisibilityUtility.SetCanvasEnabled(canvas, enabled);
        }
    }
}
