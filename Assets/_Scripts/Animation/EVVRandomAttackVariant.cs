using UnityEngine;

/// <summary>
/// Picks one of a character's attacks at random each time its attack state
/// machine is entered. Put this on the attack sub-state machine and condition its
/// entry transitions on the integer parameter, one value per attack.
///
/// It lives here because EVVEnemyVikingWalker only ever sets a single "Attack"
/// trigger, so a character with more than one attack has nothing else to choose
/// with. Nothing outside the animator needs to know how many attacks there are.
/// </summary>
public class EVVRandomAttackVariant : StateMachineBehaviour
{
    [Tooltip("Integer parameter the entry transitions compare against.")]
    [SerializeField] string parameterName = "AttackVariant";
    [Tooltip("How many attacks this character has. Values 0..count-1 are chosen.")]
    [SerializeField, Min(1)] int variantCount = 2;
    [Tooltip("Never play the same attack twice in a row while more than one exists.")]
    [SerializeField] bool avoidRepeats = true;

    int lastVariant = -1;

    public override void OnStateMachineEnter(Animator animator, int stateMachinePathHash)
    {
        if (animator == null || variantCount <= 0)
        {
            return;
        }

        int variant = Random.Range(0, variantCount);
        if (avoidRepeats && variantCount > 1 && variant == lastVariant)
        {
            variant = (variant + 1 + Random.Range(0, variantCount - 1)) % variantCount;
        }

        lastVariant = variant;
        animator.SetInteger(parameterName, variant);
    }
}
