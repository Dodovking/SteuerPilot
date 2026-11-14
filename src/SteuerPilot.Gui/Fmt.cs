using System.Globalization;

namespace SteuerPilot.Gui;

public static class Fmt
{
    public static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public static string Euro(decimal v) => v.ToString("#,##0.00 €", De);
    public static string Euro0(decimal v) => v.ToString("#,##0 €", De);
    public static string Prozent(decimal v) => (v * 100).ToString("0.0", De) + " %";

    public static decimal? Zahl(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Replace("€", "").Replace("EUR", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").Trim();
        if (s.Contains(',') && s.Contains('.')) s = s.Replace(".", "");
        else if (s.Count(c => c == '.') == 1 && s.Length - s.IndexOf('.') - 1 is 1 or 2) s = s.Replace('.', ',');
        else s = s.Replace(".", "");
        return decimal.TryParse(s, NumberStyles.Number, De, out var v) ? v : null;
    }

    public static decimal Wert(InputBox b) => Zahl(b.Text) ?? 0m;

    public static string Eingabe(decimal v) => v == 0 ? "" : v.ToString("0.##", De);
}
