using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Collapsible minimap legend panel showing what each marker color means.
///
/// SETUP:
///  1. Create a Panel named MinimapLegend inside the GameCanvas.
///     Place it beside or below the minimap display panel.
///  2. Add this script to the panel.
///  3. Create legend rows inside the panel (see structure below).
///  4. Create a Toggle Button above/outside the panel. Wire its onClick to ToggleLegend().
///
/// Panel structure:
///   MinimapLegend (this script)
///   ├── LegendTitle (TextMeshProUGUI) — "Map Legend"
///   ├── LegendContent (VerticalLayoutGroup parent)
///   │   ├── Row: dot(Image) + label(TMP) — Player
///   │   ├── Row: dot(Image) + label(TMP) — Ship
///   │   ├── Row: dot(Image) + label(TMP) — Materials (Common)
///   │   ├── Row: dot(Image) + label(TMP) — Materials (Mid)
///   │   ├── Row: dot(Image) + label(TMP) — Materials (Rare)
///   │   ├── Row: dot(Image) + label(TMP) — Rift
///   │   └── Row: dot(Image) + label(TMP) — Meteorite
///   └── CollapseButton (Button) — toggles the LegendContent active state
/// </summary>
public class MinimapLegend : MonoBehaviour
{
    [Header("Legend Panels")]
    [Tooltip("The content area that collapses/expands. Parent of all the legend rows.")]
    public GameObject legendContent;

    [Tooltip("Button used to toggle the legend. Wire its onClick to ToggleLegend().")]
    public Button collapseButton;

    [Tooltip("TMP label on the collapse button.")]
    public TextMeshProUGUI collapseButtonLabel;

    [Tooltip("Text when legend is open.")]
    public string labelOpen   = "▲ Legend";

    [Tooltip("Text when legend is closed.")]
    public string labelClosed = "▼ Legend";

    [Header("Auto-Generated Rows (if not building manually)")]
    [Tooltip("If true, the legend rows are generated in code using the colors below. " +
             "Set to false if you built the rows manually in the Hierarchy.")]
    public bool autoGenerateRows = true;

    [Tooltip("Parent Transform with Vertical Layout Group where auto-generated rows go.")]
    public Transform rowParent;

    [Header("Marker Colors (must match MinimapController settings)")]
    public Color playerColor       = new Color(1f, 0.3f, 0.2f);
    public Color shipColor         = new Color(0.2f, 0.7f, 1f);
    public Color zone1MatColor     = new Color(0.9f, 0.85f, 0.3f);
    public Color zone2MatColor     = new Color(0.2f, 0.7f, 1.0f);
    public Color zone3MatColor     = new Color(0.8f, 0.2f, 1.0f);
    public Color riftColor         = new Color(1f, 0.15f, 0.1f);
    public Color meteoriteColor    = new Color(1f, 0.5f, 0f);

    // ─────────────────────────────────────────────────────────────────────────
    private bool _isOpen = true;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        collapseButton?.onClick.AddListener(ToggleLegend);

        if (autoGenerateRows && rowParent != null)
            GenerateRows();

        // Start open
        legendContent?.SetActive(true);
        if (collapseButtonLabel != null) collapseButtonLabel.text = labelOpen;
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void ToggleLegend()
    {
        _isOpen = !_isOpen;
        legendContent?.SetActive(_isOpen);
        if (collapseButtonLabel != null)
            collapseButtonLabel.text = _isOpen ? labelOpen : labelClosed;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void GenerateRows()
    {
        var entries = new (Color color, string label)[]
        {
            (playerColor,    "Player"),
            (shipColor,      "Ship / Wreck direction"),
            (zone1MatColor,  "Material: Common (Zone 1)"),
            (zone2MatColor,  "Material: Mid-tier (Zone 2)"),
            (zone3MatColor,  "Material: Rare (Zone 3)"),
            (riftColor,      "Gravity Rift"),
            (meteoriteColor, "Meteorite"),
        };

        foreach (var (color, label) in entries)
            CreateRow(color, label);
    }

    private void CreateRow(Color color, string label)
    {
        // Row container
        var row = new GameObject($"LegendRow_{label}");
        row.transform.SetParent(rowParent, false);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childControlHeight = true;
        hlg.childControlWidth  = false;
        hlg.childForceExpandWidth = false;

        // Color dot
        var dotGO = new GameObject("Dot");
        dotGO.transform.SetParent(row.transform, false);
        var dotRect = dotGO.AddComponent<RectTransform>();
        dotRect.sizeDelta = new Vector2(12f, 12f);
        var dotImg = dotGO.AddComponent<Image>();
        dotImg.color = color;

        // Label
        var lblGO  = new GameObject("Label");
        lblGO.transform.SetParent(row.transform, false);
        var lblRect = lblGO.AddComponent<RectTransform>();
        lblRect.sizeDelta = new Vector2(120f, 14f);
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text     = label;
        tmp.fontSize = 11f;
        tmp.color    = Color.white;
    }
}
