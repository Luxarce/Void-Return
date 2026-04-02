using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central manager for all ship module repair state and gadget unlocks.
///
/// CHANGES:
///  — Engine Core no longer auto-refuels the thruster here.
///    Thruster refueling is handled by ShipModule.RefuelThrusterFromEngineCore()
///    when the player presses E at the Engine Core after Stage 1.
///  — Hull Plating grenade unlock now also done in ShipModule.ApplyStage1Bonus()
///    so it triggers at Stage 1 (not only on full repair).
///    OnModuleRepaired still handles full repair unlock for completion tracking.
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

    [Header("Life Support Bonuses")]
    public float lifeSupportOxygenBonus      = 90f;
    public float lifeSupportBootsDurationBonus = 20f;

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

        // Disable locked gadgets at game start (respect debug toggles)
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
    // Called by ShipModule.AttemptRepair() on full module completion
    // ─────────────────────────────────────────────────────────────────────────

    public void OnModuleRepaired(ModuleType type)
    {
        switch (type)
        {
            case ModuleType.LifeSupport:
                oxygenSystem?.ExtendMaxOxygen(lifeSupportOxygenBonus);
                gravityBoots?.ExtendMaxDuration(lifeSupportBootsDurationBonus);
                NotificationManager.Instance?.ShowInfo(
                    "LIFE SUPPORT FULLY REPAIRED!\nOxygen extended. Boots stamina upgraded.");
                break;

            case ModuleType.HullPlating:
                // Grenade already unlocked at Stage 1 by ShipModule.ApplyStage1Bonus().
                // Full repair just adds grenade capacity increase or other full bonus.
                if (grenadeGun != null)
                {
                    grenadeGun.gameObject.SetActive(true);
                    GadgetHUDManager.Instance?.SetGadgetAvailable(2, true);
                    grenadeGun.AddGrenades(grenadeGun.maxGrenades); // refill on full repair
                }
                NotificationManager.Instance?.ShowInfo(
                    "HULL PLATING FULLY REPAIRED!\nGrenade capacity at maximum.");
                break;

            case ModuleType.Navigation:
                if (tetherGun != null)
                {
                    tetherGun.gameObject.SetActive(true);
                    GadgetHUDManager.Instance?.SetGadgetAvailable(1, true);
                }
                minimap?.UnlockMaterialMarkers();
                NotificationManager.Instance?.ShowInfo(
                    "NAVIGATION FULLY REPAIRED!\nTether Gun and Material Map unlocked.");
                break;

            case ModuleType.EngineCore:
                if (thrusterPack != null)
                {
                    thrusterPack.gameObject.SetActive(true);
                    GadgetHUDManager.Instance?.SetGadgetAvailable(3, true);
                }
                NotificationManager.Instance?.ShowInfo(
                    "ENGINE CORE FULLY REPAIRED!\nThruster Pack unlocked. " +
                    "Escape sequence ready!");
                break;
        }

        CheckAllRepaired();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void CheckAllRepaired()
    {
        if (lifeSupport != null && !lifeSupport.IsFullyRepaired) return;
        if (hullPlating != null && !hullPlating.IsFullyRepaired) return;
        if (navigation  != null && !navigation.IsFullyRepaired)  return;
        if (engineCore  != null && !engineCore.IsFullyRepaired)  return;
        Debug.Log("[ShipRepair] ALL MODULES REPAIRED.");
        onAllModulesRepaired?.Invoke();
    }
}
