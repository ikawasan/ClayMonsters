#if UNITY_EDITOR
using System;
using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>
/// エディタマイグレーション用のLHButton解決ヘルパー
/// LighthouseExtendsへのコンパイル時参照を避ける
/// </summary>
internal static class SceneUiLhButtonUtility
{
    private const string LhButtonTypeName = "LighthouseExtends.UIComponent.Button.LHButton";
    private const string VisualUtilityTypeName = "UI.ClayEditor.View.TitleClayUiVisualUtility, UI.ClayEditor";

    private static Type lhButtonType;

    public static Type LhButtonType => lhButtonType ??= ResolveLhButtonType();

    public static Component FindInChildren(Transform transform, bool includeInactive = true)
    {
        if (transform == null || LhButtonType == null)
        {
            return null;
        }

        return transform.GetComponentInChildren(LhButtonType, includeInactive) as Component;
    }

    public static Component GetComponent(GameObject gameObject)
    {
        if (gameObject == null || LhButtonType == null)
        {
            return null;
        }

        return gameObject.GetComponent(LhButtonType) as Component;
    }

    public static Component AddComponent(GameObject gameObject)
    {
        if (gameObject == null || LhButtonType == null)
        {
            return null;
        }

        return gameObject.AddComponent(LhButtonType) as Component;
    }

    public static void ApplyPrimaryButton(Component button) =>
        InvokeVisualUtility("ApplyMenuButton", button);

    public static void ApplySecondaryButton(Component button) =>
        InvokeVisualUtility("ApplyMenuButton", button);

    public static void ApplyAccentButton(Component button) =>
        InvokeVisualUtility("ApplyMenuButton", button);

    public static void ApplyDangerButton(Component button) =>
        InvokeVisualUtility("ApplyDangerMenuButton", button);

    public static void ApplySlotButton(Component button) =>
        InvokeVisualUtility("ApplySlotButton", button);

    public static void ApplyMenuButton(Component button) =>
        InvokeVisualUtility("ApplyMenuButton", button);

    public static void ApplyMenuButtonForEditorBake(
        Component button,
        TextAlignmentOptions labelAlignment = TextAlignmentOptions.Center) =>
        InvokeVisualUtility(
            "ApplyMenuButtonForEditorBake",
            button,
            new object[] { button, labelAlignment },
            new[] { LhButtonType, typeof(TextAlignmentOptions) });

    private static Type ResolveLhButtonType()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(LhButtonTypeName, false);
            if (type != null)
            {
                return type;
            }
        }

        Debug.LogWarning("[SceneUiLhButtonUtility] LHButton type not found");
        return null;
    }

    private static void InvokeVisualUtility(string methodName, Component button)
    {
        if (button == null || LhButtonType == null)
        {
            return;
        }

        InvokeVisualUtility(methodName, button, new object[] { button }, new[] { LhButtonType });
    }

    private static void InvokeVisualUtility(
        string methodName,
        Component button,
        object[] args,
        Type[] parameterTypes)
    {
        if (button == null || LhButtonType == null)
        {
            return;
        }

        Type utilityType = ResolveVisualUtilityType();
        if (utilityType == null)
        {
            return;
        }

        MethodInfo method = utilityType.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        method?.Invoke(null, args);
    }

    private static Type ResolveVisualUtilityType()
    {
        Type utilityType = Type.GetType(VisualUtilityTypeName);
        if (utilityType != null)
        {
            return utilityType;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            utilityType = assembly.GetType("UI.ClayEditor.View.TitleClayUiVisualUtility", false);
            if (utilityType != null)
            {
                return utilityType;
            }
        }

        return null;
    }
}
#endif
