using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

public enum ModuleType { LifeSupport, HullPlating, Navigation, EngineCore }

[System.Serializable]
public class RepairRequirement
{
    public MaterialType materialType;
    [Range(1, 20)] public int required = 2;
}

[System.Serializable]
public class RepairStage
{
    public string stageName = "Stage";
    public List<RepairRequirement> requirements = new();
    [Range(0f, 1f)] public float completionFraction = 0.5f;
    [HideInInspector] public bool isComplete = false;
}

/// <summary>
/// Manages repair state for one ship module.
/// Calls ShipRepairManager.OnStageCompleted() after each stage.
/// Calls ShipRepairManager.OnModuleRepaired() and fires onModuleRepaired after final stage.
/// Also directly refreshes UI panels immediately after repair (no event chain delay).
/// </summary>
public class ShipModule : MonoBehaviour
{
    [Header("Module Identity")]
    public string     moduleName = "Life Support";
    public ModuleType moduleType;

    [Header("Repair Stages")]
    [Tooltip("Add one stage per repair phase. Completion Fractions must sum to 1.0.")]
    public List<RepairStage> stages = new();

    [Header("VFX")]
    public ParticleSystem repairParticles;
    public ParticleSystem completionParticles;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   repairClip;
    public AudioClip   completionClip;

    [Header("Events — leave empty, wired by GameManager in code")]
    public UnityEvent        onModuleRepaired;
    public UnityEvent<float> onProgressChanged;

    // ─────────────────────────────────────────────────────────────────────────
    private int  _currentStageIndex;
    private bool _isFullyRepaired;
    private bool _readyNotificationShown;

    public bool  IsFullyRepaired   => _isFullyRepaired;
    public int   CurrentStageIndex => _currentStageIndex;
    public bool  Stage1Complete    => _currentStageIndex > 0 || _isFullyRepaired;

    public float Progress
    {
        get
        {
            float t = 0f;
            if (stages != null)
                foreach (var s in stages)
                    if (s.isComplete) t += s.completionFraction;
            return Mathf.Clamp01(t);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        ValidateStages();
        FireProgressAndRefreshUI();
        Invoke(nameof(DiagnosticFire), 1f);
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged += CheckAndNotifyReady;
    }

    private void DiagnosticFire()
    {
        Debug.Log($"[ShipModule:{moduleName}] Diagnostic — progress={Progress:F3}");
        FireProgressAndRefreshUI();
    }

    private void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= CheckAndNotifyReady;
    }

    private void ValidateStages()
    {
        if (stages == null || stages.Count == 0)
        { Debug.LogError($"[ShipModule:{moduleName}] No stages. Add stages in Inspector."); return; }
        float total = 0f;
        foreach (var s in stages) total += s.completionFraction;
        if (Mathf.Abs(total - 1f) > 0.05f)
            Debug.LogWarning($"[ShipModule:{moduleName}] Stage fractions sum to {total:F2}, should be 1.0.");
    }

    private void FireProgressAndRefreshUI()
    {
        onProgressChanged?.Invoke(Progress);
        FindFirstObjectByType<GameHUD>()?.RefreshUpgradeList();
        FindFirstObjectByType<MaterialRequirementsUI>()?.RefreshAll();
    }

    // ─────────────────────────────────────────────────────────────────────────

    public bool CanRepairCurrentStage()
    {
        if (_isFullyRepaired || stages == null || stages.Count == 0) return false;
        if (_currentStageIndex >= stages.Count)                       return false;
        if (Inventory.Instance == null)                               return false;
        foreach (var req in stages[_currentStageIndex].requirements)
            if (!Inventory.Instance.HasMaterials(req.materialType, req.required)) return false;
        return true;
    }

    public void AttemptRepair()
    {
        Debug.Log($"[ShipModule:{moduleName}] AttemptRepair stage={_currentStageIndex}/{stages?.Count ?? 0}");

        if (_isFullyRepaired)
        { NotificationManager.Instance?.ShowInfo($"{moduleName} is fully repaired."); return; }
        if (stages == null || stages.Count == 0)
        { Debug.LogError($"[ShipModule:{moduleName}] No stages configured."); return; }
        if (Inventory.Instance == null)
        { Debug.LogError("[ShipModule] Inventory is null."); return; }
        if (!CanRepairCurrentStage())
        { NotificationManager.Instance?.ShowInfo($"{moduleName} — Stage {_currentStageIndex + 1}\nStill need:\n{BuildMissingText()}"); return; }

        // Consume materials
        foreach (var req in stages[_currentStageIndex].requirements)
            if (!Inventory.Instance.ConsumeMaterials(req.materialType, req.required))
            { Debug.LogError($"[ShipModule:{moduleName}] ConsumeMaterials failed for {req.materialType}."); return; }

        stages[_currentStageIndex].isComplete = true;
        _currentStageIndex++;
        _readyNotificationShown = false;

        repairParticles?.Play();
        audioSource?.PlayOneShot(repairClip);

        Debug.Log($"[ShipModule:{moduleName}] Stage {_currentStageIndex} complete. Progress={Progress:P0}");

        // Per-stage upgrades (gadget unlock, oxygen boost etc)
        ShipRepairManager.Instance?.OnStageCompleted(moduleType, _currentStageIndex);

        // Immediate UI refresh
        FireProgressAndRefreshUI();

        if (_currentStageIndex >= stages.Count)
        {
            // Module fully repaired
            _isFullyRepaired = true;
            completionParticles?.Play();
            audioSource?.PlayOneShot(completionClip);
            NotificationManager.Instance?.ShowInfo($"{moduleName.ToUpper()} FULLY REPAIRED!");

            // Fire event (GameManager listener calls ShipRepairManager.OnModuleRepaired)
            onModuleRepaired?.Invoke();

            // Also call directly on ShipRepairManager — belt-and-suspenders
            // in case GameManager wiring is missing or hasn't run yet
            ShipRepairManager.Instance?.OnModuleRepaired(moduleType);

            FireProgressAndRefreshUI();
        }
        else
        {
            NotificationManager.Instance?.ShowInfo(
                $"{moduleName} — Stage {_currentStageIndex} complete!\nNext: {stages[_currentStageIndex].stageName}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    public string GetProximityPrompt()
    {
        if (stages == null || stages.Count == 0)
            return $"{moduleName}\n[!] No stages — check Inspector.";
        if (_isFullyRepaired)
            return $"{moduleName} — [DONE] Fully repaired";
        if (CanRepairCurrentStage())
            return $"{moduleName} — Stage {_currentStageIndex + 1}\nPress [E] to repair  [READY]";
        return $"{moduleName} — Stage {_currentStageIndex + 1}\nPress [E] to check materials";
    }

    public void OnPlayerProximityEnter()
    {
        if (moduleType == ModuleType.EngineCore && Stage1Complete && !_isFullyRepaired)
        {
            var tp = FindFirstObjectByType<ThrusterPack>();
            if (tp != null && tp.FuelNormalized < 1f)
                NotificationManager.Instance?.ShowInfo($"Engine Core: [E] refuel ({tp.FuelNormalized * 100f:F0}%) or repair.");
        }
    }

    public void RefuelThrusterFromEngineCore()
    {
        if (moduleType != ModuleType.EngineCore || !Stage1Complete) return;
        var tp = FindFirstObjectByType<ThrusterPack>(); if (tp == null) return;
        tp.Refuel(tp.maxFuelCharges);
        NotificationManager.Instance?.ShowInfo("Thruster fully refueled!");
    }

    private void CheckAndNotifyReady()
    {
        if (_isFullyRepaired || _readyNotificationShown) return;
        if (!CanRepairCurrentStage()) return;
        _readyNotificationShown = true;
        NotificationManager.Instance?.ShowInfo($"{moduleName} READY TO REPAIR!\nApproach and press [E].");
    }

    private string BuildMissingText()
    {
        if (_currentStageIndex >= (stages?.Count ?? 0)) return "None";
        var sb = new System.Text.StringBuilder();
        foreach (var req in stages[_currentStageIndex].requirements)
        {
            int have = Inventory.Instance?.GetCount(req.materialType) ?? 0;
            string n = System.Text.RegularExpressions.Regex.Replace(
                req.materialType.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
            sb.AppendLine($"  {n}: {(have >= req.required ? "[OK]" : $"{have}/{req.required}")}");
        }
        return sb.ToString();
    }

    public void LoadStageIndex(int idx)
    {
        _currentStageIndex = Mathf.Clamp(idx, 0, stages?.Count ?? 0);
        if (stages != null)
            for (int i = 0; i < _currentStageIndex && i < stages.Count; i++)
                stages[i].isComplete = true;
        _isFullyRepaired = stages != null && _currentStageIndex >= stages.Count;
        FireProgressAndRefreshUI();
    }
}
