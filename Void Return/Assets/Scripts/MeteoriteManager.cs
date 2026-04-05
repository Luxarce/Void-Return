using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controls meteorite events and rift spawning.
///
/// RIFT VFX FIXES:
///  FIX 1 — GravityZone RequireComponent:
///   CreateRift() was adding GravityZone BEFORE CircleCollider2D, causing
///   the same NullReferenceException as ZoneGravitySpawner.
///   Fixed: CircleCollider2D is added first.
///
///  FIX 2 — Procedural ParticleSystem invisible in URP:
///   Unity's default ParticleSystem uses the built-in "Particles/Standard Unlit"
///   shader which is NOT included in Universal Render Pipeline projects.
///   Without an explicit URP material, particles render as invisible pink squares
///   or nothing at all, depending on the URP version.
///   Fixed: The procedural VFX now uses animated SpriteRenderer quads instead
///   of ParticleSystem. These use no shader at all (unlit sprites), are
///   100% URP-compatible, and require zero setup.
///
///  FIX 3 — Prefab VFX invisible in URP:
///   If you assign a prefab created with built-in shaders, same issue.
///   The procedural sprite-based approach bypasses this entirely.
/// </summary>
public class MeteoriteManager : MonoBehaviour
{
    public static MeteoriteManager Instance { get; private set; }

    [Header("Meteorite Prefabs")]
    public GameObject strayMeteoritePrefab;
    public GameObject showerMeteoritePrefab;
    public GameObject riftMeteoritePrefab;

    [Header("Timing")]
    public float startupDelay          = 10f;
    public float strayInterval         = 40f;
    public float showerInterval        = 120f;
    public float riftInterval          = 60f;
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

    [Header("Rift VFX Prefab (optional — built-in fallback used if null)")]
    [Tooltip("Assign a Particle System prefab here if you want custom VFX. " +
             "If left empty, a sprite-based fallback VFX is generated in code. " +
             "IMPORTANT: If using URP, the prefab material MUST use a URP shader " +
             "(Universal Render Pipeline/Particles/Lit or Unlit).")]
    public GameObject riftVFXPrefab;
    public GameObject showerRiftVFXPrefab;

    [Header("Rift Zone")]
    public float riftDuration     = 12f;
    public float riftRadius       = 12f;
    public float riftPullStrength = 12f;
    public float riftSpinForce    = 120f;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onShowerStart;
    public UnityEngine.Events.UnityEvent onRiftStart;

    // ── Prefab backups ─────────────────────────────────────────────────────
    private GameObject _strayBackup;
    private GameObject _showerBackup;
    private GameObject _riftBackup;
    private GameObject _riftVFXBackup;

    // ─────────────────────────────────────────────────────────────────────────
    public int CurrentPlayerZone { get; private set; } = 0;
    public void SetPlayerZone(int zone)
    {
        CurrentPlayerZone = zone;
        Debug.Log($"[MeteoriteManager] Player zone = {zone}");
    }

    private Transform _playerTransform;
    private Vector2   _lastShowerImpact;
    private Vector2   _lastRiftImpact;
    private bool _strayRunning, _showerRunning, _riftRunning;

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

        _strayBackup   = strayMeteoritePrefab;
        _showerBackup  = showerMeteoritePrefab;
        _riftBackup    = riftMeteoritePrefab;
        _riftVFXBackup = riftVFXPrefab;

        ValidatePrefabs();
        InvokeRepeating(nameof(RestoreAndValidatePrefabs), 5f, 5f);

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

    private void RestoreAndValidatePrefabs()
    {
        if (strayMeteoritePrefab  == null && _strayBackup   != null) { strayMeteoritePrefab  = _strayBackup;   Debug.LogWarning("[MeteoriteManager] strayMeteoritePrefab restored from backup."); }
        if (showerMeteoritePrefab == null && _showerBackup  != null) { showerMeteoritePrefab = _showerBackup;  Debug.LogWarning("[MeteoriteManager] showerMeteoritePrefab restored from backup."); }
        if (riftMeteoritePrefab   == null && _riftBackup    != null) { riftMeteoritePrefab   = _riftBackup;    Debug.LogWarning("[MeteoriteManager] riftMeteoritePrefab restored from backup."); }
        if (riftVFXPrefab         == null && _riftVFXBackup != null) { riftVFXPrefab         = _riftVFXBackup; Debug.LogWarning("[MeteoriteManager] riftVFXPrefab restored from backup."); }
        ValidatePrefabs();
    }

    private void ValidatePrefabs()
    {
        if (strayMeteoritePrefab  == null) Debug.LogError("[MeteoriteManager] strayMeteoritePrefab not assigned.");
        if (showerMeteoritePrefab == null) Debug.LogWarning("[MeteoriteManager] showerMeteoritePrefab not assigned.");
        if (riftMeteoritePrefab   == null) Debug.LogWarning("[MeteoriteManager] riftMeteoritePrefab not assigned — rift zone still creates.");
        // riftVFXPrefab being null is fine — procedural fallback runs instead
    }

    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator RunStray()  { _strayRunning  = true; yield return StartCoroutine(StrayRoutine());  _strayRunning  = false; }
    private IEnumerator RunShower() { _showerRunning = true; yield return StartCoroutine(ShowerRoutine()); _showerRunning = false; }
    private IEnumerator RunRift()   { _riftRunning   = true; yield return StartCoroutine(RiftRoutine());   _riftRunning   = false; }

    private void HandleImpact(Vector2 pos, MeteoriteType type)
    {
        switch (type)
        {
            case MeteoriteType.Stray:  FlingDrops(strayDropPrefabs, strayDropCount, pos, strayDropSpread);  break;
            case MeteoriteType.Shower: _lastShowerImpact = pos; break;
            case MeteoriteType.Rift:   _lastRiftImpact   = pos; break;
        }
    }

    public void NotifyPlayerHit()
    {
        if (meteoriteOxygenDamage > 0f)
            FindFirstObjectByType<OxygenSystem>()?.TakeDamageFromMeteorite(meteoriteOxygenDamage);
    }

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
            while (CurrentPlayerZone < 2) { yield return new WaitForSeconds(zoneCheckRetryInterval); }
            Debug.Log("[MeteoriteManager] Shower starting.");
            onShowerStart?.Invoke();
            NotificationManager.Instance?.ShowWarning("METEORITE SHOWER INCOMING!\nFind shelter!");
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
        Debug.Log("[MeteoriteManager] Rift routine started.");
        yield return new WaitForSeconds(startupDelay + 120f);
        while (true)
        {
            yield return new WaitForSeconds(riftInterval);
            while (CurrentPlayerZone < 3) { yield return new WaitForSeconds(zoneCheckRetryInterval); }
            Debug.Log("[MeteoriteManager] Rift strike starting.");
            onRiftStart?.Invoke();
            NotificationManager.Instance?.ShowWarning("GRAVITY RIFT INCOMING!\nFlee the impact zone!");
            Vector2 target  = GetTargetNearPlayer(); _lastRiftImpact = target;
            WarningManager.Instance?.ShowWarning(target, warningDuration + 2f);
            yield return new WaitForSeconds(warningDuration + 2f);
            if (riftMeteoritePrefab != null)
                LaunchMeteorite(riftMeteoritePrefab, GetSpawnAbove(target), target, MeteoriteType.Rift);
            yield return new WaitForSeconds(2.5f);
            Vector2 rc = (riftMeteoritePrefab != null) ? _lastRiftImpact : target;
            Debug.Log($"[MeteoriteManager] Creating rift at {rc}");
            CreateRift(rc);
            yield return new WaitForSeconds(riftDuration);
            FlingDrops(riftDropPrefabs, riftDropCount, rc, riftDropSpread);
            NotificationManager.Instance?.ShowInfo("Rift stabilizing — rare materials!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void LaunchMeteorite(GameObject prefab, Vector2 spawn, Vector2 target, MeteoriteType type)
    {
        if (prefab == null) { RestoreAndValidatePrefabs(); prefab = type == MeteoriteType.Stray ? strayMeteoritePrefab : type == MeteoriteType.Shower ? showerMeteoritePrefab : riftMeteoritePrefab; }
        if (prefab == null) { Debug.LogError($"[MeteoriteManager] {type} prefab null after restore."); return; }
        var inst = Instantiate(prefab, spawn, Quaternion.identity);
        if (inst.TryGetComponent<Meteorite>(out var m))
        { m.meteoriteType = type; m.SetTarget(target); MinimapController.Instance?.RegisterMeteorite(m); }
        else { Debug.LogWarning($"[MeteoriteManager] {prefab.name} missing Meteorite component."); Destroy(inst); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CreateRift — FIX: CircleCollider2D MUST be added BEFORE GravityZone
    // ─────────────────────────────────────────────────────────────────────────

    private void CreateRift(Vector2 center)
    {
        Debug.Log($"[MeteoriteManager] CreateRift executing at {center}");

        // Rift zone GameObject
        var rift = new GameObject("TempGravityRift");
        rift.transform.position = center;

        // FIX: Add CircleCollider2D FIRST — GravityZone has [RequireComponent(typeof(Collider2D))]
        var col = rift.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = riftRadius;

        // Now GravityZone can be added successfully
        var zone = rift.AddComponent<GravityZone>();
        zone.gravityType          = GravityState.GravityRift;
        zone.riftPullStrength     = riftPullStrength;
        zone.riftSpinForce        = riftSpinForce;
        zone.causesDisorientation = true;
        zone.zoneGizmoColor       = new Color(1f, 0.1f, 0f, 0.35f);

        Destroy(rift, riftDuration);

        // VFX — prefab if assigned, otherwise sprite-based fallback (URP-safe)
        var chosenVFX = showerRiftVFXPrefab != null ? showerRiftVFXPrefab
                      : riftVFXPrefab       != null ? riftVFXPrefab
                      : null;

        if (chosenVFX != null)
        {
            var vfx = Instantiate(chosenVFX, center, Quaternion.identity);
            Destroy(vfx, riftDuration + 1f);
            Debug.Log($"[MeteoriteManager] Rift VFX spawned from prefab: {chosenVFX.name}");
        }
        else
        {
            Debug.Log("[MeteoriteManager] No VFX prefab assigned — using sprite-based procedural VFX (URP-safe).");
            SpawnSpriteRiftVFX(center, riftDuration);
        }

        MinimapController.Instance?.RegisterRift(center, riftDuration);
        NotificationManager.Instance?.ShowWarning("GRAVITY RIFT ACTIVE! Escape quickly!");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sprite-based procedural rift VFX — 100% URP compatible, no shader needed
    // ─────────────────────────────────────────────────────────────────────────

    private void SpawnSpriteRiftVFX(Vector2 center, float duration)
    {
        StartCoroutine(AnimateSpriteRiftVFX(center, duration));
    }

    private IEnumerator AnimateSpriteRiftVFX(Vector2 center, float duration)
    {
        var root = new GameObject("RiftVFX_Sprites");
        root.transform.position = center;

        // Create pulsing ring sprites at random positions around the center
        var rings = new List<(Transform t, SpriteRenderer sr, float angle, float dist, float speed, float phase)>();

        int particleCount = 28;
        for (int i = 0; i < particleCount; i++)
        {
            var ringGO = new GameObject($"RiftParticle_{i}");
            ringGO.transform.SetParent(root.transform, false);

            var sr = ringGO.AddComponent<SpriteRenderer>();
            sr.sprite       = CreateCircleSprite(16);
            sr.sortingOrder = 15;

            float angle = Random.Range(0f, 360f);
            float dist  = Random.Range(riftRadius * 0.3f, riftRadius * 0.95f);
            float speed = Random.Range(20f, 80f) * (Random.value > 0.5f ? 1f : -1f);
            float phase = Random.Range(0f, Mathf.PI * 2f);
            float size  = Random.Range(0.25f, 0.7f);
            ringGO.transform.localScale = Vector3.one * size;

            // Color: red-orange with slight variation
            float hue = Random.Range(0f, 0.08f); // red to orange
            sr.color = Color.HSVToRGB(hue, 0.9f, 1f);

            rings.Add((ringGO.transform, sr, angle, dist, speed, phase));
        }

        // Pulsing central glow disc
        var glowGO = new GameObject("RiftGlow");
        glowGO.transform.SetParent(root.transform, false);
        var glowSR = glowGO.AddComponent<SpriteRenderer>();
        glowSR.sprite       = CreateCircleSprite(64);
        glowSR.sortingOrder = 10;
        glowSR.color        = new Color(1f, 0.15f, 0.05f, 0.55f);
        glowGO.transform.localScale = Vector3.one * riftRadius * 1.4f;

        // Outer ring outline
        var outlineGO = new GameObject("RiftOutline");
        outlineGO.transform.SetParent(root.transform, false);
        var outlineSR = outlineGO.AddComponent<SpriteRenderer>();
        outlineSR.sprite       = CreateRingSprite(64, 0.7f);
        outlineSR.sortingOrder = 14;
        outlineSR.color        = new Color(1f, 0.35f, 0f, 0.9f);
        outlineGO.transform.localScale = Vector3.one * riftRadius * 2f;

        float elapsed = 0f;
        float fadeInTime  = 0.5f;
        float fadeOutTime = 1.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t     = elapsed / duration;
            float alpha = elapsed < fadeInTime  ? elapsed / fadeInTime
                        : elapsed > duration - fadeOutTime ? (duration - elapsed) / fadeOutTime
                        : 1f;

            // Rotate and pulse each particle
            for (int i = 0; i < rings.Count; i++)
            {
                var (trans, sr, startAngle, dist, speed, phase) = rings[i];
                float currentAngle = (startAngle + speed * elapsed) * Mathf.Deg2Rad;
                // Particles spiral inward over time
                float currentDist  = dist * (1f - t * 0.4f);
                trans.localPosition = new Vector3(
                    Mathf.Cos(currentAngle) * currentDist,
                    Mathf.Sin(currentAngle) * currentDist, 0f);

                // Flicker
                float brightness = 0.6f + 0.4f * Mathf.Sin(elapsed * 8f + phase);
                Color c = sr.color;
                sr.color = new Color(c.r, c.g, c.b, alpha * brightness);
            }

            // Pulse glow
            float pulse = 0.4f + 0.15f * Mathf.Sin(elapsed * 3f);
            glowSR.color = new Color(1f, 0.15f, 0.05f, alpha * pulse);
            float glowScale = riftRadius * 1.4f * (0.9f + 0.1f * Mathf.Sin(elapsed * 2f));
            glowGO.transform.localScale = Vector3.one * glowScale;

            // Rotate outline
            outlineGO.transform.rotation = Quaternion.Euler(0f, 0f, elapsed * 15f);
            outlineSR.color = new Color(1f, 0.35f, 0f, alpha * 0.85f);

            yield return null;
        }

        Destroy(root);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sprite generation helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static Sprite CreateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float a = Mathf.Clamp01(1f - (d / c));
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateRingSprite(int size, float thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) * 0.5f;
        float outerR = c;
        float innerR = c * (1f - thickness);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float a = (d <= outerR && d >= innerR)
                ? Mathf.Clamp01((d - innerR) / (c * thickness * 0.5f))
                  * Mathf.Clamp01((outerR - d) / (c * thickness * 0.5f))
                : 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void FlingDrops(GameObject[] prefabs, int count, Vector2 center, float spread)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            var chosen = prefabs[Random.Range(0, prefabs.Length)]; if (chosen == null) continue;
            var obj = Instantiate(chosen,
                new Vector2(center.x + Random.Range(-spread * 0.3f, spread * 0.3f), center.y),
                Quaternion.identity);
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
