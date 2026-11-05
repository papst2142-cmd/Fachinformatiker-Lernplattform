using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Phase 13 Welle 1 (Weisung P12-16): welche Fassung je Stadt gilt, steht in <c>inhalte_csharp/spiel/stadtbau/aktiv.json</c>
/// (Stadt-Kennung -&gt; Nummer, Schluessel mit "_" sind Hinweise). Eine neue Fassung kommt nur angehaengt als eigene Datei
/// dazu; die alte bleibt Byte fuer Byte erhalten (Plan Abschnitt 6). Nico 3a: die geltende Fassung zeichnet das Spiel fuer
/// alle Spielstaende.
/// </summary>
public static class AktiveFassung
{
    /// <summary>Ordner der Fassungen (Name der eingebetteten Dateien im Programm und Pfad im Repo).</summary>
    public const string Ordner = "inhalte_csharp/spiel/stadtbau/";

    /// <summary>Datei mit der geltenden Fassung je Stadt.</summary>
    public const string Datei = "aktiv.json";

    /// <summary>Die erste Stadt (heute die einzige).</summary>
    public const string Stadt1 = "stadt1";

    /// <summary>Nummer der geltenden Fassung einer Stadt laut aktiv.json.</summary>
    public static long Nummer(PyObjekt aktiv, string stadtId) =>
        aktiv.Any(p => p.Key == stadtId) && Py.IstNurInt(aktiv[stadtId])
            ? Py.Int(aktiv[stadtId])
            : throw new StadtbauFehler([$"aktiv.json nennt keine Fassung für „{stadtId}“."]);

    /// <summary>
    /// Liest die geltende Fassung einer Stadt ueber <paramref name="lesen"/> (Pfad relativ zum Repo bzw. Name der
    /// eingebetteten Datei, null = fehlt) und prueft ihren Aufbau; Kennung und Nummer muessen zum Dateinamen passen.
    /// </summary>
    public static StadtFassung Laden(Func<string, byte[]?> lesen, string stadtId = Stadt1)
    {
        var aktiv = Py.Lesen(lesen(Ordner + Datei) ?? throw new StadtbauFehler(["aktiv.json fehlt."])) as PyObjekt
            ?? throw new StadtbauFehler(["aktiv.json ist kein Objekt."]);
        var nummer = Nummer(aktiv, stadtId);
        var name = StadtFassung.Dateiname(stadtId, nummer);
        var fassung = StadtFassung.Lesen(lesen(Ordner + name) ?? throw new StadtbauFehler([$"Die Fassung {name} fehlt."]));
        if (fassung.StadtId != stadtId || fassung.Nummer != nummer)
        {
            throw new StadtbauFehler([$"{name} enthält Stadt „{fassung.StadtId}“ Fassung {fassung.Nummer}."]);
        }
        return fassung;
    }
}
