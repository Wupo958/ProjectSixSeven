using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectSixSeven.Shared.Track
{
    [ExecuteAlways]
    public sealed class TrackMeshBuilder : MonoBehaviour
    {
        private const string ChunkPrefix = "TrackChunk_";

        [SerializeField] private TrackBuilder _track;

        [Header("Materials")]
        [SerializeField] private Material _railMaterial;
        [SerializeField] private Material _sleeperMaterial;
        [SerializeField] private Material _ballastMaterial;

        [Header("Rails")]
        [Tooltip("Distance between the inner faces of the rails. 1.435m is standard gauge.")]
        [SerializeField] private float _gauge = 1.435f;

        [Tooltip("Distance between swept rings along the rail. Smaller is smoother and heavier.")]
        [SerializeField] private float _railStep = 2f;

        [Header("Sleepers")]
        [SerializeField] private float _sleeperSpacing = 0.65f;
        [SerializeField] private float _sleeperLength = 2.6f;
        [SerializeField] private float _sleeperWidth = 0.26f;
        [SerializeField] private float _sleeperThickness = 0.16f;

        [Header("Ballast")]
        [SerializeField] private float _ballastTopWidth = 3.4f;
        [SerializeField] private float _ballastBottomWidth = 5.2f;
        [SerializeField] private float _ballastDepth = 0.5f;

        [Header("Chunking")]
        [SerializeField] private float _chunkLength = 200f;

        private static readonly Vector2[] RailProfile =
        {
            new Vector2(-0.070f, -0.170f),
            new Vector2(0.070f, -0.170f),
            new Vector2(0.070f, -0.130f),
            new Vector2(0.035f, -0.100f),
            new Vector2(0.035f, 0f),
            new Vector2(-0.035f, 0f),
            new Vector2(-0.035f, -0.100f),
            new Vector2(-0.070f, -0.130f)
        };

        private float RailTop => 0f;
        private float SleeperTop => RailTop - 0.170f;
        private float SleeperBottom => SleeperTop - _sleeperThickness;
        private float BallastTop => SleeperBottom;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                Build();
            }
        }

        [ContextMenu("Build Track Mesh")]
        public void Build()
        {
            if (_track == null)
            {
                Debug.LogError("TrackMeshBuilder: no TrackBuilder assigned.", this);
                return;
            }

            TrackPath path = _track.Path;
            if (path == null || !path.IsValid)
            {
                Debug.LogError("TrackMeshBuilder: the track has no valid path to build along.", this);
                return;
            }

            ClearChunks();

            int chunks = Mathf.Max(1, Mathf.CeilToInt(path.Length / Mathf.Max(10f, _chunkLength)));
            float chunkSpan = path.Length / chunks;

            for (int i = 0; i < chunks; i++)
            {
                float from = i * chunkSpan;
                float to = Mathf.Min(path.Length, from + chunkSpan);
                BuildChunk(path, i, from, to);
            }

            Debug.Log($"Track mesh built: {chunks} chunks over {path.Length:F0}m.", this);
        }

        private void BuildChunk(TrackPath path, int index, float from, float to)
        {
            MeshData data = new MeshData();

            AddSweep(data, path, from, to, data.Ballast, BallastSection);
            AddSweep(data, path, from, to, data.Rail, RailSection);
            AddSleepers(data, path, from, to);

            if (data.Vertices.Count == 0)
            {
                return;
            }

            Mesh mesh = new Mesh
            {
                name = $"{ChunkPrefix}{index}",
                indexFormat = IndexFormat.UInt32,
                subMeshCount = 3
            };

            mesh.SetVertices(data.Vertices);
            mesh.SetTriangles(data.Ballast, 0);
            mesh.SetTriangles(data.Sleeper, 1);
            mesh.SetTriangles(data.Rail, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject chunk = new GameObject($"{ChunkPrefix}{index}");
            chunk.transform.SetParent(transform, false);
            chunk.hideFlags = HideFlags.DontSave;

            chunk.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = chunk.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { _ballastMaterial, _sleeperMaterial, _railMaterial };
        }

        private void AddSweep(
            MeshData data,
            TrackPath path,
            float from,
            float to,
            List<int> triangles,
            System.Func<Vector3, Vector3, Vector3, List<Vector3[]>> section)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt((to - from) / Mathf.Max(0.25f, _railStep)));
            float step = (to - from) / steps;

            List<Vector3[]> previous = null;

            for (int i = 0; i <= steps; i++)
            {
                float distance = from + i * step;
                Frame(path, distance, out Vector3 position, out Vector3 right, out Vector3 up);

                List<Vector3[]> current = section(position, right, up);

                if (previous != null)
                {
                    for (int loop = 0; loop < current.Count; loop++)
                    {
                        StitchLoop(data, triangles, previous[loop], current[loop]);
                    }
                }

                previous = current;
            }
        }

        private static void StitchLoop(MeshData data, List<int> triangles, Vector3[] back, Vector3[] front)
        {
            int count = back.Length;
            int baseIndex = data.Vertices.Count;

            for (int i = 0; i < count; i++)
            {
                data.Vertices.Add(back[i]);
            }

            for (int i = 0; i < count; i++)
            {
                data.Vertices.Add(front[i]);
            }

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;

                int backA = baseIndex + i;
                int backB = baseIndex + next;
                int frontA = baseIndex + count + i;
                int frontB = baseIndex + count + next;

                triangles.Add(backA);
                triangles.Add(frontA);
                triangles.Add(backB);

                triangles.Add(backB);
                triangles.Add(frontA);
                triangles.Add(frontB);
            }
        }

        private List<Vector3[]> RailSection(Vector3 position, Vector3 right, Vector3 up)
        {
            List<Vector3[]> loops = new List<Vector3[]>(2);
            float centreOffset = (_gauge + 0.070f) * 0.5f;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] ring = new Vector3[RailProfile.Length];
                for (int i = 0; i < RailProfile.Length; i++)
                {
                    Vector2 p = RailProfile[i];
                    ring[i] = position + right * (side * centreOffset + p.x) + up * p.y;
                }

                loops.Add(ring);
            }

            return loops;
        }

        private List<Vector3[]> BallastSection(Vector3 position, Vector3 right, Vector3 up)
        {
            float top = BallastTop;
            float bottom = top - _ballastDepth;
            float halfTop = _ballastTopWidth * 0.5f;
            float halfBottom = _ballastBottomWidth * 0.5f;

            Vector3[] ring =
            {
                position + right * -halfTop + up * top,
                position + right * halfTop + up * top,
                position + right * halfBottom + up * bottom,
                position + right * -halfBottom + up * bottom
            };

            return new List<Vector3[]> { ring };
        }

        private void AddSleepers(MeshData data, TrackPath path, float from, float to)
        {
            float spacing = Mathf.Max(0.2f, _sleeperSpacing);

            float first = Mathf.Ceil(from / spacing) * spacing;

            for (float distance = first; distance < to; distance += spacing)
            {
                Frame(path, distance, out Vector3 position, out Vector3 right, out Vector3 up);
                Vector3 forward = Vector3.Cross(right, up).normalized;

                Vector3 centre = position + up * ((SleeperTop + SleeperBottom) * 0.5f);

                AddBox(
                    data,
                    data.Sleeper,
                    centre,
                    right * (_sleeperLength * 0.5f),
                    up * (_sleeperThickness * 0.5f),
                    forward * (_sleeperWidth * 0.5f));
            }
        }

        private static void AddBox(MeshData data, List<int> triangles, Vector3 centre, Vector3 x, Vector3 y, Vector3 z)
        {
            Vector3[] corners =
            {
                centre - x - y - z, centre + x - y - z, centre + x + y - z, centre - x + y - z,
                centre - x - y + z, centre + x - y + z, centre + x + y + z, centre - x + y + z
            };

            int[][] faces =
            {
                new[] { 0, 3, 2, 1 },
                new[] { 4, 5, 6, 7 },
                new[] { 0, 1, 5, 4 },
                new[] { 3, 7, 6, 2 },
                new[] { 1, 2, 6, 5 },
                new[] { 0, 4, 7, 3 }
            };

            foreach (int[] face in faces)
            {
                int baseIndex = data.Vertices.Count;

                foreach (int corner in face)
                {
                    data.Vertices.Add(corners[corner]);
                }

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }
        }

        private static void Frame(TrackPath path, float distance, out Vector3 position, out Vector3 right, out Vector3 up)
        {
            TrackSample sample = path.Sample(distance);

            position = sample.Position;
            right = Vector3.Cross(Vector3.up, sample.Forward).normalized;
            up = Vector3.Cross(sample.Forward, right).normalized;
        }

        private void ClearChunks()
        {
            List<GameObject> stale = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith(ChunkPrefix))
                {
                    stale.Add(child.gameObject);
                }
            }

            foreach (GameObject chunk in stale)
            {
                if (Application.isPlaying)
                {
                    Destroy(chunk);
                }
                else
                {
                    DestroyImmediate(chunk);
                }
            }
        }

        private sealed class MeshData
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Ballast = new List<int>();
            public readonly List<int> Sleeper = new List<int>();
            public readonly List<int> Rail = new List<int>();
        }
    }
}
