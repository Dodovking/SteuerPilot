using System.Drawing.Drawing2D;
using SteuerPilot.Core;

namespace SteuerPilot.Gui;

public static class Glyph
{
    public const string Home = "\uE80F", Work = "\uE821", Clock = "\uE823", Bank = "\uE825", Chart = "\uEAFC", Contact = "\uE77B",
        Car = "\uE804", Star = "\uE734", Heart = "\uEB51", People = "\uE716", Repair = "\uE90F", More = "\uE712", Folder = "\uE8B7",
        Mail = "\uE715", Check = "\uE930", Calc = "\uE8EF", Download = "\uE896", Upload = "\uE898", Settings = "\uE713", Sync = "\uE895",
        Info = "\uE946", Search = "\uE721", Bulb = "\uE82F", Chevron = "\uE76C", Play = "\uE768", Shield = "\uEA18", Doc = "\uE8A5";
}

/// <summary>Die Bereiche der Steuererklärung (Seitenleiste, Kacheln, Schritt-für-Schritt).</summary>
public enum Bereich
{
    Lohn,
    Rente,
    Vermietung,
    Kapital,
    Selbststaendig,
    Werbungskosten,
    Sonderausgaben,
    Belastungen,
    Kinder,
    Haushalt,
    Weitere,
}

public static class Bereiche
{
    public sealed record Info(Bereich B, string Titel, string Untertitel, string Glyph, Color Farbe);

    public static readonly Info[] Alle =
    [
        new(Bereich.Lohn, "Lohn & Gehalt", "Arbeitslohn, Lohnsteuerbescheinigung", Glyph.Work, Color.FromArgb(59, 130, 246)),
        new(Bereich.Rente, "Rente", "Gesetzliche, private & betriebliche Rente", Glyph.Clock, Color.FromArgb(245, 158, 11)),
        new(Bereich.Vermietung, "Vermietung & Miete", "Mieteinnahmen, Nebenkosten, Abschreibungen", Glyph.Bank, Color.FromArgb(96, 165, 250)),
        new(Bereich.Kapital, "Kapitalerträge", "Dividenden, Zinsen, ETFs & Sparpläne", Glyph.Chart, Color.FromArgb(34, 211, 153)),
        new(Bereich.Selbststaendig, "Selbstständigkeit", "Einnahmen, Ausgaben & Betriebskosten", Glyph.Contact, Color.FromArgb(56, 189, 248)),
        new(Bereich.Werbungskosten, "Werbungskosten", "Arbeitsweg, Homeoffice & Arbeitsmittel", Glyph.Car, Color.FromArgb(251, 146, 60)),
        new(Bereich.Sonderausgaben, "Sonderausgaben", "Versicherungen, Spenden & Vorsorge", Glyph.Star, Color.FromArgb(250, 204, 21)),
        new(Bereich.Belastungen, "Außergewöhnliche Belastungen", "Krankheitskosten, Pflege & mehr", Glyph.Heart, Color.FromArgb(244, 63, 94)),
        new(Bereich.Kinder, "Kinder & Familie", "Kinderbetreuung, Schulgeld, Alleinerziehend", Glyph.People, Color.FromArgb(96, 165, 250)),
        new(Bereich.Haushalt, "Haushalt", "Handwerker & haushaltsnahe Dienste", Glyph.Repair, Color.FromArgb(167, 139, 250)),
        new(Bereich.Weitere, "Persönliche Angaben", "Name, Familienstand, Kirchensteuer", Glyph.More, Color.FromArgb(148, 163, 184)),
    ];

    public static Info Von(Bereich b) => Alle[(int)b];

    public static Gruppe[] Gruppen(Bereich b) => b switch
    {
        Bereich.Lohn => [Gruppe.Einnahmen],
        Bereich.Rente => [Gruppe.Einnahmen],
        Bereich.Vermietung => [Gruppe.Vermietung],
        Bereich.Kapital => [Gruppe.Kapital],
        Bereich.Selbststaendig => [Gruppe.Selbststaendig],
        Bereich.Werbungskosten => [Gruppe.Werbungskosten],
        Bereich.Sonderausgaben => [Gruppe.Vorsorge, Gruppe.Sonderausgaben],
        Bereich.Belastungen => [Gruppe.AussergewoehnlicheBelastungen],
        Bereich.Haushalt => [Gruppe.Haushaltsnah],
        _ => [],
    };

    public static bool Gehoert(Bereich b, Beleg x) => b switch
    {
        Bereich.Lohn => x.Kategorie is "lohnsteuerbescheinigung" or "lohnersatz",
        Bereich.Rente => x.Kategorie == "rente",
        Bereich.Kinder => x.Kategorie is "kinderbetreuung" or "schulgeld",
        Bereich.Weitere => x.Kat.Gruppe == Gruppe.Sonstiges,
        _ => Gruppen(b).Contains(x.Kat.Gruppe),
    };

    /// <summary>Welche Bereiche für diese Person überhaupt eine Rolle spielen.</summary>
    public static bool Relevant(SteuerDaten d, int jahr, Bereich b)
    {
        var p = d.Profil;
        bool belege = d.BelegeIm(jahr).Any(x => Gehoert(b, x));
        return b switch
        {
            Bereich.Lohn => p.HatLohn,
            Bereich.Rente => p.HatRente,
            Bereich.Vermietung => p.HatVermietung || p.Mietobjekte.Count > 0,
            Bereich.Kapital => p.HatKapital || p.Geldanlagen.Count > 0,
            Bereich.Selbststaendig => p.HatSelbststaendig || belege,
            Bereich.Werbungskosten => p.HatLohn || belege,
            Bereich.Kinder => p.Kinder > 0 || belege,
            _ => true,
        };
    }

    public static bool Fertig(SteuerDaten d, int jahr, Bereich b)
    {
        var a = d.Angaben(jahr);
        if (a.Erledigt.Contains(b.ToString())) return true;
        bool belege = d.BelegeIm(jahr).Any(x => Gehoert(b, x));
        PersonAngaben[] pers = d.Profil.Splitting ? [a.Ich, a.Partner] : [a.Ich];
        return b switch
        {
            Bereich.Weitere => !string.IsNullOrWhiteSpace(d.Profil.Name),
            Bereich.Lohn => pers.Any(x => x.Bruttolohn > 0),
            Bereich.Rente => pers.Any(x => x.Rente > 0),
            Bereich.Vermietung => d.Profil.Mietobjekte.Count > 0 && belege,
            Bereich.Kapital => belege,
            Bereich.Werbungskosten => belege || pers.Any(x => x.EntfernungKm > 0 || x.HomeofficeTage > 0),
            Bereich.Sonderausgaben => belege || pers.Any(x => x.KrankenversicherungAN > 0 || x.RentnerKv > 0),
            _ => belege,
        };
    }

    public static List<(Info Info, bool Fertig)> Stand(SteuerDaten d, int jahr) =>
        Alle.Where(i => Relevant(d, jahr, i.B)).Select(i => (i, Fertig(d, jahr, i.B))).ToList();
}

/// <summary>Kreis mit Prozentangabe („Dein aktueller Stand“).</summary>
public sealed class ProgressRing : Control
{
    private int _prozent;
    public int Prozent { get => _prozent; set { _prozent = Math.Clamp(value, 0, 100); Invalidate(); } }
    public string Unten { get; set; } = "Fertiggestellt";

    public ProgressRing()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(Ui.S(150), Ui.S(150));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Ui.EffectiveBack(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int w = Ui.S(12);
        var r = new Rectangle(w, w, Width - 2 * w - 1, Height - 2 * w - 1);
        using (var bg = new Pen(Theme.P.Border, w)) g.DrawEllipse(bg, r);
        using (var fg = new Pen(Theme.P.Success, w) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            if (Prozent > 0) g.DrawArc(fg, r, -90, 360f * Prozent / 100f);
        using var big = new Font("Segoe UI Semibold", 22f);
        var t1 = new Rectangle(0, 0, Width, Height - Ui.S(18));
        TextRenderer.DrawText(g, $"{Prozent}%", big, t1, Theme.P.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        var t2 = new Rectangle(0, Height / 2 + Ui.S(12), Width, Ui.S(20));
        TextRenderer.DrawText(g, Unten, Ui.Small, t2, Theme.P.Muted, TextFormatFlags.HorizontalCenter);
    }
}

public sealed class FlatProgress : Control
{
    private float _wert;
    public float Wert { get => _wert; set { _wert = Math.Clamp(value, 0, 1); Invalidate(); } }

    public FlatProgress()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = Ui.S(8);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Ui.EffectiveBack(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = Ui.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2))
        using (var b = new SolidBrush(Theme.P.Border)) g.FillPath(b, p);
        int w = (int)((Width - 1) * Wert);
        if (w <= Height) return;
        using var p2 = Ui.Rounded(new Rectangle(0, 0, w, Height - 1), Height / 2);
        using var lg = new LinearGradientBrush(new Rectangle(0, 0, w + 1, Height), Theme.P.Accent, Color.FromArgb(56, 189, 248), 0f);
        g.FillPath(lg, p2);
    }
}

/// <summary>Großes Symbol (Windows-Symbolschrift) als Label.</summary>
public static class Symbol
{
    public static Label Make(string glyph, float size, Color farbe)
    {
        var l = new Label { Text = glyph, Font = Ui.Icon(size), AutoSize = true, ForeColor = farbe, Tag = "keepcolor", BackColor = Color.Transparent, UseMnemonic = false };
        return l;
    }
}

/// <summary>Kopfbereich mit Farbverlauf und angedeuteter Kuppel + Flagge.</summary>
public sealed class HeroPanel : Panel
{
    public string Titel { get; set; } = "";
    public string Untertitel { get; set; } = "";
    public string? Glyph { get; set; }
    public Color GlyphFarbe { get; set; } = Color.White;
    public bool Gebaeude { get; set; } = true;

    public HeroPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = Ui.S(140);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Ui.EffectiveBack(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Ui.Rounded(r, Ui.S(12));
        bool dark = Theme.P.Dark;
        using (var lg = new LinearGradientBrush(r, dark ? Color.FromArgb(14, 26, 56) : Color.FromArgb(219, 234, 254), dark ? Color.FromArgb(24, 64, 140) : Color.FromArgb(147, 197, 253), 0f))
            g.FillPath(lg, path);
        g.SetClip(path);
        if (Gebaeude) ZeichneGebaeude(g, r, dark);
        else if (Glyph != null)
        {
            using var f = Ui.Icon(Height / 2.2f / Ui.Scale);
            var gr = new Rectangle(r.Right - Height - Ui.S(30), 0, Height, Height);
            using var b = new SolidBrush(Color.FromArgb(70, GlyphFarbe));
            TextRenderer.DrawText(g, Glyph, f, gr, Color.FromArgb(110, GlyphFarbe), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        g.ResetClip();
        using (var pen = new Pen(Theme.P.Border)) g.DrawPath(pen, path);
        var tc = dark ? Color.White : Theme.P.Text;
        TextRenderer.DrawText(g, Titel, Ui.Hero, new Point(Ui.S(28), Height / 2 - Ui.S(40)), tc);
        TextRenderer.DrawText(g, Untertitel, Ui.Font(12f), new Point(Ui.S(30), Height / 2 + Ui.S(8)), dark ? Color.FromArgb(200, 214, 240) : Theme.P.Muted);
    }

    private static void ZeichneGebaeude(Graphics g, Rectangle r, bool dark)
    {
        int h = r.Height, baseY = r.Bottom - Ui.S(6), x0 = r.Right - (int)(h * 2.6);
        if (x0 < r.Width / 2) x0 = r.Width / 2;
        var stein = dark ? Color.FromArgb(70, 160, 190, 230) : Color.FromArgb(90, 255, 255, 255);
        using var b = new SolidBrush(stein);
        int bw = (int)(h * 1.9), bh = (int)(h * 0.42);
        g.FillRectangle(b, x0, baseY - bh, bw, bh);
        int tw = (int)(h * 0.28);
        g.FillRectangle(b, x0 - Ui.S(4), baseY - bh - Ui.S(18), tw, bh + Ui.S(18));
        g.FillRectangle(b, x0 + bw - tw + Ui.S(4), baseY - bh - Ui.S(18), tw, bh + Ui.S(18));
        using (var col = new SolidBrush(dark ? Color.FromArgb(60, 10, 20, 40) : Color.FromArgb(60, 30, 64, 175)))
            for (int x = x0 + tw + Ui.S(6); x < x0 + bw - tw - Ui.S(6); x += Ui.S(14)) g.FillRectangle(col, x, baseY - bh + Ui.S(10), Ui.S(5), bh - Ui.S(14));
        int dw = (int)(h * 0.62);
        var dome = new Rectangle(x0 + bw / 2 - dw / 2, baseY - bh - dw / 2 - Ui.S(4), dw, dw);
        g.FillPie(b, dome, 180, 180);
        using (var pen = new Pen(dark ? Color.FromArgb(70, 10, 20, 40) : Color.FromArgb(70, 30, 64, 175), Ui.S(2)))
            for (int i = 1; i < 6; i++) g.DrawArc(pen, dome.X + dome.Width * i / 12, dome.Y, dome.Width - dome.Width * i / 6, dome.Height, 180, 180);
        int px = x0 + bw + Ui.S(26), top = r.Top + Ui.S(14);
        using (var pole = new SolidBrush(Color.FromArgb(200, 220, 225, 235))) g.FillRectangle(pole, px, top, Ui.S(4), baseY - top);
        int fw = (int)(h * 0.62), fh = (int)(h * 0.38);
        Color[] farben = [Color.FromArgb(25, 25, 25), Color.FromArgb(221, 0, 0), Color.FromArgb(255, 206, 0)];
        for (int i = 0; i < 3; i++)
        {
            using var fb = new SolidBrush(farben[i]);
            float y = top + Ui.S(2) + fh * i / 3f;
            var pts = new List<PointF>();
            for (int s = 0; s <= 10; s++) pts.Add(new PointF(px + Ui.S(4) + fw * s / 10f, y + (float)Math.Sin(s / 10.0 * Math.PI * 1.6) * Ui.S(5)));
            for (int s = 10; s >= 0; s--) pts.Add(new PointF(px + Ui.S(4) + fw * s / 10f, y + fh / 3f + 1 + (float)Math.Sin(s / 10.0 * Math.PI * 1.6) * Ui.S(5)));
            g.FillPolygon(fb, pts.ToArray());
        }
    }
}

/// <summary>Kachel „Alle Bereiche im Überblick“.</summary>
public sealed class BereichKachel : Card
{
    private readonly bool _hervor;
    private bool _hover;
    private readonly string _glyph, _titel, _text;
    private readonly Color _farbe;
    public bool Haken { get; set; }

    public BereichKachel(string glyph, Color farbe, string titel, string text, bool hervorgehoben = false)
    {
        (_glyph, _farbe, _titel, _text, _hervor) = (glyph, farbe, titel, text, hervorgehoben);
        Size = new Size(Ui.S(300), Ui.S(74));
        Cursor = Cursors.Hand;
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.P;
        var g = e.Graphics;
        g.Clear(Ui.EffectiveBack(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Ui.Rounded(r, Ui.S(10));
        if (_hervor)
        {
            using var lg = new LinearGradientBrush(r, _hover ? p.AccentHover : p.Accent, Color.FromArgb(37, 99, 235), 45f);
            g.FillPath(lg, path);
        }
        else
        {
            using var b = new SolidBrush(_hover ? p.SurfaceHover : Ui.Blend(p.Surface, p.Back, 0.35f));
            g.FillPath(b, path);
            using var pen = new Pen(_hover ? Ui.Blend(p.Border, p.Accent, 0.5f) : p.Border);
            g.DrawPath(pen, path);
        }
        using var icon = Ui.Icon(20f);
        TextRenderer.DrawText(g, _glyph, icon, new Rectangle(Ui.S(10), 0, Ui.S(52), Height), _hervor ? Color.White : _farbe, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        var tc = _hervor ? Color.White : p.Text;
        var mc = _hervor ? Color.FromArgb(220, 232, 255) : p.Muted;
        int x = Ui.S(70), w = Width - x - Ui.S(34);
        TextRenderer.DrawText(g, _titel, Ui.Bold, new Rectangle(x, Ui.S(13), w, Ui.S(22)), tc, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, _text, Ui.Small, new Rectangle(x, Ui.S(36), w, Ui.S(34)), mc, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using var chev = Ui.Icon(10f);
        TextRenderer.DrawText(g, Haken ? Glyph.Check : Glyph.Chevron, chev, new Rectangle(Width - Ui.S(32), 0, Ui.S(24), Height),
            Haken ? p.Success : tc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>Eintrag der Seitenleiste: Symbol + Text, ausgewählt blau hinterlegt.</summary>
public sealed class NavButton : Control
{
    private bool _hover;
    public string Glyph { get; }
    public bool Ausgewaehlt { get; set; }

    public NavButton(string glyph, string text)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        Glyph = glyph;
        Text = text;
        Size = new Size(Ui.S(208), Ui.S(34));
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.P;
        var g = e.Graphics;
        g.Clear(Ui.EffectiveBack(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        if (Ausgewaehlt || _hover)
        {
            using var path = Ui.Rounded(r, Ui.S(8));
            if (Ausgewaehlt)
            {
                using var lg = new LinearGradientBrush(r, p.Accent, Color.FromArgb(37, 99, 235), 0f);
                g.FillPath(lg, path);
            }
            else
            {
                using var b = new SolidBrush(p.SurfaceHover);
                g.FillPath(b, path);
            }
        }
        var c = Ausgewaehlt ? Color.White : p.Text;
        using var icon = Ui.Icon(12f);
        TextRenderer.DrawText(g, Glyph, icon, new Rectangle(Ui.S(8), 0, Ui.S(28), Height), Ausgewaehlt ? Color.White : p.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, Text, Ausgewaehlt ? Ui.Bold : Ui.Nav, new Rectangle(Ui.S(42), 0, Width - Ui.S(46), Height), c,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}
