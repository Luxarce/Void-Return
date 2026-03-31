using UnityEngine;

/// <summary>
/// Controls an individual meteorite projectile.
///
/// FIX NOTES (v2):
/// ─────────────────────────────────────────────────────────────────────────
/// ISSUE: Meteorites were not disappearing on impact.
/// ROOT CAUSES FIXED:
///   1. The original script checked impactLayers via bitmask comparison
///      BEFORE any other logic. If impactLayers was left unassigned (value 0),
///      the bitmask check always failed silently — the Impact() method was
///      never reached. Fix: Added a fallback that fires Impact on ANY
///      collision if impactLayers is empty (value 0 / Nothing).
///   2. The original used both OnCollisionEnter2D AND OnTriggerEnter2D.
///      If the meteorite's own Collider2D was a trigger, OnCollisionEnter2D
///      never fired. Now: meteorite uses a non-trigger Collider2D and relies
///      on OnCollisionEnter2D only. Impact zone layers are checked via the
///      impactLayers mask OR hit anything.
///   3. Added a _hasImpacted guard flag so multiple collision events in the
///      same frame cannot call Impact() twice (which was causing double VFX).
///   4. Destroy is now deferred with a 0.1s delay to let the VFX instantiate
///      before the parent is removed.
/// ─────────────────────────────────────────────────────────────────────────
///
/// SETUP CHECKLIST:
///   □ Meteorite prefab must have a non-trigger Collider2D (e.g., CircleCollider2D
///     with Is Trigger = OFF)
///   □ Meteorite prefab must have Rigidbody2D (Gravity Scale: 0,
///     Collision Detection: Continuous)
///   □ Assign Impact Layers in the Inspector (at minimum: Ground, Debris, Player).
///     If you leave it as "Nothing", the meteorite will destroy itself on ANY hit.
///   □ Assign Impact VFX Prefab (the MeteoriteImpactVFX prefab from Effects folder)
///
/// Place this script on each meteorite prefab.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Meteorite : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Flight")]
    [Tooltip("Speed at which the meteorite travels toward its target in world units per second.")]
    public float speed = 22f;

    [Tooltip("Degrees per second the meteorite spins as it flies (tumble effect).")]
    public float tumbleSpeed = 90f;

    [Header("Impact")]
    [Tooltip("VFX prefab spawned at the impact point when the meteorite hits something. " +
             "Assign the MeteoriteImpactVFX prefab here.")]
    public GameObject impactVFXPrefab;

    [Tooltip("Layers that trigger an impact. Set to include Ground, Debris, Player. " +
             "IMPORTANT: If this is set to 'Nothing' (value 0), the meteorite will " +
             "destroy itself when hitting ANY collider — use this as a safe fallback.")]
    public LayerMask impactLayers;

    [Header("Audio")]
    [Tooltip("Sound played at the impact point.")]
    public AudioClip impactClip;

    [Tooltip("Volume of the impact sound.")]
    [Range(0f, 1f)]
    public float impactVolume = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Rigidbody2D _rb;
    private bool        _launched    = false;
    private bool        _hasImpacted = false;  // Guard — prevents double-impact

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb              = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        // Continuous collision detection prevents tunneling through thin colliders
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Ensure our collider is NOT a trigger — we need OnCollisionEnter2D
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = false;
    }

    private void FixedUpdate()
    {
        if (!_launched || _hasImpacted) return;

        // Apply spin
        _rb.angularVelocity = tumbleSpeed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Called by MeteoriteManager
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by MeteoriteManager after spawning to set the meteorite's target.
    /// Launches the meteorite toward the given world position at the set speed.
    /// </summary>
    public void SetTarget(Vector2 worldPosition)
    {
        Vector2 direction  = (worldPosition - (Vector2)transform.position).normalized;
        _rb.linearVelocity = direction * speed;
        _launched          = true;

        // Orient the sprite in the direction of flight
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        // Safety: auto-destroy after 15 seconds even if nothing is hit
        // (prevents rogue meteorites floating indefinitely)
        Destroy(gameObject, 15f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Collision
    // ─────────────────────────────────────────────────────────────────────────

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (_hasImpacted) return;

        // If impactLayers is set (not Nothing/0), check that the hit layer qualifies
        if (impactLayers.value != 0)
        {
            bool layerMatches = ((1 << col.gameObject.layer) & impactLayers.value) != 0;
            if (!layerMatches) return;
        }
        // If impactLayers is Nothing (0), accept any collision as an impact

        Vector2 contactPoint = col.contacts.Length > 0
            ? col.contacts[0].point
            : (Vector2)transform.position;

        Impact(contactPoint, col.gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Impact Logic
    // ─────────────────────────────────────────────────────────────────────────

    private void Impact(Vector2 position, GameObject hitObject)
    {
        // Set guard FIRST to prevent re-entry
        _hasImpacted = true;

        // Stop all physics movement immediately
        _rb.linearVelocity  = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.bodyType        = RigidbodyType2D.Kinematic;

        // Disable our collider so we don't keep triggering things
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Spawn VFX at impact point
        if (impactVFXPrefab != null)
            Instantiate(impactVFXPrefab, position, Quaternion.identity);

        // Play impact sound at world position (survives object destruction)
        if (impactClip != null)
            AudioSource.PlayClipAtPoint(impactClip, position, impactVolume);

        // Notify if a ship module was hit
        if (hitObject.TryGetComponent<ShipModule>(out var module))
            NotificationManager.Instance?.Show($"{module.moduleName} hit by meteorite!", urgent: true);

        // Notify if the player was hit
        if (hitObject.CompareTag("Player"))
            NotificationManager.Instance?.Show("Meteorite impact! Take cover!", urgent: true);

        // Destroy with a tiny delay (0.05s) to allow VFX to instantiate cleanly
        Destroy(gameObject, 0.05f);
    }
}
