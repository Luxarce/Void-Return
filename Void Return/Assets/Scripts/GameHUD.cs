using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game HUD.
///
/// UPGRADE LIST FIX:
///  Rich text color tags like <color=#27AE60> may not render if the TMP font
///  does not have rich text enabled OR if the font SDF doesn't support the
///  full Unicode range for special characters.
///
///  This version uses plain-text symbols instead:
///    Completed:  [OK] (green via a separate TMP vertex color if using TextMeshPro)
///    Incomplete: [ ]
///  If you want color, enable Rich Text on the UpgradeListText TMP component
///  in the Inspector. The tags are still included but guarded by a toggle.
///
///  IMMEDIATE UPDATE: RefreshUpgradeList() is now called both by
///  onProgressChanged AND directly at the end of AttemptRepair() in ShipModule.
///  This ensures the panel updates the moment a stage completes.
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

    [Header("Zone Badge")]
    public Image           zoneBadgeImage;
    public TextMeshProUGUI zoneNameText;
    public Color zone1Color       = new Color(0f, 1f, 0.5f, 0.9f);
    public Color zone2Color       = new Color(1f, 0.85f, 0f, 0.9f);
    public Color zone3Color       = new Color(1f, 0.2f, 0.1f, 0.9f);
    public Color defaultZoneColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

    [Header("Module Upgrade Status List")]
    [Tooltip("TextMeshProUGUI showing all stage upgrades. " +
             "Enable 'Rich Text' on this TMP component to see colored [OK] markers. " +
             "If rich text is disabled, plain [OK] / [ ] text is shown instead.")]
    public TextMeshProUGUI upgradeListText;

    [Tooltip("If true, uses color tags for done/pending status. " +
             "Requires Rich Text = ON on the upgradeListText TMP component.")]
    public bool useRichTextColors = true;

    [Header("Module References (for upgrade list)")]
    public ShipModule lifeSupportModule;
    public ShipModule hullPlatingModule;
    public ShipModule navigationModule;
    public ShipModule engineCoreModule;

    [Header("Gadget Bars")]
    public Slider          bootsBar;
    public Slider          thrusterBar;
    public TextMeshProUGUI grenadeCountText;

    [Header("Pause Menu")]
    public GameObject pausePanel;
    public Button     resumeButton, saveButton, loadButton, optionsButton, exitButton;

    [Header("Options")]
    public GameObject optionsPanel;
    public Slider     masterVolumeSlider, musicVolumeSlider, sfxVolumeSlider;
    public Toggle     musicToggle, sfxToggle;
    public Button     optionsBackButton;

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public Button     gameOverReturnButton, gameOverRetryButton;

    [Header("Victory Panel")]
    public GameObject victoryPanel;
    public Button     victoryReturnButton;

    [Header("Crosshair")]
    public GadgetCrosshair gadgetCrosshair;

    [Header("Scene")]
    public string mainMenuSceneName = "MainMenu";

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
        WireOptions();
        WireGameOver();
        WireVictory();
        RefreshUpgradeList();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
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
        if (gravityStateText != null)
            gravityStateText.text = (int)state < names.Length ? names[(int)state] : state.ToString();
    }

    public void SetZone(int zoneNumber, string zoneName)
    {
        if (zoneNameText   != null) zoneNameText.text  = zoneName;
        if (zoneBadgeImage != null)
            zoneBadgeImage.color = zoneNumber switch
            { 1 => zone1Color, 2 => zone2Color, 3 => zone3Color, _ => defaultZoneColor };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Upgrade List
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the upgrade status text panel.
    /// Called immediately when onProgressChanged fires (wired by GameManager).
    /// </summary>
    public void RefreshUpgradeList()
    {
        if (upgradeListText == null) return;

        var sb = new System.Text.StringBuilder();

        AppendModuleBlock(sb, "Life Support", lifeSupportModule, new[]
        {
            "Stage 1: Zone + Shield + Oxygen +50%",
            "Stage 2: Oxygen +50%  |  Refill x2  |  Boots +50%",
        });
        AppendModuleBlock(sb, "Navigation", navigationModule, new[]
        {
            "Stage 1: Tether Gun + Partial Minimap",
            "Stage 2: Full Minimap  |  Tether range x2",
        });
        AppendModuleBlock(sb, "Hull Plating", hullPlatingModule, new[]
        {
            "Stage 1: Gravity Grenade + Crafting",
            "Stage 2: Pull radius x2  |  +2 slots  |  Cost -1",
        });
        AppendModuleBlock(sb, "Engine Core", engineCoreModule, new[]
        {
            "Stage 1: Thruster Pack activated",
            "Stage 2: Fuel capacity x2",
        });

        upgradeListText.text = sb.ToString().TrimEnd();
    }

    private void AppendModuleBlock(System.Text.StringBuilder sb,
        string displayName, ShipModule module, string[] stageDescriptions)
    {
        if (useRichTextColors)
            sb.AppendLine($"<b>{displayName}</b>");
        else
            sb.AppendLine($"-- {displayName} --");

        if (module == null)
        {
            sb.AppendLine("  [Not assigned]");
            return;
        }

        for (int i = 0; i < stageDescriptions.Length; i++)
        {
            bool done = module.CurrentStageIndex > i || module.IsFullyRepaired;

            if (useRichTextColors)
            {
                string color  = done ? "#27AE60" : "#888888";
                string status = done ? "[OK]" : "[  ]";
                sb.AppendLine($"  <color={color}>{status}</color> {stageDescriptions[i]}");
            }
            else
            {
                string status = done ? "[OK]" : "[  ]";
                sb.AppendLine($"  {status} {stageDescriptions[i]}");
            }
        }
        sb.AppendLine();
    }

    // Backward compat — GameManager wires onProgressChanged to these
    public void UpdateModuleProgress(int idx, float p) => RefreshUpgradeList();
    public void UpdateLifeSupportProgress(float p) => RefreshUpgradeList();
    public void UpdateHullProgress(float p)         => RefreshUpgradeList();
    public void UpdateNavProgress(float p)          => RefreshUpgradeList();
    public void UpdateEngineProgress(float p)       => RefreshUpgradeList();

    // ─────────────────────────────────────────────────────────────────────────

    public void ShowGameOver()
    {
        Time.timeScale = 0f;
        gameOverPanel?.SetActive(true);
        gadgetCrosshair?.HideForPanel();
    }

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
        _isPaused = false; pausePanel?.SetActive(false); Time.timeScale = 1f;
        gadgetCrosshair?.RestoreForGadget();
    }

    private void RetryFromSave()
    {
        Time.timeScale = 1f; gameOverPanel?.SetActive(false);
        gadgetCrosshair?.RestoreForGadget();
        SaveManager.Instance?.Load();
    }

    private void WirePauseButtons()
    {
        resumeButton?.onClick.AddListener(ResumeGame);
        saveButton?.onClick.AddListener(()   => SaveManager.Instance?.Save());
        loadButton?.onClick.AddListener(()   => SaveManager.Instance?.Load());
        optionsButton?.onClick.AddListener(() => { pausePanel?.SetActive(false); optionsPanel?.SetActive(true); });
        exitButton?.onClick.AddListener(ExitToMainMenu);
    }

    private void WireOptions()
    {
        optionsBackButton?.onClick.AddListener(() => { optionsPanel?.SetActive(false); pausePanel?.SetActive(true); });
        masterVolumeSlider?.onValueChanged.AddListener(v => AudioManager.Instance?.SetMasterVolume(v));
        musicVolumeSlider?.onValueChanged.AddListener(v  => AudioManager.Instance?.SetMusicVolume(v));
        sfxVolumeSlider?.onValueChanged.AddListener(v    => AudioManager.Instance?.SetSFXVolume(v));
        musicToggle?.onValueChanged.AddListener(b        => AudioManager.Instance?.ToggleMusic(b));
        sfxToggle?.onValueChanged.AddListener(b          => AudioManager.Instance?.ToggleSFX(b));
    }

    private void WireGameOver()
    {
        gameOverReturnButton?.onClick.AddListener(ExitToMainMenu);
        gameOverRetryButton?.onClick.AddListener(RetryFromSave);
    }

    private void WireVictory() { victoryReturnButton?.onClick.AddListener(ExitToMainMenu); }
}
