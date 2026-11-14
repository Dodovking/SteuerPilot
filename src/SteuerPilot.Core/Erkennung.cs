using System.Globalization;
using System.Text.RegularExpressions;

namespace SteuerPilot.Core;

public sealed record Vorschlag(decimal? Betrag, DateTime? Datum, string? Kategorie, string? Beschreibung);

/// <summary>Liest Betrag, Datum und Kategorie aus dem per Texterkennung gelesenen Belegtext.</summary>
public static partial class Erkennung
{
    private static readonly string[] SummenWorte =
        ["gesamtbetrag", "rechnungsbetrag", "endbetrag", "zu zahlen", "zahlbetrag", "gesamtsumme", "summe", "gesamt", "total", "brutto", "betrag", "bar", "ec-karte", "girocard", "kartenzahlung"];

    [GeneratedRegex(@"(?<![\d,.])(\d{1,3}(?:\.\d{3})+|\d+)[,.](\d{2})(?![\d])")]
    private static partial Regex BetragRegex();

    [GeneratedRegex(@"(?<!\d)(\d{1,2})\s?[./-]\s?(\d{1,2})\s?[./-]\s?(\d{4}|\d{2})(?!\d)")]
    private static partial Regex DatumRegex();

    [GeneratedRegex(@"(?<!\d)(\d{4})-(\d{2})-(\d{2})(?!\d)")]
    private static partial Regex IsoRegex();

    [GeneratedRegex(@"(?<!\d)(\d{1,2})\.?\s+(januar|februar|märz|maerz|april|mai|juni|juli|august|september|oktober|november|dezember)\s+(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex MonatRegex();

    private static readonly string[] Monate = ["januar", "februar", "märz", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "dezember"];

    public static Vorschlag Auswerten(string text, DateTime? heute = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return new Vorschlag(null, null, null, null);
        return new Vorschlag(Betrag(text), Datum(text, heute ?? DateTime.Today), Kategorie(text), Beschreibung(text));
    }

    public static decimal? Betrag(string text)
    {
        var lines = text.Split('\n');
        decimal? best = null;
        int bestRang = int.MaxValue;
        decimal max = 0;
        foreach (var raw in lines)
        {
            var line = raw.ToLowerInvariant();
            if (line.Contains("mwst") || line.Contains("ust") || line.Contains("steuer") || line.Contains("netto") || line.Contains("rückgeld") || line.Contains("zurück")) continue;
            var werte = BetragRegex().Matches(line).Select(m => Parse(m)).Where(v => v is > 0 and < 1_000_000).Select(v => v!.Value).ToList();
            if (werte.Count == 0) continue;
            max = Math.Max(max, werte.Max());
            int rang = Array.FindIndex(SummenWorte, w => line.Contains(w));
            if (rang < 0) continue;
            var wert = werte[^1];
            if (rang < bestRang || (rang == bestRang && wert > best))
            {
                best = wert;
                bestRang = rang;
            }
        }
        return best ?? (max > 0 ? max : null);
    }

    private static decimal? Parse(Match m)
    {
        var ganz = m.Groups[1].Value.Replace(".", "");
        return decimal.TryParse($"{ganz}.{m.Groups[2].Value}", NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    public static DateTime? Datum(string text, DateTime heute)
    {
        var kandidaten = new List<(int pos, DateTime d)>();
        foreach (Match m in DatumRegex().Matches(text))
        {
            int t = int.Parse(m.Groups[1].Value), mo = int.Parse(m.Groups[2].Value), j = int.Parse(m.Groups[3].Value);
            if (j < 100) j += 2000;
            if (Gueltig(j, mo, t, out var d)) kandidaten.Add((m.Index, d));
        }
        foreach (Match m in IsoRegex().Matches(text))
        {
            if (Gueltig(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), out var d)) kandidaten.Add((m.Index, d));
        }
        foreach (Match m in MonatRegex().Matches(text))
        {
            var name = m.Groups[2].Value.ToLowerInvariant().Replace("maerz", "märz");
            if (Gueltig(int.Parse(m.Groups[3].Value), Array.IndexOf(Monate, name) + 1, int.Parse(m.Groups[1].Value), out var d)) kandidaten.Add((m.Index, d));
        }
        return kandidaten.Where(k => k.d <= heute.AddDays(1) && k.d >= heute.AddYears(-10)).OrderBy(k => k.pos).Select(k => (DateTime?)k.d).FirstOrDefault();
    }

    private static bool Gueltig(int j, int m, int t, out DateTime d)
    {
        d = default;
        if (j < 1990 || j > 2100 || m is < 1 or > 12 || t < 1 || t > DateTime.DaysInMonth(j, m)) return false;
        d = new DateTime(j, m, t);
        return true;
    }

    public static string? Kategorie(string text)
    {
        var t = text.ToLowerInvariant();
        Kategorie? best = null;
        int bestScore = 0;
        foreach (var k in Kategorien.Alle)
        {
            int score = k.Stichworte.Sum(w => t.Contains(w, StringComparison.Ordinal) ? (w.Length >= 8 ? 2 : 1) : 0);
            if (score > bestScore)
            {
                best = k;
                bestScore = score;
            }
        }
        return best?.Key;
    }

    public static string? Beschreibung(string text)
    {
        foreach (var raw in text.Split('\n'))
        {
            var l = raw.Trim();
            if (l.Count(char.IsLetter) < 3) continue;
            if (l.Length > 50) l = l[..50].Trim();
            return l;
        }
        return null;
    }
}
