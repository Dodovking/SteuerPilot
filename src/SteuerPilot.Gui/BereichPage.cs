using SteuerPilot.Core;

namespace SteuerPilot.Gui;

/// <summary>Eine Seite je Bereich der Steuererklärung – Eingabefelder, zugehörige Belege, Zurück/Weiter.</summary>
public sealed class BereichPage : Page
{
    private readonly Formular _f = new();
    private readonly HeroPanel _kopf = new() { Dock = DockStyle.Top, Gebaeude = false };
    private readonly ToggleSwitch _erledigt = new("Bereich erledigt");
    private readonly FlatButton _zurueck = new("← Zurück");
    private readonly FlatButton _weiter = new("Weiter →", ButtonKind.Primary);
    private readonly System.Windows.Forms.Timer _save = new() { Interval = 600 };
    private object? _geladen;
    private bool _laden, _selbst;

    public Bereich Bereich { get; }
    public Bereiche.Info Info => Bereiche.Von(Bereich);
    public override string Titel => Info.Titel;

    public BereichPage(MainForm main, App app, Bereich bereich) : base(main, app)
    {
        Bereich = bereich;
        _kopf.Height = Ui.S(104);
        _kopf.Glyph = Info.Glyph;
        _kopf.GlyphFarbe = Info.Farbe;
        _kopf.Titel = Info.Titel;
        _kopf.Untertitel = Info.Untertitel;
        var abstand = new Panel { Dock = DockStyle.Top, Height = Ui.S(16) };
        var unten = new Panel { Dock = DockStyle.Bottom, Height = Ui.S(58) };
        unten.Controls.AddRange([_zurueck, _erledigt, _weiter]);
        _zurueck.Width = _weiter.Width = Ui.S(150);
        unten.Layout += (_, _) =>
        {
            int y = Ui.S(14);
            _zurueck.Location = new Point(0, y);
            _weiter.Location = new Point(unten.Width - _weiter.Width, y);
            _erledigt.Location = new Point((unten.Width - _erledigt.Width) / 2, y + (_zurueck.Height - _erledigt.Height) / 2);
        };
        Controls.Add(_f);
        Controls.Add(abstand);
        Controls.Add(_kopf);
        Controls.Add(unten);
        _save.Tick += (_, _) => { _save.Stop(); Speichern(); };
        VisibleChanged += (_, _) => { if (!Visible && _save.Enabled) { _save.Stop(); Speichern(); } };
        App.Changed += () => { if (!_selbst) _geladen = null; };
        _erledigt.CheckedChanged += (_, _) =>
        {
            if (_laden) return;
            var e = App.Angaben.Erledigt;
            e.Remove(Bereich.ToString());
            if (_erledigt.Checked) e.Add(Bereich.ToString());
            Speichern();
        };
        _zurueck.Click += (_, _) => Springen(-1);
        _weiter.Click += (_, _) => Springen(+1);
    }

    private void Speichern()
    {
        _selbst = true;
        try { App.Speichern(); } finally { _selbst = false; }
    }

    private void Springen(int richtung)
    {
        var liste = Bereiche.Stand(App.Daten, App.Jahr).Select(s => s.Info.B).ToList();
        int i = liste.IndexOf(Bereich);
        int j = i < 0 ? 0 : i + richtung;
        if (j < 0) Main.Show<UebersichtPage>();
        else if (j >= liste.Count) Main.Show<SchaetzungPage>();
        else Main.ShowBereich(liste[j]);
    }

    private void Geaendert()
    {
        if (_laden) return;
        _save.Stop();
        _save.Start();
    }

    private void NeuAufbauen() { _save.Stop(); Speichern(); _geladen = null; BeginInvoke(Aktualisieren); }

    private InputBox Geld(decimal wert, Action<decimal> setzen)
    {
        var b = Felder.Text(130, Fmt.Eingabe(wert), rechts: true);
        b.Box.TextChanged += (_, _) => { setzen(Math.Max(0, Fmt.Zahl(b.Text) ?? 0)); Geaendert(); };
        return b;
    }

    private NumberBox Ganz(int wert, int max, Action<int> setzen)
    {
        var b = new NumberBox(0, max) { Width = Ui.S(130) };
        b.Value = wert;
        if (wert == 0) b.Text = "";
        b.Box.TextChanged += (_, _) => { setzen(b.Value); Geaendert(); };
        return b;
    }

    private CheckBox Haken(string text, bool wert, Action<bool> setzen, bool neuAufbauen = false)
    {
        var c = Felder.Haken(text, wert);
        c.AutoSize = false;
        c.Size = new Size(Ui.S(230), Ui.S(28));
        c.CheckedChanged += (_, _) => { setzen(c.Checked); if (neuAufbauen) NeuAufbauen(); else Geaendert(); };
        return c;
    }

    public override void Aktualisieren()
    {
        var d = App.Daten;
        var key = (App.Jahr, d, d.Profil.Splitting, Theme.P);
        _laden = true;
        _erledigt.Checked = App.Angaben.Erledigt.Contains(Bereich.ToString());
        var liste = Bereiche.Stand(d, App.Jahr).Select(s => s.Info.B).ToList();
        int i = liste.IndexOf(Bereich);
        _weiter.Text = i >= 0 && i == liste.Count - 1 ? "Zum Steuer-Check →" : "Weiter →";
        _weiter.FitWidth(Ui.S(150));
        _laden = false;
        if (Equals(_geladen, key)) return;
        _geladen = key;
        _laden = true;
        _f.SuspendLayout();
        _f.Leeren();
        switch (Bereich)
        {
            case Bereich.Lohn: Lohn(); break;
            case Bereich.Rente: Rente(); break;
            case Bereich.Vermietung: Vermietung(); break;
            case Bereich.Kapital: Kapital(); break;
            case Bereich.Werbungskosten: Werbungskosten(); break;
            case Bereich.Kinder: Kinder(); break;
            case Bereich.Weitere: Weitere(); break;
            default: Allgemein(); break;
        }
        _f.Abschluss();
        Theme.Apply(this);
        Felder.Einfaerben(_f);
        _f.ResumeLayout();
        _laden = false;
    }

    private (string Name, PersonAngaben A)[] Personen()
    {
        var pr = App.Daten.Profil;
        var a = App.Angaben;
        return pr.Splitting ? [(pr.PersonName(Person.Ich), a.Ich), (pr.PersonName(Person.Partner), a.Partner)] : [(pr.PersonName(Person.Ich), a.Ich)];
    }

    private void Kopf()
    {
        var p = Personen();
        if (p.Length > 1) _f.Zeile("", Make.Label(p[0].Name, Ui.Bold, width: Ui.S(130)), Make.Label(p[1].Name, Ui.Bold, width: Ui.S(130)));
    }

    private void Feld(string label, string hinweis, Func<PersonAngaben, Control> make) => _f.Zeile(label, hinweis, Personen().Select(p => make(p.A)).ToArray());

    private void Hochladen(string text, string kategorie)
    {
        var b = new FlatButton("⇪  " + text, ButtonKind.Primary);
        b.Click += (_, _) =>
        {
            var dateien = Aktionen.DateienWaehlen(Main);
            if (dateien.Length > 0) Aktionen.NeuerBeleg(Main, App, dateien, ocr: true, kategorie: kategorie);
        };
        _f.Element(b);
    }

    private void Tipp(string text)
    {
        var c = new Card { Width = Ui.S(760) };
        var g = Symbol.Make(Glyph.Bulb, 14f, Color.FromArgb(250, 204, 21));
        g.Location = new Point(Ui.S(14), Ui.S(12));
        var l = Make.Label(text, Ui.Base, width: Ui.S(680));
        l.Location = new Point(Ui.S(50), Ui.S(14));
        c.Controls.AddRange([g, l]);
        c.Height = l.PreferredHeight + Ui.S(28);
        _f.Element(c);
    }

    private static string? StandardKategorie(Bereich b) => b switch
    {
        Bereich.Lohn => "lohnsteuerbescheinigung",
        Bereich.Rente => "rente",
        Bereich.Vermietung => "mieteinnahmen",
        Bereich.Kapital => "kapitalertraege",
        Bereich.Werbungskosten => "arbeitsmittel",
        Bereich.Sonderausgaben => "spenden",
        Bereich.Belastungen => "krankheitskosten",
        Bereich.Kinder => "kinderbetreuung",
        Bereich.Haushalt => "handwerker",
        _ => null,
    };

    private void BelegListe()
    {
        var belege = App.Daten.BelegeIm(App.Jahr).Where(x => Bereiche.Gehoert(Bereich, x)).OrderByDescending(x => x.Datum).ToList();
        _f.Ueberschrift($"Belege in diesem Bereich ({belege.Count})");
        var c = new Card { Width = Ui.S(760) };
        int y = Ui.S(10);
        foreach (var b in belege.Take(12))
        {
            var zeile = new Panel { Location = new Point(Ui.S(10), y), Size = new Size(c.Width - Ui.S(20), Ui.S(28)), Cursor = Cursors.Hand };
            var links = Make.Label($"{b.Datum:dd.MM.yyyy}    {b.Beschreibung}", Ui.Base);
            links.AutoSize = false;
            links.AutoEllipsis = true;
            links.Bounds = new Rectangle(Ui.S(6), Ui.S(4), zeile.Width - Ui.S(150), Ui.S(22));
            var rechts = Make.Label(Fmt.Euro(b.Betrag), Ui.Bold);
            rechts.AutoSize = false;
            rechts.TextAlign = ContentAlignment.TopRight;
            rechts.Bounds = new Rectangle(zeile.Width - Ui.S(140), Ui.S(4), Ui.S(132), Ui.S(22));
            zeile.Controls.AddRange([links, rechts]);
            void Oeffnen(object? s, EventArgs e) { if (Aktionen.Bearbeiten(Main, App, b)) { _geladen = null; Aktualisieren(); } }
            zeile.Click += Oeffnen; links.Click += Oeffnen; rechts.Click += Oeffnen;
            c.Controls.Add(zeile);
            y += Ui.S(30);
        }
        if (belege.Count == 0)
        {
            var l = Make.Label("Noch keine Belege. Füge Rechnungen, Bescheinigungen oder Fotos hinzu – Betrag und Datum werden automatisch erkannt.", Ui.Small, muted: true, width: Ui.S(720));
            l.Location = new Point(Ui.S(16), y + Ui.S(4));
            c.Controls.Add(l);
            y += l.PreferredHeight + Ui.S(10);
        }
        else if (belege.Count > 12)
        {
            var l = Make.Label($"… und {belege.Count - 12} weitere", Ui.Small, muted: true);
            l.Location = new Point(Ui.S(16), y + Ui.S(2));
            c.Controls.Add(l);
            y += Ui.S(26);
        }
        c.Height = y + Ui.S(8);
        _f.Element(c);
        var neu = new FlatButton("+ Beleg hinzufügen", ButtonKind.Primary);
        neu.Click += (_, _) =>
        {
            var dateien = Aktionen.DateienWaehlen(Main);
            Aktionen.NeuerBeleg(Main, App, dateien, ocr: dateien.Length > 0, kategorie: StandardKategorie(Bereich));
        };
        var alle = new FlatButton("Alle in der Belegverwaltung");
        alle.Click += (_, _) =>
        {
            var g = Bereiche.Gruppen(Bereich);
            var seite = Main.Show<BelegePage>();
            if (g.Length > 0) seite.FilterAuf(g[0]);
        };
        _f.Zeile("", neu, alle);
        _f.LabelBreite = Ui.S(300);
    }

    private void Summe(string text, decimal betrag, string hinweis = "") =>
        _f.Zeile(text, hinweis, Make.Label(Fmt.Euro(betrag), Ui.Bold));

    private void Lohn()
    {
        _f.Ueberschrift($"Lohnsteuerbescheinigung {App.Jahr}");
        _f.Hinweis("Die Werte stehen auf der Lohnsteuerbescheinigung (kommt im Februar vom Arbeitgeber). Am einfachsten: Bescheinigung hochladen – die Werte werden automatisch übernommen.");
        Hochladen("Lohnsteuerbescheinigung hochladen", "lohnsteuerbescheinigung");
        Kopf();
        Feld("Bruttoarbeitslohn (Zeile 3)", "", p => Geld(p.Bruttolohn, v => p.Bruttolohn = v));
        Feld("Einbehaltene Lohnsteuer (4)", "", p => Geld(p.Lohnsteuer, v => p.Lohnsteuer = v));
        Feld("Solidaritätszuschlag (5)", "", p => Geld(p.Soli, v => p.Soli = v));
        Feld("Kirchensteuer (6/7)", "", p => Geld(p.Kirchensteuer, v => p.Kirchensteuer = v));
        Feld("Rentenversicherung AN-Anteil (23a)", "", p => Geld(p.RentenversicherungAN, v => p.RentenversicherungAN = v));
        Feld("Krankenversicherung AN-Anteil (25)", "", p => Geld(p.KrankenversicherungAN, v => p.KrankenversicherungAN = v));
        Feld("Pflegeversicherung AN-Anteil (26)", "", p => Geld(p.PflegeversicherungAN, v => p.PflegeversicherungAN = v));
        Feld("Arbeitslosenversicherung AN-Anteil (27)", "", p => Geld(p.ArbeitslosenversicherungAN, v => p.ArbeitslosenversicherungAN = v));
        Feld("Anspruch auf Krankengeld", "fast immer ja bei Angestellten", p => Haken("ja", p.KrankengeldAnspruch, v => p.KrankengeldAnspruch = v));
        Tipp("Fahrten zur Arbeit und Homeoffice-Tage trägst du im Bereich „Werbungskosten“ ein.");
        BelegListe();
    }

    private void Rente()
    {
        var a = App.Angaben;
        _f.Ueberschrift($"Rente {App.Jahr}");
        _f.Hinweis("Die Werte stehen in der Rentenbezugsmitteilung („Information über die Meldung an die Finanzverwaltung“, bei der Rentenversicherung kostenlos anforderbar). Hochladen genügt – die Werte werden automatisch übernommen.");
        Hochladen("Rentenbescheinigung hochladen", "rente");
        Kopf();
        Feld("Rente im Jahr (brutto) €", "Jahresbetrag vor Abzug der Krankenkasse", p => Geld(p.Rente, v => p.Rente = v));
        Feld("Rentenbeginn (Jahr)", "bestimmt den steuerpflichtigen Anteil", p => Ganz(p.Rentenbeginn, 2100, v => p.Rentenbeginn = v));
        Feld("Krankenversicherung der Rentner €", "eigener Anteil im Jahr", p => Geld(p.RentnerKv, v => p.RentnerKv = v));
        Feld("Pflegeversicherung der Rentner €", "", p => Geld(p.RentnerPv, v => p.RentnerPv = v));
        var beginn = a.Ich.Rentenbeginn > 0 ? a.Ich.Rentenbeginn : App.Jahr;
        Tipp($"Steuerpflichtig ist nur ein Teil der Rente – bei Rentenbeginn {beginn}: {Steuer.Besteuerungsanteil(beginn):0.#} %. Liegt alles zusammen unter dem Grundfreibetrag ({Fmt.Euro0(Steuer.Grundfreibetrag(App.Jahr))}), fällt meist keine Steuer an.");
        BelegListe();
    }

    private void Vermietung()
    {
        var d = App.Daten;
        var pr = d.Profil;
        _f.Ueberschrift("Vermietete Immobilien (Anlage V)");
        _f.Hinweis("Für jede vermietete Wohnung bzw. jedes Haus ein Objekt anlegen. Mieten und Kosten erfasst du als Belege und wählst dort das Objekt aus. Die Gebäude-AfA rechnet SteuerPilot automatisch.");
        foreach (var o in pr.Mietobjekte.ToList())
        {
            var on = Felder.Text(270, o.Name);
            on.Box.TextChanged += (_, _) =>
            {
                foreach (var b in d.Belege.Where(b => b.Objekt == o.Name && b.Kat.Gruppe == Gruppe.Vermietung)) b.Objekt = on.Text;
                o.Name = on.Text;
                Geaendert();
            };
            var weg = new FlatButton("Entfernen");
            weg.Click += (_, _) =>
            {
                if (MessageBox.Show(Main, $"Objekt „{o.Name}“ entfernen? Die Belege bleiben erhalten.", "Mietobjekt", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                pr.Mietobjekte.Remove(o);
                NeuAufbauen();
            };
            _f.Zeile("Bezeichnung", on, weg);
            var adr = Felder.Text(270, o.Adresse);
            adr.Box.TextChanged += (_, _) => { o.Adresse = adr.Text; Geaendert(); };
            _f.Zeile("Adresse", adr);
            var afaInfo = Make.Label("", Ui.Small, muted: true);
            void Afa() => afaInfo.Text = $"AfA {o.Satz:0.##} % → {Fmt.Euro(Steuer.GebaeudeAfa(o.Gebaeudekosten, o.Anschaffung, o.Satz, App.Jahr))} in {App.Jahr}";
            _f.Zeile("Gebäudekosten (ohne Grundstück) €", "Kaufpreis + Nebenkosten, nur Gebäudeanteil", Geld(o.Gebaeudekosten, v => { o.Gebaeudekosten = v; Afa(); }));
            var kauf = Felder.Datum(o.Anschaffung);
            kauf.Width = Ui.S(130);
            kauf.ValueChanged += (_, _) => { o.Anschaffung = kauf.Value.Date; Afa(); Geaendert(); };
            _f.Zeile("Gekauft / fertiggestellt am", kauf);
            _f.Zeile("Baujahr", "vor 1925: 2,5 %, ab 2023: 3 %, sonst 2 %", Ganz(o.Baujahr, 2100, v => { o.Baujahr = v; Afa(); }));
            Afa();
            _f.Zeile("", afaInfo);
        }
        var neu = new FlatButton("+ Mietobjekt hinzufügen");
        neu.Click += (_, _) =>
        {
            pr.Mietobjekte.Add(new Mietobjekt { Name = $"Wohnung {pr.Mietobjekte.Count + 1}", Anschaffung = new DateTime(App.Jahr, 1, 1) });
            pr.HatVermietung = true;
            NeuAufbauen();
        };
        _f.Element(neu);
        var objekte = Auswertung.Objekte(pr, Auswertung.Posten(d, App.Jahr), App.Jahr);
        if (objekte.Count > 0)
        {
            _f.Ueberschrift($"Ergebnis {App.Jahr}");
            foreach (var o in objekte)
                Summe(o.Name, o.Ergebnis, $"Einnahmen {Fmt.Euro(o.Einnahmen)} − Kosten {Fmt.Euro(o.Ausgaben)} − AfA {Fmt.Euro(o.Afa)}");
        }
        BelegListe();
    }

    private void Kapital()
    {
        var pr = App.Daten.Profil;
        var pausch = pr.SparerPauschbetrag;
        _f.Ueberschrift("Konten, Depots & Sparpläne (Anlage KAP)");
        _f.Hinweis($"Lade die Jahressteuerbescheinigungen der Banken hoch – Erträge und einbehaltene Steuer werden ausgelesen. Steuerfrei sind {pausch:N0} € im Jahr (Sparer-Pauschbetrag).");
        Hochladen("Bankbescheinigung hochladen", "kapitalertraege");
        foreach (var g in pr.Geldanlagen.ToList())
        {
            var gn = Felder.Text(270, g.Name);
            gn.Box.TextChanged += (_, _) => { g.Name = gn.Text; Geaendert(); };
            var weg = new FlatButton("Entfernen");
            weg.Click += (_, _) =>
            {
                if (MessageBox.Show(Main, $"„{g.Name}“ entfernen? Die Belege bleiben erhalten.", "Konto / Depot", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                pr.Geldanlagen.Remove(g);
                NeuAufbauen();
            };
            _f.Zeile("Bank / Konto / Depot", gn, weg);
            _f.Zeile("Freistellungsauftrag €", Geld(g.Freistellungsauftrag, v => g.Freistellungsauftrag = v));
            _f.Zeile("Sparplan pro Monat €", "0 = kein Sparplan", Geld(g.SparplanMonat, v => g.SparplanMonat = v));
        }
        var neuG = new FlatButton("+ Konto / Depot hinzufügen");
        neuG.Click += (_, _) => { pr.Geldanlagen.Add(new Geldanlage { Name = $"Bank {pr.Geldanlagen.Count + 1}" }); pr.HatKapital = true; NeuAufbauen(); };
        _f.Element(neuG);
        if (pr.Geldanlagen.Count > 0)
        {
            var fsa = pr.Geldanlagen.Sum(g => g.Freistellungsauftrag);
            var monat = pr.Geldanlagen.Sum(g => g.SparplanMonat);
            Tipp($"Freistellungsaufträge zusammen: {fsa:N0} € von {pausch:N0} €" + (fsa > pausch ? " – zu hoch, bitte bei einer Bank kürzen." : fsa < pausch ? $" – {pausch - fsa:N0} € sind noch frei." : " – passt.") +
                 (monat > 0 ? $"\nSparpläne: {monat:N2} € im Monat = {monat * 12:N2} € im Jahr. Steuer fällt erst beim Verkauf an." : ""));
        }
        var s = Auswertung.Schaetzen(App.Daten, App.Jahr);
        var kap = s.Zeilen.Where(z => z.Text.Contains("Kapitalerträge") || z.Text.Contains("Sparer-Pauschbetrag") || z.Text.Contains("Abgeltungsteuer") || z.Text.Contains("Kapitalerträge (pers")).ToList();
        if (kap.Count > 0)
        {
            _f.Ueberschrift($"Ergebnis {App.Jahr}");
            foreach (var z in kap) Summe(z.Text.Trim(), z.Betrag, z.Hinweis);
            foreach (var h in s.Hinweise.Where(h => h.Contains("Kapital") || h.Contains("Freistellung"))) Tipp(h);
        }
        BelegListe();
    }

    private void Werbungskosten()
    {
        _f.Ueberschrift("Arbeitsweg & Homeoffice");
        Kopf();
        Feld("Entfernung Wohnung – Arbeit (km, einfach)", "", p => Ganz(p.EntfernungKm, 500, v => p.EntfernungKm = v));
        Feld("Tage im Büro / an der Arbeitsstätte", "typisch 200–230", p => Ganz(p.Arbeitstage, 366, v => p.Arbeitstage = v));
        Feld("Mit eigenem Auto", "ohne Auto max. 4.500 €", p => Haken("ja", p.MitEigenemAuto, v => p.MitEigenemAuto = v));
        Feld("Homeoffice-Tage", "6 € pro Tag, max. 210 Tage", p => Ganz(p.HomeofficeTage, 366, v => p.HomeofficeTage = v));
        Tipp($"Erst wenn alle Werbungskosten zusammen über {Steuer.ArbeitnehmerPauschbetrag:N0} € liegen, bringen sie mehr als die Pauschale. Arbeitsmittel, Fachliteratur, Fortbildungen fügst du als Belege hinzu.");
        BelegListe();
    }

    private void Kinder()
    {
        var pr = App.Daten.Profil;
        _f.Ueberschrift("Kinder");
        _f.Zeile("Kinder (mit Kindergeld)", Ganz(pr.Kinder, 15, v => pr.Kinder = v));
        if (!pr.Splitting) _f.Zeile("Alleinerziehend", "Entlastungsbetrag 4.260 € + 240 € je weiteres Kind", Haken("ja", pr.Alleinerziehend, v => pr.Alleinerziehend = v));
        Tipp("Kita, Tagesmutter und Hort: 80 % der Kosten (max. 4.800 € je Kind) sind absetzbar – nur per Überweisung bezahlt. Kinderfreibeträge prüft das Finanzamt automatisch.");
        BelegListe();
    }

    private void Allgemein()
    {
        var posten = Auswertung.Posten(App.Daten, App.Jahr).Where(p => Bereiche.Gehoert(Bereich, p.Beleg)).ToList();
        if (posten.Count > 0)
        {
            _f.Ueberschrift($"Summe {App.Jahr}");
            Summe("Steuerlich angesetzt", posten.Sum(p => p.Ansatz), $"{posten.Count} Posten");
        }
        var tipp = Bereich switch
        {
            Bereich.Sonderausgaben => "Kranken- und Pflegeversicherung aus Lohn oder Rente sind schon eingerechnet. Hier kommen z. B. Haftpflicht-, Unfall- oder private Versicherungen, Spenden und Kirchensteuer-Nachzahlungen dazu.",
            Bereich.Belastungen => "Krankheitskosten (Zuzahlungen, Brille, Zahnersatz) wirken erst über der zumutbaren Belastung – trotzdem alle Belege sammeln.",
            Bereich.Haushalt => "Handwerker und haushaltsnahe Dienste: 20 % der Arbeitskosten werden direkt von der Steuer abgezogen – nur bei Überweisung, nicht bar.",
            Bereich.Selbststaendig => "Einnahmen und Ausgaben aus einer selbstständigen Tätigkeit oder einem Nebengewerbe als Belege erfassen – der Gewinn wird automatisch berechnet.",
            _ => "",
        };
        if (tipp.Length > 0) Tipp(tipp);
        BelegListe();
    }

    private void Weitere()
    {
        var d = App.Daten;
        var pr = d.Profil;
        var a = App.Angaben;
        _f.Ueberschrift("Persönliches");
        var name = Felder.Text(270, pr.Name);
        name.Box.PlaceholderText = "Vorname";
        name.Box.TextChanged += (_, _) => { pr.Name = name.Text; Geaendert(); };
        _f.Zeile("Dein Name", "erscheint in der Begrüßung", name);
        var veranlagung = new Segmented("Einzelveranlagung", "Zusammenveranlagung (verheiratet)") { SelectedIndex = (int)pr.Veranlagung };
        veranlagung.SelectedIndexChanged += (_, _) => { pr.Veranlagung = (Veranlagung)veranlagung.SelectedIndex; NeuAufbauen(); };
        _f.Zeile("Veranlagung", veranlagung);
        if (pr.Splitting)
        {
            var partner = Felder.Text(270, pr.PartnerName);
            partner.Box.TextChanged += (_, _) => { pr.PartnerName = partner.Text; Geaendert(); };
            _f.Zeile("Name Ehe-/Lebenspartner", partner);
        }
        var land = Felder.Auswahl(270, Profil.Bundeslaender);
        land.SelectedItem = pr.Bundesland;
        land.SelectedIndexChanged += (_, _) => { pr.Bundesland = (string)land.SelectedItem!; Geaendert(); };
        _f.Zeile("Bundesland", land);
        _f.Zeile("Kirchensteuerpflichtig", "8 % in BY/BW, sonst 9 %", Haken("ja", pr.Kirchensteuer, v => pr.Kirchensteuer = v));

        _f.Ueberschrift("Was trifft auf dich zu?");
        _f.Hinweis("Nur angehakte Bereiche erscheinen im Fortschritt und bei „Weiter“ – so bleibt es übersichtlich.");
        _f.Zeile("Ich bekomme", Haken("Rente", pr.HatRente, v => pr.HatRente = v, true), Haken("Lohn / Gehalt", pr.HatLohn, v => pr.HatLohn = v, true));
        _f.Zeile("", Haken("Mieteinnahmen", pr.HatVermietung, v => pr.HatVermietung = v, true), Haken("Zinsen, Aktien, ETFs", pr.HatKapital, v => pr.HatKapital = v, true));
        _f.Zeile("", Haken("Selbstständig / Nebengewerbe", pr.HatSelbststaendig, v => pr.HatSelbststaendig = v, true));

        _f.Ueberschrift("Sonstiges");
        _f.Zeile("Geleistete Einkommensteuer-Vorauszahlungen", Geld(a.Vorauszahlungen, v => a.Vorauszahlungen = v));
        _f.Hinweis("Alles wird automatisch gespeichert.");
    }
}
