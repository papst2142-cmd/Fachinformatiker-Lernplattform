using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// S 0.7.0 (Weisung P12-45, Plan P12-26b Abschnitt 9): Bauen in der 3D-Ansicht ohne Oberflaeche. Treffer mit einem Strahl
/// aus der Kamera (zuerst die Fassade eines Hauses, sonst der Punkt am Boden mit demselben Treffertest wie die Draufsicht),
/// Umrisse fuer den Schatten am Boden, Vorschau eines Schritts auf einer Kopie des Stands und der Vergleich der Funde
/// (gruen = keine neuen roten Funde, rot = mindestens einer). Die Schritte selbst laufen ueber dieselben Wege wie in der
/// Draufsicht (KartenFlaeche, Verlauf). Kein Treffer ueber die Grafikkarte: gleich am PC und am Tablet.
/// </summary>
public static class Bauen3D
{
    /// <summary>Neigung, mit der das Spiel die Stadt zeigt (Vorgabe von <see cref="Kamera.Neigung"/>, Test).</summary>
    public const float NeigungSpiel = 52f;

    /// <summary>Kamera kippen: flachster und steilster Blick in Grad.</summary>
    public const float NeigungMin = 25f;

    public const float NeigungMax = 85f;

    /// <summary>Hoehe eines Hauses, wenn die Szene keine kennt (Gebaeude ohne Grafik).</summary>
    public const float HausHoeheVorgabe = 1.6f;

    /// <summary>Hoehe, in der der Schatten liegt (knapp ueber Boden und Strasse).</summary>
    public const float SchattenHoehe = 0.03f;

    public static float Kippen(float neigung, float grad) => Math.Clamp(neigung + grad, NeigungMin, NeigungMax);

    /// <summary>Greifradius am Boden: wie etwa 10 Bildpunkte in der Draufsicht, mit dem Kameraabstand groesser.</summary>
    public static double Greifen(float abstand) => Math.Max(0.3, abstand * 0.03);

    /// <summary>Haus als Quader (Grundflaeche aus der Karte, Hoehe aus der Szene).</summary>
    public sealed record Kasten(AussenStelle Stelle, Vector3 Min, Vector3 Max);

    /// <summary>
    /// Alle Haeuser (mit und ohne Ort) als Quader. <paramref name="hoehe"/> liefert zur Grundflaeche (x, y, w, h) die Hoehe
    /// aus der gebauten Szene oder null; dann gilt <see cref="HausHoeheVorgabe"/>.
    /// </summary>
    public static List<Kasten> Haeuser(PyObjekt aussen, Func<(double X, double Y, double W, double H), float?>? hoehe = null) =>
    [
        .. AussenBearbeitung.Alle(aussen).Where(a => a.Stelle.Art is AussenArt.Haus or AussenArt.Gebaeude).Select(a =>
        {
            var f = AussenBearbeitung.HausFlaeche(aussen, a.Stelle);
            var h = hoehe?.Invoke(f) ?? HausHoeheVorgabe;
            return new Kasten(a.Stelle, new Vector3((float)f.X, 0, (float)f.Y), new Vector3((float)(f.X + f.W), Math.Max(0.2f, h), (float)(f.Y + f.H)));
        }),
    ];

    /// <summary>Hoehe eines Hauses aus den Quadern der Szene: der Quader, der die Mitte der Grundflaeche enthaelt.</summary>
    public static float? HoeheAus(IEnumerable<(Vector3 Min, Vector3 Max)> szene, (double X, double Y, double W, double H) f)
    {
        var mx = (float)(f.X + (f.W / 2));
        var mz = (float)(f.Y + (f.H / 2));
        foreach (var (min, max) in szene)
        {
            if (mx >= min.X && mx <= max.X && mz >= min.Z && mz <= max.Z)
            {
                return max.Y;
            }
        }
        return null;
    }

    /// <summary>Abstand entlang des Strahls bis zum Quader oder null (Slab-Verfahren).</summary>
    public static float? Schnitt(Vector3 start, Vector3 richtung, Vector3 min, Vector3 max)
    {
        var tmin = 0f;
        var tmax = float.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            var s = i == 0 ? start.X : i == 1 ? start.Y : start.Z;
            var r = i == 0 ? richtung.X : i == 1 ? richtung.Y : richtung.Z;
            var lo = i == 0 ? min.X : i == 1 ? min.Y : min.Z;
            var hi = i == 0 ? max.X : i == 1 ? max.Y : max.Z;
            if (MathF.Abs(r) < 1e-9f)
            {
                if (s < lo || s > hi)
                {
                    return null;
                }
                continue;
            }
            var t1 = (lo - s) / r;
            var t2 = (hi - s) / r;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }
            tmin = Math.Max(tmin, t1);
            tmax = Math.Min(tmax, t2);
            if (tmin > tmax)
            {
                return null;
            }
        }
        return tmin;
    }

    /// <summary>Punkt am Boden (Y = 0) auf dem Strahl oder null (Blick in den Himmel).</summary>
    public static Vector2? Boden(Vector3 start, Vector3 richtung)
    {
        if (MathF.Abs(richtung.Y) < 1e-9f)
        {
            return null;
        }
        var t = -start.Y / richtung.Y;
        if (t < 0)
        {
            return null;
        }
        var p = start + (richtung * t);
        return new Vector2(p.X, p.Z);
    }

    /// <summary>
    /// Stueck unter dem Strahl: trifft er vor dem Boden die Fassade oder das Dach eines Hauses, dieses Haus (das vorderste);
    /// sonst das Stueck am Bodenpunkt wie in der Draufsicht (<see cref="AussenBearbeitung.StueckBei"/>).
    /// </summary>
    public static AussenStelle? Treffer(PyObjekt aussen, Vector3 start, Vector3 richtung, IReadOnlyList<Kasten> haeuser, double greifen)
    {
        var boden = Boden(start, richtung);
        var tBoden = boden is null ? float.MaxValue : (-start.Y / richtung.Y);
        Kasten? vorne = null;
        var tVorne = float.MaxValue;
        foreach (var k in haeuser)
        {
            if (Schnitt(start, richtung, k.Min, k.Max) is { } t && t < tVorne)
            {
                vorne = k;
                tVorne = t;
            }
        }
        if (vorne is not null && tVorne <= tBoden + 1e-4f)
        {
            // ein kleines Stueck (Laterne, Baum, Beet, Katalog) direkt am Bodenpunkt vor dem Haus geht vor
            if (boden is { } b && AussenBearbeitung.StueckBei(aussen, b.X, b.Y, greifen, AussenBearbeitung.NurDraufsicht) is { } klein
                && klein.Art is not (AussenArt.Haus or AussenArt.Gebaeude or AussenArt.Park or AussenArt.Strasse)
                && tBoden - tVorne < 0.5f)
            {
                return klein;
            }
            return vorne.Stelle;
        }
        // S 0.27.0: Notizen sind in 3D nicht zu sehen und darum nicht anwaehlbar
        return boden is { } p ? AussenBearbeitung.StueckBei(aussen, p.X, p.Y, greifen, AussenBearbeitung.NurDraufsicht) : null;
    }

    // ---- Umrisse (Schatten am Boden) --------------------------------------------------------------

    /// <summary>Nur fuer die Gegenprobe in Tests: Umriss einer Kurve je Stueck wie vor S 0.12.1.</summary>
    internal static bool UmrissJeStueckFuerTests { get => JeStueck.Value; set => JeStueck.Value = value; }

    private static readonly AsyncLocal<bool> JeStueck = new();

    /// <summary>Umriss eines Stuecks am Boden: ein oder mehrere Vielecke in Kartenpunkten (x, y).</summary>
    public static List<Vector2[]> Umriss(PyObjekt aussen, AussenStelle s)
    {
        if (!AussenBearbeitung.Gueltig(aussen, s))
        {
            return [];
        }
        switch (s.Art)
        {
            case AussenArt.Haus or AussenArt.Gebaeude:
                return [Rechteck(AussenBearbeitung.HausFlaeche(aussen, s))];
            case AussenArt.Katalog:
                return [Rechteck(AussenBearbeitung.KatalogFlaeche(aussen, s))];
            case AussenArt.Park:
            {
                var l = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
                return [Rechteck((Py.F(l["x"]), Py.F(l["y"]), Py.F(l["w"]), Py.F(l["h"])))];
            }
            case AussenArt.Strasse:
            {
                var l = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
                var halb = (StadtSzene.StrassenBreite / 2) + AussenBearbeitung.Gehweg(aussen, s);
                var punkte = (l["punkte"] as List<object?> ?? []).OfType<List<object?>>().Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1]))).ToList();
                // Patch S 0.12.1 (Fund 233): eine Kurve (mehr als 2 Punkte) ist EIN Band mit Gehrung wie im Spiel; vorher je
                // Stueck ein Rechteck, 13 Rechtecke ueberlappten sich und wirkten unruhig
                var ohneDoppelte = punkte.Where((p, i) => i == 0 || Vector2.DistanceSquared(p, punkte[i - 1]) >= 1e-8f).ToList();
                if (ohneDoppelte.Count > 2 && !UmrissJeStueckFuerTests)
                {
                    var links = Kurve.Versetzt(ohneDoppelte, halb);
                    var rechts = Kurve.Versetzt(ohneDoppelte, -halb);
                    return [[.. links, .. Enumerable.Reverse(rechts)]];
                }
                var teile = new List<Vector2[]>();
                for (var i = 0; i + 1 < ohneDoppelte.Count; i++)
                {
                    var a = ohneDoppelte[i];
                    var b = ohneDoppelte[i + 1];
                    var d = Vector2.Normalize(b - a);
                    var q = new Vector2(-d.Y, d.X) * halb;
                    teile.Add([a + q, b + q, b - q, a - q]);
                }
                return teile;
            }
            case AussenArt.Teich when s.Punkt < 0:
                return Wasser.Punkte(Aussen.Teiche(aussen)[s.Nummer]) is { Count: >= 3 } eck ? [[.. new Teich(eck).MitUfer]] : [];
            case AussenArt.Fluss or AussenArt.Bahn when s.Punkt < 0:
            {
                // S 0.8.0: Fluss samt Ufer je Stueck des geglaetteten Bogens (wie die Strasse); Welle K2: Bahn samt Gleisbett
                var glatt = AussenBearbeitung.LinienzugGlatt(aussen, s);
                var halb = AussenBearbeitung.LinienHalb(s.Art);
                var teile = new List<Vector2[]>();
                for (var i = 0; i + 1 < glatt.Count; i++)
                {
                    var (a, b) = (glatt[i], glatt[i + 1]);
                    if (Vector2.DistanceSquared(a, b) < 1e-8f)
                    {
                        continue;
                    }
                    var q = new Vector2(-(b - a).Y, (b - a).X) / Vector2.Distance(a, b) * halb;
                    teile.Add([a + q, b + q, b - q, a - q]);
                }
                return teile;
            }
            case AussenArt.Teich or AussenArt.Fluss or AussenArt.Bahn:
            {
                // Eckpunkt: der Umriss des ganzen Stuecks und ein Kreis am Griff
                var p = AussenBearbeitung.EckpunkteWerte(aussen, s)[s.Punkt];
                return [.. Umriss(aussen, AussenBearbeitung.Ganz(s)), Kreis(p.X, p.Y, 0.2)];
            }
            case AussenArt.Fussweg:
                return [.. Band(AussenBearbeitung.FusswegGlatt(aussen, s), Fusswege.Breite / 2)];
            case AussenArt.Zebrastreifen:
            {
                // S 0.26.0: ueber die ganze Fahrbahn, Breite laengs der Strasse
                if (AussenBearbeitung.ZebraVon(aussen, s) is not { } z)
                {
                    return [];
                }
                var l = z.Richtung * (Zebrastreifen.Breite / 2);
                var q = z.Quer * (StadtSzene.StrassenBreite / 2);
                return [[z.Mitte - l - q, z.Mitte + l - q, z.Mitte + l + q, z.Mitte - l + q]];
            }
            case AussenArt.Ampel:
            {
                // S 0.35.0: die Kreuzung bis zu den Furten und je Mast ein kleiner Kreis
                if (AussenBearbeitung.AmpelVon(aussen, s) is not { } amp)
                {
                    return [];
                }
                return [Kreis(amp.Mitte.X, amp.Mitte.Y, Ampeln.FurtMitte + (Zebrastreifen.Breite / 2)), .. amp.Masten.Select(m => Kreis(m.Fuss.X, m.Fuss.Y, 0.1))];
            }
            case AussenArt.Notiz:
                // S 0.27.0: nur in der Draufsicht
                return [];
            case AussenArt.Steg:
            {
                if (Wasser.Steg(Aussen.Stege(aussen)[s.Nummer]) is not { } steg)
                {
                    return [];
                }
                var (x1, z1, x2, z2) = steg.Rechteck;
                return [Rechteck((x1, z1, x2 - x1, z2 - z1))];
            }
            default:
            {
                var t = AussenBearbeitung.Alle(aussen).First(z => z.Stelle == s);
                var (x, y) = (t.X, t.Y);
                return s.Art switch
                {
                    AussenArt.Beet => [Rechteck((x - 0.3, y - 0.2, 0.6, 0.4))],
                    AussenArt.Paketplatz => [Rechteck((x - (Lieferpaket.Breite * 1.6), y - (Lieferpaket.Breite * 1.6), Lieferpaket.Breite * 3.2, Lieferpaket.Breite * 3.2))],
                    AussenArt.Laterne => [Kreis(x, y, 0.15)],
                    _ => [Kreis(x, y, 0.45)],
                };
            }
        }
    }

    private static Vector2[] Rechteck((double X, double Y, double W, double H) r) =>
    [
        new((float)r.X, (float)r.Y), new((float)(r.X + r.W), (float)r.Y), new((float)(r.X + r.W), (float)(r.Y + r.H)), new((float)r.X, (float)(r.Y + r.H)),
    ];

    private static Vector2[] Kreis(double x, double y, double r) =>
        [.. Enumerable.Range(0, 12).Select(i => new Vector2((float)(x + (r * Math.Cos(i * Math.PI / 6))), (float)(y + (r * Math.Sin(i * Math.PI / 6)))))];

    // ---- Vorschau auf einer Kopie -----------------------------------------------------------------

    /// <summary>Stand nach dem Schritt (Kopie), das neue oder bewegte Stueck (null = der Schritt ging nicht) und sein Umriss.</summary>
    public sealed record Vorschau(PyObjekt Stand, AussenStelle? Stelle, List<Vector2[]> Umriss, string? Grund = null);

    /// <summary>Setzen wie <c>KartenFlaeche.SetzenAn</c> (gleiche Drehung, gleiches Raster), nur auf einer Kopie.</summary>
    public static Vorschau Setzen(PyObjekt aussen, AussenArt art, double x, double y, bool raster, string? modell, int dreh, Blueprint? blueprint = null)
    {
        var kopie = (PyObjekt)Py.Kopie(aussen)!;
        string? grund = null;
        AussenStelle? s;
        s = art == AussenArt.Blueprint
            ? blueprint?.SetzenUndDrehen(kopie, x, y, raster, dreh, out grund)
            : AussenBearbeitung.SetzenUndDrehen(kopie, art, x, y, raster, modell, dreh);
        return new Vorschau(kopie, s, s is null ? [] : Umriss(kopie, s), s is null ? grund ?? Texte.Bauen3DGehtNicht : null);
    }

    /// <summary>
    /// Welle Z: Ziehen von Punkt zu Punkt wie <c>KartenFlaeche.ZiehenBis</c>, nur auf einer Kopie. Der Umriss ist das Band der
    /// Verbindung (auch ohne passende Verbindung die gerade Linie, dann mit Grund und rot).
    /// </summary>
    public static Vorschau Ziehen(PyObjekt aussen, ZiehArt art, double ax, double ay, double bx, double by, bool raster)
    {
        var kopie = (PyObjekt)Py.Kopie(aussen)!;
        var (_, _, plan) = AussenBearbeitung.ZiehPlan(aussen, art, ax, ay, bx, by, raster);
        var s = AussenBearbeitung.Ziehen(kopie, art, ax, ay, bx, by, raster, out var grund);
        return new Vorschau(kopie, s, Band(AussenBearbeitung.ZiehLinie(art, plan), AussenBearbeitung.ZiehHalb(art)), s is null ? grund ?? Texte.Bauen3DGehtNicht : null);
    }

    /// <summary>Band um einen Linienzug (mehr als zwei Punkte: ein Vieleck mit Gehrung, sonst ein Rechteck).</summary>
    public static List<Vector2[]> Band(IReadOnlyList<Vector2> linie, float halb)
    {
        var ohneDoppelte = linie.Where((p, i) => i == 0 || Vector2.DistanceSquared(p, linie[i - 1]) >= 1e-8f).ToList();
        if (ohneDoppelte.Count > 2)
        {
            var links = Kurve.Versetzt(ohneDoppelte, halb);
            var rechts = Kurve.Versetzt(ohneDoppelte, -halb);
            return [[.. links, .. Enumerable.Reverse(rechts)]];
        }
        if (ohneDoppelte.Count < 2)
        {
            return [];
        }
        var (a, b) = (ohneDoppelte[0], ohneDoppelte[1]);
        var q = new Vector2(-(b - a).Y, (b - a).X) / Vector2.Distance(a, b) * halb;
        return [[a + q, b + q, b - q, a - q]];
    }

    /// <summary>Verschieben wie <c>KartenFlaeche.Verschieben</c>, nur auf einer Kopie.</summary>
    public static Vorschau Verschieben(PyObjekt aussen, AussenStelle s, double dx, double dy, bool raster)
    {
        var kopie = (PyObjekt)Py.Kopie(aussen)!;
        AussenBearbeitung.Verschieben(kopie, s, dx, dy, raster);
        var gueltig = AussenBearbeitung.Gueltig(kopie, s);
        return new Vorschau(kopie, gueltig ? s : null, gueltig ? Umriss(kopie, s) : [], gueltig ? null : Texte.Bauen3DGehtNicht);
    }

    /// <summary>S 0.30.0: mehrere Stuecke zusammen verschieben wie <c>KartenFlaeche.GruppeVerschieben</c>, nur auf einer Kopie.</summary>
    public static Vorschau GruppeVerschieben(PyObjekt aussen, IReadOnlyList<AussenStelle> gruppe, double dx, double dy, bool raster)
    {
        var kopie = (PyObjekt)Py.Kopie(aussen)!;
        var e = AussenBearbeitung.Verschieben(kopie, gruppe, dx, dy, raster);
        var umriss = e.Auswahl.Where(s => AussenBearbeitung.Gueltig(kopie, s)).SelectMany(s => Umriss(kopie, s)).ToList();
        return new Vorschau(kopie, e.Auswahl.FirstOrDefault(), umriss, e.Auswahl.Count == 0 ? Texte.Bauen3DGehtNicht : null);
    }

    /// <summary>
    /// Der erste rote Fund, den es nachher gibt und vorher nicht (gleiche Pruefung wie die Liste „Prüfungen draußen“), oder
    /// null (gruen). Gleiche Texte zaehlen einzeln (zwei gleiche Funde vorher, drei nachher = einer neu).
    /// </summary>
    public static StadtbauFund? NeuRot(IEnumerable<StadtbauFund> vorher, IEnumerable<StadtbauFund> nachher)
    {
        var rest = vorher.Where(f => f.Rot).GroupBy(f => f.Text, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        foreach (var f in nachher.Where(f => f.Rot))
        {
            if (rest.TryGetValue(f.Text, out var n) && n > 0)
            {
                rest[f.Text] = n - 1;
                continue;
            }
            return f;
        }
        return null;
    }

    /// <summary>Funde draussen fuer einen Stand (Kopie), wie die Liste rechts (<see cref="Projekt.PruefenAussen(StadtFassung, Spieldaten, PyObjekt?)"/>).</summary>
    public static List<StadtbauFund> Pruefen(PyObjekt stand, StadtFassung basis, PyObjekt innenraeume, Spieldaten daten) =>
        Projekt.PruefenAussen(Aussen.Fassung(stand, basis, innenraeume, 1, 0, Werkzeug.Kennzeile, "", ""), daten, Aussen.Bauten(stand));

    // ---- Kamera -----------------------------------------------------------------------------------

    /// <summary>„Ganze Stadt zeigen“ in 3D: Mitte der Karte und ein Abstand, bei dem sie ganz ins Bild passt.</summary>
    public static (float X, float Z, float Abstand) GanzeStadt(PyObjekt aussen)
    {
        var (b, h) = Aussen.Groesse(aussen);
        return ((float)(b / 2), (float)(h / 2), Math.Clamp((float)(Math.Max(b, h) * 1.05), Kamera.AbstandMin, Kamera.AbstandMax));
    }
}
