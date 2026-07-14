using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectSixSeven.Shared.Track.Editor
{
    /// Adds the mesh builder to the track in the open scene, hooks up the materials and builds once.
    public static class TrackMeshSetup
    {
        private const string MaterialFolder = "Assets/_Project/Materials";

        [MenuItem("Tools/ProjectSixSeven/Build Track Mesh")]
        private static void BuildTrackMesh()
        {
            TrackBuilder track = Object.FindFirstObjectByType<TrackBuilder>();
            if (track == null)
            {
                Debug.LogError("Build Track Mesh: no TrackBuilder in the open scene.");
                return;
            }

            TrackMeshBuilder mesh = track.GetComponent<TrackMeshBuilder>();
            if (mesh == null)
            {
                mesh = Undo.AddComponent<TrackMeshBuilder>(track.gameObject);
            }

            SerializedObject serialized = new SerializedObject(mesh);
            serialized.FindProperty("_track").objectReferenceValue = track;
            serialized.FindProperty("_railMaterial").objectReferenceValue = Load("RailTest_Rail");
            serialized.FindProperty("_sleeperMaterial").objectReferenceValue = Load("RailTest_Sleeper");
            serialized.FindProperty("_ballastMaterial").objectReferenceValue = Load("RailTest_Ballast");
            serialized.ApplyModifiedProperties();

            track.Rebuild();
            mesh.Build();

            EditorUtility.SetDirty(mesh);
            EditorSceneManager.MarkSceneDirty(track.gameObject.scene);
            Selection.activeGameObject = track.gameObject;
        }

        private static Material Load(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
            if (material == null)
            {
                Debug.LogWarning($"Build Track Mesh: missing material {name}.mat");
            }

            return material;
        }
    }
}
