using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AVAFlight.Avalonia.Ui;
using AVAFlight.Core.Engine;
using AVAFlight.Infrastructure.Input;
using AVAFlight.Infrastructure.Persistence;

namespace AVAFlight.Avalonia.Screens;

/// <summary>Save-slot browser (load / delete).</summary>
public sealed class LoadScreen : Screen
{
    private readonly MenuList _menu = new() { ItemSize = 22 };
    private readonly TextBlock _status = Ui.Ui.Label("", 18, Ui.Ui.Bad);
    private bool _confirmDelete;

    public LoadScreen()
    {
        Refresh();
        var help = Ui.Ui.Label("Enter: load    Del (or X/West): delete    Esc: back", 18, Ui.Ui.Dim);
        Content = new Border
        {
            Padding = new Thickness(60, 40),
            Child = new DockPanel
            {
                Children =
                {
                    Dock(Ui.Ui.Heading("LOAD GAME", 20), global::Avalonia.Controls.Dock.Top),
                    Dock(help, global::Avalonia.Controls.Dock.Bottom),
                    Dock(_status, global::Avalonia.Controls.Dock.Bottom),
                    Ui.Ui.Box(_menu),
                },
            },
        };
    }

    private static Control Dock(Control c, Dock d) { DockPanel.SetDock(c, d); return c; }

    private void Refresh()
    {
        var saves = App.Saves.List();
        _menu.SetItems(saves.Select(s => new MenuEntry(
            $"{s.DisplayName,-24} {s.Preset,-8} {s.StarDate,-11} {s.Location}",
            s.SchemaVersion > 0 ? () => Load(s.Slot) : null, s.SchemaVersion > 0,
            $"Saved {s.SavedAt:g}", s.SavedAt.ToString("yyyy-MM-dd HH:mm"))).DefaultIfEmpty(new MenuEntry("(no saved games)", null, false)));
    }

    private void Load(string slot)
    {
        try
        {
            var state = App.Saves.Load(slot);
            Host!.Navigate(new GameScreen(new GameSession(state, AVAFlight.Core.Data.GameData.Default), slot));
        }
        catch (SaveLoadException e)
        {
            _status.Text = e.Message;
        }
    }

    public override bool CaptureRawKey(string keyName)
    {
        if (keyName != "Delete") return false;
        DeleteSelected();
        return true;
    }

    private void DeleteSelected()
    {
        var saves = App.Saves.List();
        if (saves.Count == 0) return;
        var s = saves[Math.Min(_menu.SelectedIndex, saves.Count - 1)];
        if (!_confirmDelete) { _confirmDelete = true; _status.Text = $"Delete '{s.DisplayName}'? Press Delete again to confirm."; return; }
        App.Saves.Delete(s.Slot);
        _confirmDelete = false;
        _status.Text = "Deleted.";
        Refresh();
    }

    public override bool HandleAction(InputAction action)
    {
        if (action == InputAction.Back) { Host!.Navigate(new TitleScreen()); return true; }
        if (action == InputAction.Fire) { DeleteSelected(); return true; }
        _confirmDelete = false;
        return _menu.Handle(action);
    }
}
