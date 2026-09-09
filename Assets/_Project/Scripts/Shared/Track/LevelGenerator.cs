using System.Collections.Generic;
using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    /// Builds a whole level from a seed: a wobbly point-to-point track of roughly a target length, a
    /// goal marker at the far end, and primitive decor (trees, rocks, hills) scattered in a corridor
    /// either side of the line.
    ///
    /// Nothing here is networked. Every client runs the same seed and gets a byte-identical layout,
    /// and the train rides the deterministic track exactly as it does for a hand-placed one — so the
    /// level "just works" in multiplayer without any extra syncing.
    ///
    /// Placeholder art only: everything is a coloured Unity primitive, so it drops in with no assets.
    [RequireComponent(typeof(TrackBuilder))]
    public sealed class LevelGenerator : MonoBehaviour
    {
        [Header("Seed")]
        [Tooltip("Same seed => same level on every client. Change it for a different layout.")]
        [SerializeField] private int _seed = 12345;
        [SerializeField] private bool _generateOnAwake = true;

        [Header("Track shape")]
        [Tooltip("Roughly how long the track should be, in metres.")]
        [SerializeField] private float _trackLength = 2400f;
        [Tooltip("Spacing between control nodes, in metres. Keep it well above the track's transition length.")]
        [SerializeField] private float _nodeSpacing = 200f;
        [Tooltip("How far the track may swing left/right at each node, in degrees. Kept gentle so it stays above the min radius.")]
        [SerializeField] private float _wobble = 20f;

        [Header("Decor counts")]
        [SerializeField] private int _treeCount = 260;
        [SerializeField] private int _rockCount = 110;

        [Header("Decor placement")]
        [Tooltip("Clear space kept either side of the track centre before decor may appear, in metres.")]
        [SerializeField] private float _trackClearance = 9f;
        [Tooltip("How far beyond the clearance decor may be scattered, in metres.")]
        [SerializeField] private float _corridorWidth = 60f;

        private const string ContainerName = "GeneratedLevel";

        private System.Random _rng;
        private Transform _container;
        private Material _material;

        private void Awake()
        {
            if (_generateOnAwake)
            {
                Generate();
            }
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            _rng = new System.Random(_seed);
            ResetContainer();
            EnsureMaterial();

            TrackBuilder track = GetComponent<TrackBuilder>();

            // Each node gets a straight easement lead-in and lead-out; if two of those overlap (node
            // spacing < 2x easement) the solver connects them with a min-radius loop. Keep the easement
            // a fifth of the spacing so the arc between them always has room.
            track.TransitionLength = _nodeSpacing * 0.2f;

            // The arc between the easements only spans ~60% of the spacing, so clamp the per-node turn
            // to what that length can bend at the min radius, with margin, to guarantee no loops.
            float coreLength = _nodeSpacing * 0.6f;
            float maxTurn = 2f * Mathf.Asin(Mathf.Clamp01(coreLength / (2f * track.MinRadius))) * Mathf.Rad2Deg;
            float wobble = Mathf.Min(_wobble, maxTurn * 0.85f);

            track.Configure(BuildTrackNodes(wobble), TrackBuilder.TrackMode.PointToPoint);

            // The visible rails are a separate mesh; rebuild it against the new path.
            TrackMeshBuilder mesh = GetComponent<TrackMeshBuilder>();
            if (mesh != null)
            {
                mesh.Build();
            }

            TrackPath path = track.Path;
            if (path == null || !path.IsValid)
            {
                Debug.LogWarning("[LevelGenerator] Track failed to solve; skipping goal and decor.", this);
                return;
            }

            PlaceGoal(path);
            ScatterDecor(path);
        }

        // --- Track ---------------------------------------------------------------------------------

        private List<TrackNode> BuildTrackNodes(float maxWobble)
        {
            List<Vector3> points = new List<Vector3>();

            Vector3 position = transform.position;
            float heading = 0f;   // degrees around Y; 0 points along +Z
            float level = position.y;   // the track stays flat at the generator's height
            points.Add(position);

            float travelled = 0f;
            int guard = 0;
            while (travelled < _trackLength && guard++ < 10000)
            {
                // Wobble the heading but keep it within a forward cone, so the line snakes without ever
                // doubling back on itself. Clamping only ever reduces the turn, so it stays loop-safe.
                heading = Mathf.Clamp(heading + Range(-maxWobble, maxWobble), -50f, 50f);

                Vector3 step = Quaternion.Euler(0f, heading, 0f) * Vector3.forward * _nodeSpacing;
                position = new Vector3(position.x + step.x, level, position.z + step.z);
                points.Add(position);
                travelled += _nodeSpacing;
            }

            List<TrackNode> nodes = new List<TrackNode>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                // Tangent from a central difference so the solver curves smoothly through each node.
                Vector3 forward;
                if (i == 0)
                {
                    forward = points[1] - points[0];
                }
                else if (i == points.Count - 1)
                {
                    forward = points[i] - points[i - 1];
                }
                else
                {
                    forward = points[i + 1] - points[i - 1];
                }

                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f)
                {
                    forward = Vector3.forward;
                }

                GameObject go = new GameObject($"Node_{i}");
                go.transform.SetParent(_container, true);
                go.transform.SetPositionAndRotation(
                    points[i], Quaternion.LookRotation(forward.normalized, Vector3.up));
                nodes.Add(go.AddComponent<TrackNode>());
            }

            return nodes;
        }

        // --- Goal ----------------------------------------------------------------------------------

        private void PlaceGoal(TrackPath path)
        {
            TrackSample end = path.Sample(path.Length);

            GameObject goal = Spawn(PrimitiveType.Cube, end.Position + Vector3.up * 6f,
                new Vector3(5f, 12f, 5f), Quaternion.identity, new Color(1f, 0.82f, 0.05f));
            goal.name = "Goal";
        }

        // --- Decor ---------------------------------------------------------------------------------

        private void ScatterDecor(TrackPath path)
        {
            for (int i = 0; i < _treeCount; i++)
            {
                Vector3 ground = CorridorPoint(path, out _);
                float h = Range(4f, 9f);
                float w = Range(0.8f, 1.7f);
                Spawn(PrimitiveType.Cube, ground + Vector3.up * (h * 0.5f - 1.5f),
                    new Vector3(w, h, w), Quaternion.identity,
                    new Color(Range(0.08f, 0.2f), Range(0.4f, 0.65f), Range(0.08f, 0.2f)));
            }

            for (int i = 0; i < _rockCount; i++)
            {
                Vector3 ground = CorridorPoint(path, out _);
                float s = Range(0.6f, 2.2f);
                float grey = Range(0.35f, 0.55f);
                Spawn(PrimitiveType.Cube, ground + Vector3.up * (s * 0.4f - 1.2f),
                    new Vector3(s, s * Range(0.5f, 0.9f), s),
                    Quaternion.Euler(Range(0f, 20f), Range(0f, 360f), Range(0f, 20f)),
                    new Color(grey, grey, grey * 0.95f));
            }
        }

        /// A random point out in the corridor beside the track, with its height taken from the track
        /// so decor roughly follows the terrain the line runs through.
        private Vector3 CorridorPoint(TrackPath path, out TrackSample sample)
        {
            sample = path.Sample(Range(0f, path.Length));

            Vector3 forward = sample.Forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            float side = _rng.NextDouble() < 0.5 ? -1f : 1f;
            float offset = _trackClearance + Range(0f, _corridorWidth);
            return sample.Position + right * (side * offset);
        }

        // --- Helpers -------------------------------------------------------------------------------

        private GameObject Spawn(PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(_container, true);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;

            // Decor is scenery only; colliders would just cost physics on hundreds of objects.
            DestroySafe(go.GetComponent<Collider>());

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color); // URP Lit
            block.SetColor("_Color", color);      // Built-in fallback
            renderer.SetPropertyBlock(block);

            return go;
        }

        private void EnsureMaterial()
        {
            if (_material != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _material = new Material(shader) { name = "GeneratedDecor" };
        }

        private void ResetContainer()
        {
            Transform existing = transform.Find(ContainerName);
            if (existing != null)
            {
                DestroySafe(existing.gameObject);
            }

            _container = new GameObject(ContainerName).transform;
            _container.SetParent(transform, false);
        }

        private float Range(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }

        private static void DestroySafe(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
