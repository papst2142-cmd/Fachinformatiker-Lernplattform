using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Ein Ort der 3D-Stadt: Tuer (Mitte der Tuerzelle des Stadt-Rasters), Kasten fuer Tippen
/// (Grundflaeche der Gebaeude mal hoechstem Block) und Punkt fuer das Namensschild darueber.
/// </summary>
public sealed record StadtOrt(string Id, string Name, string Kurz, string Rolle, float TuerX, float TuerZ,
    Vector3 KastenMin, Vector3 KastenMax, Vector3 Schild);

/// <summary>
/// Phase 7, W4 (Weisung P7-9 Punkt 3c): die ganze Stadt aus <c>orte.json</c> und <c>stadt.json</c>
/// als 3D-Szene in eigener Geometrie (bis die CC0-Pakete da sind, Punkt 3d). Landschaft (Boden,
/// Parks, Fluss, Strassen, Bahn, Baeume, Stellwerk) und alle sichtbaren Orte (<see cref="Stadt.Orte"/>)
/// mit ihren sichtbaren Modellteilen (<see cref="StadtModelle"/>, wie Python). Abbildung der
/// 2D-Karte auf 3D: x bleibt x, y der Karte wird z (zum Betrachter), ein Block steht auf der
/// Grundflaeche x..x+w, y..y+t mit der Front bei y+t und ist h hoch; Teile, die Python auf die
/// Front zeichnet (Tor, Schild, Geruest, Kreis), liegen auf dieser Front in der Hoehe
/// Front - y. Nichts davon geht in den Spielstand.
/// </summary>
public sealed class StadtSzene
{
    /// <summary>Breite der Strassen wie landscape_shapes (0.72), Fluss 0.65.</summary>
    public const float StrassenBreite = 0.72f;
    public const float FlussBreite = 0.65f;

    private StadtSzene(IReadOnlyList<Teil> teile, IReadOnlyList<ModellStueck> stuecke, GrafikBibliothek? grafik,
        IReadOnlyList<StadtOrt> orte, string signatur, float breite, float tiefe)
    {
        Teile = teile;
        Stuecke = stuecke;
        Grafik = grafik;
        Orte = orte;
        Signatur = signatur;
        Breite = breite;
        Tiefe = tiefe;
    }

    public IReadOnlyList<Teil> Teile { get; }

    /// <summary>Platzierte CC0-Modelle (leer ohne Grafik-Bibliothek).</summary>
    public IReadOnlyList<ModellStueck> Stuecke { get; }

    public GrafikBibliothek? Grafik { get; }

    /// <summary>Das Dreiecksnetz der ganzen Stadt: eigene Teile und CC0-Modelle.</summary>
    public Netz NetzBauen()
    {
        var netz = Netz.Aus(Teile);
        foreach (var stueck in Stuecke)
        {
            if (Grafik?.Modell(stueck.Name) is { } modell)
            {
                netz.Hinzu(modell, stueck.Lage);
            }
        }
        return netz;
    }

    public IReadOnlyList<StadtOrt> Orte { get; }

    /// <summary>Welche Orte und Modellteile zu sehen sind; aendert sie sich, wird die Szene neu geladen.</summary>
    public string Signatur { get; }

    public float Breite { get; }

    public float Tiefe { get; }

    public StadtOrt? Ort(string id) => Orte.FirstOrDefault(o => o.Id == id);

    /// <summary>Farbe je Rolle wie map_role_color (Grundfarben fisi_theme).</summary>
    public static Vector3 RollenFarbe(string? rolle) => Netz.Farbe(rolle switch
    {
        "bitweiche" => "#22D3EE",
        "kunde" => "#60A5FA",
        "firma" => "#34D399",
        "zuhause" => "#F472B6",
        "filiale" => "#A78BFA",
        _ => "#8B93A1",
    });

    private static Vector3 Mischen(Vector3 a, Vector3 b, float anteil) => (a * (1 - anteil)) + (b * anteil);

    private static readonly Vector3 Weiss = Vector3.One;
    private static readonly Vector3 Schwarz = Vector3.Zero;

    private static class Farben
    {
        public static readonly Vector3 Boden = Netz.Farbe("#9CC48A");
        public static readonly Vector3 Park = Netz.Farbe("#7FB069");
        public static readonly Vector3 Wasser = Netz.Farbe("#4FA8D8");
        public static readonly Vector3 Strasse = Netz.Farbe("#4A4560");
        public static readonly Vector3 Mittellinie = Netz.Farbe("#F4F1FA");
        public static readonly Vector3 Gleisbett = Netz.Farbe("#8E8AA3");
        public static readonly Vector3 Schiene = Netz.Farbe("#3A3550");
        public static readonly Vector3 Bahnsteig = Netz.Farbe("#B9C7EA");
        public static readonly Vector3 Glas = Netz.Farbe("#A5E9F5");
        public static readonly Vector3 Holz = Netz.Farbe("#7A5232");
        public static readonly Vector3 Metall = Netz.Farbe("#8E8AA3");
        public static readonly Vector3 Fenster = Netz.Farbe("#5B6B8C");
        public static readonly Vector3 Licht = Netz.Farbe("#FCE7A6");
        public static readonly Vector3 Tuer = Netz.Farbe("#6E4A2F");
        public static readonly Vector3 Rot = Netz.Farbe("#F87171");
    }

    private static Teil Quader(float x1, float y1, float z1, float x2, float y2, float z2, Vector3 farbe) =>
        new(TeilArt.Quader, x1, y1, z1, x2, y2, z2, farbe);

    private static Teil Band(float x1, float z1, float x2, float z2, float hoehe, float breite, Vector3 farbe) =>
        new(TeilArt.Band, x1, hoehe, z1, x2, breite, z2, farbe);

    private static float Zahl(PyObjekt teil, string name, float vorgabe = 0f) => Py.Hat(teil, name) ? (float)Py.F(teil[name]) : vorgabe;

    private static List<(float X, float Z)> Punkte(object? punkte) =>
        [.. Liste(punkte).Select(p => Liste(p)).Select(p => ((float)Py.F(p[0]), (float)Py.F(p[1])))];

    /// <summary>
    /// Die Stadt fuer einen Spielstand (null = wie ein neuer Spielstand ohne Ereignisse). Mit
    /// <paramref name="grafik"/> stehen statt der eigenen Bloecke, Baeume und Schienen die CC0-Modelle
    /// (Zuordnung je Block in zuordnung.json; Bloecke ohne Zuordnung bleiben eigene Geometrie).
    /// </summary>
    public static StadtSzene Bauen(Stadt stadt, Spielstand? stand, GrafikBibliothek? grafik = null)
    {
        var stuecke = new List<ModellStueck>();
        var inhalt = stadt.Inhalt;
        var welt = Objekt(Objekt(inhalt["orte"])["welt"]);
        var breite = (float)Py.F(welt["breite"]);
        var tiefe = (float)Py.F(welt["hoehe"]);
        var teile = new List<Teil>
        {
            // Boden weit ueber den Rand, damit die geneigte Kamera keinen leeren Himmel zeigt
            Quader(-30, -0.06f, -30, breite + 30, 0f, tiefe + 30, Mischen(Farben.Boden, Schwarz, 0.12f)),
            Quader(0, -0.05f, 0, breite, 0.001f, tiefe, Farben.Boden),
        };
        var landschaft = ListeOderLeer(welt, "landschaft").Select(Objekt).ToList();
        foreach (var art in (string[])["park", "fluss", "strasse", "bahn"])
        {
            foreach (var item in landschaft.Where(t => Py.Gleich(t["typ"], art)))
            {
                switch (art)
                {
                    case "park":
                    {
                        float x = Zahl(item, "x"), z = Zahl(item, "y");
                        teile.Add(Quader(x, 0f, z, x + Zahl(item, "w"), 0.004f, z + Zahl(item, "h"), Farben.Park));
                        break;
                    }
                    case "fluss":
                        Linie(teile, Punkte(item["punkte"]), 0.006f, FlussBreite, Farben.Wasser);
                        break;
                    case "strasse":
                        Linie(teile, Punkte(item["punkte"]), 0.01f, StrassenBreite, Farben.Strasse);
                        break;
                    case "bahn":
                        Gleis(teile, stuecke, grafik, Punkte(item["punkte"]));
                        break;
                }
            }
        }
        // Mittellinien und Fusswege der Stadt (Zugaenge von der Tuer zur Strasse)
        foreach (var item in landschaft.Where(t => Py.Gleich(t["typ"], "strasse")))
        {
            foreach (var (x1, z1, x2, z2) in Striche(Punkte(item["punkte"]), 0.2f, 0.25f))
            {
                teile.Add(Band(x1, z1, x2, z2, 0.014f, 0.04f, Farben.Mittellinie));
            }
        }
        foreach (var (_, zugang) in Objekt(stadt.Daten["zugaenge"]))
        {
            Linie(teile, Punkte(Objekt(zugang)["weg"]), 0.008f, 0.5f, Mischen(Farben.Strasse, Weiss, 0.45f));
        }
        foreach (var item in landschaft.Where(t => Py.Gleich(t["typ"], "baum")))
        {
            var r = Zahl(item, "r", 0.45f);
            foreach (var (x, z) in Punkte(item["punkte"]))
            {
                Baum(teile, stuecke, grafik, x, z, r);
            }
        }

        var k = StadtModelle.Kontext(stand, inhalt);
        var modelle = StadtModelle.Modelle(inhalt, stadt.Daten);
        var signatur = new List<string>();
        foreach (var item in landschaft.Where(t => Py.Gleich(t["typ"], "gebaeude")))
        {
            var modell = Py.Str(item["modell"]);
            var sichtbar = StadtModelle.Teile(modelle, modell, k);
            var farbe = Py.Hat(item, "farbe") ? Netz.Farbe(Py.Str(item["farbe"])) : RollenFarbe(null);
            Modell(teile, stuecke, grafik, modell, sichtbar, Zahl(item, "x"), Zahl(item, "y"), farbe, modell);
            signatur.Add(modell + ":" + string.Join(",", sichtbar.Select(t => t.Nr)));
        }
        var orte = new List<StadtOrt>();
        foreach (var ort in stadt.Orte(stand))
        {
            var id = Py.Str(ort["id"]);
            var modell = Py.Str(ort["modell"]);
            var sichtbar = StadtModelle.Teile(modelle, modell, k);
            float ox = (float)Py.F(ort["x"]), oz = (float)Py.F(ort["y"]);
            var rolle = ort["rolle"] as string ?? "";
            var vorher = stuecke.Count;
            var (min, max) = Modell(teile, stuecke, grafik, modell, sichtbar, ox, oz, RollenFarbe(rolle), id);
            var mitGrafik = stuecke.Count > vorher;
            var (tx, tz) = stadt.Tuer(id) is { } zelle
                ? ((float)((zelle.Item1 + 0.5) * stadt.Zelle), (float)((zelle.Item2 + 0.5) * stadt.Zelle))
                : ((min.X + max.X) / 2, max.Z);
            // Tuer: kleines Rechteck, wo der Fussweg die Front trifft; liegt die Tuer hinten (Moebelhaus:
            // Zugang von der Strasse noerdlich), sitzt sie an der Rueckseite
            var vorne = tz >= (min.Z + max.Z) / 2;
            var seite = vorne ? max.Z : min.Z;
            // (CC0-Haeuser haben eigene Tueren)
            if (!mitGrafik)
            {
                teile.Add(Quader(tx - 0.22f, 0f, vorne ? seite - 0.02f : seite - 0.03f, tx + 0.22f, 0.7f, vorne ? seite + 0.03f : seite + 0.02f, Farben.Tuer));
            }
            var schild = new Vector3((min.X + max.X) / 2, max.Y + 0.55f, (min.Z + max.Z) / 2);
            orte.Add(new StadtOrt(id, ort["name"] as string ?? id, ort["kurz"] as string ?? ort["name"] as string ?? id, rolle,
                tx, tz, min, max, schild));
            signatur.Add(id + ":" + string.Join(",", sichtbar.Select(t => t.Nr)));
        }
        if (grafik is not null)
        {
            Zug(stuecke, grafik, landschaft.Where(t => Py.Gleich(t["typ"], "bahn")).Select(t => Punkte(t["punkte"])));
        }
        return new StadtSzene(teile, stuecke, grafik, orte, string.Join(";", signatur), breite, tiefe);
    }

    private static void Linie(List<Teil> teile, List<(float X, float Z)> p, float hoehe, float breite, Vector3 farbe)
    {
        for (var i = 0; i + 1 < p.Count; i++)
        {
            // um die halbe Breite verlaengert, damit Knicke und Kreuzungen geschlossen sind
            var (x1, z1) = p[i];
            var (x2, z2) = p[i + 1];
            var d = new Vector2(x2 - x1, z2 - z1);
            if (d.LengthSquared() < 1e-12f)
            {
                continue;
            }
            var e = Vector2.Normalize(d) * (breite / 2);
            teile.Add(Band(x1 - e.X, z1 - e.Y, x2 + e.X, z2 + e.Y, hoehe, breite, farbe));
        }
    }

    /// <summary>_along/_dashes: Striche der Laenge <paramref name="strich"/> mit Luecke entlang der Linie.</summary>
    public static List<(float X1, float Z1, float X2, float Z2)> Striche(List<(float X, float Z)> p, float strich, float luecke)
    {
        var liste = new List<(float, float, float, float)>();
        var rest = 0f;
        for (var i = 0; i + 1 < p.Count; i++)
        {
            var (x1, z1) = p[i];
            var (x2, z2) = p[i + 1];
            var laenge = MathF.Sqrt(((x2 - x1) * (x2 - x1)) + ((z2 - z1) * (z2 - z1)));
            if (laenge <= 0)
            {
                continue;
            }
            float dx = (x2 - x1) / laenge, dz = (z2 - z1) / laenge;
            var pos = rest;
            while (pos <= laenge)
            {
                var x = x1 + (dx * pos);
                var z = z1 + (dz * pos);
                liste.Add((x, z, x + (dx * strich), z + (dz * strich)));
                pos += strich + luecke;
            }
            rest = pos - laenge;
        }
        return liste;
    }

    /// <summary>Breite des Gleises in der Welt; die Schienen des Train Kit (1 breit, 4 lang) werden darauf skaliert.</summary>
    public const float GleisBreite = 0.42f;

    /// <summary>
    /// Lage eines Modells: Kasten des Modells (min) auf <paramref name="ort"/>, skaliert mit
    /// <paramref name="mass"/>, Vorderseite (+Z) in Richtung <paramref name="richtung"/> (Karte x/z).
    /// </summary>
    public static Matrix4x4 Lage(GrafikModell m, Vector3 anker, Vector3 mass, Vector3 ort, Vector2 richtung)
    {
        var drehung = Drehung(richtung);
        return Matrix4x4.CreateTranslation(-anker) * Matrix4x4.CreateScale(mass) * drehung * Matrix4x4.CreateTranslation(ort);
    }

    /// <summary>Drehung um die Hochachse, die +Z auf die Richtung (x, z) legt.</summary>
    public static Matrix4x4 Drehung(Vector2 richtung)
    {
        if (richtung.LengthSquared() < 1e-12f)
        {
            return Matrix4x4.Identity;
        }
        var d = Vector2.Normalize(richtung);
        var winkel = MathF.Atan2(d.X, d.Y);
        var m = Matrix4x4.CreateRotationY(winkel);
        var probe = Vector3.TransformNormal(Vector3.UnitZ, m);
        // Drehsinn von System.Numerics nicht voraussetzen: sonst die Gegenrichtung
        return MathF.Abs(probe.X - d.X) + MathF.Abs(probe.Z - d.Y) < 1e-3f ? m : Matrix4x4.CreateRotationY(-winkel);
    }

    private static void Gleis(List<Teil> teile, List<ModellStueck> stuecke, GrafikBibliothek? grafik, List<(float X, float Z)> p)
    {
        Linie(teile, p, 0.006f, GleisBreite, Farben.Gleisbett);
        if (grafik?.Modell("railroad-straight") is { } schiene)
        {
            // Train Kit: ein Stueck je Abschnitt (gestreckt auf die Laenge), auf dem eigenen Gleisbett
            var laenge = schiene.Max.Z - schiene.Min.Z;
            var skala = GleisBreite / (schiene.Max.X - schiene.Min.X);
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var d = new Vector2(p[i + 1].X - p[i].X, p[i + 1].Z - p[i].Z);
                var l = d.Length();
                if (l < 1e-4f)
                {
                    continue;
                }
                var n = Math.Max(1, (int)MathF.Round(l / (laenge * skala)));
                var e = d / l;
                for (var k = 0; k < n; k++)
                {
                    var start = new Vector2(p[i].X, p[i].Z) + (e * (k * l / n));
                    var anker = new Vector3((schiene.Min.X + schiene.Max.X) / 2, schiene.Min.Y, schiene.Min.Z);
                    stuecke.Add(new ModellStueck(schiene.Name, Lage(schiene, anker, new Vector3(skala, skala, l / n / laenge), new Vector3(start.X, 0.004f, start.Y), e)));
                }
            }
            return;
        }
        foreach (var (x1, z1, x2, z2) in Striche(p, 0.05f, 0.12f))
        {
            // Schwellen quer zur Linie
            var d = Vector2.Normalize(new Vector2(x2 - x1, z2 - z1));
            var q = new Vector2(-d.Y, d.X) * 0.17f;
            teile.Add(Band(x1 - q.X, z1 - q.Y, x1 + q.X, z1 + q.Y, 0.012f, 0.06f, Mischen(Farben.Holz, Weiss, 0.2f)));
        }
        for (var seite = -1; seite <= 1; seite += 2)
        {
            var versetzt = new List<(float X, float Z)>();
            for (var i = 0; i < p.Count; i++)
            {
                var vor = p[Math.Min(i + 1, p.Count - 1)];
                var nach = p[Math.Max(i - 1, 0)];
                var d = new Vector2(vor.X - nach.X, vor.Z - nach.Z);
                d = d.LengthSquared() < 1e-12f ? Vector2.UnitX : Vector2.Normalize(d);
                versetzt.Add((p[i].X + (-d.Y * 0.09f * seite), p[i].Z + (d.X * 0.09f * seite)));
            }
            Linie(teile, versetzt, 0.02f, 0.035f, Farben.Schiene);
        }
    }

    private static void Baum(List<Teil> teile, List<ModellStueck> stuecke, GrafikBibliothek? grafik, float x, float z, float r)
    {
        // Suburban-Baum (gross/klein im Wechsel nach Lage), so hoch wie der eigene Baum (0,4 + 2,2 r)
        var name = ((int)MathF.Round((x * 7) + (z * 3)) & 1) == 0 ? "tree_large" : "tree_small";
        if (grafik?.Modell(name) is { } baum)
        {
            var s = (0.4f + (2.2f * r)) / (baum.Max.Y - baum.Min.Y) * 1.15f;
            var anker = new Vector3((baum.Min.X + baum.Max.X) / 2, baum.Min.Y, (baum.Min.Z + baum.Max.Z) / 2);
            stuecke.Add(new ModellStueck(name, Lage(baum, anker, new Vector3(s), new Vector3(x, 0, z), Vector2.UnitY)));
            return;
        }
        teile.Add(Quader(x - 0.07f, 0, z - 0.07f, x + 0.07f, 0.5f, z + 0.07f, Farben.Holz));
        teile.Add(Quader(x - r, 0.4f, z - r, x + r, 0.4f + (1.6f * r), z + r, Netz.Farbe("#34D399") * 0.8f));
        teile.Add(Quader(x - (r * 0.6f), 0.4f + (1.6f * r), z - (r * 0.6f), x + (r * 0.6f), 0.4f + (2.2f * r), z + (r * 0.6f), Netz.Farbe("#34D399") * 0.9f));
    }

    /// <summary>Fester "Wuerfel" wie _lit: brennt in diesem Fenster Licht? (SHA-256 von "seed|nr")</summary>
    public static bool Licht(string seed, int nr, double anteil)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed + "|" + nr.ToString(CultureInfo.InvariantCulture)));
        var wert = (hash[0] << 16) | (hash[1] << 8) | hash[2];
        return wert / (double)(1 << 24) < anteil;
    }

    private sealed record Block(float X1, float Z1, float X2, float Z2, float H);

    /// <summary>Ein Modell an (ox, oz) bauen; Rueckgabe: Kasten (ohne Baeume) fuer Tippen und Schild.</summary>
    private static (Vector3 Min, Vector3 Max) Modell(List<Teil> teile, List<ModellStueck> stuecke, GrafikBibliothek? grafik, string modellName,
        List<(int Nr, PyObjekt Teil)> sichtbar, float ox, float oz, Vector3 ton, string seed)
    {
        var bloecke = new List<Block>();
        foreach (var (_, teil) in sichtbar.Where(t => Py.Gleich(t.Teil["art"], "block")))
        {
            float x = ox + Zahl(teil, "x"), z = oz + Zahl(teil, "y");
            bloecke.Add(new Block(x, z, x + Zahl(teil, "w"), z + Zahl(teil, "t"), Zahl(teil, "h")));
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
            float x = ox + Zahl(teil, "x"), y = oz + Zahl(teil, "y");
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
                        var hoch = GrafikBlock(stuecke, gm, x, y, w, t, h);
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
                        // Fensterraster wie model_part_shapes (Zeilen je 0,7, Spalten je 0,8), auf der Front
                        var zeilen = Math.Max(1, (int)(h / 0.7f));
                        var spalten = Math.Max(1, (int)(w / 0.8f));
                        float zelleB = (w - 0.3f) / spalten, zelleH = (h - 0.2f) / zeilen;
                        var anteil = Zahl(teil, "licht", 0.5f);
                        var lichtfarbe = teil["lichtfarbe"] is string lf && lf.Length > 0 ? Netz.Farbe(lf) : Farben.Licht;
                        for (var zeile = 0; zeile < zeilen; zeile++)
                        {
                            for (var spalte = 0; spalte < spalten; spalte++)
                            {
                                var oben = h - 0.2f - (zeile * zelleH);
                                var unten = oben - (zelleH - 0.3f);
                                if (oben > wandHoehe - 0.05f || unten < 0.1f)
                                {
                                    continue;
                                }
                                var fx = x + 0.25f + (spalte * zelleB);
                                var an = Licht(seed + "|" + nr, (zeile * spalten) + spalte, anteil);
                                teile.Add(Quader(fx, unten, y + t, fx + zelleB - 0.25f, oben, y + t + 0.03f, an ? lichtfarbe : Farben.Fenster));
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
                        teile.Add(Quader(x, 0, y, x + w, 0.05f, y + h, eigen));
                    }
                    Kasten(x, 0, y, x + w, 0.05f, y + h);
                    break;
                }
                case "schild":
                {
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
                    if (b is not null)
                    {
                        var mitte = Math.Min(front - y, b.H + 0.3f);
                        teile.Add(Quader(x - r, mitte - r, front, x + r, mitte + r, front + 0.05f, eigen));
                    }
                    else
                    {
                        teile.Add(Quader(x - r, 0, y - r, x + r, 0.06f, y + r, eigen));
                    }
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
                    Baum(teile, stuecke, grafik, x, y, Zahl(teil, "r", 0.4f));
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
        return (min, max);
    }

    /// <summary>
    /// Ein Block aus einem CC0-Modell: so viele Kopien nebeneinander, dass jede etwa die Form des
    /// Modells behaelt, zusammen genau die Grundflaeche w x t (Front bei y + t). Die Hoehe liegt
    /// zwischen der Python-Hoehe h und der natuerlichen Hoehe des Modells (Mittel), damit Fenster und
    /// Tueren nicht platt gedrueckt wirken. Rueckgabe: Hoehe in der Welt.
    /// </summary>
    public static float GrafikBlock(List<ModellStueck> stuecke, GrafikModell gm, float x, float y, float w, float t, float h)
    {
        var mass = gm.Max - gm.Min;
        // erst ab 1,5-facher Breite eine zweite Kopie (sonst wirkt ein Laden wie zwei Haeuser)
        var anzahl = Math.Max(1, (int)((w / t) / (mass.X / mass.Z) / 1.5f));
        var sx = w / anzahl / mass.X;
        var sz = t / mass.Z;
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
    private static void Zug(List<ModellStueck> stuecke, GrafikBibliothek grafik, IEnumerable<List<(float X, float Z)>> bahnen)
    {
        var wagen = new[] { "train-electric-city-a", "train-electric-city-b", "train-electric-city-c" }.Select(grafik.Modell).ToList();
        if (wagen.Any(w => w is null))
        {
            return;
        }
        (Vector2 A, Vector2 B) best = default;
        foreach (var p in bahnen)
        {
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var a = new Vector2(p[i].X, p[i].Z);
                var b = new Vector2(p[i + 1].X, p[i + 1].Z);
                if ((b - a).Length() > (best.B - best.A).Length())
                {
                    best = (a, b);
                }
            }
        }
        var l = (best.B - best.A).Length();
        if (l < 1)
        {
            return;
        }
        var e = (best.B - best.A) / l;
        var skala = GleisBreite / (wagen[0]!.Max.X - wagen[0]!.Min.X) * 1.15f;
        var gesamt = wagen.Sum(w => (w!.Max.Z - w.Min.Z) * skala);
        // Wagen hintereinander, mittig auf dem Abschnitt, Fahrtrichtung e (Kopf a vorne)
        var pos = ((l - gesamt) / 2) + gesamt;
        foreach (var w in wagen)
        {
            var lang = (w!.Max.Z - w.Min.Z) * skala;
            var vorne = best.A + (e * pos);
            var anker = new Vector3((w.Min.X + w.Max.X) / 2, w.Min.Y, w.Max.Z);
            stuecke.Add(new ModellStueck(w.Name, Lage(w, anker, new Vector3(skala), new Vector3(vorne.X, 0.03f, vorne.Y), e)));
            pos -= lang;
        }
    }

    /// <summary>_part_fill: benannte Farben der Modellteile, sonst Hex-Wert.</summary>
    private static Vector3 Teilfarbe(string name, Vector3 ton) => name switch
    {
        "bahnsteig" => Farben.Bahnsteig,
        "glas" => Farben.Glas,
        "holz" => Farben.Holz,
        "technik" => Farben.Metall,
        _ when name.StartsWith('#') => Netz.Farbe(name),
        _ => ton,
    };

    /// <summary>
    /// Ort unter einem Strahl (Kamera durch den getippten Bildpunkt): naechster Kasten, den der
    /// Strahl trifft (etwas vergroessert, damit kleine Haeuser am Handy treffbar sind), sonst der
    /// Ort, dessen Tuer naeher als <paramref name="tuerWeite"/> am getroffenen Bodenpunkt liegt.
    /// </summary>
    public StadtOrt? OrtBei(Vector3 start, Vector3 richtung, float tuerWeite = 1.2f)
    {
        StadtOrt? bester = null;
        var besterT = float.MaxValue;
        foreach (var ort in Orte)
        {
            var rand = new Vector3(0.3f, 0.3f, 0.3f);
            if (Treffer(start, richtung, ort.KastenMin - rand, ort.KastenMax + rand) is { } t && t < besterT)
            {
                besterT = t;
                bester = ort;
            }
        }
        if (bester is not null || MathF.Abs(richtung.Y) < 1e-6f)
        {
            return bester;
        }
        var boden = -start.Y / richtung.Y;
        if (boden < 0)
        {
            return null;
        }
        var p = start + (richtung * boden);
        return Orte.Select(o => (Ort: o, Weite: MathF.Sqrt(((o.TuerX - p.X) * (o.TuerX - p.X)) + ((o.TuerZ - p.Z) * (o.TuerZ - p.Z)))))
            .Where(o => o.Weite < tuerWeite).OrderBy(o => o.Weite).Select(o => o.Ort).FirstOrDefault();
    }

    /// <summary>Strahl gegen achsenparallelen Kasten (Platten-Verfahren); Abstand oder null.</summary>
    public static float? Treffer(Vector3 start, Vector3 richtung, Vector3 min, Vector3 max)
    {
        float tMin = 0, tMax = float.MaxValue;
        for (var achse = 0; achse < 3; achse++)
        {
            var s = start[achse];
            var r = richtung[achse];
            if (MathF.Abs(r) < 1e-9f)
            {
                if (s < min[achse] || s > max[achse])
                {
                    return null;
                }
                continue;
            }
            var t1 = (min[achse] - s) / r;
            var t2 = (max[achse] - s) / r;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax)
            {
                return null;
            }
        }
        return tMin;
    }
}
