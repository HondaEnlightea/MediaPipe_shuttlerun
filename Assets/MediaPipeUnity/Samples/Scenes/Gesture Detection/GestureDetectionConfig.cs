using System;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using UnityEngine;

namespace Mediapipe.Unity.Sample.GestureDetection
{
  [Serializable]
  public class GestureDetectionConfig
  {
    public string ModelPath = "gesture_recognizer.bytes";
    public Tasks.Vision.Core.RunningMode RunningMode = Tasks.Vision.Core.RunningMode.IMAGE;
    public Tasks.Core.BaseOptions.Delegate Delegate = Tasks.Core.BaseOptions.Delegate.CPU;
    public int NumHands = 2;
    public float MinHandDetectionConfidence = 0.4f;
    public float MinHandPresenceConfidence = 0.4f;
    public float MinTrackingConfidence = 0.4f;

    public GestureRecognizerOptions GetGestureRecognizerOptions(GestureRecognizerOptions.ResultCallback resultCallback = null)
    {
      return new GestureRecognizerOptions(
        baseOptions: new Tasks.Core.BaseOptions(Delegate, modelAssetPath: ModelPath),
        runningMode: RunningMode,
        numHands: NumHands,
        minHandDetectionConfidence: MinHandDetectionConfidence,
        minHandPresenceConfidence: MinHandPresenceConfidence,
        minTrackingConfidence: MinTrackingConfidence,
        resultCallback: resultCallback
      );
    }
  }
}
