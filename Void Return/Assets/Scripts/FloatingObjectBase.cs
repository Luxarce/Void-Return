using UnityEngine;

/// <summary>
/// Shared physics setup for all floating objects (debris, materials, grenades, meteorites).
/// Attach to any Rigidbody2D object that should:
///  — Float in zero-G without gravity (gravityScale = 0)
///  — Collide properly with ground and walls (not clip through)
///  — Use Continuous collision detection to prevent tunneling at speed
///
/// This is a lightweight utility component. It only configures the Rigidbody2D
/// in Awake — it has no Update overhead.
///
/// WHICH OBJECTS NEED THIS:
///  MaterialPickup prefabs  — prevent clipping into ground after launch
///  DebrisObject prefabs    — prevent fast debris from tunneling through walls
///  GravityGrenade prefab   — prevent grenade from passing through thin platforms
///
/// The MaterialPickup script handles its own setup, so you only need this
/// on objects that DON'T already have MaterialPickup.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FloatingObjectBase : MonoBehaviour
{
    [Header("Floating Physics")]
    [Tooltip("Linear drag applied on Awake. Higher = settles faster.")]
    public float linearDrag   = 1f;

    [Tooltip("Angular drag for tumble slowdown.")]
    public float angularDrag  = 0.8f;

    [Tooltip("If ON, a PhysicsMaterial2D with the given bounciness is created and " +
             "applied to all Collider2D components on this object.")]
    public bool  applyBounceMaterial = true;

    [Tooltip("Bounciness of the physics material (0 = no bounce, 1 = full bounce).")]
    [Range(0f, 1f)]
    public float bounciness = 0.2f;

    [Tooltip("Friction of the physics material.")]
    [Range(0f, 1f)]
    public float friction = 0.4f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        var rb                     = GetComponent<Rigidbody2D>();
        rb.gravityScale            = 0f;
        rb.linearDamping           = linearDrag;
        rb.angularDamping          = angularDrag;
        rb.collisionDetectionMode  = CollisionDetectionMode2D.Continuous;

        if (applyBounceMaterial)
        {
            var mat = new PhysicsMaterial2D("FloatBounce")
            {
                bounciness = this.bounciness,
                friction   = this.friction
            };

            foreach (var col in GetComponents<Collider2D>())
                if (!col.isTrigger)
                    col.sharedMaterial = mat;
        }
    }
}
