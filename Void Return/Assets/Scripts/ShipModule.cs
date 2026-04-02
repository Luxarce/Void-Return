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
/// Manages the repair state of one spacecraft module.
///
/// FIXES / ADDITIONS:
///  — Repair now works reliably. AttemptRepair() is wired via ProximityInteraction
///    which calls it directly (no UnityEvent chain needed).
///  — Hull Plating and Engine Core grant special bonuses after STAGE 1 completion
///    (not after full repair). Bonus info is shown in the proximity hover prompt.
///  — GetProximityPrompt() now includes Stage 1 bonus descriptions so players
///    know what they unlock as they repair.
///  — Thruster refill from Engine Core is blocked until stage 1 is complete.
/// </summary>
public class ShipModule : MonoBehaviour
{
    [Header("Module Identity")]
    public string     moduleName = "Life Support";
    public ModuleType moduleType;

    [Header("Repair Stages")]
    public List<RepairStage> stages = new();

    [Header("VFX")]
    public ParticleSystem repairParticles;
    public ParticleSystem completionParticles;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   repairClip;
    public AudioClip   completionClip;

    [Header("Events")]
    public UnityEvent        onModuleRepaired;
    public UnityEvent<float> onProgressChanged;

    // ─────────────────────────────────────────────────────────────────────────
    private int  _currentStageIndex;
    private bool _isFullyRepaired;
    private bool _readyNotificationShown;
    private bool _stage1BonusApplied;

    // ─────────────────────────────────────────────────────────────────────────
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
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged += CheckAndNotifyReady;
    }

    private void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= CheckAndNotifyReady;
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

    // ─────────────────────────────────────────────────────────────────────────
    // MAIN REPAIR METHOD — called directly by ProximityInteraction.cs
    // ─────────────────────────────────────────────────────────────────────────

    public void AttemptRepair()
    {
        if (_isFullyRepaired)
        {
            string bonus = GetSpecialBonusText();
            NotificationManager.Instance?.ShowInfo(
                $"{moduleName} is fully repaired." + (bonus != "" ? $"\n{bonus}" : ""));
            return;
        }
        if (stages == null || stages.Count == 0)
        {
            Debug.LogError($"[ShipModule:{moduleName}] No stages in Inspector. " +
                           "Add repair stages in the ShipModule component.");
            NotificationManager.Instance?.ShowInfo(
                $"{moduleName}: No repair data configured.");
            return;
        }
        if (Inventory.Instance == null)
        {
            Debug.LogError("[ShipModule] Inventory.Instance is null.");
            return;
        }
        if (!CanRepairCurrentStage())
        {
            NotificationManager.Instance?.ShowInfo(
                $"{moduleName} — Stage {_currentStageIndex + 1}\n" +
                $"Need:\n{BuildMissingText()}");
            return;
        }

        // Consume materials
        var stage = stages[_currentStageIndex];
        foreach (var req in stage.requirements)
        {
            if (!Inventory.Instance.ConsumeMaterials(req.materialType, req.required))
            {
                Debug.LogError($"[ShipModule:{moduleName}] Consume failed mid-repair. " +
                               "This should not happen after CanRepair check.");
                return;
            }
        }

        stage.isComplete = true;
        _currentStageIndex++;
        _readyNotificationShown = false;

        // Apply stage 1 bonuses for Hull Plating and Engine Core
        if (_currentStageIndex == 1 && !_stage1BonusApplied)
            ApplyStage1Bonus();

        repairParticles?.Play();
        audioSource?.PlayOneShot(repairClip);
        onProgressChanged?.Invoke(Progress);

        Debug.Log($"[ShipModule:{moduleName}] Stage {_currentStageIndex} complete. " +
                  $"Progress: {Progress:P0}");

        if (_currentStageIndex >= stages.Count)
        {
            _isFullyRepaired = true;
            completionParticles?.Play();
            audioSource?.PlayOneShot(completionClip);
            NotificationManager.Instance?.ShowInfo($"{moduleName.ToUpper()} FULLY REPAIRED!");
            onModuleRepaired?.Invoke();
            ShipRepairManager.Instance?.OnModuleRepaired(moduleType);
        }
        else
        {
            NotificationManager.Instance?.ShowInfo(
                $"{moduleName} — Stage {_currentStageIndex} complete!\n" +
                $"Next: {stages[_currentStageIndex].stageName}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stage 1 Bonuses (Hull Plating and Engine Core)
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyStage1Bonus()
    {
        _stage1BonusApplied = true;

        switch (moduleType)
        {
            case ModuleType.HullPlating:
                // Hull Plating Stage 1: unlock grenade crafting
                var grenadeGun = FindFirstObjectByType<GravityGrenadeLauncher>();
                if (grenadeGun != null)
                {
                    grenadeGun.gameObject.SetActive(true);
                    GadgetHUDManager.Instance?.SetGadgetAvailable(2, true);
                }
                NotificationManager.Instance?.ShowInfo(
                    "Hull Plating Stage 1 complete!\nGravity Grenade now available.\n" +
                    "Press [C] while holding grenade to craft more.");
                break;

            case ModuleType.EngineCore:
                // Engine Core Stage 1: enable thruster refill at this module
                NotificationManager.Instance?.ShowInfo(
                    "Engine Core Stage 1 complete!\nThruster fuel can now be refilled " +
                    "here.\nApproach and press [E] to refuel.");
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Proximity Prompt — called by ProximityInteraction
    // ─────────────────────────────────────────────────────────────────────────

    public string GetProximityPrompt()
    {
        if (stages == null || stages.Count == 0)
            return $"{moduleName}\nNo repair data configured.";

        string bonus = GetSpecialBonusText();
        string bonusLine = bonus != "" ? $"\n{bonus}" : "";

        if (_isFullyRepaired)
            return $"{moduleName} — [DONE] Fully repaired{bonusLine}";

        if (CanRepairCurrentStage())
            return $"{moduleName} — Stage {_currentStageIndex + 1}\n" +
                   $"Press [E] to repair  [READY]{bonusLine}";

        return $"{moduleName} — Stage {_currentStageIndex + 1}\n" +
               $"Press [E] to check materials{bonusLine}";
    }

    private string GetSpecialBonusText()
    {
        switch (moduleType)
        {
            case ModuleType.HullPlating:
                if (!Stage1Complete)
                    return "[Stage 1 unlock] Enables Gravity Grenade crafting";
                if (!_isFullyRepaired)
                    return "[Full repair] Grenade capacity upgraded";
                return "Grenade crafting: press [C] with grenade selected";

            case ModuleType.EngineCore:
                if (!Stage1Complete)
                    return "[Stage 1 unlock] Enables Thruster fuel refill here";
                if (!_isFullyRepaired)
                    return "[Full repair] Thruster Pack unlocked";
                return "Press [E] here to refuel Thruster";

            default:
                return "";
        }
    }

    // Called by ProximityInteraction when the player is nearby (not yet pressing E)
    public void OnPlayerProximityEnter()
    {
        // Show thruster refill option for Engine Core if stage 1 is done
        if (moduleType == ModuleType.EngineCore && Stage1Complete && !_isFullyRepaired)
        {
            var thruster = FindFirstObjectByType<ThrusterPack>();
            if (thruster != null && thruster.FuelNormalized < 1f)
                NotificationManager.Instance?.ShowInfo(
                    $"{moduleName}: Press [E] to refuel Thruster " +
                    $"({thruster.FuelNormalized * 100f:F0}% fuel)\n" +
                    "OR press [E] to continue repairs.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Thruster Refill (Engine Core interaction when stage 1 done)
    // ─────────────────────────────────────────────────────────────────────────

    public void RefuelThrusterFromEngineCore()
    {
        if (moduleType != ModuleType.EngineCore) return;
        if (!Stage1Complete)
        {
            NotificationManager.Instance?.ShowInfo(
                "Engine Core Stage 1 must be repaired first to enable fuel refill.");
            return;
        }
        var thruster = FindFirstObjectByType<ThrusterPack>();
        if (thruster == null) return;
        thruster.Refuel(thruster.maxFuelCharges); // full refuel, no material cost
        NotificationManager.Instance?.ShowInfo("Thruster Pack fully refueled at Engine Core!");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void CheckAndNotifyReady()
    {
        if (_isFullyRepaired || _readyNotificationShown) return;
        if (!CanRepairCurrentStage()) return;
        _readyNotificationShown = true;
        NotificationManager.Instance?.ShowInfo(
            $"{moduleName} READY TO REPAIR!\nApproach and press [E].");
    }

    private string BuildMissingText()
    {
        if (_currentStageIndex >= (stages?.Count ?? 0)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var req in stages[_currentStageIndex].requirements)
        {
            int have    = Inventory.Instance?.GetCount(req.materialType) ?? 0;
            string name = System.Text.RegularExpressions.Regex.Replace(
                req.materialType.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
            string st   = have >= req.required ? "[OK]" : $"{have}/{req.required}";
            sb.AppendLine($"  {name}: {st}");
        }
        return sb.ToString();
    }

    public void LoadStageIndex(int idx)
    {
        _currentStageIndex = Mathf.Clamp(idx, 0, stages?.Count ?? 0);
        if (stages != null)
            for (int i = 0; i < _currentStageIndex && i < stages.Count; i++)
                stages[i].isComplete = true;
        if (_currentStageIndex > 0 && !_stage1BonusApplied) ApplyStage1Bonus();
        _isFullyRepaired = stages != null && _currentStageIndex >= stages.Count;
        onProgressChanged?.Invoke(Progress);
    }
}
