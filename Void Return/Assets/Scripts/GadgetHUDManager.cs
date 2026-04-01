using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the gadget slot HUD in the bottom-left corner.
/// Shows 4 gadget slots, highlights the active one, and displays
/// per-gadget resource bars (boots stamina, thruster fuel, grenade count).
///
/// Attach to an empty child GameObject inside the Game Canvas.
/// Wire all UI references in the Inspector.
/// </summary>
public class GadgetHUDManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static GadgetHUDManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Gadget Slot Images (4 slots in order: Boots, Tether, Grenade, Thruster)")]
    [Tooltip("The four gadget slot background Images. Assign in order: 0=Boots, 1=Tether, 2=Grenade, 3=Thruster.")]
    public Image[] gadgetSlotImages;

    [Tooltip("Icon images displayed inside each gadget slot. Assign in the same order as slots.")]
    public Image[] gadgetIconImages;

    [Tooltip("Sprite icons for each gadget: [0] Boots, [1] Tether, [2] Grenade, [3] Thruster.")]
    public Sprite[] gadgetIconSprites;

    [Header("Slot Highlight Colors")]
    [Tooltip("Color of the slot background when it is the currently active gadget.")]
    public Color activeSlotColor = new Color(0f, 0.9f, 1f, 0.9f);

    [Tooltip("Color of the slot background when it is inactive.")]
    public Color inactiveSlotColor = new Color(0.15f, 0.15f, 0.2f, 0.7f);

    [Tooltip("Color overlay for gadget slots that have not yet been unlocked.")]
    public Color lockedSlotColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);

    [Header("Hotkey Labels")]
    [Tooltip("Text labels showing the hotkey for each gadget slot: [0]=1, [1]=2, etc.")]
    public TextMeshProUGUI[] hotkeyLabels;

    [Header("Resource Bars")]
    [Tooltip("Slider for Gravity Boots stamina bar (below or beside the Boots slot).")]
    public Slider bootsStaminaBar;

    [Tooltip("Slider for Thruster Pack fuel bar.")]
    public Slider thrusterFuelBar;

    [Tooltip("Text label showing remaining grenade count (e.g., 'x3').")]
    public TextMeshProUGUI grenadeCountText;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private bool[] _unlockedSlots = { true, false, false, false }; // Boots unlocked at start

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Set all icons
        if (gadgetIconImages != null && gadgetIconSprites != null)
        {
            for (int i = 0; i < gadgetIconImages.Length && i < gadgetIconSprites.Length; i++)
                if (gadgetIconImages[i] != null && gadgetIconSprites[i] != null)
                    gadgetIconImages[i].sprite = gadgetIconSprites[i];
        }

        // Set hotkey labels
        string[] keys = { "1", "2", "3", "4" };
        if (hotkeyLabels != null)
            for (int i = 0; i < hotkeyLabels.Length; i++)
                if (hotkeyLabels[i] != null && i < keys.Length)
                    hotkeyLabels[i].text = keys[i];

        // Reflect initial unlock state
        RefreshAllSlots();
        HighlightGadget(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Called by ShipRepairManager and PlayerController
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Highlights the active gadget slot and dims the others.
    /// Called by PlayerController.SwitchGadget.
    /// </summary>
    public void HighlightGadget(int index)
    {
        if (gadgetSlotImages == null) return;

        for (int i = 0; i < gadgetSlotImages.Length; i++)
        {
            if (gadgetSlotImages[i] == null) continue;

            if (!_unlockedSlots[i])
                gadgetSlotImages[i].color = lockedSlotColor;
            else
                gadgetSlotImages[i].color = (i == index) ? activeSlotColor : inactiveSlotColor;
        }
    }

    /// <summary>
    /// Unlocks a gadget slot visually and allows the player to switch to it.
    /// Called by ShipRepairManager when a module is repaired.
    /// </summary>
    public void UnlockGadgetSlot(int index)
    {
        if (index < 0 || index >= _unlockedSlots.Length) return;
        _unlockedSlots[index] = true;
        RefreshAllSlots();
    }

    /// <summary>
    /// Sets a gadget slot as available (true) or locked (false).
    /// Called by PlayerController.ApplyStartingGadgetToggles() at game start
    /// to reflect the enableXxxAtStart debug toggle settings in the Inspector.
    /// </summary>
    public void SetGadgetAvailable(int index, bool available)
    {
        if (index < 0 || index >= _unlockedSlots.Length) return;
        _unlockedSlots[index] = available;
        RefreshAllSlots();
    }

    /// <summary>
    /// Updates the Gravity Boots stamina bar (0 to 1 normalized).
    /// Called by GravityBoots.Update.
    /// </summary>
    public void UpdateBootsBar(float normalized)
    {
        if (bootsStaminaBar != null)
            bootsStaminaBar.value = normalized;
    }

    /// <summary>
    /// Updates the Thruster Pack fuel bar (0 to 1 normalized).
    /// Called by ThrusterPack.Update.
    /// </summary>
    public void UpdateThrusterFuel(float normalized)
    {
        if (thrusterFuelBar != null)
            thrusterFuelBar.value = normalized;
    }

    /// <summary>
    /// Updates the grenade count text label.
    /// Called by GravityGrenadeLauncher after launching or picking up grenades.
    /// </summary>
    public void UpdateGrenadeCount(int count)
    {
        if (grenadeCountText != null)
            grenadeCountText.text = $"x{count}";
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
            gadgetSlotImages[i].color = _unlockedSlots[i] ? inactiveSlotColor : lockedSlotColor;

            // Dim the icon as well
            if (gadgetIconImages != null && i < gadgetIconImages.Length && gadgetIconImages[i] != null)
                gadgetIconImages[i].color = _unlockedSlots[i]
                    ? Color.white
                    : new Color(0.4f, 0.4f, 0.4f, 0.5f);
        }
    }
}
