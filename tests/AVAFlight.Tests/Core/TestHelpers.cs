using AVAFlight.Core.Data;
using AVAFlight.Core.Engine;
using AVAFlight.Core.Model;

namespace AVAFlight.Tests.Core;

internal static class TestHelpers
{
    /// <summary>A new game with a full crew assigned, a named ship and the briefing read: ready to launch.</summary>
    public static GameSession ReadyToLaunch(GamePreset preset = GamePreset.Classic, ulong rngSeed = 7, string commRace = "human")
    {
        var s = GameSession.NewGame(preset, rng: new SplitMix64(rngSeed));
        string[] races = ["human", "human", "velox", "velox", commRace, "elowan"];
        CrewRole[] roles = [CrewRole.Captain, CrewRole.Science, CrewRole.Navigator, CrewRole.Engineer, CrewRole.Communications, CrewRole.Doctor];
        for (int i = 0; i < roles.Length; i++)
        {
            Assert.True(s.Crew.Create($"Crew{i}", races[i]).Ok);
            s.Crew.Assign(roles[i], s.State.Roster[^1].Id);
        }
        s.Starport.NameShip("Test");
        s.Starport.Notices().ToList();
        return s;
    }

    public static GameSession InOrbitOf(string systemName, int orbit, GamePreset preset = GamePreset.Classic)
    {
        var s = ReadyToLaunch(preset);
        Assert.True(s.Starport.Launch().Ok);
        var sys = s.Galaxy.Systems.First(x => x.Name == systemName);
        s.Navigation.EnterSystem(sys);
        s.State.Location.OrbitPlanetId = sys.Planets.First(p => p.Orbit == orbit).Id;
        s.SetMode(GameMode.Orbit);
        return s;
    }
}
