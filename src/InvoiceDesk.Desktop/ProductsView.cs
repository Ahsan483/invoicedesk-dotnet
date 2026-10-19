using InvoiceDesk.Core;

namespace InvoiceDesk.Desktop;

public sealed class ProductsView : UserControl, IReloadable
{
    private readonly MainForm _app;
    public TextBox Search { get; } = new() { Font = Theme.Body, Width = 320, PlaceholderText = "Search name, SKU or category" };
    public CheckBox LowOnly { get; } = new() { Text = "Low stock only", AutoSize = true, Font = Theme.Body, Margin = new Padding(16, 9, 0, 0) };
    public Button Add { get; } = Theme.Primary("+  Add product");
    private readonly DataGridView _grid = Theme.Grid();
    private readonly Label _count = new() { AutoSize = true, Font = Theme.Body, ForeColor = Theme.Muted, Margin = new Padding(16, 10, 0, 0) };
    private List<Product> _rows = new();

    public ProductsView(MainForm app)
    {
        _app = app;
        BackColor = Theme.Page;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, WrapContents = false };
        Search.Margin = new Padding(0, 6, 0, 0);
        bar.Controls.AddRange(new Control[] { Search, LowOnly, _count });
        var right = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 200, FlowDirection = FlowDirection.RightToLeft };
        right.Controls.Add(Add);
        var top = new Panel { Dock = DockStyle.Top, Height = 54 };
        top.Controls.Add(bar);
        top.Controls.Add(right);

        var card = Theme.CardPanel();
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(1);
        _grid.Dock = DockStyle.Fill;
        foreach (var (key, title) in new[] { ("sku", "SKU"), ("name", "Product"), ("cat", "Category"), ("price", "Price"), ("stock", "In stock"), ("re", "Reorder at") })
            _grid.Columns.Add(key, title);
        _grid.Columns["name"]!.FillWeight = 220;
        foreach (var c in new[] { "price", "stock", "re" })
            _grid.Columns[c]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.CellFormatting += HighlightLowStock;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Edit(_rows[e.RowIndex]); };
        card.Controls.Add(_grid);

        Controls.Add(card);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
        Controls.Add(top);

        Search.TextChanged += (_, _) => Reload();
        LowOnly.CheckedChanged += (_, _) => Reload();
        Add.Click += (_, _) => Edit(new Product(0, "", "", "Accessories", 0, 0, 5));
    }

    public void Reload()
    {
        _rows = _app.Store.GetProducts(Search.Text);
        if (LowOnly.Checked) _rows = _rows.Where(p => p.IsLowStock).ToList();
        _grid.Rows.Clear();
        foreach (var p in _rows)
            _grid.Rows.Add(p.Sku, p.Name, p.Category, Theme.Money(p.Price), p.Stock, p.ReorderLevel);
        _grid.ClearSelection();
        var low = _rows.Count(p => p.IsLowStock);
        _count.Text = $"{_rows.Count} products" + (low > 0 ? $"  ·  {low} need reordering" : "");
    }

    private void HighlightLowStock(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _rows.Count || !_rows[e.RowIndex].IsLowStock || e.CellStyle is null) return;
        e.CellStyle.BackColor = Theme.DangerSoft;
        if (_grid.Columns[e.ColumnIndex].Name == "stock")
        {
            e.CellStyle.ForeColor = Theme.Danger;
            e.CellStyle.Font = Theme.BodyBold;
        }
    }

    private void Edit(Product p)
    {
        using var dlg = new ProductDialog(p);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _app.Store.SaveProduct(dlg.Result);
            _app.SetStatus($"Saved {dlg.Result.Name}");
            Reload();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

internal sealed class ProductDialog : Form
{
    public Product Result { get; private set; }

    public ProductDialog(Product p)
    {
        Result = p;
        Text = p.Id == 0 ? "Add product" : "Edit product";
        Font = Theme.Body;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 360);
        BackColor = Color.White;

        var sku = new TextBox { Text = p.Sku };
        var name = new TextBox { Text = p.Name };
        var cat = new ComboBox { Text = p.Category, DropDownStyle = ComboBoxStyle.DropDown };
        cat.Items.AddRange(new object[] { "Accessories", "Audio", "Cables", "Displays", "Office", "Power", "Storage", "Video" });
        var price = new NumericUpDown { DecimalPlaces = 2, Maximum = 100000, Value = p.Price };
        var stock = new NumericUpDown { Maximum = 100000, Value = p.Stock };
        var reorder = new NumericUpDown { Maximum = 100000, Value = p.ReorderLevel };
        var y = 20;
        foreach (var (label, ctl) in new (string, Control)[] { ("SKU", sku), ("Name", name), ("Category", cat), ("Price ($)", price), ("In stock", stock), ("Reorder at", reorder) })
        {
            Controls.Add(new Label { Text = label, Location = new Point(20, y + 4), AutoSize = true, ForeColor = Theme.Muted });
            ctl.Location = new Point(130, y);
            ctl.Width = 260;
            Controls.Add(ctl);
            y += 44;
        }
        var ok = Theme.Primary("Save");
        ok.Location = new Point(290, y + 6);
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { MessageBox.Show(this, "Name is required."); return; }
            Result = p with { Sku = sku.Text.Trim(), Name = name.Text.Trim(), Category = cat.Text.Trim(),
                              Price = price.Value, Stock = (int)stock.Value, ReorderLevel = (int)reorder.Value };
            DialogResult = DialogResult.OK;
        };
        Controls.Add(ok);
        AcceptButton = ok;
    }
}
