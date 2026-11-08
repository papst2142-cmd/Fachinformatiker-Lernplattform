using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Phase 13 Welle 3 (Weisung P12-18, Plan Abschnitt 3): Bearbeitung der Stadt draussen. Raster 0,5 wie das Laufraster
/// des Spiels fuer alles, was das Gehen betrifft (Strassen, Haeuser samt Tuer und Fussweg); Gruen, Laternen und der
/// Paketplatz rasten mit 0,5, mit "Raster aus" mit 0,1 ein. Verschoben wird um ganze Rasterschritte (die Ausrichtung zu
/// den Nachbarn bleibt). Strassen rasten mit ihren Enden an andere Strassen an (<see cref="EinrastenAb"/>). Ein Haus nimmt
/// beim Verschieben Tuer, Schild und Fussweg mit; der Fussweg wird bis zur naechsten Strasse verlaengert oder gekuerzt.
/// Alle Werte werden auf 0,01 gerundet.
/// Was das Spiel noch nicht kann, bietet das Werkzeug nicht an: Orte des Spiels loeschen oder neu setzen (das aendert
/// Spielregeln, Plan Abschnitt 1). Haeuser drehen seit Welle E in Vierteln (<see cref="Hausdrehung"/>).
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>So nah muss ein Strassenende an einer anderen Strasse liegen, damit es dort einrastet.</summary>
    public const double EinrastenAb = 0.6;

    /// <summary>Laenge einer neuen Strasse.</summary>
    public const double NeueStrasse = 4;

    /// <summary>Groesse eines neuen Parks.</summary>
    public const double NeuerPark = 3;

    /// <summary>Breite eines neuen Gehwegs je Strassenseite (Plan Abschnitt 5: 0,35).</summary>
    public const double NeuerGehweg = 0.35;

    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    private static double R(double wert) => Bearbeitung.Runden(wert);

    private static List<object?> Punkt(double x, double y) => [Bearbeitung.Zahl(x), Bearbeitung.Zahl(y)];

    private static double Schritt(double d, bool raster)
    {
        var r = raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;
        return R(Math.Round(d / r, MidpointRounding.AwayFromZero) * r);
    }

    // ---- Orte, Modelle, Zugaenge ----------------------------------------------------------------------

    /// <summary>Alle Orte (Grundkarte und neue Orte aus den Stadtdaten).</summary>
    public static IEnumerable<PyObjekt> Orte(PyObjekt aussen) =>
        ListeOderLeer(Aussen.Karte(aussen), "orte").OfType<PyObjekt>().Concat(ListeOderLeer(Aussen.Stadtdaten(aussen), "neue_orte").OfType<PyObjekt>());

    public static PyObjekt? Ort(PyObjekt aussen, string id) => Orte(aussen).FirstOrDefault(o => Py.Gleich(o["id"], id));

    /// <summary>
    /// Weisung P12-89: Namen eines Orts aendern (nur das Feld <c>name</c> der Karte bzw. der neuen Orte; Kennung, Kunde,
    /// Aufgaben und Innenraum bleiben). Gibt den Grund zurueck, wenn der Name nicht geht (dann bleibt alles), sonst null.
    /// Die Regeln sind dieselben wie in der Pruefung (<see cref="StadtbauPruefung.OrtNameNicht"/>).
    /// </summary>
    public static string? OrtUmbenennen(PyObjekt aussen, string id, string name)
    {
        if (Ort(aussen, id) is not { } ort)
        {
            return Texte.OrtNichtGefunden;
        }
        var n = (name ?? "").Trim();
        var andere = Orte(aussen).Where(o => !Py.Gleich(o["id"], id)).Select(o => Get(o, "name") as string).OfType<string>();
        if (StadtbauPruefung.OrtNameNicht(n, andere) is { } grund)
        {
            return grund;
        }
        Py.Setzen(ort, "name", n);
        return null;
    }

    /// <summary>Alle Haus-Modelle (Karte und neue Modelle), nach Namen.</summary>
    public static PyObjekt Modelle(PyObjekt aussen)
    {
        var alle = new PyObjekt();
        foreach (var quelle in new[] { Get(Aussen.Karte(aussen), "modelle"), Get(Aussen.Stadtdaten(aussen), "neue_modelle") })
        {
            if (quelle is PyObjekt m)
            {
                foreach (var (k, v) in m.Where(p => !p.Key.StartsWith('_')))
                {
                    Py.Setzen(alle, k, v);
                }
            }
        }
        return alle;
    }

    /// <summary>Haus-Modelle zum Setzen (ohne Bahnhoefe, die zum Gleis gehoeren).</summary>
    public static List<string> HausModelle(PyObjekt aussen) =>
        [.. Modelle(aussen).Select(p => p.Key).Where(k => !k.StartsWith("bahnhof", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

    /// <summary>
    /// Grundflaeche eines Modells (alle Bloecke, relativ zu x, y des Hauses; ohne Block 1 × 1). Nur fuer die Draufsicht;
    /// die Pruefung rechnet mit den gezeichneten Haeusern der 3D-Szene.
    /// </summary>
    public static (double X, double Y, double W, double H) Grundflaeche(PyObjekt aussen, string modell)
    {
        if (Get(Modelle(aussen), modell) is not List<object?> teile)
        {
            return (0, 0, 1, 1);
        }
        var bloecke = teile.OfType<PyObjekt>().Where(t => Py.Gleich(t["art"], "block")).ToList();
        if (bloecke.Count == 0)
        {
            // S 0.21.0: ein Modell nur aus Flaechen (Bahnsteig-Stueck, Lernplattform.Core.Stadtbau.Bahnsteig) nimmt ihre Flaeche
            var flaechen = teile.OfType<PyObjekt>().Where(t => Py.Gleich(t["art"], "flaeche")).ToList();
            if (flaechen.Count == 0)
            {
                return (0, 0, 1, 1);
            }
            var fx1 = flaechen.Min(b => Py.F(b["x"]));
            var fy1 = flaechen.Min(b => Py.F(b["y"]));
            return (fx1, fy1, flaechen.Max(b => Py.F(b["x"]) + Py.F(b["w"])) - fx1, flaechen.Max(b => Py.F(b["y"]) + Py.F(b["h"])) - fy1);
        }
        var x1 = bloecke.Min(b => Py.F(b["x"]));
        var y1 = bloecke.Min(b => Py.F(b["y"]));
        var x2 = bloecke.Max(b => Py.F(b["x"]) + Py.F(b["w"]));
        var y2 = bloecke.Max(b => Py.F(b["y"]) + Py.F(b["t"]));
        return (x1, y1, x2 - x1, y2 - y1);
    }

    /// <summary>Zugang eines Orts (zugaenge_neu vor zugaenge, wie <see cref="Stadt.Zugaenge"/>), sonst null.</summary>
    public static PyObjekt? Zugang(PyObjekt aussen, string id)
    {
        var sd = Aussen.Stadtdaten(aussen);
        return Get(Objekt(Get(sd, "zugaenge_neu") ?? new PyObjekt()), id) as PyObjekt ?? Get(Objekt(Get(sd, "zugaenge") ?? new PyObjekt()), id) as PyObjekt;
    }

    /// <summary>Zierwege (Haeuser ohne Ort mit Tuer und Gehweg, z. B. Stellwerk) zu einem Modell.</summary>
    private static IEnumerable<PyObjekt> Zierwege(PyObjekt aussen, string modell) =>
        ListeOderLeer(Objekt(Get(Aussen.Stadtdaten(aussen), "darstellung") ?? new PyObjekt()), "zierwege").OfType<PyObjekt>().Where(z => Py.Gleich(z["modell"], modell));

    /// <summary>Mittellinien aller Strassen.</summary>
    public static List<List<Vector2>> Strassen(PyObjekt aussen) =>
        [.. Aussen.Landschaft(aussen).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "strasse")).Select(t => Linie(t["punkte"]))];

    private static List<Vector2> Linie(object? punkte) =>
        [.. ListeOderLeerWert(punkte).OfType<List<object?>>().Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1])))];

    private static List<object?> ListeOderLeerWert(object? w) => w as List<object?> ?? [];

    // ---- Lage und Treffer -----------------------------------------------------------------------------

    /// <summary>Lage eines Hauses (Ort oder Gebaeude): x, y der Grundflaeche in der Stadt.</summary>
    public static (double X, double Y, double W, double H) HausFlaeche(PyObjekt aussen, AussenStelle s)
    {
        var o = s.Art == AussenArt.Haus ? Ort(aussen, s.Ort)! : Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        var (gx, gy, gw, gh) = Grundflaeche(aussen, Py.Str(o["modell"]));
        if (HausDreh(aussen, s) % 2 == 1)
        {
            // Welle E: eine Vierteldrehung tauscht Breite und Tiefe um die Mitte
            var (mx, my) = (Py.F(o["x"]) + gx + (gw / 2), Py.F(o["y"]) + gy + (gh / 2));
            return (mx - (gh / 2), my - (gw / 2), gh, gw);
        }
        return (Py.F(o["x"]) + gx, Py.F(o["y"]) + gy, gw, gh);
    }

    /// <summary>Platz der Pakete vor der Wohnung (Mitte) aus Tuer, Richtung des Fusswegs und darstellung.paket_platz.</summary>
    public static (Vector2 Mitte, Vector2 Aussen, Vector2 Tuer)? PaketLage(PyObjekt aussen)
    {
        if (Orte(aussen).FirstOrDefault(o => Py.Gleich(o["ansicht"], OrtZuhause)) is not { } wohnung || Zugang(aussen, Py.Str(wohnung["id"])) is not { } z)
        {
            return null;
        }
        var weg = Linie(z["weg"]);
        var tuer = Linie(new List<object?> { z["tuer"] })[0];
        var n = weg.Count >= 2 && weg[1] != weg[0] ? Vector2.Normalize(weg[1] - weg[0]) : new Vector2(0, 1);
        var (vor, seitlich) = PaketWerte(aussen);
        var quer = new Vector2(-n.Y, n.X);
        return (tuer + (n * (float)vor) + (quer * (float)seitlich), n, tuer);
    }

    public static (double Vor, double Seitlich) PaketWerte(PyObjekt aussen)
    {
        var p = Get(Objekt(Get(Aussen.Stadtdaten(aussen), "darstellung") ?? new PyObjekt()), Lieferpaket.Abschnitt) as PyObjekt ?? [];
        return (Py.F(Get(p, "vor", 0.5)), Py.F(Get(p, "seitlich", 0.42)));
    }

    /// <summary>Alle Stuecke mit einem Punkt (Mitte) fuer Anzeige, Treffer und Tests.</summary>
    public static IEnumerable<(AussenStelle Stelle, double X, double Y)> Alle(PyObjekt aussen)
    {
        var land = Aussen.Landschaft(aussen);
        for (var i = 0; i < land.Count; i++)
        {
            if (land[i] is not PyObjekt l)
            {
                continue;
            }
            switch (l["typ"] as string)
            {
                case "strasse":
                    var linie = Linie(l["punkte"]);
                    if (linie.Count > 0)
                    {
                        var m = (linie[0] + linie[^1]) / 2;
                        yield return (new AussenStelle(AussenArt.Strasse, i), m.X, m.Y);
                    }
                    break;
                case "park":
                    yield return (new AussenStelle(AussenArt.Park, i), Py.F(l["x"]) + (Py.F(l["w"]) / 2), Py.F(l["y"]) + (Py.F(l["h"]) / 2));
                    break;
                case "gebaeude":
                    var s = new AussenStelle(AussenArt.Gebaeude, i);
                    var (hx, hy, hw, hh) = HausFlaeche(aussen, s);
                    yield return (s, hx + (hw / 2), hy + (hh / 2));
                    break;
                case "baum":
                    var punkte = ListeOderLeer(l, "punkte");
                    for (var k = 0; k < punkte.Count; k++)
                    {
                        if (punkte[k] is List<object?> p)
                        {
                            yield return (new AussenStelle(AussenArt.Baum, i, k), Py.F(p[0]), Py.F(p[1]));
                        }
                    }
                    break;
            }
        }
        foreach (var o in Orte(aussen))
        {
            var s = new AussenStelle(AussenArt.Haus, -1, 0, Py.Str(o["id"]));
            var (hx, hy, hw, hh) = HausFlaeche(aussen, s);
            yield return (s, hx + (hw / 2), hy + (hh / 2));
        }
        var beete = Aussen.Beete(aussen);
        for (var i = 0; i < beete.Count; i++)
        {
            yield return (new AussenStelle(AussenArt.Beet, i), Py.F(Liste(beete[i])[0]), Py.F(Liste(beete[i])[1]));
        }
        var laternen = Aussen.Laternen(aussen);
        for (var i = 0; i < laternen.Count; i++)
        {
            yield return (new AussenStelle(AussenArt.Laterne, i), Py.F(Liste(laternen[i])[0]), Py.F(Liste(laternen[i])[1]));
        }
        if (PaketLage(aussen) is { } paket)
        {
            yield return (new AussenStelle(AussenArt.Paketplatz, 0), paket.Mitte.X, paket.Mitte.Y);
        }
        var katalog = Aussen.KatalogStuecke(aussen);
        for (var i = 0; i < katalog.Count; i++)
        {
            var k = Objekt(katalog[i]);
            yield return (new AussenStelle(AussenArt.Katalog, i), Py.F(k["x"]), Py.F(k["y"]));
        }
        foreach (var w in AlleWasser(aussen))
        {
            yield return w;
        }
        foreach (var f in AlleFusswege(aussen))
        {
            yield return f;
        }
    }

    /// <summary>Rechteck eines Katalog-Stuecks draussen (x, y = Ecke oben links).</summary>
    public static (double X, double Y, double W, double H) KatalogFlaeche(PyObjekt aussen, AussenStelle s)
    {
        var k = Objekt(Aussen.KatalogStuecke(aussen)[s.Nummer]);
        double w = Py.F(k["w"]), t = Py.F(k["t"]);
        return (Py.F(k["x"]) - (w / 2), Py.F(k["y"]) - (t / 2), w, t);
    }

    /// <summary>Groesse eines Katalog-Stuecks draussen (Breite, Tiefe in Karten-Einheiten).</summary>
    public static (double W, double T) KatalogGroesse(PyObjekt aussen, AussenStelle s)
    {
        var k = Objekt(Aussen.KatalogStuecke(aussen)[s.Nummer]);
        return (Py.F(k["w"]), Py.F(k["t"]));
    }

    /// <summary>Groesse eines Katalog-Stuecks draussen setzen (Mitte bleibt; 0 oder weniger zeigt die Pruefung rot).</summary>
    public static void KatalogGroesseSetzen(PyObjekt aussen, AussenStelle s, double w, double t)
    {
        var k = Objekt(Aussen.KatalogStuecke(aussen)[s.Nummer]);
        Py.Setzen(k, "w", Bearbeitung.Zahl(w));
        Py.Setzen(k, "t", Bearbeitung.Zahl(t));
    }

    /// <summary>Kennung "paket/name" eines Katalog-Stuecks draussen.</summary>
    public static string KatalogId(PyObjekt aussen, AussenStelle s) => Py.Str(Objekt(Aussen.KatalogStuecke(aussen)[s.Nummer])["id"]);

    /// <summary>Gibt es dieses Stueck (noch)?</summary>
    public static bool Gueltig(PyObjekt aussen, AussenStelle s) =>
        MitEckpunkten(s.Art) && s.Punkt >= 0 ? EckpunktGueltig(aussen, s) : Alle(aussen).Any(a => a.Stelle == s);

    /// <summary>
    /// Oberstes Stueck an einem Punkt: zuerst kleine (Laterne, Paketplatz, Beet, Baum), dann Haeuser, dann Parks, dann
    /// Strassen (Abstand zur Mittellinie bis zur halben Breite samt Gehweg). <paramref name="greifen"/> = Greifradius.
    /// </summary>
    public static AussenStelle? StueckBei(PyObjekt aussen, double x, double y, double greifen = 0.3)
    {
        var p = new Vector2((float)x, (float)y);
        var alle = Alle(aussen).ToList();
        // S 0.8.0: Eckpunkte von Teich und Fluss zuerst (sonst liesse sich eine Ecke neben einem Baum nicht greifen)
        if (EckpunktBei(aussen, x, y, greifen) is { } ecke)
        {
            return ecke;
        }
        foreach (var art in new[] { AussenArt.Laterne, AussenArt.Paketplatz, AussenArt.Beet, AussenArt.Baum })
        {
            var naechstes = alle.Where(a => a.Stelle.Art == art).Select(a => (a.Stelle, D: Vector2.Distance(p, new Vector2((float)a.X, (float)a.Y))))
                .Where(a => a.D <= greifen).OrderBy(a => a.D).Select(a => a.Stelle).FirstOrDefault();
            if (naechstes is not null)
            {
                return naechstes;
            }
        }
        // Katalog-Stuecke (zuletzt gesetztes oben) vor den Haeusern: sie stehen oft auf oder an ihnen
        foreach (var (s, _, _) in alle.Where(a => a.Stelle.Art == AussenArt.Katalog).Reverse())
        {
            var (kx, ky, kw, kh) = KatalogFlaeche(aussen, s);
            if (x >= kx - 0.05 && x <= kx + kw + 0.05 && y >= ky - 0.05 && y <= ky + kh + 0.05)
            {
                return s;
            }
        }
        foreach (var (s, _, _) in alle.Where(a => a.Stelle.Art is AussenArt.Haus or AussenArt.Gebaeude).Reverse())
        {
            var (hx, hy, hw, hh) = HausFlaeche(aussen, s);
            if (x >= hx && x <= hx + hw && y >= hy && y <= hy + hh)
            {
                return s;
            }
        }
        // S 0.8.0: Steg und Teich vor dem Park (ein Steg liegt auf dem Wasser)
        if ((WasserBei(aussen, p, AussenArt.Steg) ?? WasserBei(aussen, p, AussenArt.Teich)) is { } wasser)
        {
            return wasser;
        }
        // Welle Z: ein Fussweg vor dem Park (er fuehrt oft hindurch)
        if (FusswegBei(aussen, p, greifen) is { } fussweg)
        {
            return fussweg;
        }
        var land = Aussen.Landschaft(aussen);
        foreach (var (s, _, _) in alle.Where(a => a.Stelle.Art == AussenArt.Park).Reverse())
        {
            var l = Objekt(land[s.Nummer]);
            if (x >= Py.F(l["x"]) && x <= Py.F(l["x"]) + Py.F(l["w"]) && y >= Py.F(l["y"]) && y <= Py.F(l["y"]) + Py.F(l["h"]))
            {
                return s;
            }
        }
        return alle.Where(a => a.Stelle.Art == AussenArt.Strasse)
            .Select(a => (a.Stelle, D: Kurve.Abstand(p, Linie(Objekt(land[a.Stelle.Nummer])["punkte"])), Halb: (StadtSzene.StrassenBreite / 2) + Gehweg(aussen, a.Stelle)))
            .Where(a => a.D <= a.Halb + 0.05).OrderBy(a => a.D).Select(a => a.Stelle).FirstOrDefault()
            ?? WasserBei(aussen, p, AussenArt.Bahn) ?? WasserBei(aussen, p, AussenArt.Fluss);
    }

    // ---- Setzen ---------------------------------------------------------------------------------------

    /// <summary>
    /// Neues Stueck mit der Mitte bei (x, y). Strasse: waagrecht, <see cref="NeueStrasse"/> lang, Gehweg
    /// <see cref="NeuerGehweg"/> je Seite, Enden rasten an Strassen ein; Gebaeude: Modell <paramref name="modell"/>; Baum kommt zur ersten Baumreihe. Haeuser mit Ort und
    /// der Paketplatz lassen sich nicht neu setzen (null).
    /// </summary>
    public static AussenStelle? Setzen(PyObjekt aussen, AussenArt art, double x, double y, bool raster = true, string? modell = null) =>
        SetzenUndDrehen(aussen, art, x, y, raster, modell, 0);

    /// <summary>
    /// Welle E: setzen, <paramref name="dreh"/> Schritte drehen (Vierteldrehungen wie der Knopf „Drehen“ beim Setzen), dann
    /// einrasten (nur mit <paramref name="raster"/>: Strassenenden, Haeuser und Katalog-Stuecke an den Gehweg, Fluss an Fluss
    /// oder Kartenrand). Ein einziger Weg fuer die Draufsicht, die 3D-Ansicht und ihre Vorschau.
    /// </summary>
    public static AussenStelle? SetzenUndDrehen(PyObjekt aussen, AussenArt art, double x, double y, bool raster, string? modell, int dreh)
    {
        var s = SetzenRoh(aussen, art, x, y, raster, modell);
        for (var i = 0; s is not null && i < dreh; i++)
        {
            Drehen(aussen, s);
        }
        if (s is not null && raster)
        {
            NachSetzenEinrasten(aussen, s, gedreht: dreh % 4 != 0);
        }
        return s;
    }

    /// <summary>Einrasten nach dem Setzen (auch eines Blueprints): Haus/Gebaeude und Katalog an den Gehweg, Fluss, gedrehte Gerade.</summary>
    public static void NachSetzenEinrasten(PyObjekt aussen, AussenStelle s, bool gedreht = false)
    {
        switch (s.Art)
        {
            case AussenArt.Haus or AussenArt.Gebaeude:
                HausAnGehweg(aussen, s);
                break;
            case AussenArt.Katalog:
                KatalogAnGehweg(aussen, s);
                break;
            case AussenArt.Fluss or AussenArt.Bahn:
                FlussEinrasten(aussen, s);
                break;
            case AussenArt.Strasse when gedreht && Objekt(Aussen.Landschaft(aussen)[s.Nummer]) is var l && !Strassenkurve.IstKurve(l) && !Kreisverkehr.IstRing(l):
                // die gedrehte Gerade rastet wie beim Ziehen ein (vorher nur waagerecht vor dem Drehen)
                GeradeEinrasten(aussen, l);
                break;
        }
    }

    private static AussenStelle? SetzenRoh(PyObjekt aussen, AussenArt art, double x, double y, bool raster, string? modell)
    {
        var (breite, hoehe) = Aussen.Groesse(aussen);
        if (x < 0 || y < 0 || x > breite || y > hoehe)
        {
            return null;
        }
        if (IstWasserOderBahn(art))
        {
            return WasserSetzen(aussen, art, x, y, raster, modell);
        }
        var land = Aussen.Landschaft(aussen);
        var r = raster ? Bearbeitung.Raster : Bearbeitung.RasterFein;
        double Auf(double w) => Bearbeitung.AufRaster(w, r);
        switch (art)
        {
            case AussenArt.Kurve:
                return KurveSetzen(aussen, x, y, modell, raster);
            case AussenArt.Kreisverkehr:
                return KreisverkehrSetzen(aussen, x, y, modell);
            case AussenArt.Strasse:
            {
                var mx = Bearbeitung.AufRaster(x, Bearbeitung.Raster);
                var my = Bearbeitung.AufRaster(y, Bearbeitung.Raster);
                var a = new Vector2((float)Math.Max(0, mx - (NeueStrasse / 2)), (float)my);
                var b = new Vector2((float)Math.Min(breite, mx + (NeueStrasse / 2)), (float)my);
                var neu = new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { PunktObjekt(a), PunktObjekt(b) } }, { "gehweg", Bearbeitung.Zahl(NeuerGehweg) } };
                land.Add(neu);
                // Patch S 0.12.1: an ein freies Ende mit Lage und Richtung, sonst Einmuendung nur auf einer Strasse ohne Bogen
                if (raster)
                {
                    GeradeEinrasten(aussen, neu);
                }
                return new AussenStelle(AussenArt.Strasse, land.Count - 1);
            }
            case AussenArt.Gebaeude:
            {
                if (modell is null || Get(Modelle(aussen), modell) is null)
                {
                    return null;
                }
                var (gx, gy, gw, gh) = Grundflaeche(aussen, modell);
                var hx = Bearbeitung.AufRaster(x - (gw / 2) - gx, r);
                var hy = Bearbeitung.AufRaster(y - (gh / 2) - gy, r);
                land.Add(new PyObjekt { { "typ", "gebaeude" }, { "modell", modell }, { "x", Bearbeitung.Zahl(hx) }, { "y", Bearbeitung.Zahl(hy) } });
                return new AussenStelle(AussenArt.Gebaeude, land.Count - 1);
            }
            case AussenArt.Baum:
            {
                var i = land.FindIndex(t => t is PyObjekt o && Py.Gleich(o["typ"], "baum"));
                if (i < 0)
                {
                    land.Add(new PyObjekt { { "typ", "baum" }, { "punkte", new List<object?>() } });
                    i = land.Count - 1;
                }
                var punkte = Liste(Objekt(land[i])["punkte"]);
                punkte.Add(Punkt(Auf(x), Auf(y)));
                return new AussenStelle(AussenArt.Baum, i, punkte.Count - 1);
            }
            case AussenArt.Park:
            {
                var px = Math.Clamp(Bearbeitung.AufRaster(x - (NeuerPark / 2), Bearbeitung.Raster), 0, Math.Max(0, breite - NeuerPark));
                var py = Math.Clamp(Bearbeitung.AufRaster(y - (NeuerPark / 2), Bearbeitung.Raster), 0, Math.Max(0, hoehe - NeuerPark));
                land.Add(new PyObjekt { { "typ", "park" }, { "x", Bearbeitung.Zahl(px) }, { "y", Bearbeitung.Zahl(py) }, { "w", Bearbeitung.Zahl(NeuerPark) }, { "h", Bearbeitung.Zahl(NeuerPark) } });
                return new AussenStelle(AussenArt.Park, land.Count - 1);
            }
            case AussenArt.Beet:
                Aussen.Beete(aussen).Add(Punkt(Auf(x), Auf(y)));
                return new AussenStelle(AussenArt.Beet, Aussen.Beete(aussen).Count - 1);
            case AussenArt.Laterne:
                Aussen.Laternen(aussen).Add(new List<object?> { Bearbeitung.Zahl(Auf(x)), Bearbeitung.Zahl(Auf(y)), false });
                return new AussenStelle(AussenArt.Laterne, Aussen.Laternen(aussen).Count - 1);
            case AussenArt.Katalog:
            {
                // Phase 13 Welle 3b: modell = Kennung "paket/name" aus dem vollen Katalog, Vorgabegroesse des Katalogs
                if (modell is null || Katalog.Aktiv?.Eintrag(modell) is not { } e)
                {
                    return null;
                }
                double w = e.Aussen[0], t = e.Aussen[1];
                var liste = Aussen.KatalogStueckeAnlegen(aussen);
                liste.Add(new PyObjekt { { "id", e.Id }, { "x", Bearbeitung.Zahl(Auf(x)) }, { "y", Bearbeitung.Zahl(Auf(y)) }, { "w", Bearbeitung.Zahl(w) }, { "t", Bearbeitung.Zahl(t) }, { "dreh", 0L } });
                return new AussenStelle(AussenArt.Katalog, liste.Count - 1);
            }
            default:
                return null;
        }
    }

    private static List<object?> PunktObjekt(Vector2 p) => Punkt(p.X, p.Y);

    /// <summary>Strassenende auf die naechste andere Strasse legen, wenn es naeher als <see cref="EinrastenAb"/> liegt.</summary>
    public static Vector2 Einrasten(Vector2 ende, IEnumerable<List<Vector2>> andere)
    {
        var bester = ende;
        var abstand = (float)EinrastenAb;
        foreach (var linie in andere)
        {
            for (var i = 0; i + 1 < linie.Count; i++)
            {
                var q = Fusspunkt(ende, linie[i], linie[i + 1]);
                var d = Vector2.Distance(ende, q);
                if (d > 0.001f && d < abstand)
                {
                    (bester, abstand) = (q, d);
                }
            }
        }
        return new Vector2((float)R(bester.X), (float)R(bester.Y));
    }

    private static Vector2 Fusspunkt(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var l = ab.LengthSquared();
        return l < 1e-9f ? a : a + (ab * Math.Clamp(Vector2.Dot(p - a, ab) / l, 0f, 1f));
    }

    // ---- Verschieben ----------------------------------------------------------------------------------

    /// <summary>
    /// Stueck um (dx, dy) verschieben, in ganzen Rasterschritten 0,5; danach rastet es ein (Strassenenden, Fluesse, Haeuser
    /// und Katalog-Stuecke an den Gehweg). Welle E: mit <paramref name="raster"/> = false („Einrasten“ aus) frei im feinen
    /// Raster 0,1 fuer alle Stuecke und ohne Einrasten.
    /// </summary>
    public static void Verschieben(PyObjekt aussen, AussenStelle s, double dx, double dy, bool raster = true)
    {
        (dx, dy) = (Schritt(dx, raster), Schritt(dy, raster));
        if (dx == 0 && dy == 0)
        {
            return;
        }
        VerschiebenGenau(aussen, s, dx, dy, raster);
        if (!raster)
        {
            return;
        }
        switch (s.Art)
        {
            case AussenArt.Haus or AussenArt.Gebaeude:
                HausAnGehweg(aussen, s);
                break;
            case AussenArt.Katalog:
                KatalogAnGehweg(aussen, s);
                break;
            case AussenArt.Fluss or AussenArt.Bahn:
                if (!FlussEinrasten(aussen, s))
                {
                    BrueckeQuer(aussen, s);
                }
                break;
        }
    }

    /// <summary>Stueck genau um (dx, dy) verschieben (ohne Raster); Strassen rasten nur mit <paramref name="einrasten"/> ein.</summary>
    private static void VerschiebenGenau(PyObjekt aussen, AussenStelle s, double dx, double dy, bool einrasten = false)
    {
        if (IstWasserOderBahn(s.Art))
        {
            WasserVerschieben(aussen, s, dx, dy);
            return;
        }
        if (s.Art == AussenArt.Fussweg)
        {
            FusswegVerschieben(aussen, s, dx, dy);
            return;
        }
        var land = Aussen.Landschaft(aussen);
        void Schieben(object? punkt)
        {
            var p = Liste(punkt);
            p[0] = Bearbeitung.Zahl(Py.F(p[0]) + dx);
            p[1] = Bearbeitung.Zahl(Py.F(p[1]) + dy);
        }
        void Feld(PyObjekt o, string x, string y)
        {
            Py.Setzen(o, x, Bearbeitung.Zahl(Py.F(o[x]) + dx));
            Py.Setzen(o, y, Bearbeitung.Zahl(Py.F(o[y]) + dy));
        }
        switch (s.Art)
        {
            case AussenArt.Strasse:
            {
                var l = Objekt(land[s.Nummer]);
                var punkte = Liste(l["punkte"]);
                // Welle K3: ein Kreisverkehr nimmt Zufahrten und Laternen mit (vorher bestimmen, solange sie am Ring liegen)
                var kreis = Kreisverkehr.IstRing(l) ? KreisverkehrTeile(aussen, s) : default;
                var fein = Strassenkurve.IstKurve(l);
                foreach (var p in punkte)
                {
                    if (fein)
                    {
                        // Kurve: Punkte auf 0,0001 (S 0.12.1, sonst knickt die erste Sehne durch die Rundung)
                        var q = Liste(p);
                        q[0] = KurvenZahl(Py.F(q[0]) + dx);
                        q[1] = KurvenZahl(Py.F(q[1]) + dy);
                        continue;
                    }
                    Schieben(p);
                }
                if (kreis.Zufahrten is not null)
                {
                    var weg = new Vector2((float)dx, (float)dy);
                    KreisverkehrMitnehmen(aussen, kreis, p => p + weg);
                    break;
                }
                if (!einrasten)
                {
                    break;
                }
                if (Strassenkurve.IstKurve(l))
                {
                    // Welle K1: eine Kurve bleibt ganz, ihre Enden rasten zusammen ein
                    KurveEinrasten(aussen, s);
                    break;
                }
                GeradeEinrasten(aussen, l);
                break;
            }
            case AussenArt.Park:
                Feld(Objekt(land[s.Nummer]), "x", "y");
                break;
            case AussenArt.Gebaeude:
            {
                var l = Objekt(land[s.Nummer]);
                Feld(l, "x", "y");
                foreach (var z in Zierwege(aussen, Py.Str(l["modell"])))
                {
                    WegMitnehmen(aussen, z, dx, dy);
                }
                break;
            }
            case AussenArt.Haus:
            {
                var o = Ort(aussen, s.Ort)!;
                Feld(o, "x", "y");
                if (o["schild"] is List<object?> schild)
                {
                    Schieben(schild);
                }
                if (Zugang(aussen, s.Ort) is { } z)
                {
                    WegMitnehmen(aussen, z, dx, dy);
                }
                break;
            }
            case AussenArt.Baum:
                Schieben(Liste(Objekt(land[s.Nummer])["punkte"])[s.Punkt]);
                break;
            case AussenArt.Beet:
                Schieben(Aussen.Beete(aussen)[s.Nummer]);
                break;
            case AussenArt.Laterne:
                Schieben(Aussen.Laternen(aussen)[s.Nummer]);
                break;
            case AussenArt.Katalog:
                Feld(Objekt(Aussen.KatalogStuecke(aussen)[s.Nummer]), "x", "y");
                break;
            case AussenArt.Paketplatz:
                if (PaketLage(aussen) is { } lage)
                {
                    PaketSetzen(aussen, lage.Mitte.X + dx, lage.Mitte.Y + dy);
                }
                break;
        }
    }

    /// <summary>Tuer und Fussweg mitnehmen, dann das Ende des Wegs wieder an eine Strasse legen (<see cref="WegAnschliessen"/>).</summary>
    private static void WegMitnehmen(PyObjekt aussen, PyObjekt zugang, double dx, double dy)
    {
        var tuer = Liste(zugang["tuer"]);
        tuer[0] = Bearbeitung.Zahl(Py.F(tuer[0]) + dx);
        tuer[1] = Bearbeitung.Zahl(Py.F(tuer[1]) + dy);
        var weg = Liste(zugang["weg"]);
        foreach (var p in weg)
        {
            var q = Liste(p);
            q[0] = Bearbeitung.Zahl(Py.F(q[0]) + dx);
            q[1] = Bearbeitung.Zahl(Py.F(q[1]) + dy);
        }
        WegAnschliessen(weg, Strassen(aussen));
    }

    /// <summary>
    /// Endet ein Fussweg nicht mehr auf einer Strasse, wird sein letztes Stueck in seiner Richtung bis zur naechsten
    /// Strasse verlaengert oder gekuerzt (hoechstens 6 weit). Findet sich keine, bleibt er (die Pruefung meldet es rot).
    /// </summary>
    public static bool WegAnschliessen(List<object?> weg, IReadOnlyList<List<Vector2>> strassen)
    {
        var punkte = weg.OfType<List<object?>>().Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1]))).ToList();
        if (punkte.Count < 2)
        {
            return false;
        }
        var ende = punkte[^1];
        if (strassen.Any(s => s.Count >= 2 && Kurve.Abstand(ende, s) <= 0.02f))
        {
            return true;
        }
        var start = punkte[^2];
        var richtung = ende - start;
        if (richtung.LengthSquared() < 1e-9f)
        {
            return false;
        }
        richtung = Vector2.Normalize(richtung);
        float? bestes = null;
        foreach (var linie in strassen)
        {
            for (var i = 0; i + 1 < linie.Count; i++)
            {
                if (Schnitt(start, richtung, linie[i], linie[i + 1]) is { } t && t > 0.01f && t <= 6f && (bestes is null || t < bestes))
                {
                    bestes = t;
                }
            }
        }
        if (bestes is not { } weit)
        {
            return false;
        }
        var neu = start + (richtung * weit);
        weg[^1] = Punkt(neu.X, neu.Y);
        return true;
    }

    /// <summary>Abstand t auf dem Strahl p + t·d bis zur Strecke a-b, sonst null.</summary>
    private static float? Schnitt(Vector2 p, Vector2 d, Vector2 a, Vector2 b)
    {
        var e = b - a;
        var nenner = (d.X * e.Y) - (d.Y * e.X);
        if (MathF.Abs(nenner) < 1e-6f)
        {
            return null;
        }
        var ap = a - p;
        var t = ((ap.X * e.Y) - (ap.Y * e.X)) / nenner;
        var u = ((ap.X * d.Y) - (ap.Y * d.X)) / nenner;
        return u is >= -0.001f and <= 1.001f ? t : null;
    }

    /// <summary>Paketplatz an diese Stelle (vor/seitlich zur Tuer der Wohnung, auf 0,01 gerundet).</summary>
    public static void PaketSetzen(PyObjekt aussen, double x, double y)
    {
        if (PaketLage(aussen) is not { } lage)
        {
            return;
        }
        var d = new Vector2((float)x, (float)y) - lage.Tuer;
        var quer = new Vector2(-lage.Aussen.Y, lage.Aussen.X);
        var darstellung = Get(Aussen.Stadtdaten(aussen), "darstellung") as PyObjekt;
        if (darstellung is null)
        {
            darstellung = [];
            Py.Setzen(Aussen.Stadtdaten(aussen), "darstellung", darstellung);
        }
        Py.Setzen(darstellung, Lieferpaket.Abschnitt, new PyObjekt { { "vor", Bearbeitung.Zahl(Vector2.Dot(d, lage.Aussen)) }, { "seitlich", Bearbeitung.Zahl(Vector2.Dot(d, quer)) } });
    }

    /// <summary>Stueck an genaue Werte setzen: Mitte bei (x, y) (Strassen: ihr erster Punkt).</summary>
    public static void Platzieren(PyObjekt aussen, AussenStelle s, double x, double y)
    {
        if (s.Art == AussenArt.Paketplatz)
        {
            PaketSetzen(aussen, x, y);
            return;
        }
        var (ax, ay) = Lage(aussen, s);
        // Welle E: genau an die Werte (auf 0,01), ohne Raster und ohne Einrasten
        VerschiebenGenau(aussen, s, R(x - ax), R(y - ay));
    }

    /// <summary>Bezugspunkt eines Stuecks fuer die Felder x, y: Strasse = erster Punkt, Haus und Park = Ecke oben links, sonst die Mitte.</summary>
    public static (double X, double Y) Lage(PyObjekt aussen, AussenStelle s)
    {
        var land = Aussen.Landschaft(aussen);
        switch (s.Art)
        {
            case AussenArt.Strasse:
                var p = Liste(Liste(Objekt(land[s.Nummer])["punkte"])[0]);
                return (Py.F(p[0]), Py.F(p[1]));
            case AussenArt.Park:
            case AussenArt.Gebaeude:
                var l = Objekt(land[s.Nummer]);
                return (Py.F(l["x"]), Py.F(l["y"]));
            case AussenArt.Haus:
                var o = Ort(aussen, s.Ort)!;
                return (Py.F(o["x"]), Py.F(o["y"]));
            case AussenArt.Fluss or AussenArt.Teich or AussenArt.Steg or AussenArt.Bahn:
                return WasserLage(aussen, s);
            default:
                var a = Alle(aussen).First(x => x.Stelle == s);
                return (a.X, a.Y);
        }
    }

    // ---- Drehen, Kopieren, Loeschen, Gehweg -----------------------------------------------------------

    /// <summary>Warum sich das Stueck nicht drehen laesst (null = es geht).</summary>
    public static string? DrehenNicht(AussenStelle s) => s.Art switch
    {
        AussenArt.Strasse or AussenArt.Park or AussenArt.Katalog or AussenArt.Fluss or AussenArt.Teich or AussenArt.Steg or AussenArt.Bahn => null,
        AussenArt.Haus or AussenArt.Gebaeude => null,
        AussenArt.Laterne => Texte.LaterneDrehenNicht,
        AussenArt.Fussweg => Texte.FusswegDrehenNicht,
        _ => Texte.DrehenNichtNoetig,
    };

    /// <summary>Vierteldrehung im Uhrzeigersinn um die Mitte (Strasse: Mitte auf das Raster 0,5; Park: Breite und Tiefe tauschen).</summary>
    public static bool Drehen(PyObjekt aussen, AussenStelle s)
    {
        if (DrehenNicht(s) is not null)
        {
            return false;
        }
        if (IstWasserOderBahn(s.Art))
        {
            return WasserDrehen(aussen, s);
        }
        if (s.Art is AussenArt.Haus or AussenArt.Gebaeude)
        {
            // Welle E (Weisung P12-67 Frage 1 = a): Haeuser drehen in Vierteln mit Tuer, Fussweg und Schild
            return HausDrehen(aussen, s, 1);
        }
        if (s.Art == AussenArt.Katalog)
        {
            // Vierteldrehung um die Mitte: Breite und Tiefe tauschen, dreh zaehlt die Vierteldrehungen (0 bis 3)
            var k = Objekt(Aussen.KatalogStuecke(aussen)[s.Nummer]);
            var (kw, kt) = (k["w"], k["t"]);
            Py.Setzen(k, "w", kt);
            Py.Setzen(k, "t", kw);
            Py.Setzen(k, "dreh", (Py.Int(Get(k, "dreh", 0L)) + 1) % 4);
            return true;
        }
        var l = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        if (s.Art == AussenArt.Park)
        {
            double x = Py.F(l["x"]), y = Py.F(l["y"]), w = Py.F(l["w"]), h = Py.F(l["h"]);
            var (mx, my) = (Bearbeitung.AufRaster(x + (w / 2), Bearbeitung.Raster), Bearbeitung.AufRaster(y + (h / 2), Bearbeitung.Raster));
            Py.Setzen(l, "x", Bearbeitung.Zahl(mx - (h / 2)));
            Py.Setzen(l, "y", Bearbeitung.Zahl(my - (w / 2)));
            Py.Setzen(l, "w", Bearbeitung.Zahl(h));
            Py.Setzen(l, "h", Bearbeitung.Zahl(w));
            return true;
        }
        var punkte = Liste(l["punkte"]);
        var linie = Linie(punkte);
        var m = new Vector2((float)Bearbeitung.AufRaster((linie.Min(p => p.X) + linie.Max(p => p.X)) / 2, Bearbeitung.Raster),
            (float)Bearbeitung.AufRaster((linie.Min(p => p.Y) + linie.Max(p => p.Y)) / 2, Bearbeitung.Raster));
        // Welle K3: ein Kreisverkehr dreht sich um seine Mitte mit Zufahrten und Laternen
        var kreis = Kreisverkehr.IstRing(l) ? KreisverkehrTeile(aussen, s) : default;
        if (kreis.Zufahrten is not null)
        {
            m = Kreisverkehr.Mitte(linie);
        }
        var fein = Strassenkurve.IstKurve(l);
        for (var i = 0; i < punkte.Count; i++)
        {
            var d = linie[i] - m;
            punkte[i] = fein ? KurvenPunkt(new Vector2(m.X - d.Y, m.Y + d.X)) : Punkt(m.X - d.Y, m.Y + d.X);
        }
        if (kreis.Zufahrten is not null)
        {
            KreisverkehrMitnehmen(aussen, kreis, p => new Vector2(m.X - (p - m).Y, m.Y + (p - m).X));
            return true;
        }
        if (Strassenkurve.IstKurve(l))
        {
            // Welle K1: die gedrehte Kurve rastet wieder an einem Strassenende ein
            KurveEinrasten(aussen, s);
        }
        return true;
    }

    /// <summary>Kopie daneben (versetzt um einen Rasterschritt bzw. die eigene Breite); Haeuser mit Ort und Paketplatz nicht.</summary>
    public static AussenStelle? Kopieren(PyObjekt aussen, AussenStelle s)
    {
        if (IstWasserOderBahn(s.Art))
        {
            return WasserKopieren(aussen, s);
        }
        if (s.Art == AussenArt.Fussweg)
        {
            return FusswegKopieren(aussen, s);
        }
        var land = Aussen.Landschaft(aussen);
        switch (s.Art)
        {
            case AussenArt.Strasse when IstKreisverkehr(aussen, s):
                return KreisverkehrKopieren(aussen, s);
            case AussenArt.Strasse:
            case AussenArt.Park:
            case AussenArt.Gebaeude:
            {
                var kopie = (PyObjekt)Py.Kopie(land[s.Nummer])!;
                land.Add(kopie);
                var neu = new AussenStelle(s.Art, land.Count - 1);
                var versatz = s.Art switch
                {
                    AussenArt.Park => Py.F(kopie["w"]) + Bearbeitung.Raster,
                    AussenArt.Gebaeude => HausFlaeche(aussen, neu).W + Bearbeitung.Raster,
                    _ => 1.0,
                };
                if (s.Art == AussenArt.Strasse)
                {
                    foreach (var p in Liste(kopie["punkte"]))
                    {
                        var q = Liste(p);
                        q[1] = Strassenkurve.IstKurve(kopie) ? KurvenZahl(Py.F(q[1]) + versatz) : Bearbeitung.Zahl(Py.F(q[1]) + versatz);
                    }
                }
                else
                {
                    Py.Setzen(kopie, "x", Bearbeitung.Zahl(Py.F(kopie["x"]) + versatz));
                }
                return neu;
            }
            case AussenArt.Baum:
            {
                var punkte = Liste(Objekt(land[s.Nummer])["punkte"]);
                var p = Liste(punkte[s.Punkt]);
                punkte.Add(Punkt(Py.F(p[0]) + Bearbeitung.Raster, Py.F(p[1])));
                return new AussenStelle(AussenArt.Baum, s.Nummer, punkte.Count - 1);
            }
            case AussenArt.Beet:
            {
                var p = Liste(Aussen.Beete(aussen)[s.Nummer]);
                Aussen.Beete(aussen).Add(Punkt(Py.F(p[0]) + Bearbeitung.Raster, Py.F(p[1])));
                return new AussenStelle(AussenArt.Beet, Aussen.Beete(aussen).Count - 1);
            }
            case AussenArt.Katalog:
            {
                var liste = Aussen.KatalogStuecke(aussen);
                var kopie = (PyObjekt)Py.Kopie(liste[s.Nummer])!;
                Py.Setzen(kopie, "x", Bearbeitung.Zahl(Py.F(kopie["x"]) + Py.F(kopie["w"]) + Bearbeitung.Raster));
                liste.Add(kopie);
                return new AussenStelle(AussenArt.Katalog, liste.Count - 1);
            }
            case AussenArt.Laterne:
            {
                var p = Liste(Aussen.Laternen(aussen)[s.Nummer]);
                Aussen.Laternen(aussen).Add(new List<object?> { Bearbeitung.Zahl(Py.F(p[0]) + 1), p[1], p[2] });
                return new AussenStelle(AussenArt.Laterne, Aussen.Laternen(aussen).Count - 1);
            }
            default:
                return null;
        }
    }

    /// <summary>Warum sich das Stueck nicht loeschen laesst (null = es geht).</summary>
    public static string? LoeschenNicht(AussenStelle s) => s.Art switch
    {
        AussenArt.Haus => Texte.OrtLoeschenNicht,
        AussenArt.Paketplatz => Texte.PaketLoeschenNicht,
        _ => null,
    };

    public static bool Loeschen(PyObjekt aussen, AussenStelle s)
    {
        if (LoeschenNicht(s) is not null)
        {
            return false;
        }
        if (IstWasserOderBahn(s.Art))
        {
            WasserLoeschen(aussen, s);
            return true;
        }
        if (s.Art == AussenArt.Fussweg)
        {
            var fusswege = Aussen.Fusswege(aussen);
            fusswege.RemoveAt(s.Nummer);
            if (fusswege.Count == 0)
            {
                // der letzte Fussweg weg: der Abschnitt faellt weg (wie vor dem ersten Ziehen)
                var d = Aussen.Stadtdaten(aussen);
                var rest = d.Where(e => e.Key != Lernplattform.Core.Spiel.Probe.Fusswege.Abschnitt).ToList();
                d.Clear();
                d.AddRange(rest);
            }
            return true;
        }
        var land = Aussen.Landschaft(aussen);
        switch (s.Art)
        {
            case AussenArt.Baum:
                Liste(Objekt(land[s.Nummer])["punkte"]).RemoveAt(s.Punkt);
                break;
            case AussenArt.Beet:
                Aussen.Beete(aussen).RemoveAt(s.Nummer);
                break;
            case AussenArt.Laterne:
                Aussen.Laternen(aussen).RemoveAt(s.Nummer);
                break;
            case AussenArt.Katalog:
                Aussen.KatalogStuecke(aussen).RemoveAt(s.Nummer);
                break;
            case AussenArt.Gebaeude:
            {
                var modell = Py.Str(Objekt(land[s.Nummer])["modell"]);
                land.RemoveAt(s.Nummer);
                // Zierweg nur weg, wenn kein Haus dieses Modells mehr steht
                if (!land.OfType<PyObjekt>().Any(t => Py.Gleich(t["typ"], "gebaeude") && Py.Gleich(t["modell"], modell))
                    && Get(Aussen.Stadtdaten(aussen), "darstellung") is PyObjekt d && d["zierwege"] is List<object?> zw)
                {
                    zw.RemoveAll(z => z is PyObjekt o && Py.Gleich(o["modell"], modell));
                }
                // Welle 4c: der Innenraum eines gesetzten Blueprints geht mit dem letzten Gebaeude seines Modells (Rueckgaengig holt beides)
                if (!land.OfType<PyObjekt>().Any(t => Py.Gleich(t["typ"], "gebaeude") && Py.Gleich(t["modell"], modell)) && Get(aussen, "bauten") is PyObjekt bauten)
                {
                    bauten.RemoveAll(b => b.Key == modell);
                }
                break;
            }
            default:
                land.RemoveAt(s.Nummer);
                break;
        }
        return true;
    }

    /// <summary>Gehweg je Seite einer Strasse (0 = keiner).</summary>
    public static float Gehweg(PyObjekt aussen, AussenStelle s) =>
        s.Art == AussenArt.Strasse && Get(Objekt(Aussen.Landschaft(aussen)[s.Nummer]), "gehweg") is { } g ? (float)Py.F(g) : 0f;

    /// <summary>Gehweg setzen (0 nimmt ihn weg). Spiel und 3D-Ansicht zeichnen ihn seit Welle 4a (StadtSzene, Feld "gehweg").</summary>
    public static void GehwegSetzen(PyObjekt aussen, AussenStelle s, double breite)
    {
        var l = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        if (breite <= 0)
        {
            var rest = l.Where(p => p.Key != "gehweg").ToList();
            l.Clear();
            l.AddRange(rest);
        }
        else
        {
            Py.Setzen(l, "gehweg", Bearbeitung.Zahl(breite));
        }
    }

    /// <summary>Laterne einfach oder doppelt.</summary>
    public static void LaterneDoppelt(PyObjekt aussen, AussenStelle s, bool doppelt) => Liste(Aussen.Laternen(aussen)[s.Nummer])[2] = doppelt;

    /// <summary>Laenge einer Strasse (Summe ihrer Stuecke).</summary>
    public static double Laenge(PyObjekt aussen, AussenStelle s)
    {
        var l = Linie(Objekt(Aussen.Landschaft(aussen)[s.Nummer])["punkte"]);
        return R(Enumerable.Range(0, Math.Max(0, l.Count - 1)).Sum(i => Vector2.Distance(l[i], l[i + 1])));
    }

    /// <summary>Gerade Strasse auf eine neue Laenge bringen (das letzte Stueck wird verlaengert oder gekuerzt, ab 1).</summary>
    public static void LaengeSetzen(PyObjekt aussen, AussenStelle s, double laenge)
    {
        var punkte = Liste(Objekt(Aussen.Landschaft(aussen)[s.Nummer])["punkte"]);
        var l = Linie(punkte);
        if (l.Count < 2)
        {
            return;
        }
        var ohneLetztes = Enumerable.Range(0, l.Count - 2).Sum(i => Vector2.Distance(l[i], l[i + 1]));
        var rest = Math.Max(1, laenge - ohneLetztes);
        var d = Vector2.Normalize(l[^1] - l[^2]);
        var ende = l[^2] + (d * (float)rest);
        var eigene = Objekt(Aussen.Landschaft(aussen)[s.Nummer]);
        var andere = EinrastenAltFuerTests ? Strassen(aussen).Where(x => !(x.Count == l.Count && x.SequenceEqual(l))).ToList() : GeradeLinien(aussen, eigene);
        punkte[^1] = PunktObjekt(Einrasten(ende, andere));
    }

    /// <summary>Name eines Katalog-Stuecks mit Gruppe und Paket (unbekannt: die Kennung).</summary>
    public static string KatalogBeschreibung(string id) =>
        Katalog.Aktiv?.Eintrag(id) is { } e
            ? (e.Art == KatalogArt.Effekt ? Texte.Effekt + " „" : "„") + e.Anzeige + "“ (" + e.Gruppe + ", " + Katalog.Aktiv.PaketTitel.GetValueOrDefault(e.Paket, e.Paket) + ")"
            : "„" + id + "“";

    /// <summary>Kurztext eines Stuecks fuer Anzeige und Screenreader.</summary>
    /// <summary>Name eines Gebaeudes aus einem Blueprint mit Innenraum (Teil "bauten"), sonst null.</summary>
    public static string? BauName(PyObjekt aussen, AussenStelle s) =>
        s.Art == AussenArt.Gebaeude && Aussen.Landschaft(aussen).ElementAtOrDefault(s.Nummer) is PyObjekt g && g["modell"] is string m
            && Get(aussen, "bauten") is PyObjekt bauten && bauten[m] is PyObjekt bau && bau["name"] is string n ? n : null;

    public static string Beschreibung(PyObjekt aussen, AussenStelle s, Func<string, string> ortName)
    {
        var (x, y) = Lage(aussen, s);
        var lage = string.Create(De, $", x {x:0.##}, y {y:0.##}");
        return s.Art switch
        {
            AussenArt.Strasse when KreisverkehrVon(aussen, s) is { } kr => Texte.KreisverkehrName(kr) + (Gehweg(aussen, s) > 0 ? string.Create(De, $", Gehweg {Gehweg(aussen, s):0.##}") : "") + lage,
            AussenArt.Strasse when KurveVon(aussen, s) is { } k => Strassenkurve.Name(k.Form, k.Winkel, k.Radius) + (Gehweg(aussen, s) > 0 ? string.Create(De, $", Gehweg {Gehweg(aussen, s):0.##}") : "") + lage,
            AussenArt.Strasse => string.Create(De, $"{Texte.Strasse}, {Laenge(aussen, s):0.##} lang") + (Gehweg(aussen, s) > 0 ? string.Create(De, $", Gehweg {Gehweg(aussen, s):0.##}") : "") + lage,
            AussenArt.Haus => Texte.Haus + " „" + ortName(s.Ort) + "“" + lage,
            AussenArt.Gebaeude => (BauName(aussen, s) is { } bau ? Texte.NeuesGebaeude + " „" + bau + "“" : Texte.HausOhneOrt)
                + " (" + Py.Str(Objekt(Aussen.Landschaft(aussen)[s.Nummer])["modell"]) + ")" + lage,
            AussenArt.Laterne => (Liste(Aussen.Laternen(aussen)[s.Nummer])[2] is true ? Texte.LaterneDoppelt : Texte.Laterne) + " Nr. " + (s.Nummer + 1).ToString(De) + lage,
            AussenArt.Beet => Texte.Beet + lage,
            AussenArt.Baum => Texte.Baum + lage,
            AussenArt.Park => Texte.Park + lage,
            AussenArt.Katalog => KatalogBeschreibung(KatalogId(aussen, s)) + lage,
            AussenArt.Fluss or AussenArt.Teich or AussenArt.Steg or AussenArt.Bahn => WasserBeschreibung(aussen, s, lage),
            AussenArt.Fussweg => FusswegBeschreibung(aussen, s, lage),
            _ => string.Create(De, $"{Texte.Paketplatz}, vor {PaketWerte(aussen).Vor:0.##}, seitlich {PaketWerte(aussen).Seitlich:0.##}"),
        };
    }
}
