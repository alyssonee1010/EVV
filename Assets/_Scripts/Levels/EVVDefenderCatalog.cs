using System.Collections.Generic;
using UnityEngine;

// Single source of truth mapping a defender id (as used in level YAML "unlocks" lists) to its
// placeable prefab, diamond cost, and display name. EVVDefenderUnlocks/EVVDefenderLoadoutUI
// look entries up here by id.
public class EVVDefenderCatalog : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string id = "";
        public string displayName = "";
        public EVVDefender prefab;
        public int cost = 1;
        [Tooltip("If true, this defender is unlocked from the start without needing a level unlock.")]
        public bool unlockedByDefault;
    }

    [SerializeField] Entry[] entries;

    public static EVVDefenderCatalog Instance { get; private set; }

    public IReadOnlyList<Entry> Entries => entries;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public Entry FindById(string id)
    {
        if (entries == null || string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (Entry entry in entries)
        {
            if (entry != null && entry.id == id)
            {
                return entry;
            }
        }

        return null;
    }
}
