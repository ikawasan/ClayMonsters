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
    public class LanguageOptionSaveData
    {
        /// <summary>
        /// 選択中言語コード空文字は未設定
        /// </summary>
        public string LanguageCode = string.Empty;
    }

    [Serializable]
    public class SaveData : ISaveData
    {
        public VideoOptionSaveData VideoOptionData = new VideoOptionSaveData();
        public SoundOptionSaveData SoundOptionData = new SoundOptionSaveData();
        public LanguageOptionSaveData LanguageOptionData = new LanguageOptionSaveData();
        public NpcBattleProgressSaveData NpcBattleProgress = new NpcBattleProgressSaveData();

        /// <summary>
        /// 育成ゴールドとは別のゲーム内ポイント
        /// </summary>
        public int Points;

        /// <summary>
        /// スキルツリー解放進捗
        /// </summary>
        public SkillTreeSaveData SkillTree = new SkillTreeSaveData();
    }
}
