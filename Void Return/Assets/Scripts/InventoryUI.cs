using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages the inventory panel display.
///
/// FIX NOTES:
///  — Subscription to Inventory.OnInventoryChanged is now retried in Update
///    if Inventory.Instance was null at Start (DontDestroyOnLoad object may
///    not yet exist when this script's Start runs).
///  — Input.GetKeyDown is now in Update (not inside a coroutine or fixed).
///    Previously the Tab check was only called if the panel was active, which
///    meant you could never open it.
///  — Rebuild() now runs immediately on Open() without waiting for the event.
///  — inventoryPanel.activeSelf check added so double-tapping Tab doesn't
///    break state.
///  — Added a public OpenFromButton() method for wiring to a UI Button's
///    onClick event in the Inspector.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The root InventoryPanel GameObject. Toggled by the Toggle Key.")]
    public GameObject inventoryPanel;

    [Header("Slot Grid")]
    [Tooltip("The Content Transform inside the Scroll View (must have Grid Layout Group).")]
    public Transform slotGridParent;

    [Tooltip("Prefab for one inventory slot. " +
             "Must have children named: 'Icon' (Image), 'Count' (TextMeshProUGUI), " +
             "'Name' (TextMeshProUGUI, optional).")]
    public GameObject inventorySlotPrefab;

    [Header("Input")]
    [Tooltip("Key that opens and closes the inventory panel. Default: Tab.")]
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Empty State")]
    [Tooltip("Label shown when no materials have been collected yet.")]
    public TextMeshProUGUI emptyLabel;

    [Tooltip("Text shown on the empty label.")]
    public string emptyText = "No materials collected yet.\nExplore the debris field.";

    // ─────────────────────────────────────────────────────────────────────────
    private readonly List<GameObject> _slotInstances = new();
    private bool _subscribed;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Panel starts closed
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);

        if (emptyLabel != null)
        {
            emptyLabel.text = emptyText;
            emptyLabel.gameObject.SetActive(true);
        }

        TrySubscribe();
    }

    private void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= Rebuild;
    }

    private void Update()
    {
        // Retry subscription every frame until it succeeds
        // (handles the case where Inventory is a DontDestroyOnLoad object
        //  that isn't ready at the frame this script's Start() runs)
        if (!_subscribed) TrySubscribe();

        // Toggle key — works regardless of whether the panel is open or closed
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Toggles the inventory open or closed.</summary>
    public void Toggle()
    {
        if (inventoryPanel == null) return;
        bool isOpen = inventoryPanel.activeSelf;
        if (isOpen) Close();
        else        Open();
    }

    /// <summary>Opens the inventory panel and forces an immediate rebuild.</summary>
    public void Open()
    {
        if (inventoryPanel == null) return;
        inventoryPanel.SetActive(true);
        Rebuild();  // Always rebuild on open so content is current
    }

    /// <summary>Closes the inventory panel.</summary>
    public void Close()
    {
        inventoryPanel?.SetActive(false);
    }

    /// <summary>
    /// Call from a UI Button's onClick event in the Inspector.
    /// Identical to Toggle() but has a clear name for Inspector wiring.
    /// </summary>
    public void OpenFromButton() => Toggle();

    /// <summary>
    /// Rebuilds all slot GameObjects from the current Inventory state.
    /// Called automatically when Inventory.OnInventoryChanged fires,
    /// and explicitly when the panel is opened.
    /// </summary>
    public void Rebuild()
    {
        if (slotGridParent == null || inventorySlotPrefab == null)
        {
            Debug.LogWarning("[InventoryUI] slotGridParent or inventorySlotPrefab is not assigned.");
            return;
        }

        // Destroy existing slot GameObjects
        foreach (var slot in _slotInstances)
            if (slot != null) Destroy(slot);
        _slotInstances.Clear();

        if (Inventory.Instance == null)
        {
            ShowEmptyLabel(true);
            return;
        }

        var items   = Inventory.Instance.GetAllItems();
        bool anyItem = false;

        foreach (var item in items)
        {
            if (item.quantity <= 0) continue;
            anyItem = true;

            GameObject slot = Instantiate(inventorySlotPrefab, slotGridParent);
            _slotInstances.Add(slot);

            // Icon
            var iconImg = slot.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImg != null)
            {
                iconImg.sprite  = item.icon;
                iconImg.enabled = item.icon != null;
            }

            // Count label  e.g.  "x5"
            var countTxt = slot.transform.Find("Count")?.GetComponent<TextMeshProUGUI>();
            if (countTxt != null)
                countTxt.text = $"x{item.quantity}";

            // Name label  e.g.  "Metal Scrap"
            var nameTxt = slot.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            if (nameTxt != null)
                nameTxt.text = item.itemName;
        }

        ShowEmptyLabel(!anyItem);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void TrySubscribe()
    {
        if (_subscribed || Inventory.Instance == null) return;
        Inventory.Instance.OnInventoryChanged += Rebuild;
        _subscribed = true;
    }

    private void ShowEmptyLabel(bool show)
    {
        if (emptyLabel != null)
            emptyLabel.gameObject.SetActive(show);
    }
}
