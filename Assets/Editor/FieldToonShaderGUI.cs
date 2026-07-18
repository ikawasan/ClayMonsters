using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// FieldToonの裏面表示トグルとCull同期を行う
/// </summary>
public sealed class FieldToonShaderGUI : ShaderGUI
{
    private static readonly int CullId = Shader.PropertyToID("_Cull");

    private const string ShowBackFacesOnlyName = "_ShowBackFacesOnly";
    private const string CullName = "_Cull";

    /// <inheritdoc/>
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        MaterialProperty showBackFacesOnly = FindProperty(ShowBackFacesOnlyName, properties, false);
        if (showBackFacesOnly != null)
        {
            EditorGUI.BeginChangeCheck();
            materialEditor.ShaderProperty(showBackFacesOnly, showBackFacesOnly.displayName);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (Material material in materialEditor.targets)
                {
                    ApplyCull(material, showBackFacesOnly.floatValue);
                }
            }
            else
            {
                foreach (Material material in materialEditor.targets)
                {
                    ApplyCull(material, material.GetFloat(ShowBackFacesOnlyName));
                }
            }
        }

        foreach (MaterialProperty property in properties)
        {
            if (property.name == ShowBackFacesOnlyName || property.name == CullName)
            {
                continue;
            }

            materialEditor.ShaderProperty(property, property.displayName);
        }
    }

    private static void ApplyCull(Material material, float showBackFacesOnly)
    {
        bool showBack = showBackFacesOnly > 0.5f;
        float cull = showBack
            ? (float)CullMode.Front
            : (float)CullMode.Back;
        material.SetFloat(CullId, cull);
        CoreUtils.SetKeyword(material, "_SHOW_BACK_FACES_ONLY", showBack);
    }
}
