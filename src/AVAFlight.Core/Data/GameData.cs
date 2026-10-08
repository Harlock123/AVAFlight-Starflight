using AVAFlight.Core.Galaxy;
using AVAFlight.Core.Serialization;

namespace AVAFlight.Core.Data;

/// <summary>Thrown when a data file is missing or fails validation.</summary>
public sealed class GameDataException(string message) : Exception(message);

/// <summary>
/// All static game content, loaded from embedded JSON and validated. Immutable after load and
/// shared by every game session.
/// </summary>
public sealed class GameData
{
    public required WorldDefinition World { get; init; }
    public required ShipCatalog Ship { get; init; }
    public required IReadOnlyList<CrewRaceDefinition> CrewRaces { get; init; }
    public required IReadOnlyList<MineralDefinition> Minerals { get; init; }
    public required IReadOnlyList<AlienRaceDefinition> Races { get; init; }
    public required IReadOnlyList<ArtifactDefinition> Artifacts { get; init; }
    public required IReadOnlyList<MessageDefinition> Messages { get; init; }
    public required IReadOnlyList<NoticeDefinition> Notices { get; init; }

    private static readonly Lazy<GameData> s_default = new(() => LoadEmbedded());
    public static GameData Default => s_default.Value;

    public CrewRaceDefinition CrewRace(string id) =>
        CrewRaces.FirstOrDefault(r => r.Id == id) ?? throw new KeyNotFoundException($"Unknown crew race '{id}'");
    public AlienRaceDefinition Race(string id) =>
        Races.FirstOrDefault(r => r.Id == id) ?? throw new KeyNotFoundException($"Unknown race '{id}'");
    public ArtifactDefinition Artifact(string id) =>
        Artifacts.FirstOrDefault(a => a.Id == id) ?? throw new KeyNotFoundException($"Unknown artifact '{id}'");
    public MineralDefinition Mineral(string id) =>
        Minerals.FirstOrDefault(m => m.Id == id) ?? throw new KeyNotFoundException($"Unknown mineral '{id}'");
    public MessageDefinition Message(string id) =>
        Messages.FirstOrDefault(m => m.Id == id) ?? throw new KeyNotFoundException($"Unknown message '{id}'");
    public ComponentTier Component(ComponentKind kind, int cls) =>
        Ship.Components.FirstOrDefault(c => c.Kind == kind && c.Class == cls)
        ?? throw new KeyNotFoundException($"No {kind} class {cls}");

    public static GameData LoadEmbedded()
    {
        var asm = typeof(GameData).Assembly;
        string Read(string name)
        {
            using var s = asm.GetManifestResourceStream($"AVAFlight.Data.{name}.json")
                          ?? throw new GameDataException($"Missing embedded data file '{name}.json'");
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        }
        return Load(Read);
    }

    /// <summary>Loads from any source (tests use this to inject broken data).</summary>
    public static GameData Load(Func<string, string> readFile)
    {
        T Parse<T>(string name)
        {
            try { return GameJson.Deserialize<T>(readFile(name)); }
            catch (Exception e) when (e is System.Text.Json.JsonException or NotSupportedException)
            {
                throw new GameDataException($"{name}.json: {e.Message}");
            }
        }
        var data = new GameData
        {
            World = Parse<WorldDefinition>("world"),
            Ship = Parse<ShipCatalog>("ship"),
            CrewRaces = Parse<List<CrewRaceDefinition>>("crew"),
            Minerals = Parse<List<MineralDefinition>>("minerals"),
            Races = Parse<List<AlienRaceDefinition>>("races"),
            Artifacts = Parse<List<ArtifactDefinition>>("artifacts"),
            Messages = Parse<List<MessageDefinition>>("messages"),
            Notices = Parse<List<NoticeDefinition>>("notices"),
        };
        var errors = data.Validate();
        if (errors.Count > 0) throw new GameDataException("Game data invalid:\n - " + string.Join("\n - ", errors));
        return data;
    }

    public List<string> Validate()
    {
        var e = new List<string>();
        void Unique<T>(IEnumerable<T> items, Func<T, string> key, string what)
        {
            foreach (var g in items.GroupBy(key).Where(g => g.Count() > 1)) e.Add($"Duplicate {what} id '{g.Key}'");
        }
        Unique(Minerals, m => m.Id, "mineral");
        Unique(Races, r => r.Id, "race");
        Unique(Artifacts, a => a.Id, "artifact");
        Unique(Messages, m => m.Id, "message");
        Unique(CrewRaces, r => r.Id, "crew race");
        Unique(World.Systems, s => $"{s.X},{s.Y}", "system coordinate");

        foreach (var m in Minerals)
        {
            if (m.ValuePerUnit <= 0) e.Add($"Mineral {m.Id}: value must be positive");
            if (m.Rarity <= 0) e.Add($"Mineral {m.Id}: rarity must be positive");
        }
        if (!Minerals.Any(m => m.Id == "endurium")) e.Add("Minerals must include endurium");

        foreach (ComponentKind kind in Enum.GetValues<ComponentKind>())
            for (int c = 1; c <= 5; c++)
                if (Ship.Components.Count(x => x.Kind == kind && x.Class == c) != 1)
                    e.Add($"Ship catalog needs exactly one {kind} class {c}");
        foreach (var c in Ship.Components)
            if (c.Price <= 0 || c.Mass < 0 || c.Strength <= 0) e.Add($"{c.Kind} {c.Class}: invalid price/mass/strength");
        if (Ship.StartingCredits < 0 || Ship.CargoPodCapacity <= 0 || Ship.MaxCargoPods <= 0) e.Add("Ship catalog: invalid basics");

        foreach (var r in CrewRaces)
            foreach (Skill s in Enum.GetValues<Skill>())
            {
                if (!r.Start.ContainsKey(s) || !r.Max.ContainsKey(s)) { e.Add($"Crew race {r.Id}: missing skill {s}"); continue; }
                if (r.Start[s] < 0 || r.Start[s] > r.Max[s] || r.Max[s] > 250) e.Add($"Crew race {r.Id}: bad range for {s}");
            }

        var artifactIds = Artifacts.Select(a => a.Id).ToHashSet();
        var messageIds = Messages.Select(m => m.Id).ToHashSet();
        var raceIds = Races.Select(r => r.Id).ToHashSet();
        foreach (var s in World.Systems)
        {
            if (s.X is < 0 or >= GalaxyMap.Width || s.Y is < 0 or >= GalaxyMap.Height) e.Add($"System {s.Name}: out of map");
            if (s.Territory is not null && !raceIds.Contains(s.Territory)) e.Add($"System {s.Name}: unknown territory {s.Territory}");
            if (s.Planets.Any(p => p.Orbit is < 1 or > 8)) e.Add($"System {s.Name}: orbit out of 1..8");
            foreach (var p in s.Planets)
            foreach (var site in p.Sites)
            {
                if (site.Lat is < -90 or > 90 || site.Lon is < -180 or > 180) e.Add($"Site {site.Id}: bad lat/lon");
                if (site.Kind == SiteKind.Artifact && !artifactIds.Contains(site.Ref)) e.Add($"Site {site.Id}: unknown artifact {site.Ref}");
                if (site.Kind == SiteKind.Message && !messageIds.Contains(site.Ref)) e.Add($"Site {site.Id}: unknown message {site.Ref}");
            }
        }
        foreach (var t in World.Territories)
            if (!raceIds.Contains(t.RaceId)) e.Add($"Territory references unknown race {t.RaceId}");
        foreach (var r in Races)
        {
            if (r.BaseAttitude is < -100 or > 100) e.Add($"Race {r.Id}: base attitude out of range");
            if (r.Enemy is not null && CrewRaces.All(c => c.Id != r.Enemy)) e.Add($"Race {r.Id}: enemy must be a crew race");
            if (r.Talks && r.Greetings.Count == 0) e.Add($"Race {r.Id}: talking race needs greetings");
            foreach (var line in r.Answers.Values.SelectMany(l => l))
                if (line.MinAttitude is < -100 or > 100) e.Add($"Race {r.Id}: line attitude out of range");
        }
        if (!World.Systems.Any(s => s.Name == "Arth")) e.Add("World must define Arth");
        if (!World.Systems.Any(s => s.Planets.Any(p => p.Sites.Any(x => x.Kind == SiteKind.Nexus)))) e.Add("World must define a Nexus site");
        return e;
    }
}
