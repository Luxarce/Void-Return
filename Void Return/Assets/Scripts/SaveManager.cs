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
    public bool   tetherUnlocked;
    public bool   grenadeUnlocked;
    public bool   thrusterUnlocked;

    // Checkpoint position — where the player was when they last saved at Life Support.
    // On "Retry from save" the player respawns here rather than at world origin.
    public float  checkpointX;
    public float  checkpointY;
    public bool   hasCheckpoint;
}

/// <summary>
/// Saves and loads game state.
///
/// ADDITIONS:
///  1. LifeSupportCheckpointSave() — called by LifeSupportZone when the player
///     enters the Life Support zone. This is a quiet auto-save that also records
///     the player's world position as a checkpoint.
///
///  2. Load() restores the player to the checkpoint position (if one exists)
///     so "Retry from save" always returns the player to the Life Support module.
///
///  3. Both the timed auto-save (every 5 min) and the checkpoint save write to
///     the same PlayerPrefs key. The checkpoint save always overwrites — the
///     most recent save is always loaded on retry.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string SAVE_KEY = "VoidReturn_Save";

    [Header("Auto-Save")]
    [Tooltip("Timed auto-save interval in minutes. Set to 0 to disable.")]
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

        // Auto-find player transform if not assigned
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
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public static bool SaveFileExists() => PlayerPrefs.HasKey(SAVE_KEY);

    /// <summary>
    /// Manual save triggered by the pause menu Save button.
    /// Records current position as checkpoint.
    /// </summary>
    public void Save()
    {
        string json = BuildSaveJson(recordCheckpoint: true);
        Write(json);
        NotificationManager.Instance?.ShowInfo("Game saved.");
        Debug.Log($"[SaveManager] Manual save.");
    }

    /// <summary>
    /// Called by LifeSupportZone when the player enters the Life Support area.
    /// Quietly saves progress and records the Life Support position as the checkpoint.
    /// This is the save that "Retry from save" loads on death.
    /// </summary>
    public void LifeSupportCheckpointSave()
    {
        string json = BuildSaveJson(recordCheckpoint: true);
        Write(json);
        // Very small notification so it doesn't interrupt gameplay
        NotificationManager.Instance?.ShowPickup("Checkpoint saved at Life Support.");
        Debug.Log("[SaveManager] Life Support checkpoint save.");
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

            // Restore gadgets
            if (tetherGun    != null) tetherGun.gameObject.SetActive(data.tetherUnlocked);
            if (grenadeGun   != null) grenadeGun.gameObject.SetActive(data.grenadeUnlocked);
            if (thrusterPack != null) thrusterPack.gameObject.SetActive(data.thrusterUnlocked);

            // Restore player to checkpoint position (Life Support location)
            if (data.hasCheckpoint && playerTransform != null)
            {
                playerTransform.position = new Vector3(data.checkpointX, data.checkpointY, 0f);
                Debug.Log($"[SaveManager] Restored player to checkpoint ({data.checkpointX:F1}, {data.checkpointY:F1})");
            }

            // Always restore full oxygen on load
            oxygenSystem?.RestoreFullOxygen();

            NotificationManager.Instance?.ShowInfo("Checkpoint loaded. Oxygen fully restored.");
            Debug.Log("[SaveManager] Load complete.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Load failed: {ex.Message}");
            NotificationManager.Instance?.ShowWarning("Load failed!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void TimedAutoSave()
    {
        // Timed auto-save preserves existing checkpoint position
        string existing = PlayerPrefs.GetString(SAVE_KEY, "");
        bool hasExistingCheckpoint = false;
        float cpX = 0f, cpY = 0f;

        if (!string.IsNullOrEmpty(existing))
        {
            try
            {
                var prev = JsonUtility.FromJson<GameSaveData>(existing);
                hasExistingCheckpoint = prev.hasCheckpoint;
                cpX = prev.checkpointX;
                cpY = prev.checkpointY;
            }
            catch { }
        }

        string json = BuildSaveJson(
            recordCheckpoint: playerTransform != null,
            overrideCheckpointX: hasExistingCheckpoint ? cpX : (playerTransform?.position.x ?? 0f),
            overrideCheckpointY: hasExistingCheckpoint ? cpY : (playerTransform?.position.y ?? 0f),
            overrideHasCheckpoint: hasExistingCheckpoint || playerTransform != null);

        Write(json);
        NotificationManager.Instance?.ShowPickup("Auto-saved.");
        Debug.Log("[SaveManager] Timed auto-save.");
    }

    private string BuildSaveJson(bool recordCheckpoint,
                                  float overrideCheckpointX = 0f,
                                  float overrideCheckpointY = 0f,
                                  bool overrideHasCheckpoint = false)
    {
        float cpX = 0f, cpY = 0f;
        bool  hasCp = false;

        if (recordCheckpoint && playerTransform != null)
        {
            cpX  = playerTransform.position.x;
            cpY  = playerTransform.position.y;
            hasCp = true;
        }
        else if (overrideHasCheckpoint)
        {
            cpX   = overrideCheckpointX;
            cpY   = overrideCheckpointY;
            hasCp = overrideHasCheckpoint;
        }

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
            checkpointX           = cpX,
            checkpointY           = cpY,
            hasCheckpoint         = hasCp,
        };
        return JsonUtility.ToJson(data);
    }

    private void Write(string json)
    {
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }
}
