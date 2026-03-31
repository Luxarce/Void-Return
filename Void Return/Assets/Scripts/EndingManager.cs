using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles the game's victory / escape ending sequence.
/// This script is triggered when all four ship modules are fully repaired.
///
/// WIRING:
///   ShipRepairManager → onAllModulesRepaired → EndingManager.TriggerEscapeEnding
///
/// SETUP:
///   1. Create an empty GameObject named 'EndingManager' in the GameScene.
///   2. Attach this script.
///   3. Create a full-screen UI Panel named 'VictoryPanel' (see field list below).
///   4. Assign all references in the Inspector.
///   5. Wire ShipRepairManager.onAllModulesRepaired → EndingManager.TriggerEscapeEnding.
/// </summary>
public class EndingManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Victory UI Panel")]
    [Tooltip("Full-screen panel shown during the ending sequence. Set inactive by default.")]
    public GameObject victoryPanel;

    [Tooltip("Title text that reads something like 'ESCAPE SUCCESSFUL'.")]
    public TextMeshProUGUI victoryTitleText;

    [Tooltip("Subtitle or flavor text shown beneath the title.")]
    public TextMeshProUGUI victorySubtitleText;

    [Tooltip("Button that returns the player to the Main Menu after the ending.")]
    public Button returnToMenuButton;

    [Tooltip("Optional button to start a new game from the victory screen.")]
    public Button playAgainButton;

    [Header("Ending Sequence Settings")]
    [Tooltip("Seconds of delay before the victory panel fades in. " +
             "Use this time to play the ship launch animation / sound.")]
    public float introDelaySeconds = 3f;

    [Tooltip("Seconds for the victory panel to fade from transparent to visible.")]
    public float panelFadeDuration = 1.5f;

    [Tooltip("Title string shown on the victory panel.")]
    public string victoryTitle = "ESCAPE SUCCESSFUL";

    [Tooltip("Subtitle string shown on the victory panel.")]
    public string victorySubtitle =
        "Against all odds, you rebuilt the ship and made it home.\n" +
        "The void did not claim you today.";

    [Header("Victory VFX & Audio")]
    [Tooltip("Particle system that plays during the escape sequence " +
             "(e.g., thruster exhaust on the repaired ship moving off screen).")]
    public ParticleSystem escapeVFX;

    [Tooltip("Sound played when TriggerEscapeEnding is called " +
             "(e.g., engine startup roar).")]
    public AudioClip engineStartClip;

    [Tooltip("Music clip that plays during the victory screen.")]
    public AudioClip victoryMusicClip;

    [Tooltip("Volume of the victory music.")]
    [Range(0f, 1f)]
    public float victoryMusicVolume = 0.8f;

    [Header("Main Menu Scene")]
    [Tooltip("Exact name of the Main Menu scene in Build Settings.")]
    public string mainMenuSceneName = "MainMenu";

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private CanvasGroup _victoryCanvasGroup;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Ensure victory panel is hidden at game start
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);

            // Get or add a CanvasGroup for alpha fading
            _victoryCanvasGroup = victoryPanel.GetComponent<CanvasGroup>();
            if (_victoryCanvasGroup == null)
                _victoryCanvasGroup = victoryPanel.AddComponent<CanvasGroup>();

            _victoryCanvasGroup.alpha = 0f;
        }

        // Wire buttons
        returnToMenuButton?.onClick.AddListener(ReturnToMainMenu);
        playAgainButton?.onClick.AddListener(PlayAgain);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers the full escape ending sequence.
    ///
    /// HOW TO WIRE IN INSPECTOR:
    ///   1. Select the ShipRepairManager GameObject in the Hierarchy.
    ///   2. In the Inspector, find the 'On All Modules Repaired' event panel.
    ///   3. Click the + button to add a new listener row.
    ///   4. Drag the EndingManager GameObject into the Object field.
    ///   5. Click the Function dropdown → EndingManager → TriggerEscapeEnding.
    ///   6. The method takes no parameters — select the no-parameter version.
    /// </summary>
    public void TriggerEscapeEnding()
    {
        StartCoroutine(EscapeSequence());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ending Sequence Coroutine
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator EscapeSequence()
    {
        // ── Phase 1: Announcement ──────────────────────────────────────────
        NotificationManager.Instance?.Show(
            "ALL MODULES REPAIRED — Initiating escape sequence!", urgent: false);

        // Play engine startup sound
        if (engineStartClip != null)
            AudioSource.PlayClipAtPoint(engineStartClip, Camera.main.transform.position);

        // Play escape VFX (ship thruster particles)
        escapeVFX?.Play();

        // ── Phase 2: Intro delay (ship appears to launch) ─────────────────
        yield return new WaitForSeconds(introDelaySeconds);

        // ── Phase 3: Switch to victory music ──────────────────────────────
        if (victoryMusicClip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
            // Play victory music directly via AudioManager's music source
            if (AudioManager.Instance.musicSource != null)
            {
                AudioManager.Instance.musicSource.clip   = victoryMusicClip;
                AudioManager.Instance.musicSource.volume = victoryMusicVolume;
                AudioManager.Instance.musicSource.loop   = false;
                AudioManager.Instance.musicSource.Play();
            }
        }

        // ── Phase 4: Show victory panel ───────────────────────────────────
        if (victoryPanel != null)
        {
            // Set text content
            if (victoryTitleText    != null) victoryTitleText.text    = victoryTitle;
            if (victorySubtitleText != null) victorySubtitleText.text = victorySubtitle;

            // Activate the panel (it starts transparent via CanvasGroup)
            victoryPanel.SetActive(true);

            // Fade in
            float elapsed = 0f;
            while (elapsed < panelFadeDuration)
            {
                if (_victoryCanvasGroup != null)
                    _victoryCanvasGroup.alpha = elapsed / panelFadeDuration;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_victoryCanvasGroup != null) _victoryCanvasGroup.alpha = 1f;
        }

        // Pause game time so the player can read the screen (optional — remove if unwanted)
        Time.timeScale = 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button Handlers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns to the Main Menu. Wire to the Return to Menu button on the Victory Panel.
    /// </summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Restarts the game from scratch (reloads the GameScene).
    /// Wire to the Play Again button on the Victory Panel.
    /// </summary>
    public void PlayAgain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
