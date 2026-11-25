using UnityEngine;
using UnityEngine.UI;

namespace Mediapipe.Unity.Sample.ShuttleHand
{
  using Color = UnityEngine.Color;
  using Image = UnityEngine.UI.Image;
  using Rect = UnityEngine.Rect;

  /// <summary>
  /// 五線譜描画。黒い下地線＋色付き上位線の2層構成。
  /// </summary>
  [RequireComponent(typeof(CanvasRenderer))]
  public class StaffLineGraphic : MaskableGraphic
  {
    private Vector2[] _leftTips = new Vector2[5];
    private Vector2[] _rightTips = new Vector2[5];
    private bool[] _lineActive = new bool[5];
    private bool _bothHands;

    [SerializeField] private float _lineWidth = 12f;
    [SerializeField] private float _colorLineWidth = 4f;

    private static readonly Color LINE_COLOR = new Color(0.2f, 0.2f, 0.2f, 0.4f);

    // 指ごとの色: 親指=紫, 人差し指=青, 中指=緑, 薬指=黄, 小指=赤
    private static readonly Color[] FINGER_COLORS = {
      new Color(0.65f, 0.3f, 0.9f, 0.5f), // 親指 紫
      new Color(0.2f, 0.5f, 1f, 0.5f),    // 人差し指 青
      new Color(0.2f, 0.85f, 0.3f, 0.5f), // 中指 緑
      new Color(1f, 0.85f, 0.1f, 0.5f),   // 薬指 黄
      new Color(1f, 0.2f, 0.2f, 0.5f),    // 小指 赤
    };

    protected override void Awake()
    {
      base.Awake();
      color = LINE_COLOR;
      raycastTarget = false;
    }

    public void UpdateFingerTips(Vector2[] left, Vector2[] right, bool both)
    {
      _bothHands = both;
      if (both)
      {
        float maxDist = rectTransform.rect.width * 0.4f;
        for (int i = 0; i < 5; i++)
        {
          _leftTips[i] = left[i];
          _rightTips[i] = right[i];
          _lineActive[i] = Vector2.Distance(left[i], right[i]) <= maxDist;
        }
      }
      SetVerticesDirty();
    }

    public bool IsPointOnStaff(Vector2 pt, float threshold)
    {
      if (!_bothHands) return false;
      for (int i = 0; i < 5; i++)
      {
        if (!_lineActive[i]) continue;
        if (PtSegDist(pt, _leftTips[i], _rightTips[i]) <= threshold) return true;
      }
      return false;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
      vh.Clear();
      if (!_bothHands) return;

      // 第1層: 黒い下地線（太い）
      for (int i = 0; i < 5; i++)
      {
        if (!_lineActive[i]) continue;
        DrawLine(vh, _leftTips[i], _rightTips[i], _lineWidth, LINE_COLOR);
      }

      // 第2層: 色付き線（上に重ねて描画）
      for (int i = 0; i < 5; i++)
      {
        if (!_lineActive[i]) continue;
        DrawLine(vh, _leftTips[i], _rightTips[i], _colorLineWidth, FINGER_COLORS[i]);
      }
    }

    private void DrawLine(VertexHelper vh, Vector2 a, Vector2 b, float w, Color c)
    {
      Vector2 dir = b - a;
      if (dir.sqrMagnitude < 0.001f) return;
      dir.Normalize();
      Vector2 perp = new Vector2(-dir.y, dir.x) * (w * 0.5f);
      int bi = vh.currentVertCount;
      UIVertex v = UIVertex.simpleVert;
      v.color = c;
      v.position = a + perp; vh.AddVert(v);
      v.position = b + perp; vh.AddVert(v);
      v.position = b - perp; vh.AddVert(v);
      v.position = a - perp; vh.AddVert(v);
      vh.AddTriangle(bi, bi + 1, bi + 2);
      vh.AddTriangle(bi + 2, bi + 3, bi);
    }

    private static float PtSegDist(Vector2 p, Vector2 a, Vector2 b)
    {
      Vector2 seg = b - a;
      float len2 = seg.sqrMagnitude;
      if (len2 < 0.0001f) return Vector2.Distance(p, a);
      float t = Mathf.Clamp01(Vector2.Dot(p - a, seg) / len2);
      return Vector2.Distance(p, a + t * seg);
    }
  }
}
