using InvoiceDesk.Core;

namespace InvoiceDesk.Tests;

public sealed class InvoiceStoreTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"invoicedesk-test-{Guid.NewGuid():N}.db");
    private readonly InvoiceStore _store;

    public InvoiceStoreTests() => _store = new InvoiceStore(_db);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_db);
    }

    [Fact]
    public void Totals_add_tax_to_the_subtotal()
    {
        var inv = new Invoice { TaxRate = 0.08m };
        inv.Lines.Add(new InvoiceLine(1, "A", 2, 10.00m));
        inv.Lines.Add(new InvoiceLine(2, "B", 1, 5.50m));

        Assert.Equal(25.50m, inv.Subtotal);
        Assert.Equal(2.04m, inv.Tax);
        Assert.Equal(27.54m, inv.Total);
    }

    [Fact]
    public void Saving_an_invoice_takes_items_out_of_stock()
    {
        var p = _store.GetProducts().First(x => x.Sku == "SS-1T0");
        var inv = new Invoice { CustomerId = 1, Date = DateTime.Now };
        inv.Lines.Add(new InvoiceLine(p.Id, p.Name, 3, p.Price));

        _store.SaveInvoice(inv);

        Assert.Equal(p.Stock - 3, _store.GetProducts().First(x => x.Id == p.Id).Stock);
    }

    [Fact]
    public void Selling_more_than_in_stock_is_rejected_and_nothing_is_saved()
    {
        var p = _store.GetProducts().First(x => x.Sku == "MN-270");   // 4 in stock
        var before = _store.GetInvoices().Count;
        var inv = new Invoice { CustomerId = 1, Date = DateTime.Now };
        inv.Lines.Add(new InvoiceLine(p.Id, p.Name, p.Stock + 1, p.Price));

        var ex = Assert.Throws<InvalidOperationException>(() => _store.SaveInvoice(inv));

        Assert.Contains("left in stock", ex.Message);
        Assert.Equal(before, _store.GetInvoices().Count);
        Assert.Equal(p.Stock, _store.GetProducts().First(x => x.Id == p.Id).Stock);
    }

    [Fact]
    public void Empty_invoices_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => _store.SaveInvoice(new Invoice { CustomerId = 1, Date = DateTime.Now }));
    }

    [Fact]
    public void Low_stock_is_flagged_at_the_reorder_level()
    {
        var low = _store.GetProducts().Where(p => p.IsLowStock).Select(p => p.Sku).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { "CB-045", "HD-500", "MN-270", "WC-108" }, low);
    }

    [Fact]
    public void Search_matches_name_sku_and_category()
    {
        Assert.All(_store.GetProducts("ssd"), p => Assert.Contains("SSD", p.Name));
        Assert.Single(_store.GetProducts("HD-500"));
        Assert.Equal(2, _store.GetProducts("Audio").Count);
    }

    [Fact]
    public void Csv_quotes_customer_names_with_commas()
    {
        var inv = new Invoice { Id = 7, CustomerName = "Smith, Jones & Co", Date = new DateTime(2026, 1, 5) };
        inv.Lines.Add(new InvoiceLine(1, "A", 1, 10m));

        var csv = InvoiceStore.ToCsv(new[] { inv });

        Assert.Contains("INV-00007,2026-01-05,\"Smith, Jones & Co\",1,10.00,0.80,10.80", csv);
    }

    [Fact]
    public void Dashboard_reports_fourteen_days_of_revenue()
    {
        var stats = _store.GetDashboard(DateTime.Today);
        Assert.Equal(14, stats.Last14Days.Count);
        Assert.Equal(DateTime.Today, stats.Last14Days[^1].Day);
        Assert.Equal(10, stats.Customers);
    }
}
