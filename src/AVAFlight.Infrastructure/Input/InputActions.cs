namespace AVAFlight.Infrastructure.Input;

/// <summary>
/// Logical, device-independent actions. Keyboard, mouse and gamepad are all translated to
/// these, so views and the engine never see raw device events.
/// </summary>
public enum InputAction
{
    None,
    Up, Down, Left, Right,
    Confirm, Back,
    NextTab, PrevTab,
    Pause,
    // Gameplay shortcuts
    StarMap, CrewStatus, ShipStatus, Cargo, CaptainsLog,
    Fire, Shields, Weapons, Scan, Land, Launch, Hail, Maneuver,
    ZoomIn, ZoomOut,
    QuickSave,
}

/// <summary>Physical input sources a binding can come from.</summary>
public enum InputDevice { Keyboard, Gamepad }

/// <summary>
/// A binding from a physical control name (Avalonia <c>Key</c> name such as "Up" or "F5", or an
/// SDL gamepad button name such as "South" / "DPadUp") to a logical action.
/// </summary>
public sealed record InputBinding(InputDevice Device, string Control, InputAction Action);

public static class DefaultBindings
{
    public static IReadOnlyList<InputBinding> Create() =>
    [
        Kb("Up", InputAction.Up), Kb("W", InputAction.Up), Kb("NumPad8", InputAction.Up),
        Kb("Down", InputAction.Down), Kb("S", InputAction.Down), Kb("NumPad2", InputAction.Down),
        Kb("Left", InputAction.Left), Kb("A", InputAction.Left), Kb("NumPad4", InputAction.Left),
        Kb("Right", InputAction.Right), Kb("D", InputAction.Right), Kb("NumPad6", InputAction.Right),
        Kb("Enter", InputAction.Confirm), Kb("Space", InputAction.Confirm),
        Kb("Escape", InputAction.Back), Kb("Back", InputAction.Back),
        Kb("Tab", InputAction.NextTab),
        Kb("P", InputAction.Pause),
        Kb("M", InputAction.StarMap), Kb("C", InputAction.CrewStatus), Kb("I", InputAction.ShipStatus),
        Kb("G", InputAction.Cargo), Kb("L", InputAction.CaptainsLog),
        Kb("F", InputAction.Fire), Kb("H", InputAction.Hail), Kb("N", InputAction.Scan),
        Kb("OemPlus", InputAction.ZoomIn), Kb("Add", InputAction.ZoomIn),
        Kb("OemMinus", InputAction.ZoomOut), Kb("Subtract", InputAction.ZoomOut),
        Kb("F5", InputAction.QuickSave),

        Pad("DPadUp", InputAction.Up), Pad("DPadDown", InputAction.Down),
        Pad("DPadLeft", InputAction.Left), Pad("DPadRight", InputAction.Right),
        Pad("South", InputAction.Confirm), Pad("East", InputAction.Back),
        Pad("RightShoulder", InputAction.NextTab), Pad("LeftShoulder", InputAction.PrevTab),
        Pad("Start", InputAction.Pause), Pad("Back", InputAction.StarMap),
        Pad("West", InputAction.Fire), Pad("North", InputAction.Scan),
    ];

    private static InputBinding Kb(string key, InputAction a) => new(InputDevice.Keyboard, key, a);
    private static InputBinding Pad(string button, InputAction a) => new(InputDevice.Gamepad, button, a);
}
