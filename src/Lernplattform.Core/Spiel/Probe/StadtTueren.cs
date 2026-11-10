using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Ein Haus ohne Ort mit Gehweg, Tuer und Namensschild (nicht betretbar, Phase 8 W8b: Stellwerk).</summary>
public sealed record StadtZierhaus(string Name, Vector3 Schild, Vector3 KastenMin, Vector3 KastenMax, float GehX, float GehZ);

/// <summary>
/// Phase 8 W8b (Weisung P8-20, Nico 4a): Tueren und Gehwege der Stadt, nur Darstellung. Jeder Fussweg endet
/// mittig an einer Tuer auf der Wand: hat das CC0-Modell selbst eine Tuer zum Weg, wird es in x so verschoben,
/// dass sie auf dem Weg sitzt (<c>darstellung.modell_tuer</c> in stadt.json), sonst bekommt das Haus eine Tuer
/// aus einfachen Kaesten (Rahmen, zwei Glasfluegel, Stufe, Vordach).
/// </summary>
public static class StadtTueren
{
    public static readonly Vector3 Rahmen = Netz.Farbe("#3A3550");
    public static readonly Vector3 Glas = Netz.Farbe("#7FB8D6");
    public static readonly Vector3 GlasHell = Netz.Farbe("#A5E9F5");
    public static readonly Vector3 Stufe = Netz.Farbe("#8E8AA3");
    public static readonly Vector3 Vordach = Netz.Farbe("#F4B942");

    /// <summary>Abschnitt <c>darstellung</c> aus stadt.json (leer, wenn er fehlt).</summary>
    public static PyObjekt Darstellung(Stadt stadt) => Get(stadt.Daten, "darstellung") as PyObjekt ?? [];

    /// <summary>Versatz des Modells in x, wenn es seine eigene Tuer zum Weg hat; sonst null.</summary>
    public static float? ModellVersatz(Stadt stadt, string ort) =>
        Get(Get(Darstellung(stadt), "modell_tuer") as PyObjekt ?? [], ort) is { } v ? (float)Py.F(v) : null;

    /// <summary>Gehwege mit Tuer an Haeusern ohne Ort (zierwege).</summary>
    public static IEnumerable<PyObjekt> Zierwege(Stadt stadt) =>
        (Get(Darstellung(stadt), "zierwege") as List<object?> ?? []).Select(Objekt);

    /// <summary>
    /// Tuer aus Kaesten: Mitte (x, z) auf der Wand, Richtung n zeigt nach aussen. Rahmen, zwei Glasfluegel
    /// mit hellem Oberlicht, Stufe davor, Vordach darueber.
    /// </summary>
    public static void Tuer(List<Teil> teile, float x, float z, Vector2 n, float hoehe, float hinten = 0f, bool vordach = true, Vector3? rahmen = null)
    {
        var b = StadtSzene.TuerBreite;
        var quer = new Vector2(-n.Y, n.X);
        void Q(float a1, float a2, float y1, float y2, float d1, float d2, Vector3 f)
        {
            // a = entlang der Wand, d = nach aussen
            var p1 = new Vector2(x, z) + (quer * a1) + (n * d1);
            var p2 = new Vector2(x, z) + (quer * a2) + (n * d2);
            teile.Add(new Teil(TeilArt.Quader, MathF.Min(p1.X, p2.X), y1, MathF.Min(p1.Y, p2.Y), MathF.Max(p1.X, p2.X), y2, MathF.Max(p1.Y, p2.Y), f));
        }
        var h = hoehe;
        // P8-28: der Rahmen reicht nach hinten bis an die tiefste Stelle der Wand dahinter (kein Spalt)
        Q(-(b / 2) - 0.05f, (b / 2) + 0.05f, 0f, h + 0.05f, -0.02f - hinten, 0.03f, rahmen ?? Rahmen);
        Q(-(b / 2) + 0.03f, -0.015f, 0.02f, h - 0.03f, 0.03f, 0.045f, Glas);
        Q(0.015f, (b / 2) - 0.03f, 0.02f, h - 0.03f, 0.03f, 0.045f, Glas);
        Q(-(b / 2) + 0.06f, -0.04f, h * 0.55f, h - 0.06f, 0.045f, 0.05f, GlasHell);
        Q(0.04f, (b / 2) - 0.06f, h * 0.55f, h - 0.06f, 0.045f, 0.05f, GlasHell);
        Q(-(b / 2) - 0.08f, (b / 2) + 0.08f, 0f, 0.035f, 0.0f, 0.16f, Stufe);
        if (vordach)
        {
            Q(-(b / 2) - 0.12f, (b / 2) + 0.12f, h + 0.05f, h + 0.1f, 0f, 0.3f, Vordach);
        }
    }

    /// <summary>
    /// P8-30: hat das CC0-Modell ueber dem Platz vor der Tuer schon ein Dach (Gewerbehof: gruenes Vordach), kommt kein
    /// eigenes Vordach dazu, sonst steckt eines im anderen. Geprueft mit 15 Strahlen nach oben (quer ueber die Breite des
    /// Vordachs, bis 0,3 vor der Wand), Treffer bis 0,3 ueber der Tuer.
    /// </summary>
    public static bool Ueberdacht(IReadOnlyList<ModellStueck> stuecke, GrafikBibliothek grafik, Vector2 mitte, Vector2 aussen, float hoehe)
    {
        var quer = new Vector2(-aussen.Y, aussen.X);
        var halb = (StadtSzene.TuerBreite / 2) + 0.12f;
        foreach (var a in (ReadOnlySpan<float>)[-halb, -halb / 2, 0f, halb / 2, halb])
        {
            foreach (var d in (ReadOnlySpan<float>)[0.05f, 0.15f, 0.28f])
            {
                var p = mitte + (quer * a) + (aussen * d);
                if (Treffer(stuecke, grafik, new Vector3(p.X, 0.1f, p.Y), Vector3.UnitY, hoehe + 0.2f) is not null)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Rand um die Tuer (Rahmen, Vordach), in dem kein Fenster liegen darf (Weisung P8-28).</summary>
    public const float Rand = 0.05f;

    /// <summary>Halbe Breite der Tuer mit Rahmen und Rand, quer zur Wand.</summary>
    public static float HalbeFlaeche => (StadtSzene.TuerBreite / 2) + 0.05f + Rand;

    /// <summary>Glas eines CC0-Modells: deutlich blau (Fensterscheiben der Pakete, z. B. #BCE2FF, #87AEE5).</summary>
    public static bool IstGlas(Vector3 f) => f.Z >= 0.85f && f.Z - f.X >= 0.2f;

    /// <summary>
    /// Weisung P8-28 (Nico: Tueren sitzen nicht am Haus): die CC0-Modelle fuellen ihren Kasten nicht ganz, ihre
    /// Wand liegt oft hinter der Kastenwand (Vorbau, Vordach, Saeulen). Gemessen mit Strahlen von aussen auf die
    /// Tuerflaeche (9 Spalten ueber die Glasbreite, 4 Hoehen): Tiefe der Tuer hinter der Kastenwand und Tiefe der
    /// hintersten getroffenen Stelle. Steht in der Mitte ein Pfeiler vor der Wand, sitzt die Tuer vor dem Pfeiler
    /// (sonst stuende er mitten in der Tuer), sonst auf der Wand (Mittel der Spalten). Null, wenn das Modell dort fehlt.
    /// </summary>
    public static (float Tiefe, float Hinten)? Wandtiefe(IReadOnlyList<ModellStueck> stuecke, GrafikBibliothek grafik, Vector2 wand, Vector2 aussen, float hoehe)
    {
        var quer = new Vector2(-aussen.Y, aussen.X);
        var halb = (StadtSzene.TuerBreite / 2) - 0.03f;
        var spalten = new List<(int I, float T)>();
        var hinten = 0f;
        for (var i = -4; i <= 4; i++)
        {
            var treffer = new List<float>();
            for (var j = 1; j <= 4; j++)
            {
                var p = wand + (quer * (i * halb / 4)) + (aussen * 0.5f);
                if (Treffer(stuecke, grafik, new Vector3(p.X, hoehe * j / 5f, p.Y), new Vector3(-aussen.X, 0, -aussen.Y), 1.4f) is { } t && t.T - 0.5f >= -0.02f)
                {
                    treffer.Add(t.T - 0.5f);
                }
            }
            if (treffer.Count >= 2)
            {
                spalten.Add((i, Mitte(treffer)));
                hinten = MathF.Max(hinten, treffer.Max());
            }
        }
        if (spalten.Count < 5)
        {
            return null;
        }
        var mittel = Mitte([.. spalten.Select(s => s.T)]);
        var mitteSpalte = spalten.Where(s => s.I == 0).Select(s => (float?)s.T).FirstOrDefault();
        var tiefe = mitteSpalte is { } m && m < mittel - 0.03f ? m : mittel;
        return (MathF.Max(0f, tiefe), MathF.Max(0f, hinten - MathF.Max(0f, tiefe)));
    }

    private static float Mitte(List<float> werte)
    {
        werte.Sort();
        return werte.Count % 2 == 1 ? werte[werte.Count / 2] : (werte[(werte.Count / 2) - 1] + werte[werte.Count / 2]) / 2;
    }

    /// <summary>
    /// Liegt ein Dreieck (a, b, c) auf der Flaeche einer Tuer? Quer zur Wand innerhalb von <see cref="HalbeFlaeche"/>,
    /// unten bis ueber das Vordach (Hoehe + 0,1 + Rand), in der Tiefe zwischen Rand vor der Tuer und Rand hinter der
    /// hintersten Stelle der Wand (mitte = Tuer auf der Wand, aussen = Richtung nach draussen).
    /// </summary>
    public static bool AufTuerFlaeche(Vector3 a, Vector3 b, Vector3 c, Vector2 mitte, Vector2 aussen, float hoehe, float hinten)
    {
        var quer = new Vector2(-aussen.Y, aussen.X);
        float qmin = float.MaxValue, qmax = float.MinValue, dmin = float.MaxValue, dmax = float.MinValue;
        foreach (var p in (ReadOnlySpan<Vector3>)[a, b, c])
        {
            var r = new Vector2(p.X, p.Z) - mitte;
            var q = Vector2.Dot(r, quer);
            var d = -Vector2.Dot(r, aussen);
            (qmin, qmax, dmin, dmax) = (MathF.Min(qmin, q), MathF.Max(qmax, q), MathF.Min(dmin, d), MathF.Max(dmax, d));
        }
        var unten = MathF.Min(a.Y, MathF.Min(b.Y, c.Y));
        return qmax > -HalbeFlaeche && qmin < HalbeFlaeche && unten < hoehe + 0.1f + Rand && dmin < hinten + Rand && dmax > -Rand;
    }

    /// <summary>
    /// Weisung P8-28 (Nico: "Bitte dann die Fenster entfernen, wenn dort eine Tuer ist."): Glas-Dreiecke der
    /// CC0-Stuecke [von, bis), die auf der Tuerflaeche liegen, bekommen die haeufigste Wandfarbe dieses Stuecks
    /// an der Tuer. Das Stueck behaelt Namen und Lage; die umgefaerbte Fassung steht in <paramref name="umgefaerbt"/>.
    /// </summary>
    public static void FensterWeg(List<ModellStueck> stuecke, int von, int bis, GrafikBibliothek grafik, Dictionary<int, GrafikModell> umgefaerbt,
        Vector2 mitte, Vector2 aussen, float hoehe, float hinten)
    {
        for (var i = von; i < bis; i++)
        {
            var gm = umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name);
            if (gm is null)
            {
                continue;
            }
            var weg = new List<int>();
            var wand = new Dictionary<Vector3, int>();
            for (var d = 0; d < gm.Dreiecke; d++)
            {
                var (a, b, c) = Ecken(gm, d, stuecke[i].Lage);
                var f = new Vector3(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2]) / 255f;
                if (!AufTuerFlaeche(a, b, c, mitte, aussen, hoehe, hinten))
                {
                    continue;
                }
                if (IstGlas(f))
                {
                    weg.Add(d);
                }
                else
                {
                    wand[f] = wand.GetValueOrDefault(f) + 1;
                }
            }
            if (weg.Count == 0)
            {
                continue;
            }
            // haeufigste Farbe ohne Glas (bei Gleichstand die zuerst gezaehlte), ohne Wand dort Grau
            var farbe = wand.Count > 0 ? wand.MaxBy(w => w.Value).Key : new Vector3(0.56f, 0.57f, 0.6f);
            var farben = (byte[])gm.Farben.Clone();
            foreach (var d in weg)
            {
                farben[d * 3] = (byte)MathF.Round(farbe.X * 255);
                farben[(d * 3) + 1] = (byte)MathF.Round(farbe.Y * 255);
                farben[(d * 3) + 2] = (byte)MathF.Round(farbe.Z * 255);
            }
            umgefaerbt[i] = gm.MitFarben(farben);
        }
    }

    /// <summary>
    /// Eine Tuer eines CC0-Modells an der Fassade einer Tuer (P8-30): Stueck, Dreiecke, Spanne quer zur Wand (A1..A2,
    /// 0 = Mitte der Tuer, positiv in Richtung (-aussen.Y, aussen.X)), Tiefe hinter der Tuer, Oberkante.
    /// </summary>
    public sealed record ModellTuer(int Stueck, List<int> Dreiecke, float A1, float A2, float Tiefe, float Oben)
    {
        public float Mitte => (A1 + A2) / 2;
    }

    /// <summary>Wie weit seitlich (quer zur Wand) Tueren der Modelle als "zweite Tuer" zaehlen (P8-30).</summary>
    public const float NebenTuerWeite = 1.2f;

    private static float Helligkeit(Vector3 f) => (0.2126f * f.X) + (0.7152f * f.Y) + (0.0722f * f.Z);

    /// <summary>
    /// Weisung P8-30 (Nico: "versetzt noch die Hintergrundtuer zu sehen"): Tueren der CC0-Stuecke [von, bis) an der
    /// Fassade der Tuer (mitte, aussen) erkennen. Eine Modell-Tuer ist ein dunkles Rechteck (Helligkeit unter 0,45,
    /// kein Glas; Rahmen, Fluegel und Stufe zusammen, alles unter 0,66) parallel zur Wand, unten am Boden (bis 0,05),
    /// mindestens 0,25 hoch und 0,15 bis 0,55 breit, hoechstens
    /// 0,4 vor bis 0,5 hinter der Tuer und hoechstens <see cref="NebenTuerWeite"/> + 0,3 seitlich. Mit
    /// <paramref name="hell"/> zaehlt jede Farbe und die Breite bis 5,5 (Rolltor der Werkstatt).
    /// </summary>
    public static List<ModellTuer> ModellTueren(IReadOnlyList<ModellStueck> stuecke, int von, int bis, Func<int, GrafikModell?> modell,
        Vector2 mitte, Vector2 aussen, bool hell = false, float weite = NebenTuerWeite, float tief = 0.5f, bool glas = false)
    {
        var quer = new Vector2(-aussen.Y, aussen.X);
        var ergebnis = new List<ModellTuer>();
        for (var i = von; i < bis; i++)
        {
            if (modell(i) is not { } gm)
            {
                continue;
            }
            // Dreiecke je Ebene (Tiefe auf 0,01 gerundet)
            var ebenen = new SortedDictionary<int, List<(int D, float A1, float A2, float Y1, float Y2)>>();
            for (var d = 0; d < gm.Dreiecke; d++)
            {
                var f = new Vector3(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2]) / 255f;
                // Fund 262: mit glas zaehlt auch eine Glastuer am Boden (Tuer-Teile des Gebaeude-Editors, Kenney Modular Buildings)
                // Sammelliste 267: das Tuerblatt an der Stelle der Glastuer zaehlt wie die Glastuer (gleiche Erkennung wie vorher)
                var istGlas = IstGlas(f) || IstTuerblatt(f);
                if ((istGlas && !glas) || (!hell && !istGlas && Helligkeit(f) >= 0.45f))
                {
                    continue;
                }
                var (a, b, c) = Ecken(gm, d, stuecke[i].Lage);
                float qmin = float.MaxValue, qmax = float.MinValue, dmin = float.MaxValue, dmax = float.MinValue;
                foreach (var p in (ReadOnlySpan<Vector3>)[a, b, c])
                {
                    var r = new Vector2(p.X, p.Z) - mitte;
                    var q = Vector2.Dot(r, quer);
                    var t = -Vector2.Dot(r, aussen);
                    (qmin, qmax, dmin, dmax) = (MathF.Min(qmin, q), MathF.Max(qmax, q), MathF.Min(dmin, t), MathF.Max(dmax, t));
                }
                var y1 = MathF.Min(a.Y, MathF.Min(b.Y, c.Y));
                var y2 = MathF.Max(a.Y, MathF.Max(b.Y, c.Y));
                // Fund 262: Glas zaehlt nur, wenn es bis zum Boden reicht (Fenster daneben gehoeren nicht zur Tuer)
                if ((glas && istGlas && y1 > 0.05f) || dmax - dmin > 0.02f || dmin < -0.4f || dmax > tief || qmax < -weite - 0.3f || qmin > weite + 0.3f || y1 > 0.7f || qmax - qmin < 0.005f)
                {
                    continue;
                }
                var k = (int)MathF.Round((dmin + dmax) / 2 * 100);
                if (!ebenen.TryGetValue(k, out var liste))
                {
                    ebenen[k] = liste = [];
                }
                liste.Add((d, qmin, qmax, y1, y2));
            }
            foreach (var (k, liste) in ebenen)
            {
                // Tueren bestehen aus mehreren Stuecken (Rahmen, Fluegel, Stufe): seitlich zusammenhaengende Dreiecke
                // einer Ebene, die ganz unter 0,66 liegen, zu Rechtecken fassen
                var unten = liste.Where(e => e.Y2 <= 0.66f).OrderBy(e => e.A1).ToList();
                var gruppe = new List<(int D, float A1, float A2, float Y1, float Y2)>();
                var rechts = float.MinValue;
                void Fertig()
                {
                    if (gruppe.Count == 0)
                    {
                        return;
                    }
                    float a1 = gruppe.Min(g => g.A1), a2 = gruppe.Max(g => g.A2), y1 = gruppe.Min(g => g.Y1), y2 = gruppe.Max(g => g.Y2);
                    if (y1 <= 0.05f && y2 >= 0.25f && a2 - a1 >= 0.15f && a2 - a1 <= (hell ? 5.5f : glas ? GlasTuerBreit : 0.55f))
                    {
                        ergebnis.Add(new ModellTuer(i, [.. gruppe.Select(g => g.D)], a1, a2, k / 100f, y2));
                    }
                    gruppe.Clear();
                }
                foreach (var e in unten)
                {
                    if (gruppe.Count > 0 && e.A1 > rechts + 0.01f)
                    {
                        Fertig();
                    }
                    gruppe.Add(e);
                    rechts = gruppe.Max(g => g.A2);
                }
                Fertig();
            }
        }
        return ergebnis;
    }

    /// <summary>Breiteste Glastuer eines Editor-Teils (Kenney "building-door": 0,75).</summary>
    public const float GlasTuerBreit = 0.8f;

    /// <summary>
    /// Fund 262 (Weisung P12-122): so nah muss ein Tuerpunkt an der Mitte einer CC0-Tuer eines Stadt-Modells liegen, damit
    /// sie die Tuer ist (Einrasten im Werkzeug setzt den Punkt genau auf die Mitte, auf 0,01 gerundet).
    /// </summary>
    public const float EigeneTuerFang = 0.02f;

    /// <summary>Fund 262: so weit (zur Wand hin) darf die Wand unter einer gezeichneten Tuer von ihrer Flaeche abweichen.</summary>
    public const float BuendigGrenze = 0.01f;

    /// <summary>Halbe Breite der gezeichneten Tuer samt Rahmen (<see cref="Tuer"/>).</summary>
    public static float HalbRahmen => (StadtSzene.TuerBreite / 2) + 0.05f;

    /// <summary>Hat das Modell eigene Glastueren am Boden (Gebaeude aus dem Gebaeude-Editor, Modellname "bau:…")?</summary>
    public static bool MitGlasTuer(string modell) => modell.StartsWith(Lernplattform.Core.Stadtbau.Bauteile.ModellPraefix, StringComparison.Ordinal);

    /// <summary>Gegenprobe Fund 262: Tuer wie bis A 0.54.0 (immer eine gezeichnete Tuer, nur in Tests, je Ablauf).</summary>
    public static bool EigeneTuerAltFuerTests { get => EigeneAlt.Value; set => EigeneAlt.Value = value; }
    private static readonly AsyncLocal<bool> EigeneAlt = new();

    /// <summary>
    /// Fund 262 (Weisung P12-122, Nico: Tueren glitchen in der Fassade): die eigene Tuer des Modells an der Tuerstelle
    /// (Wandpunkt <paramref name="wand"/>, Richtung <paramref name="aussen"/>), falls es eine gibt. Stadt-Modelle: eine
    /// dunkle CC0-Tuer genau an der Stelle (bis <see cref="EigeneTuerFang"/>). Gebaeude aus dem Editor (<paramref name="glas"/>):
    /// jede Glastuer am Boden, die eine gezeichnete Tuer samt Rahmen ueberdecken wuerde (die naechste). Dann zeichnet das
    /// Spiel keine zweite Tuer darueber.
    /// </summary>
    public static ModellTuer? EigeneTuer(IReadOnlyList<ModellStueck> stuecke, int von, int bis, Func<int, GrafikModell?> modell, Vector2 wand, Vector2 aussen, bool glas)
    {
        if (EigeneAlt.Value)
        {
            return null;
        }
        var fang = glas ? HalbRahmen : EigeneTuerFang;
        return ModellTueren(stuecke, von, bis, modell, wand, aussen, weite: fang + GlasTuerBreit, glas: glas)
            .Where(t => glas ? t.A1 < fang && t.A2 > -fang : MathF.Abs(t.Mitte) <= fang)
            // S 0.31.0: Mitte auf 0,01 gerundet, sonst gewinnt bei einem Tuerblatt mit Vorder- und Rueckseite (Halle) die Seite,
            // deren Mitte im Rechnen zufaellig naeher an 0 liegt; gleich weit = die vordere Flaeche
            .OrderBy(t => glas && !GlasOderBlatt(t) ? 1 : 0).ThenBy(t => MathF.Round(MathF.Abs(t.Mitte) * 100)).ThenBy(t => t.Tiefe).FirstOrDefault();

        // S 0.31.0: mit glas zuerst eine Tuer aus Glas oder Tuerblatt (Halle: der dunkle Rahmen des Tuerteils davor zaehlt nicht)
        bool GlasOderBlatt(ModellTuer t) => modell(t.Stueck) is { } m && t.Dreiecke.Count > 0
            && (IstGlas(m.Farbe(t.Dreiecke[0])) || IstTuerblatt(m.Farbe(t.Dreiecke[0])));
    }

    /// <summary>Sammelliste 267: Farbe des Tuerblatts an der Glastuer eines Gebaeudes aus dem Editor (dunkles Holz).</summary>
    public static readonly Vector3 Tuerblatt = Netz.Farbe("#4A3426");

    /// <summary>Das Tuerblatt als Farbe je Dreieck (R, G, B), z. B. fuer das Tuerblatt im Tuerteil einer Halle.</summary>
    public static byte[] TuerblattBytes => [0x4A, 0x34, 0x26];

    /// <summary>Hat ein Dreieck genau die Farbe des Tuerblatts (auf ein Byte genau)?</summary>
    public static bool IstTuerblatt(Vector3 f) => TuerblattFarben.Any(t => Vector3.Distance(f, t) < 0.5f / 255f);

    /// <summary>Alle Farben, die ein Tuerblatt haben kann (Tuer-Varianten, Nico 10.11.2026).</summary>
    private static readonly Vector3[] TuerblattFarben = [.. Gebaeudefarben.TuerVarianten.Select(v => Netz.Farbe(v.Wert))];

    /// <summary>Sammelliste 267: Farbe der Klinke (Messing).</summary>
    public static readonly Vector3 Klinke = Netz.Farbe("#D8B85A");

    /// <summary>Groesse der Klinke (quer, hoch) und wie weit sie vor dem Tuerblatt steht.</summary>
    public const float KlinkeBreit = 0.05f;
    public const float KlinkeHoch = 0.025f;
    public const float KlinkeVor = 0.025f;

    /// <summary>Gegenprobe Sammelliste 267: Glastuer wie in A 0.55.0 (bleibt Glas, keine Klinke).</summary>
    public static bool TuerblattAltFuerTests { get => TuerblattAlt.Value; set => TuerblattAlt.Value = value; }
    private static readonly AsyncLocal<bool> TuerblattAlt = new();

    /// <summary>
    /// Sammelliste 267 (Weisung P12-129, Nico: die Glastuer der Editor-Tuerteile sieht aus der Naehe wie ein Fenster aus):
    /// die eigene Glastuer <paramref name="tuer"/> wird zum dunklen <see cref="Tuerblatt"/> (nur ihre Dreiecke, Rahmen
    /// und Fenster daneben bleiben), dazu eine Klinke auf der Seite zur naeheren Kante (rechts, wenn die Tuer mittig sitzt).
    /// Das Tuerblatt bleibt genau in der Ebene der Glastuer (0,1 hinter der Fassade, Regel 262 unveraendert), die Klinke
    /// steht <see cref="KlinkeVor"/> davor und damit noch in der Laibung. Kein neuer Zeichenaufruf (Kasten im Stadtnetz).
    /// </summary>
    public static void TuerblattSetzen(List<Teil> teile, IReadOnlyList<ModellStueck> stuecke, Func<int, GrafikModell?> modell,
        Dictionary<int, GrafikModell> umgefaerbt, ModellTuer tuer, Vector2 wand, Vector2 aussen, Vector3? farbe = null)
    {
        var blatt = farbe ?? Tuerblatt;
        if (TuerblattAlt.Value || modell(tuer.Stueck) is not { } gm)
        {
            return;
        }
        var farben = (byte[])gm.Farben.Clone();
        var quer = new Vector2(-aussen.Y, aussen.X);
        var alle = new HashSet<int>(tuer.Dreiecke);
        // S 0.31.0 (Halle): das Tuerblatt im Tuerteil der Halle hat schon die Farbe des Tuerblatts und liegt in mehreren Ebenen
        // (Rahmen vorn, Fuellung etwas tiefer, Rueckseite); alle diese Dreiecke auf der Tuer bekommen die gewaehlte Farbe.
        // Glastueren der Wohnhaeuser haben vorher keine solchen Dreiecke (nichts aendert sich).
        for (var d = 0; d < gm.Dreiecke; d++)
        {
            if (alle.Contains(d) || !IstTuerblatt(new Vector3(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2]) / 255f))
            {
                continue;
            }
            var (a, b, c) = Ecken(gm, d, stuecke[tuer.Stueck].Lage);
            var m = (a + b + c) / 3;
            var r = new Vector2(m.X, m.Z) - wand;
            var q = Vector2.Dot(r, quer);
            var t = -Vector2.Dot(r, aussen);
            if (q >= tuer.A1 - 0.05f && q <= tuer.A2 + 0.05f && t >= tuer.Tiefe - 0.05f && t <= tuer.Tiefe + 0.15f && m.Y <= tuer.Oben + 0.1f)
            {
                alle.Add(d);
            }
        }
        foreach (var d in alle)
        {
            farben[d * 3] = (byte)MathF.Round(blatt.X * 255);
            farben[(d * 3) + 1] = (byte)MathF.Round(blatt.Y * 255);
            farben[(d * 3) + 2] = (byte)MathF.Round(blatt.Z * 255);
        }
        umgefaerbt[tuer.Stueck] = gm.MitFarben(farben);
        // Klinke 0,06 vor der Kante, auf der Seite, auf der weniger Tuer liegt (Kenney-Tueren sind mittig: Seite +quer)
        var seite = tuer.A2 - 0.06f - (KlinkeBreit / 2);
        var mitte = wand + (quer * seite) - (aussen * tuer.Tiefe);
        var y = MathF.Min(tuer.Oben * 0.45f, 0.3f);
        var p1 = mitte - (quer * (KlinkeBreit / 2));
        var p2 = mitte + (quer * (KlinkeBreit / 2)) + (aussen * KlinkeVor);
        teile.Add(new Teil(TeilArt.Quader, MathF.Min(p1.X, p2.X), y, MathF.Min(p1.Y, p2.Y), MathF.Max(p1.X, p2.X), y + KlinkeHoch, MathF.Max(p1.Y, p2.Y), Klinke));
    }

    /// <summary>
    /// Fund 262: Gruende, warum eine gezeichnete Tuer (nicht die eigene des Modells) nicht sauber sitzt, leer = bündig.
    /// Strahlen von aussen auf die ganze Flaeche samt Rahmen (9 Spalten, 3 Hoehen ueber dem Sockel): jede Stelle der Wand liegt hoechstens
    /// <see cref="BuendigGrenze"/> vor oder hinter der Tuer (sonst steckt der Rahmen in der Wand oder steht vor einer Nische),
    /// die Wand fehlt an keiner Stelle, und keine noch sichtbare Tuer des Modells (auch Glastuer) liegt unter ihr.
    /// </summary>
    public static List<string> NichtBuendig(StadtSzene szene, StadtTuer tuer)
    {
        var gruende = new List<string>();
        // Haus aus Kaesten (ohne CC0-Stuecke): die Tuer sitzt genau auf der Kastenwand
        if (tuer.Modell || szene.Grafik is null || HausVon(szene, tuer) is not { } haus || haus.StueckeBis == haus.StueckeVon)
        {
            return gruende;
        }
        var stuecke = Enumerable.Range(haus.StueckeVon, haus.StueckeBis - haus.StueckeVon)
            .Select(i => szene.Stuecke[i]).ToList();
        var quer = new Vector2(-tuer.Aussen.Y, tuer.Aussen.X);
        var halb = HalbRahmen;
        float vorne = 0f, hinten = 0f;
        var fehlt = false;
        for (var i = -4; i <= 4; i++)
        {
            // j = 1 (Hoehe 0,09) liegt im Sockel, den fast jedes CC0-Modell hat (0,035 bis 0,07 vor der Wand); davor steht
            // die Stufe der Tuer (E1: zaehlt nicht, sonst liesse sich an kaum einem Haus eine Tuer setzen)
            for (var j = 2; j <= 4; j++)
            {
                var p = tuer.Mitte + (quer * (i * halb / 4)) + (tuer.Aussen * 0.5f);
                if (Treffer(stuecke, szene.Grafik, new Vector3(p.X, tuer.Hoehe * j / 5f, p.Y), new Vector3(-tuer.Aussen.X, 0, -tuer.Aussen.Y), 1.4f) is { } t)
                {
                    var d = t.T - 0.5f;
                    (vorne, hinten) = (MathF.Min(vorne, d), MathF.Max(hinten, d));
                }
                else
                {
                    fehlt = true;
                }
            }
        }
        if (vorne < -BuendigGrenze)
        {
            gruende.Add("vorne");
        }
        if (hinten > BuendigGrenze)
        {
            gruende.Add("hinten");
        }
        if (fehlt)
        {
            gruende.Add("fehlt");
        }
        if (ModellTueren(szene.Stuecke, haus.StueckeVon, haus.StueckeBis, szene.ModellAn, tuer.Mitte, tuer.Aussen, weite: halb + GlasTuerBreit, glas: true)
            .Any(t => t.A1 < halb && t.A2 > -halb && !InWandfarbeSichtbar(szene, t, tuer)))
        {
            gruende.Add("tuer");
        }
        return gruende;
    }

    /// <summary>
    /// Haus einer Tuer: der Ort selbst (Haus-Kennung = Ort), sonst das Haus ohne Ort dieses Modells ("modell#n"), dessen
    /// Kasten die Tuer beruehrt (bis 0,05).
    /// </summary>
    public static StadtHaus? HausVon(StadtSzene szene, StadtTuer tuer) =>
        szene.Haeuser.FirstOrDefault(h => h.Id == tuer.Ort)
        ?? szene.Haeuser.FirstOrDefault(h => h.Id.StartsWith(tuer.Ort + "#", StringComparison.Ordinal)
            && h.Min.X - 0.05f <= tuer.Mitte.X && tuer.Mitte.X <= h.Max.X + 0.05f && h.Min.Z - 0.05f <= tuer.Mitte.Y && tuer.Mitte.Y <= h.Max.Z + 0.05f);

    private static bool InWandfarbeSichtbar(StadtSzene szene, ModellTuer t, StadtTuer tuer) =>
        InWandfarbe(szene.ModellAn(t.Stueck)!, szene.Stuecke[t.Stueck].Lage, t, tuer.Mitte, tuer.Aussen);

    /// <summary>
    /// Weisung P8-30 (Nico: "bei einigen Tueren sind dann versetzt noch die Hintergrundtuer zu sehen"): Tueren der
    /// CC0-Stuecke [von, bis) neben einer eigenen Tuer (bis <paramref name="weite"/> seitlich) bekommen samt ihrem Rahmen
    /// die Farbe der Wand um sie herum, damit an jedem Haus nur eine Tuer zu sehen ist.
    /// </summary>
    public static void TuerenWeg(List<ModellStueck> stuecke, int von, int bis, GrafikBibliothek grafik, Dictionary<int, GrafikModell> umgefaerbt,
        Vector2 mitte, Vector2 aussen, float weite = NebenTuerWeite, float tief = 0.5f)
    {
        GrafikModell? An(int i) => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name);
        foreach (var t in ModellTueren(stuecke, von, bis, An, mitte, aussen, weite: weite, tief: tief).Where(t => t.A2 > -weite && t.A1 < weite))
        {
            var gm = An(t.Stueck)!;
            var lage = stuecke[t.Stueck].Lage;
            var farbe = Wandfarbe(gm, lage, t, mitte, aussen);
            var farben = (byte[])gm.Farben.Clone();
            foreach (var d in t.Dreiecke.Concat(TuerRahmen(gm, lage, t, mitte, aussen)))
            {
                farben[d * 3] = (byte)MathF.Round(farbe.X * 255);
                farben[(d * 3) + 1] = (byte)MathF.Round(farbe.Y * 255);
                farben[(d * 3) + 2] = (byte)MathF.Round(farbe.Z * 255);
            }
            umgefaerbt[t.Stueck] = gm.MitFarben(farben);
        }
    }

    /// <summary>Ist eine Modell-Tuer samt Rahmen ganz in der Farbe der Wand um sie herum (also nicht zu sehen)?</summary>
    public static bool InWandfarbe(GrafikModell gm, Matrix4x4 lage, ModellTuer tuer, Vector2 mitte, Vector2 aussen)
    {
        var farbe = Wandfarbe(gm, lage, tuer, mitte, aussen);
        return tuer.Dreiecke.Concat(TuerRahmen(gm, lage, tuer, mitte, aussen)).All(d => Vector3.Distance(new Vector3(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2]) / 255f, farbe) < 0.01f);
    }

    /// <summary>
    /// Weisung P8-30: Tueren der CC0-Stuecke [von, bis) an den beiden Seitenwaenden des Hauses (Kasten min/max, quer zur
    /// Tuer mit Richtung <paramref name="aussen"/>) wie <see cref="TuerenWeg"/> in Wandfarbe; sie sind beim Blick auf die
    /// eigene Tuer von schraeg zu sehen. Die Rueckseite bleibt, wie das Modell sie hat.
    /// </summary>
    public static void SeitenTuerenWeg(List<ModellStueck> stuecke, int von, int bis, GrafikBibliothek grafik, Dictionary<int, GrafikModell> umgefaerbt,
        Vector2 aussen, Vector3 min, Vector3 max)
    {
        foreach (var n in SeitenWaende(aussen, min, max))
        {
            TuerenWeg(stuecke, von, bis, grafik, umgefaerbt, n.Mitte, n.Aussen, n.Weite, n.Tief);
        }
    }

    /// <summary>
    /// Die beiden Seitenwaende eines Hauskastens quer zur Richtung <paramref name="aussen"/>: Mitte, Richtung nach aussen,
    /// halbe Laenge und Tiefe (ganzer Kasten, denn Seitenwaende der Modelle koennen weit hinter dem Kasten liegen, z. B.
    /// das schmalere vordere Haus von Lindenau).
    /// </summary>
    public static IEnumerable<(Vector2 Mitte, Vector2 Aussen, float Weite, float Tief)> SeitenWaende(Vector2 aussen, Vector3 min, Vector3 max)
    {
        var c = new Vector2((min.X + max.X) / 2, (min.Z + max.Z) / 2);
        var halb = new Vector2((max.X - min.X) / 2, (max.Z - min.Z) / 2);
        foreach (var n in new[] { new Vector2(-aussen.Y, aussen.X), new Vector2(aussen.Y, -aussen.X) })
        {
            var tief = MathF.Abs(n.X) > 0.5f ? halb.X : halb.Y;
            var lang = MathF.Abs(n.X) > 0.5f ? halb.Y : halb.X;
            yield return (c + (n * tief), n, lang, 2 * tief);
        }
    }

    /// <summary>
    /// Rahmen einer Modell-Tuer: Dreiecke (kein Glas, jede Farbe), die ganz im Kasten um die Tuer liegen (seitlich 0,1 mehr,
    /// bis 0,1 ueber die Tuer, 0,12 davor und dahinter, weil eine Seitenwand auch von der anderen Seite her erkannt wird).
    /// </summary>
    private static IEnumerable<int> TuerRahmen(GrafikModell gm, Matrix4x4 lage, ModellTuer tuer, Vector2 mitte, Vector2 aussen)
    {
        var quer = new Vector2(-aussen.Y, aussen.X);
        for (var d = 0; d < gm.Dreiecke; d++)
        {
            var f = new Vector3(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2]) / 255f;
            if (IstGlas(f))
            {
                continue;
            }
            var (a, b, c) = Ecken(gm, d, lage);
            var drin = true;
            foreach (var p in (ReadOnlySpan<Vector3>)[a, b, c])
            {
                var r = new Vector2(p.X, p.Z) - mitte;
                var q = Vector2.Dot(r, quer);
                var t = -Vector2.Dot(r, aussen);
                drin &= q >= tuer.A1 - 0.1f && q <= tuer.A2 + 0.1f && p.Y >= -0.01f && p.Y <= tuer.Oben + 0.1f && t >= tuer.Tiefe - 0.12f && t <= tuer.Tiefe + 0.12f;
            }
            if (drin)
            {
                yield return d;
            }
        }
    }

    /// <summary>Farbe der Wand um eine Modell-Tuer: groesste Flaeche gleicher Farbe in ihrer Ebene (ohne Tuer, ohne Glas), sonst Grau.</summary>
    private static Vector3 Wandfarbe(GrafikModell gm, Matrix4x4 lage, ModellTuer tuer, Vector2 mitte, Vector2 aussen)
    {
        var quer = new Vector2(-aussen.Y, aussen.X);
        var drin = tuer.Dreiecke.ToHashSet();
        var flaechen = new Dictionary<Vector3, float>();
        for (var d = 0; d < gm.Dreiecke; d++)
        {
            var f = new Vector3(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2]) / 255f;
            if (drin.Contains(d) || IstGlas(f))
            {
                continue;
            }
            var (a, b, c) = Ecken(gm, d, lage);
            float qmin = float.MaxValue, qmax = float.MinValue, dmin = float.MaxValue, dmax = float.MinValue;
            foreach (var p in (ReadOnlySpan<Vector3>)[a, b, c])
            {
                var r = new Vector2(p.X, p.Z) - mitte;
                (qmin, qmax) = (MathF.Min(qmin, Vector2.Dot(r, quer)), MathF.Max(qmax, Vector2.Dot(r, quer)));
                (dmin, dmax) = (MathF.Min(dmin, -Vector2.Dot(r, aussen)), MathF.Max(dmax, -Vector2.Dot(r, aussen)));
            }
            if (dmax - dmin > 0.02f || MathF.Abs(((dmin + dmax) / 2) - tuer.Tiefe) > 0.05f || qmax < tuer.A1 - 0.6f || qmin > tuer.A2 + 0.6f
                || MathF.Min(a.Y, MathF.Min(b.Y, c.Y)) > tuer.Oben + 0.3f)
            {
                continue;
            }
            flaechen[f] = flaechen.GetValueOrDefault(f) + (Vector3.Cross(b - a, c - a).Length() / 2);
        }
        return flaechen.Count > 0 ? flaechen.MaxBy(w => w.Value).Key : new Vector3(0.56f, 0.57f, 0.6f);
    }

    /// <summary>Die drei Ecken eines Dreiecks in der Welt.</summary>
    public static (Vector3 A, Vector3 B, Vector3 C) Ecken(GrafikModell gm, int d, Matrix4x4 lage)
    {
        var e = gm.Ecken;
        return (Vector3.Transform(new Vector3(e[d * 9], e[(d * 9) + 1], e[(d * 9) + 2]), lage),
            Vector3.Transform(new Vector3(e[(d * 9) + 3], e[(d * 9) + 4], e[(d * 9) + 5]), lage),
            Vector3.Transform(new Vector3(e[(d * 9) + 6], e[(d * 9) + 7], e[(d * 9) + 8]), lage));
    }

    /// <summary>
    /// Erster Treffer eines Strahls (Start, Richtung mit Laenge 1) auf die Dreiecke der CC0-Stuecke, beidseitig,
    /// hoechstens <paramref name="weit"/> weit: Entfernung und Farbe des getroffenen Dreiecks, sonst null.
    /// </summary>
    public static (float T, Vector3 Farbe)? Treffer(IEnumerable<ModellStueck> stuecke, GrafikBibliothek grafik, Vector3 start, Vector3 richtung, float weit)
    {
        (float T, Vector3 Farbe)? best = null;
        foreach (var st in stuecke)
        {
            if (grafik.Modell(st.Name) is not { } gm)
            {
                continue;
            }
            var e = gm.Ecken;
            for (var d = 0; d < gm.Dreiecke; d++)
            {
                var a = Vector3.Transform(new Vector3(e[d * 9], e[(d * 9) + 1], e[(d * 9) + 2]), st.Lage);
                var b = Vector3.Transform(new Vector3(e[(d * 9) + 3], e[(d * 9) + 4], e[(d * 9) + 5]), st.Lage);
                var c = Vector3.Transform(new Vector3(e[(d * 9) + 6], e[(d * 9) + 7], e[(d * 9) + 8]), st.Lage);
                // Moeller-Trumbore
                var k1 = b - a;
                var k2 = c - a;
                var pv = Vector3.Cross(richtung, k2);
                var det = Vector3.Dot(k1, pv);
                if (MathF.Abs(det) < 1e-9f)
                {
                    continue;
                }
                var tv = start - a;
                var u = Vector3.Dot(tv, pv) / det;
                if (u < 0 || u > 1)
                {
                    continue;
                }
                var qv = Vector3.Cross(tv, k1);
                var v = Vector3.Dot(richtung, qv) / det;
                if (v < 0 || u + v > 1)
                {
                    continue;
                }
                var t = Vector3.Dot(k2, qv) / det;
                if (t >= 0 && t <= weit && (best is null || t < best.Value.T))
                {
                    best = (t, new Vector3(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2]) / 255f);
                }
            }
        }
        return best;
    }

    /// <summary>Naechste Wand eines Kastens zum Punkt p: Punkt auf der Wand und Richtung nach aussen.</summary>
    public static (Vector2 Wand, Vector2 Aussen) Wand(Vector3 min, Vector3 max, Vector2 p)
    {
        var kandidaten = new (float D, Vector2 W, Vector2 N)[]
        {
            (MathF.Abs(p.Y - max.Z), new(Math.Clamp(p.X, min.X, max.X), max.Z), new(0, 1)),
            (MathF.Abs(p.Y - min.Z), new(Math.Clamp(p.X, min.X, max.X), min.Z), new(0, -1)),
            (MathF.Abs(p.X - max.X), new(max.X, Math.Clamp(p.Y, min.Z, max.Z)), new(1, 0)),
            (MathF.Abs(p.X - min.X), new(min.X, Math.Clamp(p.Y, min.Z, max.Z)), new(-1, 0)),
        };
        // nur Waende, deren Spanne den Punkt enthaelt
        var passend = kandidaten.Where(k => k.N.X == 0 ? p.X >= min.X - 0.01f && p.X <= max.X + 0.01f : p.Y >= min.Z - 0.01f && p.Y <= max.Z + 0.01f).ToList();
        var best = (passend.Count > 0 ? passend : [.. kandidaten]).MinBy(k => k.D);
        return (best.W, best.N);
    }
}
