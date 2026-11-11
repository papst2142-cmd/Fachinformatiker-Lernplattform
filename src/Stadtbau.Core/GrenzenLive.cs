using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle D2 (Weisung P12-157, Kurzplan P12-97 Punkte b und c, Nico 3a): die Grenzen der Stadt live, ohne Grafikkarte
/// (<see cref="StadtbauGrenzen.Draussen"/>, dieselben Regeln wie das Zeichnen des Spiels). Gerechnet in der Szene der
/// Grenzen (Nacht, Winter, Weihnachten: die meisten Netze), mit dem Probe-Spielstand wie die 3D-Ansicht:
/// <list type="bullet">
/// <item>Gesamtansicht: Kamera ganz weit weg ueber der Mitte, Neigung des Spiels (wie „Grenzen messen“).</item>
/// <item>Nahansicht: Kamerastellen alle <see cref="RasterSchritt"/> Felder ueber die ganze Stadt, Abstand 4, 6, 10, 16,
/// Blick nach Norden und Sueden (wie der Start-Test des Spiels); gewertet wird das teuerste Bild.</item>
/// <item>Waermekarte: je Feld 1 × 1 der Karte die Dreiecke der nahen Netze (Mitte jedes Dreiecks), nur auf der Karte.</item>
/// </list>
/// Zu jedem Bild kommt, was sich bewegt (Autos oder Festgestalten, Figur, Kreis; <see cref="StadtbauSpiel.Bewegt"/>).
/// Bildformat 16:9 (<see cref="BildBreite"/> × <see cref="BildHoehe"/>, PC; das Tablet zeigt mit 16:10 etwas weniger).
/// </summary>
public sealed record GrenzenLive(
    (long Dreiecke, int Aufrufe) Gesamt,
    (long Dreiecke, int Aufrufe) Nah,
    float NahX, float NahZ, float NahAbstand, float NahDrehung,
    int Breite, int Tiefe,
    long[] Felder,
    int Bilder)
{
    public const float RasterSchritt = 2f;

    /// <summary>Welle Grafik G1: Anteil des teuersten Bilds an der Grenze der Stufe Niedrig (nur Info, geprueft wird gegen Mittel).</summary>
    public double AnteilNiedrig { get; init; }

    /// <summary>Welle Q1: Dreiecke der Questgeber draussen, in jedem Bild mitgezaehlt (0 = keine).</summary>
    public long QuestgeberDreiecke { get; init; }

    /// <summary>Welle Grafik G1: Anteil des teuersten Bilds an der Grenze der Stufe Hoch (nur Info).</summary>
    public double AnteilHoch { get; init; }

    /// <summary>Info-Zeile unter dem Balken, z. B. „Niedrig 81 % · Hoch 70 % (geprüft wird gegen Mittel)“.</summary>
    public string StufenInfo => GrafikTexte.WerkzeugInfo(AnteilNiedrig, AnteilHoch);

    public const int BildBreite = 1600;

    public const int BildHoehe = 900;

    /// <summary>Gelb ab diesem Anteil an der Grenze (Kurzplan P12-97 Frage 3: gruen bis 75 %, gelb bis 100 %, rot darueber).</summary>
    public const double GelbAb = 0.75;

    /// <summary>Das teurere der beiden Bilder (nach dem groesseren Anteil an der Grenze).</summary>
    public (long Dreiecke, int Aufrufe) Teuerstes =>
        StadtbauGrenzen.Anteil(Nah.Dreiecke, Nah.Aufrufe) > StadtbauGrenzen.Anteil(Gesamt.Dreiecke, Gesamt.Aufrufe) ? Nah : Gesamt;

    /// <summary>Anteil am Balken (der groessere von Dreiecken und Aufrufen im teuersten Bild).</summary>
    public double Anteil => StadtbauGrenzen.Anteil(Teuerstes.Dreiecke, Teuerstes.Aufrufe);

    public bool Ueber => Gesamt.Dreiecke > StadtbauGrenzen.Dreiecke || Gesamt.Aufrufe > StadtbauGrenzen.Aufrufe
        || Nah.Dreiecke > StadtbauGrenzen.Dreiecke || Nah.Aufrufe > StadtbauGrenzen.Aufrufe;

    /// <summary>Dreiecke im Feld (x, y); ausserhalb der Karte 0.</summary>
    public long Feld(int x, int y) => x < 0 || y < 0 || x >= Breite || y >= Tiefe ? 0 : Felder[(y * Breite) + x];

    /// <summary>Untere Grenzen der Stufen der Waermekarte (Dreiecke je Feld), Stufe 0 = ab 1.</summary>
    public static readonly long[] WaermeStufen = [1, 101, 301, 1001, 3001];

    /// <summary>Farben der Stufen (blau, gruen, gelb, orange, rot), halb durchsichtig gezeichnet.</summary>
    public static readonly string[] WaermeFarben = ["#4FC3F7", "#66BB6A", "#FFEE58", "#FFA726", "#E53935"];

    /// <summary>Stufe eines Felds (-1 = leer, nicht faerben).</summary>
    public static int Stufe(long dreiecke)
    {
        var s = -1;
        for (var i = 0; i < WaermeStufen.Length; i++)
        {
            if (dreiecke >= WaermeStufen[i])
            {
                s = i;
            }
        }
        return s;
    }

    /// <summary>Gegenprobe: ohne Instancing und Sichtkegel rechnen (Start-Test).</summary>
    public static bool OhneSparenFuerTests { get; set; }

    /// <summary>Rechnen (blockiert, im Hintergrund rufen). <paramref name="spiel"/> = gebaut mit <see cref="Ansicht3D.Einstellung.Grenzen"/>.</summary>
    /// <remarks>
    /// Welle Q1 (Kurzplan P12-111 Abschnitt 4): <paramref name="questgeber"/> = Dreiecke aller Questgeber draussen; sie
    /// zaehlen in jedem Bild mit (im Spiel erst ab Q2 gezeichnet, im Aufruf der Autos, kein neuer Aufruf).
    /// </remarks>
    public static GrenzenLive Rechnen(StadtSpiel spiel, long uhr, long questgeber = 0)
    {
        var szene = spiel.Szene;
        var fertig = szene.Vorbereiten();
        var zaehlung = new StadtbauGrenzen.Draussen(szene, fertig);
        var ohne = OhneSparenFuerTests;
        var bilder = 0;
        // Welle Grafik G1 (Weisung P12-182): gepruefte Grenze bleibt Mittel; Niedrig und Hoch nur als Info-Zeile, im selben
        // Durchgang gezaehlt (Sichtkegel je Haus einmal, Fernform/Gruen/Autos je Stufe)
        var stufen = new[] { Grafikstufen.Mittel, Grafikstufen.Niedrig, Grafikstufen.Hoch };
        var bewegt = new Dictionary<(bool Fern, bool Halb), (long Dreiecke, int Aufrufe)>();
        (long Dreiecke, int Aufrufe) Bewegt(bool fern, bool halb)
        {
            if (!bewegt.TryGetValue((fern, halb), out var b))
            {
                bewegt[(fern, halb)] = b = StadtbauSpiel.Bewegt(spiel, fern, uhr: uhr, halb: halb);
            }
            return b;
        }
        var hoechst = new (long Dreiecke, int Aufrufe)[stufen.Length];
        (long, int) Bild(float x, float z, float abstand, float drehung)
        {
            var k = new Kamera(x, z, abstand) { Neigung = Bauen3D.NeigungSpiel, Drehung = drehung };
            var je = zaehlung.BildJe(k.Sicht * k.Projektion(BildBreite, BildHoehe), abstand, stufen, ohne);
            bilder++;
            for (var i = 0; i < stufen.Length; i++)
            {
                var b = Bewegt(stufen[i].Fern(abstand), stufen[i].AutosHalb);
                var summe = (je[i].Dreiecke + b.Dreiecke + questgeber, je[i].Aufrufe + b.Aufrufe);
                hoechst[i] = (Math.Max(hoechst[i].Dreiecke, summe.Item1), Math.Max(hoechst[i].Aufrufe, summe.Item2));
                je[i] = summe;
            }
            return je[0];
        }
        var gesamt = Bild(szene.Breite / 2, szene.Tiefe / 2, Kamera.AbstandMax, 0);
        (long Dreiecke, int Aufrufe) nah = (-1, 0);
        float nx = 0, nz = 0, na = 0, nd = 0;
        var aufrufeMax = 0;
        foreach (var ab in NahAbstaende)
        {
            foreach (var dreh in new[] { 0f, 180f })
            {
                for (var x = RasterSchritt / 2; x < szene.Breite; x += RasterSchritt)
                {
                    for (var z = RasterSchritt / 2; z < szene.Tiefe; z += RasterSchritt)
                    {
                        var b = Bild(x, z, ab, dreh);
                        aufrufeMax = Math.Max(aufrufeMax, b.Item2);
                        if (b.Item1 > nah.Dreiecke)
                        {
                            (nah, nx, nz, na, nd) = (b, x, z, ab, dreh);
                        }
                    }
                }
            }
        }
        nah.Aufrufe = Math.Max(nah.Aufrufe, aufrufeMax);
        var (breite, tiefe) = ((int)MathF.Ceiling(szene.Breite), (int)MathF.Ceiling(szene.Tiefe));
        double AnteilVon(int i, Grafikstufe stufe)
        {
            var g = Grafikstufen.Grenze(stufe);
            return Math.Max((double)hoechst[i].Dreiecke / g.Dreiecke, (double)hoechst[i].Aufrufe / g.Aufrufe);
        }
        return new GrenzenLive(gesamt, nah, nx, nz, na, nd, breite, tiefe, Waerme(szene, fertig, breite, tiefe), bilder)
        {
            AnteilNiedrig = AnteilVon(1, Grafikstufe.Niedrig),
            AnteilHoch = AnteilVon(2, Grafikstufe.Hoch),
            QuestgeberDreiecke = questgeber,
        };
    }

    /// <summary>Stand eines Projekts bauen (Szene der Grenzen, mit <paramref name="vorrat"/> nur Geaendertes neu) und rechnen.</summary>
    /// <remarks>
    /// Welle Q2 (S 0.41.0): die Questgeber draussen zeichnet die Szene selbst (im Netz des Hauses ihres Orts), sie zaehlen dort mit wie im Spiel;
    /// die Zugabe aus Q1 (<see cref="QuestgeberDreieckeDraussen"/>) faellt darum weg (sonst doppelt gezaehlt).
    /// </remarks>
    public static GrenzenLive Fuer(Lernplattform.Core.Stadtbau.StadtFassung stand, Spieldaten daten, StadtSzene.Vorrat? vorrat = null) =>
        Rechnen(Ansicht3D.Bauen(stand, daten, Ansicht3D.Einstellung.Grenzen, vorrat), Ansicht3D.Einstellung.Grenzen.Uhr,
            QuestgeberZugabeAltFuerTests ? QuestgeberDreieckeDraussen(stand, daten) : 0);

    /// <summary>Gegenprobe Welle Q2: Questgeber draussen wie in Q1 zusaetzlich zaehlen (dann doppelt).</summary>
    public static bool QuestgeberZugabeAltFuerTests
    {
        get => ZugabeAlt.Value;
        set => ZugabeAlt.Value = value;
    }

    private static readonly AsyncLocal<bool> ZugabeAlt = new();

    /// <summary>Welle Q1: Dreiecke aller Questgeber draussen (Figur mit ihrem Aussehen, wie eine Person im Spiel).</summary>
    public static long QuestgeberDreieckeDraussen(Lernplattform.Core.Stadtbau.StadtFassung stand, Spieldaten daten)
    {
        if (Lernplattform.Core.Spiel.SpielDaten.Get(stand.Stadtdaten, QuestgeberDaten.Abschnitt) is null)
        {
            return 0;
        }
        return QuestgeberDaten.Alle(daten.Inhalt, Aussen.Aus(stand)).Where(q => q.Platz is { Draussen: true })
            .Sum(q => (long)(Figur.Netz(daten.Grafik, q.Aussehen, 1f).Indizes.Count / 3));
    }

    /// <summary>Kameraabstaende der Nahansicht (wie <c>AutoMessung.NahAbstaende</c> des Spiels).</summary>
    public static readonly float[] NahAbstaende = [4f, 6f, 10f, 16f];

    /// <summary>Dreiecke je Feld: nahe Netze der Stadt, Saetze an jeder Lage, Kleingruen, Paket-Netz nah, alle Haeuser nah.</summary>
    private static long[] Waerme(StadtSzene szene, StadtSzene.Fertig f, int breite, int tiefe)
    {
        var felder = new long[breite * tiefe];
        void Zaehlen(Netz netz, Matrix4x4? lage)
        {
            var e = netz.EckenFeld;
            var idx = netz.IndizesFeld;
            for (var i = 0; i + 2 < idx.Length; i += 3)
            {
                var m = Vector3.Zero;
                for (var k = 0; k < 3; k++)
                {
                    var o = (int)idx[i + k] * Netz.Zahlen;
                    m += new Vector3(e[o], e[o + 1], e[o + 2]);
                }
                m /= 3;
                if (lage is { } l)
                {
                    m = Vector3.Transform(m, l);
                }
                int x = (int)MathF.Floor(m.X), y = (int)MathF.Floor(m.Z);
                if (x >= 0 && y >= 0 && x < breite && y < tiefe)
                {
                    felder[(y * breite) + x]++;
                }
            }
        }
        Zaehlen(f.Stadt, null);
        if (f.Gruen is { } g)
        {
            Zaehlen(g, null);
        }
        if (f.PaketNah is { } p)
        {
            Zaehlen(p, null);
        }
        foreach (var h in szene.Haeuser)
        {
            Zaehlen(f.Haeuser[h], null);
        }
        foreach (var s in f.Instanzen?.Saetze ?? [])
        {
            foreach (var l in s.Lagen)
            {
                Zaehlen(s.Nah, l);
            }
        }
        return felder;
    }

    /// <summary>Gegenprobe Sammelliste 279: „Stadt zu teuer“ am Ende der Liste wie bis S 0.34.0 (nur Tests, je Ablauf).</summary>
    public static bool ZuletztAltFuerTests { get => ZuletztAlt.Value; set => ZuletztAlt.Value = value; }
    private static readonly AsyncLocal<bool> ZuletztAlt = new();

    /// <summary>
    /// Sammelliste 279 (Weisung P12-161/P12-164): die Funde der Pruefung mit „Stadt zu teuer“ <paramref name="teuer"/> ZUERST
    /// (er sperrt Speichern und Senden und betrifft die ganze Stadt, Nico soll ihn ohne Blaettern sehen).
    /// </summary>
    public static List<T> Zuerst<T>(IEnumerable<T> funde, T? teuer) where T : class =>
        teuer is null ? [.. funde] : ZuletztAlt.Value ? [.. funde, teuer] : [teuer, .. funde];

    /// <summary>
    /// Nico 3a: roter Fund „Stadt zu teuer“ mit der Stelle (teuerste Nahansicht, sonst die Mitte), solange ein Bild ueber
    /// der Grenze liegt; Stadt-Datei speichern und „An CC senden“ sind dann gesperrt wie bei jedem roten Fund.
    /// </summary>
    public StadtbauFund? Fund()
    {
        if (!Ueber)
        {
            return null;
        }
        var nahUeber = Nah.Dreiecke > StadtbauGrenzen.Dreiecke || Nah.Aufrufe > StadtbauGrenzen.Aufrufe;
        var (bild, x, z) = nahUeber ? (Nah, NahX, NahZ) : (Gesamt, Breite / 2f, Tiefe / 2f);
        return new StadtbauFund(Texte.StadtZuTeuer(nahUeber, bild.Dreiecke, bild.Aufrufe), Texte.StadtZuTeuerTipp, true, x, z);
    }

    /// <summary>Text fuer die Ausgabe des Start-Tests.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"Gesamt {Gesamt.Dreiecke}/{Gesamt.Aufrufe}, Nah {Nah.Dreiecke}/{Nah.Aufrufe} (Abstand {NahAbstand:F0}, Drehung {NahDrehung:F0}, bei {NahX:F1}/{NahZ:F1}), {Bilder} Bilder");
}
