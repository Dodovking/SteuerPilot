namespace SteuerPilot.Core;

public enum Gruppe
{
    Einnahmen,
    Werbungskosten,
    Vorsorge,
    Sonderausgaben,
    AussergewoehnlicheBelastungen,
    Haushaltsnah,
    Selbststaendig,
    Vermietung,
    Sonstiges,
}

/// <summary>Wie ein Beleg in der Steuerschätzung wirkt.</summary>
public enum Wirkung
{
    Keine,
    Werbungskosten,
    Altersvorsorge,
    KrankenPflegeBasis,
    SonstigeVorsorge,
    Spende,
    Kinderbetreuung,
    Schulgeld,
    Ausbildung,
    Unterhalt,
    Sonderausgabe,
    AussergewoehnlicheBelastung,
    Haushaltsnah,
    Handwerker,
    Minijob,
    BetriebEinnahme,
    BetriebAusgabe,
    MietEinnahme,
    MietAusgabe,
}

public sealed record Kategorie(string Key, string Name, Gruppe Gruppe, Wirkung Wirkung, string Formular, string Tipp, string[] Stichworte)
{
    public string Ordner => Path.Combine(Kategorien.GruppenOrdner(Gruppe), Kategorien.SichererName(Name));
    public bool Abschreibbar => Wirkung is Wirkung.Werbungskosten or Wirkung.BetriebAusgabe or Wirkung.MietAusgabe;
    public bool MitArbeitskosten => Wirkung is Wirkung.Handwerker or Wirkung.Haushaltsnah;
    public bool IstEinnahme => Wirkung is Wirkung.BetriebEinnahme or Wirkung.MietEinnahme || Gruppe == Gruppe.Einnahmen;
    public override string ToString() => Name;
}

public static class Kategorien
{
    public const string Standard = "sonstige-unterlagen";

    public static readonly IReadOnlyList<Kategorie> Alle =
    [
        // Einnahmen
        new("lohnsteuerbescheinigung", "Lohnsteuerbescheinigung", Gruppe.Einnahmen, Wirkung.Keine, "Anlage N",
            "Die Werte (Bruttolohn, Lohnsteuer, Sozialversicherung) trägst du unter „Steuer-Schätzung“ ein.", ["lohnsteuerbescheinigung", "ausdruck der elektronischen", "bruttoarbeitslohn", "eTIN"]),
        new("kapitalertraege", "Kapitalerträge / Steuerbescheinigung Bank", Gruppe.Einnahmen, Wirkung.Keine, "Anlage KAP",
            "Meist mit Abgeltungsteuer erledigt – trotzdem ablegen (Günstigerprüfung, Verluste).", ["kapitalertr", "steuerbescheinigung", "abgeltungsteuer", "depot", "dividende", "zinsen"]),
        new("lohnersatz", "Lohnersatz (ALG, Kranken-, Elterngeld)", Gruppe.Einnahmen, Wirkung.Keine, "Hauptvordruck",
            "Steuerfrei, aber Progressionsvorbehalt – Bescheinigung aufheben.", ["arbeitslosengeld", "krankengeld", "elterngeld", "kurzarbeit", "agentur für arbeit"]),
        new("rente", "Renten-Bescheinigung", Gruppe.Einnahmen, Wirkung.Keine, "Anlage R", "", ["rentenbezugsmitteilung", "renteninformation", "deutsche rentenversicherung"]),
        new("sonstige-einnahmen", "Sonstige Einnahmen", Gruppe.Einnahmen, Wirkung.Keine, "Anlage SO", "", []),

        // Werbungskosten (Anlage N)
        new("arbeitsmittel", "Arbeitsmittel (Laptop, Werkzeug, Möbel)", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Arbeitsmittel",
            "Über 952 € brutto wird abgeschrieben (Computer/Software: 1 Jahr). Privat mitgenutzt? Anteil einstellen.", ["laptop", "notebook", "computer", "monitor", "drucker", "werkzeug", "schreibtisch", "bürostuhl", "tastatur", "software", "media markt", "saturn", "conrad", "cyberport"]),
        new("fachliteratur", "Fachliteratur", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Arbeitsmittel", "", ["fachbuch", "fachzeitschrift", "buchhandlung", "thalia", "hugendubel"]),
        new("arbeitskleidung", "Arbeitskleidung", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Arbeitsmittel",
            "Nur typische Berufskleidung (Schutzkleidung, Uniform) – auch deren Reinigung.", ["arbeitskleidung", "sicherheitsschuh", "schutzkleidung", "engelbert strauss", "uniform", "berufskleidung"]),
        new("fortbildung", "Fortbildung / Weiterbildung", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Fortbildungskosten",
            "Kursgebühren, Prüfungen, Fahrten und Material – auch Meister/Techniker.", ["seminar", "lehrgang", "fortbildung", "weiterbildung", "kurs", "schulung", "prüfungsgebühr", "ihk", "handwerkskammer", "akademie"]),
        new("reisekosten", "Dienstreisen / Reisekosten", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Reisekosten",
            "Nur was der Arbeitgeber nicht steuerfrei erstattet hat.", ["hotel", "übernachtung", "deutsche bahn", "db fernverkehr", "flug", "taxi", "dienstreise", "parkhaus"]),
        new("fahrtkosten", "Fahrtkosten (ÖPNV-Ticket, Unfall)", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Wege zur Arbeit",
            "Die Pendlerpauschale wird automatisch aus Entfernung × Arbeitstage berechnet – hier nur Sonderfälle.", ["deutschlandticket", "monatskarte", "jobticket", "verkehrsverbund"]),
        new("arbeitszimmer", "Arbeitszimmer / Homeoffice", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Arbeitszimmer",
            "Homeoffice-Tage trägst du unter „Steuer-Schätzung“ ein (6 € pro Tag). Echtes Arbeitszimmer: Jahrespauschale 1.260 €.", ["arbeitszimmer", "homeoffice"]),
        new("telefon-internet", "Telefon & Internet", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Weitere Werbungskosten",
            "Ohne Einzelnachweis werden 20 % (max. 20 € im Monat) anerkannt – Anteil einstellen.", ["telekom", "vodafone", "o2", "telefonica", "1&1", "congstar", "mobilfunk", "internet", "dsl"]),
        new("bewerbung", "Bewerbungskosten", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Weitere Werbungskosten", "", ["bewerbung", "bewerbungsfoto", "vorstellungsgespräch"]),
        new("umzug-beruflich", "Beruflicher Umzug", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Weitere Werbungskosten", "", ["umzug", "spedition", "umzugsunternehmen"]),
        new("doppelte-haushaltsfuehrung", "Doppelte Haushaltsführung", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Doppelte Haushaltsführung",
            "Miete der Zweitwohnung bis 1.000 € im Monat, Heimfahrten.", ["zweitwohnung"]),
        new("gewerkschaft", "Gewerkschaft / Berufsverband", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Beiträge zu Berufsverbänden", "", ["gewerkschaft", "verdi", "ig metall", "berufsverband", "mitgliedsbeitrag"]),
        new("kontofuehrung", "Kontoführung", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Weitere Werbungskosten",
            "Pauschal 16 € im Jahr werden ohne Beleg anerkannt.", ["kontoführung", "kontoführungsgebühr"]),
        new("steuerberatung", "Steuerberatung / Steuer-Software", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Weitere Werbungskosten", "", ["steuerberater", "lohnsteuerhilfe", "steuersoftware", "wiso", "smartsteuer"]),
        new("sonstige-werbungskosten", "Sonstige Werbungskosten", Gruppe.Werbungskosten, Wirkung.Werbungskosten, "Anlage N – Weitere Werbungskosten", "", []),

        // Vorsorge
        new("basisrente", "Rürup / Basisrente / freiwillige Rente", Gruppe.Vorsorge, Wirkung.Altersvorsorge, "Anlage Vorsorgeaufwand",
            "Die Rentenbeiträge aus der Lohnsteuerbescheinigung werden automatisch berücksichtigt.", ["rürup", "basisrente", "freiwillige beiträge", "versorgungswerk"]),
        new("kranken-pflege", "Kranken- & Pflegeversicherung (privat/freiwillig)", Gruppe.Vorsorge, Wirkung.KrankenPflegeBasis, "Anlage Vorsorgeaufwand",
            "Gesetzliche Beiträge vom Lohn kommen aus der Lohnsteuerbescheinigung.", ["krankenversicherung", "pflegeversicherung", "aok", "techniker krankenkasse", "barmer", "dak", "ikk", "debeka", "allianz private"]),
        new("versicherungen", "Haftpflicht, Unfall, BU, Risiko-Leben", Gruppe.Vorsorge, Wirkung.SonstigeVorsorge, "Anlage Vorsorgeaufwand",
            "Wirkt nur, wenn Kranken-/Pflegeversicherung unter dem Höchstbetrag (1.900 €) bleiben.", ["haftpflicht", "unfallversicherung", "berufsunfähigkeit", "risikolebensversicherung", "kfz-haftpflicht", "huk", "versicherungsschein", "beitragsrechnung"]),
        new("riester", "Riester-Vertrag", Gruppe.Vorsorge, Wirkung.Keine, "Anlage AV",
            "Wird über Anlage AV berücksichtigt (Bescheinigung des Anbieters) – nicht in der Schätzung enthalten.", ["riester", "altersvorsorgevertrag", "zulagenantrag"]),

        // Sonderausgaben
        new("spenden", "Spenden & Mitgliedsbeiträge", Gruppe.Sonderausgaben, Wirkung.Spende, "Hauptvordruck – Sonderausgaben",
            "Bis 300 € reicht der Kontoauszug, darüber Zuwendungsbestätigung.", ["spende", "zuwendungsbestätigung", "spendenquittung", "unicef", "rotes kreuz", "caritas", "diakonie", "partei"]),
        new("kinderbetreuung", "Kinderbetreuung (Kita, Tagesmutter, Hort)", Gruppe.Sonderausgaben, Wirkung.Kinderbetreuung, "Anlage Kind",
            "Ab 2025: 80 % absetzbar, max. 4.800 € je Kind (bis 2024: 2/3, max. 4.000 €). Keine Verpflegung.", ["kita", "kindergarten", "tagesmutter", "hort", "betreuungsbeitrag", "kinderkrippe"]),
        new("schulgeld", "Schulgeld (Privatschule)", Gruppe.Sonderausgaben, Wirkung.Schulgeld, "Anlage Kind", "30 % absetzbar, max. 5.000 € je Kind.", ["schulgeld", "privatschule"]),
        new("erstausbildung", "Erstausbildung / Erststudium", Gruppe.Sonderausgaben, Wirkung.Ausbildung, "Hauptvordruck – Sonderausgaben", "Bis 6.000 € im Jahr.", ["semesterbeitrag", "studiengebühr", "universität", "hochschule"]),
        new("unterhalt-ex", "Unterhalt an Ex-Partner (Realsplitting)", Gruppe.Sonderausgaben, Wirkung.Unterhalt, "Anlage U", "Bis 13.805 € plus Kranken-/Pflegeversicherung, Zustimmung (Anlage U) nötig.", []),
        new("kirchensteuer-nachzahlung", "Kirchensteuer (nachgezahlt)", Gruppe.Sonderausgaben, Wirkung.Sonderausgabe, "Hauptvordruck – Sonderausgaben",
            "Kirchensteuer vom Lohn kommt aus der Lohnsteuerbescheinigung.", ["kirchensteuer", "kirchgeld"]),

        // Außergewöhnliche Belastungen
        new("krankheitskosten", "Krankheitskosten (Arzt, Apotheke, Brille)", Gruppe.AussergewoehnlicheBelastungen, Wirkung.AussergewoehnlicheBelastung, "Anlage Außergewöhnliche Belastungen",
            "Zuzahlungen, Brille, Zahnersatz, Fahrten zum Arzt – zählt erst über der zumutbaren Belastung.", ["apotheke", "zuzahlung", "brille", "fielmann", "apollo", "zahnarzt", "arzt", "physiotherapie", "krankengymnastik", "hörgerät", "rezept", "praxis", "klinik", "krankenhaus"]),
        new("pflege-behinderung", "Pflege & Behinderung", Gruppe.AussergewoehnlicheBelastungen, Wirkung.AussergewoehnlicheBelastung, "Anlage Außergewöhnliche Belastungen",
            "Behinderten-/Pflege-Pauschbeträge gibt es zusätzlich (Ausweis ablegen).", ["pflegeheim", "pflegedienst", "behinderung", "schwerbehindertenausweis", "rollstuhl"]),
        new("bestattung", "Bestattungskosten", Gruppe.AussergewoehnlicheBelastungen, Wirkung.AussergewoehnlicheBelastung, "Anlage Außergewöhnliche Belastungen", "Nur soweit sie den Nachlass übersteigen.", ["bestattung", "beerdigung", "friedhof", "grabstein"]),
        new("unterhalt-angehoerige", "Unterhalt an bedürftige Angehörige", Gruppe.AussergewoehnlicheBelastungen, Wirkung.Keine, "Anlage Unterhalt",
            "Eigene Berechnung über Anlage Unterhalt – nicht in der Schätzung enthalten.", []),
        new("sonstige-agb", "Sonstige außergewöhnliche Belastungen", Gruppe.AussergewoehnlicheBelastungen, Wirkung.AussergewoehnlicheBelastung, "Anlage Außergewöhnliche Belastungen", "z. B. Wiederbeschaffung nach Hochwasser/Brand.", []),

        // Haushaltsnahe Leistungen (§ 35a)
        new("handwerker", "Handwerkerleistungen", Gruppe.Haushaltsnah, Wirkung.Handwerker, "Hauptvordruck – Steuerermäßigungen",
            "20 % der Arbeits- und Fahrtkosten (nicht Material), max. 1.200 € Steuer. Nur per Überweisung bezahlt!", ["handwerker", "elektriker", "sanitär", "heizung", "maler", "dachdecker", "schornsteinfeger", "reparatur", "installation", "fliesen", "tischler", "schreiner", "montage"]),
        new("haushaltsnahe-dienstleistung", "Haushaltsnahe Dienste (Reinigung, Garten)", Gruppe.Haushaltsnah, Wirkung.Haushaltsnah, "Hauptvordruck – Steuerermäßigungen",
            "20 % der Arbeitskosten, max. 4.000 € Steuer. Auch aus der Nebenkostenabrechnung (Hausmeister, Treppenhaus).", ["reinigung", "gebäudereinigung", "gartenpflege", "hausmeister", "winterdienst", "treppenhaus", "fensterputz", "nebenkostenabrechnung", "betriebskosten"]),
        new("pflege-betreuung", "Pflege- & Betreuungsleistungen im Haushalt", Gruppe.Haushaltsnah, Wirkung.Haushaltsnah, "Hauptvordruck – Steuerermäßigungen", "", ["betreuungsleistung", "seniorenbetreuung", "alltagshilfe"]),
        new("minijob-haushalt", "Minijob im Haushalt (Haushaltsscheck)", Gruppe.Haushaltsnah, Wirkung.Minijob, "Hauptvordruck – Steuerermäßigungen", "20 %, max. 510 € Steuer.", ["haushaltsscheck", "minijob-zentrale"]),

        // Selbstständig / Nebenjob (EÜR)
        new("betriebseinnahmen", "Betriebseinnahmen (Rechnungen an Kunden)", Gruppe.Selbststaendig, Wirkung.BetriebEinnahme, "Anlage EÜR / G / S", "Einnahmen aus Nebengewerbe oder Selbstständigkeit.", ["rechnung nr", "ihre zahlung"]),
        new("betriebsausgaben", "Betriebsausgaben", Gruppe.Selbststaendig, Wirkung.BetriebAusgabe, "Anlage EÜR",
            "Über 800 € netto wird abgeschrieben – Nutzungsdauer angeben.", ["wareneinkauf", "büromaterial", "porto", "werbung", "domain", "hosting"]),
        new("betrieb-fahrzeug", "Fahrzeugkosten (betrieblich)", Gruppe.Selbststaendig, Wirkung.BetriebAusgabe, "Anlage EÜR", "", ["tankstelle", "aral", "shell", "esso", "werkstatt", "kfz-steuer", "tüv", "dekra"]),

        // Vermietung (Anlage V)
        new("mieteinnahmen", "Mieteinnahmen (Kaltmiete)", Gruppe.Vermietung, Wirkung.MietEinnahme, "Anlage V, Zeile 9", "Alle im Jahr zugeflossenen Mieten, auch Nachzahlungen. Bei Miete unter 50 % der ortsüblichen Miete sind Kosten nur anteilig absetzbar (unter 66 % Prognose nötig).", ["mieteingang", "kaltmiete", "mietzahlung"]),
        new("miet-umlagen", "Umlagen / Nebenkosten vom Mieter", Gruppe.Vermietung, Wirkung.MietEinnahme, "Anlage V, Zeile 13", "Nebenkostenvorauszahlungen und -nachzahlungen der Mieter sind Einnahmen; Erstattungen an Mieter mindern sie.", ["nebenkostenvorauszahlung", "betriebskostenvorauszahlung"]),
        new("miet-schuldzinsen", "Schuldzinsen & Finanzierung", Gruppe.Vermietung, Wirkung.MietAusgabe, "Anlage V, Zeile 37", "Nur die Zinsen (nicht die Tilgung) des Darlehens für das Mietobjekt, dazu Bereitstellungszinsen, Disagio, Grundbuch für die Grundschuld.", ["darlehenskonto", "zinsbescheinigung", "baufinanzierung", "annuitätendarlehen"]),
        new("miet-grundsteuer", "Grundsteuer & Abgaben", Gruppe.Vermietung, Wirkung.MietAusgabe, "Anlage V, Zeile 47", "Grundsteuer, Müll, Straßenreinigung, Abwasser – auch wenn auf den Mieter umgelegt.", ["grundsteuer", "abgabenbescheid", "grundbesitzabgaben", "straßenreinigung"]),
        new("miet-betriebskosten", "Betriebskosten, Versicherung & Verwaltung", Gruppe.Vermietung, Wirkung.MietAusgabe, "Anlage V, Zeilen 47–49", "Gebäudeversicherung, Hausgeld/WEG (ohne Rücklage), Hausverwaltung, Allgemeinstrom, Wasser, Hausmeister.", ["gebäudeversicherung", "wohngebäudeversicherung", "hausgeld", "hausverwaltung", "wohnungseigentümergemeinschaft", "weg-abrechnung"]),
        new("miet-erhaltung", "Reparaturen & Erhaltungsaufwand", Gruppe.Vermietung, Wirkung.MietAusgabe, "Anlage V, Zeile 40", "Sofort absetzbar. Achtung: Mehr als 15 % der Gebäudekosten (netto) in den ersten 3 Jahren nach dem Kauf → wird über die Gebäude-AfA verteilt. Große Beträge können auf 2–5 Jahre verteilt werden (§ 82b EStDV).", ["renovierung", "instandhaltung", "sanierung"]),
        new("miet-einrichtung", "Einrichtung der Mietwohnung (Küche, Möbel)", Gruppe.Vermietung, Wirkung.MietAusgabe, "Anlage V, Zeile 33", "Einbauküche 10 Jahre, Möbel 13 Jahre abschreiben; bis 952 € brutto sofort.", ["einbauküche"]),
        new("vermietung-ausgaben", "Sonstige Werbungskosten Vermietung", Gruppe.Vermietung, Wirkung.MietAusgabe, "Anlage V, Zeile 50", "Fahrten zum Objekt (0,30 €/km), Inserate, Kontoführung, Porto, Steuerberatung, Rechtsschutz, Mieterwechsel.", ["immobilienscout", "mietinserat"]),

        // Sonstiges
        new("bescheide", "Steuerbescheide & Finanzamt", Gruppe.Sonstiges, Wirkung.Keine, "–", "Bescheid prüfen, Einspruchsfrist 1 Monat.", ["finanzamt", "steuerbescheid", "bescheid für", "steuernummer", "elster"]),
        new("vertraege", "Verträge & Nachweise", Gruppe.Sonstiges, Wirkung.Keine, "–", "Arbeitsvertrag, Mietvertrag, Kindergeld, Heiratsurkunde …", ["vertrag", "familienkasse", "kindergeld"]),
        new(Standard, "Sonstige Unterlagen", Gruppe.Sonstiges, Wirkung.Keine, "–", "", []),
    ];

    private static readonly Dictionary<string, Kategorie> ByKey = Alle.ToDictionary(k => k.Key);

    public static Kategorie Get(string? key) => key != null && ByKey.TryGetValue(key, out var k) ? k : ByKey[Standard];

    public static IEnumerable<Kategorie> In(Gruppe g) => Alle.Where(k => k.Gruppe == g);

    public static string GruppenName(Gruppe g) => g switch
    {
        Gruppe.Einnahmen => "Einnahmen & Bescheinigungen",
        Gruppe.Werbungskosten => "Werbungskosten (Job)",
        Gruppe.Vorsorge => "Versicherungen & Vorsorge",
        Gruppe.Sonderausgaben => "Sonderausgaben",
        Gruppe.AussergewoehnlicheBelastungen => "Außergewöhnliche Belastungen",
        Gruppe.Haushaltsnah => "Haushaltsnahe Leistungen & Handwerker",
        Gruppe.Selbststaendig => "Selbstständig / Nebengewerbe",
        Gruppe.Vermietung => "Vermietung",
        _ => "Sonstiges",
    };

    public static string GruppenOrdner(Gruppe g) => g switch
    {
        Gruppe.Einnahmen => "01 Einnahmen",
        Gruppe.Werbungskosten => "02 Werbungskosten",
        Gruppe.Vorsorge => "03 Versicherungen",
        Gruppe.Sonderausgaben => "04 Sonderausgaben",
        Gruppe.AussergewoehnlicheBelastungen => "05 Außergewöhnliche Belastungen",
        Gruppe.Haushaltsnah => "06 Haushaltsnahe Leistungen",
        Gruppe.Selbststaendig => "07 Selbstständig",
        Gruppe.Vermietung => "08 Vermietung",
        _ => "09 Sonstiges",
    };

    public static string SichererName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*']).ToHashSet();
        var chars = name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '-' : c).ToArray();
        var s = new string(chars).Replace("&", "und").Trim().TrimEnd('.');
        while (s.Contains("  ")) s = s.Replace("  ", " ");
        return s.Length == 0 ? "Beleg" : s;
    }
}
