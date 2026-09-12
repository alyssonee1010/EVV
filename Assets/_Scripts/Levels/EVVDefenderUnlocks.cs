using System;
using System.Collections.Generic;
using UnityEngine;

// Tracks which defender ids are unlocked (persisted across sessions via PlayerPrefs) and which
// are selected for the current run's 6-slot loadout (in-memory only, reset per session start).
public static class EVVDefenderUnlocks
{
    const string UnlockedIdsPrefsKey = "EVV_UnlockedDefenderIds";
    const int MaxLoadoutSize = 6;

    static HashSet<string> unlockedIds;
    static List<string> loadoutIds = new List<string>();
    static bool seededFromCatalog;
    static bool loadoutCustomizedByPlayer;

    public static event Action UnlocksChanged;
    public static event Action LoadoutChanged;

    static void EnsureLoaded()
    {
        // Unity's built-in Clear All PlayerPrefs command does not restart the scripting domain.
        // Drop stale runtime state when that command removes our persisted key.
        if (unlockedIds != null && !PlayerPrefs.HasKey(UnlockedIdsPrefsKey))
        {
            unlockedIds = null;
            loadoutIds.Clear();
            seededFromCatalog = false;
            loadoutCustomizedByPlayer = false;
        }

        if (unlockedIds == null)
        {
            unlockedIds = new HashSet<string>();
            string saved = PlayerPrefs.GetString(UnlockedIdsPrefsKey, "");
            if (!string.IsNullOrEmpty(saved))
            {
                foreach (string id in saved.Split(','))
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        unlockedIds.Add(id);
                    }
                }
            }
        }

        // Retried every call (not just once) because EVVDefenderCatalog.Awake() may not have
        // run yet the first time a caller reaches this — Unity does not guarantee sibling
        // component Awake order, so Instance can still be null on the first EnsureLoaded call.
        if (!seededFromCatalog)
        {
            SeedDefaultUnlocks();
        }
    }

    static void SeedDefaultUnlocks()
    {
        if (EVVDefenderCatalog.Instance == null)
        {
            return;
        }

        seededFromCatalog = true;
        bool changed = false;
        foreach (EVVDefenderCatalog.Entry entry in EVVDefenderCatalog.Instance.Entries)
        {
            if (entry != null && entry.unlockedByDefault && unlockedIds.Add(entry.id))
            {
                changed = true;
            }
        }

        if (changed)
        {
            Save();
        }
    }

    public static bool IsUnlocked(string defenderId)
    {
        EnsureLoaded();
        return unlockedIds.Contains(defenderId);
    }

    public static IReadOnlyCollection<string> GetUnlockedIds()
    {
        EnsureLoaded();
        return unlockedIds;
    }

    public static bool Unlock(string defenderId)
    {
        if (string.IsNullOrEmpty(defenderId))
        {
            return false;
        }

        EnsureLoaded();
        if (!unlockedIds.Add(defenderId))
        {
            return false;
        }

        Save();
        UnlocksChanged?.Invoke();
        return true;
    }

    public static List<string> UnlockAll(IEnumerable<string> defenderIds)
    {
        List<string> newlyUnlocked = new List<string>();
        if (defenderIds == null)
        {
            return newlyUnlocked;
        }

        EnsureLoaded();
        bool changed = false;
        foreach (string defenderId in defenderIds)
        {
            if (!string.IsNullOrEmpty(defenderId) && unlockedIds.Add(defenderId))
            {
                newlyUnlocked.Add(defenderId);
                changed = true;
            }
        }

        if (changed)
        {
            Save();
            UnlocksChanged?.Invoke();
        }

        return newlyUnlocked;
    }

    static void Save()
    {
        PlayerPrefs.SetString(UnlockedIdsPrefsKey, string.Join(",", unlockedIds));
        PlayerPrefs.Save();
    }

    // PlayerPrefs can be cleared while the game is running from the editor. Reset the cached
    // progression too, otherwise callers keep seeing the values loaded before the clear.
    public static void ReloadAfterPlayerPrefsClear()
    {
        unlockedIds = null;
        loadoutIds.Clear();
        seededFromCatalog = false;
        loadoutCustomizedByPlayer = false;
        UnlocksChanged?.Invoke();
        LoadoutChanged?.Invoke();
    }

    public static IReadOnlyList<string> GetLoadout()
    {
        EnsureLoaded();
        PruneLoadout();
        FillLoadoutIfEmpty();
        return loadoutIds;
    }

    // Auto-fills open loadout slots with unlocked defenders so a fresh save is playable without
    // requiring a manual toggle first. Stops once the player has ever touched their loadout, so
    // deliberately running fewer than 6 defenders doesn't get silently refilled. Walks the
    // catalog's own order (not the unordered unlockedIds set) so the initial tray order is
    // deterministic and matches the grid's left-to-right/top-to-bottom layout.
    static void FillLoadoutIfEmpty()
    {
        if (loadoutCustomizedByPlayer || loadoutIds.Count >= MaxLoadoutSize || EVVDefenderCatalog.Instance == null)
        {
            return;
        }

        bool changed = false;
        foreach (EVVDefenderCatalog.Entry entry in EVVDefenderCatalog.Instance.Entries)
        {
            if (loadoutIds.Count >= MaxLoadoutSize)
            {
                break;
            }

            if (entry != null && unlockedIds.Contains(entry.id) && !loadoutIds.Contains(entry.id))
            {
                loadoutIds.Add(entry.id);
                changed = true;
            }
        }

        if (changed)
        {
            LoadoutChanged?.Invoke();
        }
    }

    public static bool IsInLoadout(string defenderId)
    {
        EnsureLoaded();
        return loadoutIds.Contains(defenderId);
    }

    public static bool ToggleLoadout(string defenderId)
    {
        EnsureLoaded();
        if (!IsUnlocked(defenderId))
        {
            return false;
        }

        loadoutCustomizedByPlayer = true;

        if (loadoutIds.Contains(defenderId))
        {
            loadoutIds.Remove(defenderId);
            LoadoutChanged?.Invoke();
            return true;
        }

        if (loadoutIds.Count >= MaxLoadoutSize)
        {
            return false;
        }

        loadoutIds.Add(defenderId);
        LoadoutChanged?.Invoke();
        return true;
    }

    static void PruneLoadout()
    {
        int removed = loadoutIds.RemoveAll(id => !unlockedIds.Contains(id));
        if (removed > 0)
        {
            LoadoutChanged?.Invoke();
        }
    }
}
