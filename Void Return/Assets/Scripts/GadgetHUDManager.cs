using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the gadget slot HUD panel and the active-gadget marker.
///
/// ADDITIONS:
///  — Active gadget marker: a highlighted border/overlay image that moves
///    to sit on top of whichever slot is currently selected.
///    Assign activeMarker (a child RectTransform image) in the Inspector.
///  — Slot name labels: each slot shows the gadget name below the icon
///    when selected (optional textLabels array).
///  — Hotkey labels updated to reflect Q/F/G/V keys.
///  — ShowCrosshair signal: GadgetHUDManager sets a bool on GadgetCrosshair
///    telling it which gadget is active so it can show the right reticle.
///
/// SETUP:
///  Gadget Panel layout (left-to-right): BootsSlot | TetherSlot | GrenadeSlot | ThrusterSlot
///  Each slot needs:
///    - Background Image   → gadgetSlotImages[i]
///    - Icon Image child   → gadgetIconImages[i]
///    - Hotkey TMP child  → hotkeyLabels[i]  (shows Q / F / G / V)
///    - Name TMP child    → slotNameLabels[i] (shows gadget name, optional)
///  Add one extra Image child anywhere in the panel called "ActiveMarker".
///    This image moves to overlay the selected slot. Give it a bright border or glow.
/// </summary>
public class GadgetHUDManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────────
    public static GadgetHUDManager Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("Slot Backgrounds (4 — Boots, Tether, Grenade, Thruster)")]
    [Tooltip("The Image component used as each slot's background panel.")]
    public Image[] gadgetSlotImages;

    [Header("Gadget Icons")]
    [Tooltip("Image child inside each slot that displays the gadget sprite.")]
    public Image[] gadgetIconImages;

    [Tooltip("Sprites for each gadget: [0]=Boots, [1]=Tether, [2]=Grenade, [3]=Thruster.")]
    public Sprite[] gadgetIconSprites;

    [Header("Slot Labels")]
    [Tooltip("TextMeshPro showing the hotkey for each slot (Q, F, G, V).")]
    public TextMeshProUGUI[] hotkeyLabels;

    [Tooltip("Optional TextMeshPro showing the gadget name when it is selected. " +
             "Leave empty to disable name labels.")]
    public TextMeshProUGUI[] slotNameLabels;

    [Tooltip("Display names for each gadget: [0]=Boots, [1]=Tether, [2]=Grenade, [3]=Thruster.")]
    public string[] gadgetNames = { "Gravity Boots", "Tether Gun", "G. Grenade", "Thruster" };

    [Header("Active Gadget Marker")]
    [Tooltip("An Image that moves to overlay the currently selected gadget slot. " +
             "Recommended: a bright border, arrow, or glow image. " +
             "Place it as a sibling of the slot images inside the Gadget Panel.")]
    public RectTransform activeMarker;

    [Tooltip("If true, the active marker smoothly slides to the new slot. " +
             "If false, it snaps instantly.")]
    public bool animateMarker = true;

    [Tooltip("Speed at which the active marker slides to the new slot (pixels/sec).")]
    public float markerMoveSpeed = 800f;

    [Header("Slot Colors")]
    [Tooltip("Slot background color when this slot is the active selection.")]
    public Color activeSlotColor   = new Color(0f, 0.9f, 1f, 0.9f);

    [Tooltip("Slot background color when the slot is unlocked but not selected.")]
    public Color inactiveSlotColor = new Color(0.15f, 0.15f, 0.2f, 0.7f);

    [Tooltip("Slot background color when the slot is locked (gadget not yet unlocked).")]
    public Color lockedSlotColor   = new Color(0.05f, 0.05f, 0.05f, 0.85f);

    [Header("Resource Bars")]
    [Tooltip("Slider showing Gravity Boots stamina.")]
    public Slider bootsStaminaBar;

    [Tooltip("Slider showing Thruster Pack fuel charges.")]
    public Slider thrusterFuelBar;

    [Tooltip("TextMeshPro showing remaining grenade count (e.g. 'x3').")]
    public TextMeshProUGUI grenadeCountText;

    [Header("Crosshair")]
    [Tooltip("Reference to the GadgetCrosshair script (on the crosshair canvas object). " +
             "GadgetHUDManager tells it which gadget is active so it shows the right reticle.")]
    public GadgetCrosshair gadgetCrosshair;

    // ── Private State ─────────────────────────────────────────────────────────

    private readonly bool[] _unlockedSlots = { true, false, false, false };
    private int             _activeIndex   = 0;
    private Vector2         _markerTargetPos;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Assign gadget icons
        if (gadgetIconImages != null && gadgetIconSprites != null)
            for (int i = 0; i < gadgetIconImages.Length && i < gadgetIconSprites.Length; i++)
                if (gadgetIconImages[i] != null && gadgetIconSprites[i] != null)
                    gadgetIconImages[i].sprite = gadgetIconSprites[i];

        // Assign hotkey labels (Q, F, G, V)
        string[] keys = { "Q", "F", "G", "V" };
        if (hotkeyLabels != null)
            for (int i = 0; i < hotkeyLabels.Length && i < keys.Length; i++)
                if (hotkeyLabels[i] != null)
                    hotkeyLabels[i].text = keys[i];

        // Clear name labels initially
        if (slotNameLabels != null)
            foreach (var lbl in slotNameLabels)
                if (lbl != null) lbl.text = "";

        RefreshAllSlots();
        HighlightGadget(0);
    }

    private void Update()
    {
        // Smoothly animate the active marker toward the target position
        if (animateMarker && activeMarker != null &&
            (Vector2)activeMarker.anchoredPosition != _markerTargetPos)
        {
            activeMarker.anchoredPosition = Vector2.MoveTowards(
                activeMarker.anchoredPosition,
                _markerTargetPos,
                markerMoveSpeed * Time.unscaledDeltaTime);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Highlights the selected gadget slot and updates the active marker.
    /// Called by PlayerController.SwitchGadget().
    /// </summary>
    public void HighlightGadget(int index)
    {
        _activeIndex = index;

        // Update slot background colors
        if (gadgetSlotImages != null)
            for (int i = 0; i < gadgetSlotImages.Length; i++)
            {
                if (gadgetSlotImages[i] == null) continue;
                gadgetSlotImages[i].color = !_unlockedSlots[i]    ? lockedSlotColor
                                           : i == index            ? activeSlotColor
                                                                   : inactiveSlotColor;
            }

        // Dim/brighten icons
        if (gadgetIconImages != null)
            for (int i = 0; i < gadgetIconImages.Length; i++)
            {
                if (gadgetIconImages[i] == null) continue;
                gadgetIconImages[i].color = (!_unlockedSlots[i] ? new Color(0.35f,0.35f,0.35f,0.5f)
                                                                : (i == index ? Color.white
                                                                              : new Color(0.7f,0.7f,0.7f,0.8f)));
            }

        // Update slot name labels
        if (slotNameLabels != null && gadgetNames != null)
            for (int i = 0; i < slotNameLabels.Length; i++)
            {
                if (slotNameLabels[i] == null) continue;
                slotNameLabels[i].text = (i == index && i < gadgetNames.Length)
                    ? gadgetNames[i] : "";
            }

        // Move the active marker to the selected slot's position
        MoveMarkerToSlot(index);

        // Tell the crosshair which gadget is active
        gadgetCrosshair?.SetActiveGadget(index, _unlockedSlots[index]);
    }

    /// <summary>
    /// Marks a gadget slot as available or locked. Called by PlayerController on start
    /// and by ShipRepairManager when a module is repaired.
    /// </summary>
    public void SetGadgetAvailable(int index, bool available)
    {
        if (index < 0 || index >= _unlockedSlots.Length) return;
        _unlockedSlots[index] = available;
        RefreshAllSlots();
    }

    /// <summary>
    /// Updates the Gravity Boots stamina bar.
    /// Called by GravityBoots.Update().
    /// </summary>
    public void UpdateBootsBar(float normalized)
    {
        if (bootsStaminaBar != null) bootsStaminaBar.value = normalized;
    }

    /// <summary>
    /// Updates the Thruster Pack fuel bar.
    /// </summary>
    public void UpdateThrusterFuel(float normalized)
    {
        if (thrusterFuelBar != null) thrusterFuelBar.value = normalized;
    }

    /// <summary>
    /// Updates the grenade count label.
    /// </summary>
    public void UpdateGrenadeCount(int count)
    {
        if (grenadeCountText != null) grenadeCountText.text = $"x{count}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshAllSlots()
    {
        if (gadgetSlotImages == null) return;
        for (int i = 0; i < gadgetSlotImages.Length; i++)
        {
            if (gadgetSlotImages[i] == null) continue;
            gadgetSlotImages[i].color = _unlockedSlots[i]
                ? (i == _activeIndex ? activeSlotColor : inactiveSlotColor)
                : lockedSlotColor;

            if (gadgetIconImages != null && i < gadgetIconImages.Length && gadgetIconImages[i] != null)
                gadgetIconImages[i].color = _unlockedSlots[i]
                    ? (i == _activeIndex ? Color.white : new Color(0.7f,0.7f,0.7f,0.8f))
                    : new Color(0.35f,0.35f,0.35f,0.5f);
        }
    }

    private void MoveMarkerToSlot(int index)
    {
        if (activeMarker == null || gadgetSlotImages == null) return;
        if (index < 0 || index >= gadgetSlotImages.Length) return;
        if (gadgetSlotImages[index] == null) return;

        // Get the slot's RectTransform anchored position and use that as the target
        var slotRect = gadgetSlotImages[index].rectTransform;

        // Convert slot position to the marker's parent space
        if (activeMarker.parent is RectTransform markerParent &&
            slotRect.parent is RectTransform slotParent)
        {
            Vector2 slotWorld = slotRect.position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                markerParent, RectTransformUtility.WorldToScreenPoint(null, slotWorld),
                null, out Vector2 localPos);

            _markerTargetPos = slotRect.anchoredPosition;

            // If the slot and marker share the same parent, just copy anchoredPosition
            if (slotRect.parent == activeMarker.parent)
                _markerTargetPos = slotRect.anchoredPosition;
        }

        if (!animateMarker)
            activeMarker.anchoredPosition = _markerTargetPos;
    }
}
