using ClayEditor.Rigging;
using SaveData;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scene.BattlePVPScene.Network
{
    /// <summary>
    /// 対人戦で相手へ送るモデルメタ情報
    /// </summary>
    [Serializable]
    public sealed class BattlePvpRemoteModelMeta
    {
        /// <summary>
        /// モデル名
        /// </summary>
        public string modelName;

        /// <summary>
        /// HP攻撃防御速度命中
        /// </summary>
        public ModelStatus status = new ModelStatus();

        /// <summary>
        /// 攻撃モーション(MotionTypeのint)
        /// </summary>
        public int[] attackMotions = Array.Empty<int>();

        /// <summary>
        /// スロットから送信用メタを作る
        /// </summary>
        /// <param name="slot">ローカルスロット</param>
        /// <returns>送信用メタ</returns>
        public static BattlePvpRemoteModelMeta FromSlot(ModelSaveSlot slot)
        {
            var meta = new BattlePvpRemoteModelMeta
            {
                modelName = slot != null ? slot.modelName : string.Empty,
                status = ModelStatus.CloneOrDefault(slot != null ? slot.status : null)
            };

            if (slot?.attackMotions == null || slot.attackMotions.Count == 0)
            {
                return meta;
            }

            meta.attackMotions = new int[slot.attackMotions.Count];
            for (int i = 0; i < slot.attackMotions.Count; i++)
            {
                meta.attackMotions[i] = (int)slot.attackMotions[i];
            }

            return meta;
        }

        /// <summary>
        /// JSONへ変換する
        /// </summary>
        /// <returns>JSON文字列</returns>
        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        /// <summary>
        /// JSONから復元する
        /// </summary>
        /// <param name="json">JSON</param>
        /// <returns>メタ情報</returns>
        public static BattlePvpRemoteModelMeta FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new BattlePvpRemoteModelMeta();
            }

            BattlePvpRemoteModelMeta meta = JsonUtility.FromJson<BattlePvpRemoteModelMeta>(json);
            return meta ?? new BattlePvpRemoteModelMeta();
        }

        /// <summary>
        /// 戦闘ロード用の一時スロットへ変換する
        /// </summary>
        /// <returns>一時スロット</returns>
        public ModelSaveSlot ToTemporarySlot()
        {
            var slot = new ModelSaveSlot
            {
                isUsed = true,
                modelName = modelName ?? string.Empty,
                status = ModelStatus.CloneOrDefault(status),
                attackMotions = new List<MotionType>()
            };

            if (attackMotions != null)
            {
                for (int i = 0; i < attackMotions.Length; i++)
                {
                    slot.attackMotions.Add((MotionType)attackMotions[i]);
                }
            }

            return slot;
        }
    }

    /// <summary>
    /// 受信済みの相手モデル
    /// </summary>
    public sealed class BattlePvpReceivedRemoteModel
    {
        /// <summary>
        /// メタ情報
        /// </summary>
        public BattlePvpRemoteModelMeta Meta { get; }

        /// <summary>
        /// glbバイナリ
        /// </summary>
        public byte[] GlbBytes { get; }

        /// <summary>
        /// 受信済みモデルを保持する
        /// </summary>
        /// <param name="meta">メタ</param>
        /// <param name="glbBytes">glb</param>
        public BattlePvpReceivedRemoteModel(BattlePvpRemoteModelMeta meta, byte[] glbBytes)
        {
            Meta = meta ?? new BattlePvpRemoteModelMeta();
            GlbBytes = glbBytes ?? Array.Empty<byte>();
        }

        /// <summary>
        /// 利用可能か
        /// </summary>
        public bool IsValid => GlbBytes != null && GlbBytes.Length > 0;
    }
}
