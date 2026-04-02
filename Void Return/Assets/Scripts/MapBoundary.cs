using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Invisible boundary that prevents the player from leaving the map.
///
/// SETUP:
///  1. Create an empty GameObject named 'MapBoundary'.
///  2. Attach this script.
///  3. Set boundaryCenter and boundarySize to cover your full playable area.
///  4. The boundary is enforced every FixedUpdate — no colliders needed.
///
/// The boundary works by clamping the player's position if they exceed the limits
/// and reflecting (damping) their velocity to prevent them from pushing back through.
///
/// Also fires onBoundaryReached so you can show a warning notification.
/// </summary>
public class MapBoundary : MonoBehaviour
{
    [Header("Boundary Shape")]
    [Tooltip("Center of the playable map area in world space.")]
    public Vector2 boundaryCenter = Vector2.zero;

    [Tooltip("Total width and height of the playable area (not half-extents).")]
    public Vector2 boundarySize = new Vector2(240f, 240f);

    [Header("Behavior")]
    [Tooltip("Fraction of velocity reflected back when hitting the boundary wall. " +
             "1.0 = full reflect, 0 = just stop. 0.5 is a soft push back.")]
    [Range(0f, 1f)]
    public float reflectFraction = 0.4f;

    [Tooltip("Warning notification shown when the player reaches the boundary.")]
    public string boundaryWarning = "You cannot survive further out — turn back.";

    [Tooltip("Minimum seconds between boundary warning notifications.")]
    [Range(1f, 10f)]
    public float warningCooldown = 3f;

    [Header("Events")]
    [Tooltip("Fired when the player first hits the boundary.")]
    public UnityEvent onBoundaryReached;

    // ─────────────────────────────────────────────────────────────────────────
    private Transform   _playerTransform;
    private Rigidbody2D _playerRb;
    private float       _lastWarnTime = -999f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) { Debug.LogWarning("[MapBoundary] PlayerController not found."); return; }
        _playerTransform = player.transform;
        _playerRb        = player.GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (_playerTransform == null || _playerRb == null) return;

        Vector2 pos    = _playerTransform.position;
        Vector2 half   = boundarySize * 0.5f;
        Vector2 minBnd = boundaryCenter - half;
        Vector2 maxBnd = boundaryCenter + half;

        bool    hitBoundary = false;
        Vector2 vel         = _playerRb.linearVelocity;

        // X axis
        if (pos.x < minBnd.x)
        {
            pos.x = minBnd.x;
            vel.x = Mathf.Abs(vel.x) * reflectFraction;
            hitBoundary = true;
        }
        else if (pos.x > maxBnd.x)
        {
            pos.x = maxBnd.x;
            vel.x = -Mathf.Abs(vel.x) * reflectFraction;
            hitBoundary = true;
        }

        // Y axis
        if (pos.y < minBnd.y)
        {
            pos.y = minBnd.y;
            vel.y = Mathf.Abs(vel.y) * reflectFraction;
            hitBoundary = true;
        }
        else if (pos.y > maxBnd.y)
        {
            pos.y = maxBnd.y;
            vel.y = -Mathf.Abs(vel.y) * reflectFraction;
            hitBoundary = true;
        }

        if (hitBoundary)
        {
            _playerTransform.position = pos;
            _playerRb.linearVelocity  = vel;

            if (Time.time - _lastWarnTime > warningCooldown)
            {
                _lastWarnTime = Time.time;
                NotificationManager.Instance?.ShowWarning(boundaryWarning);
                onBoundaryReached?.Invoke();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
        Gizmos.DrawCube(boundaryCenter, new Vector3(boundarySize.x, boundarySize.y, 0f));
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
        Gizmos.DrawWireCube(boundaryCenter, new Vector3(boundarySize.x, boundarySize.y, 0f));
    }
}
