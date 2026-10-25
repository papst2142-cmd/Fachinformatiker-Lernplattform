using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Eine begehbare Flaeche (Stadt oder Innenraum): Teile, Wegsuche, Startpunkt, Groesse.</summary>
public sealed record ProbeFlaeche(string Id, IReadOnlyList<Teil> Teile, Wegsuche Wege, float StartX, float StartZ,
    float Breite, float Tiefe);

/// <summary>
/// Phase 7, W1 (Stichprobe, Plan Abschnitt 4), seit W4 nur noch der Innenraum: das Buero aus
/// <c>gebaeude.json</c> (Raeume, Waende, Deko, Raster wie fisi_game._base_grid) mit freier Wegsuche,
/// dazu Figur, Zielmarke und Farben. Die Stadt baut seit W4 <see cref="StadtSzene"/> aus dem
/// Stadt-Kern. Nichts wird gespeichert.
/// </summary>
public static class ProbeWelt
{
    // Raster wie fisi_game (WALK_STEP, BODY_RADIUS)
    public const double InnenSchritt = 0.25;
    public const double Koerper = 0.3;
    public const float WandInnen = 0.16f;

    public const string Stadt = "stadt";
    public const string Buero = "buero";

    /// <summary>Das Buero aus <c>gebaeude.json</c> (<paramref name="lesen"/>: Name ohne ".json" unter inhalte/spiel).</summary>
    public static ProbeFlaeche BueroBauen(Func<string, byte[]> lesen)
    {
        using var gebaeude = JsonDocument.Parse(lesen("gebaeude"));
        return BueroBauen(gebaeude.RootElement);
    }

    private static Teil Quader(float x1, float y1, float z1, float x2, float y2, float z2, Vector3 farbe) =>
        new(TeilArt.Quader, x1, y1, z1, x2, y2, z2, farbe);

    private static float Zahl(JsonElement e, string name, float vorgabe = 0f) =>
        e.TryGetProperty(name, out var w) && w.ValueKind == JsonValueKind.Number ? (float)w.GetDouble() : vorgabe;

    private static readonly string[] BegehbareDeko = ["teppich", "fussmatte", "whiteboard", "markierung", "bild", "wimpel", "kamera"];

    /// <summary>Buero aus gebaeude.json: Boden je Raum, niedrige Waende mit Tueroeffnungen, Deko als Kloetze.</summary>
    private static ProbeFlaeche BueroBauen(JsonElement g)
    {
        var breite = Zahl(g, "breite");
        var hoehe = Zahl(g, "hoehe");
        var flur = g.GetProperty("flur");
        var flurY = Zahl(flur, "y");
        var teile = new List<Teil>
        {
            Quader(-0.5f, -0.06f, -0.5f, breite + 0.5f, 0f, hoehe + 0.5f, Farben.Wiese),
            Quader(0, 0, 0, breite, 0.02f, hoehe, Farben.Flur),
        };
        var waende = new List<(float X1, float Y1, float X2, float Y2)>();
        const float half = WandInnen / 2;
        void Waagrecht(float x1, float x2, float y, (float, float)? luecke)
        {
            if (luecke is { } l)
            {
                waende.Add((x1, y - half, l.Item1, y + half));
                waende.Add((l.Item2, y - half, x2, y + half));
            }
            else
            {
                waende.Add((x1, y - half, x2, y + half));
            }
        }
        void Senkrecht(float x, float y1, float y2, (float, float)? luecke)
        {
            if (luecke is { } l)
            {
                waende.Add((x - half, y1, x + half, l.Item1));
                waende.Add((x - half, l.Item2, x + half, y2));
            }
            else
            {
                waende.Add((x - half, y1, x + half, y2));
            }
        }
        var deko = new List<JsonElement>();
        foreach (var raum in g.GetProperty("raeume").EnumerateArray())
        {
            float x = Zahl(raum, "x"), y = Zahl(raum, "y"), w = Zahl(raum, "w"), h = Zahl(raum, "h");
            (float, float)? luecke = null;
            var seite = y + h <= flurY ? "s" : "n";
            if (raum.TryGetProperty("tuer", out var tuer) && tuer.ValueKind == JsonValueKind.Object)
            {
                luecke = (Zahl(tuer, "von"), Zahl(tuer, "bis"));
                if (tuer.TryGetProperty("seite", out var s))
                {
                    seite = s.GetString() ?? seite;
                }
            }
            var boden = raum.TryGetProperty("boden", out var b) ? b.GetString() : null;
            teile.Add(Quader(x, 0, y, x + w, 0.03f, y + h, boden == "parkett" ? Farben.Parkett : Farben.Boden));
            Waagrecht(x, x + w, y, seite == "n" ? luecke : null);
            Waagrecht(x, x + w, y + h, seite == "s" ? luecke : null);
            Senkrecht(x, y, y + h, seite == "w" ? luecke : null);
            Senkrecht(x + w, y, y + h, seite == "o" ? luecke : null);
            if (raum.TryGetProperty("deko", out var d))
            {
                deko.AddRange(d.EnumerateArray());
            }
        }
        if (flur.TryGetProperty("deko", out var flurDeko))
        {
            deko.AddRange(flurDeko.EnumerateArray());
        }
        // Aussenwand wie fisi_game._segments (der Eingang fuehrt nach draussen - dort ist Schluss)
        waende.AddRange([(0, -0.15f, breite, 0.15f), (0, hoehe - 0.15f, breite, hoehe + 0.15f), (-0.15f, 0, 0.15f, hoehe), (breite - 0.15f, 0, breite + 0.15f, hoehe)]);
        var hindernisse = new List<(double, double, double, double)>();
        foreach (var (x1, y1, x2, y2) in waende)
        {
            teile.Add(Quader(x1, 0, y1, x2, 1.1f, y2, Farben.WandInnen));
            hindernisse.Add((x1, y1, x2, y2));
        }
        foreach (var e in deko)
        {
            var typ = e.GetProperty("typ").GetString() ?? "";
            float x = Zahl(e, "x"), y = Zahl(e, "y"), w = Zahl(e, "w"), h = Zahl(e, "h");
            var begehbar = BegehbareDeko.Contains(typ);
            var (dekoHoehe, farbe) = DekoArt(typ);
            teile.Add(Quader(x, 0.03f, y, x + w, begehbar ? 0.05f : dekoHoehe, y + h, farbe));
            if (!begehbar)
            {
                hindernisse.Add((x, y, x + w, y + h));
            }
        }
        var wege = new Wegsuche(Wegsuche.FreieZellen(breite, hoehe, InnenSchritt, Koerper, hindernisse), InnenSchritt);
        var spieler = flur.GetProperty("spieler");
        return new ProbeFlaeche(Buero, teile, wege, (float)spieler[0].GetDouble(), (float)spieler[1].GetDouble(), breite, hoehe);
    }

    private static (float Hoehe, Vector3 Farbe) DekoArt(string typ) => typ switch
    {
        "schreibtisch" or "tisch" or "werkbank" => (0.75f, Farben.Holz),
        "aktenregal" or "regal" or "schrank" or "serverschrank" or "rack" => (1.6f, Farben.Metall),
        "pflanze" => (0.9f, Farben.Pflanze),
        "sessel" or "sofa" or "stuhl" or "bank" => (0.5f, Farben.Polster),
        "kaffeemaschine" or "drucker" or "feuerloescher" => (0.6f, Farben.Geraet),
        _ => (0.6f, Farben.Karton),
    };

    /// <summary>Feste Probefarben (Hex wie fisi_game WALL_OUTER, WINDOW, WOOD, METAL, PLANT ...).</summary>
    public static class Farben
    {
        public static readonly Vector3 Wiese = Netz.Farbe("#7FB069");
        public static readonly Vector3 Gehweg = Netz.Farbe("#C9C3D6");
        public static readonly Vector3 Strasse = Netz.Farbe("#4A4560");
        public static readonly Vector3 Linie = Netz.Farbe("#F4F1FA");
        public static readonly Vector3 Wand = Netz.Farbe("#D9CCF5");
        public static readonly Vector3 WandZwei = Netz.Farbe("#C4B5EA");
        public static readonly Vector3 WandInnen = Netz.Farbe("#EDE7FA");
        public static readonly Vector3 Dach = Netz.Farbe("#B7643A");
        public static readonly Vector3 Dachrand = Netz.Farbe("#8E8AA3");
        public static readonly Vector3 Fenster = Netz.Farbe("#7FE3F5");
        public static readonly Vector3 Tuer = Netz.Farbe("#6E4A2F");
        public static readonly Vector3 Tor = Netz.Farbe("#4A4560");
        public static readonly Vector3 Schild = Netz.Farbe("#8B5CF6");
        public static readonly Vector3 Stamm = Netz.Farbe("#6E4A2F");
        public static readonly Vector3 Krone = Netz.Farbe("#34D399");
        public static readonly Vector3 Flur = Netz.Farbe("#D6D0E4");
        public static readonly Vector3 Boden = Netz.Farbe("#E4DFEF");
        public static readonly Vector3 Parkett = Netz.Farbe("#C79A62");
        public static readonly Vector3 Holz = Netz.Farbe("#9A6B45");
        public static readonly Vector3 Metall = Netz.Farbe("#8E8AA3");
        public static readonly Vector3 Pflanze = Netz.Farbe("#159A6C");
        public static readonly Vector3 Polster = Netz.Farbe("#3A2A5C");
        public static readonly Vector3 Geraet = Netz.Farbe("#16122A");
        public static readonly Vector3 Karton = Netz.Farbe("#C79A62");
        public static readonly Vector3 Figur = Netz.Farbe("#8B5CF6");
        public static readonly Vector3 Kopf = Netz.Farbe("#F2C9A0");
        public static readonly Vector3 Ziel = Netz.Farbe("#F472B6");
    }

    /// <summary>Spielfigur (Ursprung unten in der Mitte), wird mit einer eigenen Lage gezeichnet.</summary>
    public static Netz Figur() => Netz.Aus(
    [
        Quader(-0.14f, 0f, -0.08f, -0.03f, 0.42f, 0.08f, Farben.Polster),
        Quader(0.03f, 0f, -0.08f, 0.14f, 0.42f, 0.08f, Farben.Polster),
        Quader(-0.2f, 0.42f, -0.12f, 0.2f, 0.95f, 0.12f, Farben.Figur),
        Quader(-0.13f, 0.98f, -0.13f, 0.13f, 1.24f, 0.13f, Farben.Kopf),
    ]);

    /// <summary>Markierung am Zielpunkt eines Wegs.</summary>
    public static Netz Zielmarke() => Netz.Aus([Quader(-0.2f, 0.03f, -0.2f, 0.2f, 0.06f, 0.2f, Farben.Ziel)]);
}
