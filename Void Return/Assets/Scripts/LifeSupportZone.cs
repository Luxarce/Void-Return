using UnityEngine;

/// <summary>
/// Oxygen refill zone around the Life Support module.
///
/// FIX — AUTOREFILL DISABLED AFTER REPAIR:
///  Root cause: LifeSupportShield adds a CircleCollider2D (isTrigger = false)
///  to the Life Support GameObject. When the player walked into the zone,
///  the new solid shield collider physically blocked them before they could
///  reach the trigger. OnTriggerEnter2D never fired for the zone because
///  the player was bouncing off the shield.
///
///  Fix: LifeSupportZone now creates its own dedicated CHILD trigger GameObject
///  at Start(). The trigger is completely independent of the shield collider.
///  It always works regardless of what colliders are on the parent.
///
/// SETUP:
///  1. Add LifeSupportZone to the Life Support repair point.
///  2. Remove any existing CircleCollider2D that was being used for the zone
///     (this script creates its own now).
///  3. Assign the lifeSupportModule field.
///  4. Set zoneRadius to match the visual zone size.
/// </summary>
public class LifeSupportZone : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the ShipModule component from this same GameObject.")]
    public ShipModule lifeSupportModule;

    [Header("Zone Settings")]
    [Tooltip("Radius of the oxygen refill zone. Should be larger than the shield radius.")]
    public float zoneRadius = 4f;

    [Tooltip("How quickly oxygen refills per second while the player is in the zone.")]
    public float refillRate = 5f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   refillLoopClip;

    // ─────────────────────────────────────────────────────────────────────────
    private OxygenSystem _oxygenSystem;
    private bool         _playerInZone;
    private GameObject   _zoneTriggerObject;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Create a dedicated child GameObject for the trigger.
        // This is separate from the parent so the shield collider cannot interfere.
        _zoneTriggerObject = new GameObject("LifeSupportZoneTrigger");
        _zoneTriggerObject.transform.SetParent(transform, false);
        _zoneTriggerObject.transform.localPosition = Vector3.zero;
        _zoneTriggerObject.layer = gameObject.layer;

        // Add Rigidbody2D (kinematic) so the trigger fires correctly
        var rb       = _zoneTriggerObject.AddComponent<Rigidbody2D>();
        rb.bodyType  = RigidbodyType2D.Kinematic;

        // Add the trigger collider
        var col       = _zoneTriggerObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = zoneRadius;

        // Add a relay script so events route back to this component
        var relay     = _zoneTriggerObject.AddComponent<LifeSupportZoneTriggerRelay>();
        relay.parent  = this;

        Debug.Log($"[LifeSupportZone] Zone trigger created at {transform.position}, radius={zoneRadius}");
    }

    private void Update()
    {
        if (!_playerInZone || _oxygenSystem == null) return;
        if (!CanRefill()) return;

        _oxygenSystem.RefillAtLifeSupport();
    }

    // Called by the relay component on the child trigger object
    public void OnPlayerEnter(Collider2D other)
    {
        _playerInZone  = true;
        _oxygenSystem  = other.GetComponent<OxygenSystem>();
        Debug.Log("[LifeSupportZone] Player entered zone.");

        if (CanRefill())
            NotificationManager.Instance?.ShowInfo("Life Support zone — oxygen refilling.");
        else
            NotificationManager.Instance?.ShowInfo("Life Support — repair Stage 1 to enable oxygen refill.");
    }

    public void OnPlayerExit(Collider2D other)
    {
        _playerInZone = false;
        _oxygenSystem = null;
    }

    private bool CanRefill()
    {
        if (lifeSupportModule == null) return true; // default: always refill if no module assigned
        return lifeSupportModule.Progress > 0f || lifeSupportModule.IsFullyRepaired;
    }
}

/// <summary>
/// Relay that passes trigger events from the child trigger GameObject to LifeSupportZone.
/// This pattern isolates the zone trigger from any other colliders on the parent object.
/// </summary>
public class LifeSupportZoneTriggerRelay : MonoBehaviour
{
    [HideInInspector] public LifeSupportZone parent;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        parent?.OnPlayerEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        parent?.OnPlayerExit(other);
    }
}
