using System.Collections.Generic;
using UnityEngine;

// The Hammer viking's spin attack: instead of hitting the one defender in front of him, the hammer
// sweeps a full circle around his body and everything standing close enough in his own lane and in
// the lanes either side is hit at once. The spin clip fires DealSpinDamageAnimationEvent once per
// revolution, so the number of ticks is the animation's business and the reach is this component's.
[RequireComponent(typeof(EVVEnemyVikingWalker))]
public class EVVHammerSpin : MonoBehaviour
{
    [SerializeField] int damage = 25;
    [Tooltip("How far along the lane the hammer head reaches, in world units.")]
    [SerializeField, Min(0f)] float radius = 1.9f;
    [Tooltip("How many lanes to either side the sweep reaches. 1 = his own lane plus the ones above and below.")]
    [SerializeField, Min(0)] int laneReach = 1;
    [SerializeField, Min(0f)] float recoilMultiplier = 1f;

    [Header("Audio")]
    [SerializeField] EVVAnimationSoundPlayer soundPlayer;

    EVVEnemyVikingWalker walker;
    readonly List<EVVDefender> hits = new List<EVVDefender>();

    void Awake()
    {
        walker = GetComponent<EVVEnemyVikingWalker>();
        if (soundPlayer == null)
        {
            soundPlayer = GetComponent<EVVAnimationSoundPlayer>();
        }
    }

    // Called by an animation event in the spin clip, at the moment the hammer is at full extension.
    public void DealSpinDamageAnimationEvent()
    {
        CollectTargets();
        for (int i = 0; i < hits.Count; i++)
        {
            hits[i].Health.TakeDamage(damage, recoilMultiplier);
        }

        // Played here rather than by its own event so the impact only sounds when something is hit.
        if (hits.Count > 0 && soundPlayer != null)
        {
            soundPlayer.PlayAttackSounds();
        }
    }

    // Collected first and damaged afterwards: taking damage can kill a defender and remove it from
    // the registry, which would shift the list being walked.
    void CollectTargets()
    {
        hits.Clear();
        float x = transform.position.x;
        IReadOnlyList<EVVDefender> defenders = EVVTargetRegistry.Defenders;
        for (int i = 0; i < defenders.Count; i++)
        {
            EVVDefender defender = defenders[i];
            if (defender == null || !defender.HasCell || !defender.IsTargetable)
            {
                continue;
            }

            if (defender.Health == null || !defender.Health.IsAlive)
            {
                continue;
            }

            if (Mathf.Abs(defender.Cell.y - walker.LaneIndex) > laneReach)
            {
                continue;
            }

            if (Mathf.Abs(defender.transform.position.x - x) > radius)
            {
                continue;
            }

            hits.Add(defender);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
