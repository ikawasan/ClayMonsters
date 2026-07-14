using System.IO;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// タイトルロゴPNGのチェッカー背景を透過化する
    /// </summary>
    public static class TitleLogoAlphaUtility
    {
        private const string LogoPath = "Assets/Resources/Image/Title/ClayMonsters_Logo_Clean.png";
        private const string SourceLogoPath = "Assets/Resources/Image/Title/ClayMonsters_Logo.png";

        [MenuItem("Tools/ClayMonsters/Fix Title Logo Transparency")]
        public static void FixTitleLogoTransparency()
        {
            string fullPath = Path.GetFullPath(LogoPath);
            byte[] bytes = File.ReadAllBytes(fullPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Debug.LogError("[TitleLogoAlphaUtility] failed to load logo");
                Object.DestroyImmediate(texture);
                return;
            }

            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (IsBackground(pixel.r, pixel.g, pixel.b))
                {
                    pixels[i] = new Color32(pixel.r, pixel.g, pixel.b, 0);
                }
                else
                {
                    pixels[i] = new Color32(pixel.r, pixel.g, pixel.b, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(LogoPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[TitleLogoAlphaUtility] logo transparency fixed");
        }

        private static bool IsBackground(byte r, byte g, byte b)
        {
            int min = Mathf.Min(r, Mathf.Min(g, b));
            int max = Mathf.Max(r, Mathf.Max(g, b));
            int saturation = max - min;
            float luminance = (r + g + b) / 3f;
            if (luminance >= 236f && saturation <= 12)
            {
                return true;
            }

            return luminance >= 228f && saturation <= 6;
        }
    }
}
