using System.Collections.Generic;
using ClayEditor.Rigging;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// モデルのステータス(HP・攻撃力・防御力・速度・命中)
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

        /// <summary>
        /// 命中
        /// </summary>
        public int hit;

        /// <summary>
        /// ステータスのコピーを返す
        /// </summary>
        /// <returns>コピーしたModelStatus</returns>
        public ModelStatus Clone()
        {
            return new ModelStatus
            {
                hp = hp,
                attack = attack,
                defense = defense,
                speed = speed,
                hit = hit
            };
        }

        /// <summary>
        /// nullなら空のModelStatusを返す
        /// </summary>
        /// <param name="status">元のステータス</param>
        /// <returns>コピーまたは新規インスタンス</returns>
        public static ModelStatus CloneOrDefault(ModelStatus status)
        {
            return status != null ? status.Clone() : new ModelStatus();
        }
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
        /// モデルのステータス(表示・基準用)
        /// </summary>
        public ModelStatus status = new ModelStatus();

        /// <summary>
        /// 敵の強さ段階ステータスが用意されているか
        /// </summary>
        public bool hasEnemyStrengthStatuses;

        /// <summary>
        /// 敵強さバランスの改訂番号
        /// </summary>
        public int enemyStrengthBalanceVersion;

        /// <summary>
        /// 敵の弱いステータス
        /// </summary>
        public ModelStatus statusWeak = new ModelStatus();

        /// <summary>
        /// 敵の普通ステータス
        /// </summary>
        public ModelStatus statusNormal = new ModelStatus();

        /// <summary>
        /// 敵の強いステータス
        /// </summary>
        public ModelStatus statusStrong = new ModelStatus();

        /// <summary>
        /// 敵の超強いステータス
        /// </summary>
        public ModelStatus statusVeryStrong = new ModelStatus();

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
    /// モデルセーブデータのコンテナ
    /// 要素数はプールごとにModelSavePoolSettings.GetSlotCountが正
    /// </summary>
    [System.Serializable]
    public class ClayModelSaveData
    {
        /// <summary>
        /// 互換用の旧最大スロット数(未育成プールと同値)
        /// </summary>
        public const int SlotCount = ModelSavePoolSettings.PlayerSlotCount;

        /// <summary>
        /// 各スロットのデータ
        /// </summary>
        public List<ModelSaveSlot> slots = new List<ModelSaveSlot>();
    }
}