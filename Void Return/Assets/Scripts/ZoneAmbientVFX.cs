using UnityEngine;

/// <summary>
/// Controls the ambient Particle System on a GravityZone.
/// Automatically activates/deactivates the zone's visual VFX when the player enters or exits.
/// Also adjusts emission color at runtime if needed.
///
/// SETUP:
/// 1. Add a Particle System as a child of the GravityZone GameObject.
/// 2. Attach this script to the GravityZone GameObject (same level as GravityZone.cs).
/// 3. Drag the child Particle System into the zoneParticles field.
/// 4. The particles will pulse when the player is inside the zone.
/// </summary>
public class ZoneAmbientVFX : MonoBehaviour
{
    [Header("VFX Reference")]
    [Tooltip("The Particle System child that forms the zone's ambient border effect.")]
    public ParticleSystem zoneParticles;

    [Header("Pulse on Player Entry")]
    [Tooltip("If true, increases the particle emission rate when the player enters the zone.")]
    public bool pulseOnPlayerEntry = true;

    [Tooltip("Normal emission rate (particles per second) when the player is NOT inside.")]
    public float idleEmissionRate = 5f;

    [Tooltip("Elevated emission rate when the player IS inside the zone.")]
    public float activeEmissionRate = 20f;

    // ─────────────────────────────────────────────────────────────────────────
    private void Start()
    {
        if (zoneParticles != null)
        {
            var emission = zoneParticles.emission;
            emission.rateOverTime = idleEmissionRate;
            zoneParticles.Play();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !pulseOnPlayerEntry) return;
        SetEmissionRate(activeEmissionRate);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !pulseOnPlayerEntry) return;
        SetEmissionRate(idleEmissionRate);
    }

    private void SetEmissionRate(float rate)
    {
        if (zoneParticles == null) return;
        var emission       = zoneParticles.emission;
        emission.rateOverTime = rate;
    }
}
