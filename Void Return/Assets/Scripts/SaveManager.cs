using UnityEngine;
using System;

[Serializable]
public class GameSaveData
{
    public float  oxygenNormalized;
    public int    lifeSupportStageIndex;
    public int    hullPlatingStageIndex;
    public int    navigationStageIndex;
    public int    engineCoreStageIndex;
    public float  checkpointX;
    public float  checkpointY;
    public bool   hasCheckpoint;
    // NOTE: tetherUnlocked, grenadeUnlocked, thrusterUnlocked are intentionally
    // removed. Gadget active state is now derived from module stage on Load()
    // via ShipRepairManager.ReapplyUnlocksFromModuleState(). This prevents the
    // common bug where a save made before a gadget was unlocked would re-disable
    // the gadget on Retry From Save.
}

/// <summary>
/// Saves and loads game state.
///
/// GADGET RESTORE FIX:
///  Load() no longer saves/restores gadget SetActive state directly.
///  Instead, after restoring module stages, it calls
///  ShipRepairManager.ReapplyUnlocksFromModuleState() which activates
///  gadgets based on which module stages are complete.
///  This is correct regardless of what was active when the save was made.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string SAVE_KEY = "VoidReturn_Save";

    [Header("Auto-Save")]
    [Range(0f, 30f)]
    public float autoSaveIntervalMinutes = 5f;

    [Header("Module References")]
    public ShipModule lifeSupport;
    public ShipModule hullPlating;
    public ShipModule navigation;
    public ShipModule engineCore;

    [Header("Player References")]
    public OxygenSystem oxygenSystem;
    public Transform    playerTransform;

    // ─────────────────────────────────────────────────────────────────────────
    private float _autoSaveTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _autoSaveTimer = autoSaveIntervalMinutes * 60f;

        if (playerTransform == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) playerTransform = pc.transform;
        }

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
            TimedAutoSave();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    public static bool SaveFileExists() => PlayerPrefs.HasKey(SAVE_KEY);

    /// <summary>Manual save from pause menu.</summary>
    public void Save()
    {
        Write(BuildSaveJson(recordPosition: true));
        NotificationManager.Instance?.ShowInfo("Game saved.");
        Debug.Log("[SaveManager] Manual save.");
    }

    /// <summary>Called by LifeSupportZone — saves and records Life Support position as checkpoint.</summary>
    public void LifeSupportCheckpointSave()
    {
        Write(BuildSaveJson(recordPosition: true));
        NotificationManager.Instance?.ShowPickup("Checkpoint saved at Life Support.");
        Debug.Log("[SaveManager] Life Support checkpoint save.");
    }

    public void Load()
    {
        string json = PlayerPrefs.GetString(SAVE_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
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

            // Restore player position to checkpoint
            if (data.hasCheckpoint && playerTransform != null)
                playerTransform.position = new Vector3(data.checkpointX, data.checkpointY, 0f);

            // Always restore full oxygen
            oxygenSystem?.RestoreFullOxygen();

            // Re-apply gadget unlocks from module state — NOT from saved booleans.
            // This is the correct way: if Navigation Stage 1 is loaded, tether is active.
            // Using saved booleans was broken: if saved before unlock, retry re-disabled them.
            ShipRepairManager.Instance?.ReapplyUnlocksFromModuleState();

            NotificationManager.Instance?.ShowInfo("Checkpoint loaded. Oxygen restored.");
            Debug.Log("[SaveManager] Load complete.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Load failed: {ex.Message}");
            NotificationManager.Instance?.ShowWarning("Load failed!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void TimedAutoSave()
    {
        // Preserve existing checkpoint on timed auto-save
        float cpX = playerTransform?.position.x ?? 0f;
        float cpY = playerTransform?.position.y ?? 0f;
        bool  hasCp = true;
        string existing = PlayerPrefs.GetString(SAVE_KEY, "");
        if (!string.IsNullOrEmpty(existing))
        {
            try { var prev = JsonUtility.FromJson<GameSaveData>(existing); if (prev.hasCheckpoint) { cpX = prev.checkpointX; cpY = prev.checkpointY; } }
            catch { }
        }
        var data = BuildDataWithCheckpoint(cpX, cpY, hasCp);
        Write(JsonUtility.ToJson(data));
        NotificationManager.Instance?.ShowPickup("Auto-saved.");
    }

    private string BuildSaveJson(bool recordPosition)
    {
        float cpX = 0f; float cpY = 0f; bool hasCp = false;
        if (recordPosition && playerTransform != null)
        { cpX = playerTransform.position.x; cpY = playerTransform.position.y; hasCp = true; }
        return JsonUtility.ToJson(BuildDataWithCheckpoint(cpX, cpY, hasCp));
    }

    private GameSaveData BuildDataWithCheckpoint(float cpX, float cpY, bool hasCp) =>
        new GameSaveData
        {
            oxygenNormalized      = oxygenSystem?.OxygenNormalized ?? 1f,
            lifeSupportStageIndex = lifeSupport?.CurrentStageIndex ?? 0,
            hullPlatingStageIndex = hullPlating?.CurrentStageIndex ?? 0,
            navigationStageIndex  = navigation?.CurrentStageIndex  ?? 0,
            engineCoreStageIndex  = engineCore?.CurrentStageIndex  ?? 0,
            checkpointX           = cpX,
            checkpointY           = cpY,
            hasCheckpoint         = hasCp,
        };

    private void Write(string json)
    {
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }
}
