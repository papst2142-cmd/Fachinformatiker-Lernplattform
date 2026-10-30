using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 8 W11 (Sammelliste 152-154, Teilplan P8-35 mit Nachtraegen, Nico P8-39 bis P8-45): Festzeiten-Verkehr, nur
/// Darstellung wie die Autos (kein Ereignis, keine Spielregel, alle Fingerabdruecke bleiben). Zu Halloween und
/// Weihnachten ab Beginn der Daemmerung, zu Ostern tagsueber laufen statt der Autos Gestalten auf den Strecken der
/// Autos (<see cref="Verkehr"/>, mit denselben Regeln und <see cref="Abstand"/> zum Vordermann):
/// <list type="bullet">
/// <item>Halloween: Zombie, Geist, Hexe, Skelett, Vampir und ein zweiter Geist, je einer auf einer Strecke.</item>
/// <item>Weihnachten: ein Weihnachtsmann auf der Hauptstrassen-Strecke, drei Geschenke absteigend dahinter.</item>
/// <item>Ostern: drei Hasen und drei Huehner, abends wieder Autos.</item>
/// </list>
/// Datum nur ueber <see cref="Festzeit"/> (die Szene kennt ihr Fest), Uhrzeit aus der Tagesuhr der Stadt. Die Netze
/// baut der Kern (testbar), die Oberflaeche zeichnet sie in einem Zeichenaufruf wie die Autos; ab
/// <c>StadtFlaeche.AutoFern</c> Abstand die Ferne-Form (<see cref="Fern"/>).
/// </summary>
public static class FestVerkehr
{
    /// <summary>Wer gerade faehrt bzw. laeuft.</summary>
    public enum Satz { Autos, Halloween, Weihnachten, Ostern }

    /// <summary>Ein Platz: Art der Gestalt, Strecke (<see cref="Verkehr.Strecken"/>) und Abstand hinter dem Fahrzeug der Strecke.</summary>
    public sealed record Platz(string Art, int Strecke, float Hinten);

    /// <summary>Eine Gestalt mit ihren Schrittbildern (nah) und der Ferne-Form.</summary>
    public sealed record Gestalt(Platz Platz, IReadOnlyList<Netz> Bilder, Netz Fern);

    /// <summary>Kleinster Abstand zum Vordermann zu den Festzeiten (Autos <see cref="Verkehr.Abstand"/>), P8-39.</summary>
    public const float Abstand = 0.85f;

    /// <summary>Halbmesser der Kurven der Gestalten (Autos 0,6): enger, damit sie in der Innenkurve einer Kreuzung
    /// nicht ueber die Ecke der Fahrbahn schneiden (Gestalten sind breiter als die Autos).</summary>
    public const float Bogen = 0.55f;

    /// <summary>Groesse der Festfiguren: 1,5-fache Hoehe der Spielfigur (sonst neben Haeusern kaum zu sehen).</summary>
    public const float Groesse = 1.5f;

    /// <summary>Schrittbilder der gehenden Puppen: links vorn, Mitte, rechts vorn (Folge 0, 1, 2, 1).</summary>
    public const int Schrittbilder = 3;

    /// <summary>Weg je Viertel eines Doppelschritts (Wechsel des Schrittbilds).</summary>
    public const float SchrittWeg = 0.12f;

    private static float H => Figur.StadtHoehe * Groesse;

    /// <summary>
    /// Satz zur Festzeit <paramref name="fest"/> (der Szene), Uhr in Spielminuten und Jahreszeit: Halloween und
    /// Weihnachten ab Beginn der Daemmerung (<see cref="Tageslicht.DaemmerungAb"/>) bis zum Schlafen, Ostern davor.
    /// </summary>
    public static Satz Fuer(string? fest, long uhr, Jahreszeit jahreszeit)
    {
        var abend = uhr >= Tageslicht.DaemmerungAb(jahreszeit);
        return fest switch
        {
            Festzeit.Halloween when abend => Satz.Halloween,
            Festzeit.Weihnachten when abend => Satz.Weihnachten,
            Festzeit.Ostern when !abend => Satz.Ostern,
            _ => Satz.Autos,
        };
    }

    /// <summary>Die Plaetze eines Satzes (leer = Autos).</summary>
    public static IReadOnlyList<Platz> Plaetze(Satz satz) => satz switch
    {
        Satz.Halloween =>
        [
            new("zombie", 0, 0), new("geist", 1, 0), new("hexe", 2, 0), new("skelett", 3, 0), new("vampir", 4, 0), new("geist", 5, 0),
        ],
        // Geschenke absteigend: rot (gross), gruen (mittel), gelb (klein)
        Satz.Weihnachten => [new("mann", 2, 0), new("geschenk", 2, 0.42f), new("geschenk", 2, 0.74f), new("geschenk", 2, 0.98f)],
        Satz.Ostern =>
        [
            new("hase", 0, 0), new("huhn", 1, 0), new("hase", 2, 0), new("huhn", 3, 0), new("hase", 4, 0), new("huhn", 5, 0),
        ],
        _ => [],
    };

    /// <summary>Tempo je Art in Karteneinheiten je Sekunde (Autos 2,2).</summary>
    public static float Tempo(string art) => art switch
    {
        "geist" => 0.9f,
        "hexe" => 1.6f,
        "mann" or "geschenk" => 0.7f,
        "hase" => 1.2f,
        "huhn" => 0.6f,
        _ => 0.8f,
    };

    /// <summary>Gehen diese Gestalten mit Schrittbildern (Puppen)?</summary>
    public static bool Geht(string art) => art is "zombie" or "skelett" or "vampir";

    /// <summary>Verkehr zum Satz: Autos wie bisher, sonst nur die Strecken der Plaetze im Tempo der Gestalt.</summary>
    public static Verkehr VerkehrFuer(StadtSzene szene, Satz satz) => satz == Satz.Autos
        ? Verkehr.Fuer(szene)
        : Verkehr.Fuer(szene, Plaetze(satz).GroupBy(p => p.Strecke).ToDictionary(g => g.Key, g => Tempo(g.First().Art)), Abstand, Bogen);

    /// <summary>Die Gestalten eines Satzes (leer = Autos).</summary>
    public static List<Gestalt> Gestalten(GrafikBibliothek g, Satz satz)
    {
        var liste = new List<Gestalt>();
        var geschenk = 0;
        foreach (var p in Plaetze(satz))
        {
            List<Netz> bilder = p.Art switch
            {
                "zombie" => [.. Enumerable.Range(0, Schrittbilder).Select(b => Zombie(g, b))],
                "skelett" => [.. Enumerable.Range(0, Schrittbilder).Select(b => Skelett(g, b))],
                "vampir" => [.. Enumerable.Range(0, Schrittbilder).Select(Vampir)],
                "geist" => [Geist(g)],
                "hexe" => [Hexe(g)],
                "mann" => [Weihnachtsmann()],
                "geschenk" => [geschenk++ switch
                {
                    0 => Modell(g, "xm-present_A_red", 0.5f * H),
                    1 => Modell(g, "xm-present_B_green", 0.36f * H),
                    _ => Modell(g, "xm-present_A_yellow", 0.24f * H),
                }],
                "hase" => [Modell(g, "os-animal-bunny", 0.5f * H)],
                _ => [Huhn(g, 0.40f * H)],
            };
            liste.Add(new Gestalt(p, bilder, Fern(p.Art)));
        }
        return liste;
    }

    /// <summary>Die Netze zum Zeichnen in einem Aufruf: nah alle Schrittbilder, fern je Gestalt die Ferne-Form.</summary>
    public static List<Netz> Netze(IReadOnlyList<Gestalt> gestalten, bool fern) =>
        fern ? [.. gestalten.Select(g => g.Fern)] : [.. gestalten.SelectMany(g => g.Bilder)];

    /// <summary>Wo die Gestalt steht und wohin sie geht (null = ihre Strecke fehlt in dieser Stadt).</summary>
    public static (Vector2 Ort, Vector2 Richtung, float Weg)? Lage(Gestalt g, Verkehr verkehr)
    {
        var auto = verkehr.Autos.FirstOrDefault(a => a.Nummer == g.Platz.Strecke);
        if (auto is null)
        {
            return null;
        }
        var weg = auto.Weg - g.Platz.Hinten;
        var (ort, richtung) = auto.Lage(weg);
        return (ort, richtung, weg);
    }

    /// <summary>Schrittbild (0 bis <see cref="Schrittbilder"/> - 1) nach dem zurueckgelegten Weg (nie nach der Uhr).</summary>
    public static int Bild(float weg) => ((int)MathF.Floor(MathF.Max(0, weg) / SchrittWeg) % 4) switch
    {
        0 => 0,
        2 => 2,
        _ => 1,
    };

    /// <summary>
    /// Lagen aller Netze aus <see cref="Netze"/> (gleiche Reihenfolge); nicht gezeigte Schrittbilder und Gestalten
    /// ohne Strecke liegen unsichtbar (auf einen Punkt verkleinert). Bewegung je Art aus dem Weg (Zeit = Weg / Tempo):
    /// Geist schwebt, Hexe fliegt eine Handbreit hoch, Hase hoppelt, Huhn pickt, Geschenke huepfen.
    /// Rueckgabe: Zahl der Lagen. <paramref name="zeigen"/> (Patch A 0.29.1, Uebergang): nur Gestalten, deren Fahrzeug
    /// gerade faehrt, alle anderen liegen unsichtbar (null = alle).
    /// </summary>
    public static int Lagen(IReadOnlyList<Gestalt> gestalten, Verkehr verkehr, bool fern, Matrix4x4[] lagen, Func<Verkehr.Auto, bool>? zeigen = null)
    {
        var n = 0;
        var weg = Matrix4x4.CreateScale(0) * Matrix4x4.CreateTranslation(0, -1000, 0);
        for (var i = 0; i < gestalten.Count; i++)
        {
            var g = gestalten[i];
            var anzahl = fern ? 1 : g.Bilder.Count;
            var lage = zeigen is null || verkehr.Autos.FirstOrDefault(a => a.Nummer == g.Platz.Strecke) is { } auto && zeigen(auto)
                ? Lage(g, verkehr) : null;
            var bild = anzahl > 1 && lage is { } l0 ? Bild(l0.Weg) : 0;
            for (var b = 0; b < anzahl && n < lagen.Length; b++, n++)
            {
                lagen[n] = lage is { } l && b == bild ? Matrix(g.Platz.Art, i, l.Ort, l.Richtung, l.Weg) : weg;
            }
        }
        return n;
    }

    /// <summary>Lage einer Gestalt (Drehung zur Richtung, Bewegung je Art).</summary>
    public static Matrix4x4 Matrix(string art, int nr, Vector2 ort, Vector2 r, float weg)
    {
        float p = (weg / Tempo(art)) + (nr * 0.37f), y = 0, neigen = 0, rollen = 0;
        switch (art)
        {
            case "zombie":
                rollen = 0.07f * MathF.Sin(p * 5);
                break;
            case "mann":
                rollen = 0.06f * MathF.Sin(p * 6);
                y = 0.012f * MathF.Abs(MathF.Sin(p * 6));
                break;
            case "geist":
                y = 0.06f + (0.03f * MathF.Sin(p * 5));
                rollen = 0.06f * MathF.Sin(p * 3);
                break;
            case "vampir":
                rollen = 0.05f * MathF.Sin(p * 4);
                break;
            case "skelett":
                y = 0.02f * MathF.Abs(MathF.Sin(p * 8));
                rollen = 0.08f * MathF.Sin(p * 8);
                break;
            case "hexe":
                y = 0.1f + (0.02f * MathF.Sin(p * 4));
                neigen = 0.12f;
                break;
            case "hase":
                y = 0.09f * MathF.Abs(MathF.Sin(p * 5));
                neigen = -0.15f;
                break;
            case "huhn":
                neigen = 0.35f * MathF.Max(0, MathF.Sin(p * 7));
                break;
            case "geschenk":
                y = 0.025f * MathF.Abs(MathF.Sin((p * 6) + nr));
                break;
        }
        var winkel = MathF.Atan2(r.X, r.Y);
        return Matrix4x4.CreateRotationX(neigen) * Matrix4x4.CreateRotationZ(rollen) * Matrix4x4.CreateRotationY(winkel)
            * Matrix4x4.CreateTranslation(ort.X, StadtSzene.Hoehe.Strasse + y, ort.Y);
    }

    /// <summary>Ferne-Form ab Kameraabstand 25 (wie die Autos): zwei Kaesten in den Hauptfarben der Gestalt.</summary>
    public static Netz Fern(string art)
    {
        var n = new Netz();
        (string Unten, string Oben, float B, float Hoch) f = art switch
        {
            "zombie" => ("#2563EB", "#7C3AED", 0.13f, 1f),
            "geist" => ("#EEF2FF", "#EEF2FF", 0.22f, 0.95f),
            "hexe" => ("#6D28D9", "#2E1065", 0.13f, 1.15f),
            "vampir" => ("#4C0519", "#9CA3AF", 0.13f, 1f),
            "skelett" => ("#1C1A24", "#EDE6D6", 0.12f, 1f),
            "mann" => ("#C81E1E", "#F8FAFC", 0.15f, 1.05f),
            "geschenk" => ("#C81E1E", "#FACC15", 0.12f, 0.35f),
            "hase" => ("#8B5E3C", "#A0704A", 0.12f, 0.5f),
            _ => ("#F5F5F0", "#DC2626", 0.12f, 0.45f),
        };
        float b = f.B * H, h = f.Hoch * H;
        // der Geist leuchtet nicht (er ist kein Licht), bleibt aber nachts so hell wie am Tag (Marke wie Schnee)
        var hell = art == "geist" ? new Vector3(Jahreszeitfarben.SchneeMarke) : Vector3.Zero;
        n.Hinzu(new Teil(TeilArt.Quader, -b, 0, -b, b, h * 0.7f, b, Netz.Farbe(f.Unten) + hell));
        n.Hinzu(new Teil(TeilArt.Quader, -b * 0.7f, h * 0.7f, -b * 0.7f, b * 0.7f, h, b * 0.7f, Netz.Farbe(f.Oben) + hell));
        return n;
    }

    private static Teil Q(float x1, float y1, float z1, float x2, float y2, float z2, string farbe) =>
        new(TeilArt.Quader, x1, y1, z1, x2, y2, z2, Netz.Farbe(farbe));

    private static Netz Mensch(GrafikBibliothek g, string oberteil, string hose, string schuhe, string? kopf, int bild, string haare = "schwarz")
    {
        var aussehen = new Lernplattform.Core.Daten.PyObjekt();
        Py.Setzen(aussehen, "haarfarbe", haare);
        var kleidung = new List<Figur.Stueck> { new("oberteil", "shirt", oberteil), new("hose", "hose", hose), new("schuhe", "schuhe", schuhe) };
        if (kopf is not null)
        {
            kleidung.Add(new("kopf", kopf.Split(':')[0], kopf.Split(':')[1]));
        }
        return Figur.Netz(g, aussehen, H, kleidung, bild >= 0 ? Figur.Gehen : null, bild);
    }

    /// <summary>Hexe (eigene, aus der Spielfigur): lila Kleid, Spitzhut, fliegt auf dem Besen; die Kuerbislaterne vorn leuchtet nachts.</summary>
    private static Netz Hexe(GrafikBibliothek g)
    {
        var n = Mensch(g, "#6D28D9", "#2E1065", "#141414", "spitzhut:#2E1065", -1, "rot");
        // Besen quer unter der Huefte: Stiel nach vorn, Reisig hinten
        float y = 0.42f * H;
        n.Hinzu(Q(-0.018f * H, y, -0.46f * H, 0.018f * H, y + 0.036f * H, 0.5f * H, "#8B5A2B"));
        n.Hinzu(Q(-0.08f * H, y - 0.05f * H, -0.72f * H, 0.08f * H, y + 0.08f * H, -0.44f * H, "#D9B26A"));
        n.Hinzu(Q(-0.05f * H, y - 0.03f * H, -0.46f * H, 0.05f * H, y + 0.06f * H, -0.40f * H, "#B91C1C"));
        // leuchtende Kuerbislaterne vorn am Stiel
        if (g.Modell("hw-pumpkin_orange_jackolantern") is { } k)
        {
            var s = 0.16f * H / (k.Max.Y - k.Min.Y);
            var m = (k.Min + k.Max) / 2;
            n.Hinzu(k, Matrix4x4.CreateTranslation(-m.X, -k.Max.Y, -m.Z) * Matrix4x4.CreateScale(s) * Matrix4x4.CreateTranslation(0, y, 0.5f * H), (d, _, _) => Glut(k, d));
        }
        return n;
    }

    private static Vector3 F(string hex) => Netz.Farbe(hex);

    /// <summary>Nachts so hell wie am Tag (Marke wie Schnee): Form und Augen bleiben sichtbar.</summary>
    private static Vector3 Hell(string hex) => Netz.Farbe(hex) + new Vector3(Jahreszeitfarben.SchneeMarke);

    /// <summary>Weiche Formen aus Dreiecken (Ellipsoide, Drehkoerper) statt Kaesten.</summary>
    private sealed class Form
    {
        private readonly List<float> _e = [];
        private readonly List<Vector3> _f = [];

        private void D(Vector3 a, Vector3 b, Vector3 c, Vector3 f)
        {
            _e.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z]);
            _f.Add(f);
        }

        /// <summary>Flaeche ueber ein Raster: u rundum (0..1), v von oben nach unten (0..1).</summary>
        public void Raster(int nu, int nv, Func<float, float, Vector3> p, Func<float, float, Vector3> f)
        {
            for (var i = 0; i < nu; i++)
            {
                for (var j = 0; j < nv; j++)
                {
                    float u0 = (float)i / nu, u1 = (float)(i + 1) / nu, v0 = (float)j / nv, v1 = (float)(j + 1) / nv;
                    var farbe = f((u0 + u1) / 2, (v0 + v1) / 2);
                    D(p(u0, v0), p(u1, v0), p(u1, v1), farbe);
                    D(p(u0, v0), p(u1, v1), p(u0, v1), farbe);
                }
            }
        }

        public void Ei(Vector3 m, Vector3 r, Vector3 farbe, int nu = 12, int nv = 8, Matrix4x4? dreh = null) =>
            Raster(nu, nv, (u, v) =>
            {
                float w = u * MathF.Tau, t = v * MathF.PI;
                var q = new Vector3(MathF.Sin(t) * MathF.Sin(w) * r.X, MathF.Cos(t) * r.Y, MathF.Sin(t) * MathF.Cos(w) * r.Z);
                return m + (dreh is { } d ? Vector3.Transform(q, d) : q);
            }, (_, _) => farbe);

        public void In(Netz n, bool leuchten = false)
        {
            var von = n.AnzahlEcken;
            n.Hinzu(new GrafikModell("form", [.. _e], new byte[_f.Count * 3]), Matrix4x4.Identity, (d, _, _) => _f[d]);
            if (leuchten)
            {
                n.Leuchten(von, n.AnzahlEcken);
            }
        }
    }

    /// <summary>Laken-Geist: Kopf rund, nach unten weiter, Saum mit Zipfeln.</summary>
    private static void Laken(Form f, Vector3 farbe, float oben, float kopfR, float saumR, int zipfel, float zipfelTief, float spitze)
    {
        const float unten = 0.08f;
        f.Raster(18, 12, (u, v) =>
        {
            var w = u * MathF.Tau;
            // Kuppel als Viertelkreis (rund statt spitz), darunter gerade nach unten weiter
            float r, y;
            if (v < 0.3f)
            {
                var t = v / 0.3f * MathF.PI / 2;
                r = kopfR * MathF.Sin(t);
                y = (oben - (kopfR * (1 - MathF.Cos(t)))) * H;
            }
            else
            {
                var q = (v - 0.3f) / 0.7f;
                r = kopfR + ((saumR - kopfR) * MathF.Pow(q, 1.4f));
                y = (oben - kopfR - (q * (oben - kopfR - unten))) * H;
            }
            y -= zipfelTief * H * (0.5f + (0.5f * MathF.Cos(zipfel * w))) * MathF.Pow(v, 8);
            // Zipfel oben nach hinten (Form c)
            var zHin = -spitze * H * MathF.Pow(MathF.Max(0, 0.3f - v) / 0.3f, 2);
            return new Vector3(MathF.Sin(w) * r * H, y, (MathF.Cos(w) * r * 0.85f * H) + zHin);
        }, (_, _) => farbe);
    }

    /// <summary>
    /// Geist (Nico P8-42): Laken mit rundem Kopf und welligem Saum, runde Augen, Mund "O"; der rechte Arm zeigt nach
    /// vorn und haelt eine Laterne (nur Glas und Kerze leuchten nachts), das Laken bleibt nachts so hell wie am Tag.
    /// </summary>
    private static Netz Geist(GrafikBibliothek g)
    {
        var n = new Netz();
        var f = new Form();
        var schwarz = Hell("#1E1B2E");
        var weiss = Hell("#EEF2FF");
        Laken(f, weiss, 0.95f, 0.2f, 0.27f, 6, 0.08f, 0);
        f.Ei(new Vector3(-0.07f * H, 0.76f * H, 0.15f * H), new Vector3(0.035f, 0.05f, 0.022f) * H, schwarz, 10, 6);
        f.Ei(new Vector3(0.07f * H, 0.76f * H, 0.15f * H), new Vector3(0.035f, 0.05f, 0.022f) * H, schwarz, 10, 6);
        f.Ei(new Vector3(0, 0.64f * H, 0.165f * H), new Vector3(0.03f, 0.042f, 0.02f) * H, schwarz, 10, 6);
        f.Ei(new Vector3(-0.25f * H, 0.5f * H, 0.06f * H), new Vector3(0.06f, 0.1f, 0.06f) * H, weiss, 8, 6, Matrix4x4.CreateRotationZ(-0.6f));
        f.Ei(new Vector3(0.19f * H, 0.53f * H, 0.17f * H), new Vector3(0.055f, 0.12f, 0.055f) * H, weiss, 8, 6, Matrix4x4.CreateRotationX(1.1f));
        Laterne(g, n, new Vector3(0.19f * H, 0.5f * H, 0.3f * H));
        f.In(n);
        return n;
    }

    /// <summary>Laterne (hw-lantern_standing, 0,2 H hoch), haengt mit der Oberkante an <paramref name="oben"/>, leuchtet nachts.</summary>
    private static void Laterne(GrafikBibliothek g, Netz n, Vector3 oben)
    {
        if (g.Modell("hw-lantern_standing") is { } la)
        {
            var s = 0.2f * H / (la.Max.Y - la.Min.Y);
            var m = (la.Min + la.Max) / 2;
            n.Hinzu(la, Matrix4x4.CreateTranslation(-m.X, -la.Max.Y, -m.Z) * Matrix4x4.CreateScale(s) * Matrix4x4.CreateTranslation(oben), (d, _, _) => Glut(la, d));
        }
    }

    /// <summary>
    /// Weihnachtsmann a1 (Nico P8-42): eigene runde, gemuetliche Form: Stiefel, Bauch, Pelzsaum, Guertel, Arme, Kopf,
    /// weicher Bart, Muetze mit Bommel, Sack auf dem Ruecken.
    /// </summary>
    private static Netz Weihnachtsmann()
    {
        var rot = F("#C81E1E");
        var weiss = F("#F8FAFC");
        var haut = F("#F2C6A0");
        var schwarz = F("#151515");
        var n = new Netz();
        var f = new Form();
        // rund und gemuetlich, eigene Form: Stiefel, Bauch, Pelzsaum, Guertel, Arme, Kopf, weicher Bart, Muetze mit Bommel
        foreach (var sx in new[] { -1f, 1f })
        {
            f.Ei(new Vector3(sx * 0.075f * H, 0.045f * H, 0.02f * H), new Vector3(0.06f, 0.045f, 0.09f) * H, schwarz, 10, 6);
            f.Ei(new Vector3(sx * 0.075f * H, 0.13f * H, 0), new Vector3(0.06f, 0.08f, 0.06f) * H, rot, 10, 6);
            f.Ei(new Vector3(sx * 0.215f * H, 0.47f * H, 0.02f * H), new Vector3(0.06f, 0.13f, 0.06f) * H, rot, 10, 6, Matrix4x4.CreateRotationZ(sx * 0.25f));
            f.Ei(new Vector3(sx * 0.245f * H, 0.355f * H, 0.025f * H), new Vector3(0.065f, 0.028f, 0.065f) * H, weiss, 10, 4);
            f.Ei(new Vector3(sx * 0.25f * H, 0.31f * H, 0.03f * H), new Vector3(0.045f, 0.045f, 0.045f) * H, schwarz, 8, 6);
        }
        f.Ei(new Vector3(0, 0.41f * H, 0), new Vector3(0.2f, 0.25f, 0.18f) * H, rot, 16, 10);
        f.Ei(new Vector3(0, 0.2f * H, 0), new Vector3(0.135f, 0.03f, 0.125f) * H, weiss, 14, 4);
        f.Ei(new Vector3(0, 0.36f * H, 0), new Vector3(0.205f, 0.03f, 0.185f) * H, schwarz, 16, 4);
        f.Ei(new Vector3(0, 0.36f * H, 0.183f * H), new Vector3(0.035f, 0.03f, 0.01f) * H, F("#FACC15"), 6, 4);
        f.Ei(new Vector3(0, 0.5f * H, 0.16f * H), new Vector3(0.028f, 0.13f, 0.02f) * H, weiss, 6, 6);
        // Kopf, Augen, Nase, Bart, Schnurrbart
        f.Ei(new Vector3(0, 0.75f * H, 0), new Vector3(0.11f, 0.11f, 0.1f) * H, haut, 14, 8);
        f.Ei(new Vector3(-0.04f * H, 0.785f * H, 0.093f * H), new Vector3(0.014f, 0.018f, 0.008f) * H, schwarz, 6, 4);
        f.Ei(new Vector3(0.04f * H, 0.785f * H, 0.093f * H), new Vector3(0.014f, 0.018f, 0.008f) * H, schwarz, 6, 4);
        f.Ei(new Vector3(0, 0.75f * H, 0.1f * H), new Vector3(0.022f, 0.02f, 0.02f) * H, F("#E8826B"), 8, 4);
        f.Ei(new Vector3(0, 0.64f * H, 0.035f * H), new Vector3(0.12f, 0.11f, 0.085f) * H, weiss, 14, 8);
        f.Ei(new Vector3(-0.036f * H, 0.722f * H, 0.1f * H), new Vector3(0.04f, 0.018f, 0.02f) * H, weiss, 8, 4, Matrix4x4.CreateRotationZ(0.3f));
        f.Ei(new Vector3(0.036f * H, 0.722f * H, 0.1f * H), new Vector3(0.04f, 0.018f, 0.02f) * H, weiss, 8, 4, Matrix4x4.CreateRotationZ(-0.3f));
        // Muetze: Kegel mit nach hinten fallender Spitze, Pelzrand, Bommel
        f.Raster(14, 6, (u, v) =>
        {
            var w = u * MathF.Tau;
            var r = 0.108f * (1 - v) * H;
            var y = (0.82f + (v * 0.2f)) * H;
            var z = -0.09f * H * v * v;
            return new Vector3(MathF.Sin(w) * r, y, (MathF.Cos(w) * r) + z);
        }, (_, _) => rot);
        f.Ei(new Vector3(0, 0.83f * H, 0), new Vector3(0.12f, 0.03f, 0.112f) * H, weiss, 14, 4);
        f.Ei(new Vector3(0, 1.02f * H, -0.09f * H), new Vector3(0.035f, 0.035f, 0.035f) * H, weiss, 8, 6);
        // Sack auf dem Ruecken
        f.Ei(new Vector3(0, 0.56f * H, -0.21f * H), new Vector3(0.14f, 0.17f, 0.1f) * H, F("#8B5E3C"), 12, 8);
        f.Ei(new Vector3(0, 0.74f * H, -0.21f * H), new Vector3(0.04f, 0.03f, 0.04f) * H, F("#6B4423"), 8, 4);
        f.In(n);
        return n;
    }

    private static Netz Modell(GrafikBibliothek g, string name, float hoehe, Func<byte, byte, byte, Vector3?>? umfaerben = null, float breit = 1f)
    {
        var m = g.Modell(name)!;
        var s = hoehe / (m.Max.Y - m.Min.Y);
        var mitte = (m.Min + m.Max) / 2;
        var lage = Matrix4x4.CreateTranslation(-mitte.X, -m.Min.Y, -mitte.Z) * Matrix4x4.CreateScale(s * breit, s, s);
        var n = new Netz();
        if (umfaerben is null)
        {
            n.Hinzu(m, lage);
            return n;
        }
        var f = (byte[])m.Farben.Clone();
        for (var d = 0; d < m.Dreiecke; d++)
        {
            if (umfaerben(f[d * 3], f[(d * 3) + 1], f[(d * 3) + 2]) is { } neu)
            {
                f[d * 3] = (byte)(neu.X * 255);
                f[(d * 3) + 1] = (byte)(neu.Y * 255);
                f[(d * 3) + 2] = (byte)(neu.Z * 255);
            }
        }
        n.Hinzu(new GrafikModell(m.Name, m.Ecken, f), lage);
        return n;
    }

    /// <summary>Das Huhn ist etwas schmaler als das Modell (Fluegel angelegt), damit es in der Innenkurve auf der Fahrbahn bleibt.</summary>
    private const float Schmal = 0.85f;

    private static Netz Huhn(GrafikBibliothek g, float hoehe)
    {
        var n = Modell(g, "os-animal-chick", hoehe, (r, gg, b) =>
        {
            // gelbes Gefieder weiss, Helligkeit behalten
            if (r > 170 && gg > 140 && b < 140 && r - b > 80 && gg - b > 60 && r - gg < 70)
            {
                var hell = Math.Clamp((r + gg) / 2f / 230f, 0.7f, 1.1f);
                return new Vector3(0.97f, 0.96f, 0.92f) * hell;
            }
            return null;
        }, Schmal);
        var m = g.Modell("os-animal-chick")!;
        var s = hoehe / (m.Max.Y - m.Min.Y);
        float b2 = (m.Max.X - m.Min.X) * s * Schmal, t = (m.Max.Z - m.Min.Z) * s;
        // roter Kamm oben und Kehllappen vorn
        n.Hinzu(Q(-b2 * 0.07f, hoehe, -t * 0.15f, b2 * 0.07f, hoehe * 1.16f, t * 0.25f, "#DC2626"));
        n.Hinzu(Q(-b2 * 0.07f, hoehe * 1.1f, t * 0.05f, b2 * 0.07f, hoehe * 1.22f, t * 0.18f, "#DC2626"));
        n.Hinzu(Q(-b2 * 0.05f, hoehe * 0.42f, t * 0.5f, b2 * 0.05f, hoehe * 0.56f, t * 0.56f, "#DC2626"));
        return n;
    }

    /// <summary>Glied als Ellipsoid von a nach b (Halbmesser r quer), Masse in Weltmass.</summary>
    private static void Glied(Form f, Vector3 a, Vector3 b, float r, Vector3 farbe, int nu = 8, int nv = 6)
    {
        var d = b - a;
        var l = d.Length();
        var z = d / l;
        var achse = Vector3.Cross(Vector3.UnitY, z);
        var s = achse.Length();
        var dreh = s < 1e-5f ? (z.Y > 0 ? Matrix4x4.Identity : Matrix4x4.CreateRotationX(MathF.PI))
            : Matrix4x4.CreateFromAxisAngle(achse / s, MathF.Atan2(s, z.Y));
        f.Ei((a + b) / 2, new Vector3(r, (l / 2) + (r * 0.4f), r), farbe, nu, nv, dreh);
    }

    /// <summary>Punkt in H-Einheiten (x, y, z) in Weltmass.</summary>
    private static Vector3 P(float x, float y, float z) => new Vector3(x, y, z) * H;

    /// <summary>Gelenke einer Puppe, Weltmass.</summary>
    private sealed record Gelenke(Vector3[] Huefte, Vector3[] Knie, Vector3[] Fuss, Vector3[] Schulter, Vector3[] Ellbogen, Vector3[] Hand, Vector3 Kopf);

    /// <summary>
    /// Gelenke im Schrittbild <paramref name="bild"/> (<see cref="Schrittbilder"/>): 0 = linkes Bein und rechter Arm vorn,
    /// 1 = Beine zusammen (Mitte des Schritts), 2 = gespiegelt (rechtes Bein und linker Arm vorn).
    /// </summary>
    private static Gelenke Stellung(int bild, float hueft = 0.46f, float schulterY = 0.7f, float schulterX = 0.15f, float kopfY = 0.85f,
        float bein = 0.22f, float arm = 0.15f, float schritt = 0.35f, (float O, float U)? armeVor = null)
    {
        if (bild == 1)
        {
            schritt = 0;
        }
        Vector3 R(float w, float l) => new(0, -MathF.Cos(w) * l, MathF.Sin(w) * l);
        var huefte = new[] { P(-0.07f, hueft, 0), P(0.07f, hueft, 0) };
        // links vorn fast gestreckt, rechts hinten mit gebeugtem Knie
        var knie = new[] { huefte[0] + (R(schritt, bein) * H), huefte[1] + (R(-schritt * 0.85f, bein) * H) };
        var fuss = new[] { knie[0] + (R(schritt * 0.4f, bein * 0.95f) * H), knie[1] + (R(-schritt * 1.7f, bein * 0.95f) * H) };
        var schulter = new[] { P(-schulterX, schulterY, 0), P(schulterX, schulterY, 0) };
        Vector3[] ell, hand;
        if (armeVor is { } av)
        {
            ell = [schulter[0] + (R(av.O, arm) * H), schulter[1] + (R(av.O, arm) * H)];
            hand = [ell[0] + (R(av.U, arm) * H), ell[1] + (R(av.U, arm) * H)];
        }
        else
        {
            // Gegenschwung: linker Arm hinten, rechter vorn
            ell = [schulter[0] + (R(-schritt * 0.8f, arm) * H), schulter[1] + (R(schritt * 0.8f, arm) * H)];
            hand = [ell[0] + (R(-schritt * 0.5f, arm * 0.95f) * H), ell[1] + (R(schritt * 1.6f, arm * 0.95f) * H)];
        }
        if (bild == 2)
        {
            // Spiegelbild: links und rechts tauschen
            Vector3[] S(Vector3[] a) => [new(-a[1].X, a[1].Y, a[1].Z), new(-a[0].X, a[0].Y, a[0].Z)];
            return new(S(huefte), S(knie), S(fuss), S(schulter), S(ell), S(hand), P(0, kopfY, 0));
        }
        return new(huefte, knie, fuss, schulter, ell, hand, P(0, kopfY, 0));
    }

    /// <summary>Ring (flacher Torus) waagerecht um die Mitte m.</summary>
    private static void Ring(Form f, Vector3 m, float rx, float rz, float dick, Vector3 farbe, float hinten = 1f) =>
        f.Raster(16, 6, (u, v) =>
        {
            var w = u * MathF.Tau;
            var t = v * MathF.Tau;
            var r = 1 + (dick / rx * MathF.Cos(t));
            var z = MathF.Cos(w) * rz * r;
            return m + new Vector3(MathF.Sin(w) * rx * r, dick * MathF.Sin(t), z < 0 ? z * hinten : z);
        }, (_, _) => farbe);

    /// <summary>Umhang hinter dem Koerper: von den Schultern (oben) zum Saum (unten), offen nach vorn.</summary>
    private static void Umhang(Form f, float oben, float unten, float rOben, float rUnten, Vector3 aussen, Vector3 innen, float offen = 0.35f)
    {
        foreach (var (dick, farbe) in new[] { (0f, innen), (0.012f, aussen) })
        {
            f.Raster(16, 8, (u, v) =>
            {
                var w = (offen * MathF.PI) + (u * (2 - (2 * offen)) * MathF.PI);
                var r = rOben + ((rUnten - rOben) * v) + dick;
                var y = oben - ((oben - unten) * v);
                // Saum leicht wellig
                y += 0.015f * MathF.Sin(w * 7) * v * v;
                return new Vector3(MathF.Sin(w) * r, y, MathF.Cos(w) * r * 0.85f) * H;
            }, (_, _) => farbe);
        }
    }

    /// <summary>Glieder einer Puppe: Beine, Arme, Fuesse, Haende; Farben je Teil.</summary>
    private static void Glieder(Form f, Gelenke g, float rBein, float rArm, Vector3 bein, Vector3 unterbein, Vector3 schuh, Vector3 arm, Vector3 hand, float rHand = 0.04f, float fuss = 1f)
    {
        for (var i = 0; i < 2; i++)
        {
            Glied(f, g.Huefte[i], g.Knie[i], rBein * H, bein);
            Glied(f, g.Knie[i], g.Fuss[i], rBein * 0.9f * H, unterbein);
            f.Ei(g.Fuss[i] + P(0, -0.005f, 0.035f * fuss), new Vector3(0.05f, 0.035f, 0.08f) * H * fuss, schuh, 10, 6);
            Glied(f, g.Schulter[i], g.Ellbogen[i], rArm * H, arm);
            Glied(f, g.Ellbogen[i], g.Hand[i], rArm * 0.9f * H, arm);
            f.Ei(g.Hand[i], new Vector3(rHand, rHand, rHand) * H, hand, 8, 6);
        }
    }

    /// <summary>Skelett c (Nico P8-42): Kostuem, schwarzer Anzug mit aufgedruckten Knochen, Totenkopf-Maske, Kuerbis-Eimer.</summary>
    private static Netz Skelett(GrafikBibliothek g, int bild)
    {
        var n = new Netz();
        var f = new Form();
        var knochen = Hell("#EDE6D6");
        var dunkel = F("#1E1B2E");
        var st = Stellung(bild);
        // Kostuem: schwarzer Anzug mit aufgedruckten Knochen, Totenkopf-Maske, Kuerbis-Eimer
        var stoff = F("#1C1A24");
        Glieder(f, st, 0.055f, 0.045f, stoff, stoff, F("#2A2A2A"), stoff, F("#1C1A24"));
        for (var i = 0; i < 2; i++)
        {
            // Knochen-Aufdruck vorn auf Beinen und Armen
            Glied(f, st.Huefte[i] + P(0, -0.02f, 0.045f), st.Knie[i] + P(0, 0.02f, 0.045f), 0.016f * H, knochen, 6, 4);
            Glied(f, st.Knie[i] + P(0, -0.02f, 0.04f), st.Fuss[i] + P(0, 0.04f, 0.04f), 0.015f * H, knochen, 6, 4);
            Glied(f, st.Schulter[i] + P(0, -0.02f, 0.035f), st.Ellbogen[i] + P(0, 0.01f, 0.035f), 0.013f * H, knochen, 6, 4);
        }
        f.Ei(P(0, 0.58f, 0), new Vector3(0.14f, 0.16f, 0.1f) * H, stoff, 14, 8);
        f.Ei(P(0, 0.46f, 0), new Vector3(0.12f, 0.06f, 0.09f) * H, stoff, 12, 4);
        for (var k = 0; k < 3; k++)
        {
            var y = 0.64f - (k * 0.05f);
            foreach (var sx in new[] { -1f, 1f })
            {
                f.Ei(P(sx * 0.055f, y, 0.093f), new Vector3(0.05f, 0.012f, 0.012f) * H, knochen, 8, 4, Matrix4x4.CreateRotationZ(sx * 0.25f));
            }
        }
        f.Ei(P(0, 0.59f, 0.098f), new Vector3(0.012f, 0.1f, 0.01f) * H, knochen, 6, 4);
        KopfTotenkopf(f, st.Kopf, knochen, dunkel, 1.05f);
        // Kapuze hinten
        f.Ei(st.Kopf + P(0, 0.01f, -0.03f), new Vector3(0.135f, 0.135f, 0.12f) * H, stoff, 12, 8);
        // Kuerbis-Eimer in der rechten Hand (leuchtet nachts)
        KuerbisEimer(g, n, st.Hand[1] + P(0, -0.1f, 0.02f), 0.13f);
        f.In(n);
        return n;
    }

    /// <summary>Totenkopf: Schaedel, Kiefer, Augenhoehlen, Nase, Zaehne (freundlich grinsend).</summary>
    private static void KopfTotenkopf(Form f, Vector3 k, Vector3 knochen, Vector3 dunkel, float s)
    {
        f.Ei(k + P(0, 0.015f, 0), new Vector3(0.12f, 0.115f, 0.11f) * H * s, knochen, 14, 10);
        f.Ei(k + P(0, -0.07f, 0.02f), new Vector3(0.08f, 0.05f, 0.08f) * H * s, knochen, 12, 6);
        foreach (var sx in new[] { -1f, 1f })
        {
            f.Ei(k + (P(sx * 0.045f, 0.01f, 0.095f) * s), new Vector3(0.034f, 0.038f, 0.02f) * H * s, dunkel, 10, 6);
        }
        f.Ei(k + (P(0, -0.035f, 0.107f) * s), new Vector3(0.014f, 0.016f, 0.01f) * H * s, dunkel, 6, 4);
        f.Ei(k + (P(0, -0.075f, 0.093f) * s), new Vector3(0.055f, 0.016f, 0.012f) * H * s, dunkel, 10, 4);
        for (var z = -2; z <= 2; z++)
        {
            f.Ei(k + (P(z * 0.02f, -0.075f, 0.1f) * s), new Vector3(0.008f, 0.012f, 0.007f) * H * s, knochen, 6, 4);
        }
    }

    /// <summary>
    /// P8-43: nur das Licht leuchtet nachts: Glas und Kerze (hell gelb-orange) bzw. das ausgeschnittene Gesicht
    /// (grau beim KayKit-Kuerbis); Rahmen, Griff, Dach und Schale behalten ihre Farbe.
    /// </summary>
    public static Vector3 Glut(GrafikModell m, int d)
    {
        var f = new Vector3(m.Farben[d * 3], m.Farben[(d * 3) + 1], m.Farben[(d * 3) + 2]) / 255f;
        return IstLicht(m, d) ? f + new Vector3(Nachtlicht.Marke) : f;
    }

    /// <summary>Ist das Dreieck d des Modells ein Licht (Glas, Kerze, ausgeschnittenes Gesicht)? Siehe <see cref="Glut"/>.</summary>
    public static bool IstLicht(GrafikModell m, int d)
    {
        int r = m.Farben[d * 3], g = m.Farben[(d * 3) + 1], b = m.Farben[(d * 3) + 2];
        return IstLichtFarbe(r, g, b) || (m.Name.StartsWith("hw-pumpkin", StringComparison.Ordinal) && r == g && g == b && r < 60);
    }

    /// <summary>Farbe eines Lichts: hell gelb-orange (Glas, Kerze, Flamme).</summary>
    public static bool IstLichtFarbe(int r, int g, int b) => r >= 240 && g >= 120 && b < 120;

    /// <summary>Kuerbis-Eimer (Graveyard Kit, Gesicht leuchtet nachts); m = Mitte unten.</summary>
    private static void KuerbisEimer(GrafikBibliothek g, Netz n, Vector3 m, float hoehe)
    {
        if ((g.Modell("gy-pumpkin-carved") ?? g.Modell("hw-pumpkin_orange_jackolantern")) is { } k)
        {
            var s = hoehe * H / (k.Max.Y - k.Min.Y);
            var mitte = (k.Min + k.Max) / 2;
            n.Hinzu(k, Matrix4x4.CreateTranslation(-mitte.X, -k.Min.Y, -mitte.Z) * Matrix4x4.CreateScale(s) * Matrix4x4.CreateTranslation(m), (d, _, _) => Glut(k, d));
        }
    }

    /// <summary>
    /// Zombie a (Nico P8-45): verkleidet mit Gummimaske, lila Kapuzenpulli, Jeans, Kuerbis-Eimer; nachts leuchtet nur
    /// das Gesicht des Kuerbisses.
    /// </summary>
    private static Netz Zombie(GrafikBibliothek g, int bild)
    {
        var n = new Netz();
        var f = new Form();
        var haut = Hell("#F2C9A0");
        var dunkel = F("#1E1B2E");
        var weiss = Hell("#F8FAFC");
        var st = Stellung(bild);
        var k = st.Kopf;
        // Gummimaske: lila Kapuzenpulli, Jeans, gruene Maske mit Rand am Hals, Kuerbis-Eimer
        var pulli = F("#7C3AED");
        var jeans = F("#2563EB");
        var maske = Hell("#7FB069");
        var maskeDunkel = Hell("#5E8C4A");
        Glieder(f, st, 0.052f, 0.044f, jeans, jeans, F("#F3F4F6"), pulli, haut, 0.038f);
        f.Ei(P(0, 0.58f, 0), new Vector3(0.145f, 0.16f, 0.1f) * H, pulli, 14, 8);
        f.Ei(P(0, 0.46f, 0), new Vector3(0.12f, 0.06f, 0.09f) * H, jeans, 12, 4);
        // Bauchtasche und Kapuze (hinten heruntergeklappt), Kordeln
        f.Ei(P(0, 0.52f, 0.088f), new Vector3(0.09f, 0.04f, 0.02f) * H, F("#6D28D9"), 10, 4);
        f.Ei(P(0, 0.72f, -0.08f), new Vector3(0.11f, 0.06f, 0.05f) * H, pulli, 10, 6);
        foreach (var sx in new[] { -1f, 1f })
        {
            Glied(f, P(sx * 0.03f, 0.73f, 0.085f), P(sx * 0.032f, 0.64f, 0.1f), 0.006f * H, weiss, 4, 3);
        }
        // Maske: etwas knubbelig, Rand am Hals sichtbar, Hals darunter in Hautfarbe
        f.Ei(k + P(0, -0.105f, 0), new Vector3(0.05f, 0.04f, 0.05f) * H, haut, 8, 4);
        f.Raster(16, 10, (u, v) =>
        {
            float w = u * MathF.Tau, t = v * MathF.PI;
            var buckel = 1 + (0.06f * MathF.Sin(w * 3) * MathF.Sin(t * 2));
            return k + (new Vector3(MathF.Sin(t) * MathF.Sin(w) * 0.125f, MathF.Cos(t) * 0.125f, MathF.Sin(t) * MathF.Cos(w) * 0.115f) * H * buckel);
        }, (_, _) => maske);
        Ring(f, k + P(0, -0.1f, 0), 0.085f * H, 0.08f * H, 0.012f * H, maskeDunkel);
        // grosse Comic-Augen (ungleich), Nase, zugenaehter Mund, Ohren, Haarbueschel
        f.Ei(k + P(-0.045f, 0.025f, 0.1f), new Vector3(0.035f, 0.038f, 0.018f) * H, weiss, 10, 6);
        f.Ei(k + P(0.048f, 0.02f, 0.1f), new Vector3(0.028f, 0.03f, 0.016f) * H, weiss, 10, 6);
        f.Ei(k + P(-0.04f, 0.02f, 0.117f), new Vector3(0.013f, 0.013f, 0.006f) * H, dunkel, 6, 4);
        f.Ei(k + P(0.044f, 0.016f, 0.115f), new Vector3(0.011f, 0.011f, 0.005f) * H, dunkel, 6, 4);
        f.Ei(k + P(0, -0.01f, 0.12f), new Vector3(0.018f, 0.022f, 0.014f) * H, maskeDunkel, 6, 4);
        f.Ei(k + P(0, -0.055f, 0.108f), new Vector3(0.05f, 0.008f, 0.007f) * H, dunkel, 8, 4);
        for (var i = -2; i <= 2; i++)
        {
            f.Ei(k + P(i * 0.02f, -0.055f, 0.113f), new Vector3(0.003f, 0.016f, 0.003f) * H, weiss, 4, 3);
        }
        foreach (var sx in new[] { -1f, 1f })
        {
            f.Ei(k + P(sx * 0.125f, 0, 0), new Vector3(0.018f, 0.032f, 0.02f) * H, maske, 6, 4);
        }
        for (var i = 0; i < 3; i++)
        {
            f.Ei(k + P(-0.02f + (i * 0.02f), 0.13f, 0.01f), new Vector3(0.012f, 0.035f, 0.012f) * H, F("#4B5563"), 5, 4, Matrix4x4.CreateRotationZ(-0.4f + (i * 0.4f)));
        }
        KuerbisEimer(g, n, st.Hand[1] + P(0, -0.1f, 0.02f), 0.13f);
        f.In(n);
        return n;
    }

    /// <summary>
    /// Vampir c (Nico P8-42/P8-43): grauer Anzug, weinroter Umhang, Stehkragen, Eckzaehne, rot leuchtende Augen,
    /// Fledermaus ueber der Schulter (nachts so hell wie am Tag, Augen leuchten gelb).
    /// </summary>
    private static Netz Vampir(int bild)
    {
        var n = new Netz();
        var f = new Form();
        var leucht = new Form();
        var haut = Hell("#ECE4F2");
        var dunkel = F("#1E1B2E");
        var st = Stellung(bild);
        var anzug = F("#9CA3AF");
        var umhangAussen = F("#4C0519");
        var umhangInnen = F("#6B7280");
        var weste = F("#4C0519");
        Glieder(f, st, 0.048f, 0.04f, anzug, anzug, F("#0F0F0F"), anzug, Hell("#F1F5F9"), 0.035f);
        const float ry = 0.585f;
        f.Ei(P(0, ry, 0), new Vector3(0.135f, 0.15f, 0.1f) * H, anzug, 14, 8);
        f.Ei(P(0, ry + 0.01f, 0.06f), new Vector3(0.07f, 0.13f, 0.05f) * H, weste, 10, 6);
        f.Ei(P(0, ry + 0.09f, 0.088f), new Vector3(0.03f, 0.04f, 0.015f) * H, Hell("#F8FAFC"), 8, 4);
        f.Ei(P(0, 0.47f, 0), new Vector3(0.12f, 0.05f, 0.09f) * H, anzug, 12, 4);
        const float schulter = 0.72f;
        Umhang(f, schulter, 0.1f, 0.15f, 0.24f, umhangAussen, umhangInnen);
        // Stehkragen innen rot
        foreach (var sx in new[] { -1f, 1f })
        {
            f.Ei(P(sx * 0.1f, schulter + 0.07f, -0.04f), new Vector3(0.07f, 0.09f, 0.012f) * H, umhangInnen, 8, 6,
                Matrix4x4.CreateRotationY(sx * 0.6f) * Matrix4x4.CreateRotationZ(sx * -0.35f));
        }
        var k = st.Kopf;
        const float kr = 0.115f;
        const float s = 1f;
        f.Ei(k, new Vector3(kr, kr * 1.05f, kr * 0.92f) * H, haut, 14, 10);
        // Haare mit Spitze in der Stirn
        f.Ei(k + (P(0, 0.035f, -0.015f) * s), new Vector3(0.118f, 0.09f, 0.105f) * H * s, F("#0B0B12"), 14, 8);
        f.Ei(k + (P(0, 0.07f, 0.072f) * s), new Vector3(0.016f, 0.02f, 0.012f) * H * s, F("#0B0B12"), 6, 4, Matrix4x4.CreateRotationZ(MathF.PI / 4));
        // Augen leuchten rot, Mund mit zwei Eckzaehnen
        foreach (var sx in new[] { -1f, 1f })
        {
            f.Ei(k + (P(sx * 0.04f, 0.005f, 0.092f) * s), new Vector3(0.02f, 0.016f, 0.01f) * H * s, Hell("#F8FAFC"), 8, 4);
            leucht.Ei(k + (P(sx * 0.04f, 0.004f, 0.1f) * s), new Vector3(0.01f, 0.01f, 0.005f) * H * s, F("#DC2626"), 6, 4);
            f.Ei(k + (P(sx * 0.018f, -0.06f, 0.098f) * s), new Vector3(0.006f, 0.014f, 0.005f) * H * s, Hell("#FFFFFF"), 4, 4);
        }
        f.Ei(k + (P(0, -0.048f, 0.1f) * s), new Vector3(0.035f, 0.008f, 0.006f) * H * s, F("#7F1D1D"), 8, 4);
        {
            // Fledermaus ueber der rechten Schulter (zum Gehweg hin, damit entgegenkommende Gestalten Platz haben),
            // violett-grau (P8-43)
            var b = k + P(-0.1f, 0.2f, 0.03f);
            var fell = Hell("#8B7AA8");
            var fluegel = Hell("#6D5C8A");
            f.Ei(b, new Vector3(0.045f, 0.042f, 0.045f) * H, fell, 8, 6);
            f.Ei(b + P(0, 0.055f, 0.008f), new Vector3(0.034f, 0.034f, 0.034f) * H, fell, 8, 6);
            foreach (var sx in new[] { -1f, 1f })
            {
                f.Ei(b + P(sx * 0.016f, 0.095f, 0), new Vector3(0.011f, 0.024f, 0.008f) * H, fell, 4, 4);
                f.Ei(b + P(sx * 0.09f, 0.02f, 0), new Vector3(0.08f, 0.006f, 0.05f) * H, fluegel, 10, 3, Matrix4x4.CreateRotationZ(sx * -0.35f));
                leucht.Ei(b + P(sx * 0.014f, 0.062f, 0.032f), new Vector3(0.008f, 0.008f, 0.004f) * H, F("#FACC15"), 4, 4);
            }
        }
        f.In(n);
        leucht.In(n, leuchten: true);
        return n;
    }
}
