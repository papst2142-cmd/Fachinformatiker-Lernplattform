using System.Numerics;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.93.0 „Haltungen“ (Weisung P12-303, Sammelliste 352, Nico 5a):
/// <list type="bullet">
/// <item>Springen (Leertaste und Knopf): kurzer Sprung an der Stelle und im Gehen. Der Weg bleibt derselbe (die Figur springt nur
/// in die Hoehe, nie ueber Hindernisse, Waende oder Fahrbahn); drinnen nur, wo ueber dem Kopf Platz ist.</item>
/// <item>Ducken und Kriechen (Knoepfe, Tasten frei belegbar): nur an der Stelle; Gehen beendet die Haltung. Die Haltung braucht
/// mehr Platz als das Stehen; seit A 0.98.0 (Sammelliste 396) behaelt die Figur dabei ihre Richtung und rutscht bei Bedarf ein Stueck
/// zurueck (vor einer Tuer oder Wand), sonst Hinweis „kein Platz“.</item>
/// </list>
/// Nur Darstellung: kein Spielschritt, kein Ereignis, keine Spielzeit (Fingerabdruecke gleich); die Figur steht logisch weiter an
/// ihrer Stelle. Ohne 3D und ohne Grafik gibt es keine Haltungen (wie bis A 0.92.0).
/// </summary>
public sealed partial class StadtSpiel
{
    /// <summary>Aktion des Knopfs „Springen“ (Leertaste).</summary>
    public const string AktionSpringen = "springen";

    /// <summary>Aktion des Knopfs „Ducken“.</summary>
    public const string AktionDucken = "ducken";

    /// <summary>Aktion des Knopfs „Kriechen“.</summary>
    public const string AktionKriechen = "kriechen";

    /// <summary>Dauer eines Sprungs in Sekunden.</summary>
    public const float SprungDauer = 0.6f;

    /// <summary>
    /// In diese Richtungen (Grad zur jetzigen) drehte sich die Figur bis A 0.97.0 der Reihe nach, wenn fuer Ducken oder Kriechen vor ihr
    /// kein Platz war; seit A 0.98.0 (Sammelliste 396) nur noch in der Gegenprobe <see cref="KoerperDrehenAltFuerTests"/>.
    /// </summary>
    public static readonly IReadOnlyList<float> KoerperRichtungen = [0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f];

    private static readonly AsyncLocal<bool> KoerperFreiAlt = new();

    /// <summary>Gegenprobe (nur Tests): ohne Platzpruefung (die Haltung gilt ueberall).</summary>
    public static void KoerperFreiAltFuerTests(bool an) => KoerperFreiAlt.Value = an;

    private Koerperhaltung _koerper;
    private (Innenraum? Innen, float X, float Z) _koerperStelle;
    private float? _sprung;

    /// <summary>Die Figur kann gerade eine Haltung einnehmen (3D, steht, nicht im Bus, nicht beim Einrichten).</summary>
    public bool KoerperMoeglich => HaltungAn && !SitztOderLiegt && Mitfahrt == MitfahrtStufe.Keine && !ImBus && !Einrichten;

    /// <summary>
    /// Haltung der Figur (Stehen, Ducken, Kriechen). Seit A 0.99.0 (Sammelliste 400, Weisung P12-319/320) geht die Figur in der
    /// Haltung weiter (langsamer, <see cref="HaltungTempo"/>); Sitzen, Bus, ein anderer Raum und jedes Versetzen ohne Gehen beenden sie,
    /// ein Hindernis, an dem die Haltung keinen Platz hat, ebenso (die Figur steht auf). Bis A 0.98.0 beendete jedes Gehen die Haltung
    /// (Gegenprobe <see cref="HaltungGehenAltFuerTests"/>).
    /// </summary>
    public Koerperhaltung Koerper =>
        _koerper != Koerperhaltung.Stehen && KoerperMoeglich && (!HaltungGehenAlt.Value || !Geht) && ReferenceEquals(Innen, _koerperStelle.Innen)
        && FigurX == _koerperStelle.X && FigurZ == _koerperStelle.Z ? _koerper : Koerperhaltung.Stehen;

    /// <summary>Welle A 0.99.0 (E1, in der Meldung P12-196 genannt): geduckt geht die Figur mit 60 % des Tempos.</summary>
    public const float DuckenTempo = 0.6f;

    /// <summary>Welle A 0.99.0 (E1): kriechend geht die Figur mit 35 % des Tempos.</summary>
    public const float KriechenTempo = 0.35f;

    /// <summary>Anteil des normalen Tempos in der Haltung <paramref name="haltung"/> (Stehen: 1).</summary>
    public static float HaltungTempo(Koerperhaltung haltung) => haltung switch
    {
        Koerperhaltung.Ducken => DuckenTempo,
        Koerperhaltung.Kriechen => KriechenTempo,
        _ => 1f,
    };

    private static readonly AsyncLocal<bool> HaltungGehenAlt = new();

    /// <summary>Gegenprobe (nur Tests, AsyncLocal): Stand bis A 0.98.0, jedes Gehen beendet Ducken und Kriechen.</summary>
    public static bool HaltungGehenAltFuerTests { get => HaltungGehenAlt.Value; set => HaltungGehenAlt.Value = value; }

    /// <summary>
    /// Nach einem Schritt in der Haltung <paramref name="haltung"/> (vor dem Schritt gemessen): hat die Haltung an der neuen Stelle
    /// Platz, geht sie mit; sonst steht die Figur auf (das Hindernis erzwingt es, Durchdringung 0).
    /// </summary>
    private void KoerperNachSchritt(Koerperhaltung haltung)
    {
        if (haltung == Koerperhaltung.Stehen || _koerper != haltung || !ReferenceEquals(Innen, _koerperStelle.Innen) || !KoerperMoeglich)
        {
            return;
        }
        if (KoerperFreiBei(FigurX, FigurZ, FigurRichtung, haltung))
        {
            _koerperStelle = (Innen, FigurX, FigurZ);
            return;
        }
        _koerper = Koerperhaltung.Stehen;
        Hinweis(SpielProbeTexte.HaltungKeinPlatzMehr);
        Geaendert?.Invoke();
    }

    /// <summary>Die Figur ist gerade im Sprung.</summary>
    public bool Springt => _sprung is not null && KoerperMoeglich && Koerper == Koerperhaltung.Stehen;

    /// <summary>Hoehe ueber dem Boden im Sprung (Anteil der Figurhoehe, Parabel bis <see cref="Probe.Innenraum.SprungHoch"/>), 0 = am Boden.</summary>
    public float SprungHoehe => Springt && _sprung is { } t ? 4f * Probe.Innenraum.SprungHoch * (t / SprungDauer) * (1f - (t / SprungDauer)) : 0f;

    /// <summary>Modell der Haltung zum Zeichnen (<see cref="Figur.HaltungBild"/>), null = Stehen oder Gehen wie bisher.</summary>
    public string? HaltungModell => Figur.HaltungBild(Koerper, Springt);

    /// <summary>Hinweis der letzten Haltungs-Aktion (Ansage am Screenreader und Zeile), null = keiner.</summary>
    public string? HaltungHinweis { get; private set; }

    /// <summary>Knopf „Springen“ (Leertaste). false = geht gerade nicht (Hinweis in <see cref="HaltungHinweis"/>).</summary>
    public bool Springen()
    {
        if (!KoerperMoeglich)
        {
            return Abgelehnt(SitztOderLiegt || Mitfahrt != MitfahrtStufe.Keine ? SpielProbeTexte.ErstAufstehen : null);
        }
        if (_sprung is not null)
        {
            return true;
        }
        if (!KoerperFreiAlt.Value && Innen is { } innen && !innen.SprungFrei(new Vector2(FigurX, FigurZ), FigurRichtung * MathF.PI / 180f))
        {
            return Abgelehnt(SpielProbeTexte.KeinPlatzSpringen);
        }
        _koerper = Koerperhaltung.Stehen;
        _sprung = 0f;
        Hinweis(SpielProbeTexte.DuSpringst);
        Geaendert?.Invoke();
        return true;
    }

    /// <summary>
    /// Knopf „Ducken“: ducken bzw. (geduckt) wieder aufstehen. <paramref name="blick"/> = Richtung (Grad wie die Figur), in die die
    /// Figur gezeichnet schaut (Ich-Sicht: die Blickrichtung); ohne: <see cref="FigurRichtung"/>.
    /// </summary>
    public bool Ducken(float? blick = null) => Einnehmen(Koerperhaltung.Ducken, blick);

    /// <summary>Knopf „Kriechen“: auf alle viere bzw. (kriechend) wieder aufstehen (<paramref name="blick"/> wie bei <see cref="Ducken"/>).</summary>
    public bool Kriechen(float? blick = null) => Einnehmen(Koerperhaltung.Kriechen, blick);

    private bool Einnehmen(Koerperhaltung haltung, float? blick)
    {
        if (!KoerperMoeglich)
        {
            return Abgelehnt(SitztOderLiegt || Mitfahrt != MitfahrtStufe.Keine ? SpielProbeTexte.ErstAufstehen : null);
        }
        if (Koerper == haltung)
        {
            _koerper = Koerperhaltung.Stehen;
            Hinweis(SpielProbeTexte.DuStehst);
            Geaendert?.Invoke();
            return true;
        }
        if (Geht && HaltungGehenAlt.Value)
        {
            // bis A 0.98.0 nur an der Stelle (P12-303): erst nach der Ankunft (seit A 0.99.0 auch im Gehen)
            return Abgelehnt(SpielProbeTexte.ErstAnkommenHaltung);
        }
        if (KoerperDrehenAlt.Value)
        {
            // Gegenprobe (Stand bis A 0.97.0): ohne Platz vor ihr drehte sich die Figur von selbst (vor einer Tuer um 90 Grad)
            foreach (var dreh in KoerperRichtungen)
            {
                var grad = (blick ?? FigurRichtung) + dreh;
                if (KoerperFreiBei(FigurX, FigurZ, grad, haltung))
                {
                    return Eingenommen(haltung, grad, FigurX, FigurZ);
                }
            }
            return Abgelehnt(haltung == Koerperhaltung.Ducken ? SpielProbeTexte.KeinPlatzDucken : SpielProbeTexte.KeinPlatzKriechen);
        }
        // Welle A 0.98.0 (Weisung P12-315, Sammelliste 396): die Figur behaelt ihre Richtung. Reicht vor ihr der Platz nicht (vor einer
        // Tuer oder Wand steht sie dicht davor), rutscht sie in kleinen Schritten zurueck (oder, steht hinter ihr etwas, vor), bis die
        // Haltung passt; sonst „kein Platz“.
        var richtung = blick ?? FigurRichtung;
        var b = richtung * MathF.PI / 180f;
        var zurueck = new Vector2(-MathF.Sin(b), -MathF.Cos(b));
        var h = Innen is not null ? Probe.Innenraum.FigurHoehe : Figur.StadtHoehe;
        for (var j = 0; j <= 2 * KoerperZurueckSchritte; j++)
        {
            // der Reihe nach: an der Stelle, 1 Schritt zurueck, 1 vor, 2 zurueck, 2 vor ... (zuerst zurueck: vor einer Tuer oder Wand)
            var i = (j + 1) / 2 * (j % 2 == 1 ? 1 : -1);
            var p = new Vector2(FigurX, FigurZ) + (zurueck * (i * KoerperZurueckSchritt * h));
            if (KoerperFreiBei(p.X, p.Y, richtung, haltung) && (i == 0 || StehenKoennen(p.X, p.Y)))
            {
                return Eingenommen(haltung, richtung, p.X, p.Y);
            }
        }
        return Abgelehnt(haltung == Koerperhaltung.Ducken ? SpielProbeTexte.KeinPlatzDucken : SpielProbeTexte.KeinPlatzKriechen);
    }

    /// <summary>Welle A 0.98.0: Schrittweite (Anteil der Figurhoehe), um die die Figur fuer eine Haltung zurueck- oder vorrutscht.</summary>
    public const float KoerperZurueckSchritt = 0.02f;

    /// <summary>Welle A 0.98.0: hoechstens so viele Schritte je Richtung (zusammen 0,4 Figurhoehen, mehr als der Koerper nach vorn braucht).</summary>
    public const int KoerperZurueckSchritte = 20;

    private static readonly AsyncLocal<bool> KoerperDrehenAlt = new();

    /// <summary>Gegenprobe (nur Tests, AsyncLocal): ohne Platz vor ihr dreht sich die Figur von selbst (Stand bis A 0.97.0, Sammelliste 396).</summary>
    public static void KoerperDrehenAltFuerTests(bool an) => KoerperDrehenAlt.Value = an;

    /// <summary>Kann die Figur an dieser Stelle stehen (drinnen frei von Einrichtung und Waenden, draussen auf dem Laufraster)?</summary>
    private bool StehenKoennen(float x, float z) => Innen is { } innen
        ? innen.KoerperFrei(new Vector2(x, z), FigurRichtung * MathF.PI / 180f, Koerperhaltung.Stehen)
        : Szene.Laufraster.KannStehen(x, z) && !Szene.Laufraster.FeinGesperrt(x, z);

    private bool Eingenommen(Koerperhaltung haltung, float grad, float x, float z)
    {
        FigurRichtung = ((grad % 360f) + 360f) % 360f;
        FigurX = x;
        FigurZ = z;
        _koerper = haltung;
        _koerperStelle = (Innen, x, z);
        _sprung = null;
        Hinweis(haltung == Koerperhaltung.Ducken ? SpielProbeTexte.DuDuckst : SpielProbeTexte.DuKriechst);
        Geaendert?.Invoke();
        return true;
    }

    /// <summary>
    /// Ich-Sicht (nah): der Koerper in Ducken oder Kriechen dreht mit dem Blick nach <paramref name="grad"/>, wenn dort Platz ist.
    /// false = kein Platz (die Figur bleibt, wie sie ist; die Kamera bleibt bei ihr).
    /// </summary>
    public bool KoerperDrehen(float grad)
    {
        var haltung = Koerper;
        if (haltung == Koerperhaltung.Stehen || MathF.Abs((((grad - FigurRichtung) % 360f) + 540f) % 360f - 180f) < 0.01f)
        {
            return true;
        }
        if (!KoerperFreiBei(FigurX, FigurZ, grad, haltung))
        {
            return false;
        }
        FigurRichtung = ((grad % 360f) + 360f) % 360f;
        return true;
    }

    /// <summary>
    /// Ist an der Stelle Platz fuer die Haltung mit Blick <paramref name="grad"/>? Drinnen: ganz im Raum, nichts ueber die Mitte einer
    /// Wand, keine Beruehrung mit Einrichtung, Fest-Deko und Personen (<see cref="Probe.Innenraum.KoerperFrei"/>). Draussen: jeder Punkt der
    /// Grundflaeche (Raster 0,25 der Figurhoehe) ist ein Punkt, an dem die Figur stehen kann (Laufraster, keine Fahrbahn ausser Uebergaengen).
    /// </summary>
    public bool KoerperFreiBei(float x, float z, float grad, Koerperhaltung haltung)
    {
        if (KoerperFreiAlt.Value || haltung == Koerperhaltung.Stehen)
        {
            return true;
        }
        var blick = grad * MathF.PI / 180f;
        if (Innen is { } innen)
        {
            return innen.KoerperFrei(new Vector2(x, z), blick, haltung);
        }
        var (hinten, vorn, halb) = Probe.Innenraum.KoerperGrundflaeche(haltung);
        var h = Figur.StadtHoehe;
        var f = new Vector2(MathF.Sin(blick), MathF.Cos(blick));
        var q = new Vector2(f.Y, -f.X);
        const int schritte = 4;
        for (var i = 0; i <= schritte; i++)
        {
            var laengs = (-hinten + ((hinten + vorn) * i / schritte)) * h;
            for (var j = 0; j <= 2; j++)
            {
                var p = new Vector2(x, z) + (f * laengs) + (q * ((j - 1) * halb * h));
                if (!Szene.Laufraster.KannStehen(p.X, p.Y) || Szene.Laufraster.FeinGesperrt(p.X, p.Y))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>Je Bild: den Sprung weiterfuehren. true = die Figur ist im Sprung (neues Bild noetig).</summary>
    public bool HaltungTakt(float sekunden)
    {
        if (_sprung is not { } t)
        {
            return false;
        }
        t += Math.Max(0f, sekunden);
        _sprung = t >= SprungDauer || !KoerperMoeglich ? null : t;
        return true;
    }

    /// <summary>Gegenprobe bis A 0.98.0: Gehen beendet Ducken und Kriechen (aufgerufen, sobald die Figur einen Weg hat).</summary>
    private void KoerperBeimGehen()
    {
        if (HaltungGehenAlt.Value && _koerper != Koerperhaltung.Stehen && Geht)
        {
            _koerper = Koerperhaltung.Stehen;
        }
    }

    private bool Hinweis(string? text)
    {
        HaltungHinweis = text;
        return false;
    }

    /// <summary>Hinweise, die (anders als die Ansage beim Gelingen) auch oben in der Zeile stehen, bis sich die Zeile aendert.</summary>
    private static readonly string[] ZeilenHinweise =
    [
        SpielProbeTexte.KeinPlatzDucken, SpielProbeTexte.KeinPlatzKriechen, SpielProbeTexte.KeinPlatzSpringen,
        SpielProbeTexte.ErstAnkommenHaltung, SpielProbeTexte.ErstAufstehen,
    ];

    /// <summary>Nicht gelungen: Hinweis ansagen und oben vor die Zeile setzen (ein frueherer Hinweis dieser Art faellt weg).</summary>
    private bool Abgelehnt(string? text)
    {
        Hinweis(text);
        if (text is not null)
        {
            var rest = Zeile;
            foreach (var alt in ZeilenHinweise)
            {
                if (rest.StartsWith(alt, StringComparison.Ordinal))
                {
                    rest = rest[alt.Length..].TrimStart('\n');
                }
            }
            Zeile = rest.Length > 0 ? text + "\n" + rest : text;
        }
        Geaendert?.Invoke();
        return false;
    }

    /// <summary>Knopf „Zurücklehnen“ bzw. „Aufrichten“ auf Sofa, Sessel und Liegestuhl; false = keine dieser Aktionen.</summary>
    private bool ZurueckAktion(string aktion)
    {
        if (aktion is not (AktionZuruecklehnen or AktionAufrichten) || MeinPlatz is not { } p || Innen is not { } innen)
        {
            return false;
        }
        var neu = aktion == AktionZuruecklehnen && p.Art == SitzArt.Sitzen ? innen.ZurueckZu(p)
            : aktion == AktionAufrichten && p.Art == SitzArt.Zurueck ? innen.SitzZu(p) : null;
        if (neu is not null)
        {
            MeinPlatz = neu;
            InnenText();
            Geaendert?.Invoke();
        }
        return true;
    }

    /// <summary>Aktion des Knopfs „Zurücklehnen“.</summary>
    public const string AktionZuruecklehnen = "zuruecklehnen";

    /// <summary>Aktion des Knopfs „Aufrichten“.</summary>
    public const string AktionAufrichten = "aufrichten";
}
