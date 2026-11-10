using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Farbe, die eine Ampel fuer eine Richtung zeigt.</summary>
public enum AmpelZustand
{
    Gruen,
    Gelb,
    Rot,
    RotGelb,
}

/// <summary>
/// Eine gelegte Ampel: Mitte genau auf einer Kreuzung oder Einmuendung, Arme = Richtungen der Strassen von der Mitte weg
/// (Laenge 1, mindestens drei). Achse 0 = alle Arme etwa gleich oder gegenueber dem ersten Arm, Achse 1 = die quer dazu.
/// </summary>
public sealed record Ampel(int Nr, Vector2 Mitte, IReadOnlyList<Vector2> Arme)
{
    /// <summary>Achse (0 oder 1) des Arms <paramref name="i"/>.</summary>
    public int Achse(int i) => Ampeln.AchseVon(Arme[0], Arme[i]);

    /// <summary>Achse einer Fahrtrichtung (ein Auto, das in Richtung <paramref name="fahrt"/> auf die Kreuzung zufaehrt).</summary>
    public int AchseFuer(Vector2 fahrt) => Ampeln.AchseVon(Arme[0], fahrt);

    /// <summary>Die Furten (je Arm ein Streifen quer ueber die Fahrbahn, wie ein Zebrastreifen ohne Balken).</summary>
    public IEnumerable<Zebra> Furten => Arme.Select(a => new Zebra(0, Mitte + (a * Ampeln.FurtMitte), a));

    /// <summary>Die Masten: je Arm einer rechts der ankommenden Spur vor der Haltelinie, der Kopf blickt zum Verkehr.</summary>
    public IEnumerable<(Vector2 Fuss, Vector2 Blick, int Arm)> Masten =>
        Arme.Select((a, i) => (Mitte + (a * Ampeln.HalteAbstand) + (Ampeln.RechtsAnkommend(a) * Ampeln.MastSeitlich), a, i));
}

/// <summary>
/// Welle A 0.60.0 / S 0.35.0 (Weisung P12-166, Kurzplan P12-79 Wunsch 6, Nico P12-79 Frage 5 = a): Ampeln, die Nico im
/// Werkzeug auf eine Kreuzung oder Einmuendung setzt. Daten <c>stadtdaten.ampeln</c> = <c>[{x, y}, …]</c> (Mitte der
/// Kreuzung); die Arme kommen aus den Strassen (<see cref="Legen"/>). Nur Darstellung und Laufraster wie am Zebrastreifen:
/// je Arm ein Mast mit Signalkopf (mit den Laternen im Paket-Netz), Haltelinie und Furt als Flaechen im Stadtnetz, die
/// leuchtenden Lampen in einem eigenen Zeichenaufruf (<see cref="LichtNetze"/>, eine Gruppe je Achse und Farbe). Die Figur
/// geht ueber die Furten wie ueber einen Zebrastreifen (sie wartet nie); Autos halten bei Rot, Gelb und Rot-Gelb vor der
/// Haltelinie und wie bisher vor der Figur. Der Takt kommt aus der Fahrzeit des Verkehrs (<see cref="Verkehr.Zeit"/>),
/// keine Uhr, kein Zufall, keine Ereignisart, kein Feld im Spielstand. Am Kreisverkehr gibt es keine Ampel. Ohne den
/// Abschnitt (Fassung 0) aendert sich nichts.
/// </summary>
public static class Ampeln
{
    public const string Abschnitt = "ampeln";

    /// <summary>So weit darf ein gesetzter Punkt von der Mitte einer Kreuzung weg sein (Werkzeug: Einrasten).</summary>
    public const float Fang = 0.95f;

    /// <summary>So nah an der Mitte der Kreuzung muss eine gespeicherte Ampel liegen (das Werkzeug legt sie genau darauf).</summary>
    public const float AufMitte = 0.05f;

    /// <summary>Abstand der Furt-Mitte von der Mitte der Kreuzung (Rand der Kreuzung, etwas Luft, halbe Furt).</summary>
    public const float FurtMitte = (StadtSzene.StrassenBreite / 2) + 0.05f + (Zebrastreifen.Breite / 2);

    /// <summary>Abstand der Haltelinie (und der Masten) von der Mitte der Kreuzung.</summary>
    public const float HalteAbstand = FurtMitte + (Zebrastreifen.Breite / 2) + 0.08f;

    /// <summary>Ein Auto haelt, wenn seine Front so weit vor der Mitte der Kreuzung steht (vor der Haltelinie).</summary>
    public const float HalteVon = HalteAbstand + 0.04f;

    /// <inheritdoc cref="HalteVon"/>
    public const float HalteBis = HalteVon + 0.3f;

    /// <summary>Abstand des Masts von der Strassenmitte (knapp neben der Fahrbahn, auf dem Gehweg).</summary>
    public const float MastSeitlich = (StadtSzene.StrassenBreite / 2) + 0.12f;

    /// <summary>Halbmesser des Grundrisses um einen Mast (fuer Laternen, Baeume und Katalog-Stuecke).</summary>
    public const float MastGrundriss = 0.3f;

    /// <summary>Dauer der Phasen in Sekunden Fahrzeit: Gruen, Gelb, alle Rot, Rot-Gelb; ein Umlauf fuer beide Achsen.</summary>
    public const double GruenZeit = 7, GelbZeit = 2, AlleRotZeit = 1, RotGelbZeit = 1;

    /// <summary>Ein ganzer Umlauf (Achse 0 und Achse 1 nacheinander).</summary>
    public const double Umlauf = 2 * (GruenZeit + GelbZeit + AlleRotZeit + RotGelbZeit);

    /// <summary>Farben der Lampen, die leuchten (genau diese erkennt der Shader und leuchtet nachts in ihrer Farbe).</summary>
    public static readonly Vector3 LichtRot = new(0.92f, 0.10f, 0.08f);

    /// <inheritdoc cref="LichtRot"/>
    public static readonly Vector3 LichtGelb = new(0.95f, 0.66f, 0.08f);

    /// <inheritdoc cref="LichtRot"/>
    public static readonly Vector3 LichtGruen = new(0.12f, 0.80f, 0.32f);

    private static readonly Vector3[] Leuchtend = [LichtRot, LichtGelb, LichtGruen];
    private static readonly Vector3[] Aus = [new(0.20f, 0.05f, 0.05f), new(0.20f, 0.15f, 0.04f), new(0.04f, 0.14f, 0.07f)];
    private static readonly Vector3 Grau = new(0.48f, 0.50f, 0.53f);
    private static readonly Vector3 Dunkel = new(0.12f, 0.13f, 0.14f);

    /// <summary>Hoehe der Lampen-Mitten (Rot oben, Gelb, Gruen unten).</summary>
    private static readonly float[] LampeY = [0.735f, 0.65f, 0.565f];

    private const float LampeHalb = 0.028f;

    /// <summary>
    /// Gespeicherte Punkte (null = kein Abschnitt). Ein falsch gespeicherter Eintrag ist null in der Liste; die Pruefung
    /// meldet ihn rot, die Szene laesst ihn weg.
    /// </summary>
    public static List<Vector2?>? Punkte(PyObjekt? stadtDaten)
    {
        if (stadtDaten is null || !Py.Hat(stadtDaten, Abschnitt))
        {
            return null;
        }
        if (stadtDaten[Abschnitt] is not List<object?> liste)
        {
            return [null];
        }
        return [.. liste.Select(Zebrastreifen.Lesen)];
    }

    /// <summary>Achse eines Arms gegen den ersten Arm: etwa gleich oder gegenueber = 0, sonst 1.</summary>
    public static int AchseVon(Vector2 erster, Vector2 richtung) => MathF.Abs(Vector2.Dot(erster, richtung)) >= 0.7071f ? 0 : 1;

    /// <summary>Rechts eines Autos, das ueber den Arm <paramref name="arm"/> auf die Kreuzung zufaehrt (Richtung -arm).</summary>
    public static Vector2 RechtsAnkommend(Vector2 arm) => Bahnuebergang.Rechts(-arm);

    /// <summary>Liegt <paramref name="p"/> auf einem geschlossenen Ring (Kreisverkehr)?</summary>
    public static bool AmRing(Vector2 p, IReadOnlyList<IReadOnlyList<Vector2>> strassen) =>
        strassen.Any(s => Kreisverkehr.Geschlossen(s) && Kurve.Abstand(p, s) < 0.1f);

    /// <summary>
    /// Die Richtungen aller Strassen von <paramref name="mitte"/> weg (Laenge 1): eine Strasse, die durch die Mitte geht,
    /// gibt zwei Arme, eine, die dort endet, einen. Fast gleiche Richtungen (unter 10 Grad) zaehlen einmal.
    /// </summary>
    public static List<Vector2> Arme(Vector2 mitte, IReadOnlyList<IReadOnlyList<Vector2>> strassen)
    {
        var arme = new List<Vector2>();
        void Dazu(Vector2 d)
        {
            if (d.Length() < 1e-4f)
            {
                return;
            }
            d = Vector2.Normalize(d);
            if (arme.All(a => Vector2.Dot(a, d) < 0.985f))
            {
                arme.Add(d);
            }
        }
        foreach (var s in strassen)
        {
            for (var i = 1; i < s.Count; i++)
            {
                var (a, b) = (s[i - 1], s[i]);
                var d = b - a;
                var l = d.Length();
                if (l < 1e-4f)
                {
                    continue;
                }
                var t = Math.Clamp(Vector2.Dot(mitte - a, d / l), 0f, l);
                if (Vector2.Distance(a + (d / l * t), mitte) > 0.05f)
                {
                    continue;
                }
                if (t > 0.05f)
                {
                    Dazu(a - mitte);
                }
                if (l - t > 0.05f)
                {
                    Dazu(b - mitte);
                }
            }
        }
        return arme;
    }

    /// <summary>
    /// Die Ampel an der naechsten Kreuzung oder Einmuendung (mindestens drei Arme, nicht am Ring eines Kreisverkehrs) bis
    /// <paramref name="fang"/> um <paramref name="p"/>; null = keine. <paramref name="strassen"/> = alle Strassen.
    /// </summary>
    public static Ampel? Legen(int nr, Vector2 p, IReadOnlyList<IReadOnlyList<Vector2>> strassen, float fang = Fang)
    {
        Ampel? beste = null;
        var abstand = float.MaxValue;
        foreach (var k in Verkehr.KreuzungenAus(strassen))
        {
            var a = Vector2.Distance(k, p);
            if (a > fang || a >= abstand || AmRing(k, strassen))
            {
                continue;
            }
            var arme = Arme(k, strassen);
            if (arme.Count >= 3)
            {
                (beste, abstand) = (new Ampel(nr, k, arme), a);
            }
        }
        return beste;
    }

    /// <summary>Alle Ampeln der Stadtdaten, die auf einer Kreuzung liegen (in der Reihenfolge der Daten).</summary>
    public static List<Ampel> Alle(PyObjekt? stadtDaten, IReadOnlyList<IReadOnlyList<Vector2>> strassen)
    {
        var liste = new List<Ampel>();
        var punkte = Punkte(stadtDaten) ?? [];
        for (var i = 0; i < punkte.Count; i++)
        {
            if (punkte[i] is { } p && Legen(i + 1, p, strassen, AufMitte) is { } a)
            {
                liste.Add(a);
            }
        }
        return liste;
    }

    /// <summary>
    /// Was die Ampel fuer <paramref name="achse"/> zur Fahrzeit <paramref name="zeit"/> zeigt. Achse 0: Gruen, Gelb, Rot
    /// (Achse 1 hat Gruen, Gelb, alle Rot), Rot-Gelb; Achse 1 genau einen halben Umlauf spaeter.
    /// </summary>
    public static AmpelZustand Zustand(double zeit, int achse)
    {
        var t = (zeit - (achse * (Umlauf / 2))) % Umlauf;
        if (t < 0)
        {
            t += Umlauf;
        }
        return t < GruenZeit ? AmpelZustand.Gruen
            : t < GruenZeit + GelbZeit ? AmpelZustand.Gelb
            : t < Umlauf - RotGelbZeit ? AmpelZustand.Rot
            : AmpelZustand.RotGelb;
    }

    /// <summary>Leuchtet die Lampe <paramref name="farbe"/> (0 Rot, 1 Gelb, 2 Gruen) im Zustand <paramref name="z"/>?</summary>
    public static bool Leuchtet(AmpelZustand z, int farbe) => z switch
    {
        AmpelZustand.Gruen => farbe == 2,
        AmpelZustand.Gelb => farbe == 1,
        AmpelZustand.RotGelb => farbe is 0 or 1,
        _ => farbe == 0,
    };

    /// <summary>Zahl der Lampen-Gruppen (je Achse Rot, Gelb, Gruen).</summary>
    public const int Gruppen = 6;

    /// <summary>Leuchtet die Gruppe <paramref name="gruppe"/> (Achse × 3 + Farbe) zur Fahrzeit <paramref name="zeit"/>?</summary>
    public static bool GruppeAn(int gruppe, double zeit) => Leuchtet(Zustand(zeit, gruppe / 3), gruppe % 3);

    /// <summary>Lage eines Masts: x = von der Strasse weg, y = oben, z = Blick zum ankommenden Verkehr (rechtshaendig).</summary>
    public static Matrix4x4 Lage(Vector2 fuss, Vector2 blick)
    {
        var q = RechtsAnkommend(blick);
        return new Matrix4x4(q.X, 0, q.Y, 0, 0, 1, 0, 0, blick.X, 0, blick.Y, 0, fuss.X, 0, fuss.Y, 1);
    }

    private static Teil Q(float x1, float y1, float z1, float x2, float y2, float z2, Vector3 f) => new(TeilArt.Quader, x1, y1, z1, x2, y2, z2, f);

    private static readonly Lazy<Netz> MastMuster = new(() =>
    {
        var s = new Netz();
        s.Hinzu(Q(-0.05f, 0f, -0.05f, 0.05f, 0.04f, 0.05f, Dunkel));
        s.Hinzu(Q(-0.02f, 0.04f, -0.02f, 0.02f, 0.80f, 0.02f, Grau));
        // Signalkopf vor dem Mast (zum Verkehr hin), je Lampe ein Schirm und die Lampe in der dunklen Farbe
        s.Hinzu(Q(-0.045f, 0.52f, 0.02f, 0.045f, 0.78f, 0.08f, Dunkel));
        for (var f = 0; f < 3; f++)
        {
            var y = LampeY[f];
            s.Hinzu(Q(-0.036f, y + LampeHalb + 0.004f, 0.08f, 0.036f, y + LampeHalb + 0.012f, 0.11f, Dunkel));
            s.Hinzu(Q(-LampeHalb, y - LampeHalb, 0.08f, LampeHalb, y + LampeHalb, 0.088f, Aus[f]));
        }
        return s;
    });

    /// <summary>Masten aller Ampeln in ein Netz der Stadt (Paket-Netz, nah und fern gleich).</summary>
    public static void Hinzu(Netz ziel, IEnumerable<Ampel> ampeln)
    {
        foreach (var a in ampeln)
        {
            foreach (var (fuss, blick, _) in a.Masten)
            {
                ziel.Hinzu(MastMuster.Value, Lage(fuss, blick));
            }
        }
    }

    /// <summary>
    /// Die leuchtenden Lampen in <see cref="Gruppen"/> Netzen (Gruppe = Achse × 3 + Farbe), in Weltlage, mit der Nachtmarke.
    /// Das Spiel zeichnet alle in einem Aufruf und blendet die Gruppen aus, die gerade nicht leuchten (<see cref="GruppeAn"/>).
    /// Ohne Ampel null.
    /// </summary>
    public static List<Netz>? LichtNetze(IReadOnlyList<Ampel> ampeln)
    {
        if (ampeln.Count == 0)
        {
            return null;
        }
        var netze = Enumerable.Range(0, Gruppen).Select(_ => new Netz()).ToList();
        foreach (var a in ampeln)
        {
            foreach (var (fuss, blick, arm) in a.Masten)
            {
                var lage = Lage(fuss, blick);
                for (var f = 0; f < 3; f++)
                {
                    var y = LampeY[f];
                    var lampe = Netz.Aus([Q(-LampeHalb, y - LampeHalb, 0.0885f, LampeHalb, y + LampeHalb, 0.092f, Leuchtend[f])]);
                    var ziel = netze[(a.Achse(arm) * 3) + f];
                    var von = ziel.AnzahlEcken;
                    ziel.Hinzu(lampe, lage);
                    ziel.Leuchten(von, ziel.AnzahlEcken);
                }
            }
        }
        return netze;
    }

    /// <summary>Haltelinie eines Arms (Anfang, Ende): quer ueber die ankommende Spur, von der Strassenmitte zum Rand.</summary>
    public static (Vector2 A, Vector2 B) Haltelinie(Ampel a, Vector2 arm)
    {
        var m = a.Mitte + (arm * HalteAbstand);
        return (m, m + (RechtsAnkommend(arm) * ((StadtSzene.StrassenBreite / 2) - 0.03f)));
    }

    /// <summary>Breite der Haltelinie.</summary>
    public const float HalteBreite = 0.05f;

    /// <summary>Striche der Furt (Anfang, Ende): zwei gestrichelte Linien an den Raendern der Furt, quer ueber die Fahrbahn.</summary>
    public static IEnumerable<(Vector2 A, Vector2 B)> FurtStriche(Zebra furt)
    {
        const float strich = 0.08f, luecke = 0.06f;
        var halb = (StadtSzene.StrassenBreite / 2) - 0.02f;
        foreach (var seite in (float[])[-1f, 1f])
        {
            var rand = furt.Mitte + (furt.Richtung * seite * ((Zebrastreifen.Breite / 2) - (FurtStrichBreite / 2)));
            for (var q = -halb; q + strich <= halb + 1e-4f; q += strich + luecke)
            {
                yield return (rand + (furt.Quer * q), rand + (furt.Quer * (q + strich)));
            }
        }
    }

    /// <summary>Breite eines Furt-Strichs.</summary>
    public const float FurtStrichBreite = 0.04f;
}
