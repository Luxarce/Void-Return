using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Spawns materials randomly within a zone area with configurable rarity weights.
/// Materials that are not collected within despawnTime seconds are removed.
///
/// SETUP:
///  1. Create an empty GameObject for each zone (e.g., Zone1MaterialSpawner).
///  2. Attach MaterialSpawner. Set spawnAreaCenter and spawnAreaSize.
///  3. Add entries to the materialEntries array: assign prefab, rarity weight, min/max count.
///  4. Set initialSpawnCount and respawnInterval.
///
/// ZONE RARITY GUIDE:
///  Zone 1 (Debris Field) — common materials: MetalScrap, Bolt, Glass, Foam, Sealant
///  Zone 2 (Drift Ring)   — mid-tier: CopperWire, Filter, CircuitBoard, Titanium, Lens
///  Zone 3 (Deep Scatter) — rare: FuelCell, Coolant, HeatShield, TitaniumRod
/// </summary>
public class MaterialSpawner : MonoBehaviour
{
    [System.Serializable]
    public class MaterialEntry
    {
        [Tooltip("MaterialPickup prefab to spawn.")]
        public GameObject prefab;

        [Tooltip("Relative rarity weight. Higher = spawns more often. " +
                 "Example: weight 10 vs weight 1 = 10x more likely to pick the first.")]
        [Range(1, 100)]
        public int rarityWeight = 10;

        [Tooltip("Minimum number to spawn per batch.")]
        [Range(1, 5)]
        public int minCount = 1;

        [Tooltip("Maximum number to spawn per batch.")]
        [Range(1, 10)]
        public int maxCount = 3;
    }

    [Header("Zone Bounds")]
    [Tooltip("Center of the spawn area in world space.")]
    public Vector2 spawnAreaCenter = Vector2.zero;

    [Tooltip("Width and Height of the spawn region.")]
    public Vector2 spawnAreaSize = new Vector2(60f, 60f);

    [Header("Materials")]
    [Tooltip("Which materials can spawn here and their rarity weights.")]
    public MaterialEntry[] materialEntries;

    [Header("Spawn Timing")]
    [Tooltip("Number of material groups spawned at scene start.")]
    [Range(1, 30)]
    public int initialSpawnCount = 8;

    [Tooltip("Seconds between automatic respawn batches after initial spawn. " +
             "Set to 0 to disable respawning.")]
    [Range(0f, 300f)]
    public float respawnInterval = 60f;

    [Tooltip("Number of material groups added per respawn batch.")]
    [Range(1, 10)]
    public int respawnBatchSize = 3;

    [Tooltip("Maximum number of material instances allowed in this zone at once.")]
    [Range(1, 100)]
    public int maxActivePickups = 20;

    [Header("Despawn")]
    [Tooltip("Seconds before an uncollected material fades and despawns. " +
             "Set to 0 to disable timed despawn.")]
    [Range(0f, 600f)]
    public float despawnTime = 120f;

    [Tooltip("Seconds the material takes to fade out before being destroyed.")]
    [Range(0f, 10f)]
    public float despawnFadeDuration = 3f;

    // ─────────────────────────────────────────────────────────────────────────
    private readonly List<GameObject> _activePickups = new();
    private int _totalWeight;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        CalculateTotalWeight();
        SpawnBatch(initialSpawnCount);

        if (respawnInterval > 0f)
            StartCoroutine(RespawnRoutine());
    }

    private void Update()
    {
        // Clean up null references (destroyed pickups)
        _activePickups.RemoveAll(go => go == null);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator RespawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(respawnInterval);
            int toSpawn = Mathf.Min(respawnBatchSize, maxActivePickups - _activePickups.Count);
            if (toSpawn > 0) SpawnBatch(toSpawn);
        }
    }

    private void SpawnBatch(int groupCount)
    {
        if (materialEntries == null || materialEntries.Length == 0) return;

        for (int i = 0; i < groupCount; i++)
        {
            if (_activePickups.Count >= maxActivePickups) break;

            var entry = PickWeightedEntry();
            if (entry?.prefab == null) continue;

            int count = Random.Range(entry.minCount, entry.maxCount + 1);
            for (int j = 0; j < count && _activePickups.Count < maxActivePickups; j++)
            {
                Vector2 pos = GetRandomPosition();
                var obj = Instantiate(entry.prefab, pos, Quaternion.identity);
                _activePickups.Add(obj);

                if (despawnTime > 0f)
                    StartCoroutine(DespawnAfter(obj, despawnTime));
            }
        }
    }

    private IEnumerator DespawnAfter(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (obj == null) yield break; // already collected

        // Fade out the SpriteRenderer if present
        var sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null && despawnFadeDuration > 0f)
        {
            float elapsed = 0f;
            Color start   = sr.color;
            while (elapsed < despawnFadeDuration && obj != null)
            {
                sr.color = new Color(start.r, start.g, start.b,
                                     Mathf.Lerp(start.a, 0f, elapsed / despawnFadeDuration));
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (obj != null) Destroy(obj);
    }

    private MaterialEntry PickWeightedEntry()
    {
        if (_totalWeight <= 0 || materialEntries == null) return null;

        int roll = Random.Range(0, _totalWeight);
        int cumulative = 0;
        foreach (var entry in materialEntries)
        {
            cumulative += entry.rarityWeight;
            if (roll < cumulative) return entry;
        }
        return materialEntries[materialEntries.Length - 1];
    }

    private void CalculateTotalWeight()
    {
        _totalWeight = 0;
        if (materialEntries == null) return;
        foreach (var e in materialEntries) _totalWeight += e.rarityWeight;
    }

    private Vector2 GetRandomPosition()
    {
        float x = Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f);
        float y = Random.Range(-spawnAreaSize.y * 0.5f, spawnAreaSize.y * 0.5f);
        return spawnAreaCenter + new Vector2(x, y);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.08f);
        Gizmos.DrawCube(spawnAreaCenter,
            new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f));
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
        Gizmos.DrawWireCube(spawnAreaCenter,
            new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f));
    }
}
