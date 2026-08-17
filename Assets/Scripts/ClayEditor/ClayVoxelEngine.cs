using ClayEditor.Backend;
using ClayEditor.Backend.Interface;
using ClayEditor.Paint;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using R3;
using Unity.Mathematics;
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
            public Color[] colors;
        }

        // チャンク1つぶんの表示用メッシュ レンダラ コライダーを束ねる
        private sealed class Chunk
        {
            public GameObject gameObject;
            public Mesh mesh;
            public MeshCollider collider;
            public bool dirty;
            public bool colliderDirty;
        }

        [Header("Backend Settings")]
        [Tooltip("ONのときGPUを使わずCPU JobSystemのみで造形する")]
        [SerializeField] private bool forceCpuBackend;
        [Tooltip("CPUバックエンド時に全体メッシュ生成へ使うチャンクのセル数上限")]
        [SerializeField] private int cpuMeshChunkSize = 16;

        [Header("Grid Settings")]
        public ComputeShader marchingCubesCompute;
        public Material material;
        [Range(4, 128)]
        public int size = 32;
        public float isoLevel = 0f;
        public float boundsSize = 16f;

        [Header("Chunk Settings")]
        [Tooltip("部分更新の単位となるチャンクのセル数（各軸）小さいほど部分更新が効く")]
        [SerializeField] private int chunkSize = 16;

        [Header("Paint Settings")]
        [SerializeField] private Color defaultVertexColor = new Color(0.94f, 0.86f, 0.74f, 1f);

        [Header("Dense Sculpt")]
        [Tooltip("三角形数がこの値以上のとき成形中のメッシュ更新間隔を dense 用にする")]
        [SerializeField] private int denseSculptTriangleThreshold = 20000;

        [SerializeField] private GameObject clayModel;

        private IClayVoxelBackend backend;

        private Mesh mesh;
        private SkinnedMeshRenderer skinnedRenderer;
        private MeshCollider meshCollider;

        private int cachedTotalTriangleCount;
        private bool pendingPaintMeshUpdate;

        // チャンク（造形中の表示用）
        private Chunk[] chunks;
        private int chunksPerAxis;
        private Transform chunkRoot;
        // 後から汚したチャンクを優先再生成するためのLIFOキュー
        private readonly List<int> dirtyChunkQueue = new List<int>(64);

        private ClayVoxelTriangle[] triangleCache;

        private readonly Subject<bool> hasMeshSubject = new Subject<bool>();
        private bool cachedHasMesh;

        /// <summary>
        /// 造形メッシュの有無が変わった通知
        /// </summary>
        public Observable<bool> HasMeshChanged => hasMeshSubject;

        private Vector3[] vertexBuffer = System.Array.Empty<Vector3>();
        private Vector3[] normalBuffer = System.Array.Empty<Vector3>();
        private Color[] colorBuffer = System.Array.Empty<Color>();
        private int[] indexBuffer = System.Array.Empty<int>();

        private GameObject brushPreviewObject;
        private MeshFilter brushPreviewFilter;
        private MeshCollider brushPreviewCollider;
        private Mesh brushPreviewMesh;
        private bool brushPreviewActive;

        /// <summary>
        /// 表示中チャンクの概算三角形数
        /// </summary>
        public int CachedTriangleCount => cachedTotalTriangleCount;

        /// <summary>
        /// 表示中チャンクの概算頂点数
        /// </summary>
        public int CachedVertexCount => cachedTotalTriangleCount * 3;

        /// <summary>
        /// 成形をdense扱いする頂点しきい値
        /// </summary>
        public int DenseSculptVertexThreshold => Mathf.Max(1, denseSculptTriangleThreshold * 3);

        /// <summary>
        /// 既存メッシュが厚く成形中更新間隔をdense用にするべきか
        /// </summary>
        public bool IsDenseSculptMesh => cachedTotalTriangleCount >= denseSculptTriangleThreshold;

        public float Scale => boundsSize / size;
        public Vector3 CenterOffset => Vector3.one * (size * Scale * 0.5f);

        /// <summary>
        /// 造形メッシュの表示Transform
        /// </summary>
        public Transform ClayModelTransform => clayModel != null ? clayModel.transform : transform;

        /// <summary>
        /// 表示中メッシュへスクリーン座標からレイを飛ばしヒット点を返す
        /// MeshCollider経由のPhysicsレイキャストで表示メッシュと一致させる
        /// </summary>
        /// <param name="camera">対象カメラ</param>
        /// <param name="screenPos">ポインタのスクリーン座標</param>
        /// <param name="worldHit">ワールド空間のヒット情報</param>
        /// <returns>ヒットした場合true</returns>
        public bool TryRaycastSurface(UnityEngine.Camera camera, Vector2 screenPos, out RaycastHit worldHit)
        {
            return TryRaycastSurface(camera.ScreenPointToRay(screenPos), out worldHit);
        }

        /// <summary>
        /// 表示中メッシュへレイを飛ばしヒット点を返す
        /// </summary>
        /// <param name="worldRay">ワールド空間のレイ</param>
        /// <param name="worldHit">ワールド空間のヒット情報</param>
        /// <returns>ヒットした場合true</returns>
        public bool TryRaycastSurface(Ray worldRay, out RaycastHit worldHit)
        {
            worldHit = default;

            // チャンク表示時は単一meshが空でも表面コライダーは存在する
            if (!HasMesh())
            {
                return false;
            }

            return CursorRaycaster.TryRaycastFrontSurface(
                worldRay,
                ~0,
                ClayModelTransform,
                out worldHit);
        }

        private int TotalVoxelCount => (size + 1) * (size + 1) * (size + 1);
        private int MaxTriangleCount => size * size * size * 5;

        /// <summary>
        /// ボクセルバックエンドが利用可能か
        /// </summary>
        public bool IsBackendReady => backend != null && backend.IsReady;

        /// <summary>
        /// 現在使用中のボクセル処理バックエンド種別
        /// </summary>
        public ClayVoxelBackendKind ActiveBackendKind =>
            backend != null ? backend.Kind : ClayVoxelBackendKind.Cpu;

        /// <summary>
        /// CPU JobSystemバックエンドを使用中か
        /// </summary>
        public bool IsUsingCpuBackend =>
            backend != null && backend.Kind == ClayVoxelBackendKind.Cpu;

        /// <inheritdoc />
        public void Initialize()
        {
            ReleaseBackend();

            mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            skinnedRenderer = clayModel.GetComponent<SkinnedMeshRenderer>();
            meshCollider = clayModel.GetComponent<MeshCollider>();

            skinnedRenderer.sharedMesh = mesh;
            skinnedRenderer.sharedMaterial = material;

            BuildChunks();

            if (!TryInitializeBackend())
            {
                Debug.LogError("[ClayVoxelEngine] ボクセルバックエンドの初期化に失敗しました");
                return;
            }

            UpdateBackendParams();
            ClearAllVoxels();
            InitializeVoxelColors();
            cachedTotalTriangleCount = 0;
            cachedHasMesh = false;
            PublishHasMeshIfChanged();
        }

        private void OnDestroy()
        {
            ClearBrushSculptPreview();
            if (brushPreviewMesh != null)
            {
                Destroy(brushPreviewMesh);
                brushPreviewMesh = null;
            }

            if (brushPreviewObject != null)
            {
                Destroy(brushPreviewObject);
                brushPreviewObject = null;
            }

            ReleaseBackend();
            hasMeshSubject.Dispose();
        }

        /// <summary>
        /// 造形されたメッシュが存在するかを返す
        /// </summary>
        /// <returns>三角形が1つ以上あればtrue</returns>
        public bool HasMesh()
        {
            if (!IsBackendReady)
            {
                return false;
            }

            return cachedTotalTriangleCount > 0;
        }

        private void PublishHasMeshIfChanged()
        {
            if (!IsBackendReady)
            {
                return;
            }

            bool current = cachedTotalTriangleCount > 0;
            if (current == cachedHasMesh)
            {
                return;
            }

            cachedHasMesh = current;
            hasMeshSubject.OnNext(current);
        }

        private void RefreshCachedTriangleCountFromChunks()
        {
            cachedTotalTriangleCount = 0;
            if (chunks == null)
            {
                return;
            }

            for (int i = 0; i < chunks.Length; i++)
            {
                Mesh chunkMesh = chunks[i].mesh;
                if (chunkMesh == null || chunkMesh.vertexCount <= 0)
                {
                    continue;
                }

                // チャンクメッシュは三角形ごとに頂点を複製するためvertexCount/3で足りる
                cachedTotalTriangleCount += chunkMesh.vertexCount / 3;
            }
        }

        private void ReleaseBackend()
        {
            backend?.Dispose();
            backend = null;
        }

        private bool TryInitializeBackend()
        {
#if CLAY_VOXEL_FORCE_CPU
            forceCpuBackend = true;
#endif
            if (!forceCpuBackend)
            {
                var gpuBackend = new ClayVoxelGpuBackend();
                if (gpuBackend.TryInitialize(marchingCubesCompute, size))
                {
                    backend = gpuBackend;
                    Debug.Log("[ClayVoxelEngine] GPU ComputeShaderバックエンドを使用します");
                    return true;
                }

                gpuBackend.Dispose();
            }

            int cs = Mathf.Max(cpuMeshChunkSize, 4);
            int maxChunkAxis = Mathf.Min(cs + 2, size + 1);
            int maxChunkCellCount = maxChunkAxis * maxChunkAxis * maxChunkAxis;

            var cpuBackend = new ClayVoxelCpuBackend();
            if (cpuBackend.TryInitialize(size, maxChunkCellCount))
            {
                backend = cpuBackend;
                string reason = forceCpuBackend ? "強制設定" : "GPUが利用できないため";
                Debug.LogWarning($"[ClayVoxelEngine] {reason} CPU JobSystemバックエンドを使用します");
                return true;
            }

            cpuBackend.Dispose();
            return false;
        }

        private void UpdateBackendParams()
        {
            if (!IsBackendReady)
            {
                return;
            }

            Color storageDefaultColor = PaintColorUtility.ToStorageColor(defaultVertexColor);
            backend.SetGridParams(
                size,
                Scale,
                isoLevel,
                CenterOffset,
                new Vector3(storageDefaultColor.r, storageDefaultColor.g, storageDefaultColor.b));
        }

        // ボクセル色を既定色で初期化する
        private void InitializeVoxelColors()
        {
            backend?.InitializeVoxelColors();
        }

        // チャンク群を構築する 各チャンクは子GameObject（MeshFilter MeshRenderer MeshCollider）を持つ
        private void BuildChunks()
        {
            if (chunkRoot != null)
            {
                Destroy(chunkRoot.gameObject);
            }

            var rootObj = new GameObject("ClayChunks");
            chunkRoot = rootObj.transform;
            chunkRoot.SetParent(clayModel.transform, false);
            chunkRoot.localPosition = Vector3.zero;
            chunkRoot.localRotation = Quaternion.identity;
            chunkRoot.localScale = Vector3.one;
            rootObj.layer = clayModel.layer;

            int cs = Mathf.Max(chunkSize, 4);
            chunksPerAxis = Mathf.CeilToInt(size / (float)cs);
            int total = chunksPerAxis * chunksPerAxis * chunksPerAxis;

            chunks = new Chunk[total];
            for (int i = 0; i < total; i++)
            {
                var go = new GameObject("Chunk_" + i);
                go.transform.SetParent(chunkRoot, false);
                go.layer = clayModel.layer;

                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;

                var col = go.AddComponent<MeshCollider>();
                // 造形中の再焼きコストを抑える
                col.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation;

                var chunkMesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                chunkMesh.MarkDynamic();
                mf.sharedMesh = chunkMesh;

                chunks[i] = new Chunk
                {
                    gameObject = go,
                    mesh = chunkMesh,
                    collider = col,
                    dirty = true
                };
            }
        }

        private int ChunkIndex(int cx, int cy, int cz)
        {
            return cx * chunksPerAxis * chunksPerAxis + cy * chunksPerAxis + cz;
        }

        // チャンク境界の隙間を防ぐため隣接セル1層分だけ重ねて生成する
        private int GetChunkDispatchSize(int origin, int cellSize)
        {
            int remaining = size - origin;
            if (remaining <= 0)
            {
                return 0;
            }

            int dispatchSize = cellSize + 1;
            return dispatchSize <= remaining ? dispatchSize : remaining;
        }

        // 造形中の表示にチャンク群を使うか 単一メッシュを使うかを切り替える
        private void SetChunksVisible(bool visible)
        {
            if (chunkRoot != null)
            {
                chunkRoot.gameObject.SetActive(visible);
            }

            if (skinnedRenderer != null)
            {
                skinnedRenderer.enabled = !visible;
            }

            if (meshCollider != null)
            {
                meshCollider.enabled = !visible;
            }
        }

        private int GetMaxChunkTriangleCount(int sizeX, int sizeY, int sizeZ)
        {
            int cellCount = sizeX * sizeY * sizeZ;
            return cellCount * ClayVoxelMeshLimits.MaxTrianglesPerCell;
        }

        // バックエンドで指定チャンク範囲のメッシュを生成し 三角形数を返す
        private int DispatchChunk(int originX, int originY, int originZ, int sizeX, int sizeY, int sizeZ)
        {
            if (!IsBackendReady)
            {
                return 0;
            }

            int requiredCapacity = GetMaxChunkTriangleCount(sizeX, sizeY, sizeZ);
            EnsureTriangleCache(requiredCapacity);
            return backend.GenerateMeshChunk(
                originX,
                originY,
                originZ,
                sizeX,
                sizeY,
                sizeZ,
                triangleCache,
                0,
                triangleCache.Length);
        }

        // ダーティなチャンクだけメッシュを生成し直す
        // maxChunksで1回あたりの処理数を制限し成形中のスパイクを抑える
        // 制限時は最近のブラシ位置に近いチャンクを優先する
        private void RebuildDirtyChunks(bool refreshColliders, int maxChunks)
        {
            if (chunks == null)
            {
                return;
            }

            if (maxChunks <= 0)
            {
                return;
            }

            int cs = Mathf.Max(chunkSize, 4);
            bool budgeted = maxChunks < int.MaxValue;
            int rebuilt = 0;

            if (budgeted)
            {
                while (dirtyChunkQueue.Count > 0 && rebuilt < maxChunks)
                {
                    int index = dirtyChunkQueue[dirtyChunkQueue.Count - 1];
                    dirtyChunkQueue.RemoveAt(dirtyChunkQueue.Count - 1);
                    if (index < 0 || index >= chunks.Length)
                    {
                        continue;
                    }

                    Chunk chunk = chunks[index];
                    if (!chunk.dirty)
                    {
                        continue;
                    }

                    RebuildChunkAtIndex(index, cs, refreshColliders);
                    rebuilt++;
                }

                return;
            }

            // 完全再生成時はキュー残差を捨て全チャンクを走査する
            dirtyChunkQueue.Clear();
            for (int cx = 0; cx < chunksPerAxis; cx++)
            {
                for (int cy = 0; cy < chunksPerAxis; cy++)
                {
                    for (int cz = 0; cz < chunksPerAxis; cz++)
                    {
                        int index = ChunkIndex(cx, cy, cz);
                        Chunk chunk = chunks[index];
                        if (!chunk.dirty)
                        {
                            continue;
                        }

                        RebuildChunkAtIndex(index, cs, refreshColliders);
                    }
                }
            }
        }

        private void RebuildChunkAtIndex(int index, int cs, bool refreshColliders)
        {
            Chunk chunk = chunks[index];
            int cx = index / (chunksPerAxis * chunksPerAxis);
            int rem = index - cx * chunksPerAxis * chunksPerAxis;
            int cy = rem / chunksPerAxis;
            int cz = rem - cy * chunksPerAxis;

            int originX = cx * cs;
            int originY = cy * cs;
            int originZ = cz * cs;
            int sizeX = GetChunkDispatchSize(originX, cs);
            int sizeY = GetChunkDispatchSize(originY, cs);
            int sizeZ = GetChunkDispatchSize(originZ, cs);

            BuildChunkMesh(chunk, originX, originY, originZ, sizeX, sizeY, sizeZ, refreshColliders);
            chunk.dirty = false;
        }

        private void MarkChunkDirty(int index)
        {
            Chunk chunk = chunks[index];
            if (chunk.dirty)
            {
                return;
            }

            chunk.dirty = true;
            dirtyChunkQueue.Add(index);
        }

        private static bool IsFiniteTriangle(ClayVoxelTriangle triangle)
        {
            // 頂点が有限でないときだけ落とす法線は見た目の問題であり穴の原因ではない
            return float.IsFinite(triangle.v1.x) && float.IsFinite(triangle.v1.y) && float.IsFinite(triangle.v1.z)
                && float.IsFinite(triangle.v2.x) && float.IsFinite(triangle.v2.y) && float.IsFinite(triangle.v2.z)
                && float.IsFinite(triangle.v3.x) && float.IsFinite(triangle.v3.y) && float.IsFinite(triangle.v3.z);
        }

        // 1チャンクぶんのメッシュを生成して反映する
        private void BuildChunkMesh(
            Chunk chunk,
            int originX,
            int originY,
            int originZ,
            int sizeX,
            int sizeY,
            int sizeZ,
            bool refreshColliders)
        {
            int triangleCount = DispatchChunk(originX, originY, originZ, sizeX, sizeY, sizeZ);

            chunk.mesh.Clear(false);

            if (triangleCount == 0)
            {
                if (refreshColliders)
                {
                    chunk.collider.sharedMesh = null;
                    chunk.colliderDirty = false;
                }
                else
                {
                    // 表示は空でもコライダーはストローク終了まで据え置く
                    chunk.colliderDirty = true;
                }

                return;
            }

            EnsureTriangleCache(Mathf.Max(triangleCount, 1));
            EnsureMeshWriteBuffers(triangleCount * 3);

            int writeCount = 0;
            for (int i = 0; i < triangleCount; i++)
            {
                if (!IsFiniteTriangle(triangleCache[i]))
                {
                    continue;
                }

                int baseIndex = writeCount;
                vertexBuffer[writeCount] = triangleCache[i].v1;
                normalBuffer[writeCount] = triangleCache[i].n1;
                colorBuffer[writeCount] = ToColor(triangleCache[i].c1);
                indexBuffer[writeCount] = baseIndex;
                writeCount++;

                vertexBuffer[writeCount] = triangleCache[i].v2;
                normalBuffer[writeCount] = triangleCache[i].n2;
                colorBuffer[writeCount] = ToColor(triangleCache[i].c2);
                indexBuffer[writeCount] = baseIndex + 1;
                writeCount++;

                vertexBuffer[writeCount] = triangleCache[i].v3;
                normalBuffer[writeCount] = triangleCache[i].n3;
                colorBuffer[writeCount] = ToColor(triangleCache[i].c3);
                indexBuffer[writeCount] = baseIndex + 2;
                writeCount++;
            }

            if (writeCount == 0)
            {
                if (refreshColliders)
                {
                    chunk.collider.sharedMesh = null;
                    chunk.colliderDirty = false;
                }
                else
                {
                    chunk.colliderDirty = true;
                }

                return;
            }

            chunk.mesh.SetVertices(vertexBuffer, 0, writeCount, MeshUpdateFlags.DontValidateIndices);
            chunk.mesh.SetNormals(normalBuffer, 0, writeCount, MeshUpdateFlags.DontValidateIndices);
            chunk.mesh.SetColors(colorBuffer, 0, writeCount, MeshUpdateFlags.DontValidateIndices);
            chunk.mesh.SetTriangles(indexBuffer, 0, writeCount, 0, calculateBounds: true, 0);

            if (refreshColliders)
            {
                chunk.collider.sharedMesh = null;
                chunk.collider.sharedMesh = chunk.mesh;
                chunk.colliderDirty = false;
            }
            else
            {
                // 表示だけ更新しコライダー焼き直しはストローク終了へ遅延する
                chunk.colliderDirty = true;
            }
        }

        private void EnsureMeshWriteBuffers(int capacity)
        {
            if (vertexBuffer.Length < capacity)
            {
                vertexBuffer = new Vector3[capacity];
                normalBuffer = new Vector3[capacity];
                colorBuffer = new Color[capacity];
                indexBuffer = new int[capacity];
            }
        }

        private static Color ToColor(float3 c)
        {
            return new Color(
                Mathf.Clamp01(c.x),
                Mathf.Clamp01(c.y),
                Mathf.Clamp01(c.z),
                1f);
        }

        /// <summary>
        /// ブラシ周辺のチャンクをダーティにして次回の本更新対象にする
        /// </summary>
        /// <param name="localPos">ブラシ中心ローカル座標</param>
        /// <param name="radius">ブラシ半径ローカル単位</param>
        public void MarkBrushChunksDirty(Vector3 localPos, float radius)
        {
            MarkDirtyChunks(localPos, radius);
        }

        // ブラシ位置と半径から ダーティにするチャンクを決める
        private void MarkDirtyChunks(Vector3 localPos, float radius)
        {
            if (chunks == null)
            {
                return;
            }

            int cs = Mathf.Max(chunkSize, 4);
            Vector3 voxelCenter = (localPos + CenterOffset) / Scale;
            float voxelRadius = radius / Scale + 2f;

            int minX = Mathf.FloorToInt((voxelCenter.x - voxelRadius) / cs);
            int maxX = Mathf.FloorToInt((voxelCenter.x + voxelRadius) / cs);
            int minY = Mathf.FloorToInt((voxelCenter.y - voxelRadius) / cs);
            int maxY = Mathf.FloorToInt((voxelCenter.y + voxelRadius) / cs);
            int minZ = Mathf.FloorToInt((voxelCenter.z - voxelRadius) / cs);
            int maxZ = Mathf.FloorToInt((voxelCenter.z + voxelRadius) / cs);

            minX = Mathf.Clamp(minX, 0, chunksPerAxis - 1);
            maxX = Mathf.Clamp(maxX, 0, chunksPerAxis - 1);
            minY = Mathf.Clamp(minY, 0, chunksPerAxis - 1);
            maxY = Mathf.Clamp(maxY, 0, chunksPerAxis - 1);
            minZ = Mathf.Clamp(minZ, 0, chunksPerAxis - 1);
            maxZ = Mathf.Clamp(maxZ, 0, chunksPerAxis - 1);

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cy = minY; cy <= maxY; cy++)
                {
                    for (int cz = minZ; cz <= maxZ; cz++)
                    {
                        MarkChunkDirty(ChunkIndex(cx, cy, cz));
                    }
                }
            }
        }

        private void MarkAllChunksDirty()
        {
            if (chunks == null)
            {
                return;
            }

            dirtyChunkQueue.Clear();
            for (int i = 0; i < chunks.Length; i++)
            {
                chunks[i].dirty = true;
                dirtyChunkQueue.Add(i);
            }
        }

        /// <summary>
        /// 造形中の表示用に ダーティなチャンクだけメッシュを更新する（部分更新）
        /// </summary>
        /// <param name="refreshColliders">true のときチャンクコライダーも更新する</param>
        /// <param name="maxChunks">1回に再生成するdirtyチャンクの上限</param>
        public void UpdateShapeFast(bool refreshColliders = false, int maxChunks = int.MaxValue)
        {
            if (!IsBackendReady)
            {
                return;
            }

            SetChunksVisible(true);
            RebuildDirtyChunks(refreshColliders, maxChunks);
            if (refreshColliders)
            {
                Physics.SyncTransforms();
            }

            RefreshCachedTriangleCountFromChunks();
            PublishHasMeshIfChanged();
        }

        /// <summary>
        /// ブラシ周辺更新APIの互換入口
        /// 本メッシュはチャンクフル再生成のみを使い部分差し替えは行わない
        /// </summary>
        /// <param name="localPos">ブラシ中心ローカル座標</param>
        /// <param name="radius">ブラシ半径ローカル単位</param>
        /// <param name="refreshColliders">true のときチャンクコライダーも更新する</param>
        public void UpdateShapeFastNearBrush(Vector3 localPos, float radius, bool refreshColliders = false)
        {
            _ = localPos;
            _ = radius;
            UpdateShapeFast(refreshColliders);
        }

        /// <summary>
        /// 造形ストローク終了時に呼び 未反映メッシュを確定する
        /// コライダーは焼かない
        /// </summary>
        public void FlushShape()
        {
            ClearBrushSculptPreview();
            // ストローク中に残ったdirtyを表示へ反映するコライダーは触らない
            UpdateShapeFast(refreshColliders: false, maxChunks: int.MaxValue);
        }

        /// <summary>
        /// 既存の厚いメッシュ上でブラシ周辺だけを軽量プレビュー更新する
        /// チャンク本更新のすき間を埋める応答表示用であり本メッシュ差し替えではない
        /// </summary>
        /// <param name="localPos">ブラシ中心ローカル座標</param>
        /// <param name="radius">ブラシ半径ローカル単位</param>
        /// <param name="updateCollider">trueのときプレビューコライダーも焼く</param>
        public void UpdateBrushSculptPreview(Vector3 localPos, float radius, bool updateCollider = false)
        {
            if (!IsBackendReady)
            {
                return;
            }

            EnsureBrushSculptPreview();
            SetChunksVisible(true);

            ComputeBrushMeshBounds(
                localPos,
                radius,
                brushPadCells: 2f,
                out int minX,
                out int minY,
                out int minZ,
                out int sizeX,
                out int sizeY,
                out int sizeZ);

            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
            {
                brushPreviewMesh.Clear(false);
                if (updateCollider && brushPreviewCollider != null)
                {
                    brushPreviewCollider.sharedMesh = null;
                }

                brushPreviewActive = true;
                return;
            }

            int triangleCount = DispatchChunk(minX, minY, minZ, sizeX, sizeY, sizeZ);
            ApplyTrianglesToMesh(brushPreviewMesh, triangleCount);

            if (updateCollider && brushPreviewCollider != null)
            {
                brushPreviewCollider.sharedMesh = null;
                if (brushPreviewMesh.vertexCount > 0)
                {
                    brushPreviewCollider.sharedMesh = brushPreviewMesh;
                }

                Physics.SyncTransforms();
            }

            brushPreviewObject.SetActive(true);
            brushPreviewActive = true;
        }

        /// <summary>
        /// ブラシ周辺プレビューを破棄する
        /// </summary>
        public void ClearBrushSculptPreview()
        {
            if (!brushPreviewActive && brushPreviewObject == null)
            {
                return;
            }

            if (brushPreviewMesh != null)
            {
                brushPreviewMesh.Clear(false);
            }

            if (brushPreviewCollider != null)
            {
                brushPreviewCollider.sharedMesh = null;
            }

            if (brushPreviewObject != null)
            {
                brushPreviewObject.SetActive(false);
            }

            brushPreviewActive = false;
        }

        private void EnsureBrushSculptPreview()
        {
            if (brushPreviewObject != null)
            {
                return;
            }

            brushPreviewObject = new GameObject("BrushSculptPreview");
            brushPreviewObject.transform.SetParent(ClayModelTransform, false);
            brushPreviewObject.layer = clayModel != null ? clayModel.layer : gameObject.layer;

            brushPreviewFilter = brushPreviewObject.AddComponent<MeshFilter>();
            var renderer = brushPreviewObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            brushPreviewCollider = brushPreviewObject.AddComponent<MeshCollider>();
            brushPreviewCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation;

            brushPreviewMesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            brushPreviewMesh.MarkDynamic();
            brushPreviewFilter.sharedMesh = brushPreviewMesh;
            brushPreviewObject.SetActive(false);
        }

        private void ComputeBrushMeshBounds(
            Vector3 localPos,
            float radius,
            float brushPadCells,
            out int minX,
            out int minY,
            out int minZ,
            out int sizeX,
            out int sizeY,
            out int sizeZ)
        {
            Vector3 voxelCenter = (localPos + CenterOffset) / Scale;
            // 削除と再生成で同じセル余白を使い球より狭いAABBにならないよう合わせる
            float voxelRadius = radius / Scale + Mathf.Max(2f, brushPadCells);

            minX = Mathf.Clamp(Mathf.FloorToInt(voxelCenter.x - voxelRadius), 0, size - 1);
            minY = Mathf.Clamp(Mathf.FloorToInt(voxelCenter.y - voxelRadius), 0, size - 1);
            minZ = Mathf.Clamp(Mathf.FloorToInt(voxelCenter.z - voxelRadius), 0, size - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(voxelCenter.x + voxelRadius), 0, size - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(voxelCenter.y + voxelRadius), 0, size - 1);
            int maxZ = Mathf.Clamp(Mathf.CeilToInt(voxelCenter.z + voxelRadius), 0, size - 1);

            sizeX = Mathf.Max(0, maxX - minX + 1);
            sizeY = Mathf.Max(0, maxY - minY + 1);
            sizeZ = Mathf.Max(0, maxZ - minZ + 1);
        }

        private void ApplyTrianglesToMesh(Mesh targetMesh, int triangleCount)
        {
            targetMesh.Clear(false);
            if (triangleCount <= 0)
            {
                return;
            }

            EnsureTriangleCache(Mathf.Max(triangleCount, 1));
            EnsureMeshWriteBuffers(triangleCount * 3);

            int writeCount = 0;
            for (int i = 0; i < triangleCount; i++)
            {
                if (!IsFiniteTriangle(triangleCache[i]))
                {
                    continue;
                }

                int baseIndex = writeCount;
                vertexBuffer[writeCount] = triangleCache[i].v1;
                normalBuffer[writeCount] = triangleCache[i].n1;
                colorBuffer[writeCount] = ToColor(triangleCache[i].c1);
                indexBuffer[writeCount] = baseIndex;
                writeCount++;

                vertexBuffer[writeCount] = triangleCache[i].v2;
                normalBuffer[writeCount] = triangleCache[i].n2;
                colorBuffer[writeCount] = ToColor(triangleCache[i].c2);
                indexBuffer[writeCount] = baseIndex + 1;
                writeCount++;

                vertexBuffer[writeCount] = triangleCache[i].v3;
                normalBuffer[writeCount] = triangleCache[i].n3;
                colorBuffer[writeCount] = ToColor(triangleCache[i].c3);
                indexBuffer[writeCount] = baseIndex + 2;
                writeCount++;
            }

            if (writeCount == 0)
            {
                return;
            }

            targetMesh.SetVertices(vertexBuffer, 0, writeCount, MeshUpdateFlags.DontValidateIndices);
            targetMesh.SetNormals(normalBuffer, 0, writeCount, MeshUpdateFlags.DontValidateIndices);
            targetMesh.SetColors(colorBuffer, 0, writeCount, MeshUpdateFlags.DontValidateIndices);
            targetMesh.SetTriangles(indexBuffer, 0, writeCount, 0, calculateBounds: true, 0);
        }

        /// <summary>
        /// 遅延していたチャンクコライダーを一括更新する
        /// </summary>
        public void FlushChunkColliders()
        {
            if (chunks == null)
            {
                return;
            }

            bool updated = false;
            for (int i = 0; i < chunks.Length; i++)
            {
                Chunk chunk = chunks[i];
                if (!chunk.colliderDirty)
                {
                    continue;
                }

                chunk.collider.sharedMesh = null;
                if (chunk.mesh != null && chunk.mesh.vertexCount > 0)
                {
                    chunk.collider.sharedMesh = chunk.mesh;
                }

                chunk.colliderDirty = false;
                updated = true;
            }

            if (updated)
            {
                Physics.SyncTransforms();
            }
        }

        private void EnsureTriangleCache(int count)
        {
            if (triangleCache == null || triangleCache.Length < count)
            {
                triangleCache = new ClayVoxelTriangle[count];
            }
        }

        private int DispatchAndGetTriangleCountWhole()
        {
            return DispatchChunk(0, 0, 0, size, size, size);
        }

        /// <summary>
        /// ボーン生成やエクスポート用に メッシュデータ（頂点カラー含む）を新規確保して返す（全体 同期）
        /// </summary>
        public MeshData GenerateMeshData()
        {
            if (!IsBackendReady)
            {
                return new MeshData();
            }

            if (IsUsingCpuBackend)
            {
                return GenerateMeshDataFromChunks();
            }

            return GenerateMeshDataFromWholeDispatch();
        }

        private MeshData GenerateMeshDataFromWholeDispatch()
        {
            int triangleCount = DispatchAndGetTriangleCountWhole();
            if (triangleCount == 0)
            {
                return new MeshData();
            }

            var vertices = new List<Vector3>(triangleCount * 3);
            var normals = new List<Vector3>(triangleCount * 3);
            var indices = new List<int>(triangleCount * 3);
            var colors = new List<Color>(triangleCount * 3);
            AppendMeshDataFromTriangleCache(triangleCount, vertices, normals, indices, colors);

            return new MeshData
            {
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                indices = indices.ToArray(),
                colors = colors.ToArray()
            };
        }

        private MeshData GenerateMeshDataFromChunks()
        {
            if (!IsBackendReady || size <= 0)
            {
                return new MeshData();
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();
            var colors = new List<Color>();
            int cs = Mathf.Max(cpuMeshChunkSize, 4);
            int meshChunksPerAxis = Mathf.CeilToInt(size / (float)cs);

            for (int cx = 0; cx < meshChunksPerAxis; cx++)
            {
                for (int cy = 0; cy < meshChunksPerAxis; cy++)
                {
                    for (int cz = 0; cz < meshChunksPerAxis; cz++)
                    {
                        int originX = cx * cs;
                        int originY = cy * cs;
                        int originZ = cz * cs;
                        int sizeX = GetChunkDispatchSize(originX, cs);
                        int sizeY = GetChunkDispatchSize(originY, cs);
                        int sizeZ = GetChunkDispatchSize(originZ, cs);
                        if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
                        {
                            continue;
                        }

                        int triangleCount = DispatchChunk(originX, originY, originZ, sizeX, sizeY, sizeZ);
                        AppendMeshDataFromTriangleCache(triangleCount, vertices, normals, indices, colors);
                    }
                }
            }

            if (vertices.Count == 0)
            {
                return new MeshData();
            }

            return new MeshData
            {
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                indices = indices.ToArray(),
                colors = colors.ToArray()
            };
        }

        private void AppendMeshDataFromTriangleCache(
            int triangleCount,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices,
            List<Color> colors)
        {
            for (int i = 0; i < triangleCount; i++)
            {
                if (!IsFiniteTriangle(triangleCache[i]))
                {
                    continue;
                }

                int baseIndex = vertices.Count;

                vertices.Add(triangleCache[i].v1);
                vertices.Add(triangleCache[i].v2);
                vertices.Add(triangleCache[i].v3);

                normals.Add(triangleCache[i].n1);
                normals.Add(triangleCache[i].n2);
                normals.Add(triangleCache[i].n3);

                colors.Add(ToColor(triangleCache[i].c1));
                colors.Add(ToColor(triangleCache[i].c2));
                colors.Add(ToColor(triangleCache[i].c3));

                indices.Add(baseIndex);
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 2);
            }
        }

        private void SetCachedTriangleCountFromMeshData(MeshData data)
        {
            cachedTotalTriangleCount = data.indices != null ? data.indices.Length / 3 : 0;
        }

        /// <summary>
        /// 全体メッシュを単一のSkinnedMeshRendererへ適用する（スキンあり 頂点カラー含む）
        /// </summary>
        public void ApplyToRenderer(MeshData data, BoneWeight[] weights, Matrix4x4[] bindPoses, Transform[] bones)
        {
            SetChunksVisible(false);
            mesh.Clear();

            if (data.vertices != null && data.vertices.Length > 0)
            {
                mesh.vertices = data.vertices;
                mesh.normals = data.normals;
                mesh.triangles = data.indices;
                mesh.SetUVs(0, data.vertices);

                if (data.colors != null && data.colors.Length == data.vertices.Length)
                {
                    mesh.colors = data.colors;
                }

                if (weights != null && weights.Length > 0)
                {
                    mesh.bindposes = bindPoses;
                    mesh.boneWeights = weights;
                    skinnedRenderer.bones = bones;
                    if (bones != null && bones.Length > 0 && bones[0] != null)
                    {
                        skinnedRenderer.rootBone = bones[0];
                    }
                }

                if (meshCollider != null)
                {
                    RefreshMeshCollider();
                }
            }
            else
            {
                if (meshCollider != null)
                {
                    meshCollider.sharedMesh = null;
                }
            }
        }

        /// <summary>
        /// スキンを解除し 全体形状（頂点カラー含む）を単一メッシュへ適用する
        /// </summary>
        /// <param name="data">適用するメッシュデータ</param>
        /// <param name="refreshCollider">true のときMeshColliderも更新する</param>
        public void ApplyShapeOnly(MeshData data, bool refreshCollider = true)
        {
            SetChunksVisible(false);
            mesh.Clear();

            if (data.vertices != null && data.vertices.Length > 0)
            {
                mesh.vertices = data.vertices;
                mesh.normals = data.normals;
                mesh.triangles = data.indices;
                mesh.SetUVs(0, data.vertices);

                if (data.colors != null && data.colors.Length == data.vertices.Length)
                {
                    mesh.colors = data.colors;
                }

                mesh.boneWeights = new BoneWeight[0];
                mesh.bindposes = new Matrix4x4[0];
                skinnedRenderer.bones = new Transform[0];

                SetCachedTriangleCountFromMeshData(data);

                if (meshCollider != null && refreshCollider)
                {
                    RefreshMeshCollider();
                }
            }
            else
            {
                cachedTotalTriangleCount = 0;
                if (meshCollider != null)
                {
                    meshCollider.sharedMesh = null;
                }
            }
        }

        /// <summary>
        /// ペイントなどで単一メッシュ表示へ切り替える 全体メッシュを生成して適用する
        /// </summary>
        public void SwitchToSingleMesh()
        {
            MeshData data = GenerateMeshData();
            ApplyShapeOnly(data, refreshCollider: true);
            PublishHasMeshIfChanged();
        }

        /// <summary>
        /// ペイント中に遅延していたメッシュ色反映を実行する
        /// チャンク表示時は汚れたチャンクのみ再生成する
        /// </summary>
        /// <param name="refreshCollider">trueのときコライダーも更新する</param>
        public void FlushPaintMesh(bool refreshCollider = false)
        {
            if (pendingPaintMeshUpdate)
            {
                pendingPaintMeshUpdate = false;

                // ペイント中は単一メッシュ全再生成せずチャンク部分更新のみ行う
                SetChunksVisible(true);
                RebuildDirtyChunks(refreshCollider, int.MaxValue);
                RefreshCachedTriangleCountFromChunks();
                PublishHasMeshIfChanged();
            }

            if (refreshCollider)
            {
                FlushChunkColliders();
            }
        }

        /// <summary>
        /// ワールド座標を中心に 半径内のボクセル色を指定色で塗る（GPUペイント）
        /// 塗った色はメッシュ生成時に頂点へ反映され 造形やモード切替やエクスポートを通じて保持される
        /// </summary>
        /// <param name="worldCenter">塗りの中心（ワールド座標）</param>
        /// <param name="worldRadius">塗りの半径（ワールド単位）</param>
        /// <param name="color">塗る色</param>
        public void PaintVoxels(Vector3 worldCenter, float worldRadius, Color color, Vector3 worldNormal)
        {
            PaintVoxelsInternal(worldCenter, worldRadius, color, worldNormal, true);
        }

        // ペイント本体 applyImmediately が true のとき汚れたチャンクへ即時反映する
        private void PaintVoxelsInternal(
            Vector3 worldCenter,
            float worldRadius,
            Color color,
            Vector3 worldNormal,
            bool applyImmediately)
        {
            if (skinnedRenderer == null)
            {
                return;
            }

            if (!IsBackendReady)
            {
                return;
            }

            Transform meshTransform = ClayModelTransform;
            Vector3 localPos = meshTransform.InverseTransformPoint(worldCenter);
            Vector3 localNormal = worldNormal.sqrMagnitude > 0.0001f
                ? meshTransform.InverseTransformDirection(worldNormal.normalized)
                : Vector3.zero;
            float scale = meshTransform.lossyScale.x;
            float localRadius = scale > 0f ? worldRadius / scale : worldRadius;
            Color storageColor = PaintColorUtility.ToStorageColor(color);

            backend.Paint(
                localPos,
                localRadius,
                new Vector3(storageColor.r, storageColor.g, storageColor.b),
                localNormal);

            MarkDirtyChunks(localPos, localRadius);

            if (!applyImmediately)
            {
                return;
            }

            pendingPaintMeshUpdate = true;
            FlushPaintMesh(refreshCollider: false);
        }

        /// <summary>
        /// ボクセル色を既定色へ戻す（ペイントのやり直し時に全消去してから塗り直すために使う）
        /// </summary>
        public void ResetVoxelColors()
        {
            InitializeVoxelColors();
            MarkAllChunksDirty();
        }

        /// <summary>
        /// 複数のペイントストロークを順に塗り直す 最後にまとめて表示へ反映する
        /// Undo/Redoでストローク列を再現するために使う
        /// </summary>
        /// <param name="centers">各ストロークの中心（ワールド座標）</param>
        /// <param name="radii">各ストロークの半径（ワールド単位）</param>
        /// <param name="colors">各ストロークの色</param>
        /// <param name="count">塗り直すストローク数</param>
        public void RepaintStrokes(
            IReadOnlyList<Vector3> centers,
            IReadOnlyList<float> radii,
            IReadOnlyList<Color> colors,
            int count)
        {
            for (int i = 0; i < count; i++)
            {
                PaintVoxelsInternal(centers[i], radii[i], colors[i], Vector3.zero, false);
            }

            // 表示中の経路に合わせて一度だけ反映する
            pendingPaintMeshUpdate = true;
            FlushPaintMesh(refreshCollider: true);
        }

        /// <summary>
        /// ボクセル色バッファのスナップショットを取得する（ペイントUndo案Bの土台 状態保存用）
        /// </summary>
        public Vector3[] GetVoxelColors()
        {
            if (!IsBackendReady)
            {
                return new Vector3[TotalVoxelCount];
            }

            return backend.GetVoxelColors();
        }

        /// <summary>
        /// ボクセル色バッファを指定のスナップショットで復元する（ペイントUndo案Bの土台）
        /// </summary>
        /// <param name="colors">復元する色バッファ 要素数が一致しない場合は無視する</param>
        public void SetVoxelColors(Vector3[] colors)
        {
            if (colors == null || colors.Length != TotalVoxelCount)
            {
                return;
            }

            backend.SetVoxelColors(colors);
            MarkAllChunksDirty();

            // 表示中の経路に合わせて反映する
            if (skinnedRenderer != null && skinnedRenderer.enabled)
            {
                ApplyShapeOnly(GenerateMeshData(), refreshCollider: true);
                PublishHasMeshIfChanged();
            }
            else
            {
                RebuildDirtyChunks(refreshColliders: true, maxChunks: int.MaxValue);
                RefreshCachedTriangleCountFromChunks();
            }
        }

        public float[] GetVoxelData()
        {
            if (!IsBackendReady)
            {
                return new float[TotalVoxelCount];
            }

            return backend.GetVoxelData();
        }

        public void SetVoxelData(float[] data)
        {
            if (!IsBackendReady)
            {
                return;
            }

            backend.SetVoxelData(data);
            MarkAllChunksDirty();
        }

        public void ClearAllVoxels()
        {
            if (!IsBackendReady)
            {
                return;
            }

            backend.ClearAllVoxels();
            MarkAllChunksDirty();
        }

        public void Modify(Vector3 localPos, float radius, float strength)
        {
            if (!IsBackendReady)
            {
                return;
            }

            backend.Modify(localPos, radius, strength);
            MarkDirtyChunks(localPos, radius);
        }

        private void RefreshMeshCollider()
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
            Physics.SyncTransforms();
        }

        /// <summary>
        /// ワールド空間メッシュを造形グリッドへボクセル化する
        /// </summary>
        /// <param name="mesh">変換元メッシュ</param>
        /// <param name="engineSpaceTransform">造形ローカル座標の基準Transform</param>
        /// <param name="meshTransform">メッシュのTransform</param>
        /// <param name="meshVertexColors">頂点カラーnull時は形状のみ復元し既定の粘土色を使う</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <param name="applyAutoOrientation">trueのときY-up自動補正を行う</param>
        /// <param name="fitToGrid">trueならグリッドへ拡大縮小してフィットするfalseなら元サイズを保つ</param>
        /// <param name="importScale">フィット後の一様スケール倍率1が既定のフィットサイズ</param>
        /// <returns>成功した場合trueと空文字失敗時はfalseと理由</returns>
        public async UniTask<(bool success, string errorMessage)> TryImportFromWorldMeshAsync(
            Mesh mesh,
            Transform engineSpaceTransform,
            Transform meshTransform,
            Color[] meshVertexColors,
            CancellationToken cancellationToken,
            bool applyAutoOrientation = true,
            bool fitToGrid = true,
            float importScale = 1f)
        {
            if (!IsBackendReady || mesh == null || engineSpaceTransform == null || meshTransform == null)
            {
                return (false, "ボクセル化の準備ができていません");
            }

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices == null || vertices.Length == 0 || triangles == null || triangles.Length < 3)
            {
                return (false, "メッシュの頂点または三角形がありません");
            }

            bool useVertexColors = meshVertexColors != null && meshVertexColors.Length == vertices.Length;
            Color[] vertexColors = null;
            if (useVertexColors)
            {
                vertexColors = new Color[meshVertexColors.Length];
                for (int i = 0; i < meshVertexColors.Length; i++)
                {
                    vertexColors[i] = PaintColorUtility.ToStorageColor(meshVertexColors[i]);
                }
            }

            Matrix4x4 meshLocalToEngine = engineSpaceTransform.worldToLocalMatrix * meshTransform.localToWorldMatrix;
            Color defaultStorageColor = PaintColorUtility.ToStorageColor(defaultVertexColor);
            Vector3 defaultColorVector = new Vector3(defaultStorageColor.r, defaultStorageColor.g, defaultStorageColor.b);

            ClayVoxelMeshEngineSpacePreparer.PreparedMesh preparedMesh = ClayVoxelMeshEngineSpacePreparer.Prepare(
                vertices,
                triangles,
                meshLocalToEngine,
                boundsSize,
                applyAutoOrientation,
                fitToGrid,
                importScale);
            Vector3 gridHalf = Vector3.one * CenterOffset.x;
            Vector3 gridMin = -gridHalf;
            Vector3 gridMax = gridHalf;

            int gridCount = size + 1;
            var request = new ClayVoxelMeshImporter.Request(
                preparedMesh.FittedEngineVertices,
                preparedMesh.Triangles,
                vertexColors,
                gridCount,
                TotalVoxelCount,
                Scale,
                CenterOffset,
                gridMin,
                gridMax,
                defaultColorVector,
                useFittedEngineVertices: true);

            ClayVoxelMeshImporter.Result result = await UniTask.Run(
                () => ClayVoxelMeshSurfaceImporter.Compute(request, cancellationToken),
                cancellationToken: cancellationToken);

            if (!result.HasSolid)
            {
                return (false, "メッシュをボクセル化できませんでした");
            }

            ClearDisplayedMesh();
            SetVoxelData(result.Voxels);
            if (useVertexColors && result.Colors != null && result.Colors.Length == TotalVoxelCount)
            {
                SetVoxelColors(result.Colors);
            }
            else
            {
                ResetVoxelColors();
            }

            FlushShape();
            return (HasMesh(), HasMesh() ? string.Empty : "メッシュをボクセル化できませんでした");
        }

        /// <summary>
        /// 保存済みボクセルスナップショットを造形グリッドへ復元する
        /// </summary>
        /// <param name="voxels">ボクセル密度配列</param>
        /// <param name="colors">ボクセル色配列 nullのとき既定色を使う</param>
        /// <param name="snapshotGridSize">保存時のグリッドセル数</param>
        /// <param name="snapshotBoundsSize">保存時のグリッドワールドサイズ</param>
        /// <returns>成功した場合trueと空文字失敗時はfalseと理由</returns>
        public (bool success, string errorMessage) TryRestoreVoxelSnapshot(
            float[] voxels,
            Vector3[] colors,
            int snapshotGridSize,
            float snapshotBoundsSize)
        {
            if (!IsBackendReady)
            {
                return (false, "ボクセル化の準備ができていません");
            }

            if (voxels == null || voxels.Length != TotalVoxelCount)
            {
                return (false, "ボクセルデータの要素数が一致しません");
            }

            if (snapshotGridSize != size || Mathf.Abs(snapshotBoundsSize - boundsSize) > 0.001f)
            {
                return (false, "保存時と造形グリッド設定が異なります");
            }

            ClearDisplayedMesh();
            SetVoxelData(voxels);
            if (colors != null && colors.Length == TotalVoxelCount)
            {
                SetVoxelColors(colors);
            }
            else
            {
                ResetVoxelColors();
            }

            FlushShape();
            return (HasMesh(), HasMesh() ? string.Empty : "復元後に造形メッシュがありません");
        }

        private void ClearDisplayedMesh()
        {
            ClearBrushSculptPreview();

            if (mesh != null)
            {
                mesh.Clear();
            }

            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
            }

            cachedTotalTriangleCount = 0;
        }
    }
}