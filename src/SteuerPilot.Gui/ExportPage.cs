using SteuerPilot.Core;

namespace SteuerPilot.Gui;

public sealed class ExportPage : Page
{
    private readonly FlowLayoutPanel _flow = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly Label _status = Make.Label("", Ui.Bold);

    public override string Titel => "Export & Sicherung";

    public ExportPage(MainForm main, App app) : base(main, app)
    {
        Karte("Steuerbericht ansehen & drucken", "Übersicht aller Belege des Jahres nach Formular/Anlage sortiert, mit Summen und Schätzung – ideal als Ausfüllhilfe für ELSTER. Öffnet im Browser (dort Strg+P zum Drucken/als PDF speichern).",
            "Bericht öffnen", Bericht);
        Karte("Tabelle für Excel (CSV)", "Alle Belege des Steuerjahres als Tabelle (Semikolon-getrennt, öffnet direkt in Excel).", "CSV speichern …", Csv);
        Karte("Steuerpaket (ZIP)", "Bericht, Tabelle und alle Belegdateien des Jahres in einer ZIP-Datei – zum Weitergeben an Steuerberater oder Lohnsteuerhilfeverein.", "Paket erstellen …", Paket);
        Karte("Komplette Sicherung (ZIP)", "Sichert den ganzen Steuerordner mit allen Jahren, Belegen und Daten. Zum Wiederherstellen ZIP entpacken und in den Einstellungen als Steuerordner wählen.", "Sicherung erstellen …", Sicherung);
        Karte("ELSTER öffnen", "Die eigentliche Steuererklärung reichst du kostenlos über „Mein ELSTER“ ein. SteuerPilot liefert dir dafür alle Zahlen.", "elster.de öffnen", () =>
        {
            Aktionen.Oeffnen("https://www.elster.de/eportal/start");
            return Task.CompletedTask;
        });
        _flow.Controls.Add(_status);
        Controls.Add(_flow);
    }

    private void Karte(string titel, string text, string knopf, Func<Task> aktion)
    {
        var c = new Card { Size = new Size(Ui.S(760), Ui.S(112)), Margin = new Padding(0, 0, 0, Ui.S(12)) };
        var t = Make.Label(titel, Ui.CardTitle);
        var d = Make.Label(text, Ui.Small, muted: true, width: Ui.S(540));
        var b = new FlatButton(knopf, ButtonKind.Primary);
        t.Location = new Point(Ui.S(18), Ui.S(14));
        d.Location = new Point(Ui.S(18), Ui.S(44));
        b.Location = new Point(c.Width - b.Width - Ui.S(18), (c.Height - b.Height) / 2);
        c.Controls.AddRange([t, d, b]);
        b.Click += async (_, _) =>
        {
            b.Enabled = false;
            try { await aktion(); }
            catch (Exception ex) { Aktionen.Fehler(Main, "Export fehlgeschlagen:", ex); }
            finally { b.Enabled = true; }
        };
        _flow.Controls.Add(c);
    }

    private Task Bericht()
    {
        var dir = Path.Combine(App.Ablage.Root, "Berichte");
        Directory.CreateDirectory(dir);
        var pfad = Path.Combine(dir, $"Steuerbericht {App.Jahr}.html");
        File.WriteAllText(pfad, Export.Bericht(App.Daten, App.Jahr), new System.Text.UTF8Encoding(true));
        _status.Text = "Bericht gespeichert: " + pfad;
        Aktionen.Oeffnen(pfad);
        return Task.CompletedTask;
    }

    private Task Csv()
    {
        var ziel = Aktionen.SpeichernUnter(Main, $"SteuerPilot Belege {App.Jahr}.csv", "CSV-Tabelle|*.csv");
        if (ziel == null) return Task.CompletedTask;
        File.WriteAllText(ziel, Export.Csv(App.Daten, App.Jahr), new System.Text.UTF8Encoding(true));
        _status.Text = "Gespeichert: " + ziel;
        return Task.CompletedTask;
    }

    private async Task Paket()
    {
        var ziel = Aktionen.SpeichernUnter(Main, $"Steuerpaket {App.Jahr}.zip", "ZIP-Archiv|*.zip");
        if (ziel == null) return;
        _status.Text = "Erstelle Steuerpaket …";
        int n = await Task.Run(() => Export.Steuerpaket(App.Daten, App.Ablage, App.Jahr, ziel));
        _status.Text = $"Steuerpaket mit {n} Belegdateien gespeichert: {ziel}";
        Ui.OpenInExplorer(ziel, select: true);
    }

    private async Task Sicherung()
    {
        var ziel = Aktionen.SpeichernUnter(Main, $"SteuerPilot Sicherung {DateTime.Now:yyyy-MM-dd}.zip", "ZIP-Archiv|*.zip");
        if (ziel == null) return;
        App.Speichern();
        _status.Text = "Erstelle Sicherung …";
        int n = await Task.Run(() => Export.Sicherung(App.Ablage, ziel));
        _status.Text = $"Sicherung mit {n} Dateien gespeichert: {ziel}";
        Ui.OpenInExplorer(ziel, select: true);
    }

    public override void Aktualisieren() => Theme.Apply(this);
}
