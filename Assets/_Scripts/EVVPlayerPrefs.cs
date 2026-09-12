using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
#endif

// Single reset point for every PlayerPrefs-backed EVV system.
public static class EVVPlayerPrefs
{
    public static void ClearAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        EVVDefenderUnlocks.ReloadAfterPlayerPrefsClear();
        EVVLevelCompletion.ReloadAfterPlayerPrefsClear();
        EVVAudioSettings.ReloadAfterPlayerPrefsClear();
    }

#if UNITY_EDITOR
    [MenuItem("Tools/EVV/Clear PlayerPrefs")]
    static void ClearAllFromMenu()
    {
        ClearAll();
        Debug.Log("EVV PlayerPrefs cleared. Progression and audio settings are back to defaults.");
    }

    [MenuItem("Tools/EVV/Unlock Everything (Dev)")]
    static void UnlockEverythingFromMenu()
    {
        if (EVVDefenderCatalog.Instance != null)
        {
            List<string> allDefenderIds = EVVDefenderCatalog.Instance.Entries
                .Where(entry => entry != null)
                .Select(entry => entry.id)
                .ToList();
            EVVDefenderUnlocks.UnlockAll(allDefenderIds);
        }
        else
        {
            Debug.LogWarning("EVV Unlock Everything: no EVVDefenderCatalog instance found (enter Play Mode first to unlock defenders).");
        }

        foreach (EVVLevelDefinition level in EVVLevelLoader.DiscoverLevels())
        {
            EVVLevelCompletion.MarkCompleted(level.Id);
        }

        Debug.Log("EVV dev unlock: all defenders and levels unlocked.");
    }
#endif
}
