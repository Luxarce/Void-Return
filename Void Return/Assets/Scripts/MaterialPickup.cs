using UnityEngine;

/// <summary>
/// Collectable material pickup.
///
/// FIXES:
///  — No more recoil: when the player collides with a material,
///    the material's velocity is zeroed and the Rigidbody is set to
///    Kinematic BEFORE Destroy so it cannot push the player.
///  — Glow pulse: a pulsing SpriteRenderer color animation runs each frame
///    so materials are visually noticeable even from a distance.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class MaterialPickup : MonoBehaviour
{
    [Header("Material Info")]
    public MaterialType materialType;
    [Range(1, 10)] public int quantity = 1;
    [Tooltip("Icon for Inventory UI. Also auto-copied to SpriteRenderer if it has no sprite.")]
    public Sprite icon;

    [Header("Physics Drift")]
    public float driftSpeed  = 0.5f;
    public float tumbleSpeed = 20f;
    public float floatDrag   = 2.5f;
    public float angularDrag = 1.5f;

    [Header("Fling From Impact")]
    [Range(0f, 90f)] public float minimumUpwardAngle = 30f;
    public float launchSpeedMin = 1f;
    public float launchSpeedMax = 3f;

    [Header("Magnet")]
    public float magnetRadius = 3f;
    public float magnetForce  = 8f;

    [Header("Glow Pulse")]
    [Tooltip("Enables a pulsing brightness effect so the item is easier to spot.")]
    public bool enableGlowPulse = true;

    [Tooltip("Speed of the pulse cycle (higher = faster flicker).")]
    public float pulseSpeed = 2f;

    [Tooltip("Minimum brightness during pulse (0=black, 1=original color).")]
    [Range(0f, 1f)]
    public float pulseMinBrightness = 0.5f;

    [Tooltip("Maximum brightness during pulse (1=original color, >1=overbright if HDR).")]
    [Range(1f, 3f)]
    public float pulseMaxBrightness = 1.8f;

    [Header("Audio")]
    public AudioClip pickupClip;
    [Range(0f, 1f)] public float pickupVolume = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    private Rigidbody2D    _rb;
    private SpriteRenderer _sr;
    private Transform      _playerTransform;
    private bool           _collected;
    private bool           _launched;
    private Color          _baseColor;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();

        if (_sr.sprite == null && icon != null) _sr.sprite = icon;
        if (_sr.sprite == null)
            Debug.LogWarning($"[MaterialPickup] '{name}' has no sprite assigned.");

        _baseColor = _sr.color;

        _rb.gravityScale           = 0f;
        _rb.linearDamping          = floatDrag;
        _rb.angularDamping         = angularDrag;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = false;
            var mat        = new PhysicsMaterial2D("MatBounce") { bounciness = 0.05f, friction = 0.8f };
            col.sharedMaterial = mat;
        }
    }

    private void Start()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) _playerTransform = player.transform;
        if (!_launched) ApplyRandomDrift();
    }

    private void Update()
    {
        if (_collected || !enableGlowPulse || _sr == null) return;

        // Sinusoidal brightness pulse so the item glows visibly
        float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float brightness = Mathf.Lerp(pulseMinBrightness, pulseMaxBrightness, t);
        _sr.color = new Color(
            _baseColor.r * brightness,
            _baseColor.g * brightness,
            _baseColor.b * brightness,
            _baseColor.a);
    }

    private void FixedUpdate()
    {
        if (_collected || _playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        if (dist <= magnetRadius)
        {
            Vector2 dir = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
            _rb.AddForce(dir * magnetForce, ForceMode2D.Force);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void LaunchFromImpact(Vector2 impactPoint)
    {
        _launched = true;
        float angleDeg = Random.Range(minimumUpwardAngle, 180f - minimumUpwardAngle);
        float angleRad = angleDeg * Mathf.Deg2Rad;
        float xDir     = Random.value > 0.5f ? 1f : -1f;

        Vector2 dir = new Vector2(xDir * Mathf.Cos(angleRad),
                                   Mathf.Abs(Mathf.Sin(angleRad))).normalized;

        _rb.linearVelocity  = dir * Random.Range(launchSpeedMin, launchSpeedMax);
        _rb.angularVelocity = Random.Range(-tumbleSpeed, tumbleSpeed);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Collection detection — both solid collision and trigger fallback
    // ─────────────────────────────────────────────────────────────────────────

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (_collected || !col.gameObject.CompareTag("Player")) return;
        Collect();
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (_collected || !col.gameObject.CompareTag("Player")) return;
        Collect();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected || !other.CompareTag("Player")) return;
        Collect();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (_collected || !other.CompareTag("Player")) return;
        Collect();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void Collect()
    {
        _collected = true;

        // STOP PHYSICS IMMEDIATELY so the material cannot push the player (recoil)
        _rb.linearVelocity  = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.bodyType        = RigidbodyType2D.Kinematic;

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Inventory.Instance?.AddItem(materialType, quantity, icon);

        string displayName = System.Text.RegularExpressions.Regex.Replace(
            materialType.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
        NotificationManager.Instance?.ShowPickup($"+{quantity}  {displayName}");

        if (pickupClip != null)
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);

        Destroy(gameObject);
    }

    private void ApplyRandomDrift()
    {
        if (driftSpeed <= 0f) return;
        _rb.linearVelocity  = Random.insideUnitCircle.normalized * (driftSpeed * Random.Range(0.3f, 1f));
        _rb.angularVelocity = Random.Range(-tumbleSpeed * 0.5f, tumbleSpeed * 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, magnetRadius);
    }
}
