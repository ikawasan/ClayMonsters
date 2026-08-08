using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using SaveData;
using System;
using System.Collections.Generic;
using System.Threading;
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
        /// ペイロードがGZip圧縮されているか
        /// </summary>
        public bool isGzipCompressed;

        /// <summary>
        /// 展開後のglbバイト数
        /// </summary>
        public int uncompressedByteCount;

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
        /// 転送情報を埋め込む
        /// </summary>
        /// <param name="payload">送信用ペイロード</param>
        public void ApplyTransferPayload(BattlePvpModelTransfer.Payload payload)
        {
            isGzipCompressed = payload.IsGzipCompressed;
            uncompressedByteCount = payload.UncompressedByteCount;
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
        private readonly byte[] wireBytes;
        private byte[] glbBytes;
        private bool decodeFailed;
        private string decodeError;

        /// <summary>
        /// メタ情報
        /// </summary>
        public BattlePvpRemoteModelMeta Meta { get; }

        /// <summary>
        /// 回線受信が完了しているか
        /// </summary>
        public bool IsWireReady => wireBytes != null && wireBytes.Length > 0;

        /// <summary>
        /// 展開済みglbバイナリ
        /// </summary>
        public byte[] GlbBytes => glbBytes ?? Array.Empty<byte>();

        /// <summary>
        /// 展開済みで利用可能か
        /// </summary>
        public bool IsValid =>
            !decodeFailed
            && glbBytes != null
            && glbBytes.Length > 0;

        /// <summary>
        /// 展開エラー内容
        /// </summary>
        public string DecodeError => decodeError ?? string.Empty;

        /// <summary>
        /// ワイヤ受信済みモデルを保持する
        /// </summary>
        /// <param name="meta">メタ</param>
        /// <param name="wireBytes">回線上のバイナリ</param>
        public BattlePvpReceivedRemoteModel(BattlePvpRemoteModelMeta meta, byte[] wireBytes)
        {
            Meta = meta ?? new BattlePvpRemoteModelMeta();
            this.wireBytes = wireBytes ?? Array.Empty<byte>();
        }

        /// <summary>
        /// ワイヤペイロードを展開済みglbへ変換する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask DecodeAsync(CancellationToken cancellationToken)
        {
            if (IsValid || decodeFailed)
            {
                return;
            }

            try
            {
                glbBytes = await BattlePvpModelTransfer.DecodeWirePayloadAsync(
                    wireBytes,
                    Meta,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                decodeFailed = true;
                decodeError = exception.Message;
                glbBytes = null;
                Debug.LogError($"[BattlePvpTransfer] 受信モデル展開失敗 {exception.Message}");
            }
        }
    }
}
