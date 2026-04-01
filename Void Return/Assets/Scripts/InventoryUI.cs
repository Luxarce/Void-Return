using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages the inventory panel display and handles the Tab key toggle.
///
/// This script is fully self-contained — it subscribes to Inventory.OnInventoryChanged
/// and rebuilds the panel automatically. No Inspector event wiring needed.
///
/// SETUP:
///  1. Create a Panel in the Canvas named 'InventoryPanel'. Set it inactive by default.
///  2. Inside InventoryPanel, add a ScrollView. Inside the ScrollView's Content object,
///     add a Grid Layout Group component (set Cell Size to 80×80, Spacing 10×10).
///  3. Create an 'InventorySlot' prefab (see PREFAB STRUCTURE below).
///  4. Attach this script to the InventoryPanel GameObject.
///  5. Assign the references below in the Inspector.
///
/// PREFAB STRUCTURE for inventorySlotPrefab:
///   InventorySlot (Image — slot background)
///   ├── Icon (Image — the material's sprite icon)
///   ├── Count (TextMeshProUGUI — shows "x3")
///   └── Name  (TextMeshProUGUI — shows material name, optional)
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Panel Reference")]
    [Tooltip("The inventory panel root GameObject. This script toggles its active state.")]
    public GameObject inventoryPanel;

    [Header("Slot Grid")]
    [Tooltip("The Content Transform inside the ScrollView. Must have a Grid Layout Group.")]
    public Transform slotGridParent;

    [Tooltip("Prefab for a single inventory slot. " +
             "Must have child GameObjects named 'Icon' (Image), 'Count' (TextMeshProUGUI), " +
             "and optionally 'Name' (TextMeshProUGUI).")]
    public GameObject inventorySlotPrefab;

    [Header("Input")]
    [Tooltip("Key that opens and closes the inventory panel.")]
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Empty State")]
    [Tooltip("Text shown when the inventory is completely empty. " +
             "Place a TextMeshProUGUI inside InventoryPanel for this.")]
    public TextMeshProUGUI emptyLabel;

    [Tooltip("Text to display when inventory is empty.")]
    public string emptyText = "No materials collected yet.\nExplore the debris field to gather resources.";

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private readonly List<GameObject> _slotInstances = new();
    private bool _isOpen = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Ensure panel starts hidden
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);

        // Subscribe to inventory changes for automatic refresh
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged += Rebuild;
    }

    private void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= Rebuild;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens or closes the inventory panel.
    /// Can also be called by a UI button's onClick event.
    /// </summary>
    public void Toggle()
    {
        _isOpen = !_isOpen;
        if (inventoryPanel != null)
            inventoryPanel.SetActive(_isOpen);

        if (_isOpen) Rebuild();
    }

    /// <summary>
    /// Force-opens the inventory.
    /// </summary>
    public void Open()
    {
        _isOpen = true;
        inventoryPanel?.SetActive(true);
        Rebuild();
    }

    /// <summary>
    /// Force-closes the inventory.
    /// </summary>
    public void Close()
    {
        _isOpen = false;
        inventoryPanel?.SetActive(false);
    }

    /// <summary>
    /// Rebuilds all inventory slot GameObjects from the current Inventory state.
    /// Called automatically when Inventory.OnInventoryChanged fires.
    /// </summary>
    public void Rebuild()
    {
        if (slotGridParent == null || inventorySlotPrefab == null) return;

        // Destroy old slots
        foreach (var slot in _slotInstances)
            if (slot != null) Destroy(slot);
        _slotInstances.Clear();

        if (Inventory.Instance == null) return;

        var items = Inventory.Instance.GetAllItems();
        bool anyItem = false;

        foreach (var item in items)
        {
            if (item.quantity <= 0) continue;
            anyItem = true;

            GameObject slot = Instantiate(inventorySlotPrefab, slotGridParent);
            _slotInstances.Add(slot);

            // Assign icon
            var iconImg = slot.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImg != null)
            {
                iconImg.sprite  = item.icon;
                iconImg.enabled = item.icon != null;
            }

            // Assign count
            var countTxt = slot.transform.Find("Count")?.GetComponent<TextMeshProUGUI>();
            if (countTxt != null)
                countTxt.text = $"x{item.quantity}";

            // Assign name
            var nameTxt = slot.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            if (nameTxt != null)
                nameTxt.text = item.itemName.Replace("_", " ");
        }

        // Show or hide empty state label
        if (emptyLabel != null)
            emptyLabel.gameObject.SetActive(!anyItem);
    }
}
