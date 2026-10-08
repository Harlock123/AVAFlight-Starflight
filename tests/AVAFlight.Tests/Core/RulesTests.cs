using AVAFlight.Core.Data;
using AVAFlight.Core.Engine;
using AVAFlight.Core.Galaxy;
using AVAFlight.Core.Model;

namespace AVAFlight.Tests.Core;

public class RulesTests
{
    [Theory]
    // Manual: 0.48 (class 1) to 0.16 (class 5) m³ per coordinate.
    [InlineData(1, 0.48)]
    [InlineData(2, 0.40)]
    [InlineData(3, 0.32)]
    [InlineData(4, 0.24)]
    [InlineData(5, 0.16)]
    public void FuelPerCoordinate_MatchesManualEndpoints(int engine, double expected)
    {
        var ship = new ShipState { Engine = engine };
        Assert.Equal(expected, ShipRules.FuelPerCoordinate(ship, tesseract: false), 3);
        Assert.Equal(expected / 2, ShipRules.FuelPerCoordinate(ship, tesseract: true), 3);
    }

    [Fact]
    public void FuelCost_ForKnownRoute_ArthToSol()
    {
        var s = TestHelpers.ReadyToLaunch();
        // Arth (125,100) -> Sol (215,86): distance sqrt(90² + 14²) = 91.08 coordinates.
        double d = GalaxyMap.Distance(125, 100, 215, 86);
        Assert.Equal(91.08, d, 2);
        Assert.Equal(d * 0.48, s.Navigation.FuelFor(d), 6);
        s.State.Ship.Engine = 5;
        Assert.Equal(d * 0.16, s.Navigation.FuelFor(d), 6);
    }

    [Fact]
    public void MassAndAcceleration_MatchDerivedFormula()
    {
        var start = new ShipState { Engine = 1 };
        Assert.Equal(100, ShipRules.Mass(start));   // documented starting ship: 100 t
        Assert.Equal(5, ShipRules.Acceleration(start)); // 5 G
        var max = new ShipState { Engine = 5, Armor = 5, CargoPods = 16, Shield = 5, Missile = 5, Laser = 5 };
        Assert.Equal(500, ShipRules.Mass(max));      // documented maximum: 500 t
        Assert.Equal(5, ShipRules.Acceleration(max));
    }

    [Fact]
    public void Calendar_UsesTenThirtyDayMonths()
    {
        Assert.Equal("01-01-4620", new GameClock().ToString());
        Assert.Equal("01-01-4621", GameClock.FormatDay(300));
        Assert.Equal("30-10-4620", GameClock.FormatDay(299));
        Assert.Equal("20-02-4620", GameClock.FormatDay(49));
    }

    [Fact]
    public void PlanetAttributes_AreAllInRange()
    {
        var g = GalaxyGenerator.Generate(GalaxyGenerator.DefaultSeed, GameData.Default.World);
        foreach (var p in g.AllPlanets)
        {
            Assert.InRange(p.Orbit, 1, 8);
            Assert.InRange(p.Mass, 1, 5);
            Assert.InRange(p.Gravity, 0.05, 12.0);
            Assert.True(Enum.IsDefined(p.Atmosphere));
            Assert.True(Enum.IsDefined(p.Temperature));
            Assert.True(Enum.IsDefined(p.Weather));
            Assert.True(Enum.IsDefined(p.MineralDensity));
            Assert.True(Enum.IsDefined(p.BioDensity));
            Assert.False(string.IsNullOrEmpty(p.AtmosphereComposition));
            if (p.Atmosphere == Atmosphere.None) Assert.Equal(Weather.Calm, p.Weather);
            if (p.Type == PlanetType.GasGiant) Assert.Equal(Density.None, p.MineralDensity);
        }
        foreach (var sys in g.Systems)
        {
            Assert.InRange(sys.X, 0, GalaxyMap.Width - 1);
            Assert.InRange(sys.Y, 0, GalaxyMap.Height - 1);
        }
    }

    [Fact]
    public void Surface_IsDeterministic_AndHasDepositsWhereMineralsExist()
    {
        var g = GalaxyGenerator.Generate(GalaxyGenerator.DefaultSeed, GameData.Default.World);
        var p = g.AllPlanets.First(x => x.MineralDensity >= Density.Moderate && x.Landable);
        var a = PlanetSurface.Generate(p, GameData.Default.Minerals);
        var b = PlanetSurface.Generate(p, GameData.Default.Minerals);
        Assert.Equal(a.Deposits, b.Deposits);
        Assert.NotEmpty(a.Deposits);
        Assert.All(a.Deposits, d => Assert.True(PlanetSurface.Passable(a.At(d.X, d.Y))));
    }

    [Fact]
    public void StellarFlareSchedule_ArthFlaresOnDay300_DeadZoneMostlyFlared()
    {
        var s = GameSession.NewGame(GamePreset.Classic);
        var arth = s.Galaxy.SystemAt(125, 100)!;
        Assert.Equal(300 * 24L, arth.FlareAtHour);
        var dead = s.Galaxy.Systems.Where(x => x.X >= 137 && x.Name is null).ToList();
        Assert.True(dead.Count(x => x.FlareAtHour < 0) > dead.Count / 2);
        Assert.Null(s.Galaxy.SystemAt(192, 152)!.FlareAtHour); // the Crystal Planet itself never flares
    }
}
