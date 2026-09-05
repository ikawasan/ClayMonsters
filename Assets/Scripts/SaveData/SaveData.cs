using SaveData.Interface;
using System;

namespace SaveData
{
    [Serializable]
    public class VideoOptionSaveData
    {
        public bool IsFullScreen = true;
    }

    [Serializable]
    public class  SoundOptionSaveData
    {
        public float MusicVolume = 0.3f;
        public float SoundEffectVolume = 0.3f;
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
        /// 中断したNPCトーナメントの再開進捗
        /// </summary>
        public NpcTournamentProgressSaveData NpcTournamentProgress = new NpcTournamentProgressSaveData();

        /// <summary>
        /// 育成ゴールドとは別のゲーム内ポイント
        /// </summary>
        public int Points = BattlePointsRules.InitialHeldPoints;

        /// <summary>
        /// 初期ポイント付与済みか
        /// </summary>
        public bool StartingPointsGranted;

        /// <summary>
        /// スキルツリー解放進捗
        /// </summary>
        public SkillTreeSaveData SkillTree = new SkillTreeSaveData();
    }
}
