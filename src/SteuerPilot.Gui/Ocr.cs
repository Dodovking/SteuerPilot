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
                var opt = new PdfPageRenderOptions { DestinationWidth = (uint)Math.Clamp(page.Size.Width * 5, 2000, 4000) };
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

    private sealed record Wort(string Text, double X, double Y, double W, double H)
    {
        public double Mitte => Y + H / 2;
    }

    /// <summary>Kleine Bilder werden vergrößert, große in Kacheln gelesen statt verkleinert – so bleibt kleine Bon-Schrift lesbar.</summary>
    private static async Task<string> Erkennen(OcrEngine engine, IRandomAccessStream stream)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        double max = OcrEngine.MaxImageDimension;
        double groesse = Math.Max(decoder.PixelWidth, decoder.PixelHeight);
        double scale = groesse < 1600 ? 2.0 : groesse > max * 2 ? max * 2 / groesse : 1.0;
        uint w = (uint)(decoder.PixelWidth * scale), h = (uint)(decoder.PixelHeight * scale);
        int kachel = (int)max - 64;
        int nx = (int)Math.Ceiling(w / (double)kachel), ny = (int)Math.Ceiling(h / (double)kachel);
        uint tw = (uint)Math.Ceiling(w / (double)nx), th = (uint)Math.Ceiling(h / (double)ny);
        const uint rand = 48;
        var woerter = new List<Wort>();
        for (int iy = 0; iy < ny; iy++)
        for (int ix = 0; ix < nx; ix++)
        {
            uint x0 = (uint)Math.Max(0, ix * tw - (ix > 0 ? rand : 0)), y0 = (uint)Math.Max(0, iy * th - (iy > 0 ? rand : 0));
            uint x1 = Math.Min(w, (uint)(ix + 1) * tw), y1 = Math.Min(h, (uint)(iy + 1) * th);
            var transform = new BitmapTransform
            {
                ScaledWidth = w,
                ScaledHeight = h,
                InterpolationMode = BitmapInterpolationMode.Fant,
            };
            if (nx * ny > 1) transform.Bounds = new BitmapBounds { X = x0, Y = y0, Width = x1 - x0, Height = y1 - y0 };
            using var bmp = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
                nx * ny > 1 ? ExifOrientationMode.IgnoreExifOrientation : ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
            var result = await engine.RecognizeAsync(bmp);
            foreach (var wd in result.Lines.SelectMany(l => l.Words))
            {
                var r = wd.BoundingRect;
                var neu = new Wort(wd.Text, r.X + (nx * ny > 1 ? x0 : 0), r.Y + (nx * ny > 1 ? y0 : 0), r.Width, r.Height);
                // Überlappungsrand: doppelt gelesene Wörter nur einmal nehmen.
                if (!woerter.Any(o => o.Text == neu.Text && Math.Abs(o.X - neu.X) < neu.H && Math.Abs(o.Y - neu.Y) < neu.H)) woerter.Add(neu);
            }
        }
        return Zeilen(woerter);
    }

    /// <summary>Setzt Wörter mit gleicher Höhe zu einer Zeile zusammen. Die OCR liefert Spalten (z. B. „SUMME EUR“ und „19,48“) oft als getrennte Zeilen.</summary>
    private static string Zeilen(List<Wort> woerter)
    {
        var zeilen = new List<List<Wort>>();
        foreach (var w in woerter.OrderBy(w => w.Mitte))
        {
            var z = zeilen.LastOrDefault();
            if (z != null && Math.Abs(w.Mitte - z.Average(x => x.Mitte)) < z.Average(x => x.H) * 0.6) z.Add(w);
            else zeilen.Add([w]);
        }
        return string.Join("\n", zeilen.Select(z => string.Join(" ", z.OrderBy(x => x.X).Select(x => x.Text))));
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
