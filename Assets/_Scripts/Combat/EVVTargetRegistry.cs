using System.Collections.Generic;
using UnityEngine;

// Enemies and defenders register themselves here while enabled, so per-frame targeting (shooters,
// melee attackers, projectiles, walking enemies) iterates a short list instead of scanning every
// MonoBehaviour in the scene with FindObjectsByType.
public static class EVVTargetRegistry
{
    static readonly List<IEVVEnemyLaneWalker> enemies = new List<IEVVEnemyLaneWalker>();
    static readonly List<IEVVEnemyLaneWalker> charmedEnemies = new List<IEVVEnemyLaneWalker>();
    static readonly List<EVVDefender> defenders = new List<EVVDefender>();

    public static IReadOnlyList<IEVVEnemyLaneWalker> Enemies => enemies;
    // Enemies that switched sides (a Viking carrying the charm lure). Defenders and projectiles
    // leave them alone; the remaining enemies fight them.
    public static IReadOnlyList<IEVVEnemyLaneWalker> CharmedEnemies => charmedEnemies;
    public static IReadOnlyList<EVVDefender> Defenders => defenders;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        enemies.Clear();
        charmedEnemies.Clear();
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
        charmedEnemies.Remove(enemy);
    }

    // Moves a walker between the enemy and charmed lists.
    public static void SetCharmed(IEVVEnemyLaneWalker enemy, bool charmed)
    {
        if (enemy == null)
        {
            return;
        }

        List<IEVVEnemyLaneWalker> from = charmed ? enemies : charmedEnemies;
        List<IEVVEnemyLaneWalker> to = charmed ? charmedEnemies : enemies;
        from.Remove(enemy);
        if (!to.Contains(enemy))
        {
            to.Add(enemy);
        }
    }

    // Whether a walker is still a valid target for the defenders' side.
    public static bool IsEnemy(IEVVEnemyLaneWalker enemy)
    {
        return enemy != null && enemies.Contains(enemy);
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
