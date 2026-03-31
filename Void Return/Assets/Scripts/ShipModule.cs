using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

// ─────────────────────────────────────────────────────────────────────────────
// Supporting Data Types
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Identifies which of the four spacecraft modules this is.
/// </summary>
public enum ModuleType
{
    LifeSupport,
    HullPlating,
    Navigation,
    EngineCore
}

/// <summary>
/// A single material cost entry for a repair stage.
/// Configure the type and required amount in the Inspector.
/// </summary>
[System.Serializable]
public class RepairRequirement
{
    [Tooltip("Which material type is required.")]
    public MaterialType materialType;

    [Tooltip("How many units of that material are needed.")]
    [Range(1, 20)]
    public int required = 2;
}

/// <summary>
/// One repair stage within a module. A module can have multiple stages.
/// Each stage has its own material cost and completion weight.
/// </summary>
[System.Serializable]
public class RepairStage
{
    [Tooltip("Display name for this stage (e.g., 'Patch Wiring', 'Seal Hull').")]
    public string stageName = "Stage";

    [Tooltip("List of materials required to complete this stage.")]
    public List<RepairRequirement> requirements = new();

    [Tooltip("How much this stage contributes to the module's overall progress (0 to 1). All stages should sum to 1.")]
    [Range(0f, 1f)]
    public float completionFraction = 0.5f;

    [HideInInspector]
    public bool isComplete = false;
}

// ─────────────────────────────────────────────────────────────────────────────
// ShipModule
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Manages the repair state of one spacecraft module.
/// Configure all stages and material requirements in the Inspector.
/// Attach to each module's GameObject alongside a ProximityInteraction script.
/// </summary>
public class ShipModule : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Module Identity")]
    [Tooltip("Display name for notifications and HUD (e.g., 'Life Support').")]
    public string moduleName = "Life Support";

    [Tooltip("Which module type this is. Must match the correct slot in ShipRepairManager.")]
    public ModuleType moduleType;

    [Header("Repair Stages")]
    [Tooltip("Define each stage of repair. Add one element per stage needed to fully repair this module.")]
    public List<RepairStage> stages = new();

    [Header("VFX")]
    [Tooltip("Particle system that plays during active repair (sparks, welding).")]
    public ParticleSystem repairParticles;

    [Tooltip("Particle system that plays when the module is fully repaired.")]
    public ParticleSystem completionParticles;

    [Header("Audio")]
    [Tooltip("AudioSource on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("Sound played when a repair stage completes.")]
    public AudioClip repairClip;

    [Tooltip("Sound played when the module is fully repaired.")]
    public AudioClip completionClip;

    [Header("Events — Wire These in the Inspector")]
    [Tooltip("Fired when all stages are complete and the module is fully repaired.")]
    public UnityEvent onModuleRepaired;

    [Tooltip("Fired every time a stage completes, passing the new progress value (0–1).")]
    public UnityEvent<float> onProgressChanged;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private int  _currentStageIndex;
    private bool _isFullyRepaired;

    // ─────────────────────────────────────────────────────────────────────────
    // Public Properties
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>True when all repair stages have been completed.</summary>
    public bool IsFullyRepaired => _isFullyRepaired;

    /// <summary>The index of the current unfinished repair stage. Used by SaveManager.</summary>
    public int CurrentStageIndex => _currentStageIndex;

    /// <summary>
    /// Overall repair progress from 0 to 1, based on completed stage fractions.
    /// </summary>
    public float Progress
    {
        get
        {
            float total = 0f;
            foreach (var stage in stages)
                if (stage.isComplete)
                    total += stage.completionFraction;
            return Mathf.Clamp01(total);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the current stage's material requirements are met in the Inventory.
    /// </summary>
    public bool CanRepairCurrentStage()
    {
        if (_isFullyRepaired || _currentStageIndex >= stages.Count) return false;

        var stage = stages[_currentStageIndex];
        foreach (var req in stage.requirements)
        {
            if (!Inventory.Instance.HasMaterials(req.materialType, req.required))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Attempts to complete the current repair stage.
    /// Called by ProximityInteraction when the player presses the interact key.
    /// Consumes required materials and advances the stage.
    /// </summary>
    public void AttemptRepair()
    {
        if (!CanRepairCurrentStage())
        {
            string missing = GetMissingMaterialsSummary();
            NotificationManager.Instance?.Show($"Missing materials:\n{missing}", urgent: false);
            return;
        }

        var stage = stages[_currentStageIndex];

        // Consume all required materials
        foreach (var req in stage.requirements)
            Inventory.Instance.ConsumeMaterials(req.materialType, req.required);

        stage.isComplete = true;
        _currentStageIndex++;

        repairParticles?.Play();
        audioSource?.PlayOneShot(repairClip);
        onProgressChanged?.Invoke(Progress);

        // Check for full completion
        if (_currentStageIndex >= stages.Count)
        {
            _isFullyRepaired = true;
            completionParticles?.Play();
            audioSource?.PlayOneShot(completionClip);
            onModuleRepaired?.Invoke();
            ShipRepairManager.Instance?.OnModuleRepaired(moduleType);
        }
        else
        {
            NotificationManager.Instance?.Show($"{moduleName} — Stage {_currentStageIndex} complete!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private string GetMissingMaterialsSummary()
    {
        if (_currentStageIndex >= stages.Count) return "None";

        var sb = new System.Text.StringBuilder();
        foreach (var req in stages[_currentStageIndex].requirements)
        {
            int have = Inventory.Instance.GetCount(req.materialType);
            if (have < req.required)
                sb.AppendLine($"  {req.materialType}: {have}/{req.required}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Restores the repair stage index when loading a save file.
    /// Called by SaveManager.Load() — do not call manually.
    /// </summary>
    public void LoadStageIndex(int stageIndex)
    {
        _currentStageIndex = Mathf.Clamp(stageIndex, 0, stages.Count);

        // Mark all stages before the loaded index as complete
        for (int i = 0; i < _currentStageIndex && i < stages.Count; i++)
            stages[i].isComplete = true;

        _isFullyRepaired = _currentStageIndex >= stages.Count;
        onProgressChanged?.Invoke(Progress);
    }
}
