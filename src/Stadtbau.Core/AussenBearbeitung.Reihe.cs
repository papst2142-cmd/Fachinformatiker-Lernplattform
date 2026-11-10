using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>Einstellungen einer Reihe: Abstand von Stueck zu Stueck, beide Seiten der Linie, zufaellig drehen (nur Katalog).</summary>
public sealed record ReiheWahl(double Abstand, bool BeideSeiten = false, bool Zufaellig = false);

/// <summary>Ein Stueck einer geplanten Reihe: Mitte, Vierteldrehungen, frei (wird gesetzt) oder mit Grund ausgelassen.</summary>
public sealed record ReihenStueck(double X, double Y, int Dreh, double W, double T, string? Grund)
{
    public bool Frei => Grund is null;
}

/// <summary>Geplante Reihe: alle Stuecke (auch ausgelassene), wie viele gesetzt werden, und ihre Dreiecke.</summary>
public sealed record ReihenPlan(List<ReihenStueck> Stuecke, long Dreiecke)
{
    public int Frei => Stuecke.Count(s => s.Frei);

    public int Ausgelassen => Stuecke.Count - Frei;
}

/// <summary>
/// Welle W-B S 0.30.0 (Weisung P12-136, Kurzplan P12-79 Wunsch 2, Nico 1a): eine Reihe von Baeumen oder Katalog-Stuecken
/// (Busch, Zaun, Bank, jedes Stueck) entlang einer geraden Linie von Tipp zu Tipp setzen, ein Schritt. Die Vorschau
/// (<see cref="ReihePlanen"/>) ist genau der Schritt (<see cref="ReiheSetzen"/>). Jedes Stueck wird einzeln geprueft
/// (<see cref="ReiheGrund"/>: Karte, Haus, Fahrbahn und Kreisverkehr-Insel, Gleis, Wasser, Fussweg, bei Katalog-Stuecken
/// auch Baum und Laterne wie Regel 223); rote Stuecke werden ausgelassen und gezaehlt. Danach laeuft die Pruefung draussen
/// wie nach jedem Schritt. Laternen stellt das Werkzeug seit S 0.26.0 selbst an die Strassen (E1: keine Laternen-Reihe).
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>„Beide Seiten“: so weit links und rechts der gezogenen Linie (auf dem Gehweg, wenn die Linie auf der Strassenmitte liegt).</summary>
    public const double ReiheSeite = 0.5;

    /// <summary>Hoechstens so viele Stuecke in einer Reihe (bei kleinem Abstand und langer Linie).</summary>
    public const int ReiheHoechstens = 200;

    /// <summary>Kleinster und groesster Abstand; Schritt der Knoepfe −/+.</summary>
    public const double ReiheAbstandMin = 0.1, ReiheAbstandMax = 10, ReiheAbstandSchritt = 0.1;

    /// <summary>Baum: Radius des Stamms fuer die Pruefung je Stueck.</summary>
    public const double ReiheBaumRadius = 0.15;

    /// <summary>Kann diese Art als Reihe gesetzt werden?</summary>
    public static bool ReiheMoeglich(AussenArt? art) => art is AussenArt.Baum or AussenArt.Katalog;

    /// <summary>Vorgabe-Abstand: Baum 1,0; Katalog-Stueck so, dass die Stuecke aneinanderstossen (Breite laengs, mindestens 0,1).</summary>
    public static double ReiheVorgabe(AussenArt art, string? modell)
    {
        if (art == AussenArt.Katalog && modell is not null && Katalog.Aktiv?.Eintrag(modell) is { } e)
        {
            return Math.Clamp(Bearbeitung.Runden(Math.Round(e.Aussen[0] * 10) / 10), ReiheAbstandMin, ReiheAbstandMax);
        }
        return 1.0;
    }

    /// <summary>Plan der Reihe von a nach b (Vorschau und Schritt gleich).</summary>
    public static ReihenPlan ReihePlanen(PyObjekt aussen, AussenArt art, string? modell, double ax, double ay, double bx, double by, ReiheWahl wahl,
        int dreh, bool raster, GrafikBibliothek? grafik = null)
    {
        var stuecke = new List<ReihenStueck>();
        if (!ReiheMoeglich(art))
        {
            return new ReihenPlan(stuecke, 0);
        }
        var eintrag = art == AussenArt.Katalog && modell is not null ? Katalog.Aktiv?.Eintrag(modell) : null;
        if (art == AussenArt.Katalog && eintrag is null)
        {
            return new ReihenPlan(stuecke, 0);
        }
        var r = raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;
        var a = new Vector2((float)Bearbeitung.AufRaster(ax, r), (float)Bearbeitung.AufRaster(ay, r));
        var b = new Vector2((float)Bearbeitung.AufRaster(bx, r), (float)Bearbeitung.AufRaster(by, r));
        var abstand = Math.Clamp(wahl.Abstand, ReiheAbstandMin, ReiheAbstandMax);
        var laenge = Vector2.Distance(a, b);
        var richtung = laenge > 1e-4f ? (b - a) / laenge : Vector2.UnitX;
        var quer = new Vector2(-richtung.Y, richtung.X);
        var anzahl = Math.Min(ReiheHoechstens, (int)Math.Floor((laenge / abstand) + 1e-6) + 1);
        var seiten = wahl.BeideSeiten ? new[] { (float)ReiheSeite, -(float)ReiheSeite } : [0f];
        // laengs der Linie: Katalog-Stueck mit seiner Breite laengs (steiler als 45 Grad = eine Vierteldrehung dazu)
        var grundDreh = ((dreh % 4) + 4 + (Math.Abs(richtung.Y) > Math.Abs(richtung.X) ? 1 : 0)) % 4;
        foreach (var seite in seiten)
        {
            for (var i = 0; i < anzahl && stuecke.Count < ReiheHoechstens; i++)
            {
                var p = a + (richtung * (float)(i * abstand)) + (quer * seite);
                double x = Bearbeitung.AufRaster(p.X, Bearbeitung.RasterFein), y = Bearbeitung.AufRaster(p.Y, Bearbeitung.RasterFein);
                var d = art == AussenArt.Katalog ? (grundDreh + (wahl.Zufaellig ? (int)(Gruen.Wurf("reihe", (float)x, (float)y) * 4) % 4 : 0)) % 4 : 0;
                double w = eintrag?.Aussen[0] ?? (2 * ReiheBaumRadius), t = eintrag?.Aussen[1] ?? (2 * ReiheBaumRadius);
                if (d % 2 == 1)
                {
                    (w, t) = (t, w);
                }
                stuecke.Add(new ReihenStueck(x, y, d, w, t, null));
            }
        }
        // jedes Stueck einzeln pruefen, auch gegen die schon geplanten freien davor (gleiche Stelle = doppelt)
        var geprueft = new List<ReihenStueck>();
        foreach (var s in stuecke)
        {
            var grund = ReiheGrund(aussen, art, eintrag, s);
            if (grund is null && geprueft.Any(g => g.Frei && Math.Abs(g.X - s.X) < 0.05 && Math.Abs(g.Y - s.Y) < 0.05))
            {
                grund = Texte.ReiheDoppelt;
            }
            geprueft.Add(s with { Grund = grund });
        }
        long dreiecke = 0;
        foreach (var s in geprueft.Where(s => s.Frei))
        {
            dreiecke += eintrag is not null ? (eintrag.Art == KatalogArt.Effekt ? KatalogPruefung.EffektDreiecke : eintrag.Dreiecke) : BaumDreiecke(grafik, s.X, s.Y);
        }
        return new ReihenPlan(geprueft, dreiecke);
    }

    /// <summary>Dreiecke eines Baums an (x, y): das Modell, das das Spiel dort zeichnet (Tanne oder Laubbaum nach Lage).</summary>
    public static long BaumDreiecke(GrafikBibliothek? grafik, double x, double y)
    {
        if (grafik is null)
        {
            return 0;
        }
        var (fx, fz) = ((float)x, (float)y);
        if (StadtSzene.Tanne(fx, fz) is { } tanne && grafik.Modell(tanne) is { } t)
        {
            return t.Dreiecke;
        }
        var (name, _) = Gruen.Baeume[(int)(Gruen.Wurf("baum", fx, fz) * Gruen.Baeume.Length) % Gruen.Baeume.Length];
        return grafik.Modell(name)?.Dreiecke ?? 0;
    }

    /// <summary>Warum dieses Stueck der Reihe nicht gesetzt wird (null = frei).</summary>
    public static string? ReiheGrund(PyObjekt aussen, AussenArt art, KatalogEintrag? eintrag, ReihenStueck s)
    {
        var effekt = eintrag?.Art == KatalogArt.Effekt;
        var halbW = s.W / 2;
        var halbT = s.T / 2;
        var mitte = new Vector2((float)s.X, (float)s.Y);
        var (breite, hoehe) = Aussen.Groesse(aussen);
        if (s.X - halbW < -1e-6 || s.Y - halbT < -1e-6 || s.X + halbW > breite + 1e-6 || s.Y + halbT > hoehe + 1e-6)
        {
            return Texte.ReiheAusserhalb;
        }
        const double Rand = StadtbauPruefung.Toleranz;
        foreach (var (h, _, _) in Alle(aussen).Where(a => a.Stelle.Art is AussenArt.Haus or AussenArt.Gebaeude))
        {
            var (hx, hy, hw, hh) = HausFlaeche(aussen, h);
            if (Math.Min(s.X + halbW, hx + hw) - Math.Max(s.X - halbW, hx) > Rand && Math.Min(s.Y + halbT, hy + hh) - Math.Max(s.Y - halbT, hy) > Rand)
            {
                return Texte.ReiheImHaus;
            }
        }
        if (effekt)
        {
            return null;   // ein Effekt schwebt und versperrt nichts (nur nicht im Haus, Regel 223)
        }
        // Abstand des Fussabdrucks (Rechteck bzw. Stamm) zu einer Linie
        float Abstand(IReadOnlyList<Vector2> linie)
        {
            if (art == AussenArt.Baum)
            {
                return Kurve.Abstand(mitte, linie);
            }
            var best = float.MaxValue;
            for (var i = 0; i + 1 < linie.Count; i++)
            {
                var (p, q) = (linie[i], linie[i + 1]);
                var schritte = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(p, q) / 0.05f));
                for (var k = 0; k <= schritte; k++)
                {
                    var z = Vector2.Lerp(p, q, (float)k / schritte);
                    var dx = Math.Max(Math.Max(s.X - halbW - z.X, 0), z.X - (s.X + halbW));
                    var dy = Math.Max(Math.Max(s.Y - halbT - z.Y, 0), z.Y - (s.Y + halbT));
                    best = MathF.Min(best, (float)Math.Sqrt((dx * dx) + (dy * dy)));
                }
            }
            return best;
        }
        var stamm = art == AussenArt.Baum ? (float)ReiheBaumRadius : 0f;
        foreach (var l in Aussen.Landschaft(aussen).OfType<PyObjekt>())
        {
            var linie = Linie(l["punkte"]);
            if (linie.Count < 2)
            {
                continue;
            }
            if (Py.Gleich(l["typ"], "strasse"))
            {
                if (Abstand(linie) < (StadtSzene.StrassenBreite / 2) - Rand + stamm)
                {
                    return Texte.ReiheAufFahrbahn;
                }
                if (Kreisverkehr.IstRing(l) && Vector2.Distance(mitte, Kreisverkehr.Mitte(linie)) < Kreisverkehr.InselRadius(Kreisverkehr.Radius(l) ?? 2) + stamm + halbW)
                {
                    return Texte.ReiheAufInsel;
                }
            }
            else if (Py.Gleich(l["typ"], "bahn") && Abstand(linie) < (art == AussenArt.Baum ? Bahnuebergang.LichtraumHalb : (StadtSzene.GleisBreite / 2) - Rand) + stamm)
            {
                return Texte.ReiheAufGleis;
            }
        }
        var ecken = new[] { mitte, mitte + new Vector2((float)halbW, (float)halbT), mitte + new Vector2((float)-halbW, (float)halbT),
            mitte + new Vector2((float)halbW, (float)-halbT), mitte + new Vector2((float)-halbW, (float)-halbT) };
        var pruefpunkte = art == AussenArt.Baum ? [mitte] : ecken;
        if (pruefpunkte.Any(p => WasserBei(aussen, p, AussenArt.Teich) is not null || WasserBei(aussen, p, AussenArt.Fluss) is not null))
        {
            return Texte.ReiheImWasser;
        }
        if (pruefpunkte.Any(p => FusswegBei(aussen, p, art == AussenArt.Baum ? ReiheBaumRadius : 0) is not null))
        {
            return Texte.ReiheAufFussweg;
        }
        if (art == AussenArt.Katalog)
        {
            var naehe = StadtbauPruefung.KatalogStammAbstand;
            bool Nah(Vector2 p) =>
                Math.Sqrt(Math.Pow(Math.Max(Math.Max(s.X - halbW - p.X, 0), p.X - (s.X + halbW)), 2) + Math.Pow(Math.Max(Math.Max(s.Y - halbT - p.Y, 0), p.Y - (s.Y + halbT)), 2)) < naehe;
            if (Alle(aussen).Any(a => a.Stelle.Art == AussenArt.Baum && Nah(new Vector2((float)a.X, (float)a.Y))))
            {
                return Texte.ReiheAufBaum;
            }
            if (Aussen.LaternenWerte(aussen).Any(l => Nah(new Vector2(l.X, l.Z))))
            {
                return Texte.ReiheAnLaterne;
            }
        }
        return null;
    }

    /// <summary>Die Reihe setzen (ein Schritt): nur die freien Stuecke; Ergebnis = neue Stellen und ausgelassene.</summary>
    public static (List<AussenStelle> Neu, int Ausgelassen) ReiheSetzen(PyObjekt aussen, AussenArt art, string? modell, double ax, double ay, double bx, double by,
        ReiheWahl wahl, int dreh, bool raster)
    {
        var plan = ReihePlanen(aussen, art, modell, ax, ay, bx, by, wahl, dreh, raster);
        var neu = new List<AussenStelle>();
        foreach (var s in plan.Stuecke.Where(s => s.Frei))
        {
            // fein gesetzt (0,1), ohne Einrasten: genau an die geplante Stelle (Vorschau = Schritt)
            if (SetzenUndDrehen(aussen, art, s.X, s.Y, false, modell, art == AussenArt.Katalog ? s.Dreh : 0) is { } stelle)
            {
                neu.Add(stelle);
            }
        }
        return (neu, plan.Ausgelassen);
    }
}
