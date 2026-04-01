/// <summary>
/// Identifies which event type spawned a meteorite.
/// Set by MeteoriteManager on each Meteorite at spawn time.
/// Read by Meteorite.OnAnyImpact to determine which drop table to use.
/// </summary>
public enum MeteoriteType
{
    /// <summary>Single random hit — drops common Zone 1 materials.</summary>
    Stray,

    /// <summary>Part of a shower wave — bulk drops after the wave ends.</summary>
    Shower,

    /// <summary>Rare catastrophic strike — drops rare Zone 3 materials.</summary>
    Rift
}
