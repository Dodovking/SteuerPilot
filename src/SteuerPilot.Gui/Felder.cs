namespace SteuerPilot.Gui;

public static class Felder
{
    public static ComboBox Auswahl(int breite, params object[] items)
    {
        var c = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = Ui.Base,
            Width = Ui.S(breite),
            MaxDropDownItems = 20,
            BackColor = Theme.P.Input,
            ForeColor = Theme.P.Text,
        };
        c.Items.AddRange(items);
        if (c.Items.Count > 0) c.SelectedIndex = 0;
        return c;
    }

    public static DateTimePicker Datum(DateTime wert) => new()
    {
        Format = DateTimePickerFormat.Short,
        Value = wert,
        Width = Ui.S(140),
        Font = Ui.Base,
    };

    public static InputBox Text(int breite, string wert = "", bool rechts = false)
    {
        var b = new InputBox { Width = Ui.S(breite), Text = wert };
        if (rechts) b.Box.TextAlign = HorizontalAlignment.Right;
        return b;
    }

    public static CheckBox Haken(string text, bool wert) => new()
    {
        Text = text,
        Checked = wert,
        AutoSize = true,
        Font = Ui.Base,
        BackColor = Color.Transparent,
        ForeColor = Theme.P.Text,
    };

    /// <summary>Färbt ComboBoxen, CheckBoxen und Datumsfelder passend zum Design.</summary>
    public static void Einfaerben(Control root)
    {
        foreach (Control c in root.Controls)
        {
            switch (c)
            {
                case ComboBox cb: cb.BackColor = Theme.P.Input; cb.ForeColor = Theme.P.Text; break;
                case CheckBox ch when ch is not ToggleSwitch: ch.ForeColor = Theme.P.Text; break;
                case DateTimePicker dp: dp.CalendarForeColor = Theme.P.Text; dp.CalendarMonthBackground = Theme.P.Input; break;
                case ListBox lb: lb.BackColor = Theme.P.Input; lb.ForeColor = Theme.P.Text; break;
            }
            Einfaerben(c);
        }
    }
}
