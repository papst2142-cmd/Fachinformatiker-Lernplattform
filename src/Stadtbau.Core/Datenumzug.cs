using Lernplattform.Core.Update;

namespace Stadtbau.Core;

/// <summary>
/// Sammelliste 250 (Weisung P12-107): bis S 0.25.0 lag der Datenordner des Werkzeugs unter Windows in
/// %LOCALAPPDATA%\Fachinformatiker-Stadtbau, also im Installationsordner von Velopack (Setup und Deinstallation leeren ihn).
/// Seit S 0.25.1 liegt er unter %APPDATA% (<see cref="Werkzeugordner.StandardPfad"/>). Beim ersten Start mit dem neuen
/// Ordner werden die Daten aus dem alten Ordner KOPIERT: nie geloescht, nie ueberschrieben (eine Datei, die im neuen
/// Ordner schon liegt, bleibt), eine Zeile in update.log. Danach eine Merkdatei, damit es genau einmal geschieht;
/// scheitert eine Datei, fehlt die Merkdatei und der naechste Start versucht die fehlenden Dateien erneut.
/// </summary>
public static class Datenumzug
{
    /// <summary>Ordner, die mitkommen (alles andere im alten Ordner gehoert Velopack: current, packages, Update.exe ...).</summary>
    public static readonly string[] Ordner = ["projekte", "blueprints", "sicherungen", "exporte"];

    /// <summary>Einzelne Dateien, die mitkommen.</summary>
    public static readonly string[] Dateien = ["einstellungen.json", "fehler.log", "update.log"];

    /// <summary>Merkdatei im neuen Ordner: der Umzug ist erledigt.</summary>
    public const string Merkdatei = "umzug_aus_installationsordner.txt";

    /// <summary>Ergebnis fuer Tests und das Protokoll.</summary>
    public sealed record Ergebnis(bool Gelaufen, int Kopiert, int SchonDa, int Fehler);

    /// <summary>Unter Windows beim Start (Program.Main): alter Ordner = Installationsordner, neuer = Datenordner.</summary>
    public static Ergebnis Windows(Werkzeugordner neu) =>
        Ausfuehren(Werkzeugordner.InstallationWindows(Environment.GetFolderPath), neu, DateTime.Now);

    /// <summary>Kopiert die Daten von alt nach neu (siehe Klasse). Wirft nie.</summary>
    public static Ergebnis Ausfuehren(string alt, Werkzeugordner neu, DateTime jetzt)
    {
        try
        {
            var altVoll = Path.GetFullPath(alt).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var neuVoll = Path.GetFullPath(neu.Wurzel).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(altVoll, neuVoll, StringComparison.OrdinalIgnoreCase) || File.Exists(Path.Combine(neuVoll, Merkdatei)))
            {
                return new Ergebnis(false, 0, 0, 0);
            }
            if (!Directory.Exists(altVoll) || !Ordner.Any(o => Directory.Exists(Path.Combine(altVoll, o))) && !Dateien.Any(d => File.Exists(Path.Combine(altVoll, d))))
            {
                // nichts zu holen (neue Installation): keine Merkdatei, damit spaeter dort abgelegte Daten noch kommen
                return new Ergebnis(false, 0, 0, 0);
            }
            Directory.CreateDirectory(neuVoll);
            int kopiert = 0, schonDa = 0, fehler = 0;
            void Kopieren(string von, string nach)
            {
                if (File.Exists(nach))
                {
                    schonDa++;
                    return;
                }
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(nach)!);
                    // overwrite: false = nie ueberschreiben, auch wenn die Datei eben erst entstanden ist
                    File.Copy(von, nach, overwrite: false);
                    File.SetLastWriteTimeUtc(nach, File.GetLastWriteTimeUtc(von));
                    kopiert++;
                }
                catch (IOException) when (File.Exists(nach))
                {
                    schonDa++;
                }
                catch (Exception x) when (x is IOException or UnauthorizedAccessException)
                {
                    fehler++;
                    neu.Fehler("Datenumzug: " + x.GetType().Name + " bei " + Path.GetFileName(von));
                }
            }
            foreach (var datei in Dateien)
            {
                var von = Path.Combine(altVoll, datei);
                if (File.Exists(von))
                {
                    Kopieren(von, Path.Combine(neuVoll, datei));
                }
            }
            foreach (var ordner in Ordner)
            {
                var quelle = Path.Combine(altVoll, ordner);
                if (!Directory.Exists(quelle))
                {
                    continue;
                }
                foreach (var von in Directory.EnumerateFiles(quelle, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                {
                    Kopieren(von, Path.Combine(neuVoll, ordner, Path.GetRelativePath(quelle, von)));
                }
            }
            Updateprotokoll.Schreiben(Texte.UmzugProtokoll(kopiert, schonDa, fehler), neuVoll, jetzt);
            if (fehler == 0)
            {
                File.WriteAllText(Path.Combine(neuVoll, Merkdatei), Stempel(jetzt) + " | kopiert " + kopiert + ", schon da " + schonDa + "\n");
            }
            return new Ergebnis(true, kopiert, schonDa, fehler);
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            neu.Fehler("Datenumzug: " + x.GetType().Name);
            return new Ergebnis(false, 0, 0, 1);
        }
    }

    private static string Stempel(DateTime jetzt) => jetzt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
}
