using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class EVVWaveDirector : MonoBehaviour
{
    enum WalkDirection
    {
        RightToLeft,
        LeftToRight
    }

    [System.Serializable]
    class UnitOption
    {
        public string unitId = "";
        public MonoBehaviour prefab;
        public int maxHealth = 100;
        public float moveSpeed = 0.9f;
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
    [SerializeField] float laneYOffset = -0.7f;
    [SerializeField] float spawnOutsideBoardTiles = 2f;
    [SerializeField] float exitPastBoardTiles = 0f;

    [Header("Units")]
    [SerializeField] UnitOption[] units;

    [Header("Wave Banner")]
    [SerializeField] string flagWaveMessage = "ELITE WAVE INCOMING";
    [SerializeField] string finalWaveMessage = "FINAL WAVE";
    [SerializeField] string levelCompleteMessage = "LEVEL COMPLETE";
    [SerializeField] Vector3 bannerWorldOffset = new Vector3(0f, 4.2f, 0f);
    [SerializeField] float bannerDuration = 3f;

    [Header("Level Completion")]
    [SerializeField] float boardClearCheckInterval = 1f;

    readonly List<Lane> lanes = new List<Lane>();
    readonly List<MonoBehaviour> aliveEnemies = new List<MonoBehaviour>();
    Coroutine runningLevelRoutine;
    TextMeshPro bannerText;

    public EVVLevelDefinition CurrentLevel { get; private set; }
    public float LevelStartTime { get; private set; }
    public bool IsRunning
    {
        get { return runningLevelRoutine != null; }
    }

    public event System.Action<EVVLevelDefinition> LevelStarted;
    public event System.Action<EVVLevelDefinition> LevelCompleted;

    void Awake()
    {
        if (boardGrid == null)
        {
            boardGrid = FindAnyObjectByType<EVVBoardGrid>();
        }

        RebuildLanes();
    }

    public void RebuildLanes()
    {
        lanes.Clear();

        if (boardGrid == null)
        {
            return;
        }

        for (int row = 0; row < boardGrid.Rows; row++)
        {
            Vector3 first = boardGrid.GetCellCenterWorld(row, 0);
            Vector3 last = boardGrid.GetCellCenterWorld(row, boardGrid.Columns - 1);
            AddLane(row, first, last, Mathf.Max(1, boardGrid.Columns - 1));
        }
    }

    void AddLane(int laneIndex, Vector3 leftTile, Vector3 rightTile, int columnSpan)
    {
        float tileWidth = Mathf.Max(0.01f, Mathf.Abs(rightTile.x - leftTile.x) / columnSpan);
        float spawnOffset = edgePadding + tileWidth * spawnOutsideBoardTiles;
        float exitOffset = edgePadding + tileWidth * exitPastBoardTiles;

        Vector3 leftSpawn = EVVLaneDepth.WithLaneZ(new Vector3(leftTile.x - spawnOffset, leftTile.y + laneYOffset, leftTile.z), laneIndex);
        Vector3 rightSpawn = EVVLaneDepth.WithLaneZ(new Vector3(rightTile.x + spawnOffset, rightTile.y + laneYOffset, rightTile.z), laneIndex);
        Vector3 leftExit = EVVLaneDepth.WithLaneZ(new Vector3(leftTile.x - exitOffset, leftTile.y + laneYOffset, leftTile.z), laneIndex);
        Vector3 rightExit = EVVLaneDepth.WithLaneZ(new Vector3(rightTile.x + exitOffset, rightTile.y + laneYOffset, rightTile.z), laneIndex);

        Lane lane;
        lane.Index = laneIndex;
        lane.Start = walkDirection == WalkDirection.RightToLeft ? rightSpawn : leftSpawn;
        lane.End = walkDirection == WalkDirection.RightToLeft ? leftExit : rightExit;
        lanes.Add(lane);
    }

    public void StartLevel(EVVLevelDefinition level)
    {
        if (level == null)
        {
            return;
        }

        StopLevel();
        CurrentLevel = level;

        RebuildLanes();
        aliveEnemies.Clear();

        EVVUsableWallet wallet = EVVUsableWallet.Instance != null ? EVVUsableWallet.Instance : FindAnyObjectByType<EVVUsableWallet>();
        if (wallet != null)
        {
            wallet.SetDiamonds(level.StartingCurrency);
            wallet.SetHealingPotions(0);
            wallet.SetSpeedPotions(0);
        }

        LevelStartTime = Time.time;
        LevelStarted?.Invoke(level);
        runningLevelRoutine = StartCoroutine(RunLevel(level));
    }

    public void StopLevel()
    {
        if (runningLevelRoutine != null)
        {
            StopCoroutine(runningLevelRoutine);
            runningLevelRoutine = null;
        }
    }

    IEnumerator RunLevel(EVVLevelDefinition level)
    {
        foreach (EVVWaveDefinition wave in level.Waves)
        {
            float waitUntil = LevelStartTime + wave.Time;
            while (Time.time < waitUntil)
            {
                yield return null;
            }

            if (wave.Type == EVVWaveType.Flag)
            {
                ShowBanner(flagWaveMessage);
            }
            else if (wave.Type == EVVWaveType.Final)
            {
                ShowBanner(finalWaveMessage);
            }

            yield return StartCoroutine(RunWave(wave));
        }

        yield return StartCoroutine(WaitForBoardClear());

        yield return new WaitForSeconds(5);

        ShowBanner(levelCompleteMessage);
        runningLevelRoutine = null;
        LevelCompleted?.Invoke(level);
    }

    IEnumerator WaitForBoardClear()
    {
        while (true)
        {
            CleanupAliveList();
            if (aliveEnemies.Count == 0)
            {
                yield break;
            }

            yield return new WaitForSeconds(boardClearCheckInterval);
        }
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

    IEnumerator RunWave(EVVWaveDefinition wave)
    {
        List<Coroutine> spawnRoutines = new List<Coroutine>();
        foreach (EVVSpawnDefinition spawn in wave.Spawns)
        {
            spawnRoutines.Add(StartCoroutine(RunSpawn(spawn)));
        }

        foreach (Coroutine routine in spawnRoutines)
        {
            yield return routine;
        }
    }

    IEnumerator RunSpawn(EVVSpawnDefinition spawn)
    {
        if (spawn.Count <= 0)
        {
            yield break;
        }

        if (spawn.TimeOffset > 0f)
        {
            yield return new WaitForSeconds(spawn.TimeOffset);
        }

        float[] spawnOffsets = BuildSpawnOffsets(spawn);
        float elapsed = 0f;
        for (int i = 0; i < spawnOffsets.Length; i++)
        {
            float waitSeconds = spawnOffsets[i] - elapsed;
            if (waitSeconds > 0f)
            {
                yield return new WaitForSeconds(waitSeconds);
                elapsed += waitSeconds;
            }

            SpawnUnit(spawn.Unit, spawn.Lane);
        }
    }

    // Offsets are relative to the spawn's own start (after TimeOffset has already elapsed).
    float[] BuildSpawnOffsets(EVVSpawnDefinition spawn)
    {
        float[] offsets = new float[spawn.Count];
        if (spawn.Count == 1)
        {
            offsets[0] = 0f;
            return offsets;
        }

        float duration = spawn.Duration > 0f ? spawn.Duration : (spawn.Count - 1) * Mathf.Max(0.01f, spawn.Interval);

        if (spawn.Spacing == EVVSpawnSpacing.Random)
        {
            offsets[0] = 0f;
            for (int i = 1; i < spawn.Count; i++)
            {
                offsets[i] = Random.Range(0f, duration);
            }

            System.Array.Sort(offsets);
            return offsets;
        }

        for (int i = 0; i < spawn.Count; i++)
        {
            offsets[i] = duration * i / (spawn.Count - 1);
        }

        return offsets;
    }

    void SpawnUnit(string unitId, string laneSpec)
    {
        if (lanes.Count == 0)
        {
            return;
        }

        UnitOption option = FindUnit(unitId);
        if (option == null || option.prefab == null)
        {
            Debug.LogWarning($"{nameof(EVVWaveDirector)}: no prefab configured for unit '{unitId}'.");
            return;
        }

        Lane lane = PickLane(laneSpec);
        MonoBehaviour instance = Instantiate(option.prefab, lane.Start, Quaternion.identity, transform);

        IEVVEnemyLaneWalker enemy;
        if (!TryGetLaneWalker(instance, out enemy))
        {
            Debug.LogWarning($"{nameof(EVVWaveDirector)}: unit '{unitId}' prefab has no lane walker script.");
            Destroy(instance.gameObject);
            return;
        }

        enemy.BeginLaneWalk(lane.Index, lane.Start, lane.End, option.moveSpeed, option.maxHealth);
        aliveEnemies.Add(instance);
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

    Lane PickLane(string laneSpec)
    {
        int laneIndex;
        bool isRandom = string.IsNullOrEmpty(laneSpec) || laneSpec.Equals("random", System.StringComparison.OrdinalIgnoreCase);
        if (!isRandom && int.TryParse(laneSpec, out laneIndex))
        {
            for (int i = 0; i < lanes.Count; i++)
            {
                if (lanes[i].Index == laneIndex)
                {
                    return lanes[i];
                }
            }
        }

        return lanes[Random.Range(0, lanes.Count)];
    }

    UnitOption FindUnit(string unitId)
    {
        if (units == null)
        {
            return null;
        }

        foreach (UnitOption option in units)
        {
            if (option != null && option.unitId == unitId)
            {
                return option;
            }
        }

        return null;
    }

    void ShowBanner(string message)
    {
        EnsureBannerText();
        bannerText.text = message;
        bannerText.gameObject.SetActive(true);
        CancelInvoke(nameof(HideBanner));
        Invoke(nameof(HideBanner), bannerDuration);
    }

    void HideBanner()
    {
        if (bannerText != null)
        {
            bannerText.gameObject.SetActive(false);
        }
    }

    void EnsureBannerText()
    {
        if (bannerText != null)
        {
            return;
        }

        GameObject textObject = new GameObject("Wave Banner Text");
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = bannerWorldOffset;

        bannerText = textObject.AddComponent<TextMeshPro>();
        bannerText.alignment = TextAlignmentOptions.Center;
        bannerText.fontSize = 3f;
        bannerText.color = new Color(1f, 0.85f, 0.2f, 1f);
        bannerText.sortingOrder = 20000;

        Renderer bannerRenderer = textObject.GetComponent<Renderer>();
        if (bannerRenderer != null)
        {
            bannerRenderer.sortingLayerName = "UI";
        }

        bannerText.gameObject.SetActive(false);
    }
}
