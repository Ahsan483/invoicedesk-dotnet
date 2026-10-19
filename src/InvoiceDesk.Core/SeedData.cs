namespace InvoiceDesk.Core;

/// <summary>Deterministic demo data: a small electronics and office-supply shop.</summary>
internal static class SeedData
{
    public static void Fill(InvoiceStore store)
    {
        var products = new (string Sku, string Name, string Cat, decimal Price, int Stock, int Reorder)[]
        {
            ("KB-201", "Mechanical Keyboard K2", "Accessories", 49.90m, 34, 10),
            ("MS-114", "Wireless Mouse M114", "Accessories", 18.50m, 62, 15),
            ("HD-500", "USB-C Hub 7-in-1", "Accessories", 32.00m, 8, 10),
            ("CB-030", "USB-C Cable 2m", "Cables", 7.90m, 140, 40),
            ("CB-045", "HDMI Cable 1.5m", "Cables", 6.50m, 25, 30),
            ("MN-240", "24\" IPS Monitor", "Displays", 139.00m, 12, 5),
            ("MN-270", "27\" QHD Monitor", "Displays", 229.00m, 4, 5),
            ("HS-310", "Headset H310 with Mic", "Audio", 39.00m, 21, 8),
            ("SP-020", "Desk Speakers S20", "Audio", 26.00m, 17, 6),
            ("WC-108", "Webcam 1080p", "Video", 44.00m, 6, 8),
            ("PR-410", "A4 Paper 500 sheets", "Office", 5.20m, 300, 80),
            ("PR-415", "Laser Toner TN-415", "Office", 58.00m, 9, 6),
            ("ST-101", "Laptop Stand Aluminium", "Accessories", 29.00m, 15, 5),
            ("SS-512", "SSD 512GB NVMe", "Storage", 54.00m, 23, 8),
            ("SS-1T0", "SSD 1TB NVMe", "Storage", 89.00m, 11, 6),
            ("UP-650", "UPS 650VA", "Power", 74.00m, 7, 4),
        };
        foreach (var p in products)
            store.SaveProduct(new Product(0, p.Sku, p.Name, p.Cat, p.Price, p.Stock + 60, p.Reorder));

        string[][] customers =
        {
            new[] { "Northwind Traders", "Seattle", "+1 206 555 0142" }, new[] { "Bright Office Supplies", "Austin", "+1 512 555 0188" },
            new[] { "Greenfield Academy", "Manchester", "+44 161 555 0123" }, new[] { "Harbor Dental Clinic", "Boston", "+1 617 555 0109" },
            new[] { "Pixel Studio", "Toronto", "+1 416 555 0175" }, new[] { "Swift Logistics", "Rotterdam", "+31 10 555 0190" },
            new[] { "Maple Accounting", "Vancouver", "+1 604 555 0133" }, new[] { "Zenith Electronics", "Dubai", "+971 4 555 0166" },
            new[] { "Hilltop Cafe", "Denver", "+1 303 555 0121" }, new[] { "Orbit Software", "Berlin", "+49 30 555 0148" },
        };
        foreach (var c in customers) store.AddCustomer(c[0], c[1], c[2]);

        // about 40 days of invoices, so the dashboard has real history
        var rng = new Random(2024);
        var all = store.GetProducts();
        var today = DateTime.Today;
        for (var day = 40; day >= 1; day--)
        {
            var count = rng.Next(0, 4);
            for (var k = 0; k < count; k++)
            {
                var inv = new Invoice
                {
                    CustomerId = rng.Next(1, customers.Length + 1),
                    Date = today.AddDays(-day).AddHours(9 + rng.Next(0, 9)).AddMinutes(rng.Next(0, 60)),
                };
                foreach (var p in all.OrderBy(_ => rng.Next()).Take(rng.Next(1, 4)))
                    inv.Lines.Add(new InvoiceLine(p.Id, p.Name, rng.Next(1, 4), p.Price));
                store.SaveInvoice(inv);
            }
        }

        // history was sold from a padded stock; now set today's real stock levels (a few are low on purpose)
        foreach (var p in store.GetProducts())
        {
            var seed = products.First(x => x.Sku == p.Sku);
            store.SaveProduct(p with { Stock = seed.Stock });
        }
    }
}
