using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Mediapipe.Unity.Sample.ShuttleHand
{
  using Color = UnityEngine.Color;
  using Image = UnityEngine.UI.Image;
  using Rect = UnityEngine.Rect;

  /// <summary>
  /// 成功時のリングエフェクト。
  /// ループ数に応じて1重〜11重のリングが波紋のように広がる。
  /// </summary>
  [RequireComponent(typeof(CanvasRenderer))]
  public class HitEffect : MaskableGraphic
  {
    private int _ringCount;
    private float[] _radii;
    private float[] _alphas;
    private float _maxRadius;
    private float _ringWidth;
    private Color _baseColor;
    private bool _animating;

    private const int CIRCLE_SEGMENTS = 32;

    protected override void Awake()
    {
      base.Awake();
      raycastTarget = false;
    }

    /// <summary>
    /// リングエフェクトを開始する。
    /// </summary>
    /// <param name="baseColor">エフェクトの色</param>
    /// <param name="ringCount">リングの数 (1〜11)</param>
    /// <param name="duration">アニメーション時間（秒）</param>
    /// <param name="maxRadius">リングが広がる最大半径</param>
    public void Play(Color baseColor, int ringCount = 1, float duration = 0.6f, float maxRadius = 120f)
    {
      _baseColor = baseColor;
      _ringCount = Mathf.Max(1, ringCount);
      _maxRadius = maxRadius;
      _ringWidth = 15f; // 太さを強化 (5f -> 15f)
      _radii = new float[_ringCount];
      _alphas = new float[_ringCount];
      _animating = true;
      StartCoroutine(AnimateRings(duration));
    }

    private IEnumerator AnimateRings(float duration)
    {
      float elapsed = 0f;
      float stagger = (_ringCount > 1) ? 0.06f : 0f; // リング間の遅延

      while (elapsed < duration + stagger * (_ringCount - 1))
      {
        elapsed += Time.deltaTime;

        for (int r = 0; r < _ringCount; r++)
        {
          float ringStart = r * stagger;
          float ringElapsed = elapsed - ringStart;
          float t = Mathf.Clamp01(ringElapsed / duration);

          float easedT = 1f - (1f - t) * (1f - t);
          _radii[r] = Mathf.Lerp(5f, _maxRadius, easedT);
          // 減衰を緩やかにして、より鮮やかに見えるように (平方根を使用)
          _alphas[r] = Mathf.Sqrt(1f - t); 
        }

        SetVerticesDirty();
        yield return null;
      }

      _animating = false;
      Destroy(gameObject);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
      vh.Clear();
      if (!_animating) return;

      for (int r = 0; r < _ringCount; r++)
      {
        if (_radii[r] < 1f || _alphas[r] < 0.01f) continue;

        Color c = _baseColor;
        // 複数重なっても色が薄くなりすぎないように調整
        c.a = _alphas[r] * (1f - (float)r / (_ringCount + 1) * 0.15f); 

        float innerR = Mathf.Max(0f, _radii[r] - _ringWidth);
        float outerR = _radii[r];

        for (int i = 0; i < CIRCLE_SEGMENTS; i++)
        {
          float a0 = (float)i / CIRCLE_SEGMENTS * Mathf.PI * 2f;
          float a1 = (float)(i + 1) / CIRCLE_SEGMENTS * Mathf.PI * 2f;

          int bi = vh.currentVertCount;
          UIVertex v = UIVertex.simpleVert;
          v.color = c;

          v.position = new Vector2(Mathf.Cos(a0) * innerR, Mathf.Sin(a0) * innerR); vh.AddVert(v);
          v.position = new Vector2(Mathf.Cos(a0) * outerR, Mathf.Sin(a0) * outerR); vh.AddVert(v);
          v.position = new Vector2(Mathf.Cos(a1) * outerR, Mathf.Sin(a1) * outerR); vh.AddVert(v);
          v.position = new Vector2(Mathf.Cos(a1) * innerR, Mathf.Sin(a1) * innerR); vh.AddVert(v);

          vh.AddTriangle(bi, bi + 1, bi + 2);
          vh.AddTriangle(bi + 2, bi + 3, bi);
        }
      }
    }
  }
}
