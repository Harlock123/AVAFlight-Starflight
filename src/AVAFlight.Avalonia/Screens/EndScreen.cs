using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AVAFlight.Avalonia.Rendering;
using AVAFlight.Avalonia.Ui;
using AVAFlight.Core.Engine;
using AVAFlight.Infrastructure.Audio;
using AVAFlight.Infrastructure.Input;

namespace AVAFlight.Avalonia.Screens;

/// <summary>Game-over and victory screens with a short summary of the voyage.</summary>
public sealed class EndScreen : Screen
{
    private readonly MenuList _menu = new() { ItemSize = 22, Width = 420 };
    private readonly bool _victory;

    public override MusicCue Music => _victory ? MusicCue.Victory : MusicCue.GameOver;

    public EndScreen(GameSession s, bool victory, GameScreen? game)
    {
        _victory = victory;
        var st = s.State.Stats;
        string title = victory ? "MISSION ACCOMPLISHED" : "GAME OVER";
        string body = victory
            ? "The Black Egg detonates at the Nexus of Control and the Crystal Planet shatters. Across the galaxy, the stars grow calm.\n\n" +
              "INTERSTEL: Thank you for saving everything we hold dear. A bonus of 500,000 M.U. has been credited to your account."
            : s.State.GameOverReason ?? "The voyage has ended.";
        var items = new List<MenuEntry>();
        if (victory && game is not null) items.Add(new MenuEntry("Continue exploring", () => Host!.Navigate(game)));
        items.Add(new MenuEntry("Return to title", () => Host!.Navigate(new TitleScreen())));
        _menu.SetItems(items);
        var summary = Ui.Ui.VStack(2,
            Ui.Ui.Row("Stardate", s.State.Clock.ToString(), null, 20),
            Ui.Ui.Row("Systems visited", st.SystemsVisited.ToString(), null, 20),
            Ui.Ui.Row("Planets landed", st.PlanetsLanded.ToString(), null, 20),
            Ui.Ui.Row("Minerals collected", $"{st.MineralsCollected:0} m³", null, 20),
            Ui.Ui.Row("Alien races contacted", st.AliensContacted.ToString(), null, 20),
            Ui.Ui.Row("Ships destroyed", st.ShipsDestroyed.ToString(), null, 20),
            Ui.Ui.Row("Credits", Ui.Ui.Money(s.State.Credits), Ui.Ui.Highlight, 20));
        summary.Width = 420;
        var center = new StackPanel
        {
            Spacing = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 820,
            Children =
            {
                new TextBlock { Text = title, FontFamily = Ui.Ui.TitleFont, FontSize = 40, Foreground = victory ? Ui.Ui.Highlight : Ui.Ui.Bad, HorizontalAlignment = HorizontalAlignment.Center },
                Ui.Ui.Prose(body, 19),
                Ui.Ui.Box(summary),
                Ui.Ui.Box(_menu),
            },
        };
        Content = new Grid { Children = { new StarfieldView(), center } };
    }

    public override bool HandleAction(InputAction action) => _menu.Handle(action);
}
