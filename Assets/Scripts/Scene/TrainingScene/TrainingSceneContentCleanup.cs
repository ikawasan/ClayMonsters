using Scene.TrainingScene.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scene.TrainingScene
{
    /// <summary>
    /// 育成背景がTitle等へ残留しないよう入場時に掃除する
    /// TrainingBackgroundはTrainingシーン配下に閉じ込める
    /// </summary>
    public static class TrainingSceneContentCleanup
    {
        private const string TrainingSceneName = "Training";

        /// <summary>
        /// Trainingシーン外へ漏れた育成背景を破棄する
        /// </summary>
        public static void DestroyLeakedTrainingBackgrounds()
        {
            UnityEngine.SceneManagement.Scene trainingScene = SceneManager.GetSceneByName(TrainingSceneName);
            bool trainingSceneLoaded = trainingScene.IsValid() && trainingScene.isLoaded;

            TrainingBackgroundView[] backgrounds = Object.FindObjectsByType<TrainingBackgroundView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < backgrounds.Length; i++)
            {
                TrainingBackgroundView background = backgrounds[i];
                if (background == null)
                {
                    continue;
                }

                UnityEngine.SceneManagement.Scene ownerScene = background.gameObject.scene;
                if (!trainingSceneLoaded || ownerScene.handle != trainingScene.handle)
                {
                    Object.Destroy(background.gameObject);
                }
            }
        }
    }
}
