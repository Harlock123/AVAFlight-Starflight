namespace AVAFlight.Infrastructure.Input;

/// <summary>
/// Translates physical controls into logical actions and suppresses duplicate activations.
/// Confirm/Back and other one-shot actions fire once per physical press (held keys and OS
/// auto-repeat are ignored); directional actions auto-repeat at a controlled rate so menus can
/// be scrolled by holding a key.
/// </summary>
public sealed class InputMapper
{
    private readonly Dictionary<(InputDevice, string), InputAction> _map = new();
    private readonly HashSet<(InputDevice, string)> _held = new();
    private readonly Dictionary<InputAction, long> _lastFire = new();
    private readonly Func<long> _clockMs;

    /// <summary>Delay before a held directional key starts repeating, and the repeat interval.</summary>
    public int RepeatDelayMs { get; set; } = 350;
    public int RepeatIntervalMs { get; set; } = 90;

    public InputMapper(IEnumerable<InputBinding> bindings, Func<long>? clockMs = null)
    {
        _clockMs = clockMs ?? (() => Environment.TickCount64);
        Rebind(bindings);
    }

    public IReadOnlyDictionary<(InputDevice Device, string Control), InputAction> Bindings => _map;

    public void Rebind(IEnumerable<InputBinding> bindings)
    {
        _map.Clear();
        foreach (var b in bindings) _map[(b.Device, b.Control)] = b.Action;
    }

    public InputAction Lookup(InputDevice device, string control) =>
        _map.TryGetValue((device, control), out var a) ? a : InputAction.None;

    public static bool IsDirectional(InputAction a) =>
        a is InputAction.Up or InputAction.Down or InputAction.Left or InputAction.Right;

    /// <summary>
    /// Handles a press (including OS auto-repeat presses). Returns the action to dispatch, or
    /// <see cref="InputAction.None"/> if the press should be swallowed as a duplicate.
    /// </summary>
    public InputAction Press(InputDevice device, string control)
    {
        var action = Lookup(device, control);
        if (action == InputAction.None) return InputAction.None;
        var key = (device, control);
        long now = _clockMs();
        bool alreadyHeld = !_held.Add(key);

        if (!alreadyHeld)
        {
            _lastFire[action] = now + RepeatDelayMs - RepeatIntervalMs;
            return action;
        }

        if (!IsDirectional(action)) return InputAction.None; // one-shot: ignore held repeats
        if (now - _lastFire.GetValueOrDefault(action) < RepeatIntervalMs) return InputAction.None;
        _lastFire[action] = now;
        return action;
    }

    public void Release(InputDevice device, string control) => _held.Remove((device, control));

    /// <summary>Clears held state, e.g. when the window loses focus.</summary>
    public void Reset() => _held.Clear();
}
