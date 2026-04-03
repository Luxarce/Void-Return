using UnityEngine;

/// <summary>
/// Zone entry detector — updates HUD badge and shows entry notification.
///
/// FIXES:
///  — No Update() distance check (prevented flickering in Zone 3).
///  — Always calls MeteoriteManager.Instance?.SetPlayerZone() on entry.
///  — isDefaultZone uses Invoke(delay) so NotificationManager is ready.
///  — notificationCooldown prevents re-fire when bouncing at a zone edge.
///  — Added GetComponent<PlayerController>() secondary check in case tag is wrong.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ZoneTrigger : MonoBehaviour
{
    [Header("Zone Identity")]
    public int    zoneNumber = 1;
    public string zoneName   = "Debris Field";

    [Header("Entry Notification")]
    public bool showNotificationOnEnter = true;
    [TextArea(2, 4)]
    public string customEntryMessage = "";

    [Tooltip("Minimum seconds between re-entry notifications for this zone.")]
    [Range(5f, 60f)]
    public float notificationCooldown = 10f;

    [Header("Default Zone")]
    [Tooltip("Apply this zone badge at scene Start. Enable ONLY on Zone 1 trigger.")]
    public bool isDefaultZone = false;

    // ─────────────────────────────────────────────────────────────────────────

    private static readonly string[] ZoneDescriptions =
    {
        "",
        "Zone 1: Debris Field\nCommon materials nearby. Watch for incoming meteorites.",
        "Zone 2: Drift Ring\nMicroPull gravity active. Mid-tier materials ahead.",
        "Zone 3: Deep Scatter\nGravity Rifts ahead — high danger. Rare materials here.",
    };

    private bool  _playerInside;
    private float _lastNotificationTime = -999f;
    private GameHUD _hud;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start()
    {
        _hud = FindFirstObjectByType<GameHUD>();
        if (isDefaultZone)
            // Delay so HUD and MeteoriteManager singletons are fully initialized
            Invoke(nameof(ApplyDefaultZone), 0.5f);
    }

    private void ApplyDefaultZone()
    {
        ApplyZone();
        if (showNotificationOnEnter)
            TriggerNotification();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Physics — no per-frame Update check
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        bool wasInside = _playerInside;
        _playerInside  = true;
        ApplyZone();
        if (!wasInside && showNotificationOnEnter) TriggerNotification();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        _playerInside = false;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private bool IsPlayer(Collider2D col)
    {
        if (col.CompareTag("Player")) return true;
        if (col.GetComponent<PlayerController>() != null) return true;
        return false;
    }

    private void ApplyZone()
    {
        if (_hud == null) _hud = FindFirstObjectByType<GameHUD>();
        _hud?.SetZone(zoneNumber, zoneName);

        // Tell MeteoriteManager which zone the player is in
        MeteoriteManager.Instance?.SetPlayerZone(zoneNumber);

        Debug.Log($"[ZoneTrigger] Zone {zoneNumber}: {zoneName} applied.");
    }

    private void TriggerNotification()
    {
        if (Time.time - _lastNotificationTime < notificationCooldown) return;
        _lastNotificationTime = Time.time;

        string msg = customEntryMessage != ""
            ? customEntryMessage
            : (zoneNumber < ZoneDescriptions.Length
                ? ZoneDescriptions[zoneNumber]
                : $"Entering Zone {zoneNumber}: {zoneName}");

        if (zoneNumber >= 3)
            NotificationManager.Instance?.ShowWarning(msg);
        else
            NotificationManager.Instance?.ShowInfo(msg);
    }
}
