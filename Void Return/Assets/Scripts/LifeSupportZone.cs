using UnityEngine;

/// <summary>
/// Oxygen refill zone around the Life Support module.
/// Also triggers a checkpoint save via SaveManager whenever the player enters.
///
/// The child trigger pattern keeps this completely isolated from the
/// LifeSupportShield's solid collider so both always work independently.
/// </summary>
public class LifeSupportZone : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the ShipModule component from this same GameObject.")]
    public ShipModule lifeSupportModule;

    [Header("Zone Settings")]
    [Tooltip("Radius of the oxygen refill zone. Must be larger than the shield radius.")]
    public float zoneRadius = 4f;

    [Header("Checkpoint Save")]
    [Tooltip("If true, triggers a checkpoint save via SaveManager each time the player enters.")]
    public bool saveOnEnter = true;

    [Tooltip("Minimum seconds between consecutive checkpoint saves. Prevents save-spam " +
             "if the player hovers at the zone boundary.")]
    [Range(5f, 60f)]
    public float saveCooldown = 10f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   refillLoopClip;

    // ─────────────────────────────────────────────────────────────────────────
    private OxygenSystem _oxygenSystem;
    private bool         _playerInZone;
    private float        _lastSaveTime = -999f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Create dedicated child trigger — isolated from the shield's solid collider
        var child = new GameObject("LifeSupportZoneTrigger");
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;
        child.layer = gameObject.layer;

        var rb      = child.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        var col       = child.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = zoneRadius;

        var relay     = child.AddComponent<LifeSupportZoneTriggerRelay>();
        relay.parent  = this;

        Debug.Log($"[LifeSupportZone] Zone trigger created, radius={zoneRadius}");
    }

    private void Update()
    {
        if (!_playerInZone || _oxygenSystem == null) return;
        if (!CanRefill()) return;
        _oxygenSystem.RefillAtLifeSupport();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Called by LifeSupportZoneTriggerRelay
    // ─────────────────────────────────────────────────────────────────────────

    public void OnPlayerEnter(Collider2D other)
    {
        _playerInZone  = true;
        _oxygenSystem  = other.GetComponent<OxygenSystem>();

        if (CanRefill())
            NotificationManager.Instance?.ShowInfo("Life Support zone — oxygen refilling.");
        else
            NotificationManager.Instance?.ShowInfo("Repair Life Support Stage 1 to enable oxygen refill.");

        // Trigger checkpoint save
        if (saveOnEnter && Time.time - _lastSaveTime >= saveCooldown)
        {
            _lastSaveTime = Time.time;
            SaveManager.Instance?.LifeSupportCheckpointSave();
        }
    }

    public void OnPlayerExit(Collider2D other)
    {
        _playerInZone = false;
        _oxygenSystem = null;
    }

    private bool CanRefill()
    {
        if (lifeSupportModule == null) return true;
        return lifeSupportModule.Progress > 0f || lifeSupportModule.IsFullyRepaired;
    }
}

/// <summary>Relay that routes trigger events from the child GameObject to LifeSupportZone.</summary>
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
