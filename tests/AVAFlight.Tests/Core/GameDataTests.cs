using AVAFlight.Core.Data;
using AVAFlight.Core.Galaxy;

namespace AVAFlight.Tests.Core;

public class GameDataTests
{
    [Fact]
    public void EmbeddedData_LoadsAndValidates()
    {
        var d = GameData.LoadEmbedded();
        Assert.Empty(d.Validate());
        Assert.Equal(12000, d.Ship.StartingCredits);
        Assert.Equal(22, d.Minerals.Count);
        Assert.Equal(1000, d.Mineral("endurium").ValuePerUnit);
        Assert.Equal(5, d.CrewRaces.Count);
        Assert.Contains(d.Races, r => r.Id == "velox");
    }

    [Fact]
    public void Galaxy_HasDocumentedTotals_AndIsDeterministic()
    {
        var d = GameData.Default;
        var a = GalaxyGenerator.Generate(GalaxyGenerator.DefaultSeed, d.World);
        var b = GalaxyGenerator.Generate(GalaxyGenerator.DefaultSeed, d.World);
        Assert.Equal(270, a.Systems.Count);
        Assert.Equal(811, a.AllPlanets.Count());
        Assert.True(a.Systems.All(s => s.Planets.Count <= 8));
        Assert.Equal(a.Systems.Select(s => (s.X, s.Y, s.Class, s.Planets.Count)), b.Systems.Select(s => (s.X, s.Y, s.Class, s.Planets.Count)));
        Assert.Equal(a.AllPlanets.Select(p => (p.Type, p.Gravity, p.MineralDensity)), b.AllPlanets.Select(p => (p.Type, p.Gravity, p.MineralDensity)));
        Assert.NotNull(a.SystemAt(125, 100));
        var c = GalaxyGenerator.Generate(42, d.World);
        Assert.NotEqual(a.Systems.Skip(40).Select(s => (s.X, s.Y)), c.Systems.Skip(40).Select(s => (s.X, s.Y)));
    }
}
