using UnityEngine;
using UnityEngine.UI;

namespace Mediapipe.Unity.Sample.ShuttleHand
{
  using Color = UnityEngine.Color;
  using Image = UnityEngine.UI.Image;
  using Rect = UnityEngine.Rect;

  public class GameUI : MonoBehaviour
  {
    private Text _scoreText;
    private Image[] _lifeImages = new Image[2]; // Gclefライフ表示
    private Text _loopText;
    private Text _centerMessage;
    private Text _instructionText;
    private CanvasGroup _centerMessageGroup;
    private RectTransform _centerMessageRect;
    private RectTransform _instructionRect;
    private Shadow _centerMessageShadow;
    private GameObject _missPopup;
    private Text _missPopupText;
    private Font _cachedFont;
    private Text _totalPlayText;
    private Text _highScoreText;
    private RectTransform _canvasRect;

    public void Initialize(RectTransform canvasRect)
    {
      _canvasRect = canvasRect;

      // --- ライフ: Gclef画像で左上に表示 ---
      Sprite gclefSprite = Resources.Load<Sprite>("Gclef");
      if (gclefSprite == null)
      {
        // Spriteとして認識されていない場合のフォールバック
        Texture2D tex = Resources.Load<Texture2D>("Gclef");
        if (tex != null)
        {
          gclefSprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
      }

      for (int i = 0; i < 2; i++)
      {
        var lifeObj = new GameObject($"LifeGclef_{i}");
        var rect = lifeObj.AddComponent<RectTransform>();
        rect.SetParent(_canvasRect, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(30f + i * 120f, -30f); // 間隔を元に戻す
        rect.sizeDelta = new Vector2(360f, 360f); // 50%拡大 (160 * 1.5 = 240)
        
        _lifeImages[i] = lifeObj.AddComponent<Image>();
        _lifeImages[i].sprite = gclefSprite;
        _lifeImages[i].color = Color.white; // 明示的に白（元の色）を設定
        _lifeImages[i].raycastTarget = false;
        lifeObj.transform.SetAsLastSibling();
      }

      // --- スコア: フォント初期化後に配置するためここでは生成しない ---

      // ループカウント（上部中央）
      _loopText = CreateText("LoopText",
        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
        new Vector2(0f, -20f), new Vector2(300f, 40f),
        28, TextAnchor.UpperCenter, new Color(0.8f, 0.8f, 0.8f));

      // 中央メッセージ（スコア表示・ゲーム状態表示用）
      var centerObj = new GameObject("CenterMessage");
      _centerMessageRect = centerObj.AddComponent<RectTransform>();
      _centerMessageRect.SetParent(_canvasRect, false);
      _centerMessageRect.anchorMin = new Vector2(0.5f, 0.5f);
      _centerMessageRect.anchorMax = new Vector2(0.5f, 0.5f);
      _centerMessageRect.pivot = new Vector2(0.5f, 0.5f);
      _centerMessageRect.anchoredPosition = new Vector2(0f, 50f);
      _centerMessageRect.sizeDelta = new Vector2(1200f, 400f);

      _cachedFont = Resources.Load<Font>("heiseimaru");
      if (_cachedFont == null)
      {
        _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      }

      // --- スコア: ライフ(360px)の下に配置。フォント初期化後に生成する ---
      _scoreText = CreateText("ScoreText",
        new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
        new Vector2(30f, -400f), new Vector2(600f, 120f),
        84, TextAnchor.UpperLeft, Color.white);
      _scoreText.transform.SetAsLastSibling();

      _centerMessageGroup = centerObj.AddComponent<CanvasGroup>();
      _centerMessage = centerObj.AddComponent<Text>();
      _centerMessage.font = _cachedFont;
      _centerMessage.fontSize = 72;
      _centerMessage.alignment = TextAnchor.MiddleCenter;
      _centerMessage.horizontalOverflow = HorizontalWrapMode.Overflow;
      _centerMessage.verticalOverflow = VerticalWrapMode.Overflow;
      _centerMessage.color = Color.white;
      _centerMessage.raycastTarget = false;
      var outline = centerObj.AddComponent<Outline>();
      outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
      outline.effectDistance = new Vector2(3f, -3f);

      _centerMessageShadow = centerObj.AddComponent<Shadow>();
      _centerMessageShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
      _centerMessageShadow.effectDistance = new Vector2(4f, -4f);

      // 操作説明（画面中央、中央メッセージの下）
      var instrObj = new GameObject("InstructionText");
      _instructionRect = instrObj.AddComponent<RectTransform>();
      _instructionRect.SetParent(_canvasRect, false);
      _instructionRect.anchorMin = new Vector2(0.5f, 0.5f);
      _instructionRect.anchorMax = new Vector2(0.5f, 0.5f);
      _instructionRect.pivot = new Vector2(0.5f, 0.5f);
      _instructionRect.anchoredPosition = new Vector2(0f, -100f);
      _instructionRect.sizeDelta = new Vector2(1800f, 500f);
      _instructionText = instrObj.AddComponent<Text>();
      _instructionText.font = _cachedFont;
      _instructionText.fontSize = 150; // ユーザー指定サイズ
      _instructionText.alignment = TextAnchor.MiddleCenter;
      _instructionText.horizontalOverflow = HorizontalWrapMode.Overflow;
      _instructionText.verticalOverflow = VerticalWrapMode.Overflow;
      _instructionText.color = Color.white;
      _instructionText.raycastTarget = false;
      var instrOutline = instrObj.AddComponent<Outline>();
      instrOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
      instrOutline.effectDistance = new Vector2(4f, -4f);
      var instrShadow = instrObj.AddComponent<Shadow>();
      instrShadow.effectColor = new Color(1f, 0.5f, 0f, 0.8f); // オレンジ色の影
      instrShadow.effectDistance = new Vector2(5f, -5f);
      instrObj.transform.SetAsLastSibling();

      // ミスポップアップ（中央）
      _missPopup = new GameObject("MissPopup");
      var mpRect = _missPopup.AddComponent<RectTransform>();
      mpRect.SetParent(_canvasRect, false);
      mpRect.anchorMin = new Vector2(0.5f, 0.5f);
      mpRect.anchorMax = new Vector2(0.5f, 0.5f);
      mpRect.pivot = new Vector2(0.5f, 0.5f);
      mpRect.anchoredPosition = new Vector2(0f, 80f);
      mpRect.sizeDelta = new Vector2(400f, 120f);
      _missPopupText = _missPopup.AddComponent<Text>();
      _missPopupText.font = _cachedFont;
      _missPopupText.fontSize = 112;
      _missPopupText.alignment = TextAnchor.MiddleCenter;
      _missPopupText.color = new Color(1f, 0.2f, 0.2f);
      _missPopupText.raycastTarget = false;
      var mpOutline = _missPopup.AddComponent<Outline>();
      mpOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
      mpOutline.effectDistance = new Vector2(2f, -2f);
      _missPopup.SetActive(false);

      // --- 累計プレイ回数・最高得点 (右下) ---
      // 累計プレイ数
      _totalPlayText = CreateText("TotalPlayCount",
        new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
        new Vector2(-30f, 30f), new Vector2(400f, 40f),
        28, TextAnchor.LowerRight, new Color(0.9f, 0.9f, 0.9f, 0.8f));

      // 最高得点 (累計プレイ数の上に配置)
      _highScoreText = CreateText("HighScoreText",
        new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
        new Vector2(-30f, 100f), new Vector2(600f, 60f),
        42, TextAnchor.LowerRight, Color.white);

      // 初期表示
      UpdateScore(0);
      UpdateMiss(0);
      UpdateLoop(0);
      ShowInstruction("Are you Ready?\n両手でサムズアップ！");
      HideCenterMessage();
    }

    public void UpdateScore(int score)
    {
      _scoreText.text = $"\u266a = {score}";
    }

    // ミス数に応じて透明度を変更 (0ミス: 100%, 1ミス: 1つ30%, 2ミス: 両方30%)
    public void UpdateMiss(int missCount)
    {
      if (missCount == 0)
      {
        SetAlpha(_lifeImages[0], 1.0f);
        SetAlpha(_lifeImages[1], 1.0f);
      }
      else if (missCount == 1)
      {
        SetAlpha(_lifeImages[0], 1.0f);
        SetAlpha(_lifeImages[1], 0.3f);
      }
      else
      {
        SetAlpha(_lifeImages[0], 0.3f);
        SetAlpha(_lifeImages[1], 0.3f);
      }
    }

    private void SetAlpha(Image img, float alpha)
    {
      if (img == null) return;
      Color c = img.color;
      c.a = alpha;
      img.color = c;
    }

    public void UpdateLoop(int loopIndex)
    {
      _loopText.text = $"LOOP {loopIndex + 1} / 11";
    }

    public void UpdateTotalPlayCount(int count)
    {
      if (_totalPlayText != null) _totalPlayText.text = $"TOTAL PLAYS: {count}";
    }

    public void UpdateHighScore(int score)
    {
      if (_highScoreText != null) _highScoreText.text = $"\u266c = {score}(MAX)";
    }

    public void ShowCenterMessage(string message)
    {
      _centerMessage.text = message;
      _centerMessageGroup.alpha = 1f;
      _centerMessageRect.anchoredPosition = new Vector2(0f, 50f); // 中央
    }

    public void ShowCenterMessage(string message, Color color)
    {
      _centerMessage.text = message;
      _centerMessage.color = color;
      _centerMessageGroup.alpha = 1f;
      _centerMessageRect.anchoredPosition = new Vector2(0f, 50f); // 中央
    }

    /// 終了メッセージ用：中央上部に配置 (緑影)
    public void ShowCenterMessageUpper(string message, Color color)
    {
      _centerMessage.text = message;
      _centerMessage.color = color;
      _centerMessage.alignment = TextAnchor.MiddleRight; // 右揃え
      _centerMessage.fontSize = 120; // ユーザー指定サイズ
      if (_centerMessageShadow != null)
      {
        _centerMessageShadow.effectColor = new Color(0f, 1f, 0.2f, 0.8f); // 緑色の影
      }
      _centerMessageGroup.alpha = 1f;
      _centerMessageRect.anchoredPosition = new Vector2(300f, 100f); // 右側に寄せる (元は0f)
    }

    public void HideCenterMessage()
    {
      _centerMessageGroup.alpha = 0f;
      _centerMessage.alignment = TextAnchor.MiddleCenter; // リセット
      _centerMessage.fontSize = 72; // リセット
      if (_centerMessageShadow != null)
      {
        _centerMessageShadow.effectColor = new Color(0f, 0f, 0f, 0.5f); // リセット
      }
      _centerMessageRect.anchoredPosition = new Vector2(0f, 50f); // リセット
    }

    public void ShowInstruction(string text)
    {
      _instructionText.text = text;
      _instructionText.alignment = TextAnchor.MiddleCenter;
      _instructionRect.anchoredPosition = new Vector2(0f, -100f); // 中央
    }

    /// 終了時指示用：中央下部に配置 (オレンジ影)
    public void ShowInstructionLower(string text)
    {
      _instructionText.text = text;
      _instructionText.alignment = TextAnchor.MiddleLeft; // 左揃え
      _instructionRect.anchoredPosition = new Vector2(0f, -250f); // さらに100px下に配置 (元は-150f)
    }

    public void HideInstruction()
    {
      _instructionText.text = "";
      _instructionText.alignment = TextAnchor.MiddleCenter; // リセット
      _instructionRect.anchoredPosition = new Vector2(0f, -100f); // リセット
    }

    public void ShowMissPopup()
    {
      _missPopupText.text = "miss";
      _missPopup.SetActive(true);
      StartCoroutine(HideMissPopupAfter(0.8f));
    }

    private System.Collections.IEnumerator HideMissPopupAfter(float delay)
    {
      yield return new WaitForSeconds(delay);
      if (_missPopup != null) _missPopup.SetActive(false);
    }

    private Text CreateText(string name,
      Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
      Vector2 anchoredPos, Vector2 sizeDelta,
      int fontSize, TextAnchor alignment, Color color)
    {
      var obj = new GameObject(name);
      var rect = obj.AddComponent<RectTransform>();
      rect.SetParent(_canvasRect, false);
      rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
      rect.pivot = pivot; rect.anchoredPosition = anchoredPos;
      rect.sizeDelta = sizeDelta;

      var text = obj.AddComponent<Text>();
      text.font = _cachedFont;
      text.fontSize = fontSize;
      text.alignment = alignment;
      text.color = color;
      text.horizontalOverflow = HorizontalWrapMode.Overflow;
      text.verticalOverflow = VerticalWrapMode.Overflow;
      text.raycastTarget = false;

      var outline = obj.AddComponent<Outline>();
      outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
      outline.effectDistance = new Vector2(3f, -3f);

      var shadow = obj.AddComponent<Shadow>();
      shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
      shadow.effectDistance = new Vector2(4f, -4f);

      return text;
    }
  }
}
