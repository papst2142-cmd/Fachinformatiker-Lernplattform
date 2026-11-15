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
    public static void Mausrad(Kamera k, double delta) => k.Zoomen(MathF.Pow(1.12f, (float)delta));

    /// <summary>Rechte Maustaste: waagrecht drehen, senkrecht (nur fest) neigen; Maus nach oben = steiler.</summary>
    public static bool RechteMaus(Kamera k, bool fest, Kameragriff.Schritt s)
    {
        if (s.Art != Kameragriff.Art.Drehen)
        {
            return false;
        }
        k.Drehen(s.Grad);
        if (fest)
        {
            k.Neigen((float)-s.Dy * SpielKamera.NeigenJePunkt);
        }
        return true;
    }

    /// <summary>Linke Maustaste oder ein Finger gezogen: drinnen verschieben, draussen (fest) nichts. true = Kamera bewegt.</summary>
    public static bool Ziehen(Kamera k, bool fest, double dx, double dy, double hoehe)
    {
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
            k.Zoomen((float)faktor);
        }
        k.Drehen((float)grad);
        if (fest)
        {
            k.Neigen((float)-dyMitte * SpielKamera.NeigenJeFingerPunkt);
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
                k.Drehen(-15f);
                return true;
            case Tastenbelegung.DrehRechts:
                k.Drehen(15f);
                return true;
            case Tastenbelegung.Naeher:
                k.Zoomen(1.15f);
                return true;
            case Tastenbelegung.Weiter:
                k.Zoomen(1 / 1.15f);
                return true;
            case Tastenbelegung.Steiler when fest:
                k.Neigen(SpielKamera.NeigenJeTaste);
                return true;
            case Tastenbelegung.Flacher when fest:
                k.Neigen(-SpielKamera.NeigenJeTaste);
                return true;
            default:
                return false;
        }
    }
}
