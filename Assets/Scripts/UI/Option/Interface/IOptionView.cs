using System;
using UnityEngine.Events;

namespace UI.Option.Interface
{
    /// <summary>
    /// ??????????????????
    /// </summary>
    public interface IOptionView
    {
        /// <summary>
        /// ????????????
        /// </summary>
        void Show();

        /// <summary>
        /// ??????????????
        /// </summary>
        void Hide();

        /// <summary>
        /// ????UI??????
        /// </summary>
        /// <param name="isFullScreen">???????</param>
        /// <param name="isVSync">????</param>
        void InitVideoSettings(bool isFullScreen, bool isVSync);

        /// <summary>
        /// ??????UI??????
        /// </summary>
        /// <param name="musicVolume">BGM??</param>
        /// <param name="soundEffectVolume">?????</param>
        void InitSoundSettings(float musicVolume, float soundEffectVolume);

        /// <summary>
        /// ???????????????
        /// </summary>
        /// <param name="action">??????</param>
        /// <returns>?????</returns>
        IDisposable SubscribeCloseButtonClick(UnityAction action);

        /// <summary>
        /// ??????????????
        /// </summary>
        /// <param name="action">??????</param>
        void SubscribeFullScreenChanged(UnityAction<bool> action);

        /// <summary>
        /// ???????????
        /// </summary>
        /// <param name="action">??????</param>
        void SubscribeVSyncChanged(UnityAction<bool> action);

        /// <summary>
        /// BGM?????????
        /// </summary>
        /// <param name="action">??????</param>
        void SubscribeMusicVolumeChanged(UnityAction<float> action);

        /// <summary>
        /// ????????????
        /// </summary>
        /// <param name="action">??????</param>
        void SubscribeSoundEffectVolumeChanged(UnityAction<float> action);
    }
}
