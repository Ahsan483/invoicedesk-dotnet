using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace InvoiceDesk.Desktop;

/// <summary>
/// Plays a scripted walkthrough of the real UI and records it: each frame is the app drawing
/// itself (DrawToBitmap), piped to ffmpeg at 30 fps. A pointer is drawn where the script clicks.
/// Also saves a few still screenshots for the README.
/// </summary>
internal sealed class DemoRecorder
{
    private const int W = 1920, H = 1080, Fps = 30;
    private readonly MainForm _f;
    private readonly string _dir, _ffmpeg;
    private Process? _enc;
    private Stream? _pipe;
    private readonly Stopwatch _clock = new();
    private long _written;
    private PointF _cursor = new(1200, 700);
    private float _click;                          // 0..1 click ripple

    public DemoRecorder(MainForm form, string dir, string ffmpeg)
    {
        _f = form;
        _dir = dir;
        _ffmpeg = ffmpeg;
        _f.FormBorderStyle = FormBorderStyle.None;  // record the app area only, at exactly 1920x1080
        _f.StartPosition = FormStartPosition.Manual;
        _f.Location = new Point(-W - 200, 0);       // off screen: nothing pops up on the desktop
        _f.ClientSize = new Size(W, H);
        _f.ShowInTaskbar = false;
        _f.Invoices.ExportFolder = dir;
    }

    public async Task RunAsync()
    {
        _f.ClientSize = new Size(W, H);
        _enc = Process.Start(new ProcessStartInfo(_ffmpeg,
            $"-y -loglevel error -f rawvideo -pix_fmt bgra -s {W}x{H} -r {Fps} -i - -c:v libx264 -preset veryfast -crf 16 -pix_fmt yuv420p \"{Path.Combine(_dir, "desktop-demo.mp4")}\"")
            { RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true })!;
        _pipe = _enc.StandardInput.BaseStream;
        _clock.Start();

        await Wait(2600);
        Snap("dashboard");
        await Click(_f.Nav["Products"]);
        await Wait(900);
        Snap("products");
        await Click(_f.Products.Search);
        await Type(_f.Products.Search, "ssd");
        await Wait(1200);
        _f.Products.Search.Text = "";
        await Click(_f.Products.LowOnly);
        _f.Products.LowOnly.Checked = true;
        await Wait(1600);
        Snap("low-stock");
        await Click(_f.Products.LowOnly);
        _f.Products.LowOnly.Checked = false;

        await Click(_f.Nav["New invoice"]);
        await Wait(700);
        await Click(_f.Editor.Customer);
        _f.Editor.Customer.SelectedIndex = _f.Editor.Customer.FindStringExact("Northwind Traders");
        await Wait(500);
        await AddLine("27\" QHD Monitor", 6);                 // more than in stock: shows the validation message
        await Wait(1600);
        Snap("stock-check");
        await SetQty(1);
        await Click(_f.Editor.AddLine);
        await Wait(500);
        await AddLine("SSD 1TB NVMe", 2);
        await AddLine("USB-C Hub 7-in-1", 3);
        await Wait(1200);
        Snap("invoice-editor");
        await Click(_f.Editor.Save);
        await Wait(2600);
        Snap("invoice-preview");

        await Click(_f.Nav["Invoices"]);
        await Wait(1000);
        await Click(_f.Invoices.CustomerFilter);
        await Type(_f.Invoices.CustomerFilter, "north");
        await Wait(1400);
        Snap("invoices");
        _f.Invoices.CustomerFilter.Text = "";
        await Wait(400);
        await Click(_f.Invoices.Export);
        await Wait(1500);

        await Click(_f.Nav["Dashboard"]);
        await Wait(2600);
        Snap("dashboard-after");

        _pipe.Close();
        await _enc.WaitForExitAsync();
    }

    private async Task AddLine(string product, int qty)
    {
        await Click(_f.Editor.Product);
        _f.Editor.SelectProduct(product);
        await Wait(350);
        await SetQty(qty);
        await Click(_f.Editor.AddLine);
        await Wait(500);
    }

    private async Task SetQty(int qty)
    {
        await Click(_f.Editor.Quantity);
        _f.Editor.Quantity.Value = qty;
        await Wait(300);
    }

    // ---------------------------------------------------------------- input simulation

    private async Task Click(Control c)
    {
        var target = _f.Root.PointToClient(c.PointToScreen(new Point(Math.Min(c.Width / 2, 60), c.Height / 2)));
        var start = _cursor;
        const int steps = 18;
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            t = t * t * (3 - 2 * t);
            _cursor = new PointF(start.X + (target.X - start.X) * t, start.Y + (target.Y - start.Y) * t);
            await Wait(16);
        }
        _click = 1;
        if (c is Button b) b.PerformClick();
        else c.Focus();
        await Wait(220);
    }

    private async Task Type(TextBox box, string text)
    {
        foreach (var ch in text)
        {
            box.AppendText(ch.ToString());
            await Wait(140);
        }
    }

    // ---------------------------------------------------------------- frames

    private async Task Wait(int ms)
    {
        var until = _clock.ElapsedMilliseconds + ms;
        do
        {
            Capture();
            await Task.Delay(1);
        } while (_clock.ElapsedMilliseconds < until);
    }

    private Bitmap Render(bool pointer)
    {
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        _f.Root.DrawToBitmap(bmp, new Rectangle(0, 0, W, H));
        if (pointer) DrawPointer(bmp);
        return bmp;
    }

    private void Capture()
    {
        var due = _clock.ElapsedMilliseconds * Fps / 1000 + 1;
        if (_written >= due) return;
        using var bmp = Render(pointer: true);
        var data = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var bytes = new byte[W * H * 4];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        bmp.UnlockBits(data);
        while (_written < due)                     // repeat the frame if rendering fell behind real time
        {
            _pipe!.Write(bytes, 0, bytes.Length);
            _written++;
        }
        _click = Math.Max(0, _click - 0.12f);
    }

    private void Snap(string name)
    {
        using var bmp = Render(pointer: false);
        bmp.Save(Path.Combine(_dir, name + ".png"), ImageFormat.Png);
    }

    private void DrawPointer(Bitmap bmp)
    {
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var x = _cursor.X;
        var y = _cursor.Y;
        if (_click > 0)
        {
            var rad = 10 + (1 - _click) * 22;
            using var ring = new Pen(Color.FromArgb((int)(160 * _click), 37, 99, 235), 3);
            g.DrawEllipse(ring, x - rad, y - rad, rad * 2, rad * 2);
        }
        PointF[] arrow =
        {
            new(x, y), new(x, y + 22), new(x + 5.5f, y + 17), new(x + 9.5f, y + 26),
            new(x + 13, y + 24.5f), new(x + 9, y + 15.5f), new(x + 16, y + 15.5f),
        };
        g.FillPolygon(Brushes.White, arrow);
        using var outline = new Pen(Color.Black, 1.5f);
        g.DrawPolygon(outline, arrow);
    }
}
