using UnityEngine;

namespace ClayEditor
{
    /// <summary>
    /// 成形ブラシ形状に合わせたカーソル見た目パラメータ
    /// 造形ロジックのブラシ空間(Z=カメラ方向Y=画面上方向)と一致させる
    /// </summary>
    public static class ClaySculptBrushCursorVisual
    {
        private const float RingMajorRadius = 0.65f;
        private const float RingTubeRadius = 0.35f;
        private const int RingSegments = 32;
        private const int TubeSegments = 16;
        private const int ConeSegments = 32;

        private static Mesh cachedConeMesh;
        private static Mesh cachedSquarePyramidMesh;
        private static Mesh cachedTorusMesh;

        /// <summary>
        /// 形状に対応するカーソルメッシュを返す
        /// </summary>
        /// <param name="shape">成形ブラシ形状</param>
        /// <param name="sphereMesh">球メッシュ</param>
        /// <param name="cubeMesh">立方体メッシュ</param>
        /// <param name="coneMesh">円錐メッシュ</param>
        /// <param name="squarePyramidMesh">四角錐メッシュ</param>
        /// <param name="torusMesh">トーラスメッシュ</param>
        public static Mesh ResolveMesh(
            ClaySculptBrushShape shape,
            Mesh sphereMesh,
            Mesh cubeMesh,
            Mesh coneMesh,
            Mesh squarePyramidMesh,
            Mesh torusMesh)
        {
            return shape switch
            {
                ClaySculptBrushShape.Cube => cubeMesh,
                ClaySculptBrushShape.Cone => coneMesh,
                ClaySculptBrushShape.SquarePyramid => squarePyramidMesh,
                ClaySculptBrushShape.CircularRing => torusMesh,
                _ => sphereMesh,
            };
        }

        /// <summary>
        /// ブラシ向き適用後に乗せるメッシュローカル回転
        /// </summary>
        /// <param name="shape">成形ブラシ形状</param>
        public static Quaternion ResolveMeshRotationOffset(ClaySculptBrushShape shape)
        {
            return Quaternion.identity;
        }

        /// <summary>
        /// ブラシ半径に合わせたカーソルローカルスケールを返す
        /// </summary>
        /// <param name="shape">成形ブラシ形状</param>
        /// <param name="brushRadius">ブラシ半径</param>
        /// <param name="meshDiameter">メッシュバウンドの最大辺</param>
        public static Vector3 ResolveLocalScale(
            ClaySculptBrushShape shape,
            float brushRadius,
            float meshDiameter)
        {
            float diameterScale = brushRadius * 2f / Mathf.Max(meshDiameter, 1e-4f);
            return Vector3.one * diameterScale;
        }

        /// <summary>
        /// カメラ向きブラシ回転をワールド回転へ変換する
        /// </summary>
        /// <param name="clayModelRotation">造形モデルのワールド回転</param>
        /// <param name="orientation">ブラシ向き</param>
        /// <param name="shape">成形ブラシ形状</param>
        public static Quaternion ResolveWorldRotation(
            Quaternion clayModelRotation,
            ClaySculptBrushOrientation orientation,
            ClaySculptBrushShape shape)
        {
            if (shape == ClaySculptBrushShape.Sphere)
            {
                return Quaternion.identity;
            }

            Quaternion localRotation = Quaternion.LookRotation(
                (Vector3)orientation.AxisZ,
                (Vector3)orientation.AxisY);
            return clayModelRotation * localRotation * ResolveMeshRotationOffset(shape);
        }

        /// <summary>
        /// 正円錐ブラシ用メッシュを返す
        /// 先端Y=+1底面Y=-1で半径1
        /// </summary>
        public static Mesh GetOrCreateConeMesh()
        {
            if (cachedConeMesh != null)
            {
                return cachedConeMesh;
            }

            cachedConeMesh = CreateConeMesh(ConeSegments);
            return cachedConeMesh;
        }

        /// <summary>
        /// 正四角錐ブラシ用メッシュを返す
        /// 先端Y=+1底面Y=-1で一辺2
        /// </summary>
        public static Mesh GetOrCreateSquarePyramidMesh()
        {
            if (cachedSquarePyramidMesh != null)
            {
                return cachedSquarePyramidMesh;
            }

            cachedSquarePyramidMesh = CreateSquarePyramidMesh();
            return cachedSquarePyramidMesh;
        }

        /// <summary>
        /// 環状ブラシ用トーラスメッシュを返す
        /// </summary>
        public static Mesh GetOrCreateTorusMesh()
        {
            if (cachedTorusMesh != null)
            {
                return cachedTorusMesh;
            }

            cachedTorusMesh = CreateTorusMesh(
                RingMajorRadius,
                RingTubeRadius,
                RingSegments,
                TubeSegments);
            return cachedTorusMesh;
        }

        private static Mesh CreateConeMesh(int segments)
        {
            var mesh = new Mesh { name = "SculptBrushCone" };
            int vertexCount = segments + 2;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];

            vertices[0] = new Vector3(0f, 1f, 0f);
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 1f);

            int baseCenterIndex = segments + 1;
            vertices[baseCenterIndex] = new Vector3(0f, -1f, 0f);
            normals[baseCenterIndex] = Vector3.down;
            uvs[baseCenterIndex] = new Vector2(0.5f, 0f);

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                int index = i + 1;
                vertices[index] = new Vector3(x, -1f, z);
                normals[index] = new Vector3(x, 0f, z);
                uvs[index] = new Vector2(i / (float)segments, 0f);
            }

            int triangleCount = segments * 6;
            var triangles = new int[triangleCount];
            int triangleIndex = 0;
            for (int i = 0; i < segments; i++)
            {
                int current = i + 1;
                int next = i == segments - 1 ? 1 : i + 2;
                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = baseCenterIndex;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateSquarePyramidMesh()
        {
            var mesh = new Mesh { name = "SculptBrushSquarePyramid" };
            var vertices = new Vector3[]
            {
                new(0f, 1f, 0f),
                new(1f, -1f, 1f),
                new(-1f, -1f, 1f),
                new(-1f, -1f, -1f),
                new(1f, -1f, -1f),
            };
            var triangles = new int[]
            {
                0, 1, 2,
                0, 2, 3,
                0, 3, 4,
                0, 4, 1,
                1, 4, 3,
                1, 3, 2,
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateTorusMesh(
            float majorRadius,
            float minorRadius,
            int ringSegments,
            int tubeSegments)
        {
            var mesh = new Mesh { name = "SculptBrushTorus" };
            int ringVertexCount = ringSegments + 1;
            int tubeVertexCount = tubeSegments + 1;
            int vertexCount = ringVertexCount * tubeVertexCount;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];

            for (int ring = 0; ring <= ringSegments; ring++)
            {
                float ringAngle = ring / (float)ringSegments * Mathf.PI * 2f;
                float ringCos = Mathf.Cos(ringAngle);
                float ringSin = Mathf.Sin(ringAngle);

                for (int tube = 0; tube <= tubeSegments; tube++)
                {
                    float tubeAngle = tube / (float)tubeSegments * Mathf.PI * 2f;
                    float tubeCos = Mathf.Cos(tubeAngle);
                    float tubeSin = Mathf.Sin(tubeAngle);
                    int index = ring * tubeVertexCount + tube;
                    float radialDistance = majorRadius + minorRadius * tubeCos;
                    vertices[index] = new Vector3(
                        radialDistance * ringCos,
                        radialDistance * ringSin,
                        minorRadius * tubeSin);
                    normals[index] = new Vector3(
                        ringCos * tubeCos,
                        ringSin * tubeCos,
                        tubeSin);
                    uvs[index] = new Vector2(
                        ring / (float)ringSegments,
                        tube / (float)tubeSegments);
                }
            }

            int triangleCount = ringSegments * tubeSegments * 6;
            var triangles = new int[triangleCount];
            int triangleIndex = 0;
            for (int ring = 0; ring < ringSegments; ring++)
            {
                for (int tube = 0; tube < tubeSegments; tube++)
                {
                    int current = ring * tubeVertexCount + tube;
                    int nextRing = current + tubeVertexCount;
                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = nextRing;
                    triangles[triangleIndex++] = current + 1;
                    triangles[triangleIndex++] = nextRing;
                    triangles[triangleIndex++] = nextRing + 1;
                    triangles[triangleIndex++] = current + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
