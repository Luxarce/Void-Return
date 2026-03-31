using UnityEngine;

/// <summary>
/// Smooth camera follow for the main game camera.
/// Follows the player with configurable smoothing and optional position clamping.
///
/// Attach to the main Camera GameObject in the GameScene.
/// Drag the Player's Transform into the 'target' field in the Inspector.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Follow Target")]
    [Tooltip("The Transform the camera will follow. Drag the Player GameObject here.")]
    public Transform target;

    [Header("Follow Settings")]
    [Tooltip("How quickly the camera catches up to the player. Higher = snappier, lower = smoother.")]
    [Range(1f, 20f)]
    public float smoothSpeed = 6f;

    [Tooltip("Offset applied to the camera position relative to the player. " +
             "Use Z = -10 to ensure the camera renders the 2D scene correctly.")]
    public Vector3 offset = new Vector3(0f, 0.5f, -10f);

    [Header("Look-Ahead (Optional)")]
    [Tooltip("If true, the camera looks slightly ahead in the player's movement direction.")]
    public bool enableLookAhead = true;

    [Tooltip("How far ahead the camera looks in the movement direction.")]
    [Range(0f, 5f)]
    public float lookAheadDistance = 2f;

    [Tooltip("How quickly the look-ahead offset adjusts to player velocity.")]
    [Range(1f, 15f)]
    public float lookAheadSpeed = 4f;

    [Header("Bounds Clamping (Optional)")]
    [Tooltip("If true, the camera position is clamped to the defined world-space bounds.")]
    public bool useBounds = false;

    [Tooltip("Minimum X and Y world-space position the camera center can reach.")]
    public Vector2 boundsMin = new Vector2(-50f, -50f);

    [Tooltip("Maximum X and Y world-space position the camera center can reach.")]
    public Vector2 boundsMax = new Vector2(50f, 50f);

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Vector3 _velocity        = Vector3.zero;
    private Vector2 _lookAheadOffset = Vector2.zero;
    private Rigidbody2D _targetRb;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (target != null)
            _targetRb = target.GetComponent<Rigidbody2D>();

        // Snap to target on start (no lerp)
        if (target != null)
            transform.position = target.position + offset;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Update look-ahead offset based on player velocity
        if (enableLookAhead && _targetRb != null)
        {
            Vector2 vel = _targetRb.linearVelocity.normalized;
            _lookAheadOffset = Vector2.Lerp(
                _lookAheadOffset,
                vel * lookAheadDistance,
                lookAheadSpeed * Time.deltaTime
            );
        }

        // Compute desired position
        Vector3 desired = target.position + offset +
                          new Vector3(_lookAheadOffset.x, _lookAheadOffset.y, 0f);

        // Smooth damp for buttery following
        Vector3 smoothed = Vector3.SmoothDamp(
            transform.position, desired, ref _velocity,
            1f / smoothSpeed, float.PositiveInfinity, Time.deltaTime
        );

        // Optional clamping to world bounds
        if (useBounds)
        {
            smoothed.x = Mathf.Clamp(smoothed.x, boundsMin.x, boundsMax.x);
            smoothed.y = Mathf.Clamp(smoothed.y, boundsMin.y, boundsMax.y);
        }

        // Always preserve Z from offset to keep 2D camera correct
        smoothed.z = offset.z;
        transform.position = smoothed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (!useBounds) return;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Vector3 center = new Vector3(
            (boundsMin.x + boundsMax.x) * 0.5f,
            (boundsMin.y + boundsMax.y) * 0.5f,
            0f
        );
        Vector3 size = new Vector3(
            boundsMax.x - boundsMin.x,
            boundsMax.y - boundsMin.y,
            0f
        );
        Gizmos.DrawWireCube(center, size);
    }
}
