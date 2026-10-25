using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W6: die 2D-Bilder der Spielansichten als Zeichenbefehle wie in Python 0.63.2
/// (fisi_game.avatar_shapes, badge_shapes mit den 24 _pic_*, _rect/_oval/_line/_poly, badge_color,
/// map_role_color, TIER_COLORS, tier_text_color). Die Formen sind Python-dicts ("k" = rect, oval,
/// line, poly), die Oberflaeche zeichnet sie wie mobile/src/spiel.py. Die Farben kommen aus der
/// aktuellen Farbwelt (Python: fisi_theme.C), Vergleichswerte G50.
/// </summary>
public static class SpielBilder
{
    /// <summary>BADGE_SIZE: Kantenlaenge eines Abzeichens in Zeicheneinheiten.</summary>
    public const double AbzeichenGroesse = 2.4;

    public const string Metall = "#8E8AA3";

    public const string ExtraFarbe = "#1B1031";

    /// <summary>TIER_COLORS</summary>
    public static readonly IReadOnlyDictionary<string, string> StufenFarben = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bronze"] = "#D08A4E", ["silber"] = "#D5DCE8", ["gold"] = "#FACC15",
    };

    /// <summary>TIER_NAMES</summary>
    public static readonly IReadOnlyDictionary<string, string> StufenNamen = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bronze"] = "Bronze", ["silber"] = "Silber", ["gold"] = "Gold",
    };

    private static readonly Dictionary<string, string> Haut = new(StringComparer.Ordinal)
    {
        ["hell"] = "#F5D0B5", ["mittel"] = "#D9A47E", ["oliv"] = "#B98A5E", ["dunkel"] = "#7A5238",
    };

    private static readonly Dictionary<string, string> Haar = new(StringComparer.Ordinal)
    {
        ["schwarz"] = "#2B2233", ["braun"] = "#6B4226", ["blond"] = "#E8C872", ["rot"] = "#C2562E", ["grau"] = "#B8B4C4",
    };

    private static readonly Dictionary<string, string> Oberteil = new(StringComparer.Ordinal)
    {
        ["cyan"] = "#22D3EE", ["pink"] = "#F472B6", ["violett"] = "#A78BFA", ["gruen"] = "#34D399", ["orange"] = "#FB923C",
    };

    /// <summary>RING_COLORS: Kreis um die Spielfigur.</summary>
    public static readonly IReadOnlyDictionary<string, string> KreisFarben = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pink"] = "#F472B6", ["cyan"] = "#22D3EE", ["gruen"] = "#34D399", ["violett"] = "#A78BFA", ["orange"] = "#FB923C", ["gelb"] = "#FBBF24",
    };

    /// <summary>APPEARANCE_LABELS: Teil und Beschriftung im Figuren-Editor.</summary>
    public static readonly IReadOnlyList<(string Teil, string Name)> AussehenNamen =
    [
        ("haut", "Hautton"), ("frisur", "Frisur"), ("haarfarbe", "Haarfarbe"), ("oberteil", "Oberteil"),
        ("extra", "Accessoire"), ("kreis", "Kreis im Büro"),
    ];

    /// <summary>
    /// avatar_shapes: der Avatar als einfache Formen auf einer 100 x 100 Flaeche,
    /// Liste von (art, (x1, y1, x2, y2), farbe) mit art "oval", "rect" (abgerundet) oder "line".
    /// </summary>
    public static List<(string Art, (long X1, long Y1, long X2, long Y2) Ecken, string Farbe)> Avatar(PyObjekt? aussehen)
    {
        var look = Aussehen.Normalisieren(aussehen);
        string Wert(string teil) => (string)look[teil]!;
        var haut = Haut[Wert("haut")];
        var haar = Haar[Wert("haarfarbe")];
        var hemd = Oberteil[Wert("oberteil")];
        var frisur = Wert("frisur");
        var s = new List<(string, (long, long, long, long), string)>();
        if (frisur == "lang")
        {
            s.Add(("rect", (26, 24, 74, 72), haar));
        }
        else if (frisur == "zopf")
        {
            s.Add(("oval", (64, 34, 84, 62), haar));
        }
        s.Add(("rect", (18, 70, 82, 104), hemd));
        s.Add(("rect", (43, 58, 57, 74), haut));
        s.Add(("oval", (30, 20, 70, 64), haut));
        if (frisur is "kurz" or "lang" or "zopf")
        {
            s.Add(("oval", (28, 16, 72, 40), haar));
        }
        else if (frisur == "locken")
        {
            foreach (var x in (long[])[28, 38, 48, 58])
            {
                s.Add(("oval", (x, 12, x + 16, 30), haar));
            }
            s.Add(("oval", (24, 22, 38, 38), haar));
            s.Add(("oval", (62, 22, 76, 38), haar));
        }
        s.Add(("oval", (40, 40, 45, 45), ExtraFarbe));
        s.Add(("oval", (55, 40, 60, 45), ExtraFarbe));
        s.Add(("line", (45, 53, 55, 53), ExtraFarbe));
        switch (Wert("extra"))
        {
            case "brille":
                s.Add(("line", (36, 42, 64, 42), ExtraFarbe));
                s.Add(("oval", (36, 37, 48, 48), ExtraFarbe));
                s.Add(("oval", (52, 37, 64, 48), ExtraFarbe));
                s.Add(("oval", (38, 39, 46, 46), haut));
                s.Add(("oval", (54, 39, 62, 46), haut));
                s.Add(("oval", (40, 40, 45, 45), ExtraFarbe));
                s.Add(("oval", (55, 40, 60, 45), ExtraFarbe));
                break;
            case "headset":
                s.Add(("line", (28, 38, 32, 16), ExtraFarbe));
                s.Add(("line", (32, 16, 68, 16), ExtraFarbe));
                s.Add(("line", (68, 16, 72, 38), ExtraFarbe));
                s.Add(("rect", (24, 36, 32, 50), ExtraFarbe));
                s.Add(("rect", (68, 36, 76, 50), ExtraFarbe));
                s.Add(("line", (28, 50, 42, 57), ExtraFarbe));
                break;
            case "kappe":
                s.Add(("rect", (28, 14, 72, 30), hemd));
                s.Add(("rect", (50, 26, 82, 32), hemd));
                break;
        }
        return s;
    }

    /// <summary>tier_text_color: Stufenfarbe als Schrift, im Hellmodus abgedunkelt; ohne Stufe fallback bzw. "muted".</summary>
    public static string StufenSchrift(Palette c, string? stufe, string? ersatz = null)
    {
        if (stufe is null || !StufenFarben.TryGetValue(stufe, out var farbe))
        {
            return ersatz ?? c.Farben["muted"];
        }
        return HellSchrift(c, farbe);
    }

    /// <summary>light_text: eine feste Farbe als Schrift, im Hellmodus auf den hellen Flaechen 4,6:1.</summary>
    public static string HellSchrift(Palette c, string farbe) =>
        !c.Hell ? farbe : Farbwelt.LesbarAuf(farbe, [.. Farbwelt.Flaechen.Select(k => c.Farben[k])], Farbwelt.MindestKontrast);

    /// <summary>map_role_color</summary>
    public static string RollenFarbe(Palette c, string? rolle) => rolle switch
    {
        "bitweiche" => c.Farben["cyan"],
        "kunde" => c.Farben["blue"],
        "firma" => c.Farben["green"],
        "zuhause" => c.Farben["pink"],
        "filiale" => c.Farben["purple"],
        _ => "#8B93A1",
    };

    /// <summary>badge_color: Rolle der Weltkarte oder Farbname der Farbwelt.</summary>
    public static string AbzeichenFarbe(Palette c, string? name) =>
        name is "bitweiche" or "kunde" or "firma" or "zuhause" or "filiale"
            ? RollenFarbe(c, name)
            : name is not null && c.Farben.TryGetValue(name, out var farbe) ? farbe : c.Farben["accent"];

    /// <summary>BADGE_PICTURES: die Namen der Bilder.</summary>
    public static readonly IReadOnlyList<string> Bilder =
    [
        "haus", "stern", "ticket", "blitz", "haken", "schild", "schluessel", "gleise", "personen", "urkunde", "formular",
        "kran", "diagramm", "pfeil", "pokal", "muenzen", "anker", "herz", "hut", "sprechblase", "magnet", "schraube",
        "buch", "server",
    ];

    /// <summary>
    /// badge_shapes: Zeichenbefehle fuer ein Abzeichen (runder Orden) links oben bei (ox, oy).
    /// stufe null = noch nicht erreicht (dunkel, mit Schloss).
    /// </summary>
    public static List<PyObjekt> Abzeichen(Palette palette, string? bild, string? farbe, string? stufe,
        double ox = 0.0, double oy = 0.0, double groesse = AbzeichenGroesse)
    {
        var c = palette.Farben;
        var k = groesse / AbzeichenGroesse;
        double cx = ox + groesse / 2.0, cy = oy + groesse / 2.0;
        var radius = 1.0 * k;
        var gesperrt = stufe is null;
        var ring = gesperrt ? c["border_hi"] : StufenFarben[stufe!];
        var dunkel = gesperrt ? c["card_alt"] : Mix(ring, "#000000", 0.55);
        var s = new List<PyObjekt>
        {
            Oval(cx - radius - 0.12 * k, cy - radius - 0.12 * k, 2 * (radius + 0.12 * k), 2 * (radius + 0.12 * k), dunkel),
            Oval(cx - radius, cy - radius, 2 * radius, 2 * radius, Mix(c["bg"], "#000000", 0.2), ring, 0.13 * k),
        };
        var ton = gesperrt ? Mix(c["muted"], c["bg"], 0.45) : AbzeichenFarbe(palette, farbe);
        s.AddRange(Bild(c, bild, cx, cy + (!gesperrt ? 0.05 : -0.08) * k, 0.72 * k, ton, gesperrt));
        if (gesperrt)
        {
            var schloss = c["muted"];
            s.Add(Oval(cx - 0.14 * k, cy + 0.34 * k, 0.28 * k, 0.3 * k, "", schloss, 0.06 * k));
            s.Add(Rect(cx - 0.2 * k, cy + 0.48 * k, 0.4 * k, 0.3 * k, schloss, r: 0.05 * k));
        }
        return s;
    }

    private static string Mix(string a, string b, double t) => Farbwelt.Mischen(a, b, t);

    private static List<PyObjekt> Bild(IReadOnlyDictionary<string, string> c, string? bild, double cx, double cy, double k, string color, bool dim)
    {
        switch (bild)
        {
            case "haus":
            {
                var wand = !dim ? Mix(c["card_alt"], color, 0.12) : color;
                var s = new List<PyObjekt>
                {
                    Rect(cx - 0.62 * k, cy - 0.5 * k, 1.24 * k, 0.55 * k, color, r: 0.08 * k),
                    Rect(cx - 0.62 * k, cy + 0.05 * k, 1.24 * k, 0.42 * k, wand),
                };
                for (var n = 0; n < 3; n++)
                {
                    s.Add(Rect(cx - 0.47 * k + n * 0.36 * k, cy + 0.14 * k, 0.2 * k, 0.2 * k,
                        dim ? c["card"] : Mix(c["yellow"], "#FFFFFF", 0.45)));
                }
                return s;
            }
            case "ticket":
            {
                var s = new List<PyObjekt> { Rect(cx - 0.5 * k, cy - 0.6 * k, 1.0 * k, 1.2 * k, color, r: 0.08 * k) };
                for (var n = 0; n < 4; n++)
                {
                    s.Add(Rect(cx - 0.32 * k, cy - 0.38 * k + n * 0.24 * k, (n < 3 ? 0.64 : 0.36) * k, 0.08 * k, c["card"]));
                }
                return s;
            }
            case "blitz":
                return [Poly([(cx + 0.12 * k, cy - 0.7 * k), (cx - 0.42 * k, cy + 0.1 * k), (cx - 0.02 * k, cy + 0.1 * k),
                    (cx - 0.16 * k, cy + 0.7 * k), (cx + 0.42 * k, cy - 0.12 * k), (cx + 0.02 * k, cy - 0.12 * k)], color)];
            case "haken":
                return [Line(cx - 0.5 * k, cy + 0.02 * k, cx - 0.14 * k, cy + 0.4 * k, color, 0.2 * k),
                    Line(cx - 0.14 * k, cy + 0.4 * k, cx + 0.55 * k, cy - 0.42 * k, color, 0.2 * k)];
            case "schild":
                return [Poly([(cx - 0.55 * k, cy - 0.55 * k), (cx + 0.55 * k, cy - 0.55 * k), (cx + 0.5 * k, cy + 0.1 * k),
                        (cx, cy + 0.68 * k), (cx - 0.5 * k, cy + 0.1 * k)], color),
                    Poly([(cx - 0.32 * k, cy - 0.36 * k), (cx, cy - 0.36 * k), (cx, cy + 0.42 * k), (cx - 0.3 * k, cy + 0.02 * k)],
                        !dim ? Mix(color, "#FFFFFF", 0.3) : color)];
            case "schluessel":
                return [Oval(cx - 0.62 * k, cy - 0.34 * k, 0.56 * k, 0.56 * k, "", color, 0.16 * k),
                    Line(cx - 0.08 * k, cy - 0.06 * k, cx + 0.62 * k, cy - 0.06 * k, color, 0.16 * k),
                    Line(cx + 0.36 * k, cy - 0.06 * k, cx + 0.36 * k, cy + 0.26 * k, color, 0.14 * k),
                    Line(cx + 0.56 * k, cy - 0.06 * k, cx + 0.56 * k, cy + 0.2 * k, color, 0.14 * k)];
            case "gleise":
            {
                var s = new List<PyObjekt>();
                for (var n = 0; n < 5; n++)
                {
                    var y = cy - 0.56 * k + n * 0.28 * k;
                    s.Add(Rect(cx - 0.55 * k, y, 1.1 * k, 0.1 * k, Mix(color, c["card"], 0.55)));
                }
                foreach (var dx in (double[])[-0.28, 0.28])
                {
                    s.Add(Line(cx + dx * k, cy - 0.7 * k, cx + dx * k, cy + 0.7 * k, color, 0.12 * k));
                }
                return s;
            }
            case "personen":
            {
                var s = new List<PyObjekt>();
                foreach (var dx in (double[])[-0.42, 0.42, 0.0])
                {
                    var ton = dx == 0 ? color : Mix(color, c["card"], 0.3);
                    s.Add(Oval(cx + (dx - 0.17) * k, cy - 0.45 * k, 0.34 * k, 0.34 * k, ton));
                    s.Add(Rect(cx + (dx - 0.24) * k, cy - 0.05 * k, 0.48 * k, 0.5 * k, ton, r: 0.2 * k));
                }
                return s;
            }
            case "urkunde":
            {
                var papier = !dim ? c["text"] : color;
                var s = new List<PyObjekt> { Rect(cx - 0.48 * k, cy - 0.62 * k, 0.96 * k, 1.2 * k, papier, r: 0.06 * k) };
                for (var n = 0; n < 3; n++)
                {
                    s.Add(Rect(cx - 0.3 * k, cy - 0.42 * k + n * 0.22 * k, 0.6 * k, 0.07 * k, !dim ? c["muted"] : c["card"]));
                }
                s.Add(Oval(cx + 0.06 * k, cy + 0.12 * k, 0.4 * k, 0.4 * k, !dim ? color : c["card"]));
                return s;
            }
            case "formular":
            {
                var papier = !dim ? c["text"] : color;
                var s = new List<PyObjekt> { Rect(cx - 0.48 * k, cy - 0.62 * k, 0.96 * k, 1.2 * k, papier, r: 0.06 * k) };
                for (var n = 0; n < 3; n++)
                {
                    var y = cy - 0.4 * k + n * 0.34 * k;
                    s.Add(Rect(cx - 0.32 * k, y, 0.16 * k, 0.16 * k, !dim ? color : c["card"]));
                    s.Add(Rect(cx - 0.06 * k, y + 0.04 * k, 0.38 * k, 0.07 * k, !dim ? c["muted"] : c["card"]));
                }
                return s;
            }
            case "kran":
                return [Line(cx - 0.3 * k, cy + 0.66 * k, cx - 0.3 * k, cy - 0.62 * k, color, 0.14 * k),
                    Line(cx - 0.6 * k, cy - 0.55 * k, cx + 0.62 * k, cy - 0.55 * k, color, 0.12 * k),
                    Line(cx + 0.42 * k, cy - 0.55 * k, cx + 0.42 * k, cy + 0.02 * k, Mix(color, c["card"], 0.3), 0.05 * k),
                    Rect(cx + 0.24 * k, cy + 0.02 * k, 0.36 * k, 0.3 * k, color, r: 0.04 * k),
                    Rect(cx - 0.62 * k, cy + 0.56 * k, 0.64 * k, 0.14 * k, Mix(color, c["card"], 0.3))];
            case "diagramm":
            {
                var s = new List<PyObjekt>();
                var hoehen = (double[])[0.45, 0.75, 1.1];
                for (var n = 0; n < 3; n++)
                {
                    s.Add(Rect(cx - 0.55 * k + n * 0.4 * k, cy + 0.6 * k - hoehen[n] * k, 0.28 * k, hoehen[n] * k,
                        Mix(color, c["card"], 0.35 - n * 0.15), r: 0.04 * k));
                }
                return s;
            }
            case "pfeil":
                return [Line(cx - 0.6 * k, cy + 0.42 * k, cx - 0.18 * k, cy, color, 0.16 * k),
                    Line(cx - 0.18 * k, cy, cx + 0.08 * k, cy + 0.22 * k, color, 0.16 * k),
                    Line(cx + 0.08 * k, cy + 0.22 * k, cx + 0.46 * k, cy - 0.28 * k, color, 0.16 * k),
                    Poly([(cx + 0.64 * k, cy - 0.56 * k), (cx + 0.66 * k, cy - 0.06 * k), (cx + 0.22 * k, cy - 0.38 * k)], color)];
            case "pokal":
                return [Oval(cx - 0.66 * k, cy - 0.5 * k, 0.4 * k, 0.44 * k, "", color, 0.1 * k),
                    Oval(cx + 0.26 * k, cy - 0.5 * k, 0.4 * k, 0.44 * k, "", color, 0.1 * k),
                    Poly([(cx - 0.45 * k, cy - 0.62 * k), (cx + 0.45 * k, cy - 0.62 * k), (cx + 0.3 * k, cy + 0.02 * k),
                        (cx - 0.3 * k, cy + 0.02 * k)], color),
                    Rect(cx - 0.08 * k, cy, 0.16 * k, 0.36 * k, color),
                    Rect(cx - 0.36 * k, cy + 0.36 * k, 0.72 * k, 0.2 * k, color, r: 0.04 * k)];
            case "muenzen":
            {
                var s = new List<PyObjekt>();
                for (var n = 0; n < 4; n++)
                {
                    var y = cy + 0.34 * k - n * 0.26 * k;
                    s.Add(Oval(cx - 0.52 * k, y - 0.2 * k, 1.04 * k, 0.4 * k, color, Mix(color, "#000000", 0.45), 0.04 * k));
                }
                return s;
            }
            case "anker":
                return [Oval(cx - 0.14 * k, cy - 0.72 * k, 0.28 * k, 0.28 * k, "", color, 0.08 * k),
                    Line(cx, cy - 0.44 * k, cx, cy + 0.6 * k, color, 0.14 * k),
                    Line(cx - 0.3 * k, cy - 0.26 * k, cx + 0.3 * k, cy - 0.26 * k, color, 0.12 * k),
                    Line(cx, cy + 0.6 * k, cx - 0.52 * k, cy + 0.24 * k, color, 0.12 * k),
                    Line(cx, cy + 0.6 * k, cx + 0.52 * k, cy + 0.24 * k, color, 0.12 * k)];
            case "herz":
                return [Oval(cx - 0.6 * k, cy - 0.5 * k, 0.66 * k, 0.62 * k, color),
                    Oval(cx - 0.06 * k, cy - 0.5 * k, 0.66 * k, 0.62 * k, color),
                    Poly([(cx - 0.56 * k, cy - 0.04 * k), (cx + 0.56 * k, cy - 0.04 * k), (cx, cy + 0.62 * k)], color)];
            case "hut":
                return [Poly([(cx - 0.72 * k, cy - 0.2 * k), (cx, cy - 0.54 * k), (cx + 0.72 * k, cy - 0.2 * k), (cx, cy + 0.14 * k)], color),
                    Poly([(cx - 0.4 * k, cy - 0.02 * k), (cx + 0.4 * k, cy - 0.02 * k), (cx + 0.4 * k, cy + 0.32 * k),
                        (cx - 0.4 * k, cy + 0.32 * k)], Mix(color, c["card"], 0.3)),
                    Line(cx + 0.6 * k, cy - 0.22 * k, cx + 0.6 * k, cy + 0.34 * k, color, 0.06 * k)];
            case "sprechblase":
            {
                var andere = Mix(color, c["card"], 0.35);
                return [Rect(cx - 0.68 * k, cy - 0.6 * k, 0.86 * k, 0.6 * k, andere, r: 0.2 * k),
                    Poly([(cx - 0.52 * k, cy - 0.04 * k), (cx - 0.3 * k, cy - 0.04 * k), (cx - 0.56 * k, cy + 0.2 * k)], andere),
                    Rect(cx - 0.18 * k, cy - 0.16 * k, 0.86 * k, 0.6 * k, color, r: 0.2 * k),
                    Poly([(cx + 0.3 * k, cy + 0.4 * k), (cx + 0.52 * k, cy + 0.4 * k), (cx + 0.56 * k, cy + 0.64 * k)], color)];
            }
            case "magnet":
                return [Line(cx - 0.36 * k, cy - 0.5 * k, cx - 0.36 * k, cy + 0.2 * k, color, 0.26 * k),
                    Line(cx + 0.36 * k, cy - 0.5 * k, cx + 0.36 * k, cy + 0.2 * k, color, 0.26 * k),
                    Oval(cx - 0.49 * k, cy - 0.16 * k, 0.98 * k, 0.8 * k, "", color, 0.26 * k),
                    Rect(cx - 0.5 * k, cy - 0.66 * k, 0.28 * k, 0.2 * k, c["text"]),
                    Rect(cx + 0.22 * k, cy - 0.66 * k, 0.28 * k, 0.2 * k, c["text"])];
            case "schraube":
                return [Line(cx - 0.52 * k, cy + 0.52 * k, cx - 0.12 * k, cy + 0.12 * k, color, 0.3 * k),
                    Line(cx - 0.12 * k, cy + 0.12 * k, cx + 0.55 * k, cy - 0.55 * k, Metall, 0.1 * k)];
            case "server":
            {
                var rahmen = !dim ? c["text"] : color;
                var s = new List<PyObjekt> { Rect(cx - 0.46 * k, cy - 0.66 * k, 0.92 * k, 1.32 * k, rahmen, r: 0.06 * k) };
                for (var n = 0; n < 3; n++)
                {
                    var y = cy - 0.5 * k + n * 0.36 * k;
                    s.Add(Rect(cx - 0.34 * k, y, 0.68 * k, 0.26 * k, !dim ? color : c["card"], r: 0.03 * k));
                    s.Add(Oval(cx + 0.16 * k, y + 0.08 * k, 0.1 * k, 0.1 * k, !dim ? c["green"] : rahmen));
                }
                return s;
            }
            case "buch":
                return [Poly([(cx - 0.7 * k, cy - 0.45 * k), (cx - 0.04 * k, cy - 0.32 * k), (cx - 0.04 * k, cy + 0.56 * k),
                        (cx - 0.7 * k, cy + 0.42 * k)], color),
                    Poly([(cx + 0.04 * k, cy - 0.32 * k), (cx + 0.7 * k, cy - 0.45 * k), (cx + 0.7 * k, cy + 0.42 * k),
                        (cx + 0.04 * k, cy + 0.56 * k)], Mix(color, c["card"], 0.25))];
            default:
            {
                // "stern" und jedes unbekannte Bild (BADGE_PICTURES.get(picture, _pic_star))
                var punkte = new List<(double, double)>();
                for (var i = 0; i < 10; i++)
                {
                    var r = i % 2 == 0 ? 0.68 * k : 0.29 * k;
                    var winkel = Math.PI / 5 * i - Math.PI / 2;
                    punkte.Add((cx + r * Math.Cos(winkel), cy + 0.06 * k + r * Math.Sin(winkel)));
                }
                return [Poly(punkte, color)];
            }
        }
    }

    /// <summary>_rect</summary>
    public static PyObjekt Rect(double x, double y, double w, double h, string fill, string line = "", double lw = 0.0, double r = 0.0) =>
        new() { { "k", "rect" }, { "x", x }, { "y", y }, { "w", w }, { "h", h }, { "fill", fill }, { "line", line }, { "lw", lw }, { "r", r } };

    /// <summary>_oval</summary>
    public static PyObjekt Oval(double x, double y, double w, double h, string fill, string line = "", double lw = 0.0) =>
        new() { { "k", "oval" }, { "x", x }, { "y", y }, { "w", w }, { "h", h }, { "fill", fill }, { "line", line }, { "lw", lw } };

    /// <summary>_line</summary>
    public static PyObjekt Line(double x1, double y1, double x2, double y2, string color, double lw = 0.06) =>
        new() { { "k", "line" }, { "pts", new List<object?> { x1, y1, x2, y2 } }, { "color", color }, { "lw", lw } };

    /// <summary>_poly</summary>
    public static PyObjekt Poly(IEnumerable<(double X, double Y)> punkte, string fill, string line = "", double lw = 0.0)
    {
        var flach = new List<object?>();
        foreach (var (x, y) in punkte)
        {
            flach.Add(x);
            flach.Add(y);
        }
        return new() { { "k", "poly" }, { "pts", flach }, { "fill", fill }, { "line", line }, { "lw", lw } };
    }
}
