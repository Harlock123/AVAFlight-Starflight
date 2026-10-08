using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AVAFlight.Avalonia.Panels;
using AVAFlight.Avalonia.Rendering;
using AVAFlight.Avalonia.Services;
using AVAFlight.Avalonia.Ui;
using AVAFlight.Core.Data;
using AVAFlight.Core.Engine;
using AVAFlight.Core.Galaxy;
using AVAFlight.Core.Model;
using AVAFlight.Infrastructure.Input;

namespace AVAFlight.Avalonia.Screens;

/// <summary>A modal panel shown over the main view.</summary>
public abstract class Overlay
{
    public abstract Control View { get; }
    /// <summary>Handle input; return false to let Back close the overlay.</summary>
    public virtual bool Handle(InputAction a) => false;
    public virtual void Tick(double dt) { }
    public virtual bool PausesTime => true;
    public virtual bool CapturesText => false;
    public virtual void Closed() { }
}

/// <summary>Overlay built on a <see cref="PageStack"/>; Back at the root closes it.</summary>
public abstract class StackOverlay : Overlay
{
    protected readonly PageStack Stack = new();
    protected readonly GameScreen Screen;
    protected GameSession S => Screen.S;
    private readonly Control _view;

    protected StackOverlay(GameScreen screen)
    {
        Screen = screen;
        Stack.ShowTooltips = S.Policy.Tooltips;
        _view = Stack;
    }

    public override Control View => _view;
    public override bool CapturesText => Stack.IsTyping;
    public override bool Handle(InputAction a)
    {
        if (a == InputAction.Back && Stack.Depth <= 1) return false;
        return Stack.Handle(a);
    }
}

// ---------------------------------------------------------------------- options / pause

public sealed class PauseOverlay : StackOverlay
{
    public PauseOverlay(GameScreen screen, bool startAtSave = false) : base(screen)
    {
        Stack.SetRoot(new MenuPage
        {
            Title = "Options (paused)",
            Items = () =>
            [
                new MenuEntry("Resume", screen.CloseOverlay),
                new MenuEntry("Save game", screen.CanSave ? () => Stack.Push(SavePage()) : null, screen.CanSave,
                    screen.CanSave ? null : "You cannot save during an encounter."),
                new MenuEntry("Settings", () => screen.Host?.Navigate(new SettingsScreen(screen))),
                new MenuEntry($"Switch to {(S.State.Preset == GamePreset.Classic ? "Modern" : "Classic")} preset", () =>
                {
                    S.SetPreset(S.State.Preset == GamePreset.Classic ? GamePreset.Modern : GamePreset.Classic);
                    S.Say($"Preset changed to {S.State.Preset}.");
                    screen.CloseOverlay();
                }, Hint: "Both presets share all rules and numbers. Modern adds information and convenience features."),
                new MenuEntry(AppServices.Current.Settings.ShowHints ? "Turn off hints permanently" : "Turn hints back on", () =>
                {
                    AppServices.Current.Settings.ShowHints = !AppServices.Current.Settings.ShowHints;
                    AppServices.Current.SaveSettings();
                    screen.CloseOverlay();
                }),
                new MenuEntry("Quit to title (unsaved progress is lost)", () => screen.Host?.Navigate(new TitleScreen())),
            ],
            Details = () => Ui.Ui.Box(Ui.Ui.VStack(4,
                Ui.Ui.Label("CONTROLS", 18, Ui.Ui.Accent),
                Ui.Ui.Label("Arrows / WASD / D-pad: move, steer, menus", 18),
                Ui.Ui.Label("Enter / Space / (A): select    Esc / (B): back", 18),
                Ui.Ui.Label("M star map  C crew  I ship  G cargo  L log", 18),
                Ui.Ui.Label("H hail  N scan  F fire  P pause  F5 save", 18),
                Ui.Ui.Label("+/- zoom   F11 fullscreen", 18))),
        });
        if (startAtSave && screen.CanSave) Stack.Push(SavePage());
    }

    private MenuPage SavePage()
    {
        if (S.Policy.NamedSaveSlots)
            return new MenuPage
            {
                Title = "Save",
                Items = () =>
                {
                    var items = new List<MenuEntry> { new("New save slot...", () => Stack.Push(NamePage())) };
                    items.AddRange(AppServices.Current.Saves.List().Where(s => s.SchemaVersion > 0).Select(s =>
                        new MenuEntry($"Overwrite: {s.DisplayName}", () => { Screen.SaveTo(s.Slot, s.DisplayName); Screen.CloseOverlay(); }, Value: s.StarDate)));
                    return items;
                },
            };
        return new MenuPage
        {
            Title = "Save",
            Items = () => [new MenuEntry($"Save over '{Screen.DefaultSlot}'", () => { Screen.SaveTo(Screen.DefaultSlot, "ISS " + S.State.Ship.Name); Screen.CloseOverlay(); })],
            Details = () => Ui.Ui.Box(Ui.Ui.Prose("Classic mode keeps one save per ship, like the original game.", 15)),
        };
    }

    private MenuPage NamePage() => new()
    {
        Title = "Name",
        TextPrompt = "Name this save:",
        MaxLength = 30,
        OnSubmitText = name =>
        {
            if (string.IsNullOrWhiteSpace(name)) { Stack.Status("Please enter a name.", true); return; }
            Screen.SaveTo(Infrastructure.Persistence.SaveStore.SanitizeSlot(name), name.Trim());
            Screen.CloseOverlay();
        },
    };
}

// ---------------------------------------------------------------------- star map

public sealed class StarMapOverlay : Overlay
{
    private readonly GameScreen _screen;
    private readonly StarMapView _map;
    private readonly Grid _view;
    private readonly TextBox _note = new() { PlaceholderText = "Waypoint note, Enter to place", IsVisible = false, FontSize = 20 };
    private readonly TextBlock _help;

    public StarMapOverlay(GameScreen screen)
    {
        _screen = screen;
        _map = new StarMapView(screen.S);
        bool modern = screen.S.Policy.Waypoints;
        _help = Ui.Ui.Label("Arrows: cursor  Enter: set course  +/-: zoom" + (modern ? "  W: waypoint  X: remove waypoint" : "") + "  Esc: close", 18, Ui.Ui.Dim);
        _help.VerticalAlignment = VerticalAlignment.Top;
        _help.HorizontalAlignment = HorizontalAlignment.Left;
        _help.Margin = new Thickness(8, 4);
        _note.VerticalAlignment = VerticalAlignment.Bottom;
        _note.Margin = new Thickness(8, 0, 8, 60);
        _note.FontFamily = Ui.Ui.DataFont;
        _view = new Grid { Children = { _map, _help, _note } };
    }

    public override Control View => _view;
    public override bool CapturesText => _note.IsVisible;

    public override void Tick(double dt)
    {
        if (_note.IsVisible) return;
        var (dx, dy) = HeldInput.Direction();
        double speed = 30 / _map.Zoom;
        _map.CursorX = Math.Clamp(_map.CursorX + dx * speed * dt, 0, GalaxyMap.Width - 1);
        _map.CursorY = Math.Clamp(_map.CursorY + dy * speed * dt, 0, GalaxyMap.Height - 1);
    }

    public override bool Handle(InputAction a)
    {
        var s = _screen.S;
        if (_note.IsVisible)
        {
            if (a == InputAction.Confirm)
            {
                string note = string.IsNullOrWhiteSpace(_note.Text) ? $"WP{s.State.Waypoints.Count + 1}" : _note.Text.Trim();
                s.State.Waypoints.Add(new Waypoint((int)Math.Round(_map.CursorX), (int)Math.Round(_map.CursorY), note[..Math.Min(24, note.Length)]));
                _note.IsVisible = false;
                _note.Text = "";
                return true;
            }
            if (a == InputAction.Back) { _note.IsVisible = false; return true; }
            return true;
        }
        switch (a)
        {
            case InputAction.Up or InputAction.Down or InputAction.Left or InputAction.Right:
                double step = 1;
                _map.CursorX = Math.Clamp(_map.CursorX + (a == InputAction.Right ? step : a == InputAction.Left ? -step : 0), 0, GalaxyMap.Width - 1);
                _map.CursorY = Math.Clamp(_map.CursorY + (a == InputAction.Down ? step : a == InputAction.Up ? -step : 0), 0, GalaxyMap.Height - 1);
                return true;
            case InputAction.ZoomIn: _map.Zoom = Math.Min(4, _map.Zoom * 1.5); return true;
            case InputAction.ZoomOut: _map.Zoom = Math.Max(1, _map.Zoom / 1.5); return true;
            case InputAction.Confirm:
                if (s.State.Location.Mode != GameMode.Hyperspace) { s.Say("Courses are set in hyperspace. Leave the system first."); return true; }
                var r = s.Navigation.SetCourse((int)Math.Round(_map.CursorX), (int)Math.Round(_map.CursorY));
                s.Say(r.Message);
                if (r.Ok) { _screen.CloseOverlay(); _screen.SetHelm(true); }
                return true;
            case InputAction.StarMap: _screen.CloseOverlay(); return true;
            default:
                return false;
        }
    }

    /// <summary>Waypoint keys (W / X) arrive as raw keys since they are not bound actions in all layouts.</summary>
    public bool RawKey(string key)
    {
        var s = _screen.S;
        if (!s.Policy.Waypoints || _note.IsVisible) return false;
        if (key == "W") { _note.IsVisible = true; _note.Focus(); return true; }
        if (key == "X")
        {
            var near = s.State.Waypoints.OrderBy(w => GalaxyMap.Distance(w.X, w.Y, _map.CursorX, _map.CursorY)).FirstOrDefault();
            if (near is not null && GalaxyMap.Distance(near.X, near.Y, _map.CursorX, _map.CursorY) < 5) s.State.Waypoints.Remove(near);
            return true;
        }
        return false;
    }
}

// ---------------------------------------------------------------------- information panels

public enum InfoKind { Crew, Ship, Cargo, Log }

public sealed class InfoOverlay : StackOverlay
{
    public InfoOverlay(GameScreen screen, InfoKind kind) : base(screen)
    {
        Stack.SetRoot(kind switch
        {
            InfoKind.Crew => CrewPage(),
            InfoKind.Ship => ShipPage(),
            InfoKind.Cargo => CargoPage(),
            _ => LogPage(),
        });
    }

    private MenuPage CrewPage()
    {
        MenuPage page = null!;
        var crew = S.State.Roster;
        var panel = new StarportPanel(S, () => { }, S.Policy.Tooltips);
        page = new MenuPage
        {
            Title = "Crew status",
            Items = () => crew.Count == 0 ? [new MenuEntry("(no crew)", null, false)] : crew.Select(m =>
            {
                var roles = S.State.Assignments.Where(kv => kv.Value == m.Id).Select(kv => StarportPanel.RoleName(kv.Key)).ToList();
                return new MenuEntry($"{m.Name} - {(roles.Count == 0 ? "unassigned" : string.Join("/", roles))}",
                    m.IsDead || S.Skill(CrewRole.Doctor) == 0 || m.Vitality >= 100 || S.State.Location.Mode == GameMode.Starport ? null : () => { var r = S.Crew.Treat(m.Id); Stack.Status(r.Message, !r.Ok); Stack.Refresh(); },
                    true, "Enter: Doctor treats this crew member.", m.IsDead ? "DEAD" : $"{m.Vitality}%",
                    m.IsDead ? Ui.Ui.Bad : m.Vitality < 50 ? Ui.Ui.Highlight : null);
            }),
            Details = () => crew.Count == 0 ? null : panel.CrewCard(crew[Math.Clamp(page.Selected, 0, crew.Count - 1)]),
        };
        return page;
    }

    private MenuPage ShipPage()
    {
        var panel = new StarportPanel(S, () => { }, S.Policy.Tooltips);
        return new MenuPage
        {
            Title = "Ship status",
            Items = () =>
            {
                var ship = S.State.Ship;
                var items = new List<MenuEntry>
                {
                    new("Stardate", null, true, Value: S.State.Clock.WithHour),
                    new("Location", null, true, Value: S.State.LocationSummary()),
                    new("Shields", null, true, Value: ship.Shield == 0 ? "none" : $"Class {ship.Shield} {(ship.ShieldsUp ? "UP" : "down")}"),
                    new("Weapons", null, true, Value: $"Laser {ship.Laser} / Missile {ship.Missile} {(ship.WeaponsArmed ? "ARMED" : "")}"),
                };
                foreach (var (sys, d) in ship.Damage)
                    items.Add(new MenuEntry($"{sys} damage", null, true, "Percent damage equals the percent chance the system fails when used.", $"{d}%", d > 0 ? Ui.Ui.Bad : null));
                return items;
            },
            Details = panel.ShipCard,
        };
    }

    private MenuPage CargoPage()
    {
        MenuPage page = null!;
        page = new MenuPage
        {
            Title = "Cargo",
            Items = () =>
            {
                var items = new List<MenuEntry>();
                var ship = S.State.Ship;
                items.Add(new MenuEntry("Endurium (fuel)", null, true, Value: $"{ship.Endurium:0.0} m³"));
                foreach (var c in ship.Cargo)
                    items.Add(new MenuEntry(c.Name, null, true, Value: c.Kind == CargoKind.Mineral ? $"{c.Quantity:0.#} m³" : c.Kind == CargoKind.Artifact ? (c.Analyzed ? "analysed" : "unanalysed") : $"x{c.Quantity:0}"));
                if (S.State.Surface is { } st)
                {
                    items.Add(new MenuEntry("-- Terrain vehicle hold --", null, false, Value: $"{S.Planets.TvCargoUsed:0.#}/50 m³"));
                    foreach (var c in st.TvCargo) items.Add(new MenuEntry("TV: " + c.Name, null, true, Value: c.Kind == CargoKind.Mineral ? $"{c.Quantity:0.#} m³" : ""));
                }
                return items;
            },
            Details = () =>
            {
                var ship = S.State.Ship;
                var msgs = ship.Cargo.Where(c => c.Kind == CargoKind.Message).ToList();
                var p = Ui.Ui.VStack(3,
                    Ui.Ui.Row("Hold", $"{ShipRules.CargoUsed(ship):0.#}/{ShipRules.CargoCapacity(ship, S.Data.Ship):0} m³", null, 19),
                    Ui.Ui.Row("Pods", $"{ship.CargoPods}/16", null, 19),
                    Ui.Ui.Row("Est. mineral value", Ui.Ui.Money(ship.Cargo.Where(c => c.Kind == CargoKind.Mineral).Sum(c => (int)(c.Quantity * S.Starport.SalePrice(c)))), Ui.Ui.Highlight, 19));
                foreach (var m in msgs) p.Children.Add(Ui.Ui.Prose($"{m.Name}: {S.Data.Message(m.Id).Text}", 14));
                return Ui.Ui.Box(p);
            },
        };
        return page;
    }

    private MenuPage LogPage()
    {
        MenuPage page = null!;
        var entries = S.Story.VisibleLog().Reverse().ToList();
        page = new MenuPage
        {
            Title = S.Policy.CaptainsLog ? "Captain's log" : "Ship's log",
            Items = () => entries.Count == 0 ? [new MenuEntry("(empty)", null, false)] :
                entries.Select(e => new MenuEntry($"{GameClock.FormatDay(e.Hour / 24)} {e.Category}", null, true)),
            Details = () => entries.Count == 0
                ? Ui.Ui.Box(Ui.Ui.Prose(S.Policy.CaptainsLog ? "Discoveries, contacts and artifacts are recorded here automatically." : "Classic mode: only mission events and planets you log (Captain > Log planet) appear here.", 15))
                : Ui.Ui.Box(Ui.Ui.Prose(entries[Math.Clamp(page.Selected, 0, entries.Count - 1)].Text, 17)),
        };
        return page;
    }
}

// ---------------------------------------------------------------------- landing

public sealed class LandingOverlay : Overlay
{
    private readonly GameScreen _screen;
    private readonly PlanetGlobeView _map;

    public LandingOverlay(GameScreen screen)
    {
        _screen = screen;
        _map = new PlanetGlobeView(screen.S) { LandingMap = true };
        var p = screen.S.CurrentPlanet!;
        var target = screen.S.Planets.VisibleSites(p).FirstOrDefault();
        if (target is not null) (_map.CursorX, _map.CursorY) = PlanetSurface.CellFromLatLon(target.Lat, target.Lon);
    }

    public override Control View => _map;

    public override bool Handle(InputAction a)
    {
        switch (a)
        {
            case InputAction.Left: _map.CursorX = PlanetSurface.WrapX(_map.CursorX - 1); return true;
            case InputAction.Right: _map.CursorX = PlanetSurface.WrapX(_map.CursorX + 1); return true;
            case InputAction.Up: _map.CursorY = Math.Max(0, _map.CursorY - 1); return true;
            case InputAction.Down: _map.CursorY = Math.Min(PlanetSurface.Height - 1, _map.CursorY + 1); return true;
            case InputAction.Confirm:
                var (lat, lon) = PlanetSurface.LatLonFromCell(_map.CursorX, _map.CursorY);
                _screen.CloseOverlay();
                _screen.Do(_screen.S.Planets.Land(lat, lon));
                return true;
            default: return false;
        }
    }
}

// ---------------------------------------------------------------------- communications

public sealed class CommsOverlay : Overlay
{
    private readonly GameScreen _screen;
    private readonly MenuList _menu = new() { ItemSize = 20 };
    private readonly StackPanel _transcript = new() { Spacing = 6 };
    private readonly ScrollViewer _scroll;
    private readonly Grid _view;
    private readonly TextBlock _attitude = Ui.Ui.Label("", 18, Ui.Ui.Accent);
    private readonly Queue<(TextBlock Block, string Full)> _reveal = new();
    private double _revealAcc;
    private enum Mode { Hail, Talk, Ask, Posture, Trade }
    private Mode _mode;

    private GameSession S => _screen.S;

    public CommsOverlay(GameScreen screen)
    {
        _screen = screen;
        var race = S.Data.Race(S.State.Encounter!.RaceId);
        _scroll = new ScrollViewer { Content = _transcript };
        var portrait = new Border { Child = new AlienPortraitView(race.Id), Height = 220, BorderBrush = Ui.Ui.Border, BorderThickness = new Thickness(2) };
        var left = new DockPanel();
        var known = S.State.Relation(race.Id).Contacted || S.Navigation.LongRangeSensors;
        var header = Ui.Ui.VStack(2, Ui.Ui.Heading(known ? race.Name.ToUpperInvariant() : "UNKNOWN ALIENS", 13), _attitude);
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(portrait, Dock.Top);
        left.Children.Add(header);
        left.Children.Add(portrait);
        left.Children.Add(Ui.Ui.Box(_menu));
        var right = Ui.Ui.Box(_scroll);
        right.Margin = new Thickness(10, 0, 0, 0);
        _view = new Grid { ColumnDefinitions = new ColumnDefinitions("320,*") };
        _view.Children.Add(left);
        Grid.SetColumn(right, 1);
        _view.Children.Add(right);
        _mode = S.State.Encounter!.Talking ? Mode.Talk : Mode.Hail;
        foreach (var t in S.State.Encounter.Transcript.TakeLast(12)) AddLine(t, t.StartsWith("You:"), instant: true);
        if (!S.Comms.CanTalk) AddLine("There is no response to our hails.", false, instant: true);
        Refresh();
    }

    public override Control View => _view;
    public override bool PausesTime => false;

    private void AddLine(string text, bool player, bool instant = false)
    {
        var tb = new TextBlock
        {
            FontFamily = player ? Ui.Ui.TextFont : Ui.Ui.TextFont, FontSize = Ui.Ui.S(17), TextWrapping = TextWrapping.Wrap,
            Foreground = player ? Ui.Ui.Accent : Ui.Ui.Bright, FontStyle = player ? FontStyle.Italic : FontStyle.Normal,
        };
        _transcript.Children.Add(tb);
        int speed = AppServices.Current.Settings.TextSpeed;
        if (instant || speed <= 0 || player) tb.Text = text;
        else { tb.Text = ""; _reveal.Enqueue((tb, text)); }
        _scroll.ScrollToEnd();
    }

    public override void Tick(double dt)
    {
        int speed = AppServices.Current.Settings.TextSpeed;
        if (_reveal.Count == 0 || speed <= 0) return;
        _revealAcc += dt * speed;
        while (_revealAcc >= 1 && _reveal.Count > 0)
        {
            _revealAcc -= 1;
            var (tb, full) = _reveal.Peek();
            tb.Text = full[..Math.Min(full.Length, (tb.Text?.Length ?? 0) + 1)];
            if (tb.Text.Length >= full.Length) _reveal.Dequeue();
        }
        _scroll.ScrollToEnd();
    }

    private void Show(IEnumerable<CommLine> lines)
    {
        var race = S.Data.Race(S.State.Encounter?.RaceId ?? "nomad");
        foreach (var l in lines) AddLine(l.FromPlayer ? "You: " + l.Text : (l.TranslatedFraction < 1 && l.TranslatedFraction > 0 ? $"{race.Name} ({l.TranslatedFraction:P0} translated): " : race.Name + ": ") + l.Text, l.FromPlayer);
        if (S.State.Encounter is { } e && !e.Talking && _mode != Mode.Hail) _mode = Mode.Hail;
        Refresh();
    }

    private void Refresh()
    {
        var e = S.State.Encounter;
        if (e is null) { _menu.SetItems([new MenuEntry("Close", _screen.CloseOverlay)]); return; }
        _attitude.Text = $"Attitude: {S.Comms.AttitudeLabel}   Posture: {e.Posture}   Translation: {S.Comms.TranslationFraction():P0}";
        bool tips = S.Policy.Tooltips;
        string? H(string h) => tips ? h : null;
        var items = new List<MenuEntry>();
        switch (_mode)
        {
            case Mode.Hail:
                string verb = e.AlienHailedFirst && !e.Talking ? "Respond" : "Hail";
                items.Add(new MenuEntry($"{verb}: friendly", () => { Show(S.Comms.Hail(Posture.Friendly)); _mode = e.Talking ? Mode.Talk : Mode.Hail; Refresh(); }, S.Comms.CanTalk));
                items.Add(new MenuEntry($"{verb}: hostile", () => { Show(S.Comms.Hail(Posture.Hostile)); _mode = e.Talking ? Mode.Talk : Mode.Hail; Refresh(); }, S.Comms.CanTalk));
                items.Add(new MenuEntry($"{verb}: obsequious", () => { Show(S.Comms.Hail(Posture.Obsequious)); _mode = e.Talking ? Mode.Talk : Mode.Hail; Refresh(); }, S.Comms.CanTalk,
                    H("Grovelling. Some races respond best to it; others despise it.")));
                items.Add(new MenuEntry("Close channel", _screen.CloseOverlay));
                break;
            case Mode.Talk:
                items.Add(new MenuEntry("Make a statement", () => Show(S.Comms.Statement()), Hint: H("Statements affect the aliens' attitude more than questions.")));
                items.Add(new MenuEntry("Ask a question...", () => { _mode = Mode.Ask; Refresh(); }));
                items.Add(new MenuEntry("Change posture...", () => { _mode = Mode.Posture; Refresh(); }));
                var offers = S.Comms.Offers();
                if (offers.Count > 0) items.Add(new MenuEntry("Trade / special...", () => { _mode = Mode.Trade; Refresh(); }));
                items.Add(new MenuEntry("Terminate", () => { Show(S.Comms.Terminate()); _screen.CloseOverlay(); }));
                break;
            case Mode.Ask:
                foreach (var (topic, label) in CommsService.TopicLabels)
                    items.Add(new MenuEntry(label, () => { Show(S.Comms.Ask(topic)); _mode = S.State.Encounter?.Talking == true ? Mode.Talk : Mode.Hail; Refresh(); }));
                items.Add(new MenuEntry("< Back", () => { _mode = Mode.Talk; Refresh(); }));
                break;
            case Mode.Posture:
                foreach (var p in Enum.GetValues<Posture>())
                    items.Add(new MenuEntry(p.ToString(), () => { Show(S.Comms.ChangePosture(p)); _mode = Mode.Talk; Refresh(); }));
                break;
            case Mode.Trade:
                foreach (var o in S.Comms.Offers())
                    items.Add(new MenuEntry(o.Description, () => { Show(S.Comms.AcceptOffer(o.Id)); _mode = Mode.Talk; Refresh(); }));
                items.Add(new MenuEntry("< Back", () => { _mode = Mode.Talk; Refresh(); }));
                break;
        }
        _menu.SetItems(items, keepSelection: false);
    }

    public override bool Handle(InputAction a)
    {
        if (a == InputAction.Back && _mode is Mode.Ask or Mode.Posture or Mode.Trade) { _mode = Mode.Talk; Refresh(); return true; }
        if (a == InputAction.Back) return false;
        if (S.State.Encounter is null) { _screen.CloseOverlay(); return true; }
        return _menu.Handle(a);
    }
}
