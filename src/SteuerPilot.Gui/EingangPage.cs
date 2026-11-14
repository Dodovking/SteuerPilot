using SteuerPilot.Core;

namespace SteuerPilot.Gui;

public sealed class EingangPage : Page
{
    private readonly ListBox _liste = new() { Dock = DockStyle.Fill, IntegralHeight = false, SelectionMode = SelectionMode.MultiExtended, BorderStyle = BorderStyle.None, Font = Ui.Base, ItemHeight = Ui.S(26), DrawMode = DrawMode.OwnerDrawFixed };
    private readonly Vorschau _vorschau = new() { Dock = DockStyle.Fill };
    private readonly Label _info = Make.Label("", Ui.Small, muted: true);
    private readonly FlatButton _erfassen = new("Als Beleg erfassen", ButtonKind.Primary);
    private readonly FlatButton _zusammen = new("Zusammen als 1 Beleg");
    private readonly FlatButton _loeschen = new("Löschen", ButtonKind.Danger);
    private readonly FlatButton _auto = new("Alle automatisch einsortieren");
    private List<string> _dateien = [];

    public override string Titel => "Eingang";

    public EingangPage(MainForm main, App app) : base(main, app)
    {
        var scan = new FlatButton("Scannen");
        var import = new FlatButton("Dateien hinzufügen");
        var ordner = new FlatButton("Ordner öffnen", ButtonKind.Ghost);
        var top = Leiste(_erfassen, _auto, _zusammen, scan, import, ordner, _loeschen);
        var infoPanel = new Panel { Dock = DockStyle.Top, Height = Ui.S(30) };
        infoPanel.Controls.Add(_info);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = Ui.S(8), FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Controls.Add(_liste);
        split.Panel2.Controls.Add(_vorschau);
        void Teilen()
        {
            if (split.Width > Ui.S(600) && split.SplitterDistance != Ui.S(380)) split.SplitterDistance = Ui.S(380);
        }
        split.HandleCreated += (_, _) => Teilen();
        split.SizeChanged += (_, _) => Teilen();
        VisibleChanged += (_, _) => { if (Visible && split.IsHandleCreated) split.BeginInvoke(Teilen); };
        Controls.Add(split);
        Controls.Add(infoPanel);
        Controls.Add(top);

        _liste.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using var bg = new SolidBrush(sel ? Theme.P.Selection : Theme.P.Surface);
            e.Graphics.FillRectangle(bg, e.Bounds);
            var f = _dateien[e.Index];
            var icon = f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "BILD";
            var r = new Rectangle(e.Bounds.X + Ui.S(8), e.Bounds.Y, Ui.S(40), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, icon, Ui.Small, r, Theme.P.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            var r2 = new Rectangle(r.Right, e.Bounds.Y, e.Bounds.Width - r.Right - Ui.S(150), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, Path.GetFileName(f), Ui.Base, r2, Theme.P.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            var t = Kurz(Belegerkennung.Fertig(f));
            var r3 = new Rectangle(e.Bounds.Right - Ui.S(148), e.Bounds.Y, Ui.S(142), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, t, Ui.Small, r3, Theme.P.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        };
        _liste.SelectedIndexChanged += (_, _) => Auswahl();
        _liste.DoubleClick += (_, _) => Erfassen(false);
        _liste.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) Loeschen(); if (e.KeyCode == Keys.Enter) Erfassen(false); };
        _erfassen.Click += (_, _) => Erfassen(false);
        _zusammen.Click += (_, _) => Erfassen(true);
        _loeschen.Click += (_, _) => Loeschen();
        _auto.Click += async (_, _) => await AutomatischEinsortieren();
        scan.Click += (_, _) =>
        {
            var p = Aktionen.Scannen(Main, App);
            Aktualisieren();
            if (p != null) _liste.SelectedIndex = _dateien.IndexOf(p);
        };
        import.Click += (_, _) =>
        {
            try { Aktionen.InEingang(App, Aktionen.DateienWaehlen(Main)); }
            catch (Exception ex) { Aktionen.Fehler(Main, "Kopieren fehlgeschlagen:", ex); }
            Aktualisieren();
        };
        ordner.Click += (_, _) => Ui.OpenInExplorer(App.Ablage.Eingang);
    }

    private static string Kurz(Belegerkennung.Ergebnis? e)
    {
        if (e == null) return "erkenne …";
        if (e.Fehler != null) return "keine Texterkennung";
        var v = e.Vorschlag;
        var teile = new List<string>();
        if (v.Betrag is decimal b) teile.Add(Fmt.Euro(b));
        if (v.Datum is DateTime d) teile.Add(d.ToString("dd.MM.yy"));
        return teile.Count == 0 ? "nichts erkannt" : string.Join(" · ", teile);
    }

    private int _lauf;

    /// <summary>Liest im Hintergrund alle Dateien im Eingang und zeigt Betrag/Datum direkt in der Liste.</summary>
    private async void HintergrundErkennen()
    {
        int lauf = ++_lauf;
        foreach (var f in _dateien.ToList())
        {
            if (lauf != _lauf || IsDisposed) return;
            if (!File.Exists(f) || Belegerkennung.Fertig(f) != null) continue;
            await Belegerkennung.Erkennen(f);
            if (IsDisposed) return;
            _liste.Invalidate();
            if (Belegerkennung.Fertig(f)?.Fehler is string fehler && lauf == _lauf)
            {
                _info.Text = "Texterkennung nicht möglich: " + fehler;
                return;
            }
        }
    }

    private async Task AutomatischEinsortieren()
    {
        var dateien = Ausgewaehlt().Count > 1 ? Ausgewaehlt() : _dateien.ToList();
        if (dateien.Count == 0) return;
        _auto.Enabled = false;
        _vorschau.Zeigen(null);
        int ok = 0;
        var offen = new List<string>();
        try
        {
            foreach (var f in dateien)
            {
                if (!File.Exists(f)) continue;
                _info.Text = $"Erkenne {Path.GetFileName(f)} …";
                var e = await Belegerkennung.Erkennen(f);
                if (e.Fehler != null) { offen.Add(Path.GetFileName(f)); continue; }
                var v = e.Vorschlag;
                if (!e.Vollstaendig) { offen.Add(Path.GetFileName(f)); continue; }
                var k = Kategorien.Get(v.Kategorie);
                var b = new Beleg
                {
                    Datum = v.Datum!.Value.Date,
                    Betrag = v.Betrag!.Value,
                    Kategorie = k.Key,
                    Beschreibung = string.IsNullOrWhiteSpace(v.Beschreibung) ? k.Name : v.Beschreibung!,
                    Notiz = "Automatisch erkannt – bitte prüfen.",
                };
                try
                {
                    App.Ablage.Hinzufuegen(b, [f], App.Settings.DateienVerschieben);
                    App.Daten.Belege.Add(b);
                    ok++;
                }
                catch (Exception ex) { Aktionen.Fehler(Main, $"„{Path.GetFileName(f)}“ konnte nicht einsortiert werden:", ex); }
            }
        }
        finally
        {
            if (ok > 0) App.Speichern();
            _auto.Enabled = true;
            Main.UpdateEingangBadge();
            Aktualisieren();
        }
        var msg = $"{ok} Beleg(e) automatisch erkannt und einsortiert (Datum, Betrag, Kategorie). Du findest sie unter „Belege“ mit dem Vermerk „Automatisch erkannt – bitte prüfen“.";
        if (offen.Count > 0)
            msg += $"\n\n{offen.Count} Datei(en) bleiben im Eingang, weil Betrag oder Datum nicht sicher erkannt wurden – bitte mit „Als Beleg erfassen“ von Hand ergänzen:\n" +
                   string.Join("\n", offen.Take(10).Select(n => "• " + n)) + (offen.Count > 10 ? "\n…" : "");
        MessageBox.Show(Main, msg, "Automatisch einsortieren", MessageBoxButtons.OK, offen.Count > 0 && ok == 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private List<string> Ausgewaehlt() => _liste.SelectedIndices.Cast<int>().Where(i => i < _dateien.Count).Select(i => _dateien[i]).ToList();

    private void Auswahl()
    {
        var a = Ausgewaehlt();
        _erfassen.Enabled = a.Count >= 1;
        _erfassen.Text = a.Count > 1 ? $"{a.Count} einzeln erfassen" : "Als Beleg erfassen";
        _erfassen.FitWidth();
        _zusammen.Enabled = a.Count > 1;
        _auto.Enabled = _dateien.Count > 0;
        _auto.Text = a.Count > 1 ? $"{a.Count} automatisch einsortieren" : "Alle automatisch einsortieren";
        _auto.FitWidth();
        _loeschen.Enabled = a.Count > 0;
        _vorschau.Zeigen(a.FirstOrDefault());
        ((Control)_erfassen.Parent!)?.PerformLayout();
    }

    private void Erfassen(bool zusammen)
    {
        var a = Ausgewaehlt();
        if (a.Count == 0) return;
        _vorschau.Zeigen(null);
        if (zusammen) Aktionen.NeuerBeleg(Main, App, a, ocr: true);
        else
            foreach (var f in a)
                if (File.Exists(f) && !Aktionen.NeuerBeleg(Main, App, [f], ocr: true)) break;
        Main.UpdateEingangBadge();
        Aktualisieren();
        if (_dateien.Count > 0) _liste.SelectedIndex = 0;
    }

    private void Loeschen()
    {
        var a = Ausgewaehlt();
        if (a.Count == 0) return;
        if (MessageBox.Show(Main, $"{a.Count} Datei(en) aus dem Eingang löschen?\n\nSie wandern in den Ordner „_Papierkorb“.", "Löschen",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _vorschau.Zeigen(null);
        foreach (var f in a)
        {
            try { App.Ablage.InPapierkorbVerschieben(f); }
            catch (Exception ex) { Aktionen.Fehler(Main, "Löschen fehlgeschlagen:", ex); }
        }
        Main.UpdateEingangBadge();
        Aktualisieren();
    }

    public override void Aktualisieren()
    {
        var auswahl = Ausgewaehlt();
        try { _dateien = App.Ablage.EingangDateien(); } catch { _dateien = []; }
        _liste.BeginUpdate();
        _liste.Items.Clear();
        foreach (var f in _dateien) _liste.Items.Add(Path.GetFileName(f));
        for (int i = 0; i < _dateien.Count; i++) if (auswahl.Contains(_dateien[i])) _liste.SetSelected(i, true);
        _liste.EndUpdate();
        _info.Text = _dateien.Count == 0
            ? $"Der Eingang ist leer. Scanne einen Beleg oder lege Fotos/PDFs in den Ordner {App.Ablage.Eingang} – sie erscheinen hier automatisch."
            : $"{_dateien.Count} Datei(en) warten auf Erfassung. Strg/Umschalt + Klick wählt mehrere (z. B. Seiten einer Rechnung).";
        if (_liste.SelectedIndices.Count == 0 && _dateien.Count > 0) _liste.SelectedIndex = 0;
        Auswahl();
        _liste.BackColor = Theme.P.Surface;
        HintergrundErkennen();
    }
}
