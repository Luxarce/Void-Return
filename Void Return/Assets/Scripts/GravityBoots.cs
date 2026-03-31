using UnityEngine;

/// <summary>
/// Gravity Boots gadget — allows the player to walk on any surface regardless of gravity direction.
/// Has a stamina bar that drains while active and recharges while off.
/// Place this script on a child GameObject under the Player.
/// </summary>
public class GravityBoots : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Boots Stamina")]
    [Tooltip("Maximum stamina / duration the boots can stay active (in seconds).")]
    public float maxDuration = 30f;

    [Tooltip("Stamina drained per second while the boots are active.")]
    public float drainRate = 1f;

    [Tooltip("Stamina recharged per second while the boots are inactive.")]
    public float rechargeRate = 0.5f;

    [Header("VFX & Audio")]
    [Tooltip("Particle system that plays when boots are activated (boot glow effect).")]
    public ParticleSystem bootParticles;

    [Tooltip("AudioSource on this GameObject for playing boot toggle sounds.")]
    public AudioSource audioSource;

    [Tooltip("Sound clip played when toggling boots on or off.")]
    public AudioClip toggleClip;

    [Tooltip("Sound clip played each footstep while boots are active on a surface.")]
    public AudioClip footstepClip;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private float _stamina;
    private bool  _isActive;

    // ─────────────────────────────────────────────────────────────────────────
    // Public Properties
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns true when boots are on AND have remaining stamina.</summary>
    public bool IsActive => _isActive && _stamina > 0f;

    /// <summary>Stamina normalized between 0 and 1. Used by the HUD bar.</summary>
    public float StaminaNormalized => maxDuration > 0f ? _stamina / maxDuration : 0f;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _stamina = maxDuration;
    }

    private void Update()
    {
        if (_isActive && _stamina > 0f)
        {
            _stamina -= drainRate * Time.deltaTime;

            if (_stamina <= 0f)
            {
                _stamina = 0f;
                Deactivate();
                NotificationManager.Instance?.Show("Gravity Boots drained! Recharging...");
            }
        }
        else if (!_isActive && _stamina < maxDuration)
        {
            _stamina += rechargeRate * Time.deltaTime;
            _stamina  = Mathf.Min(_stamina, maxDuration);
        }

        // Update HUD stamina bar every frame
        GadgetHUDManager.Instance?.UpdateBootsBar(StaminaNormalized);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Toggle boots on or off. Called by PlayerController when gadget 1 is used.
    /// </summary>
    public void Toggle()
    {
        if (_isActive)
            Deactivate();
        else
            Activate();
    }

    /// <summary>
    /// Called by ShipRepairManager after Life Support module is repaired.
    /// Permanently increases maximum boots duration.
    /// </summary>
    public void ExtendMaxDuration(float bonusSeconds)
    {
        maxDuration += bonusSeconds;
        _stamina    += bonusSeconds;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void Activate()
    {
        if (_stamina <= 0f)
        {
            NotificationManager.Instance?.Show("Gravity Boots empty! Wait for recharge.");
            return;
        }

        _isActive = true;
        bootParticles?.Play();
        audioSource?.PlayOneShot(toggleClip);
    }

    private void Deactivate()
    {
        _isActive = false;
        bootParticles?.Stop();
        audioSource?.PlayOneShot(toggleClip);
    }
}
