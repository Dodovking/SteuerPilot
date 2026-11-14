using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace SteuerPilot.Core;

public static class Export
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public static string Csv(SteuerDaten d, int jahr)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Datum;Gruppe;Kategorie;Formular;Person;Beschreibung;Betrag;Anteil %;Abschreibung Jahre;Arbeitskosten;Steuerlich angesetzt;Notiz;Dateien");
        foreach (var p in Auswertung.Posten(d, jahr))
        {
            var b = p.Beleg;
            sb.AppendJoin(';',
                b.Datum.ToString("dd.MM.yyyy", De), Feld(Kategorien.GruppenName(b.Kat.Gruppe)), Feld(b.Kat.Name), Feld(b.Kat.Formular),
                Feld(d.Profil.PersonName(b.Person)), Feld(b.Beschreibung + (p.AusVorjahr ? $" (Abschreibung aus {b.Jahr})" : "")),
                b.Betrag.ToString("0.00", De), b.Anteil, b.AfaJahre, b.Arbeitskosten?.ToString("0.00", De) ?? "", p.Ansatz.ToString("0.00", De),
                Feld(b.Notiz), Feld(string.Join(" | ", b.Dateien)));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string Feld(string s) => s.Contains(';') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    public static string Bericht(SteuerDaten d, int jahr)
    {
        var posten = Auswertung.Posten(d, jahr);
        var s = Auswertung.Schaetzen(d, jahr);
        string H(string t) => WebUtility.HtmlEncode(t);
        string E(decimal v) => v.ToString("N2", De) + " €";
        var sb = new StringBuilder();
        sb.Append($$"""
            <!doctype html><html lang="de"><head><meta charset="utf-8"><title>SteuerPilot – Übersicht {{jahr}}</title>
            <style>
            body{font-family:"Segoe UI",Arial,sans-serif;color:#111827;margin:32px;max-width:1000px}
            h1{margin:0 0 4px}h2{margin-top:28px;border-bottom:2px solid #2563eb;padding-bottom:4px}
            .muted{color:#6b7280}table{border-collapse:collapse;width:100%;margin-top:8px}
            th,td{padding:6px 8px;border-bottom:1px solid #e5e7eb;text-align:left;vertical-align:top;font-size:14px}
            td.n,th.n{text-align:right;white-space:nowrap}tr.sum td{font-weight:600;border-top:2px solid #9ca3af}
            .box{background:#eff6ff;border:1px solid #bfdbfe;border-radius:10px;padding:14px 18px;margin-top:12px;font-size:18px}
            .hint{background:#fffbeb;border:1px solid #fde68a;border-radius:8px;padding:8px 12px;margin:6px 0;font-size:14px}
            @media print{body{margin:10mm} .noprint{display:none} }
            </style></head><body>
            <h1>Steuerjahr {{jahr}}</h1>
            <div class="muted">{{H(d.Profil.PersonName(Person.Ich))}}{{(d.Profil.Splitting ? " & " + H(d.Profil.PersonName(Person.Partner)) + " (Zusammenveranlagung)" : "")}} · erstellt am {{DateTime.Now:dd.MM.yyyy}} mit SteuerPilot</div>
            """);
        var res = s.Ergebnis;
        sb.Append($"<div class=\"box\">Geschätzt: <b>{(res >= 0 ? "Erstattung " + E(res) : "Nachzahlung " + E(-res))}</b> <span class=\"muted\">(ohne Gewähr)</span></div>");

        sb.Append("<h2>Summen nach Bereich</h2><table><tr><th>Bereich</th><th>Formular</th><th class=\"n\">Belege</th><th class=\"n\">Angesetzt</th></tr>");
        foreach (var g in posten.GroupBy(p => p.Kat).OrderBy(g => g.Key.Gruppe).ThenBy(g => g.Key.Name))
            sb.Append($"<tr><td>{H(Kategorien.GruppenName(g.Key.Gruppe))} – {H(g.Key.Name)}</td><td>{H(g.Key.Formular)}</td><td class=\"n\">{g.Count()}</td><td class=\"n\">{E(g.Sum(p => p.Ansatz))}</td></tr>");
        sb.Append("</table>");

        sb.Append("<h2>Steuer-Schätzung</h2><table>");
        foreach (var z in s.Zeilen)
            sb.Append($"<tr{(z.Summe ? " class=\"sum\"" : "")}><td>{H(z.Text.Trim())}</td><td class=\"muted\">{H(z.Hinweis)}</td><td class=\"n\">{E(z.Betrag)}</td></tr>");
        sb.Append("</table>");
        foreach (var h in s.Hinweise) sb.Append($"<div class=\"hint\">{H(h)}</div>");

        sb.Append("<h2>Alle Belege</h2><table><tr><th>Datum</th><th>Kategorie</th><th>Beschreibung</th><th class=\"n\">Betrag</th><th class=\"n\">Angesetzt</th></tr>");
        foreach (var p in posten)
        {
            var b = p.Beleg;
            var extra = new List<string>();
            if (b.Anteil != 100) extra.Add($"{b.Anteil} % beruflich");
            if (b.AfaJahre > 0) extra.Add($"Abschreibung {b.AfaJahre} J.");
            if (p.AusVorjahr) extra.Add($"gekauft {b.Jahr}");
            if (b.Arbeitskosten is > 0) extra.Add($"Arbeitskosten {E(b.Arbeitskosten.Value)}");
            sb.Append($"<tr><td>{b.Datum:dd.MM.yyyy}</td><td>{H(b.Kat.Name)}</td><td>{H(b.Beschreibung)}{(extra.Count > 0 ? "<br><span class=\"muted\">" + H(string.Join(" · ", extra)) + "</span>" : "")}</td><td class=\"n\">{E(b.Betrag)}</td><td class=\"n\">{E(p.Ansatz)}</td></tr>");
        }
        sb.Append("</table><p class=\"muted\">Diese Übersicht ist eine Ausfüllhilfe und ersetzt keine Steuerberatung. Die Steuererklärung selbst wird über ELSTER (elster.de) oder eine Steuer-Software abgegeben.</p></body></html>");
        return sb.ToString();
    }

    /// <summary>ZIP mit Übersicht, CSV und allen Belegdateien des Jahres – z. B. für den Steuerberater.</summary>
    public static int Steuerpaket(SteuerDaten d, Ablage ablage, int jahr, string zipPfad)
    {
        if (File.Exists(zipPfad)) File.Delete(zipPfad);
        int dateien = 0;
        using var zip = ZipFile.Open(zipPfad, ZipArchiveMode.Create);
        Text(zip, $"Übersicht {jahr}.html", Bericht(d, jahr));
        Text(zip, $"Belege {jahr}.csv", "\uFEFF" + Csv(d, jahr));
        var namen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Auswertung.Posten(d, jahr))
        {
            foreach (var rel in p.Beleg.Dateien)
            {
                var abs = ablage.Absolut(rel);
                if (!File.Exists(abs)) continue;
                var name = $"Belege/{p.Kat.Ordner.Replace('\\', '/')}/{Path.GetFileName(abs)}";
                for (int i = 2; !namen.Add(name); i++) name = $"Belege/{p.Kat.Ordner.Replace('\\', '/')}/{Path.GetFileNameWithoutExtension(abs)} ({i}){Path.GetExtension(abs)}";
                zip.CreateEntryFromFile(abs, name, CompressionLevel.Optimal);
                dateien++;
            }
        }
        return dateien;
    }

    /// <summary>Komplette Sicherung des Steuer-Ordners (ohne Papierkorb).</summary>
    public static int Sicherung(Ablage ablage, string zipPfad)
    {
        if (File.Exists(zipPfad)) File.Delete(zipPfad);
        int n = 0;
        using var zip = ZipFile.Open(zipPfad, ZipArchiveMode.Create);
        foreach (var f in Directory.EnumerateFiles(ablage.Root, "*", SearchOption.AllDirectories))
        {
            var rel = ablage.Relativ(f);
            if (rel.StartsWith("_Papierkorb", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(Path.GetFullPath(f), Path.GetFullPath(zipPfad), StringComparison.OrdinalIgnoreCase)) continue;
            zip.CreateEntryFromFile(f, rel.Replace('\\', '/'), CompressionLevel.Optimal);
            n++;
        }
        return n;
    }

    private static void Text(ZipArchive zip, string name, string content)
    {
        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
        w.Write(content);
    }
}
