using System.Collections.Generic;
using UnityEngine;

namespace ClayEditor
{
    public class ClayBoneVisualizer : MonoBehaviour
    {
        [Header("表示設定")]
        public Color boneColor = Color.cyan;
        public Color jointColor = Color.yellow;
        [Tooltip("プレハブ画面やSceneViewでこのオブジェクトを選択している時だけ表示")]
        public bool drawOnlyWhenSelected = false;
        [Tooltip("ゲーム実行時（Game画面用）の描画マテリアル。未設定時は自動生成")]
        public Material visualizerMaterial;

        [Header("サイズ調整")]
        [Tooltip("ボーンの長さに応じて関節のサイズを自動調整")]
        public bool autoJointSize = true;

        [Range(0.01f, 0.5f)]
        [Tooltip("Auto Joint Size がオンの時の、ボーンの長さに対する関節の割合")]
        public float jointSizeRatio = 0.1f;

        [Range(0.001f, 1f)]
        [Tooltip("Auto Joint Size がオフの時の基準サイズ（モデルのスケールに追従）")]
        public float baseJointSize = 0.05f;

        [Range(0.001f, 0.1f)]
        [Tooltip("ゲーム実行時（Game画面）のボーン（線）の太さ")]
        public float boneWidth = 0.02f;

        [Header("ターゲット (任意)")]
        public SkinnedMeshRenderer targetRenderer;

        // ゲーム実行時（ランタイム）用のデータ保持構造体
        private struct BonePair
        {
            public Transform bone;
            public Transform parent;
            public LineRenderer lineRenderer;
            public Transform jointSphere;
        }

        private readonly List<BonePair> bonePairs = new();
        private GameObject containerObject;

        private void Start()
        {
            // 【ゲーム実行時のみ】動的オブジェクトを生成して実体化する
            if (Application.isPlaying)
            {
                InitializeRuntimeVisualizer();
            }
        }

        private void LateUpdate()
        {
            // 【ゲーム実行時のみ】アニメーションやボーンの移動に合わせて追従
            if (Application.isPlaying)
            {
                UpdateRuntimeVisualizer();
            }
        }

        #region 非実行時（プレハブ画面 / SceneView）の Gizmos 描画処理
        private void OnDrawGizmos()
        {
            // ゲーム実行時は実体オブジェクトが描画するため、二重描画を防ぐ
            if (Application.isPlaying)
            {
                return;
            }
            if (!drawOnlyWhenSelected)
            {
                DrawBonesGizmo();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
            {
                return;
            }
            if (drawOnlyWhenSelected)
            {
                DrawBonesGizmo();
            }
        }

        private void DrawBonesGizmo()
        {
            if (targetRenderer != null && targetRenderer.bones != null && targetRenderer.bones.Length > 0)
            {
                DrawFromRendererGizmo();
            }
            else
            {
                DrawFromHierarchyGizmo(transform, null);
            }
        }

        private void DrawFromRendererGizmo()
        {
            foreach (Transform bone in targetRenderer.bones)
            {
                if (bone == null)
                {
                    continue;
                }
                DrawBoneGizmo(bone, bone.parent);
            }
        }

        private void DrawFromHierarchyGizmo(Transform current, Transform parent)
        {
            DrawBoneGizmo(current, parent);

            foreach (Transform child in current)
            {
                DrawFromHierarchyGizmo(child, current);
            }
        }

        private void DrawBoneGizmo(Transform bone, Transform parent)
        {
            float currentJointSize = CalculateJointSize(bone, parent);

            if (parent != null)
            {
                Gizmos.color = boneColor;
                Gizmos.DrawLine(parent.position, bone.position);
            }

            Gizmos.color = jointColor;
            Gizmos.DrawSphere(bone.position, currentJointSize);
        }
        #endregion

        #region ゲーム実行時（Game画面 / ビルド製品版）の描画オブジェクト生成処理
        private void InitializeRuntimeVisualizer()
        {
            // 生成したオブジェクトを散らかさないためのコンテナ
            containerObject = new GameObject("BoneVisualizer_Runtime");

            // ★修正1: モデルの極小スケールを引き継がないよう、親に設定する処理を削除（ルートに置く）
            // containerObject.transform.SetParent(transform, false); // ← この行を削除またはコメントアウト

            if (targetRenderer != null && targetRenderer.bones != null && targetRenderer.bones.Length > 0)
            {
                foreach (Transform bone in targetRenderer.bones)
                {
                    if (bone == null) continue;
                    CreateRuntimeObjects(bone, bone.parent);
                }
            }
            else
            {
                CreateRuntimeObjectsFromHierarchy(transform, null);
            }
        }

        private void CreateRuntimeObjectsFromHierarchy(Transform current, Transform parent)
        {
            CreateRuntimeObjects(current, parent);

            foreach (Transform child in current)
            {
                // 自身が生成したコンテナオブジェクトは走査から除外する
                if (child == containerObject.transform)
                {
                    continue;
                }
                CreateRuntimeObjectsFromHierarchy(child, current);
            }
        }

        private void CreateRuntimeObjects(Transform bone, Transform parent)
        {
            BonePair pair = new BonePair { bone = bone, parent = parent };

            // 関節用の球体を生成
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(containerObject.transform, false);

            var sphereRenderer = sphere.GetComponent<MeshRenderer>();
            sphereRenderer.material = visualizerMaterial;
            sphereRenderer.material.color = jointColor;
            pair.jointSphere = sphere.transform;

            // 親ボーンがある場合は繋ぐLineRendererを生成
            if (parent != null)
            {
                GameObject lineObj = new GameObject($"Line_{parent.name}_to_{bone.name}");
                lineObj.transform.SetParent(containerObject.transform, false);

                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.material = visualizerMaterial;
                lr.startColor = lr.endColor = boneColor;
                lr.startWidth = lr.endWidth = boneWidth;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.sortingOrder = -1;

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

                // 関節球の位置とサイズを更新
                if (pair.jointSphere != null)
                {
                    pair.jointSphere.position = pair.bone.position;
                    float size = CalculateJointSize(pair.bone, pair.parent);

                    // ★修正3: Gizmos(半径)とSphere(直径)の仕様差を合わせるため「2倍」にする
                    pair.jointSphere.localScale = Vector3.one * (size * 2f);
                }

                // ボーン線の位置を更新
                if (pair.lineRenderer != null && pair.parent != null)
                {
                    pair.lineRenderer.SetPosition(0, pair.parent.position);
                    pair.lineRenderer.SetPosition(1, pair.bone.position);
                }
            }
        }
        #endregion

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
            if (containerObject != null)
            {
                Destroy(containerObject);
            }
        }
    }
}