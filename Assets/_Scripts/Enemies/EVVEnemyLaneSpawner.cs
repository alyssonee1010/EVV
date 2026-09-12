using System.Collections.Generic;
using UnityEngine;

public class EVVEnemyLaneSpawner : MonoBehaviour
{
    enum WalkDirection
    {
        RightToLeft,
        LeftToRight
    }

    [System.Serializable]
    class EnemySpawnOption
    {
        public MonoBehaviour prefab = null;
        [Min(1)] public int maxHealth = 100;
        [Min(0f)] public float moveSpeed = 0.75f;
        [Min(0f)] public float weight = 1f;
    }

    struct Lane
    {
        public int Index;
        public Vector3 Start;
        public Vector3 End;
    }

    [Header("Board")]
    [SerializeField] EVVBoardGrid boardGrid;
    [SerializeField] WalkDirection walkDirection = WalkDirection.RightToLeft;
    [SerializeField] float edgePadding = 0f;
    [SerializeField] float laneYOffset = 0f;
    [SerializeField] float spawnOutsideBoardTiles = 2f;
    [SerializeField] float exitPastBoardTiles = 0f;

    [Header("Spawning")]
    [SerializeField] EnemySpawnOption[] enemies;
    [SerializeField] bool spawnOnStart;
    [SerializeField] float initialDelay = 22f;
    [SerializeField] float spawnInterval = 11f;
    [SerializeField] int maxAliveEnemies = 4;

    [Header("Difficulty Ramp")]
    [SerializeField] bool rampDifficultyOverTime = true;
    [SerializeField, Min(1f)] float timeToMaxDifficulty = 330f;
    [SerializeField, Min(0.05f)] float minimumSpawnInterval = 3.5f;
    [SerializeField, Min(0.1f)] float difficultyCurveExponent = 2.1f;
    [SerializeField, Min(1)] int maxAliveEnemiesAtFullDifficulty = 14;

    readonly List<Lane> lanes = new List<Lane>();
    readonly List<MonoBehaviour> aliveEnemies = new List<MonoBehaviour>();
    float spawnTimer;
    float elapsedSpawnTime;

    void Awake()
    {
        if (boardGrid == null)
        {
            boardGrid = FindAnyObjectByType<EVVBoardGrid>();
        }

        RebuildLanes();
        spawnTimer = Mathf.Max(0f, initialDelay);
    }

    void Start()
    {
        if (spawnOnStart)
        {
            SpawnEnemy();
            spawnTimer = GetCurrentSpawnInterval();
        }
    }

    void Update()
    {
        CleanupAliveList();
        elapsedSpawnTime += Time.deltaTime;

        if (lanes.Count == 0 || enemies == null || enemies.Length == 0 || GetCurrentSpawnInterval() <= 0f)
        {
            return;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f)
        {
            return;
        }

        SpawnEnemy();
        spawnTimer = GetCurrentSpawnInterval();
    }

    public void RebuildLanes()
    {
        lanes.Clear();

        if (boardGrid != null)
        {
            BuildBoardLanes();
        }
    }

    public void SpawnEnemy()
    {
        CleanupAliveList();
        if (aliveEnemies.Count >= GetCurrentMaxAliveEnemies() || lanes.Count == 0)
        {
            return;
        }

        EnemySpawnOption option = PickEnemy();
        if (option == null || option.prefab == null)
        {
            return;
        }

        Lane lane = lanes[Random.Range(0, lanes.Count)];
        MonoBehaviour instance = Instantiate(option.prefab, lane.Start, Quaternion.identity, transform);
        if (!TryGetLaneWalker(instance, out IEVVEnemyLaneWalker enemy))
        {
            Debug.LogWarning($"{nameof(EVVEnemyLaneSpawner)} could not spawn '{instance.name}' because it has no lane walker script.");
            Destroy(instance.gameObject);
            return;
        }

        enemy.BeginLaneWalk(lane.Index, lane.Start, lane.End, option.moveSpeed, option.maxHealth);
        aliveEnemies.Add(instance);
    }

    void BuildBoardLanes()
    {
        for (int row = 0; row < boardGrid.Rows; row++)
        {
            Vector3 first = boardGrid.GetCellCenterWorld(row, 0);
            Vector3 last = boardGrid.GetCellCenterWorld(row, boardGrid.Columns - 1);
            AddLane(row, first, last);
        }
    }

    void AddLane(int laneIndex, Vector3 leftTile, Vector3 rightTile)
    {
        float tileWidth = Mathf.Max(0.01f, Mathf.Abs(rightTile.x - leftTile.x) / Mathf.Max(1, GetColumnSpan()));
        float spawnOffset = edgePadding + tileWidth * spawnOutsideBoardTiles;
        float exitOffset = edgePadding + tileWidth * exitPastBoardTiles;

        Vector3 leftSpawn = EVVLaneDepth.WithLaneZ(new Vector3(leftTile.x - spawnOffset, leftTile.y + laneYOffset, leftTile.z), laneIndex);
        Vector3 rightSpawn = EVVLaneDepth.WithLaneZ(new Vector3(rightTile.x + spawnOffset, rightTile.y + laneYOffset, rightTile.z), laneIndex);
        Vector3 leftExit = EVVLaneDepth.WithLaneZ(new Vector3(leftTile.x - exitOffset, leftTile.y + laneYOffset, leftTile.z), laneIndex);
        Vector3 rightExit = EVVLaneDepth.WithLaneZ(new Vector3(rightTile.x + exitOffset, rightTile.y + laneYOffset, rightTile.z), laneIndex);

        lanes.Add(new Lane
        {
            Index = laneIndex,
            Start = walkDirection == WalkDirection.RightToLeft ? rightSpawn : leftSpawn,
            End = walkDirection == WalkDirection.RightToLeft ? leftExit : rightExit
        });
    }

    int GetColumnSpan()
    {
        if (boardGrid != null)
        {
            return Mathf.Max(1, boardGrid.Columns - 1);
        }

        return 1;
    }

    EnemySpawnOption PickEnemy()
    {
        float totalWeight = 0f;
        foreach (EnemySpawnOption option in enemies)
        {
            if (option != null && IsValidLaneWalkerPrefab(option.prefab))
            {
                totalWeight += Mathf.Max(0f, option.weight);
            }
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float pick = Random.Range(0f, totalWeight);
        foreach (EnemySpawnOption option in enemies)
        {
            if (option == null || !IsValidLaneWalkerPrefab(option.prefab))
            {
                continue;
            }

            pick -= Mathf.Max(0f, option.weight);
            if (pick <= 0f)
            {
                return option;
            }
        }

        return null;
    }

    void CleanupAliveList()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (aliveEnemies[i] == null)
            {
                aliveEnemies.RemoveAt(i);
            }
        }
    }

    bool IsValidLaneWalkerPrefab(MonoBehaviour prefab)
    {
        return prefab != null && TryGetLaneWalker(prefab, out _);
    }

    bool TryGetLaneWalker(MonoBehaviour source, out IEVVEnemyLaneWalker laneWalker)
    {
        laneWalker = source as IEVVEnemyLaneWalker;
        if (laneWalker != null)
        {
            return true;
        }

        laneWalker = source.GetComponent<IEVVEnemyLaneWalker>();
        return laneWalker != null;
    }

    float GetCurrentDifficulty()
    {
        if (!rampDifficultyOverTime)
        {
            return 0f;
        }

        float normalizedTime = Mathf.Clamp01(elapsedSpawnTime / Mathf.Max(1f, timeToMaxDifficulty));
        return Mathf.Pow(normalizedTime, Mathf.Max(0.1f, difficultyCurveExponent));
    }

    float GetCurrentSpawnInterval()
    {
        float startInterval = Mathf.Max(0.05f, spawnInterval);
        float targetInterval = Mathf.Min(startInterval, Mathf.Max(0.05f, minimumSpawnInterval));
        return Mathf.Lerp(startInterval, targetInterval, GetCurrentDifficulty());
    }

    int GetCurrentMaxAliveEnemies()
    {
        int startMax = Mathf.Max(1, maxAliveEnemies);
        int targetMax = Mathf.Max(startMax, maxAliveEnemiesAtFullDifficulty);
        return Mathf.RoundToInt(Mathf.Lerp(startMax, targetMax, GetCurrentDifficulty()));
    }
}
