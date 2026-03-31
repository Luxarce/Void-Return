using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// Material Type Enum
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// All collectable material types in the game.
/// Each type maps to a specific module repair requirement in ShipModule.
/// </summary>
public enum MaterialType
{
    // Zone 1 — Debris Field (Common)
    MetalScrap,
    Bolt,
    Glass,
    Foam,
    Sealant,

    // Zone 1-2 — Transition
    CopperWire,
    OxygenCanister,
    Filter,

    // Zone 2 — Drift Ring (Mid-Tier)
    CircuitBoard,
    Titanium,
    Lens,
    AntennaShards,

    // Zone 3 — Deep Scatter (Rare)
    FuelCell,
    Coolant,
    HeatShield,
    TitaniumRod
}

// ─────────────────────────────────────────────────────────────────────────────
// MaterialItem Data Class
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Holds data for a single material type stored in the Inventory.
/// </summary>
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
/// Persists across scenes via DontDestroyOnLoad.
/// Call Inventory.Instance.AddItem() from MaterialPickup scripts.
/// Call Inventory.Instance.ConsumeMaterials() from ShipModule repair logic.
/// </summary>
public class Inventory : MonoBehaviour
{
    // ─── Singleton ───────────────────────────────────────────────────────────

    public static Inventory Instance { get; private set; }

    // ─── Events ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired whenever the inventory changes (add or consume).
    /// Subscribe to refresh UI panels.
    /// </summary>
    public event System.Action OnInventoryChanged;

    // ─── Internal Storage ────────────────────────────────────────────────────

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
    /// Add a material to the inventory. Creates a new entry if it doesn't exist.
    /// </summary>
    public void AddItem(MaterialType type, int qty, Sprite icon = null)
    {
        if (!_items.TryGetValue(type, out var item))
        {
            item = new MaterialItem
            {
                itemName = type.ToString(),
                type     = type,
                icon     = icon,
                quantity = 0
            };
            _items[type] = item;
        }

        if (icon != null && item.icon == null)
            item.icon = icon;

        item.quantity += qty;
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Returns the current count of a specific material type.
    /// </summary>
    public int GetCount(MaterialType type)
    {
        return _items.TryGetValue(type, out var item) ? item.quantity : 0;
    }

    /// <summary>
    /// Returns true if the player has at least the required amount of this material.
    /// </summary>
    public bool HasMaterials(MaterialType type, int required)
    {
        return GetCount(type) >= required;
    }

    /// <summary>
    /// Deducts the specified amount of a material from inventory.
    /// Returns false if not enough materials were available (no deduction made).
    /// </summary>
    public bool ConsumeMaterials(MaterialType type, int amount)
    {
        if (!HasMaterials(type, amount)) return false;

        _items[type].quantity -= amount;
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Returns all items currently in the inventory (for UI display).
    /// </summary>
    public List<MaterialItem> GetAllItems()
    {
        return new List<MaterialItem>(_items.Values);
    }

    /// <summary>
    /// Clears the entire inventory. Used by SaveManager when loading a save.
    /// </summary>
    public void Clear()
    {
        _items.Clear();
        OnInventoryChanged?.Invoke();
    }
}
