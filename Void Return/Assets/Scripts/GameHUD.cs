using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game HUD controller.
///
/// FIX — PROGRESS BARS NOT UPDATING:
///  Root cause: UpdateModuleProgress() checked moduleProgressSliders[i] for null
///  but if the array SIZE was correct yet the ELEMENTS were not assigned in the
///  Inspector, the null check passed (non-null default element) but value was
///  never set. Added a Debug.Log so you can see when progress is received.
///  Also: Slider Min/Max must be 0/1. If Min and Max are both 0, the slider
///  will never visually move. This is the most common mistake.
///
/// FIX — ZONE BADGE COLORS:
///  SetZone() now also accepts zone numbers outside 1-3 gracefully.
///  Colors exposed in Inspector so you can tweak them without code changes.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Oxygen Meter")]
    public Slider           oxygenSlider;
    public Image            oxygenFillImage;
    public TextMeshProUGUI  oxygenLabel;
    public Color            oxygenColorNormal   = new Color(0f, 0.9f, 1f);
    public Color            oxygenColorWarning  = Color.yellow;
    public Color            oxygenColorCritical = Color.red;
    [Range(0.05f, 0.5f)] public float oxygenWarningLevel  = 0.30f;
    [Range(0.01f, 0.2f)] public float oxygenCriticalLevel = 0.10f;

    [Header("Gravity State Indicator")]
    public TextMeshProUGUI gravityStateText;
    public Image           gravityStateIcon;
    public Sprite[]        gravityStateSprites;

    [Header("Zone Badge")]
    [Tooltip("Background image of the zone badge — its color changes per zone.")]
    public Image            zoneBadgeImage;

    [Tooltip("Text label showing the zone name on the badge.")]
    public TextMeshProUGUI  zoneNameText;

    [Tooltip("Badge color when the player is in Zone 1 (Debris Field).")]
    public Color zone1Color = new Color(0f, 1f, 0.5f, 0.9f);

    [Tooltip("Badge color when in Zone 2 (Drift Ring).")]
    public Color zone2Color = new Color(1f, 0.85f, 0f, 0.9f);

    [Tooltip("Badge color when in Zone 3 (Deep Scatter).")]
    public Color zone3Color = new Color(1f, 0.2f, 0.1f, 0.9f);

    [Tooltip("Badge color outside all defined zones.")]
    public Color defaultZoneColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

    [Header("Module Progress Bars")]
    [Tooltip("Sliders for each module: [0]=LifeSupport, [1]=Hull, [2]=Nav, [3]=Engine.\n" +
             "IMPORTANT: Set each slider's Min Value = 0 and Max Value = 1 in the Inspector.\n" +
             "If both are 0, the bar will never visually advance.")]
    public Slider[] moduleProgressSliders;
    public TextMeshProUGUI[] moduleProgressLabels;

    [Header("Pause Menu")]
    public GameObject pausePanel;
    public Button     resumeButton;
    public Button     saveButton;
    public Button     loadButton;
    public Button     optionsButton;
    public Button     exitButton;

    [Header("Options Sub-Panel")]
    public GameObject optionsPanel;
    public Slider     masterVolumeSlider;
    public Slider     musicVolumeSlider;
    public Slider     sfxVolumeSlider;
    public Toggle     musicToggle;
    public Toggle     sfxToggle;
    public Button     optionsBackButton;

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public Button     gameOverReturnButton;
    public Button     gameOverRetryButton;

    [Header("Scene Names")]
    public string mainMenuSceneName = "MainMenu";

    // ─────────────────────────────────────────────────────────────────────────
    private bool _isPaused;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        pausePanel?.SetActive(false);
        optionsPanel?.SetActive(false);
        gameOverPanel?.SetActive(false);

        WirePauseButtons();
        WireOptionsPanel();
        WireGameOverPanel();
        ValidateProgressSliders();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Validation helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ValidateProgressSliders()
    {
        if (moduleProgressSliders == null) return;
        for (int i = 0; i < moduleProgressSliders.Length; i++)
        {
            var s = moduleProgressSliders[i];
            if (s == null)
            {
                Debug.LogWarning($"[GameHUD] moduleProgressSliders[{i}] is not assigned. " +
                                 "Drag the Slider component into this element.");
                continue;
            }
            if (s.minValue != 0f || s.maxValue != 1f)
            {
                Debug.LogWarning($"[GameHUD] Slider [{i}] has Min={s.minValue}, Max={s.maxValue}. " +
                                 "Set Min=0 and Max=1 in the Slider Inspector for the bar to move.");
                s.minValue = 0f;
                s.maxValue = 1f;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Wiring
    // ─────────────────────────────────────────────────────────────────────────

    private void WirePauseButtons()
    {
        resumeButton?.onClick.AddListener(ResumeGame);
        saveButton?.onClick.AddListener(() => SaveManager.Instance?.Save());
        loadButton?.onClick.AddListener(() => SaveManager.Instance?.Load());
        optionsButton?.onClick.AddListener(OpenOptions);
        exitButton?.onClick.AddListener(ExitToMainMenu);
    }

    private void WireOptionsPanel()
    {
        optionsBackButton?.onClick.AddListener(CloseOptions);
        masterVolumeSlider?.onValueChanged.AddListener(v => AudioManager.Instance?.SetMasterVolume(v));
        musicVolumeSlider?.onValueChanged.AddListener(v  => AudioManager.Instance?.SetMusicVolume(v));
        sfxVolumeSlider?.onValueChanged.AddListener(v    => AudioManager.Instance?.SetSFXVolume(v));
        musicToggle?.onValueChanged.AddListener(b        => AudioManager.Instance?.ToggleMusic(b));
        sfxToggle?.onValueChanged.AddListener(b          => AudioManager.Instance?.ToggleSFX(b));

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
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void UpdateOxygen(float normalized)
    {
        if (oxygenSlider    != null) oxygenSlider.value = normalized;
        if (oxygenLabel     != null) oxygenLabel.text   = $"{Mathf.RoundToInt(normalized * 100f)}%";
        if (oxygenFillImage != null)
            oxygenFillImage.color = normalized > oxygenWarningLevel  ? oxygenColorNormal
                                  : normalized > oxygenCriticalLevel ? oxygenColorWarning
                                                                     : oxygenColorCritical;
    }

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
    /// Updates the zone badge color and name text.
    /// Colors are set in the Inspector (zone1Color / zone2Color / zone3Color).
    /// </summary>
    public void SetZone(int zoneNumber, string zoneName)
    {
        if (zoneNameText != null) zoneNameText.text = zoneName;
        if (zoneBadgeImage != null)
        {
            zoneBadgeImage.color = zoneNumber switch
            {
                1 => zone1Color,
                2 => zone2Color,
                3 => zone3Color,
                _ => defaultZoneColor
            };
        }
    }

    /// <summary>
    /// Updates a module's progress bar.
    /// GameManager wires ShipModule.onProgressChanged to this.
    ///
    /// If the bar does not move: check that the Slider's Min=0 and Max=1
    /// in the Inspector. The ValidateProgressSliders() call at Start() also
    /// auto-corrects this and logs a warning.
    /// </summary>
    public void UpdateModuleProgress(int moduleIndex, float progress)
    {
        if (moduleProgressSliders == null || moduleProgressSliders.Length == 0)
        {
            Debug.LogWarning("[GameHUD] moduleProgressSliders array is empty. " +
                             "Assign the slider components in the Inspector.");
            return;
        }
        if (moduleIndex < 0 || moduleIndex >= moduleProgressSliders.Length)
        {
            Debug.LogWarning($"[GameHUD] UpdateModuleProgress: index {moduleIndex} out of range " +
                             $"(array length {moduleProgressSliders.Length}).");
            return;
        }
        var slider = moduleProgressSliders[moduleIndex];
        if (slider == null)
        {
            Debug.LogWarning($"[GameHUD] moduleProgressSliders[{moduleIndex}] is null. " +
                             "Drag the Slider into this array element in the Inspector.");
            return;
        }
        slider.value = progress;
        Debug.Log($"[GameHUD] Module {moduleIndex} progress set to {progress:P0}");

        if (moduleProgressLabels != null && moduleIndex < moduleProgressLabels.Length
            && moduleProgressLabels[moduleIndex] != null)
            moduleProgressLabels[moduleIndex].text = $"{Mathf.RoundToInt(progress * 100f)}%";
    }

    // Convenience wrappers
    public void UpdateLifeSupportProgress(float p) => UpdateModuleProgress(0, p);
    public void UpdateHullProgress(float p)         => UpdateModuleProgress(1, p);
    public void UpdateNavProgress(float p)          => UpdateModuleProgress(2, p);
    public void UpdateEngineProgress(float p)       => UpdateModuleProgress(3, p);

    public void ShowGameOver()
    {
        Time.timeScale = 0f;
        gameOverPanel?.SetActive(true);
    }

    public void ExitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pause
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

    private void OpenOptions()  { pausePanel?.SetActive(false); optionsPanel?.SetActive(true); }
    private void CloseOptions() { optionsPanel?.SetActive(false); pausePanel?.SetActive(true); }

    private void RetryFromSave()
    {
        Time.timeScale = 1f;
        gameOverPanel?.SetActive(false);
        SaveManager.Instance?.Load();
    }
}
