using SteuerPilot.Core;

namespace SteuerPilot.Gui;

/// <summary>Persönliche Angaben und die Werte der Lohnsteuerbescheinigung je Steuerjahr.</summary>
public sealed class AngabenPage : Page
{
    private readonly Formular _f = new();
    private readonly System.Windows.Forms.Timer _save = new() { Interval = 600 };
    private (int Jahr, SteuerDaten? Daten, bool Splitting) _geladen;
    private bool _laden;

    public override string Titel => "Meine Angaben";

    public AngabenPage(MainForm main, App app) : base(main, app)
    {
        Controls.Add(_f);
        _save.Tick += (_, _) => { _save.Stop(); App.Speichern(); };
        VisibleChanged += (_, _) => { if (!Visible && _save.Enabled) { _save.Stop(); App.Speichern(); } };
    }

    private void Geaendert()
    {
        if (_laden) return;
        _save.Stop();
        _save.Start();
    }

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

    private CheckBox Haken(string text, bool wert, Action<bool> setzen)
    {
        var c = Felder.Haken(text, wert);
        c.Width = Ui.S(130);
        c.AutoSize = false;
        c.Height = Ui.S(28);
        c.CheckedChanged += (_, _) => { setzen(c.Checked); Geaendert(); };
        return c;
    }

    public override void Aktualisieren()
    {
        var d = App.Daten;
        if (_geladen == (App.Jahr, d, d.Profil.Splitting)) return;
        _geladen = (App.Jahr, d, d.Profil.Splitting);
        _laden = true;
        _f.SuspendLayout();
        _f.Leeren();
        var pr = d.Profil;
        _f.Ueberschrift("Persönliches");
        var name = Felder.Text(270, pr.Name);
        name.Box.TextChanged += (_, _) => { pr.Name = name.Text; Geaendert(); };
        _f.Zeile("Dein Name", name);
        var veranlagung = new Segmented("Einzelveranlagung", "Zusammenveranlagung (verheiratet)") { SelectedIndex = (int)pr.Veranlagung };
        veranlagung.SelectedIndexChanged += (_, _) =>
        {
            pr.Veranlagung = (Veranlagung)veranlagung.SelectedIndex;
            _save.Stop();
            App.Speichern();
            BeginInvoke(Aktualisieren);
        };
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
        _f.Zeile("Kinder (mit Kindergeld)", Ganz(pr.Kinder, 15, v => pr.Kinder = v));
        if (!pr.Splitting) _f.Zeile("Alleinerziehend", "Entlastungsbetrag 4.260 € + 240 € je weiteres Kind", Haken("ja", pr.Alleinerziehend, v => pr.Alleinerziehend = v));

        var a = d.Angaben(App.Jahr);
        _f.Ueberschrift($"Lohnsteuerbescheinigung {App.Jahr}");
        _f.Hinweis("Die Werte stehen auf der Lohnsteuerbescheinigung (bekommst du im Februar vom Arbeitgeber, auch in ELSTER abrufbar). Nummern = Zeile auf der Bescheinigung.");
        var personen = pr.Splitting ? new[] { (pr.PersonName(Person.Ich), a.Ich), (pr.PersonName(Person.Partner), a.Partner) } : [(pr.PersonName(Person.Ich), a.Ich)];
        if (pr.Splitting)
        {
            var k1 = Make.Label(personen[0].Item1, Ui.Bold, width: Ui.S(130));
            var k2 = Make.Label(personen[1].Item1, Ui.Bold, width: Ui.S(130));
            _f.Zeile("", k1, k2);
        }
        void Feld(string label, string hinweis, Func<PersonAngaben, Control> make) => _f.Zeile(label, hinweis, personen.Select(p => make(p.Item2)).ToArray());
        Feld("Bruttoarbeitslohn (3)", "", p => Geld(p.Bruttolohn, v => p.Bruttolohn = v));
        Feld("Einbehaltene Lohnsteuer (4)", "", p => Geld(p.Lohnsteuer, v => p.Lohnsteuer = v));
        Feld("Solidaritätszuschlag (5)", "", p => Geld(p.Soli, v => p.Soli = v));
        Feld("Kirchensteuer (6/7)", "", p => Geld(p.Kirchensteuer, v => p.Kirchensteuer = v));
        Feld("Rentenversicherung AN-Anteil (23a)", "", p => Geld(p.RentenversicherungAN, v => p.RentenversicherungAN = v));
        Feld("Krankenversicherung AN-Anteil (25)", "", p => Geld(p.KrankenversicherungAN, v => p.KrankenversicherungAN = v));
        Feld("Pflegeversicherung AN-Anteil (26)", "", p => Geld(p.PflegeversicherungAN, v => p.PflegeversicherungAN = v));
        Feld("Arbeitslosenversicherung AN-Anteil (27)", "", p => Geld(p.ArbeitslosenversicherungAN, v => p.ArbeitslosenversicherungAN = v));
        Feld("Anspruch auf Krankengeld", "fast immer ja bei Angestellten", p => Haken("ja", p.KrankengeldAnspruch, v => p.KrankengeldAnspruch = v));

        _f.Ueberschrift("Arbeitsweg & Homeoffice");
        Feld("Entfernung Wohnung – Arbeit (km, einfach)", "", p => Ganz(p.EntfernungKm, 500, v => p.EntfernungKm = v));
        Feld("Tage im Büro / an der Arbeitsstätte", "typisch 200–230", p => Ganz(p.Arbeitstage, 366, v => p.Arbeitstage = v));
        Feld("Mit eigenem Auto", "ohne Auto max. 4.500 €", p => Haken("ja", p.MitEigenemAuto, v => p.MitEigenemAuto = v));
        Feld("Homeoffice-Tage", "6 € pro Tag, max. 210 Tage", p => Ganz(p.HomeofficeTage, 366, v => p.HomeofficeTage = v));

        _f.Ueberschrift("Vermietete Immobilien (Anlage V)");
        _f.Hinweis("Für jede vermietete Wohnung bzw. jedes Haus ein Objekt anlegen. Mieten und Kosten erfasst du als Belege der Gruppe „Vermietung“ und wählst dort das Objekt aus. Die Gebäude-AfA rechnet SteuerPilot automatisch.");
        void NeuAufbauen() { _save.Stop(); App.Speichern(); _geladen = default; BeginInvoke(Aktualisieren); }
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
            var kosten = Geld(o.Gebaeudekosten, v => { o.Gebaeudekosten = v; Afa(); });
            _f.Zeile("Gebäudekosten (ohne Grundstück) €", "Kaufpreis + Nebenkosten, nur Gebäudeanteil", kosten);
            var kauf = Felder.Datum(o.Anschaffung);
            kauf.Width = Ui.S(130);
            kauf.ValueChanged += (_, _) => { o.Anschaffung = kauf.Value.Date; Afa(); Geaendert(); };
            _f.Zeile("Gekauft / fertiggestellt am", kauf);
            var bj = Ganz(o.Baujahr, 2100, v => { o.Baujahr = v; Afa(); });
            _f.Zeile("Baujahr", "vor 1925: 2,5 %, ab 2023: 3 %, sonst 2 %", bj);
            Afa();
            _f.Zeile("", afaInfo);
        }
        var neu = new FlatButton("+ Mietobjekt hinzufügen");
        neu.Click += (_, _) =>
        {
            pr.Mietobjekte.Add(new Mietobjekt { Name = $"Wohnung {pr.Mietobjekte.Count + 1}", Anschaffung = new DateTime(App.Jahr, 1, 1) });
            NeuAufbauen();
        };
        _f.Element(neu);

        _f.Ueberschrift("Sonstiges");
        _f.Zeile("Geleistete Einkommensteuer-Vorauszahlungen", Geld(a.Vorauszahlungen, v => a.Vorauszahlungen = v));
        _f.Hinweis("Alles wird automatisch gespeichert. Ausgaben (Arbeitsmittel, Spenden, Versicherungen …) erfasst du als Belege.");
        _f.Abschluss();
        Theme.Apply(_f);
        Felder.Einfaerben(_f);
        _f.ResumeLayout();
        _laden = false;
    }
}
