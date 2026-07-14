using ClayEditor.Rigging.Interface;
using System.Collections.Generic;
using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// 段階2の実装
    /// メッシュの接続性を溶接で復元し 端点からの測地距離による等測地間隔スライスから
    /// カーブスケルトン(分岐を含む中心線)を抽出してボーン階層を生成する
    /// (A)等測地間隔スライス (B)ノード位置の枝ごと平滑化 (D)子方向への向き付け に対応
    /// (P)ノード接続は上流スライスの最近傍を親とするMST的接続でX字交差を防ぐ
    /// (R)平滑化を強化し短い枝の最小ノード数を保証する
    /// 浮遊塊を除外するため 最大の連結成分(本体)だけを対象にする
    /// ルートはグラフ中心(離心率最小ノード)とし 腕や脚の本数に依存せず体の中心に置く
    /// </summary>
    public sealed class BoneSkeletonGenerator : IBoneSkeletonGenerator
    {
        private const string BoneNamePrefix = "AutoBone_";
        private const float WeldEpsilon = 0.01f;
        private const int SmoothIterations = 6;
        private const float SmoothStrength = 0.5f;

        private class SkeletonNode
        {
            public Vector3 position;            // meshRootローカル座標
            public int slice;
            public int parent = -1;             // 親ノード(上流スライスの最近傍)
            public readonly List<int> children = new();
        }

        /// <inheritdoc />
        public SkeletonGenerationResult Generate(Vector3[] localVertices, Transform meshRoot, Transform boneParent, int jointCount)
        {
            SkeletonGenerationResult empty = new SkeletonGenerationResult
            {
                Bones = new Transform[0],
                Topology = null
            };

            if (localVertices == null || localVertices.Length < 3 || meshRoot == null || boneParent == null)
            {
                return empty;
            }

            ClearChildren(boneParent);

            // 三角形スープを溶接して接続性を復元する
            MeshTopology topo = MeshTopology.Build(localVertices, WeldEpsilon);
            int n = topo.Positions.Length;
            if (n < 2)
            {
                return empty;
            }

            // 最大の連結成分(本体)に属する頂点だけを対象にする(浮遊塊を除外する)
            bool[] inMainComponent = FindLargestComponent(topo);

            // 測地距離の始点を二重BFS(Dijkstra)で決める(本体内の長手方向の端点を取る)
            int seed = FindFarthestVertex(topo, FirstVertexInMain(inMainComponent), inMainComponent);
            int source = FindFarthestVertex(topo, seed, inMainComponent);

            // sourceからの測地距離(辺長重み) 本体外は無限大のまま残る
            float[] dist = Dijkstra(topo, source, inMainComponent);
            float maxDist = 0f;
            for (int i = 0; i < n; i++)
            {
                if (!float.IsInfinity(dist[i]) && dist[i] > maxDist)
                {
                    maxDist = dist[i];
                }
            }

            if (maxDist <= 0f)
            {
                return empty;
            }

            // (A)等測地間隔でスライスに分ける 本体外(無限大)は-1にして無視する
            int sliceCount = Mathf.Max(jointCount, 4);
            float sliceWidth = maxDist / sliceCount;
            int[] sliceOf = new int[n];
            for (int i = 0; i < n; i++)
            {
                if (float.IsInfinity(dist[i]) || !inMainComponent[i])
                {
                    sliceOf[i] = -1;
                    continue;
                }

                sliceOf[i] = Mathf.Clamp((int)(dist[i] / sliceWidth), 0, sliceCount - 1);
            }

            // 各スライス内の連結成分を1ノードにする(分岐はここで分かれる)
            int[] nodeOf = new int[n];
            for (int i = 0; i < n; i++)
            {
                nodeOf[i] = -1;
            }

            var nodes = new List<SkeletonNode>();
            var nodeGeoDist = new List<float>();
            var queue = new Queue<int>();

            for (int i = 0; i < n; i++)
            {
                if (sliceOf[i] < 0 || nodeOf[i] != -1)
                {
                    continue;
                }

                int currentSlice = sliceOf[i];
                int nodeId = nodes.Count;
                Vector3 sum = Vector3.zero;
                float distSum = 0f;
                int cnt = 0;

                queue.Clear();
                queue.Enqueue(i);
                nodeOf[i] = nodeId;

                while (queue.Count > 0)
                {
                    int v = queue.Dequeue();
                    sum += topo.Positions[v];
                    distSum += dist[v];
                    cnt++;

                    foreach (int nb in topo.Adjacency[v])
                    {
                        if (sliceOf[nb] == currentSlice && nodeOf[nb] == -1)
                        {
                            nodeOf[nb] = nodeId;
                            queue.Enqueue(nb);
                        }
                    }
                }

                nodes.Add(new SkeletonNode
                {
                    position = sum / Mathf.Max(cnt, 1),
                    slice = currentSlice
                });
                nodeGeoDist.Add(distSum / Mathf.Max(cnt, 1));
            }

            int nodeCount = nodes.Count;
            if (nodeCount == 0)
            {
                return empty;
            }

            // メッシュ上で隣接するノードのペアを集める(接続候補)
            var adjacentNodes = BuildNodeAdjacency(topo, nodeOf, nodeCount);

            // (P)各ノードの親を 測地距離がより小さい隣接ノードのうち最も近いものに決める
            int baseRoot = AssignParentsByUpstream(nodes, nodeGeoDist, adjacentNodes);

            // 親子関係から子リストを作る
            for (int i = 0; i < nodeCount; i++)
            {
                if (nodes[i].parent >= 0)
                {
                    nodes[nodes[i].parent].children.Add(i);
                }
            }

            // (R)短い枝でもボーンが2個以上になるよう 長い辺の中間にノードを補間する
            InsertIntermediateNodes(nodes, sliceWidth);

            // (案X)ルートはグラフ中心(離心率最小ノード)とする
            int rootNode = FindGraphCenter(nodes);

            // ルートを基準に親子関係を張り直す(中心から各枝が伸びる向きに統一)
            RerootTree(nodes, rootNode);

            // (B)(R)ノード位置をツリー上で平滑化して中心線のガタつきを抑える
            SmoothNodePositions(nodes, rootNode);

            // ツリーからボーンを生成し (D)子方向へ向き付けする
            return BuildBones(nodes, rootNode, meshRoot, boneParent, topo);
        }

        // 溶接トポロジーの連結成分のうち 最も頂点数の多い成分に属するかどうかを返す
        private bool[] FindLargestComponent(MeshTopology topo)
        {
            int n = topo.Positions.Length;
            int[] label = new int[n];
            for (int i = 0; i < n; i++)
            {
                label[i] = -1;
            }

            var componentSizes = new List<int>();
            var queue = new Queue<int>();

            for (int start = 0; start < n; start++)
            {
                if (label[start] != -1)
                {
                    continue;
                }

                int comp = componentSizes.Count;
                int count = 0;

                queue.Clear();
                queue.Enqueue(start);
                label[start] = comp;

                while (queue.Count > 0)
                {
                    int v = queue.Dequeue();
                    count++;

                    foreach (int nb in topo.Adjacency[v])
                    {
                        if (label[nb] == -1)
                        {
                            label[nb] = comp;
                            queue.Enqueue(nb);
                        }
                    }
                }

                componentSizes.Add(count);
            }

            // 最大成分を求める
            int largest = 0;
            int largestSize = -1;
            for (int c = 0; c < componentSizes.Count; c++)
            {
                if (componentSizes[c] > largestSize)
                {
                    largestSize = componentSizes[c];
                    largest = c;
                }
            }

            bool[] inMain = new bool[n];
            for (int i = 0; i < n; i++)
            {
                inMain[i] = label[i] == largest;
            }

            return inMain;
        }

        // 本体に属する最初の頂点インデックスを返す(始点探索の起点)
        private int FirstVertexInMain(bool[] inMain)
        {
            for (int i = 0; i < inMain.Length; i++)
            {
                if (inMain[i])
                {
                    return i;
                }
            }

            return 0;
        }

        // メッシュ隣接からノード隣接(集合)を作る
        private List<HashSet<int>> BuildNodeAdjacency(MeshTopology topo, int[] nodeOf, int nodeCount)
        {
            var adjacent = new List<HashSet<int>>(nodeCount);
            for (int i = 0; i < nodeCount; i++)
            {
                adjacent.Add(new HashSet<int>());
            }

            int n = topo.Positions.Length;
            for (int v = 0; v < n; v++)
            {
                int nv = nodeOf[v];
                if (nv < 0)
                {
                    continue;
                }

                foreach (int nb in topo.Adjacency[v])
                {
                    int nnb = nodeOf[nb];
                    if (nnb < 0 || nnb == nv)
                    {
                        continue;
                    }

                    adjacent[nv].Add(nnb);
                    adjacent[nnb].Add(nv);
                }
            }

            return adjacent;
        }

        // (P)各ノードの親を 測地距離がより小さい隣接ノードのうち最も空間的に近いものにする
        private int AssignParentsByUpstream(List<SkeletonNode> nodes, List<float> geoDist, List<HashSet<int>> adjacent)
        {
            int nodeCount = nodes.Count;
            int baseRoot = 0;
            float minGeo = float.MaxValue;

            for (int i = 0; i < nodeCount; i++)
            {
                int best = -1;
                float bestSpatial = float.MaxValue;

                foreach (int nb in adjacent[i])
                {
                    if (geoDist[nb] < geoDist[i])
                    {
                        float d = (nodes[nb].position - nodes[i].position).sqrMagnitude;
                        if (d < bestSpatial)
                        {
                            bestSpatial = d;
                            best = nb;
                        }
                    }
                }

                nodes[i].parent = best;

                if (best < 0 && geoDist[i] < minGeo)
                {
                    minGeo = geoDist[i];
                    baseRoot = i;
                }
            }

            nodes[baseRoot].parent = -1;
            return baseRoot;
        }

        // (R)親子の辺が長い場合 中間にノードを挿入して枝のノード数を増やす
        private void InsertIntermediateNodes(List<SkeletonNode> nodes, float sliceWidth)
        {
            float threshold = sliceWidth * 1.5f;

            int original = nodes.Count;
            for (int i = 0; i < original; i++)
            {
                int parent = nodes[i].parent;
                if (parent < 0)
                {
                    continue;
                }

                float len = Vector3.Distance(nodes[i].position, nodes[parent].position);
                if (len <= threshold)
                {
                    continue;
                }

                int segments = Mathf.Clamp(Mathf.RoundToInt(len / sliceWidth), 2, 6);
                int childIndex = i;

                for (int s = segments - 1; s >= 1; s--)
                {
                    float t = (float)s / segments;
                    Vector3 pos = Vector3.Lerp(nodes[parent].position, nodes[i].position, t);

                    var mid = new SkeletonNode
                    {
                        position = pos,
                        slice = nodes[i].slice,
                        parent = parent
                    };

                    int midIndex = nodes.Count;
                    nodes.Add(mid);

                    nodes[childIndex].parent = midIndex;
                    childIndex = midIndex;
                }
            }
        }

        // ルートを基準にBFSで親子関係を張り直す
        private void RerootTree(List<SkeletonNode> nodes, int rootNode)
        {
            int nodeCount = nodes.Count;

            var undirected = new List<int>[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                undirected[i] = new List<int>();
            }

            for (int i = 0; i < nodeCount; i++)
            {
                int p = nodes[i].parent;
                if (p >= 0)
                {
                    undirected[i].Add(p);
                    undirected[p].Add(i);
                }
            }

            for (int i = 0; i < nodeCount; i++)
            {
                nodes[i].parent = -1;
                nodes[i].children.Clear();
            }

            var visited = new bool[nodeCount];
            var bfs = new Queue<int>();
            bfs.Enqueue(rootNode);
            visited[rootNode] = true;

            while (bfs.Count > 0)
            {
                int cur = bfs.Dequeue();
                foreach (int nb in undirected[cur])
                {
                    if (visited[nb])
                    {
                        continue;
                    }

                    visited[nb] = true;
                    nodes[nb].parent = cur;
                    nodes[cur].children.Add(nb);
                    bfs.Enqueue(nb);
                }
            }
        }

        // (案X)ツリー上の離心率最小ノードを返す
        private int FindGraphCenter(List<SkeletonNode> nodes)
        {
            int count = nodes.Count;
            if (count == 1)
            {
                return 0;
            }

            var undirected = new List<int>[count];
            for (int i = 0; i < count; i++)
            {
                undirected[i] = new List<int>();
            }
            for (int i = 0; i < count; i++)
            {
                int p = nodes[i].parent;
                if (p >= 0)
                {
                    undirected[i].Add(p);
                    undirected[p].Add(i);
                }
            }

            var leaves = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (undirected[i].Count <= 1)
                {
                    leaves.Add(i);
                }
            }
            if (leaves.Count == 0)
            {
                for (int i = 0; i < count; i++)
                {
                    leaves.Add(i);
                }
            }

            float[] eccentricity = new float[count];
            foreach (int leaf in leaves)
            {
                int[] hop = BfsHops(undirected, leaf, count);
                for (int i = 0; i < count; i++)
                {
                    if (hop[i] >= 0 && hop[i] > eccentricity[i])
                    {
                        eccentricity[i] = hop[i];
                    }
                }
            }

            int center = 0;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (eccentricity[i] < best)
                {
                    best = eccentricity[i];
                    center = i;
                }
            }

            return center;
        }

        private int[] BfsHops(List<int>[] undirected, int source, int count)
        {
            int[] hop = new int[count];
            for (int i = 0; i < count; i++)
            {
                hop[i] = -1;
            }

            hop[source] = 0;
            var q = new Queue<int>();
            q.Enqueue(source);

            while (q.Count > 0)
            {
                int u = q.Dequeue();
                foreach (int v in undirected[u])
                {
                    if (hop[v] < 0)
                    {
                        hop[v] = hop[u] + 1;
                        q.Enqueue(v);
                    }
                }
            }

            return hop;
        }

        // (B)(R)隣接ノード間で位置を平均してスケルトンを滑らかにする(端点とルートは固定)
        private void SmoothNodePositions(List<SkeletonNode> nodes, int rootNode)
        {
            int count = nodes.Count;
            Vector3[] buffer = new Vector3[count];

            for (int iter = 0; iter < SmoothIterations; iter++)
            {
                for (int i = 0; i < count; i++)
                {
                    var node = nodes[i];
                    int degree = node.children.Count + (node.parent >= 0 ? 1 : 0);

                    if (degree <= 1 || i == rootNode)
                    {
                        buffer[i] = node.position;
                        continue;
                    }

                    Vector3 avg = Vector3.zero;
                    int c = 0;
                    if (node.parent >= 0)
                    {
                        avg += nodes[node.parent].position;
                        c++;
                    }
                    foreach (int ch in node.children)
                    {
                        avg += nodes[ch].position;
                        c++;
                    }
                    avg /= Mathf.Max(c, 1);

                    buffer[i] = Vector3.Lerp(node.position, avg, SmoothStrength);
                }

                for (int i = 0; i < count; i++)
                {
                    nodes[i].position = buffer[i];
                }
            }
        }

        // ツリーからボーンを生成し 子方向へ向き付けする
        private SkeletonGenerationResult BuildBones(List<SkeletonNode> nodes, int rootNode, Transform meshRoot, Transform boneParent, MeshTopology topo)
        {
            int nodeCount = nodes.Count;
            Transform[] boneByNode = new Transform[nodeCount];

            var bones = new List<Transform>();
            var visited = new bool[nodeCount];
            var bfs = new Queue<int>();

            CreateBone(rootNode, boneParent, nodes, meshRoot, boneByNode, bones);
            visited[rootNode] = true;
            bfs.Enqueue(rootNode);

            while (bfs.Count > 0)
            {
                int cur = bfs.Dequeue();
                foreach (int nx in nodes[cur].children)
                {
                    if (visited[nx])
                    {
                        continue;
                    }

                    visited[nx] = true;
                    CreateBone(nx, boneByNode[cur], nodes, meshRoot, boneByNode, bones);
                    bfs.Enqueue(nx);
                }
            }

            // 取りこぼし(到達しないノード)はルート親直下に置く
            for (int i = 0; i < nodeCount; i++)
            {
                if (!visited[i])
                {
                    visited[i] = true;
                    CreateBone(i, boneParent, nodes, meshRoot, boneByNode, bones);
                }
            }

            // (D)子方向へ向き付け(親->子の順で回転と位置を確定し伝播でずれないようにする)
            OrientBones(nodes, boneByNode, meshRoot, rootNode);

            return new SkeletonGenerationResult
            {
                Bones = bones.ToArray(),
                Topology = topo
            };
        }

        private void CreateBone(int nodeId, Transform parent, List<SkeletonNode> nodes, Transform meshRoot, Transform[] boneByNode, List<Transform> bones)
        {
            GameObject obj = new GameObject(BoneNamePrefix + nodeId);
            obj.transform.SetParent(parent, true);
            obj.transform.position = meshRoot.TransformPoint(nodes[nodeId].position);
            obj.transform.rotation = Quaternion.identity;

            boneByNode[nodeId] = obj.transform;
            bones.Add(obj.transform);
        }

        // (D)子方向へボーンを向ける 親->子(BFS順)で回転と位置を確定し回転伝播でずれないようにする
        private void OrientBones(List<SkeletonNode> nodes, Transform[] boneByNode, Transform meshRoot, int rootNode)
        {
            int nodeCount = nodes.Count;

            Vector3[] worldPos = new Vector3[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                worldPos[i] = meshRoot.TransformPoint(nodes[i].position);
            }

            Quaternion[] targetRot = new Quaternion[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                Vector3 dir = Vector3.zero;
                if (nodes[i].children.Count > 0)
                {
                    foreach (int c in nodes[i].children)
                    {
                        dir += (worldPos[c] - worldPos[i]);
                    }
                }
                else if (nodes[i].parent >= 0)
                {
                    dir = worldPos[i] - worldPos[nodes[i].parent];
                }

                targetRot[i] = dir.sqrMagnitude > 1e-8f
                    ? Quaternion.LookRotation(dir.normalized, Vector3.up)
                    : Quaternion.identity;
            }

            var bfs = new Queue<int>();
            bool[] enqueued = new bool[nodeCount];
            bfs.Enqueue(rootNode);
            enqueued[rootNode] = true;

            while (bfs.Count > 0)
            {
                int i = bfs.Dequeue();
                if (boneByNode[i] != null)
                {
                    boneByNode[i].rotation = targetRot[i];
                    boneByNode[i].position = worldPos[i];
                }

                foreach (int c in nodes[i].children)
                {
                    if (!enqueued[c])
                    {
                        enqueued[c] = true;
                        bfs.Enqueue(c);
                    }
                }
            }

            for (int i = 0; i < nodeCount; i++)
            {
                if (!enqueued[i] && boneByNode[i] != null)
                {
                    boneByNode[i].rotation = targetRot[i];
                    boneByNode[i].position = worldPos[i];
                }
            }
        }

        // 始点から最も測地的に遠い頂点を返す(本体内に限定)
        private int FindFarthestVertex(MeshTopology topo, int start, bool[] inMain)
        {
            float[] dist = Dijkstra(topo, start, inMain);
            int best = start;
            float bestDist = 0f;

            for (int i = 0; i < dist.Length; i++)
            {
                if (!float.IsInfinity(dist[i]) && dist[i] > bestDist)
                {
                    bestDist = dist[i];
                    best = i;
                }
            }

            return best;
        }

        // 辺長を重みとした最短距離(頂点グラフDijkstra) 本体外の頂点へは進まない
        private float[] Dijkstra(MeshTopology topo, int source, bool[] inMain)
        {
            int n = topo.Positions.Length;
            float[] dist = new float[n];
            for (int i = 0; i < n; i++)
            {
                dist[i] = float.PositiveInfinity;
            }

            dist[source] = 0f;
            var heap = new MinHeap(n);
            heap.Push(source, 0f);

            while (heap.Count > 0)
            {
                heap.Pop(out int u, out float d);
                if (d > dist[u])
                {
                    continue;
                }

                Vector3 pu = topo.Positions[u];
                foreach (int v in topo.Adjacency[u])
                {
                    // 本体外の頂点へは進まない
                    if (!inMain[v])
                    {
                        continue;
                    }

                    float nd = d + Vector3.Distance(pu, topo.Positions[v]);
                    if (nd < dist[v])
                    {
                        dist[v] = nd;
                        heap.Push(v, nd);
                    }
                }
            }

            return dist;
        }

        private void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(parent.GetChild(i).gameObject);
            }
        }

        // Dijkstra用の最小ヒープ
        private sealed class MinHeap
        {
            private int[] items;
            private float[] keys;
            private int count;

            public int Count => count;

            public MinHeap(int capacity)
            {
                int c = Mathf.Max(capacity, 16);
                items = new int[c];
                keys = new float[c];
                count = 0;
            }

            public void Push(int item, float key)
            {
                if (count == items.Length)
                {
                    System.Array.Resize(ref items, items.Length * 2);
                    System.Array.Resize(ref keys, keys.Length * 2);
                }

                items[count] = item;
                keys[count] = key;
                int i = count;
                count++;

                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (keys[parent] <= keys[i])
                    {
                        break;
                    }

                    Swap(i, parent);
                    i = parent;
                }
            }

            public void Pop(out int item, out float key)
            {
                item = items[0];
                key = keys[0];
                count--;
                items[0] = items[count];
                keys[0] = keys[count];

                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1;
                    int r = i * 2 + 2;
                    int smallest = i;

                    if (l < count && keys[l] < keys[smallest])
                    {
                        smallest = l;
                    }

                    if (r < count && keys[r] < keys[smallest])
                    {
                        smallest = r;
                    }

                    if (smallest == i)
                    {
                        break;
                    }

                    Swap(i, smallest);
                    i = smallest;
                }
            }

            private void Swap(int a, int b)
            {
                int ti = items[a];
                items[a] = items[b];
                items[b] = ti;

                float tk = keys[a];
                keys[a] = keys[b];
                keys[b] = tk;
            }
        }
    }
}