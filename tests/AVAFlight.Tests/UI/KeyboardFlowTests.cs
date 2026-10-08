using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using AVAFlight.Avalonia;
using AVAFlight.Avalonia.Screens;
using AVAFlight.Avalonia.Services;
using AVAFlight.Core.Model;
using AVAFlight.Infrastructure;

namespace AVAFlight.Tests.UI;

/// <summary>Drives the app with real key events through the window's input routing.</summary>
public class KeyboardFlowTests
{
    public KeyboardFlowTests()
    {
        AppServices.Current ??= new AppServices(new AppPaths(Path.Combine(Path.GetTempPath(), "avaflight-kb-" + Guid.NewGuid().ToString("N"))), enableDevices: false);
    }

    private static void Tap(Window w, Key key, int times = 1)
    {
        for (int i = 0; i < times; i++)
        {
            w.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
            w.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Keyboard_NewGame_ReadNotices_CreateCrewMember()
    {
        var w = new MainWindow(autoStart: false) { Width = 1280, Height = 800 };
        w.Show();
        w.Activate();
        w.Navigate(new TitleScreen());
        Tap(w, Key.Enter); // New game - Modern
        var game = Assert.IsType<GameScreen>(w.Current);
        game.Tick(0.016);
        Assert.Equal(GameMode.Starport, game.S.State.Location.Mode);

        Tap(w, Key.Enter);            // Operations
        Tap(w, Key.Enter);            // Notices
        Assert.True(game.S.State.OperationsEvaluated);
        Tap(w, Key.Escape, 2);        // back to Starport root
        Tap(w, Key.Down);             // Personnel
        Tap(w, Key.Enter);
        Tap(w, Key.Enter);            // Create new crew member
        Tap(w, Key.Enter);            // Human
        w.KeyTextInput("Ann");
        Dispatcher.UIThread.RunJobs();
        Tap(w, Key.Enter);
        var ann = Assert.Single(game.S.State.Roster);
        Assert.Equal("Ann", ann.Name);
        Assert.Equal("human", ann.RaceId);
    }

    [AvaloniaFact]
    public void HeldEnter_TriggersOnlyOneAction()
    {
        var w = new MainWindow(autoStart: false) { Width = 1280, Height = 800 };
        w.Show();
        w.Navigate(new TitleScreen());
        Tap(w, Key.Enter);
        var game = Assert.IsType<GameScreen>(w.Current);
        game.Tick(0.016);
        // Hold Enter (OS auto-repeat sends repeated KeyDowns without KeyUp): only the first counts.
        for (int i = 0; i < 5; i++) { w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null); Dispatcher.UIThread.RunJobs(); }
        w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
        // One Enter opened Operations; five would have gone Operations > Notices > (first notice)...
        Assert.False(game.S.State.OperationsEvaluated);
    }

    [AvaloniaFact]
    public void SmallWindow_ScalesUiDownUniformly()
    {
        var w = new MainWindow(autoStart: false) { MinWidth = 0, MinHeight = 0, Width = 640, Height = 480 }; // as under a tiling WM
        w.Show();
        w.Navigate(new TitleScreen());
        Dispatcher.UIThread.RunJobs();
        var scaler = w.FindControl<LayoutTransformControl>("Scaler")!;
        Assert.NotNull(scaler.LayoutTransform);
        Assert.NotNull(w.CaptureRenderedFrame());
    }
}
