namespace Snapit;

static class Program
{
    public const string ShowEventName = "Snapit_ShowPanel_7f3a91";

    [STAThread]
    static void Main(string[] args)
    {
        // Si Snapit tourne déjà : on lui demande d'afficher son panneau, puis on quitte.
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName, out bool createdNew);
        if (!createdNew)
        {
            Native.AllowSetForegroundWindow(-1); // autorise l'instance déjà ouverte à passer au premier plan
            showEvent.Set();
            return;
        }

        bool silent = args.Any(a => a.Equals("--startup", StringComparison.OrdinalIgnoreCase));

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => ReportCrash(e.Exception, fatal: false);
        AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash(e.ExceptionObject as Exception, fatal: true);

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApp(showEvent, silent));
        }
        catch (Exception ex)
        {
            ReportCrash(ex, fatal: true);
        }
    }

    static void ReportCrash(Exception ex, bool fatal)
    {
        string log = "";
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Snapit");
            Directory.CreateDirectory(dir);
            log = Path.Combine(dir, "erreur.log");
            File.AppendAllText(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
        }
        catch { }

        MessageBox.Show(
            (fatal ? "Snapit a rencontré un problème et doit se fermer.\n\n" : "Une erreur est survenue :\n\n") +
            (ex?.Message ?? "erreur inconnue") +
            (log != "" ? "\n\nDétails : " + log : ""),
            "Snapit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
