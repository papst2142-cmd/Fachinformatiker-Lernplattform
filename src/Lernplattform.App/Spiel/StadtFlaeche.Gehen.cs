using Avalonia.Input;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 0.95.0 (Sammelliste 385 „frei gehen“, Weisung P12-306): die vier Gehtasten (Standard W, A, S, D, frei belegbar)
/// werden gehalten. Die Flaeche merkt sich Druecken und Loslassen, rechnet je Bild aus den gehaltenen Tasten und der Drehung
/// der Kamera die Gehrichtung (<see cref="Steuerung"/>) und gibt sie dem Spiel (<see cref="StadtSpiel.FreiGehen"/>). Die
/// Kamera bleibt in ihrer Drehung (Nico 4a: W = vom Bildschirm weg, keine Verfolgerkamera); in der Ich-Sicht geht W in
/// Blickrichtung und der Koerper bleibt im Blick. Verliert das Fenster den Fokus oder verschwindet die Flaeche, gelten alle
/// Tasten als losgelassen. Nur im Spiel (feste Kamera); das Stadtbau-Werkzeug bleibt unveraendert.
/// </summary>
public sealed partial class StadtFlaeche
{
    private readonly HashSet<string> _gehTasten = new(StringComparer.Ordinal);

    /// <summary>Die gerade gehaltenen Gehtasten (Aufgaben aus <see cref="Tastenbelegung.Gehen"/>; Start-Test, Tests).</summary>
    public IReadOnlyCollection<string> GehTasten => _gehTasten;

    /// <summary>
    /// Taste gedrueckt oder losgelassen: ist sie nach der Tastenbelegung eine Gehtaste, merkt die Flaeche sie (true). Ohne Spiel,
    /// im Werkzeug (freie Kamera) oder ohne Gehen (<see cref="OhneGehen"/>) nimmt sie keine; ohne 3D (<see cref="GlBereit"/>
    /// falsch, z. B. Mac ohne OpenGL) nimmt sie keine neue an (Loslassen geht immer).
    /// </summary>
    public bool GehTaste(Key taste, bool gedrueckt) => GehAufgabe(Tastenbelegung.Aktuell.AufgabeFuer(taste.ToString()), gedrueckt);

    /// <summary>Wie oben mit der Aufgabe statt der Taste (Start-Test).</summary>
    public bool GehAufgabe(string? aufgabe, bool gedrueckt)
    {
        if (aufgabe is null || !Tastenbelegung.Gehen.Contains(aufgabe) || !FesteKamera || OhneGehen || _spiel is null
            || (gedrueckt && !GlBereit))
        {
            return false;
        }
        var geaendert = gedrueckt ? _gehTasten.Add(aufgabe) : _gehTasten.Remove(aufgabe);
        if (geaendert)
        {
            AnfordernBild();
        }
        return true;
    }

    /// <summary>Alle Gehtasten los (Fokus verloren, Flaeche weg, Fenster vorne verdeckt); seit A 0.96.0 auch die Sticks.</summary>
    public void GehTastenLos()
    {
        if (_gehTasten.Count > 0)
        {
            _gehTasten.Clear();
            AnfordernBild();
        }
        SticksLos();
    }

    /// <summary>
    /// Gehrichtung am Boden aus den gehaltenen Tasten, dem linken Stick (Welle A 0.96.0; wenig Ausschlag = langsam) und der
    /// Drehung der Kamera (0 = keine; zusammen nie schneller als volles Tempo).
    /// </summary>
    internal (double X, double Z) GehRichtung(Kamera kamera)
    {
        // Welle A 1.6.0 (Sammelliste 446): Regler „Empfindlichkeit linker Stick“ (Vorgabe = unveraendert)
        var stick = _sticks.Links is { } links ? Empfindlichkeit.Gehen(links.Eingabe) : (0, 0);
        if (_gehTasten.Count == 0 && stick == (0, 0))
        {
            return (0, 0);
        }
        var (vor, seit) = Steuerung.AusTasten(_gehTasten.Contains(Tastenbelegung.GehVor), _gehTasten.Contains(Tastenbelegung.GehZurueck),
            _gehTasten.Contains(Tastenbelegung.GehLinks), _gehTasten.Contains(Tastenbelegung.GehRechts));
        return Steuerung.Richtung(vor + stick.Vor, seit + stick.Seit, kamera.Drehung);
    }

    /// <summary>Je Bild vor dem Gehen des Spiels: frei gehen (true = die Figur geht frei oder der Halt steht noch aus).</summary>
    private bool FreiTakt(double dt)
    {
        if (_spiel is not { } spiel || AktuelleKamera is not { } kamera || !FesteKamera)
        {
            return false;
        }
        SticksTakt(kamera, dt);
        var (rx, rz) = GehRichtung(kamera);
        // in der Ich-Sicht (nah) bleibt der Koerper im Blick, auch beim Rueckwaerts- und Seitwaertsgehen
        float? blick = kamera.IchAnteil >= IchSicht.NahAb ? IchSicht.DrehungZu(kamera.Drehung) : null;
        return spiel.FreiGehen(rx, rz, dt, blick) | spiel.FreiUnterwegs;
    }

    /// <summary>Nachladen: eine noch nicht geladene Kachel sperrt beim freien Gehen (ohne Nachladen ist alles geladen).</summary>
    private bool GeladenAn(double x, double z) => _lader is not { } lader || lader.IstGeladen((float)x, (float)z);
}
