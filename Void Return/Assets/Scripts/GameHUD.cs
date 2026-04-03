using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game HUD.
///
/// FIXES:
///  — ShowGameOver() and ShowVictory() now call GadgetCrosshair.HideForPanel()
///    so the custom crosshair disappears and the OS cursor is restored,
///    making panel buttons clickable again.
///  — ValidateProgressSliders() auto-corrects Min=0/Max=1 at Start.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Oxygen Meter")]
    public Slider          oxygenSlider;
    public Image           oxygenFillImage;
    public TextMeshProUGUI oxygenLabel;
    public Color oxygenColorNormal   = new Color(0f, 0.9f, 1f);
    public Color oxygenColorWarning  = Color.yellow;
    public Color oxygenColorCritical = Color.red;
    [Range(0.05f, 0.5f)] public float oxygenWarningLevel  = 0.30f;
    [Range(0.01f, 0.2f)] public float oxygenCriticalLevel = 0.10f;

    [Header("Gravity State")]
    public TextMeshProUGUI gravityStateText;
    public Image           gravityStateIcon;
    public Sprite[]        gravityStateSprites;

    [Header("Zone Badge")]
    public Image           zoneBadgeImage;
    public TextMeshProUGUI zoneNameText;
    public Color zone1Color       = new Color(0f, 1f, 0.5f, 0.9f);
    public Color zone2Color       = new Color(1f, 0.85f, 0f, 0.9f);
    public Color zone3Color       = new Color(1f, 0.2f, 0.1f, 0.9f);
    public Color defaultZoneColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

    [Header("Module Progress Bars")]
    [Tooltip("IMPORTANT: Min Value = 0, Max Value = 1 on each Slider.")]
    public Slider[]          moduleProgressSliders;
    public TextMeshProUGUI[] moduleProgressLabels;

    [Header("Pause Menu")]
    public GameObject pausePanel;
    public Button     resumeButton;
    public Button     saveButton;
    public Button     loadButton;
    public Button     optionsButton;
    public Button     exitButton;

    [Header("Options Panel")]
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

    [Header("Victory Panel")]
    public GameObject victoryPanel;
    public Button     victoryReturnButton;

    [Header("Scene Names")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Crosshair Reference")]
    [Tooltip("Drag GadgetCrosshair here. The crosshair is hidden when panels open.")]
    public GadgetCrosshair gadgetCrosshair;

    // ─────────────────────────────────────────────────────────────────────────
    private bool _isPaused;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        pausePanel?.SetActive(false);
        optionsPanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
        victoryPanel?.SetActive(false);

        WirePauseButtons();
        WireOptionsPanel();
        WireGameOverPanel();
        WireVictoryPanel();
        ValidateProgressSliders();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void ValidateProgressSliders()
    {
        if (moduleProgressSliders == null) return;
        for (int i = 0; i < moduleProgressSliders.Length; i++)
        {
            var s = moduleProgressSliders[i];
            if (s == null) { Debug.LogWarning($"[GameHUD] moduleProgressSliders[{i}] not assigned."); continue; }
            if (s.minValue != 0f || s.maxValue != 1f)
            {
                Debug.LogWarning($"[GameHUD] Slider[{i}] Min={s.minValue}, Max={s.maxValue} — auto-correcting to 0/1.");
                s.minValue = 0f;
                s.maxValue = 1f;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button wiring
    // ─────────────────────────────────────────────────────────────────────────

    private void WirePauseButtons()
    {
        resumeButton?.onClick.AddListener(ResumeGame);
        saveButton?.onClick.AddListener(()  => SaveManager.Instance?.Save());
        loadButton?.onClick.AddListener(()  => SaveManager.Instance?.Load());
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

    private void WireVictoryPanel()
    {
        victoryReturnButton?.onClick.AddListener(ExitToMainMenu);
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
        if (gravityStateText != null) gravityStateText.text = i < names.Length ? names[i] : state.ToString();
        if (gravityStateIcon != null && gravityStateSprites != null && i < gravityStateSprites.Length)
            gravityStateIcon.sprite = gravityStateSprites[i];
    }

    public void SetZone(int zoneNumber, string zoneName)
    {
        if (zoneNameText   != null) zoneNameText.text  = zoneName;
        if (zoneBadgeImage != null)
            zoneBadgeImage.color = zoneNumber switch
            {
                1 => zone1Color,
                2 => zone2Color,
                3 => zone3Color,
                _ => defaultZoneColor,
            };
    }

    public void UpdateModuleProgress(int moduleIndex, float progress)
    {
        if (moduleProgressSliders == null || moduleProgressSliders.Length == 0)
        {
            Debug.LogWarning("[GameHUD] moduleProgressSliders array is empty. " +
                             "Assign the Slider components in the Inspector.");
            return;
        }
        if (moduleIndex < 0 || moduleIndex >= moduleProgressSliders.Length)
        {
            Debug.LogWarning($"[GameHUD] moduleIndex {moduleIndex} out of range.");
            return;
        }
        var slider = moduleProgressSliders[moduleIndex];
        if (slider == null)
        {
            Debug.LogWarning($"[GameHUD] moduleProgressSliders[{moduleIndex}] is null.");
            return;
        }
        slider.value = progress;
        Debug.Log($"[GameHUD] Module {moduleIndex} progress bar set to {progress:P0}");
        if (moduleProgressLabels != null && moduleIndex < moduleProgressLabels.Length
            && moduleProgressLabels[moduleIndex] != null)
            moduleProgressLabels[moduleIndex].text = $"{Mathf.RoundToInt(progress * 100f)}%";
    }

    public void UpdateLifeSupportProgress(float p) => UpdateModuleProgress(0, p);
    public void UpdateHullProgress(float p)         => UpdateModuleProgress(1, p);
    public void UpdateNavProgress(float p)          => UpdateModuleProgress(2, p);
    public void UpdateEngineProgress(float p)       => UpdateModuleProgress(3, p);

    /// <summary>
    /// Shows the game-over panel.
    /// Also hides the gadget crosshair and restores the OS cursor so buttons are clickable.
    /// </summary>
    public void ShowGameOver()
    {
        Time.timeScale = 0f;
        gameOverPanel?.SetActive(true);
        gadgetCrosshair?.HideForPanel();   // restore cursor so buttons work
    }

    /// <summary>
    /// Shows the victory / escape panel.
    /// Also hides the gadget crosshair and restores the cursor.
    /// </summary>
    public void ShowVictory()
    {
        Time.timeScale = 0f;
        victoryPanel?.SetActive(true);
        gadgetCrosshair?.HideForPanel();
    }

    public void ExitToMainMenu()
    {
        Time.timeScale = 1f;
        gadgetCrosshair?.RestoreForGadget();
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
        if (_isPaused) gadgetCrosshair?.HideForPanel();
        else           gadgetCrosshair?.RestoreForGadget();
    }

    private void ResumeGame()
    {
        _isPaused = false;
        pausePanel?.SetActive(false);
        Time.timeScale = 1f;
        gadgetCrosshair?.RestoreForGadget();
    }

    private void OpenOptions()  { pausePanel?.SetActive(false); optionsPanel?.SetActive(true); }
    private void CloseOptions() { optionsPanel?.SetActive(false); pausePanel?.SetActive(true); }

    private void RetryFromSave()
    {
        Time.timeScale = 1f;
        gameOverPanel?.SetActive(false);
        gadgetCrosshair?.RestoreForGadget();
        SaveManager.Instance?.Load();
    }
}
