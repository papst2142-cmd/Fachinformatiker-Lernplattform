using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 8 W11 (Nico P8-49, Variante c): Ausstattung eines Bahnuebergangs an der Strasse, je Seite eine: Andreaskreuz aus
/// zwei rot-weiss gestreiften Balken mit Tiefe, grauer Mast auf dunklem Sockel, Blinklichtkasten mit zwei roten Lichtern
/// (nachts leuchten nur sie, rot) und eine offene Halbschranke mit Antrieb. Alles steht vor dem Gleis neben der Fahrbahn,
/// ausserhalb des Lichtraums (<see cref="LichtraumHalb"/>), den spaeter auch der fahrende Zug nutzt. Der Schrankenbaum
/// zeigt offen nach oben; er dreht sich spaeter in der Ebene des Kreuzes (quer zur Strasse), quert also nur die Fahrbahn.
/// Nur Darstellung (mit den Laternen im Paket-Netz, <see cref="StadtSzene.PaketNetzBauen"/>), kein Einfluss auf den Spielstand.
/// </summary>
public static class Bahnuebergang
{
    /// <summary>
    /// Breite des breitesten Zugwagens im Bild (Train Kit Wagen b, mit der Skala des Wagens a auf <see cref="StadtSzene.ZugBreite"/>
    /// gebracht: 0,6855; Test prueft alle Wagen).
    /// </summary>
    public const float BreitesterWagen = 0.686f;

    /// <summary>Halbe Breite des Lichtraums: halbe Breite des breitesten Zugwagens plus 0,15 (von der Gleismitte).</summary>
    public const float LichtraumHalb = (BreitesterWagen / 2) + 0.15f;

    /// <summary>Abstand des Masts von der Strassenmitte: Schranke und Antrieb bleiben ausserhalb von Fahrbahn und Rand.</summary>
    public const float Seitlich = Nachtlicht.Rand + 0.23f;

    /// <summary>Abstand der Ausstattung vom Lichtraum (Sockel, Antrieb und Schranke reichen bis 0,06 zum Gleis hin).</summary>
    public const float Luft = 0.05f;

    /// <summary>Lichter: tagsueber dunkelrot; mit der Nachtmarke leuchten sie rot (Shader erkennt genau diese Farbe).</summary>
    public static readonly Vector3 Licht = new(0.55f, 0.07f, 0.06f);

    /// <summary>Halbmesser des Grundrisses um den Mast (fuer Laternen, Gruen und Deko).</summary>
    public const float Grundriss = 0.3f;

    private static readonly Vector3 Weiss = new(0.93f, 0.93f, 0.92f);
    private static readonly Vector3 Rot = new(0.80f, 0.10f, 0.10f);
    private static readonly Vector3 Grau = new(0.55f, 0.57f, 0.60f);
    private static readonly Vector3 Dunkel = new(0.16f, 0.17f, 0.19f);

    /// <summary>
    /// Eine Ausstattung: Fuss des Masts, Blickrichtung des Kreuzes (vom Gleis weg, zum kommenden Verkehr), Richtung zur
    /// Strassenmitte und Drehpunkt der Schranke (fuer die spaetere Bewegung).
    /// </summary>
    public sealed record Kreuz(Vector2 Fuss, Vector2 Blick, Vector2 ZurStrasse)
    {
        /// <summary>Lage im Raum: x = von der Strasse weg, y = oben, z = Blick (rechtshaendig, nicht gespiegelt).</summary>
        public Matrix4x4 Lage => new(-ZurStrasse.X, 0, -ZurStrasse.Y, 0, 0, 1, 0, 0, Blick.X, 0, Blick.Y, 0, Fuss.X, 0, Fuss.Y, 1);

        /// <summary>Drehpunkt der Schranke in der Welt (oben am Antrieb).</summary>
        public Vector2 Drehpunkt => Fuss + (ZurStrasse * -SchrankeX);
    }

    // zur Strasse hin = negatives x im eigenen Raum
    private const float AntriebX = -0.16f;
    private const float SchrankeX = -0.21f;
    private const float SchrankeVon = 0.24f;
    /// <summary>Laenge des Schrankenbaums (reicht geschlossen bis etwa zur Strassenmitte).</summary>
    public const float SchrankeLaenge = 0.5f;

    /// <summary>
    /// Plaetze an einem Bahnuebergang (Nico P8-50, wie in echt bei Rechtsverkehr): je Fahrtrichtung vor dem Gleis auf der
    /// rechten Strassenseite des ankommenden Autos (rechts wie die Spur in <see cref="Verkehr"/>), die Schranke sperrt
    /// geschlossen dessen rechte Fahrspur bis zur Strassenmitte; die beiden Ausstattungen stehen sich diagonal gegenueber.
    /// <paramref name="richtung"/> = Richtung der Strasse, <paramref name="sinus"/> = |sin| des Winkels zum Gleis. Liegt das
    /// Gleis (z. B. im Bogen) naeher, rueckt die Ausstattung in Schritten von 0,02 entlang der Strasse vom Gleis weg, bis
    /// keine Ecke im Lichtraum liegt.
    /// </summary>
    public static IEnumerable<Kreuz> Plaetze(Vector2 mitte, Vector2 richtung, float sinus, IReadOnlyList<Vector2> gleis)
    {
        foreach (var vor in (float[])[-1f, 1f])
        {
            // das Auto kommt von der Seite "vor" und faehrt in Richtung -richtung * vor; rechts davon = Rechts(fahrt)
            var rechts = Rechts(-richtung * vor);
            var laengs = (LichtraumHalb + Luft + 0.06f) / sinus;
            Kreuz k;
            do
            {
                k = new Kreuz(mitte + (richtung * vor * laengs) + (rechts * Seitlich), richtung * vor, -rechts);
                laengs += 0.02f;
            }
            while (Grundpunkte(k).Any(p => Kurve.Abstand(p, gleis) < LichtraumHalb + Luft) && laengs < 5f);
            yield return k;
        }
    }

    /// <summary>Rechte Seite zur Fahrtrichtung, wie die rechte Spur der Autos (<see cref="Kurve.Versetzt"/> mit positivem Abstand).</summary>
    public static Vector2 Rechts(Vector2 fahrt) => new(-fahrt.Y, fahrt.X);

    private static readonly Lazy<Netz> Muster = new(() => Bauen().Netz);

    /// <summary>
    /// Alle Ecken der Ausstattung auf der Karte (x/z), dazu das Ende der Schranke, wenn sie spaeter geschlossen quer ueber
    /// der Fahrbahn liegt (vom Drehpunkt <see cref="SchrankeLaenge"/> zur Strassenmitte).
    /// </summary>
    public static List<Vector2> Grundpunkte(Kreuz k)
    {
        var netz = Muster.Value;
        var lage = k.Lage;
        var punkte = new List<Vector2>(netz.AnzahlEcken + 1);
        for (var e = 0; e < netz.AnzahlEcken; e++)
        {
            var w = Vector3.Transform(netz.Ort(e), lage);
            punkte.Add(new Vector2(w.X, w.Z));
        }
        punkte.Add(k.Drehpunkt + (k.ZurStrasse * SchrankeLaenge));
        return punkte;
    }

    private static Teil Q(float x1, float y1, float z1, float x2, float y2, float z2, Vector3 f) => new(TeilArt.Quader, x1, y1, z1, x2, y2, z2, f);

    /// <summary>Gestreifter Balken laengs x um die Mitte, gedreht um z.</summary>
    private static void Balken(Netz netz, float laenge, float hoehe, float tiefe, float winkel, Vector3 mitte, int streifen)
    {
        var b = new Netz();
        for (var i = 0; i < streifen; i++)
        {
            var x1 = (-laenge / 2) + (laenge * i / streifen);
            var x2 = (-laenge / 2) + (laenge * (i + 1) / streifen);
            b.Hinzu(Q(x1, -hoehe / 2, -tiefe / 2, x2, hoehe / 2, tiefe / 2, i % 2 == 0 ? Rot : Weiss));
        }
        netz.Hinzu(b, Matrix4x4.CreateRotationZ(winkel) * Matrix4x4.CreateTranslation(mitte));
    }

    /// <summary>Netz einer Ausstattung im eigenen Raum (x = von der Strasse weg, y = oben, z = Blick) und die Ecken der Lichter.</summary>
    public static (Netz Netz, int LichtVon, int LichtBis) Bauen()
    {
        var s = new Netz();
        s.Hinzu(Q(-0.06f, 0f, -0.06f, 0.06f, 0.05f, 0.06f, Dunkel));
        s.Hinzu(Q(-0.022f, 0.05f, -0.022f, 0.022f, 0.80f, 0.022f, Grau));
        s.Hinzu(Q(-0.03f, 0.78f, -0.03f, 0.03f, 0.81f, 0.03f, Dunkel));
        // Halter und Kreuz vor dem Mast (zum Verkehr hin)
        s.Hinzu(Q(-0.035f, 0.63f, 0.022f, 0.035f, 0.70f, 0.034f, Dunkel));
        var w = MathF.PI * 32f / 180f;
        Balken(s, 0.36f, 0.05f, 0.022f, w, new Vector3(0, 0.665f, 0.046f), 5);
        Balken(s, 0.36f, 0.05f, 0.022f, -w, new Vector3(0, 0.665f, 0.070f), 5);
        // Blinklichtkasten mit Schirmen und zwei Lichtern
        s.Hinzu(Q(-0.12f, 0.42f, 0.02f, 0.12f, 0.52f, 0.075f, Dunkel));
        foreach (var x in (float[])[-0.065f, 0.065f])
        {
            s.Hinzu(Q(x - 0.035f, 0.505f, 0.075f, x + 0.035f, 0.515f, 0.11f, Dunkel));
        }
        var lichtVon = s.AnzahlEcken;
        foreach (var x in (float[])[-0.065f, 0.065f])
        {
            s.Hinzu(Q(x - 0.028f, 0.442f, 0.075f, x + 0.028f, 0.498f, 0.088f, Licht));
        }
        var lichtBis = s.AnzahlEcken;
        // Antrieb zur Strasse hin und offene Halbschranke (senkrecht, dreht spaeter in der Ebene x/y ueber die Fahrbahn)
        s.Hinzu(Q(AntriebX - 0.05f, 0f, -0.05f, AntriebX + 0.05f, SchrankeVon, 0.05f, Grau));
        s.Hinzu(Q(AntriebX - 0.055f, SchrankeVon, -0.055f, AntriebX + 0.055f, SchrankeVon + 0.015f, 0.055f, Dunkel));
        const int teile = 6;
        for (var i = 0; i < teile; i++)
        {
            var y1 = SchrankeVon + (SchrankeLaenge * i / teile);
            s.Hinzu(Q(SchrankeX - 0.014f, y1, -0.014f, SchrankeX + 0.014f, y1 + (SchrankeLaenge / teile), 0.014f, i % 2 == 0 ? Weiss : Rot));
        }
        return (s, lichtVon, lichtBis);
    }

    /// <summary>Alle Ausstattungen in ein Netz der Stadt; die Lichter bekommen die Nachtmarke.</summary>
    public static void Hinzu(Netz ziel, IEnumerable<Kreuz> kreuze)
    {
        var (s, lichtVon, lichtBis) = Bauen();
        foreach (var k in kreuze)
        {
            var von = ziel.AnzahlEcken;
            ziel.Hinzu(s, k.Lage);
            ziel.Leuchten(von + lichtVon, von + lichtBis);
        }
    }
}
