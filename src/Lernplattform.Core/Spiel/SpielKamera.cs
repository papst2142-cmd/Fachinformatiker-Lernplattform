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

    // ---- Welle A 0.97.0 (Weisung P12-312 Punkt 1, Nico 1a, Sammelliste 392): drinnen bleibt die ganze Figur im Bild -------

    /// <summary>
    /// Innenkamera wie seit Phase 7 (Nachtrag 2 zu P7-12): schraeg von vorn (Sueden) auf die Mitte des Innenraums, Abstand nach
    /// der Raumgroesse (0,85 x groesste Seite, 12 bis 28), Neigung 58. <paramref name="ich"/> = Spiel (Ich-Sicht ohne
    /// Schulterblick, Welle A 0.92.0); das Werkzeug ohne. Welle A 0.97.0: nur hierher verschoben, Werte unveraendert.
    /// </summary>
    public static Kamera Drinnen(float breite, float tiefe, bool ich) =>
        new(breite / 2, tiefe / 2, Math.Clamp(Math.Max(breite, tiefe) * 0.85f, 12f, 28f))
        {
            Neigung = 58f,
            Grenzen = (0, 0, breite, tiefe),
            IchErlaubt = ich,
            IchNeigungBereich = (IchNeigungMin, IchNeigungMax),
            IchNeigung = IchNeigungStart,
        };

    /// <summary>Welle A 0.97.0: Zugabe ueber dem Scheitel der Figur (Haare, Kopfbedeckung), wie in der Rechnung der Meldung P12-191.</summary>
    public const float ScheitelZugabe = 1.04f;

    /// <summary>Welle A 0.97.0: Abstand des Scheitels zum oberen Rand des freien Bereichs in Punkten.</summary>
    public const float ScheitelRand = 4f;

    /// <summary>
    /// Versatz der Kamera fuer den freien Bereich (Bildraum, hoechstens 0,6): das Ziel steht in der Mitte zwischen Kopfzeile
    /// (<paramref name="oben"/>) und Tafel (<paramref name="unten"/>). Gleiche Rechnung wie die Spielflaeche seit Nachtrag 1 zu P7-13.
    /// </summary>
    public static float FreiVersatz(double hoehe, double oben, double unten) =>
        hoehe > 1 ? (float)Math.Clamp((unten - oben) / hoehe, -0.6, 0.6) : 0f;

    /// <summary>
    /// Welle A 0.97.0 (Nico 1a): kleinster Abstand der Kamera drinnen, bei dem Scheitel und Fuesse der Figur (am Ziel der
    /// Kamera) im freien Bereich zwischen Kopfzeile und Tafel bleiben. Die Fuesse stehen ueber den Versatz immer in der Mitte des
    /// freien Bereichs; der Scheitel liegt umso hoeher im Bild, je naeher die Kamera ist. Gerechnet je Bildschirm (Hoehe,
    /// Kopfzeile, Tafel), Neigung und Sichtfeld; das Seitenverhaeltnis spielt keine Rolle (Sichtfeld senkrecht). Ergebnis im
    /// Bereich <see cref="Kamera.AbstandMin"/> bis <see cref="Kamera.AbstandMax"/>.
    /// </summary>
    public static float InnenAbstandMin(double hoehe, double oben, double unten, float neigung, float sichtfeld, float figurHoehe)
    {
        if (hoehe <= 1)
        {
            return Kamera.AbstandMin;
        }
        var versatz = FreiVersatz(hoehe, oben, unten);
        // hoechste Stelle (Bildraum -1 bis 1), an der der Scheitel noch im freien Bereich liegt
        var platz = 1.0 - (2.0 * (oben + ScheitelRand) / hoehe) - versatz;
        if (platz <= 0.01)
        {
            return Kamera.AbstandMax;
        }
        var n = neigung * Math.PI / 180;
        var h = figurHoehe * ScheitelZugabe;
        var tan = Math.Tan(sichtfeld * Math.PI / 360);
        // Scheitel im Bildraum: h cos n / ((d - h sin n) tan) + Versatz; nach d aufgeloest
        var d = (h * Math.Sin(n)) + (h * Math.Cos(n) / (tan * platz));
        return (float)Math.Clamp(Math.Ceiling(d * 100) / 100, Kamera.AbstandMin, Kamera.AbstandMax);
    }

    /// <summary>
    /// Welle A 0.97.0: Bereich der Innenkamera fuer diesen Bildschirm setzen (kleinster Abstand nach <see cref="InnenAbstandMin"/>,
    /// weitester wie bisher 40). Der Abstand wird dabei in den Bereich geholt (nach dem Drehen des Geraets weiter weg). Waehrend
    /// einer Messung (<see cref="Kamera.MessungLaeuft"/>) bleibt der Bereich frei.
    /// </summary>
    public static void InnenBereich(Kamera k, double hoehe, double oben, double unten, float figurHoehe)
    {
        if (k.MessungLaeuft)
        {
            return;
        }
        var min = InnenAbstandMin(hoehe, oben, unten, k.Neigung, k.Sichtfeld, figurHoehe);
        if (k.Bereich != (min, Kamera.AbstandMax))
        {
            k.Bereich = (min, Kamera.AbstandMax);
        }
    }

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
