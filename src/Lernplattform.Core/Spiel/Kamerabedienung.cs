using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.80.0 (Weisung P12-268): was Maus, Finger und Tasten an der Spielkamera tun. Die Spielflaeche ruft nur diese
/// Regeln auf; so pruefen Unit-Tests dieselbe Rechnung wie das Spiel. <c>fest</c> = feste Spielkamera draussen
/// (<see cref="SpielKamera"/>): kein Verschieben, rechte Maustaste und zwei Finger neigen zusaetzlich, Pfeiltasten tun nichts,
/// R/F neigen. Ohne <c>fest</c> (Innenraeume, Nico Antwort 4 „wie heute“) alles wie bis A 0.79.0.
/// </summary>
public static class Kamerabedienung
{
    /// <summary>Weite eines Pfeiltasten-Schritts in Bildpunkten (wie Ziehen), nur drinnen.</summary>
    public const float TastenSchritt = 48f;

    /// <summary>Mausrad um <paramref name="delta"/> Rasten (positiv = naeher).</summary>
    public static void Mausrad(Kamera k, double delta) => SpielKamera.Zoomen(k, MathF.Pow(1.12f, (float)delta));

    /// <summary>Rechte Maustaste: waagrecht drehen, senkrecht (nur fest) neigen; Maus nach oben = steiler (Schalter <see cref="Umsehbedienung"/> kehren um).</summary>
    public static bool RechteMaus(Kamera k, bool fest, Kameragriff.Schritt s)
    {
        if (s.Art != Kameragriff.Art.Drehen)
        {
            return false;
        }
        // Welle A 0.97.0 (Weisung P12-314): Schalter „Umsehen umkehren“ (Standard aus = wie bisher)
        k.Drehen(s.Grad * Umsehbedienung.FaktorLinksRechts);
        if (fest || SpielKamera.ImIch(k))
        {
            SpielKamera.Neigen(k, (float)-s.Dy * SpielKamera.NeigenJePunkt * Umsehbedienung.FaktorObenUnten);
        }
        return true;
    }

    private static readonly AsyncLocal<bool> MausAlt = new();

    /// <summary>
    /// Welle A 1.6.0 (Sammelliste 441, Weisung P12-359, Fund Nico 23.11.2026): Gegenprobe. true = die Maus dreht wie bis
    /// A 1.5.0 (sie zieht die Welt: nach rechts gezogen dreht der Blick nach links). AsyncLocal, wirkt nicht in der Bildschleife.
    /// </summary>
    public static bool MausAltFuerTests { get => MausAlt.Value; set => MausAlt.Value = value; }

    /// <summary>
    /// Welle A 1.6.0 (Sammelliste 441): ein Schritt der Maus (linke oder rechte Taste, aus <see cref="Kameragriff"/>). Maus nach
    /// rechts = Blick nach rechts, nach links = Blick nach links, wie der rechte Stick am Handy (<see cref="Sticks.Drehen"/>).
    /// Das gilt fuer die rechte Taste (draussen, drinnen, Ich-Sicht) und fuer die linke Taste in der Ich-Sicht. Oben/unten bleibt
    /// wie bisher (Nico nannte nur links/rechts). Finger gehen nie hier durch (Ziehen mit dem Finger bleibt wie bisher).
    /// </summary>
    public static bool Maus(Kamera k, bool fest, Kameragriff.Schritt s, double hoehe)
    {
        var richtung = MausAlt.Value ? 1f : -1f;
        switch (s.Art)
        {
            case Kameragriff.Art.Drehen:
                return RechteMaus(k, fest, s with { Grad = s.Grad * richtung });
            case Kameragriff.Art.Ziehen when SpielKamera.ImIch(k):
                return RechteMaus(k, fest, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: s.Dy, Grad: (float)s.Dx * Kameragriff.GradJePunkt * richtung));
            case Kameragriff.Art.Ziehen:
                return Ziehen(k, fest, s.Dx, s.Dy, hoehe);
            default:
                return false;
        }
    }

    /// <summary>Linke Maustaste oder ein Finger gezogen: drinnen verschieben, draussen (fest) nichts. true = Kamera bewegt.</summary>
    public static bool Ziehen(Kamera k, bool fest, double dx, double dy, double hoehe)
    {
        if (SpielKamera.ImIch(k))
        {
            // Welle A 0.92.0 (Weisung P12-301 Punkt 2): in der Ich-Sicht umsehen wie mit der rechten Maustaste
            return RechteMaus(k, fest, new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: dy, Grad: (float)dx * Kameragriff.GradJePunkt));
        }
        if (fest)
        {
            return false;
        }
        k.Verschieben((float)dx, (float)dy, (float)hoehe);
        return true;
    }

    /// <summary>
    /// Zwei Finger: <paramref name="faktor"/> = neuer durch alten Fingerabstand (Zoom), <paramref name="grad"/> = Drehung der
    /// Linie zwischen den Fingern, <paramref name="dyMitte"/> = wie weit die Mitte der Finger senkrecht wanderte (fest: neigen,
    /// Finger nach oben = steiler).
    /// </summary>
    public static void ZweiFinger(Kamera k, bool fest, double faktor, double grad, double dyMitte)
    {
        if (faktor > 0)
        {
            SpielKamera.Zoomen(k, (float)faktor);
        }
        k.Drehen((float)grad);
        if (fest || SpielKamera.ImIch(k))
        {
            SpielKamera.Neigen(k, (float)-dyMitte * SpielKamera.NeigenJeFingerPunkt);
        }
    }

    /// <summary>Eine Aufgabe der Tastenbelegung an der Kamera; true = genommen (Hineingehen macht die Flaeche selbst).</summary>
    public static bool Taste(Kamera k, bool fest, string? aufgabe, double hoehe)
    {
        var (dx, dy) = aufgabe switch
        {
            Tastenbelegung.Links => (TastenSchritt, 0f),
            Tastenbelegung.Rechts => (-TastenSchritt, 0f),
            Tastenbelegung.Hoch => (0f, TastenSchritt),
            Tastenbelegung.Runter => (0f, -TastenSchritt),
            _ => (0f, 0f),
        };
        if (dx != 0 || dy != 0)
        {
            return Ziehen(k, fest, dx, dy, Math.Max(1, hoehe));
        }
        switch (aufgabe)
        {
            case Tastenbelegung.DrehLinks:
                k.Drehen(-15f * Umsehbedienung.FaktorLinksRechts);
                return true;
            case Tastenbelegung.DrehRechts:
                k.Drehen(15f * Umsehbedienung.FaktorLinksRechts);
                return true;
            case Tastenbelegung.Naeher:
                SpielKamera.Zoomen(k, 1.15f);
                return true;
            case Tastenbelegung.Weiter:
                SpielKamera.Zoomen(k, 1 / 1.15f);
                return true;
            case Tastenbelegung.Steiler when fest || SpielKamera.ImIch(k):
                SpielKamera.Neigen(k, SpielKamera.NeigenJeTaste);
                return true;
            case Tastenbelegung.Flacher when fest || SpielKamera.ImIch(k):
                SpielKamera.Neigen(k, -SpielKamera.NeigenJeTaste);
                return true;
            case Tastenbelegung.IchSicht:
                return SpielKamera.IchUmschalten(k);
            default:
                return false;
        }
    }
}
