using System.Collections;
using System.Collections.Generic;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using Mediapipe.Unity.Sample.GestureDetection;
using Mediapipe.Unity.CoordinateSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Mediapipe.Unity.Sample.ShuttleHand
{
  using Color = UnityEngine.Color;
  using Image = UnityEngine.UI.Image;
  using Rect = UnityEngine.Rect;

  public class ShuttleHandGameManager : MonoBehaviour
  {
    private const int NOTES_PER_LEG = 8;
    private const int TOTAL_LOOPS = 11;          // 往路11往復 = 22 legs
    private const int MAX_MISSES = 2;
    private const float THUMBS_UP_DURATION = 0.5f;
    private const float THUMBS_UP_CONFIDENCE = 0.5f;
    private const float HIT_THRESHOLD = 80f;

    // legIndex が増えるごとに到達時間・インターバルを短縮
    // leg0到達: 1.8s, leg1: 1.68s, ... MIN: 0.6s  (DECREMENT=0.12s)
    // 休憩合計: leg0後: 1.68s, leg1後: 1.56s, ... 
    private const float BASE_ARRIVAL_TIME = 2.0f;
    private const float MIN_ARRIVAL_TIME  = 0.20f;
    private const float ARRIVAL_DECREMENT = 0.22f;

    // 音階順序
    private static readonly int[] FORWARD_NOTES = { 0, 1, 2, 3, 4, 5, 6, 7 };
    private static readonly int[] REVERSE_NOTES = { 7, 6, 5, 4, 3, 2, 1, 0 };
    private static readonly int[] FINGERTIP_INDICES = { 4, 8, 12, 16, 20 };

    private enum GameState { Waiting, Starting, Playing, GameOver, GameClear, Ending }
    private GameState _state = GameState.Waiting;

    private int _score;
    private int _missCount;
    private int _currentNoteNumber;
    private int _currentLoop;
    private int _currentLegIndex;  // 0〜21: 往路0,1,... 復路は奇数
    private float _thumbsUpTimer;

    private GestureDetectionRunner _runner;
    private StaffLineGraphic _staffLine;
    private GameUI _gameUI;
    private ShuttleRunAudio _audio;
    private RectTransform _notesContainer;
    private RectTransform _effectsContainer;
    private bool _uiReady;

    private RectTransform _annotationLayer;
    private RotationAngle _rotationAngle;
    private bool _isMirrored;

    private readonly object _dataLock = new object();
    private int _rawHandCount;
    private readonly float[] _rawNorm = new float[20];
    private readonly string[] _rawGestNames = { "", "" };
    private readonly float[] _rawGestScores = new float[2];
    private bool _hasNewData;

    private Vector2[] _leftTips = new Vector2[5];
    private Vector2[] _rightTips = new Vector2[5];
    private bool _hasLeftHand;
    private bool _hasRightHand;
    private int _currentThumbsUpCount;
    private int _currentPeaceCount;
    private Vector2 _lastTargetPos = new Vector2(-9999f, -9999f); // 初期値は画面外
    private int _totalPlayCount;
    private int _highScore;

    private NoteController _activeNote;
    private GameObject _activeTargetCircle;

    // ==== Unity ライフサイクル ====

    private void Awake()
    {
      _runner = GetComponent<GestureDetectionRunner>();
    }

    private void OnEnable()
    {
      if (_runner == null) { Debug.LogError("[ShuttleHand] GestureDetectionRunner not found!"); enabled = false; return; }
      _runner.OnResultUpdated += OnResultUpdated;

      // 永続データのロード
      _totalPlayCount = PlayerPrefs.GetInt("ShuttleHand_TotalPlayCount", 0);
      _highScore = PlayerPrefs.GetInt("ShuttleHand_HighScore", 0);

      if (!_uiReady) StartCoroutine(InitUI());
    }

    private void OnDisable()
    {
      if (_runner != null) _runner.OnResultUpdated -= OnResultUpdated;
    }

    private IEnumerator InitUI()
    {
      Debug.Log("[ShuttleHand] Waiting for annotation layer...");
      while (_annotationLayer == null)
      {
        var layer = GameObject.Find("Annotation Layer");
        if (layer != null) _annotationLayer = layer.GetComponent<RectTransform>();
        else { var t = transform.Find("Annotation Layer"); if (t != null) _annotationLayer = t.GetComponent<RectTransform>(); }
        if (_annotationLayer == null) yield return new WaitForSeconds(0.5f);
      }
      Debug.Log($"[ShuttleHand] Annotation Layer found: {_annotationLayer.rect}");
      UpdateAnnotationSettings();
      if (!_uiReady) 
      { 
        SetupComponents(); 
        _gameUI.UpdateTotalPlayCount(_totalPlayCount);
        _gameUI.UpdateHighScore(_highScore);
        _uiReady = true; 
        Debug.Log("[ShuttleHand] UI Ready."); 
      }
    }

    private void UpdateAnnotationSettings()
    {
      var controllers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
      foreach (var ctrl in controllers)
      {
        var type = ctrl.GetType();
        var mp = type.GetProperty("isMirrored");
        var rp = type.GetProperty("rotationAngle");
        if (mp != null && rp != null) { _isMirrored = (bool)mp.GetValue(ctrl); _rotationAngle = (RotationAngle)rp.GetValue(ctrl); return; }
      }
      var imgSrc = ImageSourceProvider.ImageSource;
      if (imgSrc != null) { var opts = imgSrc.GetTransformationOptions(); _rotationAngle = (RotationAngle)(int)opts.rotationAngle; _isMirrored = opts.flipHorizontally; }
    }

    private void Update()
    {
      SyncTrackingData();
      if (_uiReady && _staffLine != null)
        _staffLine.UpdateFingerTips(_leftTips, _rightTips, _hasLeftHand && _hasRightHand);

      if ((_state == GameState.Waiting || _state == GameState.GameOver || _state == GameState.GameClear)
          && Input.GetKeyDown(KeyCode.Space)) { if (_uiReady) StartGame(); return; }

      switch (_state)
      {
        case GameState.Waiting:
          ProcessWaiting(); break;
      }
    }

    // ==== バックグラウンドスレッド ====

    private void OnResultUpdated(GestureRecognizerResult result)
    {
      if (result.handLandmarks == null) return;
      lock (_dataLock)
      {
        _rawHandCount = Mathf.Min(result.handLandmarks.Count, 2);
        for (int h = 0; h < _rawHandCount; h++)
        {
          var lm = result.handLandmarks[h].landmarks;
          _rawGestNames[h] = ""; _rawGestScores[h] = 0f;
          if (result.gestures != null && h < result.gestures.Count && result.gestures[h].categories.Count > 0)
          { _rawGestNames[h] = result.gestures[h].categories[0].categoryName; _rawGestScores[h] = result.gestures[h].categories[0].score; }
          for (int f = 0; f < 5; f++)
          {
            int idx = FINGERTIP_INDICES[f]; int off = h * 10 + f * 2;
            if (idx < lm.Count) { _rawNorm[off] = lm[idx].x; _rawNorm[off + 1] = lm[idx].y; }
          }
        }
        _hasNewData = true;
      }
    }

    // ==== メインスレッド ====

    private void SyncTrackingData()
    {
      int handCount; float[] norm; string[] gNames; float[] gScores;
      lock (_dataLock)
      {
        if (!_hasNewData) return;
        _hasNewData = false; handCount = _rawHandCount;
        norm = (float[])_rawNorm.Clone(); gNames = (string[])_rawGestNames.Clone(); gScores = (float[])_rawGestScores.Clone();
      }
      _hasLeftHand = false; _hasRightHand = false; _currentThumbsUpCount = 0; _currentPeaceCount = 0;
      if (_annotationLayer == null) return;
      Rect screenRect = _annotationLayer.rect;
      if (Time.frameCount % 60 == 0) UpdateAnnotationSettings();

      for (int h = 0; h < handCount; h++)
      {
        Vector2[] tips = new Vector2[5];
        for (int f = 0; f < 5; f++)
        {
          int off = h * 10 + f * 2;
          Vector3 pos = ImageCoordinate.ImageNormalizedToPoint(screenRect, norm[off], norm[off + 1], 0f, _rotationAngle, _isMirrored);
          tips[f] = new Vector2(pos.x, pos.y);
        }
        if (gNames[h] == "Thumb_Up" && gScores[h] >= THUMBS_UP_CONFIDENCE) _currentThumbsUpCount++;
        if (gNames[h] == "Victory" && gScores[h] >= THUMBS_UP_CONFIDENCE) _currentPeaceCount++;
        if (h == 0) { System.Array.Copy(tips, _leftTips, 5); _hasLeftHand = true; }
        else if (h == 1) { System.Array.Copy(tips, _rightTips, 5); _hasRightHand = true; }
      }
    }

    // ==== セットアップ ====

    private void SetupComponents()
    {
      var staffObj = new GameObject("StaffLines");
      var staffRect = staffObj.AddComponent<RectTransform>();
      staffRect.SetParent(_annotationLayer, false);
      staffRect.anchorMin = Vector2.zero; staffRect.anchorMax = Vector2.one;
      staffRect.offsetMin = Vector2.zero; staffRect.offsetMax = Vector2.zero;
      _staffLine = staffObj.AddComponent<StaffLineGraphic>();

      _notesContainer = MakeStretchChild("GameNotes", _annotationLayer);
      _effectsContainer = MakeStretchChild("GameEffects", _annotationLayer);

      var aObj = new GameObject("Audio");
      aObj.transform.SetParent(transform, false);
      _audio = aObj.AddComponent<ShuttleRunAudio>();
      _audio.Initialize();

      var canvas = _annotationLayer.GetComponentInParent<Canvas>();
      var canvasRect = canvas.GetComponent<RectTransform>();
      var uObj = new GameObject("GameUI");
      uObj.transform.SetParent(canvasRect, false);
      _gameUI = uObj.AddComponent<GameUI>();
      _gameUI.Initialize(canvasRect);
    }

    private RectTransform MakeStretchChild(string name, Transform parent)
    {
      var o = new GameObject(name); var r = o.AddComponent<RectTransform>();
      r.SetParent(parent, false); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
      r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; return r;
    }

    private void ProcessWaiting()
    {
      if (_currentThumbsUpCount >= 2)
      {
        _thumbsUpTimer += Time.deltaTime;
        if (_uiReady) _gameUI?.ShowInstruction($"Ready... {_thumbsUpTimer:F1}s / {THUMBS_UP_DURATION:F1}s");
        if (_thumbsUpTimer >= THUMBS_UP_DURATION && _uiReady) StartGame();
      }
      else
      {
        _thumbsUpTimer = Mathf.Max(0f, _thumbsUpTimer - Time.deltaTime * 2f);
        if (_uiReady)
          _gameUI?.ShowInstruction("Are you Ready?\n両手でサムズアップ！");
      }
    }

    private void StartGame()
    {
      _state = GameState.Starting;
      _score = 0; _missCount = 0; _currentNoteNumber = 0; _currentLoop = 0;
      _currentLegIndex = 0; _thumbsUpTimer = 0f;

      // プレイ回数の更新と保存
      _totalPlayCount++;
      PlayerPrefs.SetInt("ShuttleHand_TotalPlayCount", _totalPlayCount);
      PlayerPrefs.Save();
      _gameUI.UpdateTotalPlayCount(_totalPlayCount);

      _gameUI.UpdateScore(0); _gameUI.UpdateMiss(0); _gameUI.UpdateLoop(0);
      _gameUI.HideInstruction();
      _audio.PlayStart();
      StartCoroutine(GameStartSeq());
    }

    private IEnumerator GameStartSeq()
    {
      _gameUI.ShowCenterMessage("Game Start!", new Color(0.2f, 1f, 0.4f));
      yield return new WaitForSeconds(2f);
      _gameUI.HideCenterMessage();
      _state = GameState.Playing;
      StartCoroutine(SpawnSeq());
    }

    /// <summary>
    /// ① 8音 → インターバル → 8音 → インターバル
    /// インターバル詳細: n秒待機 → 合成音 → n-0.12秒待機
    /// </summary>
    private IEnumerator SpawnSeq()
    {
      _currentLegIndex = 0;

      for (int loop = 0; loop < TOTAL_LOOPS; loop++)
      {
        _currentLoop = loop;
        _gameUI.UpdateLoop(loop);

        // ── 往路 ──
        float fwdArrival = Mathf.Max(MIN_ARRIVAL_TIME, BASE_ARRIVAL_TIME - _currentLegIndex * ARRIVAL_DECREMENT);
        for (int i = 0; i < NOTES_PER_LEG; i++)
        {
          if (_state != GameState.Playing) yield break;
          SpawnNote(FORWARD_NOTES[i], fwdArrival, loop);
          yield return new WaitUntil(() => _activeNote == null);
        }
        if (_state != GameState.Playing) yield break;

        // インターバル（往路後）
        yield return StartCoroutine(PlayRestInterval());
        if (_state != GameState.Playing) yield break;

        _currentLegIndex++;

        // ── 復路 ──
        float revArrival = Mathf.Max(MIN_ARRIVAL_TIME, BASE_ARRIVAL_TIME - _currentLegIndex * ARRIVAL_DECREMENT);
        for (int i = 0; i < NOTES_PER_LEG; i++)
        {
          if (_state != GameState.Playing) yield break;
          SpawnNote(REVERSE_NOTES[i], revArrival, loop);
          yield return new WaitUntil(() => _activeNote == null);
        }
        if (_state != GameState.Playing) yield break;

        _currentLegIndex++;

        // インターバル（復路後）
        if (loop < TOTAL_LOOPS - 1)
        {
          yield return StartCoroutine(PlayRestInterval());
          if (_state != GameState.Playing) yield break;
        }
      }

      if (_state == GameState.Playing)
      {
        _state = GameState.GameClear;
        StartCoroutine(EndGameSequence());
      }
    }

    private IEnumerator PlayRestInterval()
    {
      // n = 0.9 - currentLegIndex * 0.06
      // leg0: 0.9 + 0.78 = 1.68
      // leg1: 0.84 + 0.72 = 1.56
      float n = 0.9f - _currentLegIndex * 0.06f;
      
      _gameUI.ShowCenterMessage("Rest", new Color(0.8f, 0.9f, 1f, 0.5f));
      yield return new WaitForSeconds(Mathf.Max(0.01f, n));
      
      _audio.PlayRest();
      
      yield return new WaitForSeconds(Mathf.Max(0.01f, n - 0.12f));
      _gameUI.HideCenterMessage();
    }

    /// <summary>
    /// 終了シーケンス（文化祭用: 自動ループ）
    /// ① スコア表示（右側）→ 5秒待ち
    /// ② 「終わるならピース」表示（左側）→ ピース検出待ち
    /// ③ UI非表示 → 5秒待ち → 待機画面に戻る
    /// </summary>
    private IEnumerator EndGameSequence()
    {
      _state = GameState.Ending;

      // 最高得点の判定と保存
      if (_score > _highScore)
      {
        _highScore = _score;
        PlayerPrefs.SetInt("ShuttleHand_HighScore", _highScore);
        PlayerPrefs.Save();
        _gameUI.UpdateHighScore(_highScore);
      }

      // ① スコア表示を中央上部に (緑影)
      _gameUI.HideInstruction();
      _gameUI.ShowCenterMessageUpper($"シャトルラン おつかれさま！\nあなたのスコアは\n\u266a = {_score}", Color.white);
      yield return new WaitForSeconds(3f);

      // ② 「楽しかったならピース」を中央下部に表示 (オレンジ影)
      _gameUI.ShowInstructionLower("楽しかったならピース ✌");

      // ピース（Victory）1本を待つ
      yield return new WaitUntil(() => _currentPeaceCount >= 1);

      // ③ ピース受け取り → UI全非表示
      _gameUI.HideCenterMessage();
      _gameUI.HideInstruction();

      // 3秒待ってから待機画面へ（次のプレイヤー用）
      yield return new WaitForSeconds(3f);

      // 待機画面に戻る
      _state = GameState.Waiting;
      _thumbsUpTimer = 0f;
      _gameUI.UpdateScore(0);
      _gameUI.UpdateMiss(0);
      _gameUI.UpdateLoop(0);
      _gameUI.ShowInstruction("Are you Ready?\n両手でサムズアップ！");
    }

    private void SpawnNote(int noteIdx, float arrivalTime, int loopIdx)
    {
      _currentNoteNumber++;
      var data = new NoteData(noteIdx, arrivalTime, loopIdx);
      Rect r = _annotationLayer.rect;
      Vector2 center = new Vector2((r.xMin + r.xMax) * 0.5f, (r.yMin + r.yMax) * 0.5f);
      Vector2 target = GenTargetPos(r);
      SpawnTargetCircle(target, noteIdx);
      var nObj = new GameObject($"Note_{_currentNoteNumber}");
      _activeNote = nObj.AddComponent<NoteController>();
      _activeNote.Initialize(data, center, target, _notesContainer, OnNoteArrived);
    }

    private void OnNoteArrived(bool _, NoteData noteData)
    {
      if (_activeNote == null) return;
      Vector2 pos = _activeNote.GetTargetPosition();
      bool hit = _staffLine != null && _staffLine.IsPointOnStaff(pos, HIT_THRESHOLD);

      if (hit)
      {
        _score++;
        _gameUI.UpdateScore(_score);
        _audio.PlayNote(noteData.noteIndex);
        SpawnHitEffect(pos, noteData.noteIndex, Mathf.Clamp(_currentLegIndex + 1, 1, 11));
      }
      else
      {
        _missCount++;
        _gameUI.UpdateMiss(_missCount);
        _gameUI.ShowMissPopup();
        _audio.PlayBuzzer();
      }

      _activeNote.DestroyNote(); _activeNote = null;
      if (_activeTargetCircle != null) { Destroy(_activeTargetCircle); _activeTargetCircle = null; }

      if (_missCount >= MAX_MISSES)
      {
        _state = GameState.GameOver;
        StopAllCoroutines();
        StartCoroutine(EndGameSequence());
      }
    }

    // ==== 座標・エフェクト ====

    private Vector2 GenTargetPos(Rect r)
    {
      float edge = 0.15f, cExcl = 0.10f; 
      float bottomAdd = 0.10f; // 画面下側の除外領域をさらに拡大
      float distExcl = 0.15f;  // 前回地点から画面幅の15%以内ならやり直し
      float w = r.width, h = r.height;
      float cx = (r.xMin + r.xMax) * 0.5f, cy = (r.yMin + r.yMax) * 0.5f;
      Vector2 t; int att = 0;
      do { 
        t = new Vector2(
          Random.Range(r.xMin + w * edge, r.xMax - w * edge), 
          Random.Range(r.yMin + h * (edge + bottomAdd), r.yMax - h * edge)
        ); 
        att++; 
      }
      while (att < 50 && (
        (Mathf.Abs(t.x - cx) < w * cExcl && Mathf.Abs(t.y - cy) < h * cExcl) || // 中心除外
        (Vector2.Distance(t, _lastTargetPos) < w * distExcl)                   // 前回位置除外
      ));
      
      _lastTargetPos = t;
      return t;
    }

    private void SpawnTargetCircle(Vector2 pos, int noteIdx)
    {
      if (_activeTargetCircle != null) Destroy(_activeTargetCircle);
      var o = new GameObject("TargetCircle");
      var r = o.AddComponent<RectTransform>();
      r.SetParent(_notesContainer, false);
      r.anchoredPosition = pos;
      r.sizeDelta = new Vector2(160f, 160f);
      var img = o.AddComponent<Image>();
      img.sprite = MakeCircleSprite();
      img.color = NoteController.PASTEL_COLORS[noteIdx % NoteController.PASTEL_COLORS.Length];
      img.raycastTarget = false;
      StartCoroutine(Pulse(r));
      _activeTargetCircle = o;
    }

    private IEnumerator Pulse(RectTransform r)
    {
      while (r != null) { r.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 3f) * 0.12f); yield return null; }
    }

    private void SpawnHitEffect(Vector2 pos, int noteIdx, int ringCount)
    {
      var o = new GameObject("HitEffect");
      var r = o.AddComponent<RectTransform>();
      r.SetParent(_effectsContainer, false);
      r.anchoredPosition = pos;
      r.sizeDelta = new Vector2(300f, 300f);
      var e = o.AddComponent<HitEffect>();
      Color c = NoteController.NOTE_COLORS[noteIdx % NoteController.NOTE_COLORS.Length];
      c.a = 0.8f;
      e.Play(c, ringCount, 0.7f, 140f);
    }

    private static Sprite _cSprite;
    private static Sprite MakeCircleSprite()
    {
      if (_cSprite != null) return _cSprite;
      int sz = 64; var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
      float c = sz * 0.5f, rad = c - 1f;
      for (int y = 0; y < sz; y++)
        for (int x = 0; x < sz; x++)
          tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(rad - Vector2.Distance(new Vector2(x, y), new Vector2(c, c)))));
      tex.Apply();
      _cSprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
      return _cSprite;
    }
  }
}
