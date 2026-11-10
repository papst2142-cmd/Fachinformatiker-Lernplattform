using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// S 0.9.0 (Weisung P12-48, Plan Abschnitt 5): die 3D-Ansicht des Editors ist eine kleine leere Stadt ohne Kleingruen
/// (<see cref="Aussen.Leer"/>, frei) mit dem Gebaeude als Gebaeude ohne Ort. Gebaut wird mit genau dem Weg der
/// 3D-Ansicht der Stadt (<see cref="Ansicht3D.Bauen"/>, dort mit <see cref="Bauteile.MitGebaeuden"/>): Licht, Nacht,
/// leuchtende Fenster und Dreiecke sind die des Spiels. Gezeigt werden die Teile bis zur gewaehlten Ebene (die Ebenen
/// darueber sind ausgeblendet). Das Bauraster liegt mit seiner Ecke bei (<see cref="Rand"/>, <see cref="Rand"/>).
/// </summary>
public static class EntwurfAnsicht3D
{
    /// <summary>Wiese um das Bauraster (Felder je Seite).</summary>
    public const int Rand = 4;

    public const int Groesse = Entwurf.Raster + (2 * Rand);

    public const string StadtId = "neu_entwurf";

    /// <summary>Modellname des Gebaeudes in der Vorschau.</summary>
    public const string ModellName = Bauteile.ModellPraefix + "entwurf";

    /// <summary>Die kleine Stadt mit dem Gebaeude (Teile bis <paramref name="bisEbene"/>; Teile ohne Modell fehlen).</summary>
    public static StadtFassung Stadt(Entwurf e, int bisEbene, Spieldaten daten, Func<string, GrafikModell?>? modell = null)
    {
        modell ??= Katalog.Bauteil;
        var leer = Aussen.Leer(daten.Fassung, StadtId, Groesse, Groesse, frei: true);
        var aussen = Aussen.Aus(leer);
        var teile = e.Teile.Where(t => t.Stockwerk <= bisEbene && t.Art is not null && modell(t.Id) is not null).ToList();
        if (teile.Count > 0)
        {
            var gm = Bauteile.Zusammensetzen(ModellName, teile, modell);
            if (Get(Aussen.Karte(aussen), "modelle") is not PyObjekt modelle)
            {
                modelle = new PyObjekt();
                Py.Setzen(Aussen.Karte(aussen), "modelle", modelle);
            }
            Py.Setzen(modelle, ModellName, Bauteile.Block(gm));
            Aussen.Landschaft(aussen).Add(new PyObjekt { { "typ", "gebaeude" }, { "modell", ModellName }, { "x", (long)Rand }, { "y", (long)Rand } });
            Py.Setzen(Aussen.Stadtdaten(aussen), "bauteile", new PyObjekt
            {
                { ModellName, new PyObjekt { { "teile", Bauteile.TeileDaten(teile) }, { "eingang", Bauteile.EingaengeDaten(e.Eingaenge) } } },
            });
            // S 0.31.0 (Sammelliste 264): Farben des Entwurfs gleich in der Vorschau
            foreach (var (teil, wert) in e.Farben)
            {
                AussenBearbeitung.FarbeSetzen(aussen, ModellName, teil, wert as string);
            }
        }
        return Aussen.Fassung(aussen, leer, new PyObjekt(), 0, null, Werkzeug.Kennzeile, "", "");
    }

    /// <summary>Spiel fuer die Vorschau bauen (blockiert: im Hintergrund rufen).</summary>
    public static StadtSpiel Bauen(Entwurf e, int bisEbene, Spieldaten daten, Ansicht3D.Einstellung einstellung, StadtSzene.Vorrat? vorrat = null) =>
        Ansicht3D.Bauen(Stadt(e, bisEbene, daten), daten, einstellung, vorrat);

    /// <summary>Feld im Raster unter dem Strahl auf der Hoehe der Ebene, sonst null (ausserhalb oder Blick in den Himmel).</summary>
    public static (int X, int Y)? Feld(Vector3 start, Vector3 richtung, int ebene, bool halle = false)
    {
        var hoehe = ebene * Bauteile.EbeneHoehe(halle);
        if (MathF.Abs(richtung.Y) < 1e-9f)
        {
            return null;
        }
        var t = (hoehe - start.Y) / richtung.Y;
        if (t < 0)
        {
            return null;
        }
        var p = start + (richtung * t);
        var f = Bauteile.Feld(halle);
        var (x, y) = ((int)MathF.Floor((p.X - Rand) / f), (int)MathF.Floor((p.Z - Rand) / f));
        return Entwurf.ImRaster(x, y, halle) ? (x, y) : null;
    }

    /// <summary>
    /// Das vorderste Teil unter dem Strahl (Kasten seines Felds, Ebenen bis <paramref name="bisEbene"/>), sonst -1. Teile an
    /// einer Fassade treffen als duenne Scheibe an ihrer Seite (sonst waeren sie hinter ihrer Wand nie zu waehlen).
    /// </summary>
    public static int Treffer(Entwurf e, Vector3 start, Vector3 richtung, int bisEbene)
    {
        var beste = -1;
        var bestT = float.MaxValue;
        for (var i = 0; i < e.Teile.Count; i++)
        {
            var t = e.Teile[i];
            if (t.Stockwerk > bisEbene || t.Art is null)
            {
                continue;
            }
            var (min, max) = Kasten(t, e.Halle);
            if (Bauen3D.Schnitt(start, richtung, min, max) is { } d && (d < bestT - 1e-4f || (MathF.Abs(d - bestT) <= 1e-4f && !t.Art.BelegtFeld)))
            {
                (beste, bestT) = (i, d);
            }
        }
        return beste;
    }

    /// <summary>Kasten eines Teils in der Vorschau-Stadt (Feld und Ebene; Fassaden-Teil als Scheibe an der Seite).</summary>
    public static (Vector3 Min, Vector3 Max) Kasten(Bauteile.Teil t, bool halle = false)
    {
        var f = Bauteile.Feld(halle);
        float x0 = (t.X * f) + Rand, z0 = (t.Y * f) + Rand, y0 = t.Stockwerk * Bauteile.EbeneHoehe(halle);
        var min = new Vector3(x0, y0, z0);
        var max = new Vector3(x0 + f, y0 + Bauteile.EbeneHoehe(halle), z0 + f);
        if (t.Art?.Art == Bauteile.Art.Detail)
        {
            const float d = 0.12f;
            (min, max) = t.Fassade switch
            {
                Bauteile.Seiten.Sued => (min with { Z = z0 + f - d }, max with { Z = z0 + f + d }),
                Bauteile.Seiten.Nord => (min with { Z = z0 - d }, max with { Z = z0 + d }),
                Bauteile.Seiten.Ost => (min with { X = x0 + f - d }, max with { X = x0 + f + d }),
                _ => (min with { X = x0 - d }, max with { X = x0 + d }),
            };
        }
        else if (t.Art?.Art == Bauteile.Art.DachDetail)
        {
            max = max with { Y = y0 + 0.35f };
        }
        return (min, max);
    }

    /// <summary>Ecken eines Felds der Ebene als Vieleck in der Vorschau-Stadt (Schatten, Raster).</summary>
    public static Vector3[] FeldEcken(int x, int y, int ebene, float ueber = 0.02f, bool halle = false)
    {
        var f = Bauteile.Feld(halle);
        float x0 = (x * f) + Rand, z0 = (y * f) + Rand, h = (ebene * Bauteile.EbeneHoehe(halle)) + ueber;
        return [new(x0, h, z0), new(x0 + f, h, z0), new(x0 + f, h, z0 + f), new(x0, h, z0 + f)];
    }

    /// <summary>Mitte des Bauraster und ein Abstand, bei dem das Gebaeude (oder das leere Raster) ganz ins Bild passt.</summary>
    public static (float X, float Z, float Abstand) GanzZeigen(Entwurf e)
    {
        if (e.Teile.Count == 0)
        {
            return (Rand + (Entwurf.Raster / 2f), Rand + (Entwurf.Raster / 2f), Math.Clamp(Entwurf.Raster * 1.1f, Kamera.AbstandMin, Kamera.AbstandMax));
        }
        var f = e.Feld;
        float x0 = e.Teile.Min(t => t.X) * f, x1 = (e.Teile.Max(t => t.X) + 1) * f, y0 = e.Teile.Min(t => t.Y) * f, y1 = (e.Teile.Max(t => t.Y) + 1) * f;
        var hoch = (e.Teile.Max(t => t.Stockwerk) + 1) * Bauteile.EbeneHoehe(e.Halle);
        var gross = MathF.Max(MathF.Max(x1 - x0, y1 - y0), hoch) + 3;
        return (Rand + ((x0 + x1) / 2), Rand + ((y0 + y1) / 2), Math.Clamp(gross * 1.6f, Kamera.AbstandMin, Kamera.AbstandMax));
    }
}
