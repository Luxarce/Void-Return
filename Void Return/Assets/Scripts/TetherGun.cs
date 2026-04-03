using UnityEngine;

/// <summary>
/// Tether Gun — fires a hook that pulls objects or anchors the player.
///
/// BEHAVIOR CHANGE:
///  Previously: first press fires, second press releases.
///  Now: pressing Fire ALWAYS fires a new hook. If a hook is already active,
///  it is automatically released and a new one is immediately fired.
///  This means the player never needs two separate presses —
///  every left-click fires to the new position.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class TetherGun : MonoBehaviour
{
    [Header("Settings")]
    public float tetherRange         = 20f;
    public float pullForce           = 15f;
    public float selfPullForce       = 12f;
    public LayerMask tetherMask;
    public float autoReleaseDistance = 0.8f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   fireClip;
    public AudioClip   releaseClip;
    public AudioClip   hookImpactClip;
    public AudioClip   missClip;

    [Header("VFX")]
    [Tooltip("Spawned at hook contact point. Must have Stop Action = Destroy.")]
    public GameObject hookImpactVFX;
    public float vfxLifetime = 3f;

    // ─────────────────────────────────────────────────────────────────────────
    private LineRenderer _line;
    private Rigidbody2D  _hookedRigidbody;
    private Vector2      _hookedPoint;
    private bool         _isHooked;
    private bool         _isStaticHook;
    private Rigidbody2D  _playerRb;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _line               = GetComponent<LineRenderer>();
        _line.enabled       = false;
        _line.positionCount = 2;
        _playerRb           = GetComponentInParent<Rigidbody2D>();

        if (tetherMask.value == 0)
            Debug.LogWarning("[TetherGun] tetherMask is Nothing — tether will never connect.");
    }

    private void FixedUpdate()
    {
        if (!_isHooked) return;

        if (_isStaticHook)
        {
            if (_playerRb == null) return;
            Vector2 toHook = _hookedPoint - (Vector2)transform.position;
            if (toHook.magnitude < autoReleaseDistance) { Release(); return; }
            _playerRb.AddForce(toHook.normalized * selfPullForce, ForceMode2D.Force);
        }
        else
        {
            if (_hookedRigidbody == null) { Release(); return; }
            Vector2 toPlayer = (Vector2)transform.position - _hookedRigidbody.position;
            if (toPlayer.magnitude < autoReleaseDistance) { Release(); return; }
            _hookedRigidbody.AddForce(toPlayer.normalized * pullForce, ForceMode2D.Force);
        }
    }

    private void LateUpdate()
    {
        if (!_isHooked) return;
        _line.SetPosition(0, transform.position);
        _line.SetPosition(1, _isStaticHook
            ? (Vector3)(Vector2)_hookedPoint
            : (Vector3)_hookedRigidbody.position);
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires the tether toward the given direction.
    /// If a hook is already active, it is AUTOMATICALLY released first,
    /// then a new hook is immediately fired. Single press = always fires.
    /// </summary>
    public void Fire(Vector2 direction)
    {
        // Auto-release any existing hook before firing a new one
        if (_isHooked)
            Release(silent: true);  // silent release — no sound, immediately fire new hook

        if (tetherMask.value == 0)
        {
            NotificationManager.Instance?.ShowInfo(
                "Tether failed — no target layers set. Check Inspector.");
            return;
        }

        RaycastHit2D hit = Physics2D.Raycast(
            transform.position, direction, tetherRange, tetherMask);

        if (hit.collider == null)
        {
            audioSource?.PlayOneShot(missClip);
            return;
        }

        _isHooked     = true;
        _line.enabled = true;
        audioSource?.PlayOneShot(fireClip);
        audioSource?.PlayOneShot(hookImpactClip);

        if (hookImpactVFX != null)
        {
            var vfx = Instantiate(hookImpactVFX, hit.point, Quaternion.identity);
            Destroy(vfx, vfxLifetime);
        }

        if (hit.rigidbody != null && hit.rigidbody.bodyType == RigidbodyType2D.Dynamic)
        {
            _hookedRigidbody = hit.rigidbody;
            _isStaticHook    = false;
        }
        else
        {
            _hookedPoint  = hit.point;
            _isStaticHook = true;
        }
    }

    public void Release() => Release(silent: false);

    private void Release(bool silent)
    {
        _isHooked        = false;
        _isStaticHook    = false;
        _hookedRigidbody = null;
        _line.enabled    = false;
        if (!silent) audioSource?.PlayOneShot(releaseClip);
    }
}
