using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls the minimap camera and material marker overlays.
///
/// FIX NOTES:
///  — minimapCameraSize is now exposed in the Inspector with a default of 60.
///    Previously the size was hardcoded as a comment suggestion and never
///    actually set on the camera — it stayed at the Unity default of 5.
///  — RefreshMaterialMarkers now runs in Start() regardless of lock state
///    when markersUnlockedFromStart is true.
///  — Added autoRefreshInterval: markers rebuild on a timer so newly spawned
///    pickups (e.g. from meteorite drops) appear on the map automatically.
///  — MarkerContainer is created automatically if left unassigned.
///  — Player marker now uses a red/arrow-colored dot by default (SpriteRenderer
///    on a child object) when no prefab is assigned.
///  — Camera orthographic size and background color are set from script in
///    Start() using the Inspector values, fixing the "too small" issue.
///
/// SETUP:
///  1. Create a second Camera GameObject named 'MinimapCamera'.
///     Set its Z position to -10 (or any value behind the scene).
///  2. Set Projection: Orthographic. DO NOT set the size here — this script sets it.
///  3. Create a RenderTexture: Assets → Create → Render Texture. Size: 256×256.
///  4. Drag the RenderTexture into MinimapCamera's Target Texture field.
///  5. In the UI Canvas, add a RawImage. Assign the RenderTexture to its Texture.
///     Resize the RawImage to your desired minimap display size (e.g., 200×200).
///  6. Add a circular Mask component over the RawImage if desired.
///  7. Attach this script to MinimapCamera (or any persistent Manager GameObject).
///  8. Assign minimapCamera and playerTransform in the Inspector.
/// </summary>
public class MinimapController : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Drag the MinimapCamera here.")]
    public Camera minimapCamera;

    [Tooltip("Orthographic size of the minimap camera. " +
             "Larger = more of the world is visible. Default 60 shows a wide area.")]
    public float minimapCameraSize = 60f;

    [Tooltip("The player's Transform — the minimap camera follows this.")]
    public Transform playerTransform;

    [Tooltip("How smoothly the minimap camera follows the player.")]
    [Range(1f, 20f)]
    public float followSpeed = 8f;

    [Header("Material Markers")]
    [Tooltip("Prefab used to mark material pickup positions on the minimap. " +
             "Should be a small colored sprite (e.g., a 16×16 yellow dot) " +
             "visible on the minimap camera's culling layer.")]
    public GameObject materialMarkerPrefab;

    [Tooltip("Parent transform for all marker instances. " +
             "If left empty, one is created automatically at runtime.")]
    public Transform markerContainer;

    [Tooltip("If true, material markers are visible immediately without Navigation repair.")]
    public bool markersUnlockedFromStart = false;

    [Tooltip("Seconds between automatic marker refreshes. " +
             "This ensures newly dropped materials (e.g. from meteorites) appear on the map. " +
             "Set to 0 to disable auto-refresh.")]
    [Range(0f, 30f)]
    public float autoRefreshInterval = 5f;

    [Header("Player Marker")]
    [Tooltip("Prefab for the player dot on the minimap. " +
             "If left empty, a simple colored dot is created automatically.")]
    public GameObject playerMarkerPrefab;

    [Tooltip("Color of the auto-generated player dot (used when playerMarkerPrefab is empty).")]
    public Color playerMarkerColor = Color.red;

    // ─────────────────────────────────────────────────────────────────────────
    private bool        _markersUnlocked;
    private GameObject  _playerMarkerInstance;
    private float       _refreshTimer;
    private readonly List<GameObject> _activeMarkers = new();

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        _markersUnlocked = markersUnlockedFromStart;

        SetupCamera();
        SetupMarkerContainer();
        CreatePlayerMarker();

        if (_markersUnlocked)
            RefreshMaterialMarkers();
    }

    private void LateUpdate()
    {
        FollowPlayer();
        UpdatePlayerMarkerTransform();
        AutoRefreshMarkers();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Setup
    // ─────────────────────────────────────────────────────────────────────────

    private void SetupCamera()
    {
        if (minimapCamera == null)
        {
            Debug.LogWarning("[MinimapController] minimapCamera is not assigned. " +
                             "Drag your MinimapCamera into this field.");
            return;
        }

        // Apply the orthographic size from Inspector — fixes the "too small" bug
        minimapCamera.orthographic     = true;
        minimapCamera.orthographicSize = minimapCameraSize;

        // Dark background so the minimap has contrast
        minimapCamera.backgroundColor  = new Color(0.02f, 0.02f, 0.05f, 1f);
        minimapCamera.clearFlags       = CameraClearFlags.SolidColor;
    }

    private void SetupMarkerContainer()
    {
        if (markerContainer != null) return;

        // Auto-create a container so markers have a clean parent
        var go        = new GameObject("MinimapMarkers");
        markerContainer = go.transform;
    }

    private void CreatePlayerMarker()
    {
        if (playerTransform == null) return;

        if (playerMarkerPrefab != null)
        {
            _playerMarkerInstance = Instantiate(playerMarkerPrefab,
                                                playerTransform.position,
                                                Quaternion.identity,
                                                markerContainer);
        }
        else
        {
            // Auto-generate a simple colored dot
            _playerMarkerInstance = new GameObject("PlayerMinimapDot");
            _playerMarkerInstance.transform.SetParent(markerContainer);
            _playerMarkerInstance.transform.position = playerTransform.position;
            _playerMarkerInstance.transform.localScale = Vector3.one * 2f;

            var sr   = _playerMarkerInstance.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDotSprite();
            sr.color  = playerMarkerColor;
            sr.sortingOrder = 10;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LateUpdate helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void FollowPlayer()
    {
        if (minimapCamera == null || playerTransform == null) return;

        Vector3 target = new Vector3(
            playerTransform.position.x,
            playerTransform.position.y,
            minimapCamera.transform.position.z);

        minimapCamera.transform.position = Vector3.Lerp(
            minimapCamera.transform.position,
            target,
            followSpeed * Time.deltaTime);
    }

    private void UpdatePlayerMarkerTransform()
    {
        if (_playerMarkerInstance == null || playerTransform == null) return;
        _playerMarkerInstance.transform.position = playerTransform.position;
        // Optionally rotate to match player facing
        _playerMarkerInstance.transform.rotation = Quaternion.Euler(0f, 0f,
            playerTransform.eulerAngles.z);
    }

    private void AutoRefreshMarkers()
    {
        if (!_markersUnlocked || autoRefreshInterval <= 0f) return;

        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= autoRefreshInterval)
        {
            _refreshTimer = 0f;
            RefreshMaterialMarkers();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reveals material markers on the minimap.
    /// Called by ShipRepairManager when Navigation is repaired.
    /// </summary>
    public void UnlockMaterialMarkers()
    {
        _markersUnlocked = true;
        RefreshMaterialMarkers();
        NotificationManager.Instance?.Show("Material locations now visible on minimap.");
    }

    /// <summary>
    /// Destroys all existing markers and rebuilds from all active MaterialPickup objects.
    /// </summary>
    public void RefreshMaterialMarkers()
    {
        foreach (var m in _activeMarkers)
            if (m != null) Destroy(m);
        _activeMarkers.Clear();

        if (!_markersUnlocked || materialMarkerPrefab == null) return;

        var pickups = FindObjectsByType<MaterialPickup>(FindObjectsSortMode.None);

        foreach (var pickup in pickups)
        {
            var marker = Instantiate(materialMarkerPrefab,
                                     pickup.transform.position,
                                     Quaternion.identity,
                                     markerContainer);
            _activeMarkers.Add(marker);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sprite generator for the auto-created player dot
    // ─────────────────────────────────────────────────────────────────────────

    private static Sprite CreateDotSprite()
    {
        // Creates a tiny 8×8 white circle texture programmatically
        int size    = 8;
        var tex     = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f - 0.5f;
        float radius = size / 2f - 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
            tex.SetPixel(x, y, d <= radius ? Color.white : Color.clear);
        }
        tex.Apply();

        return Sprite.Create(tex,
                             new Rect(0, 0, size, size),
                             new Vector2(0.5f, 0.5f),
                             size);
    }
}
