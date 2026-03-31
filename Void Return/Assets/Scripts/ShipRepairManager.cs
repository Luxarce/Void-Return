using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central manager for all ship module repair state.
/// When a module is fully repaired, this script unlocks the corresponding gadget
/// or system improvement and fires any connected Unity Events.
///
/// Create one instance in the scene on an empty GameObject.
/// Assign all module and system references in the Inspector.
/// </summary>
public class ShipRepairManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static ShipRepairManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Module References — Drag the ShipModule components here")]
    [Tooltip("Life Support module. Must be repaired first.")]
    public ShipModule lifeSupport;

    [Tooltip("Hull Plating module. Unlocks Gravity Grenade.")]
    public ShipModule hullPlating;

    [Tooltip("Navigation module. Unlocks Tether Gun and Material Map.")]
    public ShipModule navigation;

    [Tooltip("Engine Core module. Unlocks Thruster Pack and escape.")]
    public ShipModule engineCore;

    [Header("System References — Drag components from the Player here")]
    [Tooltip("OxygenSystem on the Player. Extended when Life Support is repaired.")]
    public OxygenSystem oxygenSystem;

    [Tooltip("GravityBoots on the Player (child). Duration extended by Life Support.")]
    public GravityBoots gravityBoots;

    [Tooltip("TetherGun on the Player (child). Enabled by Navigation repair.")]
    public TetherGun tetherGun;

    [Tooltip("GravityGrenadeLauncher on the Player (child). Enabled by Hull Plating repair.")]
    public GravityGrenadeLauncher grenadeGun;

    [Tooltip("ThrusterPack on the Player (child). Enabled by Engine Core repair.")]
    public ThrusterPack thrusterPack;

    [Tooltip("MinimapController in the scene. Material markers unlocked by Navigation repair.")]
    public MinimapController minimap;

    [Header("Life Support Unlock Values")]
    [Tooltip("Extra oxygen seconds added to OxygenSystem when Life Support is repaired.")]
    public float lifeSupportOxygenBonus = 90f;

    [Tooltip("Extra seconds added to GravityBoots max duration when Life Support is repaired.")]
    public float lifeSupportBootsBonus = 20f;

    [Header("Events")]
    [Tooltip("Fired when all four modules are fully repaired. Use to trigger the ending sequence.")]
    public UnityEvent onAllModulesRepaired;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // All locked gadgets start disabled — unlocked progressively
        if (tetherGun    != null) tetherGun.gameObject.SetActive(false);
        if (grenadeGun   != null) grenadeGun.gameObject.SetActive(false);
        if (thrusterPack != null) thrusterPack.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Called by ShipModule.AttemptRepair
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called automatically by a ShipModule when it is fully repaired.
    /// Handles the unlock logic for each module type.
    /// </summary>
    public void OnModuleRepaired(ModuleType type)
    {
        switch (type)
        {
            case ModuleType.LifeSupport:
                oxygenSystem?.ExtendMaxOxygen(lifeSupportOxygenBonus);
                gravityBoots?.ExtendMaxDuration(lifeSupportBootsBonus);
                NotificationManager.Instance?.Show(
                    "LIFE SUPPORT ONLINE\nOxygen extended. Gravity Boots upgraded.", urgent: false);
                break;

            case ModuleType.HullPlating:
                if (grenadeGun != null)
                {
                    grenadeGun.gameObject.SetActive(true);
                    GadgetHUDManager.Instance?.UnlockGadgetSlot(2);
                }
                NotificationManager.Instance?.Show(
                    "HULL PLATING REPAIRED\nGravity Grenade unlocked.", urgent: false);
                break;

            case ModuleType.Navigation:
                if (tetherGun != null)
                {
                    tetherGun.gameObject.SetActive(true);
                    GadgetHUDManager.Instance?.UnlockGadgetSlot(1);
                }
                minimap?.UnlockMaterialMarkers();
                NotificationManager.Instance?.Show(
                    "NAVIGATION ONLINE\nTether Gun and Material Map unlocked.", urgent: false);
                break;

            case ModuleType.EngineCore:
                if (thrusterPack != null)
                {
                    thrusterPack.gameObject.SetActive(true);
                    GadgetHUDManager.Instance?.UnlockGadgetSlot(3);
                }
                NotificationManager.Instance?.Show(
                    "ENGINE CORE ONLINE\nThruster Pack unlocked. Escape sequence available.", urgent: false);
                break;
        }

        CheckAllRepaired();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void CheckAllRepaired()
    {
        if (lifeSupport  != null && !lifeSupport.IsFullyRepaired)  return;
        if (hullPlating  != null && !hullPlating.IsFullyRepaired)  return;
        if (navigation   != null && !navigation.IsFullyRepaired)   return;
        if (engineCore   != null && !engineCore.IsFullyRepaired)   return;

        onAllModulesRepaired?.Invoke();
    }
}
