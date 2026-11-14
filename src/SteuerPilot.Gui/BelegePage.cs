using SteuerPilot.Core;

namespace SteuerPilot.Gui;

public sealed class BelegePage : Page
{
    private readonly InputBox _suche = Felder.Text(240);
    private readonly ComboBox _bereich = Felder.Auswahl(250);
    private readonly DataGridView _grid = Tabelle();
    private readonly Label _summe = Make.Label("", Ui.Bold);
    private readonly FlatButton _bearbeiten = new("Bearbeiten");
    private readonly FlatButton _loeschen = new("Löschen", ButtonKind.Danger);
    private readonly FlatButton _datei = new("Datei öffnen", ButtonKind.Ghost);
    private readonly FlatButton _zeigen = new("Im Ordner zeigen", ButtonKind.Ghost);
    private readonly Vorschau _vorschau = new() { Dock = DockStyle.Right };

    public override string Titel => "Belege";

    public BelegePage(MainForm main, App app) : base(main, app)
    {
        _suche.Box.PlaceholderText = "Suchen (Text, Betrag, Notiz) …";
        _bereich.Items.Clear();
        _bereich.Items.Add("Alle Bereiche");
        foreach (var g in Enum.GetValues<Gruppe>()) _bereich.Items.Add(Kategorien.GruppenName(g));
        _bereich.SelectedIndex = 0;
        var neu = new FlatButton("+ Neu", ButtonKind.Primary);
        var top = Leiste(_suche, _bereich, neu, _bearbeiten, _loeschen, _datei, _zeigen);

        _grid.MultiSelect = true;
        _grid.Columns.AddRange(Spalte("Datum", 70), Spalte("Beschreibung", 200), Spalte("Kategorie", 150), Spalte("Für", 70),
            Spalte("Betrag", 75, true), Spalte("Absetzbar", 75, true), Spalte("Dateien", 45, true));
        _grid.Columns[0].ValueType = typeof(DateTime);
        _grid.Columns[0].DefaultCellStyle.Format = "dd.MM.yyyy";
        foreach (int i in new[] { 4, 5 })
        {
            _grid.Columns[i].ValueType = typeof(decimal);
            _grid.Columns[i].DefaultCellStyle.Format = "#,##0.00 €";
            _grid.Columns[i].DefaultCellStyle.FormatProvider = Fmt.De;
        }
        _vorschau.Width = Ui.S(340);
        _vorschau.Padding = new Padding(Ui.S(12), 0, 0, 0);
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = Ui.S(36) };
        bottom.Controls.Add(_summe);
        _summe.Location = new Point(0, Ui.S(10));
        Controls.Add(_grid);
        Controls.Add(_vorschau);
        Controls.Add(bottom);
        Controls.Add(top);

        neu.Click += (_, _) => Aktionen.NeuerBeleg(Main, App);
        _bearbeiten.Click += (_, _) => Bearbeiten();
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Bearbeiten(); };
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { Bearbeiten(); e.Handled = true; }
            if (e.KeyCode == Keys.Delete) { Aktionen.Loeschen(Main, App, Ausgewaehlt()); e.Handled = true; }
        };
        _loeschen.Click += (_, _) => Aktionen.Loeschen(Main, App, Ausgewaehlt());
        _datei.Click += (_, _) => { var p = ErsteDatei(); if (p != null) Aktionen.Oeffnen(p); };
        _zeigen.Click += (_, _) =>
        {
            var b = Ausgewaehlt().FirstOrDefault();
            if (b == null) return;
            var p = ErsteDatei();
            if (p != null && File.Exists(p)) Ui.OpenInExplorer(p, select: true);
            else Ui.OpenInExplorer(Directory.Exists(App.Ablage.ZielOrdner(b)) ? App.Ablage.ZielOrdner(b) : App.Ablage.Root);
        };
        _grid.SelectionChanged += (_, _) => Auswahl();
        _suche.Box.TextChanged += (_, _) => Aktualisieren();
        _bereich.SelectedIndexChanged += (_, _) => Aktualisieren();
        AllowDrop = true;
    }

    public void Suchen() => _suche.Box.Focus();

    public void Suchen(string text) { _suche.Text = text; _suche.Box.Focus(); }

    public void FilterAuf(Gruppe g) => _bereich.SelectedIndex = (int)g + 1;

    private List<Beleg> Ausgewaehlt() =>
        _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<Beleg>().ToList();

    private string? ErsteDatei()
    {
        var b = Ausgewaehlt().FirstOrDefault();
        return b == null || b.Dateien.Count == 0 ? null : App.Ablage.Absolut(b.Dateien[0]);
    }

    private void Bearbeiten()
    {
        var b = Ausgewaehlt().FirstOrDefault();
        if (b != null) Aktionen.Bearbeiten(Main, App, b);
    }

    private void Auswahl()
    {
        var n = Ausgewaehlt().Count;
        _bearbeiten.Enabled = n == 1;
        _loeschen.Enabled = n > 0;
        _datei.Enabled = _zeigen.Enabled = n == 1;
        _vorschau.Zeigen(ErsteDatei());
    }

    public override void Aktualisieren()
    {
        int jahr = App.Jahr;
        var auswahl = Ausgewaehlt().Select(b => b.Id).ToHashSet();
        var q = _suche.Text.Trim();
        var posten = Auswertung.Posten(App.Daten, jahr).ToDictionary(p => p.Beleg.Id);
        var liste = App.Daten.BelegeIm(jahr)
            .Concat(posten.Values.Where(p => p.AusVorjahr).Select(p => p.Beleg))
            .Where(b => _bereich.SelectedIndex <= 0 || (int)b.Kat.Gruppe == _bereich.SelectedIndex - 1)
            .Where(b => q.Length == 0 || Treffer(b, q))
            .OrderByDescending(b => b.Datum).ThenByDescending(b => b.Erstellt)
            .ToList();

        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (var b in liste)
        {
            var ansatz = posten.TryGetValue(b.Id, out var p) ? p.Ansatz : 0m;
            var besch = b.Jahr != jahr ? b.Beschreibung + $"  (Abschreibung aus {b.Jahr})" : b.Beschreibung;
            int i = _grid.Rows.Add(b.Datum, besch, b.Kat.Name, App.Daten.Profil.PersonName(b.Person), b.Betrag, ansatz,
                b.Dateien.Count == 0 ? "–" : b.Dateien.Count.ToString());
            var row = _grid.Rows[i];
            row.Tag = b;
            if (b.Kat.IstEinnahme) row.Cells[4].Style.ForeColor = Theme.P.Success;
            if (b.Dateien.Any(f => !File.Exists(App.Ablage.Absolut(f)))) row.Cells[6].Style.ForeColor = Theme.P.Danger;
        }
        _grid.ClearSelection();
        foreach (DataGridViewRow r in _grid.Rows)
            if (r.Tag is Beleg b && auswahl.Contains(b.Id)) r.Selected = true;
        _grid.ResumeLayout();
        var eigene = liste.Where(b => b.Jahr == jahr).ToList();
        _summe.Text = $"{eigene.Count} Belege  ·  Summe {Fmt.Euro(eigene.Sum(b => b.Betrag))}  ·  steuerlich angesetzt {Fmt.Euro(liste.Sum(b => posten.TryGetValue(b.Id, out var p) ? p.Ansatz : 0))}";
        Auswahl();
        Theme.StyleGrid(_grid);
    }

    private static bool Treffer(Beleg b, string q) =>
        b.Beschreibung.Contains(q, StringComparison.CurrentCultureIgnoreCase)
        || b.Notiz.Contains(q, StringComparison.CurrentCultureIgnoreCase)
        || b.Kat.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)
        || b.Betrag.ToString("0.00", Fmt.De).Contains(q)
        || b.Dateien.Any(f => f.Contains(q, StringComparison.CurrentCultureIgnoreCase));
}
