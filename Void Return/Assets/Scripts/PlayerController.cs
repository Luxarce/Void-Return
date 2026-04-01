using UnityEngine;

/// <summary>
/// Main player movement, gadget controller and animation driver.
///
/// ═══════════════════════════════════════════════════════════
///  HOW THE PLAYER GADGETS WORK
/// ═══════════════════════════════════════════════════════════
///
///  SELECTING A GADGET
///  ──────────────────
///  Press the gadget key to SELECT that gadget (highlights it in HUD).
///  Then press LEFT MOUSE BUTTON to ACTIVATE / USE it.
///
///  Q — GRAVITY BOOTS
///  Each press of Q selects the boots slot. LMB TOGGLES them ON or OFF.
///  • When ON:  the player can walk on any surface that has a gravity zone.
///              Boots drain stamina (the blue bar) — they auto-turn off at 0.
///  • When OFF: the boots recharge automatically.
///  • Stamina bar: shown in the HUD below the Boots slot.
///
///  F — TETHER GUN  (unlocked after repairing Navigation)
///  Press F to select, then LMB to FIRE the hook in the mouse-aim direction.
///  • First click: fires hook. A cyan cable line extends to the hooked object.
///  • Second click: releases the hook.
///  • While hooked: the target Rigidbody2D is pulled toward the player.
///    Use it to pull debris, collect distant materials, or anchor yourself.
///
///  G — GRAVITY GRENADE  (unlocked after repairing Hull Plating)
///  Press G to select, then LMB to LAUNCH a grenade in the mouse-aim direction.
///  • After ~1.5 seconds it DETONATES and creates a temporary pull zone.
///  • Everything with a Rigidbody2D within pullRadius is drawn toward the
///    explosion point for 'duration' seconds.
///  • Excellent for clustering floating debris before collecting it.
///  • Grenade count shown in HUD (x3 by default). Replenish via pickups.
///
///  V — THRUSTER PACK  (unlocked after repairing Engine Core)
///  Press V to select, then HOLD LMB to fire thrust in the WASD input direction.
///  • In Zero-G: provides strong directional acceleration beyond normal float.
///  • Fuel depletes while active (orange bar). Refuel via Fuel Cell pickups.
///  • Most effective for quick escapes from MicroPull asteroid zones.
///
///  SCROLL WHEEL cycles through all 4 gadgets (if allowScrollCycle is ON).
///
/// ═══════════════════════════════════════════════════════════
///  FIX NOTES (this version):
///  — HandleGadgetSwitch now uses Input.GetKeyDown in Update (frame-accurate).
///  — HandleGadgetUse separated from HandleGadgetSwitch so pressing Q
///    no longer immediately activates the gadget — it only selects it.
///    The PLAYER must then press LMB to actually fire/toggle.
///  — Pressing a gadget key while that gadget is already selected now
///    ACTIVATES it directly (double-tap convenience shortcut).
///  — ApplyStartingGadgetToggles now prints to Console for easy debugging.
///  — Rotation clamp uses signed angle arithmetic to prevent 360° flip.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Movement — Surface (Gravity Boots Active)")]
    public float walkSpeed   = 5f;
    public float jumpForce   = 10f;
    public float surfaceDrag = 6f;

    [Header("Movement — Zero-G / Airborne")]
    public float thrustForce    = 10f;
    public float maxZeroGSpeed  = 14f;
    [Range(0f, 3f)]
    public float zeroGDrag      = 0.6f;

    [Header("Rotation & Tilt")]
    [Tooltip("Speed at which the player tilts to match gravity direction (deg/sec).")]
    public float rotationSpeed  = 200f;

    [Tooltip("Maximum tilt from upright (0°). 90° = wall walking allowed. " +
             "0° = always upright. 180° = full rotation.")]
    [Range(0f, 180f)]
    public float maxTiltDegrees = 90f;

    [Header("Ground Detection")]
    [Tooltip("LayerMask for surfaces the player can stand on. Must include 'Ground' layer.")]
    public LayerMask groundLayer;

    [Tooltip("Radius of the ground overlap circle at the player's feet.")]
    [Range(0.05f, 0.5f)]
    public float groundCheckRadius = 0.18f;

    [Tooltip("Offset from pivot to the feet position. Rotates with the player.")]
    public Vector2 groundCheckOffset = new Vector2(0f, -0.55f);

    [Header("Gadget References")]
    [Tooltip("GravityBoots child script. Press Q to select, LMB to toggle.")]
    public GravityBoots gravityBoots;

    [Tooltip("TetherGun child script. Press F to select, LMB to fire/release.")]
    public TetherGun tetherGun;

    [Tooltip("GravityGrenadeLauncher child script. Press G to select, LMB to launch.")]
    public GravityGrenadeLauncher grenadeGun;

    [Tooltip("ThrusterPack child script. Press V to select, LMB to activate.")]
    public ThrusterPack thrusterPack;

    [Header("Gadget Keys")]
    [Tooltip("Key to SELECT the Gravity Boots slot. Press LMB to toggle ON/OFF.")]
    public KeyCode gadgetKeyBoots    = KeyCode.Q;

    [Tooltip("Key to SELECT the Tether Gun slot. Press LMB to fire or release.")]
    public KeyCode gadgetKeyTether   = KeyCode.F;

    [Tooltip("Key to SELECT the Gravity Grenade slot. Press LMB to launch.")]
    public KeyCode gadgetKeyGrenade  = KeyCode.G;

    [Tooltip("Key to SELECT the Thruster Pack slot. Press LMB to activate.")]
    public KeyCode gadgetKeyThruster = KeyCode.V;

    [Tooltip("Scroll mouse wheel to cycle through gadgets.")]
    public bool allowScrollCycle = true;

    [Tooltip("Key to interact with nearby objects (repair modules, etc.).")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Gadget Start Toggles (Debug / Testing)")]
    [Tooltip("ON = Gravity Boots are available from the start of the game.\n" +
             "OFF = Boots only available after repairing Life Support.")]
    public bool enableBootsAtStart    = true;

    [Tooltip("ON = Tether Gun available from start (bypasses Navigation repair).")]
    public bool enableTetherAtStart   = false;

    [Tooltip("ON = Gravity Grenade available from start (bypasses Hull Plating repair).")]
    public bool enableGrenadeAtStart  = false;

    [Tooltip("ON = Thruster Pack available from start (bypasses Engine Core repair).")]
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

    // ─── Animator Hashes ──────────────────────────────────────────────────────
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
        SwitchGadget(0); // Default to boots slot on start
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
        ApplyGravityForces();
        ClampZeroGVelocity();
        SmoothRotationToGravity();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Startup
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyStartingGadgetToggles()
    {
        if (gravityBoots  != null) { gravityBoots.enabled = enableBootsAtStart;
            Debug.Log($"[Player] Gravity Boots enabled: {enableBootsAtStart}"); }

        if (tetherGun     != null) { tetherGun.gameObject.SetActive(enableTetherAtStart);
            Debug.Log($"[Player] Tether Gun enabled: {enableTetherAtStart}"); }

        if (grenadeGun    != null) { grenadeGun.gameObject.SetActive(enableGrenadeAtStart);
            Debug.Log($"[Player] Grenade enabled: {enableGrenadeAtStart}"); }

        if (thrusterPack  != null) { thrusterPack.gameObject.SetActive(enableThrusterAtStart);
            Debug.Log($"[Player] Thruster enabled: {enableThrusterAtStart}"); }

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

    /// <summary>
    /// Pressing a gadget key SELECTS that slot (shows in HUD).
    /// Pressing the SAME key again while already selected also ACTIVATES it
    /// (double-tap shortcut — so Q Q toggles boots without needing LMB).
    /// </summary>
    private void HandleGadgetSwitch()
    {
        if (Input.GetKeyDown(gadgetKeyBoots))
        {
            if (_activeGadgetIndex == 0) ActivateCurrentGadget();
            else SwitchGadget(0);
        }
        if (Input.GetKeyDown(gadgetKeyTether))
        {
            if (_activeGadgetIndex == 1) ActivateCurrentGadget();
            else SwitchGadget(1);
        }
        if (Input.GetKeyDown(gadgetKeyGrenade))
        {
            if (_activeGadgetIndex == 2) ActivateCurrentGadget();
            else SwitchGadget(2);
        }
        if (Input.GetKeyDown(gadgetKeyThruster))
        {
            if (_activeGadgetIndex == 3) ActivateCurrentGadget();
            else SwitchGadget(3);
        }

        if (allowScrollCycle)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0.01f)  SwitchGadget((_activeGadgetIndex + 1) % 4);
            if (scroll < -0.01f) SwitchGadget((_activeGadgetIndex + 3) % 4);
        }
    }

    /// <summary>
    /// Left Mouse Button activates the currently selected gadget.
    /// This is separate from switching so the player has clear two-step control.
    /// </summary>
    private void HandleGadgetActivate()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        ActivateCurrentGadget();
    }

    private void ActivateCurrentGadget()
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
        Debug.Log($"[Player] Gadget slot {index} selected.");
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

        if (_isGrounded && bootsOn && hasGravity)
        {
            Vector2 gravNorm     = _zoneGravity.normalized;
            Vector2 surfaceRight = new Vector2(-gravNorm.y, gravNorm.x);
            float   intoSurface  = Mathf.Max(0f, Vector2.Dot(_rb.linearVelocity, gravNorm));
            _rb.linearVelocity   = surfaceRight * (_inputH * walkSpeed) + gravNorm * intoSurface;
            _rb.linearDamping    = surfaceDrag;

            if (_jumpQueued)
            {
                _rb.AddForce(-gravNorm * jumpForce, ForceMode2D.Impulse);
                _anim.SetTrigger(H_Jump);
            }
        }
        else if (hasGravity && _currentGravityState != GravityState.ZeroG)
        {
            _rb.linearVelocity  = new Vector2(_inputH * walkSpeed, _rb.linearVelocity.y);
            _rb.linearDamping   = _isGrounded ? surfaceDrag : zeroGDrag;
            if (_jumpQueued && _isGrounded)
            {
                _rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
                _anim.SetTrigger(H_Jump);
            }
        }
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

    private void SmoothRotationToGravity()
    {
        if (_isRiftDisoriented) return;

        float targetAngle = 0f;
        if (_zoneGravity.sqrMagnitude > 0.01f)
        {
            Vector2 g = _zoneGravity.normalized;
            float rawAngle = Mathf.Atan2(-g.x, g.y) * Mathf.Rad2Deg;
            // Clamp to ±maxTiltDegrees from upright (0°)
            targetAngle = Mathf.Clamp(
                Mathf.DeltaAngle(0f, rawAngle),
                -maxTiltDegrees, maxTiltDegrees);
        }

        // Use signed current angle to avoid 0°/360° discontinuity
        float current = transform.eulerAngles.z;
        float signed  = current > 180f ? current - 360f : current;
        float next    = Mathf.MoveTowardsAngle(signed, targetAngle,
                                                rotationSpeed * Time.fixedDeltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, next);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
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
    // Animation & Visual
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
