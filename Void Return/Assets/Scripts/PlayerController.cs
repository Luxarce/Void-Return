using UnityEngine;

/// <summary>
/// Main player movement, gadget controller, and animation driver.
///
/// THRUSTER CHANGES:
///  — gadgetKeyThruster = KeyCode.Space (was V).
///  — Thruster is activated by HOLDING either Space or LMB (no tap/dash).
///  — BeginThrust() called every frame the key/button is held.
///  — EndThrust() called on key/button release.
///  — Pressing Space while the Thruster gadget is already selected activates it.
///  — Space does NOT select the thruster if another gadget is active —
///    it only activates the thruster when gadget slot 3 is selected.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Movement — Surface (Gravity Boots)")]
    public float walkSpeed   = 5f;
    public float surfaceDrag = 5f;

    [Header("Movement — Zero-G")]
    public float thrustForce   = 10f;
    public float maxZeroGSpeed = 14f;
    [Range(0f, 3f)] public float zeroGDrag = 0.6f;

    [Header("Rotation")]
    public float rotationSpeed      = 240f;
    public float uprightReturnSpeed = 180f;
    public float uprightThreshold   = 1.5f;
    [Range(0f, 180f)] public float maxTiltDegrees = 90f;

    [Header("Ground Detection")]
    public LayerMask groundLayer;
    [Range(0.05f, 0.5f)] public float groundCheckRadius = 0.2f;
    public Vector2 groundCheckOffset = new Vector2(0f, -0.55f);

    [Header("Gadget References")]
    public GravityBoots           gravityBoots;
    public TetherGun              tetherGun;
    public GravityGrenadeLauncher grenadeGun;
    public ThrusterPack           thrusterPack;

    [Header("Gadget Keys")]
    public KeyCode gadgetKeyBoots    = KeyCode.Q;
    public KeyCode gadgetKeyTether   = KeyCode.F;
    public KeyCode gadgetKeyGrenade  = KeyCode.G;
    [Tooltip("Key to SELECT the thruster slot. HOLD this key (or LMB) to activate boost.")]
    public KeyCode gadgetKeyThruster = KeyCode.Space;

    public bool allowScrollCycle = true;

    [Header("Debug Toggles")]
    public bool enableBootsAtStart    = true;
    public bool enableTetherAtStart   = false;
    public bool enableGrenadeAtStart  = false;
    public bool enableThrusterAtStart = false;

    // ─────────────────────────────────────────────────────────────────────────
    private Rigidbody2D _rb;
    private Animator    _anim;

    private GravityState _gravState   = GravityState.ZeroG;
    private Vector2      _zoneGravity = Vector2.zero;
    private bool         _isRiftDisoriented;
    private float        _riftSpinForce;

    private bool  _isGrounded;
    private float _inputH, _inputV;
    private int   _activeGadgetIndex;

    private static readonly int H_IsWalking     = Animator.StringToHash("IsWalking");
    private static readonly int H_IsFloating    = Animator.StringToHash("IsFloating");
    private static readonly int H_IsThrusting   = Animator.StringToHash("IsThrusting");
    private static readonly int H_IsDisoriented = Animator.StringToHash("IsDisoriented");
    private static readonly int H_UseGadget     = Animator.StringToHash("UseGadget");
    private static readonly int H_GadgetIndex   = Animator.StringToHash("GadgetIndex");
    private static readonly int H_SpeedX        = Animator.StringToHash("SpeedX");
    private static readonly int H_SpeedY        = Animator.StringToHash("SpeedY");

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
    }

    private void HandleGadgetSwitch()
    {
        if (Input.GetKeyDown(gadgetKeyBoots))
        { if (_activeGadgetIndex == 0) ActivateGadgetClick(); else SwitchGadget(0); }
        if (Input.GetKeyDown(gadgetKeyTether))
        { if (_activeGadgetIndex == 1) ActivateGadgetClick(); else SwitchGadget(1); }
        if (Input.GetKeyDown(gadgetKeyGrenade))
        { if (_activeGadgetIndex == 2) ActivateGadgetClick(); else SwitchGadget(2); }
        if (Input.GetKeyDown(gadgetKeyThruster))
        {
            // Space: if thruster slot already selected — begin thrust
            // If another slot is selected — switch to thruster
            if (_activeGadgetIndex == 3) { /* hold handled below */ }
            else SwitchGadget(3);
        }

        if (allowScrollCycle)
        {
            float s = Input.GetAxis("Mouse ScrollWheel");
            if (s >  0.01f) SwitchGadget((_activeGadgetIndex + 1) % 4);
            if (s < -0.01f) SwitchGadget((_activeGadgetIndex + 3) % 4);
        }
    }

    private void HandleGadgetActivate()
    {
        bool thrusterSlotActive = _activeGadgetIndex == 3
            && thrusterPack != null && thrusterPack.gameObject.activeSelf;

        // ── Mouse button DOWN — non-thruster gadgets fire on click ─────────
        if (Input.GetMouseButtonDown(0) && !thrusterSlotActive)
            ActivateGadgetClick();

        // ── THRUSTER: HOLD either Space or LMB to boost ────────────────────
        bool thrustKeyHeld    = Input.GetKey(gadgetKeyThruster);
        bool thrustMouseHeld  = Input.GetMouseButton(0);
        bool thrustInputActive = thrusterSlotActive && (thrustKeyHeld || thrustMouseHeld);

        if (thrustInputActive)
        {
            Vector2 dir = new Vector2(_inputH, _inputV).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = GetAimDirection();
            thrusterPack.BeginThrust(dir);
        }

        // Release thruster when both Space and LMB are released
        if (thrusterSlotActive && !thrustKeyHeld && !thrustMouseHeld
            && (Input.GetKeyUp(gadgetKeyThruster) || Input.GetMouseButtonUp(0)))
        {
            thrusterPack.EndThrust();
        }
    }

    private void ActivateGadgetClick()
    {
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
        Vector3 m = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return ((Vector2)(m - transform.position)).normalized;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Physics
    // ─────────────────────────────────────────────────────────────────────────

    private void CheckGround()
    {
        Vector2 offset   = (Vector2)(transform.rotation * (Vector3)groundCheckOffset);
        _isGrounded      = Physics2D.OverlapCircle(
            (Vector2)transform.position + offset, groundCheckRadius, groundLayer);
    }

    private void ApplyMovement()
    {
        bool bootsOn = gravityBoots != null && gravityBoots.enabled && gravityBoots.IsActive;

        if (_isGrounded && bootsOn)
        {
            Vector2 walkDir = _zoneGravity.sqrMagnitude > 0.01f
                ? new Vector2(-_zoneGravity.normalized.y, _zoneGravity.normalized.x)
                : Vector2.right;
            float down = Mathf.Min(_rb.linearVelocity.y, 0f);
            _rb.linearVelocity = walkDir * (_inputH * walkSpeed) + Vector2.up * down;
            _rb.linearDamping  = surfaceDrag;
        }
        else if (_zoneGravity.sqrMagnitude > 0.01f && _gravState != GravityState.ZeroG)
        {
            _rb.linearVelocity = new Vector2(_inputH * walkSpeed, _rb.linearVelocity.y);
            _rb.linearDamping  = _isGrounded ? surfaceDrag : zeroGDrag;
        }
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
        if (_gravState == GravityState.ZeroG || _zoneGravity.sqrMagnitude < 0.01f) return;
        _rb.AddForce(_zoneGravity, ForceMode2D.Force);
        if (_isRiftDisoriented) _rb.angularVelocity += _riftSpinForce * Time.fixedDeltaTime;
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
        _rb.angularVelocity = 0f;

        float speed = _rb.linearVelocity.magnitude;
        float gravAngle = 0f;
        if (_zoneGravity.sqrMagnitude > 0.01f)
        {
            Vector2 g = _zoneGravity.normalized;
            float raw = Mathf.Atan2(-g.x, g.y) * Mathf.Rad2Deg;
            gravAngle = Mathf.Clamp(Mathf.DeltaAngle(0f, raw), -maxTiltDegrees, maxTiltDegrees);
        }

        bool isSlowing = speed < uprightThreshold;
        float target   = isSlowing
            ? Mathf.LerpAngle(gravAngle, 0f, 1f - speed / Mathf.Max(uprightThreshold, 0.001f))
            : gravAngle;
        float useSpeed = isSlowing ? uprightReturnSpeed : rotationSpeed;
        float current  = transform.eulerAngles.z;
        float signed   = current > 180f ? current - 360f : current;
        transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.MoveTowardsAngle(signed, target, useSpeed * Time.fixedDeltaTime));
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void ApplyZoneGravity(GravityState state, Vector2 gravity, bool disorient, float spinForce)
    {
        _gravState         = state;
        _zoneGravity       = gravity;
        _isRiftDisoriented = disorient;
        _riftSpinForce     = spinForce;
        FindFirstObjectByType<GameHUD>()?.SetGravityState(state);
    }

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
        Vector2 r = Application.isPlaying
            ? (Vector2)(transform.rotation * (Vector3)groundCheckOffset)
            : groundCheckOffset;
        Gizmos.DrawWireSphere((Vector2)transform.position + r, groundCheckRadius);
    }
}
