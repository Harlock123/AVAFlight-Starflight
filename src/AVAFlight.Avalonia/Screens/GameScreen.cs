using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AVAFlight.Avalonia.Panels;
using AVAFlight.Avalonia.Rendering;
using AVAFlight.Avalonia.Ui;
using AVAFlight.Core.Engine;
using AVAFlight.Core.Model;
using AVAFlight.Infrastructure.Audio;
using AVAFlight.Infrastructure.Input;

namespace AVAFlight.Avalonia.Screens;

/// <summary>
/// The in-game console, laid out like the original: main view (left), auxiliary status view and
/// role-based control panel (right), and the text window (bottom). Overlays (star map, cargo, crew,
/// log, comms, landing map, options) open over the main view.
/// </summary>
public sealed partial class GameScreen : Screen
{
    public GameSession S { get; }
    private string? _slot;

    private readonly ContentControl _main = new();
    private readonly Grid _mainHost = new();
    private readonly Border _overlayHost = new() { IsVisible = false, Background = new SolidColorBrush(Color.FromRgb(0, 0, 0x10)), Padding = new Thickness(12) };
    private readonly StackPanel _status = new() { Spacing = 0 };
    private readonly MenuList _controls = new() { ItemSize = 20 };
    private readonly TextBlock _controlsTitle = Ui.Ui.Label("", 18, Ui.Ui.Accent);
    private readonly StackPanel _text = new() { Spacing = 0 };
    private readonly TextBlock _topBar = Ui.Ui.Label("", 20, Ui.Ui.Bright);
    private readonly TextBlock _focusLabel = Ui.Ui.Label("", 18, Ui.Ui.Highlight);
    private readonly TextBlock _hint = Ui.Ui.Label("", 17, Ui.Ui.Highlight);

    private GameMode _shownMode = (GameMode)(-1);
    private bool _helm;
    private CrewRole? _role;
    private bool _tvRole;
    private Overlay? _overlay;
    private double _statusTimer, _stepTimer, _endTimer = -1;
    private bool _victoryShown;
    private int _messageCount = -1;
    private StarportPanel? _starport;
    private HyperspaceView? _hyper;
    private PlanetGlobeView? _globe;

    public bool Helm => _helm;
    internal StarportPanel? StarportView => _starport;
    /// <summary>Forces the main view to match the current mode (normally done each frame).</summary>
    internal void SyncView() => EnsureMainView();
    public Overlay? CurrentOverlay => _overlay;

    public override MusicCue Music => MusicFor(S.State.Location.Mode);

    public GameScreen(GameSession session, string? slotName)
    {
        S = session;
        _slot = slotName;

        var top = new Border
        {
            Background = Ui.Ui.Panel, BorderBrush = Ui.Ui.Border, BorderThickness = new Thickness(0, 0, 0, 2), Padding = new Thickness(10, 4),
            Child = new DockPanel { Children = { DockRight(_focusLabel), _topBar } },
        };
        _mainHost.Children.Add(_main);
        _mainHost.Children.Add(_overlayHost);
        _hint.Background = new SolidColorBrush(Color.FromArgb(0xC0, 0, 0, 0));
        _hint.VerticalAlignment = VerticalAlignment.Top;
        _hint.HorizontalAlignment = HorizontalAlignment.Right;
        _hint.MaxWidth = 560;
        _hint.Margin = new Thickness(0, 40, 6, 0);
        _hint.TextWrapping = TextWrapping.Wrap;
        _hint.Padding = new Thickness(8, 2);
        _mainHost.Children.Add(_hint);

        var mainBorder = new Border { BorderBrush = Ui.Ui.Border, BorderThickness = new Thickness(2), Child = _mainHost, Margin = new Thickness(4) };
        var aux = Ui.Ui.Box(new ScrollViewer { Content = _status }, 6);
        aux.Margin = new Thickness(0, 4, 4, 2);
        var ctl = Ui.Ui.Box(new DockPanel { Children = { DockTop(_controlsTitle), _controls } }, 6);
        ctl.Margin = new Thickness(0, 2, 4, 4);
        var right = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Width = 340 };
        right.Children.Add(aux);
        Grid.SetRow(ctl, 1);
        right.Children.Add(ctl);

        var textWin = Ui.Ui.Box(_text, 6);
        textWin.Margin = new Thickness(4, 0, 4, 4);
        textWin.Height = 132;

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumnSpan(top, 2);
        root.Children.Add(top);
        Grid.SetRow(mainBorder, 1);
        root.Children.Add(mainBorder);
        Grid.SetRow(right, 1); Grid.SetColumn(right, 1);
        root.Children.Add(right);
        Grid.SetRow(textWin, 2); Grid.SetColumnSpan(textWin, 2);
        root.Children.Add(textWin);
        Content = root;
        Background = Brushes.Black;

        if (S.State.Clock.Hours == 0 && S.Messages.Count == 0)
            S.Say("Welcome to Starport, Captain. Visit Operations for your briefing, then outfit your ship and crew.");
        RefreshAll();
    }

    private static Control DockRight(Control c) { DockPanel.SetDock(c, Dock.Right); return c; }
    private static Control DockTop(Control c) { DockPanel.SetDock(c, Dock.Top); return c; }

    public static MusicCue MusicFor(GameMode m) => m switch
    {
        GameMode.Starport => MusicCue.Starport,
        GameMode.Hyperspace => MusicCue.Hyperspace,
        GameMode.System or GameMode.Orbit => MusicCue.System,
        GameMode.Surface => MusicCue.Planet,
        GameMode.Encounter => MusicCue.Combat,
        GameMode.GameOver => MusicCue.GameOver,
        _ => MusicCue.Title,
    };

    // ------------------------------------------------------------------ view switching

    private void EnsureMainView()
    {
        var mode = S.State.Location.Mode;
        if (mode == _shownMode) return;
        _shownMode = mode;
        _starport = null; _hyper = null; _globe = null;
        _role = null; _tvRole = mode == GameMode.Surface;
        CloseOverlay();
        switch (mode)
        {
            case GameMode.Starport:
                _starport = new StarportPanel(S, RefreshAll, S.Policy.Tooltips);
                _main.Content = _starport;
                _helm = false;
                break;
            case GameMode.Hyperspace:
                _main.Content = _hyper = new HyperspaceView(S);
                _helm = true;
                break;
            case GameMode.System:
                _main.Content = new SystemView(S);
                _helm = true;
                break;
            case GameMode.Orbit:
                _globe = new PlanetGlobeView(S);
                var survey = new SurveyPanel(S);
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*") };
                g.Children.Add(_globe);
                Grid.SetColumn(survey, 1);
                g.Children.Add(survey);
                _main.Content = g;
                _helm = false;
                _role = CrewRole.Science;
                break;
            case GameMode.Surface:
                _main.Content = new TerrainView(S);
                _helm = true;
                break;
            case GameMode.Encounter:
                _main.Content = new CombatView(S);
                _helm = true;
                if (S.State.Encounter is { AlienHailedFirst: true } e && S.Comms.CanTalk) _role = CrewRole.Communications;
                break;
            case GameMode.GameOver:
                _main.Content = Ui.Ui.Label("", 20);
                break;
        }
        App.Audio.PlayMusic(MusicFor(mode));
        RefreshAll();
    }

    // ------------------------------------------------------------------ frame

    public override void Tick(double dt)
    {
        EnsureMainView();
        foreach (var ev in S.DrainEvents())
        {
            if (ev.Sound is not null) App.PlayCue(ev.Sound);
            if (ev.Kind == EventKind.Victory && !_victoryShown) { _victoryShown = true; _endTimer = 2.5; }
            if (ev.Kind == EventKind.GameOver) _endTimer = 2.0;
        }
        if (_endTimer >= 0)
        {
            _endTimer -= dt;
            if (_endTimer < 0) { Host?.Navigate(new EndScreen(S, victory: S.State.Won && !S.IsOver, this)); return; }
        }
        bool paused = _overlay is { PausesTime: true };
        _overlay?.Tick(dt);
        if (!paused && !S.IsOver) SimulationTick(dt);
        if (S.State.Location.Mode == GameMode.Encounter && S.State.Encounter is { } enc)
            App.Audio.PlayMusic(enc.Hostile && !enc.Surrendered ? MusicCue.Combat : MusicCue.Comms);

        _statusTimer += dt;
        if (_statusTimer > 0.25) { _statusTimer = 0; RefreshStatus(); }
        if (S.Messages.Count != _messageCount) RefreshText();
    }

    private void SimulationTick(double dt)
    {
        var mode = S.State.Location.Mode;
        var (dx, dy) = _helm ? HeldInput.Direction() : (0, 0);
        switch (mode)
        {
            case GameMode.Hyperspace:
            case GameMode.System:
                S.Navigation.Fly(dx, dy, dt);
                break;
            case GameMode.Surface:
                _stepTimer -= dt;
                if (_helm && (dx != 0 || dy != 0) && _stepTimer <= 0)
                {
                    _stepTimer = 0.14;
                    var r = S.Planets.MoveTv(Math.Sign(Math.Round(dx)), Math.Sign(Math.Round(dy)));
                    if (!r.Ok && !string.IsNullOrEmpty(r.Message)) Flash(r.Message);
                }
                break;
            case GameMode.Encounter:
                var e = S.State.Encounter;
                if (e is null) break;
                bool talking = _overlay is CommsOverlay && !(e.Hostile && !e.Surrendered);
                if (!talking)
                {
                    int turn = _helm ? (HeldInput.IsHeld(InputAction.Left) ? -1 : 0) + (HeldInput.IsHeld(InputAction.Right) ? 1 : 0) : 0;
                    if (_helm && App.Gamepad is { } pad && Math.Abs(pad.LeftStick.X) > 0.3) turn = Math.Sign(pad.LeftStick.X);
                    bool thrust = _helm && (HeldInput.IsHeld(InputAction.Up) || (App.Gamepad?.LeftStick.Y ?? 0) < -0.3);
                    bool fire = _helm && (HeldInput.IsHeld(InputAction.Fire) || HeldInput.IsHeld(InputAction.Confirm));
                    S.Combat.Tick(new CombatInput(turn, thrust, fire), dt);
                }
                break;
        }
    }

    private string? _flash;
    private double _flashUntil;
    private void Flash(string text)
    {
        if (_flash == text && Environment.TickCount64 < _flashUntil) return;
        _flash = text;
        _flashUntil = Environment.TickCount64 + 1500;
        S.Say(text);
    }

    // ------------------------------------------------------------------ input

    public override bool CapturesText => (_overlay?.CapturesText ?? false) || (_starport?.IsTyping ?? false);

    public override bool CaptureRawKey(string keyName) => _overlay is StarMapOverlay map && map.RawKey(keyName);

    public override bool HandleAction(InputAction a)
    {
        if (S.IsOver) return true;
        if (_overlay is not null)
        {
            if (_overlay.Handle(a)) { RefreshAll(); return true; }
            if (a == InputAction.Back || (a == InputAction.Pause && _overlay is PauseOverlay)) { CloseOverlay(); return true; }
            return true;
        }
        if (Shortcut(a)) return true;
        var mode = S.State.Location.Mode;
        if (mode == GameMode.Starport && _starport is not null)
        {
            if (a == InputAction.Back && _starport.Stack.Depth <= 1) { OpenOverlay(new PauseOverlay(this)); return true; }
            _starport.Handle(a);
            RefreshStatus();
            return true;
        }
        if (_helm)
        {
            if (mode == GameMode.Encounter && a is InputAction.Confirm or InputAction.Fire) return true; // fire handled as held input
            if (a is InputAction.Back || (a == InputAction.Confirm && mode != GameMode.Encounter)) { SetHelm(false); return true; }
            return true; // directions are consumed by the helm (held input)
        }
        if (a == InputAction.Back)
        {
            if (_role is not null || (_tvRole && mode != GameMode.Surface)) { _role = null; _tvRole = false; RefreshControls(); return true; }
            if (mode == GameMode.Surface && _tvRole) { _tvRole = false; RefreshControls(); return true; }
            if (mode == GameMode.Surface) { _tvRole = true; RefreshControls(); return true; }
            OpenOverlay(new PauseOverlay(this));
            return true;
        }
        _controls.Handle(a);
        return true;
    }

    private bool Shortcut(InputAction a)
    {
        var mode = S.State.Location.Mode;
        switch (a)
        {
            case InputAction.Pause:
                if (mode == GameMode.Encounter && S.State.Encounter is { Hostile: true }) { S.Say("Real-time combat cannot be paused."); return true; }
                if (!S.Policy.PauseAnywhere) { S.Say("Classic mode: open the options menu from the control panel (Esc)."); return true; }
                OpenOverlay(new PauseOverlay(this));
                return true;
            case InputAction.StarMap when mode is GameMode.Hyperspace or GameMode.System or GameMode.Orbit or GameMode.Starport:
                OpenOverlay(new StarMapOverlay(this)); return true;
            case InputAction.CrewStatus: OpenOverlay(new InfoOverlay(this, InfoKind.Crew)); return true;
            case InputAction.ShipStatus: OpenOverlay(new InfoOverlay(this, InfoKind.Ship)); return true;
            case InputAction.Cargo: OpenOverlay(new InfoOverlay(this, InfoKind.Cargo)); return true;
            case InputAction.CaptainsLog: OpenOverlay(new InfoOverlay(this, InfoKind.Log)); return true;
            case InputAction.Hail when mode == GameMode.Encounter: OpenComms(); return true;
            case InputAction.Scan when mode == GameMode.Orbit: { var r = S.Planets.Sensors(); if (r.Ok) SurveyPanel.Cache[S.CurrentPlanet!.Id] = (r.Value, SurveyPanel.Cache.GetValueOrDefault(S.CurrentPlanet!.Id).A); Do(new ActionResult(r.Ok, r.Message)); return true; }
            case InputAction.ZoomIn when _hyper is not null: _hyper.Zoom = Math.Min(3, _hyper.Zoom * 1.25); return true;
            case InputAction.ZoomOut when _hyper is not null: _hyper.Zoom = Math.Max(0.4, _hyper.Zoom / 1.25); return true;
            case InputAction.QuickSave: QuickSave(); return true;
            case InputAction.Shields when mode != GameMode.Starport: Do(S.Combat.ToggleShields()); return true;
            case InputAction.Weapons when mode != GameMode.Starport: Do(S.Combat.ToggleWeapons()); return true;
            default: return false;
        }
    }

    public void SetHelm(bool on)
    {
        _helm = on;
        HeldInput.Clear();
        RefreshControls();
    }

    // ------------------------------------------------------------------ overlays

    public void OpenOverlay(Overlay o)
    {
        _overlay = o;
        _overlayHost.Child = o.View;
        _overlayHost.IsVisible = true;
        HeldInput.Clear();
        RefreshControls();
        RefreshStatus();
    }

    public void CloseOverlay()
    {
        _overlay?.Closed();
        _overlay = null;
        _overlayHost.Child = null;
        _overlayHost.IsVisible = false;
        RefreshAll();
    }

    public void OpenComms()
    {
        if (S.State.Encounter is null) { S.Say("There is no one to hail."); return; }
        OpenOverlay(new CommsOverlay(this));
    }

    public void Do(ActionResult r)
    {
        if (!string.IsNullOrEmpty(r.Message)) S.Say(r.Message);
        if (!r.Ok && !string.IsNullOrEmpty(r.Message)) App.Audio.Play(SoundEffect.Error);
        RefreshAll();
    }

    // ------------------------------------------------------------------ saving

    public string DefaultSlot => S.State.Preset == GamePreset.Classic
        ? "classic-" + (string.IsNullOrEmpty(S.State.Ship.Name) ? "game" : S.State.Ship.Name.ToLowerInvariant())
        : _slot ?? "quicksave";

    public bool CanSave => S.State.Location.Mode != GameMode.Encounter && !S.IsOver;

    public void SaveTo(string slot, string display)
    {
        if (!CanSave) { S.Say("You cannot save during an encounter."); return; }
        try
        {
            App.Saves.Save(slot, display, S.Snapshot());
            _slot = slot;
            S.Say($"Game saved ({display}).");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            S.Say("Save failed: " + e.Message);
        }
    }

    private void QuickSave() => SaveTo(DefaultSlot, S.State.Preset == GamePreset.Classic ? "ISS " + S.State.Ship.Name : (_slot ?? "Quick save"));

    // ------------------------------------------------------------------ refresh

    public void RefreshAll()
    {
        RefreshStatus();
        RefreshControls();
        RefreshText();
    }

    private void RefreshText()
    {
        _messageCount = S.Messages.Count;
        _text.Children.Clear();
        foreach (var m in S.Messages.TakeLast(5))
            _text.Children.Add(new TextBlock { Text = m, FontFamily = Ui.Ui.DataFont, FontSize = Ui.Ui.S(20), Foreground = Ui.Ui.Bright, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis });
    }

    private void RefreshStatus()
    {
        var st = S.State;
        var ship = st.Ship;
        string mode = st.Location.Mode switch
        {
            GameMode.Starport => "STARPORT", GameMode.Hyperspace => "HYPERSPACE", GameMode.System => "STAR SYSTEM",
            GameMode.Orbit => "ORBIT", GameMode.Surface => "PLANET SURFACE", GameMode.Encounter => "ENCOUNTER", _ => "",
        };
        _topBar.Text = $"ISS {(string.IsNullOrEmpty(ship.Name) ? "(UNNAMED)" : ship.Name)}   {st.Clock.WithHour}   {mode}   {Ui.Ui.Money(st.Credits)}   [{st.Preset}]";
        _focusLabel.Text = _overlay is not null ? "" : st.Location.Mode == GameMode.Starport ? "" : _helm ? "HELM (Esc: menu)" : "MENU";

        _status.Children.Clear();
        bool tips = S.Policy.Tooltips;
        string? T(string tip) => tips ? tip : null;
        var range = S.Navigation.Range;
        bool lowFuel = S.Policy.FuelWarningAndRange && ship.Endurium < S.Navigation.FuelFor(30);
        _status.Children.Add(Ui.Ui.Row("Endurium", $"{ship.Endurium:0.0} m³", lowFuel ? Ui.Ui.Bad : Ui.Ui.Bright, 19,
            T("Endurium is both fuel and a trade good. Hyperspace use: 0.48 (class 1) to 0.16 (class 5) m³ per coordinate.")));
        if (S.Policy.FuelWarningAndRange)
            _status.Children.Add(Ui.Ui.Row("Range", $"{range:0} coords{(lowFuel ? " LOW!" : "")}", lowFuel ? Ui.Ui.Bad : Ui.Ui.Good, 19, T("Estimated hyperspace range on current fuel.")));
        _status.Children.Add(Ui.Ui.Row("Hull", $"{ship.HullPoints}/{ShipRules.BaseHull}", ship.HullPoints < 100 ? Ui.Ui.Bad : Ui.Ui.Bright, 19));
        if (ship.Armor > 0) _status.Children.Add(Ui.Ui.Row("Armour", $"{ship.ArmorPoints}/{ShipRules.MaxArmorPoints(ship, S.Data.Ship)}", null, 19));
        if (ship.Shield > 0) _status.Children.Add(Ui.Ui.Row("Shields", $"{(ship.ShieldsUp ? "UP" : "down")} {ship.ShieldPoints}", ship.ShieldsUp ? Ui.Ui.Good : Ui.Ui.Dim, 19, T("Raised shields use 0.1 m³ endurium per hour and read as hostile to aliens.")));
        if (ship.Laser + ship.Missile > 0) _status.Children.Add(Ui.Ui.Row("Weapons", ship.WeaponsArmed ? "ARMED" : "unarmed", ship.WeaponsArmed ? Ui.Ui.Bad : Ui.Ui.Dim, 19));
        _status.Children.Add(Ui.Ui.Row("Cargo", $"{ShipRules.CargoUsed(ship):0}/{ShipRules.CargoCapacity(ship, S.Data.Ship):0} m³", null, 19));
        var dmg = ship.Damage.Where(kv => kv.Value > 0).ToList();
        if (dmg.Count > 0) _status.Children.Add(Ui.Ui.Label("Damage: " + string.Join(", ", dmg.Select(kv => $"{kv.Key} {kv.Value}%")), 17, Ui.Ui.Bad));
        if (S.State.Location.Mode is not GameMode.Starport && S.CurrentSystem is { } sys)
            _status.Children.Add(Ui.Ui.Row("Star", $"{sys.X},{sys.Y} class {sys.Class}", null, 19));
        var crewLine = new WrapPanel();
        foreach (CrewRole r in Enum.GetValues<CrewRole>())
        {
            var m = st.Assigned(r);
            var col = m is null ? Ui.Ui.Dim : m.IsDead ? Ui.Ui.Bad : m.Vitality < 50 ? Ui.Ui.Highlight : Ui.Ui.Good;
            var tb = Ui.Ui.Label($"{r.ToString()[..3].ToUpperInvariant()} ", 17, col);
            if (tips && m is not null) ToolTip.SetTip(tb, $"{StarportPanel.RoleName(r)}: {m.Name}, {m.HealthLabel}");
            crewLine.Children.Add(tb);
        }
        _status.Children.Add(crewLine);

        string? hint = App.Settings.ShowHints && S.Policy.Hints ? HintFor(st.Location.Mode) : null;
        _hint.Text = hint ?? "";
        _hint.IsVisible = hint is not null && _overlay is null;
    }

    private string? HintFor(GameMode m) => m switch
    {
        GameMode.Starport => "HINT: Operations > Notices first. Create crew in Personnel, assign posts, name the ship, then Docking Bay.",
        GameMode.Orbit => "HINT: Science > Sensors/Analysis surveys the planet. Captain > Land picks a landing site. Navigator > Leave orbit.",
        GameMode.System => "HINT: Fly with the arrows. Over a planet press Esc, then Navigator > Orbit. Fly past the edge to leave the system.",
        GameMode.Hyperspace => "HINT: Arrows fly the ship. M opens the star map: Enter there sets a course. +/- zoom.",
        GameMode.Surface => "HINT: Arrows drive the vehicle. Esc for the vehicle menu: pick up minerals (yellow), stun and capture life.",
        GameMode.Encounter => "HINT: H to hail. Or raise shields (Navigator) and fight: arrows steer, Space fires. Fly off-screen to flee.",
        _ => null,
    };
}
