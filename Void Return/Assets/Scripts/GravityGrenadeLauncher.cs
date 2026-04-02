using UnityEngine;

/// <summary>
/// Gravity Grenade Launcher — launches grenades and supports crafting new ones from materials.
///
/// CRAFT SYSTEM:
///  Press the craft key (default: C) while the Grenade gadget is selected
///  to craft one grenade using craftMaterialType × craftMaterialCount from inventory.
///  A notification shows success or what's missing.
/// </summary>
public class GravityGrenadeLauncher : MonoBehaviour
{
    [Header("Grenade Prefab")]
    [Tooltip("GravityGrenade prefab to launch.")]
    public GameObject grenadePrefab;

    [Tooltip("Launch speed in the aim direction.")]
    public float launchSpeed = 12f;

    [Tooltip("Maximum grenades the player can carry.")]
    [Range(1, 10)]
    public int maxGrenades = 3;

    [Header("Grenade Explosion Settings")]
    [Tooltip("Pull radius of the detonated grenade zone.")]
    public float pullRadius    = 8f;

    [Tooltip("Pull force strength.")]
    public float pullStrength  = 10f;

    [Tooltip("Duration of the pull zone in seconds.")]
    public float pullDuration  = 5f;

    [Header("Crafting")]
    [Tooltip("Key to craft one grenade while this gadget is selected.")]
    public KeyCode craftKey = KeyCode.C;

    [Tooltip("Material type required to craft one grenade.")]
    public MaterialType craftMaterialType = MaterialType.MetalScrap;

    [Tooltip("How many of that material are consumed to craft one grenade.")]
    [Range(1, 10)]
    public int craftMaterialCount = 3;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   launchClip;
    public AudioClip   emptyClip;
    public AudioClip   craftClip;

    // ─────────────────────────────────────────────────────────────────────────
    private int _grenadeCount;

    public int GrenadeCount => _grenadeCount;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _grenadeCount = maxGrenades;
    }

    private void Start()
    {
        GadgetHUDManager.Instance?.UpdateGrenadeCount(_grenadeCount);
    }

    private void Update()
    {
        // Craft grenade when C is pressed and this gadget is active
        if (Input.GetKeyDown(craftKey) && gameObject.activeSelf)
            TryCraftGrenade();
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void Launch(Vector2 direction)
    {
        if (grenadePrefab == null) return;

        if (_grenadeCount <= 0)
        {
            audioSource?.PlayOneShot(emptyClip);
            // Format craft material name
            string matName = System.Text.RegularExpressions.Regex.Replace(
                craftMaterialType.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
            int have = Inventory.Instance?.GetCount(craftMaterialType) ?? 0;
            NotificationManager.Instance?.ShowInfo(
                $"No grenades. Press [C] to craft one ({matName} x{craftMaterialCount}, have {have}).");
            return;
        }

        _grenadeCount--;

        var obj = Instantiate(grenadePrefab, transform.position, Quaternion.identity);

        if (obj.TryGetComponent<Rigidbody2D>(out var rb))
            rb.linearVelocity = direction * launchSpeed;

        if (obj.TryGetComponent<GravityGrenade>(out var gg))
        {
            gg.pullRadius   = pullRadius;
            gg.pullStrength = pullStrength;
            gg.duration     = pullDuration;
        }

        audioSource?.PlayOneShot(launchClip);
        GadgetHUDManager.Instance?.UpdateGrenadeCount(_grenadeCount);
    }

    public void AddGrenades(int amount = 1)
    {
        _grenadeCount = Mathf.Min(_grenadeCount + amount, maxGrenades);
        GadgetHUDManager.Instance?.UpdateGrenadeCount(_grenadeCount);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void TryCraftGrenade()
    {
        if (_grenadeCount >= maxGrenades)
        {
            NotificationManager.Instance?.ShowInfo(
                $"Grenades full ({maxGrenades}/{maxGrenades}).");
            return;
        }

        if (Inventory.Instance == null) return;

        string matName = System.Text.RegularExpressions.Regex.Replace(
            craftMaterialType.ToString(), "(?<=[a-z])(?=[A-Z])", " ");

        int have = Inventory.Instance.GetCount(craftMaterialType);
        if (have < craftMaterialCount)
        {
            NotificationManager.Instance?.ShowInfo(
                $"Need {matName} x{craftMaterialCount} to craft a grenade. Have {have}.");
            return;
        }

        bool consumed = Inventory.Instance.ConsumeMaterials(
            craftMaterialType, craftMaterialCount);

        if (!consumed) return;

        _grenadeCount++;
        audioSource?.PlayOneShot(craftClip);
        GadgetHUDManager.Instance?.UpdateGrenadeCount(_grenadeCount);
        NotificationManager.Instance?.ShowInfo(
            $"Grenade crafted! ({_grenadeCount}/{maxGrenades})");
    }
}
