using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pruefung.Echt;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.94.0 (Weisung P12-304): gemeinsame Hilfen fuer die Tests der eingebauten Pruefungssaetze. Die Datei traegt seit
/// A 0.94.0 Format 3 (FIAE-Satzpaare), seit A 1.8.0 Format 4 (neue Fassungen, Klassen- und Use-Case-Diagramm, Weisung P12-372),
/// seit A 1.12.0 Format 5 (Struktogramm, Weisung P12-390);
/// vor den neuen Saetzen ist sie sonst bytegleich mit dem Stand davor.
/// </summary>
internal static class EchtTestDatei
{
    public static readonly string Ordner = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen");

    public static readonly string Datei = Path.Combine(Ordner, EchtSaetze.Datei);

    /// <summary>
    /// A 1.20.0 (Format 6, Weisung P12-446): die Datei nach L0 bis A 1.19.1, bytegleich (Format 5). Die heutige Datei ist sie mit
    /// Format 6 im Kopf und den angehaengten neuen Fassungen (<see cref="EchtSchreibtischTests"/>).
    /// </summary>
    public static readonly string Format5Pfad = Path.Combine(Pfade.Wurzel, "tests", "Lerninhalte", "echt", "pruefungssaetze_format5.json");

    public static byte[] NachL0 => File.ReadAllBytes(Format5Pfad);

    private static readonly byte[] Kopf5 = "{\n \"format\": 5,"u8.ToArray();

    /// <summary>
    /// Kopie der Datei mit einer kleineren Formatzahl statt 5 (Stelle 13, sonst nichts geaendert): damit pruefen die Tests der
    /// Wellen davor weiter, dass ihr Teil der Datei bytegleich geblieben ist. Rot, wenn der Kopf nicht genau Format 5 ist.
    /// </summary>
    public static byte[] AlsFormat(byte[] roh, int format)
    {
        Assert.True(roh.AsSpan().StartsWith(Kopf5), "Kopf der Datei ist nicht genau Format 5");
        Assert.InRange(format, 1, 4);
        var kopie = (byte[])roh.Clone();
        kopie[Kopf5.Length - 2] = (byte)('0' + format);
        return kopie;
    }

    /// <summary>Wie <see cref="AlsFormat"/> mit Format 2 (Anfangs-Pruefsummen der Wellen bis A 0.93.0).</summary>
    public static byte[] AlsFormat2(byte[] roh) => AlsFormat(roh, 2);

    /// <summary>
    /// A 1.18.0 (L0): die Datei wie vor dem Loeschen, bytegleich (<see cref="L0Vorher"/>). Die Tests der Wellen davor
    /// (nur angehaengt, alte Fassungen, Gegenproben) rechnen weiter auf ihr; die heutige Datei ist sie ohne die Saetze der
    /// Loeschliste (<see cref="EchtL0Tests"/>).
    /// </summary>
    public static byte[] Vorher => L0Vorher.Datei("pruefungssaetze");

    /// <summary>Kennungen, die lernen/ausschluss.json VOR L0 ausschloss (wie im Programm bis A 1.17.0, auf <see cref="Vorher"/>).</summary>
    public static IReadOnlyList<string> Ausgeschlossen() =>
        new LernenOrdner(n => n == Lerninhalte.AusschlussDatei ? L0Vorher.Ausschluss
            : n == EchtSaetze.DateiName ? Vorher
            : n == Lerninhalte.GeloeschtDatei ? null
            : File.Exists(Path.Combine(Ordner, n + ".json")) ? File.ReadAllBytes(Path.Combine(Ordner, n + ".json")) : null)
            .AusgeschlosseneSaetze();

    /// <summary>Geloeschte Saetze (Kennung, Fassung) laut Loeschliste der App (wie im Programm seit A 1.18.0).</summary>
    public static IReadOnlyList<(string Kennung, int Fassung)> Geloescht() =>
        new LernenOrdner(n => File.Exists(Path.Combine(Ordner, n + ".json")) ? File.ReadAllBytes(Path.Combine(Ordner, n + ".json")) : null)
            .GeloeschteSaetze();

    /// <summary>
    /// Jeder Satz hat eine eigene Firma; nur die zwei Saetze eines FIAE-Satzpaars (aufbau "paar", gleiche Nummer) teilen sie.
    /// Gegenprobe in <see cref="EchtInhalteAePaareTests"/>.
    /// </summary>
    public static void FirmaJeSatzOderPaar(IReadOnlyList<EchtSatz> alle)
    {
        foreach (var gruppe in alle.GroupBy(s => s.Firma, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var zwei = gruppe.Select(s => s.Kennung).Order(StringComparer.Ordinal).ToList();
            Assert.True(zwei.Count == 2 && gruppe.All(s => s.Paar) && EchtSaetze.Partner(zwei[0]) == zwei[1],
                "Firma " + gruppe.Key + " in " + string.Join(", ", zwei));
        }
    }

    /// <summary>Bestand wie in der App bis A 1.17.0: Datei vor L0, die ausgeschlossenen nicht mehr angeboten.</summary>
    public static EchtBestand App() => EchtSaetze.Laden(Vorher, Ausgeschlossen());

    /// <summary>Bestand wie in der App seit A 1.18.0: heutige Datei, kein Ausschluss mehr, Loeschliste fuer offene Laeufe.</summary>
    public static EchtBestand AppHeute() => EchtSaetze.Laden(File.ReadAllBytes(Datei), [], Geloescht());
}
