using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectSixSeven.Shared.Track;

namespace ProjectSixSeven.Client.Editor
{
    public static class TrainCameraSetup
    {
        [MenuItem("Tools/ProjectSixSeven/Attach Camera To Train")]
        private static void AttachCameraToTrain()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindAnyObjectByType<Camera>();
            }

            TrainFollower train = Object.FindAnyObjectByType<TrainFollower>();

            if (camera == null || train == null)
            {
                Debug.LogError("Attach Camera To Train: need a camera and a TrainFollower in the open scene. " +
                               $"Camera found: {camera != null}. Train found: {train != null}.");
                return;
            }

            TrainCamera follow = camera.GetComponent<TrainCamera>();
            if (follow == null)
            {
                follow = Undo.AddComponent<TrainCamera>(camera.gameObject);
            }

            SerializedObject serialized = new SerializedObject(follow);
            serialized.FindProperty("_target").objectReferenceValue = train.transform;
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(follow);
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);

            Selection.activeGameObject = camera.gameObject;

            Debug.Log($"Camera '{camera.name}' now follows '{train.name}'. Press C in play mode to " +
                      "toggle between the chase view and the cab view. Save the scene to keep this.");
        }
    }
}
