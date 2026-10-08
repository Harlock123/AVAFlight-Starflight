using SDL;
using static SDL.SDL3;

namespace AVAFlight.Infrastructure.Input;

/// <summary>
/// Polls gamepads through SDL3 (Avalonia 12 has no gamepad API). Call <see cref="Poll"/> from a
/// UI timer (~60 Hz). Hot-plug is handled by re-enumerating devices once per second. Buttons and
/// the left stick / D-pad are reported as press/release edges using the control names used in
/// <see cref="DefaultBindings"/>, so they go through the same <see cref="InputMapper"/> as keys.
/// Never throws: if SDL3 is unavailable the service reports <see cref="IsAvailable"/> = false.
/// </summary>
public sealed unsafe class GamepadService : IDisposable
{
    private static readonly (SDL_GamepadButton Button, string Name)[] Buttons =
    [
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH, "South"),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST, "East"),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST, "West"),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH, "North"),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK, "Back"),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START, "Start"),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER, "LeftShoulder"),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER, "RightShoulder"),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP, "DPadUp"),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN, "DPadDown"),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT, "DPadLeft"),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT, "DPadRight"),
    ];

    private readonly Dictionary<SDL_JoystickID, IntPtr> _pads = new();
    private readonly HashSet<string> _down = new();
    private long _lastEnumerate;

    public bool IsAvailable { get; }
    public string Status { get; private set; }
    public double Deadzone { get; set; } = 0.25;
    public int ConnectedCount => _pads.Count;

    /// <summary>Raised for each control edge: (controlName, pressed).</summary>
    public event Action<string, bool>? ControlChanged;

    /// <summary>Left-stick position after deadzone, for analog cursor movement (star map).</summary>
    public (double X, double Y) LeftStick { get; private set; }

    public GamepadService()
    {
        try
        {
            if (!SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_GAMEPAD))
            {
                Status = "SDL gamepad init failed: " + SDL_GetError();
                return;
            }
            IsAvailable = true;
            Status = "No gamepad connected";
            Enumerate();
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or TypeInitializationException)
        {
            Status = "Gamepad support unavailable: " + e.Message;
        }
        Status ??= "Unavailable";
    }

    private void Enumerate()
    {
        using var ids = SDL_GetGamepads();
        var present = new HashSet<SDL_JoystickID>();
        if (ids != null)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                present.Add(id);
                if (_pads.ContainsKey(id)) continue;
                var pad = SDL_OpenGamepad(id);
                if (pad != null) _pads[id] = (IntPtr)pad;
            }
        }
        foreach (var gone in _pads.Keys.Where(k => !present.Contains(k)).ToList())
        {
            SDL_CloseGamepad((SDL_Gamepad*)_pads[gone]);
            _pads.Remove(gone);
        }
        Status = _pads.Count == 0
            ? "No gamepad connected"
            : string.Join(", ", _pads.Values.Select(p => SDL_GetGamepadName((SDL_Gamepad*)p) ?? "Gamepad"));
    }

    public void Poll()
    {
        if (!IsAvailable) return;
        try
        {
            SDL_PumpEvents();
            SDL_UpdateGamepads();
            long now = Environment.TickCount64;
            if (now - _lastEnumerate > 1000) { _lastEnumerate = now; Enumerate(); }

            var nowDown = new HashSet<string>();
            double lx = 0, ly = 0;
            foreach (var p in _pads.Values)
            {
                var pad = (SDL_Gamepad*)p;
                foreach (var (button, name) in Buttons)
                    if (SDL_GetGamepadButton(pad, button)) nowDown.Add(name);
                double x = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX) / 32767.0;
                double y = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY) / 32767.0;
                if (Math.Abs(x) > Math.Abs(lx)) lx = x;
                if (Math.Abs(y) > Math.Abs(ly)) ly = y;
            }
            (lx, ly) = ApplyDeadzone(lx, ly, Deadzone);
            LeftStick = (lx, ly);
            // The stick also acts as a D-pad for menu navigation.
            if (ly < -0.5) nowDown.Add("DPadUp");
            if (ly > 0.5) nowDown.Add("DPadDown");
            if (lx < -0.5) nowDown.Add("DPadLeft");
            if (lx > 0.5) nowDown.Add("DPadRight");

            foreach (var n in _down.Where(d => !nowDown.Contains(d)).ToList())
            {
                _down.Remove(n);
                ControlChanged?.Invoke(n, false);
            }
            // Held controls report every poll (like key auto-repeat); InputMapper turns that into a
            // single press for one-shot actions and rate-limited repeats for directions.
            foreach (var n in nowDown)
            {
                _down.Add(n);
                ControlChanged?.Invoke(n, true);
            }
        }
        catch (Exception e)
        {
            Status = "Gamepad polling error: " + e.Message;
        }
    }

    /// <summary>Radial deadzone with rescaling so motion starts smoothly at the deadzone edge.</summary>
    public static (double X, double Y) ApplyDeadzone(double x, double y, double deadzone)
    {
        double mag = Math.Sqrt(x * x + y * y);
        if (mag < deadzone || mag == 0) return (0, 0);
        double scaled = Math.Min(1, (mag - deadzone) / (1 - deadzone));
        return (x / mag * scaled, y / mag * scaled);
    }

    public void Dispose()
    {
        if (!IsAvailable) return;
        foreach (var p in _pads.Values) SDL_CloseGamepad((SDL_Gamepad*)p);
        _pads.Clear();
        SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_GAMEPAD);
    }
}
