using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.92.0 „Ich-Sicht“ (Weisung P12-301, Kurzplan P12-181, Nico 1a 2b 3c 4a): Regeln der Ich-Sicht im Spiel, die die
/// Spielflaeche je Bild aufruft (so pruefen Unit-Tests dieselbe Rechnung). Das Auge sitzt im Kopf der Figur
/// (<see cref="Auge"/>), beim Gehen dreht der Blick weich in die Gehrichtung, beim Sitzen, Liegen und im Bus endet die
/// Ich-Sicht und kommt beim Aufstehen und Aussteigen zurueck. Nur Darstellung: kein Spielschritt, kein Ereignis, keine
/// Spielzeit; Wege und Wandregeln (342, 347) bleiben die des Spiels.
/// </summary>
public sealed class IchSicht
{
    /// <summary>
    /// Hoehe des Auges als Anteil der Figurhoehe. Der Kopf der Figur ist gross (0,68 bis 1); das Auge sitzt vorn unten im Kopf,
    /// damit man beim Blick nach unten Brust, Bauch und Beine sieht (aus der Kopfmitte saehe man nur die Schultern).
    /// </summary>
    public const float AugeAnteil = 0.80f;

    /// <summary>So weit (Anteil der Figurhoehe) sitzt das Auge vor der Mitte der Figur, in ihrer Blickrichtung, knapp vor der Brust (Brust vorn bei 0,08): beim Blick steil nach unten sieht man Brust, Bauch, Beine und Schuhe von vorn oben; weiter vorn laege der Koerper hinter dem unteren Bildrand.</summary>
    public const float AugeVor = 0.10f;

    /// <summary>
    /// Die eigene Figur wird in der Ich-Sicht nur bis zu diesem Anteil ihrer Hoehe gezeichnet (Kopf, Haar, Brille, Bart und
    /// Kopfbedeckung liegen darueber; Weisung P12-301 Punkt 3: der Kopf darf ausgeblendet sein, nichts steht vor der Kamera).
    /// </summary>
    public const float KoerperBis = 0.67f;

    /// <summary>
    /// Grosse Karten mit Nachladen: in der Ich-Sicht (Blick waagrecht in die Ferne) zeichnet die Flaeche Kacheln nur bis zu diesem
    /// Abstand vom Auge voll, weiter weg bis zur <see cref="Sichtweite"/> ihre Fernkaesten (wie ungeladene Kacheln; der Dunst liegt ab 12 darueber). So bleiben die
    /// Grenzen in jeder Stufe eingehalten (Weisung P12-301 Punkt 5); Stadt 1 ohne Nachladen ist davon nicht beruehrt.
    /// </summary>
    public const float KachelnVollBis = 16f;

    /// <summary>
    /// Grosse Karten mit Nachladen: Sichtweite der Ich-Sicht (der Dunst wird bis hierhin dicht, dahinter zeichnet die Flaeche
    /// nichts). Gleich dem Kreis der noetigen Kacheln (<see cref="Stadtbau.Ladeplan.Nah"/>), so fehlt im Dunst nie eine Kachel.
    /// </summary>
    public const float Sichtweite = Stadtbau.Ladeplan.Nah;

    /// <summary>So schnell dreht der Blick beim Gehen in die Gehrichtung (Anteil des Rests je Sekunde).</summary>
    public const float FolgenTempo = 4f;

    /// <summary>Ab diesem Anteil zeigt die Flaeche die Figur ohne Kopf und laesst nichts mehr halb durchsichtig werden.</summary>
    public const float NahAb = 0.9f;

    private Kamera? _letzte;
    private float? _pause;
    private int _richtungZuletzt = -1;

    /// <summary>Gilt die Ich-Sicht gerade nicht (Sitzen, Liegen, im Bus)? Sie kommt danach von allein zurueck.</summary>
    public bool Pausiert => _pause is not null;

    /// <summary>
    /// Stelle des Auges: Kopf der Figur draussen (auf Weg, Strasse oder Gelaende) oder im Innenraum, vorn am Gesicht in Richtung
    /// <paramref name="drehung"/> der Kamera (ohne: in Richtung der Figur). In der Ich-Sicht dreht der Koerper mit dem Blick
    /// (<see cref="FigurDrehung"/>), darum sitzt das Auge immer vor dem Gesicht, auch beim Umsehen im Stehen.
    /// </summary>
    public static Vector3 Auge(StadtSpiel s, float? drehung = null)
    {
        var grad = drehung is { } d ? DrehungZu(d) : s.FigurRichtung;
        var r = grad * MathF.PI / 180f;
        var vor = new Vector3(MathF.Sin(r), 0f, MathF.Cos(r)) * AugeVor;
        if (s.ImInnenraum && s.Innen is { } innen)
        {
            var p = AugeDrinnen(innen, s.FigurX, s.FigurZ, grad);
            return new Vector3(p.X, Innenraum.FigurHoehe * AugeAnteil, p.Y);
        }
        var boden = MathF.Max(StadtSzene.Hoehe.Figur, s.Szene.BodenHoehe(s.FigurX, s.FigurZ));
        return new Vector3(s.FigurX, boden + (Figur.StadtHoehe * AugeAnteil), s.FigurZ) + (vor * Figur.StadtHoehe);
    }

    /// <summary>So nah (Welt) darf das Auge drinnen einem Wandstueck kommen (mehr als die halbe Diagonale der nahen Ebene).</summary>
    public const float WandAbstand = 0.03f;

    /// <summary>
    /// Auge drinnen (X, Z): vorn am Gesicht in Richtung <paramref name="grad"/> (Grad wie die Figur); steht die Figur dicht an einer
    /// Wand, rueckt das Auge in Vierteln zur Mitte der Figur zurueck, bis es <see cref="WandAbstand"/> von jedem Wandstueck hat
    /// (sonst schnitte die nahe Ebene die Wand an und man saehe hindurch; Weisung P12-301 Punkt 4).
    /// </summary>
    public static Vector2 AugeDrinnen(Innenraum innen, float x, float z, float grad)
    {
        var r = grad * MathF.PI / 180f;
        var vor = new Vector2(MathF.Sin(r), MathF.Cos(r)) * (AugeVor * Innenraum.FigurHoehe);
        var mitte = new Vector2(x, z);
        for (var teil = 4; teil > 0; teil--)
        {
            var p = mitte + (vor * (teil / 4f));
            if (innen.Waende.All(w => AbstandZurWand(p, w) > WandAbstand))
            {
                return p;
            }
        }
        return mitte;
    }

    /// <summary>Waagrechter Abstand eines Punkts zum Kasten eines Wandstuecks (0 = darin).</summary>
    public static float AbstandZurWand(Vector2 p, InnenWand w)
    {
        var dx = MathF.Max(MathF.Max(w.Min.X - p.X, 0), p.X - w.Max.X);
        var dz = MathF.Max(MathF.Max(w.Min.Z - p.Y, 0), p.Y - w.Max.Z);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>Sitzt oder liegt die Figur oder faehrt sie im Bus (dann keine Ich-Sicht, E1 Kurzplan P12-181 Abschnitt 1)?</summary>
    public static bool Gesperrt(StadtSpiel s, bool busFaehrtFigur) => busFaehrtFigur || s.Haltung is not null || s.SitztImBus;

    /// <summary>
    /// Drehung, mit der die Flaeche die eigene Figur zeichnet: nah in der Ich-Sicht (ab <see cref="NahAb"/>, nicht beim Sitzen und
    /// Liegen) schaut der Koerper in die Blickrichtung (sonst saehe man beim Umsehen nach unten Schulter und Hals von der Seite);
    /// sonst wie das Spiel (<see cref="StadtSpiel.FigurRichtung"/>). Nur Darstellung, das Spiel bleibt unberuehrt.
    /// </summary>
    public static float FigurDrehung(Kamera k, StadtSpiel s) =>
        k.IchAnteil >= NahAb && s.Haltung is null ? DrehungZu(k.Drehung) : s.FigurRichtung;

    /// <summary>Drehung der Kamera, die in die Richtung der Figur schaut (Figur 0 = +Z, Kamera 0 = Blick nach -Z).</summary>
    public static float DrehungZu(float figurRichtung) => ((figurRichtung + 180f) % 360f + 360f) % 360f;

    /// <summary>
    /// Je Bild: Auge setzen, Uebergang weich weiterfuehren, beim Gehen in die Gehrichtung drehen, beim Sitzen/Liegen/Bus pausieren.
    /// Neue Kamera (Haus betreten oder verlassen): die Ich-Sicht geht mit. true = die Kamera hat sich bewegt (neues Bild noetig).
    /// </summary>
    public bool Takt(Kamera k, StadtSpiel s, float dt, bool busFaehrtFigur = false)
    {
        var bewegt = false;
        if (!ReferenceEquals(k, _letzte))
        {
            if (_letzte is { } alt && k.IchErlaubt && (alt.IchZiel >= 1f || _pause is not null))
            {
                // in ein Haus hinein oder hinaus: in der Ich-Sicht bleiben (ohne neuen Uebergang)
                k.IchNeigung = alt.IchNeigung;
                k.Drehung = alt.Drehung;
                if (_pause is null)
                {
                    k.IchZiel = 1f;
                    k.IchAnteil = 1f;
                }
                bewegt = true;
            }
            _letzte = k;
        }
        if (!k.IchErlaubt)
        {
            return bewegt;
        }
        if (Gesperrt(s, busFaehrtFigur))
        {
            if (k.IchZiel > 0f)
            {
                _pause = Math.Max(_pause ?? 0f, k.IchZiel);
                k.IchZiel = 0f;
            }
        }
        else if (_pause is { } p)
        {
            k.IchZiel = p;
            _pause = null;
            bewegt = true;
        }
        bewegt |= k.IchTakt(dt);
        if (k.IchAnteil > 0f && s.Geht)
        {
            var soll = DrehungZu(s.FigurRichtung);
            var rest = ((soll - k.Drehung + 540f) % 360f) - 180f;
            if (MathF.Abs(rest) > 0.01f)
            {
                k.Drehen(rest * Math.Min(1f, FolgenTempo * Math.Max(0f, dt)));
                bewegt = true;
            }
        }
        k.IchAuge = Auge(s, k.Drehung);
        return bewegt || k.IchAnteil > 0f;
    }

    /// <summary>Himmelsrichtung des Blicks (Norden = -Z, oben auf der Karte), acht Richtungen.</summary>
    public static string Richtung(float drehung)
    {
        string[] namen = ["Norden", "Nordwesten", "Westen", "Südwesten", "Süden", "Südosten", "Osten", "Nordosten"];
        return namen[RichtungNummer(drehung)];
    }

    private static int RichtungNummer(float drehung) => (int)MathF.Floor(((((drehung % 360f) + 360f) % 360f) + 22.5f) / 45f) % 8;

    /// <summary>Ansage beim Einschalten und beim Wechsel der Blickrichtung: „Blick nach Norden. Vor dir: Kaufhaus, 12 m.“</summary>
    public static string Ansage(Kamera k, StadtSpiel s)
    {
        var text = SpielProbeTexte.IchBlick(Richtung(k.Drehung));
        if (!s.ImInnenraum && s.Szene.OrtBei(k.IchAuge, Vector3.Normalize(new Vector3(k.IchBlick.X, 0, k.IchBlick.Z))) is { } ort)
        {
            var meter = Vector2.Distance(new Vector2(s.FigurX, s.FigurZ), new Vector2(ort.StehX, ort.StehZ)) / StadtSzene.Meter;
            text += " " + SpielProbeTexte.IchVorDir(ort.Name, (int)MathF.Round(meter));

        }
        return text;
    }

    /// <summary>Neue Ansage noetig (Ich-Sicht und Blick in eine andere der acht Richtungen)? Merkt sich die Richtung.</summary>
    public bool RichtungNeu(Kamera k)
    {
        if (!k.Ich)
        {
            _richtungZuletzt = -1;
            return false;
        }
        var n = RichtungNummer(k.Drehung);
        if (n == _richtungZuletzt)
        {
            return false;
        }
        _richtungZuletzt = n;
        return true;
    }

    /// <summary>Zahl fuer Texte (Start-Test, Meldungen).</summary>
    public static string Zahl(float wert) => wert.ToString("0.00", CultureInfo.InvariantCulture);
}
