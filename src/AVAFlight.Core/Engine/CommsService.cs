using System.Text;
using AVAFlight.Core.Data;
using AVAFlight.Core.Model;

namespace AVAFlight.Core.Engine;

/// <summary>One line of a conversation as shown on the comm screen.</summary>
public sealed record CommLine(bool FromPlayer, string Text, double TranslatedFraction = 1);

/// <summary>
/// Alien communication. Time model: MENU-DRIVEN (the original's real-time clock is represented by a
/// patience budget that runs down per exchange). Mechanics per manual + decompiled COMM overlay:
/// three postures; statements move attitude more than questions; five question topics; replies are
/// state-dependent (attitude, surrender, context) then picked at random from the eligible pool;
/// translation quality depends on the Communications officer (+25 if any crew member is of the
/// alien race, +50 if the comms officer is).
/// </summary>
public sealed class CommsService(GameSession s)
{
    private static readonly Dictionary<Posture, string[]> PlayerStatements = new()
    {
        [Posture.Friendly] = ["We come in peace and wish to learn about your people.", "Greetings. We hope our peoples can be friends.", "We mean no harm and offer our goodwill."],
        [Posture.Hostile] = ["Stand down or be destroyed.", "We are heavily armed. Answer our questions.", "Your ships are no match for ours. Cooperate."],
        [Posture.Obsequious] = ["O mighty ones, we humbly beg a moment of your attention.", "We are unworthy, yet we beseech your wisdom.", "Your splendour dazzles us. Please forgive our intrusion."],
    };

    public static readonly Dictionary<CommTopic, string> TopicLabels = new()
    {
        [CommTopic.Themselves] = "Your race", [CommTopic.OtherRaces] = "Other races",
        [CommTopic.OldEmpire] = "The Old Empire", [CommTopic.Ancients] = "The Ancients", [CommTopic.General] = "General info",
    };

    private EncounterState E => s.State.Encounter ?? throw new InvalidOperationException("No encounter in progress.");
    private AlienRaceDefinition Race => s.Data.Race(E.RaceId);
    private RaceRelation Rel => s.State.Relation(E.RaceId, Race.BaseAttitude);

    public int Attitude => Rel.Attitude;

    public string AttitudeLabel => Rel.Attitude switch
    {
        >= 50 => "Friendly", >= 15 => "Cordial", >= -15 => "Neutral", >= -50 => "Wary", _ => "Hostile",
    };

    /// <summary>Effective translation skill with the documented race bonuses.</summary>
    public int EffectiveCommSkill()
    {
        int skill = s.Skill(CrewRole.Communications);
        var officer = s.State.Assigned(CrewRole.Communications);
        if (officer is { IsDead: false } && officer.RaceId == E.RaceId) skill += 50;
        else if (s.Crew.CrewHasRace(E.RaceId)) skill += 25;
        if (E.RaceId == "spemin" && s.HasArtifact("whining-orb")) skill += 100;
        return skill;
    }

    /// <summary>Fraction of alien speech translated. RECONSTRUCTION: skill/150, at least 15%.</summary>
    public double TranslationFraction() => Math.Clamp(EffectiveCommSkill() / 150.0, 0.15, 1.0);

    private string Garble(string text, double fraction)
    {
        if (fraction >= 0.999 || string.IsNullOrWhiteSpace(Race.Syllables)) return text;
        var syl = Race.Syllables.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words = text.Split(' ');
        var sb = new StringBuilder();
        foreach (var w in words)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(s.Rng.NextDouble() < fraction ? w : syl[s.Rng.Next(syl.Length)].ToUpperInvariant());
        }
        return sb.ToString();
    }

    private CommLine Alien(string text)
    {
        double f = TranslationFraction();
        var line = new CommLine(false, Garble(text, f), f);
        E.Transcript.Add($"{Race.Name}: {line.Text}");
        s.PlaySound("comms");
        return line;
    }

    private CommLine Player(string text)
    {
        E.Transcript.Add($"You: {text}");
        return new CommLine(true, text);
    }

    private string Pick(IReadOnlyList<string> pool) => pool.Count == 0 ? "..." : pool[s.Rng.Next(pool.Count)];

    public bool CanTalk => s.State.Encounter is { } e && s.Data.Race(e.RaceId).Talks && !s.IsOver;

    private bool CommsFail()
    {
        int dmg = s.State.Ship.Damage[ShipSystem.Comms];
        return dmg > 0 && s.Rng.Next(100) < dmg;
    }

    /// <summary>Hail (or Respond, if the alien hailed first) in a posture.</summary>
    public List<CommLine> Hail(Posture posture)
    {
        var lines = new List<CommLine>();
        if (!Race.Talks) { lines.Add(new CommLine(false, "There is no response.")); return lines; }
        if (CommsFail()) { lines.Add(new CommLine(false, "Communications system failure! (damaged)")); return lines; }
        E.Posture = posture;
        E.Talking = true;
        if (!Rel.Contacted)
        {
            Rel.Contacted = true;
            s.State.Stats.AliensContacted++;
            s.Story.Log(LogCategory.Contact, $"First contact with the {Race.Name}. {Race.Description}");
        }
        lines.Add(Player(Pick(PlayerStatements[posture])));
        ApplyPosture(posture, 1.0);

        // Special protocols (decompiled COMMSPEC actions).
        if (SpecialRefusal() is { } refusal) { lines.Add(Alien(refusal)); return lines; }
        if (E.RaceId == "mechan" && !E.VerificationPassed) return MechanVerification(lines);

        lines.Add(Alien(E.Hostile && !E.Surrendered ? Pick(Race.HostileLines) : Pick(Race.Greetings)));
        if (E.RaceId == "velox" && s.HasArtifact("focusing-stone"))
            lines.Add(Alien("You are carrying Focusing Stone of Queen! Give it to Velox now!"));
        return lines;
    }

    private string? SpecialRefusal()
    {
        if (Race.Enemy is { } enemy && s.Crew.CrewHasRace(enemy))
        {
            if (E.RaceId == "elowan") { E.Patience = 0; return "We dost detect the presence of accursed Thrynn aboard thy vessel. We have naught to say to thee."; }
            if (E.RaceId == "thrynn") { E.Hostile = true; return "Elowan filth ssstains your ssship. You are enemiesss of the Thrynn!"; }
        }
        if (Rel.PermanentEnemy) { E.Hostile = true; return E.RaceId == "elowan" ? "Murderers of Elan! Thou art now and forever our mortal enemies!" : Pick(Race.HostileLines); }
        if (E.RaceId == "velox" && s.HasArtifact("crystal-orb")) { E.Hostile = true; return "You are stealing Small Egg! This is WAR!"; }
        return null;
    }

    private List<CommLine> MechanVerification(List<CommLine> lines)
    {
        lines.Add(Alien(Pick(Race.Greetings)));
        bool human = s.Crew.CrewHasRace("human");
        bool knows = s.Story.HasFlag("lore_noah9");
        if (!human)
        {
            lines.Add(Alien("WE DO NOT DETECT THE PRESENCE OF ANY HUMAN LIFEFORMS. YOU CANNOT BE GROUP 9."));
            FailVerification(lines);
        }
        else if (!knows)
        {
            lines.Add(Alien("QUERY: CONFIRM CODE BLUE STATUS. RESPONSE INVALID."));
            FailVerification(lines);
        }
        else
        {
            E.VerificationPassed = true;
            Rel.Attitude = Math.Min(100, Math.Max(Rel.Attitude, 0) + 40);
            lines.Add(Alien("VERIFICATION ACCEPTED. GROUP 9 LIAISON RECOGNISED. MECHAN 9 WILL ANSWER QUERIES."));
            s.Story.Log(LogCategory.Contact, "Mechan 9 accepted us as liaisons of Group 9.");
        }
        return lines;
    }

    private void FailVerification(List<CommLine> lines)
    {
        Rel.Attitude = Math.Max(-100, Rel.Attitude - 25);
        E.Hostile = true;
        E.Talking = false;
        lines.Add(Alien(Pick(Race.HostileLines)));
    }

    private void ApplyPosture(Posture posture, double weight)
    {
        int delta = (int)Math.Round(Race.PostureEffect.GetValueOrDefault(posture) * weight);
        if (Rel.PermanentEnemy) return;
        Rel.Attitude = Math.Clamp(Rel.Attitude + delta, -100, 100);
        if (posture == Posture.Hostile) E.HostilePressure++;
        // Spemin bluff, then surrender under sustained hostile pressure.
        if (Race.SurrendersToPressure && E.HostilePressure >= 3 && !E.Surrendered) Surrender();
        // Aliens pushed far enough open fire.
        if (Rel.Attitude <= -60 && !Race.SurrendersToPressure && Race.Ship.Aggression > 0) { E.Hostile = true; }
    }

    private void Surrender()
    {
        E.Surrendered = true;
        E.Hostile = false;
        Rel.Surrendered = true;
        E.Patience = Math.Max(E.Patience, 6);
        s.Story.Log(LogCategory.Contact, $"The {Race.Name} surrendered and offered to tell us what they know.");
    }

    private bool Exchange(List<CommLine> lines)
    {
        if (!E.Talking) { lines.Add(new CommLine(false, "Hail them first.")); return false; }
        if (E.Hostile && !E.Surrendered) { lines.Add(Alien(Pick(Race.HostileLines))); E.Talking = false; return false; }
        if (E.Patience <= 0)
        {
            lines.Add(Alien(Pick(Race.Farewells)));
            E.Talking = false;
            s.Combat.Disengage();
            return false;
        }
        E.Patience--;
        E.Exchanges++;
        s.AdvanceHours(0.1);
        return true;
    }

    /// <summary>Makes a statement in the current posture (moves attitude more than a question).</summary>
    public List<CommLine> Statement()
    {
        var lines = new List<CommLine>();
        if (!Exchange(lines)) return lines;
        lines.Add(Player(Pick(PlayerStatements[E.Posture])));
        ApplyPosture(E.Posture, 1.5);
        if (E.Surrendered) lines.Add(Alien(Pick(Race.SurrenderLines)));
        else if (E.Hostile) lines.Add(Alien(Pick(Race.HostileLines)));
        else lines.Add(Alien(Rel.Attitude >= 0 ? Pick(Race.Greetings) : Pick(Race.HostileLines.Count > 0 ? Race.HostileLines : Race.Greetings)));
        return lines;
    }

    public List<CommLine> ChangePosture(Posture posture)
    {
        E.Posture = posture;
        return [new CommLine(true, $"(Posture changed to {posture}.)")];
    }

    /// <summary>Asks about a topic. Replies depend on attitude, surrender and story flags.</summary>
    public List<CommLine> Ask(CommTopic topic)
    {
        var lines = new List<CommLine>();
        if (!Exchange(lines)) return lines;
        lines.Add(Player($"Tell us about {TopicLabels[topic].ToLowerInvariant()}."));
        ApplyPosture(E.Posture, 0.5);
        if (E.Hostile && !E.Surrendered) { lines.Add(Alien(Pick(Race.HostileLines))); return lines; }

        var pool = Race.Answers.GetValueOrDefault(topic) ?? [];
        var eligible = pool.Where(l => l.MinAttitude <= Rel.Attitude && (!l.SurrenderOnly || E.Surrendered || Rel.Surrendered)
                                       && (l.RequiresFlag is null || s.Story.HasFlag(l.RequiresFlag))).ToList();
        if (E.RaceId == "mechan" && !E.VerificationPassed) eligible.Clear();
        if (eligible.Count == 0)
        {
            lines.Add(Alien(RefusalFor(topic)));
            return lines;
        }
        var line = eligible[s.Rng.Next(eligible.Count)];
        var shown = Alien(line.Text);
        lines.Add(shown);
        // Revelations only register when enough of the message was understood.
        if (shown.TranslatedFraction >= 0.6)
        {
            s.Story.Reveal(line.SetsFlag, line.LogNote is null ? null : $"{line.LogNote}", LogCategory.Contact);
            if (line.SetsFlag == "nomad_data") s.Story.SetFlag("nomad_data");
        }
        else lines.Add(new CommLine(false, "(Much of the message could not be translated.)"));
        return lines;
    }

    private string RefusalFor(CommTopic topic) => E.RaceId switch
    {
        "elowan" => "Of that we may not speak, not yet.",
        "thrynn" => "That information isss not for you.",
        "velox" => "Velox are not telling that to lesser beings.",
        "spemin" => "We know EVERYTHING about that! But we are not telling!",
        "mechan" => "QUERY DENIED.",
        _ => "There is no answer.",
    };

    public List<CommLine> Terminate()
    {
        var lines = new List<CommLine>();
        if (E.Talking && Race.Farewells.Count > 0) lines.Add(Alien(Pick(Race.Farewells)));
        E.Talking = false;
        s.Combat.Disengage();
        return lines;
    }

    // ------------------------------------------------------------------ trades and special exchanges

    public sealed record TradeOffer(string Id, string Description);

    public IReadOnlyList<TradeOffer> Offers()
    {
        var list = new List<TradeOffer>();
        if (s.State.Encounter is not { Talking: true } e || (e.Hostile && !e.Surrendered)) return list;
        var ship = s.State.Ship;
        switch (e.RaceId)
        {
            case "thrynn" when Rel.Attitude >= 5:
                foreach (var a in ship.Cargo.Where(c => c.Kind == CargoKind.Artifact && c.Analyzed && !s.Data.Artifact(c.Id).PlotCritical))
                    list.Add(new TradeOffer("sell:" + a.Id, $"Sell the {a.Name} to the Thrynn for {s.Data.Artifact(a.Id).Value * 6:N0} M.U."));
                var pu = ship.Cargo.FirstOrDefault(c => c.Id == "plutonium");
                if (pu is not null) list.Add(new TradeOffer("plutonium", $"Trade {pu.Quantity:0.#} m³ plutonium for endurium"));
                if (ship.Endurium >= 30 && !s.HasArtifact("black-box")) list.Add(new TradeOffer("blackbox", "Buy the 'invulnerability device' for 30 m³ endurium"));
                break;
            case "elowan" when Rel.Attitude >= 30 && ship.Endurium < 20 && !s.Story.HasFlag("elowan_fuel_gift"):
                list.Add(new TradeOffer("fuel", "Ask the Elowan for emergency fuel"));
                break;
            case "velox" when s.HasArtifact("focusing-stone"):
                list.Add(new TradeOffer("stone", "Return the Focusing Stone to the Velox"));
                break;
            case "nomad":
                list.Add(new TradeOffer("nomad", "Accept the Nomad probe's survey data"));
                break;
        }
        return list;
    }

    public List<CommLine> AcceptOffer(string id)
    {
        var lines = new List<CommLine>();
        var ship = s.State.Ship;
        if (id.StartsWith("sell:"))
        {
            var art = ship.Cargo.FirstOrDefault(c => c.Kind == CargoKind.Artifact && c.Id == id[5..]);
            if (art is null) return lines;
            int price = s.Data.Artifact(art.Id).Value * 6; // fan: Thrynn pay 6x Starport value
            ship.Cargo.Remove(art);
            s.Starport.Credit(price, $"Sold {art.Name} to the Thrynn");
            Rel.Attitude = Math.Min(100, Rel.Attitude + 10);
            lines.Add(Alien("A sssatisfactory transaction."));
            s.Story.Log(LogCategory.Trade, $"Sold the {art.Name} to the Thrynn for {price:N0} M.U., against Interstel advice.");
        }
        else if (id == "plutonium")
        {
            var pu = ship.Cargo.First(c => c.Id == "plutonium");
            double gain = Math.Min(pu.Quantity * 0.8, ShipRules.EnduriumCapacity(ship, s.Data.Ship) - ship.Endurium + pu.Volume);
            ship.Cargo.Remove(pu);
            ship.Endurium += Math.Max(0, gain);
            lines.Add(Alien("Plutonium. Excellent. Here are your energy crystalsss."));
        }
        else if (id == "blackbox")
        {
            ship.Endurium -= 30;
            s.Planets.AddArtifact("black-box", analyzed: false);
            lines.Add(Alien("Thisss device will make you invulnerable. Probably."));
        }
        else if (id == "fuel")
        {
            ship.Endurium += 15;
            s.Story.SetFlag("elowan_fuel_gift");
            lines.Add(Alien("Take thou this fuel, friend, and go in peace."));
            s.Story.Log(LogCategory.Contact, "The Elowan gave us 15 m³ of fuel.");
        }
        else if (id == "stone")
        {
            var stone = ship.Cargo.First(c => c.Id == "focusing-stone");
            ship.Cargo.Remove(stone);
            Rel.Attitude = Math.Min(100, Rel.Attitude + 50);
            lines.Add(Alien("Queen's Focusing Stone is returning! Velox are remembering this kindness."));
            s.Story.Log(LogCategory.Contact, "Returned the Focusing Stone to the Velox. They are now far friendlier.");
        }
        else if (id == "nomad")
        {
            s.Story.SetFlag("nomad_data");
            var rich = s.Galaxy.AllPlanets.Where(p => p.MineralDensity == Galaxy.Density.Rich && p.Type != Galaxy.PlanetType.Crystal)
                .OrderBy(_ => s.Rng.Next(1000)).Take(3).ToList();
            foreach (var p in rich)
            {
                var sys = s.Galaxy.System(p.SystemId);
                s.Story.Log(LogCategory.Discovery, $"Nomad survey: mineral-rich world at {sys.X},{sys.Y} planet {p.Orbit}.");
            }
            lines.Add(Alien("SURVEY RECORD TRANSMITTED. 3 MINERAL-RICH WORLDS IDENTIFIED."));
        }
        return lines;
    }
}
