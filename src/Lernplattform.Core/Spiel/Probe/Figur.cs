using System.Numerics;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 7, W5 (Weisung P7-10, Frage 1a): die Figur aus Kenney Animated Characters 3
/// (characterMedium.fbx mit der Haut humanMaleA, im Wandler als "figur" gespeichert, feste Haltung).
/// Gefaerbt nach dem Aussehen wie in der Python-Linie (SKIN_COLORS, HAIR_COLORS, SHIRT_COLORS):
/// Hemd, Haut und Haare werden an ihrer Farbe im Modell erkannt und umgefaerbt, die Helligkeit des
/// Dreiecks bleibt (Schatten der Vorlage). Ursprung unten in der Mitte, Blick nach +z.
/// Ohne Grafik-Bibliothek die Kloetzchen-Figur aus W1.
/// </summary>
public static class Figur
{
    /// <summary>Name des Modells in modelle.bin.</summary>
    public const string Modell = "figur";

    /// <summary>Hoehe der Figur in der Stadt: 1,8 m im Massstab der Stadt (Nachtrag 1 zu P7-12, Wunsch 2; vorher 1,24 = 6,2 m).</summary>
    public const float StadtHoehe = 1.8f * StadtSzene.Meter;

    public static readonly IReadOnlyDictionary<string, string> Haut = new Dictionary<string, string>
    {
        ["hell"] = "#F5D0B5", ["mittel"] = "#D9A47E", ["oliv"] = "#B98A5E", ["dunkel"] = "#7A5238",
    };

    public static readonly IReadOnlyDictionary<string, string> Haare = new Dictionary<string, string>
    {
        ["schwarz"] = "#2B2233", ["braun"] = "#6B4226", ["blond"] = "#E8C872", ["rot"] = "#C2562E", ["grau"] = "#B8B4C4",
    };

    public static readonly IReadOnlyDictionary<string, string> Oberteil = new Dictionary<string, string>
    {
        ["cyan"] = "#22D3EE", ["pink"] = "#F472B6", ["violett"] = "#A78BFA", ["gruen"] = "#34D399", ["orange"] = "#FB923C",
    };

    /// <summary>Wozu ein Dreieck der Vorlage gehoert (nach Farbe und Hoehe im Modell).</summary>
    public enum Bereich
    {
        Sonst,
        Hemd,
        Haut,
        Haare,
    }

    /// <summary>
    /// Hemd: blaugrau (Blau deutlich ueber Rot, hell) zwischen Huefte und Hals; Haut: Rot deutlich
    /// ueber Blau und hell; Haare: dunkel ueber Kinnhoehe. Hoehen relativ zur Modellhoehe.
    /// </summary>
    public static Bereich Erkennen(byte r, byte g, byte b, float hoeheAnteil)
    {
        if (b - r >= 8 && r >= 150 && hoeheAnteil > 0.33f && hoeheAnteil < 0.75f)
        {
            return Bereich.Hemd;
        }
        if (r - b >= 35 && r >= 170 && g >= 120)
        {
            return Bereich.Haut;
        }
        if (r < 110 && g < 100 && b < 100 && hoeheAnteil > 0.82f)
        {
            return Bereich.Haare;
        }
        return Bereich.Sonst;
    }

    /// <summary>Die Figur als Netz, <paramref name="hoehe"/> hoch, gefaerbt nach <paramref name="aussehen"/> (null = Voreinstellung).</summary>
    public static Netz Netz(GrafikBibliothek? grafik, PyObjekt? aussehen, float hoehe)
    {
        var a = Aussehen.Normalisieren(aussehen);
        if (grafik?.Modell(Modell) is not { } gm)
        {
            return ProbeWelt.Figur();
        }
        var hemd = Probe.Netz.Farbe(Oberteil[Py.Str(a["oberteil"])]);
        var haut = Probe.Netz.Farbe(Haut[Py.Str(a["haut"])]);
        var haar = Probe.Netz.Farbe(Haare[Py.Str(a["haarfarbe"])]);
        var groesse = gm.Max.Y - gm.Min.Y;
        var s = hoehe / groesse;
        var mitte = (gm.Min + gm.Max) / 2;
        var lage = Matrix4x4.CreateTranslation(-mitte.X, -gm.Min.Y, -mitte.Z) * Matrix4x4.CreateScale(s);
        var farben = (byte[])gm.Farben.Clone();
        for (var d = 0; d < gm.Dreiecke; d++)
        {
            var y = (gm.Ecken[(d * 9) + 1] + gm.Ecken[(d * 9) + 4] + gm.Ecken[(d * 9) + 7]) / 3;
            byte r = farben[d * 3], g = farben[(d * 3) + 1], b = farben[(d * 3) + 2];
            var (ziel, bezug) = Erkennen(r, g, b, (y - gm.Min.Y) / groesse) switch
            {
                Bereich.Hemd => (hemd, 212f),
                Bereich.Haut => (haut, 230f),
                Bereich.Haare => (haar, 60f),
                _ => (Vector3.Zero, 0f),
            };
            if (bezug == 0)
            {
                continue;
            }
            // Helligkeit der Vorlage behalten (Schatten), Farbe aus dem Aussehen
            var hell = Math.Clamp((r + g + b) / 3f / bezug, 0.55f, 1.25f);
            var neu = Vector3.Clamp(ziel * hell, Vector3.Zero, Vector3.One) * 255f;
            farben[d * 3] = (byte)neu.X;
            farben[(d * 3) + 1] = (byte)neu.Y;
            farben[(d * 3) + 2] = (byte)neu.Z;
        }
        var netz = new Probe.Netz();
        netz.Hinzu(new GrafikModell(gm.Name, gm.Ecken, farben), lage);
        if (Kopf(gm) is { } kopf)
        {
            foreach (var teil in Gesicht(kopf.Min, kopf.Max, haut))
            {
                netz.Hinzu(teil with
                {
                    X1 = (teil.X1 - mitte.X) * s, Y1 = (teil.Y1 - gm.Min.Y) * s, Z1 = (teil.Z1 - mitte.Z) * s,
                    X2 = (teil.X2 - mitte.X) * s, Y2 = (teil.Y2 - gm.Min.Y) * s, Z2 = (teil.Z2 - mitte.Z) * s,
                });
            }
        }
        return netz;
    }

    /// <summary>Kasten der Haut am Kopf (Haut-Dreiecke oberhalb von 75 % der Hoehe), null ohne Kopf.</summary>
    public static (Vector3 Min, Vector3 Max)? Kopf(GrafikModell gm)
    {
        var groesse = gm.Max.Y - gm.Min.Y;
        Vector3? min = null, max = null;
        for (var d = 0; d < gm.Dreiecke; d++)
        {
            var y = (gm.Ecken[(d * 9) + 1] + gm.Ecken[(d * 9) + 4] + gm.Ecken[(d * 9) + 7]) / 3;
            if (Erkennen(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2], (y - gm.Min.Y) / groesse) != Bereich.Haut
                || (y - gm.Min.Y) / groesse < 0.75f)
            {
                continue;
            }
            for (var e = 0; e < 3; e++)
            {
                var p = new Vector3(gm.Ecken[(d * 9) + (e * 3)], gm.Ecken[(d * 9) + (e * 3) + 1], gm.Ecken[(d * 9) + (e * 3) + 2]);
                min = min is { } a ? Vector3.Min(a, p) : p;
                max = max is { } b ? Vector3.Max(b, p) : p;
            }
        }
        return min is { } mi && max is { } ma ? (mi, ma) : null;
    }

    /// <summary>
    /// Nachtrag 1 zu P7-12 (Wunsch 1, Nico): Gesicht vorn am Kopf (+z) im Massstab des Kopfs: zwei Augen
    /// (dunkel, mit hellem Glanzpunkt) und ein Mund im Hautton, dunkler und roetlicher. Die Vorlage hat ihr
    /// Gesicht nur in der Textur, die die Grafik-Bibliothek nicht mitnimmt.
    /// </summary>
    public static List<Teil> Gesicht(Vector3 min, Vector3 max, Vector3 haut)
    {
        var b = max.X - min.X;
        var h = max.Y - min.Y;
        var vorn = max.Z;
        var t = b * 0.03f;
        var mitteX = (min.X + max.X) / 2;
        var auge = Probe.Netz.Farbe("#2B2233");
        var glanz = Probe.Netz.Farbe("#F4F1FA");
        var mund = Vector3.Clamp((haut * 0.55f) + new Vector3(0.18f, 0.02f, 0.02f), Vector3.Zero, Vector3.One);
        var teile = new List<Teil>();
        var augeY = min.Y + (h * 0.55f);
        var augeB = b * 0.11f;
        var augeH = h * 0.16f;
        foreach (var seite in (float[])[-1f, 1f])
        {
            var x = mitteX + (seite * b * 0.2f);
            teile.Add(new Teil(TeilArt.Quader, x - (augeB / 2), augeY, vorn - t, x + (augeB / 2), augeY + augeH, vorn + t, auge));
            teile.Add(new Teil(TeilArt.Quader, x, augeY + (augeH * 0.6f), vorn + t, x + (augeB * 0.4f), augeY + (augeH * 0.95f), vorn + (t * 1.6f), glanz));
        }
        var mundY = min.Y + (h * 0.3f);
        teile.Add(new Teil(TeilArt.Quader, mitteX - (b * 0.14f), mundY, vorn - t, mitteX + (b * 0.14f), mundY + (h * 0.05f), vorn + t, mund));
        return teile;
    }

    /// <summary>Gruenes Ausrufezeichen ueber einer Person mit Auftrag (wie das "!" der 2D-Ansicht), Ursprung unten.</summary>
    public static Netz Ausrufezeichen(float groesse)
    {
        var gruen = Probe.Netz.Farbe("#34D399");
        var w = groesse * 0.16f;
        return Probe.Netz.Aus(
        [
            new Teil(TeilArt.Quader, -w, groesse * 0.35f, -w, w, groesse, w, gruen),
            new Teil(TeilArt.Quader, -w, 0, -w, w, groesse * 0.22f, w, gruen),
        ]);
    }
}
