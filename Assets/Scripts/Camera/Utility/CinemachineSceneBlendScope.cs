using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Camera.Utility
{
    /// <summary>
    /// シーン入場時にCinemachineBrainのブレンドを一時的にカットする
    /// DefaultBlendがEaseInOutだとVCam切替で数秒かけて動いて見える
    /// </summary>
    public sealed class CinemachineSceneBlendScope : IDisposable
    {
        private readonly CinemachineBrain brain;
        private readonly CinemachineBlendDefinition savedBlend;
        private readonly bool isActive;

        private CinemachineSceneBlendScope(CinemachineBrain brain)
        {
            this.brain = brain;
            if (brain == null)
            {
                isActive = false;
                return;
            }

            isActive = true;
            savedBlend = brain.DefaultBlend;
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.Cut,
                0f);
        }

        /// <summary>
        /// Brainのブレンドをカットに切り替えたスコープを開始する
        /// </summary>
        public static CinemachineSceneBlendScope EnterCutBlend()
        {
            CinemachineBrain brain = UnityEngine.Object.FindFirstObjectByType<CinemachineBrain>();
            return new CinemachineSceneBlendScope(brain);
        }

        /// <summary>
        /// 保存していたDefaultBlendを復元する
        /// </summary>
        public void Dispose()
        {
            if (!isActive || brain == null)
            {
                return;
            }

            brain.DefaultBlend = savedBlend;
        }
    }
}
