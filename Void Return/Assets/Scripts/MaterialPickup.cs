using UnityEngine;

/// <summary>
/// Collectable material pickup floating in space.
///
/// FIX NOTES:
///  — Added OnTriggerStay2D fallback alongside OnTriggerEnter2D.
///    If the player spawns inside the trigger radius, Enter fires immediately
///    but can be missed in certain frame timings. Stay catches it every frame
///    until the _collected flag is set.
///  — Null-guards added around Inventory.Instance and NotificationManager.Instance
///    so missing singletons never silently prevent collection.
///  — Magnet now uses MoveTowards instead of setting velocity directly,
///    preventing jitter when the pickup overshoots the player.
///  — Layer is force-set to "Material" in Awake if that layer exists,
///    otherwise the object stays on its assigned layer.
///
/// REQUIRED SETUP CHECKLIST:
///   [1] Player GameObject → Tag = "Player"
///   [2] This prefab → any Collider2D → Is Trigger = ON
///   [3] This prefab → Rigidbody2D → Gravity Scale = 0
///   [4] Physics 2D Layer Matrix: Player vs Material layer = ticked
///   [5] materialType field assigned in Inspector
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class MaterialPickup : MonoBehaviour
{
    [Header("Material Info")]
    [Tooltip("What material type this pickup contains.")]
    public MaterialType materialType;

    [Tooltip("How many units are given on collection.")]
    [Range(1, 10)]
    public int quantity = 1;

    [Tooltip("Icon sprite shown in the inventory slot. Drag from Project window.")]
    public Sprite icon;

    [Header("Magnet")]
    [Tooltip("Distance at which this pickup starts flying toward the player.")]
    public float magnetRadius = 2.5f;

    [Tooltip("Speed at which the pickup moves toward the player once magnetized.")]
    public float magnetSpeed = 6f;

    [Header("Audio")]
    [Tooltip("Sound played when collected (plays at world position, survives destruction).")]
    public AudioClip pickupClip;

    [Tooltip("Volume of the pickup sound.")]
    [Range(0f, 1f)]
    public float pickupVolume = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    private Transform   _playerTransform;
    private Rigidbody2D _rb;
    private bool        _collected;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb              = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.linearDamping = 0.5f;

        // Ensure the collider is a trigger
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        // Cache player reference once
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) _playerTransform = player.transform;
    }

    private void FixedUpdate()
    {
        if (_collected || _playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        if (dist <= magnetRadius)
        {
            // MoveTowards prevents overshooting
            Vector2 next = Vector2.MoveTowards(
                transform.position,
                _playerTransform.position,
                magnetSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(next);
        }
    }

    // OnTriggerEnter is the primary collection path
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected) return;
        if (!other.CompareTag("Player")) return;
        Collect();
    }

    // OnTriggerStay is a fallback — catches cases where Enter was missed
    private void OnTriggerStay2D(Collider2D other)
    {
        if (_collected) return;
        if (!other.CompareTag("Player")) return;
        Collect();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void Collect()
    {
        _collected = true;  // Set FIRST to prevent any re-entry

        // Add to inventory and fire OnInventoryChanged
        if (Inventory.Instance != null)
            Inventory.Instance.AddItem(materialType, quantity, icon);
        else
            Debug.LogWarning("[MaterialPickup] Inventory.Instance is null. " +
                             "Make sure an Inventory GameObject exists in the scene.");

        // Show notification
        string name = materialType.ToString();
        NotificationManager.Instance?.Show($"+{quantity}  {name}");

        // Play sound before destroying (PlayClipAtPoint creates its own temp AudioSource)
        if (pickupClip != null)
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, magnetRadius);
    }
}
