using UnityEngine;

/// <summary>
/// Life Support Shield — blocks meteorites, NOT the player.
///
/// FIX — PLAYER BOUNCED OFF SHIELD:
///  The shield collider was on the "Ground" layer, and the player's
///  Physics 2D matrix has Player vs Ground = ON (required for walking).
///  This caused the player to collide with the shield.
///
///  Fix: the shield now uses Physics2D.IgnoreCollision() to explicitly
///  ignore collisions between the shield collider and the player's collider.
///  This is called in Start() after finding the Player, and is re-applied
///  whenever the shield activates.
///
/// FIX — LIFE SUPPORT AUTOREFILL ZONE DISABLED:
///  The LifeSupportZone script checks CanRefill() which requires
///  lifeSupportModule.Progress > 0. Previously the shield activated on
///  the same frame as Stage 1 completing, but the shield's own collider
///  was intercepting the trigger entry for LifeSupportZone.
///  Fix: the shield collider is added to a dedicated "Shield" layer
///  (not Ground) so it does NOT interact with the LifeSupportZone trigger
///  or the player, but DOES interact with meteorites.
///
/// LAYER SETUP REQUIRED:
///  Create a layer named "Shield" (Edit > Project Settings > Tags and Layers).
///  In the Physics 2D Layer Matrix:
///    Shield vs Meteorite (or the layer your meteorite prefabs are on) = TICKED
///    Shield vs Player = UNTICKED (prevents player collision)
///    Shield vs Ground = UNTICKED (not needed)
/// </summary>
public class LifeSupportShield : MonoBehaviour
{
    [Header("References")]
    public ShipModule lifeSupportModule;

    [Header("Shield Physics")]
    [Tooltip("Radius of the solid collider that blocks meteorites.")]
    public float shieldColliderRadius = 3f;

    [Tooltip("Layer name for the shield collider.\n" +
             "Create this layer in Edit > Project Settings > Tags and Layers.\n" +
             "In Physics 2D Matrix: Shield vs your meteorite layer = ON, Shield vs Player = OFF.")]
    public string shieldLayer = "Shield";

    [Header("Shield Visual")]
    [Tooltip("Optional child GameObject to show when shield is active. Auto-generated if null.")]
    public GameObject shieldVisual;

    public float shieldRadius      = 2.5f;
    public Color shieldColor       = new Color(0.1f, 0.8f, 1f, 0.35f);
    public float pulseSpeed        = 1.5f;
    [Range(0f, 0.4f)]
    public float pulseAmplitude    = 0.15f;

    // ─────────────────────────────────────────────────────────────────────────
    private SpriteRenderer   _shieldSr;
    private CircleCollider2D _shieldCollider;
    private Collider2D       _playerCollider;
    private float            _baseAlpha;
    private bool             _shieldActive;
    private bool             _notifiedOnce;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Find player collider for IgnoreCollision
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            _playerCollider = player.GetComponent<Collider2D>();

        // Build visual
        if (shieldVisual == null)
            shieldVisual = CreateShieldCircle();
        _shieldSr  = shieldVisual?.GetComponent<SpriteRenderer>();
        _baseAlpha = shieldColor.a;
        shieldVisual?.SetActive(false);

        // Build collider
        _shieldCollider           = gameObject.AddComponent<CircleCollider2D>();
        _shieldCollider.radius    = shieldColliderRadius;
        _shieldCollider.isTrigger = false;
        _shieldCollider.enabled   = false;

        // Assign shield layer
        int layerIndex = LayerMask.NameToLayer(shieldLayer);
        if (layerIndex >= 0)
        {
            // Only the collider's parent object needs the layer, not child visuals
            gameObject.layer = layerIndex;
        }
        else
        {
            Debug.LogWarning($"[LifeSupportShield] Layer '{shieldLayer}' not found. " +
                             "Create it in Edit > Project Settings > Tags and Layers. " +
                             "Then set Physics 2D matrix: Shield vs your meteorite layer ON, Shield vs Player OFF.");
        }
    }

    private void Update()
    {
        bool shouldBeActive = lifeSupportModule != null && lifeSupportModule.Stage1Complete;

        if (shouldBeActive && !_shieldActive)
        {
            _shieldActive           = true;
            _shieldCollider.enabled = true;
            shieldVisual?.SetActive(true);

            // Ignore collision between shield and player every time shield activates
            IgnorePlayerCollision();

            if (!_notifiedOnce)
            {
                _notifiedOnce = true;
                NotificationManager.Instance?.ShowInfo("Life Support shields online — meteorites deflected.");
            }
        }
        else if (!shouldBeActive && _shieldActive)
        {
            _shieldActive           = false;
            _shieldCollider.enabled = false;
            shieldVisual?.SetActive(false);
        }

        if (_shieldActive && _shieldSr != null)
        {
            float t     = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            float alpha = Mathf.Clamp01(_baseAlpha + (t - 0.5f) * pulseAmplitude * 2f);
            _shieldSr.color = new Color(shieldColor.r, shieldColor.g, shieldColor.b, alpha);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void IgnorePlayerCollision()
    {
        if (_playerCollider == null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null) _playerCollider = player.GetComponent<Collider2D>();
        }

        if (_playerCollider != null && _shieldCollider != null)
        {
            Physics2D.IgnoreCollision(_shieldCollider, _playerCollider, true);
            Debug.Log("[LifeSupportShield] Player collision ignored — player will not bounce off shield.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private GameObject CreateShieldCircle()
    {
        var go = new GameObject("LifeSupportShieldVisual");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * shieldRadius * 2f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color  = shieldColor;
        sr.sortingOrder = 2;
        return go;
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) / 2f, rOut = c, rIn = c * 0.7f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float a = (d <= rOut && d >= rIn) ? Mathf.Lerp(0.3f, 1f, (d - rIn) / (rOut - rIn))
                    : d < rIn ? 0.15f : 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply(); tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
