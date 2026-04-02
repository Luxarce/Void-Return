using UnityEngine;

/// <summary>
/// Main player movement, gadget controller and animation driver.
///
/// CHANGES:
///  — Jump removed entirely. Gravity Boots are a gravity-anchor gadget only.
///    Space bar no longer does anything. No jumpForce field.
///  — Branch 1 (boots on, grounded): walks on surface. No jump branch inside it.
///  — Branch 2 (gravity zone, no boots): standard lateral movement in gravity field.
///  — Branch 3 (Zero-G): momentum-based free float with WASD.
///  — Upright recovery: zeros angularVelocity each frame + blends to 0 when slow.
///  — Thruster dash: passes WASD direction as the dash vector.
///  — Gadget crosshair: PlayerController tells GadgetHUDManager which gadget is
///    selected so the crosshair renderer knows what to show.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Movement — Surface (Gravity Boots)")]
    [Tooltip("Walk speed while boots are active and player is grounded.")]
    public float walkSpeed   = 5f;

    [Tooltip("Drag applied while touching a ground surface.")]
    public float surfaceDrag = 5f;

    [Header("Movement — Zero-G")]
    [Tooltip("Force added per FixedUpdate when pressing WASD in zero-G.")]
    public float thrustForce   = 10f;

    [Tooltip("Max speed cap in zero-G.")]
    public float maxZeroGSpeed = 14f;

    [Tooltip("Drag in zero-G (controls momentum decay).")]
    [Range(0f, 3f)]
    public float zeroGDrag = 0.6f;

    [Header("Rotation")]
    [Tooltip("Degrees per second toward gravity-aligned angle.")]
    public float rotationSpeed = 240f;

    [Tooltip("Degrees per second back toward upright (0°) when slowing down.")]
    public float uprightReturnSpeed = 180f;

    [Tooltip("Speed threshold below which upright recovery kicks in.")]
    public float uprightThreshold = 1.5f;

    [Tooltip("Max degrees of tilt from upright. 90 = wall walking allowed.")]
    [Range(0f, 180f)]
    public float maxTiltDegrees = 90f;

    [Header("Ground Detection")]
    [Tooltip("Layer mask for surfaces the player can stand on.")]
    public LayerMask groundLayer;

    [Tooltip("Radius of the ground-check overlap circle at the player's feet.")]
    [Range(0.05f, 0.5f)]
    public float groundCheckRadius = 0.2f;

    [Tooltip("Offset from the player pivot to the feet position (rotates with the player).")]
    public Vector2 groundCheckOffset = new Vector2(0f, -0.55f);

    [Header("Gadget References — Drag child scripts here")]
    [Tooltip("GravityBoots child script. Q = select, LMB = toggle anchor on/off.")]
    public GravityBoots gravityBoots;

    [Tooltip("TetherGun child script. F = select, LMB = fire/release.")]
    public TetherGun tetherGun;

    [Tooltip("GravityGrenadeLauncher child script. G = select, LMB = launch.")]
    public GravityGrenadeLauncher grenadeGun;

    [Tooltip("ThrusterPack child script. V = select, LMB = dash.")]
    public ThrusterPack thrusterPack;

    [Header("Gadget Keys")]
    public KeyCode gadgetKeyBoots    = KeyCode.Q;
    public KeyCode gadgetKeyTether   = KeyCode.F;
    public KeyCode gadgetKeyGrenade  = KeyCode.G;
    public KeyCode gadgetKeyThruster = KeyCode.V;

    [Tooltip("Allow mouse scroll wheel to cycle through gadget slots.")]
    public bool allowScrollCycle = true;

    [Header("Debug Toggles — enable gadgets without repairing modules")]
    public bool enableBootsAtStart    = true;
    public bool enableTetherAtStart   = false;
    public bool enableGrenadeAtStart  = false;
    public bool enableThrusterAtStart = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Rigidbody2D _rb;
    private Animator    _anim;

    private GravityState _gravState   = GravityState.ZeroG;
    private Vector2      _zoneGravity = Vector2.zero;
    private bool         _isRiftDisoriented;
    private float        _riftSpinForce;

    private bool  _isGrounded;
    private float _inputH;
    private float _inputV;
    private int   _activeGadgetIndex;

    // ─── Animator Hashes ──────────────────────────────────────────────────────
    private static readonly int H_IsWalking     = Animator.StringToHash("IsWalking");
    private static readonly int H_IsFloating    = Animator.StringToHash("IsFloating");
    private static readonly int H_IsThrusting   = Animator.StringToHash("IsThrusting");
    private static readonly int H_IsDisoriented = Animator.StringToHash("IsDisoriented");
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
        ApplyStartingToggles();
        SwitchGadget(0);
    }

    private void Update()
    {
        GatherInput();
        HandleGadgetSwitch();
        HandleGadgetActivate();
        UpdateAnimations();
        FlipSprite();
    }

    private void FixedUpdate()
    {
        CheckGround();
        ApplyMovement();
        ApplyZoneGravityForces();
        ClampZeroGVelocity();
        SmoothRotationToGravity();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Startup
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyStartingToggles()
    {
        if (gravityBoots  != null) gravityBoots.enabled = enableBootsAtStart;
        if (tetherGun     != null) tetherGun.gameObject.SetActive(enableTetherAtStart);
        if (grenadeGun    != null) grenadeGun.gameObject.SetActive(enableGrenadeAtStart);
        if (thrusterPack  != null) thrusterPack.gameObject.SetActive(enableThrusterAtStart);

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
        // NOTE: Jump (Space) intentionally removed. Boots are anchor-only.
    }

    private void HandleGadgetSwitch()
    {
        if (Input.GetKeyDown(gadgetKeyBoots))
        { if (_activeGadgetIndex == 0) ActivateGadget(); else SwitchGadget(0); }
        if (Input.GetKeyDown(gadgetKeyTether))
        { if (_activeGadgetIndex == 1) ActivateGadget(); else SwitchGadget(1); }
        if (Input.GetKeyDown(gadgetKeyGrenade))
        { if (_activeGadgetIndex == 2) ActivateGadget(); else SwitchGadget(2); }
        if (Input.GetKeyDown(gadgetKeyThruster))
        { if (_activeGadgetIndex == 3) ActivateGadget(); else SwitchGadget(3); }

        if (allowScrollCycle)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll >  0.01f) SwitchGadget((_activeGadgetIndex + 1) % 4);
            if (scroll < -0.01f) SwitchGadget((_activeGadgetIndex + 3) % 4);
        }
    }

    private void HandleGadgetActivate()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        ActivateGadget();
    }

    private void ActivateGadget()
    {
        _anim.SetTrigger(H_UseGadget);
        switch (_activeGadgetIndex)
        {
            case 0:
                // Gravity Boots — toggle anchor on/off
                if (gravityBoots != null && gravityBoots.enabled)
                    gravityBoots.Toggle();
                break;
            case 1:
                // Tether Gun — fire toward mouse cursor
                if (tetherGun != null && tetherGun.gameObject.activeSelf)
                    tetherGun.Fire(GetAimDirection());
                break;
            case 2:
                // Gravity Grenade — launch toward mouse cursor
                if (grenadeGun != null && grenadeGun.gameObject.activeSelf)
                    grenadeGun.Launch(GetAimDirection());
                break;
            case 3:
                // Thruster — dash in WASD direction (or aim dir if no input)
                if (thrusterPack != null && thrusterPack.gameObject.activeSelf)
                {
                    Vector2 inputDir = new Vector2(_inputH, _inputV).normalized;
                    if (inputDir.sqrMagnitude < 0.01f) inputDir = GetAimDirection();
                    thrusterPack.Activate(inputDir);
                }
                break;
        }
    }

    public void SwitchGadget(int index)
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
        Vector2 offset   = (Vector2)(transform.rotation * (Vector3)groundCheckOffset);
        Vector2 checkPos = (Vector2)transform.position + offset;
        _isGrounded      = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundLayer);
    }

    private void ApplyMovement()
    {
        bool bootsOn = gravityBoots != null && gravityBoots.enabled && gravityBoots.IsActive;

        // BRANCH 1 — Gravity Boots active and grounded: surface walking
        // GravityBoots.FixedUpdate supplies the anchor force that keeps the player grounded.
        // No jump — boots are an anchor only.
        if (_isGrounded && bootsOn)
        {
            Vector2 walkDir;
            if (_zoneGravity.sqrMagnitude > 0.01f)
            {
                Vector2 gn = _zoneGravity.normalized;
                walkDir    = new Vector2(-gn.y, gn.x);   // surface-right direction
            }
            else
            {
                walkDir = Vector2.right;                   // default horizontal walk
            }

            // Preserve any downward velocity component so the player sticks on slopes
            float downComponent = Mathf.Min(_rb.linearVelocity.y, 0f);
            _rb.linearVelocity = walkDir * (_inputH * walkSpeed)
                                + Vector2.up * downComponent;
            _rb.linearDamping  = surfaceDrag;
        }
        // BRANCH 2 — Inside a gravity zone, boots off or airborne
        else if (_zoneGravity.sqrMagnitude > 0.01f && _gravState != GravityState.ZeroG)
        {
            _rb.linearVelocity = new Vector2(_inputH * walkSpeed, _rb.linearVelocity.y);
            _rb.linearDamping  = _isGrounded ? surfaceDrag : zeroGDrag;
        }
        // BRANCH 3 — Zero-G free float
        else
        {
            Vector2 dir = new Vector2(_inputH, _inputV).normalized;
            if (dir.sqrMagnitude > 0.01f)
                _rb.AddForce(dir * thrustForce, ForceMode2D.Force);
            _rb.linearDamping = zeroGDrag;
        }
    }

    private void ApplyZoneGravityForces()
    {
        if (_gravState == GravityState.ZeroG)   return;
        if (_zoneGravity.sqrMagnitude < 0.01f)  return;
        _rb.AddForce(_zoneGravity, ForceMode2D.Force);
        if (_isRiftDisoriented)
            _rb.angularVelocity += _riftSpinForce * Time.fixedDeltaTime;
    }

    private void ClampZeroGVelocity()
    {
        if (_gravState != GravityState.ZeroG) return;
        if (_rb.linearVelocity.magnitude > maxZeroGSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxZeroGSpeed;
    }

    private void SmoothRotationToGravity()
    {
        if (_isRiftDisoriented) return;

        // Kill any residual angular velocity so physics spin can't lock the player sideways
        _rb.angularVelocity = 0f;

        float speed     = _rb.linearVelocity.magnitude;
        bool  isSlowing = speed < uprightThreshold;

        float gravAngle = 0f;
        if (_zoneGravity.sqrMagnitude > 0.01f)
        {
            Vector2 g  = _zoneGravity.normalized;
            float   raw = Mathf.Atan2(-g.x, g.y) * Mathf.Rad2Deg;
            gravAngle  = Mathf.Clamp(Mathf.DeltaAngle(0f, raw),
                                      -maxTiltDegrees, maxTiltDegrees);
        }

        // Blend toward 0° (upright) when slowing or stopped
        float targetAngle = isSlowing
            ? Mathf.LerpAngle(gravAngle, 0f,
                               1f - speed / Mathf.Max(uprightThreshold, 0.001f))
            : gravAngle;

        float useSpeed = isSlowing ? uprightReturnSpeed : rotationSpeed;

        float current = transform.eulerAngles.z;
        float signed  = current > 180f ? current - 360f : current;
        float next    = Mathf.MoveTowardsAngle(signed, targetAngle,
                                                useSpeed * Time.fixedDeltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, next);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — called by GravityZone
    // ─────────────────────────────────────────────────────────────────────────

    public void ApplyZoneGravity(GravityState state, Vector2 gravity,
                                  bool disorient, float spinForce)
    {
        _gravState         = state;
        _zoneGravity       = gravity;
        _isRiftDisoriented = disorient;
        _riftSpinForce     = spinForce;
        FindFirstObjectByType<GameHUD>()?.SetGravityState(state);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Animation & Visual
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateAnimations()
    {
        bool walking   = _isGrounded && Mathf.Abs(_inputH) > 0.05f;
        bool floating  = !_isGrounded && _gravState == GravityState.ZeroG;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 rotated = Application.isPlaying
            ? (Vector2)(transform.rotation * (Vector3)groundCheckOffset)
            : groundCheckOffset;
        Gizmos.DrawWireSphere((Vector2)transform.position + rotated, groundCheckRadius);
    }
}
