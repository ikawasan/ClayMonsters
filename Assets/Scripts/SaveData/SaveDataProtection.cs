using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// セーブ本文の暗号化と改ざん検知
    /// </summary>
    public static class SaveDataProtection
    {
        private const string EnvelopeVersion = "CMS1";
        private const int AesKeySizeBytes = 32;
        private const int IvSizeBytes = 16;

        [Serializable]
        private sealed class SealedSaveEnvelope
        {
            public string v;
            public string d;
            public string m;
        }

        /// <summary>
        /// 平文JSONを暗号化しHMAC付き封筒へ封入する
        /// </summary>
        /// <param name="plainText">平文</param>
        /// <returns>封入済みテキスト</returns>
        public static string SealText(string plainText)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText ?? string.Empty);
            ResolveKeys(out byte[] aesKey, out byte[] macKey);
            try
            {
                byte[] iv = new byte[IvSizeBytes];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(iv);
                }

                byte[] cipherBytes;
                using (var aes = Aes.Create())
                {
                    aes.KeySize = AesKeySizeBytes * 8;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = aesKey;
                    aes.IV = iv;
                    using ICryptoTransform encryptor = aes.CreateEncryptor();
                    cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                }

                byte[] payload = new byte[iv.Length + cipherBytes.Length];
                Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
                Buffer.BlockCopy(cipherBytes, 0, payload, iv.Length, cipherBytes.Length);

                byte[] mac;
                using (var hmac = new HMACSHA256(macKey))
                {
                    mac = hmac.ComputeHash(payload);
                }

                var envelope = new SealedSaveEnvelope
                {
                    v = EnvelopeVersion,
                    d = Convert.ToBase64String(payload),
                    m = Convert.ToBase64String(mac)
                };
                return JsonUtility.ToJson(envelope);
            }
            finally
            {
                ClearBytes(aesKey);
                ClearBytes(macKey);
            }
        }

        /// <summary>
        /// 封入済みテキストを検証して平文を取り出す
        /// 旧平文JSONは移行用に受け入れる
        /// </summary>
        /// <param name="sealedOrPlainText">封入済みまたは旧平文</param>
        /// <param name="plainText">平文</param>
        /// <param name="wasLegacyPlain">旧平文として受け入れたか</param>
        /// <returns>取り出せた場合true</returns>
        public static bool TryOpenText(
            string sealedOrPlainText,
            out string plainText,
            out bool wasLegacyPlain)
        {
            plainText = null;
            wasLegacyPlain = false;
            if (string.IsNullOrEmpty(sealedOrPlainText))
            {
                return false;
            }

            if (TryOpenSealedEnvelope(sealedOrPlainText, out plainText))
            {
                return true;
            }

            if (LooksLikeLegacyPlainJson(sealedOrPlainText))
            {
                // 既存セーブ移行用不正な封筒は拒否済み
                plainText = sealedOrPlainText;
                wasLegacyPlain = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 封筒形式かどうか判定する
        /// </summary>
        /// <param name="text">判定対象</param>
        /// <returns>封筒ならtrue</returns>
        public static bool IsSealedEnvelope(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            SealedSaveEnvelope envelope = JsonUtility.FromJson<SealedSaveEnvelope>(text);
            return envelope != null
                && envelope.v == EnvelopeVersion
                && !string.IsNullOrEmpty(envelope.d)
                && !string.IsNullOrEmpty(envelope.m);
        }

        private static bool TryOpenSealedEnvelope(string text, out string plainText)
        {
            plainText = null;
            SealedSaveEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<SealedSaveEnvelope>(text);
            }
            catch (Exception)
            {
                return false;
            }

            if (envelope == null
                || envelope.v != EnvelopeVersion
                || string.IsNullOrEmpty(envelope.d)
                || string.IsNullOrEmpty(envelope.m))
            {
                return false;
            }

            byte[] payload;
            byte[] mac;
            try
            {
                payload = Convert.FromBase64String(envelope.d);
                mac = Convert.FromBase64String(envelope.m);
            }
            catch (FormatException)
            {
                return false;
            }

            if (payload == null
                || mac == null
                || payload.Length <= IvSizeBytes
                || mac.Length == 0)
            {
                return false;
            }

            ResolveKeys(out byte[] aesKey, out byte[] macKey);
            try
            {
                byte[] expectedMac;
                using (var hmac = new HMACSHA256(macKey))
                {
                    expectedMac = hmac.ComputeHash(payload);
                }

                if (!FixedTimeEquals(mac, expectedMac))
                {
                    return false;
                }

                byte[] iv = new byte[IvSizeBytes];
                byte[] cipherBytes = new byte[payload.Length - IvSizeBytes];
                Buffer.BlockCopy(payload, 0, iv, 0, IvSizeBytes);
                Buffer.BlockCopy(payload, IvSizeBytes, cipherBytes, 0, cipherBytes.Length);

                byte[] plainBytes;
                using (var aes = Aes.Create())
                {
                    aes.KeySize = AesKeySizeBytes * 8;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = aesKey;
                    aes.IV = iv;
                    using ICryptoTransform decryptor = aes.CreateDecryptor();
                    plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                }

                plainText = Encoding.UTF8.GetString(plainBytes);
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            finally
            {
                ClearBytes(aesKey);
                ClearBytes(macKey);
            }
        }

        private static bool LooksLikeLegacyPlainJson(string text)
        {
            if (IsSealedEnvelope(text))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c))
                {
                    continue;
                }

                return c == '{';
            }

            return false;
        }

        private static void ResolveKeys(out byte[] aesKey, out byte[] macKey)
        {
            byte[] master = BuildMasterKey();
            try
            {
                aesKey = DeriveKey(master, 0xA5);
                macKey = DeriveKey(master, 0x5A);
            }
            finally
            {
                ClearBytes(master);
            }
        }

        private static byte[] DeriveKey(byte[] master, byte domain)
        {
            byte[] material = new byte[master.Length + 1];
            Buffer.BlockCopy(master, 0, material, 0, master.Length);
            material[master.Length] = domain;
            try
            {
                using var sha = SHA256.Create();
                return sha.ComputeHash(material);
            }
            finally
            {
                ClearBytes(material);
            }
        }

        private static byte[] BuildMasterKey()
        {
            // 分割XORで平文キーの静的露出を避ける
            byte[] partA =
            {
                0x6C, 0x1A, 0xF3, 0x48, 0x9D, 0x22, 0xB7, 0x05,
                0xE1, 0x73, 0x4C, 0x8A, 0x19, 0xD6, 0x2F, 0x91,
                0x57, 0xC0, 0x3B, 0x84, 0xAE, 0x12, 0x69, 0xF8,
                0x0D, 0x45, 0xB2, 0x76, 0x98, 0x33, 0xCA, 0x5E
            };
            byte[] partB =
            {
                0xA3, 0x5F, 0x18, 0xC7, 0x2E, 0x94, 0x61, 0xDB,
                0x07, 0xBC, 0x4A, 0xE5, 0x73, 0x10, 0x8F, 0x36,
                0xD2, 0x59, 0x8C, 0x21, 0xF4, 0x67, 0x0B, 0xAE,
                0x35, 0xC8, 0x14, 0x9A, 0x4F, 0xE0, 0x26, 0x7B
            };
            byte[] partC =
            {
                0x29, 0x87, 0xD4, 0x50, 0xFB, 0x3C, 0x6E, 0xA1,
                0x15, 0xC3, 0x8E, 0x42, 0xA7, 0x5B, 0x09, 0xED,
                0x64, 0xB8, 0x1F, 0x7C, 0xD0, 0x53, 0x96, 0x2A,
                0xE7, 0x31, 0x8B, 0x4D, 0xF2, 0x18, 0x6A, 0xC5
            };

            byte[] master = new byte[AesKeySizeBytes];
            for (int i = 0; i < master.Length; i++)
            {
                master[i] = (byte)(partA[i] ^ partB[i] ^ partC[i] ^ (byte)(i * 31 + 17));
            }

            return master;
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < left.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }

            return diff == 0;
        }

        private static void ClearBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return;
            }

            Array.Clear(bytes, 0, bytes.Length);
        }
    }
}
