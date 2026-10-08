using AVAFlight.Core.Data;
using AVAFlight.Core.Galaxy;
using AVAFlight.Core.Model;
using AVAFlight.Core.Random;

namespace AVAFlight.Core.Engine;

public enum EventKind { Info, Warning, Danger, Success, Sound, ModeChanged, GameOver, Victory }

/// <summary>
/// Something the presentation layer should show or play. <see cref="Sound"/> is a logical cue name
/// (mapped to audio by the UI) so the engine has no audio dependency.
/// </summary>
public sealed record GameEvent(EventKind Kind, string Text, string? Sound = null);

/// <summary>Result of a player action: success flag plus a player-facing message.</summary>
public readonly record struct ActionResult(bool Ok, string Message)
{
    public static ActionResult Success(string msg = "") => new(true, msg);
    public static ActionResult Fail(string msg) => new(false, msg);
}

/// <summary>
/// One running game. Owns the canonical <see cref="GameState"/>, the immutable data, the galaxy
/// regenerated from the seed, and the injected RNG. All player actions go through the service
/// objects exposed here; nothing in this assembly touches UI, audio or files.
/// </summary>
public sealed class GameSession
{
    private readonly List<GameEvent> _events = [];
    private readonly List<string> _messages = [];

    public GameState State { get; }
    public GameData Data { get; }
    public GalaxyMap Galaxy { get; }
    public IGameRandom Rng { get; }
    public Policy Policy { get; private set; }

    public StarportService Starport { get; }
    public NavigationService Navigation { get; }
    public PlanetService Planets { get; }
    public CommsService Comms { get; }
    public CombatService Combat { get; }
    public StoryService Story { get; }
    public CrewService Crew { get; }

    public GameSession(GameState state, GameData data, IGameRandom? rng = null)
    {
        State = state;
        Data = data;
        Galaxy = GalaxyGenerator.Generate(state.GalaxySeed, data.World);
        Story = new StoryService(this);
        Story.ApplyFlareSchedule();
        Rng = rng ?? new SplitMix64(state.RngState == 0 ? state.GalaxySeed ^ 0xA5A5UL : state.RngState);
        Policy = Policy.For(state.Preset);
        Starport = new StarportService(this);
        Navigation = new NavigationService(this);
        Planets = new PlanetService(this);
        Comms = new CommsService(this);
        Combat = new CombatService(this);
        Crew = new CrewService(this);
    }

    /// <summary>Creates a brand-new game at Starport with the documented starting state.</summary>
    public static GameSession NewGame(GamePreset preset, GameData? data = null, ulong seed = GalaxyGenerator.DefaultSeed, IGameRandom? rng = null)
    {
        data ??= GameData.Default;
        var state = new GameState
        {
            GalaxySeed = seed,
            Preset = preset,
            Credits = data.Ship.StartingCredits,
            Ship = new ShipState { Engine = 1, Endurium = 20 },
        };
        foreach (var r in data.Races) state.Relation(r.Id, r.BaseAttitude);
        var s = new GameSession(state, data, rng);
        var arth = s.Galaxy.SystemAt(125, 100)!;
        state.Location.SystemId = arth.Id;
        state.VisitedSystems.Add(arth.Id);
        s.Story.Log(LogCategory.Mission, "Assigned to Interstel at Starport, Arth. Funds: 12,000 M.U.");
        return s;
    }

    /// <summary>Switches preset mid-game (allowed from settings; recorded in the log).</summary>
    public void SetPreset(GamePreset preset)
    {
        State.Preset = preset;
        Policy = Policy.For(preset);
    }

    public void Emit(EventKind kind, string text, string? sound = null)
    {
        _events.Add(new GameEvent(kind, text, sound));
        if (!string.IsNullOrEmpty(text) && kind != EventKind.Sound) Say(text);
    }

    public void PlaySound(string sound) => _events.Add(new GameEvent(EventKind.Sound, "", sound));

    /// <summary>Adds a line to the text window (the original's bottom message area).</summary>
    public void Say(string text)
    {
        _messages.Add(text);
        if (_messages.Count > 200) _messages.RemoveAt(0);
    }

    public IReadOnlyList<string> Messages => _messages;

    /// <summary>Returns and clears pending events (called by the UI each frame).</summary>
    public IReadOnlyList<GameEvent> DrainEvents()
    {
        var copy = _events.ToArray();
        _events.Clear();
        return copy;
    }

    /// <summary>Syncs RNG position into the state before saving.</summary>
    public GameState Snapshot()
    {
        State.RngState = Rng.State;
        return State;
    }

    public StarSystem? CurrentSystem => State.Location.SystemId is int id ? Galaxy.System(id) : null;
    public Planet? CurrentPlanet => State.Location.OrbitPlanetId is int id ? Galaxy.Planet(id) : null;

    public bool HasArtifact(string id) => State.Ship.Cargo.Any(c => c.Kind == CargoKind.Artifact && c.Id == id);
    public bool HasAnalyzedArtifact(string id) => State.Ship.Cargo.Any(c => c.Kind == CargoKind.Artifact && c.Id == id && c.Analyzed);

    /// <summary>Effective skill for a role; 0 if unassigned or dead (the next best crew member takes over).</summary>
    public int Skill(CrewRole role)
    {
        if (CrewRules.RoleSkill[role] is not Skill skill)
            return State.Assigned(role) is { IsDead: false } ? 1 : 0;
        var member = State.Assigned(role);
        if (member is { IsDead: false }) return ScaleByHealth(member, skill);
        // Manual: if a crew member dies, the next most capable crew member takes over the post.
        var stand = State.ActiveCrew.Where(c => !c.IsDead).OrderByDescending(c => c.Skills.GetValueOrDefault(skill)).FirstOrDefault();
        return stand is null ? 0 : ScaleByHealth(stand, skill);
    }

    private static int ScaleByHealth(CrewMember m, Skill s)
    {
        int raw = m.Skills.GetValueOrDefault(s);
        // RECONSTRUCTION: badly wounded crew perform worse (linear below 50% vitality).
        return m.Vitality >= 50 ? raw : raw * m.Vitality / 50;
    }

    public void AdvanceHours(double hours) => Story.AdvanceTime(hours);

    public void SetMode(GameMode mode)
    {
        if (State.Location.Mode == mode) return;
        State.Location.Mode = mode;
        _events.Add(new GameEvent(EventKind.ModeChanged, mode.ToString()));
    }

    public bool IsOver => State.Location.Mode == GameMode.GameOver;

    public void GameOver(string reason)
    {
        if (IsOver) return;
        State.GameOverReason = reason;
        Story.Log(LogCategory.Mission, "GAME OVER: " + reason);
        SetMode(GameMode.GameOver);
        Emit(EventKind.GameOver, reason, "explosion");
    }
}
