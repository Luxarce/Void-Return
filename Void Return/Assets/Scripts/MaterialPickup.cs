using UnityEngine;

/// <summary>
/// Represents a collectible material item floating in space.
///
/// FIX NOTES (v2):
/// ─────────────────────────────────────────────────────────────────────────
/// ISSUE: Materials were not disappearing after collection.
/// ROOT CAUSES FIXED:
///   1. The Destroy(gameObject) call could be reached while the object was
///      already being destroyed by another overlap in the same frame.
///      Added a _collected guard flag so Destroy is only ever called once.
///   2. OnTriggerEnter2D requires the Player's Collider2D to NOT also be
///      a trigger. If both colliders are triggers, OnTriggerEnter2D fires
///      but without physics — meaning physics-layer filtering can silently
///      pass. Now using CompareTag("Player") as the sole guard (reliable).
///   3. The AudioSource.PlayClipAtPoint call now runs BEFORE Destroy so the
///      sound clip is not cut off by the object being removed.
/// ─────────────────────────────────────────────────────────────────────────
///
/// SETUP CHECKLIST (required for collection to work):
///   □ Player Tag must be set to "Player" (Inspector → top Tag dropdown)
///   □ This pickup's Collider2D → Is Trigger must be ON (checked)
///   □ Player's Rigidbody2D must exist (triggers need at least one RB to fire)
///   □ This pickup's Rigidbody2D must exist (Gravity Scale: 0)
///   □ Player layer and Pickup layer must have collision enabled in
///     Edit → Project Settings → Physics 2D → Layer Collision Matrix
///
/// Place this script on each material pickup prefab.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MaterialPickup : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Material Info")]
    [Tooltip("The type of material this pickup contains.")]
    public MaterialType materialType;

    [Tooltip("How many units of this material are given when collected.")]
    [Range(1, 10)]
    public int quantity = 1;

    [Tooltip("Icon sprite shown in HUD notifications and the inventory panel.")]
    public Sprite icon;

    [Header("Magnet Behavior")]
    [Tooltip("Distance at which this pickup starts flying toward the player automatically.")]
    public float magnetRadius = 2.5f;

    [Tooltip("Speed at which the pickup moves toward the player once magnetized.")]
    public float magnetSpeed = 5f;

    [Header("Audio")]
    [Tooltip("Sound clip played when the player collects this material. " +
             "Plays at world position so it is not cut off when the object is destroyed.")]
    public AudioClip pickupClip;

    [Tooltip("Volume of the pickup sound (0–1).")]
    [Range(0f, 1f)]
    public float pickupVolume = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Transform   _playerTransform;
    private Rigidbody2D _rb;

    /// <summary>
    /// Guard flag. Prevents Destroy from being called more than once
    /// if two collision events fire in the same frame.
    /// </summary>
    private bool _collected = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb              = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.linearDamping = 0.5f;

        // Ensure our own collider is a trigger
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        // Cache the player transform once on start
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            _playerTransform = player.transform;
    }

    private void FixedUpdate()
    {
        if (_collected || _playerTransform == null) return;

        float distance = Vector2.Distance(transform.position, _playerTransform.position);

        if (distance <= magnetRadius)
        {
            Vector2 direction = ((Vector2)_playerTransform.position
                                - (Vector2)transform.position).normalized;
            _rb.linearVelocity = direction * magnetSpeed;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Trigger Detection
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Guard: only collect once, and only when touching the Player
        if (_collected) return;
        if (!other.CompareTag("Player")) return;

        Collect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Collection Logic
    // ─────────────────────────────────────────────────────────────────────────

    private void Collect()
    {
        // Set the flag FIRST to prevent any re-entry
        _collected = true;

        // Add to inventory
        Inventory.Instance?.AddItem(materialType, quantity, icon);

        // Show notification
        string displayName = materialType.ToString().Replace("_", " ");
        NotificationManager.Instance?.Show($"+{quantity}  {displayName}");

        // Play pickup sound at world position BEFORE destroying the object
        // AudioSource.PlayClipAtPoint creates a temporary AudioSource that
        // persists even after this GameObject is destroyed.
        if (pickupClip != null)
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);

        // Destroy this GameObject — it will disappear from the scene
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, magnetRadius);
    }
}
