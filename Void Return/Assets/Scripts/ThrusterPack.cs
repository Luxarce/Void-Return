using UnityEngine;

/// <summary>
/// Thruster Pack — boost-only gadget.
/// VFX particle system plays ONLY while thrusting. Stops on release or fuel empty.
/// Exposes IsThrustingDirection for PlayerController to tilt the player sprite.
/// </summary>
public class ThrusterPack : MonoBehaviour
{
    [Header("Boost")]
    public float boostForce          = 26f;
    public float boostFuelDrainRate  = 1.2f;

    [Header("Fuel")]
    [Range(1, 20)] public int maxFuelCharges = 8;
    public int fuelPerPickup = 3;

    [Header("VFX")]
    [Tooltip("Particle System — Play on Awake = OFF. Plays ONLY while boosting.")]
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

    public bool    IsActive           => _isThrusting;
    public float   FuelNormalized     => maxFuelCharges > 0 ? _fuelCharges / maxFuelCharges : 0f;
    /// <summary>Current thrust direction (world space). Zero when not thrusting.</summary>
    public Vector2 ThrustDirection    => _isThrusting ? _thrustDirection : Vector2.zero;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _fuelCharges = maxFuelCharges;
        _playerRb    = GetComponentInParent<Rigidbody2D>();
    }

    private void Start()
    {
        if (thrustVFX != null) { thrustVFX.Stop(); thrustVFX.Clear(); }
    }

    private void Update()
    {
        GadgetHUDManager.Instance?.UpdateThrusterFuel(FuelNormalized);
    }

    private void FixedUpdate()
    {
        if (!_isThrusting) return;
        _playerRb?.AddForce(_thrustDirection.normalized * boostForce, ForceMode2D.Force);
        _fuelCharges -= boostFuelDrainRate * Time.fixedDeltaTime;
        _fuelCharges  = Mathf.Max(0f, _fuelCharges);
        if (_fuelCharges <= 0f) StopBoost(true);
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void BeginThrust(Vector2 direction)
    {
        if (_fuelCharges <= 0f)
        {
            if (_isThrusting) StopBoost(true);
            else { audioSource?.PlayOneShot(emptyClip); NotificationManager.Instance?.ShowInfo("Thruster empty!"); }
            return;
        }
        if (!_isThrusting) { _isThrusting = true; audioSource?.PlayOneShot(boostClip); StartVFX(); }
        if (direction.sqrMagnitude > 0.01f) _thrustDirection = direction;
    }

    public void EndThrust()
    {
        if (_isThrusting) StopBoost(false);
    }

    public void Activate(Vector2 dir) => BeginThrust(dir);

    public void Refuel(int amount = -1)
    {
        int add = amount < 0 ? fuelPerPickup : amount;
        _fuelCharges = Mathf.Min(_fuelCharges + add, maxFuelCharges);
        audioSource?.PlayOneShot(rechargeClip);
        GadgetHUDManager.Instance?.UpdateThrusterFuel(FuelNormalized);
        NotificationManager.Instance?.ShowInfo($"Thruster refueled! ({_fuelCharges:F0}/{maxFuelCharges})");
    }

    public void Refuel(float amount) => Refuel((int)amount);

    // ─────────────────────────────────────────────────────────────────────────

    private void StopBoost(bool fuelEmpty)
    {
        _isThrusting     = false;
        _thrustDirection = Vector2.zero;
        StopVFX();
        if (fuelEmpty) { NotificationManager.Instance?.ShowInfo("Thruster fuel empty!"); GadgetHUDManager.Instance?.UpdateThrusterFuel(0f); }
    }

    private void StartVFX() { if (thrustVFX != null && !thrustVFX.isPlaying) thrustVFX.Play(); }
    private void StopVFX()  { if (thrustVFX != null && thrustVFX.isPlaying)  thrustVFX.Stop(); }
}
