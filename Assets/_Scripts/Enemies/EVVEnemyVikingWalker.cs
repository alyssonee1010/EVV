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
    [Tooltip("Raises this Viking above the lane line, for a rig whose feet sit lower than the others'.")]
    [SerializeField] float laneHeightOffset = 0f;

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
    [Tooltip("Longest a BeginDodge animation event keeps melee off him, in case the clip is cut before EndDodge.")]
    [SerializeField, Min(0f)] float dodgeMaxSeconds = 0.8f;

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
    EVVDefender attackTargetDefender;
    bool hasTarget;
    bool hasAttackTarget;
    float walkDirection = -1f;
    float fallbackAttackTimer;
    float afterKillTimer;
    float dodgeTimer;
    float pauseTimer;
    EVVCharmLure charmTarget;
    EVVCharmLure carriedLure;
    EVVCharmLure resistedLure;
    bool hasCharmTarget;
    bool isClaimant;
    bool waitingForGrabEvent;
    bool hasGrabTrigger;
    float grabTimer;
    bool isCarrying;
    Vector3 retreatPosition;
    Transform carryPoint;

    public int LaneIndex { get; private set; }
    public EVVHealth Health => health;
    public bool IsDodgingMelee => dodgeTimer > 0f;

    // Held still by something outside, like a jump over a hole that moves him itself.
    public void PauseWalk(float seconds)
    {
        pauseTimer = Mathf.Max(pauseTimer, seconds);
    }

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
            health.Died += OnDied;
        }

        if (stunProfile != null)
        {
            stunProfile.Stunned += OnStunned;
        }

        EVVTargetRegistry.Add(this);
    }

    void OnDisable()
    {
        EVVTargetRegistry.Remove(this);
        if (health != null)
        {
            health.Died -= OnDied;
        }

        if (stunProfile != null)
        {
            stunProfile.Stunned -= OnStunned;
        }
    }

    void Update()
    {
        if (dodgeTimer > 0f)
        {
            dodgeTimer -= Time.deltaTime;
        }

        if (!hasTarget || health == null || !health.IsAlive)
        {
            return;
        }

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
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
            if (!IsAttackTargetValid())
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
        startPosition.y += laneHeightOffset;
        endPosition.y += laneHeightOffset;
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
        ApplyLaneDepth(laneIndex);
    }

    // Being hit does not interrupt an attack; a stun does. The Stun animation is already playing
    // (AnyState -> stun -> walk), so the attack is only forgotten here and the next Update finds the
    // target again and fires Attack, which the animator picks up once the stun is over.
    void OnStunned()
    {
        if (!hasAttackTarget || !health.IsAlive)
        {
            return;
        }

        hasAttackTarget = false;
        attackTarget = null;
        attackTargetDefender = null;
        fallbackAttackTimer = 0f;
        afterKillTimer = 0f;
        if (animator != null)
        {
            animator.ResetTrigger(AttackTriggerName);
        }
    }

    // A defender that stops being targetable mid-fight (a trap hole that just got finished) is
    // dropped like a dead one: the Viking walks on.
    bool IsAttackTargetValid()
    {
        return attackTarget != null
            && attackTarget.IsAlive
            && (attackTargetDefender == null || (attackTargetDefender.isActiveAndEnabled && attackTargetDefender.IsTargetable));
    }

    void OnDied(EVVHealth deadHealth)
    {
        LetGo();
    }

    // Out of the fight for good, dead or fallen into a hole: the lure he carries runs off, the one
    // he is after is released to the others. Happens before the Viking is destroyed, so the lure can
    // leave his hierarchy in time.
    public void LetGo()
    {
        if (hasCharmTarget && charmTarget != null)
        {
            charmTarget.Release(gameObject);
        }

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
            if (character == null || !character.isActiveAndEnabled || !character.IsTargetable)
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

            if (!IsInReach(character.Health, allowOverlap: false))
            {
                continue;
            }

            attackTarget = character.Health;
            attackTargetDefender = character;
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

            if (!IsInReach(walker.Health, allowOverlap: true))
            {
                continue;
            }

            attackTarget = walker.Health;
            attackTargetDefender = null;
            return true;
        }

        return false;
    }

    // Vikings fighting over the lure often stand on top of each other (the loser of the claim
    // waits right where the carrier turns around), so a rival counts as in front until he is
    // fully behind this Viking; defenders only within the usual overlap tolerance.
    bool IsInReach(EVVHealth target, bool allowOverlap)
    {
        if (!IsSameDepth(target))
        {
            return false;
        }

        float forwardDistance = GetForwardDistanceTo(target, out float bodiesWidth);
        float minDistance = allowOverlap ? -bodiesWidth : -overlapTolerance;
        return forwardDistance >= minDistance && forwardDistance <= attackStartDistance;
    }

    public void DealAttackDamage()
    {
        DealAttackDamage(1f);
    }

    void DealAttackDamage(float damageMultiplier)
    {
        if (!IsAttackTargetValid())
        {
            ResumeWalking();
            return;
        }

        // A defender stays put, a rival may have walked off: only swing at one still in reach.
        if (!IsSameDepth(attackTarget) || (attackTargetDefender == null && !IsInReach(attackTarget, allowOverlap: true)))
        {
            ResumeWalking();
            return;
        }

        // A rival in the middle of his jump: the swing misses, the fight goes on.
        if (attackTargetDefender == null && attackTarget.GetComponent<IEVVEnemyLaneWalker>() is IEVVEnemyLaneWalker rival && rival.IsDodgingMelee)
        {
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

    // Animation events on a clip where the Viking leaves the ground (the Rap Viking's jump kick):
    // between them melee attackers cannot reach him; projectiles are unaffected.
    public void BeginDodgeAnimationEvent()
    {
        dodgeTimer = dodgeMaxSeconds;
    }

    public void EndDodgeAnimationEvent()
    {
        dodgeTimer = 0f;
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
    // like any defender and never rolls for that lure again. Only the first charmed Viking holds
    // the claim; a later one reaches too but waits, and gets the claim if the first one falls.
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
        attackTargetDefender = null;
        charmTarget = lure;
        hasCharmTarget = true;
        if (lure.TryClaim(gameObject))
        {
            TakeClaim();
        }

        StartGrab();
        return true;
    }

    // Holding the claim already puts the Viking on the defenders' side: from the heart on, neither
    // the archers nor the lure herself hit the one reaching for her.
    void TakeClaim()
    {
        isClaimant = true;
        EVVTargetRegistry.SetCharmed(this, true);
    }

    void StartGrab()
    {
        grabTimer = fallbackGrabDelay;
        waitingForGrabEvent = hasGrabTrigger;
        if (hasGrabTrigger)
        {
            animator.ResetTrigger(AttackTriggerName);
            animator.SetTrigger(GrabTriggerName);
        }
    }

    void TickGrab()
    {
        // Gone, or carried off by the Viking who held the claim: nothing left to wait for. The
        // carrier is now a rival in front of this one.
        if (charmTarget == null || !charmTarget.isActiveAndEnabled || charmTarget.IsCarried)
        {
            ResumeWalking();
            return;
        }

        if (!isClaimant)
        {
            if (charmTarget.TryClaim(gameObject))
            {
                TakeClaim();
                StartGrab();
            }

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
        if (hasCharmTarget && isClaimant)
        {
            CompleteGrab();
        }
    }

    // Picks the lure up, turns around and heads back to the spawn point, fighting the other
    // Vikings that come for her on the way out.
    void CompleteGrab()
    {
        if (charmTarget == null || !charmTarget.Grab(gameObject, GetCarryPoint()))
        {
            ResumeWalking();
            return;
        }

        carriedLure = charmTarget;
        isCarrying = true;
        ClearCharmTarget();
        TurnAround();
    }

    void ClearCharmTarget()
    {
        if (isClaimant && !isCarrying)
        {
            EVVTargetRegistry.SetCharmed(this, false);
        }

        charmTarget = null;
        hasCharmTarget = false;
        isClaimant = false;
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

    float GetForwardDistanceTo(EVVHealth target, out float bodiesWidth)
    {
        Bounds selfBounds = GetBounds(GetComponent<Collider2D>(), transform.position);
        Bounds targetBounds = GetBounds(target.GetComponent<Collider2D>(), target.transform.position);
        bodiesWidth = selfBounds.size.x + targetBounds.size.x;

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
