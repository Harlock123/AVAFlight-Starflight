using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(AVAFlight.Tests.UI.TestAppBuilder))]

namespace AVAFlight.Tests.UI;

public static class TestAppBuilder
{
    // UseHeadlessDrawing=false + UseSkia => real Skia rendering, so CaptureRenderedFrame
    // produces genuine pixels (used for docs/screenshots and render smoke tests).
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<AVAFlight.Avalonia.App>()
            .UseSkia()
            .UseHarfBuzz()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
