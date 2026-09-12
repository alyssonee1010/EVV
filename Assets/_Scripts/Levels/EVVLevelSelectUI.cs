using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class EVVLevelSelectUI : MonoBehaviour
{
    [SerializeField] PlantPlacementManager placementManager;
    [SerializeField] EVVWaveDirector waveDirector;
    [SerializeField] EVVDefenderLoadoutUI loadoutUI;
    [SerializeField] EVVPlacementSlotBinder slotBinder;
    [SerializeField] Vector2 cardSize = new Vector2(2.2f, 1f);
    [SerializeField] Vector2 cardSpacing = new Vector2(2.5f, 1.3f);
    [SerializeField] int cardsPerRow = 3;
    [SerializeField] Color cardColor = new Color(0.12f, 0.16f, 0.24f, 0.95f);
    [SerializeField] Color cardTextColor = Color.white;
    [SerializeField] float cameraPlaneDistance = 5f;
    [SerializeField] EVVBoardGrid boardGrid;

    GameObject root;

    void Awake()
    {
        if (placementManager == null)
        {
            placementManager = FindAnyObjectByType<PlantPlacementManager>();
        }

        if (waveDirector == null)
        {
            waveDirector = FindAnyObjectByType<EVVWaveDirector>();
        }

        if (loadoutUI == null)
        {
            loadoutUI = FindAnyObjectByType<EVVDefenderLoadoutUI>();
        }

        if (slotBinder == null)
        {
            slotBinder = FindAnyObjectByType<EVVPlacementSlotBinder>();
        }

        
        if (boardGrid == null) 
        {
            boardGrid = FindAnyObjectByType<EVVBoardGrid>();
        }

        if (waveDirector != null)
        {
            waveDirector.LevelCompleted += OnLevelCompleted;
        }
    }

    void OnDestroy()
    {
        if (waveDirector != null)
        {
            waveDirector.LevelCompleted -= OnLevelCompleted;
        }
    }

    void Start()
    {
        string pendingLevelId = EVVPendingLevelSelection.Consume();
        if (!string.IsNullOrEmpty(pendingLevelId))
        {
            EVVLevelDefinition pendingLevel = EVVLevelLoader.DiscoverLevels()
                .FirstOrDefault(level => level.Id == pendingLevelId);
            if (pendingLevel != null)
            {
                // Show defender selection + a single "Continue" prompt rather than starting the
                // level immediately, so the player can adjust their loadout first. The wave
                // director stays inactive (and the progress bar hidden, see
                // EVVLevelProgressUI.Awake) until Continue is actually pressed.
                OpenMenu(pendingLevel);
                return;
            }
        }

        OpenMenu();
    }

    void OnLevelCompleted(EVVLevelDefinition level)
    {
        if (placementManager != null)
        {
            placementManager.ResetBoard();
        }

        if (EVVBoardLife.Instance != null)
        {
            EVVBoardLife.Instance.ResetLife();
        }

        if (level != null)
        {
            EVVLevelCompletion.MarkCompleted(level.Id);

            if (level.Unlocks.Count > 0)
            {
                EVVDefenderUnlocks.UnlockAll(level.Unlocks);
            }
        }

        // Continuous flow: go back to defender selection with a "Continue" prompt for the next
        // level in sequence rather than the full stage-select grid. Falls back to the full grid
        // once there is no next level (last level completed).
        OpenMenu(FindNextLevel(level));
    }

    // Levels are already sorted by (Stage, Level) via EVVLevelLoader.DiscoverLevels(), so "next"
    // is simply the following entry in that discovery order.
    EVVLevelDefinition FindNextLevel(EVVLevelDefinition current)
    {
        if (current == null)
        {
            return null;
        }

        List<EVVLevelDefinition> levels = EVVLevelLoader.DiscoverLevels();
        int index = levels.FindIndex(candidate => candidate.Id == current.Id);
        if (index < 0 || index + 1 >= levels.Count)
        {
            return null;
        }

        return levels[index + 1];
    }

    void Update()
    {
        if (root == null || !root.activeSelf)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
    }

    // With no argument, builds the full stage-select grid (standalone/testing fallback). With
    // onlyLevel set, builds a single "Continue" prompt for that level instead — the continuous
    // in-scene flow used after Main Menu hand-off and after level completion.
    public void OpenMenu(EVVLevelDefinition onlyLevel = null)
    {
        if (placementManager != null)
        {
            placementManager.enabled = false;
        }

        if (onlyLevel != null && boardGrid != null)
        {
            boardGrid.SetDimensions(onlyLevel.BoardRows, onlyLevel.BoardColumns);
        }

        if (loadoutUI != null)
        {
            loadoutUI.SetVisible(true);
        }

        BuildMenu(onlyLevel);
    }

    void HandleClick()
    {
        Vector3 mouseWorldPosition = GetMouseWorldPosition();
        Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorldPosition);
        foreach (Collider2D hit in hits)
        {
            EVVLevelSelectCard card = hit.GetComponent<EVVLevelSelectCard>();
            if (card != null)
            {
                SelectLevel(card.Level);
                return;
            }
        }
    }

    void SelectLevel(EVVLevelDefinition level)
    {
        if (level == null)
        {
            return;
        }

        CloseMenu();

        if (slotBinder != null)
        {
            slotBinder.Rebind();
        }

        if (waveDirector != null)
        {
            waveDirector.StartLevel(level);
        }
    }

    void CloseMenu()
    {
        if (root != null)
        {
            Destroy(root);
            root = null;
        }

        if (loadoutUI != null)
        {
            loadoutUI.SetVisible(false);
        }

        if (placementManager != null)
        {
            placementManager.enabled = true;
        }
    }

    void BuildMenu(EVVLevelDefinition onlyLevel = null)
    {
        if (root != null)
        {
            Destroy(root);
        }

        List<EVVLevelDefinition> levels;
        if (onlyLevel != null)
        {
            root = new GameObject("Continue Prompt");
            levels = new List<EVVLevelDefinition> { onlyLevel };
        }
        else
        {
            root = new GameObject("Level Select Menu");
            List<EVVLevelDefinition> allLevels = EVVLevelLoader.DiscoverLevels();
            if (allLevels.Count == 0)
            {
                Debug.LogWarning("No level files found in Assets/StreamingAssets/Levels.");
            }

            levels = EVVLevelCompletion.GetAvailableLevels(allLevels);
        }

        for (int i = 0; i < levels.Count; i++)
        {
            int column = i % cardsPerRow;
            int row = i / cardsPerRow;
            Vector3 localPosition = new Vector3(column * cardSpacing.x, -row * cardSpacing.y, 0f);
            BuildCard(levels[i], localPosition, isContinuePrompt: onlyLevel != null);
        }

        CenterMenu(levels.Count);
    }

    void CenterMenu(int levelCount)
    {
        if (levelCount == 0)
        {
            root.transform.position = GetCameraPlaneCenter();
            return;
        }

        int columnsInWidestRow = Mathf.Min(cardsPerRow, levelCount);
        int rowCount = Mathf.CeilToInt(levelCount / (float)cardsPerRow);
        float widthOfWidestRow = (columnsInWidestRow - 1) * cardSpacing.x;
        float totalHeight = (rowCount - 1) * cardSpacing.y;

        Vector3 cameraCenter = GetCameraPlaneCenter();
        root.transform.position = cameraCenter + new Vector3(-widthOfWidestRow / 2f, totalHeight / 2f, 0f);
    }

    Vector3 GetCameraPlaneCenter()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return Vector3.zero;
        }

        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, cameraPlaneDistance);
        Vector3 worldCenter = camera.ScreenToWorldPoint(screenCenter);
        worldCenter.z = 0f;
        return worldCenter;
    }

    void BuildCard(EVVLevelDefinition level, Vector3 localPosition, bool isContinuePrompt = false)
    {
        GameObject card = new GameObject("Level " + level.Id);
        card.transform.SetParent(root.transform, false);
        card.transform.localPosition = localPosition;

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(card.transform, false);
        backgroundObject.transform.localScale = new Vector3(cardSize.x, cardSize.y, 1f);

        SpriteRenderer background = backgroundObject.AddComponent<SpriteRenderer>();
        background.sprite = EVVFlatSpriteFactory.GetWhiteSprite();
        background.color = cardColor;
        background.sortingLayerName = "UI";
        background.sortingOrder = 20000;

        BoxCollider2D collider = card.AddComponent<BoxCollider2D>();
        collider.size = cardSize;

        EVVLevelSelectCard cardMarker = card.AddComponent<EVVLevelSelectCard>();
        cardMarker.Level = level;

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(card.transform, false);
        textObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);

        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        text.text = isContinuePrompt
            ? "CONTINUE\n" + level.Stage + "-" + level.Level.ToString("00")
            : level.Stage + "-" + level.Level.ToString("00") + "\n" + level.Name;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 2.4f;
        text.color = cardTextColor;
        text.sortingOrder = 20001;
        text.rectTransform.sizeDelta = cardSize;

        Renderer textRenderer = textObject.GetComponent<Renderer>();
        if (textRenderer != null)
        {
            textRenderer.sortingLayerName = "UI";
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPosition = Input.mousePosition;
        mouseScreenPosition.z = Mathf.Abs(Camera.main.transform.position.z);

        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        mouseWorldPosition.z = 0f;
        return mouseWorldPosition;
    }
}
