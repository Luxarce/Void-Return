using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Material Gathering Guide — a togglable panel that shows players
/// which materials spawn in each zone and their rarity.
///
/// SETUP:
///  1. Create a Panel named MaterialGuidePanel inside GameCanvas.
///  2. Add this script to the panel.
///  3. Create a Button anywhere in the UI named MaterialGuideButton.
///     Wire its onClick to MaterialGuide.ToggleGuide().
///  4. Inside MaterialGuidePanel, create a TextMeshProUGUI named GuideText.
///     Set font size to 14. Enable rich text.
///  5. Assign guideText and the panel itself in the Inspector.
///
/// The guide content is defined entirely in code — no external data needed.
/// </summary>
public class MaterialGuide : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The guide panel GameObject. Starts closed.")]
    public GameObject guidePanel;

    [Tooltip("TextMeshProUGUI inside the panel where guide content appears.")]
    public TextMeshProUGUI guideText;

    [Tooltip("Button that opens/closes the guide.")]
    public Button toggleButton;

    [Tooltip("TMP label on the toggle button.")]
    public TextMeshProUGUI toggleButtonLabel;

    // ─────────────────────────────────────────────────────────────────────────
    private bool _isOpen = false;

    private static readonly string GUIDE_CONTENT =
@"<b><color=#F4A522>MATERIAL GATHERING GUIDE</color></b>

<b><color=#E2E84A>Zone 1 — Debris Field</color></b>  (radius 10-40 units)
  <color=#E2E84A>•</color> Metal Scrap    — Very common. Found on debris chunks.
  <color=#E2E84A>•</color> Bolt           — Common. Scattered near hull fragments.
  <color=#E2E84A>•</color> Glass          — Common. Near shattered viewports.
  <color=#E2E84A>•</color> Foam           — Common. Insulation from the ship walls.
  <color=#E2E84A>•</color> Sealant        — Common. Found in maintenance sections.

<b><color=#33B5E5>Zone 2 — Drift Ring</color></b>  (radius 40-80 units)
  <color=#33B5E5>•</color> Copper Wire    — Mid-tier. Coiled in satellite fragments.
  <color=#33B5E5>•</color> Filter         — Mid-tier. From life support wreckage.
  <color=#33B5E5>•</color> Circuit Board  — Mid-tier. Electronics in drift pods.
  <color=#33B5E5>•</color> Titanium       — Mid-tier. Structural panels.
  <color=#33B5E5>•</color> Lens           — Mid-tier. Optical equipment clusters.
  <color=#33B5E5>•</color> Antenna Shards — Mid-tier. Communication array debris.

<b><color=#CC44FF>Zone 3 — Deep Scatter</color></b>  (radius 80-120 units)
  <color=#CC44FF>•</color> Fuel Cell      — Rare. Near propulsion wreckage.
  <color=#CC44FF>•</color> Coolant        — Rare. Leaking from engine fragments.
  <color=#CC44FF>•</color> Heat Shield    — Rare. Re-entry shielding pieces.
  <color=#CC44FF>•</color> Titanium Rod   — Rare. Structural deep-space alloy.
  <color=#CC44FF>•</color> Oxygen Canister — Rare. Emergency supply caches.

<b><color=#27AE60>Tips</color></b>
  • Repair Navigation Stage 2 to see materials on the minimap.
  • Material markers are color-coded by zone (yellow/cyan/purple).
  • Uncollected materials despawn after 90-180 seconds — collect quickly!
  • Meteorite impacts scatter materials near the impact site.";

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        toggleButton?.onClick.AddListener(ToggleGuide);
        guidePanel?.SetActive(false);
        if (guideText != null) guideText.text = GUIDE_CONTENT;
        if (toggleButtonLabel != null) toggleButtonLabel.text = "Material Guide";
    }

    public void ToggleGuide()
    {
        _isOpen = !_isOpen;
        guidePanel?.SetActive(_isOpen);
    }

    public void OpenGuide()  { _isOpen = true;  guidePanel?.SetActive(true); }
    public void CloseGuide() { _isOpen = false; guidePanel?.SetActive(false); }
}
