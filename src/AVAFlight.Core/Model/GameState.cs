using AVAFlight.Core.Data;

namespace AVAFlight.Core.Model;

public enum GamePreset { Classic, Modern }

public enum CrewRole { Captain, Science, Navigator, Engineer, Communications, Doctor }

public enum GameMode { Starport, Hyperspace, System, Orbit, Surface, Encounter, GameOver }

public enum ShipSystem { Engines, Sensors, Comms, Shields, Missiles, Lasers }

public enum CargoKind { Mineral, Lifeform, Artifact, Message, Debris }

public enum LogCategory { Mission, Discovery, Contact, Artifact, Navigation, Combat, Crew, Trade, PlanetLog }

public sealed class CrewMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string RaceId { get; set; } = "human";
    public Dictionary<Skill, int> Skills { get; set; } = new();
    /// <summary>Health 0..100. 0 = dead (permanent: crew death persists in saves).</summary>
    public int Vitality { get; set; } = 100;
    public int TrainingSessions { get; set; }
    public double HealAccumulator { get; set; }
    public bool IsDead => Vitality <= 0;

    public string HealthLabel => Vitality switch
    {
        <= 0 => "Dead",
        < 20 => "Critically wounded",
        < 45 => "Heavily wounded",
        < 70 => "Moderately wounded",
        < 100 => "Slightly wounded",
        _ => "Not wounded",
    };
}

public sealed class CargoItem
{
    public CargoKind Kind { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Cubic metres (minerals) or count (other kinds).</summary>
    public double Quantity { get; set; }
    /// <summary>Hold volume used per unit of quantity.</summary>
    public double VolumePerUnit { get; set; } = 1;
    public int ValuePerUnit { get; set; }
    public bool Analyzed { get; set; }
    public int SourcePlanetId { get; set; } = -1;
    public double Volume => Quantity * VolumePerUnit;
}

public sealed class ShipState
{
    public string Name { get; set; } = "";
    public int Engine { get; set; } = 1;
    public int Shield { get; set; }
    public int Armor { get; set; }
    public int Missile { get; set; }
    public int Laser { get; set; }
    public int CargoPods { get; set; }
    /// <summary>Endurium aboard, m³. It is both fuel and a trade good (one pool).</summary>
    public double Endurium { get; set; } = 20;
    public int HullPoints { get; set; } = ShipRules.BaseHull;
    public int ArmorPoints { get; set; }
    public int ShieldPoints { get; set; }
    public bool ShieldsUp { get; set; }
    public bool WeaponsArmed { get; set; }
    /// <summary>Percent damage per system; equals the percent chance the system fails when used.</summary>
    public Dictionary<ShipSystem, int> Damage { get; set; } = Enum.GetValues<ShipSystem>().ToDictionary(s => s, _ => 0);
    public List<CargoItem> Cargo { get; set; } = [];
    /// <summary>Whether the terrain vehicle is aboard (lost TVs are replaced at Starport for a fee).</summary>
    public bool HasTerrainVehicle { get; set; } = true;
}

public sealed class RaceRelation
{
    public int Attitude { get; set; }
    public int Encounters { get; set; }
    public int ShipsDestroyed { get; set; }
    public bool Contacted { get; set; }
    public bool Surrendered { get; set; }
    public bool PermanentEnemy { get; set; }
}

public sealed class PlanetRecord
{
    public bool Scanned { get; set; }
    public bool Analyzed { get; set; }
    public bool Landed { get; set; }
    public bool Logged { get; set; }
    public bool Recommended { get; set; }
    public bool Destroyed { get; set; }
    public HashSet<int> MinedDeposits { get; set; } = [];
    public HashSet<string> CollectedSites { get; set; } = [];
    public HashSet<int> StunnedLifeforms { get; set; } = [];
    /// <summary>Hour at which ruins were last emptied (ruins restock after leaving orbit).</summary>
    public long RuinEmptiedHour { get; set; } = -1;
}

public sealed record LogEntry(long Hour, LogCategory Category, string Text);
public sealed record Waypoint(int X, int Y, string Note);
public sealed record BankTransaction(long Hour, string Description, int Amount);

/// <summary>Where the ship is and what view the game is in.</summary>
public sealed class Location
{
    public GameMode Mode { get; set; } = GameMode.Starport;
    /// <summary>Hyperspace coordinates (map units).</summary>
    public double HyperX { get; set; } = 125;
    public double HyperY { get; set; } = 100;
    public int? SystemId { get; set; }
    /// <summary>In-system position, -100..100 on each axis with the star at the origin.</summary>
    public double SysX { get; set; }
    public double SysY { get; set; }
    public int? OrbitPlanetId { get; set; }
    /// <summary>Heading and speed for continuous movement (map units per hour).</summary>
    public double VelX { get; set; }
    public double VelY { get; set; }
    /// <summary>Hyperspace destination for cruise (set on the star map); null = manual flight.</summary>
    public int? CruiseX { get; set; }
    public int? CruiseY { get; set; }
    /// <summary>After passing through a flux the navigator must re-fix position.</summary>
    public bool PositionLost { get; set; }
    public GameMode ModeBeforeEncounter { get; set; } = GameMode.Hyperspace;
}

public sealed class SurfaceState
{
    public int PlanetId { get; set; }
    public int TvX { get; set; }
    public int TvY { get; set; }
    public int ShipX { get; set; }
    public int ShipY { get; set; }
    public double TvFuel { get; set; }
    public List<CargoItem> TvCargo { get; set; } = [];
    public bool OnFoot { get; set; }
    public int Steps { get; set; }
    public List<SurfaceCreature> Creatures { get; set; } = [];
    /// <summary>TV step at which an armed Black Egg detonates; -1 = none armed.</summary>
    public int EggFuseAt { get; set; } = -1;
}

public sealed class SurfaceCreature
{
    public int Id { get; set; }
    public string Species { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Health { get; set; }
    public bool Hostile { get; set; }
    public bool Flying { get; set; }
    public int StunnedTurns { get; set; }
    public int Size { get; set; }
}

/// <summary>
/// The canonical, fully serializable game state. The galaxy itself is NOT stored: it is regenerated
/// from <see cref="GalaxySeed"/> + game data, and only player-caused changes (planet records,
/// flags, relations) are persisted.
/// </summary>
public sealed class GameState
{
    /// <summary>Bump when the save format changes, and add a migration in SaveStore.</summary>
    public const int CurrentSchemaVersion = 2;

    public ulong GalaxySeed { get; set; }
    public GamePreset Preset { get; set; } = GamePreset.Modern;
    public ulong RngState { get; set; }
    public GameClock Clock { get; set; } = new();
    public int Credits { get; set; }
    public long LastInterestHour { get; set; }
    public List<BankTransaction> Transactions { get; set; } = [];
    public ShipState Ship { get; set; } = new();
    public List<CrewMember> Roster { get; set; } = [];
    public Dictionary<CrewRole, Guid> Assignments { get; set; } = new();
    public Location Location { get; set; } = new();
    public SurfaceState? Surface { get; set; }
    public EncounterState? Encounter { get; set; }
    public HashSet<string> Flags { get; set; } = [];
    public Dictionary<string, RaceRelation> Relations { get; set; } = new();
    public List<LogEntry> Log { get; set; } = [];
    public List<Waypoint> Waypoints { get; set; } = [];
    public HashSet<int> VisitedSystems { get; set; } = [];
    public Dictionary<int, PlanetRecord> Planets { get; set; } = new();
    public HashSet<string> RecordedSpecies { get; set; } = [];
    public HashSet<string> SoldSpecies { get; set; } = [];
    public HashSet<string> NoticesSeen { get; set; } = [];
    public HashSet<string> ArtifactsSold { get; set; } = [];
    public bool Won { get; set; }
    public string? GameOverReason { get; set; }
    public GameStats Stats { get; set; } = new();
    public bool OperationsEvaluated { get; set; }

    public PlanetRecord PlanetRecord(int planetId)
    {
        if (!Planets.TryGetValue(planetId, out var r)) Planets[planetId] = r = new PlanetRecord();
        return r;
    }

    public RaceRelation Relation(string raceId, int baseAttitude = 0)
    {
        if (!Relations.TryGetValue(raceId, out var r)) Relations[raceId] = r = new RaceRelation { Attitude = baseAttitude };
        return r;
    }

    public CrewMember? Assigned(CrewRole role) =>
        Assignments.TryGetValue(role, out var id) ? Roster.FirstOrDefault(c => c.Id == id) : null;

    public IEnumerable<CrewMember> ActiveCrew =>
        Assignments.Values.Distinct().Select(id => Roster.FirstOrDefault(c => c.Id == id)).Where(c => c is not null)!;

    public string LocationSummary() => Location.Mode switch
    {
        GameMode.Starport => "Starport, Arth",
        GameMode.Hyperspace => $"Hyperspace {Location.HyperX:0},{Location.HyperY:0}",
        GameMode.GameOver => "Game over",
        _ => $"System {Location.HyperX:0},{Location.HyperY:0}",
    };

    /// <summary>Structural sanity checks run after loading a save.</summary>
    public void Validate()
    {
        if (Credits < 0) throw new InvalidDataException("Negative credits");
        if (Ship is null || Roster is null || Location is null || Clock is null) throw new InvalidDataException("Missing core section");
        if (Ship.Engine is < 0 or > 5 || Ship.Shield is < 0 or > 5 || Ship.Armor is < 0 or > 5 ||
            Ship.Missile is < 0 or > 5 || Ship.Laser is < 0 or > 5) throw new InvalidDataException("Component class out of range");
        if (Ship.CargoPods is < 0 or > 16) throw new InvalidDataException("Cargo pods out of range");
        if (Ship.Endurium < 0 || double.IsNaN(Ship.Endurium)) throw new InvalidDataException("Invalid endurium");
        foreach (var c in Roster)
            if (c.Vitality is < 0 or > 100) throw new InvalidDataException($"Crew {c.Name}: vitality out of range");
        foreach (var id in Assignments.Values)
            if (Roster.All(c => c.Id != id)) throw new InvalidDataException("Assignment references unknown crew member");
    }
}

public sealed class GameStats
{
    public double MineralsCollected { get; set; }
    public int CreditsEarned { get; set; }
    public int ShipsDestroyed { get; set; }
    public int PlanetsLanded { get; set; }
    public int SystemsVisited { get; set; }
    public int AliensContacted { get; set; }
}

/// <summary>
/// Game time in hours since 01-01-4620. Calendar per the decompiled .STARDATE logic: 10 months of
/// 30 days (300-day year). Displayed as DD-MM-YYYY like the original.
/// </summary>
public sealed class GameClock
{
    public long Hours { get; set; }
    public long Day => Hours / 24;
    public int Hour => (int)(Hours % 24);
    public int Year => 4620 + (int)Math.Floor(Day / 300.0);
    public int Month => (int)(Mod(Day, 300) / 30) + 1;
    public int DayOfMonth => (int)Mod(Day, 30) + 1;

    private static long Mod(long a, long m) => ((a % m) + m) % m;

    public static string FormatDay(long day)
    {
        var c = new GameClock { Hours = day * 24 };
        return $"{c.DayOfMonth:00}-{c.Month:00}-{c.Year}";
    }

    public override string ToString() => $"{DayOfMonth:00}-{Month:00}-{Year}";
    public string WithHour => $"{this} {Hour:00}:00";
}
