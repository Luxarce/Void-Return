using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game HUD controller.
///
/// CHANGES IN THIS VERSION:
///  — ALL button and slider listeners are wired in code (Start method).
///    You no longer need to set onClick events in the Inspector.
///    Just drag the UI component references into the fields below and it works.
///  — Added masterVolumeSlider support (requires AudioMixer / AudioManager v2).
///  — Inventory toggle is handled by InventoryUI script (Tab key). GameHUD
///    no longer manages the inventory panel directly.
///  — Game Over panel setup included with working button wiring.
///  — Module progress wrappers exposed as public methods for GameManager.
/// </summary>
public class GameHUD : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Oxygen Meter")]
    [Tooltip("Slider for the oxygen bar. Set Min=0, Max=1, Whole Numbers=OFF.")]
    public Slider oxygenSlider;

    [Tooltip("The Fill Image inside the oxygen slider (for color transitions).")]
    public Image oxygenFillImage;

    [Tooltip("Text label showing the percentage (e.g., '72%').")]
    public TextMeshProUGUI oxygenLabel;

    [Tooltip("Fill color above the warning threshold.")]
    public Color oxygenColorNormal   = new Color(0f, 0.9f, 1f);

    [Tooltip("Fill color in the warning range.")]
    public Color oxygenColorWarning  = Color.yellow;

    [Tooltip("Fill color in the critical range.")]
    public Color oxygenColorCritical = Color.red;

    [Tooltip("Normalized oxygen below which warning color activates.")]
    [Range(0.05f, 0.5f)]
    public float oxygenWarningLevel  = 0.30f;

    [Tooltip("Normalized oxygen below which critical color activates.")]
    [Range(0.01f, 0.2f)]
    public float oxygenCriticalLevel = 0.10f;

    [Header("Gravity State Indicator")]
    [Tooltip("Text label showing current gravity state name.")]
    public TextMeshProUGUI gravityStateText;

    [Tooltip("Image showing the gravity state icon.")]
    public Image gravityStateIcon;

    [Tooltip("[0]=Normal, [1]=ZeroG, [2]=MicroPull, [3]=Rift.")]
    public Sprite[] gravityStateSprites;

    [Header("Zone Badge")]
    [Tooltip("Badge background image — color changes per zone.")]
    public Image zoneBadgeImage;

    [Tooltip("Zone name text on the badge.")]
    public TextMeshProUGUI zoneNameText;

    public Color zone1Color = new Color(0f, 1f, 0.5f, 0.8f);
    public Color zone2Color = new Color(1f, 0.85f, 0f, 0.8f);
    public Color zone3Color = new Color(1f, 0.2f, 0.1f, 0.8f);

    [Header("Module Progress Bars")]
    [Tooltip("[0]=LifeSupport, [1]=HullPlating, [2]=Navigation, [3]=EngineCore.")]
    public Slider[] moduleProgressSliders;

    [Tooltip("Label text per module slider.")]
    public TextMeshProUGUI[] moduleProgressLabels;

    [Header("Pause Menu")]
    [Tooltip("Root pause overlay panel. Shown when Escape is pressed.")]
    public GameObject pausePanel;

    [Tooltip("Button: resumes the game.")]
    public Button resumeButton;

    [Tooltip("Button: saves game via SaveManager.")]
    public Button saveButton;

    [Tooltip("Button: loads last save via SaveManager.")]
    public Button loadButton;

    [Tooltip("Button: opens the options sub-panel.")]
    public Button optionsButton;

    [Tooltip("Button: returns to the Main Menu.")]
    public Button exitButton;

    [Header("Options Sub-Panel (inside Pause)")]
    [Tooltip("Options panel shown when Options is clicked from the pause menu.")]
    public GameObject optionsPanel;

    [Tooltip("Slider for master volume (0–1). Wired to AudioManager.SetMasterVolume.")]
    public Slider masterVolumeSlider;

    [Tooltip("Slider for music volume (0–1). Wired to AudioManager.SetMusicVolume.")]
    public Slider musicVolumeSlider;

    [Tooltip("Slider for SFX volume (0–1). Wired to AudioManager.SetSFXVolume.")]
    public Slider sfxVolumeSlider;

    [Tooltip("Toggle for muting music. Wired to AudioManager.ToggleMusic.")]
    public Toggle musicToggle;

    [Tooltip("Toggle for muting SFX. Wired to AudioManager.ToggleSFX.")]
    public Toggle sfxToggle;

    [Tooltip("Button: closes options and returns to pause menu.")]
    public Button optionsBackButton;

    [Header("Game Over Panel")]
    [Tooltip("Full-screen Game Over overlay. Set inactive by default.")]
    public GameObject gameOverPanel;

    [Tooltip("Button on Game Over panel: returns to Main Menu.")]
    public Button gameOverReturnButton;

    [Tooltip("Button on Game Over panel: reloads last save.")]
    public Button gameOverRetryButton;

    [Header("Scene Names")]
    [Tooltip("Exact name of the Main Menu scene in Build Settings.")]
    public string mainMenuSceneName = "MainMenu";

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private bool _isPaused;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Hide all overlay panels
        pausePanel?.SetActive(false);
        optionsPanel?.SetActive(false);
        gameOverPanel?.SetActive(false);

        WirePauseMenuButtons();
        WireOptionsPanel();
        WireGameOverPanel();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button Wiring — done entirely in code
    // ─────────────────────────────────────────────────────────────────────────

    private void WirePauseMenuButtons()
    {
        // All AddListener calls connect buttons to methods in code.
        // No Inspector onClick wiring is needed.
        resumeButton?.onClick.AddListener(ResumeGame);
        saveButton?.onClick.AddListener(() => SaveManager.Instance?.Save());
        loadButton?.onClick.AddListener(() => SaveManager.Instance?.Load());
        optionsButton?.onClick.AddListener(OpenOptions);
        exitButton?.onClick.AddListener(ExitToMainMenu);
    }

    private void WireOptionsPanel()
    {
        optionsBackButton?.onClick.AddListener(CloseOptions);

        // Sliders — AddListener fires whenever the slider value changes
        masterVolumeSlider?.onValueChanged.AddListener(v =>
            AudioManager.Instance?.SetMasterVolume(v));

        musicVolumeSlider?.onValueChanged.AddListener(v =>
            AudioManager.Instance?.SetMusicVolume(v));

        sfxVolumeSlider?.onValueChanged.AddListener(v =>
            AudioManager.Instance?.SetSFXVolume(v));

        musicToggle?.onValueChanged.AddListener(b =>
            AudioManager.Instance?.ToggleMusic(b));

        sfxToggle?.onValueChanged.AddListener(b =>
            AudioManager.Instance?.ToggleSFX(b));

        // Initialize sliders to current AudioManager values
        if (AudioManager.Instance != null)
        {
            if (masterVolumeSlider != null) masterVolumeSlider.value = AudioManager.Instance.MasterVolume;
            if (musicVolumeSlider  != null) musicVolumeSlider.value  = AudioManager.Instance.MusicVolume;
            if (sfxVolumeSlider    != null) sfxVolumeSlider.value    = AudioManager.Instance.SFXVolume;
        }
    }

    private void WireGameOverPanel()
    {
        gameOverReturnButton?.onClick.AddListener(ExitToMainMenu);
        gameOverRetryButton?.onClick.AddListener(RetryFromSave);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public Methods — called by GameManager (code wiring) or directly
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the oxygen slider and fill color.
    /// GameManager wires: oxygenSystem.onOxygenChanged → gameHUD.UpdateOxygen
    /// </summary>
    public void UpdateOxygen(float normalized)
    {
        if (oxygenSlider    != null) oxygenSlider.value = normalized;
        if (oxygenLabel     != null) oxygenLabel.text   = $"{Mathf.RoundToInt(normalized * 100f)}%";
        if (oxygenFillImage != null)
        {
            oxygenFillImage.color = normalized > oxygenWarningLevel  ? oxygenColorNormal   :
                                    normalized > oxygenCriticalLevel ? oxygenColorWarning  :
                                                                        oxygenColorCritical;
        }
    }

    /// <summary>
    /// Updates the gravity state icon and text label.
    /// Called directly from PlayerController — no wiring needed.
    /// </summary>
    public void SetGravityState(GravityState state)
    {
        string[] names = { "Normal-G", "Zero-G", "Micro-Pull", "GRAVITY RIFT!" };
        int i = (int)state;
        if (gravityStateText != null)
            gravityStateText.text = i < names.Length ? names[i] : state.ToString();
        if (gravityStateIcon != null && gravityStateSprites != null && i < gravityStateSprites.Length)
            gravityStateIcon.sprite = gravityStateSprites[i];
    }

    /// <summary>
    /// Updates the zone badge color and name.
    /// Called directly from ZoneTrigger — no wiring needed.
    /// </summary>
    public void SetZone(int zoneNumber, string zoneName)
    {
        if (zoneNameText   != null) zoneNameText.text = zoneName;
        if (zoneBadgeImage != null)
            zoneBadgeImage.color = zoneNumber switch
            {
                1 => zone1Color, 2 => zone2Color, 3 => zone3Color, _ => Color.white
            };
    }

    /// <summary>
    /// Updates a module's progress slider by index.
    /// GameManager wires each ShipModule.onProgressChanged to this.
    /// </summary>
    public void UpdateModuleProgress(int moduleIndex, float progress)
    {
        if (moduleProgressSliders == null) return;
        if (moduleIndex < 0 || moduleIndex >= moduleProgressSliders.Length) return;
        if (moduleProgressSliders[moduleIndex] != null)
            moduleProgressSliders[moduleIndex].value = progress;
    }

    // Individual wrappers for direct Inspector wiring if preferred
    public void UpdateLifeSupportProgress(float p) => UpdateModuleProgress(0, p);
    public void UpdateHullProgress(float p)         => UpdateModuleProgress(1, p);
    public void UpdateNavProgress(float p)          => UpdateModuleProgress(2, p);
    public void UpdateEngineProgress(float p)       => UpdateModuleProgress(3, p);

    /// <summary>
    /// Displays the Game Over panel.
    /// GameManager wires: oxygenSystem.onOxygenDepleted → gameHUD.ShowGameOver
    /// </summary>
    public void ShowGameOver()
    {
        Time.timeScale = 0f;
        gameOverPanel?.SetActive(true);
    }

    /// <summary>
    /// Returns to the Main Menu scene.
    /// Wired in code to the exit and gameOverReturn buttons.
    /// </summary>
    public void ExitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pause Menu
    // ─────────────────────────────────────────────────────────────────────────

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        pausePanel?.SetActive(_isPaused);
        Time.timeScale = _isPaused ? 0f : 1f;
    }

    private void ResumeGame()
    {
        _isPaused = false;
        pausePanel?.SetActive(false);
        Time.timeScale = 1f;
    }

    private void OpenOptions()
    {
        pausePanel?.SetActive(false);
        optionsPanel?.SetActive(true);
    }

    private void CloseOptions()
    {
        optionsPanel?.SetActive(false);
        pausePanel?.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Game Over
    // ─────────────────────────────────────────────────────────────────────────

    private void RetryFromSave()
    {
        Time.timeScale = 1f;
        gameOverPanel?.SetActive(false);
        SaveManager.Instance?.Load();
    }
}
