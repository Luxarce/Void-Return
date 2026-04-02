using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls the minimap camera and marker overlays.
///
/// NEW FEATURES:
///  — Shows active gravity rift markers (registered via RegisterRift()).
///  — Shows active meteorite markers (registered via RegisterMeteorite()).
///    Meteorite markers auto-remove when the meteorite is destroyed.
///  — Shipwreck/module direction arrow on the minimap that points toward
///    the ship even when it is off-screen (edge arrow).
///  — Static singleton so MeteoriteManager can call Instance.RegisterRift().
/// </summary>
public class MinimapController : MonoBehaviour
{
    public static MinimapController Instance { get; private set; }

    [Header("Camera")]
    public Camera minimapCamera;
    public Camera mainCamera;
    public float  minimapCameraSize = 60f;
    public Transform playerTransform;
    [Range(1f, 20f)] public float followSpeed = 8f;

    [Header("Minimap Layer")]
    [Tooltip("Layer index for 'Minimap' layer. Set in Edit > Project Settings > Tags and Layers.")]
    public int minimapLayerIndex = 8;

    [Header("Material Markers")]
    public GameObject materialMarkerPrefab;
    public bool markersUnlockedFromStart = false;
    [Range(0f, 30f)] public float autoRefreshInterval = 5f;

    [Header("Rift Markers")]
    [Tooltip("Prefab for the gravity rift marker on the minimap (red dot or icon).")]
    public GameObject riftMarkerPrefab;

    [Tooltip("Color of auto-generated rift markers (used when riftMarkerPrefab is null).")]
    public Color riftMarkerColor = new Color(1f, 0.15f, 0.1f);

    [Header("Meteorite Markers")]
    [Tooltip("Prefab for active meteorite markers on the minimap (orange arrow or dot).")]
    public GameObject meteoriteMarkerPrefab;
    [Tooltip("Color of auto-generated meteorite markers.")]
    public Color meteoriteMarkerColor = new Color(1f, 0.5f, 0f);

    [Header("Player Marker")]
    public GameObject playerMarkerPrefab;
    public Color playerMarkerColor = new Color(1f, 0.3f, 0.2f);

    [Header("Shipwreck Direction Marker")]
    [Tooltip("The Transform of the shipwreck or a central module (e.g., LifeSupport). " +
             "A direction arrow will point toward this from the player position.")]
    public Transform shipwreckTransform;

    [Tooltip("Prefab for the shipwreck direction arrow marker on the minimap.")]
    public GameObject shipwreckMarkerPrefab;

    [Tooltip("Color of the auto-generated shipwreck direction arrow.")]
    public Color shipwreckMarkerColor = new Color(0.2f, 0.7f, 1f);

    [Tooltip("Distance from the minimap center at which the shipwreck arrow is pinned " +
             "(when the wreck is outside the minimap view). World units.")]
    public float shipwreckEdgePinRadius = 55f;

    [Header("Marker Sizes (world units)")]
    public float markerScale         = 6f;
    public float playerMarkerScale   = 8f;
    public float riftMarkerScale     = 10f;
    public float meteoriteMarkerScale = 7f;
    public float shipwreckMarkerScale = 9f;

    // ─────────────────────────────────────────────────────────────────────────
    private bool       _markersUnlocked;
    private GameObject _playerMarkerInstance;
    private GameObject _shipwreckMarkerInstance;
    private float      _refreshTimer;
    private Transform  _markerContainer;

    private readonly List<GameObject> _materialMarkers  = new();
    private readonly List<GameObject> _riftMarkers      = new();
    // Meteorite markers are tracked alongside the Meteorite component
    private readonly Dictionary<Meteorite, GameObject> _meteoriteMarkers = new();

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _markersUnlocked = markersUnlockedFromStart;
        SetupLayers();
        SetupCamera();
        CreateMarkerContainer();
        CreatePlayerMarker();
        CreateShipwreckMarker();
        if (_markersUnlocked) RefreshMaterialMarkers();
    }

    private void LateUpdate()
    {
        FollowPlayer();
        UpdatePlayerMarker();
        UpdateShipwreckMarker();
        CleanStaleMeteorites();
        AutoRefresh();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Setup
    // ─────────────────────────────────────────────────────────────────────────

    private void SetupLayers()
    {
        int mask = 1 << minimapLayerIndex;
        if (minimapCamera != null) minimapCamera.cullingMask = mask;
        if (mainCamera == null)   mainCamera = Camera.main;
        if (mainCamera != null)   mainCamera.cullingMask &= ~mask;
    }

    private void SetupCamera()
    {
        if (minimapCamera == null) return;
        minimapCamera.orthographic     = true;
        minimapCamera.orthographicSize = minimapCameraSize;
        minimapCamera.clearFlags       = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor  = new Color(0.02f, 0.02f, 0.06f, 1f);
    }

    private void CreateMarkerContainer()
    {
        var go       = new GameObject("_MinimapMarkers");
        _markerContainer = go.transform;
    }

    private void CreatePlayerMarker()
    {
        if (playerTransform == null) return;
        _playerMarkerInstance = playerMarkerPrefab != null
            ? Instantiate(playerMarkerPrefab, playerTransform.position,
                          Quaternion.identity, _markerContainer)
            : CreateDot("PlayerDot", playerMarkerColor, playerMarkerScale);
        if (playerMarkerPrefab == null)
            _playerMarkerInstance.transform.SetParent(_markerContainer);
        SetMinimapLayer(_playerMarkerInstance);
    }

    private void CreateShipwreckMarker()
    {
        if (shipwreckTransform == null) return;
        _shipwreckMarkerInstance = shipwreckMarkerPrefab != null
            ? Instantiate(shipwreckMarkerPrefab, shipwreckTransform.position,
                          Quaternion.identity, _markerContainer)
            : CreateDot("ShipwreckDot", shipwreckMarkerColor, shipwreckMarkerScale);
        if (shipwreckMarkerPrefab == null)
            _shipwreckMarkerInstance.transform.SetParent(_markerContainer);
        SetMinimapLayer(_shipwreckMarkerInstance);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void UnlockMaterialMarkers()
    {
        _markersUnlocked = true;
        RefreshMaterialMarkers();
        NotificationManager.Instance?.ShowInfo("Material locations now visible on minimap.");
    }

    public void RefreshMaterialMarkers()
    {
        foreach (var m in _materialMarkers) if (m != null) Destroy(m);
        _materialMarkers.Clear();

        if (!_markersUnlocked) return;

        foreach (var pickup in FindObjectsByType<MaterialPickup>(FindObjectsSortMode.None))
        {
            var marker = materialMarkerPrefab != null
                ? Instantiate(materialMarkerPrefab, pickup.transform.position,
                              Quaternion.identity, _markerContainer)
                : CreateDot("MatDot", new Color(1f, 0.85f, 0.1f), markerScale);
            if (materialMarkerPrefab == null)
                marker.transform.SetParent(_markerContainer);
            marker.transform.position   = pickup.transform.position;
            marker.transform.localScale = Vector3.one * markerScale;
            SetMinimapLayer(marker);
            _materialMarkers.Add(marker);
        }
    }

    /// <summary>
    /// Called by MeteoriteManager when a gravity rift is created.
    /// Adds a red rift marker on the minimap that lives for lifetimeSeconds.
    /// </summary>
    public void RegisterRift(Vector2 worldPos, float lifetimeSeconds)
    {
        var marker = riftMarkerPrefab != null
            ? Instantiate(riftMarkerPrefab, worldPos, Quaternion.identity, _markerContainer)
            : CreateDot("RiftDot", riftMarkerColor, riftMarkerScale);

        if (riftMarkerPrefab == null)
            marker.transform.SetParent(_markerContainer);

        marker.transform.position   = worldPos;
        marker.transform.localScale = Vector3.one * riftMarkerScale;
        SetMinimapLayer(marker);
        _riftMarkers.Add(marker);

        // Auto-destroy after the rift expires
        Destroy(marker, lifetimeSeconds + 1f);
    }

    /// <summary>
    /// Called by MeteoriteManager when a meteorite is launched.
    /// Tracks the meteorite and shows its position on the minimap until it impacts.
    /// </summary>
    public void RegisterMeteorite(Meteorite meteorite)
    {
        if (meteorite == null) return;

        var marker = meteoriteMarkerPrefab != null
            ? Instantiate(meteoriteMarkerPrefab, meteorite.transform.position,
                          Quaternion.identity, _markerContainer)
            : CreateDot("MeteoriteDot", meteoriteMarkerColor, meteoriteMarkerScale);

        if (meteoriteMarkerPrefab == null)
            marker.transform.SetParent(_markerContainer);

        marker.transform.localScale = Vector3.one * meteoriteMarkerScale;
        SetMinimapLayer(marker);
        _meteoriteMarkers[meteorite] = marker;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LateUpdate helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void FollowPlayer()
    {
        if (minimapCamera == null || playerTransform == null) return;
        Vector3 t = new Vector3(playerTransform.position.x, playerTransform.position.y,
                                minimapCamera.transform.position.z);
        minimapCamera.transform.position = Vector3.Lerp(
            minimapCamera.transform.position, t, followSpeed * Time.deltaTime);
    }

    private void UpdatePlayerMarker()
    {
        if (_playerMarkerInstance == null || playerTransform == null) return;
        _playerMarkerInstance.transform.position = playerTransform.position;
        _playerMarkerInstance.transform.rotation = playerTransform.rotation;
    }

    private void UpdateShipwreckMarker()
    {
        if (_shipwreckMarkerInstance == null || shipwreckTransform == null
            || playerTransform == null) return;

        Vector2 toShip = (Vector2)shipwreckTransform.position
                       - (Vector2)playerTransform.position;
        float dist = toShip.magnitude;

        // If shipwreck is within the minimap view, place marker at its real position
        if (dist <= shipwreckEdgePinRadius)
        {
            _shipwreckMarkerInstance.transform.position = shipwreckTransform.position;
        }
        else
        {
            // Pin to the edge of the minimap view in the direction of the ship
            Vector2 pinPos = (Vector2)playerTransform.position
                           + toShip.normalized * shipwreckEdgePinRadius;
            _shipwreckMarkerInstance.transform.position = pinPos;
        }

        // Rotate the marker to point toward the ship
        float angle = Mathf.Atan2(toShip.y, toShip.x) * Mathf.Rad2Deg;
        _shipwreckMarkerInstance.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private void CleanStaleMeteorites()
    {
        // Remove markers for meteorites that have been destroyed
        var toRemove = new List<Meteorite>();
        foreach (var kv in _meteoriteMarkers)
        {
            if (kv.Key == null) // meteorite was destroyed
            {
                if (kv.Value != null) Destroy(kv.Value);
                toRemove.Add(kv.Key);
            }
            else if (kv.Value != null)
            {
                // Update position while alive
                kv.Value.transform.position = kv.Key.transform.position;
            }
        }
        foreach (var k in toRemove) _meteoriteMarkers.Remove(k);
    }

    private void AutoRefresh()
    {
        if (!_markersUnlocked || autoRefreshInterval <= 0f) return;
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= autoRefreshInterval) { _refreshTimer = 0f; RefreshMaterialMarkers(); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utilities
    // ─────────────────────────────────────────────────────────────────────────

    private void SetMinimapLayer(GameObject go)
    {
        if (go == null) return;
        go.layer = minimapLayerIndex;
        foreach (Transform child in go.transform)
            child.gameObject.layer = minimapLayerIndex;
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
        const int size = 16;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x-c)*(x-c)+(y-c)*(y-c));
            tex.SetPixel(x, y, d <= c ? Color.white : Color.clear);
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), size);
    }
}
