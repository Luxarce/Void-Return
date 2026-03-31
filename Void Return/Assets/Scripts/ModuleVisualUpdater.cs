using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Swaps a ShipModule's sprite and enables a repair glow light when it is fully repaired.
///
/// SETUP:
/// 1. Attach this script to the same GameObject as ShipModule.cs.
/// 2. Assign the damagedSprite and repairedSprite in the Inspector.
/// 3. Optionally assign a Light2D child for the repair glow.
/// 4. In the ShipModule Inspector → onModuleRepaired event → click + →
///    drag this GameObject → select ModuleVisualUpdater.SetRepaired().
/// </summary>
public class ModuleVisualUpdater : MonoBehaviour
{
    [Header("Sprite States")]
    [Tooltip("Sprite displayed when the module is broken/unrepaired. Assign the damaged-state sprite here.")]
    public Sprite damagedSprite;

    [Tooltip("Sprite displayed when the module is fully repaired. Assign the repaired/glowing state sprite here.")]
    public Sprite repairedSprite;

    [Header("Repair Glow Light")]
    [Tooltip("Optional: a 2D Point Light child on this GameObject. Starts disabled; enabled on full repair.")]
    public Light2D moduleLight;

    [Tooltip("Color the light turns on repair. Should match the module identity color.")]
    public Color repairLightColor = new Color(0f, 0.9f, 1f);

    [Tooltip("Intensity of the repair light when active.")]
    [Range(0f, 5f)]
    public float repairLightIntensity = 1.2f;

    private SpriteRenderer _sr;

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();

        // Start in damaged state
        if (_sr != null && damagedSprite != null)
            _sr.sprite = damagedSprite;

        // Light starts off
        if (moduleLight != null)
            moduleLight.enabled = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Called when the module is fully repaired.
    /// Wire this to ShipModule.onModuleRepaired in the Inspector.
    /// </summary>
    public void SetRepaired()
    {
        if (_sr != null && repairedSprite != null)
            _sr.sprite = repairedSprite;

        if (moduleLight != null)
        {
            moduleLight.enabled   = true;
            moduleLight.color     = repairLightColor;
            moduleLight.intensity = repairLightIntensity;
        }
    }
}
