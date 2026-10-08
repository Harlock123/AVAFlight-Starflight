using AVAFlight.Core.Data;
using AVAFlight.Core.Engine;
using AVAFlight.Core.Model;

namespace AVAFlight.Tests.Core;

public class DiplomacyTests
{
    private static GameSession Meet(string race, string commRace = "human", ulong seed = 7)
    {
        var s = TestHelpers.ReadyToLaunch(commRace: commRace, rngSeed: seed);
        s.Starport.Launch();
        s.Navigation.LeaveOrbit();
        s.Navigation.LeaveSystem();
        s.Combat.BeginEncounter(race);
        s.State.Encounter!.Hostile = false;
        return s;
    }

    [Fact]
    public void Spemin_SurrenderAfterSustainedHostilePressure_ThenRevealUhlekMindWorld()
    {
        var s = Meet("spemin", commRace: "thrynn");
        s.State.Assigned(CrewRole.Communications)!.Skills[Skill.Communication] = 250; // fully trained officer
        s.Comms.Hail(Posture.Hostile);
        s.Comms.Statement();
        s.Comms.Statement();
        Assert.True(s.State.Encounter!.Surrendered);
        for (int i = 0; i < 10 && !s.Story.HasFlag("knows_uhlek_brain"); i++) s.Comms.Ask(CommTopic.OtherRaces);
        Assert.True(s.Story.HasFlag("knows_uhlek_brain"));
    }

    [Fact]
    public void Velox_ObsequiousRaisesAttitude_FriendlyLowersIt()
    {
        var s = Meet("velox");
        int a0 = s.Comms.Attitude;
        s.Comms.Hail(Posture.Obsequious);
        Assert.True(s.Comms.Attitude > a0);
        int a1 = s.Comms.Attitude;
        s.Comms.ChangePosture(Posture.Friendly);
        s.Comms.Statement();
        Assert.True(s.Comms.Attitude < a1);
    }

    [Fact]
    public void Elowan_RefuseToDealWithThrynnAboard()
    {
        var s = Meet("elowan", commRace: "thrynn");
        var lines = s.Comms.Hail(Posture.Friendly);
        Assert.Contains(lines, l => l.Text.Contains("THRYNN", StringComparison.OrdinalIgnoreCase) || l.TranslatedFraction < 1);
        Assert.Equal(0, s.State.Encounter!.Patience);
    }

    [Fact]
    public void Mechan_RequireHumanAndNoah9Knowledge()
    {
        var s = Meet("mechan");
        s.Comms.Hail(Posture.Friendly);
        Assert.True(s.State.Encounter!.Hostile);          // no lore_noah9 yet
        var t = Meet("mechan");
        t.Story.SetFlag("lore_noah9");
        t.Comms.Hail(Posture.Friendly);
        Assert.True(t.State.Encounter!.VerificationPassed);
    }

    [Fact]
    public void CommSkill_ShiftsTranslationQuality_WithRaceBonuses()
    {
        var s = Meet("thrynn", commRace: "human");
        double human = s.Comms.TranslationFraction();
        var t = Meet("thrynn", commRace: "thrynn");
        double thrynn = t.Comms.TranslationFraction();
        Assert.True(thrynn > human);
        // Human comms 30 -> 30/150; Thrynn comms officer 50 + 50 bonus -> 100/150.
        Assert.Equal(0.2, human, 3);
        Assert.Equal(100 / 150.0, thrynn, 3);
    }

    [Fact]
    public void CommSkill_AffectsRevelationRate_Statistically()
    {
        // Labelled statistical bound over 40 seeds: an untrained comms officer registers far fewer
        // revelations than a fully trained one.
        static int Reveals(int comm)
        {
            int n = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var s = Meet("elowan", seed: seed);
                var c = s.State.Assigned(CrewRole.Communications)!;
                c.Skills[Skill.Communication] = comm;
                foreach (var m in s.State.Roster.Where(m => m != c)) m.Skills[Skill.Communication] = 0;
                s.State.Roster.RemoveAll(m => m.RaceId == "elowan" && m != c);
                s.State.Assignments.Remove(CrewRole.Doctor);
                s.Comms.Hail(Posture.Friendly);
                s.Comms.Ask(CommTopic.General);
                if (s.Story.HasFlag("knows_arth_deadline")) n++;
            }
            return n;
        }
        int high = Reveals(250), low = Reveals(10);
        Assert.True(high >= 15, $"high={high}");
        Assert.True(low <= 6, $"low={low}");
    }
}

public class CombatTests
{
    private static GameSession Fight(string race)
    {
        var s = TestHelpers.ReadyToLaunch();
        s.Starport.BuyComponent(ComponentKind.Laser, 1);
        s.Starport.Launch();
        s.Navigation.LeaveOrbit();
        s.Navigation.LeaveSystem();
        s.Combat.BeginEncounter(race);
        return s;
    }

    [Fact]
    public void ShipDestruction_TriggersGameOver()
    {
        var s = Fight("uhlek");
        Assert.True(s.State.Encounter!.Hostile);
        for (int i = 0; i < 30 * 120 && !s.IsOver; i++) s.Combat.Step(new CombatInput(0, false, false));
        Assert.True(s.IsOver);
        Assert.Contains("destroyed", s.State.GameOverReason);
        Assert.Null(s.State.Encounter);
    }

    [Fact]
    public void Fleeing_BeyondRange_EndsEncounter()
    {
        var s = Fight("nomad");
        var e = s.State.Encounter!;
        e.PlayerX = 300; // far outside every ship's range
        s.Combat.Step(new CombatInput(0, false, false));
        Assert.Null(s.State.Encounter);
        Assert.Equal(GameMode.Hyperspace, s.State.Location.Mode);
    }

    [Fact]
    public void Gazurtoid_ResistMissiles()
    {
        var s = Fight("gazurtoid");
        var e = s.State.Encounter!;
        var target = e.Ships[0];
        int hull = target.Hull + target.Shield;
        e.Projectiles.Add(new Projectile { Kind = ProjectileKind.Missile, X = target.X, Y = target.Y, Damage = 1000, FromPlayer = true, Life = 1 });
        s.Combat.Step(new CombatInput(0, false, false));
        Assert.True(target.Hull + target.Shield >= hull - 100 - 50);
    }

    [Fact]
    public void Combat_IsDeterministic_ForSameSeedAndInputs()
    {
        string Run()
        {
            var s = Fight("thrynn");
            s.Combat.ToggleWeapons();
            for (int i = 0; i < 600 && s.State.Encounter is not null && !s.IsOver; i++)
                s.Combat.Step(new CombatInput(i % 90 < 45 ? 1 : 0, i % 3 == 0, i % 10 == 0));
            return $"{s.State.Ship.HullPoints}|{s.State.Encounter?.Ships.Sum(x => x.Hull)}|{s.State.Clock.Hours}|{s.Rng.State}";
        }
        Assert.Equal(Run(), Run());
    }
}

public class StoryTests
{
    [Fact]
    public void Victory_CannotBeTriggered_WithoutPlantingEggAtNexus()
    {
        var s = TestHelpers.ReadyToLaunch();
        Assert.False(s.Story.TryWin());
        s.Story.SetFlag(StoryService.FlagCrystalDestroyed);
        Assert.False(s.Story.TryWin()); // destroyed flag alone is not enough
        Assert.False(s.State.Won);
    }

    [Fact]
    public void CrystalPlanet_WithoutOrb_MeltsShip()
    {
        var s = TestHelpers.ReadyToLaunch();
        s.Starport.Launch();
        s.Navigation.EnterSystem(s.Galaxy.SystemAt(192, 152)!);
        s.Navigation.EncounterRateMultiplier = 0;
        var crystal = s.Galaxy.SystemAt(192, 152)!.Planets[0];
        var (px, py) = NavigationService.PlanetPosition(crystal);
        for (int i = 0; i < 4000 && !s.IsOver; i++)
            s.Navigation.Fly(px - s.State.Location.SysX, py - s.State.Location.SysY, 0.05);
        Assert.True(s.IsOver);
        Assert.Contains("melted", s.State.GameOverReason);
    }

    [Fact]
    public void ArthFlare_EndsGame_UnlessWon()
    {
        var s = TestHelpers.ReadyToLaunch();
        s.AdvanceHours(300 * 24 + 1);
        Assert.True(s.IsOver);
        var w = TestHelpers.ReadyToLaunch();
        w.State.Won = true;
        w.AdvanceHours(300 * 24 + 1);
        Assert.False(w.IsOver);
    }

    [Fact]
    public void FullWinningSequence_OrbConeEgg_DestroysCrystalPlanet()
    {
        var s = TestHelpers.ReadyToLaunch(GamePreset.Modern);
        s.Starport.BuyPods(2);
        s.Starport.Launch();
        s.Navigation.EncounterRateMultiplier = 0;

        void CollectAt(string system, int orbit, string siteId)
        {
            var sys = s.Galaxy.Systems.First(x => x.Name == system);
            var p = sys.Planets.First(x => x.Orbit == orbit);
            s.Navigation.EnterSystem(sys);
            s.State.Location.OrbitPlanetId = p.Id;
            s.SetMode(GameMode.Orbit);
            var site = p.Sites.First(x => x.Id == siteId);
            s.State.Ship.Endurium = 40;
            Assert.True(s.Planets.Land(site.Lat, site.Lon).Ok, $"land at {system}");
            var st = s.State.Surface!;
            st.Creatures.Clear();
            var (x, y) = AVAFlight.Core.Galaxy.PlanetSurface.CellFromLatLon(site.Lat, site.Lon);
            st.TvX = x; st.TvY = y;
            Assert.True(s.Planets.PickUp().Ok, $"pick up {siteId}");
            st.TvX = st.ShipX; st.TvY = st.ShipY;
            Assert.True(s.Planets.Launch().Ok);
        }

        CollectAt("Sphexi", 1, "sphexi-orb");
        CollectAt("Crystal Cone system", 1, "cone");
        CollectAt("Phlegmak ruins", 3, "egg-1");
        Assert.True(s.HasArtifact("crystal-orb") && s.HasArtifact("crystal-cone") && s.HasArtifact("black-egg"));

        var crystalSys = s.Galaxy.SystemAt(192, 152)!;
        var crystal = crystalSys.Planets[0];
        s.Navigation.EnterSystem(crystalSys);
        var (px, py) = NavigationService.PlanetPosition(crystal);
        s.State.Location.SysX = px; s.State.Location.SysY = py;
        Assert.True(s.Navigation.EnterOrbit().Ok);
        Assert.Contains(s.Planets.VisibleSites(crystal), x => x.Kind == AVAFlight.Core.Galaxy.SiteKind.Nexus);
        var nexus = crystal.Sites.First(x => x.Kind == AVAFlight.Core.Galaxy.SiteKind.Nexus);
        s.State.Ship.Endurium = 40;
        Assert.True(s.Planets.Land(nexus.Lat, nexus.Lon).Ok);
        var surf = s.State.Surface!;
        surf.Creatures.Clear();
        var (nx, ny) = AVAFlight.Core.Galaxy.PlanetSurface.CellFromLatLon(nexus.Lat, nexus.Lon);
        surf.TvX = nx; surf.TvY = ny;
        Assert.True(s.Planets.PlantEgg().Ok);
        Assert.False(s.State.Won);
        surf.TvX = surf.ShipX; surf.TvY = surf.ShipY;
        int credits = s.State.Credits;
        Assert.True(s.Planets.Launch().Ok);
        Assert.True(s.State.Won);
        Assert.Equal(credits + StoryService.VictoryBonus, s.State.Credits);
    }

    [Fact]
    public void Policy_ClassicHidesAutoLog_ModernShowsIt()
    {
        var c = TestHelpers.ReadyToLaunch(GamePreset.Classic);
        var m = TestHelpers.ReadyToLaunch(GamePreset.Modern);
        foreach (var s in new[] { c, m }) s.Story.Log(LogCategory.Contact, "Met someone.");
        Assert.DoesNotContain(c.Story.VisibleLog(), e => e.Text == "Met someone.");
        Assert.Contains(m.Story.VisibleLog(), e => e.Text == "Met someone.");
        Assert.False(c.Policy.Waypoints);
        Assert.True(m.Policy.Waypoints);
        // Same economy in both presets.
        Assert.Equal(c.State.Credits, m.State.Credits);
        Assert.Equal(c.Navigation.FuelFor(10), m.Navigation.FuelFor(10));
    }
}
