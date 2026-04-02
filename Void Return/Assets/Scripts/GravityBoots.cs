using UnityEngine;

/// <summary>
/// Gravity Boots — a gravity-anchor gadget.
///
/// REDESIGN (no jump):
///   When ACTIVE, applies a strong configurable downward force to the player's
///   Rigidbody2D every FixedUpdate, keeping them firmly planted on any
///   Ground-layer surface. This makes grounded walking work in any gravity zone.
///
///   The boots do NOT provide a jump ability. Remove all jump references
///   from the PlayerController — jumping is no longer part of this gadget.
///
///   Think of the boots as an "anchor" — toggle ON to stick to surfaces,
///   toggle OFF to return to Zero-G floating.
///
/// STAMINA:
///   Drains while active, recharges when off.
///   Shown as the blue bar in the Gadget HUD beneath the Boots slot.
/// </summary>
public class GravityBoots : MonoBehaviour
{
    [Header("Stamina")]
    [Tooltip("Maximum duration boots can stay active (seconds).")]
    public float maxDuration  = 30f;

    [Tooltip("Stamina drained per second while active.")]
    public float drainRate    = 1f;

    [Tooltip("Stamina recharged per second while inactive.")]
    public float rechargeRate = 0.5f;

    [Header("Boot Gravity Anchor")]
    [Tooltip("Downward force applied to the player's Rigidbody2D per FixedUpdate while active.\n" +
             "This is what keeps the player glued to the floor.\n" +
             "Recommended: 25-50. Higher = stronger 'gravity feeling'.")]
    public float bootGravityStrength = 35f;

    [Tooltip("Direction of the boot gravity. Default is straight down (0, -1).\n" +
             "Change this at runtime to match angled surfaces if needed.")]
    public Vector2 bootGravityDirection = Vector2.down;

    [Header("VFX & Audio")]
    [Tooltip("Particle effect playing while boots are active (foot glow).")]
    public ParticleSystem bootParticles;

    [Tooltip("AudioSource on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("Sound played when toggling boots on or off.")]
    public AudioClip toggleClip;

    // ─────────────────────────────────────────────────────────────────────────
    private float        _stamina;
    private bool         _isActive;
    private Rigidbody2D  _playerRb;

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>True when boots are on AND have remaining stamina.</summary>
    public bool IsActive => _isActive && _stamina > 0f;

    /// <summary>Stamina 0-1 for the HUD bar.</summary>
    public float StaminaNormalized => maxDuration > 0f ? _stamina / maxDuration : 0f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _stamina  = maxDuration;
        _playerRb = GetComponentInParent<Rigidbody2D>();
    }

    private void Update()
    {
        if (_isActive && _stamina > 0f)
        {
            _stamina -= drainRate * Time.deltaTime;
            if (_stamina <= 0f)
            {
                _stamina = 0f;
                Deactivate();
                NotificationManager.Instance?.ShowInfo("Gravity Boots drained — recharging.");
            }
        }
        else if (!_isActive && _stamina < maxDuration)
        {
            _stamina = Mathf.Min(_stamina + rechargeRate * Time.deltaTime, maxDuration);
        }

        GadgetHUDManager.Instance?.UpdateBootsBar(StaminaNormalized);
    }

    private void FixedUpdate()
    {
        // Apply the gravity anchor force every physics step while boots are on.
        // This is what keeps the player on the ground in Zero-G environments.
        if (IsActive && _playerRb != null)
        {
            _playerRb.AddForce(
                bootGravityDirection.normalized * bootGravityStrength,
                ForceMode2D.Force);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void Toggle()
    {
        if (_isActive) Deactivate(); else Activate();
    }

    public void ExtendMaxDuration(float bonusSeconds)
    {
        maxDuration += bonusSeconds;
        _stamina    += bonusSeconds;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void Activate()
    {
        if (_stamina <= 0f)
        {
            NotificationManager.Instance?.ShowInfo("Gravity Boots empty — wait for recharge.");
            return;
        }
        _isActive = true;
        bootParticles?.Play();
        audioSource?.PlayOneShot(toggleClip);
        NotificationManager.Instance?.ShowInfo("Gravity Boots ON — anchored to surface.");
    }

    private void Deactivate()
    {
        _isActive = false;
        bootParticles?.Stop();
        audioSource?.PlayOneShot(toggleClip);
        NotificationManager.Instance?.ShowInfo("Gravity Boots OFF — returning to Zero-G.");
    }
}
