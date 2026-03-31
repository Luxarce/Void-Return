using UnityEngine;
using System.Collections;

/// <summary>
/// Controls all meteorite strike events in the game:
/// — Stray Hits: random single meteorite strikes
/// — Shower Events: timed waves of multiple meteorites
/// — Rift Strikes: rare catastrophic impacts creating gravity rifts
///
/// Create one instance in the scene on an empty GameObject.
/// Assign all prefabs and settings in the Inspector.
/// </summary>
public class MeteoriteManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Meteorite Prefabs")]
    [Tooltip("Prefab for a small single stray meteorite. Needs a Meteorite script.")]
    public GameObject strayMeteoritePrefab;

    [Tooltip("Prefab for shower meteorites (slightly faster/smaller). Needs a Meteorite script.")]
    public GameObject showerMeteoritePrefab;

    [Tooltip("Prefab for the large rift strike meteorite. Needs a Meteorite script.")]
    public GameObject riftMeteoritePrefab;

    [Header("Strike Intervals")]
    [Tooltip("Average seconds between random stray hits. Actual interval is ± 10 seconds.")]
    public float strayInterval = 45f;

    [Tooltip("Seconds between shower events. First shower delayed by 60 seconds.")]
    public float showerInterval = 120f;

    [Tooltip("Seconds between rift strikes. First rift delayed by 180 seconds.")]
    public float riftInterval = 240f;

    [Header("Shower Settings")]
    [Tooltip("How many meteorites appear in one shower event.")]
    [Range(5, 40)]
    public int showerCount = 15;

    [Tooltip("Min random delay between each meteorite in a shower.")]
    public float showerMinDelay = 0.4f;

    [Tooltip("Max random delay between each meteorite in a shower.")]
    public float showerMaxDelay = 2f;

    [Header("Warning")]
    [Tooltip("How many seconds the impact warning indicator shows before a stray hit.")]
    public float warningDuration = 3f;

    [Header("Spawn")]
    [Tooltip("Approximate radius around the player where meteorites can target.")]
    public float targetRadius = 20f;

    [Tooltip("Extra distance off-screen where meteorites spawn before flying in.")]
    public float spawnOffset = 40f;

    [Header("Post-Event Drops")]
    [Tooltip("Material prefabs to scatter after a shower. Pick a variety of mid-tier materials.")]
    public GameObject[] showerDropPrefabs;

    [Tooltip("Material prefabs to scatter after a rift. Use your rarest materials here.")]
    public GameObject[] riftDropPrefabs;

    [Tooltip("Number of material drops after a shower event.")]
    [Range(1, 20)]
    public int showerDropCount = 5;

    [Tooltip("Number of material drops after a rift strike.")]
    [Range(1, 30)]
    public int riftDropCount = 10;

    [Tooltip("Scatter radius for dropped materials after an event.")]
    public float dropScatterRadius = 8f;

    [Header("Events")]
    [Tooltip("Fired when a shower event starts. Use to change music or camera shake.")]
    public UnityEngine.Events.UnityEvent onShowerStart;

    [Tooltip("Fired when a rift strike event starts.")]
    public UnityEngine.Events.UnityEvent onRiftStart;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Transform _playerTransform;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) _playerTransform = player.transform;

        StartCoroutine(StrayRoutine());
        StartCoroutine(ShowerRoutine());
        StartCoroutine(RiftRoutine());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Strike Routines
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator StrayRoutine()
    {
        while (true)
        {
            float wait = strayInterval + Random.Range(-10f, 10f);
            yield return new WaitForSeconds(wait);
            yield return SpawnStray();
        }
    }

    private IEnumerator SpawnStray()
    {
        Vector2 target = GetRandomTargetNearPlayer();

        // Show warning, then spawn
        WarningManager.Instance?.ShowWarning(target, warningDuration);
        yield return new WaitForSeconds(warningDuration);

        SpawnMeteorite(strayMeteoritePrefab, target);
    }

    private IEnumerator ShowerRoutine()
    {
        yield return new WaitForSeconds(60f); // Initial delay

        while (true)
        {
            yield return new WaitForSeconds(showerInterval);

            onShowerStart?.Invoke();
            NotificationManager.Instance?.Show("METEORITE SHOWER INCOMING!\nTake cover!", urgent: true);

            for (int i = 0; i < showerCount; i++)
            {
                Vector2 target = GetRandomTargetNearPlayer();
                WarningManager.Instance?.ShowWarning(target, 2f);
                yield return new WaitForSeconds(Random.Range(showerMinDelay, showerMaxDelay));
                SpawnMeteorite(showerMeteoritePrefab, target);
            }

            // After shower, spawn material drops
            yield return new WaitForSeconds(3f);
            SpawnDrops(showerDropPrefabs, showerDropCount);
            NotificationManager.Instance?.Show("Shower passed — fresh materials in Zone 2.");
        }
    }

    private IEnumerator RiftRoutine()
    {
        yield return new WaitForSeconds(180f); // Initial delay

        while (true)
        {
            yield return new WaitForSeconds(riftInterval);

            onRiftStart?.Invoke();
            NotificationManager.Instance?.Show("GRAVITY RIFT INCOMING!\nBrace for impact!", urgent: true);

            Vector2 riftCenter = GetRandomTargetNearPlayer();

            WarningManager.Instance?.ShowWarning(riftCenter, warningDuration + 1f);
            yield return new WaitForSeconds(warningDuration + 1f);

            SpawnMeteorite(riftMeteoritePrefab, riftCenter);
            yield return new WaitForSeconds(2f);

            // Create temporary gravity rift zone at impact
            CreateGravityRift(riftCenter);

            yield return new WaitForSeconds(4f);
            SpawnDrops(riftDropPrefabs, riftDropCount);
            NotificationManager.Instance?.Show("Rift stabilizing — rare materials at the center!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void SpawnMeteorite(GameObject prefab, Vector2 target)
    {
        if (prefab == null) return;

        Vector2 spawnPos = GetOffscreenSpawnPos(target);
        var obj = Instantiate(prefab, spawnPos, Quaternion.identity);

        if (obj.TryGetComponent<Meteorite>(out var m))
            m.SetTarget(target);
    }

    private void CreateGravityRift(Vector2 center)
    {
        // Dynamically create a temporary GravityZone at the impact point
        GameObject rift = new GameObject("TemporaryGravityRift");
        rift.transform.position = center;

        var zone              = rift.AddComponent<GravityZone>();
        zone.gravityType      = GravityState.GravityRift;
        zone.gravityStrength  = 25f;
        zone.gravityDirection = Vector2.zero; // Pulls toward center
        zone.causesDisorientation = true;
        zone.riftSpinForce    = 180f;
        zone.zoneGizmoColor   = new Color(1f, 0.2f, 0.2f, 0.3f);

        var col        = rift.AddComponent<CircleCollider2D>();
        col.isTrigger  = true;
        col.radius     = 12f;

        Destroy(rift, 12f);
    }

    private void SpawnDrops(GameObject[] dropPrefabs, int count)
    {
        if (dropPrefabs == null || dropPrefabs.Length == 0) return;

        Vector2 center = _playerTransform != null
            ? (Vector2)_playerTransform.position + Random.insideUnitCircle * targetRadius * 0.4f
            : Random.insideUnitCircle * targetRadius;

        for (int i = 0; i < count; i++)
        {
            Vector2 pos = center + Random.insideUnitCircle * dropScatterRadius;
            Instantiate(dropPrefabs[Random.Range(0, dropPrefabs.Length)], pos, Quaternion.identity);
        }
    }

    private Vector2 GetRandomTargetNearPlayer()
    {
        Vector2 origin = _playerTransform != null
            ? (Vector2)_playerTransform.position
            : Vector2.zero;
        return origin + Random.insideUnitCircle * (targetRadius * 0.5f);
    }

    private Vector2 GetOffscreenSpawnPos(Vector2 target)
    {
        Vector2 origin = _playerTransform != null
            ? (Vector2)_playerTransform.position
            : Vector2.zero;
        Vector2 dir = (target - origin).normalized;
        return target - dir * spawnOffset;
    }
}
