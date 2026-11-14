using System.Collections.Concurrent;
using SteuerPilot.Core;

namespace SteuerPilot.Gui;

/// <summary>Texterkennung je Datei nur einmal (Cache nach Pfad + Änderungszeit), immer nur eine OCR gleichzeitig.</summary>
public static class Belegerkennung
{
    public sealed record Ergebnis(string Text, Vorschlag Vorschlag, string? Fehler)
    {
        public bool Vollstaendig => Vorschlag.Datum != null && (Vorschlag.Betrag is > 0 || (Vorschlag.Art is "Kontoauszug" or "Vertrag" or "Versicherung" or "Wertpapierabrechnung" && Kategorien.Get(Vorschlag.Kategorie).Wirkung == Wirkung.Keine));
    }

    private static readonly ConcurrentDictionary<(string, DateTime), Task<Ergebnis>> Cache = new();
    private static readonly SemaphoreSlim Einzeln = new(1, 1);

    private static (string, DateTime) Key(string pfad)
    {
        var voll = Path.GetFullPath(pfad).ToLowerInvariant();
        return (voll, File.Exists(pfad) ? File.GetLastWriteTimeUtc(pfad) : DateTime.MinValue);
    }

    public static Ergebnis? Fertig(string pfad) =>
        Cache.TryGetValue(Key(pfad), out var t) && t.IsCompletedSuccessfully ? t.Result : null;

    public static Task<Ergebnis> Erkennen(string pfad) => Cache.GetOrAdd(Key(pfad), _ => Lesen(pfad));

    /// <summary>Werte aus Lohnsteuer- oder Rentenbescheinigung in „Meine Angaben“ übernehmen (nur leere Felder).</summary>
    public static string? AngabenUebernehmen(App app, Vorschlag v, Beleg b)
    {
        if (v.Angaben == null) return null;
        var jahr = app.Daten.Angaben(b.Jahr);
        var ziel = b.Person == Person.Partner ? jahr.Partner : jahr.Ich;
        int n = 0;
        foreach (var p in typeof(PersonAngaben).GetProperties())
        {
            var neu = p.GetValue(v.Angaben);
            if (neu is decimal d && d != 0 && (decimal)p.GetValue(ziel)! == 0) { p.SetValue(ziel, d); n++; }
            else if (neu is int i && i != 0 && p.Name == nameof(PersonAngaben.Rentenbeginn) && ziel.Rentenbeginn == 0) { p.SetValue(ziel, i); n++; }
        }
        if (v.Angaben.Rente > 0) app.Daten.Profil.HatRente = true;
        if (v.Angaben.Bruttolohn > 0) app.Daten.Profil.HatLohn = true;
        return n == 0 ? null : $"{n} Werte aus „{b.Kat.Name}“ in „Meine Angaben {b.Jahr}“ übernommen";
    }

    private static async Task<Ergebnis> Lesen(string pfad)
    {
        await Einzeln.WaitAsync().ConfigureAwait(false);
        try
        {
            var text = Path.GetExtension(pfad).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                ? await Task.Run(() => PdfText.Lesen(pfad)).ConfigureAwait(false) : "";
            if (!PdfText.HatText(text))
            {
                try { text = await Ocr.TextAus(pfad).ConfigureAwait(false); }
                catch when (text.Length > 0) { }
            }
            return new Ergebnis(text, Erkennung.Auswerten(text), null);
        }
        catch (Exception ex)
        {
            Cache.TryRemove(Key(pfad), out _);
            return new Ergebnis("", new Vorschlag(null, null, null, null), ex.Message);
        }
        finally
        {
            Einzeln.Release();
        }
    }
}
