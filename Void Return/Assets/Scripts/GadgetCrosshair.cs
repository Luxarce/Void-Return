using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Draws an aim crosshair / reticle on screen when the Tether Gun or
/// Gravity Grenade Launcher is the active gadget.
///
/// The crosshair follows the mouse cursor and changes appearance per gadget:
///   Boots (0)    — hidden (no aim needed)
///   Tether (1)   — a simple cross/dot reticle (cyan), shows range arc
///   Grenade (2)  — a circular reticle with a small explosion radius indicator
///   Thruster (3) — hidden (dash direction shown by WASD, no cursor aim)
///
/// SETUP:
///  1. Create an empty GameObject inside your Screen-Space UI Canvas.
///     Name it 'GadgetCrosshair'. Set it to always active.
///  2. Add GadgetCrosshair script to it.
///  3. Assign all Image/RectTransform references in the Inspector.
///  4. Drag GadgetCrosshair into GadgetHUDManager → Gadget Crosshair field.
///
/// PREFAB STRUCTURE for the crosshair GameObject:
///   GadgetCrosshair (script here)
///   ├── CrosshairImage      — Image (crosshair sprite — simple cross or dot)
///   ├── RadiusIndicator     — Image (circle outline, shown for grenade range)
///   └── RangeLabel          — TextMeshProUGUI (optional range number)
/// </summary>
public class GadgetCrosshair : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Crosshair Images")]
    [Tooltip("The main crosshair image (cross, dot, or reticle sprite). " +
             "This moves to the mouse cursor position.")]
    public RectTransform crosshairImage;

    [Tooltip("A circular ring image shown around the crosshair when the Grenade is active. " +
             "Represents the grenade's pull radius. Use a circle outline sprite.")]
    public RectTransform radiusIndicator;

    [Tooltip("Optional text label showing aim distance or range info.")]
    public TextMeshProUGUI rangeLabel;

    [Header("Crosshair Sprites")]
    [Tooltip("Crosshair sprite used for the Tether Gun (sharp cross / target).")]
    public Sprite tetherCrosshairSprite;

    [Tooltip("Crosshair sprite used for the Gravity Grenade (circular target / dot).")]
    public Sprite grenadeCrosshairSprite;

    [Header("Colors")]
    [Tooltip("Tether Gun crosshair color.")]
    public Color tetherColor   = new Color(0f, 0.9f, 1f, 0.9f);

    [Tooltip("Grenade crosshair color.")]
    public Color grenadeColor  = new Color(0.7f, 0f, 1f, 0.9f);

    [Header("Grenade Radius Indicator")]
    [Tooltip("The pull radius of the gravity grenade (matches GravityGrenadeLauncher.pullRadius). " +
             "This scales the radius ring on screen.")]
    public float grenadeVisualRadius = 8f;

    [Tooltip("Pixels per world unit for scaling the radius ring on screen. " +
             "Approximate: Camera.orthographicSize maps to half the screen height. " +
             "Default 20 is a reasonable starting point — adjust per your camera zoom.")]
    public float pixelsPerUnit = 20f;

    [Header("Hide Cursor")]
    [Tooltip("If true, hides the OS mouse cursor when a crosshair gadget is active " +
             "and restores it otherwise.")]
    public bool hideCursorWhenActive = true;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Canvas     _canvas;
    private Camera     _uiCamera;
    private Image      _crosshairImg;
    private Image      _radiusImg;
    private int        _activeGadget  = -1;
    private bool       _gadgetUnlocked;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _canvas       = GetComponentInParent<Canvas>();
        _uiCamera     = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                        ? _canvas.worldCamera : null;

        if (crosshairImage  != null) _crosshairImg = crosshairImage.GetComponent<Image>();
        if (radiusIndicator != null) _radiusImg    = radiusIndicator.GetComponent<Image>();

        // Start hidden
        SetVisible(false);
    }

    private void Update()
    {
        bool showCrosshair = (_activeGadget == 1 || _activeGadget == 2) && _gadgetUnlocked;

        SetVisible(showCrosshair);

        if (hideCursorWhenActive)
            Cursor.visible = !showCrosshair;

        if (!showCrosshair) return;

        // Move the crosshair to the mouse cursor position in UI space
        MoveCrosshairToMouse();

        // Show radius ring only for the grenade
        if (radiusIndicator != null)
        {
            bool showRadius = _activeGadget == 2;
            radiusIndicator.gameObject.SetActive(showRadius);

            if (showRadius)
            {
                // Scale the ring to represent the grenade pull radius in screen pixels
                float screenPixelRadius = grenadeVisualRadius * pixelsPerUnit;
                radiusIndicator.sizeDelta = Vector2.one * screenPixelRadius * 2f;
            }
        }

        // Show range label
        if (rangeLabel != null)
        {
            rangeLabel.gameObject.SetActive(_activeGadget == 1);
            if (_activeGadget == 1)
                rangeLabel.text = GetTetherRangeText();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — called by GadgetHUDManager.HighlightGadget()
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by GadgetHUDManager whenever the player switches gadgets.
    /// index: 0=Boots, 1=Tether, 2=Grenade, 3=Thruster
    /// </summary>
    public void SetActiveGadget(int index, bool unlocked)
    {
        _activeGadget   = index;
        _gadgetUnlocked = unlocked;

        if (_crosshairImg == null) return;

        switch (index)
        {
            case 1: // Tether
                _crosshairImg.sprite = tetherCrosshairSprite  != null
                    ? tetherCrosshairSprite  : CreateDefaultCrosshair();
                _crosshairImg.color  = tetherColor;
                crosshairImage.sizeDelta = new Vector2(32f, 32f);
                break;

            case 2: // Grenade
                _crosshairImg.sprite = grenadeCrosshairSprite != null
                    ? grenadeCrosshairSprite : CreateDefaultCrosshair();
                _crosshairImg.color  = grenadeColor;
                crosshairImage.sizeDelta = new Vector2(28f, 28f);
                if (_radiusImg != null) _radiusImg.color = new Color(grenadeColor.r,
                    grenadeColor.g, grenadeColor.b, 0.35f);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void MoveCrosshairToMouse()
    {
        if (crosshairImage == null) return;

        // Convert mouse screen position to the UI canvas local position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.GetComponent<RectTransform>(),
            Input.mousePosition,
            _uiCamera,
            out Vector2 localPoint);

        crosshairImage.anchoredPosition = localPoint;

        // Move the radius indicator to the same position
        if (radiusIndicator != null)
            radiusIndicator.anchoredPosition = localPoint;
    }

    private void SetVisible(bool visible)
    {
        crosshairImage?.gameObject.SetActive(visible);
        // Radius indicator is separately controlled inside Update
        if (!visible && radiusIndicator != null)
            radiusIndicator.gameObject.SetActive(false);
        if (!visible && rangeLabel != null)
            rangeLabel.gameObject.SetActive(false);
    }

    private string GetTetherRangeText()
    {
        if (Camera.main == null) return "";
        // Calculate distance from player to mouse in world units
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return "";
        float dist = Vector2.Distance(player.transform.position, mouseWorld);
        return $"{dist:F1}m";
    }

    /// <summary>
    /// Generates a simple white cross sprite procedurally when no sprite is assigned.
    /// </summary>
    private static Sprite CreateDefaultCrosshair()
    {
        const int size = 32;
        const int half = size / 2;
        const int thickness = 2;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        // Fill transparent
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            tex.SetPixel(x, y, Color.clear);

        // Draw horizontal bar
        for (int x = 0; x < size; x++)
        for (int dy = -thickness; dy <= thickness; dy++)
        {
            int py = half + dy;
            if (py >= 0 && py < size) tex.SetPixel(x, py, Color.white);
        }

        // Draw vertical bar (center gap for cleaner look)
        for (int y = 0; y < size; y++)
        for (int dx = -thickness; dx <= thickness; dx++)
        {
            int px = half + dx;
            if (px >= 0 && px < size) tex.SetPixel(px, y, Color.white);
        }

        // Small gap in the center so there's a dot-free center
        for (int cy = half - 4; cy <= half + 4; cy++)
        for (int cx = half - 4; cx <= half + 4; cx++)
            if (cx >= 0 && cy >= 0 && cx < size && cy < size)
                tex.SetPixel(cx, cy, Color.clear);

        // Center dot
        tex.SetPixel(half, half, Color.white);

        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, size, size),
                              new Vector2(0.5f, 0.5f), size);
    }
}
