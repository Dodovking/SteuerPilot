using SteuerPilot.Core;

namespace SteuerPilot.Tests;

public sealed class AblageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "steuerpilot-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void EingangsdateiWirdEinsortiertUndUmbenannt()
    {
        var a = new Ablage(_dir);
        a.Einrichten();
        var scan = a.NeueScanDatei();
        File.WriteAllText(scan, "x");
        Assert.Single(a.EingangDateien());

        var b = new Beleg { Datum = new DateTime(2025, 2, 3), Betrag = 49.9m, Beschreibung = "Fachbuch C#", Kategorie = "fachliteratur" };
        a.Hinzufuegen(b, [scan], verschieben: false);
        Assert.Empty(a.EingangDateien());
        var datei = a.Absolut(Assert.Single(b.Dateien));
        Assert.True(File.Exists(datei));
        Assert.Contains(Path.Combine("2025", "02 Werbungskosten", "Fachliteratur"), datei);
        Assert.EndsWith("2025-02-03_Fachbuch C#_49,90EUR.jpg", datei);

        b.Kategorie = "fortbildung";
        b.Datum = new DateTime(2024, 12, 30);
        a.Umbenennen(b);
        var neu = a.Absolut(Assert.Single(b.Dateien));
        Assert.True(File.Exists(neu));
        Assert.False(File.Exists(datei));
        Assert.Contains(Path.Combine("2024", "02 Werbungskosten"), neu);
        Assert.False(Directory.Exists(Path.Combine(_dir, "2025")));
    }

    [Fact]
    public void ExterneDateiWirdKopiert()
    {
        var a = new Ablage(Path.Combine(_dir, "ablage"));
        a.Einrichten();
        Directory.CreateDirectory(_dir);
        var ext = Path.Combine(_dir, "foto.png");
        File.WriteAllText(ext, "x");
        var b = new Beleg { Betrag = 10, Kategorie = "spenden" };
        a.Hinzufuegen(b, [ext, ext], verschieben: false);
        Assert.True(File.Exists(ext));
        Assert.Equal(2, b.Dateien.Count);
        Assert.All(b.Dateien, r => Assert.Contains("_Seite", r));
        a.Loeschen(b);
        Assert.Equal(2, Directory.GetFiles(a.Papierkorb, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public void DatenSpeichernUndLaden()
    {
        var a = new Ablage(_dir);
        a.Einrichten();
        var d = new SteuerDaten();
        d.Profil.Name = "Dominik";
        d.Belege.Add(new Beleg { Betrag = 12.34m, Kategorie = "spenden" });
        d.Angaben(2025).Ich.Bruttolohn = 40000;
        Speicher.SpeichereDaten(a, d);
        Speicher.SpeichereDaten(a, d);
        var l = Speicher.LadeDaten(a);
        Assert.Equal("Dominik", l.Profil.Name);
        Assert.Equal(12.34m, l.Belege[0].Betrag);
        Assert.Equal(40000, l.Angaben(2025).Ich.Bruttolohn);
        var zip = Path.Combine(_dir, "paket.zip");
        Export.Steuerpaket(l, a, DateTime.Today.Year, zip);
        Assert.True(new FileInfo(zip).Length > 0);
    }
}
