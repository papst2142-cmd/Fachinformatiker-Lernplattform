using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="StadtSzene"/> mit den Modellen der Stadt
/// (Haeuser, Feinlage, Bahnhof, Zug). Reiner Umzug aus StadtSzene.cs, nichts geaendert.
/// </summary>
public sealed partial class StadtSzene
{
    /// <summary>
    /// Sammelliste 294 (Weisung P12-196): Grundmodell einer Kopie. Das Werkzeug legt fuer ein zweites Haus gleichen Modells mit
    /// Tuer (bzw. einen Bahnhof ohne Bahnsteig) eine Kopie <c>modell_n</c> an, deren Teile genau Teile des Grundmodells sind.
    /// Grafik je Teil (<see cref="GrafikBibliothek.Bloecke"/>, Schluessel „modell:teilnr“) und Sonderregeln (Turm, Uhr, Altbau)
    /// gelten fuer den Namen des Grundmodells; darum die Nummer jedes Teils der Kopie im Grundmodell (gleiche Daten, der Reihe
    /// nach). null = keine Kopie (Name ohne „_n“, Grundmodell fehlt oder ein Teil passt nicht). Fassung 0 hat keine Kopie.
    /// </summary>
    public static (string Name, Dictionary<int, int> Nr)? KopieGrund(PyObjekt modelle, string name)
    {
        if (KopieAlt.Value)
        {
            return null;
        }
        var m = System.Text.RegularExpressions.Regex.Match(name, "^(.+)_([0-9]+)$");
        if (!m.Success || Get(modelle, name) is not List<object?> kopie || Get(modelle, m.Groups[1].Value) is not List<object?> grund)
        {
            return null;
        }
        var texte = grund.Select(PythonJson.Standard).ToList();
        var frei = Enumerable.Range(0, grund.Count).ToList();
        var nr = new Dictionary<int, int>();
        for (var i = 0; i < kopie.Count; i++)
        {
            var t = PythonJson.Standard(kopie[i]);
            var j = frei.FirstOrDefault(f => texte[f] == t, -1);
            if (j < 0)
            {
                return null;
            }
            frei.Remove(j);
            nr[i] = j;
        }
        return (m.Groups[1].Value, nr);
    }

    /// <summary>
    /// Phase 8 W11 (Sammelliste 155, Teilplan S1): Darstellung einzelner Teile aus orte.json (die Datei bleibt wie in
    /// Python): null = nicht zeichnen, sonst Versatz in x. Der Turm des Bahnhofs entfaellt (er steckte 0,6 tief im
    /// Hauptgebaeude), der Anbau des Altbaus (lag ganz im Hauptgebaeude) steht Wand an Wand an dessen Ostwand.
    /// </summary>
    public static float? Darstellung(string modell, int teilNr) => (modell, teilNr) switch
    {
        ("bahnhof", 3) => null,
        ("wohnung", 5) => AltbauAnbau,
        _ => 0f,
    };

    /// <summary>Versatz des Altbau-Anbaus nach Osten: von x 2,3 an die Ostwand des Hauptgebaeudes (x 3,2).</summary>
    public const float AltbauAnbau = 0.9f;

    /// <summary>Welle A 0.70.0: Farben des Zugzielanzeigers (Kasten blau wie bei der Bahn, Rahmen hell, Schrift hell; die Schrift leuchtet nachts).</summary>
    public static readonly Vector3 AnzeigeBlau = Netz.Farbe("#1E3A8A");

    public static readonly Vector3 AnzeigeRahmen = Netz.Farbe("#CBD5E1");

    public static readonly Vector3 AnzeigeSchrift = new(0.96f, 0.97f, 0.99f);

    /// <summary>Hoehe des Kastens eines Zugzielanzeigers (oben = Hoehe des Masts).</summary>
    public const float AnzeigeHoehe = 0.22f;

    /// <summary>
    /// Welle A 0.70.0 (Weisung P12-218, Nico: „wie eine Bahn-Anzeige blau und rechteckig“): quer liegender Zugzielanzeiger mit
    /// Mitte <paramref name="x"/>, <paramref name="y"/> (Karte), Breite <paramref name="w"/> und Oberkante <paramref name="h"/>:
    /// zwei Pfosten, heller Rahmen, auf beiden Seiten eine blaue Flaeche mit drei hellen Zeilen (Uhrzeit, Ziel, Hinweis).
    /// Die Zeilen leuchten nachts (Farbe <see cref="AnzeigeSchrift"/> in <see cref="LichtTeile"/>), die blaue Flaeche nicht.
    /// </summary>
    public static List<Teil> Zugzielanzeiger(float x, float y, float w, float h)
    {
        var teile = new List<Teil>();
        float l = x - (w / 2), r = x + (w / 2), unten = h - AnzeigeHoehe;
        const float Tief = 0.03f, Rand = 0.018f;
        foreach (var px in new[] { l + 0.06f, r - 0.06f })
        {
            teile.Add(Quader(px - 0.02f, 0, y - 0.02f, px + 0.02f, unten + 0.02f, y + 0.02f, Farben.Metall));
        }
        teile.Add(Quader(l, unten, y - Tief, r, h, y + Tief, AnzeigeRahmen));
        foreach (var seite in new[] { -1f, 1f })
        {
            var z1 = y + (seite * Tief);
            var z2 = z1 + (seite * 0.004f);
            teile.Add(Quader(l + Rand, unten + Rand, Math.Min(z1, z2), r - Rand, h - Rand, Math.Max(z1, z2), AnzeigeBlau));
            var z3 = z2 + (seite * 0.003f);
            var innenL = l + Rand + 0.025f;
            var breite = w - (2 * Rand) - 0.05f;
            // Zeilen: oben kurz (Uhrzeit) und lang (Ziel), darunter eine mittlere Zeile
            foreach (var (von, bis, hoch) in new[] { (0f, 0.22f, 0.15f), (0.3f, 1f, 0.15f), (0f, 0.7f, 0.07f) })
            {
                var yz = unten + Rand + 0.02f + (hoch * (AnzeigeHoehe - (2 * Rand) - 0.04f) / 0.15f);
                teile.Add(Quader(innenL + (von * breite), yz - 0.012f, Math.Min(z2, z3), innenL + (bis * breite), yz + 0.012f, Math.Max(z2, z3), AnzeigeSchrift));
            }
        }
        return teile;
    }

    /// <summary>Lage der Bahnhofsuhr (Uhr c2) relativ zum Bahnhof: Mitte x, Hoehe, Wand (Suedwand des Hauptgebaeudes), Halbmesser, Arm.</summary>
    /// <summary>Farbe des Zifferblatts der Bahnhofsuhr; nachts leuchten nur seine beiden Seiten (wie die Scheibe eines Fensters).</summary>
    public static readonly Vector3 UhrBlatt = new(0.97f, 0.97f, 0.95f);

    public const float UhrX = 3.2f, UhrY = 1.15f, UhrWand = 2.0f, UhrR = 0.24f, UhrArm = 0.1f;

    /// <summary>
    /// Phase 8 W11 (Nico P8-42/P8-45, Uhr c2): runde Bahnhofsuhr am Ausleger, quer zur Suedwand und von beiden Seiten
    /// lesbar: Wandplatte auf der Wand, kurzer kraeftiger Arm bis in den Uhrring, Strebe darunter. Dunkler Ring,
    /// weisses Zifferblatt (leuchtet nachts wie ein Fenster), Zeiger 10:10 und roter Sekundenzeiger bleiben dunkel.
    /// </summary>
    public static List<Teil> BahnhofsUhr(float ox, float oz)
    {
        var teile = new List<Teil>();
        var weiss = UhrBlatt;
        var dunkel = new Vector3(0.16f, 0.17f, 0.19f);
        var zeiger = new Vector3(0.08f, 0.08f, 0.09f);
        var rot = new Vector3(0.80f, 0.12f, 0.12f);
        var wandZ = oz + UhrWand;
        float kx = ox + UhrX, ky = UhrY, kr = UhrR, arm = UhrArm;
        teile.Add(Quader(kx - 0.07f, ky - 0.16f, wandZ - 0.01f, kx + 0.07f, ky + 0.07f, wandZ + 0.03f, dunkel));
        teile.Add(Quader(kx - 0.03f, ky - 0.03f, wandZ, kx + 0.03f, ky + 0.03f, wandZ + arm + 0.04f, dunkel));
        for (var st = 0; st < 5; st++)
        {
            var z1 = wandZ + (arm * st / 5f);
            var y1 = ky - 0.15f + (0.12f * st / 5f);
            teile.Add(Quader(kx - 0.015f, y1, z1, kx + 0.015f, y1 + 0.04f, z1 + (arm / 5f) + 0.01f, dunkel));
        }
        // Scheibe quer zur Wand: a = laengs der Wand (z), c = quer (x)
        var cz = wandZ + arm + kr;
        const float d0 = 0.035f;
        Teil K(float a1, float b1, float c1, float a2, float b2, float c2, Vector3 f) => Quader(kx + c1, b1, cz + a1, kx + c2, b2, cz + a2, f);
        const int n = 10;
        for (var i = 0; i < n; i++)
        {
            var y1 = -kr + (2 * kr * i / n);
            var y2 = -kr + (2 * kr * (i + 1) / n);
            var ym = (y1 + y2) / 2;
            var halb = MathF.Sqrt(MathF.Max(0, (kr * kr) - (ym * ym)));
            var innen = MathF.Sqrt(MathF.Max(0, (kr * 0.84f * (kr * 0.84f)) - (ym * ym)));
            teile.Add(K(-halb, ky + y1, -d0, halb, ky + y2, d0, dunkel));
            if (innen > 0)
            {
                teile.Add(K(-innen, ky + y1, -d0 - 0.008f, innen, ky + y2, d0 + 0.008f, weiss));
            }
        }
        const float vz = d0 + 0.012f;
        teile.Add(K(-0.012f, ky, -vz, 0.012f, ky + (kr * 0.72f), vz, zeiger));
        teile.Add(K(-kr * 0.5f, ky - 0.012f, -vz, 0, ky + 0.012f, vz, zeiger));
        teile.Add(K(-0.006f, ky - (kr * 0.75f), -vz - 0.004f, 0.006f, ky, vz + 0.004f, rot));
        foreach (var (dx, dy) in new[] { (0f, 0.74f), (0.74f, 0f), (0f, -0.74f), (-0.74f, 0f) })
        {
            teile.Add(K((dx * kr) - 0.012f, ky + (dy * kr) - 0.012f, -vz, (dx * kr) + 0.012f, ky + (dy * kr) + 0.012f, vz, zeiger));
        }
        return teile;
    }

    /// <summary>
    /// Welle A 0.70.0 (Sammelliste 314): ein fein gedrehtes Haus waehrend des Baus. Teile, Stuecke und Tueren entstehen im Rahmen des
    /// Hauses; <see cref="FeinDrehen"/> dreht sie danach in die Welt. Die Bloecke bleiben bis dahin in <see cref="Hindernisse"/>
    /// (Rahmen des Hauses) und sperren danach als schmale Streifen (<see cref="Hausdrehung.Streifen"/>) das freie Gehen.
    /// </summary>
    private sealed class FeinBau(Hausdrehung.Feinlage lage, int teileVon, int stueckeVon)
    {
        public Hausdrehung.Feinlage Lage { get; } = lage;

        public int TeileVon { get; } = teileVon;

        public int TeileBis { get; set; } = teileVon;

        public int StueckeVon { get; } = stueckeVon;

        public int StueckeBis { get; set; } = stueckeVon;

        /// <summary>Weitere Teile des Hauses (Tuer am Zierweg), je von bis.</summary>
        public List<(int Von, int Bis)> Weitere { get; } = [];

        /// <summary>Tueren dieses Hauses (Stellen in der Liste der Tueren).</summary>
        public List<int> Tueren { get; } = [];

        /// <summary>Bloecke im Rahmen des Hauses.</summary>
        public List<(double, double, double, double)> Hindernisse { get; } = [];

        /// <summary>Baeume des Modells (Rahmen des Hauses), gesetzt erst nach dem Drehen.</summary>
        public List<(float X, float Z, float R)> Baeume { get; } = [];

        /// <summary>Stelle in der Liste der Haeuser (-1 = ohne Kasten).</summary>
        public int Haus { get; set; } = -1;
    }

    /// <summary>Welle A 0.70.0: ein fein gedrehtes Haus in die Welt drehen (Teile, Stuecke, Tueren, Bloecke als Streifen).</summary>
    private static void FeinDrehen(FeinBau f, List<Teil> teile, List<ModellStueck> stuecke, List<(double, double, double, double)> hindernisse, List<StadtTuer> tueren)
    {
        foreach (var (von, bis) in f.Weitere.Prepend((f.TeileVon, f.TeileBis)))
        {
            for (var i = von; i < bis; i++)
            {
                teile[i] = f.Lage.Teil(teile[i]);
            }
        }
        var lage = f.Lage.Matrix;
        for (var i = f.StueckeVon; i < f.StueckeBis; i++)
        {
            stuecke[i] = stuecke[i] with { Lage = stuecke[i].Lage * lage };
        }
        foreach (var i in f.Tueren)
        {
            var t = tueren[i];
            tueren[i] = t with { Mitte = f.Lage.Punkt(t.Mitte), Aussen = f.Lage.Richtung(t.Aussen), WegAnfang = f.Lage.Punkt(t.WegAnfang), AnWand = t.Windfang ? f.Lage.Punkt(t.AnWand) : t.AnWand };
        }
        foreach (var (x1, z1, x2, z2) in f.Hindernisse)
        {
            hindernisse.AddRange(Hausdrehung.Streifen(f.Lage.Ecken(x1, z1, x2, z2)));
        }
    }

    /// <summary>Ein Modell an (ox, oz) bauen; Rueckgabe: Kasten (ohne Baeume) fuer Tippen und Schild.</summary>
    private static (Vector3 Min, Vector3 Max) Modell(List<Teil> teile, List<ModellStueck> stuecke, GrafikBibliothek? grafik, string modellName,
        List<(int Nr, PyObjekt Teil)> sichtbar, float ox, float oz, Vector3 ton, string seed, List<(double, double, double, double)> hindernisse,
        BaumPlaner baeume, float versatz = 0f, List<(int, Vector3)>? fenster = null, int dreh = 0, Vector2 drehMitte = default,
        (string Name, Dictionary<int, int> Nr)? grundVon = null, List<(float X, float Z, float R)>? baeumeSpaeter = null)
    {
        // Sammelliste 294: eine Kopie (modell_n) zeichnet sich wie ihr Grundmodell (Grafik je Teil, Sonderregeln wie Turm und Uhr)
        if (grundVon is { } gv)
        {
            modellName = gv.Name;
            sichtbar = [.. sichtbar.Select(t => (gv.Nr.GetValueOrDefault(t.Nr, -1), t.Teil))];
        }
        // Welle E (Weisung P12-67 Frage 1 = a): gedrehtes Haus = ungedreht bauen, dann alle neuen Teile, Modelle und
        // Hindernisse um die Mitte drehen; Baeume des Modells erst danach an der gedrehten Stelle setzen. dreh 0 = wie vorher.
        var (drehTv, drehSv, drehHv) = (teile.Count, stuecke.Count, hindernisse.Count);
        // Welle A 0.70.0: bei feiner Drehung setzt der Aufrufer die Baeume erst nach dem Drehen (baeumeSpaeter)
        var spaeteBaeume = dreh != 0 || baeumeSpaeter is not null ? new List<(float X, float Z, float R)>() : null;
        var bloecke = new List<Block>();
        foreach (var (blockNr, teil) in sichtbar.Where(t => Py.Gleich(t.Teil["art"], "block")))
        {
            if (Darstellung(modellName, blockNr) is not { } dx)
            {
                continue;
            }
            float x = ox + Zahl(teil, "x") + dx, z = oz + Zahl(teil, "y");
            bloecke.Add(new Block(x, z, x + Zahl(teil, "w"), z + Zahl(teil, "t"), Zahl(teil, "h")));
            // Haeuser sperren das freie Gehen (Flaechen wie Bahnsteige und Hoefe nicht)
            hindernisse.Add((x, z, x + Zahl(teil, "w"), z + Zahl(teil, "t")));
        }
        // Front und Hoehe einer Stelle der Fassade (Karten-y) suchen: Block mit Ueberlappung in x
        (float Front, float Hoehe, Block? B) Fassade(float x1, float x2, float y)
        {
            foreach (var b in bloecke)
            {
                if (x2 > b.X1 && x1 < b.X2 && y >= b.Z1 - b.H - 0.6f && y <= b.Z2 + 0.05f)
                {
                    return (b.Z2, Math.Clamp(b.Z2 - y, 0f, b.H + 1.5f), b);
                }
            }
            return (y, 0f, null);
        }
        Vector3 min = new(float.MaxValue), max = new(float.MinValue);
        void Kasten(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            min = Vector3.Min(min, new Vector3(x1, y1, z1));
            max = Vector3.Max(max, new Vector3(x2, y2, z2));
        }
        var nr = 0;
        foreach (var (teilNr, teil) in sichtbar)
        {
            nr++;
            var art = Py.Str(teil["art"]);
            if (Darstellung(modellName, teilNr) is not { } verschoben)
            {
                continue;
            }
            float x = ox + Zahl(teil, "x") + verschoben, y = oz + Zahl(teil, "y");
            var farbName = teil["farbe"] as string;
            var eigen = farbName is null or "akzent" ? ton : Teilfarbe(farbName, ton);
            switch (art)
            {
                case "block":
                {
                    float w = Zahl(teil, "w"), t = Zahl(teil, "t"), h = Zahl(teil, "h");
                    if (grafik is not null && grafik.Bloecke.TryGetValue(modellName + ":" + teilNr.ToString(CultureInfo.InvariantCulture), out var grafikName)
                        && grafik.Modell(grafikName) is { } gm)
                    {
                        // W8b: Versatz, damit die eigene Tuer des Modells auf dem Fussweg sitzt (Kasten bleibt)
                        var hoch = GrafikBlock(stuecke, gm, x + versatz, y, w, t, h, modellName == "bahnhof");
                        Kasten(x, 0, y, x + w, hoch, y + t);
                        break;
                    }
                    var dach = teil["dach"] as string;
                    var wand = Mischen(eigen, Weiss, 0.55f);
                    var dachFarbe = Mischen(eigen, Schwarz, 0.3f);
                    var wandHoehe = dach == "sattel" ? h * 0.72f : h;
                    teile.Add(Quader(x, 0, y, x + w, wandHoehe, y + t, wand));
                    if (dach == "sattel")
                    {
                        teile.Add(new Teil(TeilArt.Satteldach, x - 0.08f, wandHoehe, y - 0.08f, x + w + 0.08f, h, y + t + 0.08f, dachFarbe));
                    }
                    else if (dach == "shed")
                    {
                        // Sheddach: Saegezahn aus schmalen Satteldaechern quer zur Front
                        var anzahl = Math.Max(2, (int)(w / 1.0f));
                        for (var i = 0; i < anzahl; i++)
                        {
                            var links = x + (i * w / anzahl);
                            teile.Add(new Teil(TeilArt.Satteldach, links, h, y, links + (w / anzahl), h + 0.25f, y + t, Mischen(eigen, Weiss, 0.25f)));
                        }
                    }
                    else
                    {
                        teile.Add(Quader(x - 0.04f, h, y - 0.04f, x + w + 0.04f, h + 0.07f, y + t + 0.04f, dachFarbe));
                    }
                    if (!Py.Hat(teil, "fenster") || Py.Wahr(teil["fenster"]))
                    {
                        // Fensterraster wie model_part_shapes (Zeilen je 0,7, Spalten je 0,8), auf der Front; W9
                        // (Sammelliste 147): bei Satteldach nach der Wandhoehe, sonst lag jede Reihe im Dach
                        var zeilen = Math.Max(1, (int)(wandHoehe / 0.7f));
                        var spalten = Math.Max(1, (int)(w / 0.8f));
                        float zelleB = (w - 0.3f) / spalten, zelleH = (wandHoehe - 0.2f) / zeilen;
                        var anteil = Zahl(teil, "licht", 0.5f);
                        var lichtfarbe = teil["lichtfarbe"] is string lf && lf.Length > 0 ? Netz.Farbe(lf) : Farben.Licht;
                        for (var zeile = 0; zeile < zeilen; zeile++)
                        {
                            for (var spalte = 0; spalte < spalten; spalte++)
                            {
                                var oben = wandHoehe - 0.2f - (zeile * zelleH);
                                var unten = oben - (zelleH - 0.3f);
                                if (oben > wandHoehe - 0.05f || unten < 0.1f)
                                {
                                    continue;
                                }
                                var fx = x + 0.25f + (spalte * zelleB);
                                var an = Licht(seed + "|" + nr, (zeile * spalten) + spalte, anteil);
                                fenster?.Add((teile.Count, wand));
                                teile.Add(Quader(fx, unten, y + t, fx + zelleB - 0.25f, oben, y + t + 0.03f, an ? lichtfarbe : Farben.Fenster));
                                // W8b (Nico 5a): Fenster ringsum, auch an der Rueckseite
                                var hinten = Licht(seed + "|" + nr + "|r", (zeile * spalten) + spalte, anteil);
                                fenster?.Add((teile.Count, wand));
                                teile.Add(Quader(fx, unten, y - 0.03f, fx + zelleB - 0.25f, oben, y, hinten ? lichtfarbe : Farben.Fenster));
                            }
                        }
                        // und an beiden Seiten (Spalten je 0,8 der Tiefe)
                        var seitenSpalten = Math.Max(1, (int)(t / 0.8f));
                        var zelleT = (t - 0.3f) / seitenSpalten;
                        for (var zeile = 0; zeile < zeilen; zeile++)
                        {
                            for (var spalte = 0; spalte < seitenSpalten; spalte++)
                            {
                                var oben = wandHoehe - 0.2f - (zeile * zelleH);
                                var unten = oben - (zelleH - 0.3f);
                                if (oben > wandHoehe - 0.05f || unten < 0.1f)
                                {
                                    continue;
                                }
                                var fz = y + 0.25f + (spalte * zelleT);
                                var an = Licht(seed + "|" + nr + "|s", (zeile * seitenSpalten) + spalte, anteil);
                                fenster?.Add((teile.Count, wand));
                                teile.Add(Quader(x - 0.03f, unten, fz, x, oben, fz + zelleT - 0.25f, an ? lichtfarbe : Farben.Fenster));
                                fenster?.Add((teile.Count, wand));
                                teile.Add(Quader(x + w, unten, fz, x + w + 0.03f, oben, fz + zelleT - 0.25f, an ? lichtfarbe : Farben.Fenster));
                            }
                        }
                    }
                    Kasten(x, 0, y, x + w, h, y + t);
                    break;
                }
                case "flaeche":
                {
                    float w = Zahl(teil, "w"), h = Zahl(teil, "h");
                    var (front, hoehe, b) = Fassade(x, x + w, y + h);
                    if (b is not null && hoehe > 0.05f)
                    {
                        // auf der Front (Glasfassade, Terrasse): duenne Platte vor der Wand
                        teile.Add(Quader(x, Math.Max(0, front - (y + h)), front, x + w, front - y, front + 0.04f, eigen));
                    }
                    else
                    {
                        teile.Add(Quader(x, 0, y, x + w, Hoehe.Flaeche, y + h, eigen));
                    }
                    Kasten(x, 0, y, x + w, 0.05f, y + h);
                    break;
                }
                case "schild":
                {
                    // Welle A 0.55.0 (Weisung P12-121, Nico 1b, Sammelliste 261): das Schild (Platte ohne Schrift auf der
                    // Suedwand, oft grau auf der Rueckseite des Modells) wird nicht mehr gezeichnet; die Daten bleiben.
                    if (!SchildAltFuerTests)
                    {
                        break;
                    }
                    var w = Zahl(teil, "w");
                    var (front, hoehe, _) = Fassade(x, x + w, y + 0.28f);
                    var oben = Math.Max(0.28f, front - y);
                    teile.Add(Quader(x, Math.Max(0, hoehe), front, x + w, oben, front + 0.05f, ton));
                    break;
                }
                case "tor":
                {
                    float w = Zahl(teil, "w"), h = Zahl(teil, "h");
                    var (front, hoehe, _) = Fassade(x, x + w, y + h);
                    teile.Add(Quader(x, hoehe, front, x + w, Math.Max(hoehe + 0.1f, front - y), front + 0.04f, Mischen(eigen, Schwarz, 0.35f)));
                    break;
                }
                case "kreis":
                {
                    var r = Zahl(teil, "r", 0.4f);
                    var (front, hoehe, b) = Fassade(x - r, x + r, y + r);
                    if (modellName == "bahnhof")
                    {
                        // Phase 8 W11 (Nico P8-42/P8-45, Uhr c2): statt des gelben Schilds eine runde Bahnhofsuhr am Ausleger
                        teile.AddRange(BahnhofsUhr(ox, oz));
                        break;
                    }
                    if (b is not null)
                    {
                        var mitte = Math.Min(front - y, b.H + 0.3f);
                        // Sammelliste 315 (Weisung P12-207, Nico: „fliegen 3 Schilder über dem Gebäude, bitte entfernen“): eine
                        // Scheibe, die ganz ueber ihrem Block laege (Lokschuppen: Luefter, die Python oben auf das Dach malte),
                        // stand in 3D frei in der Luft vor dem Dach; sie wird nicht mehr gezeichnet. Gegenprobe KreisAltFuerTests
                        if (mitte - r >= b.H && !KreisAlt.Value)
                        {
                            break;
                        }
                        teile.Add(Quader(x - r, mitte - r, front, x + r, mitte + r, front + 0.05f, eigen));
                    }
                    else
                    {
                        teile.Add(Quader(x - r, 0, y - r, x + r, Hoehe.Flaeche + 0.012f, y + r, eigen));
                    }
                    break;
                }
                case "mast" when Py.Hat(teil, "w"):
                {
                    // Welle A 0.70.0 (Weisung P12-218, Nico): Mast mit Breite = Zugzielanzeiger (blauer Kasten mit hellem Rahmen
                    // auf zwei Pfosten, beidseitig lesbar, die Schrift leuchtet nachts). Ohne Breite wie bisher (Fassung 0).
                    float w = Zahl(teil, "w"), h = Zahl(teil, "h");
                    teile.AddRange(Zugzielanzeiger(x, y, w, h));
                    Kasten(x - (w / 2), 0, y - 0.06f, x + (w / 2), h + 0.02f, y + 0.06f);
                    break;
                }
                case "mast":
                {
                    var h = Zahl(teil, "h");
                    teile.Add(Quader(x - 0.04f, 0, y - 0.04f, x + 0.04f, h, y + 0.04f, Farben.Metall));
                    teile.Add(Quader(x - 0.11f, h, y - 0.11f, x + 0.11f, h + 0.2f, y + 0.11f, Farben.Rot));
                    Kasten(x - 0.12f, 0, y - 0.12f, x + 0.12f, h + 0.2f, y + 0.12f);
                    break;
                }
                case "baum":
                    if (spaeteBaeume is not null)
                    {
                        spaeteBaeume.Add((x, y, Zahl(teil, "r", 0.4f)));
                        break;
                    }
                    Baum(teile, stuecke, grafik, baeume, x, y, Zahl(teil, "r", 0.4f));
                    break;
                case "geruest":
                {
                    float w = Zahl(teil, "w"), h = Zahl(teil, "h");
                    var (front, _, _) = Fassade(x, x + w, y + h);
                    var oben = Math.Max(0.5f, front - y);
                    var stufen = Math.Max(2, (int)(w / 0.7f));
                    for (var i = 0; i <= stufen; i++)
                    {
                        var links = x + (i * w / stufen);
                        teile.Add(Quader(links - 0.025f, 0, front + 0.1f, links + 0.025f, oben, front + 0.15f, Farben.Metall));
                    }
                    for (var e = 0.8f; e < oben; e += 0.8f)
                    {
                        teile.Add(Quader(x, e - 0.025f, front + 0.1f, x + w, e + 0.025f, front + 0.15f, Farben.Metall));
                    }
                    break;
                }
            }
        }
        if (min.X > max.X)
        {
            min = new Vector3(ox, 0, oz);
            max = new Vector3(ox + 1, 1, oz + 1);
        }
        if (spaeteBaeume is not null && dreh == 0)
        {
            baeumeSpaeter!.AddRange(spaeteBaeume);
        }
        else if (spaeteBaeume is not null)
        {
            var m = new Vector2(ox, oz) + drehMitte;
            for (var i = drehTv; i < teile.Count; i++)
            {
                teile[i] = Hausdrehung.Drehen(teile[i], m, dreh);
            }
            var lage = Hausdrehung.Matrix(m, dreh);
            for (var i = drehSv; i < stuecke.Count; i++)
            {
                stuecke[i] = stuecke[i] with { Lage = stuecke[i].Lage * lage };
            }
            for (var i = drehHv; i < hindernisse.Count; i++)
            {
                var (h1, h2) = (Hausdrehung.Drehen(hindernisse[i].Item1, hindernisse[i].Item2, m.X, m.Y, dreh), Hausdrehung.Drehen(hindernisse[i].Item3, hindernisse[i].Item4, m.X, m.Y, dreh));
                hindernisse[i] = (Math.Min(h1.X, h2.X), Math.Min(h1.Y, h2.Y), Math.Max(h1.X, h2.X), Math.Max(h1.Y, h2.Y));
            }
            var (k1, k2) = (Hausdrehung.Drehen(new Vector2(min.X, min.Z), m, dreh), Hausdrehung.Drehen(new Vector2(max.X, max.Z), m, dreh));
            (min, max) = (new Vector3(MathF.Min(k1.X, k2.X), min.Y, MathF.Min(k1.Y, k2.Y)), new Vector3(MathF.Max(k1.X, k2.X), max.Y, MathF.Max(k1.Y, k2.Y)));
            foreach (var (bx, bz, br) in spaeteBaeume)
            {
                var p = Hausdrehung.Drehen(new Vector2(bx, bz), m, dreh);
                if (baeumeSpaeter is not null)
                {
                    baeumeSpaeter.Add((p.X, p.Y, br));
                    continue;
                }
                Baum(teile, stuecke, grafik, baeume, p.X, p.Y, br);
            }
        }
        return (min, max);
    }

    /// <summary>
    /// Ein Block aus einem CC0-Modell: so viele Kopien nebeneinander, dass jede etwa die Form des
    /// Modells behaelt, zusammen genau die Grundflaeche w x t (Front bei y + t). Die Hoehe liegt
    /// zwischen der Python-Hoehe h und der natuerlichen Hoehe des Modells (Mittel), damit Fenster und
    /// Tueren nicht platt gedrueckt wirken. Rueckgabe: Hoehe in der Welt.
    /// </summary>
    public static float GrafikBlock(List<ModellStueck> stuecke, GrafikModell gm, float x, float y, float w, float t, float h, bool gleich = false)
    {
        var mass = gm.Max - gm.Min;
        // erst ab 1,5-facher Breite eine zweite Kopie (sonst wirkt ein Laden wie zwei Haeuser)
        var anzahl = Math.Max(1, (int)((w / t) / (mass.X / mass.Z) / 1.5f));
        var sx = w / anzahl / mass.X;
        var sz = t / mass.Z;
        if (gleich)
        {
            // Phase 8 W11 (Sammelliste 155): gleichmaessig skaliert (vorher 2,9-fach breit und 2,2-fach tief, Daecher schief)
            sx = sz = MathF.Min(sx, sz);
        }
        var sy = ((h / mass.Y) + MathF.Sqrt(sx * sz)) / 2;
        for (var i = 0; i < anzahl; i++)
        {
            stuecke.Add(new ModellStueck(gm.Name, Lage(gm, gm.Min, new Vector3(sx, sy, sz), new Vector3(x + (i * w / anzahl), 0, y), Vector2.UnitY)));
        }
        return mass.Y * sy;
    }

    /// <summary>
    /// Ein stehender Zug (Train Kit, drei Wagen) auf dem laengsten geraden Gleisabschnitt, zur Zierde
    /// am Bahnhof; faehrt nicht und verbindet nichts (Zug zwischen Spielwelten plant Nico spaeter).
    /// </summary>
    private static void Zug(List<ModellStueck> stuecke, GrafikBibliothek grafik, IEnumerable<List<Vector2>> bahnen, Vector2? halt = null)
    {
        var wagen = new[] { "train-electric-city-a", "train-electric-city-b", "train-electric-city-c" }.Select(grafik.Modell).ToList();
        if (wagen.Any(w => w is null))
        {
            return;
        }
        // laengstes gerades Stueck der geglaetteten Bahn (die Boegen sind in kurze Stuecke zerlegt)
        (Vector2 A, Vector2 B) best = default;
        foreach (var p in bahnen)
        {
            var stueck = Kurve.LaengstesStueck(p);
            if ((stueck.B - stueck.A).Length() > (best.B - best.A).Length())
            {
                best = stueck;
            }
        }
        var skala = ZugBreite / (wagen[0]!.Max.X - wagen[0]!.Min.X);
        var gesamt = wagen.Sum(w => (w!.Max.Z - w.Min.Z) * skala);
        float pos;
        Vector2 e;
        if (halt is { } h && NaechstesGleisStueck(bahnen, h) is { } gs)
        {
            // Weisung P12-220: Mitte des Zugs auf der Hoehe des verknuepften Bahnsteigs, in Richtung des naechsten Gleisstuecks
            e = gs.Richtung;
            best = (gs.Punkt - (e * (gesamt / 2)), gs.Punkt + (e * (gesamt / 2)));
            pos = gesamt;
        }
        else
        {
            var l = (best.B - best.A).Length();
            if (l < 1)
            {
                return;
            }
            e = (best.B - best.A) / l;
            // Wagen hintereinander, mittig auf dem Abschnitt, Fahrtrichtung e (Kopf a vorne)
            // W8b (Nico 2a): der Zug haelt am Ostende des Bahnsteigs, der Fussweg zum Bahnhof quert das Gleis im Westen
            pos = e.X > 0 ? l - 0.1f : gesamt + 0.1f;
        }
        foreach (var w in wagen)
        {
            var lang = (w!.Max.Z - w.Min.Z) * skala;
            var vorne = best.A + (e * pos);
            var anker = new Vector3((w.Min.X + w.Max.X) / 2, w.Min.Y, w.Max.Z);
            stuecke.Add(new ModellStueck(w.Name, Lage(w, anker, new Vector3(skala), new Vector3(vorne.X, Hoehe.Schiene - 0.015f, vorne.Y), e)));
            pos -= lang;
        }
    }

    /// <summary>
    /// Weisung P12-220: Mitte des ersten Bahnsteigs (Gebaeude ohne Ort nur aus Bahnsteig-Flaechen) mit Feld
    /// <see cref="Stadtbau.Bahnsteig.BahnhofFeld"/> in der Reihenfolge der Landschaft, sonst null.
    /// </summary>
    public static Vector2? VerknuepfterBahnsteig(IEnumerable<PyObjekt> landschaft, PyObjekt modelle)
    {
        foreach (var g in landschaft.Where(t => Py.Gleich(t["typ"], "gebaeude") && Stadtbau.Bahnsteig.BahnhofVon(t) is not null))
        {
            var teile = Get(modelle, Py.Str(g["modell"]));
            if (Stadtbau.Bahnsteig.Nur(teile) && Stadtbau.Bahnsteig.MitteVon(g, teile) is { } m)
            {
                return m;
            }
        }
        return null;
    }

    /// <summary>Naechster Punkt auf einem Gleis (Lot auf das naechste Stueck) und die Richtung dieses Stuecks.</summary>
    public static (Vector2 Punkt, Vector2 Richtung)? NaechstesGleisStueck(IEnumerable<IReadOnlyList<Vector2>> bahnen, Vector2 p)
    {
        (Vector2, Vector2)? best = null;
        var abstand = float.MaxValue;
        foreach (var b in bahnen)
        {
            for (var i = 0; i + 1 < b.Count; i++)
            {
                var d = b[i + 1] - b[i];
                var l2 = d.LengthSquared();
                if (l2 < 1e-8f)
                {
                    continue;
                }
                var t = Math.Clamp(Vector2.Dot(p - b[i], d) / l2, 0, 1);
                var q = b[i] + (d * t);
                var a = Vector2.Distance(p, q);
                if (a < abstand)
                {
                    abstand = a;
                    best = (q, d / MathF.Sqrt(l2));
                }
            }
        }
        return best;
    }
}
