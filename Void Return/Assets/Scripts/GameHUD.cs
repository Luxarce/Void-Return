using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls all in-game HUD elements.
///
/// FIX NOTES (v2):
/// ─────────────────────────────────────────────────────────────────────────
/// METHODS ADDED:
///   — ExitToMainMenu()   : public, no parameters. Wire to Exit button event.
///   — ShowGameOver()     : public, no parameters. Wire to OxygenSystem.onOxygenDepleted.
///   — UpdateModuleProgress(int, float) already existed but is now also
///     available as four individual no-parameter convenience wrappers so that
///     each ShipModule can wire onModuleRepaired without needing index logic:
///       UpdateLifeSupportProgress(float)
///       UpdateHullProgress(float)
///       UpdateNavProgress(float)
///       UpdateEngineProgress(float)
///     Wire each module's onProgressChanged → its own matching wrapper.
/// ─────────────────────────────────────────────────────────────────────────
///
/// WIRING GUIDE (see Section 6 of Solutions Supplement for full detail):
///   OxygenSystem.onOxygenChanged     → GameHUD.UpdateOxygen
///   OxygenSystem.onOxygenDepleted    → GameHUD.ShowGameOver
///   LifeSupport.onProgressChanged    → GameHUD.UpdateLifeSupportProgress
///   HullPlating.onProgressChanged    → GameHUD.UpdateHullProgress
///   Navigation.onProgressChanged     → GameHUD.UpdateNavProgress
///   EngineCore.onProgressChanged     → GameHUD.UpdateEngineProgress
///   PauseMenu Exit button.onClick    → GameHUD.ExitToMainMenu
///
/// Attach to an empty child GameObject inside the Game Canvas.
/// </summary>
public class GameHUD : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Oxygen Meter")]
    [Tooltip("Slider UI element for the oxygen bar. Min=0, Max=1.")]
    public Slider oxygenSlider;

    [Tooltip("The fill Image inside the oxygen slider (for color changes).")]
    public Image oxygenFillImage;

    [Tooltip("Text label displaying the oxygen percentage (e.g., '72%').")]
    public TextMeshProUGUI oxygenLabel;

    [Tooltip("Oxygen fill color when above the warning threshold.")]
    public Color oxygenColorNormal = new Color(0f, 0.9f, 1f);

    [Tooltip("Oxygen fill color in the warning range.")]
    public Color oxygenColorWarning = Color.yellow;

    [Tooltip("Oxygen fill color in the critical range.")]
    public Color oxygenColorCritical = Color.red;

    [Tooltip("Normalized threshold below which the warning color activates. " +
             "Match OxygenSystem.warningThreshold.")]
    [Range(0.05f, 0.5f)]
    public float oxygenWarningLevel = 0.30f;

    [Tooltip("Normalized threshold below which the critical color activates. " +
             "Match OxygenSystem.criticalThreshold.")]
    [Range(0.01f, 0.2f)]
    public float oxygenCriticalLevel = 0.10f;

    [Header("Gravity State Indicator")]
    [Tooltip("Text label showing current gravity state name.")]
    public TextMeshProUGUI gravityStateText;

    [Tooltip("Icon image for the gravity state.")]
    public Image gravityStateIcon;

    [Tooltip("Sprites for each state: [0]=Normal, [1]=ZeroG, [2]=MicroPull, [3]=Rift.")]
    public Sprite[] gravityStateSprites;

    [Header("Zone Badge")]
    [Tooltip("Background image that changes color per zone.")]
    public Image zoneBadgeImage;

    [Tooltip("Text showing the zone name.")]
    public TextMeshProUGUI zoneNameText;

    public Color zone1Color = new Color(0f, 1f, 0.5f, 0.8f);
    public Color zone2Color = new Color(1f, 0.85f, 0f, 0.8f);
    public Color zone3Color = new Color(1f, 0.2f, 0.1f, 0.8f);

    [Header("Module Progress Bars")]
    [Tooltip("Slider[0]=LifeSupport, [1]=HullPlating, [2]=Navigation, [3]=EngineCore.")]
    public Slider[] moduleProgressSliders;

    [Tooltip("Label text per module slider.")]
    public TextMeshProUGUI[] moduleProgressLabels;

    [Header("Pause Menu Panel")]
    [Tooltip("Root panel of the pause overlay. Toggled by Escape key.")]
    public GameObject pausePanel;

    public Button resumeButton;
    public Button saveButton;
    public Button loadButton;
    public Button optionsButton;
    public Button exitButton;

    [Header("Options Sub-Panel (inside Pause)")]
    public GameObject optionsPanel;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public Toggle musicToggle;
    public Toggle sfxToggle;
    public Button optionsBackButton;

    [Header("Mission / Inventory Panel")]
    [Tooltip("Sliding panel shown with Tab key.")]
    public GameObject missionPanel;

    [Tooltip("Parent transform for inventory slot prefabs (use a Grid Layout Group).")]
    public Transform inventoryGridParent;

    [Tooltip("Prefab with Icon (Image child) and Count (TextMeshProUGUI child).")]
    public GameObject inventoryItemPrefab;

    [Header("Game Over Panel")]
    [Tooltip("Full-screen Game Over overlay panel. Set inactive by default.")]
    public GameObject gameOverPanel;

    [Tooltip("Button on the Game Over panel that returns to the Main Menu.")]
    public Button gameOverReturnButton;

    [Tooltip("Button on the Game Over panel that restarts from the last save.")]
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
        // Hide all overlay panels at game start
        pausePanel?.SetActive(false);
        optionsPanel?.SetActive(false);
        missionPanel?.SetActive(false);
        gameOverPanel?.SetActive(false);

        // Wire pause menu buttons
        resumeButton?.onClick.AddListener(ResumeGame);
        saveButton?.onClick.AddListener(() => SaveManager.Instance?.Save());
        loadButton?.onClick.AddListener(() => SaveManager.Instance?.Load());
        optionsButton?.onClick.AddListener(OpenOptions);
        exitButton?.onClick.AddListener(ExitToMainMenu);

        // Wire options panel
        optionsBackButton?.onClick.AddListener(CloseOptions);
        musicVolumeSlider?.onValueChanged.AddListener(v => AudioManager.Instance?.SetMusicVolume(v));
        sfxVolumeSlider?.onValueChanged.AddListener(v => AudioManager.Instance?.SetSFXVolume(v));
        musicToggle?.onValueChanged.AddListener(b => AudioManager.Instance?.ToggleMusic(b));
        sfxToggle?.onValueChanged.AddListener(b => AudioManager.Instance?.ToggleSFX(b));

        // Wire game over panel buttons
        gameOverReturnButton?.onClick.AddListener(ExitToMainMenu);
        gameOverRetryButton?.onClick.AddListener(RetryFromSave);

        // Subscribe to inventory changes for automatic panel refresh
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged += RefreshInventory;
    }

    private void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= RefreshInventory;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
        if (Input.GetKeyDown(KeyCode.Tab))    ToggleMissionPanel();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Wired via Inspector Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the oxygen slider and its fill color.
    /// WIRE TO: OxygenSystem → onOxygenChanged (Dynamic float)
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
    /// Updates the gravity state icon and text.
    /// Called directly from PlayerController — no Inspector wiring needed.
    /// </summary>
    public void SetGravityState(GravityState state)
    {
        string[] names = { "Normal-G", "Zero-G", "Micro-Pull", "GRAVITY RIFT!" };
        int index = (int)state;

        if (gravityStateText != null)
            gravityStateText.text = index < names.Length ? names[index] : state.ToString();

        if (gravityStateIcon != null && gravityStateSprites != null
            && index < gravityStateSprites.Length)
            gravityStateIcon.sprite = gravityStateSprites[index];
    }

    /// <summary>
    /// Updates the zone badge color and name.
    /// WIRE TO: ZoneTrigger → OnTriggerEnter2D indirectly via ZoneTrigger's own call.
    /// Called directly from ZoneTrigger — no Inspector wiring needed.
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
                _ => Color.white
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Module Progress — Individual Wrappers (wire one per module)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates module progress bar by array index.
    /// Use the individual wrappers below for easier Inspector wiring.
    /// </summary>
    public void UpdateModuleProgress(int moduleIndex, float progress)
    {
        if (moduleProgressSliders == null) return;
        if (moduleIndex < 0 || moduleIndex >= moduleProgressSliders.Length) return;
        if (moduleProgressSliders[moduleIndex] != null)
            moduleProgressSliders[moduleIndex].value = progress;
    }

    /// <summary>
    /// WIRE TO: LifeSupport ShipModule → onProgressChanged (Dynamic float)
    /// </summary>
    public void UpdateLifeSupportProgress(float progress) => UpdateModuleProgress(0, progress);

    /// <summary>
    /// WIRE TO: HullPlating ShipModule → onProgressChanged (Dynamic float)
    /// </summary>
    public void UpdateHullProgress(float progress) => UpdateModuleProgress(1, progress);

    /// <summary>
    /// WIRE TO: Navigation ShipModule → onProgressChanged (Dynamic float)
    /// </summary>
    public void UpdateNavProgress(float progress) => UpdateModuleProgress(2, progress);

    /// <summary>
    /// WIRE TO: EngineCore ShipModule → onProgressChanged (Dynamic float)
    /// </summary>
    public void UpdateEngineProgress(float progress) => UpdateModuleProgress(3, progress);

    // ─────────────────────────────────────────────────────────────────────────
    // Game Over
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the Game Over panel when the player runs out of oxygen.
    /// WIRE TO: OxygenSystem → onOxygenDepleted (no parameter)
    /// </summary>
    public void ShowGameOver()
    {
        // Pause time so the game freezes on the game over screen
        Time.timeScale = 0f;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
        else
        {
            // Fallback: if no panel is assigned yet, return to main menu directly
            Debug.LogWarning("[GameHUD] No Game Over panel assigned — returning to Main Menu.");
            ExitToMainMenu();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scene Navigation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns to the Main Menu scene.
    /// WIRE TO: Pause Menu Exit button onClick
    ///          Game Over panel Return button onClick
    /// </summary>
    public void ExitToMainMenu()
    {
        Time.timeScale = 1f;   // Always reset time scale before loading a scene
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Reloads the current game scene from the last save.
    /// WIRE TO: Game Over panel Retry button onClick
    /// </summary>
    public void RetryFromSave()
    {
        Time.timeScale = 1f;
        SaveManager.Instance?.Load();
        gameOverPanel?.SetActive(false);
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
    // Mission / Inventory Panel
    // ─────────────────────────────────────────────────────────────────────────

    private void ToggleMissionPanel()
    {
        bool open = !missionPanel.activeSelf;
        missionPanel?.SetActive(open);
        if (open) RefreshInventory();
    }

    private void RefreshInventory()
    {
        if (inventoryGridParent == null || inventoryItemPrefab == null) return;

        foreach (Transform child in inventoryGridParent)
            Destroy(child.gameObject);

        if (Inventory.Instance == null) return;

        foreach (var item in Inventory.Instance.GetAllItems())
        {
            if (item.quantity <= 0) continue;

            GameObject slot    = Instantiate(inventoryItemPrefab, inventoryGridParent);
            var iconImg        = slot.transform.Find("Icon")?.GetComponent<Image>();
            var countTxt       = slot.transform.Find("Count")?.GetComponent<TextMeshProUGUI>();
            var nameTxt        = slot.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();

            if (iconImg  != null && item.icon != null) iconImg.sprite = item.icon;
            if (countTxt != null) countTxt.text = $"x{item.quantity}";
            if (nameTxt  != null) nameTxt.text  = item.itemName;
        }
    }
}
