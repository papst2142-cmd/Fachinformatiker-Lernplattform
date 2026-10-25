using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Spiel;

/// <summary>
/// Phase 7, W4: Spielinhalte aus dem Programm selbst laden (inhalte/spiel wie Python 0.63.2 und
/// inhalte_csharp/spiel, PC und Handy gleich) und die Stadt mit einem Spielstand im Arbeitsspeicher
/// starten. Bis die Oberflaeche zum Anlegen der Spielfigur kommt (W6), legt die Probe eine feste
/// Figur an; nichts davon wird gespeichert.
/// </summary>
public static class SpielStart
{
    /// <summary>Name der Probe-Figur (wird nirgends gezeigt und nie gespeichert).</summary>
    public const string ProbeName = "Probe";

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

    public static StadtSpiel Laden()
    {
        // PROJEKTARBEITEN aus dem Lernteil wie fisi_core.load_content (laeuft im Hintergrund, Warten ist hier erlaubt)
        var lernen = Programm.Zustand.Inhalte.GetAwaiter().GetResult();
        var projekte = lernen?.Testprojekte.Select(p => Py.AusKnoten(p.Daten)).ToList();
        var inhalt = SpielInhalt.Laden(name => Lesen("inhalte/spiel/" + name + ".json"), projekte);
        var daten = Py.Lesen(Muss("inhalte_csharp/spiel/stadt.json")) as PyObjekt ?? throw new InvalidDataException("stadt.json ist kein Objekt");
        var stadt = new Stadt(inhalt, daten);
        var buero = ProbeWelt.BueroBauen(name => Muss("inhalte/spiel/" + name + ".json"));
        return Neu(stadt, buero);
    }

    /// <summary>Neuer Spielstand (nur im Arbeitsspeicher) mit der Probe-Figur, Tag 1 um 07:00 zu Hause.</summary>
    public static StadtSpiel Neu(Stadt stadt, ProbeFlaeche buero)
    {
        var ablauf = new Spielablauf(stadt.Inhalt);
        ablauf.Profil(ProbeName, new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        return new StadtSpiel(stadt, ablauf, buero);
    }
}
