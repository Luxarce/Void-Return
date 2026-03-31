using UnityEngine;

/// <summary>
/// Controls a floating debris object's physics behavior in zero-gravity space.
/// Gives debris a natural, slow drifting tumble and allows it to react to
/// tether pulls and grenade gravity wells.
///
/// Attach to any debris prefab alongside Rigidbody2D and a Collider2D.
/// Layer should be set to 'Debris'.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class DebrisObject : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Drift Settings")]
    [Tooltip("Minimum initial drift speed on spawn.")]
    [Range(0f, 3f)]
    public float minDriftSpeed = 0.1f;

    [Tooltip("Maximum initial drift speed on spawn.")]
    [Range(0f, 5f)]
    public float maxDriftSpeed = 1.2f;

    [Tooltip("Minimum initial angular (tumble) speed on spawn (degrees/second).")]
    [Range(0f, 90f)]
    public float minTumbleSpeed = 5f;

    [Tooltip("Maximum initial angular speed on spawn.")]
    [Range(0f, 180f)]
    public float maxTumbleSpeed = 45f;

    [Tooltip("Linear drag applied to the Rigidbody2D. Low value for slow natural deceleration.")]
    [Range(0f, 1f)]
    public float driftDrag = 0.05f;

    [Tooltip("Angular drag to slowly reduce tumble over time.")]
    [Range(0f, 1f)]
    public float tumbleDrag = 0.1f;

    [Header("Collision Bounce")]
    [Tooltip("How bouncy the debris is when it collides with other objects.")]
    [Range(0f, 1f)]
    public float bounciness = 0.4f;

    [Header("Audio")]
    [Tooltip("AudioSource on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("Sound played when debris collides with another object (impact sound).")]
    public AudioClip[] collisionClips;

    [Tooltip("Minimum collision velocity to trigger the impact sound.")]
    public float minImpactVelocity = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        var rb = GetComponent<Rigidbody2D>();

        // All debris floats — no gravity
        rb.gravityScale = 0f;
        rb.linearDamping = driftDrag;
        rb.angularDamping = tumbleDrag;

        // Apply random initial drift and tumble on spawn
        Vector2 driftDir = Random.insideUnitCircle.normalized;
        float   driftSpd = Random.Range(minDriftSpeed, maxDriftSpeed);
        rb.linearVelocity = driftDir * driftSpd;

        float tumble = Random.Range(minTumbleSpeed, maxTumbleSpeed);
        rb.angularVelocity = Random.value > 0.5f ? tumble : -tumble;

        // Apply physics material for bounciness
        var mat = new PhysicsMaterial2D("DebrisMat")
        {
            bounciness = this.bounciness,
            friction   = 0.1f
        };

        GetComponent<Collider2D>().sharedMaterial = mat;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (collisionClips == null || collisionClips.Length == 0) return;
        if (audioSource == null) return;

        float impactSpeed = col.relativeVelocity.magnitude;
        if (impactSpeed < minImpactVelocity) return;

        AudioClip clip = collisionClips[Random.Range(0, collisionClips.Length)];
        float volume = Mathf.Clamp01(impactSpeed / 10f);
        audioSource.PlayOneShot(clip, volume);
    }
}
