using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fieldモデル配下のマテリアルをFieldToonへ一括変換する
/// エディタウィンドウから各対象フォルダ・アセットを指定可能
/// </summary>
public class FieldModelMaterialConverter : EditorWindow
{
    private string modelRoot = "Assets/Resources/Field/Model";
    private string materialRoot = "Assets/Resources/Field/Material";
    private string textureRoot = "Assets/Resources/Field/Texture";
    
    private Shader toonShader;
    private Material templateMaterial;

    private const int TextureSampleSize = 64;

    [MenuItem("Tools/Field/Convert Model Materials To FieldToon")]
    public static void ShowWindow()
    {
        var window = GetWindow<FieldModelMaterialConverter>("Field Material Converter");
        window.Show();
    }

    /// <summary>
    /// バッチモード実行用
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod FieldModelMaterialConverter.ConvertAllSilent
    /// </summary>
    public static void ConvertAllSilent()
    {
        var window = CreateInstance<FieldModelMaterialConverter>();
        
        // バッチモード用のデフォルトパス設定
        window.modelRoot = "Assets/Resources/Field/Model";
        window.materialRoot = "Assets/Resources/Field/Material";
        window.textureRoot = "Assets/Resources/Field/Texture";
        window.toonShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/FieldObject/FieldToon.shader");
        window.templateMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Field/Material/M_FieldToonTemplate.mat");
        
        window.ConvertAllInternal(showDialog: false);
        AssetDatabase.SaveAssets();
    }

    private void OnEnable()
    {
        // 前回の設定を復元
        modelRoot = EditorPrefs.GetString("FMMC_ModelRoot", "Assets/Resources/Field/Model");
        materialRoot = EditorPrefs.GetString("FMMC_MaterialRoot", "Assets/Resources/Field/Material");
        textureRoot = EditorPrefs.GetString("FMMC_TextureRoot", "Assets/Resources/Field/Texture");
        
        var shaderPath = EditorPrefs.GetString("FMMC_ShaderPath", "Assets/Shaders/FieldObject/FieldToon.shader");
        var templatePath = EditorPrefs.GetString("FMMC_TemplatePath", "Assets/Resources/Field/Material/M_FieldToonTemplate.mat");

        toonShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        templateMaterial = AssetDatabase.LoadAssetAtPath<Material>(templatePath);
    }

    private void OnDisable()
    {
        // 現在の設定を保存
        EditorPrefs.SetString("FMMC_ModelRoot", modelRoot);
        EditorPrefs.SetString("FMMC_MaterialRoot", materialRoot);
        EditorPrefs.SetString("FMMC_TextureRoot", textureRoot);
        
        if (toonShader != null)
            EditorPrefs.SetString("FMMC_ShaderPath", AssetDatabase.GetAssetPath(toonShader));
        if (templateMaterial != null)
            EditorPrefs.SetString("FMMC_TemplatePath", AssetDatabase.GetAssetPath(templateMaterial));
    }

    private void OnGUI()
    {
        GUILayout.Label("Folder Settings", EditorStyles.boldLabel);
        modelRoot = DrawFolderField("Model Root", modelRoot);
        materialRoot = DrawFolderField("Material Root", materialRoot);
        textureRoot = DrawFolderField("Texture Root", textureRoot);

        GUILayout.Space(10);
        GUILayout.Label("Asset Settings", EditorStyles.boldLabel);
        toonShader = (Shader)EditorGUILayout.ObjectField("FieldToon Shader", toonShader, typeof(Shader), false);
        templateMaterial = (Material)EditorGUILayout.ObjectField("Template Material", templateMaterial, typeof(Material), false);

        GUILayout.Space(20);
        if (GUILayout.Button("Convert Materials", GUILayout.Height(30)))
        {
            if (toonShader == null)
            {
                EditorUtility.DisplayDialog("Error", "Please specify the FieldToon Shader.", "OK");
                return;
            }
            ConvertAllInternal(showDialog: true);
        }
    }

    private string DrawFolderField(string label, string path)
    {
        GUILayout.BeginHorizontal();
        path = EditorGUILayout.TextField(label, path);
        if (GUILayout.Button("Select", GUILayout.Width(60)))
        {
            string absolutePath = EditorUtility.OpenFolderPanel("Select " + label, path, "");
            if (!string.IsNullOrEmpty(absolutePath))
            {
                if (absolutePath.StartsWith(Application.dataPath))
                {
                    path = "Assets" + absolutePath.Substring(Application.dataPath.Length);
                }
                else
                {
                    Debug.LogWarning("Please select a folder within the Assets directory.");
                }
            }
        }
        GUILayout.EndHorizontal();
        return path;
    }

    private void ConvertAllInternal(bool showDialog)
    {
        if (toonShader == null)
        {
            Debug.LogError("FieldToon shader is not assigned.");
            return;
        }

        if (!Directory.Exists(modelRoot))
        {
            Debug.LogError($"Model directory not found: {modelRoot}");
            return;
        }

        if (!Directory.Exists(materialRoot))
        {
            Directory.CreateDirectory(materialRoot);
            AssetDatabase.Refresh();
        }

        var textureLookup = BuildTextureLookup();
        var fbxPaths = Directory.GetFiles(modelRoot, "*.fbx", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .OrderBy(path => path)
            .ToArray();

        PrepareModelsForMaterialRemap(fbxPaths);

        var createdCount = 0;
        var updatedCount = 0;
        var remappedCount = 0;
        var texturedCount = 0;
        var reimportJobs = new List<MaterialRemapJob>();

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var fbxPath in fbxPaths)
            {
                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"ModelImporter not found: {fbxPath}");
                    continue;
                }

                var externalMap = importer.GetExternalObjectMap();
                var fbxName = Path.GetFileNameWithoutExtension(fbxPath);
                var materialSlots = CollectMaterialSlots(fbxPath, externalMap);

                if (materialSlots.Count == 0)
                {
                    Debug.LogWarning($"No materials found in: {fbxPath}");
                    continue;
                }

                var materialRemaps = new Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object>();

                foreach (var pair in externalMap)
                {
                    if (pair.Key.type != typeof(Material))
                    {
                        materialRemaps[pair.Key] = pair.Value;
                    }
                }

                foreach (var slot in materialSlots)
                {
                    var slotName = slot.Key;
                    var sourceMaterial = slot.Value;
                    var fieldTexture = TryResolveFieldTexture(slotName, sourceMaterial, textureLookup);
                    var baseColor = fieldTexture != null
                        ? ExtractMaterialTint(sourceMaterial)
                        : ExtractBaseMapColor(sourceMaterial);
                        
                    var targetPath = ResolveMaterialAssetPath(
                        fbxName,
                        slotName,
                        externalMap,
                        sourceMaterial);

                    var targetMaterial = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
                    if (targetMaterial == null)
                    {
                        targetMaterial = new Material(toonShader)
                        {
                            name = Path.GetFileNameWithoutExtension(targetPath)
                        };
                        AssetDatabase.CreateAsset(targetMaterial, targetPath);
                        createdCount++;
                    }
                    else
                    {
                        updatedCount++;
                    }

                    ApplyFieldToonMaterial(
                        targetMaterial,
                        toonShader,
                        templateMaterial,
                        baseColor,
                        fieldTexture);

                    if (fieldTexture != null)
                    {
                        texturedCount++;
                    }

                    var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), slotName);
                    materialRemaps[identifier] = targetMaterial;
                    remappedCount++;
                }

                reimportJobs.Add(new MaterialRemapJob(importer, materialRemaps));
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();

        foreach (var job in reimportJobs)
        {
            ApplyMaterialRemaps(job);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var message =
            $"Field model material conversion finished.\nCreated: {createdCount}\nUpdated: {updatedCount}\nRemapped: {remappedCount}\nTextured: {texturedCount}";
        Debug.Log(message);

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Field Material Converter", message, "OK");
        }
    }

    private readonly struct MaterialRemapJob
    {
        public MaterialRemapJob(
            ModelImporter importer,
            Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> materialRemaps)
        {
            Importer = importer;
            MaterialRemaps = materialRemaps;
        }

        public ModelImporter Importer { get; }
        public Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> MaterialRemaps { get; }
    }

    private Dictionary<string, Texture2D> BuildTextureLookup()
    {
        var lookup = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        if (!AssetDatabase.IsValidFolder(textureRoot))
        {
            return lookup;
        }

        foreach (var path in Directory.GetFiles(textureRoot, "*.*", SearchOption.TopDirectoryOnly))
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".tga")
            {
                continue;
            }

            var normalizedPath = path.Replace('\\', '/');
            var textureName = Path.GetFileNameWithoutExtension(normalizedPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalizedPath);
            if (texture == null || lookup.ContainsKey(textureName))
            {
                continue;
            }

            lookup.Add(textureName, texture);
        }

        return lookup;
    }

    private static Texture2D TryResolveFieldTexture(
        string slotName,
        Material sourceMaterial,
        IReadOnlyDictionary<string, Texture2D> textureLookup)
    {
        if (textureLookup == null || textureLookup.Count == 0)
        {
            return null;
        }

        if (TryGetTexture(textureLookup, slotName, out var texture))
        {
            return texture;
        }

        if (sourceMaterial != null && TryGetTexture(textureLookup, sourceMaterial.name, out texture))
        {
            return texture;
        }

        return null;
    }

    private static bool TryGetTexture(
        IReadOnlyDictionary<string, Texture2D> textureLookup,
        string materialName,
        out Texture2D texture)
    {
        texture = null;
        if (string.IsNullOrEmpty(materialName))
        {
            return false;
        }

        if (textureLookup.TryGetValue(materialName, out texture))
        {
            return true;
        }

        if (materialName.StartsWith("M_", StringComparison.Ordinal) &&
            textureLookup.TryGetValue(materialName.Substring(2), out texture))
        {
            return true;
        }

        return false;
    }

    private static Dictionary<string, Material> CollectMaterialSlots(
        string fbxPath,
        IReadOnlyDictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> externalMap)
    {
        var slots = new Dictionary<string, Material>();

        foreach (var pair in externalMap)
        {
            if (pair.Key.type != typeof(Material) || pair.Value is not Material material)
            {
                continue;
            }

            slots[pair.Key.name] = material;
        }

        foreach (var material in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>())
        {
            if (string.IsNullOrEmpty(material.name) || slots.ContainsKey(material.name))
            {
                continue;
            }

            slots[material.name] = material;
        }

        return slots;
    }

    private static void PrepareModelsForMaterialRemap(IReadOnlyList<string> fbxPaths)
    {
        foreach (var fbxPath in fbxPaths)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                continue;
            }

            var externalMap = importer.GetExternalObjectMap();
            if (CollectMaterialSlots(fbxPath, externalMap).Count > 0)
            {
                continue;
            }

            EnsureEmbeddedMaterialsAvailable(importer);
        }
    }

    private static void EnsureEmbeddedMaterialsAvailable(ModelImporter importer)
    {
        if (importer.materialLocation == ModelImporterMaterialLocation.InPrefab)
        {
            return;
        }

        var assetPath = importer.assetPath;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        AssetDatabase.WriteImportSettingsIfDirty(assetPath);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    private static void ApplyMaterialRemaps(MaterialRemapJob job)
    {
        var importer = job.Importer;
        var assetPath = importer.assetPath;

        foreach (var pair in importer.GetExternalObjectMap())
        {
            if (pair.Key.type == typeof(Material))
            {
                importer.RemoveRemap(pair.Key);
            }
        }

        foreach (var pair in job.MaterialRemaps)
        {
            if (pair.Key.type == typeof(Material) && pair.Value != null)
            {
                importer.AddRemap(pair.Key, pair.Value);
            }
        }

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
        AssetDatabase.WriteImportSettingsIfDirty(assetPath);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    private string ResolveMaterialAssetPath(
        string fbxName,
        string slotName,
        IReadOnlyDictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> externalMap,
        Material sourceMaterial)
    {
        foreach (var pair in externalMap)
        {
            if (pair.Key.type == typeof(Material) &&
                pair.Key.name == slotName &&
                pair.Value is Material externalMaterial)
            {
                var externalPath = AssetDatabase.GetAssetPath(externalMaterial);
                if (!string.IsNullOrEmpty(externalPath) && externalPath.EndsWith(".mat"))
                {
                    return externalPath;
                }
            }
        }

        var sanitizedMaterialName = SanitizeAssetName(slotName);
        var sharedPath = $"{materialRoot}/M_{sanitizedMaterialName}.mat";
        var existingSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(sharedPath);
        if (existingSharedMaterial == null || existingSharedMaterial == sourceMaterial)
        {
            return sharedPath;
        }

        var preferredPath = $"{materialRoot}/M_{fbxName}_{sanitizedMaterialName}.mat";
        var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(preferredPath);
        if (existingMaterial == null || existingMaterial == sourceMaterial)
        {
            return preferredPath;
        }

        return $"{materialRoot}/M_{fbxName}_{sanitizedMaterialName}_Alt.mat";
    }

    private static void ApplyFieldToonMaterial(
        Material material,
        Shader fieldToonShader,
        Material templateMat,
        Color baseColor,
        Texture2D fieldTexture)
    {
        material.shader = fieldToonShader;
        material.SetColor("_BaseColor", baseColor);

        if (fieldTexture != null)
        {
            material.SetTexture("_BaseMap", fieldTexture);
        }
        else
        {
            material.SetTexture("_BaseMap", null);
        }

        if (templateMat != null)
        {
            CopyToonSettings(templateMat, material);
        }
        else
        {
            ApplyDefaultToonSettings(material);
        }

        EditorUtility.SetDirty(material);
    }

    private static void CopyToonSettings(Material templateMat, Material targetMaterial)
    {
        targetMaterial.SetColor("_ShadowColor", templateMat.GetColor("_ShadowColor"));
        targetMaterial.SetColor("_RimColor", templateMat.GetColor("_RimColor"));
        targetMaterial.SetFloat("_ToonThreshold", templateMat.GetFloat("_ToonThreshold"));
        targetMaterial.SetFloat("_ToonSmoothness", templateMat.GetFloat("_ToonSmoothness"));
        targetMaterial.SetFloat("_RimPower", templateMat.GetFloat("_RimPower"));
    }

    private static void ApplyDefaultToonSettings(Material material)
    {
        material.SetColor("_ShadowColor", new Color(0.75f, 0.6f, 0.5f, 1f));
        material.SetColor("_RimColor", new Color(1f, 0.85f, 0.5f, 1f));
        material.SetFloat("_ToonThreshold", 0f);
        material.SetFloat("_ToonSmoothness", 0.1f);
        material.SetFloat("_RimPower", 3f);
    }

    private static Color ExtractMaterialTint(Material material)
    {
        if (material == null)
        {
            return Color.white;
        }

        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.GetColor("_Color");
        }

        return Color.white;
    }

    private static Color ExtractBaseMapColor(Material material)
    {
        var tint = ExtractMaterialTint(material);
        var baseMap = GetBaseMapTexture(material);
        if (baseMap == null)
        {
            return tint;
        }

        var textureColor = SampleAverageColor(baseMap);
        return MultiplyColors(tint, textureColor);
    }

    private static UnityEngine.Texture GetBaseMapTexture(Material material)
    {
        if (material.HasProperty("_BaseMap"))
        {
            var baseMap = material.GetTexture("_BaseMap");
            if (baseMap != null)
            {
                return baseMap;
            }
        }

        if (material.HasProperty("_MainTex"))
        {
            return material.GetTexture("_MainTex");
        }

        return null;
    }

    private static Color SampleAverageColor(UnityEngine.Texture source)
    {
        var previousActive = RenderTexture.active;
        var renderTexture = RenderTexture.GetTemporary(
            TextureSampleSize,
            TextureSampleSize,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);

        try
        {
            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;

            var readableTexture = new Texture2D(
                TextureSampleSize,
                TextureSampleSize,
                TextureFormat.RGBA32,
                false,
                false);

            readableTexture.ReadPixels(new Rect(0, 0, TextureSampleSize, TextureSampleSize), 0, 0);
            readableTexture.Apply();

            var pixels = readableTexture.GetPixels();
            UnityEngine.Object.DestroyImmediate(readableTexture);

            if (pixels.Length == 0)
            {
                return Color.white;
            }

            var sum = Color.black;
            foreach (var pixel in pixels)
            {
                sum += pixel;
            }

            return sum / pixels.Length;
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    private static Color MultiplyColors(Color left, Color right)
    {
        return new Color(
            left.r * right.r,
            left.g * right.g,
            left.b * right.b,
            left.a * right.a);
    }

    private static string SanitizeAssetName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = name.Select(character =>
            invalidChars.Contains(character) ? '_' : character).ToArray();
        return new string(chars);
    }
}