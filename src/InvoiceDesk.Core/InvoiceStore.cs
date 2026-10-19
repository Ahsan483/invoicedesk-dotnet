using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace InvoiceDesk.Core;

/// <summary>
/// All data access for both apps. Plain ADO.NET on SQLite, so the desktop and web
/// front ends share exactly the same business rules (stock checks, tax, totals).
/// </summary>
public sealed class InvoiceStore
{
    private readonly string _cs;

    public InvoiceStore(string dbPath)
    {
        _cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        using var c = Open();
        Exec(c, """
            CREATE TABLE IF NOT EXISTS products (id INTEGER PRIMARY KEY, sku TEXT UNIQUE NOT NULL, name TEXT NOT NULL,
                category TEXT NOT NULL, price REAL NOT NULL, stock INTEGER NOT NULL, reorder_level INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS customers (id INTEGER PRIMARY KEY, name TEXT NOT NULL, city TEXT NOT NULL, phone TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS invoices (id INTEGER PRIMARY KEY, customer_id INTEGER NOT NULL REFERENCES customers(id),
                date TEXT NOT NULL, tax_rate REAL NOT NULL);
            CREATE TABLE IF NOT EXISTS invoice_lines (invoice_id INTEGER NOT NULL REFERENCES invoices(id),
                product_id INTEGER NOT NULL REFERENCES products(id), quantity INTEGER NOT NULL, unit_price REAL NOT NULL);
            """);
        if (Scalar<long>(c, "SELECT COUNT(*) FROM products") == 0)
            SeedData.Fill(this);
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        Exec(c, "PRAGMA foreign_keys = ON;");
        return c;
    }

    private static void Exec(SqliteConnection c, string sql, params (string, object)[] args)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
        cmd.ExecuteNonQuery();
    }

    private static T Scalar<T>(SqliteConnection c, string sql, params (string, object)[] args)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
        var r = cmd.ExecuteScalar();
        return r is null or DBNull ? default! : (T)Convert.ChangeType(r, typeof(T), CultureInfo.InvariantCulture);
    }

    // ---------------------------------------------------------------- products

    public List<Product> GetProducts(string? search = null)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT id, sku, name, category, price, stock, reorder_level FROM products
            WHERE $q = '' OR name LIKE '%' || $q || '%' OR sku LIKE '%' || $q || '%' OR category LIKE '%' || $q || '%'
            ORDER BY name
            """;
        cmd.Parameters.AddWithValue("$q", search?.Trim() ?? "");
        var list = new List<Product>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Product(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3),
                                 (decimal)r.GetDouble(4), r.GetInt32(5), r.GetInt32(6)));
        return list;
    }

    public void SaveProduct(Product p)
    {
        if (string.IsNullOrWhiteSpace(p.Name)) throw new ArgumentException("Product name is required.");
        if (p.Price < 0) throw new ArgumentException("Price cannot be negative.");
        using var c = Open();
        if (p.Id == 0)
            Exec(c, "INSERT INTO products (sku, name, category, price, stock, reorder_level) VALUES ($s,$n,$c,$p,$st,$r)",
                 ("$s", p.Sku), ("$n", p.Name), ("$c", p.Category), ("$p", (double)p.Price), ("$st", p.Stock), ("$r", p.ReorderLevel));
        else
            Exec(c, "UPDATE products SET sku=$s, name=$n, category=$c, price=$p, stock=$st, reorder_level=$r WHERE id=$id",
                 ("$s", p.Sku), ("$n", p.Name), ("$c", p.Category), ("$p", (double)p.Price), ("$st", p.Stock),
                 ("$r", p.ReorderLevel), ("$id", p.Id));
    }

    // ---------------------------------------------------------------- customers

    public List<Customer> GetCustomers()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, name, city, phone FROM customers ORDER BY name";
        var list = new List<Customer>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(new Customer(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3)));
        return list;
    }

    public int AddCustomer(string name, string city, string phone)
    {
        using var c = Open();
        Exec(c, "INSERT INTO customers (name, city, phone) VALUES ($n,$c,$p)", ("$n", name), ("$c", city), ("$p", phone));
        return (int)Scalar<long>(c, "SELECT last_insert_rowid()");
    }

    // ---------------------------------------------------------------- invoices

    /// <summary>Saves the invoice and takes the items out of stock in one transaction.</summary>
    public int SaveInvoice(Invoice inv)
    {
        if (inv.Lines.Count == 0) throw new InvalidOperationException("An invoice needs at least one line.");
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var line in inv.Lines)
        {
            var stock = Scalar<long>(c, "SELECT stock FROM products WHERE id=$id", ("$id", line.ProductId));
            if (stock < line.Quantity)
                throw new InvalidOperationException($"Only {stock} x {line.ProductName} left in stock.");
        }
        Exec(c, "INSERT INTO invoices (customer_id, date, tax_rate) VALUES ($c,$d,$t)",
             ("$c", inv.CustomerId), ("$d", inv.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
             ("$t", (double)inv.TaxRate));
        inv.Id = (int)Scalar<long>(c, "SELECT last_insert_rowid()");
        foreach (var line in inv.Lines)
        {
            Exec(c, "INSERT INTO invoice_lines (invoice_id, product_id, quantity, unit_price) VALUES ($i,$p,$q,$u)",
                 ("$i", inv.Id), ("$p", line.ProductId), ("$q", line.Quantity), ("$u", (double)line.UnitPrice));
            Exec(c, "UPDATE products SET stock = stock - $q WHERE id=$p", ("$q", line.Quantity), ("$p", line.ProductId));
        }
        tx.Commit();
        return inv.Id;
    }

    public List<Invoice> GetInvoices(DateTime? from = null, DateTime? to = null, string? customer = null)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT i.id, i.customer_id, cu.name, i.date, i.tax_rate, l.product_id, p.name, l.quantity, l.unit_price
            FROM invoices i
            JOIN customers cu ON cu.id = i.customer_id
            JOIN invoice_lines l ON l.invoice_id = i.id
            JOIN products p ON p.id = l.product_id
            WHERE i.date >= $from AND i.date < $to AND ($cust = '' OR cu.name LIKE '%' || $cust || '%')
            ORDER BY i.date DESC, i.id DESC
            """;
        cmd.Parameters.AddWithValue("$from", (from ?? DateTime.MinValue).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$to", (to ?? DateTime.MaxValue.AddDays(-1)).AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$cust", customer?.Trim() ?? "");
        var byId = new Dictionary<int, Invoice>();
        var order = new List<Invoice>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var id = r.GetInt32(0);
            if (!byId.TryGetValue(id, out var inv))
            {
                inv = new Invoice
                {
                    Id = id, CustomerId = r.GetInt32(1), CustomerName = r.GetString(2),
                    Date = DateTime.Parse(r.GetString(3), CultureInfo.InvariantCulture), TaxRate = (decimal)r.GetDouble(4)
                };
                byId[id] = inv;
                order.Add(inv);
            }
            inv.Lines.Add(new InvoiceLine(r.GetInt32(5), r.GetString(6), r.GetInt32(7), (decimal)r.GetDouble(8)));
        }
        return order;
    }

    public Invoice? GetInvoice(int id) => GetInvoices().FirstOrDefault(i => i.Id == id);

    public DashboardStats GetDashboard(DateTime today)
    {
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var recent = GetInvoices(today.Date.AddDays(-40), today.Date);
        var month = recent.Where(i => i.Date >= monthStart).ToList();
        var days = Enumerable.Range(0, 14).Select(k => today.Date.AddDays(-13 + k))
            .Select(d => new DailyRevenue(d, recent.Where(i => i.Date.Date == d).Sum(i => i.Total))).ToList();
        return new DashboardStats(month.Sum(i => i.Total), month.Count, GetProducts().Count(p => p.IsLowStock),
                                  GetCustomers().Count, days);
    }

    /// <summary>CSV export, one row per invoice. Quotes fields so commas in names are safe.</summary>
    public static string ToCsv(IEnumerable<Invoice> invoices)
    {
        static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Number,Date,Customer,Items,Subtotal,Tax,Total\n");
        foreach (var i in invoices)
            sb.AppendLine(string.Join(",", i.Number, i.Date.ToString("yyyy-MM-dd"), Q(i.CustomerName),
                i.Lines.Sum(l => l.Quantity), i.Subtotal.ToString("0.00", CultureInfo.InvariantCulture),
                i.Tax.ToString("0.00", CultureInfo.InvariantCulture), i.Total.ToString("0.00", CultureInfo.InvariantCulture)));
        return sb.ToString();
    }
}
