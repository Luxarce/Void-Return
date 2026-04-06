using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles the victory / escape ending sequence.
/// Completely double-call safe. If victoryPanel is missing, falls back to GameHUD.
/// </summary>
public class EndingManager : MonoBehaviour
{
    [Header("Victory UI Panel")]
    [Tooltip("Create a UI Panel, set it INACTIVE by default, then drag it here.")]
    public GameObject victoryPanel;
    public TextMeshProUGUI victoryTitleText;
    public TextMeshProUGUI victorySubtitleText;
    public Button          returnToMenuButton;
    public Button          playAgainButton;

    [Header("Timing")]
    public float introDelaySeconds = 3f;
    public float panelFadeDuration = 1.5f;

    [Header("Text")]
    public string victoryTitle    = "ESCAPE SUCCESSFUL";
    public string victorySubtitle = "Against all odds, you rebuilt the ship and made it home.\nThe void did not claim you today.";

    [Header("VFX and Audio")]
    public ParticleSystem escapeVFX;
    public AudioClip      engineStartClip;
    public AudioClip      victoryMusicClip;
    [Range(0f, 1f)] public float victoryMusicVolume = 0.8f;

    [Header("Scene")]
    public string mainMenuSceneName = "MainMenu";

    // ─────────────────────────────────────────────────────────────────────────
    private bool        _hasTriggered;
    private CanvasGroup _cg;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
            _cg = victoryPanel.GetComponent<CanvasGroup>() ?? victoryPanel.AddComponent<CanvasGroup>();
            _cg.alpha = 0f;
        }
        returnToMenuButton?.onClick.AddListener(ReturnToMainMenu);
        playAgainButton?.onClick.AddListener(PlayAgain);
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void TriggerEscapeEnding()
    {
        if (_hasTriggered) { Debug.Log("[EndingManager] Already triggered — ignoring."); return; }
        _hasTriggered = true;
        Debug.Log("[EndingManager] TriggerEscapeEnding — starting sequence.");

        if (victoryPanel == null)
        {
            Debug.LogError("[EndingManager] victoryPanel is NULL.\n" +
                "FIX: In the Hierarchy, right-click Canvas > UI > Panel.\n" +
                "     Name it VictoryPanel. UNTICK its active checkbox in the Inspector.\n" +
                "     Drag it into EndingManager > Victory Panel field.\n" +
                "     Falling back to GameHUD.ShowVictory().");
            FindFirstObjectByType<GameHUD>()?.ShowVictory();
            return;
        }

        StartCoroutine(EscapeSequence());
    }

    private IEnumerator EscapeSequence()
    {
        NotificationManager.Instance?.ShowWarning("ALL MODULES REPAIRED — Initiating escape sequence!");
        if (engineStartClip != null)
            AudioSource.PlayClipAtPoint(engineStartClip, Camera.main.transform.position);
        escapeVFX?.Play();

        yield return new WaitForSecondsRealtime(introDelaySeconds);

        if (victoryMusicClip != null && AudioManager.Instance?.musicSource != null)
        {
            AudioManager.Instance.StopMusic();
            AudioManager.Instance.musicSource.clip   = victoryMusicClip;
            AudioManager.Instance.musicSource.volume = victoryMusicVolume;
            AudioManager.Instance.musicSource.loop   = false;
            AudioManager.Instance.musicSource.Play();
        }

        if (victoryTitleText    != null) victoryTitleText.text    = victoryTitle;
        if (victorySubtitleText != null) victorySubtitleText.text = victorySubtitle;

        victoryPanel.SetActive(true);
        if (_cg != null) _cg.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < panelFadeDuration)
        {
            if (_cg != null) _cg.alpha = Mathf.Clamp01(elapsed / panelFadeDuration);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (_cg != null) _cg.alpha = 1f;

        Time.timeScale = 0f;
    }

    public void ReturnToMainMenu() { Time.timeScale = 1f; SceneManager.LoadScene(mainMenuSceneName); }
    public void PlayAgain()        { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
}
