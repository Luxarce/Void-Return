using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls the minimap camera and material marker overlays.
/// Material markers are hidden until the Navigation module is repaired.
///
/// SETUP:
/// 1. Create a second Camera in the scene named 'MinimapCamera'.
/// 2. Position it above the scene (e.g., Z = -5), large Orthographic Size (e.g., 50).
/// 3. Set its Output Texture to a RenderTexture (Assets → Create → Render Texture, 512×512).
/// 4. In the UI Canvas, add a RawImage and assign that RenderTexture to it.
/// 5. Attach this script to any Manager GameObject.
/// 6. Assign the references in the Inspector.
/// </summary>
public class MinimapController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Minimap Camera")]
    [Tooltip("The secondary orthographic camera that renders the minimap view.")]
    public Camera minimapCamera;

    [Tooltip("The player's Transform. The minimap camera will follow this.")]
    public Transform playerTransform;

    [Tooltip("How smoothly the minimap camera follows the player.")]
    [Range(1f, 20f)]
    public float followSpeed = 10f;

    [Header("Material Markers")]
    [Tooltip("Prefab spawned on the minimap layer at each MaterialPickup location. " +
             "Use a small colored dot sprite on the 'Minimap' layer.")]
    public GameObject materialMarkerPrefab;

    [Tooltip("Parent transform (empty GameObject) that holds all marker instances.")]
    public Transform markerContainer;

    [Tooltip("If true, material markers are visible from the start (no Navigation required). " +
             "Leave false for normal gameplay.")]
    public bool markersUnlockedFromStart = false;

    [Header("Player Marker")]
    [Tooltip("Prefab for the player's own position marker on the minimap (arrow or dot).")]
    public GameObject playerMarkerPrefab;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private bool                _markersUnlocked;
    private readonly List<GameObject> _activeMarkers = new();
    private GameObject          _playerMarkerInstance;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        _markersUnlocked = markersUnlockedFromStart;

        // Create the player marker
        if (playerMarkerPrefab != null && playerTransform != null)
        {
            _playerMarkerInstance = Instantiate(
                playerMarkerPrefab,
                playerTransform.position,
                Quaternion.identity,
                markerContainer
            );
        }

        if (_markersUnlocked)
            RefreshMaterialMarkers();
    }

    private void LateUpdate()
    {
        // Follow player with minimap camera
        if (minimapCamera != null && playerTransform != null)
        {
            Vector3 target = new Vector3(
                playerTransform.position.x,
                playerTransform.position.y,
                minimapCamera.transform.position.z
            );

            minimapCamera.transform.position = Vector3.Lerp(
                minimapCamera.transform.position,
                target,
                followSpeed * Time.deltaTime
            );
        }

        // Update player marker position
        if (_playerMarkerInstance != null && playerTransform != null)
        {
            _playerMarkerInstance.transform.position = playerTransform.position;
            _playerMarkerInstance.transform.rotation = playerTransform.rotation;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reveals all material pickup locations on the minimap.
    /// Called by ShipRepairManager when the Navigation module is repaired.
    /// </summary>
    public void UnlockMaterialMarkers()
    {
        _markersUnlocked = true;
        RefreshMaterialMarkers();
        NotificationManager.Instance?.Show("Material locations now visible on minimap.");
    }

    /// <summary>
    /// Rebuilds all material markers. Call after pickups are spawned or destroyed.
    /// </summary>
    public void RefreshMaterialMarkers()
    {
        // Clear old markers
        foreach (var marker in _activeMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        _activeMarkers.Clear();

        if (!_markersUnlocked || materialMarkerPrefab == null) return;

        // Find all active pickups in the scene and place a marker at each
        var pickups = FindObjectsByType<MaterialPickup>(FindObjectsSortMode.None);

        foreach (var pickup in pickups)
        {
            GameObject marker = Instantiate(
                materialMarkerPrefab,
                pickup.transform.position,
                Quaternion.identity,
                markerContainer
            );

            // Optionally sync marker color to material type
            // (implement icon/color assignment here if needed)

            _activeMarkers.Add(marker);
        }
    }
}
