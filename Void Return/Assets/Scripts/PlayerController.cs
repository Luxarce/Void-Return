using UnityEngine;

/// <summary>
/// Main player movement, gadget controller and animation driver.
///
/// CHANGES IN THIS VERSION:
///  — Gadget keys updated: Q = Gravity Boots, F = Tether Gun,
///    G = Gravity Grenade, V = Thruster Pack.
///  — Rotation is now CLAMPED. The player sprite stays upright at 0° in
///    Zero-G and tilts at most ±90° when inside a gravity zone. This prevents
///    the astronaut from going fully upside-down unintentionally.
///  — Inspector booleans let you enable/disable each gadget at game start
///    for testing without needing to wire repair logic first.
///  — Float animation tilt is driven by velocity angle, clamped to ±45° from
///    upright so the sprite never rotates past horizontal.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Movement — Surface (Gravity Boots Active)")]
    [Tooltip("Walking speed on a surface when Gravity Boots are active.")]
    public float walkSpeed = 5f;

    [Tooltip("Impulse strength applied away from the surface when jumping.")]
    public float jumpForce = 10f;

    [Tooltip("Linear drag applied to Rigidbody2D while the player is on a surface.")]
    public float surfaceDrag = 6f;

    [Header("Movement — Zero-G / Airborne")]
    [Tooltip("Force added per FixedUpdate when the player presses WASD in Zero-G.")]
    public float thrustForce = 10f;

    [Tooltip("Maximum speed cap while floating in Zero-G.")]
    public float maxZeroGSpeed = 14f;

    [Tooltip("Linear drag while floating in Zero-G.")]
    [Range(0f, 3f)]
    public float zeroGDrag = 0.6f;

    [Header("Rotation & Tilt")]
    [Tooltip("How fast the player rotates to align with the gravity direction (degrees/sec). " +
             "Only applies when inside a gravity zone.")]
    public float rotationSpeed = 200f;

    [Tooltip("Maximum degrees the player can tilt from the global upright (0°) position. " +
             "90° = can walk on walls but never goes fully upside-down. " +
             "Set lower (e.g. 45°) for a more upright feel.")]
    [Range(0f, 180f)]
    public float maxTiltDegrees = 90f;

    [Header("Ground Detection")]
    [Tooltip("Set to your 'Ground' layer mask.")]
    public LayerMask groundLayer;

    [Tooltip("Radius of the overlap circle at the player's feet for ground detection.")]
    [Range(0.05f, 0.5f)]
    public float groundCheckRadius = 0.18f;

    [Tooltip("Offset from pivot to feet. This offset rotates with the player.")]
    public Vector2 groundCheckOffset = new Vector2(0f, -0.55f);

    [Header("Gadget References — Drag child scripts here")]
    [Tooltip("GravityBoots script on a child GameObject.")]
    public GravityBoots gravityBoots;

    [Tooltip("TetherGun script on a child GameObject.")]
    public TetherGun tetherGun;

    [Tooltip("GravityGrenadeLauncher script on a child GameObject.")]
    public GravityGrenadeLauncher grenadeGun;

    [Tooltip("ThrusterPack script on a child GameObject.")]
    public ThrusterPack thrusterPack;

    [Header("Gadget Keys")]
    [Tooltip("Key to toggle Gravity Boots ON or OFF.")]
    public KeyCode gadgetKeyBoots    = KeyCode.Q;

    [Tooltip("Key to select and fire the Tether Gun.")]
    public KeyCode gadgetKeyTether   = KeyCode.F;

    [Tooltip("Key to select and launch the Gravity Grenade.")]
    public KeyCode gadgetKeyGrenade  = KeyCode.G;

    [Tooltip("Key to select and use the Thruster Pack.")]
    public KeyCode gadgetKeyThruster = KeyCode.V;

    [Tooltip("Scroll the mouse wheel to cycle between gadgets.")]
    public bool allowScrollCycle = true;

    [Tooltip("Key to interact with nearby objects (repair modules, etc.).")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Gadget Enable Toggles (Testing / Debug)")]
    [Tooltip("If ON, the Gravity Boots start available from the first frame (good for testing). " +
             "In a real playthrough, boots unlock via Life Support repair.")]
    public bool enableBootsAtStart    = true;

    [Tooltip("If ON, the Tether Gun starts available without repairing Navigation. " +
             "Turn OFF for normal gameplay — Navigation repair unlocks it.")]
    public bool enableTetherAtStart   = false;

    [Tooltip("If ON, the Gravity Grenade starts available without repairing Hull Plating.")]
    public bool enableGrenadeAtStart  = false;

    [Tooltip("If ON, the Thruster Pack starts available without repairing the Engine Core.")]
    public bool enableThrusterAtStart = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Rigidbody2D _rb;
    private Animator    _anim;

    private GravityState _currentGravityState = GravityState.ZeroG;
    private Vector2      _zoneGravity         = Vector2.zero;
    private bool         _isRiftDisoriented;
    private float        _riftSpinForce;

    private bool  _isGrounded;
    private float _inputH;
    private float _inputV;
    private bool  _jumpQueued;
    private int   _activeGadgetIndex = 0;

    // ─── Animator Hash Cache ─────────────────────────────────────────────────
    private static readonly int H_IsWalking     = Animator.StringToHash("IsWalking");
    private static readonly int H_IsFloating    = Animator.StringToHash("IsFloating");
    private static readonly int H_IsThrusting   = Animator.StringToHash("IsThrusting");
    private static readonly int H_IsDisoriented = Animator.StringToHash("IsDisoriented");
    private static readonly int H_Jump          = Animator.StringToHash("Jump");
    private static readonly int H_UseGadget     = Animator.StringToHash("UseGadget");
    private static readonly int H_GadgetIndex   = Animator.StringToHash("GadgetIndex");
    private static readonly int H_SpeedX        = Animator.StringToHash("SpeedX");
    private static readonly int H_SpeedY        = Animator.StringToHash("SpeedY");

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb   = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();
        _rb.gravityScale = 0f;
    }

    private void Start()
    {
        ApplyStartingGadgetToggles();
    }

    private void Update()
    {
        GatherInput();
        HandleGadgetSwitch();
        HandleGadgetUse();
        UpdateAnimations();
        FlipSprite();
    }

    private void FixedUpdate()
    {
        CheckGround();
        ApplyMovement();
        ApplyGravityForces();
        ClampZeroGVelocity();
        SmoothRotationToGravity();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Startup Gadget Toggles
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyStartingGadgetToggles()
    {
        // Boots are always on the Player — just enable/disable the component
        if (gravityBoots != null)
            gravityBoots.enabled = enableBootsAtStart;

        // Tether, Grenade, Thruster activate their parent GameObjects
        if (tetherGun    != null) tetherGun.gameObject.SetActive(enableTetherAtStart);
        if (grenadeGun   != null) grenadeGun.gameObject.SetActive(enableGrenadeAtStart);
        if (thrusterPack != null) thrusterPack.gameObject.SetActive(enableThrusterAtStart);

        // Refresh HUD unlock state
        GadgetHUDManager.Instance?.SetGadgetAvailable(0, enableBootsAtStart);
        GadgetHUDManager.Instance?.SetGadgetAvailable(1, enableTetherAtStart);
        GadgetHUDManager.Instance?.SetGadgetAvailable(2, enableGrenadeAtStart);
        GadgetHUDManager.Instance?.SetGadgetAvailable(3, enableThrusterAtStart);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Input
    // ─────────────────────────────────────────────────────────────────────────

    private void GatherInput()
    {
        _inputH = Input.GetAxisRaw("Horizontal");
        _inputV = Input.GetAxisRaw("Vertical");
        if (Input.GetKeyDown(KeyCode.Space)) _jumpQueued = true;
    }

    private void HandleGadgetSwitch()
    {
        if (Input.GetKeyDown(gadgetKeyBoots))    SwitchGadget(0);
        if (Input.GetKeyDown(gadgetKeyTether))   SwitchGadget(1);
        if (Input.GetKeyDown(gadgetKeyGrenade))  SwitchGadget(2);
        if (Input.GetKeyDown(gadgetKeyThruster)) SwitchGadget(3);

        if (allowScrollCycle)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0.01f)  SwitchGadget((_activeGadgetIndex + 1) % 4);
            if (scroll < -0.01f) SwitchGadget((_activeGadgetIndex + 3) % 4);
        }
    }

    private void HandleGadgetUse()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        _anim.SetTrigger(H_UseGadget);

        switch (_activeGadgetIndex)
        {
            case 0:
                if (gravityBoots != null && gravityBoots.enabled)
                    gravityBoots.Toggle();
                break;
            case 1:
                if (tetherGun != null && tetherGun.gameObject.activeSelf)
                    tetherGun.Fire(GetAimDirection());
                break;
            case 2:
                if (grenadeGun != null && grenadeGun.gameObject.activeSelf)
                    grenadeGun.Launch(GetAimDirection());
                break;
            case 3:
                if (thrusterPack != null && thrusterPack.gameObject.activeSelf)
                    thrusterPack.Activate(new Vector2(_inputH, _inputV));
                break;
        }
    }

    private void SwitchGadget(int index)
    {
        _activeGadgetIndex = index;
        _anim.SetInteger(H_GadgetIndex, index);
        GadgetHUDManager.Instance?.HighlightGadget(index);
    }

    private Vector2 GetAimDirection()
    {
        if (Camera.main == null) return transform.right;
        Vector3 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return ((Vector2)(mouse - transform.position)).normalized;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Physics
    // ─────────────────────────────────────────────────────────────────────────

    private void CheckGround()
    {
        Vector2 rotatedOffset = (Vector2)(transform.rotation * (Vector3)groundCheckOffset);
        Vector2 checkPos      = (Vector2)transform.position + rotatedOffset;
        _isGrounded           = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundLayer);
    }

    private void ApplyMovement()
    {
        bool bootsOn    = gravityBoots != null && gravityBoots.enabled && gravityBoots.IsActive;
        bool hasGravity = _zoneGravity.sqrMagnitude > 0.01f;

        // Branch 1: Surface walk (boots + grounded + in a gravity zone)
        if (_isGrounded && bootsOn && hasGravity)
        {
            Vector2 gravNorm     = _zoneGravity.normalized;
            Vector2 surfaceRight = new Vector2(-gravNorm.y, gravNorm.x);
            float   intoSurface  = Mathf.Max(0f, Vector2.Dot(_rb.linearVelocity, gravNorm));

            _rb.linearVelocity = surfaceRight * (_inputH * walkSpeed) + gravNorm * intoSurface;
            _rb.linearDamping  = surfaceDrag;

            if (_jumpQueued)
            {
                _rb.AddForce(-gravNorm * jumpForce, ForceMode2D.Impulse);
                _anim.SetTrigger(H_Jump);
            }
        }
        // Branch 2: In a gravity zone, boots off or airborne
        else if (hasGravity && _currentGravityState != GravityState.ZeroG)
        {
            _rb.linearVelocity = new Vector2(_inputH * walkSpeed, _rb.linearVelocity.y);
            _rb.linearDamping  = _isGrounded ? surfaceDrag : zeroGDrag;

            if (_jumpQueued && _isGrounded)
            {
                _rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
                _anim.SetTrigger(H_Jump);
            }
        }
        // Branch 3: Zero-G free float
        else
        {
            Vector2 dir = new Vector2(_inputH, _inputV).normalized;
            if (dir.sqrMagnitude > 0.01f)
                _rb.AddForce(dir * thrustForce, ForceMode2D.Force);
            _rb.linearDamping = zeroGDrag;
        }

        _jumpQueued = false;
    }

    private void ApplyGravityForces()
    {
        if (_currentGravityState == GravityState.ZeroG) return;
        if (_zoneGravity.sqrMagnitude < 0.01f)          return;
        _rb.AddForce(_zoneGravity, ForceMode2D.Force);
        if (_isRiftDisoriented) _rb.angularVelocity += _riftSpinForce * Time.fixedDeltaTime;
    }

    private void ClampZeroGVelocity()
    {
        if (_currentGravityState != GravityState.ZeroG) return;
        if (_rb.linearVelocity.magnitude > maxZeroGSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxZeroGSpeed;
    }

    /// <summary>
    /// Rotates the player to align with gravity direction,
    /// but clamps the result so the sprite never tilts more than
    /// maxTiltDegrees from vertical (upright 0°).
    /// </summary>
    private void SmoothRotationToGravity()
    {
        if (_isRiftDisoriented) return;

        float targetAngle = 0f; // Default: stay upright

        if (_zoneGravity.sqrMagnitude > 0.01f)
        {
            Vector2 gravNorm = _zoneGravity.normalized;
            targetAngle = Mathf.Atan2(-gravNorm.x, gravNorm.y) * Mathf.Rad2Deg;

            // Clamp so the player never tilts more than maxTiltDegrees from upright (0°)
            targetAngle = Mathf.Clamp(
                Mathf.DeltaAngle(0f, targetAngle),
                -maxTiltDegrees,
                maxTiltDegrees
            );
        }

        float current = transform.eulerAngles.z;
        // Convert to signed angle for smooth MoveTowards
        float currentSigned = current > 180f ? current - 360f : current;
        float next = Mathf.MoveTowardsAngle(currentSigned, targetAngle,
                                             rotationSpeed * Time.fixedDeltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, next);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Called by GravityZone
    // ─────────────────────────────────────────────────────────────────────────

    public void ApplyZoneGravity(GravityState state, Vector2 gravity,
                                  bool disorient, float spinForce)
    {
        _currentGravityState = state;
        _zoneGravity         = gravity;
        _isRiftDisoriented   = disorient;
        _riftSpinForce       = spinForce;
        FindFirstObjectByType<GameHUD>()?.SetGravityState(state);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Animations
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateAnimations()
    {
        bool walking   = _isGrounded && Mathf.Abs(_inputH) > 0.05f;
        bool floating  = !_isGrounded && _currentGravityState == GravityState.ZeroG;
        bool thrusting = thrusterPack != null
                         && thrusterPack.gameObject.activeSelf
                         && thrusterPack.IsActive;

        _anim.SetBool(H_IsWalking,     walking);
        _anim.SetBool(H_IsFloating,    floating);
        _anim.SetBool(H_IsThrusting,   thrusting);
        _anim.SetBool(H_IsDisoriented, _isRiftDisoriented);
        _anim.SetFloat(H_SpeedX, _rb.linearVelocity.x);
        _anim.SetFloat(H_SpeedY, _rb.linearVelocity.y);
    }

    private void FlipSprite()
    {
        if (Mathf.Abs(_inputH) > 0.05f)
            transform.localScale = new Vector3(_inputH > 0f ? 1f : -1f, 1f, 1f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 rotated = Application.isPlaying
            ? (Vector2)(transform.rotation * (Vector3)groundCheckOffset)
            : groundCheckOffset;
        Gizmos.DrawWireSphere((Vector2)transform.position + rotated, groundCheckRadius);
    }
}
