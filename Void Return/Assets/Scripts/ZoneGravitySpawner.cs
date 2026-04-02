using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Randomly spawns gravity zones within a designated area at scene load.
///
/// ZONE RULES:
///  Zone 1 (Debris Field)  → spawns ZeroG Gravity Zones (push player outward)
///  Zone 2 (Drift Ring)    → spawns MicroPull Gravity Zones (gentle pull inward)
///  Zone 3 (Deep Scatter)  → spawns GravityRift Zones (strong pull + spin)
///
/// SETUP:
///  1. Create an empty GameObject for each zone area (e.g., Zone1Area, Zone2Area, Zone3Area).
///  2. Attach ZoneGravitySpawner to each.
///  3. Set spawnAreaCenter and spawnAreaSize to cover the zone's world-space bounds.
///  4. Set zoneType to ZeroG / MicroPull / GravityRift accordingly.
///  5. Adjust spawnCount, minRadius, maxRadius.
///
/// The spawned GravityZone GameObjects are cleaned up on scene unload automatically.
/// </summary>
public class ZoneGravitySpawner : MonoBehaviour
{
    [Header("Zone Type")]
    [Tooltip("Which gravity zone type this spawner creates.\n" +
             "Zone 1 = ZeroG, Zone 2 = MicroPull, Zone 3 = GravityRift")]
    public GravityState zoneType = GravityState.ZeroG;

    [Header("Spawn Area")]
    [Tooltip("Center of the rectangular spawn region in world space.")]
    public Vector2 spawnAreaCenter = Vector2.zero;

    [Tooltip("Width and Height of the spawn region.")]
    public Vector2 spawnAreaSize = new Vector2(40f, 40f);

    [Header("Zone Count")]
    [Tooltip("Number of gravity zones to spawn.")]
    [Range(1, 20)]
    public int spawnCount = 5;

    [Tooltip("Minimum radius of each spawned zone.")]
    [Range(1f, 20f)]
    public float minRadius = 4f;

    [Tooltip("Maximum radius of each spawned zone.")]
    [Range(2f, 30f)]
    public float maxRadius = 10f;

    [Tooltip("Minimum distance between any two spawned zones (prevents overlap).")]
    [Range(0f, 30f)]
    public float minSeparation = 6f;

    [Header("Zone Strengths")]
    [Tooltip("Strength for ZeroG zones (push force).")]
    public float zeroGStrength     = 2f;

    [Tooltip("Strength for MicroPull zones (pull force).")]
    public float microPullStrength = 3f;

    [Tooltip("Strength for GravityRift zones (pull force).")]
    public float riftPullStrength  = 12f;

    [Tooltip("Spin speed for Rift zones (degrees/sec).")]
    public float riftSpinForce     = 120f;

    [Header("Gizmo Colors per Type")]
    public Color zeroGColor     = new Color(0f, 1f, 0.8f, 0.2f);
    public Color microPullColor = new Color(0.5f, 0f, 1f, 0.2f);
    public Color riftColor      = new Color(1f, 0.1f, 0f, 0.25f);

    // ─────────────────────────────────────────────────────────────────────────
    private readonly List<Vector2> _spawnedPositions = new();
    private readonly List<GameObject> _spawnedZones  = new();

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        SpawnZones();
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void SpawnZones()
    {
        _spawnedPositions.Clear();

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 pos = TryFindPosition(30);
            if (pos == Vector2.negativeInfinity) continue;  // no valid position found

            float radius = Random.Range(minRadius, maxRadius);
            SpawnZoneAt(pos, radius);
            _spawnedPositions.Add(pos);
        }
    }

    private Vector2 TryFindPosition(int maxAttempts)
    {
        float halfW = spawnAreaSize.x * 0.5f;
        float halfH = spawnAreaSize.y * 0.5f;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float   rx  = Random.Range(-halfW, halfW);
            float   ry  = Random.Range(-halfH, halfH);
            Vector2 pos = spawnAreaCenter + new Vector2(rx, ry);

            bool tooClose = false;
            foreach (var existing in _spawnedPositions)
                if (Vector2.Distance(pos, existing) < minSeparation)
                { tooClose = true; break; }

            if (!tooClose) return pos;
        }

        return Vector2.negativeInfinity;
    }

    private void SpawnZoneAt(Vector2 pos, float radius)
    {
        var go = new GameObject($"GravZone_{zoneType}_{_spawnedZones.Count}");
        go.transform.position = pos;
        go.transform.SetParent(transform);

        var zone = go.AddComponent<GravityZone>();
        zone.gravityType = zoneType;

        switch (zoneType)
        {
            case GravityState.ZeroG:
                zone.zeroGPushStrength = zeroGStrength;
                zone.zoneGizmoColor    = zeroGColor;
                break;
            case GravityState.MicroPull:
                zone.microPullStrength = microPullStrength;
                zone.zoneGizmoColor    = microPullColor;
                break;
            case GravityState.GravityRift:
                zone.riftPullStrength     = riftPullStrength;
                zone.riftSpinForce        = riftSpinForce;
                zone.causesDisorientation = true;
                zone.zoneGizmoColor       = riftColor;
                break;
        }

        var col      = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius   = radius;

        _spawnedZones.Add(go);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Color c = zoneType switch
        {
            GravityState.MicroPull  => microPullColor,
            GravityState.GravityRift => riftColor,
            _                       => zeroGColor
        };
        Gizmos.color = new Color(c.r, c.g, c.b, 0.1f);
        Gizmos.DrawCube(spawnAreaCenter, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f));
        Gizmos.color = new Color(c.r, c.g, c.b, 0.5f);
        Gizmos.DrawWireCube(spawnAreaCenter, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f));
    }
}
