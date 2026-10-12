using System;
using UnityEngine;

// Best time per puzzle, kept in PlayerPrefs so it survives the headset being
// put down. Same shape as GameSettings: static, no scene object to wire, and
// anything that displays a record listens for the change.
public static class PuzzleRecords
{
    public const int PuzzleCount = 4;

    private const string KeyPrefix = "nau.best.";

    public static event Action Changed;

    public static bool Has(int puzzle)
    {
        return InRange(puzzle) && PlayerPrefs.HasKey(Key(puzzle));
    }

    // Seconds, or -1 when the puzzle has never been finished
    public static float GetBest(int puzzle)
    {
        return Has(puzzle) ? PlayerPrefs.GetFloat(Key(puzzle)) : -1f;
    }

    // True when this run set a new record - first finish, or faster than the
    // stored one. A slower run is simply dropped.
    public static bool Submit(int puzzle, float seconds)
    {
        if (!InRange(puzzle) || seconds <= 0f)
            return false;

        if (Has(puzzle) && PlayerPrefs.GetFloat(Key(puzzle)) <= seconds)
            return false;

        PlayerPrefs.SetFloat(Key(puzzle), seconds);
        PlayerPrefs.Save();

        Changed?.Invoke();
        return true;
    }

    public static void Clear()
    {
        for (int puzzle = 1; puzzle <= PuzzleCount; puzzle++)
            PlayerPrefs.DeleteKey(Key(puzzle));

        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // "1:08.4" - built from whole units so a rounded 59.97 cannot print ":60"
    public static string Format(float seconds, bool showTenths = true)
    {
        if (seconds < 0f) return string.Empty;

        int whole = Mathf.FloorToInt(seconds);
        int minutes = whole / 60;
        int rest = whole % 60;

        if (!showTenths)
            return $"{minutes}:{rest:00}";

        int tenths = Mathf.FloorToInt((seconds - whole) * 10f);
        return $"{minutes}:{rest:00}.{tenths}";
    }

    private static bool InRange(int puzzle) => puzzle >= 1 && puzzle <= PuzzleCount;

    private static string Key(int puzzle) => KeyPrefix + puzzle;

    // Statics survive "enter play mode without domain reload", so wipe them
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
    }
}
