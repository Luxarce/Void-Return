using UnityEngine;

/// <summary>
/// Marks a named zone region and updates the GameHUD zone badge
/// when the player enters or exits this area.
///
/// Attach to the same GameObject as a Trigger Collider2D that defines the zone boundary.
/// This can be on the same object as GravityZone, or a separate overlay object.
///
/// Set the zoneNumber and zoneName in the Inspector to match the zone design:
///   Zone 1 — Debris Field (safest, green)
///   Zone 2 — Drift Ring (medium, yellow)
///   Zone 3 — Deep Scatter (dangerous, red)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ZoneTrigger : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Zone Identity")]
    [Tooltip("Zone number used to select the badge color in GameHUD. (1 = safe, 2 = medium, 3 = dangerous)")]
    [Range(1, 3)]
    public int zoneNumber = 1;

    [Tooltip("Display name shown on the zone badge in the HUD (e.g., 'Debris Field').")]
    public string zoneName = "Debris Field";

    [Header("Entry Notification")]
    [Tooltip("Show a notification message when the player enters this zone.")]
    public bool showEntryNotification = true;

    [Tooltip("Custom notification text shown on zone entry. Leave blank to use the zone name.")]
    public string entryMessage = "";

    [Header("Default Zone")]
    [Tooltip("If true, the GameHUD will be initialized to this zone on Start. " +
             "Enable only on the Zone 1 object (the starting zone).")]
    public bool isDefaultZone = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        GetComponent<Collider2D>().isTrigger = true;

        if (isDefaultZone)
            FindFirstObjectByType<GameHUD>()?.SetZone(zoneNumber, zoneName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Trigger Events
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        FindFirstObjectByType<GameHUD>()?.SetZone(zoneNumber, zoneName);

        if (showEntryNotification)
        {
            string msg = string.IsNullOrEmpty(entryMessage)
                ? $"Entering Zone {zoneNumber}: {zoneName}"
                : entryMessage;

            NotificationManager.Instance?.Show(msg, urgent: zoneNumber >= 3);
        }
    }
}
