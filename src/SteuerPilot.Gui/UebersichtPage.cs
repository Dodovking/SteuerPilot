using SteuerPilot.Core;

namespace SteuerPilot.Gui;

public sealed class UebersichtPage : Page
{
    private readonly FlowLayoutPanel _kacheln = new() { Dock = DockStyle.Top, Height = Ui.S(130), WrapContents = true };
    private readonly Kachel _belege = new("Belege im Steuerjahr");
    private readonly Kachel _wk = new("Werbungskosten (Job)");
    private readonly Kachel _ergebnis = new("Geschätztes Ergebnis");
    private readonly Kachel _eingang = new("Eingang (noch nicht sortiert)");
    private readonly DataGridView _bereiche = Tabelle();
    private readonly Label _hinweise = Make.Label("", Ui.Base, width: Ui.S(420));
    private readonly Panel _rechts = new() { Dock = DockStyle.Right, AutoScroll = true };

    public override string Titel => "Übersicht";

    public UebersichtPage(MainForm main, App app) : base(main, app)
    {
        _kacheln.Controls.AddRange([_belege, _wk, _ergebnis, _eingang]);
        var titel = Make.Label("Bereiche – Klick öffnet die Belege", Ui.Section);
        titel.Dock = DockStyle.Top;
        _bereiche.Columns.AddRange(Spalte("Bereich", 220), Spalte("Belege", 60, true), Spalte("Steuerlich angesetzt", 110, true), Spalte("Ordner", 150));
        _bereiche.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _bereiche.Rows[e.RowIndex].Tag is Gruppe g) Main.Show<BelegePage>().FilterAuf(g);
        };
        var links = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, Ui.S(16), 0) };
        links.Controls.Add(_bereiche);
        links.Controls.Add(titel);

        _rechts.Width = Ui.S(460);
        var ht = Make.Label("Hinweise & Tipps", Ui.Section);
        ht.Location = new Point(0, 0);
        _hinweise.Location = new Point(0, Ui.S(30));
        _rechts.Controls.AddRange([ht, _hinweise]);

        Controls.Add(links);
        Controls.Add(_rechts);
        Controls.Add(_kacheln);
        _eingang.Cursor = Cursors.Hand;
        _eingang.Click += (_, _) => Main.Show<EingangPage>();
        _ergebnis.Cursor = Cursors.Hand;
        _ergebnis.Click += (_, _) => Main.Show<SchaetzungPage>();
    }

    public override void Aktualisieren()
    {
        var d = App.Daten;
        int jahr = App.Jahr;
        var belege = d.BelegeIm(jahr).ToList();
        var posten = Auswertung.Posten(d, jahr);
        _belege.Setzen(belege.Count.ToString(), belege.Count == 0 ? "Noch nichts erfasst – oben auf „+ Beleg“ klicken." : $"Summe {Fmt.Euro(belege.Sum(b => b.Betrag))}");

        var s = Auswertung.Schaetzen(d, jahr);
        var wk = Auswertung.Summe(posten, Wirkung.Werbungskosten);
        _wk.Setzen(Fmt.Euro0(s.Werbungskosten), s.Werbungskosten <= 1230 ? "Unter 1.230 € zählt die Pauschale automatisch." : $"davon Belege {Fmt.Euro0(wk)}");

        bool ohneLohn = App.Angaben.Ich.Bruttolohn == 0 && App.Angaben.Partner.Bruttolohn == 0;
        if (ohneLohn && posten.All(p => !p.Kat.IstEinnahme))
            _ergebnis.Setzen("?", "Trage unter „Meine Angaben“ deine Lohnsteuerbescheinigung ein.");
        else
            _ergebnis.Setzen((s.Ergebnis >= 0 ? "+" : "−") + Fmt.Euro0(Math.Abs(s.Ergebnis)),
                s.Ergebnis >= 0 ? "voraussichtliche Erstattung" : "voraussichtliche Nachzahlung",
                s.Ergebnis >= 0 ? Theme.P.Success : Theme.P.Danger);

        int eingang = 0;
        try { eingang = App.Ablage.EingangDateien().Count; } catch { }
        _eingang.Setzen(eingang.ToString(), eingang == 0 ? "Alles einsortiert." : "Klicken zum Erfassen", eingang > 0 ? Theme.P.Warning : null);

        _bereiche.Rows.Clear();
        foreach (Gruppe g in Enum.GetValues<Gruppe>())
        {
            var pg = posten.Where(p => p.Kat.Gruppe == g).ToList();
            int i = _bereiche.Rows.Add(Kategorien.GruppenName(g), pg.Count(p => !p.AusVorjahr), Fmt.Euro(pg.Sum(p => p.Ansatz)), Kategorien.GruppenOrdner(g));
            _bereiche.Rows[i].Tag = g;
        }

        var tipps = new List<string>(s.Hinweise);
        var fehlend = App.Ablage.FehlendeDateien(d);
        if (fehlend.Count > 0) tipps.Insert(0, $"⚠ {fehlend.Count} verknüpfte Datei(en) fehlen im Steuerordner (verschoben oder gelöscht?).");
        var ohneDatei = belege.Count(b => b.Dateien.Count == 0 && b.Kat.Gruppe != Gruppe.Sonstiges);
        if (ohneDatei > 0) tipps.Add($"{ohneDatei} Beleg(e) ohne Foto/Scan – fürs Finanzamt am besten nachreichen.");
        if (d.Profil.Splitting && string.IsNullOrWhiteSpace(d.Profil.PartnerName)) tipps.Add("Zusammenveranlagung gewählt – trage den Namen deines Partners unter „Meine Angaben“ ein.");
        tipps.Add($"Abgabefrist ohne Steuerberater für {jahr}: in der Regel 31. Juli {jahr + 1} (freiwillige Erklärungen: 4 Jahre Zeit).");
        tipps.Add("Fotos vom Handy einfach in den Ordner „Eingang“ legen – sie erscheinen hier automatisch.");
        _hinweise.MaximumSize = new Size(_rechts.Width - Ui.S(24), 0);
        _hinweise.Text = string.Join("\n\n", tipps.Select(t => "• " + t));
        Theme.Apply(this);
    }
}
