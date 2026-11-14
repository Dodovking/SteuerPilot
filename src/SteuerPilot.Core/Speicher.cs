using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteuerPilot.Core;

public static class Speicher
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string EinstellungsOrdner(string? overrideDir = null) =>
        overrideDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SteuerPilot");

    public static AppSettings LadeEinstellungen(string? dir = null) =>
        Lade<AppSettings>(Path.Combine(EinstellungsOrdner(dir), "einstellungen.json")) ?? new AppSettings();

    public static void SpeichereEinstellungen(AppSettings s, string? dir = null) =>
        Schreibe(Path.Combine(EinstellungsOrdner(dir), "einstellungen.json"), s);

    public static SteuerDaten LadeDaten(Ablage ablage)
    {
        var d = Lade<SteuerDaten>(ablage.DatenDatei);
        if (d == null && File.Exists(ablage.DatenDatei + ".bak")) d = Lade<SteuerDaten>(ablage.DatenDatei + ".bak");
        return d ?? new SteuerDaten();
    }

    public static void SpeichereDaten(Ablage ablage, SteuerDaten d) => Schreibe(ablage.DatenDatei, d, backup: true);

    private static T? Lade<T>(string file) where T : class
    {
        if (!File.Exists(file)) return null;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(file), Json); }
        catch (JsonException) { return null; }
    }

    private static void Schreibe<T>(string file, T value, bool backup = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
        if (File.Exists(file))
        {
            if (backup) File.Copy(file, file + ".bak", true);
            File.Move(tmp, file, true);
        }
        else File.Move(tmp, file);
    }
}
