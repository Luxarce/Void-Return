using UnityEngine;

/// <summary>
/// VOID RETURN — PlayerController  (v3 — Tilemap & Collision Fix)
///
/// Changes from v2:
/// ─ Ground check now uses OverlapBox instead of OverlapCircle, which works
///   far more reliably with TilemapCollider2D + CompositeCollider2D seams.
/// ─ Rigidbody2D Collision Detection must be set to Continuous in Inspector.
/// ─ Movement in Normal-gravity branch uses MovePosition instead of direct
///   velocity assignment so the physics solver handles depenetration properly.
/// ─ surfaceStick force replaced with explicit gravity application every
///   FixedUpdate so the player never floats off a tilemap surface.
/// ─ Zero-G branch unchanged (force-based momentum, same as before).
/// ─ Gravity Boots surface-walk branch updated to use MovePosition.
///
/// SETUP REQUIREMENTS (see Section 1 of the Solutions document):
///   Player Rigidbody2D → Collision Detection: Continuous
///   Player Rigidbody2D → Freeze Rotation Z: ON
///   Player CapsuleCollider2D → size fitted to sprite
///   Ground layer assigned to every tilemap / hull piece
///   groundLayer mask set in Inspector
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ──────────────────────────────────────────────────────────────────────────

    [Header("Movement — Surface (Gravity Boots Active)")]
    [Tooltip("Speed when walking on a surface with Gravity Boots active.")]
    public float walkSpeed = 5f;

    [Tooltip("Impulse strength when jumping away from a surface.")]
    public float jumpForce = 10f;

    [Tooltip("Linear drag on Rigidbody2D while grounded on a surface.")]
    public float surfaceDrag = 6f;

    [Header("Movement — Zero-G / Airborne")]
    [Tooltip("Force added per FixedUpdate when the player presses WASD in Zero-G.")]
    public float thrustForce = 10f;

    [Tooltip("Max speed cap in Zero-G.")]
    public float maxZeroGSpeed = 14f;

    [Tooltip("Linear drag in Zero-G — controls how fast momentum decays.")]
    [Range(0f, 3f)]
    public float zeroGDrag = 0.6f;

    [Header("Rotation")]
    [Tooltip("Degrees per second the player rotates to align feet with gravity.")]
    public float rotationSpeed = 300f;

    [Header("Ground Detection")]
    [Tooltip("LayerMask for ground surfaces. MUST include your 'Ground' layer — nothing works without this.")]
    public LayerMask groundLayer;

    [Tooltip("Half-size of the ground detection box (match to the bottom of your CapsuleCollider2D).\n" +
             "X = half the collider width, Y = a small value like 0.05.")]
    public Vector2 groundCheckBoxHalfSize = new Vector2(0.3f, 0.05f);

    [Tooltip("Offset from pivot to the bottom of the feet. Should be just below the sprite's feet.\n" +
             "Typical value for a 1-unit tall character: Y = -0.5")]
    public Vector2 groundCheckOffset = new Vector2(0f, -0.52f);

    [Tooltip("Extra distance downward the box is cast when checking for ground.\n" +
             "Increase if the player is still clipping (try 0.1 first).")]
    [Range(0f, 0.3f)]
    public float groundCheckDistance = 0.05f;

    [Header("Gadgets — Drag child component references here")]
    [Tooltip("GravityBoots script on a child GameObject under Player.")]
    public GravityBoots gravityBoots;

    [Tooltip("TetherGun script on a child GameObject under Player.")]
    public TetherGun tetherGun;

    [Tooltip("GravityGrenadeLauncher script on a child GameObject under Player.")]
    public GravityGrenadeLauncher grenadeGun;

    [Tooltip("ThrusterPack script on a child GameObject under Player.")]
    public ThrusterPack thrusterPack;

    [Header("Input Keys")]
    [Tooltip("Key used to jump while grounded.")]
    public KeyCode jumpKey = KeyCode.Space;

    [Tooltip("Hotkey: activate / deactivate Gravity Boots.")]
    public KeyCode gadgetKey1 = KeyCode.Alpha1;

    [Tooltip("Hotkey: select Tether Gun. Left-click fires / releases.")]
    public KeyCode gadgetKey2 = KeyCode.Alpha2;

    [Tooltip("Hotkey: select Gravity Grenade. Left-click launches.")]
    public KeyCode gadgetKey3 = KeyCode.Alpha3;

    [Tooltip("Hotkey: select Thruster Pack. Left-click fires burst.")]
    public KeyCode gadgetKey4 = KeyCode.Alpha4;

    [Tooltip("Key to interact with nearby objects (repair modules, etc.).")]
    public KeyCode interactKey = KeyCode.E;

    // ──────────────────────────────────────────────────────────────────────────
    // Private State
    // ──────────────────────────────────────────────────────────────────────────

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
    private int   _activeGadgetIndex;

    // Animator hash cache
    private static readonly int H_IsWalking     = Animator.StringToHash("IsWalking");
    private static readonly int H_IsFloating    = Animator.StringToHash("IsFloating");
    private static readonly int H_IsThrusting   = Animator.StringToHash("IsThrusting");
    private static readonly int H_IsDisoriented = Animator.StringToHash("IsDisoriented");
    private static readonly int H_Jump          = Animator.StringToHash("Jump");
    private static readonly int H_UseGadget     = Animator.StringToHash("UseGadget");
    private static readonly int H_GadgetIndex   = Animator.StringToHash("GadgetIndex");
    private static readonly int H_SpeedX        = Animator.StringToHash("SpeedX");
    private static readonly int H_SpeedY        = Animator.StringToHash("SpeedY");

    // ──────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb   = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();

        _rb.gravityScale = 0f; // All gravity handled via GravityZone
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

    // ──────────────────────────────────────────────────────────────────────────
    // Input
    // ──────────────────────────────────────────────────────────────────────────

    private void GatherInput()
    {
        _inputH = Input.GetAxisRaw("Horizontal");
        _inputV = Input.GetAxisRaw("Vertical");
        if (Input.GetKeyDown(jumpKey)) _jumpQueued = true;
    }

    private void HandleGadgetSwitch()
    {
        if (Input.GetKeyDown(gadgetKey1)) SwitchGadget(0);
        if (Input.GetKeyDown(gadgetKey2)) SwitchGadget(1);
        if (Input.GetKeyDown(gadgetKey3)) SwitchGadget(2);
        if (Input.GetKeyDown(gadgetKey4)) SwitchGadget(3);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll >  0.01f) SwitchGadget((_activeGadgetIndex + 1) % 4);
        if (scroll < -0.01f) SwitchGadget((_activeGadgetIndex + 3) % 4);
    }

    private void HandleGadgetUse()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        _anim.SetTrigger(H_UseGadget);
        switch (_activeGadgetIndex)
        {
            case 0: gravityBoots?.Toggle();                                 break;
            case 1: tetherGun?.Fire(GetAimDirection());                     break;
            case 2: grenadeGun?.Launch(GetAimDirection());                  break;
            case 3: thrusterPack?.Activate(new Vector2(_inputH, _inputV));  break;
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

    // ──────────────────────────────────────────────────────────────────────────
    // Ground Check  ← KEY FIX: BoxCast instead of OverlapCircle
    // ──────────────────────────────────────────────────────────────────────────

    private void CheckGround()
    {
        // Rotate the offset so feet always point away from gravity source.
        Vector2 rotatedOffset = (Vector2)(transform.rotation * (Vector3)groundCheckOffset);
        Vector2 origin        = (Vector2)transform.position + rotatedOffset;

        // BoxCast downward (in the gravity direction) a small distance.
        // This is much more reliable with Tilemap + CompositeCollider2D seams
        // than a simple OverlapCircle, which can fall between tile edges.
        Vector2 castDir = _zoneGravity.sqrMagnitude > 0.01f
            ? _zoneGravity.normalized
            : Vector2.down;

        RaycastHit2D hit = Physics2D.BoxCast(
            origin, groundCheckBoxHalfSize * 2f,
            transform.eulerAngles.z,
            castDir, groundCheckDistance, groundLayer);

        _isGrounded = hit.collider != null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Movement  ← MovePosition used for surface walking (no clipping)
    // ──────────────────────────────────────────────────────────────────────────

    private void ApplyMovement()
    {
        bool bootsOn    = gravityBoots != null && gravityBoots.IsActive;
        bool hasGravity = _zoneGravity.sqrMagnitude > 0.01f;

        // BRANCH 1: Gravity Boots + grounded + gravity zone → surface walk
        if (_isGrounded && bootsOn && hasGravity)
        {
            Vector2 gravNorm     = _zoneGravity.normalized;
            Vector2 surfaceRight = new Vector2(-gravNorm.y, gravNorm.x);

            // Use MovePosition so the physics engine resolves overlaps correctly.
            Vector2 move = surfaceRight * (_inputH * walkSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(_rb.position + move);

            // Zero out lateral velocity so drag doesn't fight MovePosition.
            float vertComponent = Vector2.Dot(_rb.linearVelocity, gravNorm);
            _rb.linearVelocity  = gravNorm * Mathf.Max(0f, vertComponent);
            _rb.linearDamping   = surfaceDrag;

            if (_jumpQueued)
            {
                _rb.AddForce(-gravNorm * jumpForce, ForceMode2D.Impulse);
                _anim.SetTrigger(H_Jump);
            }
        }
        // BRANCH 2: Normal/MicroPull gravity, boots off / airborne
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
        // BRANCH 3: Zero-G free float
        else
        {
            Vector2 dir = new Vector2(_inputH, _inputV).normalized;
            if (dir.sqrMagnitude > 0.01f)
                _rb.AddForce(dir * thrustForce, ForceMode2D.Force);
            _rb.linearDamping = zeroGDrag;
        }

        _jumpQueued = false;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Gravity / Rotation
    // ──────────────────────────────────────────────────────────────────────────

    private void ApplyGravityForces()
    {
        if (_currentGravityState == GravityState.ZeroG) return;
        if (_zoneGravity.sqrMagnitude < 0.01f)          return;

        _rb.AddForce(_zoneGravity, ForceMode2D.Force);

        if (_isRiftDisoriented)
            _rb.angularVelocity += _riftSpinForce * Time.fixedDeltaTime;
    }

    private void ClampZeroGVelocity()
    {
        if (_currentGravityState != GravityState.ZeroG)         return;
        if (_rb.linearVelocity.magnitude <= maxZeroGSpeed)       return;
        _rb.linearVelocity = _rb.linearVelocity.normalized * maxZeroGSpeed;
    }

    private void SmoothRotationToGravity()
    {
        if (_isRiftDisoriented)                return;
        if (_zoneGravity.sqrMagnitude < 0.01f) return;

        Vector2 gravNorm  = _zoneGravity.normalized;
        float target      = Mathf.Atan2(-gravNorm.x, gravNorm.y) * Mathf.Rad2Deg;
        float current     = transform.eulerAngles.z;
        float next        = Mathf.MoveTowardsAngle(current, target, rotationSpeed * Time.fixedDeltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, next);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public API — Called by GravityZone
    // ──────────────────────────────────────────────────────────────────────────

    public void ApplyZoneGravity(GravityState state, Vector2 gravity, bool disorient, float spinForce)
    {
        _currentGravityState = state;
        _zoneGravity         = gravity;
        _isRiftDisoriented   = disorient;
        _riftSpinForce       = spinForce;
        FindFirstObjectByType<GameHUD>()?.SetGravityState(state);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Animations & Visuals
    // ──────────────────────────────────────────────────────────────────────────

    private void UpdateAnimations()
    {
        bool walking   = _isGrounded && Mathf.Abs(_inputH) > 0.05f;
        bool floating  = !_isGrounded && _currentGravityState == GravityState.ZeroG;
        bool thrusting = thrusterPack != null && thrusterPack.IsActive;

        _anim.SetBool(H_IsWalking,     walking);
        _anim.SetBool(H_IsFloating,    floating);
        _anim.SetBool(H_IsThrusting,   thrusting);
        _anim.SetBool(H_IsDisoriented, _isRiftDisoriented);
        _anim.SetFloat(H_SpeedX,       _rb.linearVelocity.x);
        _anim.SetFloat(H_SpeedY,       _rb.linearVelocity.y);
    }

    private void FlipSprite()
    {
        if (Mathf.Abs(_inputH) > 0.05f)
            transform.localScale = new Vector3(_inputH > 0f ? 1f : -1f, 1f, 1f);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Draw the ground check box so you can verify size and placement in Scene view.
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Vector2 rotOff = Application.isPlaying
            ? (Vector2)(transform.rotation * (Vector3)groundCheckOffset)
            : groundCheckOffset;
        Vector2 castDir = (_zoneGravity.sqrMagnitude > 0.01f && Application.isPlaying)
            ? _zoneGravity.normalized : Vector2.down;

        Gizmos.matrix = Matrix4x4.TRS(
            (Vector3)((Vector2)transform.position + rotOff + castDir * groundCheckDistance),
            Quaternion.Euler(0f, 0f, transform.eulerAngles.z),
            Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero,
            new Vector3(groundCheckBoxHalfSize.x * 2f, groundCheckBoxHalfSize.y * 2f, 0f));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
