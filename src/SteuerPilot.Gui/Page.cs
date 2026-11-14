namespace SteuerPilot.Gui;

/// <summary>Eine Seite im Hauptfenster.</summary>
public abstract class Page : Panel
{
    protected readonly App App;
    protected readonly MainForm Main;

    protected Page(MainForm main, App app)
    {
        Main = main;
        App = app;
        Dock = DockStyle.Fill;
        Padding = new Padding(Ui.S(24), Ui.S(18), Ui.S(24), Ui.S(18));
    }

    public abstract string Titel { get; }
    public abstract void Aktualisieren();

    protected static Panel Leiste(params Control[] controls)
    {
        var p = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0),
        };
        foreach (var c in controls)
        {
            c.Margin = new Padding(0, 0, Ui.S(8), Ui.S(8));
            p.Controls.Add(c);
        }
        return p;
    }

    protected static DataGridView Tabelle()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = Ui.S(34),
            Font = Ui.Base,
        };
        g.RowTemplate.Height = Ui.S(30);
        Theme.StyleGrid(g);
        return g;
    }

    protected static DataGridViewTextBoxColumn Spalte(string name, int weight = 100, bool rechts = false)
    {
        var c = new DataGridViewTextBoxColumn { HeaderText = name, FillWeight = weight, SortMode = DataGridViewColumnSortMode.Automatic };
        if (rechts) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        return c;
    }
}

/// <summary>Kleine Kennzahl-Kachel.</summary>
public sealed class Kachel : Card
{
    private readonly Label _titel;
    private readonly Label _wert;
    private readonly Label _info;

    public Kachel(string titel)
    {
        Size = new Size(Ui.S(250), Ui.S(108));
        _titel = Make.Label(titel, Ui.Small, muted: true);
        _wert = Make.Label("–", new Font("Segoe UI Semibold", 17f));
        _info = Make.Label("", Ui.Small, muted: true, width: Ui.S(220));
        _titel.Location = new Point(Ui.S(16), Ui.S(12));
        _wert.Location = new Point(Ui.S(14), Ui.S(32));
        _info.Location = new Point(Ui.S(16), Ui.S(74));
        Controls.AddRange([_titel, _wert, _info]);
        Margin = new Padding(0, 0, Ui.S(12), Ui.S(12));
    }

    public void Setzen(string wert, string info = "", Color? farbe = null)
    {
        _wert.Text = wert;
        _info.Text = info;
        _wert.Tag = farbe == null ? null : "keepcolor";
        _wert.ForeColor = farbe ?? Theme.P.Text;
    }
}
