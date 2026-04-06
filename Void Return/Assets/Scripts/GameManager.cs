using UnityEngine;

/// <summary>
/// Wires cross-system events at startup.
///
/// VICTORY: ShipRepairManager.TriggerVictory() handles victory directly.
/// GameManager no longer needs to wire onAllModulesRepaired — ShipRepairManager
/// calls EndingManager/GameHUD directly. GameManager still wires
/// onModuleRepaired → ShipRepairManager.OnModuleRepaired so the victory
/// check runs when each module finishes.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Player Systems")]
    public OxygenSystem oxygenSystem;

    [Header("HUD")]
    public GameHUD gameHUD;

    [Header("Ship Modules")]
    public ShipModule lifeSupport;
    public ShipModule hullPlating;
    public ShipModule navigation;
    public ShipModule engineCore;

    [Header("Managers")]
    public ShipRepairManager    shipRepairManager;
    public MeteoriteManager     meteoriteManager;

    [Header("UI Systems")]
    public NotificationManager    notificationManager;
    public MaterialRequirementsUI requirementsUI;

    [Header("Audio")]
    public AudioManager audioManager;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        WireOxygenEvents();
        WireModuleEvents();
        WireMeteoriteEvents();

        requirementsUI?.RefreshAll();
        gameHUD?.RefreshUpgradeList();

        Debug.Log("[GameManager] All events wired.");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void WireOxygenEvents()
    {
        if (oxygenSystem == null) { Warn("oxygenSystem"); return; }
        if (gameHUD != null) oxygenSystem.onOxygenChanged.AddListener(gameHUD.UpdateOxygen);
        oxygenSystem.onLowOxygen.AddListener(() => { notificationManager?.ShowWarning("Oxygen running low!"); audioManager?.PlaySFX("oxygen_warning"); });
        oxygenSystem.onCriticalOxygen.AddListener(() => { notificationManager?.ShowWarning("CRITICAL OXYGEN!"); audioManager?.PlaySFX("oxygen_critical"); });
        if (gameHUD != null) oxygenSystem.onOxygenDepleted.AddListener(gameHUD.ShowGameOver);
    }

    private void WireModuleEvents()
    {
        WireSingleModule(lifeSupport, "Life Support",  ModuleType.LifeSupport);
        WireSingleModule(hullPlating, "Hull Plating",  ModuleType.HullPlating);
        WireSingleModule(navigation,  "Navigation",    ModuleType.Navigation);
        WireSingleModule(engineCore,  "Engine Core",   ModuleType.EngineCore);
    }

    private void WireSingleModule(ShipModule module, string displayName, ModuleType type)
    {
        if (module == null) { Warn($"ShipModule [{displayName}]"); return; }

        // HUD upgrade list
        if (gameHUD != null)
            module.onProgressChanged.AddListener(_ => gameHUD.RefreshUpgradeList());

        // Requirements panel
        if (requirementsUI != null)
            module.onProgressChanged.AddListener(_ => requirementsUI.RefreshAll());

        // Victory check — wire onModuleRepaired to ShipRepairManager
        if (shipRepairManager != null)
        {
            ModuleType captured = type;
            module.onModuleRepaired.AddListener(() =>
            {
                Debug.Log($"[GameManager] {displayName} onModuleRepaired → ShipRepairManager.OnModuleRepaired({captured})");
                shipRepairManager.OnModuleRepaired(captured);
            });
        }
        else
        {
            Debug.LogError("[GameManager] shipRepairManager is null — assign in Inspector. Victory will not fire via this path.");
        }

        module.onProgressChanged.AddListener(_ => audioManager?.PlaySFX("repair_stage"));
    }

    private void WireMeteoriteEvents()
    {
        if (meteoriteManager == null) return;
        meteoriteManager.onShowerStart.AddListener(() => audioManager?.PlayMusic("tension_zone2"));
        meteoriteManager.onRiftStart.AddListener(()   => audioManager?.PlayMusic("danger_zone3"));
    }

    private void Warn(string f) => Debug.LogWarning($"[GameManager] '{f}' not assigned in Inspector.");
}
