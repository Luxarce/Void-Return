using UnityEngine;
using System.Collections;

/// <summary>
/// Controls meteorite events.
///
/// RIFT FIX — ROOT CAUSE:
///  The zone check used "continue" inside a while(true) coroutine.
///  When the player was not in Zone 3, the coroutine called "continue"
///  which jumped back to the top of while(true) WITHOUT yielding.
///  This created an infinite tight loop that froze the coroutine (and Unity)
///  or the coroutine was silently aborted by the engine.
///
///  Fix: The zone check now yields WaitForSeconds(checkAgainInterval) before
///  retrying, so the routine always sleeps between attempts.
///
/// SHOWER FIX:
///  Shower now fires in Zone 2 AND Zone 3.
///
/// ZONE ENTRY NOTIFICATION FIX:
///  MeteoriteManager zone is initialized to 0 (not 1). ZoneTrigger.SetPlayerZone
///  must call MeteoriteManager.Instance?.SetPlayerZone() on entry.
///  If ZoneTrigger has Is Default Zone = ON, it calls SetPlayerZone(1) at Start.
/// </summary>
public class MeteoriteManager : MonoBehaviour
{
    public static MeteoriteManager Instance { get; private set; }

    [Header("Meteorite Prefabs")]
    public GameObject strayMeteoritePrefab;
    public GameObject showerMeteoritePrefab;
    public GameObject riftMeteoritePrefab;

    [Header("Timing")]
    public float startupDelay   = 10f;
    public float strayInterval  = 40f;
    public float showerInterval = 120f;
    public float riftInterval   = 60f;

    [Tooltip("How often (seconds) to check if the player has entered the required zone " +
             "when a shower/rift event is waiting. Prevents tight infinite loop.")]
    public float zoneCheckRetryInterval = 10f;

    [Header("Shower")]
    [Range(3, 30)] public int showerCount    = 10;
    public float showerMinDelay = 0.5f;
    public float showerMaxDelay = 2f;

    [Header("Warning")]
    public float warningDuration = 3f;

    [Header("Spawn")]
    public float spawnHeightAbovePlayer = 28f;
    public float spawnHorizontalSpread  = 14f;
    public float targetHorizontalSpread = 8f;

    [Header("Player Damage")]
    [Range(0f, 60f)] public float meteoriteOxygenDamage = 0f;

    [Header("Drops — Stray")]
    public GameObject[] strayDropPrefabs;
    [Range(0, 8)] public int strayDropCount = 3;
    public float strayDropSpread = 2.5f;

    [Header("Drops — Shower")]
    public GameObject[] showerDropPrefabs;
    [Range(1, 20)] public int showerDropCount = 8;
    public float showerDropSpread = 3.5f;

    [Header("Drops — Rift")]
    public GameObject[] riftDropPrefabs;
    [Range(1, 30)] public int riftDropCount = 14;
    public float riftDropSpread = 4f;

    [Header("Rift Zone")]
    [Tooltip("VFX prefab spawned at rift site. Root PS must have Stop Action = Destroy.")]
    public GameObject riftVFXPrefab;
    public GameObject showerRiftVFXPrefab;
    public float riftDuration     = 12f;
    public float riftRadius       = 12f;
    public float riftPullStrength = 12f;
    public float riftSpinForce    = 120f;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onShowerStart;
    public UnityEngine.Events.UnityEvent onRiftStart;

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Zone the player is currently in.
    /// 0 = not yet set, 1 = Zone 1, 2 = Zone 2, 3 = Zone 3.
    /// ZoneTrigger calls SetPlayerZone() on entry.
    /// </summary>
    public int CurrentPlayerZone { get; private set; } = 0;

    public void SetPlayerZone(int zone)
    {
        CurrentPlayerZone = zone;
        Debug.Log($"[MeteoriteManager] Player zone updated to {zone}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    private Transform _playerTransform;
    private Vector2   _lastShowerImpact;
    private Vector2   _lastRiftImpact;
    private bool      _strayRunning;
    private bool      _showerRunning;
    private bool      _riftRunning;

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

        ValidatePrefabs();

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

    private void ValidatePrefabs()
    {
        if (strayMeteoritePrefab  == null) Debug.LogError("[MeteoriteManager] strayMeteoritePrefab not assigned.");
        if (showerMeteoritePrefab == null) Debug.LogWarning("[MeteoriteManager] showerMeteoritePrefab not assigned.");
        if (riftMeteoritePrefab   == null) Debug.LogWarning("[MeteoriteManager] riftMeteoritePrefab not assigned.");
        if (riftVFXPrefab         == null)
            Debug.LogError("[MeteoriteManager] riftVFXPrefab not assigned. " +
                           "Create a Particle System prefab with Stop Action = Destroy and assign it here.");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator RunStray()  { _strayRunning  = true; yield return StartCoroutine(StrayRoutine());  _strayRunning  = false; }
    private IEnumerator RunShower() { _showerRunning = true; yield return StartCoroutine(ShowerRoutine()); _showerRunning = false; }
    private IEnumerator RunRift()   { _riftRunning   = true; yield return StartCoroutine(RiftRoutine());   _riftRunning   = false; }

    // ─────────────────────────────────────────────────────────────────────────

    private void HandleImpact(Vector2 pos, MeteoriteType type)
    {
        switch (type)
        {
            case MeteoriteType.Stray:   FlingDrops(strayDropPrefabs, strayDropCount, pos, strayDropSpread); break;
            case MeteoriteType.Shower:  _lastShowerImpact = pos; break;
            case MeteoriteType.Rift:    _lastRiftImpact   = pos; break;
        }
    }

    public void NotifyPlayerHit()
    {
        if (meteoriteOxygenDamage > 0f)
            FindFirstObjectByType<OxygenSystem>()?.TakeDamageFromMeteorite(meteoriteOxygenDamage);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event routines
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator StrayRoutine()
    {
        // Stray fires in any zone
        Debug.Log("[MeteoriteManager] Stray routine started.");
        yield return new WaitForSeconds(startupDelay);

        while (true)
        {
            float wait = Mathf.Max(strayInterval + Random.Range(-5f, 5f), 8f);
            Debug.Log($"[MeteoriteManager] Next stray in {wait:F1}s");
            yield return new WaitForSeconds(wait);

            Vector2 target = GetTargetNearPlayer();
            WarningManager.Instance?.ShowWarning(target, warningDuration);
            NotificationManager.Instance?.ShowWarning("INCOMING METEORITE! Take cover!");
            yield return new WaitForSeconds(warningDuration);
            LaunchMeteorite(strayMeteoritePrefab, GetSpawnAbove(target), target, MeteoriteType.Stray);
        }
    }

    private IEnumerator ShowerRoutine()
    {
        // Shower fires in Zone 2 AND Zone 3
        Debug.Log("[MeteoriteManager] Shower routine started.");
        yield return new WaitForSeconds(startupDelay + 60f);

        while (true)
        {
            yield return new WaitForSeconds(showerInterval);
            Debug.Log($"[MeteoriteManager] Shower timer done. PlayerZone={CurrentPlayerZone}");

            // Wait until player is in Zone 2 or 3 — yield between checks to avoid tight loop
            while (CurrentPlayerZone < 2)
            {
                Debug.Log("[MeteoriteManager] Shower waiting — player not in Zone 2+. Checking again in " +
                          $"{zoneCheckRetryInterval}s. CurrentZone={CurrentPlayerZone}");
                yield return new WaitForSeconds(zoneCheckRetryInterval);
            }

            Debug.Log("[MeteoriteManager] Shower event starting in Zone " + CurrentPlayerZone);
            onShowerStart?.Invoke();
            NotificationManager.Instance?.ShowWarning("METEORITE SHOWER INCOMING!\nFind shelter immediately!");

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
            NotificationManager.Instance?.ShowInfo("Shower passed — materials at impact zone.");
        }
    }

    private IEnumerator RiftRoutine()
    {
        // Rift fires only in Zone 3
        Debug.Log("[MeteoriteManager] Rift routine started.");
        yield return new WaitForSeconds(startupDelay + 120f);

        while (true)
        {
            yield return new WaitForSeconds(riftInterval);
            Debug.Log($"[MeteoriteManager] Rift timer done. PlayerZone={CurrentPlayerZone}");

            // Wait until player is in Zone 3 — yield between checks to avoid tight loop
            while (CurrentPlayerZone < 3)
            {
                Debug.Log("[MeteoriteManager] Rift waiting — player not in Zone 3. Checking again in " +
                          $"{zoneCheckRetryInterval}s. CurrentZone={CurrentPlayerZone}");
                yield return new WaitForSeconds(zoneCheckRetryInterval);
            }

            Debug.Log("[MeteoriteManager] Rift strike starting in Zone 3.");
            onRiftStart?.Invoke();
            NotificationManager.Instance?.ShowWarning("GRAVITY RIFT INCOMING!\nSpace is tearing — flee!");

            Vector2 target  = GetTargetNearPlayer();
            _lastRiftImpact = target;

            WarningManager.Instance?.ShowWarning(target, warningDuration + 2f);
            yield return new WaitForSeconds(warningDuration + 2f);

            if (riftMeteoritePrefab != null)
                LaunchMeteorite(riftMeteoritePrefab, GetSpawnAbove(target), target, MeteoriteType.Rift);

            yield return new WaitForSeconds(2.5f);

            Vector2 riftCenter = riftMeteoritePrefab != null ? _lastRiftImpact : target;
            Debug.Log($"[MeteoriteManager] Creating rift at {riftCenter}");
            CreateRift(riftCenter, MeteoriteType.Rift);

            yield return new WaitForSeconds(riftDuration);
            FlingDrops(riftDropPrefabs, riftDropCount, riftCenter, riftDropSpread);
            NotificationManager.Instance?.ShowInfo("Rift stabilizing — rare materials at impact!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void LaunchMeteorite(GameObject prefab, Vector2 spawn, Vector2 target, MeteoriteType type)
    {
        if (prefab == null)
        {
            Debug.LogError($"[MeteoriteManager] {type} prefab is null."); return;
        }
        var instance = Instantiate(prefab, spawn, Quaternion.identity);
        if (instance.TryGetComponent<Meteorite>(out var m))
        {
            m.meteoriteType = type;
            m.SetTarget(target);
            MinimapController.Instance?.RegisterMeteorite(m);
        }
        else { Debug.LogWarning($"[MeteoriteManager] {prefab.name} missing Meteorite component."); Destroy(instance); }
    }

    private void CreateRift(Vector2 center, MeteoriteType type)
    {
        Debug.Log($"[MeteoriteManager] CreateRift executing at {center}");

        var rift = new GameObject("TempGravityRift");
        rift.transform.position = center;
        var zone = rift.AddComponent<GravityZone>();
        zone.gravityType          = GravityState.GravityRift;
        zone.riftPullStrength     = riftPullStrength;
        zone.riftSpinForce        = riftSpinForce;
        zone.causesDisorientation = true;
        zone.zoneGizmoColor       = new Color(1f, 0.1f, 0f, 0.35f);
        var col     = rift.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = riftRadius;
        Destroy(rift, riftDuration);

        GameObject chosenVFX = (type == MeteoriteType.Shower && showerRiftVFXPrefab != null)
            ? showerRiftVFXPrefab : riftVFXPrefab;

        if (chosenVFX != null)
        {
            var vfx = Instantiate(chosenVFX, center, Quaternion.identity);
            Destroy(vfx, riftDuration + 1f);
            Debug.Log($"[MeteoriteManager] Rift VFX spawned: {chosenVFX.name}");
        }
        else
        {
            Debug.LogError("[MeteoriteManager] riftVFXPrefab is null — no visual spawned. " +
                           "Assign a Particle System prefab to MeteoriteManager > Rift VFX Prefab.");
        }

        MinimapController.Instance?.RegisterRift(center, riftDuration);
        NotificationManager.Instance?.ShowWarning("GRAVITY RIFT ACTIVE! Escape quickly!");
    }

    private void FlingDrops(GameObject[] prefabs, int count, Vector2 center, float spread)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            var chosen = prefabs[Random.Range(0, prefabs.Length)];
            if (chosen == null) continue;
            float ox  = Random.Range(-spread * 0.3f, spread * 0.3f);
            var   obj = Instantiate(chosen, new Vector2(center.x + ox, center.y), Quaternion.identity);
            obj.GetComponent<MaterialPickup>()?.LaunchFromImpact(center);
        }
    }

    private Vector2 GetTargetNearPlayer()
    {
        Vector2 o = _playerTransform != null ? (Vector2)_playerTransform.position : Vector2.zero;
        return o + new Vector2(Random.Range(-targetHorizontalSpread, targetHorizontalSpread), 0f);
    }

    private Vector2 GetSpawnAbove(Vector2 t) =>
        new Vector2(t.x + Random.Range(-spawnHorizontalSpread, spawnHorizontalSpread),
                    t.y + spawnHeightAbovePlayer);
}
