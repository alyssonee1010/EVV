using System.Collections.Generic;
using UnityEngine;

// Enemies and defenders register themselves here while enabled, so per-frame targeting (shooters,
// melee attackers, projectiles, walking enemies) iterates a short list instead of scanning every
// MonoBehaviour in the scene with FindObjectsByType.
public static class EVVTargetRegistry
{
    static readonly List<IEVVEnemyLaneWalker> enemies = new List<IEVVEnemyLaneWalker>();
    static readonly List<EVVDefender> defenders = new List<EVVDefender>();

    public static IReadOnlyList<IEVVEnemyLaneWalker> Enemies => enemies;
    public static IReadOnlyList<EVVDefender> Defenders => defenders;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        enemies.Clear();
        defenders.Clear();
    }

    public static void Add(IEVVEnemyLaneWalker enemy)
    {
        if (enemy != null && !enemies.Contains(enemy))
        {
            enemies.Add(enemy);
        }
    }

    public static void Remove(IEVVEnemyLaneWalker enemy)
    {
        enemies.Remove(enemy);
    }

    public static void Add(EVVDefender defender)
    {
        if (defender != null && !defenders.Contains(defender))
        {
            defenders.Add(defender);
        }
    }

    public static void Remove(EVVDefender defender)
    {
        defenders.Remove(defender);
    }
}
