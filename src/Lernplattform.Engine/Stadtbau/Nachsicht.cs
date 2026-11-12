namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle A 0.67.0 (Weisung P12-204, E2): EIN Schalter „nachsichtig“ fuer alle Ansichten des Werkzeugs (3D-Ansicht, Grenzen
/// live, Quest-Szene, Szene fuer Laternen und Tueren). Solange er gilt, bricht eine halb fertige Stadt die Ansicht nicht ab:
/// fehlerhafte Quests und Questgeber fallen nur fuer die Ansicht weg (<see cref="Questebene.Anwenden"/>), eine Laterne ohne
/// freien Platz wird ausgelassen (<see cref="Spiel.Probe.StadtSzene.LaternenOhnePlatzAuslassen"/>). Ersetzt die einzelnen
/// Schalter (Parameter in den Ansichten, <c>Nachtlicht.OhnePlatzAuslassen</c>). Das Spiel setzt ihn nie, die Pruefungen des
/// Werkzeugs auch nicht (sie melden jeden Fehler rot). Gilt im eigenen Ablauf (AsyncLocal), nebenher laufende Arbeit bleibt
/// streng.
/// </summary>
public static class Nachsicht
{
    private static readonly AsyncLocal<bool> Wert = new();

    /// <summary>Gilt gerade eine Werkzeug-Ansicht?</summary>
    public static bool An => Wert.Value;

    /// <summary>Fuer die Dauer des Blocks nachsichtig (<c>using (Nachsicht.Werkzeugansicht()) { … }</c>).</summary>
    public static IDisposable Werkzeugansicht()
    {
        var vorher = Wert.Value;
        Wert.Value = true;
        return new Zurueck(vorher);
    }

    private sealed class Zurueck(bool vorher) : IDisposable
    {
        public void Dispose() => Wert.Value = vorher;
    }
}
