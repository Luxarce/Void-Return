using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the Main Menu scene.
/// Handles panel switching between Main Menu and Options,
/// wires all buttons and sliders to their respective systems,
/// and manages scene loading into the game.
///
/// Attach to an empty GameObject in the Main Menu scene.
/// Wire all UI references in the Inspector.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Panels")]
    [Tooltip("The root panel containing the main menu buttons.")]
    public GameObject mainMenuPanel;

    [Tooltip("The options sub-panel with audio sliders and toggles.")]
    public GameObject optionsPanel;

    [Header("Main Menu Buttons")]
    [Tooltip("Button to start the game and load the GameScene.")]
    public Button startButton;

    [Tooltip("Button to open the options panel.")]
    public Button optionsButton;

    [Tooltip("Button to continue from an existing save file.")]
    public Button continueButton;

    [Tooltip("Button to quit the application.")]
    public Button exitButton;

    [Header("Title / Version")]
    [Tooltip("Optional text label for the game version number.")]
    public TextMeshProUGUI versionLabel;

    [Tooltip("Version string to display (e.g., 'v1.0.0').")]
    public string versionString = "v1.0";

    [Header("Options Panel")]
    [Tooltip("Slider for background music volume (0 to 1).")]
    public Slider musicVolumeSlider;

    [Tooltip("Slider for sound effects volume (0 to 1).")]
    public Slider sfxVolumeSlider;

    [Tooltip("Toggle to mute/unmute music entirely.")]
    public Toggle musicToggle;

    [Tooltip("Toggle to mute/unmute all sound effects.")]
    public Toggle sfxToggle;

    [Tooltip("Button to close the options panel and return to the main menu.")]
    public Button optionsBackButton;

    [Header("Scene Names")]
    [Tooltip("Exact name of the game scene to load when Start is pressed.")]
    public string gameSceneName = "GameScene";

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Initial panel state
        mainMenuPanel?.SetActive(true);
        optionsPanel?.SetActive(false);

        // Set version label
        if (versionLabel != null)
            versionLabel.text = versionString;

        // Wire main menu buttons
        startButton?.onClick.AddListener(StartGame);
        optionsButton?.onClick.AddListener(OpenOptions);
        exitButton?.onClick.AddListener(ExitGame);
        continueButton?.onClick.AddListener(ContinueGame);

        // Options back button
        optionsBackButton?.onClick.AddListener(CloseOptions);

        // Wire audio sliders / toggles to AudioManager
        musicVolumeSlider?.onValueChanged.AddListener(v => AudioManager.Instance?.SetMusicVolume(v));
        sfxVolumeSlider?.onValueChanged.AddListener(v => AudioManager.Instance?.SetSFXVolume(v));
        musicToggle?.onValueChanged.AddListener(b => AudioManager.Instance?.ToggleMusic(b));
        sfxToggle?.onValueChanged.AddListener(b => AudioManager.Instance?.ToggleSFX(b));

        // Load saved audio preferences into sliders
        if (AudioManager.Instance != null)
        {
            if (musicVolumeSlider != null) musicVolumeSlider.value = AudioManager.Instance.MusicVolume;
            if (sfxVolumeSlider   != null) sfxVolumeSlider.value   = AudioManager.Instance.SFXVolume;
        }

        // Check if a save file exists to enable/disable Continue
        bool hasSave = PlayerPrefs.HasKey("VoidReturn_Save");
        if (continueButton != null)
            continueButton.interactable = hasSave;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button Handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void StartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
    }

    private void ContinueGame()
    {
        // Load scene then SaveManager will load data after scene is ready
        PlayerPrefs.SetInt("LoadOnStart", 1);
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
    }

    private void OpenOptions()
    {
        mainMenuPanel?.SetActive(false);
        optionsPanel?.SetActive(true);
    }

    private void CloseOptions()
    {
        optionsPanel?.SetActive(false);
        mainMenuPanel?.SetActive(true);
    }

    private void ExitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
