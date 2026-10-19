namespace InvoiceDesk.Desktop;

internal static class Theme
{
    /// <summary>UI scale for fonts and sizes. Set before any window is created (the recorder uses 1.25, like a 125% display).</summary>
    public static float Scale = 1f;

    static Theme() { }   // no beforefieldinit: the fonts below are created on first use, after Scale is set

    public static readonly Color Sidebar = Color.FromArgb(17, 24, 39);
    public static readonly Color SidebarHover = Color.FromArgb(31, 41, 55);
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color Page = Color.FromArgb(243, 245, 248);
    public static readonly Color Card = Color.White;
    public static readonly Color Border = Color.FromArgb(226, 230, 236);
    public static readonly Color Text = Color.FromArgb(17, 24, 39);
    public static readonly Color Muted = Color.FromArgb(107, 114, 128);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);
    public static readonly Color DangerSoft = Color.FromArgb(254, 242, 242);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);

    public static readonly Font Body = new("Segoe UI", 10f * Scale);
    public static readonly Font BodyBold = new("Segoe UI Semibold", 10f * Scale);
    public static readonly Font Small = new("Segoe UI", 9f * Scale);
    public static readonly Font H1 = new("Segoe UI Semibold", 17f * Scale);
    public static readonly Font H2 = new("Segoe UI Semibold", 12.5f * Scale);
    public static readonly Font Big = new("Segoe UI Semibold", 22f * Scale);

    public static DataGridView Grid()
    {
        var g = new DataGridView
        {
            BackgroundColor = Card,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Border,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = (int)(40 * Scale),
            Font = Body,
        };
        g.RowTemplate.Height = (int)(36 * Scale);
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
        g.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
        g.ColumnHeadersDefaultCellStyle.Font = BodyBold;
        g.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
        g.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        g.DefaultCellStyle.SelectionForeColor = Text;
        g.DefaultCellStyle.ForeColor = Text;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = g.ColumnHeadersDefaultCellStyle.BackColor;
        g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Muted;
        // nothing should look selected until the user clicks a row
        g.VisibleChanged += (_, _) => { g.ClearSelection(); g.CurrentCell = null; };
        g.HandleCreated += (_, _) => g.BeginInvoke(() => { g.ClearSelection(); g.CurrentCell = null; });
        return g;
    }

    public static Button Primary(string text) => StyledButton(text, Accent, Color.White);
    public static Button Secondary(string text) => StyledButton(text, Color.White, Text);

    private static Button StyledButton(string text, Color back, Color fore)
    {
        var b = new Button
        {
            Text = text, BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = BodyBold,
            Height = (int)(38 * Scale), AutoSize = true, Padding = new Padding(14, 0, 14, 0), Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderColor = back == Color.White ? Border : back;
        return b;
    }

    public static Panel CardPanel()
    {
        var p = new Panel { BackColor = Card, Padding = new Padding(20) };
        p.Paint += (_, e) =>
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        return p;
    }

    public static string Money(decimal v) => v.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
}
