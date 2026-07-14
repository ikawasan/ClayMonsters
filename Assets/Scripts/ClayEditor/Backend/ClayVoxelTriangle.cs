using Unity.Mathematics;

namespace ClayEditor.Backend
{
    /// <summary>
    /// MarchingCubes生成三角形1枚分の頂点法線色
    /// </summary>
    public struct ClayVoxelTriangle
    {
        public float3 v1;
        public float3 v2;
        public float3 v3;
        public float3 n1;
        public float3 n2;
        public float3 n3;
        public float3 c1;
        public float3 c2;
        public float3 c3;
    }
}
