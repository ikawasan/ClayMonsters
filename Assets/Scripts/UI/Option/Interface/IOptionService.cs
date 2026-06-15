using UnityEngine;

namespace UI.Option.Interface
{
    public interface IOptionService
    {
        bool GetFullScreen { get; }
        bool GetVSync { get; }
        float GetBrightness { get; }
        float GetMusicVolume { get; }
        float GetSoundEffectVolume { get; }

        void SetFullScreen(bool isFullScreen);
        void SetVSync(bool isVSync);
        void SetBrightness(float brightness);
        void SetMusicVolume(float volume);
        void SetSoundEffectVolume(float volume);
    }
}
