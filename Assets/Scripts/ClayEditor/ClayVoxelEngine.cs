using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;

namespace ClayEditor
{
    public class ClayVoxelEngine : MonoBehaviour, IInitializable
    {
        public struct MeshData
        {
            public Vector3[] vertices;
            public Vector3[] normals;
            public int[] indices;
        }

        private struct Triangle
        {
            public Vector3 v1, v2, v3;
            public Vector3 n1, n2, n3;
        }

        // カーネル名の定数
        private const string KernelGenerateMesh = "GenerateMesh";
        private const string KernelModifyVoxels = "ModifyVoxels";

        // シェーダープロパティの ID キャッシュ
        private static class ShaderIDs
        {
            public static readonly int Size = Shader.PropertyToID("_Size");
            public static readonly int Scale = Shader.PropertyToID("_Scale");
            public static readonly int IsoLevel = Shader.PropertyToID("_IsoLevel");
            public static readonly int Offset = Shader.PropertyToID("_Offset");

            public static readonly int HitPosition = Shader.PropertyToID("_HitPosition");
            public static readonly int ModRadius = Shader.PropertyToID("_ModRadius");
            public static readonly int ModStrength = Shader.PropertyToID("_ModStrength");

            public static readonly int Voxels = Shader.PropertyToID("_Voxels");
            public static readonly int Triangles = Shader.PropertyToID("_Triangles");
        }

        [Header("Grid Settings")]
        public ComputeShader marchingCubesCompute;
        public Material material;
        [Range(4, 128)]
        public int size = 32;
        public float isoLevel = 0f;
        public float boundsSize = 16f;

        [Header("Paint Settings")]
        [SerializeField] private Color defaultVertexColor = Color.white;

        [SerializeField] private GameObject clayModel;

        private ComputeBuffer voxelBuffer;
        private ComputeBuffer triangleBuffer;
        private ComputeBuffer triCountBuffer;

        private Mesh mesh;
        private SkinnedMeshRenderer skinnedRenderer;
        private MeshCollider meshCollider;

        // 頂点カラー塗布用のキャッシュ（メッシュ再生成時に作り直す）
        private Vector3[] currentVertices;
        private Color[] vertexColors;

        private const int ComputeThreads = 8;
        private const int FloatsPerTriangle = 18;

        public float Scale => boundsSize / size;
        public Vector3 CenterOffset => Vector3.one * (size * Scale * 0.5f);

        private int TotalVoxelCount => (size + 1) * (size + 1) * (size + 1);
        private int MaxTriangleCount => size * size * size * 5;

        /// <inheritdoc />
        public void Initialize()
        {
            ReleaseBuffers();

            mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            skinnedRenderer = clayModel.GetComponent<SkinnedMeshRenderer>();
            meshCollider = clayModel.GetComponent<MeshCollider>();

            skinnedRenderer.sharedMesh = mesh;
            skinnedRenderer.material = material;

            voxelBuffer = new ComputeBuffer(TotalVoxelCount, sizeof(float));
            triangleBuffer = new ComputeBuffer(MaxTriangleCount, sizeof(float) * FloatsPerTriangle, ComputeBufferType.Append);
            triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

            UpdateShaderParams();
            ClearAllVoxels();
        }

        private void OnDestroy()
        {
            ReleaseBuffers();
        }

        private void ReleaseBuffers()
        {
            voxelBuffer?.Release();
            triangleBuffer?.Release();
            triCountBuffer?.Release();
        }

        public MeshData GenerateMeshData()
        {
            UpdateShaderParams();
            triangleBuffer.SetCounterValue(0);
            DispatchKernel(KernelGenerateMesh, size);

            int[] countData = new int[1] { 0 };
            ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
            triCountBuffer.GetData(countData);

            int triangleCount = countData[0];
            if (triangleCount == 0)
            {
                return new MeshData();
            }

            Triangle[] tris = new Triangle[triangleCount];
            triangleBuffer.GetData(tris);

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> indices = new List<int>();

            for (int i = 0; i < triangleCount; i++)
            {
                if (float.IsFinite(tris[i].v1.x))
                {
                    int baseIndex = vertices.Count;

                    vertices.Add(tris[i].v1);
                    vertices.Add(tris[i].v2);
                    vertices.Add(tris[i].v3);

                    normals.Add(tris[i].n1);
                    normals.Add(tris[i].n2);
                    normals.Add(tris[i].n3);

                    indices.Add(baseIndex);
                    indices.Add(baseIndex + 1);
                    indices.Add(baseIndex + 2);
                }
            }

            return new MeshData
            {
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                indices = indices.ToArray()
            };
        }

        public void ApplyToRenderer(MeshData data, BoneWeight[] weights, Matrix4x4[] bindPoses, Transform[] bones)
        {
            mesh.Clear();

            if (data.vertices != null && data.vertices.Length > 0)
            {
                mesh.vertices = data.vertices;
                mesh.normals = data.normals;
                mesh.triangles = data.indices;
                mesh.SetUVs(0, data.vertices);

                // 頂点カラーを初期化する（メッシュ再生成のたびに既定色でリセットされる）
                currentVertices = data.vertices;
                vertexColors = new Color[data.vertices.Length];
                for (int i = 0; i < vertexColors.Length; i++)
                {
                    vertexColors[i] = defaultVertexColor;
                }
                mesh.colors = vertexColors;

                if (weights != null && weights.Length > 0)
                {
                    mesh.bindposes = bindPoses;
                    mesh.boneWeights = weights;
                    skinnedRenderer.bones = bones;
                }

                if (meshCollider != null)
                {
                    meshCollider.sharedMesh = mesh;
                }
            }
            else
            {
                currentVertices = null;
                vertexColors = null;

                if (meshCollider != null)
                {
                    meshCollider.sharedMesh = null;
                }
            }
        }

        /// <summary>
        /// ワールド座標を中心に、半径内の頂点カラーを指定色で塗る
        /// </summary>
        /// <param name="worldCenter">塗りの中心（ワールド座標）</param>
        /// <param name="worldRadius">塗りの半径（ワールド単位）</param>
        /// <param name="color">塗る色</param>
        public void PaintVertices(Vector3 worldCenter, float worldRadius, Color color)
        {
            if (mesh == null || currentVertices == null || vertexColors == null || skinnedRenderer == null)
            {
                return;
            }

            Transform meshTransform = skinnedRenderer.transform;
            Vector3 localCenter = meshTransform.InverseTransformPoint(worldCenter);

            // ワールド半径をメッシュのローカル空間へ変換
            float scale = meshTransform.lossyScale.x;
            float localRadius = scale > 0f ? worldRadius / scale : worldRadius;
            float sqrRadius = localRadius * localRadius;

            bool changed = false;
            for (int i = 0; i < currentVertices.Length; i++)
            {
                if ((currentVertices[i] - localCenter).sqrMagnitude <= sqrRadius)
                {
                    vertexColors[i] = color;
                    changed = true;
                }
            }

            if (changed)
            {
                mesh.colors = vertexColors;
            }
        }

        public float[] GetVoxelData()
        {
            float[] data = new float[TotalVoxelCount];
            voxelBuffer.GetData(data);
            return data;
        }

        public void SetVoxelData(float[] data)
        {
            voxelBuffer.SetData(data);
            UpdateShaderParams();
        }

        public void ClearAllVoxels()
        {
            float[] emptyData = new float[TotalVoxelCount];
            for (int i = 0; i < emptyData.Length; i++)
            {
                emptyData[i] = -0.5f;
            }

            voxelBuffer.SetData(emptyData);
            UpdateShaderParams();
        }

        public void Modify(Vector3 localPos, float radius, float strength)
        {
            UpdateShaderParams();
            marchingCubesCompute.SetVector(ShaderIDs.HitPosition, localPos);
            marchingCubesCompute.SetFloat(ShaderIDs.ModRadius, radius);
            marchingCubesCompute.SetFloat(ShaderIDs.ModStrength, strength);

            DispatchKernel(KernelModifyVoxels, size + 1);
        }

        private void UpdateShaderParams()
        {
            marchingCubesCompute.SetInt(ShaderIDs.Size, size);
            marchingCubesCompute.SetFloat(ShaderIDs.Scale, Scale);
            marchingCubesCompute.SetFloat(ShaderIDs.IsoLevel, isoLevel);
            marchingCubesCompute.SetVector(ShaderIDs.Offset, CenterOffset);
        }

        private void DispatchKernel(string kernelName, int threadSize)
        {
            int kernelIndex = marchingCubesCompute.FindKernel(kernelName);
            marchingCubesCompute.SetBuffer(kernelIndex, ShaderIDs.Voxels, voxelBuffer);

            if (kernelName == KernelGenerateMesh)
            {
                marchingCubesCompute.SetBuffer(kernelIndex, ShaderIDs.Triangles, triangleBuffer);
            }

            int threadGroups = Mathf.CeilToInt(threadSize / (float)ComputeThreads);
            marchingCubesCompute.Dispatch(kernelIndex, threadGroups, threadGroups, threadGroups);
        }
    }
}