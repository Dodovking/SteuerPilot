using SteuerPilot.Core;

namespace SteuerPilot.Gui;

/// <summary>Beleg erfassen oder bearbeiten – mit Dateivorschau und Texterkennung.</summary>
public sealed class BelegForm : DialogBase
{
    private sealed record Eintrag(string Abs, string? Rel)
    {
        public override string ToString() => Path.GetFileName(Abs) + (Rel == null ? "  (neu)" : "");
    }

    private sealed record KatEintrag(Kategorie Kat)
    {
        public override string ToString() => Kategorien.GruppenName(Kat.Gruppe) + "  ›  " + Kat.Name;
    }

    private readonly App _app;
    private readonly Beleg _b;
    private readonly bool _neu;
    private readonly DateTimePicker _datum;
    private readonly InputBox _betrag;
    private readonly InputBox _beschreibung;
    private readonly ComboBox _kategorie;
    private readonly Label _tipp;
    private readonly Segmented _person;
    private readonly NumberBox _anteil = new(0, 100);
    private readonly NumberBox _afa = new(0, 50);
    private readonly Label _afaInfo;
    private readonly InputBox _arbeit;
    private readonly Label _arbeitLabel;
    private readonly ComboBox _objekt;
    private readonly Label _objektLabel;
    private readonly InputBox _notiz = new(multiline: true);
    private readonly ListBox _dateien = new() { IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle, Font = Ui.Base };
    private readonly FlatButton _ocr = new("Text erkennen");
    private readonly Label _ocrInfo;
    private readonly Vorschau _vorschau = new() { Dock = DockStyle.Right };
    private bool _benutzerKategorie;
    private Vorschlag? _vorschlag;
    private readonly Label _kapHeading;
    private readonly InputBox[] _kap;
    private readonly List<Control> _kapControls = [];

    public List<string> NeueDateien { get; } = [];
    public List<string> EntfernteDateien { get; } = [];

    public BelegForm(App app, Beleg b, IReadOnlyList<string> dateien, bool neu, bool autoOcr)
        : base(neu ? "Neuer Beleg" : "Beleg bearbeiten", "Speichern", 1060, 720)
    {
        _app = app;
        _b = b;
        _neu = neu;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimumSize = new Size(Ui.S(900), Ui.S(600));
        ExtraRight = Ui.S(420);
        _vorschau.Width = Ui.S(420);
        _vorschau.Padding = new Padding(Ui.S(4), Ui.S(16), Ui.S(16), Ui.S(16));
        Controls.Add(_vorschau);
        Body.BringToFront();

        int w = ContentWidth;
        AddHeading("Beleg");
        _datum = Felder.Datum(b.Datum);
        _betrag = Felder.Text(140, b.Betrag == 0 ? "" : b.Betrag.ToString("0.00", Fmt.De), rechts: true);
        AddLine("Datum", _datum, "   Betrag (€)", _betrag);
        _beschreibung = Felder.Text(w, b.Beschreibung);
        AddLine("Beschreibung");
        Place(_beschreibung, _beschreibung.Height);

        AddLine("Kategorie");
        _kategorie = Felder.Auswahl(100);
        _kategorie.Width = w;
        _kategorie.MaxDropDownItems = 25;
        foreach (var k in Kategorien.Alle) _kategorie.Items.Add(new KatEintrag(k));
        _kategorie.SelectedIndex = Kategorien.Alle.ToList().FindIndex(k => k.Key == b.Kat.Key);
        Place(_kategorie, _kategorie.Height);
        _tipp = Make.Label("", Ui.Small, muted: true, width: w);
        Place(_tipp, Ui.S(48));

        AddHeading("Steuerliche Details");
        _person = new Segmented(app.Daten.Profil.PersonName(Person.Ich), app.Daten.Profil.PersonName(Person.Partner), "Gemeinsam") { SelectedIndex = (int)b.Person };
        AddLine("Für", _person);
        _anteil.Value = b.Anteil;
        AddLine("Steuerlich absetzbarer Anteil", _anteil, "%   (z. B. 60 % bei privat mitgenutztem Handy)");
        _afa.Value = b.AfaJahre;
        _afaInfo = Make.Label("", Ui.Small, muted: true);
        AddLine("Abschreiben über", _afa, "Jahre", _afaInfo);
        _arbeit = Felder.Text(140, b.Arbeitskosten?.ToString("0.00", Fmt.De) ?? "", rechts: true);
        _arbeitLabel = Make.Label("davon Arbeits-/Fahrtkosten (€)");
        AddLine(_arbeitLabel, _arbeit, "nur dieser Teil zählt bei § 35a");
        _objekt = Felder.Auswahl(270);
        _objekt.Items.Add("(kein bestimmtes Objekt)");
        foreach (var n in app.Daten.Profil.Mietobjekte.Select(o => o.Name.Trim()).Append(b.Objekt.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
            _objekt.Items.Add(n);
        _objekt.SelectedIndex = Math.Max(0, _objekt.Items.IndexOf(b.Objekt.Trim()));
        _objektLabel = Make.Label("Mietobjekt");
        AddLine(_objektLabel, _objekt, app.Daten.Profil.Mietobjekte.Count == 0 ? "anlegen unter „Meine Angaben“" : "");

        AddHeading("Dateien (Fotos, Scans, PDFs)");
        _dateien.Size = new Size(w, Ui.S(96));
        Place(_dateien, _dateien.Height);
        var plus = new FlatButton("+ Datei");
        var scan = new FlatButton("Scannen");
        var weg = new FlatButton("Entfernen", ButtonKind.Ghost);
        var oeffnen = new FlatButton("Öffnen", ButtonKind.Ghost);
        AddLine(plus, scan, _ocr, weg, oeffnen);
        _ocrInfo = Make.Label("", Ui.Small, muted: true, width: w);
        Place(_ocrInfo, Ui.S(34));

        AddHeading("Notiz");
        _notiz.Width = w;
        _notiz.Height = Ui.S(70);
        _notiz.Text = b.Notiz;
        Place(_notiz, _notiz.Height);

        _kapHeading = AddHeading("Werte der Bankbescheinigung (Anlage KAP)");
        _kapControls.Add(_kapHeading);
        var kw = b.Kapital ?? new Kapitalwerte();
        decimal[] start = [kw.Ertraege, kw.AktienGewinne, kw.Kapest, kw.Soli, kw.Kist, kw.VerlustAktien, kw.VerlustSonstige];
        _kap = start.Select(x => Felder.Text(120, x == 0 ? "" : x.ToString("0.00", Fmt.De), rechts: true)).ToArray();
        string[] namen = ["Kapitalerträge €", "davon Aktiengewinne €", "Kapitalertragsteuer €", "Soli €", "Kirchensteuer €", "Verluste Aktien €", "Verluste sonstige €"];
        for (int i = 0; i < _kap.Length; i += 2)
        {
            static Label Fest(string t, int breite) { var l = Make.Label(t); l.AutoSize = false; l.Size = new Size(Ui.S(breite), l.PreferredHeight); return l; }
            var parts = new List<object> { Fest(namen[i], 170), _kap[i] };
            if (i + 1 < _kap.Length) parts.AddRange([Fest("   " + namen[i + 1], 190), _kap[i + 1]]);
            int vor = Body.Controls.Count;
            AddLine(parts.ToArray());
            _kapControls.AddRange(Body.Controls.Cast<Control>().Skip(vor));
        }
        FinishLayout();

        foreach (var rel in b.Dateien) _dateien.Items.Add(new Eintrag(app.Ablage.Absolut(rel), rel));
        foreach (var d in dateien) _dateien.Items.Add(new Eintrag(d, null));
        if (_dateien.Items.Count > 0) _dateien.SelectedIndex = 0;

        _kategorie.SelectedIndexChanged += (_, _) => { _benutzerKategorie = true; KategorieGeaendert(); };
        _afa.Box.TextChanged += (_, _) => AfaInfo();
        _betrag.Box.TextChanged += (_, _) => AfaInfo();
        _datum.ValueChanged += (_, _) => AfaInfo();
        _dateien.SelectedIndexChanged += (_, _) => _vorschau.Zeigen((_dateien.SelectedItem as Eintrag)?.Abs);
        _vorschau.Zeigen((_dateien.SelectedItem as Eintrag)?.Abs);
        _dateien.DoubleClick += (_, _) => _vorschau.Oeffnen();
        plus.Click += (_, _) => { var fs = Aktionen.DateienWaehlen(this); foreach (var f in fs) DateiDazu(f); if (fs.Length > 0) _ = Erkennen(); };
        scan.Click += (_, _) => { var p = Aktionen.Scannen(this, _app); if (p != null) { DateiDazu(p); _ = Erkennen(); } };
        weg.Click += (_, _) => DateiWeg();
        oeffnen.Click += (_, _) => _vorschau.Oeffnen();
        _ocr.Click += async (_, _) => await Erkennen(überschreiben: true);
        OkButton.Click += (_, _) => Uebernehmen();
        AllowDrop = true;
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] fs) { var neue = fs.Where(Ablage.Unterstuetzt).ToList(); foreach (var f in neue) DateiDazu(f); if (neue.Count > 0) _ = Erkennen(); } };

        _benutzerKategorie = !neu;
        KategorieGeaendert();
        Shown += async (_, _) =>
        {
            Felder.Einfaerben(this);
            _vorschau.BackColor = Theme.P.Back;
            if (autoOcr && _dateien.Items.Count > 0) await Erkennen();
            else _betrag.Box.Focus();
        };
    }

    private Kategorie Kat => (_kategorie.SelectedItem as KatEintrag)?.Kat ?? Kategorien.Get(null);

    private void KategorieGeaendert()
    {
        var k = Kat;
        _tipp.Text = (string.IsNullOrEmpty(k.Formular) ? "" : "Formular: " + k.Formular + "  ·  ") + k.Tipp;
        _afa.Enabled = k.Abschreibbar;
        _arbeit.Enabled = k.MitArbeitskosten;
        _arbeitLabel.Enabled = k.MitArbeitskosten;
        _objekt.Enabled = _objektLabel.Enabled = k.Gruppe == Gruppe.Vermietung;
        foreach (var c in _kapControls) c.Visible = k.Wirkung == Wirkung.Kapital;
        AfaInfo();
    }

    private void AfaInfo()
    {
        if (!Kat.Abschreibbar) { _afaInfo.Text = "  (bei dieser Kategorie nicht nötig)"; return; }
        var betrag = Fmt.Zahl(_betrag.Text) ?? 0;
        var netto = betrag / 1.19m;
        if (_afa.Value <= 1)
            _afaInfo.Text = netto > 800 && _afa.Value == 0
                ? "  Tipp: ab 800 € netto abschreiben"
                : "  0/1 = sofort komplett im Kaufjahr";
        else
            _afaInfo.Text = $"  → {Fmt.Euro(Steuer.Abschreibung(betrag * _anteil.Value / 100m, _datum.Value, _afa.Value, _datum.Value.Year))} im Kaufjahr";
    }

    private void DateiDazu(string pfad)
    {
        if (_dateien.Items.Cast<Eintrag>().Any(e => string.Equals(e.Abs, pfad, StringComparison.OrdinalIgnoreCase))) return;
        _dateien.Items.Add(new Eintrag(pfad, null));
        _dateien.SelectedIndex = _dateien.Items.Count - 1;
    }

    private void DateiWeg()
    {
        if (_dateien.SelectedItem is not Eintrag e) return;
        if (e.Rel != null &&
            MessageBox.Show(this, "Datei vom Beleg entfernen? Sie wandert in den Papierkorb-Ordner des Steuerordners.", "Datei entfernen",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        if (e.Rel != null) EntfernteDateien.Add(e.Rel);
        _dateien.Items.Remove(e);
        if (_dateien.Items.Count > 0) _dateien.SelectedIndex = 0;
        else _vorschau.Zeigen(null);
    }

    private async Task Erkennen(bool überschreiben = false)
    {
        var e = _dateien.SelectedItem as Eintrag ?? _dateien.Items.Cast<Eintrag>().FirstOrDefault();
        if (e == null) { _ocrInfo.Text = "Erst eine Datei hinzufügen."; return; }
        _ocr.Enabled = false;
        _ocrInfo.Text = "Erkenne Text …";
        try
        {
            var erg = await Belegerkennung.Erkennen(e.Abs);
            if (IsDisposed) return;
            if (erg.Fehler != null) throw new InvalidOperationException(erg.Fehler);
            var text = erg.Text;
            var v = erg.Vorschlag;
            _vorschlag = v;
            if (v.Kapital is Kapitalwerte kw && (überschreiben || _kap.All(x => string.IsNullOrWhiteSpace(x.Text))))
            {
                decimal[] w = [kw.Ertraege, kw.AktienGewinne, kw.Kapest, kw.Soli, kw.Kist, kw.VerlustAktien, kw.VerlustSonstige];
                for (int i = 0; i < w.Length; i++) _kap[i].Text = w[i] == 0 ? "" : w[i].ToString("0.00", Fmt.De);
            }
            var teile = new List<string>();
            if (v.Betrag is decimal betrag && (überschreiben || string.IsNullOrWhiteSpace(_betrag.Text)))
            {
                _betrag.Text = betrag.ToString("0.00", Fmt.De);
                teile.Add("Betrag " + Fmt.Euro(betrag));
            }
            if (v.Datum is DateTime datum && (überschreiben || _neu))
            {
                _datum.Value = datum;
                teile.Add("Datum " + datum.ToString("dd.MM.yyyy"));
            }
            if (v.Beschreibung is string besch && (überschreiben || string.IsNullOrWhiteSpace(_beschreibung.Text)))
            {
                _beschreibung.Text = besch;
                teile.Add("Beschreibung");
            }
            if (v.Kategorie is string key && (überschreiben || !_benutzerKategorie))
            {
                _kategorie.SelectedIndex = Kategorien.Alle.ToList().FindIndex(k => k.Key == key);
                _benutzerKategorie = false;
                teile.Add("Kategorie „" + Kategorien.Get(key).Name + "“");
            }
            _ocrInfo.Text = teile.Count == 0
                ? (string.IsNullOrWhiteSpace(text) ? "Kein Text gefunden – bitte von Hand ausfüllen." : "Text gelesen, aber nichts Eindeutiges gefunden.")
                : "Erkannt: " + string.Join(", ", teile) + ". Bitte kurz prüfen.";
        }
        catch (Exception ex)
        {
            _ocrInfo.Text = "Texterkennung nicht möglich: " + ex.Message;
        }
        finally
        {
            _ocr.Enabled = true;
        }
    }

    private void Uebernehmen()
    {
        var betrag = Fmt.Zahl(_betrag.Text);
        if (betrag == null && !string.IsNullOrWhiteSpace(_betrag.Text))
        {
            MessageBox.Show(this, "Der Betrag ist keine gültige Zahl (Beispiel: 49,90).", "Beleg", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _betrag.Box.Focus();
            return;
        }
        var k = Kat;
        _b.Datum = _datum.Value.Date;
        _b.Betrag = Math.Abs(betrag ?? 0);
        _b.Beschreibung = string.IsNullOrWhiteSpace(_beschreibung.Text) ? k.Name : _beschreibung.Text.Trim();
        _b.Kategorie = k.Key;
        _b.Person = (Person)Math.Clamp(_person.SelectedIndex, 0, 2);
        _b.Anteil = _anteil.Value;
        _b.AfaJahre = k.Abschreibbar ? _afa.Value : 0;
        _b.Arbeitskosten = k.MitArbeitskosten ? Fmt.Zahl(_arbeit.Text) : null;
        _b.Objekt = k.Gruppe == Gruppe.Vermietung && _objekt.SelectedIndex > 0 ? (string)_objekt.SelectedItem! : "";
        _b.Notiz = _notiz.Text.Trim();
        if (k.Wirkung == Wirkung.Kapital)
        {
            decimal W(int i) => Fmt.Zahl(_kap[i].Text) ?? 0;
            _b.Kapital = new Kapitalwerte
            {
                Ertraege = W(0), AktienGewinne = W(1), Kapest = W(2), Soli = W(3), Kist = W(4),
                VerlustAktien = Math.Abs(W(5)), VerlustSonstige = Math.Abs(W(6)),
            };
            if (_b.Kapital.Leer) _b.Kapital = new Kapitalwerte { Ertraege = _b.Betrag };
            _app.Daten.Profil.HatKapital = true;
        }
        else _b.Kapital = null;
        if (_vorschlag?.Angaben != null && k.Key is "lohnsteuerbescheinigung" or "rente" && Belegerkennung.AngabenUebernehmen(_app, _vorschlag, _b) is string info)
            MessageBox.Show(this, info + ".", "Meine Angaben", MessageBoxButtons.OK, MessageBoxIcon.Information);
        NeueDateien.Clear();
        NeueDateien.AddRange(_dateien.Items.Cast<Eintrag>().Where(e => e.Rel == null).Select(e => e.Abs));
        _vorschau.Zeigen(null);
        DialogResult = DialogResult.OK;
        Close();
    }
}
