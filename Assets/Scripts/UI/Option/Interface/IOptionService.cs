using UnityEngine;

namespace UI.Option.Interface
{
    public interface IOptionService
    {
        Resolution[] GetSupportedResolutions();
        float GetBrightness();

        void ApplyResolution(int index);
        void ApplyFullScreen(bool isFullScreen);
        void ApplyVSync(bool isVSync);
        void ApplyBrightness(float brightness);
        void ApplyMusicVolume(float volume);
        void ApplySoundEffectVolume(float volume);
    }
}
