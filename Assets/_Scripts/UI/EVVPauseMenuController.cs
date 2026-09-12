using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Thin controller over the scene-authored PauseMenuCanvas prefab, same split as
// EVVMainMenuController: the Canvas, panels, wood buttons and volume sliders are authored as
// assets (cloned from the Main Menu's own Settings panel so the look matches), and this class
// only wires behavior - Escape handling, freezing gameplay, and panel navigation.
//
// Volume is read/written through EVVAudioSettings, the same PlayerPrefs-backed store the Main
// Menu's Settings panel uses, so changes made mid-level carry over and persist.
public class EVVPauseMenuController : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] Canvas canvas;
    [SerializeField] GraphicRaycaster raycaster;

    [Header("Panels")]
    [SerializeField] CanvasGroup pausePanel;
    [SerializeField] CanvasGroup settingsPanel;

    [Header("Pause Panel Buttons")]
    [SerializeField] Button resumeButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button mainMenuButton;

    [Header("Settings Panel")]
    [SerializeField] Button settingsBackButton;
    [SerializeField] Slider masterVolumeSlider;
    [SerializeField] Slider musicVolumeSlider;
    [SerializeField] Slider sfxVolumeSlider;

    [Header("Gameplay")]
    [Tooltip("Disabled while paused so clicks on the menu don't also place defenders.")]
    [SerializeField] PlantPlacementManager placementManager;
    [SerializeField] string mainMenuSceneName = "MainMenu";

    public bool IsPaused { get; private set; }

    void Awake()
    {
        ResolveReferences();
        WireButtons();
        SetPaused(false);
    }

    // The prefab's own hierarchy is the source of truth for these, so a missing Inspector
    // reference resolves by name instead of silently producing a half-dead menu.
    void ResolveReferences()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (raycaster == null) raycaster = GetComponent<GraphicRaycaster>();
        if (pausePanel == null) pausePanel = FindChild<CanvasGroup>("PausePanel");
        if (settingsPanel == null) settingsPanel = FindChild<CanvasGroup>("SettingsPanel");
        if (resumeButton == null) resumeButton = FindChild<Button>("ResumeButton");
        if (settingsButton == null) settingsButton = FindChild<Button>("SettingsButton");
        if (mainMenuButton == null) mainMenuButton = FindChild<Button>("MainMenuButton");
        if (settingsBackButton == null && settingsPanel != null)
        {
            Transform back = settingsPanel.transform.Find("Button_Wood");
            if (back != null) settingsBackButton = back.GetComponent<Button>();
        }

        if (masterVolumeSlider == null) masterVolumeSlider = FindChild<Slider>("MasterVolumeSlider");
        if (musicVolumeSlider == null) musicVolumeSlider = FindChild<Slider>("MusicVolumeSlider");
        if (sfxVolumeSlider == null) sfxVolumeSlider = FindChild<Slider>("SfxVolumeSlider");

        if (placementManager == null) placementManager = FindAnyObjectByType<PlantPlacementManager>();

        EnsureEventSystem();
    }

    T FindChild<T>(string childName) where T : Component
    {
        foreach (T candidate in GetComponentsInChildren<T>(true))
        {
            if (candidate.gameObject.name == childName)
            {
                return candidate;
            }
        }

        return null;
    }

    // The gameplay scene's own UI is world-space and raycasts itself, so it does not necessarily
    // ship an EventSystem - but these are uGUI buttons and sliders, which need one.
    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    void WireButtons()
    {
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (settingsButton != null) settingsButton.onClick.AddListener(ShowSettings);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
        if (settingsBackButton != null) settingsBackButton.onClick.AddListener(ShowPausePanel);

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

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        // Escape closes whatever is open: Settings backs out to the pause panel, the pause panel
        // resumes, and from gameplay it opens the menu.
        if (IsPaused && settingsPanel != null && settingsPanel.alpha > 0f)
        {
            ShowPausePanel();
        }
        else if (IsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        SetPaused(true);
        ShowPausePanel();
    }

    public void Resume()
    {
        SetPaused(false);
    }

    void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (canvas != null) canvas.enabled = paused;
        if (raycaster != null) raycaster.enabled = paused;
        if (placementManager != null) placementManager.enabled = !paused;
    }

    void ShowPausePanel()
    {
        SetActivePanel(pausePanel);
    }

    void ShowSettings()
    {
        // Re-read in case the Main Menu (or another session) changed them since this level began.
        if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(EVVAudioSettings.MasterVolume);
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(EVVAudioSettings.MusicVolume);
        if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(EVVAudioSettings.SfxVolume);

        SetActivePanel(settingsPanel);
    }

    void SetActivePanel(CanvasGroup target)
    {
        CanvasGroup[] panels = { pausePanel, settingsPanel };
        foreach (CanvasGroup panel in panels)
        {
            if (panel == null)
            {
                continue;
            }

            bool isTarget = panel == target;
            panel.alpha = isTarget ? 1f : 0f;
            panel.interactable = isTarget;
            panel.blocksRaycasts = isTarget;
        }
    }

    void GoToMainMenu()
    {
        // Time scale is global and survives the scene load, so a paused level would otherwise
        // leave the Main Menu frozen.
        Time.timeScale = 1f;

        // Hide the persistent HUD immediately so it never flashes over the menu, but do NOT
        // destroy it here. Destroying a DontDestroyOnLoad root in the same end-of-frame as a
        // single-mode scene switch deadlocks the editor (main thread blocks at 0% CPU, never
        // reaches MainMenu.Awake). Destroy it once MainMenu has finished loading instead.
        SetPersistentGameplayObjectsActive(false);
        SceneManager.sceneLoaded += DestroyPersistentGameplayObjectsAfterLoad;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    static void DestroyPersistentGameplayObjectsAfterLoad(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= DestroyPersistentGameplayObjectsAfterLoad;
        DestroyPersistentGameplayObjects();
    }

    // EVVManager/EVVUiWidgetRefs mark their shared "Manager" object DontDestroyOnLoad, which drags
    // the in-game HUD (defender bar, wallet, card selection) into the Main Menu on top of its
    // canvas. Tear it down on the way out; Level 1's own copy re-initializes on the next load.
    static void SetPersistentGameplayObjectsActive(bool active)
    {
        GameObject manager = EVVManager.Instance != null ? EVVManager.Instance.gameObject : null;
        GameObject widgets = EVVUiWidgetRefs.Instance != null ? EVVUiWidgetRefs.Instance.gameObject : null;

        if (manager != null) manager.SetActive(active);
        if (widgets != null && widgets != manager) widgets.SetActive(active);
    }

    static void DestroyPersistentGameplayObjects()
    {
        GameObject manager = EVVManager.Instance != null ? EVVManager.Instance.gameObject : null;
        GameObject widgets = EVVUiWidgetRefs.Instance != null ? EVVUiWidgetRefs.Instance.gameObject : null;

        DestroyPersistent(manager);
        if (widgets != manager)
        {
            DestroyPersistent(widgets);
        }
    }

    static void DestroyPersistent(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Destroy(target);
    }
}
