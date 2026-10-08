using AVAFlight.Core.Data;

namespace AVAFlight.Core.Model;

public sealed class AlienShip
{
    public int Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Heading { get; set; }
    public int Hull { get; set; }
    public int MaxHull { get; set; }
    public int Shield { get; set; }
    public double Cooldown { get; set; }
    public bool Destroyed { get; set; }
    public bool Fleeing { get; set; }
}

public enum ProjectileKind { Missile, Plasma, Laser }

public sealed class Projectile
{
    public ProjectileKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Vx { get; set; }
    public double Vy { get; set; }
    public int Damage { get; set; }
    public bool FromPlayer { get; set; }
    public int TargetId { get; set; } = -1;
    public double Life { get; set; }
}

/// <summary>A short-lived beam drawn by the combat view.</summary>
public sealed record BeamFlash(double X1, double Y1, double X2, double Y2, bool FromPlayer, double Life);

/// <summary>
/// An encounter in the tactical view. Arena coordinates are -100..100 with the player starting
/// at the centre; flying beyond <see cref="FleeRadius"/> from every alien ship escapes.
/// </summary>
public sealed class EncounterState
{
    public const double ArenaRadius = 100;
    public const double FleeRadius = 120;

    public string RaceId { get; set; } = "";
    public List<AlienShip> Ships { get; set; } = [];
    public List<Projectile> Projectiles { get; set; } = [];
    public double PlayerX { get; set; }
    public double PlayerY { get; set; }
    public double PlayerVx { get; set; }
    public double PlayerVy { get; set; }
    public double PlayerHeading { get; set; }
    public double PlayerCooldown { get; set; }
    public bool Hostile { get; set; }
    public bool AlienHailedFirst { get; set; }
    public bool Talking { get; set; }
    public Posture Posture { get; set; } = Posture.Friendly;
    public int Exchanges { get; set; }
    public int Patience { get; set; } = 6;
    public int HostilePressure { get; set; }
    public bool Surrendered { get; set; }
    public bool Departing { get; set; }
    public double DepartTimer { get; set; }
    public double Elapsed { get; set; }
    public double ArmingTimer { get; set; }
    public bool PearlUsed { get; set; }
    public bool VerificationPassed { get; set; }
    public int DebrisCount { get; set; }
    public List<string> Transcript { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public List<BeamFlash> Beams { get; } = [];
}
