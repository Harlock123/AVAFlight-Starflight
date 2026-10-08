using AVAFlight.Core.Data;
using AVAFlight.Core.Engine;
using AVAFlight.Core.Model;

namespace AVAFlight.Tests.Core;

public class StarportAndCrewTests
{
    [Fact]
    public void NewGame_HasDocumentedStartingState()
    {
        var s = GameSession.NewGame(GamePreset.Classic);
        Assert.Equal(12000, s.State.Credits);
        Assert.Equal(1, s.State.Ship.Engine);
        Assert.Equal(20, s.State.Ship.Endurium);
        Assert.Equal(0, s.State.Ship.CargoPods);
        Assert.Equal("", s.State.Ship.Name);
        Assert.Equal("01-01-4620", s.State.Clock.ToString());
        Assert.Equal(GameMode.Starport, s.State.Location.Mode);
    }

    [Fact]
    public void Launch_IsBlockedUntilPreflightChecksPass()
    {
        var s = GameSession.NewGame(GamePreset.Classic);
        Assert.False(s.Starport.Launch().Ok);
        Assert.Contains(s.Starport.PreflightProblems(), p => p.Contains("Crew Assignment"));
        Assert.Contains(s.Starport.PreflightProblems(), p => p.Contains("unchristened"));
        var ready = TestHelpers.ReadyToLaunch();
        Assert.Empty(ready.Starport.PreflightProblems());
        Assert.True(ready.Starport.Launch().Ok);
        Assert.Equal(GameMode.Orbit, ready.State.Location.Mode);
    }

    [Fact]
    public void Training_AddsLearningRate_Costs300_AndCapsAtMax()
    {
        var s = GameSession.NewGame(GamePreset.Classic);
        s.Crew.Create("Ann", "human");
        var ann = s.State.Roster[0];
        Assert.Equal(50, ann.Skills[Skill.Science]);
        var r = s.Crew.Train(ann.Id, Skill.Science);
        Assert.True(r.Ok);
        Assert.Equal(59, ann.Skills[Skill.Science]);   // human learning rate 9
        Assert.Equal(12000 - 300, s.State.Credits);
        // Fan arithmetic: 50 -> 250 takes 23 sessions (6,900 M.U.).
        Assert.Equal(22, s.Crew.TrainingSessionsToMax(ann, Skill.Science));
        s.State.Credits = 100_000;
        while (s.Crew.Train(ann.Id, Skill.Science).Ok) { }
        Assert.Equal(250, ann.Skills[Skill.Science]);
        Assert.Equal(23, ann.TrainingSessions);
        Assert.False(s.Crew.Train(ann.Id, Skill.Science).Ok);
    }

    [Fact]
    public void Androids_CannotBeTrained()
    {
        var s = GameSession.NewGame(GamePreset.Classic);
        s.Crew.Create("DX99", "android");
        var r = s.Crew.Train(s.State.Roster[0].Id, Skill.Navigation);
        Assert.False(r.Ok);
        Assert.Contains("Androids", r.Message);
    }

    [Fact]
    public void EconomyRoundTrip_BuyEquipment_SellMinerals_CreditArithmetic()
    {
        var s = TestHelpers.ReadyToLaunch();
        int start = s.State.Credits;
        Assert.True(s.Starport.BuyPods(4).Ok);                              // 4 x 500
        Assert.True(s.Starport.BuyComponent(ComponentKind.Armor, 1).Ok);    // 1,500
        Assert.Equal(start - 2000 - 1500, s.State.Credits);
        Assert.Equal(250, s.State.Ship.ArmorPoints);

        s.State.Ship.Cargo.Add(new CargoItem { Kind = CargoKind.Mineral, Id = "gold", Name = "Gold", Quantity = 10 });
        s.State.Ship.Cargo.Add(new CargoItem { Kind = CargoKind.Mineral, Id = "lead", Name = "Lead", Quantity = 5 });
        int before = s.State.Credits;
        Assert.True(s.Starport.SellCargo(s.State.Ship.Cargo[0], 10).Ok);
        Assert.True(s.Starport.SellCargo(s.State.Ship.Cargo[0], 5).Ok);
        Assert.Equal(before + 10 * 380 + 5 * 40, s.State.Credits);           // manual mineral values
        Assert.Empty(s.State.Ship.Cargo);

        // Trade-in: upgrading armour refunds 50% of the old part.
        int c = s.State.Credits;
        Assert.True(s.Starport.BuyComponent(ComponentKind.Armor, 2).Ok);
        Assert.Equal(c - (3100 - 750), s.State.Credits);
    }

    [Fact]
    public void Endurium_PriceRisesWithNotices()
    {
        var s = GameSession.NewGame(GamePreset.Classic);
        Assert.Equal(1000, s.Story.EnduriumPrice());
        s.State.Clock.Hours = 49 * 24;
        Assert.Equal(1500, s.Story.EnduriumPrice());
        s.State.Clock.Hours = 134 * 24;
        Assert.Equal(2000, s.Story.EnduriumPrice());
    }

    [Fact]
    public void BankInterest_Is12PercentPer300Days()
    {
        var s = GameSession.NewGame(GamePreset.Classic);
        s.State.Credits = 10_000;
        s.State.Clock.Hours = 150 * 24; // half a year
        s.Starport.CreditInterest();
        Assert.Equal(10_600, s.State.Credits);
    }

    [Fact]
    public void ArtifactsMustBeAnalysedBeforeSale()
    {
        var s = TestHelpers.ReadyToLaunch();
        s.Planets.AddArtifact("ring-device", analyzed: false);
        var art = s.State.Ship.Cargo[0];
        Assert.False(s.Starport.SellCargo(art, 1).Ok);
        Assert.True(s.Starport.AnalyzeArtifact(art).Ok);
        Assert.Equal(12000 - 500, s.State.Credits);
        Assert.True(s.Starport.SellCargo(art, 1).Ok);
        Assert.Equal(12000 - 500 + 8000, s.State.Credits);
    }

    [Fact]
    public void ColonyCriteria_FollowManual()
    {
        var s = GameSession.NewGame(GamePreset.Classic);
        var candidate = s.Galaxy.SystemAt(123, 101)!.Planets.First(p => p.Orbit == 3);
        Assert.True(StarportService.ColonyEvaluation(candidate).Ok);
        var crystal = s.Galaxy.SystemAt(192, 152)!.Planets[0];
        Assert.False(StarportService.ColonyEvaluation(crystal).Ok);
    }

    [Fact]
    public void CargoHoldCapacity_IsEnforcedOnTransfer()
    {
        var s = TestHelpers.InOrbitOf("Arth", 1);
        Assert.True(s.Planets.Land(0, 0).Ok);
        var st = s.State.Surface!;
        // No cargo pods: everything except endurium (fuel bay) must be left behind.
        st.TvCargo.Add(new CargoItem { Kind = CargoKind.Mineral, Id = "iron", Name = "Iron", Quantity = 5 });
        st.TvX = st.ShipX; st.TvY = st.ShipY;
        s.Planets.ReturnToShip();
        Assert.DoesNotContain(s.State.Ship.Cargo, c => c.Id == "iron");
    }
}
