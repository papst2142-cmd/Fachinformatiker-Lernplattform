using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pruefung.Echt;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.94.0 (Weisung P12-304): gemeinsame Hilfen fuer die Tests der eingebauten Pruefungssaetze. Die Datei traegt seit
/// A 0.94.0 Format 3 (AE-Satzpaare); vor den neuen Saetzen ist sie sonst bytegleich mit dem Stand A 0.93.0.
/// </summary>
internal static class EchtTestDatei
{
    public static readonly string Ordner = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen");

    public static readonly string Datei = Path.Combine(Ordner, EchtSaetze.Datei);

    private static readonly byte[] Kopf3 = "{\n \"format\": 3,"u8.ToArray();

    /// <summary>
    /// Kopie der Datei mit der Formatzahl 2 statt 3 (Stelle 13, sonst nichts geaendert): damit pruefen die Tests der Wellen
    /// bis A 0.93.0 weiter, dass ihr Teil der Datei bytegleich geblieben ist. Rot, wenn der Kopf nicht genau Format 3 ist.
    /// </summary>
    public static byte[] AlsFormat2(byte[] roh)
    {
        Assert.True(roh.AsSpan().StartsWith(Kopf3), "Kopf der Datei ist nicht genau Format 3");
        var kopie = (byte[])roh.Clone();
        kopie[Kopf3.Length - 2] = (byte)'2';
        return kopie;
    }

    /// <summary>Kennungen, die lernen/ausschluss.json der App ausschliesst (wie im Programm).</summary>
    public static IReadOnlyList<string> Ausgeschlossen() =>
        new LernenOrdner(n => File.Exists(Path.Combine(Ordner, n + ".json")) ? File.ReadAllBytes(Path.Combine(Ordner, n + ".json")) : null)
            .AusgeschlosseneSaetze();

    /// <summary>
    /// Jeder Satz hat eine eigene Firma; nur die zwei Saetze eines AE-Satzpaars (aufbau "paar", gleiche Nummer) teilen sie.
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

    /// <summary>Bestand wie in der App: alle Saetze geladen, die ausgeschlossenen nicht mehr angeboten.</summary>
    public static EchtBestand App() => EchtSaetze.Laden(File.ReadAllBytes(Datei), Ausgeschlossen());
}
