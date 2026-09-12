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
    EVVHealth attackTarget;
    bool hasTarget;
    bool hasAttackTarget;
    int lastHealth;
    float walkDirection = -1f;
    float fallbackAttackTimer;
    float afterKillTimer;
    EVVCharmLure charmTarget;
    EVVCharmLure carriedLure;
    EVVCharmLure resistedLure;
    bool hasCharmTarget;
    bool waitingForGrabEvent;
    bool hasGrabTrigger;
    float grabTimer;
    bool isCarrying;
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
            health.Died += OnDied;
        }

        EVVTargetRegistry.Add(this);
    }

    void OnDisable()
    {
        EVVTargetRegistry.Remove(this);
        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
            health.Died -= OnDied;
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

        if (hasCharmTarget)
        {
            TickGrab();
            return;
        }

        if (hasAttackTarget)
        {
            if (attackTarget == null || !attackTarget.IsAlive)
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
            if (!isCarrying && TryStartCharm(attackTarget))
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

        Vector3 destination = isCarrying ? retreatPosition : targetPosition;
        transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
        if ((transform.position - destination).sqrMagnitude <= reachDistance * reachDistance)
        {
            if (!isCarrying)
            {
                EVVBoardLife.TryDamageActiveBoardLife(boardDamageOnExit);
            }

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
        isCarrying = false;
        carriedLure = null;
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

        if (attackTarget == null || !attackTarget.IsAlive)
        {
            return;
        }

        hasAttackTarget = false;
        attackTarget = null;
        ResumeWalking();
    }

    // The carrier's death is the lure's cue to run: it happens before the Viking is destroyed,
    // so the lure can leave his hierarchy in time.
    void OnDied(EVVHealth deadHealth)
    {
        if (carriedLure != null)
        {
            carriedLure.Escape(targetPosition);
            carriedLure = null;
        }
    }

    // A Viking attacks whatever blocks him in his lane: defenders and charmed Vikings on the way in,
    // the Vikings still coming for the lure on the way out with it.
    bool TryFindAttackTarget()
    {
        if (isCarrying)
        {
            return TryFindWalkerTarget(EVVTargetRegistry.Enemies);
        }

        return TryFindDefenderTarget() || TryFindWalkerTarget(EVVTargetRegistry.CharmedEnemies);
    }

    bool TryFindDefenderTarget()
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

            if (character.HasCell && character.Cell.y != LaneIndex)
            {
                continue;
            }

            if (!IsInReach(character.Health))
            {
                continue;
            }

            attackTarget = character.Health;
            return true;
        }

        return false;
    }

    bool TryFindWalkerTarget(IReadOnlyList<IEVVEnemyLaneWalker> walkers)
    {
        for (int i = 0; i < walkers.Count; i++)
        {
            IEVVEnemyLaneWalker walker = walkers[i];
            if (walker is not MonoBehaviour behaviour || behaviour == null || behaviour == this)
            {
                continue;
            }

            if (walker.Health == null || !walker.Health.IsAlive || walker.LaneIndex != LaneIndex)
            {
                continue;
            }

            if (!IsInReach(walker.Health))
            {
                continue;
            }

            attackTarget = walker.Health;
            return true;
        }

        return false;
    }

    bool IsInReach(EVVHealth target)
    {
        if (!IsSameDepth(target))
        {
            return false;
        }

        float forwardDistance = GetForwardDistanceTo(target);
        return forwardDistance >= -overlapTolerance && forwardDistance <= attackStartDistance;
    }

    public void DealAttackDamage()
    {
        DealAttackDamage(1f);
    }

    void DealAttackDamage(float damageMultiplier)
    {
        if (attackTarget == null || !attackTarget.IsAlive)
        {
            ResumeWalking();
            return;
        }

        if (!IsSameDepth(attackTarget))
        {
            ResumeWalking();
            return;
        }

        int scaledDamage = Mathf.RoundToInt(attackDamage * damageMultiplier);
        attackTarget.TakeDamage(scaledDamage, attackRecoilMultiplier);
        if (attackTarget.IsAlive && stunProfile != null)
        {
            stunProfile.TryStunTarget(attackTarget.gameObject);
        }

        if (!attackTarget.IsAlive)
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
    bool TryStartCharm(EVVHealth target)
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

    // Picks the lure up, turns around and heads back to the spawn point. From here on the Viking
    // is on the defenders' side: they stop shooting him, the other Vikings come for the lure and
    // he fights them on the way out.
    void CompleteGrab()
    {
        if (charmTarget == null)
        {
            ResumeWalking();
            return;
        }

        carriedLure = charmTarget;
        carriedLure.Grab(GetCarryPoint());
        ClearCharmTarget();
        TurnAround();
        isCarrying = true;
        EVVTargetRegistry.MoveToCharmed(this);
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

    float GetForwardDistanceTo(EVVHealth target)
    {
        Bounds selfBounds = GetBounds(GetComponent<Collider2D>(), transform.position);
        Bounds targetBounds = GetBounds(target.GetComponent<Collider2D>(), target.transform.position);

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

    bool IsSameDepth(EVVHealth target)
    {
        return target != null && EVVLaneDepth.IsSameDepth(transform, target.transform, depthTolerance);
    }

    void ApplyLaneDepth(int laneIndex)
    {
        transform.position = EVVLaneDepth.WithLaneZ(transform.position, laneIndex);
        EVVLaneDepth.ApplyGameplaySortingGroup(gameObject);
    }
}
