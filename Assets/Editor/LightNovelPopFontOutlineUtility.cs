using System.Collections.Generic;
using Extensions;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// LightNovelPOPv2 SDFをアウトライン可能なpaddingで再生成する
/// </summary>
public static class LightNovelPopFontOutlineUtility
{
    private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/LightNovelPOPv2.otf";
    private const int TargetPadding = 9;
    private const int SamplingPointSize = 44;
    private const int AtlasSize = 4096;
    private const float DefaultOutlineWidth = 0.2f;

    /// <summary>
    /// ソースフォントGUIDを直しpaddingを上げてアトラスを再生成する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/TMP/Enable LightNovelPOPv2 Outline Support")]
    public static void EnableOutlineSupport()
    {
        if (!EditorUtility.DisplayDialog(
                "LightNovelPOPv2 Outline",
                "SDFアトラスをpadding 9で再生成します\nマルチアトラスを有効化します\n完了まで時間がかかる場合があります",
                "実行",
                "キャンセル"))
        {
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("LightNovelPOPv2", "準備中", 0.05f);
            RegenerateFontAsset();
            EditorUtility.DisplayProgressBar("LightNovelPOPv2", "Outlineマテリアル作成中", 0.92f);
            CreateOrUpdateOutlineMaterial();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "完了",
                "LightNovelPOPv2のアウトライン対応再生成が完了しました",
                "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("失敗", exception.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// Outlineマテリアルだけを作り直す
    /// </summary>
    [MenuItem("Tools/ClayMonsters/TMP/Create LightNovelPOPv2 Outline Material")]
    public static void CreateOutlineMaterialMenu()
    {
        try
        {
            CreateOrUpdateOutlineMaterial();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "完了",
                "LightNovelPOPv2 SDF - Outline.mat を更新しました",
                "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("失敗", exception.Message, "OK");
        }
    }

    private static void RegenerateFontAsset()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            throw new System.InvalidOperationException("ソースフォントが見つかりません: " + SourceFontPath);
        }

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AppTmpFontUtility.FontAssetPath);
        if (fontAsset == null)
        {
            throw new System.InvalidOperationException("フォントアセットが見つかりません: " + AppTmpFontUtility.FontAssetPath);
        }

        string sourceGuid = AssetDatabase.AssetPathToGUID(SourceFontPath);
        if (string.IsNullOrEmpty(sourceGuid))
        {
            throw new System.InvalidOperationException("ソースフォントGUIDを取得できません");
        }

        List<uint> unicodeList = CollectUnicodes(fontAsset);
        if (unicodeList.Count == 0)
        {
            throw new System.InvalidOperationException("再生成対象の文字が空です");
        }

        EditorUtility.DisplayProgressBar("LightNovelPOPv2", "ソース参照を修正中", 0.12f);
        ApplySourceFontReference(fontAsset, sourceFont, sourceGuid);

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;

        EditorUtility.DisplayProgressBar("LightNovelPOPv2", "アトラスをクリア中", 0.2f);
        fontAsset.ClearFontAssetData(true);

        ApplySourceFontReference(fontAsset, sourceFont, sourceGuid);
        ApplyAtlasSettings(fontAsset, sourceGuid);

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;

        EditorUtility.DisplayProgressBar(
            "LightNovelPOPv2",
            $"グリフ再生成中({unicodeList.Count}文字)",
            0.35f);

        uint[] unicodes = unicodeList.ToArray();
        bool added = fontAsset.TryAddCharacters(unicodes, out uint[] missing, true);
        if (missing != null && missing.Length > 0)
        {
            Debug.LogWarning($"[LightNovelPOPv2] 欠落文字数: {missing.Length}");
        }

        if (!added && missing != null && missing.Length >= unicodes.Length)
        {
            throw new System.InvalidOperationException(
                "文字の追加に失敗しました ソースフォント参照とFont Engineを確認してください");
        }

        EditorUtility.DisplayProgressBar("LightNovelPOPv2", "マテリアルを更新中", 0.82f);
        UpdateFontMaterial(fontAsset);

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        fontAsset.isMultiAtlasTexturesEnabled = true;

        FontAssetCreationSettings creation = fontAsset.creationSettings;
        creation.sourceFontFileGUID = sourceGuid;
        creation.pointSize = SamplingPointSize;
        creation.padding = TargetPadding;
        creation.atlasWidth = AtlasSize;
        creation.atlasHeight = AtlasSize;
        fontAsset.creationSettings = creation;

        ApplySourceFontReference(fontAsset, sourceFont, sourceGuid);
        EditorUtility.SetDirty(fontAsset);
    }

    private static List<uint> CollectUnicodes(TMP_FontAsset fontAsset)
    {
        var unicodeList = new List<uint>(fontAsset.characterTable.Count);
        var seen = new HashSet<uint>();
        for (int i = 0; i < fontAsset.characterTable.Count; i++)
        {
            uint unicode = fontAsset.characterTable[i].unicode;
            if (unicode == 0 || !seen.Add(unicode))
            {
                continue;
            }

            unicodeList.Add(unicode);
        }

        return unicodeList;
    }

    private static void ApplySourceFontReference(TMP_FontAsset fontAsset, Font sourceFont, string sourceGuid)
    {
        SerializedObject serializedObject = new SerializedObject(fontAsset);
        SerializedProperty guidProperty = serializedObject.FindProperty("m_SourceFontFileGUID");
        if (guidProperty != null)
        {
            guidProperty.stringValue = sourceGuid;
        }

        SerializedProperty sourceFontProperty = serializedObject.FindProperty("m_SourceFontFile");
        if (sourceFontProperty != null)
        {
            sourceFontProperty.objectReferenceValue = sourceFont;
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyAtlasSettings(TMP_FontAsset fontAsset, string sourceGuid)
    {
        SerializedObject serializedObject = new SerializedObject(fontAsset);
        SerializedProperty guidProperty = serializedObject.FindProperty("m_SourceFontFileGUID");
        if (guidProperty != null)
        {
            guidProperty.stringValue = sourceGuid;
        }

        SerializedProperty paddingProperty = serializedObject.FindProperty("m_AtlasPadding");
        if (paddingProperty != null)
        {
            paddingProperty.intValue = TargetPadding;
        }

        SerializedProperty widthProperty = serializedObject.FindProperty("m_AtlasWidth");
        if (widthProperty != null)
        {
            widthProperty.intValue = AtlasSize;
        }

        SerializedProperty heightProperty = serializedObject.FindProperty("m_AtlasHeight");
        if (heightProperty != null)
        {
            heightProperty.intValue = AtlasSize;
        }

        SerializedProperty multiAtlasProperty = serializedObject.FindProperty("m_IsMultiAtlasTexturesEnabled");
        if (multiAtlasProperty != null)
        {
            multiAtlasProperty.boolValue = true;
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void UpdateFontMaterial(TMP_FontAsset fontAsset)
    {
        Material material = fontAsset.material;
        if (material == null)
        {
            return;
        }

        material.SetFloat(ShaderUtilities.ID_GradientScale, TargetPadding + 1);
        int textureWidth = AtlasSize;
        int textureHeight = AtlasSize;
        if (fontAsset.atlasTexture != null)
        {
            textureWidth = fontAsset.atlasTexture.width;
            textureHeight = fontAsset.atlasTexture.height;
        }

        material.SetFloat(ShaderUtilities.ID_TextureWidth, textureWidth);
        material.SetFloat(ShaderUtilities.ID_TextureHeight, textureHeight);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        EditorUtility.SetDirty(material);
    }

    private static void CreateOrUpdateOutlineMaterial()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AppTmpFontUtility.FontAssetPath);
        if (fontAsset == null || fontAsset.material == null)
        {
            throw new System.InvalidOperationException("フォントアセットまたはマテリアルがありません");
        }

        Material baseMaterial = fontAsset.material;
        Material outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(AppTmpFontUtility.OutlineMaterialPath);
        if (outlineMaterial == null)
        {
            outlineMaterial = new Material(baseMaterial)
            {
                name = "LightNovelPOPv2 SDF - Outline"
            };
            AssetDatabase.CreateAsset(outlineMaterial, AppTmpFontUtility.OutlineMaterialPath);
        }
        else
        {
            outlineMaterial.shader = baseMaterial.shader;
            outlineMaterial.CopyPropertiesFromMaterial(baseMaterial);
        }

        outlineMaterial.SetTexture(ShaderUtilities.ID_MainTex, baseMaterial.GetTexture(ShaderUtilities.ID_MainTex));
        outlineMaterial.SetFloat(ShaderUtilities.ID_GradientScale, baseMaterial.GetFloat(ShaderUtilities.ID_GradientScale));
        outlineMaterial.SetFloat(ShaderUtilities.ID_TextureWidth, baseMaterial.GetFloat(ShaderUtilities.ID_TextureWidth));
        outlineMaterial.SetFloat(ShaderUtilities.ID_TextureHeight, baseMaterial.GetFloat(ShaderUtilities.ID_TextureHeight));
        outlineMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, DefaultOutlineWidth);
        outlineMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        EditorUtility.SetDirty(outlineMaterial);
    }
}
