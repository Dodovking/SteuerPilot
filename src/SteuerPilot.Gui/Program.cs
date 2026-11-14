using SteuerPilot.Core;

namespace SteuerPilot.Gui;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains(Updater.SelfTestArg, StringComparer.OrdinalIgnoreCase))
        {
            _ = typeof(Steuer).Assembly.GetName();
            using (new Label()) { }
            Environment.Exit(Updater.SelfTestExitCode);
        }
        bool afterUpdate = Updater.WaitForPreviousInstance(args);

        using var mutex = new Mutex(true, @"Local\SteuerPilot_Instanz", out bool firstInstance);
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\SteuerPilot_Zeigen");
        if (!firstInstance)
        {
            showEvent.Set();
            return;
        }

        ApplicationConfiguration.Initialize();
        Thread.CurrentThread.CurrentCulture = Fmt.De;
        Thread.CurrentThread.CurrentUICulture = Fmt.De;
        Updater.CleanupLeftovers();
        Application.ThreadException += (_, e) =>
            MessageBox.Show("Ein unerwarteter Fehler ist aufgetreten:\n\n" + e.Exception.Message, "SteuerPilot", MessageBoxButtons.OK, MessageBoxIcon.Error);

        var settings = Speicher.LadeEinstellungen();
        if (!settings.DesignV2)
        {
            settings.Theme = ThemeChoice.Dunkel;
            settings.DesignV2 = true;
            try { Speicher.SpeichereEinstellungen(settings); } catch { }
        }
        Theme.Set(settings.Theme);
        App app;
        try
        {
            app = new App(settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Der Steuerordner konnte nicht geöffnet werden:\n\n" + ex.Message + "\n\nEs wird der Standardordner verwendet.", "SteuerPilot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            settings.Ablageordner = null;
            app = new App(settings);
        }
        app.SettingsSpeichern();

        using var form = new MainForm(app, Updater.SourceUrl(args)) { StartArgs = args };
        var waiter = new Thread(() =>
        {
            while (true)
            {
                showEvent.WaitOne();
                try { form.BeginInvoke(form.ShowWindow); } catch { return; }
            }
        }) { IsBackground = true };
        waiter.Start();
        if (afterUpdate) form.Shown += (_, _) => MessageBox.Show(form, $"SteuerPilot wurde auf Version {Updater.CurrentVersion.ToString(3)} aktualisiert.", "SteuerPilot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Application.Run(form);
    }
}
