namespace SteuerPilot.Core;

/// <summary>Ein Beleg, wie er sich in einem bestimmten Steuerjahr auswirkt.</summary>
public sealed record Posten(Beleg Beleg, decimal Ansatz, bool AusVorjahr)
{
    public Kategorie Kat => Beleg.Kat;
}

public sealed record Zeile(string Text, decimal Betrag, string Hinweis = "", bool Summe = false);

public sealed class Schaetzung
{
    public required int Jahr { get; init; }
    public required List<Zeile> Zeilen { get; init; }
    public decimal Einkuenfte { get; init; }
    public decimal ZuVersteuern { get; init; }
    public decimal Einkommensteuer { get; init; }
    public decimal Soli { get; init; }
    public decimal Kirchensteuer { get; init; }
    public decimal Festzusetzen => Einkommensteuer + Soli + Kirchensteuer;
    public decimal BereitsGezahlt { get; init; }
    public decimal Ergebnis => BereitsGezahlt - Festzusetzen;
    public decimal Grenzsteuersatz { get; init; }
    public decimal Werbungskosten { get; init; }
    public List<string> Hinweise { get; init; } = [];
}

public static class Auswertung
{
    /// <summary>Der steuerlich angesetzte Betrag eines Belegs im Jahr (Anteil, Arbeitskosten, Abschreibung).</summary>
    public static decimal Ansatz(Beleg b, int jahr)
    {
        var k = b.Kat;
        decimal basis = k.MitArbeitskosten && b.Arbeitskosten is > 0 ? Math.Min(b.Arbeitskosten.Value, b.Betrag) : b.Betrag;
        basis = Steuer.Cent(basis * Math.Clamp(b.Anteil, 0, 100) / 100m);
        if (k.Abschreibbar && b.AfaJahre > 0) return Steuer.Abschreibung(basis, b.Datum, b.AfaJahre, jahr);
        return b.Jahr == jahr ? basis : 0;
    }

    public static List<Posten> Posten(SteuerDaten d, int jahr)
    {
        var list = new List<Posten>();
        foreach (var b in d.Belege)
        {
            if (b.Jahr > jahr) continue;
            if (b.Jahr < jahr && !(b.Kat.Abschreibbar && b.AfaJahre > 1)) continue;
            var a = Ansatz(b, jahr);
            if (b.Jahr < jahr && a == 0) continue;
            list.Add(new Posten(b, a, b.Jahr < jahr));
        }
        return list.OrderBy(p => p.Beleg.Datum).ToList();
    }

    public static decimal Summe(IEnumerable<Posten> posten, Wirkung w, Person? person = null) =>
        posten.Where(p => p.Kat.Wirkung == w && (person == null || PersonVon(p.Beleg) == person)).Sum(p => p.Ansatz);

    private static Person PersonVon(Beleg b) => b.Person == Person.Partner ? Person.Partner : Person.Ich;

    public static Dictionary<Gruppe, decimal> SummenNachGruppe(IEnumerable<Posten> posten) =>
        posten.GroupBy(p => p.Kat.Gruppe).ToDictionary(g => g.Key, g => g.Sum(p => p.Ansatz));

    /// <summary>Grobe Schätzung von Erstattung bzw. Nachzahlung (ohne Gewähr, keine Steuerberatung).</summary>
    public static Schaetzung Schaetzen(SteuerDaten d, int jahr)
    {
        var profil = d.Profil;
        bool split = profil.Splitting;
        var angaben = d.Angaben(jahr);
        var posten = Posten(d, jahr);
        var zeilen = new List<Zeile>();
        var hinweise = new List<string>();
        var personen = split ? new[] { Person.Ich, Person.Partner } : [Person.Ich];

        decimal einkuenfteNs = 0, wkGesamt = 0;
        foreach (var p in personen)
        {
            var a = p == Person.Ich ? angaben.Ich : angaben.Partner;
            var name = profil.PersonName(p);
            var pendler = Steuer.Pendlerpauschale(a.EntfernungKm, a.Arbeitstage, jahr, a.MitEigenemAuto);
            var ho = Steuer.Homeoffice(a.HomeofficeTage);
            var belegWk = Summe(posten, Wirkung.Werbungskosten, p);
            var wk = pendler + ho + belegWk;
            if (a.Bruttolohn <= 0 && wk <= 0) continue;
            var pausch = Math.Min(Steuer.ArbeitnehmerPauschbetrag, a.Bruttolohn);
            var abzug = Math.Max(wk, pausch);
            zeilen.Add(new Zeile($"Bruttolohn {name}", a.Bruttolohn));
            if (pendler > 0) zeilen.Add(new Zeile($"  Pendlerpauschale {name}", -pendler, $"{a.EntfernungKm} km × {a.Arbeitstage} Tage"));
            if (ho > 0) zeilen.Add(new Zeile($"  Homeoffice-Pauschale {name}", -ho, $"{Math.Min(a.HomeofficeTage, Steuer.HomeofficeMaxTage)} Tage × 6 €"));
            if (belegWk > 0) zeilen.Add(new Zeile($"  Werbungskosten aus Belegen {name}", -belegWk));
            if (wk < pausch)
            {
                zeilen.Add(new Zeile($"  Arbeitnehmer-Pauschbetrag {name}", -pausch, "höher als deine Werbungskosten"));
                if (wk > 0) hinweise.Add($"{name}: Deine Werbungskosten ({wk:N2} €) liegen noch unter dem Pauschbetrag von {Steuer.ArbeitnehmerPauschbetrag:N0} € – erst darüber bringt jeder weitere Beleg Geld.");
            }
            einkuenfteNs += a.Bruttolohn - abzug;
            wkGesamt += wk;
        }

        var gewinn = Summe(posten, Wirkung.BetriebEinnahme) - Summe(posten, Wirkung.BetriebAusgabe);
        if (posten.Any(p => p.Kat.Gruppe == Gruppe.Selbststaendig)) zeilen.Add(new Zeile("Gewinn Selbstständig/Nebengewerbe", gewinn, "Einnahmen − Ausgaben"));
        var vuv = Summe(posten, Wirkung.MietEinnahme) - Summe(posten, Wirkung.MietAusgabe);
        if (posten.Any(p => p.Kat.Gruppe == Gruppe.Vermietung)) zeilen.Add(new Zeile("Überschuss Vermietung", vuv));

        var summeEinkuenfte = einkuenfteNs + gewinn + vuv;
        decimal gde = summeEinkuenfte;
        if (!split && profil.Alleinerziehend && profil.Kinder > 0)
        {
            var ent = Steuer.EntlastungAlleinerziehende(profil.Kinder);
            gde -= ent;
            zeilen.Add(new Zeile("Entlastungsbetrag Alleinerziehende", -ent));
        }
        zeilen.Add(new Zeile("Gesamtbetrag der Einkünfte", gde, "", true));

        // Vorsorgeaufwendungen
        var altersvorsorge = personen.Sum(p => Von(angaben, p).RentenversicherungAN) + Summe(posten, Wirkung.Altersvorsorge);
        var basisKv = personen.Sum(p =>
        {
            var a = Von(angaben, p);
            return Steuer.Cent(a.KrankenversicherungAN * (a.KrankengeldAnspruch ? 0.96m : 1m)) + a.PflegeversicherungAN;
        }) + Summe(posten, Wirkung.KrankenPflegeBasis);
        var sonstigeVorsorge = personen.Sum(p => Von(angaben, p).ArbeitslosenversicherungAN) + Summe(posten, Wirkung.SonstigeVorsorge);
        var hoechst = personen.Sum(p => Steuer.VorsorgeHoechstbetrag(Von(angaben, p).Bruttolohn <= 0));
        var weitereVorsorge = Math.Max(basisKv, Math.Min(basisKv + sonstigeVorsorge, hoechst));
        var vorsorge = altersvorsorge + weitereVorsorge;
        if (altersvorsorge > 0) zeilen.Add(new Zeile("Altersvorsorge (Rente)", -altersvorsorge));
        if (weitereVorsorge > 0) zeilen.Add(new Zeile("Kranken-, Pflege- & sonstige Versicherungen", -weitereVorsorge, sonstigeVorsorge > 0 && basisKv + sonstigeVorsorge > hoechst ? "Höchstbetrag erreicht" : ""));

        // Übrige Sonderausgaben
        var kist = personen.Sum(p => Von(angaben, p).Kirchensteuer) + Summe(posten, Wirkung.Sonderausgabe);
        var spenden = Math.Min(Summe(posten, Wirkung.Spende), Math.Max(0, gde * 0.2m));
        var kinderb = Steuer.Kinderbetreuung(Summe(posten, Wirkung.Kinderbetreuung), profil.Kinder, jahr);
        var schule = Steuer.Schulgeld(Summe(posten, Wirkung.Schulgeld), profil.Kinder);
        var ausbildung = Steuer.Erstausbildung(Summe(posten, Wirkung.Ausbildung), personen.Length);
        var unterhalt = Math.Min(Summe(posten, Wirkung.Unterhalt), Steuer.UnterhaltHoechstbetrag);
        var uebrige = kist + spenden + kinderb + schule + ausbildung + unterhalt;
        var pauschSa = Steuer.SonderausgabenPauschbetrag(split);
        void Add(string t, decimal v) { if (v > 0) zeilen.Add(new Zeile(t, -v)); }
        if (uebrige >= pauschSa)
        {
            Add("Kirchensteuer (gezahlt)", kist);
            Add("Spenden", spenden);
            Add("Kinderbetreuung", kinderb);
            Add("Schulgeld", schule);
            Add("Erstausbildung", ausbildung);
            Add("Unterhalt (Realsplitting)", unterhalt);
        }
        else zeilen.Add(new Zeile("Sonderausgaben-Pauschbetrag", -pauschSa));
        var sonderausgaben = vorsorge + Math.Max(uebrige, pauschSa);

        // Außergewöhnliche Belastungen
        var agb = Summe(posten, Wirkung.AussergewoehnlicheBelastung);
        decimal agbAbzug = 0;
        if (agb > 0)
        {
            var zumutbar = Steuer.ZumutbareBelastung(gde, split, profil.Kinder);
            agbAbzug = Math.Max(0, agb - zumutbar);
            zeilen.Add(new Zeile("Außergewöhnliche Belastungen", -agbAbzug, $"{agb:N2} € abzüglich zumutbarer Belastung {zumutbar:N2} €"));
            if (agbAbzug == 0) hinweise.Add($"Krankheitskosten & Co. wirken erst über der zumutbaren Belastung von {zumutbar:N2} € – sammle trotzdem alle Belege.");
        }

        var zve = Math.Max(0, decimal.Floor(gde - sonderausgaben - agbAbzug));
        zeilen.Add(new Zeile("Zu versteuerndes Einkommen", zve, "", true));

        var est = Steuer.Einkommensteuer(zve, jahr, split);
        zeilen.Add(new Zeile(split ? "Einkommensteuer (Splittingtarif)" : "Einkommensteuer (Grundtarif)", est));
        var hhd = Steuer.HaushaltsnaheErmaessigung(Summe(posten, Wirkung.Haushaltsnah), Summe(posten, Wirkung.Handwerker), Summe(posten, Wirkung.Minijob));
        var hhdWirksam = Math.Min(hhd, est);
        if (hhd > 0) zeilen.Add(new Zeile("Ermäßigung Handwerker & haushaltsnahe Dienste (§ 35a)", -hhdWirksam, "20 % der Arbeitskosten"));
        var estFest = est - hhdWirksam;
        var soli = Steuer.Soli(estFest, jahr, split);
        var kirche = profil.Kirchensteuer ? Steuer.Kirchensteuer(estFest, profil.KirchensteuerSatz) : 0;
        if (soli > 0) zeilen.Add(new Zeile("Solidaritätszuschlag", soli));
        if (kirche > 0) zeilen.Add(new Zeile($"Kirchensteuer ({profil.KirchensteuerSatz:0} %)", kirche));
        zeilen.Add(new Zeile("Steuer laut Schätzung", estFest + soli + kirche, "", true));

        var gezahlt = personen.Sum(p => { var a = Von(angaben, p); return a.Lohnsteuer + a.Soli + a.Kirchensteuer; }) + angaben.Vorauszahlungen;
        zeilen.Add(new Zeile("Bereits gezahlt (Lohnsteuer, Soli, KiSt, Vorauszahlungen)", gezahlt, "", true));

        if (personen.All(p => Von(angaben, p).Bruttolohn <= 0) && gewinn == 0 && vuv == 0)
            hinweise.Insert(0, "Trag zuerst die Werte deiner Lohnsteuerbescheinigung ein, sonst ist die Schätzung nicht aussagekräftig.");
        foreach (var p in personen)
        {
            var a = Von(angaben, p);
            if (a.Bruttolohn > 0 && a.RentenversicherungAN + a.KrankenversicherungAN + a.PflegeversicherungAN == 0
                && Summe(posten, Wirkung.Altersvorsorge) + Summe(posten, Wirkung.KrankenPflegeBasis) == 0)
                hinweise.Insert(0, $"{(p == Person.Ich ? "Du" : "Partner")}: Renten-, Kranken- und Pflegeversicherung fehlen – trag sie unter „Meine Angaben“ aus der Lohnsteuerbescheinigung (Zeilen 23–26) ein, sonst fällt die Schätzung viel zu ungünstig aus.");
        }
        if (profil.Kinder > 0) hinweise.Add("Kinderfreibeträge sind nicht eingerechnet – das Finanzamt prüft automatisch, ob Kindergeld oder Freibetrag günstiger ist.");
        if (posten.Any(p => p.Kat.Gruppe == Gruppe.Haushaltsnah && p.Beleg.Arbeitskosten is null or 0))
            hinweise.Add("Bei Handwerker-/Dienstleistungsrechnungen zählen nur die Arbeitskosten – trag sie im Beleg ein, sonst wird der ganze Betrag angesetzt.");
        if (posten.Any(p => p.Kat.Key == "kapitalertraege")) hinweise.Add("Kapitalerträge sind nicht eingerechnet (Abgeltungsteuer).");

        return new Schaetzung
        {
            Jahr = jahr,
            Zeilen = zeilen,
            Einkuenfte = summeEinkuenfte,
            ZuVersteuern = zve,
            Einkommensteuer = estFest,
            Soli = soli,
            Kirchensteuer = kirche,
            BereitsGezahlt = gezahlt,
            Grenzsteuersatz = Steuer.Grenzsteuersatz(zve, jahr, split),
            Werbungskosten = wkGesamt,
            Hinweise = hinweise,
        };
    }

    private static PersonAngaben Von(Jahresangaben a, Person p) => p == Person.Partner ? a.Partner : a.Ich;
}
