using SteuerPilot.Core;

namespace SteuerPilot.Gui;

/// <summary>Abläufe, die von mehreren Seiten aus gestartet werden.</summary>
public static class Aktionen
{
    public static void Fehler(IWin32Window owner, string was, Exception ex) =>
        MessageBox.Show(owner, was + "\n\n" + ex.Message, "SteuerPilot", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    /// <summary>Neuen Beleg anlegen, optional mit Dateien (aus dem Eingang oder von irgendwo).</summary>
    public static bool NeuerBeleg(IWin32Window owner, App app, IReadOnlyList<string>? dateien = null, bool ocr = false)
    {
        var b = new Beleg();
        if (app.Jahr != DateTime.Today.Year) b.Datum = new DateTime(app.Jahr, 12, 31);
        using var f = new BelegForm(app, b, dateien ?? [], neu: true, autoOcr: ocr);
        if (f.ShowDialog(owner) != DialogResult.OK) return false;
        try
        {
            app.Ablage.Hinzufuegen(b, f.NeueDateien, app.Settings.DateienVerschieben);
            app.Daten.Belege.Add(b);
            app.Speichern();
            return true;
        }
        catch (Exception ex)
        {
            if (!app.Daten.Belege.Contains(b)) app.Daten.Belege.Add(b);
            app.Speichern();
            Fehler(owner, "Der Beleg wurde gespeichert, aber nicht alle Dateien konnten einsortiert werden:", ex);
            return true;
        }
    }

    public static bool Bearbeiten(IWin32Window owner, App app, Beleg b)
    {
        var kopie = b.Kopie();
        using var f = new BelegForm(app, kopie, [], neu: false, autoOcr: false);
        if (f.ShowDialog(owner) != DialogResult.OK) return false;
        try
        {
            foreach (var weg in f.EntfernteDateien) app.Ablage.DateiEntfernen(b, weg);
            b.Datum = kopie.Datum; b.Betrag = kopie.Betrag; b.Beschreibung = kopie.Beschreibung; b.Kategorie = kopie.Kategorie;
            b.Person = kopie.Person; b.Anteil = kopie.Anteil; b.AfaJahre = kopie.AfaJahre; b.Arbeitskosten = kopie.Arbeitskosten; b.Notiz = kopie.Notiz;
            app.Ablage.Umbenennen(b);
            if (f.NeueDateien.Count > 0) app.Ablage.Hinzufuegen(b, f.NeueDateien, app.Settings.DateienVerschieben);
        }
        catch (Exception ex)
        {
            Fehler(owner, "Nicht alle Dateien konnten umbenannt oder verschoben werden:", ex);
        }
        app.Speichern();
        return true;
    }

    public static bool Loeschen(IWin32Window owner, App app, IReadOnlyList<Beleg> belege)
    {
        if (belege.Count == 0) return false;
        var text = belege.Count == 1 ? $"Beleg „{belege[0].Beschreibung}“ löschen?" : $"{belege.Count} Belege löschen?";
        if (MessageBox.Show(owner, text + "\n\nZugehörige Dateien wandern in den Ordner „_Papierkorb“ im Steuerordner.", "Löschen",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return false;
        foreach (var b in belege)
        {
            try { app.Ablage.Loeschen(b); }
            catch (Exception ex) { Fehler(owner, "Dateien konnten nicht verschoben werden:", ex); }
            app.Daten.Belege.Remove(b);
        }
        app.Speichern();
        return true;
    }

    /// <summary>Scannt eine Seite in den Eingang und gibt den Pfad zurück (null bei Abbruch).</summary>
    public static string? Scannen(IWin32Window owner, App app)
    {
        var ziel = app.Ablage.NeueScanDatei();
        try
        {
            return Scanner.Scannen(ziel) ? ziel : null;
        }
        catch (Exception ex)
        {
            if (MessageBox.Show(owner, ex.Message + "\n\nAlternativ kannst du mit der Windows-App „Scannen“ oder mit dem Handy scannen und die Datei " +
                    "in den Eingang-Ordner legen. Windows-Scan jetzt öffnen?", "Scannen", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try { Scanner.ScanAppOeffnen(); } catch { }
                Ui.OpenInExplorer(app.Ablage.Eingang);
            }
            return null;
        }
    }

    /// <summary>Kopiert Dateien in den Eingang.</summary>
    public static int InEingang(App app, IEnumerable<string> dateien)
    {
        int n = 0;
        foreach (var d in dateien.Where(File.Exists).Where(Ablage.Unterstuetzt))
        {
            if (app.Ablage.IstImEingang(d)) continue;
            File.Copy(d, Ablage.Eindeutig(Path.Combine(app.Ablage.Eingang, Path.GetFileName(d))));
            n++;
        }
        return n;
    }

    public static string[] DateienWaehlen(IWin32Window owner, string titel = "Belege auswählen")
    {
        using var d = new OpenFileDialog
        {
            Title = titel,
            Multiselect = true,
            Filter = "Belege (Bilder, PDF)|" + string.Join(";", Ablage.Endungen.Select(e => "*" + e)) + "|Alle Dateien|*.*",
        };
        return d.ShowDialog(owner) == DialogResult.OK ? d.FileNames : [];
    }

    public static void Oeffnen(string pfad)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pfad) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "SteuerPilot", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    public static string? SpeichernUnter(IWin32Window owner, string name, string filter)
    {
        using var d = new SaveFileDialog { FileName = name, Filter = filter, InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
        return d.ShowDialog(owner) == DialogResult.OK ? d.FileName : null;
    }
}
