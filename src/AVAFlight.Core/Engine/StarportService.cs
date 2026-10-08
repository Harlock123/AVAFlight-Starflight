using AVAFlight.Core.Data;
using AVAFlight.Core.Galaxy;
using AVAFlight.Core.Model;

namespace AVAFlight.Core.Engine;

/// <summary>
/// Starport modules: Operations, Trade Depot, Personnel (via CrewService), Crew Assignment, Bank,
/// Ship Configuration and the Docking Bay. All credit arithmetic flows through Credit/Debit so the
/// bank shows the last transactions like the original.
/// </summary>
public sealed class StarportService(GameSession s)
{
    /// <summary>Artifact analysis fee (decompiled text "ANALYSIS IS 500 MU").</summary>
    public const int AnalysisFee = 500;
    /// <summary>Bank interest: 12% simple per 300-day year, credited on docking (decompiled INT%).</summary>
    public const double InterestRate = 0.12;
    /// <summary>RECONSTRUCTION: component resale value fraction (manual: always lower than price, pods excepted).</summary>
    public const double ResaleFraction = 0.5;
    public const int TerrainVehicleCost = 5000;

    public void Credit(int amount, string what)
    {
        if (amount <= 0) return;
        s.State.Credits += amount;
        s.State.Stats.CreditsEarned += amount;
        AddTransaction(what, amount);
    }

    public void Debit(int amount, string what)
    {
        if (amount <= 0) return;
        s.State.Credits -= amount;
        AddTransaction(what, -amount);
    }

    private void AddTransaction(string what, int amount)
    {
        s.State.Transactions.Add(new BankTransaction(s.State.Clock.Hours, what, amount));
        if (s.State.Transactions.Count > 50) s.State.Transactions.RemoveAt(0);
    }

    public IEnumerable<BankTransaction> RecentTransactions => s.State.Transactions.TakeLast(10).Reverse();

    // ------------------------------------------------------------------ Ship configuration

    public int PriceOf(ComponentKind kind, int cls) => s.Data.Component(kind, cls).Price;
    public int ResaleOf(ComponentKind kind, int cls) => cls == 0 ? 0 : (int)(PriceOf(kind, cls) * ResaleFraction);

    /// <summary>Buys a component class; the old one is traded in at resale value.</summary>
    public ActionResult BuyComponent(ComponentKind kind, int cls)
    {
        if (cls is < 1 or > 5) return ActionResult.Fail("No such class.");
        int current = ShipRules.Get(s.State.Ship, kind);
        if (current == cls) return ActionResult.Fail($"Class {cls} {kind} is already installed.");
        int net = PriceOf(kind, cls) - ResaleOf(kind, current);
        if (s.State.Credits < net) return ActionResult.Fail($"Insufficient funds: need {net:N0} M.U.");
        if (net > 0) Debit(net, $"Class {cls} {kind}"); else Credit(-net, $"Class {cls} {kind} (trade-in)");
        ShipRules.Set(s.State.Ship, kind, cls);
        RefreshDefences(kind);
        s.PlaySound("purchase");
        return ActionResult.Success($"Class {cls} {kind} installed.");
    }

    public ActionResult SellComponent(ComponentKind kind)
    {
        int current = ShipRules.Get(s.State.Ship, kind);
        if (current == 0) return ActionResult.Fail($"No {kind} installed.");
        Credit(ResaleOf(kind, current), $"Sold class {current} {kind}");
        ShipRules.Set(s.State.Ship, kind, 0);
        RefreshDefences(kind);
        return ActionResult.Success($"Class {current} {kind} sold.");
    }

    private void RefreshDefences(ComponentKind kind)
    {
        var ship = s.State.Ship;
        if (kind == ComponentKind.Armor) ship.ArmorPoints = ShipRules.MaxArmorPoints(ship, s.Data.Ship);
        if (kind == ComponentKind.Shield) ship.ShieldPoints = ShipRules.MaxShieldPoints(ship, s.Data.Ship);
    }

    public ActionResult BuyPods(int count)
    {
        var cat = s.Data.Ship;
        if (count <= 0) return ActionResult.Fail("Nothing to buy.");
        if (s.State.Ship.CargoPods + count > cat.MaxCargoPods) return ActionResult.Fail($"The ship holds at most {cat.MaxCargoPods} cargo pods.");
        int cost = count * cat.CargoPodPrice;
        if (s.State.Credits < cost) return ActionResult.Fail("Insufficient funds.");
        Debit(cost, $"{count} cargo pod(s)");
        s.State.Ship.CargoPods += count;
        s.PlaySound("purchase");
        return ActionResult.Success($"{count} cargo pod(s) installed ({s.State.Ship.CargoPods} total).");
    }

    public ActionResult SellPods(int count)
    {
        var ship = s.State.Ship;
        var cat = s.Data.Ship;
        if (count <= 0 || count > ship.CargoPods) return ActionResult.Fail("Not that many pods installed.");
        double capAfter = (ship.CargoPods - count) * cat.CargoPodCapacity;
        if (ShipRules.CargoUsed(ship) > capAfter) return ActionResult.Fail("The pods are not empty. Sell some cargo first.");
        ship.CargoPods -= count;
        Credit(count * cat.CargoPodPrice, $"Sold {count} cargo pod(s)");
        return ActionResult.Success($"{count} pod(s) sold.");
    }

    public ActionResult NameShip(string name)
    {
        name = name.Trim().ToUpperInvariant();
        if (name.Length is 0 or > 15) return ActionResult.Fail("Ship names are 1-15 characters.");
        s.State.Ship.Name = name;
        return ActionResult.Success($"Christened ISS {name}.");
    }

    /// <summary>
    /// Dry-dock repair cost. Fan figures: hull ~1,000 for a full repair, comms/sensors ~10,000,
    /// other systems ~1.25x part price at 100% damage; armour must be replaced (proportional price).
    /// </summary>
    public int RepairCost()
    {
        var ship = s.State.Ship;
        double cost = (ShipRules.BaseHull - ship.HullPoints) * s.Data.Ship.RepairPricePerPoint;
        foreach (var (sys, dmg) in ship.Damage)
        {
            if (dmg <= 0) continue;
            double full = sys switch
            {
                ShipSystem.Sensors or ShipSystem.Comms => 10_000,
                ShipSystem.Engines => 1.25 * PriceOf(ComponentKind.Engine, Math.Max(1, ship.Engine)),
                ShipSystem.Shields => ship.Shield == 0 ? 0 : 1.25 * PriceOf(ComponentKind.Shield, ship.Shield),
                ShipSystem.Missiles => ship.Missile == 0 ? 0 : 1.25 * PriceOf(ComponentKind.Missile, ship.Missile),
                _ => ship.Laser == 0 ? 0 : 1.25 * PriceOf(ComponentKind.Laser, ship.Laser),
            };
            cost += full * dmg / 100.0;
        }
        int maxArmor = ShipRules.MaxArmorPoints(ship, s.Data.Ship);
        if (maxArmor > 0 && ship.ArmorPoints < maxArmor)
            cost += PriceOf(ComponentKind.Armor, ship.Armor) * (maxArmor - ship.ArmorPoints) / (double)maxArmor;
        return (int)Math.Ceiling(cost);
    }

    public ActionResult RepairAll()
    {
        int cost = RepairCost();
        if (cost == 0) return ActionResult.Fail("The ship needs no repairs.");
        if (s.State.Credits < cost) return ActionResult.Fail($"Repairs cost {cost:N0} M.U.; insufficient funds.");
        Debit(cost, "Ship repairs");
        var ship = s.State.Ship;
        ship.HullPoints = ShipRules.BaseHull;
        foreach (var k in ship.Damage.Keys.ToList()) ship.Damage[k] = 0;
        ship.ArmorPoints = ShipRules.MaxArmorPoints(ship, s.Data.Ship);
        ship.ShieldPoints = ShipRules.MaxShieldPoints(ship, s.Data.Ship);
        return ActionResult.Success("The entire ship has been repaired.");
    }

    // ------------------------------------------------------------------ Trade Depot

    public int SalePrice(CargoItem item) => item.Kind switch
    {
        CargoKind.Mineral => item.Id == "endurium" ? s.Story.EnduriumPrice() : s.Data.Mineral(item.Id).ValuePerUnit,
        CargoKind.Lifeform => s.State.SoldSpecies.Contains(item.Id) ? 0 : item.ValuePerUnit,
        CargoKind.Artifact => item.Analyzed ? s.Data.Artifact(item.Id).Value : 0,
        CargoKind.Debris => item.ValuePerUnit,
        _ => 0,
    };

    public ActionResult SellCargo(CargoItem item, double quantity)
    {
        if (!s.State.Ship.Cargo.Contains(item)) return ActionResult.Fail("That item is not aboard.");
        if (item.Kind == CargoKind.Message) return ActionResult.Fail("Messages and recordings have no market value.");
        if (item.Kind == CargoKind.Artifact && !item.Analyzed) return ActionResult.Fail("Artifacts must be analysed before they can be valued.");
        if (item.Kind == CargoKind.Lifeform && s.State.SoldSpecies.Contains(item.Id)) return ActionResult.Fail("Duplicate specimen: Interstel already has this species.");
        quantity = Math.Min(quantity, item.Quantity);
        if (quantity <= 0) return ActionResult.Fail("Nothing to sell.");
        int value = (int)Math.Round(SalePrice(item) * quantity);
        item.Quantity -= quantity;
        if (item.Quantity <= 1e-9) s.State.Ship.Cargo.Remove(item);
        if (item.Kind == CargoKind.Lifeform) s.State.SoldSpecies.Add(item.Id);
        if (item.Kind == CargoKind.Artifact) s.State.ArtifactsSold.Add(item.Id);
        Credit(value, $"Sold {quantity:0.#} {item.Name}");
        s.PlaySound("purchase");
        return ActionResult.Success($"Sold {quantity:0.#} {item.Name} for {value:N0} M.U.");
    }

    public ActionResult SellEndurium(double m3)
    {
        var ship = s.State.Ship;
        m3 = Math.Min(m3, ship.Endurium);
        if (m3 <= 0) return ActionResult.Fail("No endurium to sell.");
        int value = (int)Math.Round(m3 * s.Story.EnduriumPrice());
        ship.Endurium -= m3;
        Credit(value, $"Sold {m3:0.#} m³ endurium");
        return ActionResult.Success($"Sold {m3:0.#} m³ of endurium for {value:N0} M.U.");
    }

    /// <summary>
    /// Buys fuel (endurium). RECONSTRUCTION: priced at the current Endurium market rate, so fuel and
    /// the trade good stay one consistent resource; see FIDELITY.md.
    /// </summary>
    public ActionResult BuyFuel(double m3)
    {
        var ship = s.State.Ship;
        double room = ShipRules.EnduriumCapacity(ship, s.Data.Ship) - ship.Endurium;
        m3 = Math.Min(m3, room);
        if (m3 <= 0.0001) return ActionResult.Fail("No room for more endurium.");
        int cost = (int)Math.Ceiling(m3 * s.Story.EnduriumPrice());
        if (cost > s.State.Credits)
        {
            m3 = Math.Floor(s.State.Credits / (double)s.Story.EnduriumPrice() * 10) / 10;
            if (m3 <= 0) return ActionResult.Fail("Insufficient funds.");
            cost = (int)Math.Ceiling(m3 * s.Story.EnduriumPrice());
        }
        Debit(cost, $"Fuel: {m3:0.#} m³ endurium");
        ship.Endurium += m3;
        s.PlaySound("purchase");
        return ActionResult.Success($"Purchased {m3:0.#} m³ of endurium for {cost:N0} M.U.");
    }

    public ActionResult AnalyzeArtifact(CargoItem item)
    {
        if (item.Kind != CargoKind.Artifact) return ActionResult.Fail("Only artifacts can be analysed.");
        if (item.Analyzed) return ActionResult.Fail("Already analysed.");
        if (s.State.Credits < AnalysisFee) return ActionResult.Fail("Analysis is 500 M.U.; insufficient funds.");
        Debit(AnalysisFee, $"Analysis: {item.Name}");
        item.Analyzed = true;
        var def = s.Data.Artifact(item.Id);
        item.ValuePerUnit = def.Value;
        s.Story.Log(LogCategory.Artifact, $"Analysis of the {def.Name}: {def.Description} Value {def.Value:N0} M.U.");
        return ActionResult.Success($"{def.Name}: {def.Description} Estimated value {def.Value:N0} M.U.");
    }

    /// <summary>Sold artifacts can be bought back (manual). RECONSTRUCTION: at 120% of value.</summary>
    public ActionResult BuyBackArtifact(string id)
    {
        if (!s.State.ArtifactsSold.Contains(id)) return ActionResult.Fail("Interstel does not hold that artifact.");
        var def = s.Data.Artifact(id);
        int price = def.Value * 6 / 5;
        if (s.State.Credits < price) return ActionResult.Fail("Insufficient funds.");
        if (ShipRules.CargoFree(s.State.Ship, s.Data.Ship) < 1) return ActionResult.Fail("No cargo space.");
        Debit(price, $"Bought back {def.Name}");
        s.State.ArtifactsSold.Remove(id);
        s.Planets.AddArtifact(id, analyzed: true);
        return ActionResult.Success($"{def.Name} returned to your hold.");
    }

    // ------------------------------------------------------------------ Operations

    public IEnumerable<NoticeDefinition> Notices()
    {
        var list = s.Story.AvailableNotices().ToList();
        foreach (var n in list) s.State.NoticesSeen.Add(n.Id);
        s.State.OperationsEvaluated = true;
        return list;
    }

    public static (bool Ok, int Score, List<string> Reasons) ColonyEvaluation(Planet p)
    {
        var reasons = new List<string>();
        int score = 0;
        if (p.Temperature is Temperature.Temperate) score += 2;
        else if (p.Temperature is Temperature.Cold or Temperature.Hot) score += 1;
        else reasons.Add("temperature outside the acceptable band");
        if (p.Gravity >= 2.0) reasons.Add("gravity of 2.0 G or more");
        else if (p.Gravity is >= 0.7 and <= 1.3) score += 2;
        else score += 1;
        if (!p.AtmosphereComposition.Contains("Oxygen")) reasons.Add("no oxygen in the atmosphere");
        if (p.Hydrosphere != Hydrosphere.Water) reasons.Add("no free water");
        if (p.Weather is Weather.Violent or Weather.VeryViolent) reasons.Add("violent weather");
        if (p.Type is PlanetType.GasGiant or PlanetType.Crystal) reasons.Add("unsuitable world type");
        return (reasons.Count == 0, score, reasons);
    }

    /// <summary>
    /// Colony recommendation for a logged planet. Criteria are documented (manual); reward and fine
    /// amounts are fan figures / reconstruction (35,000–55,000 reward, 5,000 fine).
    /// </summary>
    public ActionResult RecommendColony(int planetId)
    {
        var rec = s.State.PlanetRecord(planetId);
        if (!rec.Logged) return ActionResult.Fail("Only planets entered in the ship's log can be recommended.");
        if (rec.Recommended) return ActionResult.Fail("That planet has already been evaluated.");
        rec.Recommended = true;
        var p = s.Galaxy.Planet(planetId);
        var (ok, score, reasons) = ColonyEvaluation(p);
        var sys = s.Galaxy.System(p.SystemId);
        if (ok)
        {
            int reward = 35_000 + score * 5_000;
            Credit(reward, $"Colony recommendation {sys.X},{sys.Y} #{p.Orbit}");
            s.Story.Log(LogCategory.Mission, $"Colony world recommended: {sys.Label} planet {p.Orbit}. Reward {reward:N0} M.U.");
            return ActionResult.Success($"Interstel accepts your recommendation. Reward: {reward:N0} M.U.");
        }
        const int fine = 5_000;
        Debit(Math.Min(fine, s.State.Credits), "Fine: unsuitable colony recommendation");
        return ActionResult.Fail($"Recommendation rejected ({string.Join(", ", reasons)}). You are fined {fine:N0} M.U.");
    }

    // ------------------------------------------------------------------ Docking

    /// <summary>Pre-flight checks from the decompiled CHKFLIGHT messages.</summary>
    public IReadOnlyList<string> PreflightProblems()
    {
        var p = new List<string>();
        foreach (CrewRole role in Enum.GetValues<CrewRole>())
            if (s.State.Assigned(role) is not { IsDead: false }) { p.Add("Report to Crew Assignment: every post needs a crew member."); break; }
        if (string.IsNullOrWhiteSpace(s.State.Ship.Name)) p.Add("Report to Ship Configuration: unchristened ships may not leave Starport.");
        if (s.State.Ship.Engine == 0) p.Add("Report to Ship Configuration to purchase engines.");
        if (s.State.Ship.Endurium < 1) p.Add("Report to the Trade Depot to purchase fuel.");
        if (!s.State.OperationsEvaluated) p.Add("Report to Operations for your briefing.");
        return p;
    }

    public ActionResult Launch()
    {
        if (s.State.Location.Mode != GameMode.Starport) return ActionResult.Fail("Not docked.");
        var problems = PreflightProblems();
        if (problems.Count > 0) return ActionResult.Fail(problems[0]);
        var arth = s.Galaxy.SystemAt(125, 100)!;
        var arthPlanet = arth.Planets.First(p => p.Name == "Arth");
        s.State.Location.SystemId = arth.Id;
        s.State.Location.OrbitPlanetId = arthPlanet.Id;
        s.State.Location.HyperX = arth.X;
        s.State.Location.HyperY = arth.Y;
        s.State.Ship.Endurium = Math.Max(0, s.State.Ship.Endurium - ShipRules.LandingFuelPerG * arthPlanet.Gravity);
        s.SetMode(GameMode.Orbit);
        s.Emit(EventKind.Info, $"ISS {s.State.Ship.Name} launched from Starport. Now in orbit of Arth.", "launch");
        return ActionResult.Success("Launched.");
    }

    /// <summary>Docks at Starport (from Arth orbit): heals crew, credits interest, refreshes shields.</summary>
    public ActionResult Dock()
    {
        var planet = s.CurrentPlanet;
        if (planet?.Name != "Arth") return ActionResult.Fail("Starport is in orbit of Arth.");
        CreditInterest();
        s.Crew.HealAllAtStarport();
        s.State.Ship.ShieldsUp = false;
        s.State.Ship.WeaponsArmed = false;
        s.State.Ship.ShieldPoints = ShipRules.MaxShieldPoints(s.State.Ship, s.Data.Ship);
        s.State.Location.OrbitPlanetId = null;
        s.SetMode(GameMode.Starport);
        if (!s.State.Ship.HasTerrainVehicle)
        {
            Debit(Math.Min(TerrainVehicleCost, s.State.Credits), "Loss of terrain vehicle");
            s.State.Ship.HasTerrainVehicle = true;
        }
        s.Emit(EventKind.Info, "Docked at Starport.", "land");
        return ActionResult.Success("Docked.");
    }

    public void CreditInterest()
    {
        long hours = s.State.Clock.Hours - s.State.LastInterestHour;
        s.State.LastInterestHour = s.State.Clock.Hours;
        int interest = (int)(s.State.Credits * InterestRate * (hours / 24.0) / 300.0);
        if (interest > 0) Credit(interest, "Interest (12%)");
    }

    /// <summary>
    /// Distress call when stranded: towed home in stasis for a distance-based fee (manual).
    /// RECONSTRUCTION: 1,000 M.U. + 150 per coordinate; time passes at towing speed.
    /// </summary>
    public ActionResult Distress()
    {
        if (s.State.Location.Mode is GameMode.Starport or GameMode.Encounter or GameMode.GameOver)
            return ActionResult.Fail("A distress call is not possible now.");
        double dist = GalaxyMap.Distance(s.State.Location.HyperX, s.State.Location.HyperY, 125, 100);
        int fee = 1000 + (int)(dist * 150);
        s.AdvanceHours(Math.Max(24, dist * 4));
        if (s.IsOver) return ActionResult.Fail("Rescue arrived too late.");
        int paid = Math.Min(fee, s.State.Credits);
        Debit(paid, "Towing charges");
        var arth = s.Galaxy.SystemAt(125, 100)!;
        s.State.Location.SystemId = arth.Id;
        s.State.Location.HyperX = arth.X;
        s.State.Location.HyperY = arth.Y;
        s.State.Location.OrbitPlanetId = arth.Planets.First(p => p.Name == "Arth").Id;
        s.State.Location.VelX = s.State.Location.VelY = 0;
        s.State.Location.CruiseX = s.State.Location.CruiseY = null;
        s.Story.Log(LogCategory.Mission, $"Towed to Starport in stasis. Towing charges: {paid:N0} M.U.");
        Dock();
        if (paid < fee) s.Emit(EventKind.Danger, $"Towing charges of {fee:N0} M.U. exceeded your funds; your account is empty.");
        return ActionResult.Success($"Towed home. Charges: {paid:N0} M.U.");
    }
}
