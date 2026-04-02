using UnityEngine;

/// <summary>
/// Thruster Pack — directional DASH gadget.
///
/// REDESIGN: Thruster is now a dash, not a sustained speed boost.
///  — Left-click fires a single burst of force in the WASD input direction.
///  — The burst is applied as a single ForceMode2D.Impulse (instantaneous).
///  — After each dash, there is a cooldown before the next dash is available.
///  — Fuel = number of dashes remaining. Replenish via Fuel Cell pickups.
///
/// This replaces the old "hold LMB for sustained thrust" mechanic.
/// Now: select V, then left-click to dash in the direction you are pressing.
/// </summary>
public class ThrusterPack : MonoBehaviour
{
    [Header("Dash Settings")]
    [Tooltip("Impulse force of each dash burst. Higher = further/faster dash.")]
    public float dashForce = 18f;

    [Tooltip("Cooldown in seconds between dashes.")]
    public float dashCooldown = 0.8f;

    [Tooltip("Maximum number of dashes before fuel runs out.")]
    [Range(1, 20)]
    public int maxFuelCharges = 8;

    [Tooltip("How many fuel charges are restored per Fuel Cell pickup.")]
    public int fuelPerPickup = 3;

    [Header("Visual Feedback")]
    [Tooltip("Particle system burst played on each dash.")]
    public ParticleSystem thrustVFX;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   dashClip;
    public AudioClip   emptyClip;
    public AudioClip   rechargeClip;

    // ─────────────────────────────────────────────────────────────────────────
    private int          _fuelCharges;
    private float        _cooldownTimer;
    private bool         _isDashing;      // true for one frame after a dash
    private Rigidbody2D  _playerRb;

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>True for exactly one frame after a dash fires (for animation).</summary>
    public bool  IsActive        => _isDashing;

    /// <summary>Fuel normalized 0-1 for the HUD bar.</summary>
    public float FuelNormalized  => maxFuelCharges > 0
        ? (float)_fuelCharges / maxFuelCharges : 0f;

    /// <summary>True when the cooldown has expired and a dash is ready.</summary>
    public bool  CanDash         => _cooldownTimer <= 0f && _fuelCharges > 0;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _fuelCharges  = maxFuelCharges;
        _playerRb     = GetComponentInParent<Rigidbody2D>();
    }

    private void Update()
    {
        // Clear the IsActive flag after one frame
        if (_isDashing) _isDashing = false;

        // Count down cooldown
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        GadgetHUDManager.Instance?.UpdateThrusterFuel(FuelNormalized);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires a single dash in the given direction.
    /// direction should be the player's WASD input (or mouse aim direction).
    /// Called by PlayerController when gadget V is activated (left-click).
    /// </summary>
    public void Activate(Vector2 direction)
    {
        if (_fuelCharges <= 0)
        {
            audioSource?.PlayOneShot(emptyClip);
            NotificationManager.Instance?.ShowInfo(
                "Thruster fuel empty! Collect Fuel Cells to refuel.");
            return;
        }

        if (_cooldownTimer > 0f)
        {
            // Cooldown still running — show remaining time
            NotificationManager.Instance?.ShowInfo(
                $"Thruster charging... {_cooldownTimer:F1}s");
            return;
        }

        if (direction.sqrMagnitude < 0.01f)
        {
            // No direction input — dash in the facing direction instead
            direction = Vector2.right; // fallback; PlayerController passes actual input
        }

        // Fire the dash impulse
        _playerRb?.AddForce(direction.normalized * dashForce, ForceMode2D.Impulse);

        _fuelCharges--;
        _cooldownTimer = dashCooldown;
        _isDashing     = true;

        // Trigger a particle burst (not a looping effect)
        if (thrustVFX != null)
        {
            thrustVFX.Stop();
            thrustVFX.Play();
        }

        audioSource?.PlayOneShot(dashClip);
        GadgetHUDManager.Instance?.UpdateThrusterFuel(FuelNormalized);

        Debug.Log($"[Thruster] Dash fired in direction {direction}. " +
                  $"Fuel remaining: {_fuelCharges}/{maxFuelCharges}");
    }

    /// <summary>
    /// Refuel the thruster pack. Called by Fuel Cell pickups.
    /// amount defaults to fuelPerPickup.
    /// </summary>
    public void Refuel(int amount = -1)
    {
        int add = amount < 0 ? fuelPerPickup : amount;
        _fuelCharges = Mathf.Min(_fuelCharges + add, maxFuelCharges);
        audioSource?.PlayOneShot(rechargeClip);
        GadgetHUDManager.Instance?.UpdateThrusterFuel(FuelNormalized);
        NotificationManager.Instance?.ShowInfo(
            $"Thruster refueled! ({_fuelCharges}/{maxFuelCharges} dashes)");
    }

    /// <summary>Old float overload kept for backward compatibility with existing code.</summary>
    public void Refuel(float amount) => Refuel((int)amount);
}
