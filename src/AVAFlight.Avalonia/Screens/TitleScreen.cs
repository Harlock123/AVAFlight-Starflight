using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using AVAFlight.Avalonia.Rendering;
using AVAFlight.Avalonia.Ui;
using AVAFlight.Core.Engine;
using AVAFlight.Core.Model;
using AVAFlight.Infrastructure.Audio;
using AVAFlight.Infrastructure.Input;
using AVAFlight.Infrastructure.Persistence;

namespace AVAFlight.Avalonia.Screens;

public sealed class TitleScreen : Screen
{
    private readonly MenuList _menu = new() { ItemSize = 24, Width = 420 };
    private readonly TextBlock _status = Ui.Ui.Label("", 18, Ui.Ui.Bad);
    private double _elapsed;
    private bool _smokeDone;

    public override MusicCue Music => MusicCue.Title;

    public TitleScreen()
    {
        var latest = App.Saves.List().FirstOrDefault(s => s.SchemaVersion > 0);
        _menu.SetItems(
        [
            new MenuEntry("New game - Modern", () => NewGame(GamePreset.Modern), Hint: "The original game with quality-of-life additions: captain's log, waypoints, tooltips, fuel range, pause anywhere."),
            new MenuEntry("New game - Classic", () => NewGame(GamePreset.Classic), Hint: "As close to the 1986 DOS original as the evidence allows."),
            new MenuEntry("Continue", latest is null ? null : () => Load(latest.Slot), latest is not null, latest is null ? "No saved games." : $"{latest.DisplayName} - {latest.StarDate}"),
            new MenuEntry("Load game", () => Host!.Navigate(new LoadScreen()), App.Saves.List().Count > 0),
            new MenuEntry("Settings", () => Host!.Navigate(new SettingsScreen())),
            new MenuEntry("Quit", Quit),
        ], keepSelection: false);
        _menu.ShowTooltips = true;

        var title = new TextBlock
        {
            Text = "AVAFLIGHT", FontFamily = Ui.Ui.TitleFont, FontSize = 64, Foreground = Ui.Ui.Accent,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var subtitle = Ui.Ui.Label("A recreation of the 1986 space exploration classic, Starflight", 24, Ui.Ui.Bright);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.TextAlignment = TextAlignment.Center;
        var disclaimer = Ui.Ui.Label("Unofficial fan project. Not affiliated with or endorsed by Electronic Arts or Binary Systems. " +
                                     "All text, art and audio are newly created.", 16, Ui.Ui.Dim);
        disclaimer.HorizontalAlignment = HorizontalAlignment.Center;
        disclaimer.TextAlignment = TextAlignment.Center;
        disclaimer.MaxWidth = 760;

        var center = new StackPanel
        {
            Spacing = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                title, subtitle,
                new Border { Height = 24 },
                new Border { Child = _menu, HorizontalAlignment = HorizontalAlignment.Center, Background = new SolidColorBrush(Color.FromArgb(0xC0, 0, 0, 0x2A)),
                             BorderBrush = Ui.Ui.Border, BorderThickness = new Thickness(2), Padding = new Thickness(12) },
                _status,
                new Border { Height = 24 },
                disclaimer,
            },
        };
        var version = Ui.Ui.Label("v" + typeof(TitleScreen).Assembly.GetName().Version?.ToString(3) + "   F11: fullscreen", 16, Ui.Ui.Dim);
        version.HorizontalAlignment = HorizontalAlignment.Right;
        version.VerticalAlignment = VerticalAlignment.Bottom;
        version.Margin = new Thickness(12);
        Content = new Grid { Children = { new StarfieldView(), center, version } };
    }

    private void NewGame(GamePreset preset)
    {
        var session = GameSession.NewGame(preset);
        Host!.Navigate(new GameScreen(session, slotName: null));
    }

    private void Load(string slot)
    {
        try
        {
            var state = App.Saves.Load(slot);
            Host!.Navigate(new GameScreen(new GameSession(state, AVAFlight.Core.Data.GameData.Default), slot));
        }
        catch (SaveLoadException e)
        {
            _status.Text = e.Message;
        }
    }

    private static void Quit()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) d.Shutdown();
    }

    public override bool HandleAction(InputAction action) => _menu.Handle(action);

    public override void Tick(double dt)
    {
        _elapsed += dt;
        if (Program.SmokeTest && !_smokeDone && _elapsed > 1.5)
        {
            _smokeDone = true;
            Console.WriteLine("AVAFLIGHT_SMOKE_OK: main menu reached");
            if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) d.Shutdown(0);
        }
    }
}
