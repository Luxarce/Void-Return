using UnityEngine;

/// <summary>
/// Thruster Pack gadget — applies directional thrust to the player in Zero-G.
/// Has limited fuel that depletes while active and can be refueled via pickups.
/// Place this script on a child GameObject under the Player.
/// </summary>
public class ThrusterPack : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Thruster Settings")]
    [Tooltip("Maximum fuel amount (arbitrary units).")]
    public float maxFuel = 100f;

    [Tooltip("Fuel consumed per second while thrusting.")]
    public float fuelDrainRate = 10f;

    [Tooltip("Force applied to the player's Rigidbody2D while thrusting.")]
    public float thrustForce = 20f;

    [Header("VFX & Audio")]
    [Tooltip("Particle system for the thruster exhaust effect. Should be a child of this GameObject.")]
    public ParticleSystem thrustVFX;

    [Tooltip("AudioSource on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("Sound clip for active thrust (looping).")]
    public AudioClip thrustClip;

    [Tooltip("Sound clip when fuel runs out.")]
    public AudioClip emptyClip;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private float        _fuel;
    private bool         _isActive;
    private Rigidbody2D  _playerRigidbody;

    // ─────────────────────────────────────────────────────────────────────────
    // Public Properties
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>True when the thruster is currently firing.</summary>
    public bool IsActive => _isActive;

    /// <summary>Current fuel as a normalized value (0–1) for the HUD bar.</summary>
    public float FuelNormalized => maxFuel > 0f ? _fuel / maxFuel : 0f;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _fuel = maxFuel;
        // Walk up to the parent to get the player's Rigidbody2D
        _playerRigidbody = GetComponentInParent<Rigidbody2D>();
    }

    private void Update()
    {
        // Auto-deactivate if fuel runs out
        if (_isActive && _fuel <= 0f)
        {
            Deactivate();
            audioSource?.PlayOneShot(emptyClip);
            NotificationManager.Instance?.Show("Thruster fuel empty!");
        }

        GadgetHUDManager.Instance?.UpdateThrusterFuel(FuelNormalized);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Activate thrust in the given input direction.
    /// Called by PlayerController each frame the gadget is used.
    /// Pass Vector2.zero to just check state without applying force.
    /// </summary>
    public void Activate(Vector2 direction)
    {
        if (_fuel <= 0f) return;

        _isActive  = true;
        _fuel     -= fuelDrainRate * Time.deltaTime;
        _fuel      = Mathf.Max(0f, _fuel);

        if (_playerRigidbody != null && direction.sqrMagnitude > 0.01f)
            _playerRigidbody.AddForce(direction.normalized * thrustForce);

        if (thrustVFX != null && !thrustVFX.isPlaying)
            thrustVFX.Play();

        if (audioSource != null && !audioSource.isPlaying)
            audioSource.PlayOneShot(thrustClip);
    }

    /// <summary>
    /// Stop thrusting.
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
        thrustVFX?.Stop();
    }

    /// <summary>
    /// Refuel the thruster pack (e.g., from a fuel cell pickup).
    /// </summary>
    public void Refuel(float amount)
    {
        _fuel = Mathf.Min(_fuel + amount, maxFuel);
    }
}
