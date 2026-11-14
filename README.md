# SteuerPilot

Deutsche Steuer-App für Windows 10/11 – Belege fotografieren oder scannen, automatisch einsortieren und die Einkommensteuer schätzen. Alles läuft offline auf deinem PC.

## Installieren

PowerShell öffnen (Windows-Taste → „PowerShell“) und einfügen:

```powershell
irm https://raw.githubusercontent.com/Dodovking/SteuerPilot/main/install.ps1 | iex
```

Oder `SteuerPilot.exe` unter [Releases](https://github.com/Dodovking/SteuerPilot/releases/latest) herunterladen und starten (keine Installation nötig). Beim ersten Start zeigt Windows evtl. „Der Computer wurde durch Windows geschützt“ → **Weitere Informationen → Trotzdem ausführen** (die EXE ist nicht signiert).

## Funktionen

- **Eigener Steuerordner** (Standard: `Dokumente\SteuerPilot`, frei wählbar, z. B. in OneDrive) mit automatischer Sortierung: `2025\02 Werbungskosten\Arbeitsmittel\2025-03-14_Laptop_1.199,00EUR.jpg`
- **Eingang**: Fotos vom Handy oder PDFs einfach in `Eingang` legen – sie erscheinen sofort in der App und werden beim Erfassen einsortiert
- **Scannen** direkt aus der App (Windows-Bilderfassung/WIA, jeder normale Scanner)
- **Texterkennung (OCR)** mit der eingebauten Windows-Erkennung: Betrag, Datum, Händler und passende Kategorie werden vorgeschlagen
- **40+ Kategorien** nach Anlagen (N, Vorsorgeaufwand, Sonderausgaben, agB, § 35a, EÜR, V) mit Tipps
- **Steuer-Schätzung**: Lohnsteuerbescheinigung eintragen → Erstattung/Nachzahlung inkl. Pendlerpauschale, Homeoffice, Vorsorge, Spenden, Krankheitskosten (zumutbare Belastung), Handwerker, Abschreibungen, Splitting, Soli, Kirchensteuer
- **Rechner**: Einkommensteuer, Steuerersparnis einer Ausgabe, Pendlerpauschale, Homeoffice, AfA-Plan, Mehrwertsteuer, zumutbare Belastung, § 35a, Kinderbetreuung
- **Export**: Steuerbericht (HTML, druckbar als PDF) als Ausfüllhilfe für ELSTER, CSV für Excel, Steuerpaket-ZIP für den Steuerberater, Komplettsicherung
- Hell/Dunkel-Design, automatische Updates

> Vereinfachte Schätzung ohne Gewähr – ersetzt keine Steuerberatung. Verbindlich ist nur der Steuerbescheid.

## Entwickeln

```bash
dotnet test
dotnet publish src/SteuerPilot.Gui -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
tools/release.sh "Was ist neu"   # Test + Build + GitHub-Release
```
