using UnityEngine;
using System.IO;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// Save Data Classes
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A single inventory entry stored in the save file.
/// </summary>
[System.Serializable]
public class SavedInventoryItem
{
    public MaterialType type;
    public int          quantity;
}

/// <summary>
/// All game state data serialized to JSON for saving.
/// </summary>
[System.Serializable]
public class SaveData
{
    // Player state
    public float   oxygenNormalized;
    public float   bootsStaminaNormalized;
    public float   thrusterFuelNormalized;
    public Vector2 playerPosition;

    // Module repair progress
    public float lifeSupportProgress;
    public float hullPlatingProgress;
    public float navigationProgress;
    public float engineCoreProgress;

    public bool lifeSupportRepaired;
    public bool hullPlatingRepaired;
    public bool navigationRepaired;
    public bool engineCoreRepaired;

    // Current repair stage index per module
    public int lifeSupportStageIndex;
    public int hullPlatingStageIndex;
    public int navigationStageIndex;
    public int engineCoreStageIndex;

    // Inventory
    public List<SavedInventoryItem> inventory = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// SaveManager
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Handles saving and loading all game state to a JSON file on disk.
/// Uses Application.persistentDataPath so it works on all platforms.
///
/// Create one instance per game scene on an empty GameObject.
/// Assign all scene references in the Inspector.
/// Wire Save button → SaveManager.Save()
/// Wire Load button → SaveManager.Load()
/// </summary>
public class SaveManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static SaveManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Save File")]
    [Tooltip("Name of the save file stored in the persistent data path (no extension needed).")]
    public string saveFileName = "void_return_save";

    [Header("Player References — Drag from Scene")]
    [Tooltip("Transform of the Player GameObject (for position save/load).")]
    public Transform playerTransform;

    [Tooltip("OxygenSystem script on the Player.")]
    public OxygenSystem oxygenSystem;

    [Tooltip("GravityBoots script on the Player (child).")]
    public GravityBoots gravityBoots;

    [Tooltip("ThrusterPack script on the Player (child).")]
    public ThrusterPack thrusterPack;

    [Header("Module References — Drag from Scene")]
    [Tooltip("ShipModule script for Life Support.")]
    public ShipModule lifeSupport;

    [Tooltip("ShipModule script for Hull Plating.")]
    public ShipModule hullPlating;

    [Tooltip("ShipModule script for Navigation.")]
    public ShipModule navigation;

    [Tooltip("ShipModule script for Engine Core.")]
    public ShipModule engineCore;

    // ─────────────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────────────

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName + ".json");

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Auto-load if the MainMenuManager set this flag via Continue button
        if (PlayerPrefs.GetInt("LoadOnStart", 0) == 1)
        {
            PlayerPrefs.SetInt("LoadOnStart", 0);
            Load();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves all current game state to disk.
    /// Wire to the Save button in the pause menu.
    /// </summary>
    public void Save()
    {
        var data = new SaveData();

        // Player state
        if (playerTransform != null) data.playerPosition          = playerTransform.position;
        if (oxygenSystem    != null) data.oxygenNormalized        = oxygenSystem.OxygenNormalized;
        if (gravityBoots    != null) data.bootsStaminaNormalized   = gravityBoots.StaminaNormalized;
        if (thrusterPack    != null) data.thrusterFuelNormalized   = thrusterPack.FuelNormalized;

        // Module progress
        if (lifeSupport != null)
        {
            data.lifeSupportProgress  = lifeSupport.Progress;
            data.lifeSupportRepaired  = lifeSupport.IsFullyRepaired;
            data.lifeSupportStageIndex = lifeSupport.CurrentStageIndex;
        }
        if (hullPlating != null)
        {
            data.hullPlatingProgress  = hullPlating.Progress;
            data.hullPlatingRepaired  = hullPlating.IsFullyRepaired;
            data.hullPlatingStageIndex = hullPlating.CurrentStageIndex;
        }
        if (navigation != null)
        {
            data.navigationProgress   = navigation.Progress;
            data.navigationRepaired   = navigation.IsFullyRepaired;
            data.navigationStageIndex = navigation.CurrentStageIndex;
        }
        if (engineCore != null)
        {
            data.engineCoreProgress   = engineCore.Progress;
            data.engineCoreRepaired   = engineCore.IsFullyRepaired;
            data.engineCoreStageIndex = engineCore.CurrentStageIndex;
        }

        // Inventory
        if (Inventory.Instance != null)
        {
            foreach (var item in Inventory.Instance.GetAllItems())
            {
                if (item.quantity > 0)
                    data.inventory.Add(new SavedInventoryItem { type = item.type, quantity = item.quantity });
            }
        }

        // Serialize and write
        string json = JsonUtility.ToJson(data, prettyPrint: true);

        try
        {
            File.WriteAllText(SaveFilePath, json);
            NotificationManager.Instance?.Show("Game saved successfully!");
            Debug.Log($"[SaveManager] Saved to: {SaveFilePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveManager] Save failed: {ex.Message}");
            NotificationManager.Instance?.Show("Save failed!", urgent: true);
        }
    }

    /// <summary>
    /// Loads game state from disk and restores all systems.
    /// Wire to the Load button in the pause menu and the Continue button in the main menu.
    /// </summary>
    public void Load()
    {
        if (!File.Exists(SaveFilePath))
        {
            Debug.LogWarning("[SaveManager] No save file found at: " + SaveFilePath);
            NotificationManager.Instance?.Show("No save file found.");
            return;
        }

        string json;
        try
        {
            json = File.ReadAllText(SaveFilePath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveManager] Load failed: {ex.Message}");
            NotificationManager.Instance?.Show("Load failed!", urgent: true);
            return;
        }

        SaveData data = JsonUtility.FromJson<SaveData>(json);
        if (data == null) return;

        // Restore player position
        if (playerTransform != null)
            playerTransform.position = data.playerPosition;

        // Restore inventory
        if (Inventory.Instance != null)
        {
            Inventory.Instance.Clear();
            foreach (var item in data.inventory)
                Inventory.Instance.AddItem(item.type, item.quantity);
        }

        // Restore module stage indices so repair resumes from correct stage
        lifeSupport?.LoadStageIndex(data.lifeSupportStageIndex);
        hullPlating?.LoadStageIndex(data.hullPlatingStageIndex);
        navigation?.LoadStageIndex(data.navigationStageIndex);
        engineCore?.LoadStageIndex(data.engineCoreStageIndex);

        NotificationManager.Instance?.Show("Game loaded!");
        Debug.Log("[SaveManager] Game loaded from: " + SaveFilePath);
    }

    /// <summary>
    /// Returns true if a save file exists. Used by the Main Menu to enable the Continue button.
    /// </summary>
    public static bool SaveFileExists()
    {
        string path = Path.Combine(
            Application.persistentDataPath, "void_return_save.json");
        return File.Exists(path);
    }
}
