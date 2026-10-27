using System.Globalization;
using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Mehr Vielfalt bei Baeumen und Gruen (Sammelliste 129, Phase 8 W7, Nico 4c). Baumarten und -groessen aus dem
/// KayKit Forest Nature Pack (ohne die Baeume mit Wuerfelkrone), dazu Buesche, Graeser, Steine und Blumenbeete in den Parks, an den Strassenraendern
/// und auf freien Flaechen (Hoefe). Alles nur Darstellung, feste Verteilung (SHA-256 der Lage, kein Zufall, keine
/// Uhr), nie auf Strasse, Fussweg, Gleis, Wasser, Stehplatz, Haus oder einem Platz der Festzeit-Deko.
/// </summary>
public static class Gruen
{
    /// <summary>
    /// Baumarten (Name in modelle.bin, Hoehenfaktor): Rundkrone, Schirmkrone, Tanne und die beiden bisherigen
    /// Baeume; ohne Wuerfelkrone (Nico 4c, Weisung P8-14).
    /// </summary>
    public static readonly (string Name, float Faktor)[] Baeume =
    [
        ("wd-tree_1_a", 0.9f), ("wd-tree_3_a", 0.85f), ("wd-tree_4_a", 1.1f), ("tree_large", 1.0f), ("tree_small", 0.9f),
    ];

    private static readonly string[] Buesche = ["wd-bush_1_a", "wd-bush_1_b", "wd-bush_2_b", "wd-bush_4_a", "wd-bush_4_c", "wd-bush_3_a"];
    private static readonly string[] Graeser = ["wd-grass_1_a", "wd-grass_1_b", "wd-grass_2_a", "wd-grass_2_b"];
    private static readonly string[] Steine = ["wd-rock_1_a", "wd-rock_1_d", "wd-rock_3_a", "wd-rock_3_g", "wd-rock_2_c"];
    /// <summary>Alle Modelle des Gruens (Baumarten, Buesche, Graeser, Steine).</summary>
    public static IEnumerable<string> Modelle => Baeume.Select(b => b.Name).Concat(Buesche).Concat(Graeser).Concat(Steine);

    private static readonly string[] Bluetenfarben = ["#F472B6", "#FDE047", "#F87171", "#A78BFA", "#FB923C", "#F8FAFC", "#60A5FA"];

    /// <summary>Fester "Wuerfel" 0..1 fuer eine Lage (SHA-256 wie StadtSzene.Licht).</summary>
    public static float Wurf(string art, float x, float z, int nr = 0)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"{art}|{x:F2}|{z:F2}|{nr}");
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return ((hash[0] << 16) | (hash[1] << 8) | hash[2]) / (float)(1 << 24);
    }

    /// <summary>Ein Baum an (x, z) mit Kronenradius r: Art und Groesse nach der Lage; false ohne das Modell.</summary>
    public static bool Baum(List<ModellStueck> stuecke, GrafikBibliothek grafik, float x, float z, float r, float groesse = 1f)
    {
        var (name, faktor) = Baeume[(int)(Wurf("baum", x, z) * Baeume.Length) % Baeume.Length];
        if (grafik.Modell(name) is not { } m)
        {
            return false;
        }
        var hoehe = (0.4f + (2.2f * r)) * 1.15f * faktor * groesse * (0.85f + (0.3f * Wurf("baumgroesse", x, z)));
        var s = hoehe / m.Max.Y;
        var winkel = Wurf("baumdreh", x, z) * MathF.Tau;
        var anker = name.StartsWith("wd-", StringComparison.Ordinal) ? Vector3.Zero : new Vector3((m.Min.X + m.Max.X) / 2, m.Min.Y, (m.Min.Z + m.Max.Z) / 2);
        stuecke.Add(new ModellStueck(name, StadtSzene.Lage(m, anker, new Vector3(s), new Vector3(x, 0, z), new Vector2(MathF.Sin(winkel), MathF.Cos(winkel)))));
        return true;
    }

    /// <summary>Ergebnis: Kleingruen (eigenes Netz, nur in der Naehe gezeichnet), gezaehlte Stuecke und Hindernisse.</summary>
    public sealed class Ergebnis
    {
        public List<ModellStueck> Stuecke { get; } = [];
        public List<Teil> Teile { get; } = [];

        /// <summary>Parkbaeume (im Netz der Stadt, immer gezeichnet, nicht im Kleingruen).</summary>
        public List<ModellStueck> Baeume { get; } = [];
        public Dictionary<string, int> Anzahl { get; } = [];

        /// <summary>Alle gesetzten Stuecke mit Art, Mitte und Radius (auch Gras).</summary>
        public List<(string Art, float X, float Z, float R)> Plaetze { get; } = [];

        /// <summary>Hindernisse des freien Gehens: Buesche, Steine, Beete und Parkbaeume (nicht Gras).</summary>
        public IEnumerable<(float X, float Z, float R)> Hindernisse =>
            Plaetze.Where(p => p.Art != "gras").Select(p => (p.X, p.Z, p.R));
    }

    /// <summary>
    /// Verteilt Gruen: Parks (Raster 0,8, Baeume, Buesche, Graeser, Steine, Beete), Strassenraender (alle 1,3) und
    /// freie Flaechen (Raster 1,6). <paramref name="frei"/> prueft einen Kreis (x, z, r).
    /// </summary>
    public static Ergebnis Verteilen(GrafikBibliothek grafik, float breite, float tiefe, IEnumerable<(float X, float Z, float W, float H)> parks,
        IEnumerable<IReadOnlyList<Vector2>> strassen, Func<float, float, float, bool> frei, List<(Vector2 Mitte, float R)> baeume)
    {
        var erg = new Ergebnis();
        var belegt = new List<(Vector2 P, float R)>();
        bool Platz(float x, float z, float r) =>
            x - r > 0.2f && z - r > 0.2f && x + r < breite - 0.2f && z + r < tiefe - 0.2f
            && frei(x, z, r) && belegt.All(b => Vector2.Distance(b.P, new Vector2(x, z)) >= b.R + r + 0.03f)
            && baeume.All(b => Vector2.Distance(b.Mitte, new Vector2(x, z)) >= (b.R * 0.5f) + r);
        void Zaehlen(string art) => erg.Anzahl[art] = erg.Anzahl.GetValueOrDefault(art) + 1;
        void Setzen(string art, float x, float z)
        {
            switch (art)
            {
                case "baum":
                    if (Platz(x, z, 0.35f))
                    {
                        Baum(erg.Baeume, grafik, x, z, 0.38f, 0.8f);
                        baeume.Add((new Vector2(x, z), 0.38f));
                        belegt.Add((new Vector2(x, z), 0.35f));
                        // Stamm als Hindernis (die Krone ueberragt den Weg)
                        erg.Plaetze.Add((art, x, z, 0.12f));
                        Zaehlen(art);
                    }
                    break;
                case "busch":
                    Modell(Buesche, art, x, z, 0.14f, 0.12f + (0.12f * Wurf("bg", x, z)));
                    break;
                case "gras":
                    // Bueschel aus drei Halmgruppen
                    Modell(Graeser, art, x, z, 0.07f, 0.16f + (0.08f * Wurf("gg", x, z)));
                    Modell(Graeser, art, x + 0.09f, z + 0.05f, 0.05f, 0.12f + (0.06f * Wurf("gg2", x, z)));
                    Modell(Graeser, art, x - 0.07f, z + 0.08f, 0.05f, 0.1f + (0.06f * Wurf("gg3", x, z)));
                    break;
                case "stein":
                    Modell(Steine, art, x, z, 0.1f, 0.08f + (0.1f * Wurf("sg", x, z)));
                    break;
                case "beet":
                    if (Platz(x, z, 0.18f))
                    {
                        Beet(erg.Teile, x, z);
                        belegt.Add((new Vector2(x, z), 0.18f));
                        erg.Plaetze.Add((art, x, z, 0.18f));
                        Zaehlen(art);
                    }
                    break;
            }
        }
        void Modell(string[] namen, string art, float x, float z, float r, float hoehe)
        {
            var name = namen[(int)(Wurf(art + "art", x, z) * namen.Length) % namen.Length];
            if (grafik.Modell(name) is not { } m || !Platz(x, z, r))
            {
                return;
            }
            var s = hoehe / Math.Max(0.05f, m.Max.Y);
            var winkel = Wurf(art + "dreh", x, z) * MathF.Tau;
            erg.Stuecke.Add(new ModellStueck(name, StadtSzene.Lage(m, Vector3.Zero, new Vector3(s), new Vector3(x, 0.012f, z), new Vector2(MathF.Sin(winkel), MathF.Cos(winkel)))));
            belegt.Add((new Vector2(x, z), r));
            erg.Plaetze.Add((art, x, z, r));
            Zaehlen(art);
        }
        // Parks
        foreach (var (px, pz, pw, ph) in parks)
        {
            for (var gx = px + 0.4f; gx < px + pw - 0.2f; gx += 0.8f)
            {
                for (var gz = pz + 0.4f; gz < pz + ph - 0.2f; gz += 0.8f)
                {
                    var x = gx + ((Wurf("pjx", gx, gz) - 0.5f) * 0.5f);
                    var z = gz + ((Wurf("pjz", gx, gz) - 0.5f) * 0.5f);
                    var w = Wurf("park", gx, gz);
                    Setzen(w < 0.1f ? "baum" : w < 0.38f ? "busch" : w < 0.62f ? "gras" : w < 0.72f ? "stein" : w < 0.88f ? "beet" : "", x, z);
                }
            }
        }
        // Strassenraender, beide Seiten
        foreach (var strasse in strassen)
        {
            foreach (var seite in (float[])[-1f, 1f])
            {
                foreach (var (ort, richtung) in Kurve.Entlang(strasse, 1.3f, 0.65f))
                {
                    var quer = new Vector2(-richtung.Y, richtung.X) * seite;
                    var p = ort + (quer * ((StadtSzene.StrassenBreite / 2) + 0.22f));
                    var w = Wurf("rand", p.X, p.Y);
                    Setzen(w < 0.5f ? "gras" : w < 0.75f ? "busch" : w < 0.9f ? "beet" : "", p.X, p.Y);
                }
            }
        }
        // freie Flaechen (Hoefe, Wiesen)
        for (var gx = 0.8f; gx < breite; gx += 1.6f)
        {
            for (var gz = 0.8f; gz < tiefe; gz += 1.6f)
            {
                var x = gx + ((Wurf("hjx", gx, gz) - 0.5f) * 0.8f);
                var z = gz + ((Wurf("hjz", gx, gz) - 0.5f) * 0.8f);
                var w = Wurf("hof", gx, gz);
                Setzen(w < 0.18f ? "gras" : w < 0.3f ? "busch" : w < 0.36f ? "stein" : "", x, z);
            }
        }
        return erg;
    }

    /// <summary>Blumenbeet: Erdflaeche mit fuenf Blumen (Stiel und Bluete), Farben nach der Lage.</summary>
    public static void Beet(List<Teil> teile, float x, float z)
    {
        var erde = Netz.Farbe("#6B4F3A");
        var gruen = Netz.Farbe("#3F8F4A");
        teile.Add(new Teil(TeilArt.Quader, x - 0.16f, 0.012f, z - 0.11f, x + 0.16f, 0.03f, z + 0.11f, erde));
        for (var i = 0; i < 5; i++)
        {
            var bx = x - 0.11f + (i * 0.055f);
            var bz = z + ((i % 2 == 0 ? -1 : 1) * 0.045f);
            var farbe = Netz.Farbe(Bluetenfarben[(int)(Wurf("bluete", x, z, i) * Bluetenfarben.Length) % Bluetenfarben.Length]);
            teile.Add(new Teil(TeilArt.Quader, bx - 0.006f, 0.03f, bz - 0.006f, bx + 0.006f, 0.075f, bz + 0.006f, gruen));
            teile.Add(new Teil(TeilArt.Quader, bx - 0.02f, 0.072f, bz - 0.02f, bx + 0.02f, 0.095f, bz + 0.02f, farbe));
        }
    }
}
