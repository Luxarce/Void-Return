using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Detects when the player enters a module's interaction zone and handles the [E] key.
///
/// FIX — NOTIFICATION PERSISTS:
///  The notification is now re-shown on a regular interval while the player
///  is inside the zone (showInterval seconds). This ensures the prompt text
///  stays visible and updates dynamically (e.g. showing [READY] once materials
///  are collected) without spamming the notification queue.
///
///  The world-space prompt UI (if assigned) also updates continuously.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ProximityInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("Key the player presses to interact.")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Prompt UI")]
    [Tooltip("Optional world-space UI panel shown above the module while player is in range.")]
    public GameObject interactPromptUI;

    [Tooltip("How often (seconds) the persistent notification re-fires while the player stays in range.")]
    [Range(1f, 10f)]
    public float showInterval = 2.5f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   interactClip;

    [Header("Optional Fallback Event")]
    [Tooltip("Only used when there is no ShipModule on this GameObject.")]
    public UnityEvent onInteract;

    // ─────────────────────────────────────────────────────────────────────────
    private bool       _playerInRange;
    private ShipModule _module;
    private float      _showTimer;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _module = GetComponent<ShipModule>();
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start()
    {
        interactPromptUI?.SetActive(false);
    }

    private void Update()
    {
        if (!_playerInRange) return;

        // Interact key
        if (Input.GetKeyDown(interactKey))
            HandleInteraction();

        // Persistent notification — re-show prompt text every showInterval seconds
        _showTimer -= Time.deltaTime;
        if (_showTimer <= 0f)
        {
            _showTimer = showInterval;
            RefreshPromptText(quiet: false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = true;
        _showTimer     = 0f;             // show immediately on entry
        interactPromptUI?.SetActive(true);
        RefreshPromptText(quiet: false);
        _module?.OnPlayerProximityEnter();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = false;
        interactPromptUI?.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void HandleInteraction()
    {
        audioSource?.PlayOneShot(interactClip);

        if (_module != null)
        {
            // Engine Core: refuel thruster first press if needed
            if (_module.moduleType == ModuleType.EngineCore &&
                _module.Stage1Complete && !_module.IsFullyRepaired)
            {
                var thruster = FindFirstObjectByType<ThrusterPack>();
                if (thruster != null && thruster.FuelNormalized < 0.99f)
                {
                    _module.RefuelThrusterFromEngineCore();
                    return;
                }
            }
            _module.AttemptRepair();
        }
        else
        {
            onInteract?.Invoke();
        }

        // Immediately refresh prompt after interaction
        _showTimer = showInterval;
        RefreshPromptText(quiet: true);
    }

    private void RefreshPromptText(bool quiet)
    {
        string text = _module != null
            ? _module.GetProximityPrompt()
            : "Press [E] to interact";

        // Show notification (not quiet = show in notification channel)
        if (!quiet)
            NotificationManager.Instance?.ShowInfo(text);

        // Always update the world-space prompt UI regardless of quiet flag
        if (interactPromptUI != null)
        {
            var tmp = interactPromptUI.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;
        }
    }
}
