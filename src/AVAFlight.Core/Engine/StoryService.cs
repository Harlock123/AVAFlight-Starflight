using AVAFlight.Core.Data;
using AVAFlight.Core.Galaxy;
using AVAFlight.Core.Model;
using AVAFlight.Core.Random;

namespace AVAFlight.Core.Engine;

/// <summary>
/// Time, flares, notices, story flags, the captain's log and the victory condition.
/// Story flags are kept separate from galaxy generation: the galaxy is a pure function of the seed,
/// flags live only in <see cref="GameState.Flags"/>.
/// </summary>
public sealed class StoryService(GameSession s)
{
    /// <summary>Day on which Arth's star flares (decompiled data: Arth system flare date 300).</summary>
    public const int ArthFlareDay = 300;
    public const int VictoryBonus = 500_000;

    // Flags required, in this order of play, for the documented winning sequence.
    public const string FlagOrbAboard = "crystal_orb_aboard";
    public const string FlagUhlekMindDestroyed = "uhlek_mind_destroyed";
    public const string FlagEggPlanted = "egg_planted_at_nexus";
    public const string FlagCrystalDestroyed = "crystal_planet_destroyed";

    /// <summary>
    /// Assigns flare times. Authored systems use their documented day; others follow the documented
    /// pattern (dead zone at high X already flared; flares spread rimward over time).
    /// RECONSTRUCTION ASSUMPTION: exact per-star dates of the original are not used.
    /// </summary>
    public void ApplyFlareSchedule()
    {
        var authored = s.Data.World.Systems.ToDictionary(a => (a.X, a.Y));
        foreach (var sys in s.Galaxy.Systems)
        {
            if (authored.TryGetValue((sys.X, sys.Y), out var def))
            {
                sys.FlareAtHour = def.FlareDay is int d ? d * 24L : null;
                continue;
            }
            var rng = SplitMix64.Derive(s.State.GalaxySeed, (ulong)(sys.Id + 77_000));
            if (sys.X >= 137)
            {
                sys.FlareAtHour = rng.Chance(0.82) ? -24L * rng.Next(30, 1200) : 24L * rng.Next(1, 280);
            }
            else
            {
                long day = (137 - sys.X) * 25 + rng.Next(-40, 41);
                sys.FlareAtHour = day * 24L;
            }
        }
    }

    public bool HasFlag(string flag) => s.State.Flags.Contains(flag);

    public void SetFlag(string flag) => s.State.Flags.Add(flag);

    public void Log(LogCategory category, string text)
    {
        s.State.Log.Add(new LogEntry(s.State.Clock.Hours, category, text));
    }

    /// <summary>Entries visible under the current policy (Classic: mission + manual planet logs).</summary>
    public IEnumerable<LogEntry> VisibleLog() => s.Policy.CaptainsLog
        ? s.State.Log
        : s.State.Log.Where(e => e.Category is LogCategory.Mission or LogCategory.PlanetLog);

    public string StellarCondition(StarSystem sys)
    {
        if (sys.FlareAtHour is not long h) return "STABLE";
        long daysLeft = (h - s.State.Clock.Hours) / 24;
        if (h <= s.State.Clock.Hours) return "STABLE (POST-FLARE)";
        if (daysLeft > 399) return "STABLE";
        return daysLeft > 60 ? "SLIGHTLY UNSTABLE" : $"UNSTABLE - ESTIMATED TIME TO FLARE: {daysLeft} ARTH DAYS";
    }

    /// <summary>Advances game time, applying per-hour effects and daily checks.</summary>
    public void AdvanceTime(double hours)
    {
        if (hours <= 0 || s.IsOver) return;
        _fraction += hours;
        while (_fraction >= 1 && !s.IsOver)
        {
            _fraction -= 1;
            long before = s.State.Clock.Day;
            s.State.Clock.Hours++;
            HourlyEffects();
            if (s.State.Clock.Day != before) DailyChecks();
        }
    }

    private double _fraction;

    private void HourlyEffects()
    {
        var ship = s.State.Ship;
        if (ship.ShieldsUp && s.State.Location.Mode is not GameMode.Starport and not GameMode.Surface)
        {
            ship.Endurium = Math.Max(0, ship.Endurium - ShipRules.ShieldFuelPerHour);
            if (ship.Endurium <= 0) { ship.ShieldsUp = false; s.Emit(EventKind.Warning, "Shields dropped: no energy."); }
        }
        s.Crew.HourlyTick();
    }

    private void DailyChecks()
    {
        long day = s.State.Clock.Day;
        if (s.State.Won) return;

        // Arth flare. RECONSTRUCTION ASSUMPTION: the original's behaviour when Arth flares while the
        // ship is elsewhere is unverified. AVAFlight ends the game, because Arth's destruction is
        // the failure of the mission and leaves no home port.
        if (day >= ArthFlareDay)
        {
            s.GameOver($"Arth's sun flared on stardate {GameClock.FormatDay(day)}. Arth and Starport were incinerated. The mission has failed.");
            return;
        }
        // Being inside a system on its flare day is fatal (decompiled HYPERMSG logic).
        if (s.CurrentSystem is { } sys && s.State.Location.Mode is not GameMode.Hyperspace and not GameMode.Starport
            && sys.FlareAtHour is long fh && fh / 24 == day)
        {
            s.GameOver($"The star in system {sys.X},{sys.Y} flared on stardate {GameClock.FormatDay(day)}. The ISS {s.State.Ship.Name} and crew were incinerated.");
            return;
        }
        foreach (var n in s.Data.Notices.Where(n => n.Day <= day && !s.State.NoticesSeen.Contains(n.Id)
                                                     && (n.RequiresFlag is null || HasFlag(n.RequiresFlag))))
        {
            if (s.Policy.Hints) s.Emit(EventKind.Info, "A new Interstel notice is waiting at Operations.");
            break;
        }
        if (s.CurrentSystem is { } here && here.FlareAtHour is long fh2 && fh2 / 24 - day is > 0 and <= 3)
            s.Emit(EventKind.Danger, $"WARNING: the star in this system will flare in {fh2 / 24 - day} day(s)!", "warning");
    }

    /// <summary>Current Endurium price at Starport (raised by dated notices).</summary>
    public int EnduriumPrice()
    {
        long day = s.State.Clock.Day;
        int basePrice = s.Data.Mineral("endurium").ValuePerUnit;
        return day >= 134 ? 2000 : day >= 49 ? 1500 : basePrice;
    }

    public IEnumerable<NoticeDefinition> AvailableNotices() =>
        s.Data.Notices.Where(n => n.Day <= s.State.Clock.Day && (n.RequiresFlag is null || HasFlag(n.RequiresFlag)))
            .OrderBy(n => n.Day);

    /// <summary>Delivers a message site / dialogue revelation: sets its flag and records it.</summary>
    public void Reveal(string? flag, string? logNote, LogCategory category = LogCategory.Discovery)
    {
        if (flag is not null && s.State.Flags.Add(flag) && logNote is not null) Log(category, logNote);
        else if (flag is null && logNote is not null && s.State.Log.All(l => l.Text != logNote)) Log(category, logNote);
    }

    /// <summary>
    /// Victory requires the Crystal Planet to be destroyed by a Black Egg planted at the Nexus, which
    /// in turn requires the Orb (to survive orbit) and the Cone (to find the Nexus) — see
    /// <see cref="PlanetService"/>. This method is the only way the game can be won.
    /// </summary>
    public bool TryWin()
    {
        if (s.State.Won) return true;
        if (!HasFlag(FlagEggPlanted) || !HasFlag(FlagCrystalDestroyed)) return false;
        s.State.Won = true;
        s.State.Credits += VictoryBonus;
        s.State.Transactions.Add(new BankTransaction(s.State.Clock.Hours, "Interstel mission bonus", VictoryBonus));
        Log(LogCategory.Mission, "The Crystal Planet has been destroyed. The flares have stopped. Interstel has awarded a bonus of 500,000 M.U.");
        s.Emit(EventKind.Victory, "The Crystal Planet is destroyed! The stars are safe.", "victory");
        return true;
    }
}
