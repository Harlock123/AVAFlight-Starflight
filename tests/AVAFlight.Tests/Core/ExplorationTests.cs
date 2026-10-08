using AVAFlight.Core.Engine;
using AVAFlight.Core.Galaxy;
using AVAFlight.Core.Model;

namespace AVAFlight.Tests.Core;

public class ExplorationTests
{
    private static GameSession InHyperspace()
    {
        var s = TestHelpers.ReadyToLaunch();
        s.Starport.Launch();
        s.Navigation.LeaveOrbit();
        s.Navigation.LeaveSystem(1, 0);
        Assert.Equal(GameMode.Hyperspace, s.State.Location.Mode);
        s.Navigation.EncounterRateMultiplier = 0; // isolate navigation from random encounters
        return s;
    }

    [Fact]
    public void Cruise_ToNeighbourSystem_BurnsDocumentedFuel_AndEntersSystem()
    {
        var s = InHyperspace();
        var target = s.Galaxy.SystemAt(121, 104)!;
        double startFuel = s.State.Ship.Endurium;
        double dist = s.Navigation.Distance(target.X, target.Y);
        Assert.True(s.Navigation.SetCourse(target.X, target.Y).Ok);
        for (int i = 0; i < 2000 && s.State.Location.Mode == GameMode.Hyperspace; i++) s.Navigation.Fly(0, 0, 0.05);
        Assert.Equal(GameMode.System, s.State.Location.Mode);
        Assert.Equal(target.Id, s.State.Location.SystemId);
        Assert.Equal(dist * 0.48, startFuel - s.State.Ship.Endurium, 1);
        Assert.True(s.State.Clock.Hours > 0);
    }

    [Fact]
    public void FuelExhaustion_StopsShip_AndDistressTowsHomeForAFee()
    {
        var s = InHyperspace();
        s.State.Ship.Endurium = 0.5;
        s.Navigation.SetCourse(10, 10);
        for (int i = 0; i < 400; i++) s.Navigation.Fly(0, 0, 0.05);
        Assert.Equal(0, s.State.Ship.Endurium, 6);
        double x = s.State.Location.HyperX;
        s.Navigation.Fly(-1, 0, 1);
        Assert.Equal(x, s.State.Location.HyperX, 6); // cannot move without fuel
        int credits = s.State.Credits;
        Assert.True(s.Starport.Distress().Ok);
        Assert.Equal(GameMode.Starport, s.State.Location.Mode);
        Assert.True(s.State.Credits < credits);
    }

    [Fact]
    public void Flux_TeleportsToPairedEndpoint()
    {
        var s = InHyperspace();
        s.State.Location.HyperX = 102;
        s.State.Location.HyperY = 82;
        for (int i = 0; i < 100 && s.State.Location.HyperX < 110; i++) s.Navigation.Fly(1, 0, 0.02);
        // 104,82 is linked with 118,107 (documented notice).
        Assert.True(Math.Abs(s.State.Location.HyperX - 118) < 2 && Math.Abs(s.State.Location.HyperY - 107) < 2,
            $"ship at {s.State.Location.HyperX},{s.State.Location.HyperY}");
    }

    [Fact]
    public void ScienceSkill_ImprovesSensorCertainty_Statistically()
    {
        // Labelled statistical bound: with Science 250 every field is revealed; with 0, ~35% each.
        static int Revealed(int skill, int trials)
        {
            int known = 0;
            for (int t = 0; t < trials; t++)
            {
                var s = TestHelpers.InOrbitOf("Arth", 1);
                var sci = s.State.Assigned(CrewRole.Science)!;
                sci.Skills[AVAFlight.Core.Data.Skill.Science] = skill;
                foreach (var c in s.State.Roster.Where(c => c != sci)) c.Skills[AVAFlight.Core.Data.Skill.Science] = 0;
                s.Rng.State = (ulong)(t * 7919 + 1);
                var r = s.Planets.Sensors().Value!;
                known += new object?[] { r.Mass, r.BioPercent, r.MineralPercent, r.Atmosphere, r.Hydrosphere, r.Lithosphere }.Count(v => v is not null);
            }
            return known;
        }
        int high = Revealed(250, 30), low = Revealed(0, 30);
        Assert.Equal(180, high);
        Assert.InRange(low, 30, 110); // expected 63 (35%); bound ±~45
    }

    [Fact]
    public void Landing_OnCrushingGravity_IsFatal()
    {
        var s = TestHelpers.ReadyToLaunch();
        s.Starport.Launch();
        var giant = s.Galaxy.AllPlanets.First(p => p.Type == PlanetType.GasGiant && p.Gravity > 8);
        s.Navigation.EnterSystem(s.Galaxy.System(giant.SystemId));
        s.State.Location.OrbitPlanetId = giant.Id;
        s.SetMode(GameMode.Orbit);
        s.Planets.Land(0, 0);
        Assert.True(s.IsOver);
        Assert.Contains("crushed", s.State.GameOverReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TerrainVehicle_CollectsMinerals_UpToFiftyCubicMetres()
    {
        var s = TestHelpers.InOrbitOf("Arth", 1); // documented mineral-rich innermost planet
        Assert.True(s.Planets.Land(0, 0).Ok);
        var surf = s.Planets.CurrentSurface!;
        var st = s.State.Surface!;
        st.Creatures.Clear();
        double collected = 0;
        foreach (var d in surf.Deposits)
        {
            st.TvX = d.X; st.TvY = d.Y;
            var r = s.Planets.PickUp();
            if (!r.Ok) { Assert.Contains("full", r.Message); break; }
            collected = s.Planets.TvCargoUsed;
        }
        Assert.True(s.Planets.TvCargoUsed <= PlanetService.TvHold + 1e-9);
        Assert.Equal(PlanetService.TvHold, s.Planets.TvCargoUsed, 6);
        Assert.False(s.Planets.PickUp().Ok);
    }

    [Fact]
    public void TerrainVehicle_RunningOutOfFuel_ForcesReturnOnFoot_AndLosesVehicle()
    {
        var s = TestHelpers.InOrbitOf("Arth", 3);
        s.Planets.Land(0, -40);
        var st = s.State.Surface!;
        st.Creatures.Clear();
        st.TvFuel = 1.5;
        var path = new List<(int dx, int dy)>();
        (int, int)[] dirs = [(1, 0), (0, 1), (-1, 0), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1)];
        for (int i = 0; i < 6; i++)
            foreach (var (dx, dy) in dirs)
                if (s.Planets.MoveTv(dx, dy).Ok) { path.Add((dx, dy)); break; }
        Assert.True(st.OnFoot);
        // Walk back along the same path.
        for (int i = path.Count - 1; i >= 0 && s.State.Surface is not null; i--)
            s.Planets.MoveTv(-path[i].dx, -path[i].dy);
        Assert.False(s.State.Ship.HasTerrainVehicle);
    }

    [Fact]
    public void Ruins_YieldEndurium()
    {
        var s = TestHelpers.InOrbitOf("Arth", 3);
        s.Planets.Land(12, -40);
        var st = s.State.Surface!;
        var (x, y) = PlanetSurface.CellFromLatLon(12, -40);
        st.TvX = x; st.TvY = y;
        Assert.True(s.Planets.PickUp().Ok);
        Assert.Contains(st.TvCargo, c => c.Id == "endurium" && c.Quantity > 0);
    }
}
