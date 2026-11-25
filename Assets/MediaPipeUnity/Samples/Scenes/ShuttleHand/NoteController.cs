using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Mediapipe.Unity.Sample.ShuttleHand
{
  using Color = UnityEngine.Color;
  using Image = UnityEngine.UI.Image;
  using Rect = UnityEngine.Rect;

  public class NoteController : MonoBehaviour
  {
    private NoteData _noteData;
    private Vector2 _startPos;
    private Vector2 _targetPos;
    private Action<bool, NoteData> _onComplete;
    private RectTransform _rectTransform;
    private Text _noteText;
    private bool _completed;

    public static readonly Color[] NOTE_COLORS = {
      new Color(1f, 0.231f, 0.188f),   // ド
      new Color(1f, 0.584f, 0f),        // レ
      new Color(1f, 0.8f, 0f),          // ミ
      new Color(0.204f, 0.78f, 0.349f), // ファ
      new Color(0.353f, 0.784f, 0.98f), // ソ
      new Color(0f, 0.478f, 1f),        // ラ
      new Color(0.686f, 0.322f, 0.871f),// シ
      new Color(1f, 0.176f, 0.333f),    // 高ド
    };

    public static readonly Color[] PASTEL_COLORS = {
      new Color(1f, 0.702f, 0.682f),
      new Color(1f, 0.839f, 0.627f),
      new Color(1f, 0.941f, 0.627f),
      new Color(0.659f, 0.91f, 0.737f),
      new Color(0.722f, 0.894f, 0.992f),
      new Color(0.6f, 0.784f, 1f),
      new Color(0.851f, 0.702f, 0.937f),
      new Color(1f, 0.62f, 0.69f),
    };

    /// <summary>
    /// 到達時間ベースで音符を初期化。arrivalTime 秒で center → target に移動。
    /// </summary>
    public void Initialize(NoteData data, Vector2 startPos, Vector2 targetPos,
                           Transform parent, Action<bool, NoteData> onComplete)
    {
      _noteData = data;
      _startPos = startPos;
      _targetPos = targetPos;
      _onComplete = onComplete;
      _completed = false;

      _rectTransform = gameObject.AddComponent<RectTransform>();
      _rectTransform.SetParent(parent, false);
      _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
      _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
      _rectTransform.pivot = new Vector2(0.5f, 0.5f);
      _rectTransform.sizeDelta = new Vector2(250, 250); // サイズ拡大に合わせて調整
      _rectTransform.anchoredPosition = startPos;

      Font gameFont = Resources.Load<Font>("heiseimaru");
      if (gameFont == null)
      {
        gameFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      }

      _noteText = gameObject.AddComponent<Text>();
      _noteText.text = "\u266A";
      _noteText.font = gameFont;
      _noteText.fontSize = 140; // 30%拡大 (108 * 1.3 = 140)
      _noteText.alignment = TextAnchor.MiddleCenter;
      _noteText.color = NOTE_COLORS[data.noteIndex % NOTE_COLORS.Length];
      _noteText.raycastTarget = false;

      // アウトライン（視認性向上）
      var outline = gameObject.AddComponent<Outline>();
      outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
      outline.effectDistance = new Vector2(2f, -2f);

      StartCoroutine(MoveToTarget());
    }

    private IEnumerator MoveToTarget()
    {
      float totalTime = _noteData.arrivalTime;
      if (totalTime <= 0f) totalTime = 1f;
      float elapsed = 0f;

      while (elapsed < totalTime)
      {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / totalTime);
        // リニア移動（均一なリズム感）
        _rectTransform.anchoredPosition = Vector2.Lerp(_startPos, _targetPos, t);
        yield return null;
      }

      _rectTransform.anchoredPosition = _targetPos;
      if (!_completed)
      {
        _completed = true;
        _onComplete?.Invoke(false, _noteData);
      }
    }

    public Vector2 GetTargetPosition() => _targetPos;
    public void DestroyNote() => Destroy(gameObject);
  }
}
