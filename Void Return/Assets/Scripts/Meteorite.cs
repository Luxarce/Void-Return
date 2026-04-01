using UnityEngine;
using System;

/// <summary>
/// Controls a single meteorite projectile.
///
/// CHANGES IN THIS VERSION:
///  — Added static event OnAnyImpact that MeteoriteManager subscribes to.
///    When any meteorite hits, it fires this event with its world position and
///    type — MeteoriteManager then spawns the correct material drops there.
///  — meteoriteType field set by MeteoriteManager at spawn time.
///  — _hasImpacted guard prevents double-impact.
///  — Collider is force-set to non-trigger in Awake so OnCollisionEnter2D fires.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Meteorite : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Static Event — MeteoriteManager subscribes to this
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired by every Meteorite when it impacts.
    /// Params: impact world position, meteorite type.
    /// MeteoriteManager.HandleMeteoriteImpact subscribes to this to spawn drops.
    /// </summary>
    public static event Action<Vector2, MeteoriteType> OnAnyImpact;

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Identity")]
    [Tooltip("Set by MeteoriteManager at spawn. Determines which drop table is used.")]
    public MeteoriteType meteoriteType = MeteoriteType.Stray;

    [Header("Flight")]
    [Tooltip("Speed the meteorite travels toward its target.")]
    public float speed = 22f;

    [Tooltip("Degrees per second the meteorite tumbles as it flies.")]
    public float tumbleSpeed = 90f;

    [Header("Impact")]
    [Tooltip("VFX prefab spawned at the impact point. Assign MeteoriteImpactVFX.")]
    public GameObject impactVFXPrefab;

    [Tooltip("Layers that register as a valid impact. " +
             "Assign: Ground, Debris, Player. " +
             "If left as 'Nothing' (0), any collision will trigger an impact.")]
    public LayerMask impactLayers;

    [Header("Audio")]
    [Tooltip("Sound played at impact world position.")]
    public AudioClip impactClip;

    [Tooltip("Volume of the impact sound.")]
    [Range(0f, 1f)]
    public float impactVolume = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Rigidbody2D _rb;
    private bool        _launched    = false;
    private bool        _hasImpacted = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb              = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Must be non-trigger so OnCollisionEnter2D fires
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = false;
    }

    private void FixedUpdate()
    {
        if (!_launched || _hasImpacted) return;
        _rb.angularVelocity = tumbleSpeed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by MeteoriteManager after spawning to launch toward a target.
    /// </summary>
    public void SetTarget(Vector2 worldPosition)
    {
        Vector2 dir    = (worldPosition - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * speed;
        _launched      = true;

        float angle    = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        // Safety auto-destroy if nothing is ever hit
        Destroy(gameObject, 15f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Collision
    // ─────────────────────────────────────────────────────────────────────────

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (_hasImpacted) return;

        // If impactLayers is set, only accept valid layers
        if (impactLayers.value != 0)
        {
            bool layerMatch = ((1 << col.gameObject.layer) & impactLayers.value) != 0;
            if (!layerMatch) return;
        }

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
        _hasImpacted = true;

        // Stop physics
        _rb.linearVelocity  = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.bodyType        = RigidbodyType2D.Kinematic;

        // Disable collider
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Spawn VFX
        if (impactVFXPrefab != null)
            Instantiate(impactVFXPrefab, position, Quaternion.identity);

        // Play sound at world position (survives destruction)
        if (impactClip != null)
            AudioSource.PlayClipAtPoint(impactClip, position, impactVolume);

        // Notify of module hit
        if (hitObject.TryGetComponent<ShipModule>(out var module))
            NotificationManager.Instance?.Show($"{module.moduleName} damaged by meteorite!", urgent: true);

        // Notify of player hit
        if (hitObject.CompareTag("Player"))
            NotificationManager.Instance?.Show("Meteorite impact! Watch out!", urgent: true);

        // Fire the static event — MeteoriteManager will spawn material drops
        OnAnyImpact?.Invoke(position, meteoriteType);

        // Destroy with tiny delay so VFX instantiates cleanly
        Destroy(gameObject, 0.05f);
    }
}
