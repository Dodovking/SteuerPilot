using SteuerPilot.Core;

namespace SteuerPilot.Tests;

public class SteuerTests
{
    [Theory]
    [InlineData(2024, 50000, 10872)]
    [InlineData(2025, 50000, 10691)]
    [InlineData(2025, 12096, 0)]
    [InlineData(2026, 12348, 0)]
    public void Grundtarif(int jahr, int zve, int erwartet) =>
        Assert.Equal(erwartet, Steuer.Einkommensteuer(zve, jahr, false));

    [Theory]
    [InlineData(2023)]
    [InlineData(2024)]
    [InlineData(2025)]
    [InlineData(2026)]
    public void TarifIstStetigUndSteigend(int jahr)
    {
        decimal vorher = 0;
        for (int x = 0; x <= 320000; x += 1)
        {
            var st = Steuer.Einkommensteuer(x, jahr, false);
            Assert.True(st >= vorher, $"{jahr}: Steuer sinkt bei {x}");
            Assert.True(st - vorher <= 2, $"{jahr}: Sprung bei {x}: {vorher} -> {st}");
            vorher = st;
        }
    }

    [Fact]
    public void SplittingIstDoppelterHalberTarif() =>
        Assert.Equal(2 * Steuer.Einkommensteuer(40000, 2025, false), Steuer.Einkommensteuer(80000, 2025, true));

    [Fact]
    public void SoliErstUeberFreigrenze()
    {
        Assert.Equal(0, Steuer.Soli(19950, 2025, false));
        Assert.True(Steuer.Soli(25000, 2025, false) > 0);
        Assert.Equal(Steuer.Cent(60000 * 0.055m), Steuer.Soli(60000, 2025, false));
    }

    [Fact]
    public void Pendlerpauschale()
    {
        Assert.Equal(2156m, Steuer.Pendlerpauschale(30, 220, 2025));
        Assert.Equal(30 * 0.38m * 220, Steuer.Pendlerpauschale(30, 220, 2026));
        Assert.Equal(4500m, Steuer.Pendlerpauschale(100, 220, 2025, mitAuto: false));
    }

    [Fact]
    public void Homeoffice() => Assert.Equal(1260m, Steuer.Homeoffice(300));

    [Fact]
    public void ZumutbareBelastungStufenweise() => Assert.Equal(2846m, Steuer.ZumutbareBelastung(50000, false, 0));

    [Fact]
    public void AbschreibungErgibtGenauDieKosten()
    {
        var plan = Steuer.Abschreibungsplan(1999.99m, new DateTime(2024, 9, 15), 3).ToList();
        Assert.Equal(1999.99m, plan.Sum(p => p.Betrag));
        Assert.Equal(2024, plan[0].Jahr);
        Assert.Equal(Steuer.Cent(1999.99m * 4 / 36), plan[0].Betrag);
        Assert.Equal(2027, plan[^1].Jahr);
    }

    [Fact]
    public void ComputerSofortImKaufjahr()
    {
        Assert.Equal(1500m, Steuer.Abschreibung(1500, new DateTime(2025, 11, 1), 1, 2025));
        Assert.Equal(0m, Steuer.Abschreibung(1500, new DateTime(2025, 11, 1), 1, 2026));
    }

    [Fact]
    public void Haushaltsnah() => Assert.Equal(4000m + 1200m, Steuer.HaushaltsnaheErmaessigung(50000, 10000, 0));

    [Fact]
    public void Kinderbetreuung()
    {
        Assert.Equal(4000m, Steuer.Kinderbetreuung(6000, 1, 2024));
        Assert.Equal(4800m, Steuer.Kinderbetreuung(6000, 1, 2025));
    }
}

public class AuswertungTests
{
    private static SteuerDaten Beispiel()
    {
        var d = new SteuerDaten();
        var a = d.Angaben(2025).Ich;
        a.Bruttolohn = 45000; a.Lohnsteuer = 6500; a.RentenversicherungAN = 4185; a.KrankenversicherungAN = 3667; a.PflegeversicherungAN = 810; a.ArbeitslosenversicherungAN = 585;
        a.EntfernungKm = 25; a.Arbeitstage = 200; a.HomeofficeTage = 20;
        d.Belege.Add(new Beleg { Datum = new DateTime(2025, 3, 1), Betrag = 1200, Kategorie = "arbeitsmittel", AfaJahre = 1, Beschreibung = "Laptop" });
        d.Belege.Add(new Beleg { Datum = new DateTime(2025, 5, 1), Betrag = 800, Kategorie = "handwerker", Arbeitskosten = 500 });
        d.Belege.Add(new Beleg { Datum = new DateTime(2024, 7, 1), Betrag = 3600, Kategorie = "arbeitsmittel", AfaJahre = 3, Beschreibung = "E-Bike" });
        return d;
    }

    [Fact]
    public void PostenEnthaltenAbschreibungAusVorjahren()
    {
        var posten = Auswertung.Posten(Beispiel(), 2025);
        var bike = Assert.Single(posten, p => p.Beleg.Beschreibung == "E-Bike");
        Assert.True(bike.AusVorjahr);
        Assert.Equal(1200m, bike.Ansatz);
    }

    [Fact]
    public void SchaetzungIstPlausibel()
    {
        var s = Auswertung.Schaetzen(Beispiel(), 2025);
        // Pendler 25 km: (20*0.30 + 5*0.38) * 200 = 1580, HO 120, Laptop 1200, E-Bike 1200
        Assert.Equal(4100m, s.Werbungskosten);
        Assert.True(s.ZuVersteuern is > 30000 and < 40000, s.ZuVersteuern.ToString());
        Assert.True(s.Ergebnis > 0, "Erstattung erwartet");
        Assert.Contains(s.Zeilen, z => z.Text.Contains("35a") && z.Betrag == -100m);
    }

    [Fact]
    public void ZusammenveranlagungNutztSplitting()
    {
        var d = Beispiel();
        d.Profil.Veranlagung = Veranlagung.Zusammen;
        var s = Auswertung.Schaetzen(d, 2025);
        Assert.Contains(s.Zeilen, z => z.Text.Contains("Splitting"));
        Assert.True(s.Einkommensteuer < Auswertung.Schaetzen(Beispiel(), 2025).Einkommensteuer);
    }
}
