using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Displays material requirements per module with color-coded have/need counts.
///
/// CHANGE: Panel now starts OPEN by default (Start sets it active).
/// Player can close it manually. TogglePanel() still works as before.
/// </summary>
public class MaterialRequirementsUI : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The RequirementsPanel root. Starts OPEN by default.")]
    public GameObject requirementsPanel;

    [Header("Module References")]
    public ShipModule lifeSupport;
    public ShipModule hullPlating;
    public ShipModule navigation;
    public ShipModule engineCore;

    [Header("Row Prefab")]
    [Tooltip("Prefab: HorizontalLayoutGroup with children ModuleLabel, MaterialLabel, CountLabel (all TMP).")]
    public GameObject requirementRowPrefab;

    [Tooltip("Content Transform with Vertical Layout Group.")]
    public Transform rowParent;

    [Header("Colors")]
    public Color colorSufficient   = new Color(0.2f, 1f, 0.4f);
    public Color colorInsufficient = new Color(1f, 0.3f, 0.2f);
    public Color colorRepaired     = new Color(0.5f, 0.6f, 0.5f);

    // ─────────────────────────────────────────────────────────────────────────
    private readonly List<GameObject> _rows = new();
    private bool _inventorySubscribed;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Open by default — player can see requirements from the start
        requirementsPanel?.SetActive(true);

        TrySubscribeInventory();
        RefreshAll(); // Initial build
    }

    private void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= RefreshAll;
    }

    private void Update()
    {
        if (!_inventorySubscribed) TrySubscribeInventory();
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void ShowPanel()  { requirementsPanel?.SetActive(true);  RefreshAll(); }
    public void HidePanel()  { requirementsPanel?.SetActive(false); }

    public void TogglePanel()
    {
        if (requirementsPanel == null) return;
        bool nowOpen = !requirementsPanel.activeSelf;
        requirementsPanel.SetActive(nowOpen);
        if (nowOpen) RefreshAll();
    }

    public void RefreshAll()
    {
        if (rowParent == null || requirementRowPrefab == null) return;

        foreach (var r in _rows) if (r != null) Destroy(r);
        _rows.Clear();

        BuildModuleRows(lifeSupport,  "Life Support");
        BuildModuleRows(hullPlating,  "Hull Plating");
        BuildModuleRows(navigation,   "Navigation");
        BuildModuleRows(engineCore,   "Engine Core");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void BuildModuleRows(ShipModule module, string displayName)
    {
        if (module == null) return;

        if (module.IsFullyRepaired)
        {
            var row      = CreateRow();
            SetText(row, "ModuleLabel",   displayName);
            SetText(row, "MaterialLabel", "");
            var lbl      = GetTMP(row, "CountLabel");
            if (lbl != null) { lbl.text = "[DONE]"; lbl.color = colorRepaired; }
            return;
        }

        int stageIdx = module.CurrentStageIndex;
        if (stageIdx >= module.stages.Count) return;

        var stage    = module.stages[stageIdx];
        bool firstRow = true;

        foreach (var req in stage.requirements)
        {
            int  have   = Inventory.Instance?.GetCount(req.materialType) ?? 0;
            bool enough = have >= req.required;

            var row = CreateRow();
            SetText(row, "ModuleLabel",
                firstRow ? $"{displayName} (Stage {stageIdx + 1})" : "");

            string matName = System.Text.RegularExpressions.Regex.Replace(
                req.materialType.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
            SetText(row, "MaterialLabel", matName);

            var countLbl = GetTMP(row, "CountLabel");
            if (countLbl != null)
            {
                countLbl.text  = $"{have} / {req.required}";
                countLbl.color = enough ? colorSufficient : colorInsufficient;
            }

            firstRow = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private GameObject CreateRow()
    {
        var row = Instantiate(requirementRowPrefab, rowParent);
        _rows.Add(row);
        return row;
    }

    private void SetText(GameObject row, string childName, string text)
    {
        var t = GetTMP(row, childName);
        if (t != null) t.text = text;
    }

    private TextMeshProUGUI GetTMP(GameObject row, string childName)
    {
        var child = row.transform.Find(childName);
        return child?.GetComponent<TextMeshProUGUI>();
    }

    private void TrySubscribeInventory()
    {
        if (_inventorySubscribed || Inventory.Instance == null) return;
        Inventory.Instance.OnInventoryChanged += RefreshAll;
        _inventorySubscribed = true;
    }
}
