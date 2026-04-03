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
/// Saves and loads game state.
///
/// ADDITION: Auto-save every autoSaveIntervalMinutes (default 5 minutes).
/// A small notification confirms the auto-save.
/// Load always restores full oxygen.
/// SaveFileExists() is static so MainMenuManager can call it without a scene reference.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string SAVE_KEY = "VoidReturn_Save";

    [Header("Auto-Save")]
    [Tooltip("Auto-save interval in minutes. Set to 0 to disable auto-save.")]
    [Range(0f, 30f)]
    public float autoSaveIntervalMinutes = 5f;

    [Header("Module References")]
    public ShipModule lifeSupport;
    public ShipModule hullPlating;
    public ShipModule navigation;
    public ShipModule engineCore;

    [Header("Player References")]
    public OxygenSystem oxygenSystem;

    [Header("Gadget References")]
    public TetherGun              tetherGun;
    public GravityGrenadeLauncher grenadeGun;
    public ThrusterPack           thrusterPack;

    // ─────────────────────────────────────────────────────────────────────────
    private float _autoSaveTimer;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _autoSaveTimer = autoSaveIntervalMinutes * 60f;

        if (PlayerPrefs.GetInt("LoadOnStart", 0) == 1)
        {
            PlayerPrefs.SetInt("LoadOnStart", 0);
            Load();
        }
    }

    private void Update()
    {
        if (autoSaveIntervalMinutes <= 0f) return;

        _autoSaveTimer -= Time.deltaTime;
        if (_autoSaveTimer <= 0f)
        {
            _autoSaveTimer = autoSaveIntervalMinutes * 60f;
            AutoSave();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Static helper — called by MainMenuManager (no scene instance needed)
    // ─────────────────────────────────────────────────────────────────────────

    public static bool SaveFileExists() => PlayerPrefs.HasKey(SAVE_KEY);

    // ─────────────────────────────────────────────────────────────────────────

    public void Save()
    {
        string json = BuildSaveJson();
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
        NotificationManager.Instance?.ShowInfo("Game saved.");
        Debug.Log($"[SaveManager] Manual save: {json}");
    }

    private void AutoSave()
    {
        string json = BuildSaveJson();
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
        // Quiet notification for auto-save (pickup channel = small and brief)
        NotificationManager.Instance?.ShowPickup("Auto-saved.");
        Debug.Log("[SaveManager] Auto-saved.");
    }

    private string BuildSaveJson()
    {
        var data = new GameSaveData
        {
            oxygenNormalized      = oxygenSystem?.OxygenNormalized ?? 1f,
            lifeSupportStageIndex = lifeSupport?.CurrentStageIndex ?? 0,
            hullPlatingStageIndex = hullPlating?.CurrentStageIndex ?? 0,
            navigationStageIndex  = navigation?.CurrentStageIndex  ?? 0,
            engineCoreStageIndex  = engineCore?.CurrentStageIndex  ?? 0,
            tetherUnlocked        = tetherGun?.gameObject.activeSelf    ?? false,
            grenadeUnlocked       = grenadeGun?.gameObject.activeSelf   ?? false,
            thrusterUnlocked      = thrusterPack?.gameObject.activeSelf ?? false,
        };
        return JsonUtility.ToJson(data);
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
            lifeSupport?.LoadStageIndex(data.lifeSupportStageIndex);
            hullPlating?.LoadStageIndex(data.hullPlatingStageIndex);
            navigation?.LoadStageIndex(data.navigationStageIndex);
            engineCore?.LoadStageIndex(data.engineCoreStageIndex);
            if (tetherGun    != null) tetherGun.gameObject.SetActive(data.tetherUnlocked);
            if (grenadeGun   != null) grenadeGun.gameObject.SetActive(data.grenadeUnlocked);
            if (thrusterPack != null) thrusterPack.gameObject.SetActive(data.thrusterUnlocked);

            // Always restore full oxygen on load
            oxygenSystem?.RestoreFullOxygen();

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
