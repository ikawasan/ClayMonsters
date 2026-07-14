using System.Collections.Generic;
using ClayEditor.Rigging;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// モデルのステータス(HP・攻撃力・防御力・速度)
    /// 保存時はModelStatusCalculatorがモデル形状と色から算出した値を保持する
    /// </summary>
    [System.Serializable]
    public class ModelStatus
    {
        /// <summary>
        /// 体力
        /// </summary>
        public int hp;

        /// <summary>
        /// 攻撃力
        /// </summary>
        public int attack;

        /// <summary>
        /// 防御力
        /// </summary>
        public int defense;

        /// <summary>
        /// 速度
        /// </summary>
        public int speed;
    }

    /// <summary>
    /// 1スロット分のモデルセーブデータ
    /// </summary>
    [System.Serializable]
    public class ModelSaveSlot
    {
        /// <summary>
        /// このスロットを使用中か
        /// </summary>
        public bool isUsed;

        /// <summary>
        /// プレイヤーが付けたモデルの名前
        /// </summary>
        public string modelName;

        /// <summary>
        /// モデルのステータス
        /// </summary>
        public ModelStatus status = new ModelStatus();

        /// <summary>
        /// glbファイルの名前(persistentDataPath内)
        /// </summary>
        public string glbFileName;

        /// <summary>
        /// サムネイル画像(PNG)のファイル名(persistentDataPath内)。未撮影なら空
        /// </summary>
        public string thumbnailFileName;

        /// <summary>
        /// 骨格解析で使用可能と判定した攻撃から選んだ4種類の攻撃モーション
        /// </summary>
        public List<MotionType> attackMotions = new List<MotionType>();

        /// <summary>
        /// 育成途中の進行データ
        /// </summary>
        public TrainingSlotProgress trainingProgress;
    }

    /// <summary>
    /// 全スロット(最大10個)のセーブデータ
    /// JsonUtilityでシリアライズするためのコンテナ
    /// </summary>
    [System.Serializable]
    public class ClayModelSaveData
    {
        /// <summary>
        /// 保存できるスロットの最大数
        /// </summary>
        public const int SlotCount = ModelSavePoolSettings.SlotCount;

        /// <summary>
        /// 各スロットのデータ(要素数はSlotCount)
        /// </summary>
        public List<ModelSaveSlot> slots = new List<ModelSaveSlot>();
    }
}