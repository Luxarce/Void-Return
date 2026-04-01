using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// MaterialType Enum
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// All collectable material types in the game.
/// </summary>
public enum MaterialType
{
    MetalScrap,
    Bolt,
    Glass,
    Foam,
    Sealant,
    CopperWire,
    OxygenCanister,
    Filter,
    CircuitBoard,
    Titanium,
    Lens,
    AntennaShards,
    FuelCell,
    Coolant,
    HeatShield,
    TitaniumRod
}

// ─────────────────────────────────────────────────────────────────────────────
// MaterialItem
// ─────────────────────────────────────────────────────────────────────────────

[System.Serializable]
public class MaterialItem
{
    public string       itemName;
    public MaterialType type;
    public Sprite       icon;
    public int          quantity;
}

// ─────────────────────────────────────────────────────────────────────────────
// Inventory Singleton
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Singleton that stores all collected materials.
///
/// FIX NOTES:
///  — OnInventoryChanged is now a plain C# event (not a UnityEvent).
///    InventoryUI subscribes to it in Start() via +=.
///  — Added null-guard: DontDestroyOnLoad so the singleton survives
///    scene reloads without losing collected materials.
///  — Clear() now correctly resets the dictionary AND fires the event
///    so InventoryUI rebuilds to show the empty state.
///  — GetAllItems() returns a defensive copy so iteration is safe even
///    if something modifies the dictionary during a loop.
/// </summary>
public class Inventory : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static Inventory Instance { get; private set; }

    // ── Event — fires whenever items are added, consumed, or cleared ──────────
    /// <summary>
    /// Subscribe to this event to be notified whenever the inventory changes.
    /// Usage:  Inventory.Instance.OnInventoryChanged += MyMethod;
    /// </summary>
    public event System.Action OnInventoryChanged;

    // ── Internal storage ──────────────────────────────────────────────────────
    private readonly Dictionary<MaterialType, MaterialItem> _items = new();

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Add materials of the given type. Creates a new entry if needed.
    /// Fires OnInventoryChanged so InventoryUI rebuilds automatically.
    /// </summary>
    public void AddItem(MaterialType type, int qty, Sprite icon = null)
    {
        if (qty <= 0) return;

        if (!_items.TryGetValue(type, out var item))
        {
            item = new MaterialItem
            {
                itemName = FormatName(type),
                type     = type,
                icon     = icon,
                quantity = 0
            };
            _items[type] = item;
        }

        // Assign icon if we now have one and didn't before
        if (icon != null && item.icon == null)
            item.icon = icon;

        item.quantity += qty;

        // Notify all listeners (InventoryUI.Rebuild, MaterialRequirementsUI.RefreshAll, etc.)
        OnInventoryChanged?.Invoke();
    }

    /// <summary>Returns how many of a material type the player currently holds.</summary>
    public int GetCount(MaterialType type) =>
        _items.TryGetValue(type, out var item) ? item.quantity : 0;

    /// <summary>Returns true if the player has at least 'required' of this material.</summary>
    public bool HasMaterials(MaterialType type, int required) =>
        GetCount(type) >= required;

    /// <summary>
    /// Deducts 'amount' of a material. Returns false (no deduction) if insufficient.
    /// </summary>
    public bool ConsumeMaterials(MaterialType type, int amount)
    {
        if (!HasMaterials(type, amount)) return false;
        _items[type].quantity -= amount;
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>Returns a snapshot list of all items (safe to iterate while inventory changes).</summary>
    public List<MaterialItem> GetAllItems() => new List<MaterialItem>(_items.Values);

    /// <summary>
    /// Clears the entire inventory. Used by SaveManager when loading a save file.
    /// </summary>
    public void Clear()
    {
        _items.Clear();
        OnInventoryChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string FormatName(MaterialType type) =>
        System.Text.RegularExpressions.Regex.Replace(
            type.ToString(),
            "(?<=[a-z])(?=[A-Z])",   // insert space before each capital after a lowercase
            " ");
}
