namespace SteuerPilot.Gui;

/// <summary>Scrollbares Formular, das Zeilen von oben nach unten stapelt (Label links, Felder rechts).</summary>
public sealed class Formular : Panel
{
    private int _y;
    public int LabelBreite { get; set; } = Ui.S(300);

    public Formular()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
    }

    public void Leeren()
    {
        foreach (Control c in Controls.Cast<Control>().ToList()) c.Dispose();
        Controls.Clear();
        AutoScrollPosition = Point.Empty;
        _y = 0;
    }

    public Label Ueberschrift(string text)
    {
        if (_y > 0) _y += Ui.S(18);
        var l = Make.Label(text, Ui.Section);
        l.Location = new Point(0, _y);
        Controls.Add(l);
        _y += l.PreferredHeight + Ui.S(8);
        return l;
    }

    public Label Hinweis(string text, int breite = 760)
    {
        var l = Make.Label(text, Ui.Small, muted: true, width: Ui.S(breite));
        l.Location = new Point(0, _y);
        Controls.Add(l);
        _y += l.PreferredHeight + Ui.S(8);
        return l;
    }

    public void Zeile(string label, params Control[] felder) => Zeile(label, "", felder);

    public void Zeile(string label, string hinweis, params Control[] felder)
    {
        int h = Math.Max(Ui.S(30), felder.Length == 0 ? 0 : felder.Max(f => f is Label l ? l.PreferredHeight : f.Height));
        var lb = Make.Label(label);
        lb.Location = new Point(0, _y + (h - lb.PreferredHeight) / 2);
        Controls.Add(lb);
        int x = LabelBreite;
        foreach (var f in felder)
        {
            int fh = f is Label l ? l.PreferredHeight : f.Height;
            f.Location = new Point(x, _y + (h - fh) / 2);
            Controls.Add(f);
            x += (f is Label l2 ? l2.PreferredWidth : f.Width) + Ui.S(10);
        }
        if (hinweis.Length > 0)
        {
            var hl = Make.Label(hinweis, Ui.Small, muted: true);
            hl.Location = new Point(x + Ui.S(4), _y + (h - hl.PreferredHeight) / 2);
            Controls.Add(hl);
        }
        _y += h + Ui.S(8);
    }

    public void Element(Control c)
    {
        c.Location = new Point(0, _y);
        Controls.Add(c);
        _y += c.Height + Ui.S(8);
    }

    public void Abschluss() => Controls.Add(new Panel { Location = new Point(0, _y), Size = new Size(1, Ui.S(24)) });
}
