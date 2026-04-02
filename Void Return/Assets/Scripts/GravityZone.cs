using UnityEngine;

/// <summary>
/// Gravity zone types and their behaviors:
///
///  ZeroG      — slowly PUSHES the player away from the zone center.
///               Simulates an outward repulsion field.
///
///  MicroPull  — slowly PULLS the player toward the zone center.
///               Light attraction — player can still move freely.
///
///  GravityRift— strongly PULLS the player inward AND spins them.
///               Pull is strong but not inescapable — player can slowly fight out.
///
///  Normal     — applies directional gravity (like a floor).
///               Used near debris/asteroid surfaces for walking.
/// </summary>
public enum GravityState { Normal, ZeroG, MicroPull, GravityRift }

[RequireComponent(typeof(Collider2D))]
public class GravityZone : MonoBehaviour
{
    [Header("Zone Type")]
    public GravityState gravityType = GravityState.ZeroG;

    [Header("Normal Zone Settings (applies only when gravityType = Normal)")]
    [Tooltip("Direction of gravity for Normal zones (e.g., toward a floor surface).")]
    public Vector2 gravityDirection  = Vector2.down;

    [Tooltip("Strength of the Normal gravity force per FixedUpdate.")]
    [Range(0f, 30f)]
    public float gravityStrength = 9.8f;

    [Header("ZeroG Zone Settings")]
    [Tooltip("How strongly the ZeroG zone pushes the player away from its center.")]
    [Range(0f, 20f)]
    public float zeroGPushStrength = 2f;

    [Header("MicroPull Zone Settings")]
    [Tooltip("How strongly the MicroPull zone pulls the player toward its center.")]
    [Range(0f, 20f)]
    public float microPullStrength = 3f;

    [Header("Rift Zone Settings")]
    [Tooltip("How strongly the Gravity Rift pulls the player inward. " +
             "Keep below the player's maximum thrust force so they can escape slowly.")]
    [Range(0f, 30f)]
    public float riftPullStrength   = 12f;

    [Tooltip("Degrees per second the player spins inside a Gravity Rift.")]
    [Range(0f, 360f)]
    public float riftSpinForce = 120f;

    [Tooltip("If true, the player receives the disoriented animation state inside a rift.")]
    public bool causesDisorientation = true;

    [Header("Scene View Gizmo")]
    [Tooltip("Color of the zone boundary in the Scene view.")]
    public Color zoneGizmoColor = new Color(0f, 0.8f, 1f, 0.25f);

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.TryGetComponent<PlayerController>(out var pc)) return;

        Vector2 forceVector;
        bool    disorient = false;
        float   spin      = 0f;

        switch (gravityType)
        {
            case GravityState.Normal:
                // Standard directional gravity toward a surface
                forceVector = gravityDirection.normalized * gravityStrength;
                break;

            case GravityState.ZeroG:
                // Push the player AWAY from the zone center (repulsion)
                Vector2 awayDir = ((Vector2)other.transform.position
                                  - (Vector2)transform.position);
                if (awayDir.sqrMagnitude < 0.01f) awayDir = Vector2.up;
                forceVector = awayDir.normalized * zeroGPushStrength;
                break;

            case GravityState.MicroPull:
                // Pull the player gently TOWARD the zone center
                Vector2 toCenter = ((Vector2)transform.position
                                   - (Vector2)other.transform.position);
                if (toCenter.sqrMagnitude < 0.01f) { forceVector = Vector2.zero; break; }
                forceVector = toCenter.normalized * microPullStrength;
                break;

            case GravityState.GravityRift:
                // Strong inward pull + spin — player can still escape with effort
                Vector2 toCenterRift = ((Vector2)transform.position
                                       - (Vector2)other.transform.position);
                if (toCenterRift.sqrMagnitude < 0.01f) { forceVector = Vector2.zero; break; }
                forceVector = toCenterRift.normalized * riftPullStrength;
                disorient   = causesDisorientation;
                spin        = riftSpinForce;
                break;

            default:
                forceVector = Vector2.zero;
                break;
        }

        pc.ApplyZoneGravity(gravityType, forceVector, disorient, spin);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.TryGetComponent<PlayerController>(out var pc)) return;
        // Reset to open-space default on exit
        pc.ApplyZoneGravity(GravityState.ZeroG, Vector2.zero, false, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = zoneGizmoColor;
        var col = GetComponent<Collider2D>();
        if (col is CircleCollider2D cc)
            Gizmos.DrawWireSphere(transform.position, cc.radius * transform.lossyScale.x);
        else if (col is BoxCollider2D bc)
            Gizmos.DrawWireCube(transform.position,
                new Vector3(bc.size.x * transform.lossyScale.x,
                            bc.size.y * transform.lossyScale.y, 0f));
    }
}
