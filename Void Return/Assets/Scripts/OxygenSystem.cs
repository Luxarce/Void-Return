using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages the player's oxygen supply.
/// Oxygen depletes over time and faster during exertion or inside hull breach zones.
/// Connect the UnityEvents in the Inspector to wire HUD updates and warning triggers.
/// </summary>
public class OxygenSystem : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Oxygen Supply")]
    [Tooltip("Maximum oxygen in seconds of survival time.")]
    public float maxOxygen = 180f;

    [Tooltip("Oxygen drained per second during normal movement.")]
    public float normalDrainRate = 1f;

    [Tooltip("Oxygen drained per second while the player is using the thruster pack or running.")]
    public float exertionDrainRate = 2f;

    [Tooltip("Multiplier applied to the current drain rate when inside a hull breach zone.")]
    public float breachDrainMultiplier = 3f;

    [Header("Canister Recovery")]
    [Tooltip("Oxygen seconds restored when the player collects one oxygen canister.")]
    public float canisterRestoreAmount = 30f;

    [Header("Warning Thresholds")]
    [Tooltip("Normalized oxygen level (0–1) at which the low oxygen warning event fires.")]
    [Range(0.05f, 0.5f)]
    public float warningThreshold = 0.30f;

    [Tooltip("Normalized oxygen level (0–1) at which the critical oxygen event fires.")]
    [Range(0.01f, 0.2f)]
    public float criticalThreshold = 0.10f;

    [Header("Events — Wire These in the Inspector")]
    [Tooltip("Fired once when oxygen drops below the warning threshold.")]
    public UnityEvent onLowOxygen;

    [Tooltip("Fired once when oxygen drops below the critical threshold.")]
    public UnityEvent onCriticalOxygen;

    [Tooltip("Fired when oxygen reaches zero (game over trigger).")]
    public UnityEvent onOxygenDepleted;

    [Tooltip("Fired every frame with the normalized value (0–1). Wire to GameHUD.UpdateOxygen.")]
    public UnityEvent<float> onOxygenChanged;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
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

    /// <summary>Current oxygen as a normalized value between 0 and 1.</summary>
    public float OxygenNormalized => maxOxygen > 0f ? _currentOxygen / maxOxygen : 0f;

    /// <summary>True when oxygen has fully run out.</summary>
    public bool IsDepleted => _depleted;

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

        float rate = _isExerting ? exertionDrainRate : normalDrainRate;
        if (_inBreachZone) rate *= breachDrainMultiplier;

        _currentOxygen = Mathf.Max(0f, _currentOxygen - rate * Time.deltaTime);
        onOxygenChanged?.Invoke(OxygenNormalized);

        CheckWarningThresholds();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void CheckWarningThresholds()
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

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Called by Other Scripts
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call with true when the player starts heavy activity (thrusting, sprinting).
    /// Call with false when activity stops.
    /// </summary>
    public void SetExertion(bool isExerting)
    {
        _isExerting = isExerting;
    }

    /// <summary>
    /// Call with true when the player enters a hull breach area.
    /// Call with false when they leave.
    /// </summary>
    public void SetBreachZone(bool inBreach)
    {
        _inBreachZone = inBreach;
    }

    /// <summary>
    /// Restore oxygen when the player picks up an oxygen canister.
    /// </summary>
    public void CollectCanister()
    {
        _currentOxygen = Mathf.Min(maxOxygen, _currentOxygen + canisterRestoreAmount);

        // Reset warning flags if oxygen recovered above thresholds
        if (OxygenNormalized > warningThreshold)  _lowWarningFired      = false;
        if (OxygenNormalized > criticalThreshold) _criticalWarningFired = false;

        _depleted = false;
        onOxygenChanged?.Invoke(OxygenNormalized);
    }

    /// <summary>
    /// Called by ShipRepairManager when Life Support module is fully repaired.
    /// Permanently increases max oxygen and partially refills current supply.
    /// </summary>
    public void ExtendMaxOxygen(float bonusSeconds)
    {
        maxOxygen      += bonusSeconds;
        _currentOxygen  = Mathf.Min(maxOxygen, _currentOxygen + bonusSeconds * 0.5f);
        onOxygenChanged?.Invoke(OxygenNormalized);
    }
}
