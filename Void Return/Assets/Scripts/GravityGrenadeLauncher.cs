using UnityEngine;

/// <summary>
/// Gravity Grenade Launcher — launches a grenade that creates a temporary gravity pull zone.
/// Manages grenade inventory count and communicates with the HUD.
/// Place this script on a child GameObject under the Player.
/// </summary>
public class GravityGrenadeLauncher : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Grenade Settings")]
    [Tooltip("Prefab for the Gravity Grenade projectile. Must have a GravityGrenade script and Rigidbody2D.")]
    public GameObject grenadePrefab;

    [Tooltip("Launch speed applied to the grenade in the aim direction.")]
    public float launchSpeed = 12f;

    [Tooltip("Maximum number of grenades the player can carry at once.")]
    [Range(1, 10)]
    public int maxGrenades = 3;

    [Header("Grenade Explosion Settings (override on prefab defaults)")]
    [Tooltip("Radius of the gravity pull zone the grenade creates on detonation.")]
    public float pullRadius = 8f;

    [Tooltip("Pull force strength applied to objects within radius.")]
    public float pullStrength = 10f;

    [Tooltip("How long the gravity pull zone lasts in seconds.")]
    public float pullDuration = 5f;

    [Header("Audio")]
    [Tooltip("AudioSource on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("Sound played when a grenade is launched.")]
    public AudioClip launchClip;

    [Tooltip("Sound played when the player tries to launch with no grenades remaining.")]
    public AudioClip emptyClip;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private int _grenadeCount;

    // ─────────────────────────────────────────────────────────────────────────
    // Public Properties
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Current number of grenades held by the player.</summary>
    public int GrenadeCount => _grenadeCount;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _grenadeCount = maxGrenades;
    }

    private void Start()
    {
        GadgetHUDManager.Instance?.UpdateGrenadeCount(_grenadeCount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Launch a grenade in the given direction.
    /// Called by PlayerController when gadget 3 is used.
    /// </summary>
    public void Launch(Vector2 direction)
    {
        if (grenadePrefab == null) return;

        if (_grenadeCount <= 0)
        {
            audioSource?.PlayOneShot(emptyClip);
            NotificationManager.Instance?.Show("No grenades remaining!");
            return;
        }

        _grenadeCount--;

        GameObject grenadeObj = Instantiate(grenadePrefab, transform.position, Quaternion.identity);

        // Apply launch velocity
        if (grenadeObj.TryGetComponent<Rigidbody2D>(out var rb))
            rb.linearVelocity = direction * launchSpeed;

        // Pass configuration values to the grenade
        if (grenadeObj.TryGetComponent<GravityGrenade>(out var grenade))
        {
            grenade.pullRadius   = pullRadius;
            grenade.pullStrength = pullStrength;
            grenade.duration     = pullDuration;
        }

        audioSource?.PlayOneShot(launchClip);
        GadgetHUDManager.Instance?.UpdateGrenadeCount(_grenadeCount);
    }

    /// <summary>
    /// Add grenades to the player's stock (e.g., from a pickup).
    /// </summary>
    public void AddGrenades(int amount = 1)
    {
        _grenadeCount = Mathf.Min(_grenadeCount + amount, maxGrenades);
        GadgetHUDManager.Instance?.UpdateGrenadeCount(_grenadeCount);
    }
}
