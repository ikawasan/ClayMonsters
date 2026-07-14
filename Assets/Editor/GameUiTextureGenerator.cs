#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ゲーム内UI用の9スライステクスチャを生成する
/// </summary>
public static class GameUiTextureGenerator
{
    private const string OutputDirectory = "Assets/Resources/Image/GameUi";
    private const int TextureSize = 128;
    private const int SliceBorder = 24;
    private const int CornerRadius = 20;

    [MenuItem("Tools/ClayMonsters/Generate Game UI Textures")]
    public static void GenerateAll()
    {
        EnsureDirectory();

        GenerateFrameTexture(
            "GameUi_Frame.png",
            new Color32(255, 255, 255, 255),
            new Color32(210, 214, 222, 255),
            new Color32(150, 156, 168, 255));

        GenerateFrameTexture(
            "GameUi_InputField.png",
            new Color32(245, 240, 230, 255),
            new Color32(228, 222, 210, 255),
            new Color32(170, 162, 148, 255));

        GenerateFrameTexture(
            "GameUi_SlotInner.png",
            new Color32(220, 224, 232, 255),
            new Color32(180, 186, 198, 255),
            new Color32(110, 118, 132, 255));

        GenerateFrameTexture(
            "GameUi_ThumbnailFrame.png",
            new Color32(255, 248, 238, 255),
            new Color32(228, 208, 182, 255),
            new Color32(148, 108, 72, 255));

        GenerateSolidTexture(
            "GameUi_SliderFill.png",
            new Color32(255, 255, 255, 255),
            new Color32(235, 210, 170, 255),
            new Color32(190, 150, 95, 255));

        GenerateSolidTexture(
            "GameUi_SliderHandle.png",
            new Color32(255, 255, 255, 255),
            new Color32(248, 244, 236, 255),
            new Color32(200, 192, 178, 255));

        AssetDatabase.Refresh();
        ConfigureSpriteImportSettings();

        EditorUtility.DisplayDialog(
            "Game UI Textures",
            "ゲームUIテクスチャの生成を完了しました\n" + OutputDirectory,
            "OK");
    }

    private static void EnsureDirectory()
    {
        if (!Directory.Exists(OutputDirectory))
        {
            Directory.CreateDirectory(OutputDirectory);
        }
    }

    private static void GenerateFrameTexture(
        string fileName,
        Color32 centerColor,
        Color32 edgeColor,
        Color32 borderColor)
    {
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                texture.SetPixel(x, y, SampleFramePixel(x, y, centerColor, edgeColor, borderColor));
            }
        }

        texture.Apply();
        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);
        File.WriteAllBytes(Path.Combine(OutputDirectory, fileName), png);
    }

    private static void GenerateSolidTexture(
        string fileName,
        Color32 centerColor,
        Color32 edgeColor,
        Color32 borderColor)
    {
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float radius = CornerRadius;
        float half = TextureSize * 0.5f;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float signedDistance = SignedRoundedRectDistance(x + 0.5f, y + 0.5f, half, half, radius);
                if (signedDistance > 1.5f)
                {
                    texture.SetPixel(x, y, new Color32(0, 0, 0, 0));
                    continue;
                }

                float edgeFactor = Mathf.Clamp01((signedDistance + 1.5f) / (radius * 0.55f));
                Color32 body = Color32.Lerp(centerColor, edgeColor, edgeFactor);
                if (signedDistance > -2.5f)
                {
                    float borderBlend = Mathf.Clamp01((signedDistance + 2.5f) / 3.5f);
                    body = Color32.Lerp(body, borderColor, borderBlend);
                }

                float alpha = Mathf.Clamp01(1.5f - signedDistance);
                body.a = (byte)Mathf.RoundToInt(body.a * alpha);
                texture.SetPixel(x, y, body);
            }
        }

        texture.Apply();
        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);
        File.WriteAllBytes(Path.Combine(OutputDirectory, fileName), png);
    }

    private static Color32 SampleFramePixel(
        int x,
        int y,
        Color32 centerColor,
        Color32 edgeColor,
        Color32 borderColor)
    {
        float signedDistance = SignedRoundedRectDistance(
            x + 0.5f,
            y + 0.5f,
            TextureSize * 0.5f,
            TextureSize * 0.5f,
            CornerRadius);

        if (signedDistance > 1.5f)
        {
            return new Color32(0, 0, 0, 0);
        }

        float nx = (x / (float)(TextureSize - 1)) * 2f - 1f;
        float ny = (y / (float)(TextureSize - 1)) * 2f - 1f;
        float vignette = Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny))) * 1.15f);
        Color32 body = Color32.Lerp(edgeColor, centerColor, vignette);

        float topHighlight = Mathf.Clamp01((ny + 0.15f) * -1.4f);
        body = Color32.Lerp(body, Color32.Lerp(centerColor, Color.white, 0.35f), topHighlight * 0.35f);

        float bottomShadow = Mathf.Clamp01((ny - 0.10f) * 1.2f);
        body = Color32.Lerp(body, edgeColor, bottomShadow * 0.25f);

        if (signedDistance > -2.5f)
        {
            float borderBlend = Mathf.Clamp01((signedDistance + 2.5f) / 3.5f);
            body = Color32.Lerp(body, borderColor, borderBlend);
        }

        float alpha = Mathf.Clamp01(1.5f - signedDistance);
        body.a = (byte)Mathf.RoundToInt(body.a * alpha);
        return body;
    }

    private static float SignedRoundedRectDistance(float x, float y, float halfWidth, float halfHeight, float radius)
    {
        float clampedRadius = Mathf.Min(radius, Mathf.Min(halfWidth, halfHeight) - 1f);
        float localX = Mathf.Abs(x - halfWidth) - halfWidth + clampedRadius;
        float localY = Mathf.Abs(y - halfHeight) - halfHeight + clampedRadius;
        float ax = Mathf.Max(localX, 0f);
        float ay = Mathf.Max(localY, 0f);
        float outside = Mathf.Sqrt(ax * ax + ay * ay);
        float inside = Mathf.Min(Mathf.Max(localX, localY), 0f);
        return outside + inside - clampedRadius;
    }

    private static void ConfigureSpriteImportSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { OutputDirectory });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = new Vector4(
                SliceBorder,
                SliceBorder,
                SliceBorder,
                SliceBorder);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
#endif
