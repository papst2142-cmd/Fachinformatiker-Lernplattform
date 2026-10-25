using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Eine Person im Innenraum (Platz wie Python "platz", Aussehen fuer die Figur).</summary>
public sealed record InnenPerson(string Id, string Name, float X, float Z, PyObjekt? Aussehen, bool Auftrag, PyObjekt Daten);

/// <summary>
/// Phase 7, W5 (Weisung P7-10): ein Innenraum in 3D aus genau dem Grundriss, den die 2D-Ansichten
/// der Python-Linie zeichnen (<see cref="SpielOrte.Gebaeude"/>: Buero, Wohnung, Kunde, Filiale;
/// Serverfarm aus <c>inhalte_csharp/spiel/innenraeume.json</c>). Koordinaten wie Python (x nach
/// rechts, y des Grundrisses wird z), damit Wege (<see cref="Wege"/>), Personen und Texte dieselben
/// bleiben. Boeden je Raum, Waende mit Tueroeffnungen (halbhoch, man sieht hinein), Fenster und
/// Eingang in der Aussenwand, die Einrichtung als CC0-Moebel (Furniture Kit) oder eigene Geometrie.
/// Nichts davon geht in den Spielstand.
/// </summary>
public sealed class Innenraum
{
    /// <summary>Eine Einheit des Grundrisses sind etwa 0,6 m (Tuer 1,4 = 0,84 m, Schreibtisch 2,4 x 1,0).</summary>
    public const float Meter = 1f / 0.6f;

    /// <summary>Hoehe der Waende (1,8 m): niedriger als echt, damit die schraege Kamera in die Raeume sieht.</summary>
    public const float WandHoehe = 1.8f * Meter;

    /// <summary>Groesse der Figuren im Innenraum (1,75 m).</summary>
    public const float FigurHoehe = 1.75f * Meter;

    /// <summary>Ansicht der Serverfarm (orte.json "ansicht" des Lokschuppens).</summary>
    public const string Serverfarm = "serverfarm";

    /// <summary>Alle CC0-Modelle, die Innenraeume nutzen (Pruefung: jedes Modell in modelle.bin wird gebraucht).</summary>
    public static readonly IReadOnlyList<string> Modelle = ["bathroomSink", "bathtub", "bedDouble", "bedSingle", "bench", "bookcaseClosedDoors", "bookcaseClosedWide", "bookcaseOpen", "cabinetTelevision", "cardboardBoxClosed", "chair", "chairDesk", "computerScreen", "desk", "kitchenBar", "kitchenCabinet", "kitchenCoffeeMachine", "kitchenFridge", "lampRoundFloor", "loungeChair", "loungeChairRelax", "loungeSofa", "loungeSofaOttoman", "pottedPlant", "rugRectangle", "shower", "sideTableDrawers", "table", "tableCoffee", "televisionModern", "toilet", "train-electric-city-a", "washer"];

    private readonly List<Teil> _teile = [];
    private readonly List<ModellStueck> _stuecke = [];

    private Innenraum(string ansicht, string ort, PyObjekt gebaeude, List<InnenPerson> personen, GrafikBibliothek? grafik, PyObjekt? boeden)
    {
        Ansicht = ansicht;
        Ort = ort;
        Gebaeude = gebaeude;
        Personen = personen;
        Grafik = grafik;
        Breite = (float)Py.F(gebaeude["breite"]);
        Tiefe = (float)Py.F(gebaeude["hoehe"]);
        var (sx, sz) = Wege.StartPlatz(gebaeude);
        StartX = (float)sx;
        StartZ = (float)sz;
        Lauf = new LaufOrt(gebaeude, [.. personen.Select(p => (object?)p.Daten)]);
        Aufbauen(boeden);
    }

    /// <summary>"buero", "zuhause", "filiale", "kunde:&lt;id&gt;" oder "serverfarm".</summary>
    public string Ansicht { get; }

    /// <summary>Ort wie Python site (buero, zuhause, filiale, Kundenort-id, serverfarm).</summary>
    public string Ort { get; }

    public PyObjekt Gebaeude { get; }

    public IReadOnlyList<InnenPerson> Personen { get; }

    /// <summary>Raster und Personen fuer die Wege (wie fisi_game.walk_path).</summary>
    public LaufOrt Lauf { get; }

    public GrafikBibliothek? Grafik { get; }

    public float Breite { get; }

    public float Tiefe { get; }

    public float StartX { get; }

    public float StartZ { get; }

    public IReadOnlyList<Teil> Teile => _teile;

    public IReadOnlyList<ModellStueck> Stuecke => _stuecke;

    /// <summary>Welche Moebel wo stehen (aendert sie sich, laedt die Darstellung neu).</summary>
    public string Signatur { get; private set; } = "";

    public Netz NetzBauen()
    {
        var netz = Netz.Aus(_teile);
        foreach (var stueck in _stuecke)
        {
            if (Grafik?.Modell(stueck.Name) is { } modell)
            {
                netz.Hinzu(modell, stueck.Lage);
            }
        }
        return netz;
    }

    /// <summary>Innenraum aus einem beliebigen Grundriss (Pruefungen, Fotos): ohne Personen und Bodenwahl.</summary>
    public static Innenraum AusGrundriss(string ansicht, PyObjekt gebaeude, GrafikBibliothek? grafik) =>
        new(ansicht, OrtFuer(ansicht) ?? ansicht, gebaeude, [], grafik, null);

    /// <summary>Ort (Python site) zur Ansicht eines Stadt-Orts; null = (noch) kein Innenraum.</summary>
    public static string? OrtFuer(string? ansicht) => ansicht switch
    {
        "buero" => OrtBuero,
        "zuhause" => OrtZuhause,
        "filiale" => OrtFiliale,
        Serverfarm => Serverfarm,
        not null when ansicht.StartsWith("kunde:", StringComparison.Ordinal) => ansicht["kunde:".Length..],
        _ => null,
    };

    /// <summary>
    /// Innenraum zur Ansicht fuer den Spielstand; null, wenn es ihn (noch) nicht gibt (keine Wohnung,
    /// Filiale nicht eroeffnet, Serverfarm ohne innenraeume.json). <paramref name="innenraeume"/>:
    /// Inhalt von innenraeume.json.
    /// </summary>
    public static Innenraum? Bauen(string? ansicht, Spielstand stand, PyObjekt? innenraeume, GrafikBibliothek? grafik)
    {
        if (OrtFuer(ansicht) is not { } ort)
        {
            return null;
        }
        if (ort == Serverfarm)
        {
            if ((innenraeume is null ? null : Get(innenraeume, Serverfarm)) is not PyObjekt farm)
            {
                return null;
            }
            var plaetze = Get(farm, "plaetze") as PyObjekt ?? [];
            var personen = new List<InnenPerson>();
            foreach (var p in ListeOderLeer(Betrieb.FarmInhalt(stand.Inhalt), "personen").Select(Objekt))
            {
                if (Get(plaetze, Py.Str(p["id"])) is List<object?> platz)
                {
                    var kopie = Py.Flach(p);
                    Py.Setzen(kopie, "platz", new List<object?> { Py.F(platz[0]), Py.F(platz[1]) });
                    personen.Add(Person(kopie, false));
                }
            }
            return new Innenraum(ansicht!, ort, Objekt(farm["gebaeude"]), personen, grafik, null);
        }
        if (ort == OrtZuhause && Wohnung.Bleibe(stand.Wohnung, stand.Inhalt) is null)
        {
            return null;
        }
        if (ort == OrtFiliale && Betrieb.FilialGebaeude(stand) is null)
        {
            return null;
        }
        if (ort != OrtBuero && ort != OrtZuhause && ort != OrtFiliale && !Kundenorte(stand.Inhalt).Any(k => Py.Gleich(k["id"], ort)))
        {
            return null;
        }
        var gebaeude = SpielOrte.Gebaeude(ort, stand);
        var auftraege = ort == OrtZuhause ? new PyDict() : stand.Auftraege(ort);
        var leute = ort == OrtZuhause ? [] : SpielOrte.Personen(ort, stand)
            .Select(p => Person(p, auftraege.Get(p["id"]) is List<object?> { Count: > 0 })).ToList();
        var boeden = ort == OrtZuhause ? Objekt(Wohnung.Einrichtung(stand)["boeden"]) : null;
        return new Innenraum(ansicht!, ort, gebaeude, leute, grafik, boeden);
    }

    private static InnenPerson Person(PyObjekt p, bool auftrag)
    {
        var platz = Liste(p["platz"]);
        return new InnenPerson(Py.Str(p["id"]), Py.Str(Get(p, "name", "")), (float)Py.F(platz[0]), (float)Py.F(platz[1]),
            Get(p, "aussehen") as PyObjekt, auftrag, p);
    }

    /// <summary>Weg der Figur zu einem Punkt (reichweite &gt; 0: bis auf diesen Abstand heran), wie walk_path.</summary>
    public List<(double X, double Y)> Weg(double vonX, double vonZ, double nachX, double nachZ, double reichweite = 0) =>
        Wege.Weg((vonX, vonZ), (nachX, nachZ), reichweite, Lauf);

    /// <summary>Person an der Stelle (Tippen), wie person_at.</summary>
    public InnenPerson? PersonBei(double x, double z) =>
        Wege.PersonBei(x, z, Lauf) is { } p ? Personen.FirstOrDefault(q => ReferenceEquals(q.Daten, p)) : null;

    /// <summary>Person in Reichweite der Figur (zum Ansprechen), wie person_near.</summary>
    public InnenPerson? PersonNahe(double x, double z) =>
        Wege.PersonNahe(x, z, Lauf) is { } p ? Personen.FirstOrDefault(q => ReferenceEquals(q.Daten, p)) : null;

    /// <summary>Text unter der Ansicht (Ueberschrift, Text, Knoepfe) wie place_message/home_message.</summary>
    public (string Titel, string Text, List<(string Aktion, string Name)> Knoepfe) Nachricht(double x, double z, Spielstand stand)
    {
        if (Ort == Serverfarm)
        {
            if (PersonNahe(x, z) is { } p)
            {
                return (PyFormat.Prozent("%s · %s", p.Name, Get(p.Daten, "rolle", "")), Py.Str(Get(p.Daten, "text", "")), []);
            }
            var raum = Wege.RaumBei(x, z, Gebaeude);
            var wo = raum is not null ? PyFormat.Prozent("Du bist im Raum %s.", raum["name"]) : "Du bist im Flur.";
            return (wo, Betrieb.FarmOrtText(stand, stand.Inhalt), []);
        }
        return SpielOrte.Nachricht(Ort, x, z, PersonNahe(x, z)?.Daten, stand, Gebaeude);
    }

    // ---- Aufbau ----------------------------------------------------------

    private static Teil Quader(float x1, float y1, float z1, float x2, float y2, float z2, Vector3 farbe) =>
        new(TeilArt.Quader, x1, y1, z1, x2, y2, z2, farbe);

    private static Vector3 Mischen(Vector3 a, Vector3 b, float anteil) => (a * (1 - anteil)) + (b * anteil);

    /// <summary>Farben aus der Palette der Python-Linie (fisi_game WOOD, METAL, PLANT ...).</summary>
    public static class Farben
    {
        public static readonly Vector3 Wiese = Netz.Farbe("#7FB069");
        public static readonly Vector3 Boden = Netz.Farbe("#E4DFEF");
        public static readonly Vector3 Parkett = Netz.Farbe("#C79A62");
        public static readonly Vector3 Teppich = Netz.Farbe("#9C8FB8");
        public static readonly Vector3 Fliesen = Netz.Farbe("#E3E6EC");
        public static readonly Vector3 Fuge = Netz.Farbe("#C5C9D3");
        public static readonly Vector3 Beton = Netz.Farbe("#B9B5C2");
        public static readonly Vector3 Wand = Netz.Farbe("#EDE7FA");
        public static readonly Vector3 WandKante = Netz.Farbe("#C4B5EA");
        public static readonly Vector3 Aussen = Netz.Farbe("#D9CCF5");
        public static readonly Vector3 Glas = Netz.Farbe("#A5E9F5");
        public static readonly Vector3 Rahmen = Netz.Farbe("#8E8AA3");
        public static readonly Vector3 Tor = Netz.Farbe("#5E6472");
        public static readonly Vector3 Holz = Netz.Farbe("#9A6B45");
        public static readonly Vector3 HolzDunkel = Netz.Farbe("#6E4A2F");
        public static readonly Vector3 Metall = Netz.Farbe("#8E8AA3");
        public static readonly Vector3 MetallDunkel = Netz.Farbe("#4B4566");
        public static readonly Vector3 Geraet = Netz.Farbe("#1E1A30");
        public static readonly Vector3 GeraetHell = Netz.Farbe("#2E2848");
        public static readonly Vector3 Bildschirm = Netz.Farbe("#22D3EE");
        public static readonly Vector3 Gruen = Netz.Farbe("#34D399");
        public static readonly Vector3 Gelb = Netz.Farbe("#FBBF24");
        public static readonly Vector3 Rot = Netz.Farbe("#EF4444");
        public static readonly Vector3 Weiss = Netz.Farbe("#F8FAFC");
        public static readonly Vector3 Karton = Netz.Farbe("#C79A62");
        public static readonly Vector3 Wasser = Netz.Farbe("#60A5FA");
        public static readonly Vector3 Pflanze = Netz.Farbe("#159A6C");
        public static readonly Vector3 Polster = Netz.Farbe("#7C3AED");
        public static readonly Vector3 Hell = Netz.Farbe("#DDE6F0");
    }

    /// <summary>Akzent je Bodenfarbe der Wohnung (_FLOOR_ACCENT, Grundfarben fisi_theme).</summary>
    private static Vector3? BodenAkzent(object? farbe) => farbe switch
    {
        "violett" => Netz.Farbe("#A78BFA"),
        "cyan" => Netz.Farbe("#22D3EE"),
        "gruen" => Netz.Farbe("#34D399"),
        "orange" => Netz.Farbe("#FB923C"),
        "pink" => Netz.Farbe("#F472B6"),
        "grau" => Netz.Farbe("#FFFFFF"),
        _ => null,
    };

    private void Aufbauen(PyObjekt? boeden)
    {
        var signatur = new List<string>();
        // Wiese um das Haus, damit die geneigte Kamera keinen leeren Himmel zeigt
        _teile.Add(Quader(-20, -0.08f, -20, Breite + 20, -0.02f, Tiefe + 20, Mischen(Farben.Wiese, Vector3.Zero, 0.1f)));
        var flur = Objekt(Gebaeude["flur"]);
        _teile.Add(Quader(0, -0.02f, 0, Breite, 0.02f, Tiefe, Farben.Boden));
        Flaeche(0, (float)Py.F(flur["y"]), Breite, (float)Py.F(flur["h"]), Get(flur, "boden") as string, null, null, 0.03f);
        foreach (var raum in Liste(Gebaeude["raeume"]).Select(Objekt))
        {
            var boden = Get(raum, "boden") as string;
            Vector3? akzent = Get(raum, "farbe") is string hex ? Netz.Farbe(hex) : null;
            Vector3? wahl = null;
            if (boeden is not null && Get(boeden, Py.Str(raum["id"])) is List<object?> eintrag && eintrag.Count > 1)
            {
                wahl = BodenAkzent(eintrag[1]);
            }
            Flaeche((float)Py.F(raum["x"]), (float)Py.F(raum["y"]), (float)Py.F(raum["w"]), (float)Py.F(raum["h"]), boden, akzent, wahl, 0.035f);
        }
        // Innenwaende wie fisi_game._segments (ohne die vier Aussenwaende am Ende)
        var stuecke = Wege.Wandstuecke(Gebaeude);
        foreach (var (x1, y1, x2, y2) in stuecke.Take(stuecke.Count - 4))
        {
            Wand((float)x1, (float)y1, (float)x2, (float)y2);
        }
        Aussenwaende(Objekt(flur["eingang"]));
        var deko = Liste(Gebaeude["raeume"]).Select(Objekt).SelectMany(r => ListeOderLeer(r, "deko"))
            .Concat(ListeOderLeer(flur, "deko")).Select(Objekt);
        foreach (var d in deko)
        {
            Deko(d);
            signatur.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{d["typ"]}@{Py.F(d["x"])},{Py.F(d["y"])},{Py.Int(Get(d, "dreh", 0L))}"));
        }
        Signatur = string.Join(";", signatur);
    }

    /// <summary>Boden einer Flaeche: Art (parkett, teppichboden, fliesen, beton), Raumfarbe als Hauch, Fliesen mit Fugen.</summary>
    private void Flaeche(float x, float z, float w, float t, string? boden, Vector3? raumFarbe, Vector3? wahl, float hoehe)
    {
        var farbe = boden switch
        {
            "parkett" => Farben.Parkett,
            "teppichboden" => raumFarbe is { } r ? Mischen(Farben.Teppich, r, 0.35f) : Farben.Teppich,
            "fliesen" => Farben.Fliesen,
            "beton" => Farben.Beton,
            _ => raumFarbe is { } r2 ? Mischen(Farben.Boden, r2, 0.18f) : Farben.Boden,
        };
        if (wahl is { } akzent)
        {
            farbe = Mischen(farbe, akzent, 0.3f);
        }
        _teile.Add(Quader(x, 0.02f, z, x + w, hoehe, z + t, farbe));
        if (boden == "fliesen")
        {
            for (var fx = x + 0.6f; fx < x + w - 0.05f; fx += 0.6f)
            {
                _teile.Add(Quader(fx - 0.012f, hoehe, z, fx + 0.012f, hoehe + 0.002f, z + t, Farben.Fuge));
            }
            for (var fz = z + 0.6f; fz < z + t - 0.05f; fz += 0.6f)
            {
                _teile.Add(Quader(x, hoehe, fz - 0.012f, x + w, hoehe + 0.002f, fz + 0.012f, Farben.Fuge));
            }
        }
        else if (boden == "parkett")
        {
            for (var fz = z + 0.35f; fz < z + t - 0.05f; fz += 0.35f)
            {
                _teile.Add(Quader(x, hoehe, fz - 0.008f, x + w, hoehe + 0.002f, fz + 0.008f, Mischen(farbe, Vector3.Zero, 0.15f)));
            }
        }
    }

    private void Wand(float x1, float z1, float x2, float z2)
    {
        if (x2 - x1 < 1e-4f || z2 - z1 < 1e-4f)
        {
            return;
        }
        _teile.Add(Quader(x1, 0, z1, x2, WandHoehe - 0.06f, z2, Farben.Wand));
        _teile.Add(Quader(x1, WandHoehe - 0.06f, z1, x2, WandHoehe, z2, Farben.WandKante));
    }

    /// <summary>Aussenwaende mit Eingang (links im Flur), Fenstern (Glas ab Bruestung) und Rolltor.</summary>
    private void Aussenwaende(PyObjekt eingang)
    {
        const float d = 0.15f;
        var fenster = ListeOderLeer(Gebaeude, "fenster").Select(Liste).Select(f => (X1: (float)Py.F(f[0]), Y1: (float)Py.F(f[1]), X2: (float)Py.F(f[2]), Y2: (float)Py.F(f[3]))).ToList();
        var tore = ListeOderLeer(Gebaeude, "rolltor").Select(Liste).Select(f => (X1: (float)Py.F(f[0]), Y1: (float)Py.F(f[1]), X2: (float)Py.F(f[2]), Y2: (float)Py.F(f[3]))).ToList();
        // je Seite: Laenge und Oeffnungen (von, bis, art)
        void Seite(bool waagerecht, float fest, float laenge, float innenSeite)
        {
            var oeffnungen = new List<(float Von, float Bis, char Art)>();
            foreach (var f in fenster)
            {
                if (waagerecht && Math.Abs(f.Y1 - fest) < 0.01f && Math.Abs(f.Y2 - fest) < 0.01f)
                {
                    oeffnungen.Add((Math.Min(f.X1, f.X2), Math.Max(f.X1, f.X2), 'f'));
                }
                else if (!waagerecht && Math.Abs(f.X1 - fest) < 0.01f && Math.Abs(f.X2 - fest) < 0.01f)
                {
                    oeffnungen.Add((Math.Min(f.Y1, f.Y2), Math.Max(f.Y1, f.Y2), 'f'));
                }
            }
            foreach (var r in tore)
            {
                if (!waagerecht && Math.Abs(r.X1 - fest) < 0.01f)
                {
                    oeffnungen.Add((Math.Min(r.Y1, r.Y2), Math.Max(r.Y1, r.Y2), 'r'));
                }
                else if (waagerecht && Math.Abs(r.Y1 - fest) < 0.01f)
                {
                    oeffnungen.Add((Math.Min(r.X1, r.X2), Math.Max(r.X1, r.X2), 'r'));
                }
            }
            if (!waagerecht && fest == 0)
            {
                oeffnungen.Add(((float)Py.F(eingang["von"]), (float)Py.F(eingang["bis"]), 't'));
            }
            oeffnungen.Sort((a, b) => a.Von.CompareTo(b.Von));
            var a0 = innenSeite < 0 ? fest - d : fest - d;
            var a1 = fest + d;
            void Stueck(float von, float bis, float unten, float oben, Vector3 farbe)
            {
                if (bis - von < 1e-3f || oben - unten < 1e-3f)
                {
                    return;
                }
                _teile.Add(waagerecht ? Quader(von, unten, a0, bis, oben, a1, farbe) : Quader(a0, unten, von, a1, oben, bis, farbe));
            }
            var pos = waagerecht ? -d : 0f;
            var ende = waagerecht ? laenge + d : laenge;
            foreach (var (von, bis, art) in oeffnungen)
            {
                Stueck(pos, Math.Max(pos, von), 0, WandHoehe, Farben.Aussen);
                switch (art)
                {
                    case 'f':
                        Stueck(von, bis, 0, 0.9f * Meter, Farben.Aussen);
                        Stueck(von, bis, 0.9f * Meter, WandHoehe - 0.12f, Farben.Glas);
                        Stueck(von, bis, WandHoehe - 0.12f, WandHoehe, Farben.Rahmen);
                        break;
                    case 'r':
                        Stueck(von, bis, 0, WandHoehe, Farben.Tor);
                        for (var h = 0.25f; h < WandHoehe; h += 0.25f)
                        {
                            Stueck(von, bis, h - 0.02f, h, Mischen(Farben.Tor, Vector3.Zero, 0.25f));
                        }
                        break;
                }
                pos = Math.Max(pos, bis);
            }
            Stueck(pos, ende, 0, WandHoehe, Farben.Aussen);
        }
        Seite(true, 0, Breite, 1);
        Seite(true, Tiefe, Breite, -1);
        Seite(false, 0, Tiefe, 1);
        Seite(false, Breite, Tiefe, -1);
    }

    // ---- Einrichtung -------------------------------------------------------

    /// <summary>
    /// Lage eines Einrichtungsstuecks: Rechteck im Grundriss (x, z, w, t, wie Python schon gedreht) und
    /// Vierteldrehungen im Uhrzeigersinn (Python "dreh"; dreh 0 = Vorderseite nach +z). Rechnet Stellen
    /// im ungedrehten Stueck (lx, lz) in den Grundriss um wie fisi_game._rotate_shape.
    /// </summary>
    private readonly record struct Rahmen(float X, float Z, float W, float T, int Dreh)
    {
        public float B0 => Dreh % 2 == 1 ? T : W;

        public float T0 => Dreh % 2 == 1 ? W : T;

        public Vector2 Punkt(float lx, float lz)
        {
            float b = B0, t = T0;
            for (var i = 0; i < Dreh; i++)
            {
                (lx, lz) = (t - lz, lx);
                (b, t) = (t, b);
            }
            return new Vector2(X + lx, Z + lz);
        }

        public (float X, float Z, float W, float T) Rechteck(float lx, float lz, float lw, float lt)
        {
            var a = Punkt(lx, lz);
            var b = Punkt(lx + lw, lz + lt);
            return (Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
        }
    }

    /// <summary>Ein CC0-Modell in das Rechteck (x, z, w, t) gestreckt, <paramref name="hoehe"/> hoch, ab <paramref name="unten"/>.</summary>
    private void Modell(string name, float x, float z, float w, float t, float hoehe, int dreh, float unten, Vector3 ersatz)
    {
        dreh = ((dreh % 4) + 4) % 4;
        if (Grafik?.Modell(name) is not { } gm)
        {
            _teile.Add(Quader(x, unten, z, x + w, unten + hoehe, z + t, ersatz));
            return;
        }
        var (b0, t0) = dreh % 2 == 1 ? (t, w) : (w, t);
        var g = gm.Max - gm.Min;
        var lage = Matrix4x4.CreateTranslation(-gm.Min)
            * Matrix4x4.CreateScale(b0 / MathF.Max(g.X, 1e-4f), hoehe / MathF.Max(g.Y, 1e-4f), t0 / MathF.Max(g.Z, 1e-4f))
            * Matrix4x4.CreateTranslation(-b0 / 2, 0, -t0 / 2)
            * Matrix4x4.CreateRotationY(-MathF.PI / 2 * dreh)
            * Matrix4x4.CreateTranslation(x + (w / 2), unten, z + (t / 2));
        _stuecke.Add(new ModellStueck(name, lage));
    }

    /// <summary>Modell an einer Stelle des ungedrehten Stuecks (Rahmen r), zusaetzlich um <paramref name="mehr"/> Vierteldrehungen.</summary>
    private void Modell(Rahmen r, string name, float lx, float lz, float lw, float lt, float hoehe, float unten, Vector3 ersatz, int mehr = 0)
    {
        var (x, z, w, t) = r.Rechteck(lx, lz, lw, lt);
        Modell(name, x, z, w, t, hoehe, r.Dreh + mehr, unten, ersatz);
    }

    private void Kasten(Rahmen r, float lx, float lz, float lw, float lt, float unten, float oben, Vector3 farbe)
    {
        var (x, z, w, t) = r.Rechteck(lx, lz, lw, lt);
        _teile.Add(Quader(x, unten, z, x + w, oben, z + t, farbe));
    }

    /// <summary>Stuecke an der Wand ohne eigene Drehung: Rueckseite zur naechsten Wand des Raums.</summary>
    private static readonly HashSet<string> AnDerWand =
    [
        "regal", "aktenregal", "hochregal", "kleiderschrank", "kuechenzeile", "kuehlschrank", "waschmaschine", "fernseher",
        "kommode", "sofa", "bett", "matratze", "tresen", "waschbecken", "wc", "badewanne", "kaffeemaschine", "usv", "rack",
        "klima", "drucker", "automat", "wasserspender", "monitorwand", "anzeigetafel", "whiteboard", "bild", "aquarium",
        "werkbank", "leitstand", "bank",
    ];

    private int AutoDrehung(float x, float z, float w, float t)
    {
        var cx = x + (w / 2);
        var cz = z + (t / 2);
        var flaeche = Liste(Gebaeude["raeume"]).Select(Objekt).FirstOrDefault(r =>
            Py.F(r["x"]) <= cx && cx <= Py.F(r["x"]) + Py.F(r["w"]) && Py.F(r["y"]) <= cz && cz <= Py.F(r["y"]) + Py.F(r["h"]));
        float rx, rz, rw, rt;
        if (flaeche is not null)
        {
            (rx, rz, rw, rt) = ((float)Py.F(flaeche["x"]), (float)Py.F(flaeche["y"]), (float)Py.F(flaeche["w"]), (float)Py.F(flaeche["h"]));
        }
        else
        {
            var flur = Objekt(Gebaeude["flur"]);
            (rx, rz, rw, rt) = (0, (float)Py.F(flur["y"]), Breite, (float)Py.F(flur["h"]));
        }
        if (w >= t)
        {
            // Rueckseite an die naehere Wand oben (Norden) oder unten
            return z - rz <= rz + rt - (z + t) ? 0 : 2;
        }
        // hochkant: Rueckseite links (Vorderseite nach Osten = 3) oder rechts (1)
        return x - rx <= rx + rw - (x + w) ? 3 : 1;
    }

    private void Deko(PyObjekt d)
    {
        var typ = Py.Str(d["typ"]);
        float x = (float)Py.F(d["x"]), z = (float)Py.F(d["y"]), w = (float)Py.F(d["w"]), t = (float)Py.F(d["h"]);
        int dreh;
        if (Py.Hat(d, "dreh") || Py.Hat(d, "blick"))
        {
            dreh = (int)(((Py.Int(Get(d, "dreh", 0L)) % 4) + 4) % 4);
            if (Py.Gleich(Get(d, "blick", "s"), "n"))
            {
                // Blick nach Norden: der Stuhl steht oben (Python chair_y = y - 0.3)
                dreh = (dreh + 2) % 4;
                // das ungedrehte Stueck hat dieselben Masse
            }
        }
        else
        {
            dreh = AnDerWand.Contains(typ) ? AutoDrehung(x, z, w, t) : 0;
        }
        var r = new Rahmen(x, z, w, t, dreh);
        float b = r.B0, tt = r.T0;
        const float M = Meter;
        switch (typ)
        {
            case "schreibtisch":
                Modell(r, "desk", 0, 0, b, tt, 0.75f * M, 0, Farben.Holz);
                Modell(r, "computerScreen", (b / 2) - 0.55f, 0.08f, 1.1f, 0.25f, 0.45f * M, 0.75f * M, Farben.Geraet);
                // Stuhl vor dem Tisch, mit dem Gesicht zum Tisch (Python: 0,3 vor der Kante)
                Modell(r, "chairDesk", (b / 2) - 0.38f, tt - 0.05f, 0.76f, 0.76f, 1.0f * M, 0, Farben.Polster, 2);
                break;
            case "leitstand":
                Modell(r, "desk", 0, 0, b, tt, 0.75f * M, 0, Farben.Holz);
                for (var i = 0; i < 3; i++)
                {
                    Modell(r, "computerScreen", (b * (i + 0.5f) / 3) - 0.5f, 0.08f, 1.0f, 0.25f, 0.45f * M, 0.75f * M, Farben.Geraet);
                }
                break;
            case "tisch":
            case "werkbank":
                Modell(r, "table", 0, 0, b, tt, (typ == "werkbank" ? 0.9f : 0.75f) * M, 0, Farben.Holz);
                if (typ == "werkbank")
                {
                    Kasten(r, 0.3f, 0.2f, 0.8f, 0.6f, 0.9f * M, (0.9f + 0.4f) * M, Farben.GeraetHell);
                }
                break;
            case "besprechungstisch":
            case "esstisch":
            {
                Modell(r, "table", 0, 0, b, tt, 0.75f * M, 0, Farben.Holz);
                var plaetze = typ == "esstisch" ? Math.Max(1, (int)PythonRunden.Runde(b / 1.2)) : 3;
                for (var i = 0; i < plaetze; i++)
                {
                    var cx = typ == "esstisch" ? b * (i + 0.5f) / plaetze : b * (i + 1) / 4f;
                    Modell(r, "chair", cx - 0.32f, -0.7f, 0.64f, 0.64f, 0.9f * M, 0, Farben.Holz, 0);
                    Modell(r, "chair", cx - 0.32f, tt + 0.06f, 0.64f, 0.64f, 0.9f * M, 0, Farben.Holz, 2);
                }
                break;
            }
            case "tresen":
                Modell(r, "kitchenBar", 0, 0, b, tt, 1.05f * M, 0, Farben.Holz);
                break;
            case "aktenregal":
                Modell(r, "bookcaseClosedWide", 0, 0, b, tt, 1.8f * M, 0, Farben.HolzDunkel);
                break;
            case "regal":
                Modell(r, "bookcaseOpen", 0, 0, b, tt, 1.8f * M, 0, Farben.HolzDunkel);
                break;
            case "hochregal":
                Modell(r, "bookcaseOpen", 0, 0, b, tt, 2.4f * M, 0, Farben.Metall);
                break;
            case "kleiderschrank":
                Modell(r, "bookcaseClosedDoors", 0, 0, b, tt, 2.0f * M, 0, Farben.Holz);
                break;
            case "pflanze":
                Modell(r, "pottedPlant", 0, 0, b, tt, 1.0f * M, 0, Farben.Pflanze);
                break;
            case "sessel":
                Modell(r, "loungeChair", 0, 0, b, tt, 0.8f * M, 0, Farben.Polster);
                break;
            case "sofa":
                Modell(r, "loungeSofa", 0, 0, b, tt, 0.8f * M, 0, Farben.Polster);
                break;
            case "sitzsack":
                Modell(r, "loungeSofaOttoman", 0, 0, b, tt, 0.45f * M, 0, Netz.Farbe("#FB923C"));
                break;
            case "couchtisch":
                Modell(r, "tableCoffee", 0, 0, b, tt, 0.4f * M, 0, Farben.Holz);
                break;
            case "bank":
                Modell(r, "bench", 0, 0, b, tt, 0.45f * M, 0, Farben.Holz);
                break;
            case "kaffeemaschine":
                Modell(r, "kitchenCabinet", 0, 0, b, tt, 0.9f * M, 0, Farben.Hell);
                Modell(r, "kitchenCoffeeMachine", b * 0.15f, tt * 0.1f, b * 0.7f, tt * 0.7f, 0.35f * M, 0.9f * M, Farben.MetallDunkel);
                break;
            case "kuechenzeile":
                Modell(r, "kitchenCabinet", 0, 0, b, tt, 0.9f * M, 0, Farben.Hell);
                break;
            case "kuehlschrank":
                Modell(r, "kitchenFridge", 0, 0, b, tt, 1.8f * M, 0, Farben.Weiss);
                break;
            case "waschmaschine":
                Modell(r, "washer", 0, 0, b, tt, 0.85f * M, 0, Farben.Hell);
                break;
            case "wc":
                Modell(r, "toilet", 0, 0, b, tt, 0.8f * M, 0, Farben.Weiss);
                break;
            case "waschbecken":
                Modell(r, "bathroomSink", 0, 0, b, tt, 0.9f * M, 0, Farben.Weiss);
                break;
            case "dusche":
                Modell(r, "shower", 0, 0, b, tt, 2.0f * M, 0, Farben.Weiss);
                break;
            case "badewanne":
                Modell(r, "bathtub", 0, 0, b, tt, 0.6f * M, 0, Farben.Weiss);
                break;
            case "teppich":
                Modell(r, "rugRectangle", 0, 0, b, tt, 0.02f, 0.04f, Netz.Farbe("#F472B6"));
                break;
            case "kartons":
                Kartons(r, b, tt);
                break;
            case "bett":
                Modell(r, b >= 2.4f ? "bedDouble" : "bedSingle", 0, 0, b, tt, 0.55f * M, 0, Farben.Holz);
                break;
            case "matratze":
                Modell(r, "bedSingle", 0, 0, b, tt, 0.3f * M, 0, Farben.Weiss);
                break;
            case "fernseher":
                Modell(r, "cabinetTelevision", 0, 0, b, tt, 0.5f * M, 0, Farben.HolzDunkel);
                Modell(r, "televisionModern", b * 0.1f, 0.05f, b * 0.8f, Math.Min(0.25f, tt * 0.4f), 0.6f * M, 0.5f * M, Farben.Geraet);
                break;
            case "kommode":
                Modell(r, "sideTableDrawers", 0, 0, b, tt, 0.8f * M, 0, Farben.HolzDunkel);
                break;
            case "stehlampe":
                Modell(r, "lampRoundFloor", 0, 0, b, tt, 1.6f * M, 0, Farben.Gelb);
                break;
            case "liegestuhl":
                Modell(r, "loungeChairRelax", 0, 0, b, tt, 0.8f * M, 0, Farben.Hell);
                break;
            default:
                Eigen(typ, r, b, tt);
                break;
        }
    }

    private void Kartons(Rahmen r, float b, float t)
    {
        // gestapelte Umzugskartons: so viele, wie in das Rechteck passen (hoechstens zwei uebereinander)
        var nx = Math.Max(1, (int)(b / 0.6f));
        var nz = Math.Max(1, (int)(t / 0.6f));
        float kb = b / nx, kt = t / nz;
        var k = 0;
        for (var i = 0; i < nx; i++)
        {
            for (var j = 0; j < nz; j++)
            {
                Modell(r, "cardboardBoxClosed", (i * kb) + 0.03f, (j * kt) + 0.03f, kb - 0.06f, kt - 0.06f, 0.5f * Meter, 0, Farben.Karton);
                if (k++ % 2 == 0)
                {
                    Modell(r, "cardboardBoxClosed", (i * kb) + 0.08f, (j * kt) + 0.08f, kb - 0.16f, kt - 0.16f, 0.4f * Meter, 0.5f * Meter, Farben.Karton);
                }
            }
        }
    }

    /// <summary>Einrichtung ohne passendes CC0-Modell: eigene Geometrie in den Farben der Python-Zeichnung.</summary>
    private void Eigen(string typ, Rahmen r, float b, float t)
    {
        const float M = Meter;
        var wandOben = WandHoehe - 0.2f;
        switch (typ)
        {
            case "rack":
            {
                Kasten(r, 0, 0, b, t, 0, 2.0f * M, Farben.Geraet);
                // Einschuebe mit Leuchten auf der Vorderseite
                for (var h = 0.25f; h < 1.9f * M; h += 0.28f)
                {
                    Kasten(r, 0.08f, t - 0.02f, b - 0.16f, 0.04f, h, h + 0.18f, Farben.GeraetHell);
                    Kasten(r, b - 0.2f, t, 0.08f, 0.03f, h + 0.06f, h + 0.12f, h % 0.84f < 0.28f ? Farben.Bildschirm : Farben.Gruen);
                }
                break;
            }
            case "usv":
                Kasten(r, 0, 0, b, t, 0, 1.0f * M, Farben.MetallDunkel);
                Kasten(r, 0.15f, t - 0.02f, b * 0.45f, 0.04f, 0.6f * M, 0.8f * M, Netz.Farbe("#0B3B2E"));
                Kasten(r, b - 0.35f, t - 0.02f, 0.18f, 0.04f, 0.7f * M, 0.8f * M, Farben.Gruen);
                break;
            case "klima":
                Kasten(r, 0, 0, b, t, 0, 1.9f * M, Farben.Hell);
                for (var h = 0.3f * M; h < 1.8f * M; h += 0.18f)
                {
                    Kasten(r, 0.05f, t - 0.01f, b - 0.1f, 0.02f, h, h + 0.05f, Farben.Metall);
                }
                break;
            case "drucker":
                Kasten(r, 0, 0, b, t, 0, 0.75f * M, Farben.Hell);
                Kasten(r, 0.05f, 0.05f, b - 0.1f, t - 0.1f, 0.75f * M, 0.95f * M, Farben.Weiss);
                break;
            case "pc":
                Kasten(r, b * 0.3f, t * 0.2f, b * 0.4f, t * 0.6f, 0, 0.5f * M, Farben.GeraetHell);
                break;
            case "feuerloescher":
                Kasten(r, b * 0.25f, t * 0.25f, b * 0.5f, t * 0.5f, 0, 0.6f * M, Farben.Rot);
                Kasten(r, b * 0.38f, t * 0.38f, b * 0.24f, t * 0.24f, 0.6f * M, 0.68f * M, Farben.Geraet);
                break;
            case "fussmatte":
                Kasten(r, 0, 0, b, t, 0.03f, 0.05f, Farben.HolzDunkel);
                break;
            case "markierung":
                Kasten(r, 0, 0, b, t, 0.03f, 0.05f, Farben.Gelb);
                break;
            case "whiteboard":
            case "anzeigetafel":
            case "monitorwand":
            {
                // an der Wand: flache Tafel in Augenhoehe
                var unten = typ == "anzeigetafel" ? 1.1f * M : 0.8f * M;
                var farbe = typ == "whiteboard" ? Farben.Weiss : Farben.Geraet;
                Kasten(r, 0, 0, b, Math.Min(t, 0.12f), unten, wandOben, Farben.Rahmen);
                Kasten(r, 0.06f, 0.0f, b - 0.12f, Math.Min(t, 0.12f) + 0.02f, unten + 0.06f, wandOben - 0.06f, farbe);
                if (typ == "monitorwand")
                {
                    var n = Math.Max(2, (int)(b / 1.3f));
                    for (var i = 0; i < n; i++)
                    {
                        var bx = (b * i / n) + 0.12f;
                        Kasten(r, bx, 0.0f, (b / n) - 0.24f, Math.Min(t, 0.12f) + 0.03f, unten + 0.15f, wandOben - 0.15f, i % 2 == 0 ? Farben.Bildschirm : Netz.Farbe("#0E7490"));
                    }
                }
                else if (typ == "anzeigetafel")
                {
                    Kasten(r, 0.15f, 0.0f, b - 0.3f, Math.Min(t, 0.12f) + 0.03f, unten + 0.2f, unten + 0.3f, Farben.Gelb);
                }
                break;
            }
            case "bild":
            case "wimpel":
            case "kamera":
            {
                var farbe = typ == "bild" ? Netz.Farbe("#F472B6") : typ == "wimpel" ? Farben.Gelb : Farben.Geraet;
                Kasten(r, b * 0.1f, 0, b * 0.8f, Math.Min(t, 0.1f), 1.2f * M, Math.Min(wandOben, 1.6f * M), farbe);
                break;
            }
            case "wasserspender":
                Kasten(r, 0, 0, b, t, 0, 1.0f * M, Farben.Hell);
                Kasten(r, b * 0.15f, t * 0.15f, b * 0.7f, t * 0.7f, 1.0f * M, 1.35f * M, Farben.Wasser);
                break;
            case "palette":
                Kasten(r, 0, 0, b, t, 0, 0.15f * M, Farben.Holz);
                Modell(r, "cardboardBoxClosed", 0.08f, 0.08f, b - 0.16f, t - 0.16f, 0.7f * M, 0.15f * M, Farben.Karton);
                break;
            case "hubwagen":
                Kasten(r, 0, 0, b, t, 0, 0.12f * M, Farben.MetallDunkel);
                Kasten(r, 0, 0, b * 0.25f, t, 0.12f * M, 0.5f * M, Farben.Gelb);
                break;
            case "gleis":
            {
                // Gleisbett mit zwei Schienen laengs der langen Seite
                Kasten(r, 0, 0, b, t, 0.03f, 0.06f, Farben.Metall);
                var laengs = b >= t;
                for (var s = -1; s <= 1; s += 2)
                {
                    if (laengs)
                    {
                        Kasten(r, 0, (t / 2) + (s * 0.3f) - 0.04f, b, 0.08f, 0.06f, 0.12f, Farben.Geraet);
                    }
                    else
                    {
                        Kasten(r, (b / 2) + (s * 0.3f) - 0.04f, 0, 0.08f, t, 0.06f, 0.12f, Farben.Geraet);
                    }
                }
                break;
            }
            case "zug":
            {
                // Zug laengs: das Modell steht laengs z, also um eine Vierteldrehung, wenn das Rechteck breit ist
                var (x, z, w, tt) = r.Rechteck(0, 0, b, t);
                Modell("train-electric-city-a", x, z, w, tt, Math.Min(wandOben, 1.9f * M), w >= tt ? 1 : 0, 0.06f, Farben.Rahmen);
                break;
            }
            case "automat":
                Kasten(r, 0, 0, b, t, 0, 1.6f * M, Farben.MetallDunkel);
                Kasten(r, 0.08f, t - 0.02f, b - 0.16f, 0.04f, 0.9f * M, 1.4f * M, Farben.Bildschirm);
                break;
            case "absperrband":
            case "bauzaun":
            {
                var laengs = b >= t;
                var farbe = typ == "absperrband" ? Farben.Rot : Farben.Metall;
                Kasten(r, 0, 0, laengs ? b : t, laengs ? Math.Min(t, 0.08f) : 0.08f, 0.85f * M, 0.95f * M, farbe);
                Kasten(r, 0, 0, 0.08f, 0.08f, 0, 1.0f * M, Farben.Geraet);
                Kasten(r, b - 0.08f, t - 0.08f, 0.08f, 0.08f, 0, 1.0f * M, Farben.Geraet);
                break;
            }
            case "aquarium":
                Kasten(r, 0, 0, b, t, 0, 0.7f * M, Farben.MetallDunkel);
                Kasten(r, 0.05f, 0.05f, b - 0.1f, t - 0.1f, 0.7f * M, 1.2f * M, Farben.Wasser);
                break;
            case "grill":
                Kasten(r, b * 0.15f, t * 0.15f, b * 0.7f, t * 0.7f, 0, 0.9f * M, Farben.Geraet);
                break;
            case "sonnenschirm":
                Kasten(r, (b / 2) - 0.05f, (t / 2) - 0.05f, 0.1f, 0.1f, 0, 2.1f * M, Farben.Metall);
                Kasten(r, 0, 0, b, t, 2.1f * M, 2.2f * M, Farben.Gelb);
                break;
            case "hochbeet":
                Kasten(r, 0, 0, b, t, 0, 0.6f * M, Farben.Holz);
                Kasten(r, 0.08f, 0.08f, b - 0.16f, t - 0.16f, 0.6f * M, 0.7f * M, Farben.Pflanze);
                break;
            case "spielkonsole":
                Kasten(r, 0, 0, b, t, 0, 0.12f * M, Farben.MetallDunkel);
                break;
            default:
                Kasten(r, 0, 0, b, t, 0, 0.6f * M, Farben.Karton);
                break;
        }
    }
}
