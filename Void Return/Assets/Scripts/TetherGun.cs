using UnityEngine;

/// <summary>
/// Tether Gun gadget — fires a hook that latches onto debris or surfaces.
/// Once hooked, it continuously pulls the target object toward the player.
/// Fire again while hooked to release.
/// Place this script on a child GameObject under the Player.
/// Requires: a LineRenderer component on this same GameObject.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class TetherGun : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Tether Settings")]
    [Tooltip("Maximum range of the tether in world units.")]
    public float tetherRange = 20f;

    [Tooltip("Force applied to the hooked object per frame, pulling it toward the player.")]
    public float pullForce = 15f;

    [Tooltip("LayerMask for objects the tether can hook onto (e.g., Debris, Ground layers).")]
    public LayerMask tetherMask;

    [Header("Audio")]
    [Tooltip("AudioSource on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("Sound when the tether hook fires and connects.")]
    public AudioClip fireClip;

    [Tooltip("Sound when the tether is released.")]
    public AudioClip releaseClip;

    [Tooltip("Sound played when hook impact makes contact.")]
    public AudioClip hookImpactClip;

    [Header("VFX")]
    [Tooltip("Optional particle effect spawned at hook attachment point.")]
    public GameObject hookImpactVFX;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private LineRenderer _line;
    private Rigidbody2D  _hookedRigidbody;
    private bool         _isHooked;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _line         = GetComponent<LineRenderer>();
        _line.enabled = false;
        _line.positionCount = 2;
    }

    private void FixedUpdate()
    {
        if (!_isHooked || _hookedRigidbody == null) return;

        // Pull the hooked object toward the player each physics step
        Vector2 dirToPlayer = (Vector2)transform.position - _hookedRigidbody.position;
        _hookedRigidbody.AddForce(dirToPlayer.normalized * pullForce);

        // Auto-release if the object is very close
        if (dirToPlayer.magnitude < 0.5f)
            Release();
    }

    private void LateUpdate()
    {
        if (!_isHooked || _hookedRigidbody == null) return;

        // Update the tether line visual each frame
        _line.SetPosition(0, transform.position);
        _line.SetPosition(1, _hookedRigidbody.position);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fire the tether in the given direction. If already hooked, releases instead.
    /// Called by PlayerController when gadget 2 is used.
    /// </summary>
    public void Fire(Vector2 direction)
    {
        if (_isHooked)
        {
            Release();
            return;
        }

        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, tetherRange, tetherMask);

        if (hit.collider == null) return;

        // Attach to a Rigidbody2D if present (debris), otherwise tether to a static surface
        if (hit.rigidbody != null)
        {
            _hookedRigidbody = hit.rigidbody;
            _isHooked        = true;
            _line.enabled    = true;

            if (hookImpactVFX != null)
                Instantiate(hookImpactVFX, hit.point, Quaternion.identity);

            audioSource?.PlayOneShot(fireClip);
            audioSource?.PlayOneShot(hookImpactClip);
        }
    }

    /// <summary>
    /// Releases the tether hook.
    /// </summary>
    public void Release()
    {
        _isHooked        = false;
        _hookedRigidbody = null;
        _line.enabled    = false;
        audioSource?.PlayOneShot(releaseClip);
    }
}
