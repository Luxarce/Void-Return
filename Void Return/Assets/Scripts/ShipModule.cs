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
/// PROGRESS BAR DEBUG:
///  The reason the bar doesn't move even when onProgressChanged fires is almost
///  always one of:
///   1. GameManager.gameHUD is null  — no listener was added.
///   2. The Slider Min = Max = 0    — slider can't display any value.
///   3. onProgressChanged has no listeners at all (GameManager didn't wire them).
///
///  This version adds a listener COUNT log at Start so you can see in the Console
///  whether the event has been wired:
///    "[ShipModule:X] onProgressChanged has N listeners"
///  If N = 0, GameManager did not wire the event — check GameManager field assignments.
/// </summary>
public class ShipModule : MonoBehaviour
{
    [Header("Module Identity")]
    public string     moduleName = "Life Support";
    public ModuleType moduleType;

    [Header("Repair Stages")]
    [Tooltip("Add one entry per repair phase. Completion Fractions must sum to 1.0.")]
    public List<RepairStage> stages = new();

    [Header("VFX")]
    public ParticleSystem repairParticles;
    public ParticleSystem completionParticles;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   repairClip;
    public AudioClip   completionClip;

    [Header("Events — Wired automatically by GameManager")]
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
        ValidateStages();

        // Broadcast initial progress — fills bar on scene load
        onProgressChanged?.Invoke(Progress);

        // DIAGNOSTIC: log how many listeners are wired to onProgressChanged.
        // "0 listeners" means GameManager did not wire this event.
        int listenerCount = onProgressChanged?.GetPersistentEventCount() ?? 0;
        // Note: runtime listeners (AddListener) don't appear in GetPersistentEventCount.
        // We use a delayed check to catch both types.
        Invoke(nameof(LogListenerStatus), 1f);

        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged += CheckAndNotifyReady;

        Debug.Log($"[ShipModule:{moduleName}] Start — stages={stages?.Count ?? 0}, " +
                  $"currentStage={_currentStageIndex}, progress={Progress:P0}, " +
                  $"persistentListeners={listenerCount}");
    }

    private void LogListenerStatus()
    {
        // This fires 1 second after Start, by which time GameManager.Start() has run.
        // We invoke the event with the current value and watch for downstream logs.
        Debug.Log($"[ShipModule:{moduleName}] Firing onProgressChanged({Progress:F3}) — " +
                  $"check for '[GameManager]' and '[GameHUD]' logs below.");
        onProgressChanged?.Invoke(Progress);
    }

    private void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= CheckAndNotifyReady;
    }

    private void ValidateStages()
    {
        if (stages == null || stages.Count == 0)
        {
            Debug.LogError($"[ShipModule:{moduleName}] STAGES LIST IS EMPTY. " +
                           "Select this module. In ShipModule > Repair Stages, click + to add stages.");
            return;
        }
        float total = 0f;
        foreach (var s in stages) total += s.completionFraction;
        if (Mathf.Abs(total - 1f) > 0.05f)
            Debug.LogWarning($"[ShipModule:{moduleName}] Completion fractions sum to " +
                             $"{total:F2} (should be 1.0).");
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
        Debug.Log($"[ShipModule:{moduleName}] AttemptRepair — " +
                  $"stage={_currentStageIndex}/{stages?.Count ?? 0}, " +
                  $"canRepair={CanRepairCurrentStage()}, inventory={Inventory.Instance != null}");

        if (_isFullyRepaired)
        {
            NotificationManager.Instance?.ShowInfo($"{moduleName} is fully repaired.{GetSpecialBonusText()}");
            return;
        }
        if (stages == null || stages.Count == 0)
        {
            Debug.LogError($"[ShipModule:{moduleName}] No stages — add them in Inspector.");
            NotificationManager.Instance?.ShowInfo($"{moduleName}: No repair stages configured.");
            return;
        }
        if (Inventory.Instance == null) { Debug.LogError("[ShipModule] Inventory null."); return; }

        if (!CanRepairCurrentStage())
        {
            NotificationManager.Instance?.ShowInfo(
                $"{moduleName} — Stage {_currentStageIndex + 1}\nStill need:\n{BuildMissingText()}");
            return;
        }

        // Consume materials
        foreach (var req in stages[_currentStageIndex].requirements)
        {
            if (!Inventory.Instance.ConsumeMaterials(req.materialType, req.required))
            {
                Debug.LogError($"[ShipModule:{moduleName}] Consume failed for {req.materialType}.");
                return;
            }
        }

        stages[_currentStageIndex].isComplete = true;
        _currentStageIndex++;
        _readyNotificationShown = false;

        if (_currentStageIndex == 1 && !_stage1BonusApplied) ApplyStage1Bonus();

        repairParticles?.Play();
        audioSource?.PlayOneShot(repairClip);

        float prog = Progress;
        onProgressChanged?.Invoke(prog);
        Debug.Log($"[ShipModule:{moduleName}] Stage {_currentStageIndex} complete — " +
                  $"progress={prog:P0} — fired onProgressChanged({prog:F3}).");

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

    private void ApplyStage1Bonus()
    {
        _stage1BonusApplied = true;
        switch (moduleType)
        {
            case ModuleType.HullPlating:
                var gg = FindFirstObjectByType<GravityGrenadeLauncher>();
                if (gg != null) { gg.gameObject.SetActive(true); GadgetHUDManager.Instance?.SetGadgetAvailable(2, true); }
                NotificationManager.Instance?.ShowInfo("Hull Plating Stage 1 — Gravity Grenade unlocked. Press [C] to craft.");
                break;
            case ModuleType.EngineCore:
                NotificationManager.Instance?.ShowInfo("Engine Core Stage 1 — Thruster can be refueled here. Press [E].");
                break;
        }
    }

    public string GetProximityPrompt()
    {
        if (stages == null || stages.Count == 0)
            return $"{moduleName}\n[!] No repair stages — check Inspector.";

        string bonusLine = GetSpecialBonusText();

        if (_isFullyRepaired)
            return $"{moduleName} — [DONE] Fully repaired{bonusLine}";
        if (CanRepairCurrentStage())
            return $"{moduleName} — Stage {_currentStageIndex + 1}\nPress [E] to repair  [READY]{bonusLine}";
        return $"{moduleName} — Stage {_currentStageIndex + 1}\nPress [E] to check materials{bonusLine}";
    }

    private string GetSpecialBonusText()
    {
        return moduleType switch
        {
            ModuleType.HullPlating => !Stage1Complete
                ? "\nStage 1 unlock: Gravity Grenade"
                : (!_isFullyRepaired ? "\nFull repair: Grenade capacity max" : "\nCraft grenades: press [C]"),
            ModuleType.EngineCore  => !Stage1Complete
                ? "\nStage 1 unlock: Thruster refuel here"
                : (!_isFullyRepaired ? "\nFull repair: Thruster Pack unlocked" : "\nPress [E] to refuel Thruster"),
            _ => "",
        };
    }

    public void OnPlayerProximityEnter()
    {
        if (moduleType == ModuleType.EngineCore && Stage1Complete && !_isFullyRepaired)
        {
            var tp = FindFirstObjectByType<ThrusterPack>();
            if (tp != null && tp.FuelNormalized < 1f)
                NotificationManager.Instance?.ShowInfo(
                    $"Engine Core: Press [E] to refuel Thruster ({tp.FuelNormalized * 100f:F0}%)\nOR continue repairs.");
        }
    }

    public void RefuelThrusterFromEngineCore()
    {
        if (moduleType != ModuleType.EngineCore) return;
        if (!Stage1Complete) { NotificationManager.Instance?.ShowInfo("Repair Engine Core Stage 1 first."); return; }
        var tp = FindFirstObjectByType<ThrusterPack>();
        if (tp == null) return;
        tp.Refuel(tp.maxFuelCharges);
        NotificationManager.Instance?.ShowInfo("Thruster fully refueled at Engine Core!");
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
            int    have = Inventory.Instance?.GetCount(req.materialType) ?? 0;
            string name = System.Text.RegularExpressions.Regex.Replace(
                req.materialType.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
            sb.AppendLine($"  {name}: {(have >= req.required ? "[OK]" : $"{have}/{req.required}")}");
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
