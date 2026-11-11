using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Weisung P12-179 Schritt 2: was die eigene Engine von der Stadt des Spiels lesen darf (nur lesen). Die Stadt des
/// Spiel-Kerns liefert sie (<see cref="Lernplattform.Core.Spiel.Stadt"/>, jedes Glied unveraendert weitergereicht); die Engine
/// kennt die Stadt des Spiels nicht mehr.
/// </summary>
public interface IStadtSicht
{
    /// <summary>Spielinhalt (content) mit der Karte.</summary>
    PyObjekt Inhalt { get; }

    /// <summary>Stadtdaten (stadt.json bzw. Abschnitt stadtdaten der Fassung).</summary>
    PyObjekt Daten { get; }

    /// <summary>Zugang je Ort (Tuer und Weg).</summary>
    PyObjekt Zugaenge { get; }

    /// <summary>Geltende Fassung (null nur in Tests).</summary>
    StadtFassung? Fassung { get; }

    /// <summary>Fester Tag fuer Verkehr und Parkplaetze (Werkzeug 1), sonst null.</summary>
    long? VerkehrTag { get; }

    /// <summary>Parkplaetze voll (Werkzeug), sonst nach Tag.</summary>
    bool ParkplaetzeVoll { get; }

    /// <summary>Kantenlaenge einer Zelle des Laufrasters.</summary>
    double Zelle { get; }

    /// <summary>Alle Orte der Karte (Regeln, ohne Sichtbarkeit).</summary>
    IReadOnlyList<PyObjekt> Regeln { get; }

    /// <summary>Tuerzelle eines Orts, null = keine.</summary>
    (long, long)? Tuer(string id);

    /// <summary>Die sichtbaren Orte fuer einen Spielstand (null = neuer Spielstand).</summary>
    List<PyObjekt> Orte(ISpielSicht? stand);

    /// <summary>Ort, an dem ein neuer Spielstand beginnt (Startpunkt der Pruefung "zu Fuss erreichbar").</summary>
    string StartOrt { get; }
}
