using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Gadget aim crosshair.
///
/// FIX — GAME OVER / VICTORY BUTTONS HARD TO PRESS:
///  Root cause: hideCursorWhenActive was hiding the OS cursor AND the custom
///  crosshair image had a Raycast Target = true Image component. This blocked
///  mouse click raycasts from reaching UI buttons behind it.
///
///  Fix 1: All Image components on the crosshair are set to Raycast Target = false
///         so the crosshair never blocks button clicks.
///
///  Fix 2: When a game-over or victory panel is active (any panel with
///         a Canvas Group set to interactable = true is detected), the crosshair
///         is hidden and the cursor is restored so buttons are clickable.
///
///  Fix 3: Added a public HideForPanel() / RestoreForGadget() API so GameHUD
///         can call it directly when panels open/close.
/// </summary>
public class GadgetCrosshair : MonoBehaviour
{
    [Header("Images")]
    public RectTransform crosshairImage;
    public RectTransform radiusIndicator;
    public TextMeshProUGUI rangeLabel;

    [Header("Sprites")]
    public Sprite tetherCrosshairSprite;
    public Sprite grenadeCrosshairSprite;

    [Header("Colors")]
    public Color tetherColor  = new Color(0f, 0.9f, 1f, 0.9f);
    public Color grenadeColor = new Color(0.7f, 0f, 1f, 0.9f);

    [Header("Grenade Radius")]
    public float grenadeVisualRadius = 8f;
    public float pixelsPerUnit = 20f;

    [Header("Cursor")]
    [Tooltip("Hide the OS cursor when a targeting gadget (Tether/Grenade) is active.")]
    public bool hideCursorWhenActive = true;

    // ─────────────────────────────────────────────────────────────────────────
    private Canvas     _canvas;
    private Camera     _uiCamera;
    private Image      _crosshairImg;
    private Image      _radiusImg;
    private int        _activeGadget = -1;
    private bool       _gadgetUnlocked;
    private bool       _forcedHidden;   // set true when a panel forces crosshair off

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _canvas   = GetComponentInParent<Canvas>();
        _uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? _canvas.worldCamera : null;

        if (crosshairImage  != null) _crosshairImg = crosshairImage.GetComponent<Image>();
        if (radiusIndicator != null) _radiusImg    = radiusIndicator.GetComponent<Image>();

        // CRITICAL: disable Raycast Target on all crosshair images
        // so they never block button clicks behind them
        DisableRaycastTargets();

        SetVisible(false);
    }

    private void DisableRaycastTargets()
    {
        foreach (var img in GetComponentsInChildren<Image>(true))
            img.raycastTarget = false;
        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
            tmp.raycastTarget = false;
    }

    private void Update()
    {
        if (_forcedHidden)
        {
            SetVisible(false);
            Cursor.visible = true;
            return;
        }

        bool showCrosshair = (_activeGadget == 1 || _activeGadget == 2) && _gadgetUnlocked;
        SetVisible(showCrosshair);

        if (hideCursorWhenActive)
            Cursor.visible = !showCrosshair;

        if (!showCrosshair) return;

        MoveCrosshairToMouse();

        if (radiusIndicator != null)
        {
            bool showRadius = _activeGadget == 2;
            radiusIndicator.gameObject.SetActive(showRadius);
            if (showRadius)
            {
                float px = grenadeVisualRadius * pixelsPerUnit;
                radiusIndicator.sizeDelta = Vector2.one * px * 2f;
            }
        }

        if (rangeLabel != null)
        {
            rangeLabel.gameObject.SetActive(_activeGadget == 1);
            if (_activeGadget == 1) rangeLabel.text = GetTetherRangeText();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void SetActiveGadget(int index, bool unlocked)
    {
        _activeGadget   = index;
        _gadgetUnlocked = unlocked;

        if (_crosshairImg == null) return;

        switch (index)
        {
            case 1:
                _crosshairImg.sprite     = tetherCrosshairSprite  ?? CreateDefaultCrosshair();
                _crosshairImg.color      = tetherColor;
                crosshairImage.sizeDelta = new Vector2(32f, 32f);
                break;
            case 2:
                _crosshairImg.sprite     = grenadeCrosshairSprite ?? CreateDefaultCrosshair();
                _crosshairImg.color      = grenadeColor;
                crosshairImage.sizeDelta = new Vector2(28f, 28f);
                if (_radiusImg != null) _radiusImg.color = new Color(grenadeColor.r, grenadeColor.g, grenadeColor.b, 0.35f);
                break;
        }
    }

    /// <summary>
    /// Called by GameHUD when a fullscreen panel (game over, victory) becomes active.
    /// Forces the crosshair off and restores the OS cursor so buttons are clickable.
    /// </summary>
    public void HideForPanel()
    {
        _forcedHidden  = true;
        SetVisible(false);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>
    /// Called by GameHUD when the fullscreen panel closes.
    /// Restores normal gadget crosshair behaviour.
    /// </summary>
    public void RestoreForGadget()
    {
        _forcedHidden = false;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void MoveCrosshairToMouse()
    {
        if (crosshairImage == null || _canvas == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.GetComponent<RectTransform>(),
            Input.mousePosition,
            _uiCamera,
            out Vector2 localPoint);
        crosshairImage.anchoredPosition = localPoint;
        if (radiusIndicator != null) radiusIndicator.anchoredPosition = localPoint;
    }

    private void SetVisible(bool visible)
    {
        crosshairImage?.gameObject.SetActive(visible);
        if (!visible && radiusIndicator != null) radiusIndicator.gameObject.SetActive(false);
        if (!visible && rangeLabel     != null) rangeLabel.gameObject.SetActive(false);
    }

    private string GetTetherRangeText()
    {
        if (Camera.main == null) return "";
        Vector3 mw = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mw.z = 0f;
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return "";
        return $"{Vector2.Distance(player.transform.position, mw):F1}m";
    }

    private static Sprite CreateDefaultCrosshair()
    {
        const int size = 32, half = 16, thick = 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++) tex.SetPixel(x, y, Color.clear);
        for (int x = 0; x < size; x++)
        for (int dy = -thick; dy <= thick; dy++) { int py = half + dy; if (py >= 0 && py < size) tex.SetPixel(x, py, Color.white); }
        for (int y = 0; y < size; y++)
        for (int dx = -thick; dx <= thick; dx++) { int px = half + dx; if (px >= 0 && px < size) tex.SetPixel(px, y, Color.white); }
        for (int cy = half-4; cy <= half+4; cy++)
        for (int cx = half-4; cx <= half+4; cx++)
            if (cx >= 0 && cy >= 0 && cx < size && cy < size) tex.SetPixel(cx, cy, Color.clear);
        tex.SetPixel(half, half, Color.white);
        tex.Apply(); tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), size);
    }
}
