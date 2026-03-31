using UnityEngine;

/// <summary>
/// Defines the gravity state type for a zone.
/// </summary>
[System.Serializable]
public enum GravityState
{
    Normal,
    ZeroG,
    MicroPull,
    GravityRift
}

/// <summary>
/// Attach to any GameObject with a Trigger Collider2D to define a gravity zone.
/// The zone applies a custom gravity force and state to the PlayerController when inside.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class GravityZone : MonoBehaviour
{
    [Header("Gravity Settings")]
    [Tooltip("The type of gravity this zone applies to the player.")]
    public GravityState gravityType = GravityState.ZeroG;

    [Tooltip("Strength of the gravity pull. Positive = pull in gravity direction, Negative = push away.")]
    [Range(-30f, 30f)]
    public float gravityStrength = 9.8f;

    [Tooltip("Direction of the gravitational pull. Will be normalized automatically.")]
    public Vector2 gravityDirection = Vector2.down;

    [Tooltip("If true, the player will be spun/disoriented while in this zone (for Rift zones).")]
    public bool causesDisorientation = false;

    [Tooltip("Degrees per second the player spins when disoriented. Only active if causesDisorientation is true.")]
    [Range(0f, 360f)]
    public float riftSpinForce = 180f;

    [Header("Visual (Scene View)")]
    [Tooltip("Color of the gizmo boundary drawn in the Scene view. Use to distinguish zone types visually.")]
    public Color zoneGizmoColor = new Color(0f, 0.8f, 1f, 0.25f);

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.TryGetComponent<PlayerController>(out var pc)) return;

        pc.ApplyZoneGravity(
            gravityType,
            gravityDirection.normalized * gravityStrength,
            causesDisorientation,
            riftSpinForce
        );
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.TryGetComponent<PlayerController>(out var pc)) return;

        // When leaving, reset to Zero-G (open space default)
        pc.ApplyZoneGravity(GravityState.ZeroG, Vector2.zero, false, 0f);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = zoneGizmoColor;

        var circle = GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            Gizmos.DrawWireSphere(transform.position + (Vector3)(Vector2)circle.offset, circle.radius);
            return;
        }

        var box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.DrawWireCube(transform.position + (Vector3)(Vector2)box.offset, box.size);
        }
    }
}
