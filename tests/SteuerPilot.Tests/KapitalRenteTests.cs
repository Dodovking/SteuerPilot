using SteuerPilot.Core;

namespace SteuerPilot.Tests;

public class KapitalRenteTests
{
    private static readonly DateTime Heute = new(2026, 3, 1);

    private const string Bankbescheinigung = """
        Musterbank AG
        Jahressteuerbescheinigung für das Jahr 2025
        für Privatkonten und/oder -depots
        Höhe der Kapitalerträge 1.850,00
        davon: Gewinn aus Aktienveräußerungen 300,00
        Höhe des in Anspruch genommenen Sparer-Pauschbetrages 1.000,00
        Kapitalertragsteuer 212,50
        Solidaritätszuschlag 11,68
        Kirchensteuer zur Kapitalertragsteuer 0,00
        Datum 14.02.2026
        """;

    [Fact]
    public void ErkenntJahressteuerbescheinigung()
    {
        var v = Erkennung.Auswerten(Bankbescheinigung, Heute);
        Assert.Equal("kapitalertraege", v.Kategorie);
        Assert.Equal(new DateTime(2025, 12, 31), v.Datum);
        Assert.Equal(1850m, v.Betrag);
        Assert.NotNull(v.Kapital);
        Assert.Equal(300m, v.Kapital!.AktienGewinne);
        Assert.Equal(212.50m, v.Kapital.Kapest);
        Assert.Equal(11.68m, v.Kapital.Soli);
        Assert.Equal(1000m, v.Kapital.Pauschbetrag);
    }

    [Fact]
    public void ErkenntRentenbezugsmitteilung()
    {
        const string text = """
            Deutsche Rentenversicherung Bund
            Information über die Meldung an die Finanzverwaltung für das Jahr 2025
            Rentenbetrag 2025 18.240,00 EUR
            Beginn der Rente 01.07.2019
            Beiträge zur Krankenversicherung 1.495,68 EUR
            Beiträge zur Pflegeversicherung 620,16 EUR
            """;
        var v = Erkennung.Auswerten(text, Heute);
        Assert.Equal("rente", v.Kategorie);
        Assert.Equal(new DateTime(2025, 12, 31), v.Datum);
        Assert.Equal(18240m, v.Angaben!.Rente);
        Assert.Equal(2019, v.Angaben.Rentenbeginn);
        Assert.Equal(1495.68m, v.Angaben.RentnerKv);
        Assert.Equal(620.16m, v.Angaben.RentnerPv);
    }

    [Fact]
    public void ErkenntLohnsteuerbescheinigung()
    {
        const string text = """
            Ausdruck der elektronischen Lohnsteuerbescheinigung für 2025
            3. Bruttoarbeitslohn einschl. Sachbezüge ohne 9. und 10. 42.000,00
            4. Einbehaltene Lohnsteuer von 3. 6.120,00
            5. Einbehaltener Solidaritätszuschlag von 3. 0,00
            23. a) Arbeitnehmeranteil zur gesetzlichen Rentenversicherung 3.906,00
            25. Arbeitnehmerbeiträge zur gesetzlichen Krankenversicherung 3.423,00
            """;
        var v = Erkennung.Auswerten(text, Heute);
        Assert.Equal("lohnsteuerbescheinigung", v.Kategorie);
        Assert.Equal(2025, v.Datum!.Value.Year);
        Assert.Equal(42000m, v.Angaben!.Bruttolohn);
        Assert.Equal(6120m, v.Angaben.Lohnsteuer);
        Assert.Equal(3906m, v.Angaben.RentenversicherungAN);
    }

    [Fact]
    public void AugustZeileWirdNichtUebersprungen() =>
        Assert.Equal(89.90m, Erkennung.Betrag("Rechnung August 2025\nGesamtbetrag 89,90"));

    [Theory]
    [InlineData(2005, 50)]
    [InlineData(2015, 70)]
    [InlineData(2020, 80)]
    [InlineData(2022, 82)]
    [InlineData(2025, 83.5)]
    public void Besteuerungsanteil(int beginn, decimal anteil) => Assert.Equal(anteil, Steuer.Besteuerungsanteil(beginn));

    [Fact]
    public void RenteUndKapitalInSchaetzung()
    {
        var d = new SteuerDaten();
        var a = d.Angaben(2025);
        a.Ich.Rente = 18000;
        a.Ich.Rentenbeginn = 2020;
        a.Ich.RentnerKv = 1500;
        a.Ich.RentnerPv = 600;
        d.Belege.Add(new Beleg
        {
            Datum = new DateTime(2025, 12, 31), Kategorie = "kapitalertraege", Betrag = 1500,
            Kapital = new Kapitalwerte { Ertraege = 1500, Kapest = 125, Soli = 6.87m },
        });
        var s = Auswertung.Schaetzen(d, 2025);
        Assert.Contains(s.Zeilen, z => z.Text.StartsWith("Rente") && z.Betrag == 18000 * 0.8m - 102);
        Assert.Contains(s.Zeilen, z => z.Text.Contains("Sparer-Pauschbetrag") && z.Betrag == -1000);
        Assert.True(s.Kapitalsteuer <= 131.88m);
        Assert.Contains(s.Hinweise, h => h.Contains("zu viel einbehalten"));
    }
}

public class ArtTests
{
    [Fact]
    public void ErkenntSteuerbescheid()
    {
        const string text = """
            Finanzamt Musterstadt
            Bescheid für 2024 über Einkommensteuer
            Festgesetzt werden 4.120,00
            Abrechnung: Erstattung 612,40 €
            Datum 12.05.2025
            """;
        var v = Erkennung.Auswerten(text, new DateTime(2025, 6, 1));
        Assert.Equal("Steuerbescheid", v.Art);
        Assert.Equal("bescheide", v.Kategorie);
        Assert.Equal(612.40m, v.Betrag);
        Assert.Equal(new DateTime(2024, 12, 31), v.Datum);
    }

    [Fact]
    public void ErkenntRechnung() =>
        Assert.Equal("Rechnung", Erkennung.Art("Maler Müller\nRechnung Nr. 123\nRechnungsbetrag 450,00\nZahlbar bis 30.06.2025"));
}
