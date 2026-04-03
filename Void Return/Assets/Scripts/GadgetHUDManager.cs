using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the gadget slot HUD panel.
///
/// ADDITION: Gadget Activation Feedback
///  When the player switches or activates a gadget slot, the selected slot
///  flashes briefly (activationFlashColor) then returns to activeSlotColor.
///  This gives a clear visual signal that the gadget selection changed.
///
///  Flash is driven by a lerp in Update() — no coroutines needed.
/// </summary>
public class GadgetHUDManager : MonoBehaviour
{
    public static GadgetHUDManager Instance { get; private set; }

    [Header("Slot Backgrounds (4 slots: Boots, Tether, Grenade, Thruster)")]
    public Image[] gadgetSlotImages;

    [Header("Gadget Icons")]
    public Image[]  gadgetIconImages;
    public Sprite[] gadgetIconSprites;

    [Header("Slot Labels")]
    public TextMeshProUGUI[] hotkeyLabels;
    public TextMeshProUGUI[] slotNameLabels;
    public string[] gadgetNames = { "Gravity Boots", "Tether Gun", "G. Grenade", "Thruster" };

    [Header("Active Marker (slides to selected slot)")]
    [Tooltip("A RectTransform image that slides to the currently selected slot.")]
    public RectTransform activeMarker;

    [Tooltip("If true, the marker slides smoothly instead of snapping.")]
    public bool animateMarker = true;

    [Tooltip("Slide speed in pixels per second.")]
    public float markerMoveSpeed = 800f;

    [Header("Slot Colors")]
    [Tooltip("Background color of the currently selected slot.")]
    public Color activeSlotColor   = new Color(0f, 0.9f, 1f, 0.9f);

    [Tooltip("Background color of unlocked but unselected slots.")]
    public Color inactiveSlotColor = new Color(0.15f, 0.15f, 0.2f, 0.7f);

    [Tooltip("Background color of locked (unavailable) slots.")]
    public Color lockedSlotColor   = new Color(0.05f, 0.05f, 0.05f, 0.85f);

    [Header("Activation Flash Feedback")]
    [Tooltip("Color the selected slot flashes to when the player switches gadgets. " +
             "A bright white or accent flash gives clear activation feedback.")]
    public Color activationFlashColor = new Color(1f, 1f, 0.6f, 1f);

    [Tooltip("Duration of the activation flash in seconds.")]
    [Range(0.05f, 0.5f)]
    public float activationFlashDuration = 0.18f;

    [Header("Resource Bars")]
    public Slider          bootsStaminaBar;
    public Slider          thrusterFuelBar;
    public TextMeshProUGUI grenadeCountText;

    [Header("Crosshair")]
    public GadgetCrosshair gadgetCrosshair;

    // ─────────────────────────────────────────────────────────────────────────
    private readonly bool[] _unlockedSlots = { true, false, false, false };
    private int    _activeIndex;
    private Vector2 _markerTargetPos;

    // Flash state
    private float _flashTimer;
    private int   _flashSlotIndex = -1;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (gadgetIconImages != null && gadgetIconSprites != null)
            for (int i = 0; i < gadgetIconImages.Length && i < gadgetIconSprites.Length; i++)
                if (gadgetIconImages[i] != null && gadgetIconSprites[i] != null)
                    gadgetIconImages[i].sprite = gadgetIconSprites[i];

        string[] keys = { "Q", "F", "G", "V" };
        if (hotkeyLabels != null)
            for (int i = 0; i < hotkeyLabels.Length && i < keys.Length; i++)
                if (hotkeyLabels[i] != null) hotkeyLabels[i].text = keys[i];

        if (slotNameLabels != null)
            foreach (var lbl in slotNameLabels)
                if (lbl != null) lbl.text = "";

        RefreshAllSlots();
        HighlightGadget(0);
    }

    private void Update()
    {
        // Animate active marker slide
        if (animateMarker && activeMarker != null &&
            (Vector2)activeMarker.anchoredPosition != _markerTargetPos)
        {
            activeMarker.anchoredPosition = Vector2.MoveTowards(
                activeMarker.anchoredPosition,
                _markerTargetPos,
                markerMoveSpeed * Time.unscaledDeltaTime);
        }

        // Decay the activation flash
        if (_flashTimer > 0f && _flashSlotIndex >= 0)
        {
            _flashTimer -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_flashTimer / activationFlashDuration);

            if (gadgetSlotImages != null && _flashSlotIndex < gadgetSlotImages.Length
                && gadgetSlotImages[_flashSlotIndex] != null)
            {
                // Lerp from flash color back to active color
                gadgetSlotImages[_flashSlotIndex].color = Color.Lerp(
                    activeSlotColor, activationFlashColor, t);
            }

            if (_flashTimer <= 0f)
            {
                _flashSlotIndex = -1;
                RefreshAllSlots(); // restore correct colors
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Highlights the selected gadget slot, moves the active marker, and triggers the flash.
    /// Called by PlayerController.SwitchGadget().
    /// </summary>
    public void HighlightGadget(int index)
    {
        _activeIndex = index;

        RefreshAllSlots();
        MoveMarkerToSlot(index);

        // Update name labels
        if (slotNameLabels != null && gadgetNames != null)
            for (int i = 0; i < slotNameLabels.Length; i++)
            {
                if (slotNameLabels[i] == null) continue;
                slotNameLabels[i].text = (i == index && i < gadgetNames.Length)
                    ? gadgetNames[i] : "";
            }

        // Trigger activation flash on the newly selected slot
        _flashSlotIndex = index;
        _flashTimer     = activationFlashDuration;
        if (gadgetSlotImages != null && index < gadgetSlotImages.Length
            && gadgetSlotImages[index] != null)
            gadgetSlotImages[index].color = activationFlashColor;

        gadgetCrosshair?.SetActiveGadget(index, _unlockedSlots.Length > index && _unlockedSlots[index]);
    }

    public void SetGadgetAvailable(int index, bool available)
    {
        if (index < 0 || index >= _unlockedSlots.Length) return;
        _unlockedSlots[index] = available;
        RefreshAllSlots();
    }

    public void UpdateBootsBar(float normalized)
    {
        if (bootsStaminaBar != null) bootsStaminaBar.value = normalized;
    }

    public void UpdateThrusterFuel(float normalized)
    {
        if (thrusterFuelBar != null) thrusterFuelBar.value = normalized;
    }

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

            bool locked   = i >= _unlockedSlots.Length || !_unlockedSlots[i];
            bool active   = i == _activeIndex;
            bool flashing = i == _flashSlotIndex && _flashTimer > 0f;

            if (!flashing) // don't overwrite flash color
                gadgetSlotImages[i].color = locked   ? lockedSlotColor
                                          : active   ? activeSlotColor
                                                     : inactiveSlotColor;

            if (gadgetIconImages != null && i < gadgetIconImages.Length && gadgetIconImages[i] != null)
                gadgetIconImages[i].color = locked ? new Color(0.35f, 0.35f, 0.35f, 0.5f)
                                          : active ? Color.white
                                                   : new Color(0.7f, 0.7f, 0.7f, 0.8f);
        }
    }

    private void MoveMarkerToSlot(int index)
    {
        if (activeMarker == null || gadgetSlotImages == null) return;
        if (index < 0 || index >= gadgetSlotImages.Length) return;
        if (gadgetSlotImages[index] == null) return;

        var slotRect = gadgetSlotImages[index].rectTransform;
        if (slotRect.parent == activeMarker.parent)
            _markerTargetPos = slotRect.anchoredPosition;

        if (!animateMarker)
            activeMarker.anchoredPosition = _markerTargetPos;
    }
}
