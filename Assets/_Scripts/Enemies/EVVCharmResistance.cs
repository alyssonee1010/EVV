using UnityEngine;

// How a Viking answers the Girl's charm (EVVCharmLure). Same shape as the stun resistance on
// EVVHitRecoil: a percent that scales the lure's chance down (0 = always charmed, 100 = never),
// plus a hard flag for Vikings the lure simply does nothing to. Vikings without this component
// are fully susceptible.
public class EVVCharmResistance : MonoBehaviour
{
    [Tooltip("Never charmed, whatever the roll says: this Viking is just not interested.")]
    [SerializeField] bool immune;
    [Tooltip("Chance to shrug the charm off. 0 = always charmed, 100 = never charmed.")]
    [SerializeField, Range(0f, 100f)] float resistancePercent;

    public bool Immune => immune;
    public float ResistancePercent => Mathf.Clamp(resistancePercent, 0f, 100f);
}
