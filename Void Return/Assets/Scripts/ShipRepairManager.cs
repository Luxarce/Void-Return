using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages all stage-by-stage upgrades for ship modules.
///
/// Every stage of every module now grants specific bonuses as specified:
///
/// LIFE SUPPORT
///  Stage 1: Zone active, Shield active, Oxygen capacity +50%
///  Stage 2: Oxygen capacity +50%, Refill rate +100%, Boots duration +50%
///
/// NAVIGATION
///  Stage 1: Tether Gun activated, Minimap partial (player + shipwreck only)
///  Stage 2: Minimap full (rifts, materials, meteors), Tether range +100%
///
/// HULL PLATING
///  Stage 1: Grenade Launcher activated, Crafting open
///  Stage 2: Grenade pull radius +100%, Capacity +2 slots, Craft cost -1 material
///
/// ENGINE CORE
///  Stage 1: Thruster activated
///  Stage 2: Thruster fuel capacity +100%
/// </summary>
public class ShipRepairManager : MonoBehaviour
{
    public static ShipRepairManager Instance { get; private set; }

    [Header("Module References")]
    public ShipModule lifeSupport;
    public ShipModule hullPlating;
    public ShipModule navigation;
    public ShipModule engineCore;

    [Header("Player Systems")]
    public OxygenSystem           oxygenSystem;
    public GravityBoots           gravityBoots;
    public TetherGun              tetherGun;
    public GravityGrenadeLauncher grenadeGun;
    public ThrusterPack           thrusterPack;
    public MinimapController      minimap;

    [Header("Events")]
    public UnityEvent onAllModulesRepaired;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null)
        {
            if (tetherGun    != null && !pc.enableTetherAtStart)
                tetherGun.gameObject.SetActive(false);
            if (grenadeGun   != null && !pc.enableGrenadeAtStart)
                grenadeGun.gameObject.SetActive(false);
            if (thrusterPack != null && !pc.enableThrusterAtStart)
                thrusterPack.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Called by ShipModule when a stage completes
    // ─────────────────────────────────────────────────────────────────────────

    public void OnStageCompleted(ModuleType type, int stageIndex)
    {
        switch (type)
        {
            case ModuleType.LifeSupport:  ApplyLifeSupportStage(stageIndex);  break;
            case ModuleType.Navigation:   ApplyNavigationStage(stageIndex);   break;
            case ModuleType.HullPlating:  ApplyHullPlatingStage(stageIndex);  break;
            case ModuleType.EngineCore:   ApplyEngineCoreStage(stageIndex);   break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Life Support
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyLifeSupportStage(int stage)
    {
        if (stage == 1) // Stage 1 complete
        {
            // Oxygen +50% (of base 180 = +90s)
            float bonus = (oxygenSystem?.maxOxygen ?? 180f) * 0.5f;
            oxygenSystem?.ExtendMaxOxygen(bonus);
            // LifeSupportZone and Shield are activated by LifeSupportZone/Shield scripts
            NotificationManager.Instance?.ShowInfo(
                "Life Support Stage 1 complete!\n" +
                $"Oxygen capacity +50% (+{bonus:F0}s)\n" +
                "Life Support Zone activated\n" +
                "Life Support Shield activated");
        }
        else if (stage == 2) // Stage 2 / Full repair
        {
            // Oxygen +50% again
            float oxyBonus = (oxygenSystem?.maxOxygen ?? 270f) * 0.5f;
            oxygenSystem?.ExtendMaxOxygen(oxyBonus);
            // Refill rate +100%
            if (oxygenSystem != null)
                oxygenSystem.lifeSupportRefillRate *= 2f;
            // Boots duration +50%
            float bootBonus = (gravityBoots?.maxDuration ?? 30f) * 0.5f;
            gravityBoots?.ExtendMaxDuration(bootBonus);
            NotificationManager.Instance?.ShowInfo(
                "Life Support FULLY REPAIRED!\n" +
                $"Oxygen capacity +50% (+{oxyBonus:F0}s)\n" +
                "Refill rate doubled\n" +
                $"Gravity Boots capacity +50% (+{bootBonus:F0}s)");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Navigation
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyNavigationStage(int stage)
    {
        if (stage == 1)
        {
            // Tether Gun activated
            if (tetherGun != null)
            {
                tetherGun.gameObject.SetActive(true);
                GadgetHUDManager.Instance?.SetGadgetAvailable(1, true);
            }
            // Minimap partial — player + shipwreck only (materials/rifts/meteors still locked)
            minimap?.UnlockPartialMinimap();
            NotificationManager.Instance?.ShowInfo(
                "Navigation Stage 1 complete!\n" +
                "Tether Gun activated [F]\n" +
                "Minimap activated — player and ship markers visible");
        }
        else if (stage == 2)
        {
            // Minimap full
            minimap?.UnlockMaterialMarkers();
            minimap?.UnlockFullMinimap();
            // Tether range +100%
            if (tetherGun != null)
                tetherGun.tetherRange *= 2f;
            NotificationManager.Instance?.ShowInfo(
                "Navigation FULLY REPAIRED!\n" +
                "Minimap fully activated — rifts, materials, meteorites visible\n" +
                "Tether Gun range doubled");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Hull Plating
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyHullPlatingStage(int stage)
    {
        if (stage == 1)
        {
            // Grenade Launcher activated, crafting open
            if (grenadeGun != null)
            {
                grenadeGun.gameObject.SetActive(true);
                GadgetHUDManager.Instance?.SetGadgetAvailable(2, true);
            }
            NotificationManager.Instance?.ShowInfo(
                "Hull Plating Stage 1 complete!\n" +
                "Gravity Grenade Launcher activated [G]\n" +
                "Grenade crafting unlocked — Press [C] while grenade selected");
        }
        else if (stage == 2)
        {
            // Pull radius +100%
            if (grenadeGun != null)
            {
                grenadeGun.pullRadius *= 2f;
                grenadeGun.maxGrenades += 2;
                grenadeGun.craftMaterialCount = Mathf.Max(1, grenadeGun.craftMaterialCount - 1);
            }
            NotificationManager.Instance?.ShowInfo(
                "Hull Plating FULLY REPAIRED!\n" +
                "Grenade pull radius doubled\n" +
                "Grenade capacity +2 slots\n" +
                "Craft cost reduced by 1 material");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Engine Core
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyEngineCoreStage(int stage)
    {
        if (stage == 1)
        {
            // Thruster activated
            if (thrusterPack != null)
            {
                thrusterPack.gameObject.SetActive(true);
                GadgetHUDManager.Instance?.SetGadgetAvailable(3, true);
            }
            NotificationManager.Instance?.ShowInfo(
                "Engine Core Stage 1 complete!\n" +
                "Thruster Pack activated [Space / LMB]\n" +
                "Refuel at Engine Core by pressing [E]");
        }
        else if (stage == 2)
        {
            // Fuel capacity +100%
            if (thrusterPack != null)
                thrusterPack.maxFuelCharges = (int)(thrusterPack.maxFuelCharges * 2f);
            NotificationManager.Instance?.ShowInfo(
                "Engine Core FULLY REPAIRED!\n" +
                "Thruster fuel capacity doubled\n" +
                "Escape sequence ready!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Called by ShipModule on full module repair (backward compat)
    // ─────────────────────────────────────────────────────────────────────────

    public void OnModuleRepaired(ModuleType type)
    {
        CheckAllRepaired();
    }

    private void CheckAllRepaired()
    {
        if (lifeSupport != null && !lifeSupport.IsFullyRepaired)  return;
        if (hullPlating != null && !hullPlating.IsFullyRepaired)  return;
        if (navigation  != null && !navigation.IsFullyRepaired)   return;
        if (engineCore  != null && !engineCore.IsFullyRepaired)   return;
        Debug.Log("[ShipRepairManager] ALL MODULES REPAIRED.");
        onAllModulesRepaired?.Invoke();
    }
}
