#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using Mediapipe.Unity.Sample.GestureDetection;
using Mediapipe.Unity;

namespace Mediapipe.Unity.Editor
{
  public class GestureRecognitionSetupHelper : EditorWindow
  {
    [MenuItem("MediaPipe/Setup Gesture Recognition")]
    public static void Setup()
    {
      GameObject solutionObj = GameObject.Find("Solution");
      if (solutionObj == null)
      {
        Debug.LogError("Could not find 'Solution' object in the active scene. Please open the duplicated scene.");
        return;
      }

      // 1. Find or Restore GestureDetectionRunner
      var newRunner = solutionObj.GetComponent<GestureDetectionRunner>();
      if (newRunner == null)
      {
        newRunner = solutionObj.AddComponent<GestureDetectionRunner>();
        Debug.Log("Added GestureDetectionRunner to Solution.");
      }

      // Try to find old runner to copy settings
      var oldRunner = Object.FindAnyObjectByType<HandLandmarkerRunner>();
      
      // Copy Bootstrap Prefab and Screen references
      SerializedObject soNew = new SerializedObject(newRunner);
      SerializedProperty propBootstrap = soNew.FindProperty("_bootstrapPrefab");
      SerializedProperty propScreen = soNew.FindProperty("screen");

      if (oldRunner != null)
      {
        SerializedObject soOld = new SerializedObject(oldRunner);
        if (propBootstrap.objectReferenceValue == null) propBootstrap.objectReferenceValue = soOld.FindProperty("_bootstrapPrefab").objectReferenceValue;
        if (propScreen.objectReferenceValue == null) propScreen.objectReferenceValue = soOld.FindProperty("screen").objectReferenceValue;
        Debug.Log("Copied settings from existing HandLandmarkerRunner.");
        oldRunner.enabled = false;
      }
      
      // Fallback: If still null, find Bootstrap prefab from assets
      if (propBootstrap.objectReferenceValue == null)
      {
        // GUID of Assets/MediaPipeUnity/Samples/Resources/Bootstrap.prefab
        string bootstrapGuid = "1146a9de09159a948b683e94fb3d3c95";
        string path = AssetDatabase.GUIDToAssetPath(bootstrapGuid);
        if (!string.IsNullOrEmpty(path))
        {
          propBootstrap.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(path);
          Debug.Log("Restored Bootstrap prefab from Assets.");
        }
      }

      // Fallback for Screen: find in scene
      if (propScreen.objectReferenceValue == null)
      {
        var screenInScene = Object.FindAnyObjectByType<Mediapipe.Unity.Screen>();
        if (screenInScene != null)
        {
          propScreen.objectReferenceValue = screenInScene;
          Debug.Log("Found and assigned Screen from scene.");
        }
      }

      soNew.ApplyModifiedProperties();

      // 2. Find Annotation Controller
      var oldController = Object.FindAnyObjectByType<HandLandmarkerResultAnnotationController>();
      if (oldController == null)
      {
        Debug.LogWarning("HandLandmarkerResultAnnotationController not found. Searching by hierarchy...");
        // Manual search as fallback
        var annotationsObj = solutionObj.transform.Find("Annotatable Screen/Annotations/Hand Landmarker");
        if (annotationsObj != null)
        {
          oldController = annotationsObj.GetComponent<HandLandmarkerResultAnnotationController>();
        }
      }

      if (oldController != null)
      {
        GameObject controllerObj = oldController.gameObject;
        
        // Get Annotation reference via Reflection
        var oldAnnotationController = oldController.GetComponent<AnnotationController<MultiHandLandmarkListAnnotation>>();
        var annotationField = typeof(AnnotationController<MultiHandLandmarkListAnnotation>).GetField("annotation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var annotationRef = annotationField?.GetValue(oldAnnotationController);
        
        var newController = controllerObj.GetComponent<GestureResultAnnotationController>();
        if (newController == null)
        {
          newController = controllerObj.AddComponent<GestureResultAnnotationController>();
        }
        
        // Link Annotation via Reflection
        if (annotationField != null)
        {
          annotationField.SetValue(newController, annotationRef);
        }
        
        // Link to Runner
        var runnerControllerField = typeof(GestureDetectionRunner).GetField("_gestureResultAnnotationController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (runnerControllerField != null)
        {
          runnerControllerField.SetValue(newRunner, newController);
        }

        Debug.Log($"Successfully setup GestureResultAnnotationController on {controllerObj.name}");
        
        // We don't remove old components immediately to let user verify, 
        // but we can disable them.
        oldController.enabled = false;
        if (oldRunner != null) oldRunner.enabled = false;
        
        EditorUtility.SetDirty(solutionObj);
        EditorUtility.SetDirty(controllerObj);
        
        Debug.Log("Setup Complete! Please check the Inspector and run the scene.");
      }
      else
      {
        Debug.LogError("Could not locate HandLandmarkerResultAnnotationController. Please ensure the scene is based on Hand Landmark Detection.");
      }
    }
  }
}
#endif
