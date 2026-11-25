using System;

namespace Mediapipe.Unity.Sample.ShuttleHand
{
  [Serializable]
  public class NoteData
  {
    /// <summary>0=ド, 1=レ, 2=ミ, 3=ファ, 4=ソ, 5=ラ, 6=シ, 7=高ド</summary>
    public int noteIndex;
    /// <summary>到達時間（秒）</summary>
    public float arrivalTime;
    /// <summary>ループ番号 (0-10)</summary>
    public int loopIndex;
    /// <summary>休憩回かどうか</summary>
    public bool isRest;

    public NoteData(int noteIndex, float arrivalTime, int loopIndex, bool isRest = false)
    {
      this.noteIndex = noteIndex;
      this.arrivalTime = arrivalTime;
      this.loopIndex = loopIndex;
      this.isRest = isRest;
    }

    public static string GetNoteName(int index)
    {
      return index switch
      {
        0 => "ド",
        1 => "レ",
        2 => "ミ",
        3 => "ファ",
        4 => "ソ",
        5 => "ラ",
        6 => "シ",
        7 => "高ド",
        _ => "?"
      };
    }
  }
}
