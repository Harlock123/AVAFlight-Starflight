using AVAFlight.Avalonia.Panels;
using AVAFlight.Avalonia.Ui;
using AVAFlight.Core.Engine;
using AVAFlight.Core.Model;

namespace AVAFlight.Avalonia.Screens;

/// <summary>
/// Control panel: crew posts first (Captain, Science, Navigator, Engineer, Communications, Doctor),
/// then that post's functions, as in the original. On a planet surface a Terrain Vehicle menu is
/// added. Functions unavailable in the current situation are shown dimmed rather than hidden, so the
/// command structure stays learnable.
/// </summary>
public sealed partial class GameScreen
{
    private void RefreshControls()
    {
        var mode = S.State.Location.Mode;
        _controls.Active = !_helm && _overlay is null && mode != GameMode.Starport;
        _controls.ShowTooltips = S.Policy.Tooltips;
        if (mode == GameMode.Starport)
        {
            _controlsTitle.Text = "STARPORT";
            _controls.SetItems(
            [
                new MenuEntry("Arrows/Enter: choose module", null, false),
                new MenuEntry("Esc: back / options", null, false),
                new MenuEntry("M: star map   C: crew", null, false),
                new MenuEntry("G: cargo   L: log", null, false),
                new MenuEntry("F5: save", null, false),
            ]);
            return;
        }
        if (_tvRole && mode == GameMode.Surface)
        {
            _controlsTitle.Text = "TERRAIN VEHICLE";
            _controls.SetItems(TvItems());
            return;
        }
        if (_role is not { } role)
        {
            _controlsTitle.Text = "CREW POSTS";
            var items = new List<MenuEntry>();
            if (mode == GameMode.Surface) items.Add(new MenuEntry("Terrain Vehicle", () => { _tvRole = true; RefreshControls(); }));
            foreach (CrewRole r in Enum.GetValues<CrewRole>())
            {
                var m = S.State.Assigned(r);
                items.Add(new MenuEntry(StarportPanel.RoleName(r), () => { _role = r; RefreshControls(); },
                    Value: m is null ? "--" : m.IsDead ? "dead" : m.Name, Color: m is { IsDead: false } ? null : Ui.Ui.Bad));
            }
            if (mode is GameMode.Hyperspace or GameMode.System or GameMode.Surface or GameMode.Encounter)
                items.Insert(0, new MenuEntry(mode == GameMode.Encounter ? "Helm (fly and fight)" : mode == GameMode.Surface ? "Drive vehicle" : "Helm (manoeuvre)", () => SetHelm(true)));
            items.Add(new MenuEntry("Options", () => OpenOverlay(new PauseOverlay(this))));
            _controls.SetItems(items);
            return;
        }
        _controlsTitle.Text = StarportPanel.RoleName(role).ToUpperInvariant();
        _controls.SetItems(RoleItems(role).Append(new MenuEntry("< Back", () => { _role = null; RefreshControls(); })));
    }

    private IEnumerable<MenuEntry> RoleItems(CrewRole role)
    {
        var mode = S.State.Location.Mode;
        var planet = S.CurrentPlanet;
        bool orbit = mode == GameMode.Orbit;
        bool tips = S.Policy.Tooltips;
        string? H(string h) => tips ? h : null;
        switch (role)
        {
            case CrewRole.Captain:
                yield return new MenuEntry("Land", orbit && planet?.Name != "Arth" ? () => OpenOverlay(new LandingOverlay(this)) : null,
                    orbit && planet?.Name != "Arth", H("Choose a landing site. Landing and launch cost 0.25 m³ per G. Over 8 G crushes the ship."));
                yield return new MenuEntry("Dock at Starport", orbit && planet?.Name == "Arth" ? () => Do(S.Starport.Dock()) : null, orbit && planet?.Name == "Arth");
                yield return new MenuEntry("Launch", mode == GameMode.Surface ? () => Do(S.Planets.Launch()) : null, mode == GameMode.Surface);
                yield return new MenuEntry("Log planet", orbit ? () => Do(S.Planets.LogPlanet()) : null, orbit, H("Enter this planet in the ship's log so it can be recommended as a colony at Operations."));
                yield return new MenuEntry("Collect debris", mode == GameMode.Encounter ? () => Do(S.Combat.CollectDebris()) : null, mode == GameMode.Encounter && S.State.Encounter?.DebrisCount > 0);
                yield return new MenuEntry("Cargo", () => OpenOverlay(new InfoOverlay(this, InfoKind.Cargo)));
                yield return new MenuEntry(S.Policy.CaptainsLog ? "Captain's log" : "Ship's log", () => OpenOverlay(new InfoOverlay(this, InfoKind.Log)));
                yield return new MenuEntry("Save game", CanSave ? () => OpenOverlay(new PauseOverlay(this, startAtSave: true)) : null, CanSave);
                break;
            case CrewRole.Science:
                yield return new MenuEntry("Sensors", orbit ? () => { var r = S.Planets.Sensors(); if (r.Ok) SurveyPanel.Cache[S.CurrentPlanet!.Id] = (r.Value, SurveyPanel.Cache.GetValueOrDefault(S.CurrentPlanet!.Id).A); Do(new ActionResult(r.Ok, r.Message)); } : null, orbit,
                    H("Scan from orbit: mass, life, minerals, atmosphere, hydrosphere, lithosphere. A better Science Officer is more certain."));
                yield return new MenuEntry("Analysis", orbit ? () => { var r = S.Planets.Analyze(); if (r.Ok) SurveyPanel.Cache[S.CurrentPlanet!.Id] = (SurveyPanel.Cache.GetValueOrDefault(S.CurrentPlanet!.Id).S, r.Value); Do(new ActionResult(r.Ok, r.Message)); } : null, orbit,
                    H("Detailed analysis: surface type, gravity, atmosphere, temperature and weather."));
                yield return new MenuEntry("Ship status", () => OpenOverlay(new InfoOverlay(this, InfoKind.Ship)));
                break;
            case CrewRole.Navigator:
                bool flying = mode is GameMode.Hyperspace or GameMode.System;
                yield return new MenuEntry("Manoeuvre", flying ? () => SetHelm(true) : null, flying);
                yield return new MenuEntry("Star map", mode != GameMode.Surface && mode != GameMode.Encounter ? () => OpenOverlay(new StarMapOverlay(this)) : null, mode is not (GameMode.Surface or GameMode.Encounter));
                yield return new MenuEntry("Orbit planet", mode == GameMode.System ? () => Do(S.Navigation.EnterOrbit()) : null, mode == GameMode.System);
                yield return new MenuEntry("Leave orbit", orbit ? () => Do(S.Navigation.LeaveOrbit()) : null, orbit);
                yield return new MenuEntry("Clear course", S.State.Location.CruiseX is not null ? () => { S.Navigation.ClearCourse(); RefreshAll(); } : null, S.State.Location.CruiseX is not null);
                yield return new MenuEntry(S.State.Ship.ShieldsUp ? "Drop shields" : "Raise shields", () => Do(S.Combat.ToggleShields()), S.State.Ship.Shield > 0);
                yield return new MenuEntry(S.State.Ship.WeaponsArmed ? "Disarm weapons" : "Arm weapons", () => Do(S.Combat.ToggleWeapons()), S.State.Ship.Laser + S.State.Ship.Missile > 0);
                yield return new MenuEntry("Combat", mode == GameMode.Encounter ? () => SetHelm(true) : null, mode == GameMode.Encounter);
                break;
            case CrewRole.Engineer:
                yield return new MenuEntry("Damage report", () => OpenOverlay(new InfoOverlay(this, InfoKind.Ship)));
                yield return new MenuEntry("(Repairs proceed automatically)", null, false, H("The Engineer repairs damaged systems in flight. Heavy damage needs repair minerals (cobalt, molybdenum, aluminum, titanium, promethium)."));
                break;
            case CrewRole.Communications:
                bool enc = mode == GameMode.Encounter;
                yield return new MenuEntry(S.State.Encounter?.AlienHailedFirst == true ? "Respond" : "Hail", enc ? OpenComms : null, enc && S.Comms.CanTalk);
                yield return new MenuEntry("Distress call", mode is not (GameMode.Encounter or GameMode.Starport) ? () => Do(S.Starport.Distress()) : null,
                    mode is not (GameMode.Encounter or GameMode.Starport), H("Call for a tow to Starport. You are towed home in stasis for a stiff fee."));
                break;
            case CrewRole.Doctor:
                yield return new MenuEntry("Examine crew", () => OpenOverlay(new InfoOverlay(this, InfoKind.Crew)));
                foreach (var m in S.State.ActiveCrew.Where(c => !c.IsDead && c.Vitality < 100))
                    yield return new MenuEntry($"Treat {m.Name}", () => Do(S.Crew.Treat(m.Id)), Value: $"{m.Vitality}%");
                break;
        }
    }

    private IEnumerable<MenuEntry> TvItems()
    {
        bool atShip = S.Planets.AtShip;
        bool tips = S.Policy.Tooltips;
        string? H(string h) => tips ? h : null;
        yield return new MenuEntry("Move", () => SetHelm(true), Hint: H("Drive with the arrow keys. Each step uses vehicle fuel; mountains cost more."));
        yield return new MenuEntry("Pick up", () => Do(S.Planets.PickUp()), Hint: H("Collect the mineral deposit, artifact or message under the vehicle. Hold: 50 m³."));
        yield return new MenuEntry("Scan lifeforms", () => Do(S.Planets.ScanLife()), Hint: H("Record bio-data on nearby life (sells for less than a live specimen)."));
        yield return new MenuEntry("Stunner", () => Do(S.Planets.Fire(stun: true)), Hint: H("Stun the nearest lifeform within 3 cells."));
        yield return new MenuEntry("Laser", () => Do(S.Planets.Fire(stun: false)));
        yield return new MenuEntry("Capture specimen", () => Do(S.Planets.Capture()), Hint: H("Capture an adjacent stunned lifeform. Flying lifeforms cannot be captured."));
        yield return new MenuEntry("Plant Black Egg", S.HasArtifact("black-egg") || S.State.Surface?.TvCargo.Any(c => c.Id == "black-egg") == true ? () => Do(S.Planets.PlantEgg()) : null,
            S.HasArtifact("black-egg") || S.State.Surface?.TvCargo.Any(c => c.Id == "black-egg") == true);
        yield return new MenuEntry("Cargo", () => OpenOverlay(new InfoOverlay(this, InfoKind.Cargo)));
        yield return new MenuEntry("Return to ship and launch", atShip ? () => Do(S.Planets.Launch()) : null, atShip, H("Drive back to the ship (white triangle) first."));
        yield return new MenuEntry("Crew posts >", () => { _tvRole = false; RefreshControls(); });
    }
}
