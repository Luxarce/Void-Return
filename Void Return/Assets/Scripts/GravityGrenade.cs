using UnityEngine;
using System.Collections;

/// <summary>
/// The Gravity Grenade projectile itself.
/// After a short flight delay, it detonates and creates a temporary pull zone
/// that attracts all Rigidbody2D objects within radius toward the detonation point.
/// Place this script on the Grenade prefab (with Rigidbody2D and CircleCollider2D).
/// Values are set by GravityGrenadeLauncher at spawn time.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GravityGrenade : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Pull Zone")]
    [Tooltip("Radius of the gravity pull zone created on detonation.")]
    public float pullRadius = 8f;

    [Tooltip("Pull force per FixedUpdate frame applied to objects in range.")]
    public float pullStrength = 10f;

    [Tooltip("How many seconds the pull zone remains active after detonation.")]
    public float duration = 5f;

    [Header("Timing")]
    [Tooltip("Seconds after launch before the grenade detonates.")]
    public float fuseTime = 1.5f;

    [Header("VFX & Audio")]
    [Tooltip("Particle system that plays during the pull phase (detonation effect).")]
    public ParticleSystem pullVFX;

    [Tooltip("AudioSource on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("Sound played on detonation.")]
    public AudioClip detonateClip;

    [Tooltip("LayerMask for objects the grenade can pull. Set to Debris and Player layers.")]
    public LayerMask pullMask;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        Invoke(nameof(Detonate), fuseTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private Logic
    // ─────────────────────────────────────────────────────────────────────────

    private void Detonate()
    {
        // Stop grenade movement
        if (TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType       = RigidbodyType2D.Kinematic;
        }

        // Disable collider so it no longer blocks things
        if (TryGetComponent<Collider2D>(out var col))
            col.enabled = false;

        pullVFX?.Play();
        audioSource?.PlayOneShot(detonateClip);

        StartCoroutine(PullRoutine());
    }

    private IEnumerator PullRoutine()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Each physics step, pull all Rigidbody2D objects within radius
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, pullRadius, pullMask);

            foreach (var hit in hits)
            {
                if (!hit.TryGetComponent<Rigidbody2D>(out var hitRb)) continue;

                Vector2 toCenter = (Vector2)transform.position - hitRb.position;
                hitRb.AddForce(toCenter.normalized * pullStrength);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        pullVFX?.Stop();
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, pullRadius);
    }
}
