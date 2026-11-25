using System.Collections.Generic;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;

using mptcc = Mediapipe.Tasks.Components.Containers;

namespace Mediapipe.Unity.Sample.GestureDetection
{
  public class GestureResultAnnotationController : AnnotationController<MultiHandLandmarkListAnnotation>
  {
    [SerializeField] private bool _visualizeZ = false;

    private readonly object _currentTargetLock = new object();
    private GestureRecognizerResult _currentTarget;
    private bool _hasTarget = false;

    public void DrawLater(GestureRecognizerResult target)
    {
      lock (_currentTargetLock)
      {
        target.CloneTo(ref _currentTarget);
        _hasTarget = true;
        isStale = true;
      }
    }

    protected override void SyncNow()
    {
      lock (_currentTargetLock)
      {
        isStale = false;
        if (!_hasTarget)
        {
          annotation.Draw((IReadOnlyList<mptcc.NormalizedLandmarks>)null);
          return;
        }

        // GestureRecognizerResult を MultiHandLandmarkListAnnotation が受け取れる形式に変換
        // 内部的には List<NormalizedLandmarks> を直接渡すが、型を明示して曖昧さを回避
        annotation.SetHandedness(_currentTarget.handedness);
        annotation.Draw((IReadOnlyList<mptcc.NormalizedLandmarks>)_currentTarget.handLandmarks, _visualizeZ);
      }
    }

    // 画面上にジェスチャー名を表示（最短実装のため OnGUI を使用）
    private void OnGUI()
    {
      if (!_hasTarget) return;

      lock (_currentTargetLock)
      {
        if (_currentTarget.gestures == null || _currentTarget.gestures.Count == 0 || _currentTarget.gestures[0].categories.Count == 0) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 30;
        style.normal.textColor = UnityEngine.Color.white;
        
        for (int i = 0; i < _currentTarget.gestures.Count; i++)
        {
          var gestureList = _currentTarget.gestures[i];
          if (gestureList.categories.Count > 0)
          {
            var topGesture = gestureList.categories[0];
            if (topGesture.score > 0.5f)
            {
               // 手の付近に表示するため、最初のランドマークの座標を参考に（簡易的）
               string text = $"Hand {i}: {topGesture.categoryName} ({topGesture.score:F2})";
               GUI.Label(new UnityEngine.Rect(50, UnityEngine.Screen.height - 100 + (i * 40), 1000, 50), text, style);
            }
          }
        }
      }
    }
  }
}
