using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AVAFlight.Avalonia.Screens;
using AVAFlight.Avalonia.Services;
using AVAFlight.Infrastructure.Input;

namespace AVAFlight.Avalonia;

/// <summary>
/// Hosts one <see cref="Screen"/> at a time and routes input. Physical keys and gamepad buttons go
/// through the <see cref="InputMapper"/> (which suppresses duplicate presses) before reaching the
/// screen as logical actions; held directions are tracked separately for real-time flight.
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private DateTime _last = DateTime.UtcNow;
    private Screen? _screen;

    public MainWindow() : this(autoStart: true) { }

    public MainWindow(bool autoStart)
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Deactivated += (_, _) => { AppServices.Current?.Input.Reset(); HeldInput.Clear(); };
        if (AppServices.Current?.Gamepad is { } pad) pad.ControlChanged += OnPad;
        SizeChanged += (_, e) => FitToWindow(e.NewSize);
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Input, (_, _) => OnFrame());
        if (autoStart)
        {
            _timer.Start();
            Navigate(new TitleScreen());
            if (AppServices.Current?.Settings.Fullscreen == true) SetFullscreen(true);
        }
    }

    public Screen? Current => _screen;

    /// <summary>
    /// Stable binding name for a key. Some Avalonia keys share a value (Enter/Return,
    /// Back/Backspace...), so Enum.ToString() is not reliable; known aliases are normalised here.
    /// </summary>
    public static string KeyName(Key key) => key switch
    {
        Key.Enter => "Enter",
        Key.Back => "Back",
        Key.Escape => "Escape",
        Key.PageDown => "PageDown",
        Key.PageUp => "PageUp",
        Key.CapsLock => "CapsLock",
        _ => key.ToString(),
    };

    public const double DesignMinWidth = 1024, DesignMinHeight = 640;

    private void FitToWindow(Size size)
    {
        var host = this.FindControl<ContentControl>("ScreenHost")!;
        var scaler = this.FindControl<LayoutTransformControl>("Scaler")!;
        double scale = Math.Min(1.0, Math.Min(size.Width / DesignMinWidth, size.Height / DesignMinHeight));
        if (scale >= 0.999)
        {
            scaler.LayoutTransform = null;
            host.Width = double.NaN;
            host.Height = double.NaN;
        }
        else
        {
            scaler.LayoutTransform = new global::Avalonia.Media.ScaleTransform(scale, scale);
            host.Width = size.Width / scale;
            host.Height = size.Height / scale;
        }
    }

    public void Navigate(Screen screen)
    {
        _screen = screen;
        screen.Host = this;
        this.FindControl<ContentControl>("ScreenHost")!.Content = screen;
        AppServices.Current?.Audio.PlayMusic(screen.Music);
        HeldInput.Clear();
        FitToWindow(ClientSize);
    }

    public void SetFullscreen(bool on) =>
        WindowState = on ? WindowState.FullScreen : WindowState.Normal; // borderless fullscreen

    private void OnFrame()
    {
        var now = DateTime.UtcNow;
        double dt = Math.Min(0.1, (now - _last).TotalSeconds);
        _last = now;
        AppServices.Current?.Gamepad?.Poll();
        _screen?.Tick(dt);
    }

    private void OnPad(string control, bool pressed)
    {
        var svc = AppServices.Current;
        if (svc is null) return;
        var action = svc.Input.Lookup(InputDevice.Gamepad, control);
        HeldInput.Set(InputDevice.Gamepad, control, action, pressed);
        if (!pressed) { svc.Input.Release(InputDevice.Gamepad, control); return; }
        var a = svc.Input.Press(InputDevice.Gamepad, control);
        if (a != InputAction.None) _screen?.HandleAction(a);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var svc = AppServices.Current;
        if (svc is null || _screen is null) return;
        if (e.Key == Key.F11 || (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            svc.Settings.Fullscreen = WindowState != WindowState.FullScreen;
            SetFullscreen(svc.Settings.Fullscreen);
            e.Handled = true;
            return;
        }
        // Let text boxes receive typing; only Escape/Enter/Tab are routed while typing.
        if (_screen.CapturesText && e.Source is TextBox && e.Key is not (Key.Escape or Key.Enter or Key.Tab)) return;
        string name = KeyName(e.Key);
        if (_screen.CaptureRawKey(name)) { e.Handled = true; return; }
        HeldInput.Set(InputDevice.Keyboard, name, svc.Input.Lookup(InputDevice.Keyboard, name), true);
        var action = svc.Input.Press(InputDevice.Keyboard, name);
        if (action != InputAction.None && _screen.HandleAction(action)) e.Handled = true;
        else if (action != InputAction.None) e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        var svc = AppServices.Current;
        if (svc is null) return;
        string name = KeyName(e.Key);
        svc.Input.Release(InputDevice.Keyboard, name);
        HeldInput.Set(InputDevice.Keyboard, name, InputAction.None, false);
    }
}
