using SteuerPilot.Core;

namespace SteuerPilot.Gui;

/// <summary>Zustand der laufenden App: Einstellungen, Ablageordner, Daten und gewähltes Steuerjahr.</summary>
public sealed class App
{
    public AppSettings Settings { get; }
    public Ablage Ablage { get; private set; }
    public SteuerDaten Daten { get; private set; }
    public event Action? Changed;
    public event Action? JahrChanged;

    public App(AppSettings settings)
    {
        Settings = settings;
        Ablage = new Ablage(settings.Ablageordner ?? Ablage.StandardOrdner());
        Ablage.Einrichten();
        Daten = Speicher.LadeDaten(Ablage);
        if (Settings.Jahr < 2000) Settings.Jahr = StandardJahr();
    }

    public static int StandardJahr() => DateTime.Today.Month <= 9 ? DateTime.Today.Year - 1 : DateTime.Today.Year;

    public int Jahr
    {
        get => Settings.Jahr;
        set
        {
            if (Settings.Jahr == value) return;
            Settings.Jahr = value;
            SettingsSpeichern();
            JahrChanged?.Invoke();
            Changed?.Invoke();
        }
    }

    public Jahresangaben Angaben => Daten.Angaben(Jahr);

    public IEnumerable<int> Jahre()
    {
        var set = new SortedSet<int>(Daten.Belege.Select(b => b.Jahr).Concat(Daten.Jahre.Keys)) { Jahr, DateTime.Today.Year, DateTime.Today.Year - 1 };
        for (int j = Steuer.ErstesJahr; j <= Steuer.LetztesJahr; j++) set.Add(j);
        return set.Reverse();
    }

    public void Speichern()
    {
        Speicher.SpeichereDaten(Ablage, Daten);
        Changed?.Invoke();
    }

    public void SettingsSpeichern() => Speicher.SpeichereEinstellungen(Settings);

    /// <summary>Wechselt auf einen anderen Steuerordner; vorhandene Daten dort werden geladen.</summary>
    public void OrdnerWechseln(string ordner)
    {
        var neu = new Ablage(ordner);
        neu.Einrichten();
        Ablage = neu;
        Daten = Speicher.LadeDaten(neu);
        Settings.Ablageordner = neu.Root;
        SettingsSpeichern();
        Changed?.Invoke();
    }
}
