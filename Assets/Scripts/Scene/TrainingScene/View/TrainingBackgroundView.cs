using Scene.TrainingScene.Domain;
using Scene.TrainingScene.Interface;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 育成シーンの背景オブジェクトを切り替える
    /// 開始時と最終放課後のみDefaultそれ以外の訓練後はRoamを使う
    /// </summary>
    public sealed class TrainingBackgroundView : MonoBehaviour, ITrainingBackgroundView
    {
        /// <summary>
        /// 行き先と背景オブジェクトの対応
        /// </summary>
        [Serializable]
        private struct LocationBackground
        {
            [SerializeField] private TrainingLocation location;
            [SerializeField] private GameObject backgroundObject;

            /// <summary>
            /// 対応する行き先
            /// </summary>
            public TrainingLocation Location => location;

            /// <summary>
            /// 表示する背景オブジェクト
            /// </summary>
            public GameObject BackgroundObject => backgroundObject;
        }

        [SerializeField] private Transform backgroundRoot;
        [SerializeField] private GameObject defaultBackground;
        [SerializeField] private GameObject restBackground;
        [Tooltip("訓練後の徘徊用背景未設定時はRestBackgroundを使う")]
        [SerializeField] private GameObject roamBackground;
        [SerializeField] private GameObject inheritanceBackground;
        [SerializeField] private LocationBackground[] locationBackgrounds = Array.Empty<LocationBackground>();

        private void Awake()
        {
            EnsureSceneOwnership();
        }

        /// <inheritdoc/>
        public void ShowLocationBackground(TrainingLocation location)
        {
            GameObject target = FindLocationBackground(location);
            Activate(target != null ? target : defaultBackground);
        }

        /// <inheritdoc/>
        public void ShowRestBackground()
        {
            Activate(restBackground != null ? restBackground : defaultBackground);
        }

        /// <inheritdoc/>
        public void ShowRoamBackground()
        {
            GameObject target = ResolveRoamBackground();
            if (target == null)
            {
                Debug.LogError(
                    "[TrainingBackgroundView] roamBackgroundが未配線ですHierarchyで専用背景を接続してください",
                    this);
                Activate(defaultBackground);
                return;
            }

            Activate(target);
        }

        /// <inheritdoc/>
        public void ShowDefaultBackground()
        {
            Activate(defaultBackground);
        }

        /// <inheritdoc/>
        public void ShowInheritanceBackground()
        {
            if (inheritanceBackground == null)
            {
                Debug.LogError(
                    "[TrainingBackgroundView] inheritanceBackgroundが未配線ですHierarchyで黒背景を接続してください",
                    this);
                HideForLeave();
                return;
            }

            Activate(inheritanceBackground);
        }

        /// <inheritdoc/>
        public void HideForLeave()
        {
            if (backgroundRoot != null)
            {
                for (int i = 0; i < backgroundRoot.childCount; i++)
                {
                    backgroundRoot.GetChild(i).gameObject.SetActive(false);
                }

                return;
            }

            foreach (GameObject background in EnumerateBackgrounds())
            {
                if (background != null)
                {
                    background.SetActive(false);
                }
            }
        }

        /// <summary>
        /// TrainingSceneルート配下へ移して他シーンと分離する
        /// </summary>
        internal void EnsureSceneOwnership()
        {
            if (transform.parent != null)
            {
                return;
            }

            Transform trainingRoot = ResolveTrainingSceneRoot();
            if (trainingRoot != null)
            {
                transform.SetParent(trainingRoot, false);
            }
        }

        private static Transform ResolveTrainingSceneRoot()
        {
            GameObject trainingSceneRoot = GameObject.Find("TrainingScene");
            return trainingSceneRoot != null ? trainingSceneRoot.transform : null;
        }

        private GameObject FindLocationBackground(TrainingLocation location)
        {
            for (int i = 0; i < locationBackgrounds.Length; i++)
            {
                if (locationBackgrounds[i].Location == location)
                {
                    return locationBackgrounds[i].BackgroundObject;
                }
            }

            return null;
        }

        private GameObject ResolveRoamBackground()
        {
            if (roamBackground != null)
            {
                return roamBackground;
            }

            if (restBackground != null && restBackground != defaultBackground)
            {
                return restBackground;
            }

            return null;
        }

        private void Activate(GameObject target)
        {
            foreach (GameObject background in EnumerateBackgrounds())
            {
                if (background == null)
                {
                    continue;
                }

                background.SetActive(background == target);
            }
        }

        private IEnumerable<GameObject> EnumerateBackgrounds()
        {
            if (defaultBackground != null)
            {
                yield return defaultBackground;
            }

            if (restBackground != null)
            {
                yield return restBackground;
            }

            if (roamBackground != null)
            {
                yield return roamBackground;
            }

            if (inheritanceBackground != null)
            {
                yield return inheritanceBackground;
            }

            for (int i = 0; i < locationBackgrounds.Length; i++)
            {
                if (locationBackgrounds[i].BackgroundObject != null)
                {
                    yield return locationBackgrounds[i].BackgroundObject;
                }
            }
        }
    }
}
