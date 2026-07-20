using UnityEngine;

namespace Setting
{
    /// <summary>
    /// ROM敵保存Build Profile向けの常時FPS表示
    /// CLAY_ENABLE_ENEMY_SAVEビルドでのみ有効化する
    /// </summary>
    public sealed class BuildProfileFpsDisplay : MonoBehaviour
    {
#if CLAY_ENABLE_ENEMY_SAVE
        private const float SmoothFactor = 0.1f;
        private const float Margin = 12f;
        private const float LabelWidth = 120f;
        private const float LabelHeight = 36f;

        private float smoothedDeltaTime = 0.016f;
        private GUIStyle labelStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<BuildProfileFpsDisplay>() != null)
            {
                return;
            }

            var host = new GameObject(nameof(BuildProfileFpsDisplay));
            Object.DontDestroyOnLoad(host);
            host.AddComponent<BuildProfileFpsDisplay>();
        }

        private void Update()
        {
            smoothedDeltaTime += (Time.unscaledDeltaTime - smoothedDeltaTime) * SmoothFactor;
        }

        private void OnGUI()
        {
            EnsureStyle();
            float fps = smoothedDeltaTime > 0.0001f ? 1f / smoothedDeltaTime : 0f;
            Rect rect = new Rect(Screen.width - LabelWidth - Margin, Margin, LabelWidth, LabelHeight);
            GUI.Label(rect, $"{fps:0} FPS", labelStyle);
        }

        private void EnsureStyle()
        {
            if (labelStyle != null)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperRight,
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;
        }
#endif
    }
}
