using System.Collections.Generic;
using ClayEditor.Rigging;
using UnityEngine;

namespace ClayEditor
{
    /// <summary>
    /// 生成ボーンの関節と線をランタイム表示するEditor向けデバッグ用Visualizer
    /// </summary>
    public class ClayBoneVisualizer : MonoBehaviour
    {
        [Header("表示設定")]
        [SerializeField] private Color boneColor = Color.cyan;
        [SerializeField] private Color jointColor = Color.yellow;

        [Header("部位ごとの色分け")]
        [Tooltip("部位ごとに色分けするか、オフのときはboneColor/jointColorを使う")]
        [SerializeField] private bool colorByPart = true;

        [Tooltip("部位分類に使うアナライザー、未設定だと色分けはオフになる")]
        [SerializeField] private SkeletonPartAnalyzer partAnalyzer;

        [SerializeField] private Color bodyColor = Color.gray;
        [SerializeField] private Color legColor = Color.green;
        [SerializeField] private Color armColor = Color.red;
        [SerializeField] private Color frontColor = Color.blue;
        [SerializeField] private Color backColor = Color.magenta;

        [Header("サイズ調整")]
        [Tooltip("ボーンの長さに応じて関節のサイズを自動調整する")]
        [SerializeField] private bool autoJointSize = true;

        [Range(0.01f, 0.5f)]
        [Tooltip("autoJointSizeがオンの時のボーンの長さに対する関節の割合")]
        [SerializeField] private float jointSizeRatio = 0.1f;

        [Range(0.001f, 1f)]
        [Tooltip("autoJointSizeがオフの時の基準サイズ")]
        [SerializeField] private float baseJointSize = 0.05f;

        [Range(0.001f, 0.1f)]
        [Tooltip("ゲーム実行中のボーンの太さ")]
        [SerializeField] private float boneWidth = 0.02f;

        [Header("ターゲット")]
        [Tooltip("自動生成されたボーンが配置されるBoneRoot")]
        [SerializeField] private Transform boneRoot;
        [Tooltip("BoneRoot未設定時に参照するSkinnedMeshRenderer(任意)")]
        [SerializeField] private SkinnedMeshRenderer targetRenderer;

        // ゲーム実行中用のデータ保持構造体
        private struct BonePair
        {
            public Transform bone;
            public Transform parent;
            public LineRenderer lineRenderer;
            public Transform jointSphere;
        }

        private readonly List<BonePair> bonePairs = new();
        private GameObject containerObject;

        // 座標収集中のボーンと生成済みボーンの対応
        private readonly List<(Transform bone, Transform parent)> collected = new();
        private readonly List<Transform> builtBones = new();

        // ボーンごとの部位分類
        private Dictionary<Transform, BonePart> partMap = new();

        // 実行時生成の前面描画用マテリアル
        private Material runtimeMaterial;

        private void Awake()
        {
            // ROM(プレイヤービルド)ではデバッグ用ボーン表示を行わない
            if (!Application.isEditor)
            {
                enabled = false;
                ClearRuntime();
            }
        }

        private void LateUpdate()
        {
            // ROMではボーン可視化を更新しない
            if (!Application.isEditor || !Application.isPlaying)
            {
                return;
            }

            CollectBones();

            if (NeedsRebuild())
            {
                RebuildRuntime();
            }

            UpdateRuntimeVisualizer();
        }

        public void Rebuild()
        {
            // ROMでは可視化オブジェクトを生成しない
            if (!Application.isEditor || !Application.isPlaying)
            {
                ClearRuntime();
                return;
            }

            CollectBones();
            RebuildRuntime();
            UpdateRuntimeVisualizer();
        }

        /// <summary>
        /// 実行時に生成した可視化オブジェクトを破棄する
        /// </summary>
        private void ClearRuntime()
        {
            if (containerObject != null)
            {
                Destroy(containerObject);
                containerObject = null;
            }

            bonePairs.Clear();
            builtBones.Clear();
        }

        // 表示元から現在のボーンを収集する
        private void CollectBones()
        {
            collected.Clear();

            if (boneRoot != null)
            {
                foreach (Transform child in boneRoot)
                {
                    CollectHierarchy(child, null);
                }
            }
            else if (targetRenderer != null && targetRenderer.bones != null && targetRenderer.bones.Length > 0)
            {
                foreach (Transform bone in targetRenderer.bones)
                {
                    if (bone == null)
                    {
                        continue;
                    }

                    collected.Add((bone, bone.parent));
                }
            }
            else
            {
                foreach (Transform child in transform)
                {
                    CollectHierarchy(child, null);
                }
            }
        }

        private void CollectHierarchy(Transform current, Transform parent)
        {
            if (containerObject != null && current == containerObject.transform)
            {
                return;
            }

            collected.Add((current, parent));

            foreach (Transform child in current)
            {
                CollectHierarchy(child, current);
            }
        }

        private bool NeedsRebuild()
        {
            if (builtBones.Count != collected.Count)
            {
                return true;
            }

            for (int i = 0; i < collected.Count; i++)
            {
                if (collected[i].bone == null || collected[i].bone != builtBones[i])
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildRuntime()
        {
            if (containerObject != null)
            {
                Destroy(containerObject);
            }

            bonePairs.Clear();
            builtBones.Clear();

            if (runtimeMaterial == null)
            {
                runtimeMaterial = CreateRuntimeMaterial();
            }

            // 部位分類を更新する(色分けがオンでアナライザーがある場合のみ)
            UpdatePartMap();

            containerObject = new GameObject("BoneVisualizer_Runtime");

            foreach (var (bone, parent) in collected)
            {
                if (bone == null)
                {
                    continue;
                }

                CreateRuntimeObjects(bone, parent);
                builtBones.Add(bone);
            }
        }

        // ボーンごとの部位分類を求める
        private void UpdatePartMap()
        {
            partMap.Clear();

            if (!colorByPart || partAnalyzer == null)
            {
                return;
            }

            // 収集したボーンを配列にしてアナライザーへ渡す
            var bones = new Transform[collected.Count];
            for (int i = 0; i < collected.Count; i++)
            {
                bones[i] = collected[i].bone;
            }

            partMap = partAnalyzer.ClassifyBones(bones);
        }

        // ボーンの部位に応じた関節の色を返す
        private Color GetJointColor(Transform bone)
        {
            if (!colorByPart || partAnalyzer == null)
            {
                return jointColor;
            }

            return GetPartColor(bone);
        }

        // ボーンの部位に応じたボーン線の色を返す
        private Color GetBoneColor(Transform bone)
        {
            if (!colorByPart || partAnalyzer == null)
            {
                return boneColor;
            }

            return GetPartColor(bone);
        }

        private Color GetPartColor(Transform bone)
        {
            if (bone != null && partMap.TryGetValue(bone, out BonePart part))
            {
                switch (part)
                {
                    case BonePart.Leg:
                        return legColor;
                    case BonePart.Arm:
                        return armColor;
                    case BonePart.Front:
                        return frontColor;
                    case BonePart.Back:
                        return backColor;
                    case BonePart.Body:
                        return bodyColor;
                }
            }

            return bodyColor;
        }

        private Material CreateRuntimeMaterial()
        {
            // URP環境でもZTestを無効化して上書き可能な「UI用標準シェーダー」を流用する
            Shader shader = Shader.Find("UI/Default");
            Material mat = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            // UIシェーダー専用のZTestプロパティを Always(8: 常に前面) に設定
            mat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);

            return mat;
        }

        private void CreateRuntimeObjects(Transform bone, Transform parent)
        {
            BonePair pair = new BonePair { bone = bone, parent = parent };

            // 関節用の球体を生成
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(containerObject.transform, false);

            var sphereRenderer = sphere.GetComponent<MeshRenderer>();
            sphereRenderer.material = runtimeMaterial;
            sphereRenderer.material.color = GetJointColor(bone);

            pair.jointSphere = sphere.transform;

            // LineRendererを生成、ボーン線の色は子ボーン側の部位で決める
            if (parent != null)
            {
                GameObject lineObj = new GameObject($"Line_{parent.name}_to_{bone.name}");
                lineObj.transform.SetParent(containerObject.transform, false);

                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.material = runtimeMaterial;
                lr.startColor = lr.endColor = GetBoneColor(bone);
                lr.startWidth = lr.endWidth = boneWidth;
                lr.positionCount = 2;
                lr.useWorldSpace = true;

                pair.lineRenderer = lr;
            }

            bonePairs.Add(pair);
        }

        private void UpdateRuntimeVisualizer()
        {
            foreach (var pair in bonePairs)
            {
                if (pair.bone == null)
                {
                    continue;
                }

                if (pair.jointSphere != null)
                {
                    pair.jointSphere.position = pair.bone.position;
                    float size = CalculateJointSize(pair.bone, pair.parent);
                    pair.jointSphere.localScale = Vector3.one * (size * 2f);
                }

                if (pair.lineRenderer != null && pair.parent != null)
                {
                    pair.lineRenderer.SetPosition(0, pair.parent.position);
                    pair.lineRenderer.SetPosition(1, pair.bone.position);
                }
            }
        }

        private float CalculateJointSize(Transform bone, Transform parent)
        {
            if (autoJointSize)
            {
                if (parent != null)
                {
                    return Vector3.Distance(parent.position, bone.position) * jointSizeRatio;
                }
                else if (bone.childCount > 0)
                {
                    Transform firstChild = bone.GetChild(0);
                    if (containerObject != null && firstChild == containerObject.transform && bone.childCount > 1)
                    {
                        firstChild = bone.GetChild(1);
                    }

                    return Vector3.Distance(bone.position, firstChild.position) * jointSizeRatio;
                }
            }

            float maxScale = Mathf.Max(bone.lossyScale.x, bone.lossyScale.y, bone.lossyScale.z);
            return baseJointSize * maxScale;
        }

        private void OnDestroy()
        {
            ClearRuntime();

            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }
        }
    }
}