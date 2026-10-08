using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AVAFlight.Infrastructure.Input;

namespace AVAFlight.Avalonia.Ui;

public sealed record MenuEntry(string Label, Action? Action, bool Enabled = true, string? Hint = null, string? Value = null, IBrush? Color = null);

/// <summary>
/// The universal list/menu control: Up/Down (keys, D-pad, stick), Confirm (Enter, Space, A),
/// mouse click and wheel. Every menu in AVAFlight uses it, so keyboard, mouse and gamepad
/// navigation behave identically everywhere. Optional hints become tooltips (Modern mode).
/// </summary>
public sealed class MenuList : UserControl
{
    private readonly StackPanel _stack = new() { Spacing = 1 };
    private readonly ScrollViewer _scroll;
    private List<MenuEntry> _items = [];
    private int _selected;

    public double ItemSize { get; set; } = 20;
    public bool ShowTooltips { get; set; } = true;
    public bool Active { get; set; } = true;
    public event Action<int>? SelectionChanged;

    public MenuList()
    {
        _scroll = new ScrollViewer { Content = _stack, VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        Content = _scroll;
        Focusable = false;
    }

    public int SelectedIndex
    {
        get => _selected;
        set { _selected = Math.Clamp(value, 0, Math.Max(0, _items.Count - 1)); Render(); SelectionChanged?.Invoke(_selected); }
    }

    public IReadOnlyList<MenuEntry> Items => _items;

    public void SetItems(IEnumerable<MenuEntry> items, bool keepSelection = true)
    {
        _items = items.ToList();
        if (!keepSelection) _selected = 0;
        _selected = Math.Clamp(_selected, 0, Math.Max(0, _items.Count - 1));
        if (_items.Count > 0 && !_items[_selected].Enabled)
        {
            int first = _items.FindIndex(i => i.Enabled);
            if (first >= 0) _selected = first;
        }
        Render();
    }

    private void Render()
    {
        _stack.Children.Clear();
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            int index = i;
            bool sel = i == _selected && Active;
            var color = !item.Enabled ? Ui.Dim : sel ? Ui.Highlight : item.Color ?? Ui.Bright;
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var label = Ui.Label((sel ? "> " : "  ") + item.Label, ItemSize, color);
            label.TextWrapping = TextWrapping.NoWrap;
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            grid.Children.Add(label);
            if (item.Value is not null)
            {
                var v = Ui.Label(item.Value, ItemSize, item.Enabled ? Ui.Accent : Ui.Dim);
                v.TextWrapping = TextWrapping.NoWrap;
                v.Margin = new Thickness(8, 0, 4, 0);
                Grid.SetColumn(v, 1);
                grid.Children.Add(v);
            }
            var row = new Border
            {
                Child = grid, Background = sel ? Ui.SelectedBg : Brushes.Transparent, Padding = new Thickness(4, 0), Cursor = new Cursor(StandardCursorType.Hand),
            };
            if (item.Hint is not null && ShowTooltips) ToolTip.SetTip(row, item.Hint);
            row.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed) return;
                _selected = index;
                Activate();
                e.Handled = true;
            };
            _stack.Children.Add(row);
            if (sel) row.AttachedToVisualTree += (_, _) => row.BringIntoView();
        }
    }

    private void Activate()
    {
        if (_selected < 0 || _selected >= _items.Count) return;
        var item = _items[_selected];
        Render();
        SelectionChanged?.Invoke(_selected);
        if (!item.Enabled || item.Action is null)
        {
            AVAFlight.Avalonia.Services.AppServices.Current?.Audio.Play(AVAFlight.Infrastructure.Audio.SoundEffect.Error);
            return;
        }
        AVAFlight.Avalonia.Services.AppServices.Current?.Audio.Play(AVAFlight.Infrastructure.Audio.SoundEffect.MenuSelect);
        item.Action();
    }

    /// <summary>Handles a logical action; returns true if consumed.</summary>
    public bool Handle(InputAction a)
    {
        if (_items.Count == 0) return false;
        switch (a)
        {
            case InputAction.Up: Move(-1); return true;
            case InputAction.Down: Move(1); return true;
            case InputAction.Confirm: Activate(); return true;
            default: return false;
        }
    }

    private void Move(int dir)
    {
        int n = _items.Count;
        for (int step = 1; step <= n; step++)
        {
            int i = ((_selected + dir * step) % n + n) % n;
            if (_items[i].Enabled || step == n) { _selected = i; break; }
        }
        AVAFlight.Avalonia.Services.AppServices.Current?.Audio.Play(AVAFlight.Infrastructure.Audio.SoundEffect.MenuMove);
        Render();
        SelectionChanged?.Invoke(_selected);
    }
}
