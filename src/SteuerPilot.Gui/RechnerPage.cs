using SteuerPilot.Core;

namespace SteuerPilot.Gui;

/// <summary>Einzelne Steuer-Rechner, unabhängig von den erfassten Belegen.</summary>
public sealed class RechnerPage : Page
{
    private readonly FlowLayoutPanel _flow = new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };
    private bool _gebaut;

    public override string Titel => "Rechner";

    public RechnerPage(MainForm main, App app) : base(main, app) => Controls.Add(_flow);

    private sealed class Rechner : Card
    {
        private int _y;
        private readonly Label _ergebnis;
        private readonly Func<string> _rechnen;

        public Rechner(string titel, string info, Func<string> rechnen)
        {
            _rechnen = rechnen;
            Width = Ui.S(400);
            Margin = new Padding(0, 0, Ui.S(14), Ui.S(14));
            var t = Make.Label(titel, Ui.CardTitle);
            t.Location = new Point(Ui.S(18), Ui.S(14));
            Controls.Add(t);
            _y = t.Bottom + Ui.S(2);
            var i = Make.Label(info, Ui.Small, muted: true, width: Ui.S(364));
            i.Location = new Point(Ui.S(18), _y);
            Controls.Add(i);
            _y = i.Top + i.PreferredHeight + Ui.S(10);
            _ergebnis = Make.Label("", Ui.Base, width: Ui.S(364));
        }

        public T Feld<T>(string label, T c) where T : Control
        {
            var l = Make.Label(label);
            l.Location = new Point(Ui.S(18), _y + (c.Height - l.PreferredHeight) / 2);
            c.Location = new Point(Ui.S(230), _y);
            if (c is not CheckBox) c.Width = Math.Min(c.Width, Ui.S(150));
            Controls.AddRange([l, c]);
            _y += c.Height + Ui.S(6);
            switch (c)
            {
                case InputBox b: b.Box.TextChanged += (_, _) => Rechnen(); break;
                case ComboBox cb: cb.SelectedIndexChanged += (_, _) => Rechnen(); break;
                case CheckBox ch: ch.CheckedChanged += (_, _) => Rechnen(); break;
                case DateTimePicker dp: dp.ValueChanged += (_, _) => Rechnen(); break;
            }
            return c;
        }

        public void Fertig()
        {
            _y += Ui.S(4);
            _ergebnis.Location = new Point(Ui.S(18), _y);
            _ergebnis.Font = Ui.Bold;
            Controls.Add(_ergebnis);
            Rechnen();
        }

        public void Rechnen()
        {
            if (!Controls.Contains(_ergebnis)) return;
            try { _ergebnis.Text = _rechnen(); }
            catch (Exception e) { _ergebnis.Text = e.Message; }
            Height = _ergebnis.Top + _ergebnis.PreferredHeight + Ui.S(18);
        }
    }

    private static InputBox Geld(string wert = "") => Felder.Text(150, wert, rechts: true);
    private ComboBox Jahr() { var c = Felder.Auswahl(100, Steuer.Jahre.Reverse().Cast<object>().ToArray()); c.SelectedItem = Math.Clamp(App.Jahr, Steuer.ErstesJahr, Steuer.LetztesJahr); return c; }
    private static int J(ComboBox c) => (int)c.SelectedItem!;

    private void Bauen()
    {
        var pr = App.Daten.Profil;

        {
            Rechner r = null!;
            InputBox zve = null!; ComboBox jahr = null!; CheckBox split = null!, kirche = null!;
            r = new Rechner("Einkommensteuer", "Tarif nach § 32a EStG inkl. Soli und Kirchensteuer.", () =>
            {
                var z = Fmt.Wert(zve); int j = J(jahr);
                var est = Steuer.Einkommensteuer(z, j, split.Checked);
                var soli = Steuer.Soli(est, j, split.Checked);
                var kist = kirche.Checked ? Steuer.Kirchensteuer(est, pr.KirchensteuerSatz) : 0;
                var sum = est + soli + kist;
                return $"Einkommensteuer {Fmt.Euro(est)}\nSoli {Fmt.Euro(soli)}  ·  Kirchensteuer {Fmt.Euro(kist)}\nGesamt {Fmt.Euro(sum)}\n" +
                       $"Durchschnitt {Fmt.Prozent(z > 0 ? sum / z : 0)}  ·  Grenzsteuersatz {Fmt.Prozent(Steuer.Grenzsteuersatz(z, j, split.Checked))}";
            });
            zve = r.Feld("Zu versteuerndes Einkommen €", Geld("40000"));
            jahr = r.Feld("Steuerjahr", Jahr());
            split = r.Feld("Zusammenveranlagung", Felder.Haken("Splittingtarif", pr.Splitting));
            kirche = r.Feld("Kirchensteuer", Felder.Haken($"{pr.KirchensteuerSatz:0} %", pr.Kirchensteuer));
            r.Fertig(); _flow.Controls.Add(r);
        }
        {
            InputBox ausgabe = null!, zve = null!; CheckBox split = null!;
            var r = new Rechner("Was bringt eine Ausgabe?", "Wie viel Steuer spart z. B. ein neuer Laptop für den Job (Werbungskosten)?", () =>
            {
                var a = Fmt.Wert(ausgabe); var z = Fmt.Wert(zve); int j = App.Jahr;
                decimal Steuern(decimal x) { var e = Steuer.Einkommensteuer(Math.Max(0, x), j, split.Checked); return e + Steuer.Soli(e, j, split.Checked) + (pr.Kirchensteuer ? Steuer.Kirchensteuer(e, pr.KirchensteuerSatz) : 0); }
                var spar = Steuern(z) - Steuern(z - a);
                return $"Ersparnis ca. {Fmt.Euro(spar)}  ({Fmt.Prozent(a > 0 ? spar / a : 0)})\nGilt nur, soweit die Werbungskosten insgesamt über 1.230 € liegen.";
            });
            ausgabe = r.Feld("Ausgabe €", Geld("1000"));
            zve = r.Feld("Zu versteuerndes Einkommen €", Geld("40000"));
            split = r.Feld("Zusammenveranlagung", Felder.Haken("Splittingtarif", pr.Splitting));
            r.Fertig(); _flow.Controls.Add(r);
        }
        {
            NumberBox km = null!, tage = null!; ComboBox jahr = null!; CheckBox auto = null!;
            var r = new Rechner("Pendlerpauschale", "Entfernungspauschale: 0,30 € je km (ab dem 21. km 0,38 €; ab 2026 0,38 € ab dem 1. km).", () =>
                $"Pendlerpauschale {Fmt.Euro(Steuer.Pendlerpauschale(km.Value, tage.Value, J(jahr), auto.Checked))}");
            km = r.Feld("Entfernung (km, einfach)", new NumberBox(0, 500) { Value = 25 });
            tage = r.Feld("Arbeitstage", new NumberBox(0, 366) { Value = 220 });
            jahr = r.Feld("Steuerjahr", Jahr());
            auto = r.Feld("Verkehrsmittel", Felder.Haken("eigenes Auto", true));
            r.Fertig(); _flow.Controls.Add(r);
        }
        {
            NumberBox tage = null!;
            var r = new Rechner("Homeoffice-Pauschale", "6 € pro Tag, an dem überwiegend zu Hause gearbeitet wurde – höchstens 210 Tage (1.260 €).", () =>
                $"Homeoffice-Pauschale {Fmt.Euro(Steuer.Homeoffice(tage.Value))}");
            tage = r.Feld("Homeoffice-Tage", new NumberBox(0, 366) { Value = 100 });
            r.Fertig(); _flow.Controls.Add(r);
        }
        {
            InputBox preis = null!; DateTimePicker kauf = null!; NumberBox jahre = null!;
            var r = new Rechner("Abschreibung (AfA)", "Bis 800 € netto (952 € brutto) sofort absetzbar, Computer & Software sofort (1 Jahr), sonst monatsgenau über die Nutzungsdauer (Handy 5, Möbel 13, Auto 6 Jahre).", () =>
            {
                var plan = Steuer.Abschreibungsplan(Fmt.Wert(preis), kauf.Value, jahre.Value).ToList();
                return string.Join("\n", plan.Select(p => $"{p.Jahr}: {Fmt.Euro(p.Betrag)}"));
            });
            preis = r.Feld("Kaufpreis brutto €", Geld("1500"));
            kauf = r.Feld("Kaufdatum", Felder.Datum(new DateTime(App.Jahr, 7, 1)));
            jahre = r.Feld("Nutzungsdauer (Jahre)", new NumberBox(1, 50) { Value = 3 });
            r.Fertig(); _flow.Controls.Add(r);
        }
        {
            InputBox kosten = null!; DateTimePicker kauf = null!; NumberBox baujahr = null!;
            var r = new Rechner("Gebäude-AfA (Vermietung)", "Abschreibung des Gebäudeanteils einer vermieteten Immobilie: vor 1925 gebaut 2,5 %, ab 2023 fertiggestellt 3 %, sonst 2 % pro Jahr – im Kaufjahr monatsgenau. Grundstück wird nicht abgeschrieben.", () =>
            {
                var k = Fmt.Wert(kosten); var s = Steuer.GebaeudeAfaSatz(baujahr.Value);
                var j = kauf.Value.Year;
                return $"Satz {s:0.##} % · volles Jahr {Fmt.Euro(Steuer.Cent(k * s / 100m))}\n{j}: {Fmt.Euro(Steuer.GebaeudeAfa(k, kauf.Value, s, j))} · {j + 1}: {Fmt.Euro(Steuer.GebaeudeAfa(k, kauf.Value, s, j + 1))}";
            });
            kosten = r.Feld("Gebäudekosten €", Geld("250000"));
            kauf = r.Feld("Gekauft am", Felder.Datum(new DateTime(App.Jahr, 4, 1)));
            baujahr = r.Feld("Baujahr", new NumberBox(1800, 2100) { Value = 1990 });
            r.Fertig(); _flow.Controls.Add(r);
        }
        {
            InputBox ortsueblich = null!, miete = null!;
            var r = new Rechner("Verbilligte Vermietung (z. B. an Angehörige)", "Ab 66 % der ortsüblichen Miete (inkl. Nebenkosten) zählen alle Kosten voll, ab 50 % mit positiver Totalüberschussprognose, darunter nur anteilig.", () =>
            {
                var o = Fmt.Wert(ortsueblich); var m = Fmt.Wert(miete);
                if (o <= 0) return "Ortsübliche Miete eingeben";
                var q = m / o;
                return $"{q:P0} der ortsüblichen Miete\n" + (q >= 0.66m ? "→ Kosten voll absetzbar" : q >= 0.5m ? "→ voll nur mit positiver Prognose, sonst anteilig" : $"→ Kosten nur zu {q:P0} absetzbar");
            });
            ortsueblich = r.Feld("Ortsübliche Warmmiete €/Monat", Geld("900"));
            miete = r.Feld("Verlangte Warmmiete €/Monat", Geld("550"));
            r.Fertig(); _flow.Controls.Add(r);
        }
        {
            InputBox betrag = null!; ComboBox satz = null!;
            var r = new Rechner("Mehrwertsteuer", "Brutto ↔ Netto umrechnen.", () =>
            {
                var b = Fmt.Wert(betrag); var s = satz.SelectedIndex == 0 ? 19m : 7m;
                return $"Als Brutto: netto {Fmt.Euro(Steuer.Netto(b, s))}, MwSt {Fmt.Euro(b - Steuer.Netto(b, s))}\nAls Netto: brutto {Fmt.Euro(Steuer.Brutto(b, s))}, MwSt {Fmt.Euro(Steuer.Brutto(b, s) - b)}";
            });
            betrag = r.Feld("Betrag €", Geld("119"));
            satz = r.Feld("Steuersatz", Felder.Auswahl(100, "19 %", "7 %"));
            r.Fertig(); _flow.Controls.Add(r);
        }
        {
            InputBox gde = null!, kosten = null!; CheckBox split = null!; NumberBox kinder = null!;
            var r = new Rechner("Zumutbare Belastung", "Krankheitskosten & Co. zählen erst über einem Eigenanteil (§ 33 EStG, gestaffelt nach Einkünften).", () =>
            {
                var zb = Steuer.ZumutbareBelastung(Fmt.Wert(gde), split.Checked, kinder.Value);
                return $"Eigenanteil {Fmt.Euro(zb)}\nAbsetzbar {Fmt.Euro(Math.Max(0, Fmt.Wert(kosten) - zb))}";
            });
            gde = r.Feld("Gesamtbetrag der Einkünfte €", Geld("40000"));
            kosten = r.Feld("Krankheitskosten etc. €", Geld("2500"));
            split = r.Feld("Zusammenveranlagung", Felder.Haken("ja", pr.Splitting));
            kinder = r.Feld("Kinder", new NumberBox(0, 15) { Value = pr.Kinder });
            r.Fertig(); _flow.Controls.Add(r);
        }
        {
            InputBox dl = null!, hw = null!, mj = null!;
            var r = new Rechner("Haushaltsnahe Leistungen & Handwerker", "§ 35a: 20 % der Arbeitskosten werden direkt von der Steuer abgezogen (nur unbar bezahlt).", () =>
                $"Steuerermäßigung {Fmt.Euro(Steuer.HaushaltsnaheErmaessigung(Fmt.Wert(dl), Fmt.Wert(hw), Fmt.Wert(mj)))}\nmax. 4.000 € + 1.200 € + 510 €");
            dl = r.Feld("Dienstleistungen (Putzen, Garten) €", Geld("1200"));
            hw = r.Feld("Handwerker-Arbeitskosten €", Geld("800"));
            mj = r.Feld("Minijob im Haushalt €", Geld());
            r.Fertig(); _flow.Controls.Add(r);
        }
        {
            InputBox kosten = null!; NumberBox kinder = null!; ComboBox jahr = null!;
            var r = new Rechner("Kinderbetreuung", "Kita, Tagesmutter, Hort für Kinder unter 14: 2/3 der Kosten (ab 2025 80 %), max. 4.000 € (ab 2025 4.800 €) je Kind.", () =>
                $"Absetzbar {Fmt.Euro(Steuer.Kinderbetreuung(Fmt.Wert(kosten), kinder.Value, J(jahr)))}");
            kosten = r.Feld("Kosten im Jahr €", Geld("3000"));
            kinder = r.Feld("Kinder", new NumberBox(1, 15) { Value = Math.Max(1, pr.Kinder) });
            jahr = r.Feld("Steuerjahr", Jahr());
            r.Fertig(); _flow.Controls.Add(r);
        }
    }

    public override void Aktualisieren()
    {
        if (!_gebaut)
        {
            _gebaut = true;
            _flow.SuspendLayout();
            Bauen();
            _flow.ResumeLayout();
        }
        Theme.Apply(this);
        Felder.Einfaerben(this);
    }
}
