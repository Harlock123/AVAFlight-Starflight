using AVAFlight.Core.Data;

namespace AVAFlight.Core.Model;

/// <summary>
/// Ship formulas. Each documents its evidence level (see docs/FIDELITY.md).
/// </summary>
public static class ShipRules
{
    /// <summary>Base hull points without armour (fan measurement: 250).</summary>
    public const int BaseHull = 250;
    /// <summary>Endurium carried outside the cargo pods (reconstruction: the starting ship holds 20 m³ with no pods).</summary>
    public const double FuelBayCapacity = 50;
    /// <summary>Landing/launch cost: 0.25 m³ per G of surface gravity (manual energy chart).</summary>
    public const double LandingFuelPerG = 0.25;
    /// <summary>Maximum survivable landing gravity (manual: over 8.0 G crushes the ship).</summary>
    public const double MaxLandingGravity = 8.0;
    /// <summary>Shield energy use: 0.1 m³ per star-hour (manual).</summary>
    public const double ShieldFuelPerHour = 0.1;
    public const double LaserShotFuel = 0.01;
    public const double MissileShotFuel = 0.05;

    /// <summary>
    /// Ship mass in tons. Derived from the decompiled SHIPGRPH formula:
    /// 50 + 50·[engine] + 9·armor² + 10·pods + 5 each for shields, missiles, lasers.
    /// </summary>
    public static int Mass(ShipState s) =>
        50 + (s.Engine > 0 ? 50 : 0) + 9 * s.Armor * s.Armor + 10 * s.CargoPods +
        (s.Shield > 0 ? 5 : 0) + (s.Missile > 0 ? 5 : 0) + (s.Laser > 0 ? 5 : 0);

    /// <summary>Acceleration in G: engine class × 500 / mass, integer (decompiled .ACC).</summary>
    public static int Acceleration(ShipState s) => s.Engine == 0 ? 0 : s.Engine * 500 / Mass(s);

    /// <summary>
    /// Hyperspace fuel per map coordinate: 0.08 × (7 − engine class), giving the manual's
    /// 0.48 (class 1) to 0.16 (class 5) m³ per coordinate. Halved by an analysed Tesseract.
    /// </summary>
    public static double FuelPerCoordinate(ShipState s, bool tesseract) =>
        0.08 * (7 - Math.Clamp(s.Engine, 1, 5)) * (tesseract ? 0.5 : 1.0);

    /// <summary>
    /// Hyperspace travel time in game hours per coordinate. RECONSTRUCTION ASSUMPTION: faster with
    /// acceleration (2.5 h at 5 G, falling toward 1 h); calibrated so a 100-coordinate trip takes
    /// around 10 days, leaving the 300-day Arth deadline tight but fair.
    /// </summary>
    public static double HoursPerCoordinate(ShipState s) => Math.Max(1.0, 3.0 - Acceleration(s) / 10.0);

    public static double CargoCapacity(ShipState s, ShipCatalog cat) => s.CargoPods * cat.CargoPodCapacity;

    /// <summary>Hold volume used, including endurium beyond the fuel bay.</summary>
    public static double CargoUsed(ShipState s) =>
        s.Cargo.Sum(c => c.Volume) + Math.Max(0, s.Endurium - FuelBayCapacity);

    public static double CargoFree(ShipState s, ShipCatalog cat) => Math.Max(0, CargoCapacity(s, cat) - CargoUsed(s));

    /// <summary>Maximum endurium the ship can hold right now.</summary>
    public static double EnduriumCapacity(ShipState s, ShipCatalog cat) =>
        FuelBayCapacity + Math.Max(0, CargoCapacity(s, cat) - s.Cargo.Sum(c => c.Volume));

    public static int MaxArmorPoints(ShipState s, ShipCatalog cat) => s.Armor == 0 ? 0 : cat.Components.First(c => c.Kind == ComponentKind.Armor && c.Class == s.Armor).Strength;
    public static int MaxShieldPoints(ShipState s, ShipCatalog cat) => s.Shield == 0 ? 0 : cat.Components.First(c => c.Kind == ComponentKind.Shield && c.Class == s.Shield).Strength;

    /// <summary>
    /// Range in coordinates on current fuel, keeping the landing reserve. Shown on the Modern
    /// star map as the estimated range indicator.
    /// </summary>
    public static double RangeCoordinates(ShipState s, bool tesseract) =>
        Math.Max(0, s.Endurium) / FuelPerCoordinate(s, tesseract);

    public static int Get(ShipState s, ComponentKind k) => k switch
    {
        ComponentKind.Engine => s.Engine, ComponentKind.Shield => s.Shield, ComponentKind.Armor => s.Armor,
        ComponentKind.Missile => s.Missile, _ => s.Laser,
    };

    public static void Set(ShipState s, ComponentKind k, int cls)
    {
        switch (k)
        {
            case ComponentKind.Engine: s.Engine = cls; break;
            case ComponentKind.Shield: s.Shield = cls; break;
            case ComponentKind.Armor: s.Armor = cls; break;
            case ComponentKind.Missile: s.Missile = cls; break;
            default: s.Laser = cls; break;
        }
    }
}

public static class CrewRules
{
    /// <summary>Training cost per session (decompiled PERSONNEL text: "COST: 300 MU/SESSION").</summary>
    public const int TrainingCost = 300;

    public static readonly Dictionary<CrewRole, Skill?> RoleSkill = new()
    {
        [CrewRole.Captain] = null,
        [CrewRole.Science] = Skill.Science,
        [CrewRole.Navigator] = Skill.Navigation,
        [CrewRole.Engineer] = Skill.Engineering,
        [CrewRole.Communications] = Skill.Communication,
        [CrewRole.Doctor] = Skill.Medicine,
    };

    /// <summary>Efficiency 0..1 = skill/250 (fan model from game testing; see FIDELITY.md).</summary>
    public static double Efficiency(int skill) => Math.Clamp(skill / 250.0, 0, 1);
}

/// <summary>
/// Classic vs Modern differences, in one place. Both presets share every rule and number; Modern
/// only adds information and convenience (see docs/FIDELITY.md, "Modern-mode policy").
/// </summary>
public sealed record Policy
{
    public required GamePreset Preset { get; init; }
    /// <summary>Show the auto-recorded captain's log (Classic shows only manually logged entries).</summary>
    public bool CaptainsLog { get; init; }
    public bool Waypoints { get; init; }
    public bool FuelWarningAndRange { get; init; }
    public bool Tooltips { get; init; }
    /// <summary>Pause in any non-combat view (Classic: only via the options menu, like the original Esc menu).</summary>
    public bool PauseAnywhere { get; init; }
    public bool Hints { get; init; }
    public bool NamedSaveSlots { get; init; }
    /// <summary>Show the destination/distance/fuel readout on the star map before committing.</summary>
    public bool CourseReadout { get; init; }

    public static Policy For(GamePreset p) => p == GamePreset.Classic
        ? new Policy { Preset = p, CourseReadout = true }
        : new Policy
        {
            Preset = p, CaptainsLog = true, Waypoints = true, FuelWarningAndRange = true, Tooltips = true,
            PauseAnywhere = true, Hints = true, NamedSaveSlots = true, CourseReadout = true,
        };
}
