using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages the player's oxygen supply.
///
/// ADDITIONS:
///  — TakeDamageFromMeteorite(float amount): called by MeteoriteManager when
///    a meteorite hits the player. Drains oxygen as a direct damage mechanic.
///  — RefillAtLifeSupport(float amount): called when the player stands inside
///    the Life Support module's zone and presses E. Gradually restores oxygen.
///    Can also be used as a full instant refill if amount >= maxOxygen.
///  — Warning flags reset when oxygen recovers above thresholds so they can
///    fire again if oxygen drops low a second time.
/// </summary>
public class OxygenSystem : MonoBehaviour
{
    [Header("Oxygen Supply")]
    [Tooltip("Maximum oxygen in seconds of survival time.")]
    public float maxOxygen = 180f;

    [Tooltip("Oxygen drained per second during normal movement.")]
    public float normalDrainRate = 1f;

    [Tooltip("Oxygen drained per second during exertion (thruster use, running).")]
    public float exertionDrainRate = 2f;

    [Tooltip("Multiplier applied to the drain rate inside a hull breach zone.")]
    public float breachDrainMultiplier = 3f;

    [Header("Canister Recovery")]
    [Tooltip("Oxygen seconds restored by picking up one oxygen canister.")]
    public float canisterRestoreAmount = 30f;

    [Header("Life Support Refill")]
    [Tooltip("Oxygen restored per second while standing inside the Life Support zone " +
             "and the module is at least partially repaired (stage 1 complete).")]
    public float lifeSupportRefillRate = 5f;

    [Header("Warning Thresholds")]
    [Tooltip("Normalized oxygen below which the low-oxygen warning fires (0–1).")]
    [Range(0.05f, 0.5f)]
    public float warningThreshold  = 0.30f;

    [Tooltip("Normalized oxygen below which the critical warning fires (0–1).")]
    [Range(0.01f, 0.2f)]
    public float criticalThreshold = 0.10f;

    [Header("Events — Wire via GameManager or Inspector")]
    [Tooltip("Fires every frame with the normalized oxygen value (0–1). Wire to GameHUD.UpdateOxygen.")]
    public UnityEvent<float> onOxygenChanged;

    [Tooltip("Fires once when oxygen drops below warningThreshold.")]
    public UnityEvent onLowOxygen;

    [Tooltip("Fires once when oxygen drops below criticalThreshold.")]
    public UnityEvent onCriticalOxygen;

    [Tooltip("Fires when oxygen reaches zero.")]
    public UnityEvent onOxygenDepleted;

    // ─────────────────────────────────────────────────────────────────────────
    private float _currentOxygen;
    private bool  _isExerting;
    private bool  _inBreachZone;
    private bool  _lowWarningFired;
    private bool  _criticalWarningFired;
    private bool  _depleted;

    // ─────────────────────────────────────────────────────────────────────────
    // Public Properties
    // ─────────────────────────────────────────────────────────────────────────

    public float OxygenNormalized => maxOxygen > 0f ? _currentOxygen / maxOxygen : 0f;
    public bool  IsDepleted       => _depleted;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        _currentOxygen = maxOxygen;
        onOxygenChanged?.Invoke(1f);
    }

    private void Update()
    {
        if (_depleted) return;

        // Calculate drain rate
        float rate = _isExerting ? exertionDrainRate : normalDrainRate;
        if (_inBreachZone) rate *= breachDrainMultiplier;

        _currentOxygen = Mathf.Max(0f, _currentOxygen - rate * Time.deltaTime);
        onOxygenChanged?.Invoke(OxygenNormalized);

        CheckWarnings();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // State Setters (called by other components)
    // ─────────────────────────────────────────────────────────────────────────

    public void SetExertion(bool isExerting) => _isExerting = isExerting;
    public void SetBreachZone(bool inBreach) => _inBreachZone = inBreach;

    // ─────────────────────────────────────────────────────────────────────────
    // Oxygen Restore Methods
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Restores oxygen when the player collects an oxygen canister.
    /// </summary>
    public void CollectCanister()
    {
        AddOxygen(canisterRestoreAmount);
        NotificationManager.Instance?.Show("Oxygen canister collected!");
    }

    /// <summary>
    /// Called each frame by LifeSupportZone while the player stands in range.
    /// Restores oxygen gradually at lifeSupportRefillRate per second.
    /// </summary>
    public void RefillAtLifeSupport()
    {
        if (_currentOxygen >= maxOxygen) return;
        AddOxygen(lifeSupportRefillRate * Time.deltaTime);
    }

    /// <summary>
    /// Direct oxygen damage from a meteorite hit.
    /// Called by MeteoriteManager.NotifyPlayerHit().
    /// </summary>
    public void TakeDamageFromMeteorite(float amount)
    {
        if (_depleted) return;
        _currentOxygen = Mathf.Max(0f, _currentOxygen - amount);
        onOxygenChanged?.Invoke(OxygenNormalized);
        CheckWarnings();
        Debug.Log($"[OxygenSystem] Meteorite hit — oxygen reduced by {amount}. " +
                  $"Remaining: {_currentOxygen:F1}");
    }

    /// <summary>
    /// Called by ShipRepairManager after Life Support is fully repaired.
    /// Permanently increases max oxygen.
    /// </summary>
    public void ExtendMaxOxygen(float bonusSeconds)
    {
        maxOxygen      += bonusSeconds;
        _currentOxygen  = Mathf.Min(maxOxygen, _currentOxygen + bonusSeconds * 0.5f);
        onOxygenChanged?.Invoke(OxygenNormalized);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void AddOxygen(float amount)
    {
        _currentOxygen = Mathf.Min(maxOxygen, _currentOxygen + amount);
        onOxygenChanged?.Invoke(OxygenNormalized);

        // Reset warning flags if oxygen has recovered above thresholds
        if (OxygenNormalized > warningThreshold)  _lowWarningFired      = false;
        if (OxygenNormalized > criticalThreshold) _criticalWarningFired = false;
        _depleted = false;
    }

    private void CheckWarnings()
    {
        if (!_lowWarningFired && OxygenNormalized <= warningThreshold)
        {
            _lowWarningFired = true;
            onLowOxygen?.Invoke();
        }
        if (!_criticalWarningFired && OxygenNormalized <= criticalThreshold)
        {
            _criticalWarningFired = true;
            onCriticalOxygen?.Invoke();
        }
        if (!_depleted && _currentOxygen <= 0f)
        {
            _depleted = true;
            onOxygenDepleted?.Invoke();
        }
    }

    /// <summary>Instantly restores oxygen to maximum. Called by SaveManager.Load().</summary>
    public void RestoreFullOxygen()
    {
        _currentOxygen        = maxOxygen;
        _lowWarningFired      = false;
        _criticalWarningFired = false;
        _depleted             = false;
        onOxygenChanged?.Invoke(1f);
    }
}