using SteuerPilot.Core;

namespace SteuerPilot.Gui;

/// <summary>Zeigt ein Bild oder die erste PDF-Seite an, ohne die Datei zu sperren.</summary>
public sealed class Vorschau : Panel
{
    private readonly PictureBox _pic = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly Label _info = Make.Label("Keine Datei ausgewählt", Ui.Base, muted: true);
    private string? _pfad;
    private int _version;

    public Vorschau()
    {
        Controls.Add(_pic);
        Controls.Add(_info);
        _info.Dock = DockStyle.Fill;
        _info.AutoSize = false;
        _info.TextAlign = ContentAlignment.MiddleCenter;
        _pic.Cursor = Cursors.Hand;
        _pic.DoubleClick += (_, _) => Oeffnen();
        _info.DoubleClick += (_, _) => Oeffnen();
    }

    public string? Pfad => _pfad;

    public void Oeffnen()
    {
        if (_pfad == null || !File.Exists(_pfad)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_pfad) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "SteuerPilot", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    public async void Zeigen(string? pfad)
    {
        int v = ++_version;
        _pfad = pfad;
        var alt = _pic.Image;
        _pic.Image = null;
        alt?.Dispose();
        _pic.Visible = false;
        _info.Visible = true;
        if (pfad == null) { _info.Text = "Keine Datei ausgewählt"; return; }
        if (!File.Exists(pfad)) { _info.Text = "Datei fehlt:\n" + Path.GetFileName(pfad); return; }
        _info.Text = "Lade Vorschau …";
        Image? img = null;
        try
        {
            if (Ablage.IstBild(pfad))
                img = await Task.Run(() => Laden(pfad));
            else if (pfad.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                img = await Ocr.PdfVorschau(pfad, Math.Max(Width, 600) * 2);
        }
        catch { img = null; }
        if (v != _version || IsDisposed) { img?.Dispose(); return; }
        if (img == null)
        {
            _info.Text = "Keine Vorschau für " + Path.GetExtension(pfad).ToUpperInvariant().TrimStart('.') + "\nDoppelklick öffnet die Datei.";
            return;
        }
        _pic.Image = img;
        _pic.Visible = true;
        _info.Visible = false;
    }

    private static Image Laden(string pfad)
    {
        var bytes = File.ReadAllBytes(pfad);
        using var ms = new MemoryStream(bytes);
        using var src = Image.FromStream(ms);
        const int orientationId = 0x0112;
        var bmp = new Bitmap(src);
        if (src.PropertyIdList.Contains(orientationId))
        {
            var o = src.GetPropertyItem(orientationId)?.Value?[0] ?? 1;
            var flip = o switch
            {
                3 => RotateFlipType.Rotate180FlipNone,
                6 => RotateFlipType.Rotate90FlipNone,
                8 => RotateFlipType.Rotate270FlipNone,
                _ => RotateFlipType.RotateNoneFlipNone,
            };
            bmp.RotateFlip(flip);
        }
        return bmp;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _pic.Image?.Dispose();
        base.Dispose(disposing);
    }
}
