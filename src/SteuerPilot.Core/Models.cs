using System.Text.Json.Serialization;

namespace SteuerPilot.Core;

public enum ThemeChoice
{
    System,
    Hell,
    Dunkel,
}

public enum Person
{
    Ich,
    Partner,
    Gemeinsam,
}

public enum Veranlagung
{
    Einzel,
    Zusammen,
}

public sealed class Beleg
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Datum { get; set; } = DateTime.Today;
    public decimal Betrag { get; set; }
    public string Beschreibung { get; set; } = "";
    public string Kategorie { get; set; } = Kategorien.Standard;
    public Person Person { get; set; } = Person.Ich;

    /// <summary>Beruflich/steuerlich genutzter Anteil in Prozent (z. B. Handy 50 %).</summary>
    public int Anteil { get; set; } = 100;

    /// <summary>Nutzungsdauer für die Abschreibung in Jahren; 0 = sofort im Kaufjahr absetzen.</summary>
    public int AfaJahre { get; set; }

    /// <summary>Nur Handwerker/haushaltsnahe Leistungen: in der Rechnung ausgewiesene Arbeits- und Fahrtkosten.</summary>
    public decimal? Arbeitskosten { get; set; }

    /// <summary>Dateien relativ zum Ablageordner.</summary>
    public List<string> Dateien { get; set; } = [];

    /// <summary>Nur Vermietung: Name des Mietobjekts (siehe Profil.Mietobjekte).</summary>
    public string Objekt { get; set; } = "";

    /// <summary>Nur Steuerbescheinigungen der Bank (Anlage KAP).</summary>
    public Kapitalwerte? Kapital { get; set; }

    public string Notiz { get; set; } = "";
    public DateTime Erstellt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public int Jahr => Datum.Year;

    [JsonIgnore]
    public Kategorie Kat => Kategorien.Get(Kategorie);

    public Beleg Kopie()
    {
        var b = (Beleg)MemberwiseClone();
        b.Dateien = [.. Dateien];
        if (Kapital != null) b.Kapital = Kapital with { };
        return b;
    }
}

/// <summary>Werte einer Jahressteuer- oder Verlustbescheinigung der Bank.</summary>
public sealed record Kapitalwerte
{
    public decimal Ertraege { get; set; }
    public decimal AktienGewinne { get; set; }
    public decimal VerlustAktien { get; set; }
    public decimal VerlustSonstige { get; set; }
    public decimal Kapest { get; set; }
    public decimal Soli { get; set; }
    public decimal Kist { get; set; }
    public decimal Pauschbetrag { get; set; }

    [JsonIgnore]
    public decimal Einbehalten => Kapest + Soli + Kist;

    [JsonIgnore]
    public bool Leer => Ertraege == 0 && VerlustAktien == 0 && VerlustSonstige == 0 && Einbehalten == 0;
}

public sealed class PersonAngaben
{
    /// <summary>Jahresbetrag der gesetzlichen/privaten Rente (Rentenbezugsmitteilung).</summary>
    public decimal Rente { get; set; }
    public int Rentenbeginn { get; set; }
    public decimal RentnerKv { get; set; }
    public decimal RentnerPv { get; set; }

    public decimal Bruttolohn { get; set; }
    public decimal Lohnsteuer { get; set; }
    public decimal Soli { get; set; }
    public decimal Kirchensteuer { get; set; }
    public decimal RentenversicherungAN { get; set; }
    public decimal KrankenversicherungAN { get; set; }
    public decimal PflegeversicherungAN { get; set; }
    public decimal ArbeitslosenversicherungAN { get; set; }
    public bool KrankengeldAnspruch { get; set; } = true;
    public int EntfernungKm { get; set; }
    public int Arbeitstage { get; set; }
    public bool MitEigenemAuto { get; set; } = true;
    public int HomeofficeTage { get; set; }
}

public sealed class Jahresangaben
{
    public PersonAngaben Ich { get; set; } = new();
    public PersonAngaben Partner { get; set; } = new();
    public decimal Vorauszahlungen { get; set; }

    /// <summary>Bereiche, die als erledigt markiert wurden (für den Fortschritt).</summary>
    public List<string> Erledigt { get; set; } = [];
}

/// <summary>Vermietete Wohnung/Haus (Anlage V).</summary>
public sealed class Mietobjekt
{
    public string Name { get; set; } = "";
    public string Adresse { get; set; } = "";
    /// <summary>Anschaffungs-/Herstellungskosten nur des Gebäudes (ohne Grund und Boden), inkl. Nebenkosten.</summary>
    public decimal Gebaeudekosten { get; set; }
    public DateTime Anschaffung { get; set; } = new(DateTime.Today.Year, 1, 1);
    public int Baujahr { get; set; } = 1990;
    /// <summary>AfA-Satz in Prozent; 0 = automatisch nach Baujahr.</summary>
    public decimal AfaSatz { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public decimal Satz => AfaSatz > 0 ? AfaSatz : Steuer.GebaeudeAfaSatz(Baujahr);
}

/// <summary>Konto oder Depot (mit Freistellungsauftrag und ggf. Sparplan).</summary>
public sealed class Geldanlage
{
    public string Name { get; set; } = "";
    public decimal Freistellungsauftrag { get; set; }
    public decimal SparplanMonat { get; set; }
}

public sealed class Profil
{
    public string Name { get; set; } = "";
    public string PartnerName { get; set; } = "";
    public Veranlagung Veranlagung { get; set; } = Veranlagung.Einzel;
    public bool Kirchensteuer { get; set; }
    public string Bundesland { get; set; } = "Nordrhein-Westfalen";
    public int Kinder { get; set; }
    public bool Alleinerziehend { get; set; }
    public List<Mietobjekt> Mietobjekte { get; set; } = [];
    public List<Geldanlage> Geldanlagen { get; set; } = [];

    public bool HatLohn { get; set; } = true;
    public bool HatRente { get; set; }
    public bool HatVermietung { get; set; }
    public bool HatKapital { get; set; }
    public bool HatSelbststaendig { get; set; }

    [JsonIgnore]
    public decimal SparerPauschbetrag => Splitting ? 2000m : 1000m;

    [JsonIgnore]
    public bool Splitting => Veranlagung == Veranlagung.Zusammen;

    [JsonIgnore]
    public decimal KirchensteuerSatz => Bundesland is "Bayern" or "Baden-Württemberg" ? 8m : 9m;

    public string PersonName(Person p) => p switch
    {
        Person.Ich => string.IsNullOrWhiteSpace(Name) ? "Ich" : Name.Trim(),
        Person.Partner => string.IsNullOrWhiteSpace(PartnerName) ? "Partner/in" : PartnerName.Trim(),
        _ => "Gemeinsam",
    };

    public static readonly string[] Bundeslaender =
    [
        "Baden-Württemberg", "Bayern", "Berlin", "Brandenburg", "Bremen", "Hamburg", "Hessen", "Mecklenburg-Vorpommern",
        "Niedersachsen", "Nordrhein-Westfalen", "Rheinland-Pfalz", "Saarland", "Sachsen", "Sachsen-Anhalt", "Schleswig-Holstein", "Thüringen",
    ];
}

public sealed class SteuerDaten
{
    public int Version { get; set; } = 1;
    public Profil Profil { get; set; } = new();
    public List<Beleg> Belege { get; set; } = [];
    public Dictionary<int, Jahresangaben> Jahre { get; set; } = [];

    public Jahresangaben Angaben(int jahr)
    {
        if (!Jahre.TryGetValue(jahr, out var a)) Jahre[jahr] = a = new Jahresangaben();
        return a;
    }

    public IEnumerable<Beleg> BelegeIm(int jahr) => Belege.Where(b => b.Jahr == jahr);
}

public sealed class AppSettings
{
    public string? Ablageordner { get; set; }
    public ThemeChoice Theme { get; set; } = ThemeChoice.System;
    public int Jahr { get; set; }
    public bool UpdatesPruefen { get; set; } = true;
    public bool DesignV2 { get; set; }
    /// <summary>Dateien aus eigenen Ordnern verschieben statt kopieren (Eingang wird immer verschoben).</summary>
    public bool OriginaleVerschieben { get; set; }
}
