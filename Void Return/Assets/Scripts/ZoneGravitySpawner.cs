using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Spawns gravity zones in a ring-shaped area at scene load.
/// GravityRift zones get a persistent animated sprite-based VFX ring
/// (identical technique to MeteoriteManager's procedural rift VFX).
/// Each zone type has a distinct VFX color:
///   ZeroG     — cyan/teal
///   MicroPull — purple/blue
///   GravityRift — red/orange (same as meteorite rifts)
/// </summary>
public class ZoneGravitySpawner : MonoBehaviour
{
    [Header("Zone Type")]
    [Tooltip("Zone 1 = ZeroG, Zone 2 = MicroPull, Zone 3 = GravityRift")]
    public GravityState zoneType = GravityState.ZeroG;

    [Header("Ring-Shaped Spawn Area")]
    [Range(0f, 200f)] public float innerRadius   = 10f;
    [Range(5f, 200f)] public float outerRadius   = 40f;
    public Vector2 ringCenter = Vector2.zero;

    [Header("Zone Count and Size")]
    [Range(1, 20)]  public int   spawnCount    = 5;
    [Range(1f, 20f)] public float minRadius    = 4f;
    [Range(2f, 30f)] public float maxRadius    = 10f;
    [Range(0f, 30f)] public float minSeparation = 6f;

    [Header("Strengths")]
    public float zeroGStrength     = 2f;
    public float microPullStrength = 3f;
    public float riftPullStrength  = 12f;
    public float riftSpinForce     = 120f;

    [Header("Gizmo Colors")]
    public Color zeroGColor     = new Color(0f, 1f, 0.8f, 0.3f);
    public Color microPullColor = new Color(0.5f, 0f, 1f, 0.3f);
    public Color riftColor      = new Color(1f, 0.1f, 0f, 0.35f);

    [Header("Rift Zone VFX")]
    [Tooltip("Particle System or sprite prefab for GravityRift zones. " +
             "If null, the procedural sprite VFX is generated automatically.")]
    public GameObject riftZoneVFXPrefab;

    // ─────────────────────────────────────────────────────────────────────────
    private readonly List<Vector2>    _positions = new();
    private readonly List<GameObject> _zones     = new();

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        SpawnZones();
    }

    public void SpawnZones()
    {
        _positions.Clear();
        foreach (var z in _zones) if (z != null) Destroy(z);
        _zones.Clear();

        if (innerRadius >= outerRadius)
        {
            Debug.LogError($"[ZoneGravitySpawner] innerRadius ({innerRadius}) must be < outerRadius ({outerRadius}). No zones spawned.");
            return;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 pos = TryFindPosition(50);
            if (pos == Vector2.negativeInfinity)
            {
                Debug.LogWarning($"[ZoneGravitySpawner] Could not place zone {i+1}/{spawnCount}. Reduce minSeparation or spawnCount.");
                continue;
            }
            float radius = Random.Range(minRadius, maxRadius);
            SpawnZoneAt(pos, radius);
            _positions.Add(pos);
        }

        Debug.Log($"[ZoneGravitySpawner] Spawned {_zones.Count}/{spawnCount} {zoneType} zones in ring {innerRadius}-{outerRadius}u");
    }

    private Vector2 TryFindPosition(int maxAttempts)
    {
        float innerSq = innerRadius * innerRadius;
        float outerSq = outerRadius * outerRadius;
        for (int i = 0; i < maxAttempts; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r     = Mathf.Sqrt(Random.Range(innerSq, outerSq));
            Vector2 pos = ringCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            bool tooClose = false;
            foreach (var existing in _positions)
                if (Vector2.Distance(pos, existing) < minSeparation) { tooClose = true; break; }
            if (!tooClose) return pos;
        }
        return Vector2.negativeInfinity;
    }

    private void SpawnZoneAt(Vector2 pos, float radius)
    {
        var go = new GameObject($"GravZone_{zoneType}_{_zones.Count}");
        go.transform.position = pos;
        go.transform.SetParent(transform);

        // CircleCollider2D MUST come before GravityZone (RequireComponent rule)
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = radius;

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

        _zones.Add(go);

        // Spawn persistent VFX for ALL zone types, each with a different color
        SpawnRiftZoneVFX(pos, radius);

        // Register on minimap (shown after Navigation Stage 2 is repaired)
        MinimapController.Instance?.RegisterZone(pos, zoneType);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Persistent rift zone VFX — stays for the lifetime of the scene
    // ─────────────────────────────────────────────────────────────────────────

    private void SpawnRiftZoneVFX(Vector2 center, float zoneRadius)
    {
        if (riftZoneVFXPrefab != null)
        {
            Instantiate(riftZoneVFXPrefab, center, Quaternion.identity, transform);
            return;
        }
        // Procedural sprite VFX — identical visual language to meteorite rifts
        // but uses the zone's riftColor and runs indefinitely (no Destroy)
        StartCoroutine(AnimatePersistentRiftVFX(center, zoneRadius, riftColor));
    }

    private IEnumerator AnimatePersistentRiftVFX(Vector2 center, float zoneRadius, Color baseColor)
    {
        var root = new GameObject($"RiftZoneVFX_{center.x:F0}_{center.y:F0}");
        root.transform.SetParent(transform);
        root.transform.position = center;

        // Determine color scheme from zoneType
        // ZeroG  → cyan,   MicroPull → purple,   GravityRift → red-orange
        Color dotColor, glowColor, outlineColor;
        switch (zoneType)
        {
            case GravityState.ZeroG:
                dotColor     = new Color(0f,  0.9f, 1f,  1f);
                glowColor    = new Color(0f,  0.6f, 0.8f, 0.4f);
                outlineColor = new Color(0f,  0.8f, 1f,  0.8f);
                break;
            case GravityState.MicroPull:
                dotColor     = new Color(0.6f, 0.1f, 1f, 1f);
                glowColor    = new Color(0.4f, 0f,   0.8f, 0.35f);
                outlineColor = new Color(0.7f, 0.2f, 1f, 0.75f);
                break;
            default: // GravityRift
                dotColor     = new Color(1f,  0.25f, 0.05f, 1f);
                glowColor    = new Color(1f,  0.1f,  0.05f, 0.45f);
                outlineColor = new Color(1f,  0.4f,  0f,    0.85f);
                break;
        }

        // Orbiting particles
        var particles = new List<(Transform t, SpriteRenderer sr, float angle, float dist, float speed, float phase)>();
        int count = Mathf.RoundToInt(Mathf.Lerp(12, 24, (zoneRadius - minRadius) / Mathf.Max(maxRadius - minRadius, 1f)));

        for (int i = 0; i < count; i++)
        {
            var pGO = new GameObject($"P{i}");
            pGO.transform.SetParent(root.transform, false);
            var sr        = pGO.AddComponent<SpriteRenderer>();
            sr.sprite     = CreateCircleSprite(16);
            sr.color      = dotColor;
            sr.sortingOrder = 15;
            float sz      = Random.Range(0.18f, 0.55f);
            pGO.transform.localScale = Vector3.one * sz;
            float angle   = (360f / count) * i + Random.Range(-15f, 15f);
            float dist    = zoneRadius * Random.Range(0.35f, 0.92f);
            float spd     = Random.Range(18f, 55f) * (i % 2 == 0 ? 1f : -1f);
            float phase   = Random.Range(0f, Mathf.PI * 2f);
            particles.Add((pGO.transform, sr, angle, dist, spd, phase));
        }

        // Central glow
        var glowGO = new GameObject("Glow");
        glowGO.transform.SetParent(root.transform, false);
        var glowSR        = glowGO.AddComponent<SpriteRenderer>();
        glowSR.sprite     = CreateCircleSprite(64);
        glowSR.color      = glowColor;
        glowSR.sortingOrder = 10;
        glowGO.transform.localScale = Vector3.one * zoneRadius * 1.6f;

        // Ring outline
        var ringGO = new GameObject("Ring");
        ringGO.transform.SetParent(root.transform, false);
        var ringSR        = ringGO.AddComponent<SpriteRenderer>();
        ringSR.sprite     = CreateRingSprite(64, 0.65f);
        ringSR.color      = outlineColor;
        ringSR.sortingOrder = 14;
        ringGO.transform.localScale = Vector3.one * zoneRadius * 2.1f;

        float elapsed = 0f;
        float fadeInTime = 1f;

        // Run indefinitely — destroyed when the zone parent is destroyed
        while (root != null)
        {
            elapsed += Time.deltaTime;
            float alpha = elapsed < fadeInTime ? elapsed / fadeInTime : 1f;

            for (int i = 0; i < particles.Count; i++)
            {
                var (t, sr, startAngle, dist, spd, phase) = particles[i];
                float angle = (startAngle + spd * elapsed) * Mathf.Deg2Rad;
                t.localPosition = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);
                float flicker = 0.55f + 0.45f * Mathf.Sin(elapsed * 7f + phase);
                sr.color = new Color(dotColor.r, dotColor.g, dotColor.b, alpha * flicker);
            }

            float pulse = 0.35f + 0.15f * Mathf.Sin(elapsed * 2.2f);
            glowSR.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha * pulse);

            ringGO.transform.rotation = Quaternion.Euler(0f, 0f, elapsed * 12f);
            float ringAlpha = (0.6f + 0.25f * Mathf.Sin(elapsed * 1.5f)) * alpha;
            ringSR.color = new Color(outlineColor.r, outlineColor.g, outlineColor.b, ringAlpha);

            yield return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sprite helpers (shared with MeteoriteManager pattern)
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
        tex.Apply(); tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateRingSprite(int size, float thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) * 0.5f, outerR = c, innerR = c * (1f - thickness);
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
        tex.Apply(); tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Color c = zoneType == GravityState.MicroPull   ? microPullColor
                : zoneType == GravityState.GravityRift ? riftColor
                : zeroGColor;
        Gizmos.color = new Color(c.r, c.g, c.b, 0.5f);
        DrawCircleGizmo(ringCenter, innerRadius);
        DrawCircleGizmo(ringCenter, outerRadius);
        Gizmos.color = new Color(c.r, c.g, c.b, 0.06f);
        for (float r = innerRadius; r < outerRadius; r += (outerRadius - innerRadius) * 0.25f)
            DrawCircleGizmo(ringCenter, r);
    }

    private void OnDrawGizmosSelected()
    {
        Color c = zoneType == GravityState.MicroPull   ? microPullColor
                : zoneType == GravityState.GravityRift ? riftColor
                : zeroGColor;
        Gizmos.color = new Color(c.r, c.g, c.b, 0.6f);
        foreach (var pos in _positions)
        { DrawCircleGizmo(pos, minRadius); DrawCircleGizmo(pos, maxRadius); }
    }

    private static void DrawCircleGizmo(Vector2 center, float radius)
    {
        if (radius <= 0f) return;
        int segs = 48; float step = Mathf.PI * 2f / segs;
        for (int i = 0; i < segs; i++)
        {
            float a1 = i * step, a2 = (i + 1) * step;
            Gizmos.DrawLine(center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius,
                            center + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * radius);
        }
    }
}
