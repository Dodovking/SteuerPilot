using System.Globalization;

namespace SteuerPilot.Core;

/// <summary>
/// Der eigene Steuer-Ordner: Eingang für neue Scans/Fotos, sortierte Ablage nach Jahr\Gruppe\Kategorie.
/// </summary>
public sealed class Ablage(string root)
{
    public static readonly string[] Endungen = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".pdf"];
    public static readonly string[] BildEndungen = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"];

    public string Root { get; } = Path.GetFullPath(root);
    public string Eingang => Path.Combine(Root, "Eingang");
    public string Papierkorb => Path.Combine(Root, "_Papierkorb");
    public string DatenDatei => Path.Combine(Root, "SteuerPilot-Daten.json");

    public static string StandardOrdner() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SteuerPilot");

    public void Einrichten()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Eingang);
        var info = Path.Combine(Root, "LIES MICH.txt");
        if (!File.Exists(info))
        {
            File.WriteAllText(info,
                "Das ist dein SteuerPilot-Ordner.\r\n\r\n" +
                "Eingang\\  – hier landen neue Scans und Fotos. Du kannst auch selbst Dateien hineinlegen\r\n" +
                "            (z. B. Fotos vom Handy oder den Speicherordner deines Scanners hierher legen).\r\n" +
                "2025\\ usw. – sortierte Belege je Steuerjahr und Kategorie.\r\n" +
                "SteuerPilot-Daten.json – deine Angaben und Beträge (nicht von Hand ändern).\r\n");
        }
    }

    public string Absolut(string relativ) => Path.GetFullPath(Path.Combine(Root, relativ));

    public string Relativ(string absolut) => Path.GetRelativePath(Root, absolut);

    public bool IstImOrdner(string pfad)
    {
        var full = Path.GetFullPath(pfad);
        return full.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public bool IstImEingang(string pfad) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(pfad))?.TrimEnd(Path.DirectorySeparatorChar), Eingang, StringComparison.OrdinalIgnoreCase);

    public string ZielOrdner(Beleg b)
    {
        var jahr = Path.Combine(Root, b.Jahr.ToString(CultureInfo.InvariantCulture));
        var k = b.Kat;
        if (k.Gruppe == Gruppe.Vermietung && !string.IsNullOrWhiteSpace(b.Objekt))
            return Path.Combine(jahr, Kategorien.GruppenOrdner(k.Gruppe), Kategorien.SichererName(b.Objekt.Trim()), Kategorien.SichererName(k.Name));
        return Path.Combine(jahr, k.Ordner);
    }

    public static string Dateiname(Beleg b, int seite, int seiten, string endung)
    {
        var beschr = Kategorien.SichererName(string.IsNullOrWhiteSpace(b.Beschreibung) ? b.Kat.Name : b.Beschreibung);
        if (beschr.Length > 60) beschr = beschr[..60].TrimEnd();
        var betrag = b.Betrag.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"));
        var s = seiten > 1 ? $"_Seite{seite + 1}" : "";
        return $"{b.Datum:yyyy-MM-dd}_{beschr}_{betrag}EUR{s}{endung.ToLowerInvariant()}";
    }

    public static bool Unterstuetzt(string pfad) => Endungen.Contains(Path.GetExtension(pfad).ToLowerInvariant());

    public static bool IstBild(string pfad) => BildEndungen.Contains(Path.GetExtension(pfad).ToLowerInvariant());

    public List<string> EingangDateien()
    {
        if (!Directory.Exists(Eingang)) return [];
        return Directory.EnumerateFiles(Eingang)
            .Where(f => Unterstuetzt(f) && !Path.GetFileName(f).StartsWith('.') && !Path.GetFileName(f).StartsWith("~"))
            .OrderBy(File.GetLastWriteTime)
            .ToList();
    }

    public string NeueScanDatei(string endung = ".jpg")
    {
        Directory.CreateDirectory(Eingang);
        return Eindeutig(Path.Combine(Eingang, $"Scan_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}{endung}"));
    }

    /// <summary>Kopiert/verschiebt Dateien in den passenden Ordner und hängt sie an den Beleg an.</summary>
    public void Hinzufuegen(Beleg b, IEnumerable<string> quellen, bool verschieben)
    {
        var liste = quellen.ToList();
        var ziel = ZielOrdner(b);
        Directory.CreateDirectory(ziel);
        foreach (var q in liste)
        {
            var neu = Eindeutig(Path.Combine(ziel, Dateiname(b, b.Dateien.Count, b.Dateien.Count + liste.Count, Path.GetExtension(q))));
            // Dateien aus dem Eingang werden immer verschoben, damit er leer wird.
            if (verschieben || IstImEingang(q)) MoveFile(q, neu);
            else File.Copy(q, neu);
            b.Dateien.Add(Relativ(neu));
        }
        Umbenennen(b);
    }

    /// <summary>Bringt die Dateien nach einer Änderung (Datum, Kategorie, Text, Betrag) an den richtigen Platz.</summary>
    public void Umbenennen(Beleg b)
    {
        var ziel = ZielOrdner(b);
        var neu = new List<string>();
        int n = b.Dateien.Count;
        for (int i = 0; i < n; i++)
        {
            var alt = Absolut(b.Dateien[i]);
            if (!File.Exists(alt)) { neu.Add(b.Dateien[i]); continue; }
            var wunsch = Path.Combine(ziel, Dateiname(b, i, n, Path.GetExtension(alt)));
            if (string.Equals(Path.GetFullPath(alt), Path.GetFullPath(wunsch), StringComparison.OrdinalIgnoreCase)) { neu.Add(Relativ(alt)); continue; }
            Directory.CreateDirectory(ziel);
            var dest = Eindeutig(wunsch);
            MoveFile(alt, dest);
            neu.Add(Relativ(dest));
            AufraeumenLeer(Path.GetDirectoryName(alt)!);
        }
        b.Dateien = neu;
    }

    public void DateiEntfernen(Beleg b, string relativ)
    {
        var abs = Absolut(relativ);
        if (File.Exists(abs)) InPapierkorbVerschieben(abs);
        b.Dateien.Remove(relativ);
        Umbenennen(b);
    }

    public void Loeschen(Beleg b)
    {
        foreach (var rel in b.Dateien)
        {
            var abs = Absolut(rel);
            if (File.Exists(abs)) InPapierkorbVerschieben(abs);
        }
        b.Dateien.Clear();
    }

    public void InPapierkorbVerschieben(string abs)
    {
        var dir = Path.Combine(Papierkorb, DateTime.Today.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dir);
        var src = Path.GetDirectoryName(abs)!;
        MoveFile(abs, Eindeutig(Path.Combine(dir, Path.GetFileName(abs))));
        if (!string.Equals(src.TrimEnd(Path.DirectorySeparatorChar), Eingang, StringComparison.OrdinalIgnoreCase)) AufraeumenLeer(src);
    }

    public List<string> FehlendeDateien(SteuerDaten d) =>
        d.Belege.SelectMany(b => b.Dateien).Where(r => !File.Exists(Absolut(r))).ToList();

    private void AufraeumenLeer(string dir)
    {
        try
        {
            while (IstImOrdner(dir) && !string.Equals(dir.TrimEnd(Path.DirectorySeparatorChar), Eingang, StringComparison.OrdinalIgnoreCase)
                   && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
                dir = Path.GetDirectoryName(dir)!;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void MoveFile(string from, string to)
    {
        try { File.Move(from, to); }
        catch (IOException) when (File.Exists(from) && !File.Exists(to))
        {
            File.Copy(from, to);
            File.Delete(from);
        }
    }

    public static string Eindeutig(string pfad)
    {
        if (!File.Exists(pfad)) return pfad;
        var dir = Path.GetDirectoryName(pfad)!;
        var name = Path.GetFileNameWithoutExtension(pfad);
        var ext = Path.GetExtension(pfad);
        for (int i = 2; ; i++)
        {
            var p = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(p)) return p;
        }
    }
}
