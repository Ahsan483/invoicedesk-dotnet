using InvoiceDesk.Core;

namespace InvoiceDesk.Desktop;

public sealed class InvoiceEditorView : UserControl, IReloadable
{
    private readonly MainForm _app;
    public ComboBox Customer { get; } = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330, Font = Theme.Body };
    public ComboBox Product { get; } = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330, Font = Theme.Body };
    public NumericUpDown Quantity { get; } = new() { Minimum = 1, Maximum = 999, Value = 1, Width = 90, Font = Theme.Body };
    public Button AddLine { get; } = Theme.Secondary("Add line");
    public Button Save { get; } = Theme.Primary("Save invoice");
    private readonly Label _message = new() { AutoSize = true, Font = Theme.Body, ForeColor = Theme.Danger };
    private readonly DataGridView _lines = Theme.Grid();
    private readonly Label _subtotal = new(), _tax = new(), _total = new();
    private Invoice _draft = new();
    private List<Product> _products = new();

    public InvoiceEditorView(MainForm app)
    {
        _app = app;
        BackColor = Theme.Page;

        var form = Theme.CardPanel();
        form.Dock = DockStyle.Top;
        form.Height = 150;
        form.Controls.Add(new Label { Text = "Customer", Location = new Point(22, 20), AutoSize = true, ForeColor = Theme.Muted });
        Customer.Location = new Point(22, 44);
        form.Controls.Add(Customer);
        form.Controls.Add(new Label { Text = "Product", Location = new Point(380, 20), AutoSize = true, ForeColor = Theme.Muted });
        Product.Location = new Point(380, 44);
        form.Controls.Add(Product);
        form.Controls.Add(new Label { Text = "Qty", Location = new Point(730, 20), AutoSize = true, ForeColor = Theme.Muted });
        Quantity.Location = new Point(730, 44);
        form.Controls.Add(Quantity);
        AddLine.Location = new Point(840, 40);
        form.Controls.Add(AddLine);
        _message.Location = new Point(22, 98);
        form.Controls.Add(_message);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 16, 0, 0) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));

        var linesCard = Theme.CardPanel();
        linesCard.Dock = DockStyle.Fill;
        linesCard.Margin = new Padding(0, 0, 8, 0);
        linesCard.Padding = new Padding(1);
        _lines.Dock = DockStyle.Fill;
        foreach (var (k, t) in new[] { ("item", "Item"), ("qty", "Qty"), ("price", "Unit price"), ("total", "Line total") })
            _lines.Columns.Add(k, t);
        _lines.Columns["item"]!.FillWeight = 240;
        foreach (var c in new[] { "qty", "price", "total" })
            _lines.Columns[c]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        linesCard.Controls.Add(_lines);

        var sum = Theme.CardPanel();
        sum.Dock = DockStyle.Fill;
        sum.Margin = new Padding(8, 0, 0, 0);
        sum.Controls.Add(new Label { Text = "Summary", Font = Theme.H2, Location = new Point(22, 18), AutoSize = true });
        var y = 66;
        foreach (var (caption, value, big) in new[] { ("Subtotal", _subtotal, false), ("Sales tax (8%)", _tax, false), ("Total", _total, true) })
        {
            sum.Controls.Add(new Label { Text = caption, Location = new Point(22, y), AutoSize = true,
                                         ForeColor = big ? Theme.Text : Theme.Muted, Font = big ? Theme.H2 : Theme.Body });
            value.AutoSize = true;
            value.Font = big ? Theme.Big : Theme.Body;
            value.ForeColor = Theme.Text;
            value.Location = new Point(200, big ? y - 8 : y);
            sum.Controls.Add(value);
            y += big ? 60 : 36;
        }
        Save.Location = new Point(22, y + 10);
        sum.Controls.Add(Save);
        sum.Resize += (_, _) => { foreach (var l in new[] { _subtotal, _tax, _total }) l.Left = sum.Width - l.Width - 24; };

        body.Controls.Add(linesCard, 0, 0);
        body.Controls.Add(sum, 1, 0);
        Controls.Add(body);
        Controls.Add(form);

        AddLine.Click += (_, _) => AddCurrentLine();
        Save.Click += (_, _) => SaveInvoice();
    }

    public void Reload()
    {
        var keepCustomer = Customer.SelectedIndex;
        Customer.DataSource = _app.Store.GetCustomers();
        Customer.DisplayMember = nameof(Core.Customer.Name);
        Customer.SelectedIndex = keepCustomer >= 0 && keepCustomer < Customer.Items.Count ? keepCustomer : -1;
        _products = _app.Store.GetProducts();
        Product.DataSource = _products.Select(p => $"{p.Name}  ({p.Stock} in stock)").ToList();
        Product.SelectedIndex = -1;
        Refresh_();
    }

    public void SelectProduct(string name) => Product.SelectedIndex = _products.FindIndex(p => p.Name == name);

    private void AddCurrentLine()
    {
        _message.Text = "";
        if (Product.SelectedIndex < 0) { _message.Text = "Choose a product first."; return; }
        var p = _products[Product.SelectedIndex];
        var already = _draft.Lines.Where(l => l.ProductId == p.Id).Sum(l => l.Quantity);
        if (already + Quantity.Value > p.Stock)
        {
            _message.Text = $"Only {p.Stock} x {p.Name} in stock. Reduce the quantity or reorder.";
            return;
        }
        _draft.Lines.Add(new InvoiceLine(p.Id, p.Name, (int)Quantity.Value, p.Price));
        Quantity.Value = 1;
        Refresh_();
    }

    private void SaveInvoice()
    {
        _message.Text = "";
        if (Customer.SelectedItem is not Core.Customer c) { _message.Text = "Choose a customer."; return; }
        if (_draft.Lines.Count == 0) { _message.Text = "Add at least one line."; return; }
        _draft.CustomerId = c.Id;
        _draft.CustomerName = c.Name;
        _draft.Date = DateTime.Now;
        try
        {
            _app.Store.SaveInvoice(_draft);
            _app.SetStatus($"Saved {_draft.Number} for {c.Name}  ·  {Theme.Money(_draft.Total)}  ·  stock updated");
            _app.Preview.Current = _draft;
            _draft = new Invoice();
            Customer.SelectedIndex = -1;
            _app.Show("Preview");
        }
        catch (InvalidOperationException ex)
        {
            _message.Text = ex.Message;
        }
    }

    private void Refresh_()
    {
        _lines.Rows.Clear();
        foreach (var l in _draft.Lines)
            _lines.Rows.Add(l.ProductName, l.Quantity, Theme.Money(l.UnitPrice), Theme.Money(l.Total));
        _lines.ClearSelection();
        _subtotal.Text = Theme.Money(_draft.Subtotal);
        _tax.Text = Theme.Money(_draft.Tax);
        _total.Text = Theme.Money(_draft.Total);
        foreach (var l in new[] { _subtotal, _tax, _total }) l.Left = l.Parent!.Width - l.PreferredWidth - 24;
    }
}
