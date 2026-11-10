namespace Snapit;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "Snapit_SingleInstance_7f3a91", out bool createdNew);
        if (!createdNew) return; // déjà lancé : l'icône est dans la barre des tâches

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
            MessageBox.Show(e.Exception.Message, "Snapit", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
