using System.Runtime.InteropServices;
using InvoiceDesk.Core;

namespace InvoiceDesk.Desktop;

public sealed class MainForm : Form
{
    public InvoiceStore Store { get; }
    public Panel Root { get; } = new() { Dock = DockStyle.Fill, BackColor = Theme.Page };
    public Dictionary<string, Button> Nav { get; } = new();
    public DashboardView Dashboard { get; }
    public ProductsView Products { get; }
    public InvoiceEditorView Editor { get; }
    public InvoicesView Invoices { get; }
    public InvoicePreviewView Preview { get; }

    private readonly Panel _content = new() { Dock = DockStyle.Fill, Padding = new Padding(28, 20, 28, 20), BackColor = Theme.Page };
    private readonly Label _title = new() { AutoSize = true, Font = Theme.H1, ForeColor = Theme.Text, Location = new Point(28, 16) };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 30, Font = Theme.Small, ForeColor = Theme.Muted,
                                             BackColor = Color.White, Padding = new Padding(16, 6, 0, 0) };

    public MainForm(InvoiceStore store)
    {
        Store = store;
        Text = "InvoiceDesk";
        Font = Theme.Body;
        ClientSize = new Size(1440, 860);
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;

        Dashboard = new DashboardView(this);
        Products = new ProductsView(this);
        Editor = new InvoiceEditorView(this);
        Invoices = new InvoicesView(this);
        Preview = new InvoicePreviewView(this);

        var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.White };
        header.Controls.Add(_title);
        var today = new Label { AutoSize = true, Font = Theme.Body, ForeColor = Theme.Muted, Anchor = AnchorStyles.Top | AnchorStyles.Right,
                                Text = DateTime.Today.ToString("dddd, d MMMM yyyy") };
        header.Controls.Add(today);
        header.Resize += (_, _) => today.Location = new Point(header.Width - today.Width - 28, 22);
        header.Paint += (_, e) => e.Graphics.DrawLine(new Pen(Theme.Border), 0, header.Height - 1, header.Width, header.Height - 1);

        Root.Controls.Add(_content);
        Root.Controls.Add(header);
        Root.Controls.Add(_status);
        Root.Controls.Add(BuildSidebar());
        Controls.Add(Root);

        if (Theme.Scale != 1f)
        {
            // positions and sizes; fonts are scaled in Theme. Views are scaled on their own because only one is attached at a time.
            var k = new SizeF(Theme.Scale, Theme.Scale);
            Root.Scale(k);
            foreach (Control v in new Control[] { Dashboard, Products, Editor, Invoices, Preview }) v.Scale(k);
        }
        SetStatus($"Connected to {Path.GetFileName("invoicedesk.db")}  ·  {Store.GetProducts().Count} products  ·  {Store.GetCustomers().Count} customers");
        Show("Dashboard");
    }

    private Panel BuildSidebar()
    {
        var side = new Panel { Dock = DockStyle.Left, Width = 240, BackColor = Theme.Sidebar };
        var logo = new Label { Text = "InvoiceDesk", Font = new Font("Segoe UI Semibold", 16f * Theme.Scale), ForeColor = Color.White,
                               AutoSize = true, Location = new Point(24, 22) };
        var sub = new Label { Text = "Sales & stock", Font = Theme.Small, ForeColor = Color.FromArgb(156, 163, 175),
                              AutoSize = true, Location = new Point(26, 54) };
        side.Controls.Add(logo);
        side.Controls.Add(sub);
        var y = 104;
        foreach (var name in new[] { "Dashboard", "Products", "New invoice", "Invoices" })
        {
            var b = new Button
            {
                Text = "   " + name, TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(209, 213, 219),
                BackColor = Theme.Sidebar, Font = Theme.Body, Size = new Size(208, 42), Location = new Point(16, y), Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Theme.SidebarHover;
            b.Click += (_, _) => Show(name);
            Nav[name] = b;
            side.Controls.Add(b);
            y += 48;
        }
        var store = new Label { Text = "Main branch\nv1.0  ·  .NET 10", Font = Theme.Small, ForeColor = Color.FromArgb(107, 114, 128),
                                AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
        side.Controls.Add(store);
        side.Resize += (_, _) => store.Location = new Point(26, side.Height - 64);
        return side;
    }

    public void Show(string page)
    {
        foreach (var (name, b) in Nav)
        {
            var on = name == page;
            b.BackColor = on ? Theme.Accent : Theme.Sidebar;
            b.ForeColor = on ? Color.White : Color.FromArgb(209, 213, 219);
        }
        UserControl view = page switch
        {
            "Products" => Products,
            "New invoice" => Editor,
            "Invoices" => Invoices,
            "Preview" => Preview,
            _ => Dashboard,
        };
        _title.Text = page == "Preview" ? "Invoice " + Preview.Current?.Number : page;
        (view as IReloadable)?.Reload();
        _content.Controls.Clear();
        view.Dock = DockStyle.Fill;
        _content.Controls.Add(view);
        ClearGridSelection(view);
    }

    // a freshly shown grid selects its first cell; nothing should look selected until the user clicks
    private static void ClearGridSelection(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is DataGridView g) { g.ClearSelection(); g.CurrentCell = null; }
            ClearGridSelection(c);
        }
    }

    public void SetStatus(string text) => _status.Text = text;

    // let the window be larger than the screen (needed for 1920x1080 recordings on scaled displays)
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x0024) // WM_GETMINMAXINFO
        {
            var info = Marshal.PtrToStructure<MinMaxInfo>(m.LParam);
            info.MaxTrackSize = new Point(4096, 4096);
            Marshal.StructureToPtr(info, m.LParam, false);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }
}

public interface IReloadable
{
    void Reload();
}
