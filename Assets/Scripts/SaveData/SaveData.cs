using SaveData.Interface;
using System;

namespace SaveData
{
    [Serializable]
    public class VideoOptionSaveData
    {
        public bool IsFullScreen = true;
        public bool VSync = true;
    }

    [Serializable]
    public class  SoundOptionSaveData
    {
        public float MusicVolume = 0.5f;
        public float SoundEffectVolume = 0.5f;
    }

    [Serializable]
    public class SaveData : ISaveData
    {
        public VideoOptionSaveData VideoOptionData = new VideoOptionSaveData();
        public SoundOptionSaveData SoundOptionData = new SoundOptionSaveData();
    }
}
