using UnityEngine;

/// <summary>
/// A specialized pickup for Oxygen Canisters.
/// Restores oxygen directly to the OxygenSystem on the player
/// and plays appropriate feedback.
///
/// Different from MaterialPickup because it has an immediate
/// survival effect rather than being stored in inventory.
///
/// Attach to the Oxygen Canister prefab.
/// Requires: CircleCollider2D (Is Trigger = true), Rigidbody2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class OxygenCanisterPickup : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Canister Settings")]
    [Tooltip("How much oxygen (in seconds) this canister restores to the player. " +
             "Overrides the default canisterRestoreAmount on OxygenSystem if > 0.")]
    [Range(0f, 120f)]
    public float oxygenRestoreOverride = 0f;

    [Tooltip("Also add this canister to the inventory as a material item (optional).")]
    public bool addToInventory = false;

    [Header("Magnet")]
    [Tooltip("Distance at which the canister flies toward the player.")]
    public float magnetRadius = 2.5f;

    [Tooltip("Speed at which the canister flies to the player once magnetized.")]
    public float magnetSpeed = 6f;

    [Header("VFX & Audio")]
    [Tooltip("Particle effect spawned on pickup.")]
    public GameObject pickupVFX;

    [Tooltip("AudioSource on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("Sound played when collected.")]
    public AudioClip pickupClip;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Transform   _playerTransform;
    private Rigidbody2D _rb;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb              = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
    }

    private void Start()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) _playerTransform = player.transform;
    }

    private void FixedUpdate()
    {
        if (_playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        if (dist <= magnetRadius)
        {
            Vector2 dir = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * magnetSpeed;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var oxygenSys = other.GetComponent<OxygenSystem>();
        if (oxygenSys != null)
        {
            // Use override amount if specified, else use OxygenSystem's default
            if (oxygenRestoreOverride > 0f)
            {
                float saved = oxygenSys.canisterRestoreAmount;
                oxygenSys.canisterRestoreAmount = oxygenRestoreOverride;
                oxygenSys.CollectCanister();
                oxygenSys.canisterRestoreAmount = saved;
            }
            else
            {
                oxygenSys.CollectCanister();
            }
        }

        if (addToInventory)
            Inventory.Instance?.AddItem(MaterialType.OxygenCanister, 1);

        NotificationManager.Instance?.Show("Oxygen replenished!");

        if (pickupVFX != null)
            Instantiate(pickupVFX, transform.position, Quaternion.identity);

        if (pickupClip != null)
            AudioSource.PlayClipAtPoint(pickupClip, transform.position);

        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, magnetRadius);
    }
}
