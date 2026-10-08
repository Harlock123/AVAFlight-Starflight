using AVAFlight.Core.Galaxy;

namespace AVAFlight.Core.Data;

// All game content is data-driven. These records mirror the JSON files embedded under
// AVAFlight.Core/Data/*.json and are validated on load by GameData.Validate().

public sealed record WorldDefinition
{
    public List<SystemDefinition> Systems { get; init; } = [];
    public List<TerritoryDefinition> Territories { get; init; } = [];
    public List<FluxDefinition> Fluxes { get; init; } = [];
    public List<NebulaDefinition> Nebulae { get; init; } = [];
}

public sealed record SystemDefinition
{
    public required string Name { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public SpectralClass Class { get; init; } = SpectralClass.G;
    public string? Territory { get; init; }
    public int PlanetCount { get; init; }
    public List<PlanetDefinition> Planets { get; init; } = [];
    /// <summary>Evidence tag: "documented" (position from public sources) or "reconstructed".</summary>
    public string Evidence { get; init; } = "reconstructed";
    /// <summary>Flare time as days from game start (Arth's star flares per the Elowan hint).</summary>
    public int? FlareDay { get; init; }
}

public sealed record PlanetDefinition
{
    public required int Orbit { get; init; }
    public string? Name { get; init; }
    public PlanetType? Type { get; init; }
    public int? Mass { get; init; }
    public double? Gravity { get; init; }
    public Atmosphere? Atmosphere { get; init; }
    public string? AtmosphereComposition { get; init; }
    public Hydrosphere? Hydrosphere { get; init; }
    public Temperature? Temperature { get; init; }
    public Weather? Weather { get; init; }
    public Density? MineralDensity { get; init; }
    public Density? BioDensity { get; init; }
    public List<SiteDefinition> Sites { get; init; } = [];
}

public sealed record SiteDefinition(string Id, SiteKind Kind, int Lat, int Lon, string Ref);
public sealed record TerritoryDefinition(string RaceId, int X, int Y, double Radius);
public sealed record FluxDefinition(int X1, int Y1, int X2, int Y2);
public sealed record NebulaDefinition(double X, double Y, double Radius);

public sealed record MineralDefinition(string Id, string Name, int ValuePerUnit, double Rarity);

public enum ComponentKind { Engine, Shield, Armor, Missile, Laser }

public sealed record ComponentTier
{
    public required ComponentKind Kind { get; init; }
    public required int Class { get; init; }
    public required int Price { get; init; }
    public required int Mass { get; init; }
    /// <summary>Kind-specific strength: engine thrust, shield/armor points, weapon damage.</summary>
    public required int Strength { get; init; }
    public string Evidence { get; init; } = "reconstructed";
}

public sealed record ShipCatalog
{
    public required List<ComponentTier> Components { get; init; }
    public required int CargoPodPrice { get; init; }
    public required int CargoPodCapacity { get; init; }
    public required int MaxCargoPods { get; init; }
    public required int HullMass { get; init; }
    public required int PodMass { get; init; }
    public required int RepairPricePerPoint { get; init; }
    public required int FuelPricePerUnit { get; init; }
    public required int StartingCredits { get; init; }
}

public enum Skill { Science, Navigation, Engineering, Communication, Medicine }

public sealed record CrewRaceDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Dictionary<Skill, int> Start { get; init; }
    public required Dictionary<Skill, int> Max { get; init; }
    public required int Durability { get; init; }
    public required int LearnRate { get; init; }
    public string Evidence { get; init; } = "reconstructed";
}

public enum Posture { Friendly, Hostile, Obsequious }
public enum CommTopic { Themselves, OtherRaces, OldEmpire, Ancients, General }

/// <summary>A dialogue line. Conditions gate availability; text is newly authored for AVAFlight.</summary>
public sealed record DialogueLine
{
    public required string Text { get; init; }
    /// <summary>Minimum attitude (-100..100) for this line to be eligible.</summary>
    public int MinAttitude { get; init; } = -100;
    /// <summary>Story flag that must be set (null = none).</summary>
    public string? RequiresFlag { get; init; }
    /// <summary>Story flag set when the line is delivered (a "revelation").</summary>
    public string? SetsFlag { get; init; }
    /// <summary>Log entry recorded in the captain's log when delivered (Modern mode shows it).</summary>
    public string? LogNote { get; init; }
    /// <summary>Line only offered after the race has surrendered.</summary>
    public bool SurrenderOnly { get; init; }
}

public sealed record AlienShipStats(int Hull, int Shield, int LaserDamage, int MissileDamage, double Speed, double Aggression, bool MissileResistant);

public sealed record AlienRaceDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Disposition { get; init; }
    public required int BaseAttitude { get; init; }
    /// <summary>Attitude change per exchange for each posture the player uses.</summary>
    public required Dictionary<Posture, int> PostureEffect { get; init; }
    public bool Talks { get; init; } = true;
    public bool AlwaysHostile { get; init; }
    /// <summary>Surrenders after sustained hostile pressure (Spemin).</summary>
    public bool SurrendersToPressure { get; init; }
    /// <summary>Crew race that this race refuses to deal with (Elowan vs Thrynn).</summary>
    public string? Enemy { get; init; }
    public required string Syllables { get; init; }
    public required string PortraitColor { get; init; }
    public required AlienShipStats Ship { get; init; }
    public int FleetSize { get; init; } = 2;
    public List<string> Greetings { get; init; } = [];
    public List<string> Farewells { get; init; } = [];
    public List<string> HostileLines { get; init; } = [];
    public List<string> SurrenderLines { get; init; } = [];
    public Dictionary<CommTopic, List<DialogueLine>> Answers { get; init; } = [];
    public string Evidence { get; init; } = "documented";
}

public sealed record ArtifactDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int Value { get; init; }
    /// <summary>Effect id interpreted by the engine (e.g. "orb_protection", "flux_reveal").</summary>
    public string? Effect { get; init; }
    public bool PlotCritical { get; init; }
    public string Evidence { get; init; } = "documented";
}

public sealed record MessageDefinition(string Id, string Title, string Text, string? SetsFlag, string? LogNote, string Evidence);

public sealed record NoticeDefinition(string Id, int Day, string Text, string? RequiresFlag);
