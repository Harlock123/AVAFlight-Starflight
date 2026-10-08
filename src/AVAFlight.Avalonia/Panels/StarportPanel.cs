using Avalonia;
using Avalonia.Controls;
using AVAFlight.Avalonia.Ui;
using AVAFlight.Core.Data;
using AVAFlight.Core.Engine;
using AVAFlight.Core.Model;
using AVAFlight.Infrastructure.Input;

namespace AVAFlight.Avalonia.Panels;

/// <summary>
/// Starport: Operations, Personnel, Crew Assignment, Bank, Ship Configuration, Trade Depot and
/// Docking Bay. The original's walk-around concourse is replaced by a direct module menu
/// (interface friction removed; every service and number is unchanged).
/// </summary>
public sealed class StarportPanel : UserControl
{
    private readonly GameSession _s;
    private readonly PageStack _stack = new();
    private readonly Action _changed;

    public bool IsTyping => _stack.IsTyping;
    public PageStack Stack => _stack;

    public StarportPanel(GameSession s, Action changed, bool tooltips)
    {
        _s = s;
        _changed = changed;
        _stack.ShowTooltips = tooltips;
        _stack.SetRoot(Root());
        Content = new Border { Padding = new Thickness(12), Child = _stack };
    }

    public bool Handle(InputAction a) => _stack.Handle(a);

    private void Done(ActionResult r, bool pop = false)
    {
        _stack.Status(r.Message, !r.Ok);
        if (r.Ok && pop) _stack.Pop();
        else _stack.Refresh();
        _changed();
    }

    private MenuPage Root() => new()
    {
        Title = "Starport",
        Items = () =>
        [
            new MenuEntry("Operations", () => _stack.Push(Operations()), Hint: "Notices, mission briefing and colony evaluations."),
            new MenuEntry("Personnel", () => _stack.Push(Personnel()), Hint: "Create crew members, train their skills (300 M.U. per session), delete files."),
            new MenuEntry("Crew Assignment", () => _stack.Push(Assignment()), Hint: "Assign a crew member to each of the six posts."),
            new MenuEntry("Bank", () => _stack.Push(Bank()), Hint: "Balance and recent transactions. Interest: 12% per year, credited on docking."),
            new MenuEntry("Ship Configuration", () => _stack.Push(ShipConfig()), Hint: "Buy and sell engines, shields, armour, weapons and cargo pods; repairs; name your ship."),
            new MenuEntry("Trade Depot", () => _stack.Push(Trade()), Hint: "Sell minerals, lifeforms and artifacts; buy fuel; analyse artifacts (500 M.U.)."),
            new MenuEntry("Docking Bay", () => _stack.Push(Docking()), Hint: "Launch, once the pre-flight checklist is complete."),
        ],
        Details = () => Ui.Ui.VStack(6,
            Ui.Ui.Heading("STARPORT - ARTH", 12),
            Ui.Ui.Prose("Interstel's orbital Starport is your home base. Outfit the ship, gather and train a crew, sell what you find, and read the notices at Operations."),
            Ui.Ui.Row("Funds", Ui.Ui.Money(_s.State.Credits), Ui.Ui.Highlight),
            Ui.Ui.Row("Stardate", _s.State.Clock.ToString()),
            Ui.Ui.Row("Ship", string.IsNullOrEmpty(_s.State.Ship.Name) ? "(unnamed)" : "ISS " + _s.State.Ship.Name)),
    };

    // ------------------------------------------------------------------ Operations

    private MenuPage Operations() => new()
    {
        Title = "Operations",
        Items = () =>
        [
            new MenuEntry("Notices", () => _stack.Push(Notices())),
            new MenuEntry("Mission briefing", () => _stack.Push(Briefing())),
            new MenuEntry("Colony evaluation", () => _stack.Push(Evaluation()), Hint: "Recommend planets you have logged. Good recommendations are rewarded; bad ones are fined."),
        ],
    };

    private MenuPage Notices()
    {
        var list = _s.Starport.Notices().ToList();
        _changed();
        MenuPage page = null!;
        page = new MenuPage
        {
            Title = "Notices",
            Items = () => list.Select(n => new MenuEntry("Notice of " + GameClock.FormatDay(n.Day), null, true)),
            Details = () => list.Count == 0 ? null : Ui.Ui.Box(Ui.Ui.Prose(list[Math.Clamp(page.Selected, 0, list.Count - 1)].Text, 16)),
        };
        return page;
    }

    private MenuPage Briefing() => new()
    {
        Title = "Briefing",
        Items = () => [new MenuEntry("Back", () => _stack.Pop())],
        Details = () => Ui.Ui.Box(Ui.Ui.Prose(
            "NEWLY WRITTEN BRIEFING (AVAFlight): Arth was settled centuries ago by colonists who had lost all memory of where they came from. " +
            "Rich deposits of Endurium found beneath an old shaft have made starflight possible again, and Interstel was founded to explore. " +
            "You command one of its survey ships.\n\nYour orders: find minerals and lifeforms, identify worlds suitable for colonies, recover artifacts of " +
            "the Ancients, and make contact with any intelligent races. Interstel's scientists have one more concern: Arth's sun is becoming unstable.", 16)),
    };

    private MenuPage Evaluation() => new()
    {
        Title = "Evaluation",
        Items = () =>
        {
            var logged = _s.State.Planets.Where(kv => kv.Value.Logged && !kv.Value.Recommended).Select(kv => _s.Galaxy.Planet(kv.Key)).ToList();
            if (logged.Count == 0) return [new MenuEntry("(no logged planets awaiting evaluation)", null, false)];
            return logged.Select(p =>
            {
                var sys = _s.Galaxy.System(p.SystemId);
                return new MenuEntry($"{sys.X},{sys.Y} planet {p.Orbit} ({p.Type})", () => Done(_s.Starport.RecommendColony(p.Id)),
                    Hint: "Recommend as a colony world.");
            });
        },
    };

    // ------------------------------------------------------------------ Personnel

    private MenuPage Personnel() => new()
    {
        Title = "Personnel",
        Items = () =>
        {
            var items = new List<MenuEntry> { new("Create new crew member", () => _stack.Push(ChooseRace())) };
            foreach (var m in _s.State.Roster)
            {
                var race = _s.Data.CrewRace(m.RaceId);
                items.Add(new MenuEntry($"{m.Name} ({race.Name})", m.IsDead ? null : () => _stack.Push(Member(m)), !m.IsDead,
                    Value: m.IsDead ? "DEAD" : m.HealthLabel));
            }
            return items;
        },
    };

    private MenuPage ChooseRace() => new()
    {
        Title = "Choose race",
        Items = () => _s.Data.CrewRaces.Select(r => new MenuEntry(r.Name, () => _stack.Push(NameCrew(r.Id)),
            Hint: $"{r.Description} Durability {r.Durability}, learning rate {r.LearnRate}.")),
        Details = () => Ui.Ui.Box(RaceTable()),
    };

    private Control RaceTable()
    {
        var p = Ui.Ui.VStack(2, Ui.Ui.Label("RACE      SCI NAV ENG COM MED  DUR LRN", 18, Ui.Ui.Accent));
        foreach (var r in _s.Data.CrewRaces)
            p.Children.Add(Ui.Ui.Label($"{r.Name,-9} {r.Start[Skill.Science],3} {r.Start[Skill.Navigation],3} {r.Start[Skill.Engineering],3} {r.Start[Skill.Communication],3} {r.Start[Skill.Medicine],3}  {r.Durability,3} {r.LearnRate,3}", 18));
        p.Children.Add(Ui.Ui.Label("Starting skills; maxima 100-250 by aptitude.", 16, Ui.Ui.Dim));
        return p;
    }

    private MenuPage NameCrew(string raceId) => new()
    {
        Title = "Name",
        TextPrompt = "Name for the new crew member:",
        OnSubmitText = name =>
        {
            var r = _s.Crew.Create(name, raceId);
            _stack.Status(r.Message, !r.Ok);
            if (r.Ok) { _stack.Pop(); _stack.Pop(); _stack.Refresh(); }
            _changed();
        },
    };

    private MenuPage Member(CrewMember m) => new()
    {
        Title = m.Name,
        Items = () =>
        {
            var race = _s.Data.CrewRace(m.RaceId);
            var items = Enum.GetValues<Skill>().Select(sk => new MenuEntry($"Train {sk}",
                race.LearnRate == 0 || m.Skills[sk] >= race.Max[sk] ? null : () => Done(_s.Crew.Train(m.Id, sk)),
                race.LearnRate > 0 && m.Skills[sk] < race.Max[sk],
                $"+{race.LearnRate} per session, {CrewRules.TrainingCost} M.U. {_s.Crew.TrainingSessionsToMax(m, sk)} sessions to maximum.",
                $"{m.Skills[sk]}/{race.Max[sk]}")).ToList();
            items.Add(new MenuEntry("Delete this personnel file", () => { Done(_s.Crew.Delete(m.Id)); _stack.Pop(); }, Hint: "Training costs are not refunded."));
            return items;
        },
        Details = () => CrewCard(m),
    };

    public Control CrewCard(CrewMember m)
    {
        var race = _s.Data.CrewRace(m.RaceId);
        var p = Ui.Ui.VStack(2, Ui.Ui.Label(m.Name.ToUpperInvariant(), 22, Ui.Ui.Bright), Ui.Ui.Label(race.Name + (m.IsDead ? " - DEAD" : ""), 18, Ui.Ui.Accent));
        foreach (var sk in Enum.GetValues<Skill>())
            p.Children.Add(Ui.Ui.Row(sk.ToString(), $"{m.Skills[sk],3} / {race.Max[sk]}", null, 19,
                tooltip: SkillTip(sk)));
        p.Children.Add(Ui.Ui.Row("Durability", race.Durability.ToString(), null, 19, "Resistance to injury."));
        p.Children.Add(Ui.Ui.Row("Learning rate", race.LearnRate.ToString(), null, 19, "Skill points gained per training session."));
        p.Children.Add(Ui.Ui.Row("Health", $"{m.Vitality}% - {m.HealthLabel}", m.Vitality < 50 ? Ui.Ui.Bad : Ui.Ui.Good, 19));
        p.Children.Add(Ui.Ui.Row("Sessions trained", m.TrainingSessions.ToString(), null, 19));
        return Ui.Ui.Box(p);
    }

    public static string SkillTip(Skill sk) => sk switch
    {
        Skill.Science => "Science Officer: completeness of sensor scans and analyses; above 150 detects aliens at long range.",
        Skill.Navigation => "Navigator: weapon accuracy, flux detection above 150, re-fixing position after a flux, keeping the TV on course in storms (200+).",
        Skill.Engineering => "Engineer: speed of in-flight repairs.",
        Skill.Communication => "Communications: how much alien speech is translated (+25 if any crew member shares the alien's race, +50 if the officer does).",
        _ => "Doctor: speed of healing and treatment.",
    };

    // ------------------------------------------------------------------ Crew Assignment

    private MenuPage Assignment() => new()
    {
        Title = "Crew Assignment",
        Items = () => Enum.GetValues<CrewRole>().Select(role =>
        {
            var m = _s.State.Assigned(role);
            return new MenuEntry(RoleName(role), () => _stack.Push(AssignRole(role)), Value: m?.Name ?? "-- vacant --",
                Color: m is null ? Ui.Ui.Bad : null);
        }),
        Details = () => Ui.Ui.Box(Ui.Ui.Prose("Every post must be filled before launch. One crew member may hold more than one post. " +
                                              "If an officer dies, the next most capable crew member takes over.", 16)),
    };

    public static string RoleName(CrewRole r) => r switch
    {
        CrewRole.Science => "Science Officer", CrewRole.Communications => "Communications", CrewRole.Doctor => "Doctor",
        _ => r.ToString(),
    };

    private MenuPage AssignRole(CrewRole role) => new()
    {
        Title = RoleName(role),
        Items = () =>
        {
            var skill = CrewRules.RoleSkill[role];
            var list = _s.State.Roster.Where(m => !m.IsDead).Select(m => new MenuEntry(m.Name,
                () => Done(_s.Crew.Assign(role, m.Id), pop: true),
                Value: skill is { } sk ? $"{sk.ToString()[..3].ToUpperInvariant()} {m.Skills[sk]}" : _s.Data.CrewRace(m.RaceId).Name)).ToList();
            list.Add(new MenuEntry("(vacate post)", () => Done(_s.Crew.Assign(role, null), pop: true)));
            return list;
        },
    };

    // ------------------------------------------------------------------ Bank

    private MenuPage Bank() => new()
    {
        Title = "Bank",
        Items = () => [new MenuEntry("Back", () => _stack.Pop())],
        Details = () =>
        {
            var p = Ui.Ui.VStack(2, Ui.Ui.Row("Balance", Ui.Ui.Money(_s.State.Credits), Ui.Ui.Highlight, 22),
                Ui.Ui.Label("Last 10 transactions:", 18, Ui.Ui.Accent));
            foreach (var t in _s.Starport.RecentTransactions)
                p.Children.Add(Ui.Ui.Row($"{GameClock.FormatDay(t.Hour / 24)} {t.Description}", $"{t.Amount:+#,0;-#,0}", t.Amount >= 0 ? Ui.Ui.Good : Ui.Ui.Bad, 17));
            p.Children.Add(Ui.Ui.Label("Interest: 12% per year, credited on docking. No credit purchases.", 16, Ui.Ui.Dim));
            return Ui.Ui.Box(p);
        },
    };

    // ------------------------------------------------------------------ Ship configuration

    private MenuPage ShipConfig() => new()
    {
        Title = "Ship Configuration",
        Items = () =>
        {
            var ship = _s.State.Ship;
            var items = Enum.GetValues<ComponentKind>().Select(k =>
            {
                int cls = ShipRules.Get(ship, k);
                return new MenuEntry(k.ToString(), () => _stack.Push(ComponentPage(k)), Value: cls == 0 ? "none" : $"Class {cls}", Hint: ComponentTip(k));
            }).ToList();
            items.Add(new MenuEntry("Buy cargo pod", () => Done(_s.Starport.BuyPods(1)), ship.CargoPods < _s.Data.Ship.MaxCargoPods,
                "50 m³ per pod, 500 M.U. Each pod adds 10 tons.", $"{ship.CargoPods}/16"));
            items.Add(new MenuEntry("Sell cargo pod", () => Done(_s.Starport.SellPods(1)), ship.CargoPods > 0));
            int repair = _s.Starport.RepairCost();
            items.Add(new MenuEntry("Repair entire ship", repair > 0 ? () => Done(_s.Starport.RepairAll()) : null, repair > 0, Value: repair > 0 ? Ui.Ui.Money(repair) : "OK"));
            items.Add(new MenuEntry("Name ship", () => _stack.Push(NameShip()), Value: string.IsNullOrEmpty(ship.Name) ? "(unnamed)" : ship.Name));
            return items;
        },
        Details = () => ShipCard(),
    };

    public static string ComponentTip(ComponentKind k) => k switch
    {
        ComponentKind.Engine => "Higher classes give better acceleration and fuel economy (0.48 to 0.16 m³ per coordinate).",
        ComponentKind.Shield => "Absorb damage and recharge during encounters. Use energy; useless inside nebulae.",
        ComponentKind.Armor => "Absorbs damage after shields. Heavy (9 x class² tons); must be replaced, not repaired. Works in nebulae.",
        ComponentKind.Missile => "Long range, can be dodged, about three times laser damage, five times the energy.",
        _ => "Short range; cannot be dodged.",
    };

    public Control ShipCard()
    {
        var ship = _s.State.Ship;
        var cat = _s.Data.Ship;
        var p = Ui.Ui.VStack(2,
            Ui.Ui.Label(string.IsNullOrEmpty(ship.Name) ? "UNNAMED SHIP" : "ISS " + ship.Name, 22, Ui.Ui.Bright),
            Ui.Ui.Row("Mass", $"{ShipRules.Mass(ship)} tons", null, 19, "50 + engine 50 + armour 9 x class² + pods 10 each + 5 per weapon/shield system."),
            Ui.Ui.Row("Acceleration", $"{ShipRules.Acceleration(ship)} G", null, 19, "Engine class x 500 / mass."),
            Ui.Ui.Row("Fuel use", $"{ShipRules.FuelPerCoordinate(ship, _s.HasAnalyzedArtifact("tesseract")):0.00} m³/coord", null, 19),
            Ui.Ui.Row("Cargo", $"{ShipRules.CargoUsed(ship):0.#}/{ShipRules.CargoCapacity(ship, cat):0} m³", null, 19),
            Ui.Ui.Row("Endurium", $"{ship.Endurium:0.0} m³", null, 19),
            Ui.Ui.Row("Hull", $"{ship.HullPoints}/{ShipRules.BaseHull}", null, 19),
            Ui.Ui.Row("Armour", $"{ship.ArmorPoints}/{ShipRules.MaxArmorPoints(ship, cat)}", null, 19),
            Ui.Ui.Row("Shields", $"{ship.ShieldPoints}/{ShipRules.MaxShieldPoints(ship, cat)}", null, 19),
            Ui.Ui.Row("Funds", Ui.Ui.Money(_s.State.Credits), Ui.Ui.Highlight, 19));
        foreach (var (sys, dmg) in ship.Damage.Where(kv => kv.Value > 0))
            p.Children.Add(Ui.Ui.Row(sys + " damage", dmg + "%", Ui.Ui.Bad, 19));
        return Ui.Ui.Box(p);
    }

    private MenuPage ComponentPage(ComponentKind k) => new()
    {
        Title = k.ToString(),
        Items = () =>
        {
            int cur = ShipRules.Get(_s.State.Ship, k);
            var items = Enumerable.Range(1, 5).Select(c =>
            {
                var tier = _s.Data.Component(k, c);
                int net = tier.Price - _s.Starport.ResaleOf(k, cur);
                return new MenuEntry($"Class {c}{(c == cur ? " (installed)" : "")}", c == cur ? null : () => Done(_s.Starport.BuyComponent(k, c)),
                    c != cur, $"Price {tier.Price:N0}; mass {tier.Mass} t; strength {tier.Strength}. Net cost after trade-in: {net:N0}. Evidence: {tier.Evidence}.",
                    $"{tier.Price,7:N0}");
            }).ToList();
            if (cur > 0) items.Add(new MenuEntry($"Sell installed class {cur}", () => Done(_s.Starport.SellComponent(k)), Value: $"+{_s.Starport.ResaleOf(k, cur):N0}"));
            return items;
        },
        Details = () => ShipCard(),
    };

    private MenuPage NameShip() => new()
    {
        Title = "Name ship",
        TextPrompt = "Christen your ship (ISS ...):",
        MaxLength = 15,
        OnSubmitText = name => { var r = _s.Starport.NameShip(name); _stack.Status(r.Message, !r.Ok); if (r.Ok) _stack.Pop(); _changed(); },
    };

    // ------------------------------------------------------------------ Trade depot

    private MenuPage Trade() => new()
    {
        Title = "Trade Depot",
        Items = () =>
        [
            new MenuEntry("Sell cargo", () => _stack.Push(SellCargo())),
            new MenuEntry("Buy fuel (10 m³)", () => Done(_s.Starport.BuyFuel(10)), Value: $"{_s.Story.EnduriumPrice() * 10:N0}"),
            new MenuEntry("Buy fuel (fill)", () => Done(_s.Starport.BuyFuel(1000))),
            new MenuEntry("Sell endurium (10 m³)", () => Done(_s.Starport.SellEndurium(10)), _s.State.Ship.Endurium >= 10,
                "Endurium is your fuel. Selling it reduces your range.", Value: $"{_s.Story.EnduriumPrice() * 10:N0}"),
            new MenuEntry("Analyse artifacts", () => _stack.Push(Analyse())),
            new MenuEntry("Buy back artifacts", () => _stack.Push(BuyBack()), _s.State.ArtifactsSold.Count > 0),
        ],
        Details = () => Ui.Ui.Box(Ui.Ui.VStack(2,
            Ui.Ui.Row("Endurium price", $"{_s.Story.EnduriumPrice():N0} M.U./m³", Ui.Ui.Highlight, 19),
            Ui.Ui.Row("Endurium aboard", $"{_s.State.Ship.Endurium:0.0} m³", null, 19),
            Ui.Ui.Row("Hold", $"{ShipRules.CargoUsed(_s.State.Ship):0.#}/{ShipRules.CargoCapacity(_s.State.Ship, _s.Data.Ship):0} m³", null, 19),
            Ui.Ui.Row("Funds", Ui.Ui.Money(_s.State.Credits), Ui.Ui.Highlight, 19))),
    };

    private MenuPage SellCargo() => new()
    {
        Title = "Sell",
        Items = () =>
        {
            var cargo = _s.State.Ship.Cargo.Where(c => c.Kind != CargoKind.Message).ToList();
            if (cargo.Count == 0) return [new MenuEntry("(nothing to sell)", null, false)];
            var items = cargo.Select(c =>
            {
                int unit = _s.Starport.SalePrice(c);
                bool ok = (c.Kind != CargoKind.Artifact || c.Analyzed) && unit > 0;
                return new MenuEntry($"{c.Name} {(c.Kind == CargoKind.Mineral ? $"{c.Quantity:0.#} m³" : c.Quantity > 1 ? $"x{c.Quantity:0}" : "")}",
                    ok ? () => Done(_s.Starport.SellCargo(c, c.Quantity)) : null, ok,
                    c.Kind == CargoKind.Artifact && !c.Analyzed ? "Must be analysed first." : null, ok ? $"{(int)(unit * c.Quantity):N0}" : "--");
            }).ToList();
            int all = cargo.Where(c => c.Kind is CargoKind.Mineral or CargoKind.Debris).Sum(c => (int)(_s.Starport.SalePrice(c) * c.Quantity));
            if (all > 0) items.Insert(0, new MenuEntry("Sell all minerals and debris", () =>
            {
                foreach (var c in _s.State.Ship.Cargo.Where(c => c.Kind is CargoKind.Mineral or CargoKind.Debris).ToList()) _s.Starport.SellCargo(c, c.Quantity);
                Done(ActionResult.Success($"Sold all minerals and debris for {all:N0} M.U."));
            }, Value: $"{all:N0}"));
            return items;
        },
    };

    private MenuPage Analyse() => new()
    {
        Title = "Analyse",
        Items = () =>
        {
            var arts = _s.State.Ship.Cargo.Where(c => c.Kind == CargoKind.Artifact).ToList();
            if (arts.Count == 0) return [new MenuEntry("(no artifacts aboard)", null, false)];
            return arts.Select(a => new MenuEntry(a.Name, a.Analyzed ? null : () => Done(_s.Starport.AnalyzeArtifact(a)), !a.Analyzed,
                a.Analyzed ? _s.Data.Artifact(a.Id).Description : "Analysis fee: 500 M.U.", a.Analyzed ? $"{_s.Data.Artifact(a.Id).Value:N0}" : "500"));
        },
    };

    private MenuPage BuyBack() => new()
    {
        Title = "Buy back",
        Items = () => _s.State.ArtifactsSold.Select(id => new MenuEntry(_s.Data.Artifact(id).Name,
            () => Done(_s.Starport.BuyBackArtifact(id)), Value: $"{_s.Data.Artifact(id).Value * 6 / 5:N0}")),
    };

    // ------------------------------------------------------------------ Docking bay

    private MenuPage Docking() => new()
    {
        Title = "Docking Bay",
        Items = () =>
        [
            new MenuEntry("LAUNCH", () => Done(_s.Starport.Launch()), _s.Starport.PreflightProblems().Count == 0),
        ],
        Details = () =>
        {
            var problems = _s.Starport.PreflightProblems();
            var p = Ui.Ui.VStack(4, Ui.Ui.Label("PRE-FLIGHT CHECKLIST", 20, Ui.Ui.Accent));
            if (problems.Count == 0) p.Children.Add(Ui.Ui.Label("All systems ready. Cleared for launch.", 20, Ui.Ui.Good));
            foreach (var pr in problems) p.Children.Add(Ui.Ui.Label("* " + pr, 19, Ui.Ui.Bad));
            return Ui.Ui.Box(p);
        },
    };
}
