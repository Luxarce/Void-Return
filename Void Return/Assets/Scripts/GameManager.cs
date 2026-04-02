using UnityEngine;

/// <summary>
/// Wires all cross-system events in code at runtime.
///
/// FIX — PROGRESS BARS NOT UPDATING:
///  Added explicit validation that logs which references are null.
///  The most common cause of bars not updating is gameHUD being null or
///  moduleProgressSliders not being assigned in the GameHUD Inspector.
///  Both are now logged as errors with clear messages.
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
    public ShipRepairManager   shipRepairManager;
    public EndingManager       endingManager;
    public MeteoriteManager    meteoriteManager;

    [Header("UI Systems")]
    public NotificationManager   notificationManager;
    public InventoryUI           inventoryUI;
    public MaterialRequirementsUI requirementsUI;

    [Header("Audio")]
    public AudioManager audioManager;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        WireOxygenEvents();
        WireModuleEvents();
        WireRepairManagerEvents();
        WireMeteoriteEvents();

        requirementsUI?.RefreshAll();

        Debug.Log("[GameManager] All events wired.");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void WireOxygenEvents()
    {
        if (oxygenSystem == null) { Warn("oxygenSystem"); return; }

        if (gameHUD != null)
            oxygenSystem.onOxygenChanged.AddListener(gameHUD.UpdateOxygen);

        oxygenSystem.onLowOxygen.AddListener(() =>
        {
            notificationManager?.ShowWarning("Oxygen running low — find a canister!");
            audioManager?.PlaySFX("oxygen_warning");
        });

        oxygenSystem.onCriticalOxygen.AddListener(() =>
        {
            notificationManager?.ShowWarning("CRITICAL OXYGEN!");
            audioManager?.PlaySFX("oxygen_critical");
        });

        if (gameHUD != null)
            oxygenSystem.onOxygenDepleted.AddListener(gameHUD.ShowGameOver);
    }

    private void WireModuleEvents()
    {
        WireSingleModule(lifeSupport, 0, "Life Support",  ModuleType.LifeSupport);
        WireSingleModule(hullPlating, 1, "Hull Plating",  ModuleType.HullPlating);
        WireSingleModule(navigation,  2, "Navigation",    ModuleType.Navigation);
        WireSingleModule(engineCore,  3, "Engine Core",   ModuleType.EngineCore);
    }

    private void WireSingleModule(ShipModule module, int index,
                                   string displayName, ModuleType type)
    {
        if (module == null) { Warn($"ShipModule [{displayName}]"); return; }

        // Progress bar update
        if (gameHUD != null)
        {
            int captured = index;
            module.onProgressChanged.AddListener(p =>
            {
                Debug.Log($"[GameManager] {displayName} progress -> {p:P0} -> sending to HUD[{captured}]");
                gameHUD.UpdateModuleProgress(captured, p);
            });
        }
        else
        {
            Debug.LogError("[GameManager] gameHUD is not assigned — module progress bars will NOT update. " +
                           "Drag the GameHUD component into the GameManager Inspector.");
        }

        // Requirements panel refresh
        if (requirementsUI != null)
            module.onProgressChanged.AddListener(_ => requirementsUI.RefreshAll());

        // Repair manager notification
        if (shipRepairManager != null)
        {
            ModuleType capturedType = type;
            module.onModuleRepaired.AddListener(() =>
                shipRepairManager.OnModuleRepaired(capturedType));
        }

        // Repair SFX
        module.onProgressChanged.AddListener(_ =>
            audioManager?.PlaySFX("repair_stage"));
    }

    private void WireRepairManagerEvents()
    {
        if (shipRepairManager == null) { Warn("shipRepairManager"); return; }

        if (endingManager != null)
            shipRepairManager.onAllModulesRepaired.AddListener(
                endingManager.TriggerEscapeEnding);
    }

    private void WireMeteoriteEvents()
    {
        if (meteoriteManager == null) return;

        meteoriteManager.onShowerStart.AddListener(() =>
            audioManager?.PlayMusic("tension_zone2"));

        meteoriteManager.onRiftStart.AddListener(() =>
            audioManager?.PlayMusic("danger_zone3"));
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void Warn(string field) =>
        Debug.LogWarning($"[GameManager] '{field}' is not assigned. " +
                         "Drag it into the GameManager Inspector.");
}
