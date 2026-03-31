using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Detects when the player enters a trigger area and shows an interaction prompt.
/// On pressing the interact key (default: E), it fires the onInteract UnityEvent.
///
/// Attach to any ShipModule GameObject alongside a Trigger Collider2D.
/// Wire onInteract → ShipModule.AttemptRepair() in the Inspector,
/// or wire it to any custom method (e.g., door open, cutscene trigger).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ProximityInteraction : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Interaction Settings")]
    [Tooltip("The key the player must press to trigger the interaction.")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("Optional world-space UI GameObject shown as an 'E to interact' prompt. " +
             "Place this as a child of this GameObject and set it inactive by default.")]
    public GameObject interactPromptUI;

    [Tooltip("Optional label text to show in the prompt (e.g., 'Repair Life Support').")]
    public string promptText = "Press [E] to interact";

    [Tooltip("If true, shows a notification banner when the player enters range.")]
    public bool showNotificationOnEnter = true;

    [Header("Event — Wire to ShipModule.AttemptRepair() or any method")]
    [Tooltip("Fired when the player is in range and presses the interact key.")]
    public UnityEvent onInteract;

    [Header("Audio")]
    [Tooltip("AudioSource on this GameObject (optional).")]
    public AudioSource audioSource;

    [Tooltip("Sound played when the interaction is triggered.")]
    public AudioClip interactClip;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private bool _playerInRange;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Ensure the prompt starts hidden
        interactPromptUI?.SetActive(false);

        // Make sure the collider on this GameObject is a trigger
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Update()
    {
        if (!_playerInRange) return;

        if (Input.GetKeyDown(interactKey))
            TriggerInteraction();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Trigger Detection
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInRange = true;
        interactPromptUI?.SetActive(true);

        if (showNotificationOnEnter)
            NotificationManager.Instance?.Show(promptText);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInRange = false;
        interactPromptUI?.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Interaction
    // ─────────────────────────────────────────────────────────────────────────

    private void TriggerInteraction()
    {
        audioSource?.PlayOneShot(interactClip);
        onInteract?.Invoke();
    }
}
