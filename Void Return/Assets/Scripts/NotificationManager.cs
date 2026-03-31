using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Singleton that displays short on-screen notification messages.
/// Messages fade in, display, then fade out automatically.
/// Urgent messages appear in red; normal messages in white.
///
/// Place one instance in the Game Canvas (not the World Canvas).
/// Wire the notificationText and notificationGroup references in the Inspector.
/// Use: NotificationManager.Instance.Show("Your message here");
/// </summary>
public class NotificationManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static NotificationManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("UI References")]
    [Tooltip("The TextMeshPro text element that displays the notification message.")]
    public TextMeshProUGUI notificationText;

    [Tooltip("CanvasGroup on the notification panel. Used to fade alpha in and out.")]
    public CanvasGroup notificationGroup;

    [Header("Timing")]
    [Tooltip("How long the notification stays fully visible before fading out.")]
    public float displayDuration = 3f;

    [Tooltip("How long the fade in/out takes in seconds.")]
    public float fadeDuration = 0.4f;

    [Header("Colors")]
    [Tooltip("Text color for standard informational notifications.")]
    public Color normalColor = Color.white;

    [Tooltip("Text color for urgent/danger notifications (meteorite warnings, low oxygen, etc.).")]
    public Color urgentColor = new Color(1f, 0.25f, 0.15f);

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Coroutine _currentCoroutine;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (notificationGroup != null)
            notificationGroup.alpha = 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Display a notification message on screen.
    /// If a notification is already showing, it is immediately replaced.
    /// </summary>
    /// <param name="message">The text to display.</param>
    /// <param name="urgent">If true, displays in the urgent (red) color.</param>
    public void Show(string message, bool urgent = false)
    {
        if (_currentCoroutine != null)
            StopCoroutine(_currentCoroutine);

        _currentCoroutine = StartCoroutine(ShowRoutine(message, urgent));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private Logic
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator ShowRoutine(string message, bool urgent)
    {
        if (notificationText  != null) notificationText.text  = message;
        if (notificationText  != null) notificationText.color = urgent ? urgentColor : normalColor;

        // Fade in
        yield return Fade(0f, 1f, fadeDuration);

        // Hold
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        yield return Fade(1f, 0f, fadeDuration);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (notificationGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            notificationGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            elapsed += Time.unscaledDeltaTime; // Use unscaled so it works when paused
            yield return null;
        }
        notificationGroup.alpha = to;
    }
}
