namespace InvoiceDesk.Core;

public sealed record Product(int Id, string Sku, string Name, string Category, decimal Price, int Stock, int ReorderLevel)
{
    public bool IsLowStock => Stock <= ReorderLevel;
}

public sealed record Customer(int Id, string Name, string City, string Phone);

public sealed record InvoiceLine(int ProductId, string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal Total => Quantity * UnitPrice;
}

public sealed class Invoice
{
    public int Id { get; set; }
    public string Number => $"INV-{Id:D5}";
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public DateTime Date { get; set; }
    public List<InvoiceLine> Lines { get; } = new();
    public decimal TaxRate { get; set; } = 0.08m;   // 8% sales tax

    public decimal Subtotal => Lines.Sum(l => l.Total);
    public decimal Tax => Math.Round(Subtotal * TaxRate, 2);
    public decimal Total => Subtotal + Tax;
}

public sealed record DailyRevenue(DateTime Day, decimal Amount);

public sealed record DashboardStats(decimal RevenueThisMonth, int InvoicesThisMonth, int LowStockItems, int Customers,
                                    IReadOnlyList<DailyRevenue> Last14Days);
