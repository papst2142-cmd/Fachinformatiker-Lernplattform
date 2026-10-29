using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 8 W6 (Weisung P8-12, Teilplan P8-11, Nico 3a/4a/5a): Deko je <see cref="Festzeit"/>, nur Darstellung.
/// Die Muster stehen in <c>inhalte_csharp/spiel/festzeiten.json</c> (je Stadt-Id: Tueren, Plaetze, Innenraeume).
/// Alles wird am Ende der Szene angehaengt; ohne Festzeit entsteht genau dieselbe Szene wie vorher.
/// <list type="bullet">
/// <item>Stadt: je Tuer links und rechts vor der Fassade, dazu feste Plaetze. Ein Stueck, das auf Strasse,
/// Fussweg, Gleis oder Wasser stuende (<see cref="StadtSzene.BaumSperren"/>), entfaellt; jedes stehende Stueck
/// ist ein Hindernis fuer das freie Gehen der Figur. Konfetti (flach) und Feuerwerk (am Himmel) sind keins.</item>
/// <item>Innenraeume: Ecke, links und rechts vom Eingang. Gesucht wird ein freier Platz im Flur; genommen wird er
/// nur, wenn er keine Einrichtung, Person oder Tuer beruehrt und danach jede vorher erreichbare Stelle, jede
/// Person, jede Ware und der Ausgang erreichbar bleiben. Die Stuecke kommen als Hindernis in die Laufwege der
/// Darstellung (eine Kopie des Grundrisses, die Spielregeln sehen sie nie). Wohnung: nur der feste Platz am
/// Eingang, steht dort ein Moebel, entfaellt die Deko.</item>
/// </list>
/// Eigene einfache Formen (keine Lizenzfrage): Ei, Rakete, Konfetti, Blume, Feuerwerk (ruhige Funkenkugel).
/// </summary>
public static class FestDeko
{
    /// <summary>
    /// Phase 8 W11 (Sammelliste 161, P8-41b): die Kuerbisse der Stadt aus dem Graveyard Kit (gleiche Plaetze und
    /// Anzahl, festzeiten.json bleibt; innen bleiben die alten); sie stehen im eigenen Netz (<see cref="StadtSzene.PaketNetzBauen"/>).
    /// </summary>
    public static string Kuerbis(string name) => name switch
    {
        "hw-pumpkin_orange_jackolantern" => "gy-pumpkin-carved",
        "hw-pumpkin_yellow_small" => "gy-pumpkin-tall-carved",
        "hw-pumpkin_orange_small" => "gy-pumpkin",
        _ => name,
    };

    /// <summary>Stadt-Id der heutigen Stadt in festzeiten.json (spaetere Staedte bekommen einen eigenen Abschnitt).</summary>
    public const string Standard = "standard";

    /// <summary>Typ der Hindernisse in der Kopie des Grundrisses (Laufwege der Darstellung).</summary>
    public const string Hindernis = "festdeko";

    /// <summary>Die drei Plaetze im Innenraum.</summary>
    public static readonly IReadOnlyList<string> InnenPlaetze = ["ecke", "links", "rechts"];

    /// <summary>Die Arten der Eintraege.</summary>
    public static readonly IReadOnlyList<string> Arten = ["modell", "rakete", "konfetti", "eier", "beet", "feuerwerk", "kranz", "laterne"];

    private static readonly Vector3[] Pastell = [Netz.Farbe("#F9A8D4"), Netz.Farbe("#93C5FD"), Netz.Farbe("#FDE68A"), Netz.Farbe("#A7F3D0"), Netz.Farbe("#C4B5FD"), Netz.Farbe("#FDBA74")];
    private static readonly Vector3[] Bunt = [Netz.Farbe("#EF4444"), Netz.Farbe("#F59E0B"), Netz.Farbe("#22C55E"), Netz.Farbe("#3B82F6"), Netz.Farbe("#A855F7"), Netz.Farbe("#EC4899"), Netz.Farbe("#FDE047")];

    /// <summary>Abstand der Stuecke links und rechts von der Tuermitte (Stadt), damit sie neben dem Fussweg stehen.</summary>
    public const float TuerAbstand = 0.34f;

    /// <summary>Abstand der Stuecke vor der Fassade (Stadt).</summary>
    public const float FassadeAbstand = 0.13f;

    /// <summary>Ein gesetztes Stueck (fuer Tests und Signatur): Art, Modell, Mitte und Radius in der Ebene, Hindernis ja/nein.</summary>
    /// <summary>Ein gesetztes Stueck; <paramref name="Ort"/> = Ort der Tuer, an der es steht (null an festen Plaetzen und innen).</summary>
    public sealed record Gesetzt(string Art, string? Name, float X, float Z, float R, bool IstHindernis, string? Ort = null);

    // ---- Daten -------------------------------------------------------------

    /// <summary>Das Muster einer Festzeit fuer eine Stadt (null = keins).</summary>
    public static PyObjekt? Muster(PyObjekt? daten, string? fest, string stadt = Standard) =>
        fest is null || daten is null || Get(daten, "staedte") is not PyObjekt staedte || Get(staedte, stadt) is not PyObjekt s
            ? null
            : Get(s, fest) as PyObjekt;

    private static IEnumerable<PyObjekt> Eintraege(PyObjekt? muster, string feld) =>
        muster is null ? [] : ListeOderLeer(muster, feld).OfType<PyObjekt>();

    private static IEnumerable<PyObjekt> InnenEintraege(PyObjekt? muster, string platz) =>
        muster is not null && Get(muster, "innen") is PyObjekt innen ? ListeOderLeer(innen, platz).OfType<PyObjekt>() : [];

    /// <summary>Alle CC0-Modelle, die die Datei nennt (Pruefung: jedes Modell in modelle.bin wird gebraucht).</summary>
    public static List<string> Modelle(PyObjekt daten)
    {
        var namen = new List<string>();
        foreach (var (_, stadt) in Get(daten, "staedte") as PyObjekt ?? [])
        {
            foreach (var (_, muster) in stadt as PyObjekt ?? [])
            {
                var m = muster as PyObjekt;
                var alle = Eintraege(m, "tueren").Concat(Eintraege(m, "plaetze")).Concat(InnenPlaetze.SelectMany(p => InnenEintraege(m, p)));
                namen.AddRange(alle.Where(e => Py.Gleich(Get(e, "art"), "modell")).Select(e => Py.Str(e["name"])));
            }
        }
        return [.. namen.Distinct().Order(StringComparer.Ordinal)];
    }

    /// <summary>Wie viele Puppen im Flur des Kaufhauses die Festkleidung tragen.</summary>
    public static int Puppen(PyObjekt? daten) =>
        daten is not null && Get(daten, "innen") is PyObjekt innen && Get(innen, "puppen") is long n ? (int)n : 0;

    private static readonly Dictionary<string, string[]> Felder = new()
    {
        ["modell"] = ["name", "hoehe", "y", "blick"],
        ["rakete"] = ["hoehe", "farbe"],
        ["konfetti"] = ["r", "anzahl", "groesse", "y"],
        ["eier"] = ["r", "hoehe", "anzahl"],
        ["beet"] = ["r", "hoehe", "anzahl"],
        ["feuerwerk"] = ["y", "r", "farbe"],
        ["kranz"] = ["hoehe"],
        ["laterne"] = ["hoehe"],
    };

    /// <summary>Fehler in festzeiten.json (leer = alles gut); <paramref name="grafik"/>: Modelle muessen da sein.</summary>
    public static List<string> Pruefen(PyObjekt daten, GrafikBibliothek? grafik = null)
    {
        var fehler = new List<string>();
        // Zeitraeume zum Nachlesen: gleich Festzeit.cs
        var zeiten = Get(daten, "zeitraeume") as PyObjekt;
        var soll = new PyObjekt
        {
            { Festzeit.Ostern, new PyObjekt { { "von_ostern", (long)Festzeit.OsternVon }, { "bis_ostern", (long)Festzeit.OsternBis } } },
        };
        foreach (var z in Festzeit.Zeitraeume)
        {
            soll.Add(z.Fest, new PyObjekt { { "von", new List<object?> { (long)z.VonMonat, (long)z.VonTag } }, { "bis", new List<object?> { (long)z.BisMonat, (long)z.BisTag } } });
        }
        if (zeiten is null || PythonJson.Standard(zeiten) != PythonJson.Standard(soll))
        {
            fehler.Add("festzeiten: zeitraeume ungleich Festzeit.cs");
        }
        foreach (var (stadtId, stadt) in Get(daten, "staedte") as PyObjekt ?? [])
        {
            foreach (var (fest, muster) in stadt as PyObjekt ?? [])
            {
                var wo = stadtId + "/" + fest;
                if (!Festzeit.Alle.Contains(fest) || muster is not PyObjekt m)
                {
                    fehler.Add("festzeiten: unbekannte Festzeit " + wo);
                    continue;
                }
                foreach (var (feld, _) in m)
                {
                    if (feld is not ("tueren" or "plaetze" or "innen"))
                    {
                        fehler.Add("festzeiten: " + wo + ": unbekanntes Feld " + feld);
                    }
                }
                foreach (var e in Eintraege(m, "tueren"))
                {
                    EintragPruefen(e, wo + "/tueren", ["an", "dx", "dz"], grafik, fehler);
                    if (Get(e, "an") is not ("links" or "rechts" or "mitte" or "ueber"))
                    {
                        fehler.Add("festzeiten: " + wo + "/tueren: an ungueltig");
                    }
                }
                foreach (var e in Eintraege(m, "plaetze"))
                {
                    EintragPruefen(e, wo + "/plaetze", ["x", "z"], grafik, fehler);
                    if (!IstZahl(Get(e, "x")) || !IstZahl(Get(e, "z")))
                    {
                        fehler.Add("festzeiten: " + wo + "/plaetze: x oder z fehlt");
                    }
                }
                if (Get(m, "innen") is PyObjekt innen)
                {
                    foreach (var (platz, liste) in innen)
                    {
                        if (!InnenPlaetze.Contains(platz) || liste is not List<object?>)
                        {
                            fehler.Add("festzeiten: " + wo + "/innen: unbekannter Platz " + platz);
                            continue;
                        }
                        foreach (var e in InnenEintraege(m, platz))
                        {
                            EintragPruefen(e, wo + "/innen/" + platz, ["dx", "dz"], grafik, fehler);
                        }
                    }
                }
            }
        }
        if (Get(daten, "innen") is PyObjekt allgemein)
        {
            foreach (var (ort, plaetze) in Get(allgemein, "orte") as PyObjekt ?? [])
            {
                foreach (var (platz, punkt) in plaetze as PyObjekt ?? [])
                {
                    if (!InnenPlaetze.Contains(platz) || punkt is not List<object?> { Count: 2 } p || !p.All(IstZahl))
                    {
                        fehler.Add("festzeiten: innen/orte/" + ort + ": Platz ungueltig " + platz);
                    }
                }
            }
            if (ListeOderLeer(allgemein, "wohnung").Any(p => p is not string s || !InnenPlaetze.Contains(s)))
            {
                fehler.Add("festzeiten: innen/wohnung ungueltig");
            }
            if (Get(allgemein, "puppen") is not long { } n || n < 0)
            {
                fehler.Add("festzeiten: innen/puppen ungueltig");
            }
        }
        else
        {
            fehler.Add("festzeiten: innen fehlt");
        }
        return fehler;
    }

    private static void EintragPruefen(PyObjekt e, string wo, string[] lage, GrafikBibliothek? grafik, List<string> fehler)
    {
        if (Get(e, "art") is not string art || !Felder.TryGetValue(art, out var felder))
        {
            fehler.Add("festzeiten: " + wo + ": unbekannte Art " + Py.Str(Get(e, "art")));
            return;
        }
        foreach (var (feld, wert) in e)
        {
            if (feld != "art" && !felder.Contains(feld) && !lage.Contains(feld))
            {
                fehler.Add("festzeiten: " + wo + ": " + art + ": unbekanntes Feld " + feld);
            }
            else if (feld is not ("art" or "name" or "an" or "blick") && !IstZahl(wert))
            {
                fehler.Add("festzeiten: " + wo + ": " + art + ": " + feld + " ist keine Zahl");
            }
        }
        var pflicht = art switch
        {
            "modell" => ["name", "hoehe"],
            "rakete" or "kranz" or "laterne" => ["hoehe"],
            "konfetti" => ["r", "anzahl", "groesse"],
            "eier" or "beet" => ["r", "hoehe", "anzahl"],
            _ => new[] { "y", "r" },
        };
        foreach (var p in pflicht.Where(p => !Py.Hat(e, p)))
        {
            fehler.Add("festzeiten: " + wo + ": " + art + ": " + p + " fehlt");
        }
        if (art == "modell" && grafik is not null && Get(e, "name") is string name && grafik.Modell(name) is null)
        {
            fehler.Add("festzeiten: " + wo + ": Modell fehlt " + name);
        }
        if (Get(e, "blick") is { } blick && (blick is not List<object?> { Count: 2 } b || !b.All(IstZahl)))
        {
            fehler.Add("festzeiten: " + wo + ": blick ungueltig");
        }
    }

    // ---- Formen --------------------------------------------------------------

    private static bool IstZahl(object? wert) => wert is long or int or double;

    private static float Zahl(PyObjekt e, string feld, float vorgabe = 0f) => Py.Hat(e, feld) ? (float)Py.F(e[feld]) : vorgabe;

    private static Teil Q(float x1, float y1, float z1, float x2, float y2, float z2, Vector3 f) => new(TeilArt.Quader, x1, y1, z1, x2, y2, z2, f);

    /// <summary>Ein Modell, gleichmaessig auf <paramref name="hoehe"/> skaliert, unten mittig auf (x, y, z).</summary>
    public static void Stueck(List<Teil> teile, List<ModellStueck> stuecke, GrafikBibliothek? g, string name, float x, float z, float hoehe,
        Vector2 richtung, float y = 0f)
    {
        if (g?.Modell(name) is not { } m)
        {
            teile.Add(Q(x - (hoehe * 0.3f), y, z - (hoehe * 0.3f), x + (hoehe * 0.3f), y + hoehe, z + (hoehe * 0.3f), Netz.Farbe("#F97316")));
            return;
        }
        var s = hoehe / MathF.Max(m.Max.Y - m.Min.Y, 1e-4f);
        var anker = new Vector3((m.Min.X + m.Max.X) / 2, m.Min.Y, (m.Min.Z + m.Max.Z) / 2);
        stuecke.Add(new ModellStueck(name, StadtSzene.Lage(m, anker, new Vector3(s), new Vector3(x, y, z), richtung)));
    }

    /// <summary>Halbe Breite eines Modells in der Ebene bei dieser Hoehe (ohne Grafik wie der Ersatzquader).</summary>
    public static float ModellRadius(GrafikBibliothek? g, string name, float hoehe)
    {
        if (g?.Modell(name) is not { } m)
        {
            return hoehe * 0.3f;
        }
        var s = hoehe / MathF.Max(m.Max.Y - m.Min.Y, 1e-4f);
        return MathF.Max(m.Max.X - m.Min.X, m.Max.Z - m.Min.Z) * s / 2;
    }

    /// <summary>Osterei aus Scheiben (unten schmal, Mitte breit, oben spitz) mit Band in einer zweiten Farbe.</summary>
    public static void Ei(List<Teil> t, float x, float z, float h, Vector3 farbe, Vector3 band, float y = 0f)
    {
        var r = h * 0.36f;
        t.Add(Q(x - (r * 0.75f), y, z - (r * 0.75f), x + (r * 0.75f), y + (h * 0.18f), z + (r * 0.75f), farbe));
        t.Add(Q(x - r, y + (h * 0.12f), z - r, x + r, y + (h * 0.42f), z + r, farbe));
        t.Add(Q(x - (r * 1.02f), y + (h * 0.42f), z - (r * 1.02f), x + (r * 1.02f), y + (h * 0.52f), z + (r * 1.02f), band));
        t.Add(Q(x - (r * 0.9f), y + (h * 0.52f), z - (r * 0.9f), x + (r * 0.9f), y + (h * 0.75f), z + (r * 0.9f), farbe));
        t.Add(Q(x - (r * 0.55f), y + (h * 0.75f), z - (r * 0.55f), x + (r * 0.55f), y + h, z + (r * 0.55f), farbe));
    }

    /// <summary>Rakete in einer Flasche: Flasche, Stab, Koerper, weisse Spitze.</summary>
    public static void Rakete(List<Teil> t, float x, float z, float h, Vector3 farbe, float y = 0f)
    {
        var r = h * 0.07f;
        var flasche = Netz.Farbe("#166534");
        t.Add(Q(x - (h * 0.12f), y, z - (h * 0.12f), x + (h * 0.12f), y + (h * 0.3f), z + (h * 0.12f), flasche));
        t.Add(Q(x - (h * 0.05f), y + (h * 0.3f), z - (h * 0.05f), x + (h * 0.05f), y + (h * 0.4f), z + (h * 0.05f), flasche));
        t.Add(Q(x - (r * 0.25f), y + (h * 0.3f), z - (r * 0.25f), x + (r * 0.25f), y + (h * 0.62f), z + (r * 0.25f), Netz.Farbe("#A16207")));
        t.Add(Q(x - r, y + (h * 0.6f), z - r, x + r, y + (h * 0.9f), z + r, farbe));
        t.Add(Q(x - (r * 0.6f), y + (h * 0.9f), z - (r * 0.6f), x + (r * 0.6f), y + (h * 0.96f), z + (r * 0.6f), Netz.Farbe("#F8FAFC")));
        t.Add(Q(x - (r * 0.25f), y + (h * 0.96f), z - (r * 0.25f), x + (r * 0.25f), y + h, z + (r * 0.25f), Netz.Farbe("#F8FAFC")));
    }

    /// <summary>
    /// Tuerkranz (Weisung P8-12 Tempo, Teilplan Abschnitt 5): statt des Modells (2 454 Dreiecke je Tuer) ein Ring aus zehn
    /// gruenen Wuerfeln mit roter Schleife (rund 130 Dreiecke), flach an der Wand quer zu <paramref name="blick"/>; unten
    /// mittig auf (x, y, z), Durchmesser <paramref name="h"/>.
    /// </summary>
    public static void Kranz(List<Teil> t, float x, float z, float h, Vector2 blick, float y = 0f)
    {
        var quer = new Vector2(MathF.Abs(blick.Y), MathF.Abs(blick.X));
        var r = h * 0.38f;
        var dick = h * 0.13f;
        var tief = h * 0.08f;
        var mitte = y + (h / 2);
        Teil Wuerfel(float a, float b, float halbQuer, float halbHoch, Vector3 farbe)
        {
            var (px, pz) = (x + (quer.X * a), z + (quer.Y * a));
            var (hx, hz) = ((quer.X * halbQuer) + (MathF.Abs(blick.X) * tief / 2), (quer.Y * halbQuer) + (MathF.Abs(blick.Y) * tief / 2));
            return Q(px - hx, mitte + b - halbHoch, pz - hz, px + hx, mitte + b + halbHoch, pz + hz, farbe);
        }
        var gruen = Netz.Farbe("#166534");
        var hell = Netz.Farbe("#15803D");
        for (var i = 0; i < 10; i++)
        {
            var winkel = i * MathF.PI / 5;
            t.Add(Wuerfel(MathF.Cos(winkel) * r, MathF.Sin(winkel) * r, dick / 2, dick / 2, i % 2 == 0 ? gruen : hell));
        }
        var rot = Netz.Farbe("#DC2626");
        t.Add(Wuerfel(-dick * 0.6f, -r, dick * 0.45f, dick * 0.3f, rot));
        t.Add(Wuerfel(dick * 0.6f, -r, dick * 0.45f, dick * 0.3f, rot));
        t.Add(Wuerfel(0, -r - (dick * 0.5f), dick * 0.15f, dick * 0.45f, rot));
    }

    /// <summary>
    /// Laterne auf einem Pfosten (Weisung P8-12 Tempo, Teilplan Abschnitt 5): statt des Modells (1 451 Dreiecke je Tuer)
    /// sechs Quader (72 Dreiecke): Fuss, Pfosten, Gehaeuse mit warmem Licht, Dach; unten mittig auf (x, y, z).
    /// </summary>
    public static void Laterne(List<Teil> t, float x, float z, float h, float y = 0f)
    {
        var dunkel = Netz.Farbe("#1F2937");
        var w = h * 0.13f;
        t.Add(Q(x - w, y, z - w, x + w, y + (h * 0.06f), z + w, dunkel));
        t.Add(Q(x - (w * 0.25f), y + (h * 0.06f), z - (w * 0.25f), x + (w * 0.25f), y + (h * 0.62f), z + (w * 0.25f), dunkel));
        t.Add(Q(x - (w * 0.75f), y + (h * 0.62f), z - (w * 0.75f), x + (w * 0.75f), y + (h * 0.66f), z + (w * 0.75f), dunkel));
        t.Add(Q(x - (w * 0.6f), y + (h * 0.66f), z - (w * 0.6f), x + (w * 0.6f), y + (h * 0.88f), z + (w * 0.6f), Netz.Farbe("#FCD34D")));
        t.Add(Q(x - (w * 0.85f), y + (h * 0.88f), z - (w * 0.85f), x + (w * 0.85f), y + (h * 0.94f), z + (w * 0.85f), dunkel));
        t.Add(Q(x - (w * 0.35f), y + (h * 0.94f), z - (w * 0.35f), x + (w * 0.35f), y + h, z + (w * 0.35f), Netz.Farbe("#DC2626")));
    }

    /// <summary>
    /// Feuerwerk (Nico 3a): ruhige Funkenkugel am Himmel, 22 Strahlen aus kleinen Wuerfeln in alle Richtungen
    /// (Fibonacci-Kugel), aussen heller, in der Mitte ein weisser Blitz.
    /// </summary>
    public static void Feuerwerk(List<Teil> t, float x, float y, float z, float r, Vector3 farbe, int strahlen = 22)
    {
        var weiss = new Vector3(1, 1, 0.92f);
        var gold = MathF.PI * (3 - MathF.Sqrt(5));
        for (var i = 0; i < strahlen; i++)
        {
            var hy = 1 - (2 * (i + 0.5f) / strahlen);
            var ring = MathF.Sqrt(1 - (hy * hy));
            var richtung = new Vector3(MathF.Cos(gold * i) * ring, hy, MathF.Sin(gold * i) * ring);
            for (var k = 1; k <= 5; k++)
            {
                var a = k / 5f;
                var p = new Vector3(x, y, z) + (richtung * r * (0.25f + (0.75f * a)));
                var g = r * (0.055f - (0.006f * k));
                var f = Vector3.Lerp(farbe, weiss, a * 0.6f);
                t.Add(Q(p.X - g, p.Y - g, p.Z - g, p.X + g, p.Y + g, p.Z + g, f));
            }
        }
        var m = r * 0.09f;
        t.Add(Q(x - m, y - m, z - m, x + m, y + m, z + m, weiss));
    }

    /// <summary>Konfetti: kleine bunte Plaettchen am Boden (fester Zufall aus der Lage); <paramref name="boden"/>: nur dort, wo frei ist.</summary>
    public static void Konfetti(List<Teil> t, float x, float z, float r, int anzahl, float groesse, float y = 0f, Func<float, float, bool>? boden = null)
    {
        var zufall = new Random(((int)(x * 100) * 7919) + (int)(z * 100));
        for (var i = 0; i < anzahl; i++)
        {
            var w = (float)(zufall.NextDouble() * 2 * Math.PI);
            var d = r * MathF.Sqrt((float)zufall.NextDouble());
            float px = x + (d * MathF.Cos(w)), pz = z + (d * MathF.Sin(w));
            var f = Bunt[zufall.Next(Bunt.Length)];
            if (boden is null || boden(px, pz))
            {
                t.Add(Q(px - groesse, y + 0.004f, pz - (groesse * 0.6f), px + groesse, y + 0.008f + (groesse * 0.2f), pz + (groesse * 0.6f), f));
            }
        }
    }

    /// <summary>Fruehlingsblume: Stiel, vier Bluetenblaetter, Mitte gelb.</summary>
    public static void Blume(List<Teil> t, float x, float z, float h, Vector3 farbe, float y = 0f)
    {
        float s = h * 0.06f, b = h * 0.22f;
        t.Add(Q(x - s, y, z - s, x + s, y + (h * 0.8f), z + s, Netz.Farbe("#16A34A")));
        t.Add(Q(x - b, y + (h * 0.78f), z - (b * 0.35f), x + b, y + (h * 0.9f), z + (b * 0.35f), farbe));
        t.Add(Q(x - (b * 0.35f), y + (h * 0.78f), z - b, x + (b * 0.35f), y + (h * 0.9f), z + b, farbe));
        t.Add(Q(x - (b * 0.3f), y + (h * 0.84f), z - (b * 0.3f), x + (b * 0.3f), y + h, z + (b * 0.3f), Netz.Farbe("#FACC15")));
    }

    /// <summary>Blumenbeet: Blumen in einem Kreis (fester Zufall aus der Lage).</summary>
    public static void Beet(List<Teil> t, float x, float z, float r, float h, int anzahl)
    {
        var zufall = new Random(((int)(x * 100) * 31) + (int)(z * 100));
        for (var i = 0; i < anzahl; i++)
        {
            var w = (float)(zufall.NextDouble() * 2 * Math.PI);
            var d = r * MathF.Sqrt((float)zufall.NextDouble());
            Blume(t, x + (d * MathF.Cos(w)), z + (d * MathF.Sin(w)), h * (0.8f + (0.4f * (float)zufall.NextDouble())), Bunt[zufall.Next(Bunt.Length)]);
        }
    }

    /// <summary>Ostereier in einem Kreis (fester Zufall aus der Lage).</summary>
    public static void Eier(List<Teil> t, float x, float z, float r, float h, int anzahl)
    {
        var zufall = new Random(((int)(x * 100) * 131) + (int)(z * 100));
        for (var i = 0; i < anzahl; i++)
        {
            var w = (float)(zufall.NextDouble() * 2 * Math.PI);
            var d = r * MathF.Sqrt((float)zufall.NextDouble());
            Ei(t, x + (d * MathF.Cos(w)), z + (d * MathF.Sin(w)), h, Pastell[zufall.Next(Pastell.Length)], Pastell[zufall.Next(Pastell.Length)]);
        }
    }

    /// <summary>Radius eines Eintrags in der Ebene (0 = kein Hindernis: Konfetti liegt flach, Feuerwerk steht am Himmel).</summary>
    private static float Radius(PyObjekt e, GrafikBibliothek? g, float meter) => Py.Str(e["art"]) switch
    {
        "modell" => ModellRadius(g, Py.Str(e["name"]), Zahl(e, "hoehe") * meter),
        "rakete" => Zahl(e, "hoehe") * meter * 0.12f,
        "eier" => Zahl(e, "r") + (Zahl(e, "hoehe") * meter * 0.4f),
        "beet" => Zahl(e, "r") + (Zahl(e, "hoehe") * meter * 0.25f),
        "laterne" => Zahl(e, "hoehe") * meter * 0.13f,
        _ => 0f,
    };

    /// <summary>Einen Eintrag an (x, z) zeichnen; <paramref name="meter"/>: Faktor fuer Hoehen (Stadt 1, innen Innenraum.Meter).</summary>
    private static void Zeichnen(PyObjekt e, float x, float z, float meter, float y0, Vector2 blick, int farbVersatz,
        List<Teil> teile, List<ModellStueck> stuecke, GrafikBibliothek? g, Func<float, float, bool>? boden, bool stadt = false)
    {
        var farbe = Bunt[(((int)Zahl(e, "farbe") + farbVersatz) % Bunt.Length + Bunt.Length) % Bunt.Length];
        switch (Py.Str(e["art"]))
        {
            case "modell":
                var richtung = Get(e, "blick") is List<object?> b ? new Vector2((float)Py.F(b[0]), (float)Py.F(b[1])) : blick;
                var mname = stadt ? Kuerbis(Py.Str(e["name"])) : Py.Str(e["name"]);
                Stueck(teile, stuecke, g, mname, x, z, Zahl(e, "hoehe") * meter, richtung, y0 + (Zahl(e, "y") * meter));
                break;
            case "rakete":
                Rakete(teile, x, z, Zahl(e, "hoehe") * meter, farbe);
                break;
            case "konfetti":
                Konfetti(teile, x, z, Zahl(e, "r"), (int)Zahl(e, "anzahl"), Zahl(e, "groesse"), Zahl(e, "y") * meter, boden);
                break;
            case "eier":
                Eier(teile, x, z, Zahl(e, "r"), Zahl(e, "hoehe") * meter, (int)Zahl(e, "anzahl"));
                break;
            case "beet":
                Beet(teile, x, z, Zahl(e, "r"), Zahl(e, "hoehe") * meter, (int)Zahl(e, "anzahl"));
                break;
            case "feuerwerk":
                Feuerwerk(teile, x, Zahl(e, "y") * meter, z, Zahl(e, "r"), farbe);
                break;
            case "kranz":
                Kranz(teile, x, z, Zahl(e, "hoehe") * meter, blick, y0);
                break;
            case "laterne":
                Laterne(teile, x, z, Zahl(e, "hoehe") * meter, y0);
                break;
        }
    }

    // ---- Stadt ---------------------------------------------------------------

    /// <summary>
    /// Deko der Stadt: je Tuer (Muster "tueren") und an festen Plaetzen ("plaetze"). Kein stehendes Stueck beruehrt eine
    /// <paramref name="sperren"/>-Linie (Strasse, Fussweg, Gleis, Wasser) oder einen Stehplatz: die Gruppe links oder
    /// rechts der Tuer rueckt dafuer an der Fassade entlang nach aussen (hoechstens bis zur Hausecke), sonst entfaellt
    /// sie wie ein Platz, der nicht passt. Jedes stehende Stueck kommt als Quadrat in <paramref name="hindernisse"/>
    /// (freies Gehen). Rueckgabe: alle gesetzten Stuecke. Mit <paramref name="tueren"/> (P8-48) steht die Deko an der Wand
    /// der echten Tuer (Mitte und Aussenseite wie gezeichnet, auch fuer Orte mit verlegtem Zugang), sonst an der Seite des
    /// Kastens, die der Tuer am naechsten liegt.
    /// </summary>
    public static List<Gesetzt> Stadt(List<Teil> teile, List<ModellStueck> stuecke, GrafikBibliothek? g, PyObjekt? muster,
        IReadOnlyList<StadtOrt> orte, IReadOnlyList<(IReadOnlyList<Vector2> Linie, float Halb)> sperren, List<(double, double, double, double)> hindernisse,
        IReadOnlyList<StadtTuer>? tueren = null)
    {
        var gesetzt = new List<Gesetzt>();
        if (muster is null)
        {
            return gesetzt;
        }
        // frei vom Weg wie die Baeume (BaumPlaner): halbe Wegbreite plus Radius plus 0,02; ebenso vom Stehplatz vor der
        // Tuer (Radius 0,25, immer frei). Die Zellen der Wege bleiben so frei, ohne dass die Figur durch ein Stueck geht.
        bool Frei(float x, float z, float r) =>
            sperren.All(s => Kurve.Abstand(new Vector2(x, z), s.Linie) >= s.Halb + r + WegRand)
            && orte.All(o => Vector2.Distance(new Vector2(x, z), new Vector2(o.StehX, o.StehZ)) >= StehRand + r + WegRand);
        void Setzen(PyObjekt e, float x, float z, float y0, Vector2 blick, int farbVersatz, bool amBoden, string? ort = null)
        {
            var r = amBoden ? Radius(e, g, 1f) : 0f;
            Zeichnen(e, x, z, 1f, y0, blick, farbVersatz, teile, stuecke, g, null, stadt: true);
            gesetzt.Add(new Gesetzt(Py.Str(e["art"]), Get(e, "name") as string, x, z, r, r > 0, ort));
            if (r > 0)
            {
                hindernisse.Add((x - r, z - r, x + r, z + r));
            }
        }
        foreach (var o in orte)
        {
            // die Fassade mit der Tuer: die Seite des Kastens, der die Tuer am naechsten liegt; n zeigt nach draussen,
            // t an der Fassade entlang (links = -t); dx der Eintraege laeuft entlang t, dz nach draussen
            var seiten = new (float Abstand, Vector2 N, Vector2 T, Vector2 F, float Von, float Bis, float Tuer)[]
            {
                (MathF.Abs(o.TuerZ - o.KastenMax.Z), Vector2.UnitY, Vector2.UnitX, new(o.TuerX, o.KastenMax.Z), o.KastenMin.X, o.KastenMax.X, o.TuerX),
                (MathF.Abs(o.TuerZ - o.KastenMin.Z), -Vector2.UnitY, Vector2.UnitX, new(o.TuerX, o.KastenMin.Z), o.KastenMin.X, o.KastenMax.X, o.TuerX),
                (MathF.Abs(o.TuerX - o.KastenMax.X), Vector2.UnitX, Vector2.UnitY, new(o.KastenMax.X, o.TuerZ), o.KastenMin.Z, o.KastenMax.Z, o.TuerZ),
                (MathF.Abs(o.TuerX - o.KastenMin.X), -Vector2.UnitX, Vector2.UnitY, new(o.KastenMin.X, o.TuerZ), o.KastenMin.Z, o.KastenMax.Z, o.TuerZ),
            };
            var (_, n, t, f, von, bis, tuer) = seiten.MinBy(x => x.Abstand);
            if (tueren?.FirstOrDefault(d => d.Ort == o.Id) is { } echt)
            {
                // P8-48: die Wand der gezeichneten Tuer (Mitte auf der Wand, Aussen nach draussen)
                n = echt.Aussen;
                t = MathF.Abs(n.X) > MathF.Abs(n.Y) ? Vector2.UnitY : Vector2.UnitX;
                f = echt.Mitte;
                (von, bis) = t == Vector2.UnitX ? (o.KastenMin.X, o.KastenMax.X) : (o.KastenMin.Z, o.KastenMax.Z);
                tuer = Vector2.Dot(echt.Mitte, t);
            }
            Vector2 Ort(float entlang, float draussen) => f + (t * entlang) + (n * draussen);
            var versatz = (int)(o.TuerX * 10);
            var eintraege = Eintraege(muster, "tueren").ToList();
            foreach (var e in eintraege.Where(e => Get(e, "an") is "mitte" or "ueber"))
            {
                var ueber = Py.Gleich(e["an"], "ueber");
                var p = Ort(Zahl(e, "dx"), (ueber ? 0.03f : FassadeAbstand) + Zahl(e, "dz"));
                // ueber der Tuer haengt es an der Wand, in der Mitte liegt nur Flaches (Konfetti): beides kein Hindernis
                if (ueber || Radius(e, g, 1f) <= 0 || Frei(p.X, p.Y, Radius(e, g, 1f)))
                {
                    Setzen(e, p.X, p.Y, ueber ? StadtSzene.TuerHoehe + 0.02f : 0f, n, versatz, !ueber, o.Id);
                }
            }
            // links und rechts: die Gruppe ruht vor der Fassade; liegt dort ein Fussweg, rueckt sie an der Fassade entlang nach aussen
            foreach (var (seite, vorzeichen) in new[] { ("links", -1f), ("rechts", 1f) })
            {
                var gruppe = eintraege.Where(e => Py.Gleich(Get(e, "an"), seite)).ToList();
                if (gruppe.Count == 0)
                {
                    continue;
                }
                for (var schritt = 0; schritt <= 60; schritt++)
                {
                    var plaetze = gruppe.Select(e => (E: e, A: (vorzeichen * (TuerAbstand + (schritt * 0.06f))) + Zahl(e, "dx"),
                        P: Ort((vorzeichen * (TuerAbstand + (schritt * 0.06f))) + Zahl(e, "dx"), FassadeAbstand + Zahl(e, "dz")), R: Radius(e, g, 1f))).ToList();
                    if (plaetze.Any(p => tuer + p.A - p.R < von || tuer + p.A + p.R > bis))
                    {
                        break;
                    }
                    if (plaetze.All(p => p.R <= 0 || Frei(p.P.X, p.P.Y, p.R)))
                    {
                        foreach (var p in plaetze)
                        {
                            Setzen(p.E, p.P.X, p.P.Y, 0f, n, versatz, true, o.Id);
                        }
                        break;
                    }
                }
            }
        }
        foreach (var e in Eintraege(muster, "plaetze"))
        {
            var (x, z, r) = (Zahl(e, "x"), Zahl(e, "z"), Radius(e, g, 1f));
            if (r <= 0 || Frei(x, z, r))
            {
                Setzen(e, x, z, 0f, Vector2.UnitY, 0, true);
            }
        }
        return gesetzt;
    }

    /// <summary>Luecke zwischen einem Stueck und dem Rand eines Wegs (wie bei den Baeumen).</summary>
    public const float WegRand = 0.02f;

    /// <summary>Halbmesser des immer freien Stehplatzes vor einer Tuer (wie im Laufraster).</summary>
    public const float StehRand = 0.25f;

    // ---- Innenraeume -------------------------------------------------------

    /// <summary>Plan der Innen-Deko: gesetzte Plaetze (Name, Mitte) und die Kopie des Grundrisses mit den Hindernissen.</summary>
    public sealed record InnenPlan(PyObjekt Gebaeude, LaufOrt Lauf, List<(string Platz, float X, float Z)> Plaetze, List<Gesetzt> Gesetzt);

    /// <summary>
    /// Plaetze fuer Ecke, links und rechts vom Eingang suchen (Wohnung: nur die Plaetze aus innen.wohnung, ohne
    /// Suche). <paramref name="personen"/>: Daten der Personen (platz); <paramref name="ziele"/>: weitere Punkte mit
    /// Reichweite, die erreichbar bleiben muessen (Waren). Null ohne Muster oder ohne gesetzten Platz.
    /// <paramref name="gruende"/>: warum ein Platz nicht genommen wurde (Tests).
    /// </summary>
    public static InnenPlan? InnenPlanen(string ort, PyObjekt gebaeude, List<object?> personen, GrafikBibliothek? g, PyObjekt? daten,
        PyObjekt? muster, bool wohnung, IReadOnlyList<(double X, double Y, double Reichweite)> ziele, List<string>? gruende = null)
    {
        if (muster is null || Get(muster, "innen") is not PyObjekt || daten is null)
        {
            return null;
        }
        var allgemein = Get(daten, "innen") as PyObjekt;
        var feste = allgemein is not null && Get(allgemein, "orte") is PyObjekt orte ? Get(orte, ort) as PyObjekt : null;
        var nurWohnung = wohnung && allgemein is not null ? ListeOderLeer(allgemein, "wohnung").Select(Py.Str).ToList() : null;
        var flur = Objekt(gebaeude["flur"]);
        double breite = Py.F(gebaeude["breite"]), tiefe = Py.F(gebaeude["hoehe"]);
        double flurY = Py.F(flur["y"]), flurH = Py.F(flur["h"]);
        var (sx, sy) = Wege.StartPlatz(gebaeude);
        var personenPlaetze = personen.Select(Wege.Platz).OfType<(double, double)>().ToList();
        var zonen = Wohnung.TuerFlaechen(gebaeude);
        var hindernisse = Wege.Hindernisse(gebaeude);

        var frei = new HashSet<(long, long)>(new LaufOrt(gebaeude, personen).Frei);
        var vorher = Erreichbar(frei, sx, sy);
        // erreichbar bleiben muss, was es vorher war: Personen (Reichweite zum Ansprechen), Waren, der Ausgang
        var pflicht = personenPlaetze.Select(p => (X: p.Item1, Y: p.Item2, Reichweite: Wege.Reichweite + 0.15))
            .Concat(ziele).Where(z => Nahe(vorher, z.X, z.Y, z.Reichweite)).ToList();
        var plaetze = new List<(string, float, float)>();
        var gesetzt = new List<Gesetzt>();
        var alle = new List<(double X1, double Z1, double X2, double Z2)>();
        foreach (var platz in InnenPlaetze)
        {
            if (nurWohnung is not null && !nurWohnung.Contains(platz))
            {
                continue;
            }
            var kreise = InnenEintraege(muster, platz).Select(e => (E: e, Dx: Zahl(e, "dx"), Dz: Zahl(e, "dz"), R: Radius(e, g, Innenraum.Meter))).ToList();
            var stehend = kreise.Where(k => k.R > 0).ToList();
            if (stehend.Count == 0)
            {
                continue;
            }
            float minX = stehend.Min(k => k.Dx - k.R), maxX = stehend.Max(k => k.Dx + k.R);
            float minZ = stehend.Min(k => k.Dz - k.R), maxZ = stehend.Max(k => k.Dz + k.R);
            const double Rand = 0.2;
            var kandidaten = new List<(double X, double Z)>();
            if (nurWohnung is not null)
            {
                // Wohnung (Nico 4a): fester Platz am Eingang, keine Suche: im Raum ueber dem Eingang (Wohnraum) an der
                // Wand zum Flur, rechts neben seiner Tuer (der Flur selbst und die Ecke links der Tuer sind zu schmal)
                var raum = Liste(gebaeude["raeume"]).Select(Objekt)
                    .FirstOrDefault(r => Py.F(r["x"]) <= 0 && Math.Abs(Py.F(r["y"]) + Py.F(r["h"]) - flurY) < 1e-6);
                if (raum is not null && Get(raum, "tuer") is PyObjekt tuer && tuer.Count > 0)
                {
                    kandidaten.Add((Py.F(tuer["bis"]) + Wohnung.TuerFrei + Rand - minX, Py.F(raum["y"]) + Py.F(raum["h"]) - Rand - maxZ));
                }
            }
            else if (feste is not null && Get(feste, platz) is List<object?> p)
            {
                // fester Platz des Orts (wie auf den Entwurfsfotos); passt er nicht, der naechste freie in der Naehe
                var (fx, fz) = (Py.F(p[0]), Py.F(p[1]));
                for (var dx = -1.6; dx <= 1.6001; dx += 0.2)
                {
                    for (var dz = -1.6; dz <= 1.6001; dz += 0.2)
                    {
                        kandidaten.Add((fx + dx, fz + dz));
                    }
                }
                kandidaten = [.. kandidaten.OrderBy(k => ((k.X - fx) * (k.X - fx)) + ((k.Z - fz) * (k.Z - fz))).ThenBy(k => k.X).ThenBy(k => k.Z)];
            }
            else
            {
                // im Flur: links = an der oberen Wand, rechts = an der unteren, je vom Eingang weg; Ecke = hinten im Flur
                double obenZ = flurY + Rand - minZ, untenZ = flurY + flurH - Rand - maxZ;
                if (platz == "ecke")
                {
                    for (var x = breite - Rand - maxX; x > sx; x -= Wege.Schritt)
                    {
                        kandidaten.Add((x, obenZ));
                        kandidaten.Add((x, untenZ));
                    }
                }
                else
                {
                    var z = platz == "links" ? obenZ : untenZ;
                    for (var x = sx + 1.0 - minX; x + maxX < breite; x += Wege.Schritt)
                    {
                        kandidaten.Add((x, z));
                    }
                }
            }
            var versuche = 0;
            foreach (var (kx, kz) in kandidaten)
            {
                var rechtecke = stehend.Select(k => (X1: kx + k.Dx - k.R, Z1: kz + k.Dz - k.R, X2: kx + k.Dx + k.R, Z2: kz + k.Dz + k.R)).ToList();
                if (Grund(rechtecke, hindernisse, zonen, breite, tiefe, sx, sy, nurWohnung is not null ? StartAbstandWohnung : StartAbstand, personenPlaetze) is { } grund)
                {
                    gruende?.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{platz}@{kx:F2},{kz:F2}: {grund}"));
                    continue;
                }
                if (++versuche > 24)
                {
                    gruende?.Add(platz + ": zu viele Versuche");
                    break;
                }
                // wie BasisRaster: gesperrt ist jede Zelle, deren Mitte naeher als Koerper an einem Rechteck liegt
                var neuFrei = new HashSet<(long, long)>(frei);
                neuFrei.RemoveWhere(c =>
                {
                    var (mx, my) = Wege.Mitte(c);
                    return rechtecke.Any(r => Abstand(mx, my, r.X1, r.Z1, r.X2, r.Z2) < Wege.Koerper);
                });
                var nachher = Erreichbar(neuFrei, sx, sy);
                if (nachher.Count != vorher.Count(neuFrei.Contains) || pflicht.Any(z => !Nahe(nachher, z.X, z.Y, z.Reichweite)))
                {
                    gruende?.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{platz}@{kx:F2},{kz:F2}: sperrt einen Weg"));
                    continue;
                }
                frei = neuFrei;
                vorher = nachher;
                alle.AddRange(rechtecke);
                hindernisse.AddRange(rechtecke.Select(r => (r.X1, r.Z1, r.X2, r.Z2)));
                plaetze.Add((platz, (float)kx, (float)kz));
                foreach (var k in kreise)
                {
                    gesetzt.Add(new Gesetzt(Py.Str(k.E["art"]), Get(k.E, "name") as string, (float)kx + k.Dx, (float)kz + k.Dz, k.R, k.R > 0));
                }
                break;
            }
        }
        if (plaetze.Count == 0)
        {
            return null;
        }
        var neu = MitHindernissen(gebaeude, alle);
        return new InnenPlan(neu, new LaufOrt(neu, personen), plaetze, gesetzt);
    }

    /// <summary>Abstand der Deko zum Startplatz (Wohnung: der feste Platz liegt am Eingang, darum kleiner).</summary>
    private const double StartAbstand = 1.0, StartAbstandWohnung = 0.5;

    /// <summary>Warum die Rechtecke hier nicht stehen duerfen (null = sie passen).</summary>
    private static string? Grund(List<(double X1, double Z1, double X2, double Z2)> rechtecke, List<(double, double, double, double)> hindernisse,
        List<(double X, double Y, double W, double H)> zonen, double breite, double tiefe, double sx, double sy, double startAbstand, List<(double, double)> personen)
    {
        foreach (var (x1, z1, x2, z2) in rechtecke)
        {
            // innerhalb des Grundrisses (die Kaesten der Modelle sind groesser als ihr Fuss, Baum und Wand duerfen sich nahe kommen)
            if (x1 < 0 || z1 < 0 || x2 > breite || z2 > tiefe)
            {
                return "am Rand";
            }
            if (hindernisse.Any(h => x1 < h.Item3 && h.Item1 < x2 && z1 < h.Item4 && h.Item2 < z2))
            {
                return "Wand oder Einrichtung";
            }
            if (zonen.Any(t => x1 < t.X + t.W && t.X < x2 && z1 < t.Y + t.H && t.Y < z2))
            {
                return "vor einer Tuer";
            }
            if (Abstand(sx, sy, x1, z1, x2, z2) < startAbstand)
            {
                return "am Startplatz";
            }
            if (personen.Any(p => Abstand(p.Item1, p.Item2, x1, z1, x2, z2) < Wege.PersonRadius + 0.3))
            {
                return "bei einer Person";
            }
        }
        return null;
    }

    /// <summary>Die Innen-Deko eines Plans zeichnen; Konfetti nur auf freiem Boden (<paramref name="frei"/>).</summary>
    public static void InnenZeichnen(InnenPlan plan, PyObjekt muster, List<Teil> teile, List<ModellStueck> stuecke, GrafikBibliothek? g, PyMenge frei)
    {
        bool Boden(float x, float z) => frei.Contains(Wege.Zelle(x, z));
        foreach (var (platz, x, z) in plan.Plaetze)
        {
            foreach (var e in InnenEintraege(muster, platz))
            {
                Zeichnen(e, x + Zahl(e, "dx"), z + Zahl(e, "dz"), Innenraum.Meter, 0f, Vector2.UnitY, 0, teile, stuecke, g, Boden);
            }
        }
    }

    private static double Abstand(double x, double y, double x1, double y1, double x2, double y2)
    {
        var dx = Math.Max(Math.Max(x1 - x, 0), x - x2);
        var dy = Math.Max(Math.Max(y1 - y, 0), y - y2);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>Kopie des Grundrisses mit den Rechtecken als Einrichtung im Flur (Typ <see cref="Hindernis"/>).</summary>
    private static PyObjekt MitHindernissen(PyObjekt gebaeude, List<(double X1, double Z1, double X2, double Z2)> rechtecke)
    {
        var neu = (PyObjekt)Py.Kopie(gebaeude)!;
        var flur = Objekt(neu["flur"]);
        var deko = Get(flur, "deko") as List<object?> ?? [];
        foreach (var (x1, z1, x2, z2) in rechtecke)
        {
            deko.Add(new PyObjekt { { "typ", Hindernis }, { "x", x1 }, { "y", z1 }, { "w", x2 - x1 }, { "h", z2 - z1 } });
        }
        Py.Setzen(flur, "deko", deko);
        return neu;
    }

    /// <summary>Wie <see cref="Erreichbar(IReadOnlySet{ValueTuple{long, long}}, double, double)"/> fuer die Zellen eines Laufrasters.</summary>
    public static HashSet<(long, long)> Erreichbar(PyMenge frei, double x, double y) => Erreichbar(new HashSet<(long, long)>(frei), x, y);

    /// <summary>Alle Zellen, die vom Startplatz aus erreichbar sind (diagonal nur ueber freie Nachbarn, also wie vier Richtungen).</summary>
    public static HashSet<(long, long)> Erreichbar(IReadOnlySet<(long, long)> frei, double x, double y)
    {
        var erreicht = new HashSet<(long, long)>();
        var start = Wege.Zelle(x, y);
        if (!frei.Contains(start))
        {
            // wie NaechsteFrei: die naechste freie Zelle zum Startpunkt
            if (frei.Count == 0)
            {
                return erreicht;
            }
            start = frei.MinBy(c => { var (mx, my) = Wege.Mitte(c); return ((mx - x) * (mx - x)) + ((my - y) * (my - y)); });
        }
        var reihe = new Queue<(long, long)>();
        reihe.Enqueue(start);
        erreicht.Add(start);
        while (reihe.Count > 0)
        {
            var (s, z) = reihe.Dequeue();
            foreach (var n in (ReadOnlySpan<(long, long)>)[(s + 1, z), (s - 1, z), (s, z + 1), (s, z - 1)])
            {
                if (frei.Contains(n) && erreicht.Add(n))
                {
                    reihe.Enqueue(n);
                }
            }
        }
        return erreicht;
    }

    private static bool Nahe(HashSet<(long, long)> erreicht, double x, double y, double reichweite)
    {
        var grenze = reichweite * reichweite;
        return erreicht.Any(c =>
        {
            var (mx, my) = Wege.Mitte(c);
            return ((mx - x) * (mx - x)) + ((my - y) * (my - y)) <= grenze;
        });
    }
}
