using SteuerPilot.Core;

namespace SteuerPilot.Gui;

public sealed class SchaetzungPage : Page
{
    private readonly FlowLayoutPanel _kacheln = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(0, 0, 0, Ui.S(12)) };
    private readonly Kachel _zve = new("Zu versteuerndes Einkommen");
    private readonly Kachel _steuer = new("Festzusetzende Steuer");
    private readonly Kachel _gezahlt = new("Bereits gezahlt (Lohnsteuer …)");
    private readonly Kachel _ergebnis = new("Ergebnis");
    private readonly DataGridView _grid = Tabelle();
    private readonly Label _hinweise = Make.Label("", Ui.Small, muted: true);

    public override string Titel => "Steuer-Schätzung";

    public SchaetzungPage(MainForm main, App app) : base(main, app)
    {
        _kacheln.Controls.AddRange([_zve, _steuer, _gezahlt, _ergebnis]);
        _grid.Columns.AddRange(Spalte("Position", 260), Spalte("Betrag", 90, true), Spalte("Erläuterung", 260));
        foreach (DataGridViewColumn c in _grid.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
        var unten = new Panel { Dock = DockStyle.Bottom, Height = Ui.S(120), AutoScroll = true, Padding = new Padding(0, Ui.S(10), 0, 0) };
        unten.Controls.Add(_hinweise);
        _hinweise.Location = new Point(0, Ui.S(10));
        Controls.Add(_grid);
        Controls.Add(unten);
        Controls.Add(_kacheln);
    }

    public override void Aktualisieren()
    {
        var s = Auswertung.Schaetzen(App.Daten, App.Jahr);
        _zve.Setzen(Fmt.Euro0(s.ZuVersteuern), $"Grenzsteuersatz {Fmt.Prozent(s.Grenzsteuersatz)}");
        _steuer.Setzen(Fmt.Euro0(s.Festzusetzen), $"ESt {Fmt.Euro0(s.Einkommensteuer)} · Soli {Fmt.Euro0(s.Soli)} · KiSt {Fmt.Euro0(s.Kirchensteuer)}");
        _gezahlt.Setzen(Fmt.Euro0(s.BereitsGezahlt));
        _ergebnis.Setzen((s.Ergebnis >= 0 ? "+" : "−") + Fmt.Euro0(Math.Abs(s.Ergebnis)), s.Ergebnis >= 0 ? "voraussichtliche Erstattung" : "voraussichtliche Nachzahlung",
            s.Ergebnis >= 0 ? Theme.P.Success : Theme.P.Danger);
        _grid.Rows.Clear();
        foreach (var z in s.Zeilen)
        {
            int i = _grid.Rows.Add(z.Text, Fmt.Euro(z.Betrag), z.Hinweis);
            if (z.Summe) _grid.Rows[i].DefaultCellStyle.Font = Ui.Bold;
        }
        _grid.ClearSelection();
        Theme.StyleGrid(_grid);
        _hinweise.MaximumSize = new Size(Math.Max(Ui.S(400), Width - Ui.S(60)), 0);
        _hinweise.Text = string.Join("\n", s.Hinweise.Select(h => "• " + h).Append(
            "• Vereinfachte Schätzung ohne Gewähr – ersetzt keine Steuerberatung. Verbindlich ist nur der Steuerbescheid des Finanzamts."));
        Theme.Apply(this);
    }
}
