using UnityEngine;

/// <summary>
/// Marks a named zone region.
/// On player entry: updates the HUD zone badge (color changes per zone)
/// and shows an informational notification about the zone.
///
/// ZONE COLORS:
///   Zone 1 (Debris Field)  — Green/teal
///   Zone 2 (Drift Ring)    — Yellow/amber
///   Zone 3 (Deep Scatter)  — Red/orange
///
/// The GameHUD.SetZone() call passes the zone number so the badge image color
/// changes automatically based on the zone1Color / zone2Color / zone3Color fields.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ZoneTrigger : MonoBehaviour
{
    [Header("Zone Identity")]
    [Tooltip("Zone number: 1 = Debris Field, 2 = Drift Ring, 3 = Deep Scatter.")]
    public int zoneNumber = 1;

    [Tooltip("Zone display name shown on the HUD badge.")]
    public string zoneName = "Debris Field";

    [Header("Entry Notification")]
    [Tooltip("Show an informational notification when the player enters this zone.")]
    public bool showNotificationOnEnter = true;

    [Tooltip("Custom notification text. Leave blank to use the auto-generated zone description.")]
    [TextArea(2, 4)]
    public string customEntryMessage = "";

    [Header("Default Zone")]
    [Tooltip("If true, this zone's badge is shown at Start. " +
             "Enable only on the Zone 1 trigger (the player starts here).")]
    public bool isDefaultZone = false;

    // ─────────────────────────────────────────────────────────────────────────

    private static readonly string[] ZoneDescriptions =
    {
        "",  // index 0 unused
        "Zone 1: Debris Field\nCommon materials nearby. Watch for incoming meteorites.",
        "Zone 2: Drift Ring\nMicroPull currents active. Mid-tier materials ahead.",
        "Zone 3: Deep Scatter\nGravity Rifts detected. Rare materials — high danger.",
    };

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start()
    {
        if (isDefaultZone)
            FindFirstObjectByType<GameHUD>()?.SetZone(zoneNumber, zoneName);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // Update the HUD zone badge (color + name)
        FindFirstObjectByType<GameHUD>()?.SetZone(zoneNumber, zoneName);

        if (!showNotificationOnEnter) return;

        // Build or use the custom entry message
        string msg = customEntryMessage != ""
            ? customEntryMessage
            : (zoneNumber < ZoneDescriptions.Length
                ? ZoneDescriptions[zoneNumber]
                : $"Entering Zone {zoneNumber}: {zoneName}");

        // Zone 3 entry is shown as a warning, others as info
        if (zoneNumber >= 3)
            NotificationManager.Instance?.ShowWarning(msg);
        else
            NotificationManager.Instance?.ShowInfo(msg);
    }
}
