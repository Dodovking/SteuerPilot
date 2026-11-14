namespace SteuerPilot.Gui;

/// <summary>Scannt über die Windows-Bilderfassung (WIA) – funktioniert mit praktisch allen Flachbett- und Einzugsscannern.</summary>
public static class Scanner
{
    private const string FormatJpeg = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";

    /// <summary>Öffnet den Windows-Scan-Dialog und speichert das Ergebnis unter <paramref name="ziel"/>. Liefert false bei Abbruch.</summary>
    public static bool Scannen(string ziel)
    {
        var type = Type.GetTypeFromProgID("WIA.CommonDialog")
                   ?? throw new InvalidOperationException("Die Windows-Bilderfassung (WIA) ist auf diesem PC nicht verfügbar.");
        dynamic dialog = Activator.CreateInstance(type)!;
        dynamic? image;
        try
        {
            // 1 = ScannerDeviceType, 1 = ColorIntent, 131072 = MinimizeSize
            image = dialog.ShowAcquireImage(1, 1, 131072, FormatJpeg, false, true, false);
        }
        catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x80210015)
        {
            throw new InvalidOperationException("Es wurde kein Scanner gefunden. Ist er eingeschaltet und per USB/WLAN verbunden?");
        }
        if (image == null) return false;
        if (File.Exists(ziel)) File.Delete(ziel);
        image.SaveFile(ziel);
        return true;
    }

    /// <summary>Öffnet „Windows-Fax und -Scan“ bzw. die Store-App „Windows-Scan“ als Alternative.</summary>
    public static void ScanAppOeffnen()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("wfs.exe") { UseShellExecute = true });
        }
        catch
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-windows-store://pdp/?productid=9WZDNCRFJ3PV") { UseShellExecute = true });
        }
    }
}
