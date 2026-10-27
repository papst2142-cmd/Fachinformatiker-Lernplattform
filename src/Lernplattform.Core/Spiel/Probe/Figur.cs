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
        Hose,
        Schuhe,
    }

    /// <summary>Phase 8 W5 (Kaufhaus): ein getragenes Kleidungsstueck, Slot (oberteil, hose, schuhe, kopf), Form und Farbe.</summary>
    public sealed record Stueck(string Slot, string Form, string Farbe);

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
    public static Netz Netz(GrafikBibliothek? grafik, PyObjekt? aussehen, float hoehe) => Netz(grafik, aussehen, hoehe, null);

    /// <summary>Hose: blaugrau unter der Huefte, Schuhe: grau ganz unten (nur wenn ein Stueck getragen wird).</summary>
    public static Bereich Kleidung(byte r, byte g, byte b, float hoeheAnteil)
    {
        if (b - r >= 30 && r < 140 && hoeheAnteil > 0.06f && hoeheAnteil < 0.36f)
        {
            return Bereich.Hose;
        }
        if (hoeheAnteil < 0.08f && Math.Abs(r - g) < 14 && Math.Abs(g - b) < 14 && r > 150)
        {
            return Bereich.Schuhe;
        }
        return Bereich.Sonst;
    }

    /// <summary>Die Figur mit getragener Kleidung (Kaufhaus): Oberteil, Hose und Schuhe umgefaerbt, Kopfbedeckung dazu.</summary>
    public static Netz Netz(GrafikBibliothek? grafik, PyObjekt? aussehen, float hoehe, IReadOnlyList<Stueck>? kleidung)
    {
        var a = Aussehen.Normalisieren(aussehen);
        if (grafik?.Modell(Modell) is not { } gm)
        {
            return ProbeWelt.Figur();
        }
        var oben = kleidung?.FirstOrDefault(k => k.Slot == "oberteil");
        var hose = kleidung?.FirstOrDefault(k => k.Slot == "hose");
        var schuhe = kleidung?.FirstOrDefault(k => k.Slot == "schuhe");
        var kopfStueck = kleidung?.FirstOrDefault(k => k.Slot == "kopf");
        var hemd = Probe.Netz.Farbe(oben?.Farbe ?? Oberteil[Py.Str(a["oberteil"])]);
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
            var bereich = Erkennen(r, g, b, (y - gm.Min.Y) / groesse);
            if (bereich == Bereich.Sonst && (hose is not null || schuhe is not null))
            {
                bereich = Kleidung(r, g, b, (y - gm.Min.Y) / groesse);
            }
            var (ziel, bezug) = bereich switch
            {
                Bereich.Hemd => (hemd, 212f),
                Bereich.Haut => (haut, 230f),
                Bereich.Haare when kopfStueck is not null && Bedeckt(kopfStueck.Form, (y - gm.Min.Y) / groesse) => (Probe.Netz.Farbe(kopfStueck.Farbe), 60f),
                Bereich.Haare => (haar, 60f),
                Bereich.Hose when hose is not null => (Probe.Netz.Farbe(hose.Farbe), 115f),
                Bereich.Schuhe when schuhe is not null => (Probe.Netz.Farbe(schuhe.Farbe), 215f),
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
            var teile = Gesicht(kopf.Min, kopf.Max, haut);
            if (kopfStueck is not null)
            {
                teile.AddRange(Kopfbedeckung(kopf.Min, kopf.Max, gm.Max.Y, kopfStueck));
            }
            if (oben is { Form: "kapuze" })
            {
                teile.AddRange(Kapuze(kopf.Min, kopf.Max, hemd));
            }
            foreach (var teil in teile)
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
    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 2): Kreis am Boden um die Spielfigur in der Stadt, in der Farbe "Kreis"
    /// des Aussehens (RING_COLORS, wie der Kreis im Buero der 2D-Ansicht). Ursprung in der Mitte, flach
    /// auf Hoehe 0; die Flaeche zeichnet ihn ohne Tiefenpruefung, damit ihn kein Haus verdeckt.
    /// </summary>
    public static Netz Ring(PyObjekt? aussehen, float radius, float breite)
    {
        var name = Aussehen.Normalisieren(aussehen)["kreis"] as string ?? "pink";
        var farbe = Probe.Netz.Farbe(SpielBilder.KreisFarben.GetValueOrDefault(name, "#F472B6"));
        const int Stuecke = 24;
        var teile = new List<Teil>();
        for (var i = 0; i < Stuecke; i++)
        {
            // Sehnen etwas ueber die Ecke hinaus, damit zwischen den Stuecken keine Luecke bleibt
            float a = i * 2 * MathF.PI / Stuecke, b = (i + 1) * 2 * MathF.PI / Stuecke, mitte = (a + b) / 2, halb = (b - a) / 2 * 1.08f;
            teile.Add(new Teil(TeilArt.Band, radius * MathF.Sin(mitte - halb), 0f, radius * MathF.Cos(mitte - halb),
                radius * MathF.Sin(mitte + halb), breite, radius * MathF.Cos(mitte + halb), farbe));
        }
        return Probe.Netz.Aus(teile);
    }

    /// <summary>Kappe (mit Schirm nach vorn), Muetze (mit Bommel) oder Hut (mit Krempe) auf dem Kopf, Masse nach dem Kopf.</summary>
    public static List<Teil> Kopfbedeckung(Vector3 min, Vector3 max, float oben, Stueck stueck)
    {
        var f = Probe.Netz.Farbe(stueck.Farbe);
        var dunkel = f * 0.75f;
        float b = max.X - min.X, t = max.Z - min.Z, h = max.Y - min.Y, rand = b * 0.08f;
        float x1 = min.X - rand, x2 = max.X + rand, z1 = min.Z - rand, z2 = max.Z + rand;
        var teile = new List<Teil>();
        switch (stueck.Form)
        {
            case "kappe":
                // die Kappe ist das umgefaerbte Haar oben (Bedeckt), dazu der Schirm nach vorn
                teile.Add(new Teil(TeilArt.Quader, min.X + (b * 0.2f), max.Y + (h * 0.12f), max.Z - (t * 0.1f), max.X - (b * 0.2f), max.Y + (h * 0.15f), max.Z + (t * 0.12f), dunkel));
                break;
            case "muetze":
                // Umschlag am Rand und Bommel oben
                teile.AddRange(Rund(x1, z1, x2, z2, max.Y - (h * 0.06f), max.Y + (h * 0.04f), dunkel));
                teile.Add(new Teil(TeilArt.Quader, ((min.X + max.X) / 2) - (b * 0.08f), oben - (h * 0.02f), ((min.Z + max.Z) / 2) - (b * 0.08f),
                    ((min.X + max.X) / 2) + (b * 0.08f), oben + (h * 0.12f), ((min.Z + max.Z) / 2) + (b * 0.08f), Probe.Netz.Farbe("#F8FAFC")));
                break;
            case "spitzhut":
            {
                // Phase 8 W6: Hexenhut (Halloween): breite Krempe, Kegel aus vier Stufen, schraeg nach hinten, Band
                teile.AddRange(Rund(x1 - (b * 0.2f), z1 - (t * 0.2f), x2 + (b * 0.2f), z2 + (t * 0.2f), max.Y - (h * 0.02f), max.Y + (h * 0.03f), f));
                teile.AddRange(Rund(x1, z1, x2, z2, max.Y + (h * 0.03f), max.Y + (h * 0.12f), Probe.Netz.Farbe("#F97316")));
                float cx = (min.X + max.X) / 2, cz = (min.Z + max.Z) / 2;
                for (var i = 0; i < 4; i++)
                {
                    var r = b * (0.42f - (i * 0.09f));
                    var y = max.Y + (h * (0.12f + (i * 0.16f)));
                    var dz = -t * 0.06f * i;
                    teile.Add(new Teil(TeilArt.Quader, cx - r, y, cz + dz - r, cx + r, y + (h * 0.17f), cz + dz + r, f));
                }
                break;
            }
            case "zipfelmuetze":
            {
                // Phase 8 W6: Weihnachtsmuetze: weisser Rand, roter Zipfel zur Seite geneigt, weisser Bommel
                var weiss = Probe.Netz.Farbe("#F8FAFC");
                teile.AddRange(Rund(x1, z1, x2, z2, max.Y - (h * 0.08f), max.Y + (h * 0.05f), weiss));
                float cx = (min.X + max.X) / 2, cz = (min.Z + max.Z) / 2;
                for (var i = 0; i < 4; i++)
                {
                    var r = b * (0.4f - (i * 0.085f));
                    var y = max.Y + (h * (0.05f + (i * 0.09f)));
                    var dx = b * 0.16f * i;
                    teile.Add(new Teil(TeilArt.Quader, cx + dx - r, y, cz - r, cx + dx + r, y + (h * 0.12f), cz + r, f));
                }
                var bx = cx + (b * 0.68f);
                teile.Add(new Teil(TeilArt.Quader, bx - (b * 0.13f), max.Y + (h * 0.18f), cz - (b * 0.13f), bx + (b * 0.13f), max.Y + (h * 0.42f), cz + (b * 0.13f), weiss));
                break;
            }
            case "partyhut":
            {
                // Phase 8 W6: Silvester, bunter Kegel mit Streifen und Bommel
                float cx = (min.X + max.X) / 2, cz = (min.Z + max.Z) / 2;
                var streifen = Probe.Netz.Farbe("#FDE047");
                for (var i = 0; i < 5; i++)
                {
                    var r = b * (0.3f - (i * 0.055f));
                    var y = max.Y + (h * (i * 0.12f)) - (h * 0.04f);
                    teile.Add(new Teil(TeilArt.Quader, cx - r, y, cz - r, cx + r, y + (h * 0.13f), cz + r, i % 2 == 0 ? f : streifen));
                }
                teile.Add(new Teil(TeilArt.Quader, cx - (b * 0.08f), max.Y + (h * 0.6f), cz - (b * 0.08f), cx + (b * 0.08f), max.Y + (h * 0.76f), cz + (b * 0.08f), Probe.Netz.Farbe("#EC4899")));
                break;
            }
            case "hasenohren":
            {
                // Phase 8 W6: Ostern, Haarreif mit zwei langen Ohren (aussen Farbe, innen rosa)
                var rosa = Probe.Netz.Farbe("#F9A8D4");
                float cz = (min.Z + max.Z) / 2;
                teile.Add(new Teil(TeilArt.Quader, x1, max.Y - (h * 0.02f), cz - (t * 0.06f), x2, max.Y + (h * 0.04f), cz + (t * 0.06f), f * 0.85f));
                foreach (var seite in (float[])[-1f, 1f])
                {
                    var x = ((min.X + max.X) / 2) + (seite * b * 0.22f);
                    teile.Add(new Teil(TeilArt.Quader, x - (b * 0.1f), max.Y, cz - (t * 0.07f), x + (b * 0.1f), max.Y + (h * 0.75f), cz + (t * 0.07f), f));
                    teile.Add(new Teil(TeilArt.Quader, x - (b * 0.05f), max.Y + (h * 0.08f), cz + (t * 0.07f), x + (b * 0.05f), max.Y + (h * 0.68f), cz + (t * 0.09f), rosa));
                }
                break;
            }
            default:
                // Hut: Krempe rundum und Band, der Kopf des Huts ist das umgefaerbte Haar
                teile.AddRange(Rund(x1 - (b * 0.14f), z1 - (t * 0.14f), x2 + (b * 0.14f), z2 + (t * 0.14f), max.Y - (h * 0.01f), max.Y + (h * 0.03f), f));
                teile.AddRange(Rund(x1, z1, x2, z2, max.Y + (h * 0.03f), max.Y + (h * 0.09f), dunkel));
                break;
        }
        return teile;
    }

    /// <summary>Kaufhaus (Weisung P8-8c, Hinweis Chat): runde Scheibe aus vier versetzten Quadern (von oben fast rund statt eckig).</summary>
    private static List<Teil> Rund(float x1, float z1, float x2, float z2, float y1, float y2, Vector3 farbe)
    {
        float cx = (x1 + x2) / 2, cz = (z1 + z2) / 2, rx = (x2 - x1) / 2, rz = (z2 - z1) / 2;
        Teil Kasten(float fx, float fz) => new(TeilArt.Quader, cx - (rx * fx), y1, cz - (rz * fz), cx + (rx * fx), y2, cz + (rz * fz), farbe);
        return [Kasten(1f, 0.4f), Kasten(0.4f, 1f), Kasten(0.86f, 0.7f), Kasten(0.7f, 0.86f)];
    }

    /// <summary>Welche Haar-Dreiecke die Kopfbedeckung bedeckt (Hoehe relativ zur Figur).</summary>
    public static bool Bedeckt(string form, float hoeheAnteil) => form switch
    {
        "kappe" => hoeheAnteil > 0.93f,
        "muetze" => hoeheAnteil > 0.86f,
        "spitzhut" or "zipfelmuetze" => hoeheAnteil > 0.88f,
        "partyhut" or "hasenohren" => false,
        _ => hoeheAnteil > 0.89f,
    };

    /// <summary>Kapuze des Kapuzenpullis: Wulst hinten am Hals in der Farbe des Oberteils.</summary>
    public static List<Teil> Kapuze(Vector3 min, Vector3 max, Vector3 farbe)
    {
        float b = max.X - min.X, h = max.Y - min.Y;
        return
        [
            new Teil(TeilArt.Quader, min.X + (b * 0.05f), min.Y - (h * 0.25f), min.Z - (b * 0.18f), max.X - (b * 0.05f), min.Y + (h * 0.25f), min.Z + (b * 0.12f), farbe * 0.9f),
        ];
    }
}
