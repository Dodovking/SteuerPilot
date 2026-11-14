using SteuerPilot.Core;

namespace SteuerPilot.Gui;

public sealed class EinstellungenPage : Page
{
    private readonly Formular _f = new();

    public override string Titel => "Einstellungen";

    public EinstellungenPage(MainForm main, App app) : base(main, app) => Controls.Add(_f);

    public override void Aktualisieren()
    {
        _f.SuspendLayout();
        _f.Leeren();
        _f.LabelBreite = Ui.S(260);
        var s = App.Settings;

        _f.Ueberschrift("Steuerordner");
        _f.Hinweis("Hier liegen alle Belege, sortiert nach Jahr und Bereich (z. B. 2025\\02 Werbungskosten\\Arbeitsmittel), dazu der Ordner „Eingang“ für neue Fotos und Scans und die Datei SteuerPilot-Daten.json. Tipp: Ein Ordner in OneDrive/Dropbox sichert alles automatisch in der Cloud.");
        var pfad = Make.Label(App.Ablage.Root, Ui.Bold);
        _f.Zeile("Aktueller Ordner", pfad);
        var oeffnen = new FlatButton("Öffnen");
        var aendern = new FlatButton("Anderen Ordner wählen …");
        oeffnen.Click += (_, _) => Ui.OpenInExplorer(App.Ablage.Root);
        aendern.Click += (_, _) => OrdnerAendern();
        _f.Zeile("", oeffnen, aendern);

        _f.Ueberschrift("Dateien");
        var verschieben = new ToggleSwitch("Dateien beim Hinzufügen verschieben statt kopieren") { Checked = s.DateienVerschieben };
        verschieben.CheckedChanged += (_, _) => { s.DateienVerschieben = verschieben.Checked; App.SettingsSpeichern(); };
        _f.Element(verschieben);
        _f.Hinweis("Gilt für Dateien von außerhalb (z. B. Downloads). Dateien aus dem Eingang werden immer in den richtigen Ordner verschoben.");

        _f.Ueberschrift("Darstellung");
        var design = new Segmented("Wie Windows", "Hell", "Dunkel") { SelectedIndex = (int)s.Theme };
        design.SelectedIndexChanged += (_, _) =>
        {
            s.Theme = (ThemeChoice)design.SelectedIndex;
            App.SettingsSpeichern();
            Theme.Set(s.Theme);
        };
        _f.Zeile("Design", design);

        _f.Ueberschrift("Updates");
        var auto = new ToggleSwitch("Automatisch nach neuen Versionen suchen") { Checked = s.UpdatesPruefen };
        auto.CheckedChanged += (_, _) => { s.UpdatesPruefen = auto.Checked; App.SettingsSpeichern(); };
        _f.Element(auto);
        var check = new FlatButton("Jetzt nach Updates suchen");
        check.Click += (_, _) => Main.OpenUpdate();
        _f.Zeile($"Installiert: Version {Updater.CurrentVersion.ToString(3)}", check);

        _f.Ueberschrift("Datenschutz & Hinweis");
        _f.Hinweis("SteuerPilot arbeitet komplett offline auf deinem PC. Belege, Texterkennung und Berechnungen verlassen den Computer nicht (nur die Update-Prüfung fragt GitHub nach neuen Versionen).\n\n" +
                   "Alle Berechnungen sind vereinfachte Schätzungen nach bestem Wissen (Stand Steuerjahre 2023–2026) und ersetzen keine Steuerberatung. Maßgeblich ist der Bescheid des Finanzamts.");
        _f.Abschluss();
        Theme.Apply(_f);
        Felder.Einfaerben(_f);
        _f.ResumeLayout();
    }

    private void OrdnerAendern()
    {
        using var d = new FolderBrowserDialog { Description = "Steuerordner wählen", UseDescriptionForTitle = true, SelectedPath = App.Ablage.Root, ShowNewFolderButton = true };
        if (d.ShowDialog(Main) != DialogResult.OK) return;
        var ziel = Path.GetFullPath(d.SelectedPath);
        var alt = App.Ablage.Root;
        if (string.Equals(ziel.TrimEnd('\\', '/'), alt.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) return;
        if (ziel.StartsWith(alt + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(Main, "Der neue Ordner darf nicht im bisherigen Steuerordner liegen.", "Steuerordner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        bool zielHatDaten = File.Exists(Path.Combine(ziel, "SteuerPilot-Daten.json"));
        try
        {
            if (!zielHatDaten && App.Daten.Belege.Count > 0)
            {
                var r = MessageBox.Show(Main, $"Sollen alle bisherigen Belege und Dateien nach\n{ziel}\nkopiert werden?\n\nJa = kopieren (der alte Ordner bleibt als Sicherung erhalten)\nNein = mit leerem Ordner neu beginnen",
                    "Steuerordner wechseln", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return;
                if (r == DialogResult.Yes)
                {
                    App.Speichern();
                    Cursor.Current = Cursors.WaitCursor;
                    Kopieren(alt, ziel);
                }
            }
            App.OrdnerWechseln(ziel);
        }
        catch (Exception ex)
        {
            Aktionen.Fehler(Main, "Der Ordner konnte nicht gewechselt werden:", ex);
        }
        Aktualisieren();
    }

    private static void Kopieren(string von, string nach)
    {
        Directory.CreateDirectory(nach);
        foreach (var dir in Directory.GetDirectories(von, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(nach, Path.GetRelativePath(von, dir)));
        foreach (var f in Directory.GetFiles(von, "*", SearchOption.AllDirectories))
        {
            var z = Path.Combine(nach, Path.GetRelativePath(von, f));
            if (!File.Exists(z)) File.Copy(f, z);
        }
    }
}
