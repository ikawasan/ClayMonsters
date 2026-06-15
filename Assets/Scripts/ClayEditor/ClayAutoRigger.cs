#nullable enable
using ClayEditor.Interface;
using UnityEngine;
using VContainer;

namespace ClayEditor
{
    public class ClayAutoRigger : MonoBehaviour
    {
        // 必要な状態を受け取る
        [Inject] private readonly IClaySceneContext sceneContext = default!;

        private Transform[] bones;

        /// <summary>
        /// スキニングに用いるボーン Transform 群
        /// </summary>
        public Transform[] Bones => bones;

        [Header("Compute Shader")]
        [SerializeField] private ComputeShader riggerCompute;

        /// <summary>
        /// 現在編集中モデルのボーンルート
        /// </summary>
        public Transform? BoneRoot
        {
            get
            {
                // 実行中かつ DI が解決済みの場合のみシーン状態を参照する
                if (Application.isPlaying && sceneContext != null)
                {
                    return sceneContext.CurrentModel.Value?.BoneRoot;
                }

                // エディタ非実行時は DI が無いため、安全に null を返す
                return null;
            }
        }

        /// <summary>
        /// 直近の計算で生成されたバインドポーズ行列群
        /// </summary>
        public Matrix4x4[] BindPoses { get; private set; }

        // ComputeShader用構造体
        private struct CSBoneWeight
        {
            public float w0, w1, w2, w3;
            public int i0, i1, i2, i3;
        }

        // 使い回すためのバッファ
        private ComputeBuffer? vertexBuffer;
        private ComputeBuffer? bonePosBuffer;
        private ComputeBuffer? weightBuffer;
        private int currentVertexBufferSize = 0;

        /// <summary>
        /// 頂点群に対するボーンウェイトを計算する
        /// </summary>
        /// <param name="vertices">対象頂点（ローカル座標）</param>
        /// <param name="meshRoot">メッシュのルート Transform</param>
        /// <returns>計算済みボーンウェイト。前提条件を満たさない場合は null</returns>
        public BoneWeight[] Calculate(Vector3[] vertices, Transform meshRoot)
        {
            if (bones == null || bones.Length == 0 || vertices == null || vertices.Length == 0)
            {
                return null;
            }

            // バインドポーズとボーンのワールド座標を計算
            BindPoses = new Matrix4x4[bones.Length];
            Vector3[] bonePositions = new Vector3[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null)
                {
                    BindPoses[i] = bones[i].worldToLocalMatrix * meshRoot.localToWorldMatrix;
                    bonePositions[i] = bones[i].position;
                }
                else
                {
                    BindPoses[i] = Matrix4x4.identity;
                    bonePositions[i] = Vector3.zero;
                }
            }

            // ComputeShader が設定されていない場合のフォールバック
            if (riggerCompute == null)
            {
                Debug.LogWarning("ClayAutoRigger: ComputeShader がアサインされていません。CPU 計算へフォールバックします。");
                return CalculateCPU(vertices, meshRoot);
            }

            int vertexCount = vertices.Length;
            int boneCount = bones.Length;

            // バッファの確保
            if (vertexBuffer == null || currentVertexBufferSize < vertexCount)
            {
                vertexBuffer?.Release();
                weightBuffer?.Release();

                currentVertexBufferSize = Mathf.Max(vertexCount, 4096);
                vertexBuffer = new ComputeBuffer(currentVertexBufferSize, sizeof(float) * 3);
                weightBuffer = new ComputeBuffer(currentVertexBufferSize, sizeof(float) * 4 + sizeof(int) * 4);
            }

            // ボーンバッファの再構築
            if (bonePosBuffer == null || bonePosBuffer.count != boneCount)
            {
                bonePosBuffer?.Release();
                bonePosBuffer = new ComputeBuffer(boneCount, sizeof(float) * 3);
            }

            // バッファへのデータ転送（必要な要素数だけ転送）
            vertexBuffer.SetData(vertices, 0, 0, vertexCount);
            bonePosBuffer.SetData(bonePositions);

            // ComputeShaderの実行
            int kernel = riggerCompute.FindKernel("CalculateBoneWeights");
            riggerCompute.SetBuffer(kernel, "_Vertices", vertexBuffer);
            riggerCompute.SetBuffer(kernel, "_BonePositions", bonePosBuffer);
            riggerCompute.SetBuffer(kernel, "_BoneWeights", weightBuffer);
            riggerCompute.SetInt("_VertexCount", vertexCount);
            riggerCompute.SetInt("_BoneCount", boneCount);
            riggerCompute.SetMatrix("_LocalToWorld", meshRoot.localToWorldMatrix);

            int threadGroups = Mathf.CeilToInt(vertexCount / 64f);
            riggerCompute.Dispatch(kernel, threadGroups, 1, 1);

            // 結果の取得とBoneWeight型への変換
            CSBoneWeight[] csWeights = new CSBoneWeight[vertexCount];
            weightBuffer?.GetData(csWeights, 0, 0, vertexCount);

            BoneWeight[] weights = new BoneWeight[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                weights[i] = new BoneWeight
                {
                    weight0 = csWeights[i].w0,
                    weight1 = csWeights[i].w1,
                    boneIndex0 = csWeights[i].i0,
                    boneIndex1 = csWeights[i].i1
                };
            }

            return weights;
        }

        // 旧来の CPU ロジック（フォールバック用）
        private BoneWeight[] CalculateCPU(Vector3[] vertices, Transform meshRoot)
        {
            BoneWeight[] weights = new BoneWeight[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = meshRoot.TransformPoint(vertices[i]);
                int b0 = 0, b1 = 0;
                float d0 = float.MaxValue, d1 = float.MaxValue;

                for (int b = 0; b < bones.Length; b++)
                {
                    if (bones[b] == null)
                    {
                        continue;
                    }

                    float d = Vector3.Distance(worldPos, bones[b].position);
                    if (d < d0)
                    {
                        d1 = d0;
                        b1 = b0;
                        d0 = d;
                        b0 = b;
                    }
                    else if (d < d1)
                    {
                        d1 = d;
                        b1 = b;
                    }
                }

                float w0 = 1f / Mathf.Max(d0, 0.001f);
                float w1 = 1f / Mathf.Max(d1, 0.001f);
                float sum = w0 + w1;
                weights[i] = new BoneWeight { boneIndex0 = b0, weight0 = w0 / sum, boneIndex1 = b1, weight1 = w1 / sum };
            }

            return weights;
        }

        /// <summary>
        /// BoneRoot 配下の Transform からボーン配列を自動構築する。
        /// </summary>
        [ContextMenu("Auto Setup Bones From Root")]
        public void AutoSetup()
        {
            if (BoneRoot != null)
            {
                // BoneRoot 配下の Transform をすべて取得
                bones = BoneRoot.GetComponentsInChildren<Transform>(true);
            }
            else
            {
                Debug.LogWarning("[ClayAutoRigger] BoneRoot が null のためボーンを設定できません。");
            }
        }

        private void ReleaseBuffers()
        {
            if (vertexBuffer != null)
            {
                vertexBuffer.Release();
                vertexBuffer = null;
            }

            if (weightBuffer != null)
            {
                weightBuffer.Release();
                weightBuffer = null;
            }

            if (bonePosBuffer != null)
            {
                bonePosBuffer.Release();
                bonePosBuffer = null;
            }
        }

        void OnDestroy()
        {
            ReleaseBuffers();
        }
    }
}