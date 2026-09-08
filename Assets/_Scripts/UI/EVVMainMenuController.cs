using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Thin controller over a scene-authored Canvas. The Canvas, panels, buttons, art, and layout are
// built and tuned directly in the Unity Editor (drag in sprites, position/scale RectTransforms,
// etc.) instead of in code, so visual changes don't require touching this script or recompiling.
// This class only wires behavior: panel navigation, button click handlers, populating the dynamic
// Stage Select list from level data, and spawning the Defender showcase. See the class fields for
// exactly which scene objects need to be assigned in the Inspector for the menu to work.
public class EVVMainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] CanvasGroup mainPanel;
    [SerializeField] CanvasGroup stageSelectPanel;
    [SerializeField] CanvasGroup settingsPanel;
    [SerializeField] float panelFadeDuration = 0.2f;

    [Header("Main Panel Buttons")]
    [SerializeField] Button playButton;
    [SerializeField] Button stageSelectButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button quitButton;

    [Header("Stage Select")]
    [SerializeField] Button stageSelectBackButton;
    [SerializeField] Transform stageListContent;
    [SerializeField] EVVStageSelectListItem stageListItemPrefab;
    [Tooltip("Optional. If assigned, one is instantiated as a non-clickable heading before each stage's levels.")]
    [SerializeField] EVVStageSelectListItem stageHeadingPrefab;

    [Header("Settings")]
    [SerializeField] Button settingsBackButton;
    [SerializeField] Slider masterVolumeSlider;
    [SerializeField] Slider musicVolumeSlider;
    [SerializeField] Slider sfxVolumeSlider;

    [Header("Gameplay Hand-off")]
    [SerializeField] string gameplaySceneName = "Level 1";

    [Header("Defender Showcase")]
    [SerializeField] EVVDefender defenderPrefab;
    [SerializeField] Transform defenderSpawnPoint;
    [SerializeField] float idleBobHeight = 0.15f;
    [SerializeField] float idleBobDuration = 1.4f;

    Coroutine activeFade;

    void Awake()
    {
        EnsureEventSystem();
        EVVAudioSettings.ApplySavedVolume();
        WireButtons();
        PopulateStageSelect();
        SetActivePanel(mainPanel, instant: true);
    }

    void OnEnable()
    {
        EVVLevelCompletion.ProgressChanged += PopulateStageSelect;
    }

    void OnDisable()
    {
        EVVLevelCompletion.ProgressChanged -= PopulateStageSelect;
    }

    void Start()
    {
        SpawnDefenderShowcase();
    }

    void WireButtons()
    {
        // "Play" and "Stage Select" both open Stage Select for now - there is no separate
        // resume/continue flow yet, so the two buttons are intentionally the same action.
        if (playButton != null)
        {
            playButton.onClick.AddListener(ShowStageSelect);
        }

        if (stageSelectButton != null)
        {
            stageSelectButton.onClick.AddListener(ShowStageSelect);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(ShowSettings);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuitClicked);
        }

        if (stageSelectBackButton != null)
        {
            stageSelectBackButton.onClick.AddListener(ShowMainPanel);
        }

        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.AddListener(ShowMainPanel);
        }

        WireVolumeSlider(masterVolumeSlider, EVVAudioSettings.MasterVolume, EVVAudioSettings.SetMasterVolume);
        WireVolumeSlider(musicVolumeSlider, EVVAudioSettings.MusicVolume, EVVAudioSettings.SetMusicVolume);
        WireVolumeSlider(sfxVolumeSlider, EVVAudioSettings.SfxVolume, EVVAudioSettings.SetSfxVolume);
    }

    static void WireVolumeSlider(Slider slider, float initialValue, UnityEngine.Events.UnityAction<float> onChanged)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(initialValue);
        slider.onValueChanged.AddListener(onChanged);
    }

    // ---- Navigation -----------------------------------------------------

    public void ShowMainPanel()
    {
        SetActivePanel(mainPanel, instant: false);
    }

    public void ShowStageSelect()
    {
        SetActivePanel(stageSelectPanel, instant: false);
    }

    public void ShowSettings()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(EVVAudioSettings.MasterVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.SetValueWithoutNotify(EVVAudioSettings.MusicVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(EVVAudioSettings.SfxVolume);
        }

        SetActivePanel(settingsPanel, instant: false);
    }

    void SetActivePanel(CanvasGroup target, bool instant)
    {
        CanvasGroup[] allPanels = { mainPanel, stageSelectPanel, settingsPanel };
        foreach (CanvasGroup panel in allPanels)
        {
            if (panel == null)
            {
                continue;
            }

            bool isTarget = panel == target;
            panel.interactable = isTarget;
            panel.blocksRaycasts = isTarget;
        }

        if (activeFade != null)
        {
            StopCoroutine(activeFade);
            activeFade = null;
        }

        if (instant || !isActiveAndEnabled)
        {
            foreach (CanvasGroup panel in allPanels)
            {
                if (panel != null)
                {
                    panel.alpha = panel == target ? 1f : 0f;
                }
            }

            return;
        }

        activeFade = StartCoroutine(FadePanels(allPanels, target));
    }

    IEnumerator FadePanels(CanvasGroup[] panels, CanvasGroup target)
    {
        float[] startAlphas = panels.Select(panel => panel != null ? panel.alpha : 0f).ToArray();
        float elapsed = 0f;

        while (elapsed < panelFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / panelFadeDuration);
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null)
                {
                    continue;
                }

                float targetAlpha = panels[i] == target ? 1f : 0f;
                panels[i].alpha = Mathf.Lerp(startAlphas[i], targetAlpha, t);
            }

            yield return null;
        }

        foreach (CanvasGroup panel in panels)
        {
            if (panel != null)
            {
                panel.alpha = panel == target ? 1f : 0f;
            }
        }

        activeFade = null;
    }

    // ---- Handlers ----------------------------------------------------

    void OnQuitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void OnLevelSelected(EVVLevelDefinition level)
    {
        EVVPendingLevelSelection.Set(level.Id);
        SceneManager.LoadScene(gameplaySceneName);
    }

    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    // Groups by EVVLevelDefinition.Stage (the level YAML's own "stage" int field) rather than
    // parsing the filename, so this stays correct for however many stages exist without a second
    // hard-coded notion of "stage" living in the Main Menu.
    void PopulateStageSelect()
    {
        if (stageListContent == null || stageListItemPrefab == null)
        {
            return;
        }

        foreach (Transform child in stageListContent)
        {
            Destroy(child.gameObject);
        }

        List<EVVLevelDefinition> allLevels = EVVLevelLoader.DiscoverLevels();
        if (allLevels.Count == 0)
        {
            Debug.LogWarning("No level files found in Assets/Levels.");
        }

        List<EVVLevelDefinition> levels = EVVLevelCompletion.GetAvailableLevels(allLevels);

        var groupedByStage = levels.GroupBy(level => level.Stage).OrderBy(group => group.Key);

        foreach (var stageGroup in groupedByStage)
        {
            if (stageHeadingPrefab != null)
            {
                EVVStageSelectListItem heading = Instantiate(stageHeadingPrefab, stageListContent);
                heading.Bind("STAGE " + stageGroup.Key, null);
            }

            foreach (EVVLevelDefinition level in stageGroup.OrderBy(l => l.Level))
            {
                string label = level.Stage + "-" + level.Level.ToString("00")
                    + (string.IsNullOrEmpty(level.Name) ? "" : "  " + level.Name);
                EVVLevelDefinition capturedLevel = level;
                EVVStageSelectListItem item = Instantiate(stageListItemPrefab, stageListContent);
                item.Bind(label, () => OnLevelSelected(capturedLevel));
            }
        }
    }

    void SpawnDefenderShowcase()
    {
        if (defenderPrefab == null || defenderSpawnPoint == null)
        {
            return;
        }

        EVVDefender instance = Instantiate(defenderPrefab, defenderSpawnPoint.position, Quaternion.identity);
        StartCoroutine(IdleBob(instance.transform, defenderSpawnPoint.position));
    }

    IEnumerator IdleBob(Transform target, Vector3 basePosition)
    {
        while (target != null)
        {
            float offset = Mathf.Sin(Time.time * (Mathf.PI * 2f / idleBobDuration)) * idleBobHeight;
            target.position = basePosition + new Vector3(0f, offset, 0f);
            yield return null;
        }
    }
}
