using System.Drawing.Drawing2D;
using InvoiceDesk.Core;

namespace InvoiceDesk.Desktop;

public sealed class DashboardView : UserControl, IReloadable
{
    private readonly MainForm _app;
    private readonly (Label Value, Label Caption)[] _kpi = new (Label, Label)[4];
    private readonly Panel _chart;
    private readonly DataGridView _recent = Theme.Grid();
    private DashboardStats? _stats;

    public DashboardView(MainForm app)
    {
        _app = app;
        BackColor = Theme.Page;

        var cards = new TableLayoutPanel { Dock = DockStyle.Top, Height = 120, ColumnCount = 4, RowCount = 1 };
        for (var i = 0; i < 4; i++)
        {
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            var card = Theme.CardPanel();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(i == 0 ? 0 : 8, 0, i == 3 ? 0 : 8, 0);
            var cap = new Label { AutoSize = true, Font = Theme.Body, ForeColor = Theme.Muted, Location = new Point(20, 18) };
            var val = new Label { AutoSize = true, Font = Theme.Big, ForeColor = Theme.Text, Location = new Point(18, 46) };
            card.Controls.Add(cap);
            card.Controls.Add(val);
            _kpi[i] = (val, cap);
            cards.Controls.Add(card, i, 0);
        }

        var middle = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 16, 0, 0) };
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));

        _chart = Theme.CardPanel();
        _chart.Dock = DockStyle.Fill;
        _chart.Margin = new Padding(0, 0, 8, 0);
        _chart.Paint += PaintChart;
        _chart.Resize += (_, _) => _chart.Invalidate();

        var recentCard = Theme.CardPanel();
        recentCard.Dock = DockStyle.Fill;
        recentCard.Margin = new Padding(8, 0, 0, 0);
        var title = new Label { Text = "Recent invoices", Font = Theme.H2, ForeColor = Theme.Text, Dock = DockStyle.Top, Height = 40 };
        _recent.Dock = DockStyle.Fill;
        _recent.Columns.Add("num", "Invoice");
        _recent.Columns.Add("cust", "Customer");
        _recent.Columns.Add("total", "Total");
        _recent.Columns["total"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        recentCard.Controls.Add(_recent);
        recentCard.Controls.Add(title);

        middle.Controls.Add(_chart, 0, 0);
        middle.Controls.Add(recentCard, 1, 0);
        Controls.Add(middle);
        Controls.Add(cards);
    }

    public void Reload()
    {
        _stats = _app.Store.GetDashboard(DateTime.Today);
        string[] caps = { "Revenue this month", "Invoices this month", "Low-stock items", "Customers" };
        string[] vals =
        {
            Theme.Money(_stats.RevenueThisMonth), _stats.InvoicesThisMonth.ToString(),
            _stats.LowStockItems.ToString(), _stats.Customers.ToString(),
        };
        for (var i = 0; i < 4; i++)
        {
            _kpi[i].Caption.Text = caps[i];
            _kpi[i].Value.Text = vals[i];
            _kpi[i].Value.ForeColor = i == 2 && _stats.LowStockItems > 0 ? Theme.Danger : Theme.Text;
        }
        _recent.Rows.Clear();
        foreach (var inv in _app.Store.GetInvoices().Take(9))
            _recent.Rows.Add(inv.Number, inv.CustomerName, Theme.Money(inv.Total));
        _recent.ClearSelection();
        _chart.Invalidate();
    }

    private void PaintChart(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (var pen = new Pen(Theme.Border)) g.DrawRectangle(pen, 0, 0, _chart.Width - 1, _chart.Height - 1);
        TextRenderer.DrawText(g, "Revenue, last 14 days", Theme.H2, new Point(20, 18), Theme.Text);
        if (_stats is null) return;

        var days = _stats.Last14Days;
        var max = Math.Max(100m, days.Max(d => d.Amount));
        var area = new Rectangle(70, 70, _chart.Width - 100, _chart.Height - 130);
        using var grid = new Pen(Color.FromArgb(238, 240, 244));
        for (var k = 0; k <= 4; k++)
        {
            var y = area.Bottom - area.Height * k / 4;
            g.DrawLine(grid, area.Left, y, area.Right, y);
            TextRenderer.DrawText(g, "$" + (max * k / 4).ToString("#,##0"), Theme.Small, new Rectangle(0, y - 9, 62, 18), Theme.Muted,
                                  TextFormatFlags.Right);
        }
        var slot = area.Width / (float)days.Count;
        using var bar = new SolidBrush(Theme.Accent);
        using var today = new SolidBrush(Color.FromArgb(96, 165, 250));
        for (var i = 0; i < days.Count; i++)
        {
            var h = (float)(days[i].Amount / max) * area.Height;
            var x = area.Left + i * slot + slot * 0.2f;
            var r = new RectangleF(x, area.Bottom - h, slot * 0.6f, h);
            if (h > 0) g.FillRectangle(i == days.Count - 1 ? today : bar, r);
            if (i % 2 == 1 || i == days.Count - 1)
                TextRenderer.DrawText(g, days[i].Day.ToString("d MMM"), Theme.Small,
                                      new Rectangle((int)(x - 14), area.Bottom + 8, (int)slot + 28, 18), Theme.Muted, TextFormatFlags.HorizontalCenter);
        }
    }
}
