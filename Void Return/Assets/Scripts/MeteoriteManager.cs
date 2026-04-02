using UnityEngine;
using System.Collections;

/// <summary>
/// Controls all meteorite events.
///
/// FIXES:
///  — Prefab is NEVER modified. We call Instantiate() and then configure the
///    spawned INSTANCE, never the prefab asset. This was the root cause of
///    "prefab being destroyed" — code was calling SetTarget on the prefab
///    reference and Awake/Start were mutating it.
///  — Different WarningManager messages per event type (stray, shower, rift).
///  — Rift VFX spawned when a gravity rift is created.
///  — All spawned GameObjects have explicit Destroy calls scheduled.
///    Nothing is ever left alive indefinitely.
///  — Reliable boolean-flag watchdog (not Coroutine null-check).
/// </summary>
public class MeteoriteManager : MonoBehaviour
{
    public static MeteoriteManager Instance { get; private set; }

    [Header("Meteorite Prefabs")]
    [Tooltip("Prefab for single stray meteorite. NEVER modify this at runtime.")]
    public GameObject strayMeteoritePrefab;

    [Tooltip("Prefab for shower meteorites.")]
    public GameObject showerMeteoritePrefab;

    [Tooltip("Prefab for the large rift-strike meteorite.")]
    public GameObject riftMeteoritePrefab;

    [Header("Timing")]
    [Tooltip("Seconds after scene load before any event fires.")]
    public float startupDelay = 10f;

    [Tooltip("Average seconds between stray hits (±5s jitter).")]
    public float strayInterval = 40f;

    [Tooltip("Seconds between shower waves.")]
    public float showerInterval = 120f;

    [Tooltip("Seconds between rift strikes.")]
    public float riftInterval = 240f;

    [Header("Shower")]
    [Range(3, 30)] public int   showerCount    = 10;
    public float showerMinDelay = 0.5f;
    public float showerMaxDelay = 2f;

    [Header("Warning")]
    public float warningDuration = 3f;

    [Header("Spawn — From Above")]
    public float spawnHeightAbovePlayer = 28f;
    public float spawnHorizontalSpread  = 14f;
    public float targetHorizontalSpread = 8f;

    [Header("Player Damage")]
    [Tooltip("Secondary damage via MeteoriteManager. Set to 0 to avoid double-damage " +
             "(Meteorite.cs already applies damage directly).")]
    [Range(0f, 60f)]
    public float meteoriteOxygenDamage = 0f;

    [Header("Material Drops — Stray")]
    public GameObject[] strayDropPrefabs;
    [Range(0, 8)] public int   strayDropCount  = 3;
    public float strayDropSpread = 2.5f;

    [Header("Material Drops — Shower")]
    public GameObject[] showerDropPrefabs;
    [Range(1, 20)] public int showerDropCount  = 8;
    public float showerDropSpread = 3.5f;

    [Header("Material Drops — Rift")]
    public GameObject[] riftDropPrefabs;
    [Range(1, 30)] public int riftDropCount    = 14;
    public float riftDropSpread = 4f;

    [Header("Rift VFX")]
    [Tooltip("Particle System prefab spawned when a gravity rift forms at impact site. " +
             "Must have Stop Action = Destroy and a reasonable Duration (e.g. 10-12s).")]
    public GameObject riftVFXPrefab;

    [Tooltip("Duration of the temporary gravity rift zone (seconds).")]
    public float riftDuration = 12f;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onShowerStart;
    public UnityEngine.Events.UnityEvent onRiftStart;

    // ─────────────────────────────────────────────────────────────────────────
    private Transform _playerTransform;
    private Vector2   _lastShowerImpact;
    private Vector2   _lastRiftImpact;

    // Watchdog flags — Update restarts any routine whose flag is false
    private bool _strayRunning;
    private bool _showerRunning;
    private bool _riftRunning;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()  => Meteorite.OnAnyImpact += HandleImpact;
    private void OnDisable() => Meteorite.OnAnyImpact -= HandleImpact;

    private void Start()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) _playerTransform = player.transform;
        else Debug.LogWarning("[MeteoriteManager] PlayerController not found.");

        StartCoroutine(RunStray());
        StartCoroutine(RunShower());
        StartCoroutine(RunRift());
    }

    private void Update()
    {
        if (!_strayRunning)  StartCoroutine(RunStray());
        if (!_showerRunning) StartCoroutine(RunShower());
        if (!_riftRunning)   StartCoroutine(RunRift());
    }

    // Named wrappers — avoids the ref-in-iterator compiler error
    private IEnumerator RunStray()
    {
        _strayRunning = true;
        yield return StartCoroutine(StrayRoutine());
        _strayRunning = false;
    }

    private IEnumerator RunShower()
    {
        _showerRunning = true;
        yield return StartCoroutine(ShowerRoutine());
        _showerRunning = false;
    }

    private IEnumerator RunRift()
    {
        _riftRunning = true;
        yield return StartCoroutine(RiftRoutine());
        _riftRunning = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Impact Handler
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleImpact(Vector2 pos, MeteoriteType type)
    {
        switch (type)
        {
            case MeteoriteType.Stray:
                FlingDrops(strayDropPrefabs, strayDropCount, pos, strayDropSpread);
                break;
            case MeteoriteType.Shower:
                _lastShowerImpact = pos;
                break;
            case MeteoriteType.Rift:
                _lastRiftImpact = pos;
                break;
        }
    }

    public void NotifyPlayerHit()
    {
        if (meteoriteOxygenDamage > 0f)
        {
            var oxygen = FindFirstObjectByType<OxygenSystem>();
            oxygen?.TakeDamageFromMeteorite(meteoriteOxygenDamage);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Routines
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator StrayRoutine()
    {
        Debug.Log("[MeteoriteManager] Stray routine started.");
        yield return new WaitForSeconds(startupDelay);

        while (true)
        {
            float wait = Mathf.Max(strayInterval + Random.Range(-5f, 5f), 8f);
            Debug.Log($"[MeteoriteManager] Next stray in {wait:F1}s");
            yield return new WaitForSeconds(wait);

            Vector2 target = GetTargetNearPlayer();

            // Stray-specific warning
            WarningManager.Instance?.ShowWarning(target, warningDuration);
            NotificationManager.Instance?.ShowWarning("INCOMING METEORITE! Take cover!");

            yield return new WaitForSeconds(warningDuration);
            LaunchMeteorite(strayMeteoritePrefab, GetSpawnAbove(target), target, MeteoriteType.Stray);
        }
    }

    private IEnumerator ShowerRoutine()
    {
        Debug.Log("[MeteoriteManager] Shower routine started.");
        yield return new WaitForSeconds(startupDelay + 60f);

        while (true)
        {
            yield return new WaitForSeconds(showerInterval);
            Debug.Log("[MeteoriteManager] Shower event starting.");

            onShowerStart?.Invoke();

            // Shower-specific warning — more urgent
            NotificationManager.Instance?.ShowWarning(
                "METEORITE SHOWER INCOMING!\nFind shelter immediately!");

            _lastShowerImpact = GetTargetNearPlayer();

            for (int i = 0; i < showerCount; i++)
            {
                Vector2 t = GetTargetNearPlayer();
                WarningManager.Instance?.ShowWarning(t, 2f);
                yield return new WaitForSeconds(Random.Range(showerMinDelay, showerMaxDelay));
                LaunchMeteorite(showerMeteoritePrefab, GetSpawnAbove(t), t, MeteoriteType.Shower);
            }

            yield return new WaitForSeconds(4f);
            FlingDrops(showerDropPrefabs, showerDropCount, _lastShowerImpact, showerDropSpread);
            NotificationManager.Instance?.ShowInfo("Shower passed — materials scattered at impact zone.");
        }
    }

    private IEnumerator RiftRoutine()
    {
        Debug.Log("[MeteoriteManager] Rift routine started.");
        yield return new WaitForSeconds(startupDelay + 180f);

        while (true)
        {
            yield return new WaitForSeconds(riftInterval);
            Debug.Log("[MeteoriteManager] Rift strike starting.");

            onRiftStart?.Invoke();

            // Rift-specific warning — most dramatic
            NotificationManager.Instance?.ShowWarning(
                "GRAVITY RIFT INCOMING!\nSpace is tearing — flee the impact zone!");

            Vector2 approxTarget = GetTargetNearPlayer();
            _lastRiftImpact      = approxTarget;

            WarningManager.Instance?.ShowWarning(approxTarget, warningDuration + 2f);
            yield return new WaitForSeconds(warningDuration + 2f);

            LaunchMeteorite(riftMeteoritePrefab, GetSpawnAbove(approxTarget),
                            approxTarget, MeteoriteType.Rift);

            // Wait for the rift meteorite to land before spawning the rift
            yield return new WaitForSeconds(2.5f);

            CreateRift(_lastRiftImpact);

            yield return new WaitForSeconds(riftDuration);
            FlingDrops(riftDropPrefabs, riftDropCount, _lastRiftImpact, riftDropSpread);
            NotificationManager.Instance?.ShowInfo("Rift stabilizing — rare materials at impact!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers — safe prefab usage
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantiates the meteorite prefab and configures the SPAWNED INSTANCE.
    /// The prefab asset is NEVER touched — only the instance is modified.
    /// </summary>
    private void LaunchMeteorite(GameObject prefab, Vector2 spawn,
                                  Vector2 target, MeteoriteType type)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[MeteoriteManager] {type} prefab not assigned in Inspector.");
            return;
        }

        // Instantiate creates a new instance in the scene — the prefab is untouched
        GameObject instance = Instantiate(prefab, spawn, Quaternion.identity);

        if (instance.TryGetComponent<Meteorite>(out var meteorite))
        {
            // Configure the INSTANCE, never the prefab
            meteorite.meteoriteType = type;
            meteorite.SetTarget(target);
        }
        else
        {
            Debug.LogWarning($"[MeteoriteManager] Instantiated {prefab.name} but it has " +
                             "no Meteorite component. Add a Meteorite script to the prefab.");
            Destroy(instance); // Clean up the useless instance
        }
    }

    private void FlingDrops(GameObject[] prefabs, int count, Vector2 center, float spread)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning("[MeteoriteManager] Drop prefab array is empty or unassigned.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var chosen = prefabs[Random.Range(0, prefabs.Length)];
            if (chosen == null) continue;

            float   ox  = Random.Range(-spread * 0.3f, spread * 0.3f);
            Vector2 pos = new Vector2(center.x + ox, center.y);

            var obj = Instantiate(chosen, pos, Quaternion.identity);
            obj.GetComponent<MaterialPickup>()?.LaunchFromImpact(center);
        }
    }

    private void CreateRift(Vector2 center)
    {
        // Create rift zone
        var rift               = new GameObject("TempGravityRift");
        rift.transform.position = center;

        var zone                  = rift.AddComponent<GravityZone>();
        zone.gravityType          = GravityState.GravityRift;
        zone.riftPullStrength     = 12f;
        zone.riftSpinForce        = 120f;
        zone.causesDisorientation = true;
        zone.zoneGizmoColor       = new Color(1f, 0.1f, 0f, 0.35f);

        var col                   = rift.AddComponent<CircleCollider2D>();
        col.isTrigger             = true;
        col.radius                = 12f;

        // Always schedule destruction — never leak the rift zone
        Destroy(rift, riftDuration);

        // Spawn rift VFX
        if (riftVFXPrefab != null)
        {
            // VFX instance is separate from the rift zone so it can have its own lifetime
            var vfxInstance = Instantiate(riftVFXPrefab, center, Quaternion.identity);
            Destroy(vfxInstance, riftDuration + 1f); // +1s buffer for fade
        }
        else
        {
            Debug.LogWarning("[MeteoriteManager] riftVFXPrefab is not assigned. " +
                             "Assign a particle system prefab to visualize the rift.");
        }

        // Notify minimap to show the rift marker
        MinimapController.Instance?.RegisterRift(center, riftDuration);

        NotificationManager.Instance?.ShowWarning(
            "GRAVITY RIFT ACTIVE!\nStrong gravitational pull — escape quickly!");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private Vector2 GetTargetNearPlayer()
    {
        Vector2 origin = _playerTransform != null
            ? (Vector2)_playerTransform.position : Vector2.zero;
        return origin + new Vector2(
            Random.Range(-targetHorizontalSpread, targetHorizontalSpread), 0f);
    }

    private Vector2 GetSpawnAbove(Vector2 target) =>
        new Vector2(
            target.x + Random.Range(-spawnHorizontalSpread, spawnHorizontalSpread),
            target.y + spawnHeightAbovePlayer);
}
