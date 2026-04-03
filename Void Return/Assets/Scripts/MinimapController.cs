using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls the minimap camera and all marker overlays.
///
/// FIX — SHIPWRECK MARKER MISSING:
///  CreateShipwreckMarker() is now called explicitly and logs to the Console
///  if shipwreckTransform is null (the most common reason the marker doesn't appear).
///
/// FIX — ZONE REPORTING TO METEORITEMANAGER:
///  The player's current zone number is forwarded to MeteoriteManager each time
///  the minimap detects the player (via ZoneTrigger calling back here).
///  Actually zone forwarding is done directly in ZoneTrigger — see that script.
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
    [Tooltip("Layer index of the 'Minimap' layer (Edit > Project Settings > Tags and Layers). Usually 8.")]
    public int minimapLayerIndex = 8;

    [Header("Material Markers")]
    public GameObject materialMarkerPrefab;
    public bool markersUnlockedFromStart = false;
    [Range(0f, 30f)] public float autoRefreshInterval = 5f;

    [Header("Rift Markers")]
    public GameObject riftMarkerPrefab;
    public Color riftMarkerColor = new Color(1f, 0.15f, 0.1f);

    [Header("Meteorite Markers")]
    public GameObject meteoriteMarkerPrefab;
    public Color meteoriteMarkerColor = new Color(1f, 0.5f, 0f);

    [Header("Player Marker")]
    public GameObject playerMarkerPrefab;
    public Color playerMarkerColor = new Color(1f, 0.3f, 0.2f);

    [Header("Shipwreck Direction Marker")]
    [Tooltip("REQUIRED: Drag the LifeSupport repair point Transform here. " +
             "Without this, the shipwreck arrow will not appear on the minimap.")]
    public Transform shipwreckTransform;

    public GameObject shipwreckMarkerPrefab;
    public Color      shipwreckMarkerColor    = new Color(0.2f, 0.7f, 1f);
    public float      shipwreckEdgePinRadius  = 55f;

    [Header("Marker Sizes (world units)")]
    public float markerScale          = 8f;
    public float playerMarkerScale    = 10f;
    public float riftMarkerScale      = 12f;
    public float meteoriteMarkerScale = 9f;
    public float shipwreckMarkerScale = 11f;

    // ─────────────────────────────────────────────────────────────────────────
    private bool       _markersUnlocked;
    private GameObject _playerMarkerInstance;
    private GameObject _shipwreckMarkerInstance;
    private float      _refreshTimer;
    private Transform  _markerContainer;

    private readonly List<GameObject>                   _materialMarkers  = new();
    private readonly List<GameObject>                   _riftMarkers      = new();
    private readonly Dictionary<Meteorite, GameObject>  _meteoriteMarkers = new();

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
        CreateShipwreckMarker();      // Now logs clearly if missing
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
        if (mainCamera == null)    mainCamera = Camera.main;
        if (mainCamera != null)    mainCamera.cullingMask &= ~mask;
    }

    private void SetupCamera()
    {
        if (minimapCamera == null) { Debug.LogWarning("[MinimapController] minimapCamera not assigned."); return; }
        minimapCamera.orthographic     = true;
        minimapCamera.orthographicSize = minimapCameraSize;
        minimapCamera.clearFlags       = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor  = new Color(0.02f, 0.02f, 0.06f, 1f);
    }

    private void CreateMarkerContainer()
    {
        _markerContainer = new GameObject("_MinimapMarkers").transform;
    }

    private void CreatePlayerMarker()
    {
        if (playerTransform == null) return;
        _playerMarkerInstance = playerMarkerPrefab != null
            ? Instantiate(playerMarkerPrefab, playerTransform.position, Quaternion.identity, _markerContainer)
            : CreateDot("PlayerDot", playerMarkerColor, playerMarkerScale);
        if (playerMarkerPrefab == null)
            _playerMarkerInstance.transform.SetParent(_markerContainer);
        SetMinimapLayer(_playerMarkerInstance);
    }

    private void CreateShipwreckMarker()
    {
        if (shipwreckTransform == null)
        {
            Debug.LogWarning("[MinimapController] shipwreckTransform is NOT assigned. " +
                             "The shipwreck direction arrow will not appear on the minimap. " +
                             "Select the MinimapController in the Inspector and drag the " +
                             "LifeSupport repair point Transform into the 'Shipwreck Transform' field.");
            return;
        }

        _shipwreckMarkerInstance = shipwreckMarkerPrefab != null
            ? Instantiate(shipwreckMarkerPrefab, shipwreckTransform.position,
                          Quaternion.identity, _markerContainer)
            : CreateDot("ShipwreckDot", shipwreckMarkerColor, shipwreckMarkerScale);

        if (shipwreckMarkerPrefab == null)
            _shipwreckMarkerInstance.transform.SetParent(_markerContainer);

        SetMinimapLayer(_shipwreckMarkerInstance);
        Debug.Log($"[MinimapController] Shipwreck marker created at {shipwreckTransform.position}");
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
            if (materialMarkerPrefab == null) marker.transform.SetParent(_markerContainer);
            marker.transform.position   = pickup.transform.position;
            marker.transform.localScale = Vector3.one * markerScale;
            SetMinimapLayer(marker);
            _materialMarkers.Add(marker);
        }
    }

    public void RegisterRift(Vector2 worldPos, float lifetimeSeconds)
    {
        var marker = riftMarkerPrefab != null
            ? Instantiate(riftMarkerPrefab, worldPos, Quaternion.identity, _markerContainer)
            : CreateDot("RiftDot", riftMarkerColor, riftMarkerScale);
        if (riftMarkerPrefab == null) marker.transform.SetParent(_markerContainer);
        marker.transform.position   = worldPos;
        marker.transform.localScale = Vector3.one * riftMarkerScale;
        SetMinimapLayer(marker);
        _riftMarkers.Add(marker);
        Destroy(marker, lifetimeSeconds + 1f);
    }

    public void RegisterMeteorite(Meteorite meteorite)
    {
        if (meteorite == null) return;
        var marker = meteoriteMarkerPrefab != null
            ? Instantiate(meteoriteMarkerPrefab, meteorite.transform.position,
                          Quaternion.identity, _markerContainer)
            : CreateDot("MeteoriteDot", meteoriteMarkerColor, meteoriteMarkerScale);
        if (meteoriteMarkerPrefab == null) marker.transform.SetParent(_markerContainer);
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

        Vector2 toShip = (Vector2)shipwreckTransform.position - (Vector2)playerTransform.position;
        float   dist   = toShip.magnitude;

        Vector2 markerPos = dist <= shipwreckEdgePinRadius
            ? (Vector2)shipwreckTransform.position
            : (Vector2)playerTransform.position + toShip.normalized * shipwreckEdgePinRadius;

        _shipwreckMarkerInstance.transform.position = markerPos;

        float angle = Mathf.Atan2(toShip.y, toShip.x) * Mathf.Rad2Deg;
        _shipwreckMarkerInstance.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private void CleanStaleMeteorites()
    {
        var toRemove = new List<Meteorite>();
        foreach (var kv in _meteoriteMarkers)
        {
            if (kv.Key == null)
            { if (kv.Value != null) Destroy(kv.Value); toRemove.Add(kv.Key); }
            else if (kv.Value != null)
                kv.Value.transform.position = kv.Key.transform.position;
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
