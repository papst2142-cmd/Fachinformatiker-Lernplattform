using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Ein Haus der Stichprobe mit Tuer und Oeffnungszeit (Minuten seit 0 Uhr; null = immer offen).</summary>
public sealed record ProbeHaus(string Id, string Name, float TuerX, float TuerZ, int? Oeffnet, int? Schliesst, bool HatInnenraum)
{
    public bool Offen(int minute) => Oeffnet is not { } von || Schliesst is not { } bis || (minute >= von && minute < bis);
}

/// <summary>Eine begehbare Flaeche (Stadt oder Innenraum): Teile, Wegsuche, Startpunkt, Groesse.</summary>
public sealed record ProbeFlaeche(string Id, IReadOnlyList<Teil> Teile, Wegsuche Wege, float StartX, float StartZ,
    float Breite, float Tiefe);

/// <summary>
/// Phase 7, W1 (Stichprobe, Plan Abschnitt 4): ein Strassenstueck mit drei Haeusern und ein
/// Innenraum. Die Haeuser kommen aus den Aussenmodellen der Python-Weltkarte
/// (<c>inhalte/spiel/orte.json</c>, Teile block/baum/tor/schild), der Innenraum ist das Buero
/// aus <c>gebaeude.json</c> (Raeume, Waende, Deko, Raster wie fisi_game._base_grid). Noch kein
/// Spiel-Kern, keine Spielzahlen, nichts wird gespeichert. Uhr und Oeffnungszeiten sind nur
/// Probewerte (Spielzeit in Schritten, nie die echte Uhr).
/// </summary>
public sealed class ProbeWelt
{
    // Raster wie fisi_game (WALK_STEP, BODY_RADIUS); die Stadt nutzt groessere Zellen
    public const double InnenSchritt = 0.25;
    public const double StadtSchritt = 0.5;
    public const double Koerper = 0.3;
    public const float WandInnen = 0.16f;

    /// <summary>Start der Probe-Uhr: 8:00 Uhr.</summary>
    public const int StartMinute = 8 * 60;

    /// <summary>Ein Weg kostet je angefangene halbe Karteneinheit eine Spielminute.</summary>
    public const double MinuteJeEinheit = 2.0;

    public const string Stadt = "stadt";
    public const string Buero = "buero";

    private static readonly (string Id, string Name, string Modell, string? Variante, float X, float Z, int? Oeffnet, int? Schliesst, bool Innen)[] Plan =
    [
        ("zuhause", "Zuhause", "wohnung", "apartment", 2.0f, 3.0f, null, null, false),
        ("bitweiche", "Bitweiche", "bitweiche", null, 9.0f, 3.4f, 7 * 60, 19 * 60, true),
        ("talheim", "Bahnhof Talheim", "bahnhof", null, 17.5f, 2.6f, 10 * 60, 18 * 60, false),
    ];

    public const float StadtBreite = 26f;
    public const float StadtTiefe = 12f;
    public const float StrasseZ1 = 8.2f;
    public const float StrasseZ2 = 9.6f;

    private ProbeWelt(ProbeFlaeche stadtFlaeche, ProbeFlaeche buero, IReadOnlyList<ProbeHaus> haeuser)
    {
        StadtFlaeche = stadtFlaeche;
        BueroFlaeche = buero;
        Haeuser = haeuser;
    }

    public ProbeFlaeche StadtFlaeche { get; }

    public ProbeFlaeche BueroFlaeche { get; }

    public IReadOnlyList<ProbeHaus> Haeuser { get; }

    public static string Uhrzeit(int minute) =>
        string.Create(CultureInfo.InvariantCulture, $"{minute / 60 % 24:00}:{minute % 60:00}");

    /// <summary>Spielminuten fuer einen Weg der Laenge <paramref name="laenge"/> (mindestens 1, wenn man geht).</summary>
    public static int Minuten(double laenge) => laenge <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(laenge * MinuteJeEinheit - 1e-9));

    /// <summary>Welt aus den Inhalten bauen (<paramref name="lesen"/>: Name ohne ".json" unter inhalte/spiel).</summary>
    public static ProbeWelt Bauen(Func<string, byte[]> lesen)
    {
        using var orte = JsonDocument.Parse(lesen("orte"));
        using var gebaeude = JsonDocument.Parse(lesen("gebaeude"));
        var modelle = orte.RootElement.GetProperty("modelle");
        var teile = new List<Teil>();
        var hindernisse = new List<(double, double, double, double)>();
        var haeuser = new List<ProbeHaus>();
        // Boden: Wiese, Gehweg, Strasse mit Mittellinie
        teile.Add(Quader(-2, -0.06f, -2, StadtBreite + 2, 0f, StadtTiefe + 2, Farben.Wiese));
        teile.Add(Quader(0, 0f, StrasseZ1 - 0.7f, StadtBreite, 0.02f, StrasseZ1, Farben.Gehweg));
        teile.Add(Quader(0, 0f, StrasseZ1, StadtBreite, 0.015f, StrasseZ2, Farben.Strasse));
        for (var x = 0.5f; x < StadtBreite; x += 2f)
        {
            teile.Add(Quader(x, 0.015f, ((StrasseZ1 + StrasseZ2) / 2) - 0.04f, x + 1f, 0.02f, ((StrasseZ1 + StrasseZ2) / 2) + 0.04f, Farben.Linie));
        }
        foreach (var (id, name, modell, variante, ox, oz, oeffnet, schliesst, innen) in Plan)
        {
            var (tuerX, tuerZ) = HausBauen(modelle.GetProperty(modell), variante, ox, oz, teile, hindernisse);
            haeuser.Add(new ProbeHaus(id, name, tuerX, tuerZ, oeffnet, schliesst, innen));
        }
        var stadtWege = new Wegsuche(Wegsuche.FreieZellen(StadtBreite, StadtTiefe, StadtSchritt, Koerper, hindernisse), StadtSchritt);
        var start = haeuser[0];
        var stadt = new ProbeFlaeche(Stadt, teile, stadtWege, start.TuerX, start.TuerZ, StadtBreite, StadtTiefe);
        return new ProbeWelt(stadt, BueroBauen(gebaeude.RootElement), haeuser);
    }

    private static Teil Quader(float x1, float y1, float z1, float x2, float y2, float z2, Vector3 farbe) =>
        new(TeilArt.Quader, x1, y1, z1, x2, y2, z2, farbe);

    private static float Zahl(JsonElement e, string name, float vorgabe = 0f) =>
        e.TryGetProperty(name, out var w) && w.ValueKind == JsonValueKind.Number ? (float)w.GetDouble() : vorgabe;

    private static bool PasstZurVariante(JsonElement teil, string? variante)
    {
        if (!teil.TryGetProperty("wohnung", out var liste))
        {
            return true;
        }
        return variante is not null && liste.EnumerateArray().Any(x => x.GetString() == variante);
    }

    /// <summary>Ein Aussenmodell aus orte.json an (ox, oz) bauen; Rueckgabe: Punkt vor der Tuer des ersten Blocks.</summary>
    private static (float X, float Z) HausBauen(JsonElement modell, string? variante, float ox, float oz,
        List<Teil> teile, List<(double, double, double, double)> hindernisse)
    {
        (float X, float Z)? tuer = null;
        foreach (var teil in modell.EnumerateArray())
        {
            if (!PasstZurVariante(teil, variante))
            {
                continue;
            }
            var art = teil.GetProperty("art").GetString();
            float x = ox + Zahl(teil, "x"), z = oz + Zahl(teil, "y");
            switch (art)
            {
                case "block":
                {
                    float w = Zahl(teil, "w"), t = Zahl(teil, "t"), h = Zahl(teil, "h");
                    var dach = teil.TryGetProperty("dach", out var d) ? d.GetString() : "flach";
                    var wand = tuer is null ? Farben.Wand : Farben.WandZwei;
                    var hoehe = dach == "sattel" ? h * 0.72f : h;
                    teile.Add(Quader(x, 0, z, x + w, hoehe, z + t, wand));
                    if (dach == "sattel")
                    {
                        teile.Add(new Teil(TeilArt.Satteldach, x - 0.1f, hoehe, z - 0.1f, x + w + 0.1f, h, z + t + 0.1f, Farben.Dach));
                    }
                    else
                    {
                        teile.Add(Quader(x - 0.05f, hoehe, z - 0.05f, x + w + 0.05f, hoehe + 0.08f, z + t + 0.05f, Farben.Dachrand));
                    }
                    var fenster = !teil.TryGetProperty("fenster", out var f) || f.ValueKind != JsonValueKind.False;
                    if (fenster)
                    {
                        // Fensterband vorne, je Geschoss (etwa 0,9 Einheiten) eines
                        for (var y = 0.45f; y + 0.35f < hoehe; y += 0.9f)
                        {
                            for (var fx = x + 0.3f; fx + 0.45f <= x + w - 0.2f; fx += 0.8f)
                            {
                                teile.Add(Quader(fx, y, z + t, fx + 0.45f, y + 0.35f, z + t + 0.03f, Farben.Fenster));
                            }
                        }
                    }
                    hindernisse.Add((x, z, x + w, z + t));
                    if (tuer is null)
                    {
                        var tx = x + (w / 2);
                        teile.Add(Quader(tx - 0.25f, 0, z + t, tx + 0.25f, 0.8f, z + t + 0.04f, Farben.Tuer));
                        tuer = (tx, z + t + 0.45f);
                    }
                    break;
                }
                case "tor":
                {
                    float w = Zahl(teil, "w"), h = Zahl(teil, "h");
                    // Tor vorne am Gebaeude (y ist im Modell die Unterkante)
                    teile.Add(Quader(x, 0, z - 0.02f, x + w, h, z + 0.04f, Farben.Tor));
                    break;
                }
                case "schild":
                {
                    var w = Zahl(teil, "w");
                    teile.Add(Quader(x, 1.1f, z - 0.02f, x + w, 1.35f, z + 0.06f, Farben.Schild));
                    break;
                }
                case "baum":
                {
                    var r = Zahl(teil, "r", 0.4f);
                    teile.Add(Quader(x - 0.08f, 0, z - 0.08f, x + 0.08f, 0.6f, z + 0.08f, Farben.Stamm));
                    teile.Add(Quader(x - r, 0.5f, z - r, x + r, 0.5f + (2 * r), z + r, Farben.Krone));
                    hindernisse.Add((x - 0.1, z - 0.1, x + 0.1, z + 0.1));
                    break;
                }
            }
        }
        return tuer ?? (ox, oz);
    }

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
