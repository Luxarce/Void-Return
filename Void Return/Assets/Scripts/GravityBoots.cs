using UnityEngine;

/// <summary>
/// Gravity Boots — gravity anchor gadget.
/// Boot particles play ONLY while boots are active. They stop on deactivation or stamina drain.
/// </summary>
public class GravityBoots : MonoBehaviour
{
    [Header("Stamina")]
    public float maxDuration  = 30f;
    public float drainRate    = 1f;
    public float rechargeRate = 0.5f;

    [Header("Boot Gravity")]
    public float bootGravityStrength = 35f;
    public Vector2 bootGravityDirection = Vector2.down;

    [Header("VFX")]
    [Tooltip("Particle System that plays ONLY while boots are active. Set Play on Awake = OFF.")]
    public ParticleSystem bootParticles;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   toggleClip;

    // ─────────────────────────────────────────────────────────────────────────
    private float       _stamina;
    private bool        _isActive;
    private Rigidbody2D _playerRb;

    public bool  IsActive          => _isActive && _stamina > 0f;
    public float StaminaNormalized => maxDuration > 0f ? _stamina / maxDuration : 0f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _stamina  = maxDuration;
        _playerRb = GetComponentInParent<Rigidbody2D>();
    }

    private void Start()
    {
        // Ensure particles are off at start
        if (bootParticles != null) { bootParticles.Stop(); bootParticles.Clear(); }
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
        if (IsActive && _playerRb != null)
            _playerRb.AddForce(bootGravityDirection.normalized * bootGravityStrength, ForceMode2D.Force);
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

    private void Activate()
    {
        if (_stamina <= 0f) { NotificationManager.Instance?.ShowInfo("Boots empty — recharging."); return; }
        _isActive = true;
        StartParticles();
        audioSource?.PlayOneShot(toggleClip);
        NotificationManager.Instance?.ShowInfo("Gravity Boots ON");
    }

    private void Deactivate()
    {
        _isActive = false;
        StopParticles();
        audioSource?.PlayOneShot(toggleClip);
        NotificationManager.Instance?.ShowInfo("Gravity Boots OFF");
    }

    private void StartParticles()
    {
        if (bootParticles == null) return;
        if (!bootParticles.isPlaying) bootParticles.Play();
    }

    private void StopParticles()
    {
        if (bootParticles == null) return;
        if (bootParticles.isPlaying) bootParticles.Stop();
    }
}
