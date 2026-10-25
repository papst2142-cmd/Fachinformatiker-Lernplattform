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
        // PROJEKTARBEITEN aus dem Lernteil wie fisi_core.load_content (laeuft im Hintergrund, Warten ist hier erlaubt)
        var lernen = Programm.Zustand.Inhalte.GetAwaiter().GetResult();
        var projekte = lernen?.Testprojekte.Select(p => Py.AusKnoten(p.Daten)).ToList();
        var inhalt = SpielInhalt.Laden(name => Lesen("inhalte/spiel/" + name + ".json"), projekte);
        var daten = Py.Lesen(Muss("inhalte_csharp/spiel/stadt.json")) as PyObjekt ?? throw new InvalidDataException("stadt.json ist kein Objekt");
        var stadt = new Stadt(inhalt, daten);
        var innenraeume = Py.Lesen(Muss("inhalte_csharp/spiel/innenraeume.json")) as PyObjekt;
        var suche = Programm.Zustand.Suchtext.GetAwaiter().GetResult();
        // Geraet im Spielprotokoll wie fg.Game(app.db, device="PC"/"Handy") (fisi_game_gui 2863, spiel.py 1801)
        var geraet = Pc ? "PC" : "Handy";
        var geschaefte = Py.Lesen(Muss("inhalte_csharp/spiel/geschaefte.json")) as PyObjekt;
        return new SpielSitzung(stadt, GrafikLaden(), innenraeume, lernen, suche, Programm.Zustand.Schreiber, geraet, geschaefte);
    }

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
    public static StadtSpiel Neu(Stadt stadt, GrafikBibliothek? grafik, PyObjekt? innenraeume)
    {
        var ablauf = new Spielablauf(stadt.Inhalt);
        ablauf.Profil(ProbeName, new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        return new StadtSpiel(stadt, ablauf, grafik, innenraeume);
    }
}
