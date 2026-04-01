using UnityEngine;
using System.Collections;

/// <summary>
/// Controls all meteorite strike events and spawns material drops after impacts.
///
/// CHANGES IN THIS VERSION:
///  — Material drops now spawn immediately after each meteorite impact via the
///    Meteorite.onImpact static event. Previously drops only spawned after
///    shower/rift event timers — stray hits spawned nothing.
///  — Added strayDropPrefabs array and strayDropCount so single stray hits
///    also scatter materials (lower value, common materials).
///  — MeteoriteManager now subscribes to Meteorite.OnAnyImpact event to
///    receive the impact world position and spawn drops at that location.
///  — All three event types (stray, shower, rift) now reliably drop materials.
/// </summary>
public class MeteoriteManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Meteorite Prefabs")]
    [Tooltip("Prefab for a small stray meteorite.")]
    public GameObject strayMeteoritePrefab;

    [Tooltip("Prefab for shower meteorites (slightly faster).")]
    public GameObject showerMeteoritePrefab;

    [Tooltip("Prefab for the large rift-strike meteorite.")]
    public GameObject riftMeteoritePrefab;

    [Header("Strike Intervals")]
    [Tooltip("Average seconds between stray hits. Actual interval = this ± 10 seconds.")]
    public float strayInterval = 45f;

    [Tooltip("Seconds between shower events. First shower is delayed 60 seconds.")]
    public float showerInterval = 120f;

    [Tooltip("Seconds between rift strikes. First rift delayed 180 seconds.")]
    public float riftInterval = 240f;

    [Header("Shower Settings")]
    [Tooltip("Number of meteorites in one shower wave.")]
    [Range(5, 40)]
    public int showerCount = 15;

    [Tooltip("Min delay between meteorites within a shower.")]
    public float showerMinDelay = 0.4f;

    [Tooltip("Max delay between meteorites within a shower.")]
    public float showerMaxDelay = 2f;

    [Header("Warning")]
    [Tooltip("Seconds the warning indicator shows before a stray hit.")]
    public float warningDuration = 3f;

    [Header("Spawn Area")]
    [Tooltip("Radius around the player within which meteorites target.")]
    public float targetRadius = 20f;

    [Tooltip("Distance off-screen from which meteorites spawn before flying in.")]
    public float spawnOffset = 40f;

    [Header("Material Drops — Stray Hits")]
    [Tooltip("Materials scattered after a single stray meteorite impact. " +
             "Use common Zone 1 materials: metal scraps, bolts, glass.")]
    public GameObject[] strayDropPrefabs;

    [Tooltip("Number of materials dropped by each stray hit.")]
    [Range(0, 8)]
    public int strayDropCount = 3;

    [Header("Material Drops — Shower Events")]
    [Tooltip("Materials scattered after a shower event ends. Use mid-tier materials.")]
    public GameObject[] showerDropPrefabs;

    [Tooltip("Number of materials dropped after a shower ends.")]
    [Range(1, 20)]
    public int showerDropCount = 6;

    [Header("Material Drops — Rift Strikes")]
    [Tooltip("Rare materials scattered after a rift strike. Use your rarest materials.")]
    public GameObject[] riftDropPrefabs;

    [Tooltip("Number of materials dropped after a rift strike.")]
    [Range(1, 30)]
    public int riftDropCount = 12;

    [Tooltip("Scatter radius for all dropped materials around the impact point.")]
    public float dropScatterRadius = 6f;

    [Header("Events")]
    [Tooltip("Fired when a shower event begins. Wire to AudioManager.PlayMusic if desired.")]
    public UnityEngine.Events.UnityEvent onShowerStart;

    [Tooltip("Fired when a rift strike event begins.")]
    public UnityEngine.Events.UnityEvent onRiftStart;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Transform _playerTransform;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Subscribe to the static event that every Meteorite fires on impact
        Meteorite.OnAnyImpact += HandleMeteoriteImpact;
    }

    private void OnDisable()
    {
        Meteorite.OnAnyImpact -= HandleMeteoriteImpact;
    }

    private void Start()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) _playerTransform = player.transform;

        StartCoroutine(StrayRoutine());
        StartCoroutine(ShowerRoutine());
        StartCoroutine(RiftRoutine());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Impact Handler — Receives position from Meteorite.OnAnyImpact event
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by every Meteorite when it impacts something.
    /// Spawns stray drop materials at the impact position.
    /// </summary>
    private void HandleMeteoriteImpact(Vector2 impactPosition, MeteoriteType type)
    {
        switch (type)
        {
            case MeteoriteType.Stray:
                SpawnDrops(strayDropPrefabs, strayDropCount, impactPosition);
                break;
            // Shower and Rift drops are handled after their full event completes
            // (spawned in bulk after the wave ends, not per-meteorite)
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Strike Routines
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator StrayRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(strayInterval + Random.Range(-10f, 10f));
            yield return SpawnStray();
        }
    }

    private IEnumerator SpawnStray()
    {
        Vector2 target = GetRandomTargetNearPlayer();
        WarningManager.Instance?.ShowWarning(target, warningDuration);
        yield return new WaitForSeconds(warningDuration);
        SpawnMeteorite(strayMeteoritePrefab, target, MeteoriteType.Stray);
    }

    private IEnumerator ShowerRoutine()
    {
        yield return new WaitForSeconds(60f);
        while (true)
        {
            yield return new WaitForSeconds(showerInterval);
            onShowerStart?.Invoke();
            NotificationManager.Instance?.Show("METEORITE SHOWER INCOMING!\nTake cover!", urgent: true);

            Vector2 lastImpact = GetRandomTargetNearPlayer();
            for (int i = 0; i < showerCount; i++)
            {
                Vector2 target = GetRandomTargetNearPlayer();
                lastImpact     = target;
                WarningManager.Instance?.ShowWarning(target, 2f);
                yield return new WaitForSeconds(Random.Range(showerMinDelay, showerMaxDelay));
                SpawnMeteorite(showerMeteoritePrefab, target, MeteoriteType.Shower);
            }

            yield return new WaitForSeconds(4f);
            // Shower drop: spawn near the last impact point
            SpawnDrops(showerDropPrefabs, showerDropCount, lastImpact);
            NotificationManager.Instance?.Show("Shower passed — fresh materials scattered nearby.");
        }
    }

    private IEnumerator RiftRoutine()
    {
        yield return new WaitForSeconds(180f);
        while (true)
        {
            yield return new WaitForSeconds(riftInterval);
            onRiftStart?.Invoke();
            NotificationManager.Instance?.Show("GRAVITY RIFT INCOMING!\nBrace for impact!", urgent: true);

            Vector2 riftCenter = GetRandomTargetNearPlayer();
            WarningManager.Instance?.ShowWarning(riftCenter, warningDuration + 1f);
            yield return new WaitForSeconds(warningDuration + 1f);

            SpawnMeteorite(riftMeteoritePrefab, riftCenter, MeteoriteType.Rift);
            yield return new WaitForSeconds(2f);

            CreateGravityRift(riftCenter);

            yield return new WaitForSeconds(5f);
            SpawnDrops(riftDropPrefabs, riftDropCount, riftCenter);
            NotificationManager.Instance?.Show("Rift stabilizing — rare materials at impact point!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void SpawnMeteorite(GameObject prefab, Vector2 target, MeteoriteType type)
    {
        if (prefab == null) return;
        Vector2 spawnPos = GetOffscreenSpawnPos(target);
        var obj = Instantiate(prefab, spawnPos, Quaternion.identity);
        if (obj.TryGetComponent<Meteorite>(out var m))
        {
            m.meteoriteType = type;
            m.SetTarget(target);
        }
    }

    private void SpawnDrops(GameObject[] prefabs, int count, Vector2 center)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            Vector2 pos = center + Random.insideUnitCircle * dropScatterRadius;
            var chosen  = prefabs[Random.Range(0, prefabs.Length)];
            if (chosen != null) Instantiate(chosen, pos, Quaternion.identity);
        }
    }

    private void CreateGravityRift(Vector2 center)
    {
        GameObject rift            = new GameObject("TemporaryGravityRift");
        rift.transform.position    = center;
        var zone                   = rift.AddComponent<GravityZone>();
        zone.gravityType           = GravityState.GravityRift;
        zone.gravityStrength       = 25f;
        zone.causesDisorientation  = true;
        zone.riftSpinForce         = 180f;
        var col                    = rift.AddComponent<CircleCollider2D>();
        col.isTrigger              = true;
        col.radius                 = 12f;
        Destroy(rift, 12f);
    }

    private Vector2 GetRandomTargetNearPlayer()
    {
        Vector2 origin = _playerTransform != null
            ? (Vector2)_playerTransform.position : Vector2.zero;
        return origin + Random.insideUnitCircle * (targetRadius * 0.5f);
    }

    private Vector2 GetOffscreenSpawnPos(Vector2 target)
    {
        Vector2 origin = _playerTransform != null
            ? (Vector2)_playerTransform.position : Vector2.zero;
        Vector2 dir = (target - origin).normalized;
        return target - dir * spawnOffset;
    }
}

/// <summary>Identifies which event type spawned this meteorite.</summary>
public enum MeteoriteType { Stray, Shower, Rift }
