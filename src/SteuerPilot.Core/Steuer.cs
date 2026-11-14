namespace SteuerPilot.Core;

/// <summary>Formeln des deutschen Einkommensteuerrechts (vereinfacht, ohne Gewähr).</summary>
public static class Steuer
{
    private sealed record Tarif(int Jahr, decimal Grundfreibetrag, decimal Zone2Ende, decimal Zone3Ende, decimal Zone4Ende,
        decimal A2, decimal A3, decimal C3, decimal Abzug4, decimal Abzug5, decimal SoliFreigrenze);

    // § 32a EStG; Zone 2/3: (A·y + 1400)·y bzw. (A·z + 2397)·z + C
    private static readonly Tarif[] Tarife =
    [
        new(2023, 10908, 15999, 62809, 277825, 979.18m, 192.59m, 966.53m, 9972.98m, 18307.73m, 17543),
        new(2024, 11784, 17005, 66760, 277825, 954.80m, 181.19m, 991.21m, 10636.31m, 18971.06m, 18130),
        new(2025, 12096, 17443, 68480, 277825, 932.30m, 176.64m, 1015.13m, 10911.92m, 19246.67m, 19950),
        new(2026, 12348, 17799, 69878, 277825, 914.51m, 173.10m, 1034.87m, 11135.63m, 19470.38m, 20350),
    ];

    public static readonly int[] Jahre = Tarife.Select(t => t.Jahr).ToArray();

    public static int ErstesJahr => Jahre[0];
    public static int LetztesJahr => Jahre[^1];

    private static Tarif TarifFuer(int jahr) => Tarife.FirstOrDefault(t => t.Jahr == jahr) ?? (jahr < ErstesJahr ? Tarife[0] : Tarife[^1]);

    public static decimal Grundfreibetrag(int jahr) => TarifFuer(jahr).Grundfreibetrag;

    /// <summary>Tarifliche Einkommensteuer für das zu versteuernde Einkommen.</summary>
    public static decimal Einkommensteuer(decimal zvE, int jahr, bool splitting)
    {
        if (zvE <= 0) return 0;
        return splitting ? 2 * Grundtarif(decimal.Floor(zvE / 2), jahr) : Grundtarif(decimal.Floor(zvE), jahr);
    }

    private static decimal Grundtarif(decimal x, int jahr)
    {
        var t = TarifFuer(jahr);
        decimal st;
        if (x <= t.Grundfreibetrag) st = 0;
        else if (x <= t.Zone2Ende)
        {
            var y = (x - t.Grundfreibetrag) / 10000m;
            st = (t.A2 * y + 1400m) * y;
        }
        else if (x <= t.Zone3Ende)
        {
            var z = (x - t.Zone2Ende) / 10000m;
            st = (t.A3 * z + 2397m) * z + t.C3;
        }
        else if (x <= t.Zone4Ende) st = 0.42m * x - t.Abzug4;
        else st = 0.45m * x - t.Abzug5;
        return decimal.Floor(st);
    }

    public static decimal Grenzsteuersatz(decimal zvE, int jahr, bool splitting)
    {
        var a = Einkommensteuer(zvE, jahr, splitting);
        var b = Einkommensteuer(zvE + 100, jahr, splitting);
        return (b - a) / 100m;
    }

    public static decimal Soli(decimal est, int jahr, bool splitting)
    {
        var fg = TarifFuer(jahr).SoliFreigrenze * (splitting ? 2 : 1);
        if (est <= fg) return 0;
        return Cent(Math.Min(est * 0.055m, (est - fg) * 0.119m));
    }

    public static decimal Kirchensteuer(decimal est, decimal satz) => est <= 0 ? 0 : Cent(est * satz / 100m);

    public const decimal ArbeitnehmerPauschbetrag = 1230m;

    public static decimal SonderausgabenPauschbetrag(bool splitting) => splitting ? 72m : 36m;

    /// <summary>Entfernungspauschale; ohne eigenes Auto auf 4.500 € begrenzt.</summary>
    public static decimal Pendlerpauschale(int km, int tage, int jahr, bool mitAuto = true)
    {
        if (km <= 0 || tage <= 0) return 0;
        decimal proTag = jahr >= 2026
            ? km * 0.38m
            : Math.Min(km, 20) * 0.30m + Math.Max(km - 20, 0) * 0.38m;
        var summe = proTag * tage;
        return Cent(mitAuto ? summe : Math.Min(summe, 4500m));
    }

    public const int HomeofficeMaxTage = 210;
    public const decimal HomeofficeProTag = 6m;

    public static decimal Homeoffice(int tage) => Math.Clamp(tage, 0, HomeofficeMaxTage) * HomeofficeProTag;

    /// <summary>Zumutbare Belastung nach § 33 Abs. 3 EStG, stufenweise berechnet.</summary>
    public static decimal ZumutbareBelastung(decimal gesamtbetrag, bool splitting, int kinder)
    {
        if (gesamtbetrag <= 0) return 0;
        decimal[] saetze = kinder switch
        {
            >= 3 => [1, 1, 2],
            >= 1 => [2, 3, 4],
            _ => splitting ? [4, 5, 6] : [5, 6, 7],
        };
        decimal[] grenzen = [15340m, 51130m, decimal.MaxValue];
        decimal summe = 0, unten = 0;
        for (int i = 0; i < 3 && gesamtbetrag > unten; i++)
        {
            var teil = Math.Min(gesamtbetrag, grenzen[i]) - unten;
            summe += decimal.Floor(teil * saetze[i] / 100m);
            unten = grenzen[i];
        }
        return summe;
    }

    /// <summary>Steuerermäßigung nach § 35a EStG.</summary>
    public static decimal HaushaltsnaheErmaessigung(decimal dienstleistungen, decimal handwerker, decimal minijob) =>
        Cent(Math.Min(dienstleistungen * 0.2m, 4000m) + Math.Min(handwerker * 0.2m, 1200m) + Math.Min(minijob * 0.2m, 510m));

    public static decimal Kinderbetreuung(decimal kosten, int kinder, int jahr)
    {
        int k = Math.Max(1, kinder);
        return Cent(jahr >= 2025 ? Math.Min(kosten * 0.8m, 4800m * k) : Math.Min(kosten * 2m / 3m, 4000m * k));
    }

    public static decimal Schulgeld(decimal kosten, int kinder) => Cent(Math.Min(kosten * 0.3m, 5000m * Math.Max(1, kinder)));

    public static decimal Erstausbildung(decimal kosten, int personen) => Math.Min(kosten, 6000m * Math.Max(1, personen));

    public const decimal UnterhaltHoechstbetrag = 13805m;

    /// <summary>Grenze für geringwertige Wirtschaftsgüter (Arbeitnehmer: brutto, inkl. 19 % USt).</summary>
    public const decimal GwgNetto = 800m;
    public const decimal GwgBrutto = 952m;

    /// <summary>
    /// Lineare Abschreibung monatsgenau. Nutzungsdauer 1 Jahr (z. B. Computer/Software): komplett im Kaufjahr.
    /// </summary>
    public static decimal Abschreibung(decimal kosten, DateTime kauf, int nutzungsjahre, int jahr)
    {
        if (kosten <= 0) return 0;
        if (nutzungsjahre <= 1) return jahr == kauf.Year ? kosten : 0;
        int start = kauf.Year * 12 + kauf.Month - 1;
        int monate = nutzungsjahre * 12;
        int von = Math.Max(start, jahr * 12);
        int bis = Math.Min(start + monate, (jahr + 1) * 12);
        if (bis <= von) return 0;
        // Rundungsrest landet im letzten Monat, damit die Summe genau den Kosten entspricht.
        decimal bisher = Cent(kosten * (von - start) / monate);
        decimal danach = bis == start + monate ? kosten : Cent(kosten * (bis - start) / monate);
        return danach - bisher;
    }

    /// <summary>Gebäude-AfA (§ 7 Abs. 4 EStG): Baujahr vor 1925 2,5 %, ab 2023 fertiggestellt 3 %, sonst 2 %.</summary>
    public static decimal GebaeudeAfaSatz(int baujahr) => baujahr < 1925 ? 2.5m : baujahr >= 2023 ? 3m : 2m;

    /// <summary>Gebäude-AfA im Jahr, im Anschaffungsjahr monatsgenau, insgesamt höchstens die Kosten.</summary>
    public static decimal GebaeudeAfa(decimal kosten, DateTime anschaffung, decimal satz, int jahr)
    {
        if (kosten <= 0 || satz <= 0 || jahr < anschaffung.Year) return 0;
        decimal voll = kosten * satz / 100m;
        decimal Bis(int j) => Math.Min(kosten, Cent(voll * (13 - anschaffung.Month) / 12m) + Cent(voll * (j - anschaffung.Year)));
        return Bis(jahr) - (jahr == anschaffung.Year ? 0 : Bis(jahr - 1));
    }

    public static IEnumerable<(int Jahr, decimal Betrag)> Abschreibungsplan(decimal kosten, DateTime kauf, int nutzungsjahre)
    {
        int ende = nutzungsjahre <= 1 ? kauf.Year : kauf.Year + nutzungsjahre;
        for (int j = kauf.Year; j <= ende; j++)
        {
            var b = Abschreibung(kosten, kauf, nutzungsjahre, j);
            if (b > 0) yield return (j, b);
        }
    }

    public static decimal Netto(decimal brutto, decimal satz) => Cent(brutto / (1 + satz / 100m));

    public static decimal Brutto(decimal netto, decimal satz) => Cent(netto * (1 + satz / 100m));

    /// <summary>Besteuerungsanteil der Rente nach Jahr des Rentenbeginns (§ 22 EStG, ab 2023 +0,5 % je Jahr).</summary>
    public static decimal Besteuerungsanteil(int beginn) => beginn <= 2005 ? 50m
        : beginn <= 2020 ? 50m + 2m * (beginn - 2005)
        : beginn <= 2022 ? 80m + (beginn - 2020)
        : Math.Min(100m, 82m + 0.5m * (beginn - 2022));

    public const decimal WerbungskostenPauschbetragRente = 102m;

    /// <summary>Abgeltungsteuer 25 % inkl. Soli und ggf. Kirchensteuer (die KiSt mindert die KapESt).</summary>
    public static (decimal Kapest, decimal Soli, decimal Kist) Abgeltungsteuer(decimal betrag, decimal kistSatz)
    {
        if (betrag <= 0) return (0, 0, 0);
        var kapest = Cent(betrag / (4m + kistSatz / 100m));
        return (kapest, Cent(kapest * 0.055m), Cent(kapest * kistSatz / 100m));
    }

    public static decimal Cent(decimal v) => decimal.Round(v, 2, MidpointRounding.AwayFromZero);

    public static decimal VorsorgeHoechstbetrag(bool selbstzahler) => selbstzahler ? 2800m : 1900m;

    public static decimal EntlastungAlleinerziehende(int kinder) => kinder <= 0 ? 0 : 4260m + 240m * (kinder - 1);
}
