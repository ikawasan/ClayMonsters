using UnityEngine;

namespace Scene.BattlePVPScene
{
    /// <summary>
    /// BattlePVPSceneコンテンツルートをDontDestroyOnLoadへ移しUnityシーンアンロードから守る
    /// LifetimeScopeは子BattlePvpDiに分離済みであること
    /// </summary>
    public static class BattlePvpContentPersistence
    {
        /// <summary>
        /// コンテンツルートをDontDestroyOnLoadへ移す
        /// </summary>
        public static void EnsurePersisted(GameObject contentRoot)
        {
            if (contentRoot == null)
            {
                return;
            }

            if (contentRoot.scene.name == "DontDestroyOnLoad")
            {
                return;
            }

            if (contentRoot.GetComponent<BattlePVPScene>() == null)
            {
                Debug.LogWarning("[BattlePvpScene] コンテンツ永続化対象がBattlePVPSceneルートではありません");
                return;
            }

            if (contentRoot.GetComponent<BattlePVPLifetimeScope>() != null)
            {
                Debug.LogError(
                    "[BattlePvpScene] LifetimeScopeがルートに残っています"
                    + " BattlePvpDiへの分離後に永続化してください");
                return;
            }

            Object.DontDestroyOnLoad(contentRoot);
            Debug.Log(
                "[BattlePvpScene] コンテンツルートをDontDestroyOnLoadへ移動"
                + $" childCount={contentRoot.transform.childCount}");
            BattlePvpSceneDiagnostics.LogState("コンテンツDDOL移動後");
        }
    }
}
