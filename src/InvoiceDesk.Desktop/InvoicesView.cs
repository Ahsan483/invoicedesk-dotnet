using InvoiceDesk.Core;

namespace InvoiceDesk.Desktop;

public sealed class InvoicesView : UserControl, IReloadable
{
    private readonly MainForm _app;
    public DateTimePicker From { get; } = new() { Format = DateTimePickerFormat.Short, Width = 130, Font = Theme.Body };
    public DateTimePicker To { get; } = new() { Format = DateTimePickerFormat.Short, Width = 130, Font = Theme.Body };
    public TextBox CustomerFilter { get; } = new() { Width = 260, Font = Theme.Body, PlaceholderText = "Filter by customer" };
    public Button Export { get; } = Theme.Secondary("Export CSV");
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string? ExportFolder { get; set; }          // set by the demo recorder to skip the save dialog
    private readonly DataGridView _grid = Theme.Grid();
    private readonly Label _summary = new() { AutoSize = true, Font = Theme.Body, ForeColor = Theme.Muted, Margin = new Padding(16, 10, 0, 0) };
    private List<Invoice> _rows = new();

    public InvoicesView(MainForm app)
    {
        _app = app;
        BackColor = Theme.Page;
        From.Value = DateTime.Today.AddDays(-30);
        To.Value = DateTime.Today;

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        foreach (var c in new Control[] { new Label { Text = "From", AutoSize = true, Margin = new Padding(0, 10, 6, 0), ForeColor = Theme.Muted }, From,
                                          new Label { Text = "To", AutoSize = true, Margin = new Padding(14, 10, 6, 0), ForeColor = Theme.Muted }, To,
                                          CustomerFilter, _summary })
        {
            if (c is not Label) c.Margin = new Padding(c == CustomerFilter ? 18 : 0, 6, 0, 0);
            bar.Controls.Add(c);
        }
        var right = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 160, FlowDirection = FlowDirection.RightToLeft };
        right.Controls.Add(Export);
        var top = new Panel { Dock = DockStyle.Top, Height = 54 };
        top.Controls.Add(bar);
        top.Controls.Add(right);

        var card = Theme.CardPanel();
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(1);
        _grid.Dock = DockStyle.Fill;
        foreach (var (k, t) in new[] { ("num", "Invoice"), ("date", "Date"), ("cust", "Customer"), ("items", "Items"), ("tax", "Tax"), ("total", "Total") })
            _grid.Columns.Add(k, t);
        _grid.Columns["cust"]!.FillWeight = 200;
        foreach (var c in new[] { "items", "tax", "total" })
            _grid.Columns[c]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            _app.Preview.Current = _rows[e.RowIndex];
            _app.Show("Preview");
        };
        card.Controls.Add(_grid);

        Controls.Add(card);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
        Controls.Add(top);

        From.ValueChanged += (_, _) => Reload();
        To.ValueChanged += (_, _) => Reload();
        CustomerFilter.TextChanged += (_, _) => Reload();
        Export.Click += (_, _) => ExportCsv();
    }

    public void Reload()
    {
        _rows = _app.Store.GetInvoices(From.Value.Date, To.Value.Date, CustomerFilter.Text);
        _grid.Rows.Clear();
        foreach (var i in _rows)
            _grid.Rows.Add(i.Number, i.Date.ToString("dd MMM yyyy  HH:mm"), i.CustomerName, i.Lines.Sum(l => l.Quantity),
                           Theme.Money(i.Tax), Theme.Money(i.Total));
        _grid.ClearSelection();
        _summary.Text = $"{_rows.Count} invoices  ·  {Theme.Money(_rows.Sum(i => i.Total))}";
    }

    private void ExportCsv()
    {
        string path;
        if (ExportFolder is not null)
        {
            path = Path.Combine(ExportFolder, "invoices.csv");
        }
        else
        {
            using var dlg = new SaveFileDialog { Filter = "CSV file|*.csv", FileName = "invoices.csv" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            path = dlg.FileName;
        }
        File.WriteAllText(path, InvoiceStore.ToCsv(_rows));
        _app.SetStatus($"Exported {_rows.Count} invoices to {Path.GetFileName(path)}");
    }
}
