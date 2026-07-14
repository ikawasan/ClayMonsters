using UnityEngine;

namespace Scene.BattlePVPScene
{
    /// <summary>
    /// BattlePVPLifetimeScopeをBattlePVPSceneルートからBattlePvpDi子オブジェクトへ分離する
    /// </summary>
    public static class BattlePvpLifetimeScopeSetup
    {
        private const string DedicatedScopeObjectName = "BattlePvpDi";

        /// <summary>
        /// 専用子オブジェクトへLifetimeScopeを分離する
        /// </summary>
        public static void EnsureDedicatedScope(Transform sceneRoot)
        {
            if (sceneRoot == null)
            {
                return;
            }

            BattlePVPLifetimeScope scopeOnRoot = sceneRoot.GetComponent<BattlePVPLifetimeScope>();
            Transform dedicatedTransform = sceneRoot.Find(DedicatedScopeObjectName);
            if (scopeOnRoot == null)
            {
                if (dedicatedTransform == null)
                {
                    Debug.LogWarning("[BattlePvpScene] BattlePvpDiが未作成です");
                }

                return;
            }

            if (scopeOnRoot.Container != null)
            {
                Debug.LogError(
                    "[BattlePvpScene] ビルド済みLifetimeScopeがルートに残っています"
                    + " Tools/ClayMonsters/Create BattlePVP Sceneを実行してください");
                return;
            }

            GameObject dedicatedObject = dedicatedTransform != null
                ? dedicatedTransform.gameObject
                : new GameObject(DedicatedScopeObjectName);
            if (dedicatedTransform == null)
            {
                dedicatedObject.transform.SetParent(sceneRoot, false);
            }

            BattlePVPLifetimeScope dedicatedScope = dedicatedObject.GetComponent<BattlePVPLifetimeScope>();
            if (dedicatedScope == null)
            {
                dedicatedScope = dedicatedObject.AddComponent<BattlePVPLifetimeScope>();
            }

            dedicatedScope.CopyReferencesFrom(scopeOnRoot);
            scopeOnRoot.autoRun = false;
            Object.Destroy(scopeOnRoot);

            Debug.LogWarning("[BattlePvpScene] LifetimeScopeをBattlePvpDiへ分離しました");
        }
    }
}
