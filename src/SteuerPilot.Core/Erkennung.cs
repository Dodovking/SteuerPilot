using System.Globalization;
using System.Text.RegularExpressions;

namespace SteuerPilot.Core;

public sealed record Vorschlag(decimal? Betrag, DateTime? Datum, string? Kategorie, string? Beschreibung, Kapitalwerte? Kapital = null, PersonAngaben? Angaben = null, string? Art = null, string? Notiz = null);

/// <summary>Liest Betrag, Datum und Kategorie aus dem per Texterkennung gelesenen Belegtext.</summary>
public static partial class Erkennung
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    private static readonly string[] SummenWorte =
        ["gesamtbetrag", "rechnungsbetrag", "endbetrag", "zu zahlen", "zahlbetrag", "gesamtsumme", "summe", "gesamt", "total", "brutto", "betrag", " bar", "ec-karte", "girocard", "kartenzahlung"];

    [GeneratedRegex(@"(?<![\d,.])(\d{1,3}(?:\.\d{3})+|\d+)[,.](\d{2})(?![\d])")]
    private static partial Regex BetragRegex();

    [GeneratedRegex(@"(?<!\d)(\d{1,2})\s?[./-]\s?(\d{1,2})\s?[./-]\s?(\d{4}|\d{2})(?!\d)")]
    private static partial Regex DatumRegex();

    [GeneratedRegex(@"\b(mwst|ust|umsatzsteuer|mehrwertsteuer|vorsteuer|netto|rückgeld|zurück|steuer\s*\d{1,2}\s*%)\b")]
    private static partial Regex SteuerZeileRegex();

    [GeneratedRegex(@"(-\s?)?(\d{1,3}(?:\.\d{3})+|\d+),(\d{2})(?!\d)(\s?-)?")]
    private static partial Regex WertRegex();

    [GeneratedRegex(@"(?:für\s+(?:das\s+)?(?:kalender|steuer)?jahr|kalenderjahr|steuerjahr|bescheinigung(?:\s+für)?|bescheid\s+für|rentenbezüge|meldung für)\s*:?\s*(20\d{2})(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex SteuerjahrRegex();

    [GeneratedRegex(@"(?<!\d)(19[5-9]\d|20\d{2})(?!\d)")]
    private static partial Regex JahrRegex();

    [GeneratedRegex(@"(?<!\d)(\d{4})-(\d{2})-(\d{2})(?!\d)")]
    private static partial Regex IsoRegex();

    [GeneratedRegex(@"(?<!\d)(\d{1,2})\.?\s+(januar|februar|märz|maerz|april|mai|juni|juli|august|september|oktober|november|dezember)\s+(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex MonatRegex();

    private static readonly string[] Monate = ["januar", "februar", "märz", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "dezember"];

    public static Vorschlag Auswerten(string text, DateTime? heute = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return new Vorschlag(null, null, null, null);
        var h = heute ?? DateTime.Today;
        var kat = Kategorie(text);
        var (betrag, einzeln) = Betraege(text);
        var notiz = einzeln.Count > 1 ? $"Summe aus {einzeln.Count} Belegen: {string.Join(" + ", einzeln.Select(x => x.ToString("N2", De)))} €" : null;
        var datum = Datum(text, h);
        Kapitalwerte? kap = null;
        PersonAngaben? angaben = null;
        var k = Kategorien.Get(kat);
        if (k.Wirkung == Wirkung.Kapital)
        {
            kap = Kapital(text);
            if (kap != null) betrag = Math.Abs(kap.Ertraege != 0 ? kap.Ertraege : kap.VerlustAktien + kap.VerlustSonstige);
        }
        else if (k.Key == "lohnsteuerbescheinigung")
        {
            angaben = Lohnsteuer(text);
            if (angaben != null) betrag = angaben.Bruttolohn;
        }
        else if (k.Key == "rente")
        {
            angaben = Rente(text);
            if (angaben != null) betrag = angaben.Rente;
        }
        else if (k.Key == "bescheide")
        {
            var z = Klein(text);
            var b = Wert(z, l => l.Contains("erstattung") || l.Contains("nachzahlung") || l.Contains("verbleibende") || l.Contains("abrechnungsergebnis"));
            if (b != null) betrag = Math.Abs(b.Value);
        }
        if (k.Key is "lohnsteuerbescheinigung" or "rente" or "bescheide" || k.Wirkung == Wirkung.Kapital)
        {
            var sj = Steuerjahr(text, h);
            if (sj != null) datum = new DateTime(sj.Value, 12, 31);
        }
        var art = Art(text);
        if (art == "Kontoauszug")
        {
            kat = "kap-konto";
            betrag = null;
            notiz = null;
        }
        else if (Kilometer(text) is int km && k.Wirkung != Wirkung.Kapital)
        {
            var kosten = km * 0.30m;
            betrag = (betrag ?? 0) + kosten;
            notiz = (notiz == null ? "" : notiz + "; ") + $"{km} km × 0,30 € = {kosten.ToString("N2", De)} € Fahrtkosten hinzugerechnet";
        }
        var beschreibung = Beschreibung(text);
        if (art != null) beschreibung = beschreibung == null ? art : $"{art} – {beschreibung}";
        if (beschreibung?.Length > 60) beschreibung = beschreibung[..60].Trim();
        return new Vorschlag(betrag, datum, kat, beschreibung, kap, angaben, art, notiz);
    }

    private static readonly (string Art, string[] Worte)[] Arten =
    [
        ("Steuerbescheid", ["steuerbescheid", "bescheid fur", "bescheid uber", "festsetzung", "festgesetzt", "finanzamt"]),
        ("Lohnsteuerbescheinigung", ["lohnsteuerbescheinigung", "bruttoarbeitslohn"]),
        ("Rentenbescheinigung", ["rentenbezugsmitteilung", "rentenanpassung", "meldung an die finanzverwaltung", "rentenbetrag"]),
        ("Bankbescheinigung", ["jahressteuerbescheinigung", "steuerbescheinigung", "verlustbescheinigung", "kapitalertragsteuer"]),
        ("Spendenbescheinigung", ["zuwendungsbestatigung", "spendenbescheinigung", "spendenquittung"]),
        ("Kontoauszug", ["kontoauszug", "depotauszug", "kontostand", "girokonto", "buchungstag", "umsatzubersicht", "saldo", "valuta"]),
        ("Wertpapierabrechnung", ["wertpapierabrechnung", "sparplan", "orderabrechnung", "ausfuhrung"]),
        ("Versicherung", ["versicherungsschein", "beitragsrechnung", "beitragsbescheinigung", "police"]),
        ("Vertrag", ["vertrag", "vereinbarung zwischen"]),
        ("Kassenbon", ["kassenbon", "bon-nr", "bonnr", "ruckgeld", "gegeben", "kasse", "ec-karte", "girocard", "tse"]),
        ("Rechnung", ["rechnung", "rechnungsnummer", "rechnungs-nr", "rechnungsbetrag", "zahlbar bis", "leistungszeitraum"]),
    ];

    /// <summary>Was für ein Dokument ist das? (Rechnung, Kassenbon, Steuerbescheid, Bescheinigung …)</summary>
    public static string? Art(string text)
    {
        var t = string.Join("\n", Klein(text));
        string? best = null;
        int bestScore = 0;
        foreach (var (art, worte) in Arten)
        {
            int score = worte.Count(w => t.Contains(w, StringComparison.Ordinal));
            if (score > bestScore) { best = art; bestScore = score; }
        }
        return best;
    }

    /// <summary>„für das Jahr 2025“, „Steuerbescheinigung 2025“ – Bescheinigungen gehören ins bescheinigte Jahr, nicht ins Ausstellungsjahr.</summary>
    public static int? Steuerjahr(string text, DateTime heute)
    {
        foreach (Match m in SteuerjahrRegex().Matches(text))
        {
            var j = int.Parse(m.Groups[1].Value);
            if (j <= heute.Year && j >= heute.Year - 10) return j;
        }
        return null;
    }

    /// <summary>Betrag in der Zeile, die passt (letzter Wert), sonst erster Wert der nächsten Zeile.</summary>
    private static decimal? Wert(string[] zeilen, Func<string, bool> passt)
    {
        for (int i = 0; i < zeilen.Length; i++)
        {
            if (!passt(zeilen[i])) continue;
            var m = WertRegex().Matches(zeilen[i]);
            if (m.Count == 0 && i + 1 < zeilen.Length) m = WertRegex().Matches(zeilen[i + 1]);
            if (m.Count == 0) continue;
            var x = m[^1];
            if (!decimal.TryParse($"{x.Groups[2].Value.Replace(".", "")}.{x.Groups[3].Value}", NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) continue;
            return x.Groups[1].Success || x.Groups[4].Success ? -v : v;
        }
        return null;
    }

    private static string[] Klein(string text) => text.ToLowerInvariant().Replace("ä", "a").Replace("ö", "o").Replace("ü", "u").Replace("ß", "ss").Split('\n');

    public static Kapitalwerte? Kapital(string text)
    {
        var z = Klein(text);
        var w = new Kapitalwerte
        {
            Ertraege = Wert(z, l => l.Contains("kapitalertr") && !l.Contains("kapitalertragsteuer") && !l.Contains("davon")) ?? 0,
            AktienGewinne = Math.Max(0, Wert(z, l => l.Contains("aktienverausserung") && l.Contains("gewinn") && !l.Contains("verlust")) ?? 0),
            VerlustAktien = Math.Abs(Wert(z, l => l.Contains("verlust") && l.Contains("aktie") && !l.Contains("ohne")) ?? 0),
            VerlustSonstige = Math.Abs(Wert(z, l => l.Contains("verlust") && l.Contains("ohne")) ?? 0),
            Kapest = Math.Abs(Wert(z, l => l.Contains("kapitalertragsteuer") && !l.Contains("kirchensteuer")) ?? 0),
            Soli = Math.Abs(Wert(z, l => l.Contains("solidaritat")) ?? 0),
            Kist = Math.Abs(Wert(z, l => l.Contains("kirchensteuer")) ?? 0),
            Pauschbetrag = Math.Abs(Wert(z, l => l.Contains("pauschbetrag") || l.Contains("freistellungsauftrag")) ?? 0),
        };
        return w.Leer && w.Pauschbetrag == 0 ? null : w;
    }

    public static PersonAngaben? Lohnsteuer(string text)
    {
        var z = Klein(text);
        var a = new PersonAngaben
        {
            Bruttolohn = Wert(z, l => l.Contains("bruttoarbeitslohn")) ?? 0,
            Lohnsteuer = Wert(z, l => l.Contains("lohnsteuer") && l.Contains("einbehalten")) ?? 0,
            Soli = Wert(z, l => l.Contains("solidaritat") && l.Contains("einbehalten")) ?? 0,
            Kirchensteuer = Wert(z, l => l.Contains("kirchensteuer des arbeitnehmers")) ?? 0,
            RentenversicherungAN = Wert(z, l => l.Contains("rentenversicherung") && l.Contains("arbeitnehmer")) ?? 0,
            KrankenversicherungAN = Wert(z, l => l.Contains("krankenversicherung") && l.Contains("arbeitnehmer")) ?? 0,
            PflegeversicherungAN = Wert(z, l => l.Contains("pflegeversicherung") && l.Contains("arbeitnehmer")) ?? 0,
            ArbeitslosenversicherungAN = Wert(z, l => l.Contains("arbeitslosenversicherung")) ?? 0,
        };
        return a.Bruttolohn > 0 ? a : null;
    }

    public static PersonAngaben? Rente(string text)
    {
        var z = Klein(text);
        var a = new PersonAngaben
        {
            Rente = Math.Abs(Wert(z, l => (l.Contains("rentenbetrag") || l.Contains("leibrente") || l.Contains("jahresbetrag") || l.Contains("betrag der rente") || l.Contains("rentenbezuge"))
                                            && !l.Contains("kranken") && !l.Contains("pflege")) ?? 0),
            RentnerKv = Math.Abs(Wert(z, l => l.Contains("krankenversicherung")) ?? 0),
            RentnerPv = Math.Abs(Wert(z, l => l.Contains("pflegeversicherung")) ?? 0),
        };
        for (int i = 0; i < z.Length && a.Rentenbeginn == 0; i++)
        {
            if (!z[i].Contains("beginn")) continue;
            var m = JahrRegex().Match(z[i]);
            if (!m.Success && i + 1 < z.Length) m = JahrRegex().Match(z[i + 1]);
            if (m.Success) a.Rentenbeginn = int.Parse(m.Value);
        }
        return a.Rente > 0 ? a : null;
    }

    public static decimal? Betrag(string text) => Betraege(text).Betrag;

    private static readonly string[] Endsummen = ["gesamtbetrag", "rechnungsbetrag", "endbetrag", "zu zahlen", "zahlbetrag", "gesamtsumme", "summe", "gesamt", "total", "brutto"];

    /// <summary>Endbetrag; liegen mehrere Bons auf einer Seite (z. B. aufgeklebt), wird ihre Summe gebildet.</summary>
    public static (decimal? Betrag, List<decimal> Einzeln) Betraege(string text)
    {
        var lines = text.Split('\n');
        decimal? best = null;
        int bestRang = int.MaxValue;
        decimal max = 0;
        var summen = new List<decimal>();
        foreach (var raw in lines)
        {
            var line = raw.ToLowerInvariant();
            if (SteuerZeileRegex().IsMatch(line)) continue;
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
            // Nebeneinanderliegende Bons landen in einer Zeile: jede Endsumme einzeln nehmen.
            if (line.Contains("zwischensumme") || line.Contains("teilsumme")) continue;
            var starts = Endsummen.SelectMany(w => AlleStellen(line, w)).OrderBy(i => i).ToList();
            var behalten = new List<int>();
            foreach (var p in starts) if (behalten.Count == 0 || p - behalten[^1] > 10) behalten.Add(p);
            starts = behalten;
            for (int i = 0; i < starts.Count; i++)
            {
                var stueck = line[starts[i]..(i + 1 < starts.Count ? starts[i + 1] : line.Length)];
                var m = BetragRegex().Match(stueck);
                if (m.Success && Parse(m) is decimal v and > 0) summen.Add(v);
            }
        }
        var verschieden = summen.Distinct().ToList();
        int daten = DatumRegex().Matches(text).Select(m => m.Value).Distinct().Count();
        bool mwst = verschieden.Any(x => verschieden.Any(y => y != x && (Math.Abs(y * 1.19m - x) < 0.02m || Math.Abs(y * 1.07m - x) < 0.02m)));
        bool teilsummen = verschieden.Count > 2 && verschieden.Max() == verschieden.Sum() - verschieden.Max();
        if (verschieden.Count >= 2 && daten >= 2 && !mwst && !teilsummen)
            return (verschieden.Sum(), verschieden);
        return (best ?? (max > 0 ? max : null), []);
    }

    private static IEnumerable<int> AlleStellen(string s, string w)
    {
        for (int i = s.IndexOf(w, StringComparison.Ordinal); i >= 0; i = s.IndexOf(w, i + w.Length, StringComparison.Ordinal)) yield return i;
    }

    [GeneratedRegex(@"(?<!\d)(\d{1,4})\s?km\b", RegexOptions.IgnoreCase)]
    private static partial Regex KmRegex();

    /// <summary>Gefahrene Kilometer (z. B. handschriftlich „3 x 52 km = 156 km Fahrten“).</summary>
    public static int? Kilometer(string text)
    {
        var t = text.ToLowerInvariant();
        if (!t.Contains("fahrt") && !t.Contains("kilomet")) return null;
        var km = KmRegex().Matches(text).Select(m => int.Parse(m.Groups[1].Value)).Where(k => k > 0).ToList();
        return km.Count == 0 ? null : km.Max();
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
