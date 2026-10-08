using Avalonia;
using Avalonia.Controls;
using AVAFlight.Avalonia.Ui;
using AVAFlight.Infrastructure.Audio;
using AVAFlight.Infrastructure.Input;

namespace AVAFlight.Avalonia.Screens;

/// <summary>Audio, display, accessibility, preset and control settings. Left/Right adjust values.</summary>
public sealed class SettingsScreen : Screen
{
    private readonly MenuList _menu = new() { ItemSize = 22 };
    private readonly MenuList _bindings = new() { ItemSize = 18 };
    private readonly TextBlock _status = Ui.Ui.Label("", 18, Ui.Ui.Highlight);
    private readonly Screen? _returnTo;
    private InputAction? _listening;
    private bool _inBindings;

    private static readonly InputAction[] Rebindable =
    [
        InputAction.Up, InputAction.Down, InputAction.Left, InputAction.Right, InputAction.Confirm, InputAction.Back,
        InputAction.Pause, InputAction.StarMap, InputAction.CrewStatus, InputAction.ShipStatus, InputAction.Cargo,
        InputAction.CaptainsLog, InputAction.Fire, InputAction.Hail, InputAction.Scan, InputAction.ZoomIn, InputAction.ZoomOut,
        InputAction.NextTab, InputAction.QuickSave,
    ];

    public SettingsScreen(Screen? returnTo = null)
    {
        _returnTo = returnTo;
        Refresh();
        var info = Ui.Ui.Label($"Audio: {App.Audio.Status}.  Gamepad: {App.Gamepad?.Status ?? "disabled"}.", 16, Ui.Ui.Dim);
        var help = Ui.Ui.Label("Up/Down select   Left/Right adjust   Enter toggle   Tab: key bindings   Esc: back", 18, Ui.Ui.Dim);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 24 };
        var left = Ui.Ui.Box(_menu);
        var right = Ui.Ui.Box(new DockPanel
        {
            Children = { DockTop(Ui.Ui.Label("KEY BINDINGS (Enter to rebind)", 18, Ui.Ui.Accent)), _bindings },
        });
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        Content = new Border
        {
            Padding = new Thickness(48, 32),
            Child = new DockPanel
            {
                Children =
                {
                    DockTop(Ui.Ui.Heading("SETTINGS", 20)),
                    DockBottom(help), DockBottom(info), DockBottom(_status),
                    grid,
                },
            },
        };
    }

    private static Control DockTop(Control c) { DockPanel.SetDock(c, Dock.Top); return c; }
    private static Control DockBottom(Control c) { DockPanel.SetDock(c, Dock.Bottom); return c; }

    private void Refresh()
    {
        var s = App.Settings;
        _menu.SetItems(
        [
            new MenuEntry("Music volume", () => Adjust(0, 1), Value: Pct(s.MusicVolume)),
            new MenuEntry("Effects volume", () => Adjust(1, 1), Value: Pct(s.EffectsVolume)),
            new MenuEntry("Audio enabled", () => Adjust(2, 1), Value: s.AudioEnabled ? "On" : "Off (restart)", Hint: "Takes effect on next launch."),
            new MenuEntry("Fullscreen (borderless)", () => Adjust(3, 1), Value: s.Fullscreen ? "On" : "Off"),
            new MenuEntry("Font scale", () => Adjust(4, 1), Value: $"{s.FontScale:0.0}x"),
            new MenuEntry("Dialogue text speed", () => Adjust(5, 1), Value: s.TextSpeed == 0 ? "Instant" : $"{s.TextSpeed} cps"),
            new MenuEntry("High contrast", () => Adjust(6, 1), Value: s.HighContrast ? "On" : "Off"),
            new MenuEntry("Beginner hints", () => Adjust(7, 1), Value: s.ShowHints ? "On" : "Off (dismissed)"),
            new MenuEntry("Default preset", () => Adjust(8, 1), Value: s.DefaultPreset,
                Hint: "Classic: 1986 DOS behaviour. Modern: same rules with quality-of-life additions."),
            new MenuEntry("Gamepad deadzone", () => Adjust(9, 1), Value: Pct(s.GamepadDeadzone)),
            new MenuEntry("Reset key bindings", () => { s.Bindings = null; App.Input.Rebind(DefaultBindings.Create()); App.SaveSettings(); Refresh(); _status.Text = "Bindings reset."; }),
            new MenuEntry("Back", Back),
        ]);
        _menu.Active = !_inBindings;
        _bindings.Active = _inBindings;
        _bindings.SetItems(Rebindable.Select(a => new MenuEntry(a.ToString(), () => StartListening(a),
            Value: _listening == a ? "press a key..." : string.Join(", ", App.Input.Bindings.Where(kv => kv.Value == a).Select(kv => kv.Key.Control)))));
    }

    private static string Pct(double v) => $"{v * 100:0}%";

    private void Adjust(int index, int dir)
    {
        var s = App.Settings;
        switch (index)
        {
            case 0: s.MusicVolume = Math.Round(Math.Clamp(s.MusicVolume + 0.1 * dir, 0, 1), 1); break;
            case 1: s.EffectsVolume = Math.Round(Math.Clamp(s.EffectsVolume + 0.1 * dir, 0, 1), 1); App.Audio.Play(SoundEffect.Laser); break;
            case 2: s.AudioEnabled = !s.AudioEnabled; break;
            case 3: s.Fullscreen = !s.Fullscreen; Host?.SetFullscreen(s.Fullscreen); break;
            case 4: s.FontScale = Math.Round(Math.Clamp(s.FontScale + 0.1 * dir, 0.8, 1.6), 1); Ui.Ui.FontScale = s.FontScale; break;
            case 5:
                int[] speeds = [0, 30, 60, 120, 240];
                int i = Array.IndexOf(speeds, s.TextSpeed);
                s.TextSpeed = speeds[((i < 0 ? 2 : i) + dir + speeds.Length) % speeds.Length];
                break;
            case 6: s.HighContrast = !s.HighContrast; Ui.Ui.HighContrast = s.HighContrast; break;
            case 7: s.ShowHints = !s.ShowHints; break;
            case 8: s.DefaultPreset = s.DefaultPreset == "Modern" ? "Classic" : "Modern"; break;
            case 9: s.GamepadDeadzone = Math.Round(Math.Clamp(s.GamepadDeadzone + 0.05 * dir, 0.05, 0.6), 2); break;
        }
        App.SaveSettings();
        if (index is 4 or 6) { Host?.Navigate(new SettingsScreen(_returnTo) { }); return; } // rebuild with new font/contrast
        Refresh();
    }

    private void StartListening(InputAction a)
    {
        _listening = a;
        _status.Text = $"Press a key for {a} (Esc cancels).";
        Refresh();
    }

    public override bool CaptureRawKey(string keyName)
    {
        if (_listening is not { } a) return false;
        _listening = null;
        if (keyName == "Escape") { _status.Text = "Cancelled."; Refresh(); return true; }
        var list = (App.Settings.Bindings ?? DefaultBindings.Create()).Where(b => !(b.Device == InputDevice.Keyboard && (b.Control == keyName || b.Action == a))).ToList();
        list.Add(new InputBinding(InputDevice.Keyboard, keyName, a));
        App.Settings.Bindings = list;
        App.Input.Rebind(list);
        App.SaveSettings();
        _status.Text = $"{a} bound to {keyName}.";
        Refresh();
        return true;
    }

    private void Back()
    {
        if (_returnTo is not null) Host!.Navigate(_returnTo);
        else Host!.Navigate(new TitleScreen());
    }

    public override bool HandleAction(InputAction action)
    {
        if (action == InputAction.Back) { Back(); return true; }
        if (action is InputAction.NextTab or InputAction.PrevTab) { _inBindings = !_inBindings; Refresh(); return true; }
        if (_inBindings) return _bindings.Handle(action);
        if (action is InputAction.Left or InputAction.Right)
        {
            if (_menu.SelectedIndex <= 9) Adjust(_menu.SelectedIndex, action == InputAction.Right ? 1 : -1);
            return true;
        }
        return _menu.Handle(action);
    }
}
