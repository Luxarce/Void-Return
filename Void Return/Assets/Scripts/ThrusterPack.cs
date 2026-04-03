using UnityEngine;

/// <summary>
/// Thruster Pack — pure boost gadget.
///
/// REDESIGN (no dash):
///  Pressing and holding either the Space key OR Left Mouse Button applies a
///  continuous force (boostForce) to the player every FixedUpdate.
///  There is no dash / tap mode — just sustained thrust in the input direction.
///  Releasing the key/button stops the thrust.
///
///  The jetpack VFX particle system plays ONLY while thrusting and stops the
///  moment the input is released or fuel runs out.
///
///  Fuel depletes at boostFuelDrainRate per second while active.
/// </summary>
public class ThrusterPack : MonoBehaviour
{
    [Header("Boost Settings")]
    [Tooltip("Continuous force applied to the player's Rigidbody2D every FixedUpdate while active.")]
    public float boostForce = 26f;

    [Tooltip("Fuel charges drained per second while boosting. Lower = longer duration.")]
    public float boostFuelDrainRate = 1.2f;

    [Header("Fuel")]
    [Tooltip("Maximum fuel charge count.")]
    [Range(1, 20)]
    public int maxFuelCharges = 8;

    [Tooltip("Fuel charges restored per Fuel Cell pickup.")]
    public int fuelPerPickup = 3;

    [Header("VFX — Jetpack Particles")]
    [Tooltip("Particle System that plays ONLY while thrusting. " +
             "Attach a child Particle System here. Set Play on Awake = OFF.")]
    public ParticleSystem thrustVFX;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   boostClip;
    public AudioClip   emptyClip;
    public AudioClip   rechargeClip;

    // ─────────────────────────────────────────────────────────────────────────
    private float       _fuelCharges;
    private bool        _isThrusting;
    private Vector2     _thrustDirection;
    private Rigidbody2D _playerRb;

    // ─────────────────────────────────────────────────────────────────────────

    public bool  IsActive       => _isThrusting;
    public float FuelNormalized => maxFuelCharges > 0 ? _fuelCharges / maxFuelCharges : 0f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _fuelCharges = maxFuelCharges;
        _playerRb    = GetComponentInParent<Rigidbody2D>();
    }

    private void Start()
    {
        // Ensure VFX is off at start
        if (thrustVFX != null)
        {
            thrustVFX.Stop();
            thrustVFX.Clear();
        }
    }

    private void Update()
    {
        GadgetHUDManager.Instance?.UpdateThrusterFuel(FuelNormalized);
    }

    private void FixedUpdate()
    {
        if (!_isThrusting) return;

        // Apply continuous boost force
        _playerRb?.AddForce(_thrustDirection.normalized * boostForce, ForceMode2D.Force);

        // Drain fuel
        _fuelCharges -= boostFuelDrainRate * Time.fixedDeltaTime;
        _fuelCharges  = Mathf.Max(0f, _fuelCharges);

        if (_fuelCharges <= 0f)
        {
            StopBoost(fuelEmpty: true);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — called by PlayerController on key/button press
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called every frame the thrust key/button is held.
    /// Starts or continues boosting in the given direction.
    /// PlayerController calls this for both Space key and LMB hold.
    /// </summary>
    public void BeginThrust(Vector2 direction)
    {
        if (_fuelCharges <= 0f)
        {
            if (_isThrusting) StopBoost(fuelEmpty: true);
            else
            {
                audioSource?.PlayOneShot(emptyClip);
                NotificationManager.Instance?.ShowInfo("Thruster fuel empty! Collect Fuel Cells.");
            }
            return;
        }

        if (!_isThrusting)
        {
            _isThrusting = true;
            audioSource?.PlayOneShot(boostClip);
            StartVFX();
        }

        // Update direction every frame so the player can steer while boosting
        if (direction.sqrMagnitude > 0.01f)
            _thrustDirection = direction;
    }

    /// <summary>
    /// Called when the thrust key/button is released.
    /// Stops boosting.
    /// </summary>
    public void EndThrust()
    {
        if (!_isThrusting) return;
        StopBoost(fuelEmpty: false);
    }

    /// <summary>Backward-compatible wrapper used by older code paths.</summary>
    public void Activate(Vector2 direction) => BeginThrust(direction);

    public void Refuel(int amount = -1)
    {
        int add      = amount < 0 ? fuelPerPickup : amount;
        _fuelCharges = Mathf.Min(_fuelCharges + add, maxFuelCharges);
        audioSource?.PlayOneShot(rechargeClip);
        GadgetHUDManager.Instance?.UpdateThrusterFuel(FuelNormalized);
        NotificationManager.Instance?.ShowInfo(
            $"Thruster refueled! ({_fuelCharges:F0}/{maxFuelCharges} charges)");
    }

    public void Refuel(float amount) => Refuel((int)amount);

    // ─────────────────────────────────────────────────────────────────────────

    private void StopBoost(bool fuelEmpty)
    {
        _isThrusting     = false;
        _thrustDirection = Vector2.zero;
        StopVFX();

        if (fuelEmpty)
        {
            NotificationManager.Instance?.ShowInfo("Thruster fuel empty!");
            GadgetHUDManager.Instance?.UpdateThrusterFuel(0f);
        }
    }

    private void StartVFX()
    {
        if (thrustVFX == null) return;
        if (!thrustVFX.isPlaying)
            thrustVFX.Play();
    }

    private void StopVFX()
    {
        if (thrustVFX == null) return;
        if (thrustVFX.isPlaying)
            thrustVFX.Stop();
    }
}
