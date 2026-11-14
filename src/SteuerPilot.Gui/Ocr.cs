using Windows.Data.Pdf;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SteuerPilot.Gui;

/// <summary>Texterkennung mit der in Windows 10/11 eingebauten OCR (offline, kostenlos).</summary>
public static class Ocr
{
    public static bool Verfuegbar
    {
        get
        {
            try { return Engine() != null; }
            catch { return false; }
        }
    }

    private static OcrEngine? Engine() =>
        OcrEngine.TryCreateFromLanguage(new Language("de-DE")) ?? OcrEngine.TryCreateFromUserProfileLanguages();

    /// <summary>Installiert die deutsche Texterkennung von Windows (fragt nach Administratorrechten).</summary>
    public static void DeutschInstallieren()
    {
        var cmd = "Add-WindowsCapability -Online -Name 'Language.OCR~~~de-DE~0.0.1.0'; Write-Host 'Fertig - SteuerPilot bitte neu starten.'; Start-Sleep 5";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powershell.exe", $"-NoProfile -Command \"{cmd}\"")
        {
            UseShellExecute = true,
            Verb = "runas",
        });
    }

    public static async Task<string> TextAus(string pfad, CancellationToken ct = default)
    {
        var engine = Engine() ?? throw new InvalidOperationException(
            "Unter Windows ist keine Texterkennung installiert. Einstellungen → Zeit und Sprache → Sprache → Deutsch → Optionen → \"Optische Zeichenerkennung\" hinzufügen.");
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(pfad));
        var texte = new List<string>();
        if (Path.GetExtension(pfad).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var pdf = await PdfDocument.LoadFromFileAsync(file);
            for (uint i = 0; i < Math.Min(pdf.PageCount, 5u); i++)
            {
                ct.ThrowIfCancellationRequested();
                using var page = pdf.GetPage(i);
                using var stream = new InMemoryRandomAccessStream();
                var opt = new PdfPageRenderOptions { DestinationWidth = (uint)Math.Min(2400, page.Size.Width * 3) };
                await page.RenderToStreamAsync(stream, opt);
                texte.Add(await Erkennen(engine, stream));
            }
        }
        else
        {
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            texte.Add(await Erkennen(engine, stream));
        }
        return string.Join("\n", texte);
    }

    private static async Task<string> Erkennen(OcrEngine engine, IRandomAccessStream stream)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        double scale = Math.Min(1.0, OcrEngine.MaxImageDimension / (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)(decoder.PixelWidth * scale),
            ScaledHeight = (uint)(decoder.PixelHeight * scale),
            InterpolationMode = BitmapInterpolationMode.Fant,
        };
        using var bmp = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
        var result = await engine.RecognizeAsync(bmp);
        return Zeilen(result);
    }

    /// <summary>Setzt Wörter mit gleicher Höhe zu einer Zeile zusammen. Die OCR liefert Spalten (z. B. „SUMME EUR“ und „19,48“) oft als getrennte Zeilen.</summary>
    private static string Zeilen(OcrResult result)
    {
        static double Mitte(OcrWord w) => w.BoundingRect.Y + w.BoundingRect.Height / 2;
        var zeilen = new List<List<OcrWord>>();
        foreach (var w in result.Lines.SelectMany(l => l.Words).OrderBy(Mitte))
        {
            var z = zeilen.LastOrDefault();
            if (z != null && Math.Abs(Mitte(w) - z.Average(Mitte)) < z.Average(x => x.BoundingRect.Height) * 0.6) z.Add(w);
            else zeilen.Add([w]);
        }
        return string.Join("\n", zeilen.Select(z => string.Join(" ", z.OrderBy(x => x.BoundingRect.X).Select(x => x.Text))));
    }

    /// <summary>Rendert die erste PDF-Seite als Bild für die Vorschau.</summary>
    public static async Task<Image?> PdfVorschau(string pfad, int breite)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(pfad));
            var pdf = await PdfDocument.LoadFromFileAsync(file);
            if (pdf.PageCount == 0) return null;
            using var page = pdf.GetPage(0);
            using var stream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = (uint)Math.Max(200, breite) });
            using var net = stream.AsStreamForRead();
            var ms = new MemoryStream();
            await net.CopyToAsync(ms);
            ms.Position = 0;
            return Image.FromStream(ms);
        }
        catch { return null; }
    }
}
