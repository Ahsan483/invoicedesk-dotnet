using InvoiceDesk.Core;

namespace InvoiceDesk.Desktop;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // --record <dir> <ffmpeg.exe>: plays a scripted demo and records it (used for the README video)
        var record = args.Length >= 3 && args[0] == "--record";
        if (record)
        {
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);   // fixed 1:1 pixels for the recording
            Theme.Scale = 1.25f;                                  // and the look of a 125% display
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }
        else
        {
            ApplicationConfiguration.Initialize();
        }

        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InvoiceDesk");
        Directory.CreateDirectory(dataDir);
        var dbPath = record ? Path.Combine(args[1], "demo.db") : Path.Combine(dataDir, "invoicedesk.db");
        if (record && File.Exists(dbPath)) File.Delete(dbPath);

        var form = new MainForm(new InvoiceStore(dbPath));
        if (record)
        {
            var demo = new DemoRecorder(form, args[1], args[2]);
            form.Shown += async (_, _) => { await demo.RunAsync(); form.Close(); };
        }
        Application.Run(form);
    }
}
