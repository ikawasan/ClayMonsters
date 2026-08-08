using Cysharp.Threading.Tasks;
using Localization;
using SaveData;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Scene.BattlePVPScene.Network
{
    /// <summary>
    /// 対人戦モデルのGZip転送ペイロードを構築と検証と展開する
    /// </summary>
    public static class BattlePvpModelTransfer
    {
        /// <summary>
        /// 回線上の転送バイト上限
        /// 実測最大圧縮約5MBに余白を加えた値
        /// </summary>
        public const int MaxTransferByteCount = 16 * 1024 * 1024;

        /// <summary>
        /// 展開後glbの上限
        /// 実測最大約33MBに余白を加えた値
        /// </summary>
        public const int MaxUncompressedByteCount = 64 * 1024 * 1024;

        /// <summary>
        /// 送信用に組み立てたペイロード
        /// </summary>
        public readonly struct Payload
        {
            /// <summary>
            /// 送信用バイナリ(GZipまたは生glb)
            /// </summary>
            public byte[] WireBytes { get; }

            /// <summary>
            /// 展開後サイズ
            /// </summary>
            public int UncompressedByteCount { get; }

            /// <summary>
            /// WireBytesがGZipか
            /// </summary>
            public bool IsGzipCompressed { get; }

            /// <summary>
            /// 送信用ペイロードを作る
            /// </summary>
            public Payload(byte[] wireBytes, int uncompressedByteCount, bool isGzipCompressed)
            {
                WireBytes = wireBytes ?? Array.Empty<byte>();
                UncompressedByteCount = uncompressedByteCount;
                IsGzipCompressed = isGzipCompressed;
            }

            /// <summary>
            /// 送信可能な構成か
            /// </summary>
            public bool IsValid =>
                WireBytes != null
                && WireBytes.Length > 0
                && UncompressedByteCount > 0
                && WireBytes.Length <= MaxTransferByteCount
                && UncompressedByteCount <= MaxUncompressedByteCount;
        }

        /// <summary>
        /// セーブファイルから送信用ペイロードを作る
        /// 圧縮保存があればそのまま使い無ければ展開済みをGZipする
        /// UnityのパスAPIはメインスレッドでのみ触る
        /// </summary>
        /// <param name="glbFileName">論理glbファイル名</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>送信用ペイロード</returns>
        public static async UniTask<Payload> CreateFromSaveFileAsync(
            string glbFileName,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(glbFileName))
            {
                throw new InvalidOperationException("[BattlePvpTransfer] glbファイル名が空です");
            }

            // persistentDataPathはメインスレッド限定
            byte[] compressedStorage = ModelSaveStorage.TryReadCompressedStorageBytes(glbFileName);
            byte[] rawGlb = null;
            if (compressedStorage == null || compressedStorage.Length == 0)
            {
                rawGlb = ModelSaveStorage.ReadAllBytes(glbFileName);
            }

            return await UniTask.RunOnThreadPool(
                () => BuildPayloadFromLoadedBytes(glbFileName, compressedStorage, rawGlb),
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// セーブファイルから送信用ペイロードを作る(同期メインスレッド向け)
        /// </summary>
        /// <param name="glbFileName">論理glbファイル名</param>
        /// <returns>送信用ペイロード</returns>
        public static Payload CreateFromSaveFile(string glbFileName)
        {
            if (string.IsNullOrEmpty(glbFileName))
            {
                throw new InvalidOperationException("[BattlePvpTransfer] glbファイル名が空です");
            }

            byte[] compressedStorage = ModelSaveStorage.TryReadCompressedStorageBytes(glbFileName);
            byte[] rawGlb = null;
            if (compressedStorage == null || compressedStorage.Length == 0)
            {
                rawGlb = ModelSaveStorage.ReadAllBytes(glbFileName);
            }

            return BuildPayloadFromLoadedBytes(glbFileName, compressedStorage, rawGlb);
        }

        /// <summary>
        /// 読込済みバイト列から送信用ペイロードを構築する
        /// Unity APIを呼ばないためワーカスレッドでも安全
        /// </summary>
        private static Payload BuildPayloadFromLoadedBytes(
            string glbFileName,
            byte[] compressedStorage,
            byte[] rawGlb)
        {
            if (compressedStorage != null && compressedStorage.Length > 0)
            {
                byte[] rawFromCompressed = ModelSaveStorage.DecompressGzipBytes(compressedStorage);
                if (rawFromCompressed == null || rawFromCompressed.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"[BattlePvpTransfer] 圧縮セーブの展開に失敗しました file={glbFileName}");
                }

                ValidateUncompressedLength(rawFromCompressed.Length);
                ValidateTransferLength(compressedStorage.Length);
                return new Payload(compressedStorage, rawFromCompressed.Length, isGzipCompressed: true);
            }

            if (rawGlb == null || rawGlb.Length == 0)
            {
                throw new InvalidOperationException(
                    $"[BattlePvpTransfer] glbが読めません file={glbFileName}");
            }

            return CreateFromRawGlb(rawGlb);
        }

        /// <summary>
        /// 生glbから送信用GZipペイロードを作る
        /// </summary>
        /// <param name="rawGlb">展開済みglb</param>
        /// <returns>送信用ペイロード</returns>
        public static Payload CreateFromRawGlb(byte[] rawGlb)
        {
            if (rawGlb == null || rawGlb.Length == 0)
            {
                throw new InvalidOperationException("[BattlePvpTransfer] 生glbが空です");
            }

            ValidateUncompressedLength(rawGlb.Length);
            byte[] wire = ModelSaveStorage.CompressToGzipBytes(rawGlb);
            if (wire == null || wire.Length == 0)
            {
                throw new InvalidOperationException("[BattlePvpTransfer] GZip圧縮に失敗しました");
            }

            ValidateTransferLength(wire.Length);
            return new Payload(wire, rawGlb.Length, isGzipCompressed: true);
        }

        /// <summary>
        /// 受信ワイヤペイロードを展開済みglbへ戻す
        /// </summary>
        /// <param name="wireBytes">受信バイナリ</param>
        /// <param name="meta">メタ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>展開済みglb</returns>
        public static async UniTask<byte[]> DecodeWirePayloadAsync(
            byte[] wireBytes,
            BattlePvpRemoteModelMeta meta,
            CancellationToken cancellationToken)
        {
            return await UniTask.RunOnThreadPool(
                () => DecodeWirePayload(wireBytes, meta),
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 受信ワイヤペイロードを展開済みglbへ戻す(同期)
        /// </summary>
        /// <param name="wireBytes">受信バイナリ</param>
        /// <param name="meta">メタ</param>
        /// <returns>展開済みglb</returns>
        public static byte[] DecodeWirePayload(byte[] wireBytes, BattlePvpRemoteModelMeta meta)
        {
            if (wireBytes == null || wireBytes.Length == 0)
            {
                throw new InvalidOperationException("[BattlePvpTransfer] 受信ペイロードが空です");
            }

            ValidateTransferLength(wireBytes.Length);

            bool isGzip = meta != null && meta.isGzipCompressed;
            if (!isGzip && LooksLikeGzip(wireBytes))
            {
                // 旧互換メタ欠落時もGZipマジックで判定する
                isGzip = true;
            }

            byte[] raw = isGzip
                ? ModelSaveStorage.DecompressGzipBytes(wireBytes)
                : wireBytes;
            if (raw == null || raw.Length == 0)
            {
                throw new InvalidOperationException("[BattlePvpTransfer] 受信ペイロードの展開に失敗しました");
            }

            ValidateUncompressedLength(raw.Length);
            if (meta != null
                && meta.uncompressedByteCount > 0
                && meta.uncompressedByteCount != raw.Length)
            {
                throw new InvalidOperationException(
                    "[BattlePvpTransfer] 展開サイズがメタと一致しません"
                    + $" meta={meta.uncompressedByteCount}"
                    + $" actual={raw.Length}");
            }

            return raw;
        }

        /// <summary>
        /// 転送サイズ超過メッセージ
        /// </summary>
        /// <param name="byteCount">実サイズ</param>
        /// <returns>文言</returns>
        public static string BuildTransferTooLargeMessage(int byteCount)
        {
            float sizeMb = byteCount / (1024f * 1024f);
            float maxMb = MaxTransferByteCount / (1024f * 1024f);
            string message = LocalizedText.GetOrFallback(
                GameTextKeys.BattlePvpModelTooLarge,
                "モデルが大きすぎて送信できません ({sizeMb}MB / 上限{maxMb}MB)",
                new Dictionary<string, object>
                {
                    { "sizeMb", sizeMb.ToString("0.0") },
                    { "maxMb", maxMb.ToString("0") }
                });
            Debug.LogError(
                $"[BattlePvpTransfer] 転送サイズ超過 size={byteCount} max={MaxTransferByteCount}");
            return message;
        }

        /// <summary>
        /// 展開サイズ超過メッセージ
        /// </summary>
        /// <param name="byteCount">実サイズ</param>
        /// <returns>文言</returns>
        public static string BuildUncompressedTooLargeMessage(int byteCount)
        {
            float sizeMb = byteCount / (1024f * 1024f);
            float maxMb = MaxUncompressedByteCount / (1024f * 1024f);
            string message = LocalizedText.GetOrFallback(
                GameTextKeys.BattlePvpModelTooLarge,
                "モデルが大きすぎて送信できません ({sizeMb}MB / 上限{maxMb}MB)",
                new Dictionary<string, object>
                {
                    { "sizeMb", sizeMb.ToString("0.0") },
                    { "maxMb", maxMb.ToString("0") }
                });
            Debug.LogError(
                $"[BattlePvpTransfer] 展開サイズ超過 size={byteCount} max={MaxUncompressedByteCount}");
            return message;
        }

        private static void ValidateTransferLength(int byteCount)
        {
            if (byteCount <= 0 || byteCount > MaxTransferByteCount)
            {
                float sizeMb = byteCount / (1024f * 1024f);
                float maxMb = MaxTransferByteCount / (1024f * 1024f);
                // ワーカスレッドから呼ばれるためLocalizedTextは使わない
                throw new InvalidOperationException(
                    $"[BattlePvpTransfer] 転送サイズ超過 sizeMb={sizeMb:0.0} maxMb={maxMb:0}");
            }
        }

        private static void ValidateUncompressedLength(int byteCount)
        {
            if (byteCount <= 0 || byteCount > MaxUncompressedByteCount)
            {
                float sizeMb = byteCount / (1024f * 1024f);
                float maxMb = MaxUncompressedByteCount / (1024f * 1024f);
                // ワーカスレッドから呼ばれるためLocalizedTextは使わない
                throw new InvalidOperationException(
                    $"[BattlePvpTransfer] 展開サイズ超過 sizeMb={sizeMb:0.0} maxMb={maxMb:0}");
            }
        }

        private static bool LooksLikeGzip(byte[] bytes)
        {
            return bytes != null
                && bytes.Length >= 2
                && bytes[0] == 0x1f
                && bytes[1] == 0x8b;
        }
    }
}
