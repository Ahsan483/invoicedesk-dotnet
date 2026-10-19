using System.Drawing.Printing;
using InvoiceDesk.Core;

namespace InvoiceDesk.Desktop;

/// <summary>Shows an invoice as a printable page. The same drawing code is used for the printer.</summary>
public sealed class InvoicePreviewView : UserControl
{
    private readonly MainForm _app;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Invoice? Current { get; set; }
    public Button Print { get; } = Theme.Primary("Print / Save as PDF");
    public Button Back { get; } = Theme.Secondary("Back to invoices");
    private readonly Panel _page = new() { BackColor = Color.White };

    public InvoicePreviewView(MainForm app)
    {
        _app = app;
        BackColor = Theme.Page;
        DoubleBuffered = true;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54 };
        bar.Controls.Add(Print);
        Back.Margin = new Padding(10, 3, 3, 3);
        bar.Controls.Add(Back);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(_page);
        scroll.Resize += (_, _) => Layout_(scroll);
        Controls.Add(scroll);
        Controls.Add(bar);

        _page.Paint += (_, e) => { if (Current is not null) InvoiceRenderer.Draw(e.Graphics, new Rectangle(0, 0, _page.Width, _page.Height), Current); };
        Print.Click += (_, _) => PrintInvoice();
        Back.Click += (_, _) => _app.Show("Invoices");
    }

    private void Layout_(Panel scroll)
    {
        var h = Math.Max(600, scroll.ClientSize.Height - 20);
        var w = (int)(h / 1.414);
        _page.Bounds = new Rectangle(Math.Max(0, (scroll.ClientSize.Width - w) / 2), 0, w, h);
        _page.Invalidate();
    }

    private void PrintInvoice()
    {
        if (Current is null) return;
        using var doc = new PrintDocument { DocumentName = Current.Number };
        doc.PrintPage += (_, e) =>
        {
            InvoiceRenderer.Draw(e.Graphics!, e.MarginBounds, Current);
            e.HasMorePages = false;
        };
        using var dlg = new PrintPreviewDialog { Document = doc, Width = 900, Height = 1000 };
        dlg.ShowDialog(this);
    }
}

internal static class InvoiceRenderer
{
    public static void Draw(Graphics g, Rectangle r, Invoice inv)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        var s = r.Width / 600f;                                     // scale everything from a 600-unit wide page
        Font F(float size, FontStyle st = FontStyle.Regular) => new("Segoe UI", size * s, st);
        float X(float v) => r.Left + v * s;
        float Y(float v) => r.Top + v * s;
        using var ink = new SolidBrush(Theme.Text);
        using var muted = new SolidBrush(Theme.Muted);
        using var accent = new SolidBrush(Theme.Accent);
        using var line = new Pen(Theme.Border, Math.Max(1, s));

        g.FillRectangle(accent, X(40), Y(40), 8 * s, 40 * s);
        g.DrawString("InvoiceDesk Demo Store", F(15, FontStyle.Bold), ink, X(56), Y(38));
        g.DrawString("12 Market Street, Seattle WA  ·  hello@example.com", F(8), muted, X(57), Y(64));
        g.DrawString("INVOICE", F(20, FontStyle.Bold), accent, X(430), Y(36));
        g.DrawString(inv.Number, F(9), ink, X(432), Y(70));
        g.DrawString(inv.Date.ToString("dd MMM yyyy"), F(9), muted, X(432), Y(86));

        g.DrawString("BILL TO", F(8, FontStyle.Bold), muted, X(40), Y(130));
        g.DrawString(inv.CustomerName, F(11, FontStyle.Bold), ink, X(40), Y(146));

        float y = 200;
        g.DrawLine(line, X(40), Y(y), X(560), Y(y));
        g.DrawString("ITEM", F(8, FontStyle.Bold), muted, X(40), Y(y + 8));
        var right = new StringFormat { Alignment = StringAlignment.Far };
        g.DrawString("QTY", F(8, FontStyle.Bold), muted, new RectangleF(X(330), Y(y + 8), 50 * s, 20 * s), right);
        g.DrawString("PRICE", F(8, FontStyle.Bold), muted, new RectangleF(X(390), Y(y + 8), 80 * s, 20 * s), right);
        g.DrawString("AMOUNT", F(8, FontStyle.Bold), muted, new RectangleF(X(470), Y(y + 8), 90 * s, 20 * s), right);
        y += 30;
        g.DrawLine(line, X(40), Y(y), X(560), Y(y));
        foreach (var l in inv.Lines)
        {
            y += 10;
            g.DrawString(l.ProductName, F(9.5f), ink, X(40), Y(y));
            g.DrawString(l.Quantity.ToString(), F(9.5f), ink, new RectangleF(X(330), Y(y), 50 * s, 20 * s), right);
            g.DrawString(Theme.Money(l.UnitPrice), F(9.5f), ink, new RectangleF(X(390), Y(y), 80 * s, 20 * s), right);
            g.DrawString(Theme.Money(l.Total), F(9.5f), ink, new RectangleF(X(470), Y(y), 90 * s, 20 * s), right);
            y += 24;
            g.DrawLine(line, X(40), Y(y), X(560), Y(y));
        }
        y += 18;
        foreach (var (label, value, bold) in new[] { ("Subtotal", inv.Subtotal, false), ($"Sales tax ({inv.TaxRate:P0})", inv.Tax, false), ("Total due", inv.Total, true) })
        {
            g.DrawString(label, F(bold ? 11 : 9.5f, bold ? FontStyle.Bold : FontStyle.Regular), bold ? ink : muted, X(360), Y(y));
            g.DrawString(Theme.Money(value), F(bold ? 11 : 9.5f, bold ? FontStyle.Bold : FontStyle.Regular), ink,
                         new RectangleF(X(450), Y(y), 110 * s, 24 * s), right);
            y += bold ? 30 : 22;
        }
        g.DrawString("Thank you for your business. Payment due within 14 days.", F(8.5f), muted, X(40), Y(r.Height / s - 70));
    }
}
