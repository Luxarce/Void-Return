using UnityEngine;
using System;

[Serializable]
public class GameSaveData
{
    public float oxygenNormalized;
    public int   lifeSupportStageIndex;
    public int   hullPlatingStageIndex;
    public int   navigationStageIndex;
    public int   engineCoreStageIndex;
    public bool  tetherUnlocked;
    public bool  grenadeUnlocked;
    public bool  thrusterUnlocked;
}

/// <summary>
/// Saves and loads game state to PlayerPrefs.
///
/// FIX — FULL OXYGEN ON LOAD:
///  When the player dies and loads from save, they should respawn with full
///  oxygen (not the low oxygen that caused their death). Previously
///  Load() restored oxygenNormalized from the save — which was the saved
///  value at save time, but if they saved with low oxygen they'd still respawn
///  with low oxygen.
///
///  Behavior:
///   Save() stores current oxygen (so the player can reload a mid-game state).
///   Load() ALWAYS restores oxygen to FULL, regardless of the saved value.
///   This gives the player a fair restart. The saved value is intentionally ignored.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string SAVE_KEY = "VoidReturn_Save";

    [Header("Module References")]
    public ShipModule lifeSupport;
    public ShipModule hullPlating;
    public ShipModule navigation;
    public ShipModule engineCore;

    [Header("Player References")]
    [Tooltip("OxygenSystem on the Player.")]
    public OxygenSystem oxygenSystem;

    [Header("Gadget References")]
    public TetherGun              tetherGun;
    public GravityGrenadeLauncher grenadeGun;
    public ThrusterPack           thrusterPack;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (PlayerPrefs.GetInt("LoadOnStart", 0) == 1)
        {
            PlayerPrefs.SetInt("LoadOnStart", 0);
            Load();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void Save()
    {
        var data = new GameSaveData
        {
            oxygenNormalized      = oxygenSystem?.OxygenNormalized ?? 1f,
            lifeSupportStageIndex = lifeSupport?.CurrentStageIndex ?? 0,
            hullPlatingStageIndex = hullPlating?.CurrentStageIndex ?? 0,
            navigationStageIndex  = navigation?.CurrentStageIndex  ?? 0,
            engineCoreStageIndex  = engineCore?.CurrentStageIndex  ?? 0,
            tetherUnlocked        = tetherGun?.gameObject.activeSelf   ?? false,
            grenadeUnlocked       = grenadeGun?.gameObject.activeSelf  ?? false,
            thrusterUnlocked      = thrusterPack?.gameObject.activeSelf ?? false,
        };

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();

        NotificationManager.Instance?.ShowInfo("Game saved.");
        Debug.Log($"[SaveManager] Saved: {json}");
    }

    public void Load()
    {
        string json = PlayerPrefs.GetString(SAVE_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
            Debug.Log("[SaveManager] No save data found.");
            NotificationManager.Instance?.ShowInfo("No save data found.");
            return;
        }

        try
        {
            var data = JsonUtility.FromJson<GameSaveData>(json);

            // Restore module stages
            lifeSupport?.LoadStageIndex(data.lifeSupportStageIndex);
            hullPlating?.LoadStageIndex(data.hullPlatingStageIndex);
            navigation?.LoadStageIndex(data.navigationStageIndex);
            engineCore?.LoadStageIndex(data.engineCoreStageIndex);

            // Restore gadget unlock state
            if (tetherGun    != null) tetherGun.gameObject.SetActive(data.tetherUnlocked);
            if (grenadeGun   != null) grenadeGun.gameObject.SetActive(data.grenadeUnlocked);
            if (thrusterPack != null) thrusterPack.gameObject.SetActive(data.thrusterUnlocked);

            // ── ALWAYS RESTORE FULL OXYGEN ON LOAD ────────────────────────────
            // Intentionally ignore data.oxygenNormalized.
            // If the player died from oxygen depletion and loads from save,
            // they should start fresh with full oxygen, not respawn already dying.
            if (oxygenSystem != null)
                oxygenSystem.RestoreFullOxygen();

            NotificationManager.Instance?.ShowInfo("Save loaded. Oxygen fully restored.");
            Debug.Log("[SaveManager] Loaded. Oxygen reset to full.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Load failed: {ex.Message}");
            NotificationManager.Instance?.ShowWarning("Load failed!");
        }
    }
}
