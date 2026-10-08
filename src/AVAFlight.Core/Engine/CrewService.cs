using AVAFlight.Core.Data;
using AVAFlight.Core.Model;

namespace AVAFlight.Core.Engine;

/// <summary>
/// Crew creation, training, assignment, injury, healing and in-flight repair.
/// Skills improve only through paid training sessions (manual); no source documents
/// experience-based growth in the DOS original, so none is implemented.
/// </summary>
public sealed class CrewService(GameSession s)
{
    public const int MaxRoster = 20;
    private double _repairProgress;

    public ActionResult Create(string name, string raceId)
    {
        name = name.Trim();
        if (name.Length is 0 or > 20) return ActionResult.Fail("Name must be 1-20 characters.");
        if (s.State.Roster.Count(c => !c.IsDead) >= MaxRoster) return ActionResult.Fail("Personnel files are full.");
        var race = s.Data.CrewRace(raceId);
        var m = new CrewMember { Name = name, RaceId = race.Id, Skills = new Dictionary<Skill, int>(race.Start) };
        s.State.Roster.Add(m);
        s.Story.Log(LogCategory.Crew, $"Recruited {name} ({race.Name}).");
        return ActionResult.Success($"{name} the {race.Name} has joined Interstel.");
    }

    public int TrainingSessionsToMax(CrewMember m, Skill skill)
    {
        var race = s.Data.CrewRace(m.RaceId);
        if (race.LearnRate == 0) return 0;
        int gap = race.Max[skill] - m.Skills[skill];
        return gap <= 0 ? 0 : (gap + race.LearnRate - 1) / race.LearnRate;
    }

    /// <summary>One session: +learning rate, capped at the race maximum, 300 MU.</summary>
    public ActionResult Train(Guid id, Skill skill)
    {
        var m = s.State.Roster.FirstOrDefault(c => c.Id == id);
        if (m is null || m.IsDead) return ActionResult.Fail("No such crew member.");
        var race = s.Data.CrewRace(m.RaceId);
        if (race.LearnRate == 0) return ActionResult.Fail("Androids can't be trained.");
        if (m.Skills[skill] >= race.Max[skill]) return ActionResult.Fail("Maximum training level has already been attained.");
        if (s.State.Credits < CrewRules.TrainingCost) return ActionResult.Fail("Insufficient funds for training.");
        s.Starport.Debit(CrewRules.TrainingCost, $"Training: {m.Name} ({skill})");
        m.Skills[skill] = Math.Min(race.Max[skill], m.Skills[skill] + race.LearnRate);
        m.TrainingSessions++;
        return ActionResult.Success($"{m.Name}'s {skill} is now {m.Skills[skill]}.");
    }

    /// <summary>Deletes a personnel file. Training money is not refunded (manual).</summary>
    public ActionResult Delete(Guid id)
    {
        var m = s.State.Roster.FirstOrDefault(c => c.Id == id);
        if (m is null) return ActionResult.Fail("No such crew member.");
        foreach (var role in s.State.Assignments.Where(kv => kv.Value == id).Select(kv => kv.Key).ToList())
            s.State.Assignments.Remove(role);
        s.State.Roster.Remove(m);
        return ActionResult.Success($"{m.Name}'s file has been deleted.");
    }

    public ActionResult Assign(CrewRole role, Guid? id)
    {
        if (id is null) { s.State.Assignments.Remove(role); return ActionResult.Success($"{role} post vacated."); }
        var m = s.State.Roster.FirstOrDefault(c => c.Id == id);
        if (m is null) return ActionResult.Fail("No such crew member.");
        if (m.IsDead) return ActionResult.Fail("A dead crew member cannot be assigned.");
        s.State.Assignments[role] = m.Id;
        return ActionResult.Success($"{m.Name} assigned as {role}.");
    }

    public bool CrewHasRace(string raceId) => s.State.ActiveCrew.Any(c => c.RaceId == raceId && !c.IsDead);

    public bool AllCrewDead => s.State.ActiveCrew.Any() && s.State.ActiveCrew.All(c => c.IsDead);

    /// <summary>Injures a random living crew member; durability reduces the damage.</summary>
    public CrewMember? InjureRandom(int severity, string cause)
    {
        var alive = s.State.ActiveCrew.Where(c => !c.IsDead).ToList();
        if (alive.Count == 0) return null;
        var victim = alive[s.Rng.Next(alive.Count)];
        Injure(victim, severity, cause);
        return victim;
    }

    public void Injure(CrewMember m, int severity, string cause)
    {
        int durability = s.Data.CrewRace(m.RaceId).Durability;
        // RECONSTRUCTION: damage scaled by (12 - durability)/8: Elowan (2) take 1.25x, Androids (10) 0.25x.
        int dmg = Math.Max(1, severity * (12 - durability) / 8);
        m.Vitality = Math.Max(0, m.Vitality - dmg);
        if (m.IsDead)
        {
            s.Emit(EventKind.Danger, $"{m.Name} has been killed ({cause}).", "warning");
            s.Story.Log(LogCategory.Crew, $"{m.Name} died: {cause}.");
            if (AllCrewDead) s.GameOver("!! CREW DECEASED !!");
        }
        else s.Emit(EventKind.Warning, $"{m.Name} is injured ({cause}): {m.HealthLabel}.");
    }

    /// <summary>
    /// Hourly natural healing (scaled by the Doctor's skill, per the manual) and engineer repairs.
    /// RECONSTRUCTION: heal rate 0.05..0.6 vitality/hour; repair 1..6 damage points/hour.
    /// </summary>
    public void HourlyTick()
    {
        double med = CrewRules.Efficiency(s.Skill(CrewRole.Doctor));
        foreach (var c in s.State.ActiveCrew.Where(c => !c.IsDead && c.Vitality < 100))
        {
            c.HealAccumulator += 0.05 + 0.55 * med;
            while (c.HealAccumulator >= 1 && c.Vitality < 100) { c.HealAccumulator -= 1; c.Vitality++; }
        }
        var damaged = s.State.Ship.Damage.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
        if (damaged.Count > 0 && s.State.Location.Mode != GameMode.Starport)
        {
            double eng = CrewRules.Efficiency(s.Skill(CrewRole.Engineer));
            _repairProgress += 1 + 5 * eng;
            while (_repairProgress >= 1 && damaged.Count > 0)
            {
                _repairProgress -= 1;
                var sys = damaged[0];
                // Manual: long repairs may need repair minerals; heavy damage (>50%) consumes one m³.
                if (s.State.Ship.Damage[sys] > 50 && !ConsumeRepairMineral())
                {
                    damaged.RemoveAt(0);
                    continue;
                }
                if (--s.State.Ship.Damage[sys] <= 0) damaged.RemoveAt(0);
            }
        }
    }

    private static readonly string[] RepairMinerals = ["cobalt", "molybdenum", "aluminum", "titanium", "promethium"];
    private double _mineralUse;

    private bool ConsumeRepairMineral()
    {
        var m = s.State.Ship.Cargo.FirstOrDefault(c => c.Kind == CargoKind.Mineral && RepairMinerals.Contains(c.Id) && c.Quantity > 0);
        if (m is null) return false;
        _mineralUse += 0.05;
        if (_mineralUse >= 1) { _mineralUse -= 1; m.Quantity -= 1; if (m.Quantity <= 0) s.State.Ship.Cargo.Remove(m); }
        return true;
    }

    /// <summary>Doctor → Treat: an immediate treatment boost for one patient.</summary>
    public ActionResult Treat(Guid id)
    {
        var m = s.State.Roster.FirstOrDefault(c => c.Id == id);
        if (m is null || m.IsDead) return ActionResult.Fail("Cannot treat that crew member.");
        if (s.Skill(CrewRole.Doctor) == 0) return ActionResult.Fail("No doctor is available.");
        int gain = 5 + (int)(20 * CrewRules.Efficiency(s.Skill(CrewRole.Doctor)));
        m.Vitality = Math.Min(100, m.Vitality + gain);
        s.AdvanceHours(2);
        return ActionResult.Success($"{m.Name} treated: {m.HealthLabel}.");
    }

    /// <summary>Full medical care at Starport.</summary>
    public void HealAllAtStarport()
    {
        foreach (var c in s.State.Roster.Where(c => !c.IsDead)) c.Vitality = 100;
    }
}
