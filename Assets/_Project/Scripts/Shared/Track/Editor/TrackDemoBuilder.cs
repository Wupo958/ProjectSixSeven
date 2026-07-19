using UnityEditor;
using UnityEngine;
using ProjectSixSeven.Shared.Track;

namespace ProjectSixSeven.Shared.Track.Editor
{
    /// Spawns a throwaway demo track with a block train, so the rail system can be inspected
    /// without hand-wiring a scene.
    public static class TrackDemoBuilder
    {
        [MenuItem("Tools/ProjectSixSeven/Create Demo Track")]
        private static void CreateDemoTrack()
        {
            GameObject root = new GameObject("DemoTrack");

            TrackBuilder builder = root.AddComponent<TrackBuilder>();

            (Vector3 position, float heading)[] layout =
            {
                (new Vector3(0f, 0f, 0f), 0f),
                (new Vector3(1200f, 8f, 600f), 45f),
                (new Vector3(2400f, 16f, 200f), 120f),
                (new Vector3(1800f, 10f, -1000f), 200f),
                (new Vector3(200f, 0f, -800f), 280f)
            };

            TrackNode[] nodes = new TrackNode[layout.Length];
            for (int i = 0; i < layout.Length; i++)
            {
                GameObject nodeObject = new GameObject($"Node_{i}");
                nodeObject.transform.SetParent(root.transform);
                nodeObject.transform.position = layout[i].position;
                nodeObject.transform.rotation = Quaternion.Euler(0f, layout[i].heading, 0f);
                nodes[i] = nodeObject.AddComponent<TrackNode>();
            }

            SerializedObject serialized = new SerializedObject(builder);
            SerializedProperty nodeList = serialized.FindProperty("_nodes");
            nodeList.arraySize = nodes.Length;
            for (int i = 0; i < nodes.Length; i++)
            {
                nodeList.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
            }

            serialized.FindProperty("_mode").enumValueIndex = (int)TrackBuilder.TrackMode.Loop;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            builder.Rebuild();

            GameObject train = GameObject.CreatePrimitive(PrimitiveType.Cube);
            train.name = "TrainBlock";
            train.transform.SetParent(root.transform);
            train.transform.localScale = new Vector3(3f, 4f, 24f);

            TrainFollower follower = train.AddComponent<TrainFollower>();
            SerializedObject followerSerialized = new SerializedObject(follower);
            followerSerialized.FindProperty("_track").objectReferenceValue = builder;
            followerSerialized.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root, "Create Demo Track");
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log($"Demo track built: {builder.Path.Length:F0}m, " +
                      $"steepest grade {builder.Path.SteepestGrade() * 100f:F2}%.");
        }
    }
}
