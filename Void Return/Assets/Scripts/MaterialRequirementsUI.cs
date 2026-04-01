using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Displays a real-time panel showing which materials are needed
/// for each module's current repair stage, with "have / need" counts.
///
/// SETUP:
///  1. Create a Panel in the Canvas named 'RequirementsPanel'. Set inactive by default.
///  2. Inside it, add a Vertical Layout Group. Set spacing to 8, padding 10.
///  3. Create a 'RequirementRow' prefab (see PREFAB STRUCTURE below).
///  4. Attach this script to a manager GameObject or RequirementsPanel itself.
///  5. Assign the four ShipModule references in the Inspector.
///  6. Open/close via the InventoryUI.Toggle or wire a button to ShowPanel().
///
/// PREFAB STRUCTURE for requirementRowPrefab:
///   RequirementRow (HorizontalLayoutGroup)
///   ├── ModuleLabel   (TextMeshProUGUI — "Life Support")
///   ├── MaterialLabel (TextMeshProUGUI — "Copper Wire")
///   └── CountLabel    (TextMeshProUGUI — "2 / 3")  ← color-coded green/red
/// </summary>
public class MaterialRequirementsUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Panel")]
    [Tooltip("The requirements panel root. Toggled by the same Tab key as inventory, " +
             "or wire a separate button to ShowPanel().")]
    public GameObject requirementsPanel;

    [Header("Module References")]
    [Tooltip("ShipModule for Life Support.")]
    public ShipModule lifeSupport;

    [Tooltip("ShipModule for Hull Plating.")]
    public ShipModule hullPlating;

    [Tooltip("ShipModule for Navigation.")]
    public ShipModule navigation;

    [Tooltip("ShipModule for Engine Core.")]
    public ShipModule engineCore;

    [Header("Row Prefab")]
    [Tooltip("Prefab for one requirement row. " +
             "Must have children named: ModuleLabel, MaterialLabel, CountLabel " +
             "(all TextMeshProUGUI).")]
    public GameObject requirementRowPrefab;

    [Tooltip("Parent Transform (with Vertical Layout Group) that holds all rows.")]
    public Transform rowParent;

    [Header("Colors")]
    [Tooltip("Color shown on the count label when the player has enough materials.")]
    public Color colorSufficient = new Color(0.2f, 1f, 0.4f);

    [Tooltip("Color shown when the player does not yet have enough.")]
    public Color colorInsufficient = new Color(1f, 0.3f, 0.2f);

    [Tooltip("Color shown when the module is fully repaired.")]
    public Color colorRepaired = new Color(0.5f, 0.5f, 0.5f);

    [Header("Labels")]
    [Tooltip("Text shown in the count label for a fully repaired module.")]
    public string repairedText = "REPAIRED ✓";

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private readonly List<GameObject> _rows = new();

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        requirementsPanel?.SetActive(false);

        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged += RefreshAll;

        BuildAllRows();
    }

    private void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= RefreshAll;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void ShowPanel()
    {
        requirementsPanel?.SetActive(true);
        RefreshAll();
    }

    public void HidePanel() => requirementsPanel?.SetActive(false);

    public void TogglePanel()
    {
        if (requirementsPanel == null) return;
        bool newState = !requirementsPanel.activeSelf;
        requirementsPanel.SetActive(newState);
        if (newState) RefreshAll();
    }

    /// <summary>
    /// Rebuilds and updates all requirement rows.
    /// Called automatically when the inventory changes.
    /// Also called by GameManager when module progress changes.
    /// </summary>
    public void RefreshAll()
    {
        // Rebuild rows from scratch to reflect the latest stage
        BuildAllRows();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Build
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildAllRows()
    {
        if (rowParent == null || requirementRowPrefab == null) return;

        // Clear old rows
        foreach (var row in _rows)
            if (row != null) Destroy(row);
        _rows.Clear();

        BuildModuleRows(lifeSupport,  "Life Support");
        BuildModuleRows(hullPlating,  "Hull Plating");
        BuildModuleRows(navigation,   "Navigation");
        BuildModuleRows(engineCore,   "Engine Core");
    }

    private void BuildModuleRows(ShipModule module, string moduleName)
    {
        if (module == null) return;

        // ── Module is fully repaired ──────────────────────────────────────
        if (module.IsFullyRepaired)
        {
            var row = CreateRow();
            SetLabel(row, "ModuleLabel",   moduleName);
            SetLabel(row, "MaterialLabel", "");
            var countLbl = GetLabel(row, "CountLabel");
            if (countLbl != null)
            {
                countLbl.text  = repairedText;
                countLbl.color = colorRepaired;
            }
            return;
        }

        // ── Current stage requirements ─────────────────────────────────────
        int stageIdx = module.CurrentStageIndex;
        if (stageIdx >= module.stages.Count) return;

        var stage = module.stages[stageIdx];
        bool isFirstRowForModule = true;

        foreach (var req in stage.requirements)
        {
            int have        = Inventory.Instance?.GetCount(req.materialType) ?? 0;
            bool sufficient = have >= req.required;

            var row = CreateRow();
            // Only show the module name on the first row for this module
            SetLabel(row, "ModuleLabel",
                isFirstRowForModule ? $"{moduleName} — Stage {stageIdx + 1}" : "");

            SetLabel(row, "MaterialLabel",
                req.materialType.ToString().Replace("_", " "));

            var countLbl = GetLabel(row, "CountLabel");
            if (countLbl != null)
            {
                countLbl.text  = $"{have} / {req.required}";
                countLbl.color = sufficient ? colorSufficient : colorInsufficient;
            }

            isFirstRowForModule = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private GameObject CreateRow()
    {
        var row = Instantiate(requirementRowPrefab, rowParent);
        _rows.Add(row);
        return row;
    }

    private void SetLabel(GameObject row, string childName, string text)
    {
        var lbl = GetLabel(row, childName);
        if (lbl != null) lbl.text = text;
    }

    private TextMeshProUGUI GetLabel(GameObject row, string childName)
    {
        var child = row.transform.Find(childName);
        return child?.GetComponent<TextMeshProUGUI>();
    }
}
