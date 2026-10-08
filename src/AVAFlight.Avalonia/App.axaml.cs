using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AVAFlight.Avalonia.Services;
using AVAFlight.Infrastructure;

namespace AVAFlight.Avalonia;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppServices.Current ??= new AppServices(new AppPaths(), enableDevices: !Program.Muted);
            Ui.Ui.FontScale = AppServices.Current.Settings.FontScale;
            Ui.Ui.HighContrast = AppServices.Current.Settings.HighContrast;
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, _) => AppServices.Current?.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
