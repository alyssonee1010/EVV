using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(EVVDefender))]
public class EVVBoardMeleeAttacker : MonoBehaviour
{
    [SerializeField] float attackRange = 1.1f;
    [SerializeField] float attackCooldown = 1.1f;
    [SerializeField] int attackDamage = 20;
    [SerializeField] float recoilMultiplier = 0f;
    [SerializeField] Vector2 attackDirection = Vector2.right;
    [FormerlySerializedAs("laneTolerance")]
    [SerializeField, Min(0f)] float depthTolerance = EVVLaneDepth.DefaultDepthTolerance;
    [SerializeField] string attackTriggerName = "Attack";

    [Header("Audio")]
    [SerializeField] EVVAnimationSoundPlayer soundPlayer;

    EVVDefender boardCharacter;
    EVVHitRecoil stunProfile;
    Animator animator;
    IEVVEnemyLaneWalker attackTarget;
    float attackTimer;

    void Awake()
    {
        boardCharacter = GetComponent<EVVDefender>();
        stunProfile = GetComponent<EVVHitRecoil>();
        animator = GetComponent<Animator>();
        if (soundPlayer == null)
        {
            soundPlayer = GetComponent<EVVAnimationSoundPlayer>();
        }
    }

    void Update()
    {
        attackTimer -= Time.deltaTime * EVVActionSpeedModifier.GetMultiplier(this);
        if (attackTimer > 0f || animator == null || string.IsNullOrWhiteSpace(attackTriggerName))
        {
            return;
        }

        if (!TryFindEnemyInRange(out IEVVEnemyLaneWalker target))
        {
            attackTarget = null;
            animator.ResetTrigger(attackTriggerName);
            return;
        }

        attackTarget = target;
        attackTimer = Mathf.Max(0.01f, attackCooldown);
        animator.ResetTrigger(attackTriggerName);
        animator.SetTrigger(attackTriggerName);
    }

    public void DealAttackDamage()
    {
        if (!IsValidTarget(attackTarget) || !IsInSameLane(attackTarget) || !IsInRange(attackTarget))
        {
            if (!TryFindEnemyInRange(out attackTarget))
            {
                return;
            }
        }

        EVVHealth targetHealth = attackTarget.Health;
        targetHealth.TakeDamage(attackDamage, recoilMultiplier);
        // Played here instead of by an animation event so the impact sound only plays when the swing
        // actually connects; the animation keeps going after the target has already died.
        if (soundPlayer != null)
        {
            soundPlayer.PlayAttackSounds();
        }

        if (targetHealth.IsAlive && stunProfile != null && TryGetEnemyObject(attackTarget, out GameObject targetObject))
        {
            stunProfile.TryStunTarget(targetObject);
        }
    }

    public void DealAttackDamageAnimationEvent()
    {
        DealAttackDamage();
    }

    bool TryFindEnemyInRange(out IEVVEnemyLaneWalker target)
    {
        target = null;
        float bestForwardDistance = float.PositiveInfinity;
        Vector2 forward = attackDirection.sqrMagnitude > 0f ? attackDirection.normalized : Vector2.right;
        IReadOnlyList<IEVVEnemyLaneWalker> enemies = EVVTargetRegistry.Enemies;

        for (int i = 0; i < enemies.Count; i++)
        {
            IEVVEnemyLaneWalker enemy = enemies[i];
            if (!TryGetEnemyObject(enemy, out GameObject enemyObject))
            {
                continue;
            }

            if (!IsValidTarget(enemy))
            {
                continue;
            }

            if (!IsInSameLane(enemy))
            {
                continue;
            }

            Vector2 toEnemy = enemyObject.transform.position - transform.position;
            float forwardDistance = Vector2.Dot(toEnemy, forward);
            if (forwardDistance >= 0f && forwardDistance <= attackRange)
            {
                if (forwardDistance < bestForwardDistance)
                {
                    bestForwardDistance = forwardDistance;
                    target = enemy;
                }
            }
        }

        return target != null;
    }

    bool IsValidTarget(IEVVEnemyLaneWalker enemy)
    {
        return TryGetEnemyObject(enemy, out _) && enemy.Health != null && enemy.Health.IsAlive;
    }

    bool IsInRange(IEVVEnemyLaneWalker enemy)
    {
        if (!TryGetEnemyObject(enemy, out GameObject enemyObject))
        {
            return false;
        }

        Vector2 forward = attackDirection.sqrMagnitude > 0f ? attackDirection.normalized : Vector2.right;
        Vector2 toEnemy = enemyObject.transform.position - transform.position;
        float forwardDistance = Vector2.Dot(toEnemy, forward);
        return forwardDistance >= 0f && forwardDistance <= attackRange;
    }

    bool IsInSameLane(IEVVEnemyLaneWalker enemy)
    {
        return TryGetEnemyObject(enemy, out GameObject enemyObject)
            && (boardCharacter == null || !boardCharacter.HasCell || enemy.LaneIndex == boardCharacter.Cell.y)
            && EVVLaneDepth.IsSameDepth(transform, enemyObject.transform, depthTolerance);
    }

    bool TryGetEnemyObject(IEVVEnemyLaneWalker enemy, out GameObject enemyObject)
    {
        enemyObject = null;
        if (enemy is not MonoBehaviour enemyBehaviour || enemyBehaviour == null)
        {
            return false;
        }

        enemyObject = enemyBehaviour.gameObject;
        return enemyObject != null;
    }
}
