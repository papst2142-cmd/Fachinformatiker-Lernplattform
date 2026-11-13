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

    /// <summary>
    /// Engine Stufe 2 Teil 1 (Weisung P12-176, Sammelliste 286): Formatnummer von aktiv.json (Feld <see cref="FormatFeld"/>).
    /// Fehlt das Feld, gilt Format 1 (Dateien bis A 0.62.0 bleiben lesbar); eine groessere Nummer ist ein klarer Fehler.
    /// </summary>
    public const long Format = 1;

    /// <summary>Name des Felds mit der Formatnummer (keine Stadt kann so heissen).</summary>
    public const string FormatFeld = "format";

    private static readonly AsyncLocal<bool> FormatAlt = new();

    /// <summary>Gegenprobe Sammelliste 286: das Feld „format“ wird wie bis A 0.62.0 nicht gelesen (nur in Tests).</summary>
    public static bool FormatAltFuerTests
    {
        get => FormatAlt.Value;
        set => FormatAlt.Value = value;
    }

    /// <summary>Formatnummer von aktiv.json; fehlt das Feld: 1. Keine ganze Zahl, kleiner 1 oder neuer als <see cref="Format"/> = Fehler.</summary>
    public static long FormatVon(PyObjekt aktiv)
    {
        if (FormatAlt.Value || !aktiv.Any(p => p.Key == FormatFeld))
        {
            return 1;
        }
        if (!Py.IstNurInt(aktiv[FormatFeld]) || Py.Int(aktiv[FormatFeld]) < 1)
        {
            throw new StadtbauFehler(["aktiv.json: die Formatnummer ist keine ganze Zahl ab 1."]);
        }
        var nummer = Py.Int(aktiv[FormatFeld]);
        return nummer <= Format ? nummer
            : throw new StadtbauFehler([$"aktiv.json hat Format {nummer}, dieses Programm kennt nur Format {Format}. Bitte das Programm aktualisieren."]);
    }

    /// <summary>Nummer der geltenden Fassung einer Stadt laut aktiv.json.</summary>
    public static long Nummer(PyObjekt aktiv, string stadtId) =>
        stadtId != FormatFeld && aktiv.Any(p => p.Key == stadtId) && Py.IstNurInt(aktiv[stadtId])
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
        FormatVon(aktiv);
        return LadenNummer(lesen, Nummer(aktiv, stadtId), stadtId);
    }

    /// <summary>
    /// W4b (Weisung P12-228): liest genau Fassung <paramref name="nummer"/> einer Stadt (ohne aktiv.json), mit derselben
    /// Pruefung von Kennung und Nummer; fuer die Grundlage des Werkzeugs (Fassung 0) und Tests, die Fassung 0 pruefen.
    /// </summary>
    public static StadtFassung LadenNummer(Func<string, byte[]?> lesen, long nummer, string stadtId = Stadt1)
    {
        var name = StadtFassung.Dateiname(stadtId, nummer);
        var fassung = StadtFassung.Lesen(lesen(Ordner + name) ?? throw new StadtbauFehler([$"Die Fassung {name} fehlt."]));
        if (fassung.StadtId != stadtId || fassung.Nummer != nummer)
        {
            throw new StadtbauFehler([$"{name} enthält Stadt „{fassung.StadtId}“ Fassung {fassung.Nummer}."]);
        }
        return fassung;
    }
}
