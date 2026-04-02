using UnityEngine;
using System.Collections;

/// <summary>
/// Gravity Grenade projectile that detonates and creates a pull zone.
///
/// FIX — GRENADE CLIPPING THROUGH GROUND:
///  The Rigidbody2D Collision Detection was set to Discrete (default).
///  At the grenade's launch speed, it can travel several units in one physics
///  step and skip past thin colliders entirely.
///  FIX: Set Collision Detection to Continuous in Awake().
///  FIX: Collider is NOT a trigger — it's a solid physics collider.
///       The pull zone is implemented via OverlapCircleAll (not a trigger).
///
/// PREFAB REQUIREMENTS:
///   Rigidbody2D  — Gravity Scale: 0, Collision Detection: Continuous (set in Awake)
///   CircleCollider2D — Is Trigger: OFF (solid — bounces off walls and ground)
///   SpriteRenderer — grenade sprite
///   GravityGrenade script
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GravityGrenade : MonoBehaviour
{
    [Header("Pull Zone")]
    [Tooltip("Radius of the gravity pull zone on detonation.")]
    public float pullRadius    = 8f;

    [Tooltip("Force applied to objects in the pull zone each FixedUpdate.")]
    public float pullStrength  = 10f;

    [Tooltip("How long the pull zone stays active after detonation (seconds).")]
    public float duration      = 5f;

    [Header("Timing")]
    [Tooltip("Seconds after launch before detonation.")]
    public float fuseTime      = 1.5f;

    [Header("VFX & Audio")]
    public ParticleSystem pullVFX;
    public AudioSource    audioSource;
    public AudioClip      detonateClip;

    [Tooltip("LayerMask for objects the grenade pulls. Should include Debris, Player, Materials.")]
    public LayerMask pullMask;

    // ─────────────────────────────────────────────────────────────────────────
    private Rigidbody2D _rb;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        // CRITICAL: Continuous collision detection prevents passing through ground
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Ensure the collider is NOT a trigger so it bounces off surfaces
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = false;

            // Apply a bouncy physics material so the grenade rolls/bounces naturally
            var mat = new PhysicsMaterial2D("GrenadeBounce");
            mat.bounciness  = 0.3f;
            mat.friction    = 0.5f;
            col.sharedMaterial = mat;
        }
    }

    private void Start()
    {
        Invoke(nameof(Detonate), fuseTime);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void Detonate()
    {
        // Stop grenade movement and go kinematic
        if (_rb != null)
        {
            _rb.linearVelocity  = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.bodyType        = RigidbodyType2D.Kinematic;
        }

        // Disable the physics collider so it no longer blocks objects
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        pullVFX?.Play();
        audioSource?.PlayOneShot(detonateClip);

        StartCoroutine(PullRoutine());
    }

    private IEnumerator PullRoutine()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position, pullRadius, pullMask);

            foreach (var hit in hits)
            {
                if (!hit.TryGetComponent<Rigidbody2D>(out var rb)) continue;
                if (rb.bodyType == RigidbodyType2D.Kinematic) continue;

                Vector2 toCenter = (Vector2)transform.position - rb.position;
                rb.AddForce(toCenter.normalized * pullStrength, ForceMode2D.Force);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        pullVFX?.Stop();
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, pullRadius);
    }
}
