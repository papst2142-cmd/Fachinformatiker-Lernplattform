using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.80.0 (Weisung P12-268, Nicos Antworten P12-257 Abschnitt 2 auf den Kurzplan P12-150): feste naehere Kamera
/// draussen im Spiel. Abstand fest 8 mit kleinem Rest-Zoom 7 bis 9 (Mausrad, Tasten +/−, zwei Finger), ohne Verschieben;
/// rechte Maustaste bzw. zwei Finger drehen und neigen, Neigung 35 bis 70 Grad. Innenraeume und das Stadtbau-Werkzeug
/// behalten ihre Kamera (<see cref="Kamera"/> ohne Bereich). Die Regeln stehen nur hier; die Spielflaeche ruft sie auf.
/// </summary>
public static class SpielKamera
{
    /// <summary>Fester Abstand draussen (Nico Antwort 1).</summary>
    public const float Abstand = 8f;

    /// <summary>Rest-Zoom (Nico Antwort 3): naechster Abstand.</summary>
    public const float AbstandMin = 7f;

    /// <summary>Rest-Zoom (Nico Antwort 3): weitester Abstand.</summary>
    public const float AbstandMax = 9f;

    /// <summary>Neigung beim Start wie bis A 0.79.0.</summary>
    public const float NeigungStart = 52f;

    /// <summary>Flachster Blick (Nico Antwort 2).</summary>
    public const float NeigungMin = 35f;

    /// <summary>Steilster Blick (Nico Antwort 2).</summary>
    public const float NeigungMax = 70f;

    /// <summary>Neigen mit der rechten Maustaste: Grad je Bildpunkt senkrecht (Maus nach oben = steiler).</summary>
    public const float NeigenJePunkt = 0.25f;

    /// <summary>Neigen mit zwei Fingern: Grad je Bildpunkt, um den beide Finger gemeinsam hoch oder runter wandern.</summary>
    public const float NeigenJeFingerPunkt = 0.25f;

    /// <summary>Neigen mit einer Taste (R steiler, F flacher): Grad je Druck.</summary>
    public const float NeigenJeTaste = 5f;

    /// <summary>Neue Kamera draussen auf (x, z) mit Abstand 8, Neigung 52 und den Bereichen dieser Welle.</summary>
    public static Kamera Draussen(float x, float z, float breite, float tiefe) =>
        new(x, z, Abstand)
        {
            Grenzen = (0, 0, breite, tiefe),
            Bereich = (AbstandMin, AbstandMax),
            NeigungBereich = (NeigungMin, NeigungMax),
            Neigung = NeigungStart,
            IchErlaubt = true,
            IchMitSchulter = true,
            IchNeigungBereich = (IchNeigungMin, IchNeigungMax),
            IchNeigung = IchNeigungStart,
        };

    // ---- Welle A 0.92.0 (Weisung P12-301 Punkt 1, Nico 1a): Heranzoomen bis in die Ich-Sicht ------------------------------

    /// <summary>
    /// Welle A 0.92.0: unter diesem gedachten Abstand springt die Kamera (weich, <see cref="Kamera.IchTakt"/>) in die Ich-Sicht.
    /// Zwischen <see cref="AbstandMin"/> (7) und hier liegt der Schulterblick: die Kamera kommt naeher und wird flacher.
    /// </summary>
    public const float SchulterAb = 2f;

    /// <summary>Welle A 0.92.0: Anteil der Ich-Sicht am Ende des Schulterblicks (gedachter Abstand 2 von 7).</summary>
    public const float SchulterAnteil = 1f - (SchulterAb / AbstandMin);

    /// <summary>Welle A 0.92.0: Blick in der Ich-Sicht beim Hineinkommen (5 Grad nach unten).</summary>
    public const float IchNeigungStart = 5f;

    /// <summary>Welle A 0.92.0 (Kurzplan P12-181 Abschnitt 2): hoechstens so weit nach oben (40 Grad).</summary>
    public const float IchNeigungMin = -40f;

    /// <summary>Welle A 0.92.0 (Weisung P12-301 Punkt 3): so weit nach unten, dass man Koerper und Beine der Figur sieht.</summary>
    public const float IchNeigungMax = 85f;

    /// <summary>
    /// Welle A 0.92.0: gedachter Abstand der Kamera (gleich dem Abstand bis 7; darunter der Schulterblick bis 2; 0 = Ich-Sicht
    /// oder auf dem Weg hinein). Er laeuft nach dem Ziel des Uebergangs, damit Zoomen waehrend des weichen Wegs richtig weiterzaehlt.
    /// </summary>
    public static float GedachterAbstand(Kamera k) =>
        k.IchZiel >= 1f ? 0f : k.IchZiel > 0f ? k.Bereich.Min * (1f - k.IchZiel) : k.Abstand;

    /// <summary>
    /// Welle A 0.92.0: Zoomen mit Ich-Sicht (Faktor &gt; 1 = naeher). Ohne <see cref="Kamera.IchErlaubt"/> genau wie
    /// <see cref="Kamera.Zoomen"/> (Werkzeug). Draussen: bis 7 wie bisher, darunter der Schulterblick (Weg doppelt so schnell),
    /// unter 2 weich in die Ich-Sicht; heraus denselben Weg zurueck, weiter als 9 nie. Drinnen (ohne Schulterblick): naeher als
    /// der kleinste Abstand fuehrt weich in die Ich-Sicht, heraus weich zurueck in die heutige Innenkamera.
    /// </summary>
    public static void Zoomen(Kamera k, float faktor)
    {
        if (!k.IchErlaubt)
        {
            k.Zoomen(faktor);
            return;
        }
        if (faktor <= 0 || faktor == 1f || float.IsNaN(faktor))
        {
            return;
        }
        var min = k.Bereich.Min;
        if (!k.IchMitSchulter)
        {
            if (faktor > 1f)
            {
                if (k.IchZiel < 1f && k.Abstand <= min)
                {
                    k.IchZiel = 1f;
                }
                else if (k.IchZiel < 1f)
                {
                    k.Zoomen(faktor);
                }
            }
            else if (k.IchZiel > 0f || k.IchAnteil > 0f)
            {
                k.IchZiel = 0f;
            }
            else
            {
                k.Zoomen(faktor);
            }
            return;
        }
        var v = GedachterAbstand(k);
        if (faktor < 1f && v <= 0f)
        {
            // aus der Ich-Sicht heraus: weich zurueck an das Ende des Schulterblicks
            k.IchZiel = SchulterAnteil;
            return;
        }
        if (faktor > 1f && v <= 0f)
        {
            return;
        }
        var neu = v / faktor;
        if (neu < min)
        {
            // im Schulterblick zaehlt der Weg doppelt (sonst braeuchte man rund zehn Rasten bis zur Ich-Sicht)
            var vor = Math.Min(v, min);
            var rest = neu / vor;
            neu = vor * rest * rest;
        }
        if (neu >= min)
        {
            k.IchZiel = 0f;
            k.IchAnteil = 0f;
            k.Zoomen(k.Abstand / Math.Min(neu, k.Bereich.Max));
        }
        else
        {
            k.Zoomen(k.Abstand / min);
            if (neu >= SchulterAb)
            {
                k.IchZiel = 1f - (neu / min);
                k.IchAnteil = k.IchZiel;
            }
            else
            {
                k.IchAnteil = Math.Max(k.IchAnteil, SchulterAnteil);
                k.IchZiel = 1f;
            }
        }
    }

    /// <summary>Welle A 0.92.0 (Menue „Ich-Sicht“, Taste I): an (weich hinein) oder aus (weich zurueck zur normalen Kamera).</summary>
    public static bool IchUmschalten(Kamera k)
    {
        if (!k.IchErlaubt)
        {
            return false;
        }
        if (k.IchZiel >= 1f || k.Ich)
        {
            k.IchZiel = 0f;
        }
        else
        {
            k.IchZiel = 1f;
        }
        return true;
    }

    /// <summary>Welle A 0.92.0: in der Ich-Sicht oder auf dem Weg hinein (die Bedienung sieht sich dann um).</summary>
    public static bool ImIch(Kamera k) => k.IchErlaubt && (k.IchZiel >= 1f || k.IchAnteil >= 1f);

    /// <summary>Welle A 0.92.0: neigen; in der Ich-Sicht der Blick (positiv = nach unten), sonst die Kamera (positiv = steiler).</summary>
    public static void Neigen(Kamera k, float grad)
    {
        if (ImIch(k))
        {
            k.IchNeigung += grad;
        }
        else
        {
            k.Neigen(grad);
        }
    }
}
