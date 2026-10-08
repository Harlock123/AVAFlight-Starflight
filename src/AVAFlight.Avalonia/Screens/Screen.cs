using Avalonia.Controls;
using AVAFlight.Avalonia.Services;
using AVAFlight.Infrastructure.Audio;
using AVAFlight.Infrastructure.Input;

namespace AVAFlight.Avalonia.Screens;

/// <summary>Base class for top-level screens hosted by <see cref="MainWindow"/>.</summary>
public abstract class Screen : UserControl
{
    protected AppServices App => AppServices.Current;
    public MainWindow? Host { get; set; }

    /// <summary>Background music while this screen is shown.</summary>
    public virtual MusicCue Music => MusicCue.Title;

    /// <summary>Handles a logical input action; return true if consumed.</summary>
    public virtual bool HandleAction(InputAction action) => false;

    /// <summary>Per-frame update (~60 Hz) with the real elapsed time in seconds.</summary>
    public virtual void Tick(double dt) { }

    /// <summary>True while a text box on this screen should receive raw typing.</summary>
    public virtual bool CapturesText => false;

    /// <summary>Called with every raw key name before mapping (used by key rebinding). Return true to consume.</summary>
    public virtual bool CaptureRawKey(string keyName) => false;
}

/// <summary>Currently held logical directions (keyboard + gamepad), for continuous movement.</summary>
public static class HeldInput
{
    private static readonly Dictionary<(InputDevice, string), InputAction> Down = new();

    public static void Set(InputDevice d, string control, InputAction a, bool down)
    {
        if (down && a != InputAction.None) Down[(d, control)] = a;
        else Down.Remove((d, control));
    }

    public static bool IsHeld(InputAction a) => Down.Values.Contains(a);

    public static void Clear() => Down.Clear();

    /// <summary>Steering vector from held directions plus the analog stick.</summary>
    public static (double X, double Y) Direction()
    {
        double x = (IsHeld(InputAction.Right) ? 1 : 0) - (IsHeld(InputAction.Left) ? 1 : 0);
        double y = (IsHeld(InputAction.Down) ? 1 : 0) - (IsHeld(InputAction.Up) ? 1 : 0);
        if (AppServices.Current?.Gamepad is { } pad && (pad.LeftStick.X != 0 || pad.LeftStick.Y != 0))
            return pad.LeftStick;
        return (x, y);
    }
}
