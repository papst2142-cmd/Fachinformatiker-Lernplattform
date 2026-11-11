using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 13 Welle 4a (Weisung P12-28): Stuecke aus dem vollen CC0-Katalog des Stadtbau-Werkzeugs im Spiel. Innen steht ein
/// Stueck als Einrichtung mit dem Typ "cc0:paket/name" (Rechteck x, y, w, h wie jedes Moebel), draussen in
/// <c>stadtdaten.katalog</c> als {id, x, y (Mitte), w, t, dreh}. Das Spiel zeichnet nur, was mitgeliefert ist
/// (<c>inhalte_csharp/grafik/katalog_verbaut.bin</c>, Modelle mit dem Namen "cc0:paket/name"); ohne Modell ist die Fassung
/// ein roter Fund der Ladepruefung (<c>StadtbauPruefung.Verbaut</c>), nie ein stilles Weglassen.
/// Ein Effekt (Paket "particles") ist nur Aussehen: ein leuchtendes Zeichen aus 2 Dreiecken (Stern), das nichts versperrt.
/// </summary>
public static class Katalogstueck
{
    /// <summary>Vorsilbe der Typen und Modellnamen aus dem Katalog (gleich <c>Stadtbau.Core.Katalog.Praefix</c>).</summary>
    public const string Praefix = "cc0:";

    /// <summary>Vorsilbe der Effekte: genau die Stuecke des Pakets "particles" sind Effekte (Test im Werkzeug).</summary>
    public const string EffektPraefix = Wege.EffektPraefix;

    /// <summary>Dreiecke eines Effekts (Stern aus zwei Dreiecken, gleich <c>KatalogPruefung.EffektDreiecke</c>).</summary>
    public const int EffektDreiecke = 2;

    public static bool IstTyp(string? typ) => typ is not null && typ.StartsWith(Praefix, StringComparison.Ordinal);

    public static bool IstEffekt(string? typ) => Wege.IstEffekt(typ);

    /// <summary>
    /// Modell eines Effekts: ein Stern aus zwei gleichseitigen Dreiecken (eins mit der Spitze nach Norden, eins nach Sueden)
    /// flach im Quadrat 0..1 bei y = 0, nach oben gerichtet, in der Farbe <paramref name="farbe"/> (#RRGGBB). Dieselbe Form
    /// schreibt <c>werkzeuge/stadtbau/verbaut.py</c> in die mitgelieferte Datei (Test bytegleich).
    /// </summary>
    public static GrafikModell Stern(string name, string farbe)
    {
        var f = Netz.Farbe(farbe);
        var (r, g, b) = ((byte)Math.Round(f.X * 255), (byte)Math.Round(f.Y * 255), (byte)Math.Round(f.Z * 255));
        float[] ecken =
        [
            // Spitze nach Norden (z = 0), Grundseite unten; Reihenfolge gegen den Uhrzeigersinn von oben gesehen
            0.5f, 0f, 0f, 0.0669873f, 0f, 0.75f, 0.9330127f, 0f, 0.75f,
            // Spitze nach Sueden
            0.5f, 0f, 1f, 0.9330127f, 0f, 0.25f, 0.0669873f, 0f, 0.25f,
        ];
        return new GrafikModell(name, ecken, [r, g, b, r, g, b]);
    }

    /// <summary>
    /// Hoehe eines Modells im Rechteck b x t (ungedreht): das Modell behaelt sein Verhaeltnis, gestreckt mit dem Mittel der
    /// Streckung in Breite und Tiefe (so entstehen auch die Vorgabegroessen des Katalogs, <c>wandle_katalog.py</c>).
    /// </summary>
    public static float Hoehe(GrafikModell modell, float b, float t)
    {
        var g = modell.Max - modell.Min;
        return g.Y * (((b / MathF.Max(g.X, 1e-4f)) + (t / MathF.Max(g.Z, 1e-4f))) / 2);
    }

    /// <summary>Hoehe, in der ein Effekt schwebt: das Mittel von Breite und Tiefe (Vorgabe des Katalogs: Hoehe = Breite).</summary>
    public static float EffektHoehe(float b, float t) => (b + t) / 2;

    /// <summary>
    /// Lage eines Stuecks im Rechteck mit der Mitte (<paramref name="mx"/>, <paramref name="mz"/>), Breite <paramref name="w"/>
    /// und Tiefe <paramref name="t"/> (schon gedreht), <paramref name="dreh"/> Vierteldrehungen im Uhrzeigersinn: wie die
    /// Einrichtung der Innenraeume (<c>Innenraum.Modell</c>). Effekt: Stern in seiner Hoehe ueber dem Boden.
    /// </summary>
    public static Matrix4x4 Lage(GrafikModell modell, float mx, float mz, float w, float t, int dreh, float unten, bool effekt)
    {
        dreh = ((dreh % 4) + 4) % 4;
        var (b0, t0) = dreh % 2 == 1 ? (t, w) : (w, t);
        var g = modell.Max - modell.Min;
        var hoehe = effekt ? 0f : Hoehe(modell, b0, t0);
        var y = effekt ? unten + EffektHoehe(b0, t0) : unten;
        return Matrix4x4.CreateTranslation(-modell.Min)
            * Matrix4x4.CreateScale(b0 / MathF.Max(g.X, 1e-4f), effekt ? 1f : hoehe / MathF.Max(g.Y, 1e-4f), t0 / MathF.Max(g.Z, 1e-4f))
            * Matrix4x4.CreateTranslation(-b0 / 2, 0, -t0 / 2)
            * Matrix4x4.CreateRotationY(-MathF.PI / 2 * dreh)
            * Matrix4x4.CreateTranslation(mx, y, mz);
    }

    /// <summary>Ein Stueck draussen (Eintrag aus <c>stadtdaten.katalog</c>).</summary>
    public sealed record Draussen(string Typ, float X, float Z, float W, float T, int Dreh)
    {
        public bool Effekt => IstEffekt(Typ);

        public (float X1, float Z1, float X2, float Z2) Rechteck => (X - (W / 2), Z - (T / 2), X + (W / 2), Z + (T / 2));
    }

    /// <summary>
    /// Die Stuecke draussen aus den Stadtdaten (Feld "katalog"; fehlt es, keine). Wirft bei einem Eintrag ohne die Felder
    /// id, x, y, w, t (dreh 0 bis 3, fehlt = 0) wie jede Ladepruefung des Spiels.
    /// </summary>
    public static List<Draussen> AusStadtdaten(PyObjekt? stadtdaten)
    {
        var liste = new List<Draussen>();
        if (stadtdaten is null || Get(stadtdaten, "katalog") is not List<object?> roh)
        {
            return liste;
        }
        foreach (var e in roh)
        {
            var o = e as PyObjekt ?? throw new InvalidDataException("stadtdaten.katalog: Eintrag ist kein Objekt");
            if (o["id"] is not string id || id.Length == 0)
            {
                throw new InvalidDataException("stadtdaten.katalog: id fehlt");
            }
            foreach (var feld in (string[])["x", "y", "w", "t"])
            {
                if (o[feld] is not (long or double))
                {
                    throw new InvalidDataException("stadtdaten.katalog: " + feld + " ist keine Zahl (" + id + ")");
                }
            }
            var dreh = Get(o, "dreh", 0L);
            if (dreh is not long d || d < 0 || d > 3)
            {
                throw new InvalidDataException("stadtdaten.katalog: dreh ist nicht 0 bis 3 (" + id + ")");
            }
            liste.Add(new Draussen(Praefix + id, (float)Py.F(o["x"]), (float)Py.F(o["y"]), (float)Py.F(o["w"]), (float)Py.F(o["t"]), (int)d));
        }
        return liste;
    }
}
