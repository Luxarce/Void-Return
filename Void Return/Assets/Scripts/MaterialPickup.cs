using UnityEngine;

/// <summary>
/// Collectable material pickup that floats in zero-G and collides with ground.
///
/// ROOT CAUSE OF COLLECTION FAILURE:
///   The previous version added a programmatic CircleCollider2D trigger in Awake()
///   AFTER setting all existing colliders to isTrigger = false. However, because
///   the Player's Collider2D was also non-trigger and on the same physics interaction
///   layer as the material's solid collider, OnTriggerEnter2D never fired —
///   two non-trigger colliders produce OnCollisionEnter2D, not OnTriggerEnter2D.
///   Meanwhile the programmatically added trigger was on a fresh CircleCollider2D
///   that Unity treated as part of the same Rigidbody2D composite, so it only fired
///   when its specific radius was breached — which was 0.4 units, too small when
///   the item was settling and the player was walking over it at normal speed.
///
/// FIX APPROACH:
///   Instead of runtime-added colliders, collection is now detected via
///   OnCollisionEnter2D / OnCollisionStay2D on the SOLID collider.
///   The player's CapsuleCollider2D (non-trigger) touches the material's
///   CircleCollider2D (non-trigger) → OnCollisionEnter2D fires on the material.
///   We check CompareTag("Player") on the collision and call Collect().
///
///   This is simpler, more reliable, and does not require any runtime collider setup.
///
/// PREFAB SETUP (simplified):
///   Rigidbody2D         — Dynamic, Gravity Scale 0, Continuous
///   CircleCollider2D    — Is Trigger: OFF  (solid, for ground collision AND collection)
///   SpriteRenderer      — sprite assigned
///   MaterialPickup      — fields assigned
///
/// PHYSICS LAYER SETUP REQUIRED:
///   Edit → Project Settings → Physics 2D → Layer Collision Matrix
///   The Player layer and the Material layer (or Default) MUST have their
///   intersection checkbox ticked for OnCollisionEnter2D to fire.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class MaterialPickup : MonoBehaviour
{
    [Header("Material Info")]
    [Tooltip("What material type this pickup contains.")]
    public MaterialType materialType;

    [Range(1, 10)]
    [Tooltip("How many units are given to the player on collection.")]
    public int quantity = 1;

    [Tooltip("Icon sprite shown in the Inventory UI. Also auto-assigned to the SpriteRenderer if its Sprite field is empty.")]
    public Sprite icon;

    [Header("Physics Drift (zero-G floating)")]
    [Tooltip("Ambient drift speed when spawned without a launch. Keep low (0.3-0.8).")]
    public float driftSpeed = 0.5f;

    [Tooltip("Initial tumble rotation speed (degrees/sec).")]
    public float tumbleSpeed = 20f;

    [Tooltip("Linear drag — higher = settles faster. 2-4 recommended.")]
    public float floatDrag = 2.5f;

    [Tooltip("Angular drag for tumble slowdown.")]
    public float angularDrag = 1.5f;

    [Header("Fling From Meteorite Impact")]
    [Tooltip("Minimum upward launch angle (0-90). Prevents items spawning downward.")]
    [Range(0f, 90f)]
    public float minimumUpwardAngle = 30f;

    [Tooltip("Min speed when flung from impact.")]
    public float launchSpeedMin = 1f;

    [Tooltip("Max speed when flung from impact.")]
    public float launchSpeedMax = 3f;

    [Header("Magnet")]
    [Tooltip("Distance at which the pickup is pulled toward the player.")]
    public float magnetRadius = 3f;

    [Tooltip("Force magnitude of the magnet pull toward the player.")]
    public float magnetForce = 8f;

    [Header("Audio")]
    [Tooltip("Sound played at collection (AudioSource.PlayClipAtPoint survives destruction).")]
    public AudioClip pickupClip;

    [Range(0f, 1f)]
    public float pickupVolume = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    private Rigidbody2D    _rb;
    private SpriteRenderer _sr;
    private Transform      _playerTransform;
    private bool           _collected;
    private bool           _launched;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();

        // Auto-assign sprite to SpriteRenderer if it is empty
        if (_sr.sprite == null && icon != null)
        {
            _sr.sprite = icon;
            Debug.Log($"[MaterialPickup] Auto-assigned icon to SpriteRenderer on '{name}'.");
        }
        if (_sr.sprite == null)
            Debug.LogWarning($"[MaterialPickup] '{name}' has no sprite — " +
                             "assign one to the SpriteRenderer or the Icon field.");

        // Physics setup
        _rb.gravityScale           = 0f;
        _rb.linearDamping          = floatDrag;
        _rb.angularDamping         = angularDrag;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // The solid collider (first on the object) handles BOTH ground collision
        // AND collection detection via OnCollisionEnter2D. Ensure it is NOT a trigger.
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = false;

            // Apply a slightly bouncy physics material so items bounce off surfaces
            var mat        = new PhysicsMaterial2D("MaterialBounce");
            mat.bounciness = 0.15f;
            mat.friction   = 0.3f;
            col.sharedMaterial = mat;
        }
    }

    private void Start()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            _playerTransform = player.transform;

        if (!_launched)
            ApplyRandomDrift();
    }

    private void FixedUpdate()
    {
        if (_collected || _playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        if (dist <= magnetRadius)
        {
            Vector2 dir = ((Vector2)_playerTransform.position
                          - (Vector2)transform.position).normalized;
            _rb.AddForce(dir * magnetForce, ForceMode2D.Force);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Collision Detection — fires when the solid collider touches the player
    // ─────────────────────────────────────────────────────────────────────────

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (_collected) return;
        if (!col.gameObject.CompareTag("Player")) return;
        Collect();
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (_collected) return;
        if (!col.gameObject.CompareTag("Player")) return;
        Collect();
    }

    // Keep trigger callbacks as a fallback in case the prefab has a trigger collider
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected) return;
        if (!other.CompareTag("Player")) return;
        Collect();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (_collected) return;
        if (!other.CompareTag("Player")) return;
        Collect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Flings the item upward and outward from a meteorite impact point.
    /// Called by MeteoriteManager immediately after instantiation.
    /// </summary>
    public void LaunchFromImpact(Vector2 impactPoint)
    {
        _launched = true;

        float angleDeg = Random.Range(minimumUpwardAngle, 180f - minimumUpwardAngle);
        float angleRad = angleDeg * Mathf.Deg2Rad;
        float xDir     = Random.value > 0.5f ? 1f : -1f;

        Vector2 dir = new Vector2(
            xDir * Mathf.Cos(angleRad),
            Mathf.Abs(Mathf.Sin(angleRad))
        ).normalized;

        _rb.linearVelocity  = dir * Random.Range(launchSpeedMin, launchSpeedMax);
        _rb.angularVelocity = Random.Range(-tumbleSpeed, tumbleSpeed);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void Collect()
    {
        _collected = true;  // Guard — must be first to prevent double collection

        if (Inventory.Instance != null)
            Inventory.Instance.AddItem(materialType, quantity, icon);
        else
            Debug.LogWarning("[MaterialPickup] Inventory.Instance is null on collect. " +
                             "Ensure an Inventory GameObject exists in the scene.");

        string displayName = System.Text.RegularExpressions.Regex.Replace(
            materialType.ToString(), "(?<=[a-z])(?=[A-Z])", " ");

        NotificationManager.Instance?.ShowPickup($"+{quantity}  {displayName}");

        if (pickupClip != null)
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);

        Destroy(gameObject);
    }

    private void ApplyRandomDrift()
    {
        if (driftSpeed <= 0f) return;
        _rb.linearVelocity  = Random.insideUnitCircle.normalized
                              * (driftSpeed * Random.Range(0.3f, 1f));
        _rb.angularVelocity = Random.Range(-tumbleSpeed * 0.5f, tumbleSpeed * 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, magnetRadius);
    }
}
