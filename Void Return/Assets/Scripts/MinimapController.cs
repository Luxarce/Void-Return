using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls the minimap camera and all marker overlays.
///
/// MATERIAL MARKER COLOR: uses the prefab's original color — no rarity tinting.
/// If materialMarkerPrefab is null, auto-generated dots use a single default color.
/// </summary>
public class MinimapController : MonoBehaviour
{
    public static MinimapController Instance { get; private set; }

    [Header("Camera")]
    public Camera    minimapCamera;
    public Camera    mainCamera;
    public float     minimapCameraSize = 60f;
    public Transform playerTransform;
    [Range(1f, 20f)] public float followSpeed = 8f;

    [Header("Minimap Layer")]
    [Tooltip("Layer index for the 'Minimap' layer. Edit > Project Settings > Tags and Layers.")]
    public int minimapLayerIndex = 8;

    [Header("Material Markers")]
    [Tooltip("Prefab for material markers. Its original color is used — no tinting is applied.")]
    public GameObject materialMarkerPrefab;

    [Tooltip("Color of auto-generated dots when materialMarkerPrefab is null.")]
    public Color defaultMaterialMarkerColor = new Color(1f, 0.85f, 0.1f);

    public bool markersUnlockedFromStart = false;
    [Range(0f, 30f)] public float autoRefreshInterval = 5f;

    [Header("Rift Markers")]
    public GameObject riftMarkerPrefab;
    public Color riftMarkerColor = new Color(1f, 0.15f, 0.1f);

    [Header("Gravity Zone Markers")]
    [Tooltip("Marker color for ZeroG zones (cyan).")]
    public Color zeroGZoneColor     = new Color(0f,  0.9f, 1f,   0.85f);
    [Tooltip("Marker color for MicroPull zones (purple).")]
    public Color microPullZoneColor = new Color(0.6f, 0.1f, 1f,  0.85f);
    [Tooltip("Marker color for GravityRift permanent zones (red-orange).")]
    public Color gravityRiftZoneColor = new Color(1f, 0.25f, 0.05f, 0.85f);
    [Tooltip("Size of permanent gravity zone markers on the minimap.")]
    public float gravityZoneMarkerScale = 9f;

    [Header("Gravity Zone Marker Opacity")]
    [Tooltip("Alpha/opacity of ZeroG zone markers. 0 = invisible, 1 = fully opaque.")]
    [Range(0f, 1f)] public float zeroGZoneOpacity     = 0.5f;
    [Tooltip("Alpha/opacity of MicroPull zone markers.")]
    [Range(0f, 1f)] public float microPullZoneOpacity  = 0.5f;
    [Tooltip("Alpha/opacity of GravityRift zone markers.")]
    [Range(0f, 1f)] public float gravityRiftZoneOpacity = 0.6f;
    [Tooltip("Alpha/opacity of temporary meteorite rift markers.")]
    [Range(0f, 1f)] public float meteoriteRiftOpacity   = 0.85f;

    [Header("Meteorite Markers")]
    public GameObject meteoriteMarkerPrefab;
    public Color meteoriteMarkerColor = new Color(1f, 0.5f, 0f);

    [Header("Player Marker")]
    public GameObject playerMarkerPrefab;
    public Color playerMarkerColor = new Color(1f, 0.3f, 0.2f);

    [Header("Shipwreck Direction")]
    [Tooltip("REQUIRED: Drag the Life Support Transform here.")]
    public Transform  shipwreckTransform;
    public GameObject shipwreckMarkerPrefab;
    public Color      shipwreckMarkerColor   = new Color(0.2f, 0.7f, 1f);
    public float      shipwreckEdgePinRadius = 55f;

    [Header("Marker Sizes")]
    public float markerScale          = 8f;
    public float playerMarkerScale    = 10f;
    public float riftMarkerScale      = 12f;
    public float meteoriteMarkerScale = 9f;
    public float shipwreckMarkerScale = 11f;

    // ─────────────────────────────────────────────────────────────────────────
    private bool       _partialUnlocked;
    private bool       _fullUnlocked;
    private GameObject _playerMarkerInstance;
    private GameObject _shipwreckMarkerInstance;
    private float      _refreshTimer;
    private Transform  _markerContainer;

    private readonly List<GameObject>                  _materialMarkers  = new();
    private readonly List<GameObject>                  _riftMarkers      = new();
    private readonly List<GameObject>                  _gravityZoneMarkers = new();
    // Zones registered before full minimap unlock — shown when Navigation Stage 2 completes
    private readonly List<(Vector2 pos, GravityState type)> _pendingZones = new();
    private readonly Dictionary<Meteorite, GameObject> _meteoriteMarkers = new();

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (markersUnlockedFromStart) _fullUnlocked = true;
        SetupLayers();
        SetupCamera();
        _markerContainer = new GameObject("_MinimapMarkers").transform;
        CreatePlayerMarker();
        CreateShipwreckMarker();
        if (_fullUnlocked) RefreshMaterialMarkers();
    }

    private void LateUpdate()
    {
        FollowPlayer();
        UpdatePlayerMarker();
        UpdateShipwreckMarker();
        CleanStaleMeteorites();
        if (_fullUnlocked) AutoRefresh();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void SetupLayers()
    {
        int mask = 1 << minimapLayerIndex;
        if (minimapCamera != null) minimapCamera.cullingMask = mask;
        if (mainCamera == null)    mainCamera = Camera.main;
        if (mainCamera != null)    mainCamera.cullingMask &= ~mask;
    }

    private void SetupCamera()
    {
        if (minimapCamera == null) return;
        minimapCamera.orthographic     = true;
        minimapCamera.orthographicSize = minimapCameraSize;
        minimapCamera.clearFlags       = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor  = new Color(0.02f, 0.02f, 0.06f, 1f);
    }

    private void CreatePlayerMarker()
    {
        if (playerTransform == null) return;
        _playerMarkerInstance = playerMarkerPrefab != null
            ? Instantiate(playerMarkerPrefab, playerTransform.position, Quaternion.identity, _markerContainer)
            : CreateDot("PlayerDot", playerMarkerColor, playerMarkerScale);
        if (playerMarkerPrefab == null) _playerMarkerInstance.transform.SetParent(_markerContainer);
        SetMinimapLayer(_playerMarkerInstance);
    }

    private void CreateShipwreckMarker()
    {
        if (shipwreckTransform == null)
        {
            Debug.LogWarning("[MinimapController] shipwreckTransform not assigned — direction arrow missing.");
            return;
        }
        _shipwreckMarkerInstance = shipwreckMarkerPrefab != null
            ? Instantiate(shipwreckMarkerPrefab, shipwreckTransform.position, Quaternion.identity, _markerContainer)
            : CreateDot("ShipwreckDot", shipwreckMarkerColor, shipwreckMarkerScale);
        if (shipwreckMarkerPrefab == null) _shipwreckMarkerInstance.transform.SetParent(_markerContainer);
        SetMinimapLayer(_shipwreckMarkerInstance);
        Debug.Log($"[MinimapController] Shipwreck marker created at {shipwreckTransform.position}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void UnlockPartialMinimap()
    {
        _partialUnlocked = true;
        Debug.Log("[MinimapController] Partial minimap unlocked.");
    }

    public void UnlockFullMinimap()
    {
        _fullUnlocked = true;
        RefreshMaterialMarkers();
        // Re-register any gravity zones that were registered before full unlock
        RefreshGravityZoneMarkers();
        Debug.Log("[MinimapController] Full minimap unlocked.");
    }

    /// <summary>
    /// Called after full minimap unlock to show all gravity zones that were
    /// registered before Navigation Stage 2 was complete.
    /// </summary>
    private void RefreshGravityZoneMarkers()
    {
        // The queued zone data is stored in _pendingZones so we can re-show them
        foreach (var (pos, type) in _pendingZones)
        {
            Color color = type switch
            {
                GravityState.ZeroG       => new Color(zeroGZoneColor.r,      zeroGZoneColor.g,      zeroGZoneColor.b,      zeroGZoneOpacity),
                GravityState.MicroPull   => new Color(microPullZoneColor.r,  microPullZoneColor.g,  microPullZoneColor.b,  microPullZoneOpacity),
                GravityState.GravityRift => new Color(gravityRiftZoneColor.r,gravityRiftZoneColor.g,gravityRiftZoneColor.b,gravityRiftZoneOpacity),
                _                        => new Color(0.5f, 0.5f, 0.5f, 0.5f),
            };
            string label = type switch
            {
                GravityState.ZeroG       => "ZeroGDot",
                GravityState.MicroPull   => "MicroPullDot",
                GravityState.GravityRift => "RiftZoneDot",
                _                        => "ZoneDot",
            };
            var marker = CreateDot(label, color, gravityZoneMarkerScale);
            marker.transform.SetParent(_markerContainer);
            marker.transform.position   = pos;
            marker.transform.localScale = Vector3.one * gravityZoneMarkerScale;
            SetMinimapLayer(marker);
            _gravityZoneMarkers.Add(marker);
        }
    }

    public void UnlockMaterialMarkers()
    {
        _fullUnlocked = true;
        RefreshMaterialMarkers();
        NotificationManager.Instance?.ShowInfo("Material locations now visible on minimap.");
    }

    public void RefreshMaterialMarkers()
    {
        foreach (var m in _materialMarkers) if (m != null) Destroy(m);
        _materialMarkers.Clear();
        if (!_fullUnlocked) return;

        foreach (var pickup in FindObjectsByType<MaterialPickup>(FindObjectsSortMode.None))
        {
            GameObject marker;
            if (materialMarkerPrefab != null)
            {
                // Use prefab as-is — no color override, preserving its original appearance
                marker = Instantiate(materialMarkerPrefab, pickup.transform.position,
                                     Quaternion.identity, _markerContainer);
            }
            else
            {
                // Auto-generated dot uses the default single color (no rarity distinction)
                marker = CreateDot("MatDot", defaultMaterialMarkerColor, markerScale);
                marker.transform.SetParent(_markerContainer);
            }

            marker.transform.position   = pickup.transform.position;
            marker.transform.localScale = Vector3.one * markerScale;
            SetMinimapLayer(marker);
            _materialMarkers.Add(marker);
        }
    }

    public void RegisterRift(Vector2 worldPos, float lifetimeSeconds)
    {
        // Rifts are always shown on the minimap regardless of unlock state
        Color riftColor = new Color(riftMarkerColor.r, riftMarkerColor.g, riftMarkerColor.b, meteoriteRiftOpacity);
        var marker = riftMarkerPrefab != null
            ? Instantiate(riftMarkerPrefab, worldPos, Quaternion.identity, _markerContainer)
            : CreateDot("RiftDot", riftColor, riftMarkerScale);
        if (riftMarkerPrefab == null) marker.transform.SetParent(_markerContainer);
        marker.transform.position   = worldPos;
        marker.transform.localScale = Vector3.one * riftMarkerScale;
        SetMinimapLayer(marker);
        _riftMarkers.Add(marker);
        Destroy(marker, lifetimeSeconds + 1f);
    }

    /// <summary>
    /// Registers a permanent gravity zone marker on the minimap.
    /// Called by ZoneGravitySpawner for every spawned zone.
    /// Color is determined by the zone type.
    /// Only shown when the minimap is fully unlocked (Navigation Stage 2).
    /// </summary>
    public void RegisterZone(Vector2 worldPos, GravityState zoneType)
    {
        // Queue zone for display when minimap is fully unlocked
        // Zones are always shown on the minimap regardless of unlock state

        float opacity = zoneType switch
        {
            GravityState.ZeroG       => zeroGZoneOpacity,
            GravityState.MicroPull   => microPullZoneOpacity,
            GravityState.GravityRift => gravityRiftZoneOpacity,
            _                        => 0.5f,
        };
        Color baseColor = zoneType switch
        {
            GravityState.ZeroG        => zeroGZoneColor,
            GravityState.MicroPull    => microPullZoneColor,
            GravityState.GravityRift  => gravityRiftZoneColor,
            _                         => new Color(0.5f, 0.5f, 0.5f, 0.7f),
        };
        Color color = new Color(baseColor.r, baseColor.g, baseColor.b, opacity);

        string label = zoneType switch
        {
            GravityState.ZeroG       => "ZeroGDot",
            GravityState.MicroPull   => "MicroPullDot",
            GravityState.GravityRift => "RiftZoneDot",
            _                        => "ZoneDot",
        };

        var marker = CreateDot(label, color, gravityZoneMarkerScale);
        marker.transform.SetParent(_markerContainer);
        marker.transform.position   = worldPos;
        marker.transform.localScale = Vector3.one * gravityZoneMarkerScale;
        SetMinimapLayer(marker);
        _gravityZoneMarkers.Add(marker);
    }

    public void RegisterMeteorite(Meteorite meteorite)
    {
        if (!_fullUnlocked || meteorite == null) return;
        var marker = meteoriteMarkerPrefab != null
            ? Instantiate(meteoriteMarkerPrefab, meteorite.transform.position, Quaternion.identity, _markerContainer)
            : CreateDot("MeteoriteDot", meteoriteMarkerColor, meteoriteMarkerScale);
        if (meteoriteMarkerPrefab == null) marker.transform.SetParent(_markerContainer);
        marker.transform.localScale = Vector3.one * meteoriteMarkerScale;
        SetMinimapLayer(marker);
        _meteoriteMarkers[meteorite] = marker;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void FollowPlayer()
    {
        if (minimapCamera == null || playerTransform == null) return;
        Vector3 t = new Vector3(playerTransform.position.x, playerTransform.position.y,
                                minimapCamera.transform.position.z);
        minimapCamera.transform.position = Vector3.Lerp(minimapCamera.transform.position, t, followSpeed * Time.deltaTime);
    }

    private void UpdatePlayerMarker()
    {
        if (_playerMarkerInstance == null || playerTransform == null) return;
        _playerMarkerInstance.transform.position = playerTransform.position;
        _playerMarkerInstance.transform.rotation = playerTransform.rotation;
    }

    private void UpdateShipwreckMarker()
    {
        if (_shipwreckMarkerInstance == null || shipwreckTransform == null || playerTransform == null) return;
        Vector2 toShip   = (Vector2)shipwreckTransform.position - (Vector2)playerTransform.position;
        Vector2 markerPos = toShip.magnitude <= shipwreckEdgePinRadius
            ? (Vector2)shipwreckTransform.position
            : (Vector2)playerTransform.position + toShip.normalized * shipwreckEdgePinRadius;
        _shipwreckMarkerInstance.transform.position = markerPos;
        float angle = Mathf.Atan2(toShip.y, toShip.x) * Mathf.Rad2Deg;
        _shipwreckMarkerInstance.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private void CleanStaleMeteorites()
    {
        var rem = new List<Meteorite>();
        foreach (var kv in _meteoriteMarkers)
        {
            if (kv.Key == null) { if (kv.Value != null) Destroy(kv.Value); rem.Add(kv.Key); }
            else if (kv.Value != null) kv.Value.transform.position = kv.Key.transform.position;
        }
        foreach (var k in rem) _meteoriteMarkers.Remove(k);
    }

    private void AutoRefresh()
    {
        if (autoRefreshInterval <= 0f) return;
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= autoRefreshInterval) { _refreshTimer = 0f; RefreshMaterialMarkers(); }
    }

    private void SetMinimapLayer(GameObject go)
    {
        if (go == null) return;
        go.layer = minimapLayerIndex;
        foreach (Transform child in go.transform) child.gameObject.layer = minimapLayerIndex;
    }

    private GameObject CreateDot(string name, Color color, float scale)
    {
        var go = new GameObject(name);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateDotSprite();
        sr.color  = color;
        sr.sortingOrder = 5;
        return go;
    }

    private static Sprite CreateDotSprite()
    {
        const int size = 16; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            tex.SetPixel(x, y, Mathf.Sqrt((x-c)*(x-c)+(y-c)*(y-c)) <= c ? Color.white : Color.clear);
        tex.Apply(); tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), size);
    }
}
