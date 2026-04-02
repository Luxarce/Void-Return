using UnityEngine;
using System;

/// <summary>
/// Individual meteorite projectile.
///
/// OPTIMIZATION / LEAK PREVENTION:
///  — impactVFXPrefab is Instantiated once at impact then auto-destroyed
///    because the VFX prefab itself should have a ParticleSystem with Stop
///    Action = Destroy. If you leave the prefab as a persistent object you
///    will leak VFX instances. Ensure your impactVFXPrefab's root Particle
///    System has: Stop Action = Destroy, Loop = OFF.
///  — The meteorite itself always has a 15-second safety Destroy() scheduled
///    in SetTarget() so it can never linger forever if it misses everything.
///  — Oxygen damage is applied DIRECTLY via GetComponent on the hit object —
///    no singleton dependency that could be null.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Meteorite : MonoBehaviour
{
    // All Meteorite instances report their actual impact position here.
    public static event Action<Vector2, MeteoriteType> OnAnyImpact;

    [Header("Identity")]
    [Tooltip("Set by MeteoriteManager at spawn. Determines drop table.")]
    public MeteoriteType meteoriteType = MeteoriteType.Stray;

    [Header("Flight")]
    public float speed       = 22f;
    public float tumbleSpeed = 90f;

    [Header("Impact")]
    [Tooltip("VFX prefab. MUST have Stop Action = Destroy on its root Particle System.")]
    public GameObject impactVFXPrefab;

    [Tooltip("Layers that count as valid collision targets.\n" +
             "If left as Nothing (0), any collision triggers impact.")]
    public LayerMask impactLayers;

    [Header("Oxygen Damage")]
    [Tooltip("Oxygen drained when this meteorite hits the player.")]
    public float oxygenDamageOnPlayerHit = 20f;

    [Header("Audio")]
    public AudioClip impactClip;
    [Range(0f, 1f)] public float impactVolume = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    private Rigidbody2D _rb;
    private bool        _launched;
    private bool        _hasImpacted;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale           = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = false;
    }

    private void FixedUpdate()
    {
        if (!_launched || _hasImpacted) return;
        _rb.angularVelocity = tumbleSpeed;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Launches the meteorite toward a world position.
    /// Called by MeteoriteManager immediately after Instantiate.
    /// A 15-second safety destroy is always scheduled so stray meteorites
    /// that miss everything are never left alive indefinitely.
    /// </summary>
    public void SetTarget(Vector2 worldPosition)
    {
        Vector2 dir        = (worldPosition - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * speed;
        _launched          = true;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        // Always schedule a safety cleanup so this object can NEVER leak.
        Destroy(gameObject, 15f);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (_hasImpacted) return;

        if (impactLayers.value != 0)
        {
            bool match = ((1 << col.gameObject.layer) & impactLayers.value) != 0;
            if (!match) return;
        }

        Vector2 contact = col.contacts.Length > 0
            ? col.contacts[0].point
            : (Vector2)transform.position;

        Impact(contact, col.gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void Impact(Vector2 position, GameObject hitObject)
    {
        _hasImpacted = true;

        // Stop physics immediately
        _rb.linearVelocity  = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.bodyType        = RigidbodyType2D.Kinematic;

        // Disable collider so we stop triggering things
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Spawn VFX — must have Stop Action = Destroy on the Particle System
        if (impactVFXPrefab != null)
        {
            var vfxObj = Instantiate(impactVFXPrefab, position, Quaternion.identity);
            Destroy(vfxObj, 8f); // safety cleanup — VFX prefab should also have Stop Action = Destroy
        }

        // Play sound at world position (survives this object's destruction)
        if (impactClip != null)
            AudioSource.PlayClipAtPoint(impactClip, position, impactVolume);

        // Module hit
        if (hitObject.TryGetComponent<ShipModule>(out var module))
            NotificationManager.Instance?.ShowWarning($"{module.moduleName} hit by meteorite!");

        // Player hit — direct oxygen damage, no singleton dependency
        if (hitObject.CompareTag("Player"))
        {
            NotificationManager.Instance?.ShowWarning("METEORITE IMPACT! Oxygen depleting!");

            var oxygen = hitObject.GetComponent<OxygenSystem>();
            if (oxygen != null)
                oxygen.TakeDamageFromMeteorite(oxygenDamageOnPlayerHit);
            else
                Debug.LogWarning("[Meteorite] Player hit but OxygenSystem not found.");

            MeteoriteManager.Instance?.NotifyPlayerHit();
        }

        // Notify MeteoriteManager so it can spawn drops
        OnAnyImpact?.Invoke(position, meteoriteType);

        // Small delay so VFX spawns cleanly before we destroy
        Destroy(gameObject, 0.05f);
    }
}
