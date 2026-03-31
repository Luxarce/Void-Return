using UnityEngine;

/// <summary>
/// Spawns world-space warning indicators at a position to show where a meteorite will land.
/// The indicator is a self-destroying prefab (flashing ring or arrow).
///
/// Create one instance in the scene. Assign the warningIndicatorPrefab in the Inspector.
/// Use: WarningManager.Instance.ShowWarning(worldPosition, duration);
/// </summary>
public class WarningManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static WarningManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Warning Indicator")]
    [Tooltip("World-space prefab that marks where a meteorite will hit. " +
             "Should have an Animator or script that loops a flashing animation " +
             "and is set to destroy itself after the given duration.")]
    public GameObject warningIndicatorPrefab;

    [Tooltip("Scale multiplier applied to the warning indicator. Increase for visibility.")]
    public float indicatorScale = 1.5f;

    [Header("Screen-Edge Arrow")]
    [Tooltip("Optional prefab for a screen-edge arrow that points toward an off-screen meteorite target.")]
    public GameObject screenEdgeArrowPrefab;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a warning indicator at the given world position that lasts for the given duration.
    /// Called by MeteoriteManager before each incoming meteorite.
    /// </summary>
    public void ShowWarning(Vector2 worldPosition, float duration)
    {
        if (warningIndicatorPrefab == null) return;

        GameObject indicator = Instantiate(
            warningIndicatorPrefab,
            worldPosition,
            Quaternion.identity
        );

        indicator.transform.localScale = Vector3.one * indicatorScale;

        // Self-destruct after duration
        Destroy(indicator, duration);
    }
}
