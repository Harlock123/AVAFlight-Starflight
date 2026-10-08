using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AVAFlight.Infrastructure.Input;

namespace AVAFlight.Avalonia.Ui;

/// <summary>A menu page: title, menu entries and an optional details panel shown beside the menu.</summary>
public sealed class MenuPage
{
    public required string Title { get; init; }
    public Func<IEnumerable<MenuEntry>> Items { get; init; } = () => [];
    public Func<Control?>? Details { get; init; }
    /// <summary>If set, the page shows a text box instead of a menu; Enter submits.</summary>
    public Action<string>? OnSubmitText { get; init; }
    public string TextPrompt { get; init; } = "";
    public int MaxLength { get; init; } = 20;
    public int Selected { get; set; }
}

/// <summary>
/// Navigable stack of <see cref="MenuPage"/>s with a breadcrumb title, used for the Starport modules
/// and other menu-driven screens. Back pops a page; the root page cannot be popped.
/// </summary>
public sealed class PageStack : UserControl
{
    private readonly Stack<MenuPage> _pages = new();
    private readonly MenuList _menu = new() { ItemSize = 21 };
    private readonly TextBlock _title = Ui.Heading("", 13);
    private readonly ContentControl _details = new();
    private readonly TextBlock _status = Ui.Label("", 19, Ui.Highlight);
    private TextBox? _textBox;
    private readonly Grid _body = new() { ColumnDefinitions = new ColumnDefinitions("5*,4*"), ColumnSpacing = 12 };

    public double MenuItemSize { get => _menu.ItemSize; set => _menu.ItemSize = value; }
    public bool ShowTooltips { get => _menu.ShowTooltips; set => _menu.ShowTooltips = value; }
    public int Depth => _pages.Count;
    public bool IsTyping => _textBox is not null;

    public PageStack()
    {
        var left = new DockPanel();
        DockPanel.SetDock(_title, Dock.Top);
        left.Children.Add(_title);
        left.Children.Add(_menu);
        _body.Children.Add(left);
        Grid.SetColumn(_details, 1);
        _body.Children.Add(_details);
        var root = new DockPanel();
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(_status);
        root.Children.Add(_body);
        Content = root;
        _menu.SelectionChanged += i => { if (_pages.Count > 0) _pages.Peek().Selected = i; RefreshDetails(); };
    }

    public void SetRoot(MenuPage p)
    {
        _pages.Clear();
        Push(p);
    }

    public void Push(MenuPage p)
    {
        _pages.Push(p);
        Refresh(resetSelection: true);
    }

    public bool Pop()
    {
        if (_pages.Count <= 1) return false;
        _pages.Pop();
        Refresh();
        return true;
    }

    public void PopToRoot()
    {
        while (_pages.Count > 1) _pages.Pop();
        Refresh();
    }

    public void Status(string text, bool error = false)
    {
        _status.Text = text;
        _status.Foreground = error ? Ui.Bad : Ui.Highlight;
    }

    public void Refresh(bool resetSelection = false)
    {
        if (_pages.Count == 0) return;
        var p = _pages.Peek();
        _title.Text = string.Join(" > ", _pages.Reverse().Select(x => x.Title)).ToUpperInvariant();
        if (p.OnSubmitText is not null)
        {
            _textBox = new TextBox
            {
                MaxLength = p.MaxLength, FontFamily = Ui.DataFont, FontSize = Ui.S(24), PlaceholderText = p.TextPrompt,
                Background = Ui.SelectedBg, Foreground = Ui.Bright, BorderBrush = Ui.Border, Margin = new Thickness(0, 8),
            };
            var tb = _textBox;
            var box = Ui.VStack(6, Ui.Label(p.TextPrompt, 20, Ui.Accent), _textBox, Ui.Label("Enter: confirm    Esc: cancel", 18, Ui.Dim));
            ((DockPanel)_body.Children[0]).Children[1] = box;
            // Focus once layout has run (the box attaches synchronously above).
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => tb.Focus(), global::Avalonia.Threading.DispatcherPriority.Loaded);
        }
        else
        {
            _textBox = null;
            var left = (DockPanel)_body.Children[0];
            if (left.Children[1] != _menu) left.Children[1] = _menu;
            _menu.SetItems(p.Items(), keepSelection: true);
            _menu.SelectedIndex = resetSelection ? 0 : p.Selected;
        }
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        if (_pages.Count == 0) return;
        _details.Content = _pages.Peek().Details?.Invoke();
    }

    public bool Handle(InputAction a)
    {
        if (_pages.Count == 0) return false;
        var p = _pages.Peek();
        if (_textBox is not null)
        {
            if (a == InputAction.Confirm) { var t = _textBox.Text ?? ""; p.OnSubmitText!(t); return true; }
            if (a == InputAction.Back) { Pop(); return true; }
            return false;
        }
        if (a == InputAction.Back) return Pop();
        return _menu.Handle(a);
    }
}
