using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

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
        var daten = Py.Lesen(Muss("inhalte_csharp/spiel/stadt.json")) as PyObjekt ?? throw new InvalidDataException("stadt.json ist kein Objekt");
        var stadt = new Stadt(inhalt, daten);
        var innenraeume = Py.Lesen(Muss("inhalte_csharp/spiel/innenraeume.json")) as PyObjekt;
        var suche = Programm.Zustand.Suchtext.GetAwaiter().GetResult();
        // Geraet im Spielprotokoll wie fg.Game(app.db, device="PC"/"Handy") (fisi_game_gui 2863, spiel.py 1801)
        var geraet = Pc ? "PC" : "Handy";
        var geschaefte = Py.Lesen(Muss("inhalte_csharp/spiel/geschaefte.json")) as PyObjekt;
        var feste = Py.Lesen(Muss("inhalte_csharp/spiel/festzeiten.json")) as PyObjekt;
        return new SpielSitzung(stadt, GrafikLaden(), innenraeume, lernen, suche, Programm.Zustand.Schreiber, geraet, geschaefte, feste);
    }

    /// <summary>Spielinhalte (inhalte/spiel) und die Lerninhalte, aus denen sie die Testprojekte nehmen (laeuft im Hintergrund).</summary>
    private static (PyObjekt Inhalt, Core.Inhalte.Lerninhalte? Lernen) InhaltLaden()
    {
        // PROJEKTARBEITEN aus dem Lernteil wie fisi_core.load_content (laeuft im Hintergrund, Warten ist hier erlaubt)
        // Phase 9 W2 (Nico 5c): das Spiel liest nur die bisherigen Inhalte aus inhalte/, gleich in jeder Fachrichtung
        var lernen = Programm.Zustand.Inhalte.GetAwaiter().GetResult()?.Bisher;
        var projekte = lernen?.Testprojekte.Select(p => Py.AusKnoten(p.Daten)).ToList();
        return (SpielInhalt.Laden(name => Lesen("inhalte/spiel/" + name + ".json"), projekte), lernen);
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
            return GrafikBibliothek.Laden(Muss("inhalte_csharp/grafik/modelle.bin"),
                System.Text.Encoding.UTF8.GetString(Muss("inhalte_csharp/grafik/modelle.json")),
                System.Text.Encoding.UTF8.GetString(Muss("inhalte_csharp/grafik/zuordnung.json")));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            GrafikFehler = e.Message;
            return null;
        }
    }

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
