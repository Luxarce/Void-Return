using UnityEngine;

/// <summary>
/// Creates a parallax scrolling effect for background layers.
/// Each background layer moves at a fraction of the camera's movement
/// to simulate depth in the 2D space environment.
///
/// SETUP:
/// 1. Create 2-3 background GameObjects (each with a SpriteRenderer).
///    Example: Background_Stars (farthest), Background_Nebula (mid), Background_Dust (closest)
/// 2. Attach this script to each background layer GameObject.
/// 3. Assign the main Camera to the 'cameraTransform' field.
/// 4. Set 'parallaxFactor':
///       0.0 = no movement (fixed in place)
///       0.1 = very slow / farthest layer (stars)
///       0.3 = medium / nebula layer
///       0.6 = faster / closest dust layer
///       1.0 = moves exactly with the camera (no parallax)
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Camera Reference")]
    [Tooltip("The main game camera. Leave empty to auto-find Camera.main on Start.")]
    public Transform cameraTransform;

    [Header("Parallax Settings")]
    [Tooltip("How much this layer moves relative to the camera.\n" +
             "0 = completely fixed (no movement)\n" +
             "0.1 = very slow, farthest layer (e.g., stars)\n" +
             "0.5 = medium (e.g., nebula)\n" +
             "1.0 = matches camera exactly (no parallax effect)")]
    [Range(0f, 1f)]
    public float parallaxFactor = 0.1f;

    [Tooltip("If true, apply parallax on both X and Y axes (good for Zero-G space). " +
             "If false, only X axis parallax is applied (standard side-scroller).")]
    public bool parallaxOnBothAxes = true;

    [Header("Infinite Tiling")]
    [Tooltip("If true, the sprite tiles infinitely so no empty space is visible. " +
             "Requires the SpriteRenderer's Draw Mode set to Tiled.")]
    public bool infiniteTiling = false;

    [Tooltip("Size of one tile for infinite scrolling. Should match sprite dimensions.")]
    public Vector2 tileSize = new Vector2(20f, 11.25f);

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Vector3 _lastCameraPosition;
    private Vector3 _startPosition;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
            _lastCameraPosition = cameraTransform.position;

        _startPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        Vector3 cameraDelta = cameraTransform.position - _lastCameraPosition;

        float moveX = cameraDelta.x * parallaxFactor;
        float moveY = parallaxOnBothAxes ? cameraDelta.y * parallaxFactor : 0f;

        transform.position += new Vector3(moveX, moveY, 0f);

        _lastCameraPosition = cameraTransform.position;

        // Infinite tiling: re-center the background when it drifts too far
        if (infiniteTiling)
        {
            float distX = cameraTransform.position.x - transform.position.x;
            float distY = cameraTransform.position.y - transform.position.y;

            if (Mathf.Abs(distX) >= tileSize.x)
                transform.position += new Vector3(
                    Mathf.Sign(distX) * tileSize.x, 0f, 0f);

            if (parallaxOnBothAxes && Mathf.Abs(distY) >= tileSize.y)
                transform.position += new Vector3(
                    0f, Mathf.Sign(distY) * tileSize.y, 0f);
        }
    }
}
