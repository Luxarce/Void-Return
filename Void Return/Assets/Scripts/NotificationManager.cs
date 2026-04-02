using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Multi-channel notification system.
///
/// Three separate notification panels, each for a different urgency level:
///
///  ShowPickup(msg)  — Bottom of screen. Small, brief. For material collection.
///                     Color: soft green/cyan. Auto-dismisses in 2s.
///
///  Show(msg)        — Center-lower screen. Standard info. Module ready,
///                     repair stage complete, zone entry, etc.
///                     Color: white. Dismisses in 3s.
///
///  ShowWarning(msg) — Top-center screen. Urgent/danger. Meteorite incoming,
///                     critical oxygen, ship hit. Color: red/orange. Dismisses in 4s.
///
/// Each channel has its own CanvasGroup so they can coexist on screen at the same time.
///
/// SETUP:
///  Create three separate Panel objects in the UI Canvas:
///    PickupNotifPanel    — bottom-center, small
///    InfoNotifPanel      — center-lower, medium
///    WarningNotifPanel   — top-center, large
///  Each panel needs: TextMeshProUGUI child + CanvasGroup on the panel itself.
///  Assign all six references in the Inspector.
/// </summary>
public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields — three independent panels
    // ─────────────────────────────────────────────────────────────────────────

    [Header("PICKUP channel (bottom screen — material collection)")]
    [Tooltip("TextMeshProUGUI inside the pickup notification panel.")]
    public TextMeshProUGUI pickupText;

    [Tooltip("CanvasGroup on the pickup notification panel.")]
    public CanvasGroup pickupGroup;

    [Tooltip("Color of pickup notifications (soft green/cyan).")]
    public Color pickupColor = new Color(0.4f, 1f, 0.7f);

    [Tooltip("How long pickup notifications stay visible.")]
    public float pickupDuration = 2f;

    [Header("INFO channel (center-lower — general information)")]
    [Tooltip("TextMeshProUGUI inside the info notification panel.")]
    public TextMeshProUGUI infoText;

    [Tooltip("CanvasGroup on the info notification panel.")]
    public CanvasGroup infoGroup;

    [Tooltip("Color of info notifications (white).")]
    public Color infoColor = Color.white;

    [Tooltip("How long info notifications stay visible.")]
    public float infoDuration = 3f;

    [Header("WARNING channel (top screen — danger and urgency)")]
    [Tooltip("TextMeshProUGUI inside the warning notification panel.")]
    public TextMeshProUGUI warningText;

    [Tooltip("CanvasGroup on the warning notification panel.")]
    public CanvasGroup warningGroup;

    [Tooltip("Color of warning notifications (red/orange).")]
    public Color warningColor = new Color(1f, 0.25f, 0.1f);

    [Tooltip("How long warning notifications stay visible.")]
    public float warningDuration = 4f;

    [Header("Shared Timing")]
    [Tooltip("Fade in/out duration for all channels.")]
    public float fadeDuration = 0.3f;

    // ─────────────────────────────────────────────────────────────────────────
    private Coroutine _pickupRoutine;
    private Coroutine _infoRoutine;
    private Coroutine _warningRoutine;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (pickupGroup  != null) pickupGroup.alpha  = 0f;
        if (infoGroup    != null) infoGroup.alpha    = 0f;
        if (warningGroup != null) warningGroup.alpha = 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows a small pickup notification at the bottom of the screen.
    /// Use for: material collected, oxygen canister, item pickup.
    /// </summary>
    public void ShowPickup(string message)
    {
        if (_pickupRoutine != null) StopCoroutine(_pickupRoutine);
        _pickupRoutine = StartCoroutine(
            ShowChannel(pickupGroup, pickupText, message, pickupColor, pickupDuration));
    }

    /// <summary>
    /// Shows a standard info notification.
    /// Use for: module ready to repair, repair stage complete, zone entry, gadget unlock.
    /// BACKWARD COMPATIBLE: maps to the info channel (replaces the old Show() method).
    /// </summary>
    public void Show(string message, bool urgent = false)
    {
        if (urgent)
            ShowWarning(message);
        else
            ShowInfo(message);
    }

    /// <summary>
    /// Shows a centered info notification.
    /// Use for: module ready, repair progress, zone notifications, gadget status.
    /// </summary>
    public void ShowInfo(string message)
    {
        if (_infoRoutine != null) StopCoroutine(_infoRoutine);
        _infoRoutine = StartCoroutine(
            ShowChannel(infoGroup, infoText, message, infoColor, infoDuration));
    }

    /// <summary>
    /// Shows a top-screen warning notification.
    /// Use for: meteorite incoming, critical oxygen, player hit, module damaged.
    /// </summary>
    public void ShowWarning(string message)
    {
        if (_warningRoutine != null) StopCoroutine(_warningRoutine);
        _warningRoutine = StartCoroutine(
            ShowChannel(warningGroup, warningText, message, warningColor, warningDuration));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator ShowChannel(CanvasGroup group, TextMeshProUGUI text,
                                     string message, Color color, float duration)
    {
        if (text  != null) { text.text  = message; text.color = color; }
        if (group == null) yield break;

        // Fade in
        float t = 0f;
        while (t < fadeDuration)
        {
            group.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        group.alpha = 1f;

        // Hold
        yield return new WaitForSecondsRealtime(duration);

        // Fade out
        t = 0f;
        while (t < fadeDuration)
        {
            group.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        group.alpha = 0f;
    }
}
