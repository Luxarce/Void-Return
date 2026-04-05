using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Inventory panel UI — open by default, collapsible.
///
/// CHANGE: Panel starts OPEN at scene load.
/// A collapse/expand button toggles visibility.
/// The button label changes between "▲ Inventory" and "▼ Inventory".
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The root panel containing the inventory slot grid. Starts open.")]
    public GameObject inventoryPanel;

    [Tooltip("Toggle button. Place it above or beside the inventory panel. " +
             "Wire its onClick to InventoryUI.TogglePanel().")]
    public Button     collapseButton;

    [Tooltip("Text on the collapse button. Updated to show open/closed state.")]
    public TextMeshProUGUI collapseButtonLabel;

    [Tooltip("Label shown when panel is open.")]
    public string labelOpen   = "▲ Inventory";

    [Tooltip("Label shown when panel is closed.")]
    public string labelClosed = "▼ Inventory";

    [Header("Slot Grid")]
    [Tooltip("Content Transform inside a Scroll View. Has a Grid Layout Group component.")]
    public Transform slotGridParent;

    [Tooltip("Prefab with: Icon (Image), Count (TMP), Name (TMP) children.")]
    public GameObject inventorySlotPrefab;

    [Header("Empty State")]
    public TextMeshProUGUI emptyLabel;

    [Header("Toggle Key")]
    public KeyCode toggleKey = KeyCode.Tab;

    // ─────────────────────────────────────────────────────────────────────────
    private bool           _isOpen = true;
    private List<GameObject> _slots = new();

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Wire the collapse button
        collapseButton?.onClick.AddListener(TogglePanel);

        // Start open
        Open();
        RebuildSlots();

        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged += RebuildSlots;
    }

    private void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= RebuildSlots;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) TogglePanel();
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void TogglePanel()
    {
        if (_isOpen) Close(); else Open();
    }

    public void Open()
    {
        _isOpen = true;
        inventoryPanel?.SetActive(true);
        if (collapseButtonLabel != null) collapseButtonLabel.text = labelOpen;
    }

    public void Close()
    {
        _isOpen = false;
        inventoryPanel?.SetActive(false);
        if (collapseButtonLabel != null) collapseButtonLabel.text = labelClosed;
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void RebuildSlots()
    {
        foreach (var s in _slots) if (s != null) Destroy(s);
        _slots.Clear();

        if (Inventory.Instance == null) return;

        var items = Inventory.Instance.GetAllItems();
        bool hasItems = items != null && items.Count > 0;

        emptyLabel?.gameObject.SetActive(!hasItems);

        if (!hasItems) return;

        foreach (var item in items)
        {
            if (inventorySlotPrefab == null || slotGridParent == null) break;

            var slot = Instantiate(inventorySlotPrefab, slotGridParent);
            _slots.Add(slot);

            var iconImg = slot.transform.Find("Icon")?.GetComponent<Image>();
            var countTmp = slot.transform.Find("Count")?.GetComponent<TextMeshProUGUI>();
            var nameTmp  = slot.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();

            if (iconImg  != null && item.icon  != null) iconImg.sprite = item.icon;
            if (countTmp != null) countTmp.text = $"x{item.quantity}";
            if (nameTmp  != null)
            {
                string display = System.Text.RegularExpressions.Regex.Replace(
                    item.type.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
                nameTmp.text = display;
            }
        }
    }
}
