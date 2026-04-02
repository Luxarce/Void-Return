using UnityEngine;

/// <summary>
/// Attach this to the Life Support module repair point.
/// When the player stands inside this zone, their oxygen slowly refills
/// (as long as Life Support has had at least Stage 1 repaired).
///
/// SETUP:
///  1. Select the LifeSupport repair point GameObject.
///  2. Add Component → LifeSupportZone.
///  3. Add a second CircleCollider2D set to Is Trigger = ON.
///     Set the radius to cover the repair area (e.g., 4 units).
///  4. Assign the lifeSupportModule field (drag the ShipModule component).
///  5. No other setup needed — oxygen refill is automatic when player is inside.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LifeSupportZone : MonoBehaviour
{
    [Header("Life Support Reference")]
    [Tooltip("The ShipModule component on this Life Support repair point. " +
             "Oxygen only refills if this module has been at least partially repaired.")]
    public ShipModule lifeSupportModule;

    [Header("Refill Settings")]
    [Tooltip("If true, oxygen refills even before the module is repaired. " +
             "Useful for testing. Turn OFF for normal gameplay.")]
    public bool alwaysRefill = false;

    [Tooltip("Notification shown when the player enters the zone while it is active.")]
    public string enterMessage = "Life Support zone — oxygen slowly refilling.";

    [Tooltip("Notification shown when the player enters but the module is not yet repaired.")]
    public string notRepairedMessage = "Life Support offline — repair it first to enable oxygen refill.";

    // ─────────────────────────────────────────────────────────────────────────
    private OxygenSystem _oxygenSystem;
    private bool         _playerInZone;
    private bool         _notificationShown;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        if (!_playerInZone || _oxygenSystem == null) return;

        if (CanRefill())
            _oxygenSystem.RefillAtLifeSupport();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInZone = true;
        _oxygenSystem = other.GetComponent<OxygenSystem>();

        if (CanRefill())
        {
            if (!_notificationShown)
            {
                NotificationManager.Instance?.Show(enterMessage);
                _notificationShown = true;
            }
        }
        else
        {
            NotificationManager.Instance?.Show(notRepairedMessage);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInZone      = false;
        _oxygenSystem      = null;
        _notificationShown = false;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private bool CanRefill()
    {
        if (alwaysRefill) return true;
        if (lifeSupportModule == null) return false;
        // Refill active once at least one stage is complete (Progress > 0)
        return lifeSupportModule.Progress > 0f || lifeSupportModule.IsFullyRepaired;
    }
}
