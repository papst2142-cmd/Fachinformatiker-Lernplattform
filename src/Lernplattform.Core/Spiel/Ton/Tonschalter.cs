namespace Lernplattform.Core.Spiel.Ton;

/// <summary>
/// Phase 8 W8 (Teilplan 1.1, Weisung P8-17e Punkt 2): wann der Ton laeuft. Nur auf der Spielseite, nicht im
/// Hintergrund (Android OnPause, PC minimiert) und nur bei Gesamt ueber 0. Kehrt das Programm aus dem Hintergrund
/// zurueck, geht der Ton von selbst wieder an (ein Wiederanlauf), ohne dass man ihn aus- und einschalten muss.
/// Der Schalter sagt nur, ob das Geraet geoeffnet oder geschlossen werden soll; das Oeffnen selbst macht der Dienst
/// im Hintergrund (nie auf dem Oberflaechen-Thread).
/// </summary>
public sealed class Tonschalter
{
    public bool ImSpiel { get; private set; }

    public bool ImHintergrund { get; private set; }

    /// <summary>Phase 8 W9: laeuft gerade eine Hoerprobe (auch ausserhalb des Spiels)?</summary>
    public bool Hoerprobe { get; private set; }

    public int Gesamt { get; private set; } = TonEinstellungen.GesamtVorgabe;

    /// <summary>Soll der Ton gerade laufen?</summary>
    public bool Laeuft { get; private set; }

    /// <summary>Wie oft der Ton angegangen ist (Geraet geoeffnet).</summary>
    public int Starts { get; private set; }

    /// <summary>Wie oft der Ton nach dem Hintergrund von selbst wieder angegangen ist.</summary>
    public int Wiederanlaeufe { get; private set; }

    private bool _ausHintergrund;

    /// <summary>Neuer Zustand; Rueckgabe true = Geraet oeffnen, false = schliessen, null = nichts aendern.</summary>
    /// <remarks>Phase 8 W9: <paramref name="hoerprobe"/> = eine Hoerprobe in den Optionen braucht das Geraet auch ausserhalb des Spiels.</remarks>
    public bool? Setzen(bool? imSpiel = null, bool? hintergrund = null, int? gesamt = null, bool? hoerprobe = null)
    {
        ImSpiel = imSpiel ?? ImSpiel;
        ImHintergrund = hintergrund ?? ImHintergrund;
        Gesamt = gesamt ?? Gesamt;
        Hoerprobe = hoerprobe ?? Hoerprobe;
        var soll = (ImSpiel || Hoerprobe) && !ImHintergrund && Gesamt > 0;
        if (soll == Laeuft)
        {
            return null;
        }
        Laeuft = soll;
        if (!soll)
        {
            // nur ein Aus durch den Hintergrund zaehlt beim naechsten An als Wiederanlauf
            _ausHintergrund = ImHintergrund && ImSpiel && Gesamt > 0;
            return false;
        }
        Starts++;
        if (_ausHintergrund)
        {
            Wiederanlaeufe++;
        }
        _ausHintergrund = false;
        return true;
    }
}
