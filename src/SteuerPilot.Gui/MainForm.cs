using SteuerPilot.Core;

namespace SteuerPilot.Gui;

public sealed class MainForm : Form
{
    private readonly App _app;
    private readonly Panel _header = new() { Dock = DockStyle.Top };
    private readonly Label _title = Make.Label("SteuerPilot", Ui.Title);
    private readonly Label _subtitle = Make.Label("", Ui.Small, muted: true);
    private readonly Label _jahrLabel = Make.Label("Steuerjahr", Ui.Small, muted: true);
    private readonly ComboBox _jahr = Felder.Auswahl(90);
    private readonly FlatButton _neu = new("+ Beleg", ButtonKind.Primary);
    private readonly FlatButton _scan = new("Scannen");
    private readonly FlatButton _import = new("Dateien hinzufügen");
    private readonly FlatButton _ordner = new("Ordner öffnen", ButtonKind.Ghost);
    private readonly Panel _nav = new() { Dock = DockStyle.Left, Tag = "surface" };
    private readonly Label _version = Make.Label("", Ui.Small, muted: true);
    private readonly Panel _content = new() { Dock = DockStyle.Fill };
    private readonly List<(FlatButton Button, Page Page)> _pages = [];
    private Page? _current;
    private FlatButton? _eingangButton;
    private FileSystemWatcher? _watcher;
    private string _watchedRoot = "";
    private readonly System.Windows.Forms.Timer _eingangTimer = new() { Interval = 700 };

    private readonly Panel _banner = new() { Dock = DockStyle.Top, Visible = false };
    private readonly Label _bannerText = Make.Label("", Ui.Bold);
    private readonly FlatButton _bannerInstall = new("Jetzt aktualisieren", ButtonKind.Primary);
    private readonly FlatButton _bannerLater = new("Später", ButtonKind.Ghost);
    private readonly UpdateChecker _updateChecker;
    private UpdateInfo? _available;
    private bool _updating;
    private bool _jahrWirdGesetzt;

    public string[] StartArgs { get; init; } = [];
    public App App => _app;

    public MainForm(App app, string? updateSource = null)
    {
        _app = app;
        _updateChecker = new UpdateChecker(releaseUrl: updateSource);
        Text = "SteuerPilot";
        Icon = Ui.AppIcon();
        Font = Ui.Base;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterScreen;
        var platz = Screen.PrimaryScreen?.WorkingArea.Size ?? new Size(int.MaxValue, int.MaxValue);
        ClientSize = new Size(Math.Min(Ui.S(1240), platz.Width - Ui.S(40)), Math.Min(Ui.S(780), platz.Height - Ui.S(60)));
        MinimumSize = new Size(Math.Min(Ui.S(1040), platz.Width), Math.Min(Ui.S(620), platz.Height));
        AllowDrop = true;
        KeyPreview = true;

        BuildHeader();
        BuildBanner();
        Controls.Add(_content);
        Controls.Add(_nav);
        Controls.Add(_banner);
        Controls.Add(_header);

        AddPage("Übersicht", new UebersichtPage(this, app));
        AddPage("Belege", new BelegePage(this, app));
        _eingangButton = AddPage("Eingang", new EingangPage(this, app));
        AddPage("Meine Angaben", new AngabenPage(this, app));
        AddPage("Steuer-Schätzung", new SchaetzungPage(this, app));
        AddPage("Rechner", new RechnerPage(this, app));
        AddPage("Export & Sicherung", new ExportPage(this, app));
        AddPage("Einstellungen", new EinstellungenPage(this, app));
        BuildNav();

        _app.Changed += () => { if (!IsDisposed) { FillYears(); WatchEingang(); UpdateHeader(); _current?.Aktualisieren(); } };
        _eingangTimer.Tick += (_, _) =>
        {
            _eingangTimer.Stop();
            UpdateEingangBadge();
            if (_current is EingangPage or UebersichtPage) _current.Aktualisieren();
        };
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files) DateienAufnehmen(files);
        };
        Theme.Changed += ApplyTheme;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is Microsoft.Win32.UserPreferenceCategory.General or Microsoft.Win32.UserPreferenceCategory.Color)
                if (IsHandleCreated) BeginInvoke(Theme.Refresh);
        };

        FillYears();
        UpdateHeader();
        WatchEingang();
        UpdateEingangBadge();
        ApplyTheme();
        Show(_pages[0].Page);

        var updateTimer = new System.Windows.Forms.Timer { Interval = 8000 };
        updateTimer.Tick += async (_, _) =>
        {
            updateTimer.Interval = 6 * 60 * 60 * 1000;
            if (_app.Settings.UpdatesPruefen) await CheckUpdatesQuietly();
        };
        updateTimer.Start();
    }

    private FlatButton AddPage(string name, Page page)
    {
        var b = new FlatButton(name, ButtonKind.Ghost) { AlignLeft = true, Width = Ui.S(196), Height = Ui.S(38) };
        b.Click += (_, _) => Show(page);
        page.Visible = false;
        _content.Controls.Add(page);
        _pages.Add((b, page));
        return b;
    }

    public void Show(Page page)
    {
        _current = page;
        foreach (var (b, p) in _pages)
        {
            b.Kind = p == page ? ButtonKind.Secondary : ButtonKind.Ghost;
            b.Font = p == page ? Ui.Bold : Ui.Base;
            b.Invalidate();
            p.Visible = p == page;
        }
        page.Aktualisieren();
    }

    public T Show<T>() where T : Page
    {
        var p = _pages.Select(x => x.Page).OfType<T>().First();
        Show(p);
        return p;
    }

    private void BuildNav()
    {
        _nav.Width = Ui.S(220);
        _nav.Padding = new Padding(Ui.S(12), Ui.S(14), Ui.S(12), Ui.S(12));
        foreach (var (b, _) in _pages) _nav.Controls.Add(b);
        _nav.Controls.Add(_version);
        _version.Text = $"Version {Updater.CurrentVersion.ToString(3)}\nOhne Gewähr – keine Steuerberatung";
        _nav.Layout += (_, _) =>
        {
            int y = Ui.S(14);
            foreach (var (b, _) in _pages)
            {
                b.Location = new Point(Ui.S(12), y);
                y += b.Height + Ui.S(4);
            }
            _version.Location = new Point(Ui.S(20), _nav.Height - _version.PreferredHeight - Ui.S(14));
        };
        _nav.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.P.Border);
            e.Graphics.DrawLine(pen, _nav.Width - 1, 0, _nav.Width - 1, _nav.Height);
        };
    }

    private void BuildHeader()
    {
        _header.Height = Ui.S(72);
        _header.Controls.AddRange([_title, _subtitle, _jahrLabel, _jahr, _neu, _scan, _import, _ordner]);
        _header.Layout += (_, _) =>
        {
            _title.Location = new Point(Ui.S(22), Ui.S(10));
            _subtitle.Location = new Point(Ui.S(24), _title.Bottom + Ui.S(1));
            int x = _header.Width - Ui.S(22);
            foreach (var b in new Control[] { _ordner, _import, _scan, _neu })
            {
                x -= b.Width;
                b.Location = new Point(x, (_header.Height - b.Height) / 2);
                x -= Ui.S(8);
            }
            x -= Ui.S(14) + _jahr.Width;
            _jahr.Location = new Point(x, (_header.Height - _jahr.Height) / 2 + Ui.S(7));
            _jahrLabel.Location = new Point(x, _jahr.Top - _jahrLabel.PreferredHeight - Ui.S(1));
        };
        _header.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.P.Border);
            e.Graphics.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
        };
        _neu.Click += (_, _) => Aktionen.NeuerBeleg(this, _app);
        _scan.Click += (_, _) => ScannenUndErfassen();
        _import.Click += (_, _) =>
        {
            var files = Aktionen.DateienWaehlen(this);
            if (files.Length > 0) DateienAufnehmen(files);
        };
        _ordner.Click += (_, _) => Ui.OpenInExplorer(_app.Ablage.Root);
        _jahr.SelectedIndexChanged += (_, _) =>
        {
            if (!_jahrWirdGesetzt && _jahr.SelectedItem is int j) _app.Jahr = j;
        };
    }

    private void BuildBanner()
    {
        _banner.Height = Ui.S(52);
        _banner.Controls.AddRange([_bannerText, _bannerInstall, _bannerLater]);
        _banner.Layout += (_, _) =>
        {
            _bannerLater.Location = new Point(_banner.Width - Ui.S(24) - _bannerLater.Width, (_banner.Height - _bannerLater.Height) / 2);
            _bannerInstall.Location = new Point(_bannerLater.Left - Ui.S(8) - _bannerInstall.Width, _bannerLater.Top);
            _bannerText.Location = new Point(Ui.S(26), (_banner.Height - _bannerText.PreferredHeight) / 2);
        };
        _banner.Paint += (_, e) =>
        {
            using var bar = new SolidBrush(Theme.P.Accent);
            e.Graphics.FillRectangle(bar, 0, 0, Ui.S(4), _banner.Height);
        };
        _bannerInstall.Click += (_, _) => OpenUpdate();
        _bannerLater.Click += (_, _) => _banner.Visible = false;
    }

    private void FillYears()
    {
        var jahre = _app.Jahre().ToList();
        if (_jahr.Items.Cast<int>().SequenceEqual(jahre) && Equals(_jahr.SelectedItem, _app.Jahr)) return;
        _jahrWirdGesetzt = true;
        _jahr.Items.Clear();
        foreach (var j in jahre) _jahr.Items.Add(j);
        _jahr.SelectedItem = _app.Jahr;
        _jahrWirdGesetzt = false;
    }

    private void UpdateHeader()
    {
        var n = _app.Daten.BelegeIm(_app.Jahr).Count();
        _subtitle.Text = $"{n} Belege in {_app.Jahr}  ·  Steuerordner: {_app.Ablage.Root}";
    }

    private void WatchEingang()
    {
        if (_watchedRoot == _app.Ablage.Root) return;
        _watchedRoot = _app.Ablage.Root;
        _watcher?.Dispose();
        try
        {
            _watcher = new FileSystemWatcher(_app.Ablage.Eingang) { IncludeSubdirectories = false, EnableRaisingEvents = true, SynchronizingObject = this };
            FileSystemEventHandler h = (_, _) => { _eingangTimer.Stop(); _eingangTimer.Start(); };
            _watcher.Created += h;
            _watcher.Deleted += h;
            _watcher.Renamed += (s, e) => h(s, e);
        }
        catch { _watcher = null; }
    }

    public void UpdateEingangBadge()
    {
        if (_eingangButton == null) return;
        int n = 0;
        try { n = _app.Ablage.EingangDateien().Count; } catch { }
        _eingangButton.Text = n > 0 ? $"Eingang  ({n} neu)" : "Eingang";
        UpdateHeader();
    }

    /// <summary>Dateien von außen: einzelne Datei direkt als Beleg erfassen, mehrere in den Eingang legen.</summary>
    public void DateienAufnehmen(IReadOnlyList<string> files)
    {
        var ok = files.Where(File.Exists).Where(Ablage.Unterstuetzt).ToList();
        if (ok.Count == 0)
        {
            MessageBox.Show(this, "Unterstützt werden Bilder (JPG, PNG, TIFF, HEIC …) und PDF-Dateien.", "SteuerPilot", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (ok.Count == 1)
        {
            Aktionen.NeuerBeleg(this, _app, ok, ocr: true);
            return;
        }
        var r = MessageBox.Show(this, $"{ok.Count} Dateien hinzufügen.\n\nJa = alle zusammen als EIN Beleg (z. B. mehrseitige Rechnung)\nNein = in den Eingang legen und dort einzeln erfassen",
            "Dateien hinzufügen", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (r == DialogResult.Yes) Aktionen.NeuerBeleg(this, _app, ok, ocr: true);
        else if (r == DialogResult.No)
        {
            try { Aktionen.InEingang(_app, ok); }
            catch (Exception ex) { Aktionen.Fehler(this, "Kopieren fehlgeschlagen:", ex); }
            UpdateEingangBadge();
            Show<EingangPage>();
        }
    }

    public void ScannenUndErfassen()
    {
        var pfad = Aktionen.Scannen(this, _app);
        UpdateEingangBadge();
        if (pfad != null) Aktionen.NeuerBeleg(this, _app, [pfad], ocr: true);
        UpdateEingangBadge();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.N)) { Aktionen.NeuerBeleg(this, _app); return true; }
        if (keyData == (Keys.Control | Keys.F)) { Show<BelegePage>().Suchen(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ApplyTheme()
    {
        Theme.Apply(this);
        Felder.Einfaerben(this);
        _header.BackColor = Theme.P.Surface;
        _banner.BackColor = Theme.P.Selection;
        _nav.BackColor = Theme.P.Surface;
        Theme.ApplyTitleBar(this);
        Invalidate(true);
        _current?.Aktualisieren();
    }

    public void ShowWindow()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Show();
        Activate();
    }

    public async Task CheckUpdatesQuietly()
    {
        if (_updating) return;
        UpdateInfo? info;
        try { info = await _updateChecker.GetLatestAsync(); }
        catch (Exception e) when (e is UpdateException or OperationCanceledException or HttpRequestException) { return; }
        if (info == null || !info.IsNewerThan(Updater.CurrentVersion) || IsDisposed) return;
        _available = info;
        _bannerText.Text = $"Neue Version {info.Version.ToString(3)} verfügbar  ·  Installiert: {Updater.CurrentVersion.ToString(3)}";
        _banner.Visible = true;
        _banner.PerformLayout();
    }

    public void OpenUpdate()
    {
        if (_updating) return;
        using var f = new UpdateForm(_updateChecker, _available);
        var result = f.ShowDialog(this);
        _available = f.Info != null && f.Info.IsNewerThan(Updater.CurrentVersion) ? f.Info : null;
        _banner.Visible = _available != null;
        if (result != DialogResult.OK || f.Info == null) return;
        _updating = true;
        try
        {
            _app.Speichern();
            Updater.Swap();
            Updater.StartUpdated(StartArgs);
        }
        catch (Exception e)
        {
            try { Updater.Rollback(); } catch { }
            _updating = false;
            MessageBox.Show(this, "Das Update konnte nicht installiert werden:\n\n" + e.Message + "\n\nDie bisherige Version läuft unverändert weiter.",
                "Update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Close();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _watcher?.Dispose();
        base.OnFormClosed(e);
    }
}
