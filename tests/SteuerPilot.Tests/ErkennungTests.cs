using SteuerPilot.Core;

namespace SteuerPilot.Tests;

public class ErkennungTests
{
    private const string Kassenbon = """
        Apotheke am Markt
        Hauptstr. 5, 12345 Musterstadt
        Ibuprofen 400        5,99
        Nasenspray           3,49
        Zuzahlung Rezept    10,00
        SUMME EUR           19,48
        MwSt 19%  1,23
        Gegeben Bar         20,00
        Rückgeld             0,52
        14.03.2025 10:22
        """;

    [Fact]
    public void ErkenntSumme() => Assert.Equal(19.48m, Erkennung.Betrag(Kassenbon));

    [Fact]
    public void ErkenntDatum() => Assert.Equal(new DateTime(2025, 3, 14), Erkennung.Datum(Kassenbon, new DateTime(2025, 6, 1)));

    [Fact]
    public void ErkenntKategorie() => Assert.Equal("krankheitskosten", Erkennung.Kategorie(Kassenbon));

    [Fact]
    public void ErkenntBeschreibung() => Assert.Equal("Apotheke am Markt", Erkennung.Beschreibung(Kassenbon));

    [Fact]
    public void TausenderPunkt() => Assert.Equal(1234.56m, Erkennung.Betrag("Rechnungsbetrag: 1.234,56 €"));

    [Fact]
    public void DatumMitMonatsname() => Assert.Equal(new DateTime(2024, 5, 3), Erkennung.Datum("Rechnung vom 3. Mai 2024", new DateTime(2025, 1, 1)));

    [Fact]
    public void ZukunftsdatumWirdIgnoriert() => Assert.Null(Erkennung.Datum("Fällig am 01.01.2030", new DateTime(2025, 1, 1)));
}
