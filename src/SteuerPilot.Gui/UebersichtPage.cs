using SteuerPilot.Core;

namespace SteuerPilot.Gui;

/// <summary>Startseite: Begrüßung, Fortschritt, voraussichtliche Erstattung, alle Bereiche, Tipps und Belege.</summary>
public sealed class UebersichtPage : Page
{
    private readonly Panel _scroll = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly HeroPanel _hero = new();
    private readonly Card _stand = new();
    private readonly ProgressRing _ring = new();
    private readonly FlatProgress _balken = new();
    private readonly Label _standText = Make.Label("", Ui.Small, muted: true);
    private readonly Panel _liste = new();
    private readonly Card _erstattung = new();
    private readonly Label _erstTitel = Make.Label("Deine voraussichtliche Erstattung", Ui.CardTitle);
    private readonly Label _erstBetrag = Make.Label("–", new Font("Segoe UI Semibold", 30f));
    private readonly Label _erstInfo = Make.Label("", Ui.Small, muted: true);
    private readonly FlatButton _details = new("Details anzeigen →");
    private readonly Card _bereiche = new();
    private readonly List<BereichKachel> _kacheln = [];
    private readonly Card _tipps = new();
    private readonly Label _tippText = Make.Label("", Ui.Base);
    private readonly LinkLabel _naechster = new() { AutoSize = true, Font = Ui.Bold, LinkBehavior = LinkBehavior.HoverUnderline };
    private readonly Card _belege = new();
    private readonly Panel _belegListe = new();
    private readonly Panel _drop = new() { Cursor = Cursors.Hand };
    private Bereich? _naechsterBereich;

    public override string Titel => "Übersicht";

    public UebersichtPage(MainForm main, App app) : base(main, app)
    {
        Controls.Add(_scroll);
        _scroll.Controls.AddRange([_hero, _stand, _erstattung, _bereiche, _tipps, _belege]);

        var st = Make.Label("Dein aktueller Stand", Ui.CardTitle);
        st.Location = new Point(Ui.S(20), Ui.S(16));
        _ring.Location = new Point(Ui.S(24), Ui.S(52));
        _stand.Controls.AddRange([st, _ring, _liste, _balken, _standText]);

        _erstTitel.Location = new Point(Ui.S(20), Ui.S(16));
        _erstBetrag.Tag = "keepcolor";
        _erstBetrag.Location = new Point(Ui.S(16), Ui.S(56));
        _details.Kind = ButtonKind.Secondary;
        _details.Click += (_, _) => Main.Show<SchaetzungPage>();
        _erstattung.Controls.AddRange([_erstTitel, _erstBetrag, _erstInfo, _details]);

        var bt = Make.Label("Alle Bereiche im Überblick", Ui.CardTitle);
        bt.Location = new Point(Ui.S(20), Ui.S(16));
        _bereiche.Controls.Add(bt);
        foreach (var i in Bereiche.Alle)
        {
            var k = new BereichKachel(i.Glyph, i.Farbe, i.Titel, i.Untertitel);
            var b = i.B;
            k.Click += (_, _) => Main.ShowBereich(b);
            _kacheln.Add(k);
        }
        var assistent = new BereichKachel(Glyph.Play, Color.White, "Assistent", "Schritt für Schritt durch alle Bereiche", true);
        assistent.Click += (_, _) => Main.ShowBereich(Bereiche.Stand(App.Daten, App.Jahr).FirstOrDefault(s => !s.Fertig).Info?.B ?? Bereich.Weitere);
        _kacheln.Add(assistent);
        _bereiche.Controls.AddRange(_kacheln.ToArray());

        var tt = Make.Label("Fortschritt & Tipps", Ui.CardTitle);
        tt.Location = new Point(Ui.S(20), Ui.S(16));
        var bulb = Symbol.Make(Glyph.Bulb, 14f, Color.FromArgb(250, 204, 21));
        bulb.Location = new Point(Ui.S(20), Ui.S(52));
        _tippText.Location = new Point(Ui.S(52), Ui.S(52));
        _naechster.Click += (_, _) => { if (_naechsterBereich is Bereich b) Main.ShowBereich(b); };
        _tipps.Controls.AddRange([tt, bulb, _tippText, _naechster]);

        var bl = Make.Label("Deine Belege", Ui.CardTitle);
        bl.Location = new Point(Ui.S(20), Ui.S(16));
        var alle = new LinkLabel { Text = "Alle anzeigen →", AutoSize = true, Font = Ui.Small, LinkBehavior = LinkBehavior.HoverUnderline };
        alle.Click += (_, _) => Main.Show<BelegePage>();
        _belege.Controls.AddRange([bl, alle, _belegListe, _drop]);
        _drop.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Theme.P.Border, Ui.S(1)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            using var path = Ui.Rounded(new Rectangle(0, 0, _drop.Width - 1, _drop.Height - 1), Ui.S(8));
            g.DrawPath(pen, path);
            using var icon = Ui.Icon(16f);
            TextRenderer.DrawText(g, Glyph.Upload, icon, new Rectangle(Ui.S(14), 0, Ui.S(30), _drop.Height), Theme.P.Muted, TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "Belege hier ablegen oder klicken zum Auswählen", Ui.Small, new Rectangle(Ui.S(52), 0, _drop.Width - Ui.S(56), _drop.Height), Theme.P.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        };
        _drop.Click += (_, _) => { var f = Aktionen.DateienWaehlen(Main); if (f.Length > 0) Main.DateienAufnehmen(f); };

        _scroll.Layout += (_, _) => Anordnen();
        _belege.Layout += (_, _) => alle.Location = new Point(_belege.Width - alle.PreferredWidth - Ui.S(18), Ui.S(20));
    }

    private void Anordnen()
    {
        int w = Math.Max(Ui.S(900), _scroll.ClientSize.Width - Ui.S(4));
        int gap = Ui.S(16), y = 0;
        _hero.Bounds = new Rectangle(0, y, w, Ui.S(140));
        y = _hero.Bottom + gap;
        int rechtsW = Math.Max(Ui.S(320), w * 30 / 100), linksW = w - rechtsW - gap;
        _stand.Bounds = new Rectangle(0, y, linksW, Ui.S(240));
        _erstattung.Bounds = new Rectangle(linksW + gap, y, rechtsW, Ui.S(240));
        _liste.Bounds = new Rectangle(_ring.Right + Ui.S(28), Ui.S(52), linksW - _ring.Right - Ui.S(48), Ui.S(150));
        _balken.Bounds = new Rectangle(Ui.S(20), Ui.S(212), linksW - Ui.S(180), Ui.S(8));
        _standText.Location = new Point(_balken.Right + Ui.S(12), Ui.S(206));
        _erstInfo.Location = new Point(Ui.S(20), _erstBetrag.Bottom + Ui.S(4));
        _details.Width = rechtsW - Ui.S(40);
        _details.Location = new Point(Ui.S(20), Ui.S(240) - _details.Height - Ui.S(18));
        y = _stand.Bottom + gap;
        int spalten = 3, kw = (linksW - Ui.S(40) - (spalten - 1) * Ui.S(10)) / spalten, kh = Ui.S(74);
        for (int i = 0; i < _kacheln.Count; i++)
            _kacheln[i].Bounds = new Rectangle(Ui.S(20) + i % spalten * (kw + Ui.S(10)), Ui.S(52) + i / spalten * (kh + Ui.S(10)), kw, kh);
        int bh = Ui.S(52) + (_kacheln.Count + spalten - 1) / spalten * (kh + Ui.S(10)) + Ui.S(10);
        _bereiche.Bounds = new Rectangle(0, y, linksW, bh);
        _tippText.MaximumSize = new Size(rechtsW - Ui.S(70), 0);
        _naechster.Location = new Point(Ui.S(52), _tippText.Bottom + Ui.S(12));
        int th = Math.Max(Ui.S(150), _naechster.Bottom + Ui.S(18));
        _tipps.Bounds = new Rectangle(linksW + gap, y, rechtsW, th);
        _belegListe.Bounds = new Rectangle(Ui.S(20), Ui.S(50), rechtsW - Ui.S(40), Ui.S(30) * Math.Max(1, _belegListe.Controls.Count));
        _drop.Bounds = new Rectangle(Ui.S(20), _belegListe.Bottom + Ui.S(10), rechtsW - Ui.S(40), Ui.S(60));
        _belege.Bounds = new Rectangle(linksW + gap, _tipps.Bottom + gap, rechtsW, Math.Max(bh - th - gap, _drop.Bottom + Ui.S(18)));
    }

    public override void Aktualisieren()
    {
        var d = App.Daten;
        int jahr = App.Jahr;
        var name = d.Profil.Name.Trim();
        _hero.Titel = name.Length > 0 ? $"Hallo {name}!" : "Hallo!";
        _hero.Untertitel = name.Length > 0 ? $"Deine Steuererklärung {jahr} – einfach. schnell. digital." : "Trag unter „Persönliche Angaben“ deinen Namen ein – dann geht's los.";
        _hero.Invalidate();

        var stand = Bereiche.Stand(d, jahr);
        int fertig = stand.Count(s => s.Fertig);
        _ring.Prozent = stand.Count == 0 ? 0 : fertig * 100 / stand.Count;
        _balken.Wert = stand.Count == 0 ? 0 : fertig / (float)stand.Count;
        _standText.Text = $"{fertig} von {stand.Count} Bereichen";
        foreach (Control c in _liste.Controls.Cast<Control>().ToList()) c.Dispose();
        _liste.Controls.Clear();
        int hälfte = (stand.Count + 1) / 2;
        for (int i = 0; i < stand.Count; i++)
        {
            var (info, ok) = stand[i];
            var g = Symbol.Make(ok ? Glyph.Check : "\uEA3A", 10f, ok ? Theme.P.Success : Theme.P.Muted);
            var l = new LinkLabel { Text = info.Titel, AutoSize = true, Font = Ui.Base, LinkBehavior = LinkBehavior.HoverUnderline, LinkColor = Theme.P.Text, ActiveLinkColor = Theme.P.Accent, BackColor = Color.Transparent };
            var b = info.B;
            l.Click += (_, _) => Main.ShowBereich(b);
            int x = i < hälfte ? 0 : Math.Max(Ui.S(250), _liste.Width / 2), y = (i % hälfte) * Ui.S(25);
            g.Location = new Point(x, y + Ui.S(2));
            l.Location = new Point(x + Ui.S(26), y);
            _liste.Controls.AddRange([g, l]);
        }
        foreach (var k in _kacheln.Take(Bereiche.Alle.Length)) k.Haken = false;
        foreach (var (info, ok) in stand) _kacheln[(int)info.B].Haken = ok;

        var s = Auswertung.Schaetzen(d, jahr);
        bool nichts = stand.Where(x => x.Info.B is Bereich.Lohn or Bereich.Rente).All(x => !x.Fertig) && s.Einkuenfte == 0;
        _erstTitel.Text = s.Ergebnis >= 0 ? "Deine voraussichtliche Erstattung" : "Voraussichtliche Nachzahlung";
        _erstBetrag.Text = nichts ? "–" : Fmt.Euro0(Math.Abs(s.Ergebnis));
        _erstBetrag.ForeColor = nichts ? Theme.P.Muted : s.Ergebnis >= 0 ? Theme.P.Success : Theme.P.Danger;
        _erstInfo.Text = nichts ? "Trag zuerst Rente oder Lohn ein." : $"Stand: {DateTime.Today:dd.MM.yyyy} · Steuerjahr {jahr} · ohne Gewähr";

        var offen = stand.FirstOrDefault(x => !x.Fertig).Info;
        _naechsterBereich = offen?.B;
        _tippText.Text = s.Hinweise.FirstOrDefault() ?? "Lege Fotos und PDFs einfach in den Eingang – Datum, Betrag und Art werden automatisch erkannt.";
        _naechster.Text = offen != null ? $"Nächster Schritt: {offen.Titel} →" : "Alles erledigt – zum Steuer-Check →";
        if (offen == null) { _naechsterBereich = null; _naechster.Click -= ZumCheck; _naechster.Click += ZumCheck; }

        foreach (Control c in _belegListe.Controls.Cast<Control>().ToList()) c.Dispose();
        _belegListe.Controls.Clear();
        int yy = 0;
        foreach (var b in d.Belege.Where(b => b.Dateien.Count > 0).OrderByDescending(b => b.Erstellt).Take(4))
        {
            var icon = Symbol.Make(Glyph.Doc, 11f, Theme.P.Danger);
            icon.Location = new Point(0, yy + Ui.S(4));
            var l = new LinkLabel { Text = b.Beschreibung, AutoSize = false, AutoEllipsis = true, Font = Ui.Base, Bounds = new Rectangle(Ui.S(26), yy + Ui.S(3), Ui.S(260), Ui.S(22)), LinkBehavior = LinkBehavior.HoverUnderline, LinkColor = Theme.P.Text };
            var bb = b;
            l.Click += (_, _) => Aktionen.Bearbeiten(Main, App, bb);
            _belegListe.Controls.AddRange([icon, l]);
            yy += Ui.S(30);
        }
        Theme.Apply(this);
        _naechster.LinkColor = Theme.P.Accent;
        Anordnen();
        Invalidate(true);
    }

    private void ZumCheck(object? s, EventArgs e) { if (_naechsterBereich == null) Main.Show<SchaetzungPage>(); }
}
