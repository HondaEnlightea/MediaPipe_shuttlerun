using System.Collections;
using System.Collections.Generic;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mediapipe.Unity.Sample.GestureDetection
{
  public class GestureDetectionRunner : VisionTaskApiRunner<GestureRecognizer>
  {
    [SerializeField] private GestureResultAnnotationController _gestureResultAnnotationController;

    private Experimental.TextureFramePool _textureFramePool;

    public readonly GestureDetectionConfig config = new GestureDetectionConfig();
    
    /// <summary>
    /// ジェスチャーを検知した際に発行されるイベント。
    /// 第一引数: 手のインデックス(0 or 1), 第二引数: ジェスチャー名
    /// </summary>
    public event System.Action<int, string> OnGestureDetected;

    /// <summary>
    /// 認識結果が更新された際に発行されるイベント。
    /// ランドマーク座標・ジェスチャー情報を含む完全な結果を渡す。
    /// 注意: バックグラウンドスレッドから呼ばれる可能性あり。
    /// </summary>
    public event System.Action<GestureRecognizerResult> OnResultUpdated;

    public override void Stop()
    {
      base.Stop();
      _textureFramePool?.Dispose();
      _textureFramePool = null;
    }

    protected override IEnumerator Run()
    {
      // Step 2: gesture_recognizer.bytes を読み込み
      yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

      // Step 3: GestureRecognizer 初期化
      var options = config.GetGestureRecognizerOptions(config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnGestureRecognitionOutput : null);
      taskApi = GestureRecognizer.CreateFromOptions(options, GpuManager.GpuResources);
      
      var imageSource = ImageSourceProvider.ImageSource;
      Debug.Log($"[GestureDetection] Target ImageSource: {imageSource.sourceName}");
      
      Debug.Log("[GestureDetection] Starting ImageSource.Play()...");
      yield return imageSource.Play();
      
      // Wait for texture to be valid
      yield return new WaitUntil(() => imageSource.textureWidth > 1);
      
      Debug.Log($"[GestureDetection] ImageSource.Play() finished.");
      Debug.Log($"[GestureDetection] Current Device: {imageSource.sourceName}");
      Debug.Log($"[GestureDetection] Resolution: {imageSource.textureWidth}x{imageSource.textureHeight}");
      Debug.Log($"[GestureDetection] isPrepared: {imageSource.isPrepared}, isPlaying: {imageSource.isPlaying}");

      Debug.Log($"[GestureDetection] Initializing Screen with {imageSource.textureWidth}x{imageSource.textureHeight}");
      _textureFramePool = new Experimental.TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);
      screen.Initialize(imageSource);

      SetupAnnotationController(_gestureResultAnnotationController, imageSource);

      var transformationOptions = imageSource.GetTransformationOptions();
      var flipHorizontally = transformationOptions.flipHorizontally;
      var flipVertically = transformationOptions.flipVertically;
      var imageProcessingOptions = new Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: (int)transformationOptions.rotationAngle);

      AsyncGPUReadbackRequest req = default;
      var waitUntilReqDone = new WaitUntil(() => req.done);
      var waitForEndOfFrame = new WaitForEndOfFrame();
      
      // Step 4: 毎フレーム処理
      while (imageSource.isPlaying)
      {
        if (isPaused)
        {
          yield return new WaitWhile(() => isPaused);
        }

        if (_textureFramePool == null || !_textureFramePool.TryGetTextureFrame(out var textureFrame))
        {
          yield return new WaitForEndOfFrame();
          continue;
        }

        // Build the input Image
        Image image;
        // 最短到達のため、サンプルで安定している方式（通常はCPU読み出し）
        yield return waitForEndOfFrame;
        var currentTexture = imageSource.GetCurrentTexture();
        if (currentTexture == null) 
        {
          textureFrame.Release();
          continue;
        }

        textureFrame.ReadTextureOnCPU(currentTexture, flipHorizontally, flipVertically);
        image = textureFrame.BuildCPUImage();

        // Recognize
        if (taskApi.runningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM)
        {
          taskApi.RecognizeAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
        }
        else
        {
          // IMAGE or VIDEO mode
          var result = taskApi.Recognize(image, imageProcessingOptions);
          OnGestureRecognitionOutput(result, image, GetCurrentTimestampMillisec());
        }

        image.Dispose();
        textureFrame.Release();
      }
    }

    // Step 5: 結果取得
    private void OnGestureRecognitionOutput(GestureRecognizerResult result, Image image, long timestamp)
    {
      if (_gestureResultAnnotationController != null)
      {
        _gestureResultAnnotationController.DrawLater(result);
      }

      // ゲームスクリプト向けに完全な認識結果を通知
      OnResultUpdated?.Invoke(result);
      
      // ジェスチャー判定処理とログ出力
      if (result.gestures != null && result.gestures.Count > 0)
      {
        for (int i = 0; i < result.gestures.Count; i++)
        {
          var handGestures = result.gestures[i];
          foreach (var gesture in handGestures.categories)
          {
            if (gesture.score > 0.5f)
            {
              Debug.Log($"Detected Gesture [Hand {i}]: {gesture.categoryName} (Score: {gesture.score:F2})");
              // ゲーム開発用のイベントを発行
              OnGestureDetected?.Invoke(i, gesture.categoryName);
            }
          }
        }
      }
    }
  }
}
