using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AVAFlight.Avalonia;
using AVAFlight.Avalonia.Rendering;
using AVAFlight.Avalonia.Screens;
using AVAFlight.Avalonia.Services;
using AVAFlight.Core.Data;
using AVAFlight.Core.Engine;
using AVAFlight.Core.Galaxy;
using AVAFlight.Core.Model;
using AVAFlight.Infrastructure;
using AVAFlight.Infrastructure.Input;
using AVAFlight.Tests.Core;
using SkiaSharp;

namespace AVAFlight.Tests.UI;

/// <summary>
/// Renders every documented game view headlessly with real Skia rendering. Each test is also a
/// UI smoke test (the view renders without exception). When AVAFLIGHT_SCREENSHOTS is set to a
/// directory, the PNGs are written there (tools/screenshots.sh points it at docs/screenshots).
/// </summary>
public class ScreenshotTests
{
    private static readonly string OutDir =
        Environment.GetEnvironmentVariable("AVAFLIGHT_SCREENSHOTS") is { Length: > 0 } d ? d : Path.Combine(Path.GetTempPath(), "avaflight-shots");

    public ScreenshotTests() => EnsureServices();

    private static void EnsureServices()
    {
        if (AppServices.Current is not null) return;
        var dir = Path.Combine(Path.GetTempPath(), "avaflight-ui-" + Guid.NewGuid().ToString("N"));
        AppServices.Current = new AppServices(new AppPaths(dir), enableDevices: false);
        AppServices.Current.Settings.TextSpeed = 0; // instant text for deterministic captures
    }

    private static MainWindow Window(Screen screen)
    {
        EnsureServices();
        HeldInput.Clear();
        var w = new MainWindow(autoStart: false) { Width = 1280, Height = 800 };
        w.Show();
        w.Navigate(screen);
        screen.Tick(0.016);
        Dispatcher.UIThread.RunJobs();
        return w;
    }

    private static void FreezeAnimations(Control root, double time)
    {
        foreach (var v in root.GetVisualDescendants().OfType<SkiaView>()) v.Time = time;
    }

    private static string Save(MainWindow w, string name, double time = 2.0)
    {
        FreezeAnimations(w, time);
        Dispatcher.UIThread.RunJobs();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.True(frame!.PixelSize.Width >= 1000);
        Directory.CreateDirectory(OutDir);
        var path = Path.Combine(OutDir, name);
        frame.Save(path, new PngBitmapEncoderOptions());
        return path;
    }

    private static void Press(Screen s, params InputAction[] actions)
    {
        foreach (var a in actions) { s.HandleAction(a); Dispatcher.UIThread.RunJobs(); }
    }

    private static GameSession Outfitted(GamePreset preset = GamePreset.Modern)
    {
        var s = TestHelpers.ReadyToLaunch(preset, rngSeed: 11);
        string[] names = ["Reyes", "Okafor", "Vexx", "Kitikk", "Lindqvist", "Fernleaf"];
        for (int i = 0; i < names.Length; i++) s.State.Roster[i].Name = names[i];
        s.Starport.NameShip("Endeavour");
        s.Starport.BuyPods(6);
        s.Starport.BuyComponent(ComponentKind.Laser, 1);
        s.State.Credits = 25_000;
        s.Starport.BuyComponent(ComponentKind.Shield, 1);
        s.Starport.BuyComponent(ComponentKind.Armor, 1);
        s.State.Ship.Endurium = 45;
        return s;
    }

    private static void Orbit(GameSession s, string system, int orbit)
    {
        var sys = s.Galaxy.Systems.First(x => x.Name == system);
        s.Navigation.EnterSystem(sys);
        s.State.Location.OrbitPlanetId = sys.Planets.First(p => p.Orbit == orbit).Id;
        s.SetMode(GameMode.Orbit);
    }

    [AvaloniaFact]
    public void Shot01_MainMenu() => Save(Window(new TitleScreen()), "01_main_menu.png");

    [AvaloniaFact]
    public void Shot02_StarportHub()
    {
        var s = GameSession.NewGame(GamePreset.Modern);
        var screen = new GameScreen(s, null);
        var w = Window(screen);
        Press(screen, InputAction.Down, InputAction.Up);
        Save(w, "02_starport_hub.png");
    }

    [AvaloniaFact]
    public void Shot03_ShipConfiguration()
    {
        var s = Outfitted();
        s.SetMode(GameMode.Starport);
        var screen = new GameScreen(s, null);
        var w = Window(screen);
        Press(screen, InputAction.Down, InputAction.Down, InputAction.Down, InputAction.Down, InputAction.Confirm); // Ship Configuration
        Press(screen, InputAction.Down, InputAction.Down, InputAction.Down, InputAction.Down, InputAction.Confirm); // Lasers
        Press(screen, InputAction.Down);
        Save(w, "03_ship_configuration.png");
    }

    [AvaloniaFact]
    public void Shot04_CrewManagement()
    {
        var s = Outfitted();
        var screen = new GameScreen(s, null);
        var w = Window(screen);
        Press(screen, InputAction.Down, InputAction.Confirm);          // Personnel
        Press(screen, InputAction.Down, InputAction.Down, InputAction.Confirm); // second crew member
        Press(screen, InputAction.Confirm, InputAction.Confirm);       // two training sessions in Science
        Save(w, "04_crew_management.png");
    }

    private static GameSession InHyperspace(GamePreset preset = GamePreset.Modern)
    {
        var s = Outfitted(preset);
        s.Starport.Launch();
        s.Navigation.LeaveOrbit();
        s.Navigation.LeaveSystem(1, 0.2);
        s.State.Location.HyperX = 131.4;
        s.State.Location.HyperY = 103.2;
        s.State.Clock.Hours = 24 * 12 + 5;
        s.State.VisitedSystems.Add(s.Galaxy.SystemAt(121, 104)!.Id);
        if (s.Policy.Waypoints) s.State.Waypoints.Add(new Waypoint(143, 115, "Forbidden ruins?"));
        s.Say("Entering hyperspace.");
        return s;
    }

    [AvaloniaFact]
    public void Shot05_StarMap()
    {
        var s = InHyperspace();
        s.Navigation.SetCourse(148, 63);
        var screen = new GameScreen(s, null);
        var w = Window(screen);
        screen.OpenOverlay(new StarMapOverlay(screen));
        Dispatcher.UIThread.RunJobs();
        var map = w.GetVisualDescendants().OfType<StarMapView>().Single();
        map.CursorX = 148; map.CursorY = 63;
        Save(w, "05_star_map.png");
    }

    [AvaloniaFact]
    public void Shot06_InSystemView()
    {
        var s = Outfitted();
        s.Starport.Launch();
        s.Navigation.LeaveOrbit();
        s.State.Location.SysX = -48; s.State.Location.SysY = 30;
        var screen = new GameScreen(s, null);
        var w = Window(screen);
        Save(w, "06_insystem_view.png");
    }

    [AvaloniaFact]
    public void Shot07_PlanetSurvey()
    {
        var s = Outfitted();
        s.Starport.Launch();
        Orbit(s, "Colony candidate", 3);
        var p = s.CurrentPlanet!;
        s.State.Roster[1].Skills[Skill.Science] = 250;
        var sensors = s.Planets.Sensors();
        var analysis = s.Planets.Analyze();
        AVAFlight.Avalonia.Panels.SurveyPanel.Cache[p.Id] = (sensors.Value, analysis.Value);
        s.Planets.LogPlanet();
        var screen = new GameScreen(s, null);
        var w = Window(screen);
        Save(w, "07_planet_survey.png", time: 6);
    }

    private static GameSession Landed(string system, int orbit, int lat, int lon)
    {
        var s = Outfitted();
        s.Starport.Launch();
        Orbit(s, system, orbit);
        Assert.True(s.Planets.Land(lat, lon).Ok);
        return s;
    }

    [AvaloniaFact]
    public void Shot08_TerrainVehicle()
    {
        var s = Landed("Colony candidate", 3, 10, 20);
        var st = s.State.Surface!;
        // Drive a few steps away from the ship so both are visible.
        foreach (var (dx, dy) in new[] { (1, 0), (1, 0), (1, 1), (1, 0), (0, 1) }) s.Planets.MoveTv(dx, dy);
        var screen = new GameScreen(s, null);
        var w = Window(screen);
        Save(w, "08_terrain_vehicle.png");
    }

    [AvaloniaFact]
    public void Shot09_MineralCollection()
    {
        var s = Landed("Arth", 1, 0, 0);
        var st = s.State.Surface!;
        var surf = s.Planets.CurrentSurface!;
        var deposits = surf.Deposits.OrderBy(d => PlanetService.WrapDist(d.X, st.TvX) + Math.Abs(d.Y - st.TvY)).Take(3).ToList();
        foreach (var d in deposits.Take(2)) { st.TvX = d.X; st.TvY = d.Y; s.Planets.PickUp(); }
        // Park next to a remaining deposit so it is visible beside the vehicle.
        var next = surf.Deposits.Where(d => !s.State.PlanetRecord(st.PlanetId).MinedDeposits.Contains(d.Id))
            .OrderBy(d => PlanetService.WrapDist(d.X, st.TvX) + Math.Abs(d.Y - st.TvY)).First();
        st.TvX = PlanetSurface.WrapX(next.X - 1); st.TvY = next.Y;
        var screen = new GameScreen(s, null);
        var w = Window(screen);
        Press(screen, InputAction.Back); // open the vehicle menu
        Save(w, "09_mineral_collection.png");
    }

    [AvaloniaFact]
    public void Shot10_AlienContact()
    {
        var s = InHyperspace();
        s.State.Roster[4].Skills[Skill.Communication] = 200;
        s.Combat.BeginEncounter("velox");
        s.State.Encounter!.Hostile = false;
        s.Comms.Hail(Posture.Obsequious);
        s.Comms.Ask(CommTopic.Themselves);
        s.Comms.Ask(CommTopic.Ancients);
        var screen = new GameScreen(s, null);
        var w = Window(screen);
        screen.OpenComms();
        Dispatcher.UIThread.RunJobs();
        Save(w, "10_alien_contact.png");
    }

    [AvaloniaFact]
    public void Shot11_Combat()
    {
        var s = InHyperspace();
        s.Combat.BeginEncounter("thrynn");
        var e = s.State.Encounter!;
        e.Hostile = true;
        s.Combat.ToggleShields();
        s.Combat.ToggleWeapons();
        e.ArmingTimer = 0;
        // Point at the nearest enemy and fight until beams are on screen.
        for (int i = 0; i < 400; i++)
        {
            var t = e.Ships.Where(a => !a.Destroyed).OrderBy(a => Math.Abs(a.X - e.PlayerX) + Math.Abs(a.Y - e.PlayerY)).FirstOrDefault();
            if (t is null) break;
            e.PlayerHeading = Math.Atan2(t.Y - e.PlayerY, t.X - e.PlayerX);
            s.Combat.Step(new CombatInput(0, i % 4 == 0, true));
            if (i > 120 && e.Beams.Count > 0 && e.Projectiles.Count > 0) break;
        }
        var screen = new GameScreen(s, null);
        var w = Window(screen);
        Save(w, "11_combat.png");
    }

    [AvaloniaFact]
    public void Shot12_CargoManifest()
    {
        var s = Outfitted();
        s.State.Ship.Cargo.Add(new CargoItem { Kind = CargoKind.Mineral, Id = "titanium", Name = "Titanium", Quantity = 34 });
        s.State.Ship.Cargo.Add(new CargoItem { Kind = CargoKind.Mineral, Id = "gold", Name = "Gold", Quantity = 12.5 });
        s.State.Ship.Cargo.Add(new CargoItem { Kind = CargoKind.Mineral, Id = "plutonium", Name = "Plutonium", Quantity = 6 });
        s.State.Ship.Cargo.Add(new CargoItem { Kind = CargoKind.Lifeform, Id = "x", Name = "Specimen: Krathlith", Quantity = 1, VolumePerUnit = 3, ValuePerUnit = 1800 });
        s.Planets.AddArtifact("ring-device", analyzed: true);
        s.Planets.AddArtifact("tesseract", analyzed: false);
        s.State.Ship.Cargo.Add(new CargoItem { Kind = CargoKind.Message, Id = "msg-first-wave-ruin", Name = "Weathered marker", Quantity = 1, VolumePerUnit = 0 });
        s.Starport.Launch();
        var screen = new GameScreen(s, null);
        var w = Window(screen);
        Press(screen, InputAction.Cargo);
        Save(w, "12_cargo_manifest.png");
    }

    [AvaloniaFact]
    public void Shot13_CrewStatus()
    {
        var s = Outfitted();
        s.Starport.Launch();
        s.State.Roster[2].Vitality = 38;
        s.State.Roster[5].Vitality = 72;
        var screen = new GameScreen(s, null);
        var w = Window(screen);
        Press(screen, InputAction.CrewStatus, InputAction.Down, InputAction.Down);
        Save(w, "13_crew_status.png");
    }

    [AvaloniaFact]
    public void Shot14_GameOver()
    {
        var s = InHyperspace();
        s.State.Stats.SystemsVisited = 14; s.State.Stats.PlanetsLanded = 9; s.State.Stats.MineralsCollected = 212;
        s.GameOver("The ISS ENDEAVOUR was destroyed in combat with the Uhlek.");
        Save(Window(new EndScreen(s, victory: false, null)), "14_game_over.png");
    }

    [AvaloniaFact]
    public void Shot15_Victory()
    {
        var s = InHyperspace();
        s.State.Stats.SystemsVisited = 61; s.State.Stats.PlanetsLanded = 37; s.State.Stats.MineralsCollected = 1430; s.State.Stats.AliensContacted = 7;
        s.Story.SetFlag(StoryService.FlagEggPlanted);
        s.Story.SetFlag(StoryService.FlagCrystalDestroyed);
        Assert.True(s.Story.TryWin());
        Save(Window(new EndScreen(s, victory: true, null)), "15_victory.png");
    }

    [AvaloniaFact]
    public void Shot16_Settings() => Save(Window(new SettingsScreen()), "16_settings.png");

    [AvaloniaFact]
    public void Shot17_ClassicVsModern()
    {
        Bitmap Render(GamePreset preset)
        {
            var s = InHyperspace(preset);
            s.Navigation.SetCourse(148, 63);
            var screen = new GameScreen(s, null);
            var w = Window(screen);
            screen.OpenOverlay(new StarMapOverlay(screen));
            Dispatcher.UIThread.RunJobs();
            var map = w.GetVisualDescendants().OfType<StarMapView>().Single();
            map.CursorX = 55; map.CursorY = 32; // the Uhlek mind world: far beyond starting range
            FreezeAnimations(w, 2);
            return w.CaptureRenderedFrame()!;
        }
        var classic = Render(GamePreset.Classic);
        var modern = Render(GamePreset.Modern);
        Directory.CreateDirectory(OutDir);
        string a = Path.Combine(OutDir, "_classic.png"), b = Path.Combine(OutDir, "_modern.png");
        classic.Save(a, new PngBitmapEncoderOptions());
        modern.Save(b, new PngBitmapEncoderOptions());
        using var ca = SKBitmap.Decode(a);
        using var mb = SKBitmap.Decode(b);
        int w2 = ca.Width * 3 / 4, h2 = ca.Height * 3 / 4;
        using var surface = SKSurface.Create(new SKImageInfo(w2 * 2 + 12, h2 + 90));
        var c = surface.Canvas;
        c.Clear(SKColors.Black);
        var font = new SKFont(SKTypeface.Default, 22);
        var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
        var cyan = new SKPaint { Color = new SKColor(0x55, 0xFF, 0xFF), IsAntialias = true };
        c.DrawText("CLASSIC: original information only", 10, 28, font, cyan);
        c.DrawText("MODERN: + fuel-range ring, out-of-range warning, waypoints, range readout", w2 + 22, 28, font, cyan);
        c.DrawBitmap(ca, new SKRect(0, 40, w2, 40 + h2));
        c.DrawBitmap(mb, new SKRect(w2 + 12, 40, w2 * 2 + 12, 40 + h2));
        var small = new SKFont(SKTypeface.Default, 17);
        c.DrawText("Same star map, same rules, prices and fuel costs. Classic gives the original distance/fuel/time readout; Modern adds the range ring (dashed green),", 10, h2 + 64, small, white);
        c.DrawText("a BEYOND FUEL RANGE warning, the Range row in the status panel, player waypoints with notes, hints and tooltips.", 10, h2 + 84, small, white);
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(OutDir, "17_classic_vs_modern.png"), data.ToArray());
        File.Delete(a);
        File.Delete(b);
    }

    [AvaloniaFact]
    public void Navigation_BetweenScreens_AndResize_DoNotThrow()
    {
        var s = Outfitted();
        var game = new GameScreen(s, null);
        var w = Window(new TitleScreen());
        w.Navigate(new SettingsScreen());
        w.Navigate(new LoadScreen());
        w.Navigate(game);
        game.Tick(0.016);
        foreach (var (width, height) in new[] { (1024, 640), (1920, 1080), (1280, 800) })
        {
            w.Width = width; w.Height = height;
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(w.CaptureRenderedFrame());
        }
        // Every mode renders.
        s.Starport.Launch(); game.Tick(0.016); w.CaptureRenderedFrame();
        s.Navigation.LeaveOrbit(); game.Tick(0.016); w.CaptureRenderedFrame();
        s.Navigation.LeaveSystem(); game.Tick(0.016); w.CaptureRenderedFrame();
        s.Combat.BeginEncounter("minstrel"); game.Tick(0.016); w.CaptureRenderedFrame();
        game.OpenComms(); w.CaptureRenderedFrame();
        game.CloseOverlay();
        s.Combat.EndEncounter(fled: true); game.Tick(0.016); w.CaptureRenderedFrame();
        Assert.Equal(GameMode.Hyperspace, s.State.Location.Mode);
    }
}
