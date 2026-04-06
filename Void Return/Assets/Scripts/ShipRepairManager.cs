using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages all per-stage upgrades and triggers victory when all modules are repaired.
///
/// VICTORY FIX:
///  Previous CheckAllRepaired() skipped null modules, meaning if any module was
///  unassigned, victory would fire too early. Now it counts how many non-null
///  modules are assigned and how many are fully repaired. Victory only fires when
///  repaired == total and total > 0.
///
/// GADGET FIX:
///  ShipRepairManager has direct Inspector references to gadgets.
///  SaveManager.Load() was re-disabling gadgets by loading saved booleans that
///  were false at save time. That is fixed in SaveManager — it now re-derives
///  gadget state from module stage completion rather than saved booleans.
/// </summary>
public class ShipRepairManager : MonoBehaviour
{
    public static ShipRepairManager Instance { get; private set; }

    [Header("Module References — assign all four")]
    public ShipModule lifeSupport;
    public ShipModule hullPlating;
    public ShipModule navigation;
    public ShipModule engineCore;

    [Header("Player Systems")]
    public OxygenSystem      oxygenSystem;
    public GravityBoots      gravityBoots;
    public MinimapController minimap;

    [Header("Gadgets — drag the same child objects that are on PlayerController")]
    [Tooltip("Player > TetherGun child GameObject")]
    public TetherGun              tetherGun;
    [Tooltip("Player > GravityGrenadeLauncher child GameObject")]
    public GravityGrenadeLauncher grenadeGun;
    [Tooltip("Player > ThrusterPack child GameObject")]
    public ThrusterPack           thrusterPack;

    [Header("Victory — assign EndingManager OR GameHUD (or both)")]
    public EndingManager endingManager;
    public GameHUD       gameHUD;

    [Header("Events")]
    public UnityEvent onAllModulesRepaired;

    // ─────────────────────────────────────────────────────────────────────────
    private bool _victoryFired;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Auto-find victory UI if not assigned
        if (endingManager == null) endingManager = FindFirstObjectByType<EndingManager>();
        if (gameHUD       == null) gameHUD       = FindFirstObjectByType<GameHUD>();

        // Validate
        if (tetherGun    == null) Debug.LogWarning("[ShipRepairManager] tetherGun not assigned in Inspector.");
        if (grenadeGun   == null) Debug.LogWarning("[ShipRepairManager] grenadeGun not assigned in Inspector.");
        if (thrusterPack == null) Debug.LogWarning("[ShipRepairManager] thrusterPack not assigned in Inspector.");

        int moduleCount = 0;
        if (lifeSupport != null) moduleCount++;
        if (hullPlating != null) moduleCount++;
        if (navigation  != null) moduleCount++;
        if (engineCore  != null) moduleCount++;
        if (moduleCount == 0)
            Debug.LogError("[ShipRepairManager] No modules assigned! Assign all four ShipModule references.");
        else if (moduleCount < 4)
            Debug.LogWarning($"[ShipRepairManager] Only {moduleCount}/4 modules assigned. Victory requires ALL assigned modules to be repaired.");
        else
            Debug.Log("[ShipRepairManager] Ready — all 4 modules assigned.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Per-stage upgrades
    // ─────────────────────────────────────────────────────────────────────────

    public void OnStageCompleted(ModuleType type, int stageIndex)
    {
        Debug.Log($"[ShipRepairManager] OnStageCompleted({type}, stage={stageIndex})");
        switch (type)
        {
            case ModuleType.LifeSupport:  ApplyLifeSupportStage(stageIndex);  break;
            case ModuleType.Navigation:   ApplyNavigationStage(stageIndex);   break;
            case ModuleType.HullPlating:  ApplyHullPlatingStage(stageIndex);  break;
            case ModuleType.EngineCore:   ApplyEngineCoreStage(stageIndex);   break;
        }
    }

    private void ApplyLifeSupportStage(int stage)
    {
        if (stage == 1)
        {
            float b = (oxygenSystem?.maxOxygen ?? 180f) * 0.5f;
            oxygenSystem?.ExtendMaxOxygen(b);
            NotificationManager.Instance?.ShowInfo($"Life Support Stage 1!\nOxygen +50% (+{b:F0}s)  |  Zone + Shield active");
        }
        else if (stage == 2)
        {
            float ob = (oxygenSystem?.maxOxygen ?? 270f) * 0.5f;
            oxygenSystem?.ExtendMaxOxygen(ob);
            if (oxygenSystem != null) oxygenSystem.lifeSupportRefillRate *= 2f;
            if (gravityBoots != null) gravityBoots.ExtendMaxDuration(gravityBoots.maxDuration * 0.5f);
            NotificationManager.Instance?.ShowInfo("Life Support FULLY REPAIRED!\nOxygen +50%  |  Refill x2  |  Boots +50%");
        }
    }

    private void ApplyNavigationStage(int stage)
    {
        if (stage == 1)
        {
            Unlock(tetherGun?.gameObject, 1, "Tether Gun [F]");
            minimap?.UnlockPartialMinimap();
            NotificationManager.Instance?.ShowInfo("Navigation Stage 1!\nTether Gun unlocked  |  Minimap partial");
        }
        else if (stage == 2)
        {
            minimap?.UnlockMaterialMarkers();
            minimap?.UnlockFullMinimap();
            if (tetherGun != null) tetherGun.tetherRange *= 2f;
            NotificationManager.Instance?.ShowInfo("Navigation FULLY REPAIRED!\nMinimap fully unlocked  |  Tether range x2");
        }
    }

    private void ApplyHullPlatingStage(int stage)
    {
        if (stage == 1)
        {
            Unlock(grenadeGun?.gameObject, 2, "Gravity Grenade [G]");
            NotificationManager.Instance?.ShowInfo("Hull Plating Stage 1!\nGravity Grenade unlocked  |  Crafting open");
        }
        else if (stage == 2)
        {
            if (grenadeGun != null)
            {
                grenadeGun.pullRadius        *= 2f;
                grenadeGun.maxGrenades        += 2;
                grenadeGun.craftMaterialCount  = Mathf.Max(1, grenadeGun.craftMaterialCount - 1);
            }
            NotificationManager.Instance?.ShowInfo("Hull Plating FULLY REPAIRED!\nPull x2  |  +2 slots  |  Craft cost -1");
        }
    }

    private void ApplyEngineCoreStage(int stage)
    {
        if (stage == 1)
        {
            Unlock(thrusterPack?.gameObject, 3, "Thruster Pack [Space]");
            NotificationManager.Instance?.ShowInfo("Engine Core Stage 1!\nThruster Pack unlocked");
        }
        else if (stage == 2)
        {
            if (thrusterPack != null)
                thrusterPack.maxFuelCharges = (int)(thrusterPack.maxFuelCharges * 2f);
            NotificationManager.Instance?.ShowInfo("Engine Core FULLY REPAIRED!\nFuel capacity x2");
        }
    }

    private void Unlock(GameObject go, int slot, string name)
    {
        if (go == null)
        {
            Debug.LogError($"[ShipRepairManager] Cannot unlock '{name}' — reference is null. " +
                           "Drag the gadget child GameObject into ShipRepairManager in the Inspector.");
            return;
        }
        go.SetActive(true);
        GadgetHUDManager.Instance?.SetGadgetAvailable(slot, true);
        Debug.Log($"[ShipRepairManager] UNLOCKED: {name} (slot {slot}) — SetActive(true) called.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Called by both GameManager listener AND ShipModule direct call
    // ─────────────────────────────────────────────────────────────────────────

    public void OnModuleRepaired(ModuleType type)
    {
        Debug.Log($"[ShipRepairManager] OnModuleRepaired({type})");
        CheckAllRepaired();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FIXED: counts assigned+repaired vs assigned total
    // ─────────────────────────────────────────────────────────────────────────

    private void CheckAllRepaired()
    {
        int total    = 0;
        int repaired = 0;

        void Check(ShipModule m, string name)
        {
            if (m == null)
            {
                Debug.LogWarning($"[ShipRepairManager] CheckAllRepaired: {name} is null — not counted.");
                return;
            }
            total++;
            if (m.IsFullyRepaired) repaired++;
            Debug.Log($"[ShipRepairManager] {name}: IsFullyRepaired={m.IsFullyRepaired} ({repaired}/{total})");
        }

        Check(lifeSupport, "Life Support");
        Check(hullPlating, "Hull Plating");
        Check(navigation,  "Navigation");
        Check(engineCore,  "Engine Core");

        Debug.Log($"[ShipRepairManager] CheckAllRepaired: {repaired}/{total} modules repaired.");

        if (total == 0)
        {
            Debug.LogError("[ShipRepairManager] No modules assigned — victory cannot fire. " +
                           "Assign all four ShipModule fields in the Inspector.");
            return;
        }

        if (repaired < total)
        {
            Debug.Log($"[ShipRepairManager] Not all repaired yet ({repaired}/{total}). Waiting.");
            return;
        }

        if (_victoryFired)
        {
            Debug.Log("[ShipRepairManager] Victory already fired — ignoring duplicate call.");
            return;
        }
        _victoryFired = true;

        Debug.Log("[ShipRepairManager] ALL MODULES REPAIRED — triggering victory!");
        onAllModulesRepaired?.Invoke();
        TriggerVictory();
    }

    private void TriggerVictory()
    {
        if (endingManager != null)
        {
            Debug.Log("[ShipRepairManager] Calling endingManager.TriggerEscapeEnding()");
            endingManager.TriggerEscapeEnding();
            return;
        }
        if (gameHUD != null)
        {
            Debug.Log("[ShipRepairManager] Calling gameHUD.ShowVictory()");
            gameHUD.ShowVictory();
            return;
        }
        // Final fallbacks
        var em = FindFirstObjectByType<EndingManager>();
        if (em != null) { em.TriggerEscapeEnding(); return; }
        var hud = FindFirstObjectByType<GameHUD>();
        if (hud != null) { hud.ShowVictory(); return; }
        Debug.LogError("[ShipRepairManager] VICTORY CANNOT FIRE — no EndingManager or GameHUD in scene.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Called by SaveManager after loading, to re-apply gadget state from module progress
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Re-applies all gadget unlocks based on current module stage completion.
    /// Called after Load() so gadgets reflect the loaded save state correctly.
    /// </summary>
    public void ReapplyUnlocksFromModuleState()
    {
        Debug.Log("[ShipRepairManager] Re-applying gadget unlocks from module state.");

        // Navigation Stage 1+ → Tether
        if (navigation != null && navigation.Stage1Complete)
            Unlock(tetherGun?.gameObject, 1, "Tether Gun (save restore)");

        // Hull Plating Stage 1+ → Grenade
        if (hullPlating != null && hullPlating.Stage1Complete)
            Unlock(grenadeGun?.gameObject, 2, "Gravity Grenade (save restore)");

        // Engine Core Stage 1+ → Thruster
        if (engineCore != null && engineCore.Stage1Complete)
            Unlock(thrusterPack?.gameObject, 3, "Thruster Pack (save restore)");

        // Navigation Stage 2 → full minimap
        if (navigation != null && navigation.IsFullyRepaired)
        {
            minimap?.UnlockMaterialMarkers();
            minimap?.UnlockFullMinimap();
        }
        else if (navigation != null && navigation.Stage1Complete)
        {
            minimap?.UnlockPartialMinimap();
        }
    }
}
