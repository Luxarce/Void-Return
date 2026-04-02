using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Detects when the player enters the module's interaction zone and handles the E key.
///
/// HOW REPAIR WORKS:
///  This script calls module.AttemptRepair() DIRECTLY on the [E] key press.
///  No UnityEvent wiring is needed — the connection is made in code via
///  GetComponent<ShipModule>() in Awake().
///
///  For Engine Core, pressing [E] when Stage 1 is complete and the module is
///  not yet fully repaired gives the player a CHOICE:
///    - If thruster needs fuel: refuel first, then on the next press: repair.
///    - This is handled by choosing RefuelThrusterFromEngineCore() vs AttemptRepair().
///
/// SETUP:
///  1. Add ProximityInteraction to each module's repair point GameObject.
///  2. Make sure that GameObject also has ShipModule and a trigger Collider2D.
///  3. No Inspector event wiring needed.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ProximityInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("Key the player presses to interact.")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("Optional world-space UI shown while player is in range.")]
    public GameObject interactPromptUI;

    [Tooltip("Re-evaluates the prompt text every N seconds while player is nearby.")]
    [Range(0.5f, 5f)]
    public float promptRefreshInterval = 1f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   interactClip;

    [Header("Optional Fallback Event")]
    [Tooltip("Fires when the player presses [E] in range. " +
             "Only needed if this object has NO ShipModule — the script calls " +
             "module.AttemptRepair() directly when a ShipModule is present.")]
    public UnityEvent onInteract;

    // ─────────────────────────────────────────────────────────────────────────
    private bool       _playerInRange;
    private ShipModule _module;
    private float      _refreshTimer;

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

        if (Input.GetKeyDown(interactKey))
            HandleInteraction();

        // Refresh prompt text periodically so [READY] appears automatically
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= promptRefreshInterval)
        {
            _refreshTimer = 0f;
            RefreshPromptText(quiet: true);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = true;
        _refreshTimer  = 0f;
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
            // Engine Core special: if stage 1 done but not fully repaired,
            // first press refuels thruster; second press continues repair.
            if (_module.moduleType == ModuleType.EngineCore &&
                _module.Stage1Complete && !_module.IsFullyRepaired)
            {
                var thruster = FindFirstObjectByType<ThrusterPack>();
                if (thruster != null && thruster.FuelNormalized < 0.99f)
                {
                    _module.RefuelThrusterFromEngineCore();
                    return;   // refueled this press — repair on next press
                }
            }

            _module.AttemptRepair();
        }
        else
        {
            onInteract?.Invoke();
        }

        RefreshPromptText(quiet: true);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshPromptText(bool quiet)
    {
        string text = _module != null
            ? _module.GetProximityPrompt()
            : "Press [E] to interact";

        if (!quiet)
            NotificationManager.Instance?.ShowInfo(text);

        // Update the world-space prompt UI if one is assigned
        if (interactPromptUI != null)
        {
            var tmp = interactPromptUI.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;
        }
    }
}
