using System.Globalization;

namespace Stadtbau.Core;

/// <summary>
/// Phase 13 Welle 2 (Plan Abschnitte 3 und 9): Sicherungen je Projekt im Ordner <c>sicherungen/&lt;Name&gt;/</c>, die letzten
/// <see cref="Hoechstens"/> bleiben. Es gibt 20 feste Plaetze (<c>sicherung_01</c> bis <c>sicherung_20</c>), die der Reihe
/// nach belegt werden; die 21. Sicherung kommt auf den Platz der aeltesten. Projekte und Exporte loescht das Werkzeug nie.
/// Seit Welle 4c (Weisung P12-32) auch fuer Blueprints: Ordner <c>sicherungen/(Blueprints)/&lt;Name&gt;/</c>, Endung
/// <see cref="Blueprint.Endung"/> (<see cref="Blueprint.SicherungenFuer"/>).
/// </summary>
public sealed class Sicherungen(Werkzeugordner ablage, string projekt, string endung = Projekt.Endung)
{
    public const int Hoechstens = 20;

    /// <summary>Unterordner der Sicherungen fuer Blueprints (mit Klammern: so kann kein Projekt heissen).</summary>
    public const string BlueprintOrdner = "(Blueprints)";

    public string Ordner => Path.Combine(ablage.Sicherungen, projekt);

    public static string Platz(int nummer) => Platz(nummer, Projekt.Endung);

    public static string Platz(int nummer, string endung) => "sicherung_" + nummer.ToString("00", CultureInfo.InvariantCulture) + endung;

    /// <summary>Neue Sicherung auf den freien oder aeltesten Platz; Rueckgabe: Pfad.</summary>
    public string Anlegen(byte[] daten, DateTime zeit)
    {
        Directory.CreateDirectory(Ordner);
        var plaetze = Enumerable.Range(1, Hoechstens).Select(n => Path.Combine(Ordner, Platz(n, endung))).ToList();
        var ziel = plaetze.FirstOrDefault(p => !File.Exists(p)) ?? plaetze.OrderBy(File.GetLastWriteTimeUtc).ThenBy(p => p, StringComparer.Ordinal).First();
        Werkzeugordner.SicherSchreiben(ziel, daten);
        File.SetLastWriteTimeUtc(ziel, zeit.ToUniversalTime());
        return ziel;
    }

    /// <summary>Alle Sicherungen, neueste zuerst.</summary>
    public List<string> Liste() =>
        Directory.Exists(Ordner)
            ? [.. new DirectoryInfo(Ordner).GetFiles("sicherung_*" + endung).OrderByDescending(f => f.LastWriteTimeUtc).Select(f => f.FullName)]
            : [];
}
