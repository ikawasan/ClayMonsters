#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TrainingシーンHUD用の9スライステクスチャを生成する
/// </summary>
public static class TrainingUiTextureGenerator
{
    private const string OutputDirectory = "Assets/Resources/Image/Training";
    private const int SliceBorderLarge = 24;
    private const int SliceBorderSmall = 10;
    private const int CornerRadiusLarge = 20;
    private const int CornerRadiusSmall = 12;

    [MenuItem("Tools/ClayMonsters/Generate Training UI Textures")]
    public static void GenerateAll()
    {
        EnsureDirectory();

        GenerateFrameTexture(
            "TrainingUi_HudHeader.png",
            256,
            96,
            SliceBorderLarge,
            CornerRadiusLarge,
            new Color32(58, 64, 82, 245),
            new Color32(42, 48, 64, 245),
            new Color32(24, 28, 38, 255));

        GenerateFrameTexture(
            "TrainingUi_LogPanel.png",
            256,
            128,
            SliceBorderLarge,
            CornerRadiusLarge,
            new Color32(48, 54, 70, 238),
            new Color32(34, 40, 54, 238),
            new Color32(18, 22, 32, 255));

        GenerateFrameTexture(
            "TrainingUi_RestButton.png",
            128,
            128,
            SliceBorderLarge,
            CornerRadiusLarge,
            new Color32(118, 168, 198, 255),
            new Color32(88, 138, 168, 255),
            new Color32(52, 88, 112, 255));

        GenerateSolidTexture(
            "TrainingUi_StaminaTrack.png",
            128,
            32,
            SliceBorderSmall,
            CornerRadiusSmall,
            new Color32(34, 38, 48, 255),
            new Color32(24, 28, 36, 255),
            new Color32(14, 16, 22, 255));

        GenerateSolidTexture(
            "TrainingUi_StaminaFill.png",
            128,
            32,
            SliceBorderSmall,
            CornerRadiusSmall,
            new Color32(255, 236, 196, 255),
            new Color32(236, 188, 118, 255),
            new Color32(196, 140, 72, 255));

        AssetDatabase.Refresh();
        ConfigureSpriteImportSettings();

        EditorUtility.DisplayDialog(
            "Training UI Textures",
            "Training UIテクスチャの生成を完了しました\n" + OutputDirectory,
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
        int width,
        int height,
        int sliceBorder,
        int cornerRadius,
        Color32 centerColor,
        Color32 edgeColor,
        Color32 borderColor)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float radius = Mathf.Min(cornerRadius, Mathf.Min(halfWidth, halfHeight) - 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(
                    x,
                    y,
                    SampleFramePixel(x, y, halfWidth, halfHeight, radius, centerColor, edgeColor, borderColor));
            }
        }

        WriteTexture(fileName, texture);
    }

    private static void GenerateSolidTexture(
        string fileName,
        int width,
        int height,
        int sliceBorder,
        int cornerRadius,
        Color32 centerColor,
        Color32 edgeColor,
        Color32 borderColor)
    {
        GenerateFrameTexture(
            fileName,
            width,
            height,
            sliceBorder,
            cornerRadius,
            centerColor,
            edgeColor,
            borderColor);
    }

    private static Color32 SampleFramePixel(
        int x,
        int y,
        float halfWidth,
        float halfHeight,
        float cornerRadius,
        Color32 centerColor,
        Color32 edgeColor,
        Color32 borderColor)
    {
        float signedDistance = SignedRoundedRectDistance(
            x + 0.5f,
            y + 0.5f,
            halfWidth,
            halfHeight,
            cornerRadius);

        if (signedDistance > 1.5f)
        {
            return new Color32(0, 0, 0, 0);
        }

        float nx = halfWidth > 0f ? (x / (halfWidth * 2f - 1f)) * 2f - 1f : 0f;
        float ny = halfHeight > 0f ? (y / (halfHeight * 2f - 1f)) * 2f - 1f : 0f;
        float vignette = Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny))) * 1.15f);
        Color32 body = Color32.Lerp(edgeColor, centerColor, vignette);

        float topHighlight = Mathf.Clamp01((ny + 0.15f) * -1.4f);
        body = Color32.Lerp(body, Color32.Lerp(centerColor, Color.white, 0.28f), topHighlight * 0.30f);

        float bottomShadow = Mathf.Clamp01((ny - 0.10f) * 1.2f);
        body = Color32.Lerp(body, edgeColor, bottomShadow * 0.22f);

        if (signedDistance > -2.5f)
        {
            float borderBlend = Mathf.Clamp01((signedDistance + 2.5f) / 3.5f);
            body = Color32.Lerp(body, borderColor, borderBlend);
        }

        float alpha = Mathf.Clamp01(1.5f - signedDistance);
        body.a = (byte)Mathf.RoundToInt(body.a * alpha);
        return body;
    }

    private static float SignedRoundedRectDistance(
        float x,
        float y,
        float halfWidth,
        float halfHeight,
        float radius)
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

    private static void WriteTexture(string fileName, Texture2D texture)
    {
        texture.Apply();
        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);
        File.WriteAllBytes(Path.Combine(OutputDirectory, fileName), png);
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

            bool isSmallBar = path.Contains("Stamina");
            float border = isSmallBar ? SliceBorderSmall : SliceBorderLarge;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = new Vector4(border, border, border, border);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
#endif
