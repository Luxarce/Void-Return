using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game manager that wires all cross-system events in code.
///
/// PURPOSE:
/// This script replaces all the manual Inspector event wiring from the
/// previous guides. Instead of dragging GameObjects into UnityEvent panels,
/// you simply assign the scene component references in THIS script's Inspector
/// fields and all connections are made automatically at runtime.
///
/// SETUP:
///  1. Create an empty GameObject named 'GameManager' in the GameScene.
///  2. Attach this script.
///  3. Assign every reference in the Inspector (drag in from the Hierarchy).
///  4. Do NOT wire events manually in other script Inspectors — this handles it.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields — Assign all of these in the Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Player Systems")]
    [Tooltip("OxygenSystem component on the Player GameObject.")]
    public OxygenSystem oxygenSystem;

    [Header("HUD")]
    [Tooltip("GameHUD component on the GameCanvas.")]
    public GameHUD gameHUD;

    [Header("Ship Modules")]
    [Tooltip("ShipModule on the Life Support repair point.")]
    public ShipModule lifeSupport;

    [Tooltip("ShipModule on the Hull Plating repair point.")]
    public ShipModule hullPlating;

    [Tooltip("ShipModule on the Navigation repair point.")]
    public ShipModule navigation;

    [Tooltip("ShipModule on the Engine Core repair point.")]
    public ShipModule engineCore;

    [Header("Managers")]
    [Tooltip("ShipRepairManager in the scene.")]
    public ShipRepairManager shipRepairManager;

    [Tooltip("EndingManager in the scene.")]
    public EndingManager endingManager;

    [Tooltip("MeteoriteManager in the scene.")]
    public MeteoriteManager meteoriteManager;

    [Header("Notification / Warning")]
    [Tooltip("NotificationManager in the scene.")]
    public NotificationManager notificationManager;

    [Header("Audio")]
    [Tooltip("AudioManager in the scene (or DontDestroyOnLoad from MainMenu).")]
    public AudioManager audioManager;

    [Header("Inventory UI")]
    [Tooltip("InventoryUI script on the inventory panel.")]
    public InventoryUI inventoryUI;

    [Header("Material Requirements UI")]
    [Tooltip("MaterialRequirementsUI script on its panel.")]
    public MaterialRequirementsUI requirementsUI;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        WireOxygenEvents();
        WireModuleEvents();
        WireRepairManagerEvents();
        WireMeteoriteEvents();
        Debug.Log("[GameManager] All events wired successfully.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Wiring
    // ─────────────────────────────────────────────────────────────────────────

    private void WireOxygenEvents()
    {
        if (oxygenSystem == null) { Warn("OxygenSystem"); return; }

        // Oxygen bar updates every frame
        if (gameHUD != null)
            oxygenSystem.onOxygenChanged.AddListener(gameHUD.UpdateOxygen);

        // Low oxygen warning
        oxygenSystem.onLowOxygen.AddListener(() =>
        {
            notificationManager?.Show("Oxygen running low — find a canister!", urgent: false);
            audioManager?.PlaySFX("oxygen_warning");
        });

        // Critical oxygen warning
        oxygenSystem.onCriticalOxygen.AddListener(() =>
        {
            notificationManager?.Show("CRITICAL OXYGEN!", urgent: true);
            audioManager?.PlaySFX("oxygen_critical");
        });

        // Oxygen depleted — show game over
        if (gameHUD != null)
            oxygenSystem.onOxygenDepleted.AddListener(gameHUD.ShowGameOver);
    }

    private void WireModuleEvents()
    {
        WireSingleModule(lifeSupport,   0, "Life Support",  ModuleType.LifeSupport);
        WireSingleModule(hullPlating,   1, "Hull Plating",  ModuleType.HullPlating);
        WireSingleModule(navigation,    2, "Navigation",    ModuleType.Navigation);
        WireSingleModule(engineCore,    3, "Engine Core",   ModuleType.EngineCore);
    }

    private void WireSingleModule(ShipModule module, int index,
                                   string displayName, ModuleType type)
    {
        if (module == null) { Warn($"ShipModule [{displayName}]"); return; }

        // Progress bar update
        if (gameHUD != null)
        {
            int capturedIndex = index;
            module.onProgressChanged.AddListener(p =>
                gameHUD.UpdateModuleProgress(capturedIndex, p));
        }

        // Requirements UI refresh when progress changes
        if (requirementsUI != null)
            module.onProgressChanged.AddListener(_ => requirementsUI.RefreshAll());

        // Notify ShipRepairManager on full repair
        if (shipRepairManager != null)
        {
            ModuleType capturedType = type;
            module.onModuleRepaired.AddListener(() =>
                shipRepairManager.OnModuleRepaired(capturedType));
        }

        // Play repair sound on stage complete
        module.onProgressChanged.AddListener(_ =>
            audioManager?.PlaySFX("repair_stage"));
    }

    private void WireRepairManagerEvents()
    {
        if (shipRepairManager == null) { Warn("ShipRepairManager"); return; }

        // All modules repaired → trigger ending
        if (endingManager != null)
            shipRepairManager.onAllModulesRepaired.AddListener(
                endingManager.TriggerEscapeEnding);
    }

    private void WireMeteoriteEvents()
    {
        if (meteoriteManager == null) return;

        // Shower start — change music
        meteoriteManager.onShowerStart.AddListener(() =>
            audioManager?.PlayMusic("tension_zone2"));

        // Rift start — change music
        meteoriteManager.onRiftStart.AddListener(() =>
            audioManager?.PlayMusic("danger_zone3"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper
    // ─────────────────────────────────────────────────────────────────────────

    private void Warn(string field) =>
        Debug.LogWarning($"[GameManager] '{field}' is not assigned. " +
                         "Drag it into the GameManager Inspector.");
}
