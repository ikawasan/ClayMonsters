using System.Text;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Scene.BattlePVPScene
{
    /// <summary>
    /// BattlePVPシーンのルートオブジェクト数とBattlePVPSceneの所在をログ出力する
    /// </summary>
    public static class BattlePvpSceneDiagnostics
    {
        private const string SceneName = "BattlePVP";

        /// <summary>
        /// 指定フェーズのシーン構成をログ出力する
        /// </summary>
        public static void LogState(string phase)
        {
            var builder = new StringBuilder();
            builder.Append($"[BattlePvpScene] {phase}");
            AppendAllBattlePvpSceneInstances(builder);

            GameObject battlePvpSceneRoot = FindBattlePvpSceneRoot();
            builder.Append($" BattlePVPSceneFound={(battlePvpSceneRoot != null)}");
            if (battlePvpSceneRoot != null)
            {
                builder.Append($" ownerScene={battlePvpSceneRoot.scene.name}");
                builder.Append($" activeSelf={battlePvpSceneRoot.activeSelf}");
                builder.Append($" activeInHierarchy={battlePvpSceneRoot.activeInHierarchy}");
                AppendChildSummary(builder, battlePvpSceneRoot.transform);
            }
            else
            {
                builder.Append(" DDOLBattlePVPScene=missing");
            }

            AppendLoadSlotCanvasState(builder);
            Debug.Log(builder.ToString());
        }

        private static void AppendAllBattlePvpSceneInstances(StringBuilder builder)
        {
            int sceneCount = SceneManager.sceneCount;
            int battlePvpSceneCount = 0;
            for (int i = 0; i < sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene loadedScene = SceneManager.GetSceneAt(i);
                if (loadedScene.name != SceneName)
                {
                    continue;
                }

                battlePvpSceneCount++;
                GameObject[] roots = loadedScene.GetRootGameObjects();
                builder.Append($" [UnityScene#{battlePvpSceneCount} roots={roots.Length} loaded={loadedScene.isLoaded}]");
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    GameObject root = roots[rootIndex];
                    builder.Append($" ({root.name} active={root.activeSelf})");
                }
            }

            if (battlePvpSceneCount == 0)
            {
                builder.Append(" UnitySceneBattlePVP=missing");
            }
        }

        private static void AppendChildSummary(StringBuilder builder, Transform root)
        {
            builder.Append(" children=[");
            int childCount = root.childCount;
            int limit = Mathf.Min(childCount, 12);
            for (int i = 0; i < limit; i++)
            {
                Transform child = root.GetChild(i);
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(child.name);
                builder.Append(':');
                builder.Append(child.gameObject.activeSelf ? 'A' : 'I');
            }

            if (childCount > limit)
            {
                builder.Append(",…");
            }

            builder.Append(']');
        }

        private static void AppendLoadSlotCanvasState(StringBuilder builder)
        {
            Canvas loadSlotCanvas = FindLoadSlotCanvas();
            if (loadSlotCanvas == null)
            {
                builder.Append(" LoadSlotCanvas=missing");
                return;
            }

            builder.Append($" LoadSlotCanvasScene={loadSlotCanvas.gameObject.scene.name}");
            builder.Append($" loadSlotActive={loadSlotCanvas.gameObject.activeInHierarchy}");
            builder.Append($" canvasEnabled={loadSlotCanvas.enabled}");
            builder.Append($" renderMode={loadSlotCanvas.renderMode}");
        }

        private static Canvas FindLoadSlotCanvas()
        {
            GameObject sceneRoot = FindBattlePvpSceneRoot();
            if (sceneRoot == null)
            {
                return null;
            }

            LoadSlotView loadSlotView = sceneRoot.GetComponentInChildren<LoadSlotView>(true);
            return loadSlotView != null ? loadSlotView.GetComponent<Canvas>() : null;
        }

        /// <summary>
        /// BattlePVPSceneルートを検索する
        /// </summary>
        public static GameObject FindBattlePvpSceneRoot()
        {
            BattlePVPScene scene = Object.FindFirstObjectByType<BattlePVPScene>(FindObjectsInactive.Include);
            return scene != null ? scene.gameObject : null;
        }
    }
}
