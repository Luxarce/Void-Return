using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages the inventory panel display.
///
/// FIX SUMMARY:
///
/// 1. TAB KEY NOT WORKING
///    Root cause: InventoryUI was attached to the InventoryPanel itself.
///    When the panel was inactive (SetActive=false), Update() stops running,
///    so Input.GetKeyDown(Tab) was never checked.
///    FIX: The Tab check is now in a separate persistent MonoBehaviour
///    (InventoryToggleListener) that you attach to a GameObject that is
///    ALWAYS active (e.g., GameCanvas or GameManager). InventoryUI still
///    handles all the display logic. See InventoryToggleListener.cs.
///    Alternatively, attach InventoryUI to the GameCanvas (always active),
///    not the InventoryPanel itself.
///
/// 2. ICONS NOT SHOWING
///    Root cause: The 'icon' field on MaterialPickup is what gets stored in
///    Inventory.MaterialItem. However if the icon was null when AddItem was
///    called, it stays null forever. The fix stores the icon from the prefab's
///    MaterialPickup.icon field. Make sure each MaterialPickup prefab has the
///    icon sprite assigned in its Inspector → Material Info → Icon field.
///    The InventoryUI Rebuild() now logs which items have missing icons so
///    you can find and fix them quickly in the Console.
///
/// 3. ITEMS NOT POPULATING
///    Root cause: InventoryUI was attached to InventoryPanel (inactive by default).
///    Start() never ran, so the OnInventoryChanged subscription never happened.
///    FIX: Attach InventoryUI to the GameCanvas or another always-active parent,
///    not to the InventoryPanel. The inventoryPanel field just references the
///    panel to show/hide — InventoryUI does not need to be on that object.
///
/// CORRECT HIERARCHY:
///   GameCanvas (always active)
///   ├── InventoryUI script ← attach HERE, not on InventoryPanel
///   └── InventoryPanel (starts inactive — just a child panel)
///       └── ScrollView → Content (slotGridParent)
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Panel Reference")]
    [Tooltip("The InventoryPanel GameObject to show/hide. " +
             "Must be a child of an always-active parent. " +
             "This script should NOT be on InventoryPanel itself — " +
             "attach it to GameCanvas or another always-active object.")]
    public GameObject inventoryPanel;

    [Header("Slot Grid")]
    [Tooltip("The Content Transform inside the Scroll View. Must have a Grid Layout Group.")]
    public Transform slotGridParent;

    [Tooltip("Prefab for one inventory slot.\n" +
             "REQUIRED child names:\n" +
             "  'Icon'  — Image component (displays the material sprite icon)\n" +
             "  'Count' — TextMeshProUGUI (shows x3, x5, etc.)\n" +
             "  'Name'  — TextMeshProUGUI (shows material name, optional)")]
    public GameObject inventorySlotPrefab;

    [Header("Input")]
    [Tooltip("Key that opens and closes the inventory panel.\n" +
             "IMPORTANT: This only works if InventoryUI is on an always-active " +
             "GameObject (not on InventoryPanel itself, which starts inactive).")]
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Empty State")]
    [Tooltip("Label shown when no materials have been collected. " +
             "Place this TextMeshProUGUI inside InventoryPanel.")]
    public TextMeshProUGUI emptyLabel;

    [Tooltip("Message shown on the empty label.")]
    public string emptyText = "No materials collected yet.\nExplore the debris field.";

    // ─────────────────────────────────────────────────────────────────────────
    private readonly List<GameObject> _slotInstances = new();
    private bool _subscribed;
    private bool _isOpen;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Make sure the panel starts closed
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
        _isOpen = false;
    }

    private void Start()
    {
        if (emptyLabel != null)
        {
            emptyLabel.text = emptyText;
            emptyLabel.gameObject.SetActive(true);
        }

        TrySubscribe();

        Debug.Log("[InventoryUI] Started. Toggle key: " + toggleKey +
                  " | Subscribed: " + _subscribed +
                  " | Panel assigned: " + (inventoryPanel != null) +
                  " | Slot prefab assigned: " + (inventorySlotPrefab != null));
    }

    private void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= Rebuild;
    }

    private void Update()
    {
        // Retry subscription in case Inventory wasn't ready at Start
        if (!_subscribed) TrySubscribe();

        if (Input.GetKeyDown(toggleKey))
        {
            Debug.Log("[InventoryUI] Tab pressed — toggling inventory.");
            Toggle();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void Toggle()
    {
        if (_isOpen) Close();
        else         Open();
    }

    public void Open()
    {
        if (inventoryPanel == null)
        {
            Debug.LogWarning("[InventoryUI] inventoryPanel is not assigned in Inspector.");
            return;
        }
        _isOpen = true;
        inventoryPanel.SetActive(true);
        Rebuild();
    }

    public void Close()
    {
        _isOpen = false;
        inventoryPanel?.SetActive(false);
    }

    // Called from a UI Button's onClick if you want a button to open inventory
    public void OpenFromButton() => Toggle();

    /// <summary>
    /// Rebuilds all inventory slot GameObjects from the current Inventory contents.
    /// Called automatically when Inventory.OnInventoryChanged fires AND when Open() runs.
    /// </summary>
    public void Rebuild()
    {
        if (slotGridParent == null)
        {
            Debug.LogWarning("[InventoryUI] slotGridParent is not assigned. " +
                             "Drag the Content object from inside your Scroll View here.");
            return;
        }
        if (inventorySlotPrefab == null)
        {
            Debug.LogWarning("[InventoryUI] inventorySlotPrefab is not assigned. " +
                             "Drag your InventorySlot prefab here.");
            return;
        }

        // Clear old slots
        foreach (var slot in _slotInstances)
            if (slot != null) Destroy(slot);
        _slotInstances.Clear();

        if (Inventory.Instance == null)
        {
            Debug.LogWarning("[InventoryUI] Inventory.Instance is null during Rebuild.");
            ShowEmptyLabel(true);
            return;
        }

        var items    = Inventory.Instance.GetAllItems();
        bool anyItem = false;

        foreach (var item in items)
        {
            if (item.quantity <= 0) continue;
            anyItem = true;

            // Instantiate a slot and parent it to the grid
            var slot = Instantiate(inventorySlotPrefab, slotGridParent);
            _slotInstances.Add(slot);

            // ── ICON ─────────────────────────────────────────────────────────
            // Looks for a child named exactly "Icon" with an Image component.
            // The sprite comes from Inventory.MaterialItem.icon which is set
            // when MaterialPickup.Collect() calls Inventory.AddItem(type, qty, icon).
            // If your prefab's MaterialPickup.icon field has a sprite assigned,
            // it will appear here automatically.
            var iconTransform = slot.transform.Find("Icon");
            var iconImage     = iconTransform?.GetComponent<Image>();
            if (iconImage != null)
            {
                if (item.icon != null)
                {
                    iconImage.sprite         = item.icon;
                    iconImage.color          = Color.white;
                    iconImage.preserveAspect = true;
                    iconImage.enabled        = true;
                }
                else
                {
                    // Icon is missing — log it so you can fix the prefab
                    Debug.LogWarning($"[InventoryUI] Item '{item.itemName}' has no icon sprite. " +
                                     $"Open the MaterialPickup prefab for '{item.type}' and " +
                                     $"assign a sprite to the 'Icon' field in Material Info.");
                    iconImage.enabled = false;
                }
            }
            else
            {
                Debug.LogWarning("[InventoryUI] InventorySlot prefab is missing a child " +
                                 "named 'Icon' with an Image component.");
            }

            // ── COUNT ─────────────────────────────────────────────────────────
            var countTxt = slot.transform.Find("Count")?.GetComponent<TextMeshProUGUI>();
            if (countTxt != null)
                countTxt.text = $"x{item.quantity}";

            // ── NAME ──────────────────────────────────────────────────────────
            var nameTxt = slot.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            if (nameTxt != null)
                nameTxt.text = item.itemName;
        }

        ShowEmptyLabel(!anyItem);
        Debug.Log($"[InventoryUI] Rebuilt — {_slotInstances.Count} slots populated.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void TrySubscribe()
    {
        if (_subscribed || Inventory.Instance == null) return;
        Inventory.Instance.OnInventoryChanged += Rebuild;
        _subscribed = true;
        Debug.Log("[InventoryUI] Subscribed to Inventory.OnInventoryChanged.");
    }

    private void ShowEmptyLabel(bool show)
    {
        if (emptyLabel != null)
            emptyLabel.gameObject.SetActive(show);
    }
}
