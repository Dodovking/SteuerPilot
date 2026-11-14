using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SteuerPilot.Core;

/// <summary>Liest den eingebetteten Text digitaler PDFs (Bank, Rentenversicherung) – genauer als Texterkennung.</summary>
public static class PdfText
{
    public static string Lesen(string pfad, int maxSeiten = 5)
    {
        try
        {
            using var doc = PdfDocument.Open(pfad);
            var seiten = doc.GetPages().Take(maxSeiten).Select(Zeilen);
            return string.Join("\n", seiten);
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Wörter mit gleicher Grundlinie zu Zeilen zusammensetzen (Beschriftung links, Betrag rechts).</summary>
    private static string Zeilen(Page page)
    {
        var zeilen = new List<List<Word>>();
        foreach (var w in page.GetWords().OrderByDescending(w => w.BoundingBox.Bottom))
        {
            var z = zeilen.LastOrDefault();
            var h = Math.Max(1, w.BoundingBox.Height);
            if (z != null && Math.Abs(z[0].BoundingBox.Bottom - w.BoundingBox.Bottom) < h * 0.5) z.Add(w);
            else zeilen.Add([w]);
        }
        return string.Join("\n", zeilen.Select(z => string.Join(" ", z.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text))));
    }

    public static bool HatText(string text) => text.Count(char.IsLetter) >= 40;
}
