using UnityEngine;
using System.Collections;

/// <summary>
/// Controls all meteorite events and material drops after impacts.
///
/// FIX NOTES:
///  — Meteorites now ALWAYS spawn from ABOVE the player (positive Y offset).
///    Previous code used a random off-screen position that could come from
///    any direction including below, left, or right — which looked wrong for
///    a space-from-above feel.
///  — Stray interval now uses a guaranteed minimum wait so events cannot
///    stack or fire before the scene is ready. A 10-second startup delay
///    prevents events firing before the player has control.
///  — Drop prefabs are now validated before Instantiate so a missing
///    reference can't silently skip all drops.
///  — Shower and Rift routines now log to the Console so you can confirm
///    they are running (visible in Window → Console during Play mode).
///  — strayDropPrefabs, showerDropPrefabs, riftDropPrefabs must be assigned
///    in the Inspector — see SETUP section below.
///
/// SETUP:
///  1. Attach to an empty GameObject named 'MeteoriteManager'.
///  2. Assign Meteorite prefabs for each strike type.
///  3. Assign drop prefab arrays with MaterialPickup prefabs.
///  4. Adjust intervals for testing (set strayInterval to 5 for quick testing).
/// </summary>
public class MeteoriteManager : MonoBehaviour
{
    [Header("Meteorite Prefabs")]
    [Tooltip("Small single-hit meteorite.")]
    public GameObject strayMeteoritePrefab;

    [Tooltip("Faster shower meteorite (used in wave events).")]
    public GameObject showerMeteoritePrefab;

    [Tooltip("Large rift-strike meteorite.")]
    public GameObject riftMeteoritePrefab;

    [Header("Timing")]
    [Tooltip("Seconds after scene load before the first event can fire. " +
             "Gives the player time to get oriented.")]
    public float startupDelay = 10f;

    [Tooltip("Seconds between stray hits (approximate — adds ±5s random spread).")]
    public float strayInterval = 40f;

    [Tooltip("Seconds between shower events.")]
    public float showerInterval = 120f;

    [Tooltip("Seconds between rift strikes.")]
    public float riftInterval = 240f;

    [Header("Shower")]
    [Range(5, 30)]
    [Tooltip("Number of meteorites in one shower wave.")]
    public int showerCount = 12;

    [Tooltip("Minimum delay between individual shower meteorites.")]
    public float showerMinDelay = 0.5f;

    [Tooltip("Maximum delay between individual shower meteorites.")]
    public float showerMaxDelay = 2f;

    [Header("Warning")]
    [Tooltip("Seconds of warning flash before a stray meteorite lands.")]
    public float warningDuration = 3f;

    [Header("Spawn — FROM ABOVE")]
    [Tooltip("How far above the player the meteorite spawns (world units). " +
             "Meteorites always come from above — adjust this to match your camera height.")]
    public float spawnHeightAbovePlayer = 25f;

    [Tooltip("Horizontal random spread of the spawn position (world units). " +
             "A value of 10 means meteorites can spawn up to 10 units left or right.")]
    public float spawnHorizontalSpread = 12f;

    [Tooltip("How far the target point is horizontally offset from the player. " +
             "0 = always targets player directly. Increase for near-misses.")]
    public float targetHorizontalSpread = 6f;

    [Header("Material Drops — Stray Hits")]
    [Tooltip("MaterialPickup prefabs dropped after a stray hit. " +
             "Assign Zone 1 common materials: MetalScrap, Bolt, Glass, Foam prefabs.")]
    public GameObject[] strayDropPrefabs;

    [Tooltip("Number of material drops per stray hit.")]
    [Range(0, 8)]
    public int strayDropCount = 3;

    [Header("Material Drops — Shower Events")]
    [Tooltip("Mid-tier materials (CircuitBoard, Titanium, Lens prefabs).")]
    public GameObject[] showerDropPrefabs;

    [Tooltip("Number of drops after the entire shower ends.")]
    [Range(1, 20)]
    public int showerDropCount = 8;

    [Header("Material Drops — Rift Strikes")]
    [Tooltip("Rare materials (FuelCell, Coolant, HeatShield, TitaniumRod prefabs).")]
    public GameObject[] riftDropPrefabs;

    [Tooltip("Number of drops after a rift strike.")]
    [Range(1, 30)]
    public int riftDropCount = 14;

    [Tooltip("Radius around the impact point in which drops scatter.")]
    public float dropScatterRadius = 5f;

    [Header("Events (optional)")]
    public UnityEngine.Events.UnityEvent onShowerStart;
    public UnityEngine.Events.UnityEvent onRiftStart;

    // ─────────────────────────────────────────────────────────────────────────
    private Transform _playerTransform;

    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        Meteorite.OnAnyImpact += HandleImpact;
    }

    private void OnDisable()
    {
        Meteorite.OnAnyImpact -= HandleImpact;
    }

    private void Start()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            _playerTransform = player.transform;
        else
            Debug.LogWarning("[MeteoriteManager] PlayerController not found. " +
                             "Meteorites will spawn at world origin instead.");

        StartCoroutine(StrayRoutine());
        StartCoroutine(ShowerRoutine());
        StartCoroutine(RiftRoutine());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Impact handler — receives world position and type from Meteorite.cs
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleImpact(Vector2 pos, MeteoriteType type)
    {
        if (type == MeteoriteType.Stray)
            SpawnDrops(strayDropPrefabs, strayDropCount, pos);
        // Shower and Rift drops are spawned in bulk after their events complete
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Strike Routines
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator StrayRoutine()
    {
        yield return new WaitForSeconds(startupDelay);

        while (true)
        {
            float wait = strayInterval + Random.Range(-5f, 5f);
            yield return new WaitForSeconds(Mathf.Max(wait, 8f)); // never less than 8s

            Vector2 target = GetTargetNearPlayer();
            Vector2 spawn  = GetSpawnPositionAbove(target);

            WarningManager.Instance?.ShowWarning(target, warningDuration);
            Debug.Log($"[MeteoriteManager] Stray hit incoming → target {target}");

            yield return new WaitForSeconds(warningDuration);
            LaunchMeteorite(strayMeteoritePrefab, spawn, target, MeteoriteType.Stray);
        }
    }

    private IEnumerator ShowerRoutine()
    {
        yield return new WaitForSeconds(startupDelay + 60f); // extra delay for first shower

        while (true)
        {
            yield return new WaitForSeconds(showerInterval);

            onShowerStart?.Invoke();
            Debug.Log("[MeteoriteManager] Shower event starting.");
            NotificationManager.Instance?.Show("METEORITE SHOWER INCOMING!\nTake cover!", urgent: true);

            Vector2 lastTarget = GetTargetNearPlayer();
            for (int i = 0; i < showerCount; i++)
            {
                Vector2 target = GetTargetNearPlayer();
                lastTarget     = target;
                Vector2 spawn  = GetSpawnPositionAbove(target);

                WarningManager.Instance?.ShowWarning(target, 2f);
                yield return new WaitForSeconds(Random.Range(showerMinDelay, showerMaxDelay));
                LaunchMeteorite(showerMeteoritePrefab, spawn, target, MeteoriteType.Shower);
            }

            yield return new WaitForSeconds(5f);
            SpawnDrops(showerDropPrefabs, showerDropCount, lastTarget);
            NotificationManager.Instance?.Show("Shower passed — fresh materials scattered.");
        }
    }

    private IEnumerator RiftRoutine()
    {
        yield return new WaitForSeconds(startupDelay + 180f);

        while (true)
        {
            yield return new WaitForSeconds(riftInterval);

            onRiftStart?.Invoke();
            Debug.Log("[MeteoriteManager] Rift strike event starting.");
            NotificationManager.Instance?.Show("GRAVITY RIFT INCOMING!\nBrace for impact!", urgent: true);

            Vector2 riftCenter = GetTargetNearPlayer();
            Vector2 spawn      = GetSpawnPositionAbove(riftCenter);

            WarningManager.Instance?.ShowWarning(riftCenter, warningDuration + 2f);
            yield return new WaitForSeconds(warningDuration + 2f);

            LaunchMeteorite(riftMeteoritePrefab, spawn, riftCenter, MeteoriteType.Rift);
            yield return new WaitForSeconds(2f);

            CreateRift(riftCenter);
            yield return new WaitForSeconds(6f);

            SpawnDrops(riftDropPrefabs, riftDropCount, riftCenter);
            NotificationManager.Instance?.Show("Rift stabilizing — rare materials at impact!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void LaunchMeteorite(GameObject prefab, Vector2 spawn,
                                  Vector2 target, MeteoriteType type)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[MeteoriteManager] Prefab for {type} is not assigned.");
            return;
        }
        var obj = Instantiate(prefab, spawn, Quaternion.identity);
        if (obj.TryGetComponent<Meteorite>(out var m))
        {
            m.meteoriteType = type;
            m.SetTarget(target);
        }
    }

    /// <summary>
    /// Returns a point near the player with a small random horizontal offset.
    /// This is the LANDING point the meteorite aims at.
    /// </summary>
    private Vector2 GetTargetNearPlayer()
    {
        Vector2 playerPos = _playerTransform != null
            ? (Vector2)_playerTransform.position
            : Vector2.zero;

        float offsetX = Random.Range(-targetHorizontalSpread, targetHorizontalSpread);
        return new Vector2(playerPos.x + offsetX, playerPos.y);
    }

    /// <summary>
    /// Returns a spawn position ABOVE the target point.
    /// Meteorites ALWAYS come from above — they never spawn from the sides or below.
    /// </summary>
    private Vector2 GetSpawnPositionAbove(Vector2 target)
    {
        float spawnX = target.x + Random.Range(-spawnHorizontalSpread, spawnHorizontalSpread);
        float spawnY = target.y + spawnHeightAbovePlayer;
        return new Vector2(spawnX, spawnY);
    }

    private void SpawnDrops(GameObject[] prefabs, int count, Vector2 center)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning("[MeteoriteManager] Drop prefab array is empty or unassigned. " +
                             "Assign MaterialPickup prefabs in the Inspector.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var chosen = prefabs[Random.Range(0, prefabs.Length)];
            if (chosen == null) continue;

            Vector2 pos = center + Random.insideUnitCircle * dropScatterRadius;
            Instantiate(chosen, pos, Quaternion.identity);
        }
    }

    private void CreateRift(Vector2 center)
    {
        var rift               = new GameObject("TemporaryGravityRift");
        rift.transform.position = center;
        var zone               = rift.AddComponent<GravityZone>();
        zone.gravityType       = GravityState.GravityRift;
        zone.gravityStrength   = 25f;
        zone.causesDisorientation = true;
        zone.riftSpinForce     = 180f;
        var col                = rift.AddComponent<CircleCollider2D>();
        col.isTrigger          = true;
        col.radius             = 12f;
        Destroy(rift, 12f);
    }
}
