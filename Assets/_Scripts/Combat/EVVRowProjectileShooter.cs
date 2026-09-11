using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(EVVDefender))]
public class EVVRowProjectileShooter : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] GameObject projectilePrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] Vector2 firePointOffset = new Vector2(0.65f, 0.25f);
    [SerializeField] Vector2 projectileDirection = Vector2.right;
    [SerializeField] float fireInterval = 1.25f;
    [Tooltip("Damage assigned to each projectile spawned by this character.")]
    [Min(0)]
    [SerializeField] int projectileDamage = 25;
    [Tooltip("How far a hit enemy gets knocked back. 0 means no knockback at all.")]
    [Min(0f)]
    [SerializeField] float recoilMultiplier = 0f;
    [SerializeField] float projectileSpeed = 6f;
    [SerializeField] float projectileLifetime = 5f;
    [SerializeField] bool destroyProjectileOnHit = true;
    [Tooltip("If true, a hit is only checked against the enemy's horizontal position along the row (ignoring its exact travel path). Turn off for projectiles that need precise path-based hit detection.")]
    [SerializeField] bool useLaneBasedHit = true;
    [Tooltip("Extra margin added around an enemy's collider bounds when checking whether this projectile hit it.")]
    [Min(0f)]
    [SerializeField] float hitPadding = 0.6f;

    [Header("Pooling")]
    [SerializeField] bool useProjectilePool = true;
    [SerializeField] int preloadProjectileCount = 6;
    [SerializeField] int maxPooledProjectiles = 24;

    [Header("Targeting")]
    [FormerlySerializedAs("detectionRange")]
    [Tooltip("How far this character can see and start shooting along its row.")]
    [Min(0f)]
    [SerializeField] float sightRange = 12f;
    [Tooltip("How close an enemy can be before this character stops considering it a valid shooting target.")]
    [Min(0f)]
    [SerializeField] float minimumFireDistance = 0.2f;
    [Tooltip("Only used before the character is placed on a board cell.")]
    [Min(0f)]
    [FormerlySerializedAs("laneTolerance")]
    [SerializeField] float depthTolerance = EVVLaneDepth.DefaultDepthTolerance;

    [Header("Animation")]
    [SerializeField] bool playShootAnimation = true;
    [SerializeField] bool waitForShootAnimationEvent;
    [SerializeField] string shootTriggerName = "Attack";

    EVVDefender boardCharacter;
    EVVHitRecoil stunProfile;
    Animator animator;
    IEVVEnemyLaneWalker currentTarget;
    readonly List<EVVDamageProjectile> projectilePool = new List<EVVDamageProjectile>();
    float fireTimer;
    bool waitingForShootAnimationEvent;

    void Awake()
    {
        boardCharacter = GetComponent<EVVDefender>();
        stunProfile = GetComponent<EVVHitRecoil>();
        animator = GetComponent<Animator>();
        PreloadProjectiles();
    }

    void Update()
    {
        fireTimer -= Time.deltaTime * EVVActionSpeedModifier.GetMultiplier(this);

        if (projectilePrefab == null || !TryFindTarget(out IEVVEnemyLaneWalker target))
        {
            currentTarget = null;
            waitingForShootAnimationEvent = false;
            ResetShootAnimationTrigger();
            return;
        }

        currentTarget = target;
        if (fireTimer > 0f || waitingForShootAnimationEvent)
        {
            return;
        }

        fireTimer = Mathf.Max(0.01f, fireInterval);
        bool triggeredAnimation = PlayShootAnimation();
        if (waitForShootAnimationEvent && triggeredAnimation)
        {
            waitingForShootAnimationEvent = true;
            return;
        }

        ShootProjectile();
    }

    public void ShootProjectile()
    {
        waitingForShootAnimationEvent = false;
        if (projectilePrefab == null)
        {
            return;
        }

        int laneIndex = GetLaneIndex();
        Vector3 spawnPosition = firePoint != null
            ? firePoint.position
            : transform.TransformPoint(firePointOffset);
        spawnPosition = EVVLaneDepth.WithLaneZ(spawnPosition, laneIndex);

        EVVDamageProjectile projectile = GetProjectile();
        if (projectile == null)
        {
            return;
        }

        GameObject projectileObject = projectile.gameObject;
        projectile.Configure(projectileDamage, recoilMultiplier, projectileSpeed, projectileLifetime,
            destroyProjectileOnHit, useLaneBasedHit, hitPadding, depthTolerance);
        projectile.SetStunSource(stunProfile);
        projectileObject.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
        EVVLaneDepth.ApplyGameplaySortingGroup(projectileObject);

        projectileObject.SetActive(true);
        projectile.Launch(projectileDirection, laneIndex);
    }

    public void ShootProjectileAnimationEvent()
    {
        ShootProjectile();
    }

    bool TryFindTarget(out IEVVEnemyLaneWalker bestTarget)
    {
        bestTarget = null;
        float bestForwardDistance = float.PositiveInfinity;
        Vector2 forward = projectileDirection.sqrMagnitude > 0f ? projectileDirection.normalized : Vector2.right;
        IReadOnlyList<IEVVEnemyLaneWalker> enemies = EVVTargetRegistry.Enemies;

        for (int i = 0; i < enemies.Count; i++)
        {
            IEVVEnemyLaneWalker enemy = enemies[i];
            if (!TryGetEnemyObject(enemy, out GameObject enemyObject))
            {
                continue;
            }

            if (enemy.Health == null || !enemy.Health.IsAlive)
            {
                continue;
            }

            if (!IsInSameLane(enemy))
            {
                continue;
            }

            Vector2 toEnemy = enemyObject.transform.position - transform.position;
            float forwardDistance = Vector2.Dot(toEnemy, forward);
            if (forwardDistance < minimumFireDistance || forwardDistance > sightRange)
            {
                continue;
            }

            if (forwardDistance < bestForwardDistance)
            {
                bestForwardDistance = forwardDistance;
                bestTarget = enemy;
            }
        }

        return bestTarget != null;
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

    int GetLaneIndex()
    {
        if (boardCharacter != null && boardCharacter.HasCell)
        {
            return boardCharacter.Cell.y;
        }

        return currentTarget != null ? currentTarget.LaneIndex : 0;
    }

    void PreloadProjectiles()
    {
        if (!useProjectilePool || projectilePrefab == null)
        {
            return;
        }

        int count = Mathf.Clamp(preloadProjectileCount, 0, Mathf.Max(0, maxPooledProjectiles));
        for (int i = projectilePool.Count; i < count; i++)
        {
            CreatePooledProjectile();
        }
    }

    EVVDamageProjectile GetProjectile()
    {
        if (!useProjectilePool)
        {
            return CreateProjectile(false);
        }

        for (int i = 0; i < projectilePool.Count; i++)
        {
            EVVDamageProjectile pooledProjectile = projectilePool[i];
            if (pooledProjectile != null && !pooledProjectile.gameObject.activeSelf)
            {
                return pooledProjectile;
            }
        }

        if (projectilePool.Count >= maxPooledProjectiles)
        {
            return null;
        }

        return CreatePooledProjectile();
    }

    EVVDamageProjectile CreatePooledProjectile()
    {
        EVVDamageProjectile projectile = CreateProjectile(true);
        if (projectile != null)
        {
            projectilePool.Add(projectile);
            projectile.gameObject.SetActive(false);
        }

        return projectile;
    }

    EVVDamageProjectile CreateProjectile(bool pooled)
    {
        GameObject projectileObject = Instantiate(projectilePrefab);
        EVVDamageProjectile projectile = projectileObject.GetComponent<EVVDamageProjectile>();
        if (projectile == null)
        {
            projectile = projectileObject.AddComponent<EVVDamageProjectile>();
        }

        projectile.SetReturnToPool(pooled);
        return projectile;
    }

    bool PlayShootAnimation()
    {
        if (!playShootAnimation || animator == null || string.IsNullOrWhiteSpace(shootTriggerName))
        {
            return false;
        }

        animator.ResetTrigger(shootTriggerName);
        animator.SetTrigger(shootTriggerName);
        return true;
    }

    void ResetShootAnimationTrigger()
    {
        if (animator != null && !string.IsNullOrWhiteSpace(shootTriggerName))
        {
            animator.ResetTrigger(shootTriggerName);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 start = firePoint != null
            ? firePoint.position
            : transform.TransformPoint(firePointOffset);
        Vector3 direction = projectileDirection.sqrMagnitude > 0f
            ? (Vector3)projectileDirection.normalized
            : Vector3.right;

        Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.9f);
        Gizmos.DrawLine(start, start + direction * sightRange);
        Gizmos.DrawWireSphere(start + direction * sightRange, 0.08f);

        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.65f);
        Gizmos.DrawWireSphere(start + direction * minimumFireDistance, 0.06f);
    }
}
