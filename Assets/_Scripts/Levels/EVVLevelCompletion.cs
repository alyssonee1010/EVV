using System;
using System.Collections.Generic;
using UnityEngine;

// Persists the stable ids of levels the player has completed.
public static class EVVLevelCompletion
{
    const string CompletedLevelIdsPrefsKey = "EVV_CompletedLevelIds";

    static HashSet<string> completedLevelIds;

    public static event Action ProgressChanged;

    static void EnsureLoaded()
    {
        // Unity can clear PlayerPrefs without reloading scripts, so do not retain stale history.
        if (completedLevelIds != null && !PlayerPrefs.HasKey(CompletedLevelIdsPrefsKey))
        {
            completedLevelIds = null;
        }

        if (completedLevelIds != null)
        {
            return;
        }

        completedLevelIds = new HashSet<string>();
        string saved = PlayerPrefs.GetString(CompletedLevelIdsPrefsKey, "");
        foreach (string id in saved.Split(','))
        {
            if (!string.IsNullOrEmpty(id))
            {
                completedLevelIds.Add(id);
            }
        }
    }

    public static bool IsCompleted(string levelId)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(levelId) && completedLevelIds.Contains(levelId);
    }

    public static IReadOnlyCollection<string> GetCompletedLevelIds()
    {
        EnsureLoaded();
        return completedLevelIds;
    }

    // Levels are supplied in progression order. The first level is always available, then each
    // following level is revealed only after the immediately previous one has been completed.
    public static List<EVVLevelDefinition> GetAvailableLevels(IReadOnlyList<EVVLevelDefinition> levels)
    {
        List<EVVLevelDefinition> availableLevels = new List<EVVLevelDefinition>();
        if (levels == null || levels.Count == 0)
        {
            return availableLevels;
        }

        availableLevels.Add(levels[0]);
        for (int i = 1; i < levels.Count; i++)
        {
            if (!IsCompleted(levels[i - 1].Id))
            {
                break;
            }

            availableLevels.Add(levels[i]);
        }

        return availableLevels;
    }

    public static bool MarkCompleted(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
        {
            return false;
        }

        EnsureLoaded();
        if (!completedLevelIds.Add(levelId))
        {
            return false;
        }

        PlayerPrefs.SetString(CompletedLevelIdsPrefsKey, string.Join(",", completedLevelIds));
        PlayerPrefs.Save();
        ProgressChanged?.Invoke();
        return true;
    }

    // Keep runtime state in sync when PlayerPrefs are cleared without restarting Unity.
    public static void ReloadAfterPlayerPrefsClear()
    {
        completedLevelIds = null;
        ProgressChanged?.Invoke();
    }
}
