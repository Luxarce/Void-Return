using UnityEngine;

/// <summary>
/// Defines a hull breach zone. When the player enters, oxygen drains faster
/// and a warning notification is displayed. On exit, drain rate returns to normal.
///
/// Attach to any GameObject with a Trigger Collider2D that marks a breach area.
/// The OxygenSystem on the player handles the actual increased drain rate.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BreachZone : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Breach Settings")]
    [Tooltip("Label shown in the warning notification when the player enters.")]
    public string breachLabel = "Hull Breach — Oxygen draining rapidly!";

    [Tooltip("If true, a notification is shown when the player enters this breach zone.")]
    public bool showWarningOnEnter = true;

    [Header("VFX")]
    [Tooltip("Optional particle system that activates while the player is in the breach zone.")]
    public ParticleSystem breachParticles;

    [Header("Audio")]
    [Tooltip("AudioSource on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("Sound that plays on loop while the player is inside the breach zone.")]
    public AudioClip breachAmbientClip;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Trigger Detection
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // Tell the OxygenSystem to increase drain rate
        var oxygen = other.GetComponent<OxygenSystem>();
        oxygen?.SetBreachZone(true);

        if (showWarningOnEnter)
            NotificationManager.Instance?.Show(breachLabel, urgent: true);

        breachParticles?.Play();

        if (audioSource != null && breachAmbientClip != null)
        {
            audioSource.clip = breachAmbientClip;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var oxygen = other.GetComponent<OxygenSystem>();
        oxygen?.SetBreachZone(false);

        breachParticles?.Stop();
        audioSource?.Stop();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.25f);
        var col = GetComponent<BoxCollider2D>();
        if (col != null)
            Gizmos.DrawWireCube(transform.position + (Vector3)(Vector2)col.offset, col.size);
    }
}
