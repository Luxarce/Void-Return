using UnityEngine;

/// <summary>
/// Central event-wiring hub. All cross-system connections are made here in code
/// at runtime so no manual Inspector event wiring is needed.
///
/// WHY onModuleRepaired DOESN'T APPEAR IN THE INSPECTOR EVENTS LIST:
///  ShipModule.onModuleRepaired is a UnityEvent declared in the script.
///  Unity's Inspector only shows events if the script is compiled without
///  errors AND the target method signature matches exactly.
///  ShipRepairManager.OnModuleRepaired(ModuleType) takes a ModuleType argument,
///  which the Inspector's drag-drop UI cannot supply at design time.
///
///  SOLUTION: This GameManager wires it in code using AddListener().
///  You do NOT need to set anything in the ShipModule event Inspector fields.
///  GameManager.Start() connects everything automatically.
///  Do NOT also add a manual Inspector connection — that would double-fire.
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
    public EndingManager        endingManager;
    public MeteoriteManager     meteoriteManager;

    [Header("UI Systems")]
    public NotificationManager    notificationManager;
    public InventoryUI            inventoryUI;
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

        // Immediately refresh HUD upgrade list after all modules are wired
        gameHUD?.RefreshUpgradeList();

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

        // Wire onProgressChanged → HUD upgrade list refresh
        // This fires immediately whenever a stage completes (from AttemptRepair)
        if (gameHUD != null)
        {
            module.onProgressChanged.AddListener(p =>
            {
                Debug.Log($"[GameManager] {displayName} progress={p:F3} — refreshing HUD");
                gameHUD.RefreshUpgradeList();
            });
        }
        else
        {
            Debug.LogError("[GameManager] gameHUD is null. " +
                           "Drag GameHUD into GameManager Inspector.");
        }

        // Wire onProgressChanged → requirements panel refresh
        if (requirementsUI != null)
            module.onProgressChanged.AddListener(_ => requirementsUI.RefreshAll());

        // ── onModuleRepaired → ShipRepairManager ─────────────────────────────
        // THIS IS THE KEY WIRING that cannot be done from the Inspector because
        // OnModuleRepaired(ModuleType) takes an argument that the Inspector UI
        // cannot supply. It MUST be wired here in code with AddListener.
        if (shipRepairManager != null)
        {
            ModuleType capturedType = type;
            module.onModuleRepaired.AddListener(() =>
            {
                Debug.Log($"[GameManager] {displayName} repaired — calling ShipRepairManager.OnModuleRepaired({capturedType})");
                shipRepairManager.OnModuleRepaired(capturedType);
            });
        }
        else
        {
            Warn("shipRepairManager");
        }

        // SFX on progress
        module.onProgressChanged.AddListener(_ => audioManager?.PlaySFX("repair_stage"));
    }

    private void WireRepairManagerEvents()
    {
        if (shipRepairManager == null) { Warn("shipRepairManager"); return; }

        if (endingManager != null)
            shipRepairManager.onAllModulesRepaired.AddListener(endingManager.TriggerEscapeEnding);
    }

    private void WireMeteoriteEvents()
    {
        if (meteoriteManager == null) return;
        meteoriteManager.onShowerStart.AddListener(() => audioManager?.PlayMusic("tension_zone2"));
        meteoriteManager.onRiftStart.AddListener(()   => audioManager?.PlayMusic("danger_zone3"));
    }

    private void Warn(string field) =>
        Debug.LogWarning($"[GameManager] '{field}' is not assigned in the Inspector.");
}
