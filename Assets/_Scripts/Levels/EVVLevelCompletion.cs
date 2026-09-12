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

    // Stage 0 is the developer stage: its levels are always listed and never gate the real
    // progression, so a test level can sit first without hiding the game's first level.
    public const int DeveloperStage = 0;

    // Levels are supplied in progression order. The first level is always available, then each
    // following level is revealed only after the immediately previous one has been completed.
    public static List<EVVLevelDefinition> GetAvailableLevels(IReadOnlyList<EVVLevelDefinition> levels)
    {
        List<EVVLevelDefinition> availableLevels = new List<EVVLevelDefinition>();
        if (levels == null)
        {
            return availableLevels;
        }

        EVVLevelDefinition previousProgressionLevel = null;
        foreach (EVVLevelDefinition level in levels)
        {
            if (level.Stage == DeveloperStage)
            {
                availableLevels.Add(level);
                continue;
            }

            if (previousProgressionLevel != null && !IsCompleted(previousProgressionLevel.Id))
            {
                break;
            }

            availableLevels.Add(level);
            previousProgressionLevel = level;
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
