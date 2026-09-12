using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(EVVHealth))]
public class EVVEnemyVikingWalker : MonoBehaviour, IEVVEnemyLaneWalker
{
    const string AttackTriggerName = "Attack";
    const string AfterKillTriggerName = "AfterKill";
    const string GrabTriggerName = "Grab";
    const string CarryPointName = "Carry Point";
    const string WalkStateName = "walk";
    const string WalkingStateName = "walking";

    [Header("Movement")]
    [SerializeField] float moveSpeed = 0.75f;
    [SerializeField] float reachDistance = 0.05f;

    [Header("Targeting")]
    [SerializeField] float attackStartDistance = 0.8f;
    [FormerlySerializedAs("laneTolerance")]
    [SerializeField, Min(0f)] float depthTolerance = EVVLaneDepth.DefaultDepthTolerance;
    [SerializeField] float overlapTolerance = 0.05f;

    [Header("Attack")]
    [SerializeField] int attackDamage = 25;
    [SerializeField, Min(0f)] float firstAttackDamageMultiplier = 1f;
    [SerializeField] float attackRecoilMultiplier = 0f;
    [SerializeField] bool useAttackAnimationEvents = true;
    [SerializeField, Min(0f)] float fallbackFirstAttackDelay = 0.5f;
    [SerializeField, Min(0.01f)] float fallbackAttackInterval = 1.5f;

    [Header("Animation")]
    [SerializeField, Min(0f)] float afterKillLockSeconds = 0.8f;

    [Header("Board Damage")]
    [SerializeField, Min(1)] int boardDamageOnExit = 1;

    [Header("Charm")]
    [Tooltip("Bone a charmed lure is held by, so it rides the walk cycle. Falls back to the root when missing.")]
    [SerializeField] string carryBoneName = "body";
    [Tooltip("Where the lure is held: world units from the Viking's feet while he still faces left.")]
    [SerializeField] Vector3 carryOffset = new Vector3(-0.05f, 1.75f, 0f);
    [Tooltip("Delay before the grab when the animator has no Grab trigger to fire the animation event.")]
    [SerializeField, Min(0f)] float fallbackGrabDelay = 0.4f;

    EVVHealth health;
    EVVHitRecoil stunProfile;
    Animator animator;
    Vector3 targetPosition;
    EVVDefender attackTarget;
    bool hasTarget;
    bool hasAttackTarget;
    int lastHealth;
    float walkDirection = -1f;
    float fallbackAttackTimer;
    float afterKillTimer;
    EVVCharmLure charmTarget;
    EVVCharmLure resistedLure;
    bool hasCharmTarget;
    bool waitingForGrabEvent;
    bool hasGrabTrigger;
    float grabTimer;
    bool isRetreating;
    Vector3 retreatPosition;
    Transform carryPoint;

    public int LaneIndex { get; private set; }
    public EVVHealth Health => health;

    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0f, value);
    }

    void Awake()
    {
        health = GetComponent<EVVHealth>();
        stunProfile = GetComponent<EVVHitRecoil>();
        animator = GetComponent<Animator>();
        hasGrabTrigger = animator != null && HasAnimatorParameter(GrabTriggerName);
        // Created before any animation plays, so a carried lure's tilt is relative to the rest pose
        // and only sways with the walk cycle, whatever pose the grab happened in.
        GetCarryPoint();
    }

    void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<EVVHealth>();
        }

        if (health != null)
        {
            lastHealth = health.CurrentHealth;
            health.HealthChanged += OnHealthChanged;
        }

        EVVTargetRegistry.Add(this);
    }

    void OnDisable()
    {
        EVVTargetRegistry.Remove(this);
        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
        }
    }

    void Update()
    {
        if (!hasTarget || health == null || !health.IsAlive)
        {
            return;
        }

        if (afterKillTimer > 0f)
        {
            afterKillTimer -= Time.deltaTime;
            return;
        }

        if (isRetreating)
        {
            Retreat();
            return;
        }

        if (hasCharmTarget)
        {
            TickGrab();
            return;
        }

        if (hasAttackTarget)
        {
            if (attackTarget == null || attackTarget.Health == null || !attackTarget.Health.IsAlive)
            {
                ResumeWalking();
                return;
            }

            if (!useAttackAnimationEvents)
            {
                TickFallbackAttack();
            }

            return;
        }

        if (TryFindAttackTarget())
        {
            if (TryStartCharm(attackTarget))
            {
                return;
            }

            hasAttackTarget = true;
            fallbackAttackTimer = fallbackFirstAttackDelay;
            if (animator != null)
            {
                animator.SetTrigger(AttackTriggerName);
            }

            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        if ((transform.position - targetPosition).sqrMagnitude <= reachDistance * reachDistance)
        {
            EVVBoardLife.TryDamageActiveBoardLife(boardDamageOnExit);
            Destroy(gameObject);
        }
    }

    public void BeginLaneWalk(int laneIndex, Vector3 startPosition, Vector3 endPosition, float speed, int maxHealth)
    {
        LaneIndex = laneIndex;
        startPosition = EVVLaneDepth.WithLaneZ(startPosition, laneIndex);
        endPosition = EVVLaneDepth.WithLaneZ(endPosition, laneIndex);
        transform.position = startPosition;
        targetPosition = endPosition;
        moveSpeed = Mathf.Max(0f, speed);
        hasTarget = true;
        hasAttackTarget = false;
        attackTarget = null;
        fallbackAttackTimer = 0f;
        afterKillTimer = 0f;
        retreatPosition = startPosition;
        isRetreating = false;
        ClearCharmTarget();
        resistedLure = null;
        walkDirection = Mathf.Sign(endPosition.x - startPosition.x);
        if (Mathf.Approximately(walkDirection, 0f))
        {
            walkDirection = -1f;
        }

        if (health == null)
        {
            health = GetComponent<EVVHealth>();
        }

        health.SetMaxHealth(maxHealth);
        lastHealth = health.CurrentHealth;
        ApplyLaneDepth(laneIndex);
    }

    void OnHealthChanged(EVVHealth changedHealth, int currentHealth)
    {
        bool tookDamage = currentHealth < lastHealth;
        lastHealth = currentHealth;

        if (!tookDamage || currentHealth <= 0 || !hasAttackTarget)
        {
            return;
        }

        if (attackTarget == null || attackTarget.Health == null || !attackTarget.Health.IsAlive)
        {
            return;
        }

        hasAttackTarget = false;
        attackTarget = null;
        ResumeWalking();
    }

    bool TryFindAttackTarget()
    {
        IReadOnlyList<EVVDefender> characters = EVVTargetRegistry.Defenders;
        for (int i = 0; i < characters.Count; i++)
        {
            EVVDefender character = characters[i];
            if (character == null || !character.isActiveAndEnabled)
            {
                continue;
            }

            if (character.Health == null || !character.Health.IsAlive)
            {
                continue;
            }

            if (!IsInSameLaneDepth(character))
            {
                continue;
            }

            float forwardDistance = GetForwardDistanceTo(character);
            if (forwardDistance < -overlapTolerance || forwardDistance > attackStartDistance)
            {
                continue;
            }

            attackTarget = character;
            return true;
        }

        return false;
    }

    public void DealAttackDamage()
    {
        DealAttackDamage(1f);
    }

    void DealAttackDamage(float damageMultiplier)
    {
        if (attackTarget == null || attackTarget.Health == null || !attackTarget.Health.IsAlive)
        {
            ResumeWalking();
            return;
        }

        if (!IsInSameLaneDepth(attackTarget))
        {
            ResumeWalking();
            return;
        }

        int scaledDamage = Mathf.RoundToInt(attackDamage * damageMultiplier);
        attackTarget.Health.TakeDamage(scaledDamage, attackRecoilMultiplier);
        if (attackTarget.Health.IsAlive && stunProfile != null)
        {
            stunProfile.TryStunTarget(attackTarget.gameObject);
        }

        if (!attackTarget.Health.IsAlive)
        {
            HandleKilledAttackTarget();
        }
    }

    public void DealAttackDamageAnimationEvent()
    {
        DealAttackDamage();
    }

    public void DealFirstAttackDamageAnimationEvent()
    {
        DealAttackDamage(firstAttackDamageMultiplier);
    }

    void ResumeWalking()
    {
        attackTarget = null;
        hasAttackTarget = false;
        fallbackAttackTimer = 0f;
        afterKillTimer = 0f;
        ClearCharmTarget();
        if (animator != null)
        {
            animator.ResetTrigger(AttackTriggerName);
            PlayWalkState();
        }
    }

    // A lure in front of the Viking gets one charm roll per encounter. Charmed: he reaches for it
    // (Grab animation, or a plain delay without one) instead of attacking. Resisted: he attacks it
    // like any defender and never rolls for that lure again.
    bool TryStartCharm(EVVDefender target)
    {
        EVVCharmLure lure = target != null ? target.GetComponent<EVVCharmLure>() : null;
        if (lure == null || lure == resistedLure)
        {
            return false;
        }

        if (!lure.TryCharm(gameObject))
        {
            resistedLure = lure;
            return false;
        }

        attackTarget = null;
        charmTarget = lure;
        hasCharmTarget = true;
        grabTimer = fallbackGrabDelay;
        waitingForGrabEvent = hasGrabTrigger;
        if (hasGrabTrigger)
        {
            animator.ResetTrigger(AttackTriggerName);
            animator.SetTrigger(GrabTriggerName);
        }

        return true;
    }

    void TickGrab()
    {
        if (charmTarget == null || !charmTarget.isActiveAndEnabled)
        {
            ResumeWalking();
            return;
        }

        if (waitingForGrabEvent)
        {
            return;
        }

        grabTimer -= Time.deltaTime;
        if (grabTimer <= 0f)
        {
            CompleteGrab();
        }
    }

    public void GrabTargetAnimationEvent()
    {
        if (hasCharmTarget)
        {
            CompleteGrab();
        }
    }

    // Picks the lure up, turns around and heads back to the spawn point. The Viking stays a
    // living enemy on the way out, so defenders can still shoot him; if they do, the lure goes
    // down with him.
    void CompleteGrab()
    {
        if (charmTarget != null)
        {
            charmTarget.Grab(GetCarryPoint());
        }

        ClearCharmTarget();
        TurnAround();
        isRetreating = true;
    }

    void ClearCharmTarget()
    {
        charmTarget = null;
        hasCharmTarget = false;
        waitingForGrabEvent = false;
        grabTimer = 0f;
        if (hasGrabTrigger)
        {
            animator.ResetTrigger(GrabTriggerName);
        }
    }

    void Retreat()
    {
        transform.position = Vector3.MoveTowards(transform.position, retreatPosition, moveSpeed * Time.deltaTime);
        if ((transform.position - retreatPosition).sqrMagnitude <= reachDistance * reachDistance)
        {
            Destroy(gameObject);
        }
    }

    void TurnAround()
    {
        walkDirection = -walkDirection;
        Vector3 scale = transform.localScale;
        scale.x = -scale.x;
        transform.localScale = scale;
    }

    // World-aligned, world-sized point under the carry bone, so the lure bobs and leans with the
    // walk cycle instead of sliding rigidly with the root. Undoing the rig's scale here lets the
    // lure keep its board size and its pose offsets stay in world units.
    Transform GetCarryPoint()
    {
        if (carryPoint != null)
        {
            return carryPoint;
        }

        Transform bone = FindChildByName(transform, carryBoneName);
        carryPoint = new GameObject(CarryPointName).transform;
        carryPoint.SetParent(bone != null ? bone : transform, false);
        carryPoint.SetLossyScale(Vector3.one);
        carryPoint.position = transform.position + carryOffset;
        carryPoint.rotation = transform.rotation;
        return carryPoint;
    }

    static Transform FindChildByName(Transform root, string childName)
    {
        if (string.IsNullOrEmpty(childName))
        {
            return null;
        }

        foreach (Transform child in root)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    void HandleKilledAttackTarget()
    {
        attackTarget = null;
        hasAttackTarget = false;
        fallbackAttackTimer = 0f;

        if (animator != null && HasAnimatorParameter(AfterKillTriggerName))
        {
            animator.ResetTrigger(AttackTriggerName);
            animator.SetTrigger(AfterKillTriggerName);
            afterKillTimer = afterKillLockSeconds;
            return;
        }

        ResumeWalking();
    }

    void TickFallbackAttack()
    {
        fallbackAttackTimer -= Time.deltaTime;
        if (fallbackAttackTimer > 0f)
        {
            return;
        }

        DealAttackDamage();
        if (hasAttackTarget)
        {
            fallbackAttackTimer = fallbackAttackInterval;
        }
    }

    void PlayWalkState()
    {
        int walkingHash = Animator.StringToHash(WalkingStateName);
        if (animator.HasState(0, walkingHash))
        {
            animator.Play(walkingHash, 0);
            return;
        }

        int walkHash = Animator.StringToHash(WalkStateName);
        if (animator.HasState(0, walkHash))
        {
            animator.Play(walkHash, 0);
        }
    }

    bool HasAnimatorParameter(string parameterName)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    float GetForwardDistanceTo(EVVDefender character)
    {
        Bounds selfBounds = GetBounds(GetComponent<Collider2D>(), transform.position);
        Bounds targetBounds = GetBounds(character.GetComponent<Collider2D>(), character.transform.position);

        float selfFrontX = walkDirection < 0f ? selfBounds.min.x : selfBounds.max.x;
        float targetFrontX = walkDirection < 0f ? targetBounds.max.x : targetBounds.min.x;
        return (targetFrontX - selfFrontX) * walkDirection;
    }

    Bounds GetBounds(Collider2D collider, Vector3 fallbackPosition)
    {
        if (collider != null)
        {
            return collider.bounds;
        }

        return new Bounds(fallbackPosition, Vector3.one * 0.5f);
    }

    bool IsInSameLaneDepth(EVVDefender character)
    {
        if (character == null)
        {
            return false;
        }

        if (character.HasCell && character.Cell.y != LaneIndex)
        {
            return false;
        }

        return EVVLaneDepth.IsSameDepth(transform, character.transform, depthTolerance);
    }

    void ApplyLaneDepth(int laneIndex)
    {
        transform.position = EVVLaneDepth.WithLaneZ(transform.position, laneIndex);
        EVVLaneDepth.ApplyGameplaySortingGroup(gameObject);
    }
}
