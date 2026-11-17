using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace JianyingBatchExporter;

internal sealed class RoundedPanel : Panel
{
    public int CornerRadius { get; set; } = 10;
    public Color BorderColor { get; set; } = Color.Transparent;

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
        using var path = CreatePath(ClientRectangle, CornerRadius);
        using var fill = new SolidBrush(BackColor);
        e.Graphics.FillPath(fill, path);
        if (BorderColor != Color.Transparent)
        {
            using var pen = new Pen(BorderColor);
            var borderRect = Rectangle.Inflate(ClientRectangle, -1, -1);
            using var borderPath = CreatePath(borderRect, CornerRadius);
            e.Graphics.DrawPath(pen, borderPath);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    private static GraphicsPath CreatePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0) return path;
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class RoundButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public int CornerRadius { get; set; } = 9;

    public RoundButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Region = null;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _pressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Color.Transparent);
        var bounds = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        if (bounds.Width == 0 || bounds.Height == 0) return;

        var background = !Enabled
            ? ControlPaint.Dark(BackColor, 0.35F)
            : _pressed ? ControlPaint.Dark(BackColor, 0.08F)
            : _hovered ? ControlPaint.Light(BackColor, 0.08F)
            : BackColor;

        using var path = RoundedRectangle(bounds, CornerRadius);
        using var brush = new SolidBrush(background);
        e.Graphics.FillPath(brush, path);

        var textColor = Enabled ? ForeColor : Color.FromArgb(112, 118, 128);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) return path;
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class NavigationButton : Button
{
    private bool _active;
    private bool _hovered;

    public bool Active
    {
        get => _active;
        set { _active = value; Invalidate(); }
    }

    public Color ActiveColor { get; set; } = Color.FromArgb(20, 205, 187);
    public Color ActiveBackground { get; set; } = Color.FromArgb(29, 47, 45);
    public Color HoverBackground { get; set; } = Color.FromArgb(26, 29, 34);
    public Color IdleBackground { get; set; } = Color.FromArgb(10, 12, 15);
    public int CornerRadius { get; set; } = 8;

    public NavigationButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(IdleBackground);
        var bounds = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        if (bounds.Width == 0 || bounds.Height == 0) return;

        var background = Active ? ActiveBackground : _hovered ? HoverBackground : IdleBackground;
        using var path = RoundedRectangle(bounds, CornerRadius);
        using var backgroundBrush = new SolidBrush(background);
        e.Graphics.FillPath(backgroundBrush, path);

        if (Active)
        {
            using var accentBrush = new SolidBrush(ActiveColor);
            e.Graphics.FillRectangle(accentBrush, 0, 12, 3, Math.Max(0, Height - 24));
        }

        var textColor = !Enabled
            ? Color.FromArgb(92, 97, 106)
            : Active ? Color.White : Color.FromArgb(166, 172, 182);
        var textBounds = new Rectangle(18, 0, Math.Max(0, Width - 28), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) return path;
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class SlimProgressBar : Control
{
    private int _maximum = 1;
    private int _value;

    public int Maximum
    {
        get => _maximum;
        set { _maximum = Math.Max(1, value); _value = Math.Min(_value, _maximum); Invalidate(); }
    }

    public int Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, _maximum); Invalidate(); }
    }

    public Color TrackColor { get; set; } = Color.FromArgb(43, 47, 55);
    public Color ProgressColor { get; set; } = Color.FromArgb(20, 205, 187);

    public SlimProgressBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        Height = 6;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        if (rect.Width == 0 || rect.Height == 0) return;

        using var track = new SolidBrush(TrackColor);
        using var trackPath = RoundedRectangle(rect, Math.Max(1, rect.Height / 2));
        e.Graphics.FillPath(track, trackPath);

        var width = (int)Math.Round(rect.Width * (_value / (double)_maximum));
        if (width <= 0) return;
        using var progressBrush = new SolidBrush(ProgressColor);
        using var progressPath = RoundedRectangle(
            new Rectangle(rect.X, rect.Y, width, rect.Height),
            Math.Max(1, rect.Height / 2));
        e.Graphics.FillPath(progressBrush, progressPath);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) return path;
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class DarkComboBox : ComboBox
{
    private const int WmPaint = 0x000F;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmMouseWheel = 0x020A;
    private const int CbShowDropDown = 0x014F;
    private readonly ContextMenuStrip _menu = new()
    {
        ShowImageMargin = false,
        ShowCheckMargin = false,
        BackColor = Color.FromArgb(30, 34, 40),
        ForeColor = Color.FromArgb(242, 244, 247),
        Padding = new Padding(4),
        AutoSize = false
    };

    public Color FieldColor { get; set; } = Color.FromArgb(37, 40, 45);
    public Color HoverColor { get; set; } = Color.FromArgb(48, 52, 58);
    public Color OutlineColor { get; set; } = Color.FromArgb(72, 80, 92);
    public Color FocusOutlineColor { get; set; } = Color.FromArgb(20, 205, 187);
    public Color AccentColor { get; set; } = Color.FromArgb(20, 205, 187);
    public int CornerRadius { get; set; } = 8;

    public DarkComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        IntegralHeight = true;
        MaxDropDownItems = 16;
        ItemHeight = 30;
        BackColor = FieldColor;
        ForeColor = Color.FromArgb(242, 244, 247);
        _menu.Renderer = new SpeedDropDown.DarkMenuRenderer();
        _menu.Closed += (_, _) => Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        using var background = new SolidBrush(selected ? HoverColor : FieldColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        var textRect = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 18), e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            GetItemText(Items[e.Index]),
            Font,
            textRect,
            selected ? Color.White : ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (selected)
        {
            using var accent = new SolidBrush(AccentColor);
            e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y + 7, 3, Math.Max(0, e.Bounds.Height - 14));
        }
    }

    private void OpenDropDownMenu()
    {
        if (_menu.Visible)
        {
            _menu.Close();
            return;
        }

        RebuildDropDownMenu();
        if (_menu.Items.Count == 0) return;
        Focus();
        var y = Items.Count > 5 ? -_menu.Height - 4 : Height + 4;
        _menu.Show(this, new Point(0, y));
        Invalidate();
    }

    private void RebuildDropDownMenu()
    {
        _menu.Items.Clear();
        for (var index = 0; index < Items.Count; index++)
        {
            var capturedIndex = index;
            var selected = index == SelectedIndex;
            var menuItem = new ToolStripMenuItem(GetItemText(Items[index]))
            {
                AutoSize = false,
                Size = new Size(Math.Max(120, Width - 8), ItemHeight),
                BackColor = Color.FromArgb(30, 34, 40),
                ForeColor = selected ? AccentColor : ForeColor,
                Font = new Font(Font, selected ? FontStyle.Bold : FontStyle.Regular),
                Padding = new Padding(10, 0, 8, 0)
            };
            menuItem.Click += (_, _) => SelectedIndex = capturedIndex;
            _menu.Items.Add(menuItem);
        }

        _menu.Size = new Size(Width, _menu.Items.Count * ItemHeight + 10);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseWheel) return;
        if ((m.Msg == WmLeftButtonDown || (m.Msg == CbShowDropDown && m.WParam != IntPtr.Zero)) && Enabled)
        {
            OpenDropDownMenu();
            return;
        }
        if (m.Msg == CbShowDropDown && _menu.Visible)
        {
            _menu.Close();
            return;
        }
        base.WndProc(ref m);
        if (m.Msg != WmPaint || Width <= 0 || Height <= 0) return;

        using var graphics = CreateGraphics();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? FieldColor);
        var arrowWidth = Math.Min(38, Width);
        var borderBounds = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        using var borderPath = RoundedRectangle(borderBounds, CornerRadius);
        using var background = new SolidBrush(FieldColor);
        graphics.FillPath(background, borderPath);

        var textBounds = new Rectangle(12, 0, Math.Max(0, Width - arrowWidth - 16), Height);
        TextRenderer.DrawText(
            graphics,
            SelectedItem is null ? string.Empty : GetItemText(SelectedItem),
            Font,
            textBounds,
            Enabled ? ForeColor : Color.FromArgb(112, 118, 128),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        using var border = new Pen(Focused || _menu.Visible ? FocusOutlineColor : OutlineColor);
        graphics.DrawPath(border, borderPath);

        using var arrowPen = new Pen(Color.FromArgb(184, 191, 201), 1.6F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        var centerX = Width - arrowWidth / 2;
        var centerY = Height / 2;
        graphics.DrawLines(arrowPen, new[]
        {
            new Point(centerX - 4, centerY - 2),
            new Point(centerX, centerY + 2),
            new Point(centerX + 4, centerY - 2)
        });
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _menu.Dispose();
        base.Dispose(disposing);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) return path;
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class SpeedDropDown : Control
{
    private readonly ContextMenuStrip _menu = new()
    {
        ShowImageMargin = false,
        ShowCheckMargin = false,
        BackColor = Color.FromArgb(30, 34, 40),
        ForeColor = Color.FromArgb(242, 244, 247),
        Padding = new Padding(4),
        AutoSize = false
    };
    private object? _selectedItem;
    private bool _hovered;
    private bool _expanded;

    public List<object> Items { get; } = new();
    public Color FieldColor { get; set; } = Color.FromArgb(34, 38, 45);
    public Color HoverColor { get; set; } = Color.FromArgb(42, 47, 55);
    public Color OutlineColor { get; set; } = Color.FromArgb(72, 80, 92);
    public Color FocusOutlineColor { get; set; } = Color.FromArgb(20, 205, 187);
    public Color AccentColor { get; set; } = Color.FromArgb(20, 205, 187);
    public int CornerRadius { get; set; } = 8;

    public object? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (Equals(_selectedItem, value)) return;
            _selectedItem = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectedIndexChanged;

    public SpeedDropDown()
    {
        Height = 40;
        Cursor = Cursors.Hand;
        TabStop = true;
        BackColor = FieldColor;
        ForeColor = Color.FromArgb(242, 244, 247);
        _menu.Renderer = new DarkMenuRenderer();
        _menu.Closed += (_, _) =>
        {
            _expanded = false;
            Invalidate();
        };
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint |
                 ControlStyles.Selectable, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        Focus();
        OpenDropDown();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!Enabled || Items.Count == 0) return base.ProcessCmdKey(ref msg, keyData);
        if (keyData is Keys.Enter or Keys.Space or Keys.F4 || keyData == (Keys.Alt | Keys.Down))
        {
            OpenDropDown();
            return true;
        }
        if (keyData is Keys.Up or Keys.Down)
        {
            var current = Items.FindIndex(item => Equals(item, SelectedItem));
            var next = keyData == Keys.Down
                ? Math.Min(Items.Count - 1, current + 1)
                : Math.Max(0, current <= 0 ? 0 : current - 1);
            SelectedItem = Items[next];
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Color.Transparent);
        var bounds = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        if (bounds.Width == 0 || bounds.Height == 0) return;

        using var path = RoundedRectangle(bounds, CornerRadius);
        using var background = new SolidBrush(_hovered && Enabled ? HoverColor : FieldColor);
        using var border = new Pen(Focused || _expanded ? FocusOutlineColor : OutlineColor, Focused || _expanded ? 1.5F : 1F);
        e.Graphics.FillPath(background, path);
        e.Graphics.DrawPath(border, path);

        var textColor = Enabled ? ForeColor : Color.FromArgb(112, 118, 128);
        var textBounds = new Rectangle(14, 0, Math.Max(0, Width - 56), Height);
        TextRenderer.DrawText(e.Graphics, SelectedItem?.ToString() ?? string.Empty, Font, textBounds, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        using var arrowPen = new Pen(Enabled ? Color.FromArgb(184, 191, 201) : Color.FromArgb(102, 108, 118), 1.6F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        var centerX = Width - 19;
        var centerY = Height / 2;
        e.Graphics.DrawLines(arrowPen, new[]
        {
            new Point(centerX - 4, centerY - 2),
            new Point(centerX, centerY + 2),
            new Point(centerX + 4, centerY - 2)
        });
    }

    private void OpenDropDown()
    {
        if (_menu.Visible)
        {
            _menu.Close();
            return;
        }

        _menu.Items.Clear();
        foreach (var item in Items)
        {
            var captured = item;
            var menuItem = new ToolStripMenuItem(item.ToString())
            {
                AutoSize = false,
                Size = new Size(Math.Max(120, Width - 8), 36),
                BackColor = Color.FromArgb(30, 34, 40),
                ForeColor = Equals(item, SelectedItem) ? AccentColor : ForeColor,
                Font = new Font(Font, Equals(item, SelectedItem) ? FontStyle.Bold : FontStyle.Regular),
                Padding = new Padding(10, 0, 8, 0)
            };
            menuItem.Click += (_, _) => SelectedItem = captured;
            _menu.Items.Add(menuItem);
        }

        _menu.Size = new Size(Width, _menu.Items.Count * 36 + 10);
        _expanded = true;
        Invalidate();
        _menu.Show(this, new Point(0, Height + 4));
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) return path;
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Color.FromArgb(30, 34, 40));
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var color = e.Item.Selected ? Color.FromArgb(27, 52, 49) : Color.FromArgb(30, 34, 40);
            using var brush = new SolidBrush(color);
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(72, 80, 92));
            var bounds = new Rectangle(0, 0, Math.Max(0, e.ToolStrip.Width - 1), Math.Max(0, e.ToolStrip.Height - 1));
            e.Graphics.DrawRectangle(pen, bounds);
        }
    }
}

internal sealed class AccentRadioButton : RadioButton
{
    public Color AccentColor { get; set; } = Color.FromArgb(20, 205, 187);

    public AccentRadioButton()
    {
        Appearance = Appearance.Button;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        TextAlign = ContentAlignment.MiddleLeft;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Color.Transparent);
        var glyph = new Rectangle(2, Math.Max(0, (Height - 18) / 2), 18, 18);
        var outline = Checked ? AccentColor : Color.FromArgb(188, 194, 203);
        using var outlinePen = new Pen(outline, Checked ? 2F : 1.4F);
        e.Graphics.DrawEllipse(outlinePen, glyph);
        if (Checked)
        {
            using var fill = new SolidBrush(AccentColor);
            e.Graphics.FillEllipse(fill, Rectangle.Inflate(glyph, -5, -5));
        }

        var textColor = Enabled ? ForeColor : Color.FromArgb(112, 118, 128);
        var textBounds = new Rectangle(30, 0, Math.Max(0, Width - 30), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class AccentCheckBox : CheckBox
{
    public Color AccentColor { get; set; } = Color.FromArgb(20, 205, 187);

    public AccentCheckBox()
    {
        Appearance = Appearance.Button;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        TextAlign = ContentAlignment.MiddleLeft;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Color.Transparent);
        var glyph = new Rectangle(1, Math.Max(0, (Height - 18) / 2), 18, 18);
        using var path = RoundedRectangle(glyph, 4);
        using var fill = new SolidBrush(Checked ? AccentColor : Color.FromArgb(34, 38, 45));
        using var outline = new Pen(Checked ? AccentColor : Color.FromArgb(188, 194, 203), Checked ? 1.8F : 1.2F);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(outline, path);

        if (Checked)
        {
            using var check = new Pen(Color.FromArgb(4, 31, 28), 2.2F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            e.Graphics.DrawLines(check, new[]
            {
                new Point(glyph.Left + 4, glyph.Top + 9),
                new Point(glyph.Left + 8, glyph.Top + 13),
                new Point(glyph.Left + 15, glyph.Top + 5)
            });
        }

        var textColor = Enabled ? ForeColor : Color.FromArgb(112, 118, 128);
        var textBounds = new Rectangle(30, 0, Math.Max(0, Width - 30), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
internal sealed class RoundedTextBox : UserControl
{
    private readonly TextBox _editor = new()
    {
        BorderStyle = BorderStyle.None,
        Multiline = false
    };

    private bool _focused;

    public Color BorderColor { get; set; } = Color.FromArgb(72, 80, 92);
    public Color FocusBorderColor { get; set; } = Color.FromArgb(20, 205, 187);
    public int CornerRadius { get; set; } = 8;

    [AllowNull]
    public override string Text
    {
        get => _editor.Text;
        set
        {
            base.Text = value;
            if (_editor.Text != value) _editor.Text = value;
        }
    }

    public string PlaceholderText
    {
        get => _editor.PlaceholderText;
        set => _editor.PlaceholderText = value;
    }

    public bool ReadOnly
    {
        get => _editor.ReadOnly;
        set => _editor.ReadOnly = value;
    }

    public int MaxLength
    {
        get => _editor.MaxLength;
        set => _editor.MaxLength = value;
    }

    public HorizontalAlignment TextAlign
    {
        get => _editor.TextAlign;
        set => _editor.TextAlign = value;
    }

    public new event KeyPressEventHandler? KeyPress
    {
        add => _editor.KeyPress += value;
        remove => _editor.KeyPress -= value;
    }

    public RoundedTextBox()
    {
        Height = 38;
        BackColor = Color.FromArgb(34, 38, 45);
        ForeColor = Color.FromArgb(242, 244, 247);
        Cursor = Cursors.IBeam;
        TabStop = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);

        _editor.BackColor = BackColor;
        _editor.ForeColor = ForeColor;
        _editor.Font = Font;
        _editor.TextChanged += (_, _) =>
        {
            if (base.Text != _editor.Text) base.Text = _editor.Text;
            Invalidate();
        };
        _editor.Enter += (_, _) => { _focused = true; Invalidate(); };
        _editor.Leave += (_, _) => { _focused = false; Invalidate(); };
        Controls.Add(_editor);
        UpdateEditorBounds();
    }

    public new bool Focus() => _editor.Focus();

    public void SelectAll() => _editor.SelectAll();

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        _editor.Focus();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateEditorBounds();
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        _editor.BackColor = BackColor;
        Invalidate();
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        _editor.ForeColor = ForeColor;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _editor.Font = Font;
        UpdateEditorBounds();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        _editor.Enabled = Enabled;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        var bounds = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        using var path = RoundedRectangle(bounds, CornerRadius);
        using var fill = new SolidBrush(BackColor);
        using var border = new Pen(_focused ? FocusBorderColor : BorderColor);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
    }

    private void UpdateEditorBounds()
    {
        if (_editor is null || Width <= 0 || Height <= 0) return;
        var editorHeight = _editor.PreferredHeight;
        _editor.SetBounds(12, Math.Max(0, (Height - editorHeight) / 2), Math.Max(0, Width - 24), editorHeight);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) return path;
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class DarkRichTextBox : RichTextBox
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr handle, string? subAppName, string? subIdList);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int valueSize);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SetWindowTheme(Handle, "DarkMode_Explorer", null);
        var enabled = 1;
        DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int));
    }
}

internal sealed class DarkScrollPanel : Panel
{
    private const int ScrollBarWidth = 10;
    private Control? _content;
    private int _offset;
    private bool _draggingThumb;
    private int _dragStartY;
    private int _dragStartOffset;

    public Color TrackColor { get; set; } = Color.FromArgb(24, 26, 30);
    public Color ThumbColor { get; set; } = Color.FromArgb(72, 77, 85);
    public Color ThumbHoverColor { get; set; } = Color.FromArgb(92, 99, 108);

    public DarkScrollPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint |
                 ControlStyles.Selectable, true);
        TabStop = true;
        AutoScroll = false;
    }

    public void SetContent(Control content)
    {
        Controls.Clear();
        _content = content;
        content.Location = Point.Empty;
        content.SizeChanged += (_, _) => UpdateContentBounds();
        Controls.Add(content);
        HookMouseWheel(content);
        UpdateContentBounds();
    }

    public void ScrollToTop()
    {
        _offset = 0;
        UpdateContentBounds();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateContentBounds();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        if (!ContainsFocus) Focus();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollBy(e.Delta > 0 ? -72 : 72);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !CanScroll) return;
        var thumb = ThumbRectangle();
        if (thumb.Contains(e.Location))
        {
            _draggingThumb = true;
            _dragStartY = e.Y;
            _dragStartOffset = _offset;
            Capture = true;
        }
        else if (e.X >= Width - ScrollBarWidth)
        {
            ScrollBy(e.Y < thumb.Top ? -ClientSize.Height : ClientSize.Height);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_draggingThumb || !CanScroll) return;
        var trackTravel = Math.Max(1, ClientSize.Height - ThumbRectangle().Height - 8);
        var contentTravel = Math.Max(1, MaximumOffset);
        _offset = Math.Clamp(_dragStartOffset + (e.Y - _dragStartY) * contentTravel / trackTravel, 0, MaximumOffset);
        UpdateContentBounds();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _draggingThumb = false;
        Capture = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!CanScroll) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var track = new Rectangle(Width - ScrollBarWidth, 0, ScrollBarWidth, Height);
        using var trackBrush = new SolidBrush(TrackColor);
        e.Graphics.FillRectangle(trackBrush, track);
        var thumb = ThumbRectangle();
        using var thumbBrush = new SolidBrush(_draggingThumb ? ThumbHoverColor : ThumbColor);
        using var path = RoundedRectangle(thumb, 3);
        e.Graphics.FillPath(thumbBrush, path);
    }

    private bool CanScroll => MaximumOffset > 0;
    private int MaximumOffset => _content is null ? 0 : Math.Max(0, _content.Height - ClientSize.Height);

    private void ScrollBy(int amount)
    {
        if (!CanScroll) return;
        _offset = Math.Clamp(_offset + amount, 0, MaximumOffset);
        UpdateContentBounds();
    }

    private void UpdateContentBounds()
    {
        if (_content is null) return;
        _offset = Math.Clamp(_offset, 0, MaximumOffset);
        _content.Location = new Point(0, -_offset);
        Invalidate();
    }

    private Rectangle ThumbRectangle()
    {
        var trackHeight = Math.Max(1, ClientSize.Height - 8);
        var contentHeight = Math.Max(1, _content?.Height ?? ClientSize.Height);
        var thumbHeight = Math.Clamp(trackHeight * ClientSize.Height / contentHeight, 34, trackHeight);
        var travel = Math.Max(0, trackHeight - thumbHeight);
        var top = 4 + (MaximumOffset == 0 ? 0 : travel * _offset / MaximumOffset);
        return new Rectangle(Width - 7, top, 4, thumbHeight);
    }

    private void HookMouseWheel(Control control)
    {
        if (control is not TextBoxBase and not ComboBox and not NumericUpDown)
        {
            control.MouseEnter += (_, _) =>
            {
                if (!ContainsFocus) Focus();
            };
            control.MouseWheel += (_, e) => ScrollBy(e.Delta > 0 ? -72 : 72);
        }
        control.ControlAdded += (_, e) =>
        {
            if (e.Control is not null) HookMouseWheel(e.Control);
        };
        foreach (Control child in control.Controls) HookMouseWheel(child);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
