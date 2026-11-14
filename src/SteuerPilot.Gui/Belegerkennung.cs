using System.Collections.Concurrent;
using SteuerPilot.Core;

namespace SteuerPilot.Gui;

/// <summary>Texterkennung je Datei nur einmal (Cache nach Pfad + Änderungszeit), immer nur eine OCR gleichzeitig.</summary>
public static class Belegerkennung
{
    public sealed record Ergebnis(string Text, Vorschlag Vorschlag, string? Fehler)
    {
        public bool Vollstaendig => Vorschlag.Betrag is > 0 && Vorschlag.Datum != null;
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

    private static async Task<Ergebnis> Lesen(string pfad)
    {
        await Einzeln.WaitAsync().ConfigureAwait(false);
        try
        {
            var text = await Ocr.TextAus(pfad).ConfigureAwait(false);
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
