using AVAFlight.Core.Data;
using AVAFlight.Core.Model;

namespace AVAFlight.Core.Engine;

/// <summary>Player controls for one combat frame.</summary>
public readonly record struct CombatInput(int Turn, bool Thrust, bool Fire);

/// <summary>
/// Encounters and tactical combat. Time model: REAL-TIME, as in the original ("Starflight runs in
/// real time"), simulated in fixed 1/30 s steps so a recorded input sequence replays exactly.
/// Player weapons fire along the ship's nose; the ship computer picks lasers at short range and
/// missiles at long range (manual). Values: fan measurements where available, else reconstruction.
/// </summary>
public sealed class CombatService(GameSession s)
{
    public const double StepSeconds = 1.0 / 30;
    public const double LaserRange = 30;
    public const double MissileSpeed = 70;
    public const double ArmingSeconds = 3;
    /// <summary>Game hours per real combat second (encounters take minutes of game time).</summary>
    public const double HoursPerSecond = 0.01;

    private double _accumulator;

    public EncounterState? Encounter => s.State.Encounter;
    public AlienRaceDefinition? Race => Encounter is { } e ? s.Data.Race(e.RaceId) : null;

    // ------------------------------------------------------------------ setup

    public void BeginEncounter(string raceId)
    {
        var race = s.Data.Race(raceId);
        var rel = s.State.Relation(raceId, race.BaseAttitude);
        rel.Encounters++;
        var e = new EncounterState { RaceId = raceId, PlayerHeading = -Math.PI / 2 };
        int fleet = Math.Max(1, race.FleetSize + s.Rng.Next(-1, 2));
        if (raceId == "uhlek" && s.Story.HasFlag(StoryService.FlagUhlekMindDestroyed)) fleet = Math.Max(1, fleet / 2);
        double shieldScale = 1 + Math.Min(1.0, rel.ShipsDestroyed * 0.1); // fan: enemy shields grow with kills
        for (int i = 0; i < fleet; i++)
        {
            double a = s.Rng.NextDouble() * Math.PI * 2;
            double r = 55 + s.Rng.Next(30);
            e.Ships.Add(new AlienShip
            {
                Id = i, X = Math.Cos(a) * r, Y = Math.Sin(a) * r, Heading = a + Math.PI,
                Hull = race.Ship.Hull, MaxHull = race.Ship.Hull, Shield = (int)(race.Ship.Shield * shieldScale),
                Cooldown = 1 + s.Rng.NextDouble() * 2,
            });
        }
        e.Hostile = DecideHostility(race, rel);
        e.AlienHailedFirst = race.Talks && !e.Hostile && s.Rng.Chance(0.5);
        e.Patience = 6 + Math.Max(0, rel.Attitude) / 20;
        s.State.Location.ModeBeforeEncounter = s.State.Location.Mode;
        s.State.Encounter = e;
        s.Navigation.ClearCourse();
        s.SetMode(GameMode.Encounter);
        string what = race.Id is "nomad" or "minstrel" or "unknown" ? race.Name.ToLowerInvariant() : $"{fleet} {race.Name} vessel{(fleet > 1 ? "s" : "")}";
        bool known = rel.Contacted || s.Navigation.LongRangeSensors;
        s.Emit(EventKind.Warning, $"Encounter! Sensors detect {(known ? what : $"{fleet} unidentified vessel{(fleet > 1 ? "s" : "")}")}.", "warning");
        if (e.AlienHailedFirst) s.Emit(EventKind.Info, "We are being hailed.", "comms");
    }

    private bool DecideHostility(AlienRaceDefinition race, RaceRelation rel)
    {
        if (race.AlwaysHostile || rel.PermanentEnemy) return true;
        if (race.Id == "velox" && s.HasArtifact("crystal-orb")) return true; // war over the Small Egg
        if (race.Enemy is not null && s.Crew.CrewHasRace(race.Enemy) && race.Id == "thrynn") return true;
        if (race.Ship.Aggression <= 0) return false;
        // Repeated combat makes friendly relations less likely (manual).
        double p = race.Ship.Aggression * (rel.Attitude < 0 ? 1 + -rel.Attitude / 50.0 : 0.5) * (rel.ShipsDestroyed > 0 ? 1.5 : 1);
        if (s.HasArtifact("shimmering-ball")) p *= 0.5;
        return rel.Attitude < -30 || s.Rng.NextDouble() < Math.Min(0.9, p * 0.6);
    }

    // ------------------------------------------------------------------ ship systems (Navigator)

    public ActionResult ToggleShields()
    {
        var ship = s.State.Ship;
        if (ship.Shield == 0) return ActionResult.Fail("No shields installed.");
        if (!ship.ShieldsUp && s.Galaxy.InNebula(s.State.Location.HyperX, s.State.Location.HyperY) && s.State.Location.Mode != GameMode.Starport)
            return ActionResult.Fail("Shields do not function inside a nebula.");
        if (!ship.ShieldsUp && ship.Damage[ShipSystem.Shields] > 0 && s.Rng.Next(100) < ship.Damage[ShipSystem.Shields])
            return ActionResult.Fail("Shield generator failure! (damaged)");
        ship.ShieldsUp = !ship.ShieldsUp;
        NoteHostileGesture();
        return ActionResult.Success(ship.ShieldsUp ? "Shields raised." : "Shields lowered.");
    }

    public ActionResult ToggleWeapons()
    {
        var ship = s.State.Ship;
        if (ship.Laser == 0 && ship.Missile == 0) return ActionResult.Fail("No weapons installed.");
        ship.WeaponsArmed = !ship.WeaponsArmed;
        if (ship.WeaponsArmed && Encounter is { } e) e.ArmingTimer = ArmingSeconds;
        NoteHostileGesture();
        return ActionResult.Success(ship.WeaponsArmed ? "Weapons arming." : "Weapons disarmed.");
    }

    /// <summary>Aliens scan for raised shields and armed weapons and read them as hostile (manual).</summary>
    private void NoteHostileGesture()
    {
        if (Encounter is not { } e || Race is not { } race) return;
        if (s.State.Ship.ShieldsUp || s.State.Ship.WeaponsArmed)
        {
            var rel = s.State.Relation(race.Id);
            rel.Attitude = Math.Max(-100, rel.Attitude - 3);
        }
    }

    // ------------------------------------------------------------------ simulation

    public void Tick(CombatInput input, double realSeconds)
    {
        if (Encounter is null || s.IsOver) return;
        _accumulator += Math.Min(0.25, realSeconds);
        while (_accumulator >= StepSeconds && Encounter is not null && !s.IsOver)
        {
            _accumulator -= StepSeconds;
            Step(input);
        }
    }

    public void Step(CombatInput input)
    {
        var e = Encounter!;
        var race = Race!;
        var ship = s.State.Ship;
        double dt = StepSeconds;
        e.Elapsed += dt;
        s.AdvanceHours(HoursPerSecond * dt);
        if (s.IsOver) return;

        // Player movement: rotate, thrust with acceleration-based top speed, light drag.
        e.PlayerHeading += input.Turn * Math.PI * dt;
        double accel = ShipRules.Acceleration(ship);
        double topSpeed = 12 + accel * 2.2;
        if (input.Thrust && ship.Endurium > 0)
        {
            e.PlayerVx += Math.Cos(e.PlayerHeading) * (20 + accel * 4) * dt;
            e.PlayerVy += Math.Sin(e.PlayerHeading) * (20 + accel * 4) * dt;
        }
        double sp = Math.Sqrt(e.PlayerVx * e.PlayerVx + e.PlayerVy * e.PlayerVy);
        if (sp > topSpeed) { e.PlayerVx *= topSpeed / sp; e.PlayerVy *= topSpeed / sp; }
        e.PlayerVx *= 1 - 0.4 * dt;
        e.PlayerVy *= 1 - 0.4 * dt;
        e.PlayerX += e.PlayerVx * dt;
        e.PlayerY += e.PlayerVy * dt;

        if (e.ArmingTimer > 0) e.ArmingTimer -= dt;
        if (e.PlayerCooldown > 0) e.PlayerCooldown -= dt;
        if (input.Fire) PlayerFire(e, race);

        foreach (var a in e.Ships.Where(a => !a.Destroyed)) AlienAct(e, race, a, dt);
        MoveProjectiles(e, race, dt);
        for (int i = e.Beams.Count - 1; i >= 0; i--)
        {
            var b = e.Beams[i];
            if (b.Life - dt <= 0) e.Beams.RemoveAt(i); else e.Beams[i] = b with { Life = b.Life - dt };
        }
        if (s.IsOver) return;

        // Shield recharge during encounters (manual: shields recharge). RECONSTRUCTION: 2% of max per second.
        int maxShield = ShipRules.MaxShieldPoints(ship, s.Data.Ship);
        if (ship.ShieldsUp && ship.ShieldPoints < maxShield)
            ship.ShieldPoints = Math.Min(maxShield, ship.ShieldPoints + Math.Max(1, (int)(maxShield * 0.02 * dt + 0.5)));

        CheckEnd(e);
    }

    private (AlienShip? Target, double Dist, double Angle) NearestInArc(EncounterState e, double arc)
    {
        AlienShip? best = null; double bestD = double.MaxValue, bestAng = 0;
        foreach (var a in e.Ships.Where(a => !a.Destroyed))
        {
            double dx = a.X - e.PlayerX, dy = a.Y - e.PlayerY;
            double d = Math.Sqrt(dx * dx + dy * dy);
            double ang = AngleDiff(Math.Atan2(dy, dx), e.PlayerHeading);
            if (Math.Abs(ang) <= arc && d < bestD) { best = a; bestD = d; bestAng = ang; }
        }
        return (best, bestD, bestAng);
    }

    public static double AngleDiff(double a, double b)
    {
        double d = a - b;
        while (d > Math.PI) d -= 2 * Math.PI;
        while (d < -Math.PI) d += 2 * Math.PI;
        return d;
    }

    private void PlayerFire(EncounterState e, AlienRaceDefinition race)
    {
        var ship = s.State.Ship;
        if (!ship.WeaponsArmed || e.ArmingTimer > 0 || e.PlayerCooldown > 0) return;
        var (target, dist, _) = NearestInArc(e, 0.45);
        bool useLaser = ship.Laser > 0 && (ship.Missile == 0 || (target is not null && dist <= LaserRange));
        if (useLaser)
        {
            if (WeaponFails(ShipSystem.Lasers)) { e.PlayerCooldown = 0.5; return; }
            ship.Endurium = Math.Max(0, ship.Endurium - ShipRules.LaserShotFuel);
            e.PlayerCooldown = 0.6;
            double ex = e.PlayerX + Math.Cos(e.PlayerHeading) * LaserRange, ey = e.PlayerY + Math.Sin(e.PlayerHeading) * LaserRange;
            s.PlaySound("laser");
            if (target is not null && dist <= LaserRange)
            {
                ex = target.X; ey = target.Y;
                // Navigator sets weapon accuracy (manual).
                double hit = 0.55 + 0.45 * CrewRules.Efficiency(s.Skill(CrewRole.Navigator));
                if (s.Rng.NextDouble() < hit)
                {
                    int dmg = s.Data.Component(Data.ComponentKind.Laser, ship.Laser).Strength;
                    dmg = (int)(dmg * Math.Max(0.3, 1 - 0.05 * (dist / 3))); // fan: laser damage falls with distance
                    DamageAlien(e, race, target, dmg);
                }
            }
            e.Beams.Add(new BeamFlash(e.PlayerX, e.PlayerY, ex, ey, true, 0.15));
        }
        else if (ship.Missile > 0)
        {
            if (WeaponFails(ShipSystem.Missiles)) { e.PlayerCooldown = 1; return; }
            ship.Endurium = Math.Max(0, ship.Endurium - ShipRules.MissileShotFuel);
            e.PlayerCooldown = 1.5;
            int dmg = s.Data.Component(Data.ComponentKind.Missile, ship.Missile).Strength;
            double spread = (1 - CrewRules.Efficiency(s.Skill(CrewRole.Navigator))) * 0.15 * (s.Rng.NextDouble() - 0.5);
            double h = e.PlayerHeading + spread;
            e.Projectiles.Add(new Projectile
            {
                Kind = ProjectileKind.Missile, X = e.PlayerX, Y = e.PlayerY, Vx = Math.Cos(h) * MissileSpeed + e.PlayerVx,
                Vy = Math.Sin(h) * MissileSpeed + e.PlayerVy, Damage = dmg, FromPlayer = true, Life = 3,
            });
            s.PlaySound("missile");
        }
        if (!e.Hostile && race.Talks)
        {
            e.Hostile = true;
            var rel = s.State.Relation(race.Id);
            rel.Attitude = Math.Max(-100, rel.Attitude - 30);
            e.Talking = false;
            s.Emit(EventKind.Warning, $"The {race.Name} return fire!");
        }
        else e.Hostile = true;
    }

    private bool WeaponFails(ShipSystem sys)
    {
        int dmg = s.State.Ship.Damage[sys];
        if (dmg > 0 && s.Rng.Next(100) < dmg) { s.Emit(EventKind.Warning, $"{sys} malfunction!"); return true; }
        return false;
    }

    private void DamageAlien(EncounterState e, AlienRaceDefinition race, AlienShip a, int dmg)
    {
        int toShield = Math.Min(a.Shield, dmg);
        a.Shield -= toShield;
        a.Hull -= dmg - toShield;
        if (a.Hull <= 0 && !a.Destroyed)
        {
            a.Destroyed = true;
            e.DebrisCount++;
            var rel = s.State.Relation(race.Id);
            rel.ShipsDestroyed++;
            rel.Attitude = Math.Max(-100, rel.Attitude - 20);
            s.State.Stats.ShipsDestroyed++;
            s.Emit(EventKind.Success, $"{race.Name} vessel destroyed!", "explosion");
        }
        else if (!e.Surrendered && race.Talks && e.Ships.Where(x => !x.Destroyed).All(x => x.Hull < x.MaxHull / 2) && s.Rng.Chance(0.02))
        {
            // Battered fleets may offer surrender if hailed (manual: stop firing and talk).
            e.Departing = false;
            s.Emit(EventKind.Info, $"The {race.Name} are signalling. Hail them, and they may surrender.");
        }
    }

    private void AlienAct(EncounterState e, AlienRaceDefinition race, AlienShip a, double dt)
    {
        double dx = e.PlayerX - a.X, dy = e.PlayerY - a.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double toPlayer = Math.Atan2(dy, dx);
        double speed = 10 * race.Ship.Speed;
        double desired;
        if (e.Departing || a.Fleeing) desired = toPlayer + Math.PI; // move away
        else if (!e.Hostile) desired = a.Heading + 0.15 * dt;     // idle drift
        else
        {
            double preferred = race.Ship.LaserDamage > 0 && race.Ship.MissileDamage == 0 ? 18 : 40;
            // Pursue to preferred range, then strafe (orbit) around the player.
            desired = dist > preferred + 8 ? toPlayer : dist < preferred - 8 ? toPlayer + Math.PI : toPlayer + Math.PI / 2;
            if (a.Hull < a.MaxHull / 4 && race.Ship.Aggression < 0.8) a.Fleeing = true;
        }
        a.Heading += Math.Clamp(AngleDiff(desired, a.Heading), -2 * dt, 2 * dt);
        double v = e.Hostile || e.Departing || a.Fleeing ? speed : speed * 0.2;
        a.X += Math.Cos(a.Heading) * v * dt;
        a.Y += Math.Sin(a.Heading) * v * dt;

        if (!e.Hostile || a.Fleeing || e.Surrendered) return;
        a.Cooldown -= dt;
        if (a.Cooldown > 0) return;
        if (race.Ship.MissileDamage > 0 && dist > 15 && dist < 90)
        {
            bool plasma = race.Id == "uhlek";
            double h = toPlayer;
            double spd = plasma ? MissileSpeed * 2 : MissileSpeed * 0.8;
            e.Projectiles.Add(new Projectile
            {
                Kind = plasma ? ProjectileKind.Plasma : ProjectileKind.Missile, X = a.X, Y = a.Y,
                Vx = Math.Cos(h) * spd, Vy = Math.Sin(h) * spd, Damage = race.Ship.MissileDamage, FromPlayer = false, Life = 3,
                TargetId = plasma ? 0 : -1,
            });
            a.Cooldown = plasma ? 4 : 2.5 + s.Rng.NextDouble();
            s.PlaySound(plasma ? "missile" : "missile");
        }
        else if (race.Ship.LaserDamage > 0 && dist <= LaserRange)
        {
            a.Cooldown = 1.2 + s.Rng.NextDouble() * 0.6;
            e.Beams.Add(new BeamFlash(a.X, a.Y, e.PlayerX, e.PlayerY, false, 0.15));
            s.PlaySound("laser");
            if (s.Rng.Chance(0.7)) DamagePlayer(race.Ship.LaserDamage, laser: true);
        }
    }

    private void MoveProjectiles(EncounterState e, AlienRaceDefinition race, double dt)
    {
        for (int i = e.Projectiles.Count - 1; i >= 0; i--)
        {
            var p = e.Projectiles[i];
            if (p.Kind == ProjectileKind.Plasma && !p.FromPlayer)
            {
                // Uhlek plasma bolts home on the player (fan observation).
                double h = Math.Atan2(e.PlayerY - p.Y, e.PlayerX - p.X);
                double spd = Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy);
                double cur = Math.Atan2(p.Vy, p.Vx);
                cur += Math.Clamp(AngleDiff(h, cur), -1.5 * dt, 1.5 * dt);
                p.Vx = Math.Cos(cur) * spd; p.Vy = Math.Sin(cur) * spd;
            }
            p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Life -= dt;
            bool remove = p.Life <= 0;
            if (p.FromPlayer)
            {
                var hit = e.Ships.FirstOrDefault(a => !a.Destroyed && Dist(a.X, a.Y, p.X, p.Y) < 4);
                if (hit is not null)
                {
                    int dmg = race.Ship.MissileResistant ? p.Damage / 10 : p.Damage;
                    DamageAlien(e, race, hit, dmg);
                    if (race.Ship.MissileResistant) s.Emit(EventKind.Info, "The missile has little effect on that hull.");
                    remove = true;
                }
            }
            else if (Dist(e.PlayerX, e.PlayerY, p.X, p.Y) < 4)
            {
                DamagePlayer(p.Damage, laser: false);
                remove = true;
            }
            if (remove) e.Projectiles.RemoveAt(i);
            if (s.IsOver) return;
        }
    }

    private static double Dist(double x1, double y1, double x2, double y2) => Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));

    /// <summary>Shields absorb first, then armour, then hull. Hull/armour hits can damage systems and injure crew.</summary>
    public void DamagePlayer(int dmg, bool laser)
    {
        var ship = s.State.Ship;
        if (laser && s.HasArtifact("rod-device")) dmg /= 2;
        if (ship.ShieldsUp && ship.ShieldPoints > 0)
        {
            int absorbed = Math.Min(ship.ShieldPoints, dmg);
            ship.ShieldPoints -= absorbed;
            dmg -= absorbed;
            s.PlaySound("shieldhit");
            if (dmg <= 0) return;
        }
        int toArmor = Math.Min(ship.ArmorPoints, dmg);
        ship.ArmorPoints -= toArmor;
        dmg -= toArmor;
        ship.HullPoints -= dmg;
        s.PlaySound("hullhit");
        if (toArmor + dmg > 0 && s.Rng.Chance(0.35))
        {
            var sys = (ShipSystem)s.Rng.Next(Enum.GetValues<ShipSystem>().Length);
            ship.Damage[sys] = Math.Min(100, ship.Damage[sys] + Math.Max(5, (toArmor + dmg) / 20));
            s.Emit(EventKind.Warning, $"{sys} damaged ({ship.Damage[sys]}%).");
        }
        if (dmg > 0 && s.Rng.Chance(0.3)) s.Crew.InjureRandom(Math.Min(60, 5 + dmg / 25), "combat damage");
        if (s.IsOver) return;
        if (ship.HullPoints <= ShipRules.BaseHull / 5 && s.HasArtifact("crystal-pearl") && Encounter is { PearlUsed: false } e)
        {
            e.PearlUsed = true;
            s.Emit(EventKind.Success, "The Crystal Pearl is pulsing! The ship is hurled clear of the battle.", "hyperspace");
            EndEncounter(fled: true);
            return;
        }
        if (ship.HullPoints <= 0)
        {
            ship.HullPoints = 0;
            s.State.Encounter = null;
            s.GameOver($"The ISS {ship.Name} was destroyed in combat with the {Race?.Name ?? "aliens"}.");
        }
    }

    private void CheckEnd(EncounterState e)
    {
        var alive = e.Ships.Where(a => !a.Destroyed).ToList();
        if (alive.Count == 0)
        {
            s.Emit(EventKind.Success, "All hostile vessels destroyed. Debris can be collected with Captain > Cargo.");
            EndEncounter(fled: false);
            return;
        }
        if (alive.All(a => Dist(a.X, a.Y, e.PlayerX, e.PlayerY) > EncounterState.FleeRadius))
        {
            s.Emit(EventKind.Info, e.Departing ? "The aliens have gone." : "We have left the aliens behind.");
            EndEncounter(fled: true);
        }
    }

    /// <summary>Captain → Cargo after a fight: salvage debris (value is reconstruction).</summary>
    public ActionResult CollectDebris()
    {
        if (Encounter is not { } e || e.DebrisCount == 0) return ActionResult.Fail("No debris in range.");
        int value = 0;
        for (int i = 0; i < e.DebrisCount; i++) value += 300 + s.Rng.Next(1200);
        var ship = s.State.Ship;
        if (ShipRules.CargoFree(ship, s.Data.Ship) < e.DebrisCount) return ActionResult.Fail("No room in the hold for debris.");
        ship.Cargo.Add(new CargoItem { Kind = CargoKind.Debris, Id = "debris", Name = $"{Race!.Name} debris", Quantity = e.DebrisCount, ValuePerUnit = value / e.DebrisCount });
        e.DebrisCount = 0;
        return ActionResult.Success("Debris collected.");
    }

    public void EndEncounter(bool fled)
    {
        if (Encounter is not { } e) return;
        if (!fled && e.DebrisCount > 0 && ShipRules.CargoFree(s.State.Ship, s.Data.Ship) >= e.DebrisCount) CollectDebris();
        s.State.Encounter = null;
        _accumulator = 0;
        var back = s.State.Location.ModeBeforeEncounter;
        s.SetMode(back is GameMode.Hyperspace or GameMode.System ? back : GameMode.Hyperspace);
        if (fled && back == GameMode.System)
        {
            // Escape: shove the ship a little so the same encounter roll doesn't trigger immediately.
            s.State.Location.SysX *= 0.95;
        }
    }

    /// <summary>Peaceful end after talking: the aliens depart.</summary>
    public void Disengage()
    {
        if (Encounter is { } e && !e.Hostile) { e.Departing = true; }
    }
}
