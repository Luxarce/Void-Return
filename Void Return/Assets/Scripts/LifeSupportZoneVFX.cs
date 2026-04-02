using UnityEngine;

/// <summary>
/// Draws a visual radius indicator for the Life Support oxygen refill zone.
/// Attach to the Life Support repair point alongside LifeSupportZone.
///
/// The VFX has three elements:
///  1. A pulsing SpriteRenderer circle overlay (soft green glow).
///  2. A particle ring on the zone edge (gentle rising particles).
///  3. A Gizmo circle visible in the Scene view editor.
///
/// SETUP:
///  1. Select the LifeSupport repair point GameObject.
///  2. Add Component → LifeSupportZoneVFX.
///  3. If you have a soft radial gradient circle sprite (recommended):
///     drag it into the 'zoneFillSprite' field.
///     Otherwise the script creates a procedural circle automatically.
///  4. Adjust zoneRadius to match the LifeSupportZone's CircleCollider2D radius.
///  5. Assign lifeSupportModule so the VFX changes color when repaired.
/// </summary>
public class LifeSupportZoneVFX : MonoBehaviour
{
    [Header("Zone Radius")]
    [Tooltip("Should match the LifeSupportZone's CircleCollider2D radius.")]
    public float zoneRadius = 4f;

    [Tooltip("Module reference — VFX changes color once module is partially repaired.")]
    public ShipModule lifeSupportModule;

    [Header("Fill Circle VFX")]
    [Tooltip("Soft radial gradient sprite used as the zone fill (alpha ~30). " +
             "If empty, a programmatic circle is generated.")]
    public Sprite zoneFillSprite;

    [Tooltip("Color of the zone fill when the module is NOT yet repaired.")]
    public Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.12f);

    [Tooltip("Color of the zone fill when the module is active (at least Stage 1 done).")]
    public Color activeColor = new Color(0.1f, 1f, 0.4f, 0.18f);

    [Tooltip("Pulse speed (how fast the fill alpha breathes in and out).")]
    public float pulseSpeed = 1.2f;

    [Tooltip("How much the alpha varies during pulsing (0 = no pulse, 0.5 = large pulse).")]
    [Range(0f, 0.5f)]
    public float pulseAmplitude = 0.06f;

    [Header("Edge Particle Ring")]
    [Tooltip("Particle System for the edge ring effect. " +
             "If empty, no particles are generated.")]
    public ParticleSystem edgeParticles;

    [Tooltip("Color of edge particles when active.")]
    public Color particleActiveColor = new Color(0.2f, 1f, 0.5f, 0.8f);

    // ─────────────────────────────────────────────────────────────────────────
    private SpriteRenderer _fillRenderer;
    private float          _baseAlpha;
    private float          _pulseTimer;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        CreateFillCircle();
        SetupEdgeParticles();
    }

    private void Update()
    {
        if (_fillRenderer == null) return;

        bool isActive = lifeSupportModule == null
            || lifeSupportModule.Progress > 0f
            || lifeSupportModule.IsFullyRepaired;

        Color baseColor = isActive ? activeColor : inactiveColor;
        _baseAlpha      = baseColor.a;

        // Pulse the alpha
        _pulseTimer += Time.deltaTime * pulseSpeed;
        float pulse  = Mathf.Sin(_pulseTimer * Mathf.PI * 2f) * pulseAmplitude;

        baseColor.a          = Mathf.Clamp01(_baseAlpha + pulse);
        _fillRenderer.color  = baseColor;

        // Enable/disable particles based on active state
        if (edgeParticles != null)
        {
            if (isActive && !edgeParticles.isPlaying) edgeParticles.Play();
            if (!isActive && edgeParticles.isPlaying) edgeParticles.Stop();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void CreateFillCircle()
    {
        var go = new GameObject("LifeSupportZoneFill");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        // Scale the fill circle to match the zone radius
        // A standard sprite at PPU 100 is 1 unit wide, so scale = 2 * radius
        go.transform.localScale = Vector3.one * zoneRadius * 2f;

        _fillRenderer         = go.AddComponent<SpriteRenderer>();
        _fillRenderer.sprite  = zoneFillSprite != null
            ? zoneFillSprite
            : CreateCircleSprite();
        _fillRenderer.color   = inactiveColor;
        _fillRenderer.sortingOrder = -1; // render below other objects
    }

    private void SetupEdgeParticles()
    {
        if (edgeParticles == null) return;

        var shape = edgeParticles.shape;
        shape.enabled    = true;
        shape.shapeType  = ParticleSystemShapeType.Circle;
        shape.radius     = zoneRadius;
        // emitFromEdge was removed in Unity 6.
        // To emit from the edge of a Circle shape, use arcMode = Loop / Random.
        // Particles spawn around the full circle edge by default with Arc = 360.
        shape.arc = 360f;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

        var main = edgeParticles.main;
        main.startColor    = particleActiveColor;
        main.startSize     = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startSpeed    = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.loop          = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = edgeParticles.emission;
        emission.rateOverTime = 8f;
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c  = (size - 1) / 2f;
        float r  = c * 0.95f;
        float edge = c * 0.75f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            if (d > r)        { tex.SetPixel(x, y, Color.clear); continue; }
            // Stronger alpha toward the edge, softer in the center — ring-like feel
            float t = d / r;  // 0 at center, 1 at edge
            float a = Mathf.Lerp(0.3f, 1f, t);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size),
                              new Vector2(0.5f, 0.5f), size);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scene view gizmo
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        bool active = lifeSupportModule != null &&
                      (lifeSupportModule.Progress > 0f ||
                       lifeSupportModule.IsFullyRepaired);

        Gizmos.color = active
            ? new Color(0.1f, 1f, 0.4f, 0.4f)
            : new Color(0.5f, 0.5f, 0.5f, 0.25f);

        Gizmos.DrawWireSphere(transform.position, zoneRadius);
    }
}
