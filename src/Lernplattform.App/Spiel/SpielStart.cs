using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Spiel;

/// <summary>
/// Phase 7, W4: Spielinhalte aus dem Programm selbst laden (inhalte/spiel wie Python 0.63.2 und
/// inhalte_csharp/spiel, PC und Handy gleich) und die Stadt mit einem Spielstand im Arbeitsspeicher
/// starten. W6: <see cref="Laden"/> liefert die Sitzung mit dem Spielstand aus lernplattform.db;
/// <see cref="Neu"/> bleibt die Probe im Arbeitsspeicher (Hintergrund ohne Spielfigur, Start-Test).
/// </summary>
public static class SpielStart
{
    /// <summary>Name der Probe-Figur (wird nirgends gezeigt und nie gespeichert).</summary>
    public const string ProbeName = "Probe";

    private static readonly bool Pc = !OperatingSystem.IsAndroid();

    public static byte[]? Lesen(string pfad)
    {
        using var strom = typeof(SpielStart).Assembly.GetManifestResourceStream(pfad);
        if (strom is null)
        {
            return null;
        }
        using var kopie = new MemoryStream();
        strom.CopyTo(kopie);
        return kopie.ToArray();
    }

    private static byte[] Muss(string pfad) => Lesen(pfad) ?? throw new FileNotFoundException(pfad);

    public static SpielSitzung Laden()
    {
        var (inhalt, lernen) = InhaltLaden();
        // Phase 13 Welle 1 (Weisung P12-16): Stadt und Innenraeume aus der geltenden Stadt-Fassung (aktiv.json);
        // die Karte (orte) hat schon InhaltLaden aus derselben Fassung genommen
        var fassung = Fassung();
        var stadt = fassung.StadtFuer(inhalt);
        var innenraeume = fassung.Innenraeume;
        var suche = Programm.Zustand.Suchtext.GetAwaiter().GetResult();
        // Geraet im Spielprotokoll wie fg.Game(app.db, device="PC"/"Handy") (fisi_game_gui 2863, spiel.py 1801)
        var geraet = Pc ? "PC" : "Handy";
        var geschaefte = Py.Lesen(Muss("inhalte_csharp/spiel/geschaefte.json")) as PyObjekt;
        var feste = Py.Lesen(Muss("inhalte_csharp/spiel/festzeiten.json")) as PyObjekt;
        var sitzung = new SpielSitzung(stadt, GrafikLaden(), innenraeume, lernen, suche, Programm.Zustand.Schreiber, geraet, geschaefte, feste);
        Aktiv = sitzung;
        return sitzung;
    }

    /// <summary>
    /// Phase 11 W1 (Sammelliste 180): die zuletzt geladene Sitzung der Spielseite (null, solange das Spiel nicht
    /// betreten wurde). Optionen > Loeschen setzt ueber ihre Verwaltung zurueck, damit die Spielseite danach
    /// denselben Stand zeigt (Python nutzt dasselbe Game-Objekt der Spielansicht).
    /// </summary>
    public static SpielSitzung? Aktiv { get; private set; }

    /// <summary>
    /// Phase 11 W1: eine Verwaltung der Spielstaende fuer Optionen > Loeschen, ohne Sitzung eine neue (laeuft im
    /// Hintergrund; danach nur auf dem Oberflaechen-Faden benutzen). null, wenn das Spiel nicht geladen werden kann.
    /// </summary>
    public static Spielverwaltung? LoeschVerwaltung()
    {
        if (Aktiv is { } sitzung)
        {
            return sitzung.Verwaltung;
        }
        try
        {
            Spielschlange.AlleLeeren();
            _optionenInhalt ??= InhaltLaden().Inhalt;
            var geraet = Pc ? "PC" : "Handy";
            return new Spielverwaltung(Programm.Zustand.Schreiber, geraet, _optionenInhalt);
        }
        catch (Exception fehler) when (fehler is IOException or InvalidDataException or Microsoft.Data.Sqlite.SqliteException or SpielFehler or KeyNotFoundException)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben("Optionen > Loeschen: Spiel nicht geladen: " + fehler.GetType().Name);
            return null;
        }
    }

    /// <summary>
    /// Phase 11 W1: active_label fuer den Text zu „Spielstand zurücksetzen“ (nach game.reload(), wie _show_difficulty);
    /// "" ohne Platz oder wenn das Spiel nicht geladen werden kann. Laeuft im Hintergrund, ohne die Sitzung anzufassen.
    /// </summary>
    public static string PlatzAnzeige()
    {
        try
        {
            Spielschlange.AlleLeeren();
            _optionenInhalt ??= InhaltLaden().Inhalt;
            var geraet = Pc ? "PC" : "Handy";
            return new Spielverwaltung(Programm.Zustand.Schreiber, geraet, _optionenInhalt).AktivAnzeige();
        }
        catch (Exception fehler) when (fehler is IOException or InvalidDataException or Microsoft.Data.Sqlite.SqliteException or SpielFehler or KeyNotFoundException)
        {
            return "";
        }
    }

    /// <summary>
    /// Phase 13 Welle 1 (Weisung P12-16): die geltende Fassung von Stadt 1 (<c>inhalte_csharp/spiel/stadtbau/</c>), einmal je
    /// Programmlauf gelesen und im Aufbau geprueft (<see cref="StadtbauPruefung.Format"/>, Pruefsumme).
    /// </summary>
    public static StadtFassung Fassung()
    {
        lock (FassungSperre)
        {
            return _fassung ??= MitVerbautPruefen(AktiveFassung.Laden(Lesen));
        }
    }

    /// <summary>
    /// Phase 13 Welle 4a (Weisung P12-28 Punkt 1): jedes Stueck aus dem Katalog, auf das die Fassung verweist, muss
    /// mitgeliefert sein (<see cref="StadtbauPruefung.Verbaut"/>); sonst laedt die Fassung nicht (roter Fund, nie still weg).
    /// </summary>
    private static StadtFassung MitVerbautPruefen(StadtFassung fassung)
    {
        var funde = StadtbauPruefung.Verbaut(fassung, GrafikBibliothek.Aus(KatalogVerbaut()));
        return funde.Count == 0 ? fassung : throw new StadtbauFehler(funde);
    }

    private static readonly Lock FassungSperre = new();

    private static StadtFassung? _fassung;

    /// <summary>
    /// Spielinhalte (inhalte/spiel) und die Lerninhalte, aus denen sie die Testprojekte nehmen (laeuft im Hintergrund).
    /// Phase 13 Welle 1: der Abschnitt "orte" (Karte) kommt aus der geltenden Stadt-Fassung und durchlaeuft dieselbe
    /// Ladepruefung wie vorher orte.json.
    /// </summary>
    private static (PyObjekt Inhalt, Core.Inhalte.Lerninhalte? Lernen) InhaltLaden()
    {
        var karte = PythonJson.Utf8(PythonJson.Standard(Fassung().Karte));
        // PROJEKTARBEITEN aus dem Lernteil wie fisi_core.load_content (laeuft im Hintergrund, Warten ist hier erlaubt)
        // Phase 9 W2 (Nico 5c): das Spiel liest nur die bisherigen Inhalte aus inhalte/, gleich in jeder Fachrichtung
        var lernen = Programm.Zustand.Inhalte.GetAwaiter().GetResult()?.Bisher;
        var projekte = lernen?.Testprojekte.Select(p => Py.AusKnoten(p.Daten)).ToList();
        var inhalt = SpielInhalt.Laden(name => name == "orte" ? karte : Lesen("inhalte/spiel/" + name + ".json"), projekte);
        // Phase 12 Welle 1: die Moebel nur der C#-Linie (geschaefte.json moebel_neu, z. B. die vier Kleiderschraenke)
        return (Geschaefte.NeueMoebelEinfuegen(inhalt, Py.Lesen(Muss("inhalte_csharp/spiel/geschaefte.json")) as PyObjekt), lernen);
    }

    /// <summary>
    /// Phase 9 W4 (Sammelliste 178): Zeile der Karte "Spiel" in den Optionen wie slot_options_text nach game.reload()
    /// ("Platz 1 · Schwierigkeitsgrad: Normal · ändern nur durch „Spielstand zurücksetzen“"). Liest den Spielstand
    /// aus lernplattform.db (vorher werden alle Spielschritte geschrieben), ohne das laufende Spiel anzufassen.
    /// "" wie Python, wenn das Spiel nicht geladen werden kann. Laeuft im Hintergrund.
    /// </summary>
    public static string OptionenZeile()
    {
        try
        {
            Spielschlange.AlleLeeren();
            _optionenInhalt ??= InhaltLaden().Inhalt;
            var geraet = Pc ? "PC" : "Handy";
            return new Spielverwaltung(Programm.Zustand.Schreiber, geraet, _optionenInhalt).OptionenZeile();
        }
        catch (Exception fehler) when (fehler is IOException or InvalidDataException or Microsoft.Data.Sqlite.SqliteException or SpielFehler or KeyNotFoundException)
        {
            return "";
        }
    }

    private static PyObjekt? _optionenInhalt;

    /// <summary>balancing.json des Spiels (Kaution in Prozent fuer den Erklaertext der Karte "Spiel").</summary>
    public static PyObjekt Balancing() => Py.Lesen(Muss("inhalte/spiel/balancing.json")) as PyObjekt ?? throw new InvalidDataException("balancing.json ist kein Objekt");

    /// <summary>
    /// CC0-Modelle (inhalte_csharp/grafik, Weisung P7-9 Nachtrag 3/4). Fehlen sie oder stimmt die
    /// Pruefsumme nicht, zeichnet die Stadt eigene Geometrie (das Spiel geht weiter); der Grund steht
    /// in <see cref="GrafikFehler"/> (Start-Test).
    /// </summary>
    public static GrafikBibliothek? GrafikLaden()
    {
        try
        {
            GrafikFehler = null;
            // Phase 13 Welle 4a (Weisung P12-28): dazu die mitgelieferten Stuecke aus dem Katalog des Stadtbau-Werkzeugs
            return GrafikBibliothek.Laden(Muss("inhalte_csharp/grafik/modelle.bin"),
                System.Text.Encoding.UTF8.GetString(Muss("inhalte_csharp/grafik/modelle.json")),
                System.Text.Encoding.UTF8.GetString(Muss("inhalte_csharp/grafik/zuordnung.json"))).Mit(KatalogVerbaut());
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            GrafikFehler = e.Message;
            return null;
        }
    }

    /// <summary>
    /// Phase 13 Welle 4a: die mitgelieferten Stuecke aus dem Katalog (<c>inhalte_csharp/grafik/katalog_verbaut.bin</c>, nur
    /// Verbautes, Erzeuger <c>werkzeuge/stadtbau/verbaut.py</c>), Pruefsumme aus <c>katalog_verbaut.json</c>.
    /// </summary>
    public static List<GrafikModell> KatalogVerbaut() =>
        GrafikBibliothek.KatalogLaden(Muss("inhalte_csharp/grafik/katalog_verbaut.bin"),
            System.Text.Encoding.UTF8.GetString(Muss("inhalte_csharp/grafik/katalog_verbaut.json")));

    /// <summary>Warum die CC0-Modelle nicht geladen wurden (null = geladen oder noch nicht versucht).</summary>
    public static string? GrafikFehler { get; private set; }

    /// <summary>Neuer Spielstand (nur im Arbeitsspeicher) mit der Probe-Figur, Tag 1 um 07:00 zu Hause.</summary>
    /// <remarks>Phase 8 W6: <paramref name="feste"/> = festzeiten.json, <paramref name="festzeit"/> und (W9) <paramref name="jahreszeit"/> wie bei <see cref="StadtSpiel"/>.</remarks>
    public static StadtSpiel Neu(Stadt stadt, GrafikBibliothek? grafik, PyObjekt? innenraeume, PyObjekt? geschaefte = null,
        PyObjekt? feste = null, Func<string?>? festzeit = null, Func<Jahreszeit>? jahreszeit = null)
    {
        var ablauf = new Spielablauf(stadt.Inhalt);
        ablauf.Profil(ProbeName, new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        return new StadtSpiel(stadt, ablauf, grafik, innenraeume, geschaefte, feste, festzeit, jahreszeit);
    }
}
