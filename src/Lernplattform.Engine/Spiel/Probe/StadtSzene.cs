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
    Vector3 KastenMin, Vector3 KastenMax, Vector3 Schild)
{
    /// <summary>
    /// Wo die Figur vor dem Ort steht (P7-10 Nachtrag 1): ein kleines Stueck von der Tuer den Fussweg
    /// entlang nach draussen, damit sie nicht in der Tuer, auf dem Bahnsteig im Zug oder halb im Haus steht.
    /// </summary>
    public float StehX { get; init; } = TuerX;

    public float StehZ { get; init; } = TuerZ;

    /// <summary>Ansicht wie orte.json ("buero", "zuhause", "kunde:talheim", "serverfarm" ...), W5: welcher Innenraum.</summary>
    public string? Ansicht { get; init; }
}

/// <summary>
/// Ein Haus der Stadt mit seinen Teilen (Bereiche in <see cref="StadtSzene.Teile"/> und
/// <see cref="StadtSzene.Stuecke"/>) und seinem Kasten: steht die Figur dahinter, zeichnet die
/// Darstellung es halb durchsichtig (Sammelliste 102, W5).
/// </summary>
/// <summary>
/// W8b: Tuer eines Orts auf der Wand (Mitte), Richtung nach aussen, Anfang des Fusswegs; Modell = Tuer des CC0-Modells.
/// P8-28: Mitte liegt auf der echten Wand des Modells; Hoehe der Tuer, Hinten = wie weit ihr Rahmen nach hinten reicht.
/// </summary>
public sealed record StadtTuer(string Ort, Vector2 Mitte, Vector2 Aussen, Vector2 WegAnfang, bool Modell, float Hoehe = 0f, float Hinten = 0f)
{
    /// <summary>
    /// Welle A 0.70.0 (Weisung P12-218 Punkt 1 = a): die Tuer sitzt vorn an einem kleinen Vorbau (Windfang) bis an die Wand;
    /// Mitte = Vorderseite des Vorbaus. Die Regel „Tür bündig“ prueft so eine Tuer nicht (der Vorbau ist buendig gebaut).
    /// </summary>
    public bool Windfang { get; init; }

    /// <summary>Mit <see cref="Windfang"/>: Punkt auf der Hauswand hinter dem Vorbau (dort beginnt der Weg, dort prueft die Stadt die Tuer).</summary>
    public Vector2 AnWand { get; init; }
}

public sealed record StadtHaus(string Id, int TeileVon, int TeileBis, int StueckeVon, int StueckeBis, Vector3 Min, Vector3 Max)
{
    /// <summary>
    /// Welle A 0.70.0 (Sammelliste 314): feine Drehung des Hauses (15 bis 75 Grad nach den Vierteln). Ist sie gesetzt, sind
    /// <see cref="Min"/>/<see cref="Max"/> der achsenparallele Kasten um das gedrehte Haus, <see cref="LokalMin"/>/<see cref="LokalMax"/>
    /// der genaue Kasten im Rahmen des Hauses (<see cref="Hausdrehung.Feinlage.Zurueck"/>).
    /// </summary>
    public Hausdrehung.Feinlage Fein { get; init; }

    /// <summary>Kasten im Rahmen des Hauses (ohne feine Drehung gleich <see cref="Min"/>).</summary>
    public Vector3 LokalMin { get; init; } = Min;

    /// <summary>Kasten im Rahmen des Hauses (ohne feine Drehung gleich <see cref="Max"/>).</summary>
    public Vector3 LokalMax { get; init; } = Max;

    /// <summary>Liegt der Kartenpunkt (mit Rand) in der Grundflaeche des Hauses (genau, auch fein gedreht)?</summary>
    public bool Drin(Vector2 p, float rand = 0f) => Fein.Drin(LokalMin, LokalMax, p, rand);

    /// <summary>Ecken der Grundflaeche in der Welt (im Umlauf).</summary>
    public Vector2[] Grundflaeche => Fein.Ecken(LokalMin.X, LokalMin.Z, LokalMax.X, LokalMax.Z);

    /// <summary>Abstand eines Kartenpunkts zur Grundflaeche (0 = darin; genau, auch fein gedreht).</summary>
    public float Abstand(Vector2 p)
    {
        var l = Fein.Zurueck(p);
        return Vector2.Distance(l, new Vector2(Math.Clamp(l.X, LokalMin.X, LokalMax.X), Math.Clamp(l.Y, LokalMin.Z, LokalMax.Z)));
    }
}

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
public sealed partial class StadtSzene
{
    /// <summary>
    /// Massstab der Stadt (Nachtrag 1 zu P7-12, Wunsch 2): eine Karteneinheit sind 5 m. Damit passen die
    /// Groessen aus orte.json (Strasse 0,72 = 3,6 m, Fussweg 0,5 = 2,5 m, Baeume 6 bis 8 m, Haeuser 2 bis
    /// 3 Stockwerke); Figur (1,8 m), Zug (2,9 m breit), Spur (1,435 m) und Tueren (2,2 m) richten sich danach.
    /// </summary>
    public const float Meter = 0.2f;

    /// <summary>Breite der Strassen wie landscape_shapes (0.72), Fluss 0.65.</summary>
    public const float StrassenBreite = 0.72f;
    public const float FlussBreite = 0.65f;

    /// <summary>
    /// Hoehen der flachen Schichten am Boden (Wunsch Nico 25.10.2026 nach der S24-Probe: kein Flimmern
    /// an Fusswegen und Wasser). Je Schicht mindestens 0,012 Abstand, sonst kaempfen zwei Flaechen im
    /// Tiefenpuffer des Handys (16 Bit) um dasselbe Bildpixel; was oben liegen muss, liegt hoeher:
    /// Park, Ufer, Wasser, Gleisbett, Fussweg, Strasse, Mittellinie, Uebergang, Schienen.
    /// </summary>
    public static class Hoehe
    {
        public const float Park = 0.012f;
        public const float Ufer = 0.022f;
        public const float Wasser = 0.034f;
        public const float Gleisbett = 0.034f;
        public const float Weg = 0.046f;
        /// <summary>Phase 13 Welle 4a: Gehweg neben der Strasse (unter der Strasse, ueber dem Fussweg zur Tuer).</summary>
        public const float Gehweg = 0.052f;
        public const float Strasse = 0.058f;
        public const float Mittellinie = 0.068f;

        /// <summary>Flaechen der Modelle am Boden (Hof, Bahnsteig), ueber Fussweg und Strasse.</summary>
        public const float Flaeche = 0.064f;
        public const float Uebergang = 0.07f;
        public const float Schwelle = 0.05f;
        public const float Schiene = 0.08f;

        /// <summary>Die Figur steht auf Weg und Strasse, nicht darin.</summary>
        public const float Figur = Strasse;
    }

    /// <summary>Breite der Fusswege (Zugaenge von der Tuer zur Strasse).</summary>
    public const float WegBreite = 0.5f;

    /// <summary>Rasterweite des freien Gehens (wie im Innenraum, WALK_STEP).</summary>
    public const double LaufSchritt = 0.25;

    /// <summary>So weit bleibt die Figur beim freien Gehen von Haeusern, Wasser und Gleis weg.</summary>
    public const double LaufKoerper = 0.15;

    private StadtSzene(IReadOnlyList<Teil> teile, IReadOnlyList<Streifen> streifen, IReadOnlyList<ModellStueck> stuecke, GrafikBibliothek? grafik,
        IReadOnlyList<StadtOrt> orte, string signatur, float breite, float tiefe, Wegsuche laufraster, IReadOnlyList<StadtHaus> haeuser)
    {
        Haeuser = haeuser;
        Teile = teile;
        Streifen = streifen;
        Laufraster = laufraster;
        Stuecke = stuecke;
        Grafik = grafik;
        Orte = orte;
        Signatur = signatur;
        Breite = breite;
        Tiefe = tiefe;
    }

    public IReadOnlyList<Teil> Teile { get; }

    /// <summary>
    /// Phase 8 W11 (Sammelliste 157): beide Enden jeder Strasse mit Richtung nach aussen und Verlaengerung ueber den
    /// Endpunkt (halbe Strassenbreite am Kartenrand und an einer anderen Strasse, sonst halbe Gehwegbreite).
    /// </summary>
    public IReadOnlyList<(Vector2 Punkt, Vector2 Richtung, float Verlaengert)> StrassenEnden { get; private init; } = [];

    /// <summary>Mitten der Bruecken (Strasse ueber dem Fluss).</summary>
    public IReadOnlyList<Vector2> Bruecken { get; private init; } = [];

    /// <summary>
    /// Grundriss jeder Bruecke (W11, Weisung P8-47): Mitte, Richtung der Strasse, halbe Laenge bis zur Aussenkante der
    /// Endpfosten und halbe Breite bis zur Aussenkante der Bruestung.
    /// </summary>
    public IReadOnlyList<(Vector2 Mitte, Vector2 Richtung, float HalbLaenge, float HalbBreite)> BrueckenKaesten { get; private init; } = [];

    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 1): alle Baeume (Landschaft und Modelle) mit Mitte und Radius, so wie
    /// sie stehen; keiner steht auf Strasse, Fussweg, Gleis oder Wasser (<see cref="BaumSperren"/>).
    /// </summary>
    public IReadOnlyList<(Vector2 Mitte, float R)> Baeume { get; private init; } = [];

    /// <summary>Phase 8 W8 (Toene): Parks als Rechtecke (Schritte auf Gras, Vogelrufe).</summary>
    public IReadOnlyList<(float X1, float Z1, float X2, float Z2)> Parks { get; private init; } = [];

    /// <summary>S 0.8.0 (Weisung P12-47): Teiche aus <c>stadtdaten.wasser</c> (leer ohne Wasser, Fassung 0).</summary>
    public IReadOnlyList<Teich> Teiche { get; private init; } = [];

    /// <summary>S 0.8.0: Stege aus <c>stadtdaten.wasser</c>.</summary>
    public IReadOnlyList<Steg> Stege { get; private init; } = [];

    /// <summary>Welle A 0.66.0 (Weisung P12-192): das Gelaende der Stadt (null ohne Abschnitt <c>gelaende</c>, Fassung 0).</summary>
    public Gelaende? Gelaende { get; private init; }

    /// <summary>Hoehe, auf der an (x, z) Figur und Stuecke stehen (ohne Gelaende 0).</summary>
    public float BodenHoehe(float x, float z) => Gelaende?.Boden(x, z) ?? 0f;

    /// <summary>Hebt die Stuecke von <paramref name="von"/> bis vor <paramref name="bis"/> auf die Hoehe des Gelaendes an ihrer Mitte.</summary>
    private static void Anheben(Gelaende gelaende, List<ModellStueck> stuecke, int von, int bis)
    {
        for (var i = von; i < bis; i++)
        {
            var lage = stuecke[i].Lage;
            var h = gelaende.Boden(lage.M41, lage.M43);
            if (h > 0f)
            {
                lage.M42 += h;
                stuecke[i] = stuecke[i] with { Lage = lage };
            }
        }
    }

    private static Teil Anheben(Gelaende gelaende, Teil t)
    {
        var h = gelaende.Boden((t.X1 + t.X2) / 2, (t.Z1 + t.Z2) / 2);
        return h > 0f ? t with { Y1 = t.Y1 + h, Y2 = t.Y2 + h } : t;
    }

    /// <summary>S 0.8.0: Flaechen am Boden (Ufer und Wasser der Teiche) im Netz der Stadt.</summary>
    public IReadOnlyList<Bodenflaeche> Flaechen { get; private init; } = [];

    /// <summary>S 0.8.0 (Frage 6 = a): Schalter <c>stadtdaten.wasser.eis</c>; nur im Winter friert das Wasser zu.</summary>
    public bool Eis { get; private init; }

    /// <summary>Farbe des Eises im Netz (mit Schneemarke: im dunklen Design nicht abgedunkelt wie Schnee).</summary>
    public static readonly Vector3 EisFarbe = Wasser.Eis + new Vector3(Jahreszeitfarben.SchneeMarke);

    /// <summary>Phase 8 W8 (Toene, P8-17c Punkt 2): Mittellinien des Flusses (geglaettet).</summary>
    public IReadOnlyList<IReadOnlyList<Vector2>> Fluesse { get; private init; } = [];

    /// <summary>Phase 8 W6: die Festzeit, mit der die Szene gebaut wurde (null = keine).</summary>
    public string? Fest { get; private init; }

    /// <summary>Phase 8 W6: die gesetzte Fest-Deko (leer ohne Festzeit).</summary>
    public IReadOnlyList<FestDeko.Gesetzt> FestGesetzt { get; private init; } = [];

    /// <summary>Linien, auf denen kein Baum stehen darf, mit halber Breite (Strassen, Fusswege, Gleise, Fluss mit Ufer).</summary>
    public IReadOnlyList<(IReadOnlyList<Vector2> Linie, float Halb)> BaumSperren { get; private init; } = [];

    /// <summary>Mitten der Bahnuebergaenge; Strasse = mit Warnkreuzen, sonst Fussweg.</summary>
    public IReadOnlyList<(Vector2 Mitte, bool Strasse)> Uebergaenge { get; private init; } = [];

    /// <summary>Welle A 0.54.0: die Zebrastreifen der Stadt (auf einer geraden Strasse gelegt).</summary>
    public IReadOnlyList<Zebra> Zebras { get; private init; } = [];

    /// <summary>Welle A 0.60.0 (Weisung P12-166): die Ampeln der Stadt (auf einer Kreuzung oder Einmuendung gelegt).</summary>
    public IReadOnlyList<Ampel> Ampeln { get; private init; } = [];

    /// <summary>Welle A 0.61.0 (Weisung P12-171): die Bushaltestellen der Stadt (an einer geraden Strasse mit Gehweg gelegt).</summary>
    public IReadOnlyList<Haltestelle> Haltestellen { get; private init; } = [];

    /// <summary>
    /// Welle Q2 (Weisung P12-190): Questgeber, die draussen stehen (Quest-Ebene der Fassung, hoechstens 12). Nur Darstellung
    /// im Netz des Hauses ihres Orts (<see cref="QuestgeberJeHaus"/>, kein neuer Zeichenaufruf); sie versperren keinen Weg (Laufraster unveraendert). Fassung 0: keine.
    /// </summary>
    public IReadOnlyList<Stadtbau.Questebene.Draussen> Questgeber { get; private init; } = [];

    /// <summary>
    /// Welle Q2: Ort, zu dem ein Questgeber draussen gehoert: der mit dem naechsten Standplatz (gleich weit: erster der
    /// Karte). Das Spiel spricht ihn an diesem Ort an, und er steht im Netz des Hauses dieses Orts (<see cref="HausNetz"/>).
    /// </summary>
    public string? QuestgeberOrt(Stadtbau.Questebene.Draussen q) =>
        Orte.OrderBy(o => ((o.StehX - q.X) * (o.StehX - q.X)) + ((o.StehZ - q.Z) * (o.StehZ - q.Z))).FirstOrDefault()?.Id;

    /// <summary>
    /// Welle Q2: die Questgeber draussen je Haus. Ein Questgeber steht im Netz des Hauses seines Orts: so gelten fuer ihn
    /// Sichtkegel und Fernform des Hauses (Welle D1) ohne neuen Zeichenaufruf. Hat sein Ort kein Haus, steht er im Paket-Netz.
    /// Gegenprobe <see cref="QuestgeberImPaketFuerTests"/>: alle im Paket-Netz (Stand vor dieser Regel).
    /// </summary>
    public IReadOnlyDictionary<string, List<Stadtbau.Questebene.Draussen>> QuestgeberJeHaus => _questgeberJeHaus ??= QuestgeberJeHausBauen();

    private Dictionary<string, List<Stadtbau.Questebene.Draussen>>? _questgeberJeHaus;

    /// <summary>Nur Tests und Start-Test (Gegenprobe): Questgeber draussen alle im Paket-Netz wie vor der Regel je Haus.</summary>
    public static bool QuestgeberImPaketFuerTests
    {
        get => _questgeberImPaket.Value;
        set => _questgeberImPaket.Value = value;
    }

    private static readonly AsyncLocal<bool> _questgeberImPaket = new();

    private Dictionary<string, List<Stadtbau.Questebene.Draussen>> QuestgeberJeHausBauen()
    {
        var ergebnis = new Dictionary<string, List<Stadtbau.Questebene.Draussen>>();
        if (Questgeber.Count == 0 || QuestgeberImPaketFuerTests)
        {
            return ergebnis;
        }
        var haeuser = Haeuser.Select(h => h.Id).ToHashSet();
        foreach (var q in Questgeber)
        {
            if (QuestgeberOrt(q) is { } ort && haeuser.Contains(ort))
            {
                if (!ergebnis.TryGetValue(ort, out var liste))
                {
                    ergebnis[ort] = liste = [];
                }
                liste.Add(q);
            }
        }
        return ergebnis;
    }

    /// <summary>Welle Q2: Questgeber draussen, die im Paket-Netz stehen (ihr Ort hat kein Haus).</summary>
    private IEnumerable<Stadtbau.Questebene.Draussen> QuestgeberImPaket =>
        Questgeber.Where(q => !QuestgeberJeHaus.Values.Any(l => l.Contains(q)));

    /// <summary>
    /// Welle A 0.61.0: Vieleck gegen den Uhrzeigersinn in (x, y) (so verlangt es <see cref="Wasser.Dreiecke"/>; im
    /// Uhrzeigersinn findet es kein Ohr und die Flaeche fehlt). Ein Parkplatz auf der einen Strassenseite liegt so, auf der
    /// anderen andersherum (Fund in den Fotos P12-103: Flaeche fehlte auf einer Seite).
    /// </summary>
    public static Vector2[] GegenUhr(Vector2[] p)
    {
        var flaeche = 0f;
        for (var i = 0; i < p.Length; i++)
        {
            var (a, b) = (p[i], p[(i + 1) % p.Length]);
            flaeche += (a.X * b.Y) - (b.X * a.Y);
        }
        return flaeche >= 0 ? p : [.. p.Reverse()];
    }

    /// <summary>Welle A 0.61.0 (Weisung P12-171): die Parkplaetze der Stadt (an einer geraden Strasse mit Gehweg gelegt).</summary>
    public IReadOnlyList<Parkplatz> Parkplaetze { get; private init; } = [];

    /// <summary>Welle A 0.54.0: hat die Stadt eine Strasse mit Gehweg (dann ist deren Fahrbahn nur am Zebrastreifen begehbar)?</summary>
    public bool MitGehweg { get; private init; }

    /// <summary>Ausstattung der Bahnuebergaenge an Strassen (P8-49): Andreaskreuz, Lichter, Halbschranke.</summary>
    public IReadOnlyList<Bahnuebergang.Kreuz> Andreaskreuze { get; private init; } = [];

    /// <summary>Mittellinien der Gleise (geglaettet), z. B. fuer den Lichtraum (<see cref="Bahnuebergang.LichtraumHalb"/>).</summary>
    public IReadOnlyList<IReadOnlyList<Vector2>> Gleise { get; private init; } = [];

    /// <summary>Fluss, Ufer und Gleis als glatte Baender (P7-10 Nachtrag 1).</summary>
    public IReadOnlyList<Streifen> Streifen { get; }

    /// <summary>
    /// Freies Gehen (P7-10 Nachtrag 1, Wunsch 3): begehbar sind Strassen, Fusswege, Wiese und Parks;
    /// nicht Haeuser, Wasser (nur ueber Bruecken = Strasse ueber dem Fluss) und Gleis (nur an
    /// Uebergaengen = Strasse ueber dem Gleis). Zellen zu <see cref="LaufSchritt"/>.
    /// </summary>
    public Wegsuche Laufraster { get; }

    /// <summary>Platzierte CC0-Modelle (leer ohne Grafik-Bibliothek).</summary>
    public IReadOnlyList<ModellStueck> Stuecke { get; }

    public GrafikBibliothek? Grafik { get; }

    /// <summary>Die Haeuser (Gebaeude der Landschaft und Orte), jedes mit eigenem Netz (<see cref="HausNetz"/>).</summary>
    public IReadOnlyList<StadtHaus> Haeuser { get; }

    private Fertig? _fertig;

    /// <summary>Weisung P8-55 (W12): die fertigen Netze der Szene (Stadt, je Haus, Kleingruen, Paket nah und fern).</summary>
    /// <remarks>Welle D1 (Weisung P12-143): dazu die Fernformen der Stadt (Fest-Deko vereinfacht, <see cref="Fernform"/>) und je Haus.</remarks>
    /// <remarks>Welle D2 (Weisung P12-157): <paramref name="Instanzen"/> = die Stuecke mit Instancing; Stadt, StadtFern und Gruen enthalten sie dann nicht.</remarks>
    /// <remarks>Welle A 0.60.0 (Weisung P12-166): <paramref name="AmpelLichter"/> = die leuchtenden Lampen je Gruppe (<see cref="Probe.Ampeln.LichtNetze"/>), ohne Ampel null.</remarks>
    public sealed record Fertig(Netz Stadt, IReadOnlyDictionary<StadtHaus, Netz> Haeuser, Netz? Gruen, Netz? PaketNah, Netz? PaketFern,
        Netz? StadtFern = null, IReadOnlyDictionary<StadtHaus, Netz>? HaeuserFern = null, Instanzplan? Instanzen = null, IReadOnlyList<Netz>? AmpelLichter = null);

    /// <summary>Welle D1/D2: ab diesem Kameraabstand zeichnet das Spiel die Fernformen (wie Autos und Laternen).</summary>
    public const float FernAb = 25f;

    /// <summary>Phase 8: das Kleingruen wird nur bis zu diesem Kameraabstand gezeichnet.</summary>
    public const float GruenBis = 18f;

    /// <summary>
    /// Welle D1 (Teil D): Dreiecke der CC0-Modelle, die bis zu dieser Hoehe nach unten zeigen, kommen nicht ins Netz der
    /// Stadt oder eines Hauses (<see cref="Netz.UnterseiteBis"/>); der Boden, die Wege und Strassen liegen darunter.
    /// </summary>
    public const float UnterseiteBis = 0.1f;

    /// <summary>Gegenprobe Welle D1: Unterseiten wie bis A 0.56.0 zeichnen (Sollwerte von vorher gelten damit weiter).</summary>
    public static bool UnterseitenAltFuerTests
    {
        get => UnterseitenAlt.Value;
        set => UnterseitenAlt.Value = value;
    }

    private static readonly AsyncLocal<bool> UnterseitenAlt = new();

    /// <summary>Welle D1: die Grenze fuer <see cref="Netz.UnterseiteBis"/> in den Netzen der Stadt und der Haeuser.</summary>
    private static float? Unterseiten => UnterseitenAltFuerTests ? null : UnterseiteBis;

    /// <summary>
    /// Weisung P8-55 (W12): alle Netze der Szene einmal bauen und behalten (auch ausserhalb des Oberflaechen-Threads,
    /// bevor die Szene gezeigt wird; wie <see cref="Innenraum.Vorbereiten"/>). Danach baut das Hochladen nichts mehr.
    /// Das Paket-Netz gibt es nur mit CC0-Grafik.
    /// </summary>
    public Fertig Vorbereiten()
    {
        if (_fertig is { } fertig)
        {
            return fertig;
        }
        // Phase 13 W3 (Weisung P12-21): mit Vorrat (nur Werkzeug) werden unveraenderte Hausnetze und das Paket-Netz uebernommen
        var vorrat = _vorrat;
        lock (vorrat?.Sperre ?? new object())
        {
            var haeuser = new Dictionary<StadtHaus, Netz>();
            var haeuserFern = new Dictionary<StadtHaus, Netz>();
            var neu = new Dictionary<string, (HausEingang, Netz, Netz)>();
            foreach (var haus in Haeuser)
            {
                (haeuser[haus], haeuserFern[haus]) = HausNetzMitVorrat(haus, neu);
            }
            // Welle D2 (Weisung P12-157): oft gleiche Modelle mit Instancing, die Netze der Stadt ohne sie
            var plan = Instanzen.AusFuerTests ? null : InstanzplanBauen();
            var stadtNetz = NetzBauen(false, plan);
            var stadtFern = NetzBauen(true, plan);
            var gruenNetz = GruenNetzBauen(plan);
            var (nah, fern) = PaketMitVorrat();
            fertig = new Fertig(stadtNetz, haeuser, gruenNetz, nah, fern, stadtFern, haeuserFern, plan, Probe.Ampeln.LichtNetze(Ampeln));
            if (vorrat is not null)
            {
                vorrat.Haeuser = neu;
            }
        }
        _fertig = fertig;
        return fertig;
    }

    /// <summary>
    /// Das Dreiecksnetz der Stadt ohne die Haeuser: eigene Teile und CC0-Modelle. Welle D1: ohne Unterseiten am Boden;
    /// <paramref name="fern"/> = die Fest-Deko in ihrer <see cref="Fernform"/> (Teil C), alles andere gleich.
    /// </summary>
    public Netz NetzBauen(bool fern = false) => NetzBauen(fern, null);

    /// <summary>Welle D2: wie <see cref="NetzBauen(bool)"/>, ohne die Stuecke und Baeume aus <paramref name="plan"/> (die zeichnet das Instancing).</summary>
    public Netz NetzBauen(bool fern, Instanzplan? plan)
    {
        var ohneStueck = plan?.Stuecke ?? Instanzplan.Leer.Stuecke;
        var ohneBaum = plan?.Baeume ?? Instanzplan.Leer.Baeume;
        var teileHaus = new bool[Teile.Count];
        var stueckeHaus = new bool[Stuecke.Count];
        foreach (var h in Haeuser)
        {
            Array.Fill(teileHaus, true, h.TeileVon, h.TeileBis - h.TeileVon);
            Array.Fill(stueckeHaus, true, h.StueckeVon, h.StueckeBis - h.StueckeVon);
        }
        if (Jahreszeit == Jahreszeit.Sommer)
        {
            var netz = Netz.Aus(Teile.Where((_, i) => !teileHaus[i] && i < FestTeileVon));
            // Weisung P8-56: die Fest-Teile danach, das Licht der eigenen Laternen leuchtet nachts
            for (var i = FestTeileVon; i < Teile.Count; i++)
            {
                if (!teileHaus[i])
                {
                    var von = netz.AnzahlEcken;
                    netz.Hinzu(Teile[i]);
                    if (Teile[i].Farbe == FestDeko.LaternenLicht)
                    {
                        netz.Leuchten(von, netz.AnzahlEcken);
                    }
                }
            }
            foreach (var streifen in Streifen)
            {
                netz.Hinzu(streifen);
            }
            // S 0.8.0 (Weisung P12-47): Ufer und Wasser der Teiche im selben Netz (ohne Teiche nichts, bitgleich wie vorher)
            foreach (var flaeche in Flaechen)
            {
                netz.Hinzu(flaeche);
            }
            // Welle A 0.66.0: das Gelaende im selben Netz (kein neuer Zeichenaufruf; aus der Ferne Raster 1,0)
            Gelaende?.Hinzu(netz, Breite, Tiefe, fern);
            netz.UnterseiteBis = Unterseiten;
            for (var i = 0; i < Stuecke.Count; i++)
            {
                if (!stueckeHaus[i] && !ohneStueck.Contains(i) && !ImPaket(Stuecke[i].Name) && ModellAn(i) is { } original)
                {
                    var von = netz.AnzahlEcken;
                    var modell = fern && i >= FestStueckeVon ? Fernform.Von(original) : original;
                    netz.Hinzu(modell, Stuecke[i].Lage, i < FestStueckeVon ? null : FestLicht(modell));
                    if (Katalogstueck.IstEffekt(modell.Name))
                    {
                        // Phase 13 Welle 4a: ein Effekt leuchtet nachts wie die Laternen
                        netz.Leuchten(von, netz.AnzahlEcken);
                    }
                }
            }
            var nb = -1;
            foreach (var baum in GruenKlein?.Baeume ?? [])
            {
                nb++;
                if (!ohneBaum.Contains(nb) && Grafik?.Modell(baum.Name) is { } modell)
                {
                    netz.Hinzu(modell, baum.Lage);
                }
            }
            return netz;
        }
        // Phase 8 W9: Farben nach der Jahreszeit; die Fest-Deko (ab FestTeileVon/FestStueckeVon) behaelt ihre Farben
        var gefaerbt = new Netz();
        for (var i = 0; i < Teile.Count; i++)
        {
            if (!teileHaus[i])
            {
                var t = Teile[i];
                gefaerbt.Faerbung = i < FestTeileVon
                    ? Jahreszeitfarben.FuerTeile(Jahreszeit, Faerbeart.Stadt, Jahreszeitfarben.Samen(new Vector3((t.X1 + t.X2) / 2, 0, (t.Z1 + t.Z2) / 2)))
                    : null;
                var von = gefaerbt.AnzahlEcken;
                gefaerbt.Hinzu(t);
                if (i >= FestTeileVon && t.Farbe == FestDeko.LaternenLicht)
                {
                    gefaerbt.Leuchten(von, gefaerbt.AnzahlEcken);
                }
            }
        }
        gefaerbt.Faerbung = Jahreszeitfarben.FuerTeile(Jahreszeit, Faerbeart.Stadt);
        // S 0.8.0 (Weisung P12-47, Frage 6 = a): mit dem Schalter eis friert im Winter das Wasser (Fluss und Teiche) zu,
        // das Ufer ist im Winter weiss wie bisher; ohne Schalter bleibt alles wie vorher
        var eis = Eis && Jahreszeit == Jahreszeit.Winter;
        foreach (var streifen in Streifen)
        {
            gefaerbt.Hinzu(eis && streifen.Farbe == Farben.Wasser ? streifen with { Farbe = EisFarbe } : streifen);
        }
        foreach (var flaeche in Flaechen)
        {
            gefaerbt.Hinzu(eis && flaeche.Farbe == Farben.Wasser ? flaeche with { Farbe = EisFarbe } : flaeche);
        }
        Gelaende?.Hinzu(gefaerbt, Breite, Tiefe, fern);
        gefaerbt.Faerbung = null;
        gefaerbt.UnterseiteBis = Unterseiten;
        for (var i = 0; i < Stuecke.Count; i++)
        {
            if (!stueckeHaus[i] && !ohneStueck.Contains(i) && !ImPaket(Stuecke[i].Name) && ModellAn(i) is { } original)
            {
                var modell = fern && i >= FestStueckeVon ? Fernform.Von(original) : original;
                if (Katalogstueck.IstEffekt(modell.Name))
                {
                    // Phase 13 Welle 4a: ein Effekt behaelt seine Farbe (kein Schnee) und leuchtet nachts
                    var von = gefaerbt.AnzahlEcken;
                    gefaerbt.Hinzu(modell, Stuecke[i].Lage, null);
                    gefaerbt.Leuchten(von, gefaerbt.AnzahlEcken);
                    continue;
                }
                gefaerbt.Hinzu(modell, Stuecke[i].Lage, i < FestStueckeVon ? ModellFaerbung(modell, Stuecke[i].Lage, Faerbeart.Stadt) : FestLicht(modell));
            }
        }
        var nbf = -1;
        foreach (var baum in GruenKlein?.Baeume ?? [])
        {
            nbf++;
            if (!ohneBaum.Contains(nbf) && Grafik?.Modell(baum.Name) is { } modell)
            {
                gefaerbt.Hinzu(modell, baum.Lage, ModellFaerbung(modell, baum.Lage, Faerbeart.Stadt));
            }
        }
        return gefaerbt;
    }

    /// <summary>
    /// Welle D2 (Weisung P12-157, Nico 1a): welche CC0-Stuecke draussen (nicht am Haus, nicht im Paket-Netz, kein Effekt)
    /// mit Instancing gezeichnet werden. Jedes Stueck wird genau wie in <see cref="NetzBauen(bool, Instanzplan?)"/> bzw.
    /// <see cref="GruenNetzBauen(Instanzplan?)"/> entschieden und gefaerbt (Jahreszeit nach seiner Lage), nur im Raum des
    /// Modells; gleiche Netze bilden einen Satz (<see cref="Instanzen.Planen"/>). Fern zeigt jedes Stueck seine
    /// <see cref="Fernform"/> (Nico 1a: Fernform je Stueck), das Kleingruen wird fern nicht gezeichnet.
    /// </summary>
    public Instanzplan InstanzplanBauen()
    {
        if (Grafik is null)
        {
            return Instanzplan.Leer;
        }
        var stueckeHaus = new bool[Stuecke.Count];
        foreach (var h in Haeuser)
        {
            Array.Fill(stueckeHaus, true, h.StueckeVon, h.StueckeBis - h.StueckeVon);
        }
        var kandidaten = new List<Instanzen.Kandidat>();
        Netz Lokal(GrafikModell m, Matrix4x4 lage, Modellfaerbung? f, float? unterseite)
        {
            var n = new Netz { UnterseiteBis = unterseite };
            n.HinzuLokal(m, lage, f);
            return n;
        }
        for (var i = 0; i < Stuecke.Count; i++)
        {
            if (stueckeHaus[i] || ImPaket(Stuecke[i].Name) || ModellAn(i) is not { } original || Katalogstueck.IstEffekt(original.Name)
                || !Instanzen.Geeignet(Stuecke[i].Lage))
            {
                continue;
            }
            var lage = Stuecke[i].Lage;
            var fernModell = Fernform.Von(original);
            var nah = Lokal(original, lage, i < FestStueckeVon ? ModellFaerbung(original, lage, Faerbeart.Stadt) : FestLicht(original), Unterseiten);
            var fern = Lokal(fernModell, lage, i < FestStueckeVon ? ModellFaerbung(fernModell, lage, Faerbeart.Stadt) : FestLicht(fernModell), Unterseiten);
            if (nah.AnzahlEcken > 0)
            {
                kandidaten.Add(new(Instanzen.Stadt, 's', i, original.Name, lage, nah, fern));
            }
        }
        var b = -1;
        foreach (var baum in GruenKlein?.Baeume ?? [])
        {
            b++;
            if (Grafik.Modell(baum.Name) is not { } modell || !Instanzen.Geeignet(baum.Lage))
            {
                continue;
            }
            var fernModell = Fernform.Von(modell);
            var nah = Lokal(modell, baum.Lage, ModellFaerbung(modell, baum.Lage, Faerbeart.Stadt), Unterseiten);
            var fern = Lokal(fernModell, baum.Lage, ModellFaerbung(fernModell, baum.Lage, Faerbeart.Stadt), Unterseiten);
            if (nah.AnzahlEcken > 0)
            {
                kandidaten.Add(new(Instanzen.Stadt, 'b', b, modell.Name, baum.Lage, nah, fern));
            }
        }
        var g = -1;
        foreach (var st in GruenKlein?.Stuecke ?? [])
        {
            g++;
            if (Grafik.Modell(st.Name) is not { } m || !Instanzen.Geeignet(st.Lage))
            {
                continue;
            }
            var nah = Lokal(m, st.Lage, ModellFaerbung(m, st.Lage, Faerbeart.Klein), null);
            if (nah.AnzahlEcken > 0)
            {
                kandidaten.Add(new(Instanzen.Gruen, 'g', g, m.Name, st.Lage, nah, null));
            }
        }
        return Instanzen.Planen(kandidaten);
    }

    /// <summary>
    /// Weisung P8-56 (Nico, Bild 4): die Lampen der Fest-Deko an den Tueren leuchten nachts wie die Laternen: bei
    /// Laternen-Modellen nur Glas und Kerze (<see cref="FestVerkehr.Glut"/>), alles andere behaelt seine Farbe.
    /// </summary>
    private static Modellfaerbung? FestLicht(GrafikModell modell) =>
        modell.Name.Contains("lantern", StringComparison.Ordinal) ? (d, grund, _, _) => FestVerkehr.Glut(modell, d, grund) : null;

    /// <summary>
    /// Das Netz eines Hauses (eigene Teile und CC0-Modelle). Welle D1: ohne Unterseiten am Boden; <paramref name="fern"/> =
    /// die CC0-Modelle in ihrer <see cref="Fernform"/> (Teil B), die eigenen Teile bleiben.
    /// </summary>
    public Netz HausNetz(StadtHaus haus, bool fern = false)
    {
        if (Jahreszeit == Jahreszeit.Sommer)
        {
            var netz = new Netz { UnterseiteBis = Unterseiten };
            for (var i = haus.TeileVon; i < haus.TeileBis; i++)
            {
                TeilHinzu(netz, i);
            }
            for (var i = haus.StueckeVon; i < haus.StueckeBis; i++)
            {
                if (!ImPaket(Stuecke[i].Name) && ModellAn(i) is { } original)
                {
                    var modell = fern ? Fernform.Von(original) : original;
                    var von = netz.Indizes.Count;
                    var flaechen = new List<Vector3>();
                    netz.Hinzu(modell, Stuecke[i].Lage, null, flaechen);
                    Nachtlicht.GlasMarkieren(netz, von, haus.Id + "|" + i.ToString(CultureInfo.InvariantCulture), flaechen);
                }
            }
            QuestgeberAmHaus(netz, haus, fern);
            return netz;
        }
        // Phase 8 W9: am Haus Schnee nur auf dem Dach; Baeume am Haus (Modellname mit "tree") wie die Baeume der Stadt
        var gefaerbt = new Netz { Faerbung = Jahreszeitfarben.FuerTeile(Jahreszeit, Faerbeart.Haus), UnterseiteBis = Unterseiten };
        for (var i = haus.TeileVon; i < haus.TeileBis; i++)
        {
            TeilHinzu(gefaerbt, i);
        }
        gefaerbt.Faerbung = null;
        for (var i = haus.StueckeVon; i < haus.StueckeBis; i++)
        {
            if (!ImPaket(Stuecke[i].Name) && ModellAn(i) is { } original)
            {
                var modell = fern ? Fernform.Von(original) : original;
                var art = Stuecke[i].Name.Contains("tree", StringComparison.Ordinal) ? Faerbeart.Stadt : Faerbeart.Haus;
                var von = gefaerbt.Indizes.Count;
                var flaechen = new List<Vector3>();
                gefaerbt.Hinzu(modell, Stuecke[i].Lage, ModellFaerbung(modell, Stuecke[i].Lage, art), flaechen);
                Nachtlicht.GlasMarkieren(gefaerbt, von, haus.Id + "|" + i.ToString(CultureInfo.InvariantCulture), flaechen);
            }
        }
        QuestgeberAmHaus(gefaerbt, haus, fern);
        return gefaerbt;
    }

    /// <summary>Welle Q2: die Questgeber draussen, die zu diesem Haus gehoeren, nah als Figur, fern als Kaesten.</summary>
    private void QuestgeberAmHaus(Netz netz, StadtHaus haus, bool fern)
    {
        if (QuestgeberJeHaus.TryGetValue(haus.Id, out var liste))
        {
            QuestgeberHinzu(netz, fern, liste);
        }
    }

    /// <summary>
    /// W10: eigenes Teil ins Netz, Fenster mit Licht mit der Marke "leuchtet nachts", aber nur die Scheibe selbst
    /// (Flaechen quer zur duennsten Seite): die schmalen Kanten des Fensterblocks (Seitenkante der Bank, Weisung
    /// P8-34) bleiben dunkel.
    /// </summary>
    private void TeilHinzu(Netz netz, int i)
    {
        var von = netz.AnzahlEcken;
        var t = Teile[i];
        netz.Hinzu(t);
        if (!LichtTeile.Contains(i))
        {
            return;
        }
        var mass = new Vector3(MathF.Abs(t.X2 - t.X1), MathF.Abs(t.Y2 - t.Y1), MathF.Abs(t.Z2 - t.Z1));
        // das Zifferblatt der Bahnhofsuhr steht quer zur Suedwand: es leuchtet nach Osten und Westen (aus Streifen, die flacher als dick sind)
        var duenn = t.Farbe == UhrBlatt ? Vector3.UnitX
            : mass.X <= mass.Y && mass.X <= mass.Z ? Vector3.UnitX : mass.Y <= mass.Z ? Vector3.UnitY : Vector3.UnitZ;
        if (t.Dreh.Z != 0f && !LichtDrehAlt.Value)
        {
            // Welle A 0.70.0: das Netz hat den Teil gedreht, die duenne Seite dreht mit (sonst leuchtet ein schraeges Fenster nie)
            var (sin, cos) = MathF.SinCos(t.Dreh.Z * MathF.PI / 180);
            duenn = new Vector3((duenn.X * cos) - (duenn.Z * sin), duenn.Y, (duenn.X * sin) + (duenn.Z * cos));
        }
        for (var e = von; e < netz.AnzahlEcken; e++)
        {
            if (MathF.Abs(Vector3.Dot(netz.Normale(e), duenn)) > 0.9f)
            {
                netz.Leuchten(e, e + 1);
            }
        }
    }

    /// <summary>Gegenprobe A 0.70.0: duenne Seite eines gedrehten Teils ungedreht wie im ersten Entwurf (nur Tests, je Ablauf).</summary>
    public static bool LichtDrehAltFuerTests { get => LichtDrehAlt.Value; set => LichtDrehAlt.Value = value; }
    private static readonly AsyncLocal<bool> LichtDrehAlt = new();

    /// <summary>
    /// Phase 8 W11 (Nico 5a, P8-42): Modelle aus dem Graveyard Kit (Tannen, Kuerbisse) stehen nicht im Netz der Stadt
    /// oder eines Hauses, sondern mit den Laternen im eigenen Netz (<see cref="PaketNetzBauen"/>).
    /// </summary>
    public static bool ImPaket(string modell) => modell.StartsWith("gy-", StringComparison.Ordinal);

    /// <summary>
    /// Phase 8 W11 (Nico 5a, P8-42): eigenes Netz fuer Laternen, Tannen, Kuerbisse und Bahnuebergaenge (P8-49) (ein Zeichenaufruf mehr, die
    /// Grenze der Gesamtansicht bleibt): nah die Paket-Modelle, fern (<paramref name="fern"/>) einfache Kaesten.
    /// Tannen nach der Jahreszeit gefaerbt (im Herbst das orange Modell), Kuerbisse und Laternen behalten ihre Farben.
    /// </summary>
    public Netz PaketNetzBauen(bool fern)
    {
        var netz = new Netz();
        // P8-49: Bahnuebergaenge nah und fern ganz (klein), ihre Lichter leuchten nachts rot
        Bahnuebergang.Hinzu(netz, Andreaskreuze);
        // Welle A 0.60.0: Masten der Ampeln wie die Bahnuebergaenge (nah und fern ganz, klein); die Lampen leuchten im eigenen Aufruf
        Probe.Ampeln.Hinzu(netz, Ampeln);
        // Welle A 0.61.0: Haeuschen und Mast der Haltestellen wie die Masten der Ampeln (nah und fern ganz, klein)
        Probe.Haltestellen.Hinzu(netz, Haltestellen);
        // Welle Q2: Questgeber draussen (nah die Figur mit ihrem Aussehen, fern zwei Kaesten wie die wartenden Figuren)
        QuestgeberHinzu(netz, fern);
        if (Grafik is null)
        {
            return netz;
        }
        for (var i = 0; i < Stuecke.Count; i++)
        {
            if (!ImPaket(Stuecke[i].Name) || Grafik.Modell(TanneIn(Stuecke[i].Name, Jahreszeit)) is not { } m)
            {
                continue;
            }
            var lage = Stuecke[i].Lage;
            var faerbung = i < FestStueckeVon ? ModellFaerbung(m, lage, Faerbeart.Stadt) : null;
            if (fern)
            {
                FernKaesten(netz, m, lage, faerbung);
            }
            else
            {
                netz.Hinzu(m, lage, faerbung);
            }
        }
        foreach (var l in Laternen)
        {
            Nachtlicht.Hinzu(netz, Grafik, l, fern);
        }
        return netz;
    }

    /// <summary>
    /// Welle Q2: Questgeber draussen in ein Netz (Paket-Netz oder Netz ihres Hauses); Fuesse auf dem Boden an ihrem Platz, Blick wie im Werkzeug
    /// (0 = +y, 90 = +x). Ohne Grafik (Tests) die einfache Probefigur.
    /// </summary>
    private void QuestgeberHinzu(Netz netz, bool fern) => QuestgeberHinzu(netz, fern, QuestgeberImPaket);

    private void QuestgeberHinzu(Netz netz, bool fern, IEnumerable<Stadtbau.Questebene.Draussen> questgeber)
    {
        foreach (var q in questgeber)
        {
            var (bx, by) = Stadtbau.Etagen.Richtung(q.Blick);
            var lage = Matrix4x4.CreateRotationY(MathF.Atan2((float)bx, (float)by)) * Matrix4x4.CreateTranslation((float)q.X, 0f, (float)q.Z);
            netz.Hinzu(QuestgeberNetz(Grafik, q.Aussehen, fern), lage);
        }
    }

    /// <summary>Welle Q2: Netz eines Questgebers draussen (nah die Figur in Stadthoehe, fern Beine und Oberteil als Kaesten).</summary>
    public static Netz QuestgeberNetz(GrafikBibliothek? grafik, PyObjekt? aussehen, bool fern)
    {
        if (!fern)
        {
            return Figur.Netz(grafik, aussehen, Figur.StadtHoehe);
        }
        var a = Lernplattform.Core.Spiel.Aussehen.Normalisieren(aussehen);
        var h = Figur.StadtHoehe;
        var b = h * 0.14f;
        var oben = Netz.Farbe(Figur.Oberteil.GetValueOrDefault(Py.Str(a["oberteil"]), "#22D3EE"));
        var haut = Netz.Farbe(Figur.Haut.GetValueOrDefault(Py.Str(a["haut"]), "#F1C27D"));
        return Netz.Aus(
        [
            new Teil(TeilArt.Quader, -b * 0.8f, 0, -b * 0.5f, b * 0.8f, h * 0.45f, b * 0.5f, Netz.Farbe("#334155")),
            new Teil(TeilArt.Quader, -b, h * 0.45f, -b * 0.6f, b, h * 0.82f, b * 0.6f, oben),
            new Teil(TeilArt.Quader, -b * 0.55f, h * 0.82f, -b * 0.55f, b * 0.55f, h, b * 0.55f, haut),
        ]);
    }

    /// <summary>Ferne-Form eines Modells: untere und obere Haelfte als Kasten in der mittleren Farbe der Haelfte (oben schmaler).</summary>
    private static void FernKaesten(Netz netz, GrafikModell m, Matrix4x4 lage, Modellfaerbung? faerbung)
    {
        var mitteY = (m.Min.Y + m.Max.Y) / 2;
        var summe = new Vector3[2];
        var zahl = new int[2];
        for (var d = 0; d < m.Dreiecke; d++)
        {
            var y = (m.Ecken[(d * 9) + 1] + m.Ecken[(d * 9) + 4] + m.Ecken[(d * 9) + 7]) / 3;
            var h = y < mitteY ? 0 : 1;
            summe[h] += faerbung is null
                ? m.Farbe(d)
                : faerbung(d, m.Farbe(d), 0.5f, Vector3.Transform(new Vector3(0, y, 0), lage).Y);
            zahl[h]++;
        }
        var unten = Vector3.Transform(new Vector3((m.Min.X + m.Max.X) / 2, m.Min.Y, (m.Min.Z + m.Max.Z) / 2), lage);
        var oben = Vector3.Transform(new Vector3((m.Min.X + m.Max.X) / 2, m.Max.Y, (m.Min.Z + m.Max.Z) / 2), lage);
        var breite = MathF.Max(m.Max.X - m.Min.X, m.Max.Z - m.Min.Z) * new Vector3(lage.M11, lage.M12, lage.M13).Length() / 2;
        var mitte = (unten.Y + oben.Y) / 2;
        for (var h = 0; h < 2; h++)
        {
            if (zahl[h] == 0)
            {
                continue;
            }
            var b = breite * (h == 0 ? 1f : 0.55f);
            netz.Hinzu(new Teil(TeilArt.Quader, unten.X - b, h == 0 ? unten.Y : mitte, unten.Z - b, unten.X + b, h == 0 ? mitte : oben.Y, unten.Z + b, summe[h] / zahl[h]));
        }
    }

    /// <summary>Phase 8 W9: Faerbung eines Modells an seiner Lage (Samen und Nadelbaum je Modell); null im Sommer.</summary>
    private Modellfaerbung? ModellFaerbung(GrafikModell modell, Matrix4x4 lage, Faerbeart art) =>
        Jahreszeitfarben.FuerModell(Jahreszeit, art, modell, Jahreszeitfarben.Samen(lage.Translation), Jahreszeitfarben.Nadel(modell));

    /// <summary>
    /// Haeuser, die zwischen Auge und Figur stehen (Sammelliste 102): die Strecken vom Auge zu Fuss,
    /// Mitte und Kopf der Figur schneiden ihren Kasten (etwas verkleinert, damit ein Haus, vor dem die
    /// Figur steht, nicht zaehlt).
    /// </summary>
    public List<StadtHaus> Verdeckt(Vector3 auge, float figurX, float figurZ, float figurHoehe)
    {
        var ergebnis = new List<StadtHaus>();
        Verdeckt(auge, figurX, figurZ, figurHoehe, ergebnis);
        return ergebnis;
    }

    /// <summary>Wie <see cref="Verdeckt(Vector3, float, float, float)"/>, fuellt aber <paramref name="ergebnis"/> (je Bild ohne neue Liste, Wunsch 4).</summary>
    public void Verdeckt(Vector3 auge, float figurX, float figurZ, float figurHoehe, ICollection<StadtHaus> ergebnis)
    {
        ergebnis.Clear();
        foreach (var h in Haeuser)
        {
            var min = h.Min + new Vector3(0.05f, 0, 0.05f);
            var max = h.Max - new Vector3(0.05f, 0, 0.05f);
            foreach (var anteil in (ReadOnlySpan<float>)[0.15f, 0.5f, 0.95f])
            {
                if (Schneidet(auge, new Vector3(figurX, figurHoehe * anteil, figurZ), min, max))
                {
                    ergebnis.Add(h);
                    break;
                }
            }
        }
    }

    /// <summary>Schneidet die Strecke a-b den Kasten (Platten-Verfahren)?</summary>
    public static bool Schneidet(Vector3 a, Vector3 b, Vector3 min, Vector3 max)
    {
        var d = b - a;
        float t0 = 0, t1 = 1;
        for (var i = 0; i < 3; i++)
        {
            var da = i == 0 ? d.X : i == 1 ? d.Y : d.Z;
            var pa = i == 0 ? a.X : i == 1 ? a.Y : a.Z;
            var lo = i == 0 ? min.X : i == 1 ? min.Y : min.Z;
            var hi = i == 0 ? max.X : i == 1 ? max.Y : max.Z;
            if (MathF.Abs(da) < 1e-8f)
            {
                if (pa < lo || pa > hi)
                {
                    return false;
                }
                continue;
            }
            var u = (lo - pa) / da;
            var v = (hi - pa) / da;
            if (u > v)
            {
                (u, v) = (v, u);
            }
            t0 = MathF.Max(t0, u);
            t1 = MathF.Min(t1, v);
            if (t0 > t1)
            {
                return false;
            }
        }
        return true;
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

    /// <summary>Welle K3: heller Rand (Bordstein) der Mittelinsel eines Kreisverkehrs.</summary>
    public static readonly Vector3 InselRand = Mischen(Farben.Strasse, Weiss, 0.7f);
    private static readonly Vector3 Schwarz = Vector3.Zero;

    private static class Farben
    {
        public static readonly Vector3 Boden = Netz.Farbe("#9CC48A");
        public static readonly Vector3 Park = Netz.Farbe("#7FB069");
        public static readonly Vector3 Wasser = Netz.Farbe("#4FA8D8");
        public static readonly Vector3 Ufer = Netz.Farbe("#B9A77A");
        public static readonly Vector3 Schwelle = Netz.Farbe("#6E4A2F");
        public static readonly Vector3 Stahl = Netz.Farbe("#5E6472");
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

    /// <summary>Welle A 0.61.0: Gelb der Zickzack-Linie an Haltestellen.</summary>
    private static readonly Vector3 ZickzackGelb = Netz.Farbe("#F2C21B");

    /// <summary>Welle A 0.61.0: achsparalleles Rechteck um die Punkte (fuer das Laufraster).</summary>
    private static (double, double, double, double) Rechteck(Vector2[] p) =>
        (p.Min(q => q.X), p.Min(q => q.Y), p.Max(q => q.X), p.Max(q => q.Y));

    private static Teil Band(float x1, float z1, float x2, float z2, float hoehe, float breite, Vector3 farbe) =>
        new(TeilArt.Band, x1, hoehe, z1, x2, breite, z2, farbe);

    private static float Zahl(PyObjekt teil, string name, float vorgabe = 0f) => Py.Hat(teil, name) ? (float)Py.F(teil[name]) : vorgabe;

    private static List<(float X, float Z)> Punkte(object? punkte) =>
        [.. Liste(punkte).Select(p => Liste(p)).Select(p => ((float)Py.F(p[0]), (float)Py.F(p[1])))];

    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 4): nur die <see cref="Signatur"/> der Stadt fuer einen Spielstand,
    /// ohne Geometrie (Bauen braucht am PC rund 160 ms und 12 MB; vorher baute jede Ankunft die ganze
    /// Stadt neu, nur um die Signatur zu vergleichen). Gleich <c>Bauen(stadt, stand).Signatur</c>.
    /// </summary>
    /// <remarks>Phase 8 W6: mit Festzeit steht "fest:&lt;festzeit&gt;" angehaengt am Ende.</remarks>
    public static string SignaturFuer(IStadtSicht stadt, ISpielSicht? stand, string? fest = null)
    {
        var inhalt = stadt.Inhalt;
        var welt = Objekt(Objekt(inhalt["orte"])["welt"]);
        var k = StadtModelle.Kontext(stand, inhalt);
        var modelle = StadtModelle.Modelle(inhalt, stadt.Daten);
        var signatur = new List<string>();
        foreach (var item in ListeOderLeer(welt, "landschaft").Select(Objekt).Where(t => Py.Gleich(t["typ"], "gebaeude")))
        {
            var modell = Py.Str(item["modell"]);
            signatur.Add(modell + ":" + string.Join(",", OhneGeruest(StadtModelle.Teile(modelle, modell, k)).Select(t => t.Nr)));
        }
        foreach (var ort in stadt.Orte(stand))
        {
            signatur.Add(Py.Str(ort["id"]) + ":" + string.Join(",", StadtModelle.Teile(modelle, Py.Str(ort["modell"]), k).Select(t => t.Nr)));
        }
        if (fest is not null)
        {
            signatur.Add("fest:" + fest);
        }
        return string.Join(";", signatur);
    }

    /// <summary>
    /// Die Stadt fuer einen Spielstand (null = wie ein neuer Spielstand ohne Ereignisse). Mit
    /// <paramref name="grafik"/> stehen statt der eigenen Bloecke, Baeume und Schienen die CC0-Modelle
    /// (Zuordnung je Block in zuordnung.json; Bloecke ohne Zuordnung bleiben eigene Geometrie).
    /// </summary>
    /// <remarks>
    /// Phase 8 W6: <paramref name="fest"/> = Festzeit (null = keine, die Szene wie vorher), <paramref name="feste"/> =
    /// Inhalt von festzeiten.json; die Deko kommt nur angehaengt dazu (<see cref="FestDeko.Stadt"/>).
    /// Phase 8 W9: <paramref name="jahreszeit"/> faerbt nur die Netze (<see cref="Jahreszeitfarben"/>), die Szene
    /// selbst (Teile, Modelle, Laufraster, Signatur) bleibt gleich.
    /// </remarks>
    public static StadtSzene Bauen(IStadtSicht stadt, ISpielSicht? stand, GrafikBibliothek? grafik = null, string? fest = null, PyObjekt? feste = null,
        Jahreszeit jahreszeit = Jahreszeit.Sommer, Vorrat? vorrat = null)
    {
        if (vorrat is null)
        {
            return BauenOhneSperre(stadt, stand, grafik, fest, feste, jahreszeit, null);
        }
        lock (vorrat.Sperre)
        {
            vorrat.Beginnen(grafik);
            return BauenOhneSperre(stadt, stand, grafik, fest, feste, jahreszeit, vorrat);
        }
    }

    private static StadtSzene BauenOhneSperre(IStadtSicht stadt, ISpielSicht? stand, GrafikBibliothek? grafik, string? fest, PyObjekt? feste,
        Jahreszeit jahreszeit, Vorrat? vorrat)
    {
        var stuecke = new List<ModellStueck>();
        var inhalt = stadt.Inhalt;
        // Phase 13 Welle 1 (Weisung P12-16): Laternenplaetze und Beete aus der Stadt-Fassung (vorher fest im Programm)
        var laternenSkizze = stadt.Fassung?.Laternen ?? Nachtlicht.Skizze;
        var beeteFest = stadt.Fassung?.Beete ?? Nachtlicht.Beete;
        var welt = Objekt(Objekt(inhalt["orte"])["welt"]);
        var breite = (float)Py.F(welt["breite"]);
        var tiefe = (float)Py.F(welt["hoehe"]);
        // Welle A 0.66.0 (Weisung P12-192): Gelaende (ohne Abschnitt null, dann aendert sich nichts)
        var gelaende = Gelaende.Aus(stadt.Daten);
        var teile = new List<Teil>
        {
            // Boden weit ueber den Rand, damit die geneigte Kamera keinen leeren Himmel zeigt
            Quader(-30, -0.06f, -30, breite + 30, 0f, tiefe + 30, Mischen(Farben.Boden, Schwarz, 0.12f)),
            Quader(0, -0.05f, 0, breite, 0.001f, tiefe, Farben.Boden),
        };
        var streifen = new List<Streifen>();
        var hindernisse = new List<(double, double, double, double)>();
        var wasser = new List<List<Vector2>>();
        var parkFlaechen = new List<(float X1, float Z1, float X2, float Z2)>();
        var gleise = new List<List<Vector2>>();
        var gleisWerkzeug = new List<bool>();
        var andreaskreuze = new List<Bahnuebergang.Kreuz>();
        var landschaft = ListeOderLeer(welt, "landschaft").Select(Objekt).ToList();
        var strassen = landschaft.Where(t => Py.Gleich(t["typ"], "strasse")).Select(t => Vektoren(t["punkte"])).ToList();
        // Phase 13 Welle 4a (Weisung P12-28): Strassen mit Gehweg (Feld "gehweg" = Breite je Seite, nur aus dem Stadtbau-
        // Werkzeug; Fassung 0 hat keine): Gehweg gezeichnet, frei zum Gehen, kein Kleingruen darauf (Laternen stehen wie
        // bisher 0,5 neben der Strassenmitte, also auf dem Gehweg)
        var gehwege = landschaft.Where(t => Py.Gleich(t["typ"], "strasse") && GehwegBreite(t) > 0)
            .Select(t => (Linie: Vektoren(t["punkte"]), Halb: (StrassenBreite / 2) + GehwegBreite(t))).ToList();
        // Welle A 0.54.0 (Weisung P12-115, Sammelliste 254/255): Zebrastreifen aus stadtdaten.zebrastreifen; die Fahrbahn
        // einer Strasse mit Gehweg ist nur dort frei zum Gehen (Fassung 0: keine Zebrastreifen, keine Strasse mit Gehweg)
        var zebras = Zebrastreifen.Alle(stadt.Daten, strassen);
        // Welle A 0.60.0 (Weisung P12-166): Ampeln aus stadtdaten.ampeln; ihre Furten sind fuer die Figur frei wie ein
        // Zebrastreifen (Fassung 0: keine Ampeln)
        var ampeln = Probe.Ampeln.Alle(stadt.Daten, strassen);
        var furten = ampeln.SelectMany(a => a.Furten).ToList();
        // Welle A 0.61.0 (Weisung P12-171): Haltestellen und Parkplaetze an geraden Strassen mit Gehweg (Fassung 0: keine)
        var gehwegLinien = gehwege.Select(g => (g.Linie, g.Halb)).ToList();
        var haltestellen = Probe.Haltestellen.Alle(stadt.Daten, gehwegLinien);
        var parkplaetze = Probe.Parkplaetze.Alle(stadt.Daten, gehwegLinien);
        var zugangswege = stadt.Zugaenge.Select(z => Vektoren(Objekt(z.Value)["weg"])).ToList();
        // W8b: Gehwege an Haeusern ohne Ort (Stellwerk) halten Baeume fern und sind frei zum Gehen
        zugangswege.AddRange(StadtTueren.Zierwege(stadt).Select(z => Vektoren(z["weg"])));
        // Welle Z (Weisung P12-71): eigene Fusswege aus stadtdaten.fusswege wie die Wege zu den Tueren (frei zum Gehen,
        // Baeume und Kleingruen halten Abstand); Fassung 0 hat keine
        var fusswege = Fusswege.Alle(stadt.Daten);
        zugangswege.AddRange(fusswege.Select(f => f.Glatt));
        // Nachtrag 1 zu P7-13 (Chat, Foto 02): gezeichnet (mit Bahnuebergang) wird nur der Fussweg eines
        // sichtbaren Orts; Weg zu Talheim-Nord und zur Werkstatt erscheinen mit dem Ort (Tag 12 bzw. 22),
        // vorher lag er als Ring ohne Ziel auf der Wiese. Laufraster und Baumsperren nutzen alle Wege.
        var sichtbareOrte = stadt.Orte(stand).Select(o => Py.Str(o["id"])).ToHashSet();
        var sichtbareWege = stadt.Zugaenge.Where(z => sichtbareOrte.Contains(z.Key)).Select(z => Vektoren(Objekt(z.Value)["weg"])).ToList();
        var bruecken = new List<Vector2>();
        var brueckenKaesten = new List<(Vector2, Vector2, float, float)>();
        var strassenEnden = new List<(Vector2 Punkt, Vector2 Richtung, float Verlaengert)>();
        var uebergaenge = new List<(Vector2 Mitte, bool Strasse)>();
        foreach (var art in (string[])["park", "fluss", "strasse", "bahn"])
        {
            foreach (var item in landschaft.Where(t => Py.Gleich(t["typ"], art)))
            {
                switch (art)
                {
                    case "park":
                    {
                        float x = Zahl(item, "x"), z = Zahl(item, "y");
                        teile.Add(Quader(x, 0f, z, x + Zahl(item, "w"), Hoehe.Park, z + Zahl(item, "h"), Farben.Park));
                        parkFlaechen.Add((x, z, x + Zahl(item, "w"), z + Zahl(item, "h")));
                        break;
                    }
                    case "fluss":
                    {
                        // Bruecken (Wunsch Nico 25.10.2026): wo eine Strasse den Fluss kreuzt
                        // Weisung P12-214 (Nico): die Bruecke umrahmt auch den Gehweg; Strassen ohne Gehweg wie bisher (gw = 0)
                        var strassenGehweg = landschaft.Where(t => Py.Gleich(t["typ"], "strasse")).Select(GehwegBreite).ToList();
                        for (var si = 0; si < strassen.Count; si++)
                        {
                            var gw = BrueckeSchmalAlt.Value ? 0f : strassenGehweg[si];
                            foreach (var (mitte, richtung, sinus) in Kreuzungen(Linienzug(item), strassen[si]))
                            {
                                Bruecke(teile, streifen, mitte, richtung, sinus, gw);
                                bruecken.Add(mitte);
                                brueckenKaesten.Add((mitte, richtung, BrueckeHalb(sinus) + 0.07f, (StrassenBreite / 2) + gw + 0.1f));
                            }
                        }
                        // glatter Bogen, gleich breit, mit Ufer (Nachtrag 1, Wunsch 2); Welle K1: Radius je Eckpunkt
                        var glatt = Linienzug(item);
                        streifen.Add(new Streifen(glatt, FlussBreite + 0.3f, Hoehe.Ufer, Hoehe.Ufer, Farben.Ufer));
                        streifen.Add(new Streifen(glatt, FlussBreite, Hoehe.Wasser, Hoehe.Wasser, Farben.Wasser));
                        wasser.Add(glatt);
                        break;
                    }
                    case "strasse":
                        {
                            // Phase 8 W11 (Sammelliste 157): ein Strassenende, das weder am Kartenrand noch an einer
                            // anderen Strasse liegt, endet buendig an der Gehwegkante (halbe Gehwegbreite statt halber Strassenbreite)
                            var pp = Punkte(item["punkte"]);
                            var alleStr = landschaft.Where(t => Py.Gleich(t["typ"], "strasse")).Select(t => Vektoren(t["punkte"])).ToList();
                            float Ende(Vector2 e)
                            {
                                var amRand = e.X <= 0.01f || e.Y <= 0.01f || e.X >= breite - 0.01f || e.Y >= tiefe - 0.01f;
                                var anStrasse = alleStr.Any(a => !a.SequenceEqual(Vektoren(item["punkte"])) && Kurve.Abstand(e, a) <= 0.05f);
                                return amRand || anStrasse ? StrassenBreite / 2 : WegBreite / 2;
                            }
                            var eigeneLinie = Vektoren(item["punkte"]);
                            if (Kreisverkehr.IstRing(item) && Kreisverkehr.Geschlossen(eigeneLinie))
                            {
                                // Welle K3 (Weisung P12-59): der Ring als geschlossenes Band mit Gehrung auch am Stoss (je
                                // ein Stueck ueber Anfang und Ende hinaus, gleiche Hoehe und Farbe), ohne Strassenenden
                                var ring = new List<Vector2>(eigeneLinie.Count + 2) { eigeneLinie[^2] };
                                ring.AddRange(eigeneLinie);
                                ring.Add(eigeneLinie[1]);
                                if (GehwegBreite(item) is var gwr and > 0)
                                {
                                    streifen.Add(new Streifen(ring, StrassenBreite + (2 * gwr), Hoehe.Gehweg, Hoehe.Gehweg, Mischen(Farben.Strasse, Weiss, 0.45f)));
                                }
                                streifen.Add(new Streifen(ring, StrassenBreite, Hoehe.Strasse, Hoehe.Strasse, Farben.Strasse));
                                break;
                            }
                            if (AlsBand(eigeneLinie))
                            {
                                // Welle K1 (Weisung P12-55): Kurve als ein Band mit Gehrung, Enden wie bei der geraden Strasse
                                var r0 = Vector2.Normalize(eigeneLinie[1] - eigeneLinie[0]);
                                var rn = Vector2.Normalize(eigeneLinie[^1] - eigeneLinie[^2]);
                                var w0 = Weiter(eigeneLinie[0], -r0, eigeneLinie, alleStr);
                                var wn = Weiter(eigeneLinie[^1], rn, eigeneLinie, alleStr);
                                var e0 = w0 is null ? Ende(eigeneLinie[0]) : 0f;
                                var en = wn is null ? Ende(eigeneLinie[^1]) : 0f;
                                strassenEnden.Add((eigeneLinie[0], -r0, e0));
                                strassenEnden.Add((eigeneLinie[^1], rn, en));
                                var band = new List<Vector2>(eigeneLinie);
                                // stumpf angesetzt: ein kurzes Stueck (StumpfZugabe) in Richtung der anschliessenden Strasse, damit
                                // die Kante genau quer zu ihr liegt wie das Ende ihres Bands (sonst ein Keil von 3,75 Grad)
                                band.Insert(0, w0 is { } a0 ? eigeneLinie[0] + (a0 * StumpfZugabe) : eigeneLinie[0] - (r0 * e0));
                                band.Add(wn is { } an ? eigeneLinie[^1] + (an * StumpfZugabe) : eigeneLinie[^1] + (rn * en));
                                if (GehwegBreite(item) is var gwk and > 0)
                                {
                                    streifen.Add(new Streifen(band, StrassenBreite + (2 * gwk), Hoehe.Gehweg, Hoehe.Gehweg, Mischen(Farben.Strasse, Weiss, 0.45f)));
                                }
                                streifen.Add(new Streifen(band, StrassenBreite, Hoehe.Strasse, Hoehe.Strasse, Farben.Strasse));
                                break;
                            }
                            for (var i = 0; i + 1 < pp.Count; i++)
                            {
                                var a = new Vector2(pp[i].X, pp[i].Z);
                                var b2 = new Vector2(pp[i + 1].X, pp[i + 1].Z);
                                var r = Vector2.Normalize(b2 - a);
                                var ea = i == 0 ? (Fortgesetzt(a, -r, eigeneLinie, alleStr) ? 0f : Ende(a)) : StrassenBreite / 2;
                                var eb = i + 2 == pp.Count ? (Fortgesetzt(b2, r, eigeneLinie, alleStr) ? 0f : Ende(b2)) : StrassenBreite / 2;
                                if (i == 0)
                                {
                                    strassenEnden.Add((a, -r, ea));
                                }
                                if (i + 2 == pp.Count)
                                {
                                    strassenEnden.Add((b2, r, eb));
                                }
                                if (GehwegBreite(item) is var gw and > 0)
                                {
                                    teile.Add(Band(a.X - (r.X * ea), a.Y - (r.Y * ea), b2.X + (r.X * eb), b2.Y + (r.Y * eb), Hoehe.Gehweg,
                                        StrassenBreite + (2 * gw), Mischen(Farben.Strasse, Weiss, 0.45f)));
                                }
                                teile.Add(Band(a.X - (r.X * ea), a.Y - (r.Y * ea), b2.X + (r.X * eb), b2.Y + (r.Y * eb), Hoehe.Strasse, StrassenBreite, Farben.Strasse));
                            }
                        }
                        break;
                    case "bahn":
                    {
                        var glatt = Linienzug(item);
                        uebergaenge.AddRange(Gleis(teile, streifen, andreaskreuze, glatt, strassen, sichtbareWege));
                        gleise.Add(glatt);
                        gleisWerkzeug.Add(Radien(item) is not null);
                        break;
                    }
                }
            }
        }
        // S 0.8.0 (Weisung P12-47): Teiche (Ufer und Wasser als Flaechen im Netz der Stadt) und Stege (Holzkaesten)
        var teiche = Wasser.Teiche(stadt.Daten);
        var stege = Wasser.Stege(stadt.Daten);
        var flaechen = new List<Bodenflaeche>();
        foreach (var teich in teiche)
        {
            flaechen.Add(new Bodenflaeche(teich.MitUfer, Hoehe.Ufer, Farben.Ufer));
            flaechen.Add(new Bodenflaeche(teich.Rund, Hoehe.Wasser, Farben.Wasser));
        }
        foreach (var steg in stege)
        {
            teile.AddRange(Wasser.StegTeile(steg));
        }
        // Mittellinien und Fusswege der Stadt (Zugaenge von der Tuer zur Strasse); keine Striche
        // mitten auf einer Kreuzung (dort lagen sonst zwei Striche als "+" uebereinander)
        // Welle K3 (Sammelliste 231): Strassen, die nach Weiter aneinander anschliessen (Kurve an gerade Strasse, Kurve an
        // Kurve), bekommen EINE Mittellinie im gleichen Takt; vorher lief der Takt je Strasse ab 0 und die Kreuzungsregel nahm
        // am Anschluss auf beiden Seiten Striche weg (Luecke). Eine Strasse ohne solchen Anschluss (alle in Fassung 0) wie bisher.
        foreach (var (x1, z1, x2, z2) in MittellinienStriche(strassen))
        {
            // Welle A 0.54.0: kein Strich auf einem Zebrastreifen
            if (zebras.Any(z => z.Drauf(new Vector2(x1, z1), 0.02f) || z.Drauf(new Vector2(x2, z2), 0.02f) || z.Drauf(new Vector2((x1 + x2) / 2, (z1 + z2) / 2), 0.02f))
                || furten.Any(z => z.Drauf(new Vector2(x1, z1), 0.02f) || z.Drauf(new Vector2(x2, z2), 0.02f) || z.Drauf(new Vector2((x1 + x2) / 2, (z1 + z2) / 2), 0.02f)))
            {
                continue;
            }
            teile.Add(Band(x1, z1, x2, z2, Hoehe.Mittellinie, 0.04f, Farben.Mittellinie));
        }
        // Welle A 0.54.0: weisse Balken je Zebrastreifen (flach auf der Hoehe der Mittellinie, je Balken 2 Dreiecke)
        foreach (var z in zebras)
        {
            foreach (var (a, b) in Zebrastreifen.BalkenVon(z))
            {
                teile.Add(Band(a.X, a.Y, b.X, b.Y, Hoehe.Mittellinie, Zebrastreifen.BalkenBreite, Weiss));
            }
        }
        // Welle A 0.60.0: je Arm einer Ampel die Furt (zwei gestrichelte Linien) und die Haltelinie
        foreach (var amp in ampeln)
        {
            foreach (var arm in amp.Arme)
            {
                var (h1, h2) = Probe.Ampeln.Haltelinie(amp, arm);
                teile.Add(Band(h1.X, h1.Y, h2.X, h2.Y, Hoehe.Mittellinie, Probe.Ampeln.HalteBreite, Weiss));
            }
            foreach (var furt in amp.Furten)
            {
                foreach (var (a, b) in Probe.Ampeln.FurtStriche(furt))
                {
                    teile.Add(Band(a.X, a.Y, b.X, b.Y, Hoehe.Mittellinie, Probe.Ampeln.FurtStrichBreite, Weiss));
                }
            }
        }
        // Welle A 0.61.0: gelbe Zickzack-Linie je Haltestelle; Parkplatz als Flaeche mit weissen Linien, Einfahrt ueber den Gehweg
        foreach (var h in haltestellen)
        {
            foreach (var (a, b) in Probe.Haltestellen.Zickzack(h))
            {
                teile.Add(Band(a.X, a.Y, b.X, b.Y, Hoehe.Mittellinie, Probe.Haltestellen.ZickzackBreite, ZickzackGelb));
            }
            hindernisse.Add(Rechteck(h.HaeuschenEcken()));
        }
        foreach (var pp in parkplaetze)
        {
            flaechen.Add(new Bodenflaeche(GegenUhr(pp.Ecken()), Probe.Parkplaetze.FlaecheHoehe + 0.002f, Probe.Parkplaetze.Flaeche));
            var (e1, e2) = pp.Einfahrt;
            teile.Add(Band(e1.X, e1.Y, e2.X, e2.Y, Probe.Parkplaetze.FlaecheHoehe + 0.002f, Probe.Parkplaetze.Gasse, Probe.Parkplaetze.Flaeche));
            foreach (var (a, b) in pp.Linien())
            {
                teile.Add(Band(a.X, a.Y, b.X, b.Y, Probe.Parkplaetze.LinieHoehe, 0.025f, Weiss));
            }
        }
        foreach (var (_, zugang) in stadt.Zugaenge.Where(z => sichtbareOrte.Contains(z.Key)))
        {
            // Sammelliste 280 (A 0.59.1): ein Weg mit Feld radien als ein Band mit runden Ecken (ohne Feld wie bisher)
            if (StadtTuerweg.Gezeichnet(zugang, WegBreite) is { } rund)
            {
                streifen.Add(new Streifen(rund, WegBreite, Hoehe.Weg, Hoehe.Weg, Mischen(Farben.Strasse, Weiss, 0.45f)));
                continue;
            }
            Linie(teile, Punkte(Objekt(zugang)["weg"]), Hoehe.Weg, WegBreite, Mischen(Farben.Strasse, Weiss, 0.45f));
        }
        // Welle Z: eigene Fusswege als ein Band mit Gehrung (gleiche Hoehe und Farbe wie die Wege zu den Tueren)
        foreach (var f in fusswege)
        {
            streifen.Add(new Streifen(f.Glatt, Fusswege.Breite, Hoehe.Weg, Hoehe.Weg, Mischen(Farben.Strasse, Weiss, 0.45f)));
        }
        // Nachtrag 1 zu P7-13 (Wunsch 1): Baeume weichen Strassen, Fusswegen, Gleisen und dem Fluss aus
        var baeume = new BaumPlaner([
            .. strassen.Select(l => ((IReadOnlyList<Vector2>)l, StrassenBreite / 2)),
            .. zugangswege.Select(l => ((IReadOnlyList<Vector2>)l, WegBreite / 2)),
            .. gleise.Select(l => ((IReadOnlyList<Vector2>)l, (GleisBreite / 2) + 0.1f)),
            .. wasser.Select(l => ((IReadOnlyList<Vector2>)l, (FlussBreite + 0.3f) / 2)),
        ]);
        foreach (var item in landschaft.Where(t => Py.Gleich(t["typ"], "baum")))
        {
            var r = Zahl(item, "r", 0.45f);
            foreach (var (x, z) in Punkte(item["punkte"]))
            {
                Baum(teile, stuecke, grafik, baeume, x, z, r);
            }
        }
        // Welle K3 (Weisung P12-59, Frage 2 = a): Mittelinsel jedes Kreisverkehrs (heller Rand, Rasen, Baum in der Mitte),
        // danach fuer Baeume, Kleingruen und Laufraster gesperrt; Fassung 0 hat keinen Kreisverkehr
        var inseln = new List<List<Vector2>>();
        foreach (var (ring, radius) in Kreisverkehr.Alle(landschaft))
        {
            var m = Kreisverkehr.Mitte(ring);
            var ri = Kreisverkehr.InselRadius(radius);
            flaechen.Add(new Bodenflaeche(Kreisverkehr.Kreis(m, ri), Kreisverkehr.RandHoehe, InselRand));
            flaechen.Add(new Bodenflaeche(Kreisverkehr.Kreis(m, ri - Kreisverkehr.RandBreite), Kreisverkehr.RasenHoehe, Farben.Park));
            Baum(teile, stuecke, grafik, baeume, m.X, m.Y, Kreisverkehr.BaumRadius);
            baeume.Sperren.Add(([m, m], ri));
            inseln.Add(Kreisverkehr.Kreis(m, ri - 0.02f));
        }

        var k = StadtModelle.Kontext(stand, inhalt);
        var modelle = StadtModelle.Modelle(inhalt, stadt.Daten);
        var signatur = new List<string>();
        var haeuser = new List<StadtHaus>();
        var fensterJeHaus = new Dictionary<string, List<(int, Vector3)>>();
        var alleFenster = new List<(int Teil, Vector3 Wand)>();
        // Welle A 0.70.0 (Sammelliste 314): fein gedrehte Gebaeude ohne Ort werden im Rahmen des Hauses gebaut und erst nach ihrer
        // Tuer (Zierweg unten) als Ganzes gedreht
        var feinOffen = new List<FeinBau>();
        var hausBloecke = new Dictionary<string, List<(double, double, double, double)>>();
        foreach (var item in landschaft.Where(t => Py.Gleich(t["typ"], "gebaeude")))
        {
            var modell = Py.Str(item["modell"]);
            var sichtbar = OhneGeruest(StadtModelle.Teile(modelle, modell, k));
            var farbe = Py.Hat(item, "farbe") ? Netz.Farbe(Py.Str(item["farbe"])) : RollenFarbe(null);
            var (tv, sv) = (teile.Count, stuecke.Count);
            var fenster = new List<(int, Vector3)>();
            var fein = new Hausdrehung.Feinlage(new Vector2(Zahl(item, "x"), Zahl(item, "y")) + Hausdrehung.Mitte(Get(modelle, modell)), Hausdrehung.GradVon(item));
            var feinBau = fein.Aktiv ? new FeinBau(fein, tv, sv) : null;
            var hListe = feinBau?.Hindernisse ?? hindernisse;
            var hVorher = hListe.Count;
            var (hmin, hmax) = Modell(teile, stuecke, grafik, modell, sichtbar, Zahl(item, "x"), Zahl(item, "y"), farbe, modell, feinBau?.Hindernisse ?? hindernisse, baeume, fenster: fenster,
                dreh: Hausdrehung.Von(item), drehMitte: Hausdrehung.Mitte(Get(modelle, modell)), grundVon: KopieGrund(modelle, modell), baeumeSpaeter: feinBau?.Baeume);
            alleFenster.AddRange(fenster);
            if (feinBau is not null)
            {
                feinBau.TeileBis = teile.Count;
                feinBau.StueckeBis = stuecke.Count;
                feinOffen.Add(feinBau);
            }
            if (hmin.X <= hmax.X)
            {
                var hausId = modell + "#" + haeuser.Count.ToString(CultureInfo.InvariantCulture);
                if (feinBau is not null)
                {
                    feinBau.Haus = haeuser.Count;
                }
                haeuser.Add(new StadtHaus(hausId, tv, teile.Count, sv, stuecke.Count, hmin, hmax));
                fensterJeHaus[hausId] = fenster;
                hausBloecke[hausId] = [.. hListe.Skip(hVorher)];
            }
            signatur.Add(modell + ":" + string.Join(",", sichtbar.Select(t => t.Nr)));
        }
        // W8b (Nico 3a): Haeuser ohne Ort (Stellwerk) mit Gehweg zur Strasse, Tuer und Namensschild, nicht betretbar
        var zierhaeuser = new List<StadtZierhaus>();
        var tueren = new List<StadtTuer>();
        var umgefaerbt = new Dictionary<int, GrafikModell>();
        // P8-28 (Nico: Tueren sitzen nicht am Haus, Fenster halb verdeckt): Tuer auf die echte Wand des CC0-Modells
        // (gemessen, StadtTueren.Wandtiefe), Gehweg bis dorthin, Rahmen nach hinten bis an die Wand, Fenster an der
        // Tuer weg (Glas der Modelle in Wandfarbe, eigene Fensterscheiben in der Farbe ihres Blocks); P8-30: Tueren der
        // Modelle daneben und an den Seitenwaenden samt Rahmen in Wandfarbe (nur eine Tuer je Haus), kein eigenes
        // Vordach unter einem Dach des Modells
        StadtTuer TuerSetzen(string ort, Vector2 wand, Vector2 aussen, Vector2 anfang, float hoehe, bool modellTuer, bool gehweg,
            int stueckeVon, int stueckeBis, List<(int Teil, Vector3 Wand)> fenster, Vector3 hausMin, Vector3 hausMax, bool windfang = false)
        {
            if (windfang && !WindfangAlt.Value)
            {
                // Welle A 0.70.0 (Weisung P12-218 Punkt 1 = a): kleiner Vorbau mit Tuer vorn, von der tiefsten Stelle der Wand bis vor
                // alles, was an dieser Stelle aus der Fassade ragt (Band der Betriebswerkstatt); die Tuer liegt buendig auf ihm
                var (vor, tief) = grafik is not null && stueckeBis > stueckeVon
                    ? StadtTueren.Ausdehnung(stuecke.GetRange(stueckeVon, stueckeBis - stueckeVon), grafik, wand, aussen, hoehe)
                    : (0f, 0f);
                var vorn = wand + (aussen * (MathF.Max(0f, -vor) + StadtTueren.WindfangVor));
                StadtTueren.Windfang(teile, wand - (aussen * (tief + 0.02f)), vorn, aussen, hoehe, Gebaeudefarben.Tuer(stadt.Daten, ort));
                if (gehweg && Vector2.Distance(vorn, anfang) > 0.02f && Vector2.Dot(anfang - vorn, aussen) > 0)
                {
                    Linie(teile, [(anfang.X, anfang.Y), (vorn.X, vorn.Y)], Hoehe.Weg, WegBreite, Mischen(Farben.Strasse, Weiss, 0.45f));
                }
                return new StadtTuer(ort, vorn, aussen, anfang, false, hoehe, 0f) { Windfang = true, AnWand = wand };
            }
            // Fund 262 (Weisung P12-122): hat das Modell an dieser Stelle selbst eine Tuer (Stadt-Modell: genau dort, Gebaeude
            // aus dem Editor: jede Glastuer, die die gezeichnete Tuer ueberdecken wuerde), ist sie die Tuer; keine zweite darueber
            if (!modellTuer && grafik is not null && stueckeBis > stueckeVon
                && StadtTueren.EigeneTuer(stuecke, stueckeVon, stueckeBis, i => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name), wand, aussen,
                    Enumerable.Range(stueckeVon, stueckeBis - stueckeVon).Any(i => StadtTueren.MitGlasTuer(stuecke[i].Name))) is { } eigene)
            {
                var querE = new Vector2(-aussen.Y, aussen.X);
                // liegt der Tuerpunkt auf der Tuer, bleibt er (Weg gerade auf die Tuer), sonst an ihren naechsten Rand
                var mitteE = wand + (querE * Math.Clamp(0f, eigene.A1, eigene.A2)) - (aussen * eigene.Tiefe);
                if (gehweg && Vector2.Distance(mitteE, anfang) > 0.02f && Vector2.Dot(anfang - mitteE, aussen) > 0)
                {
                    Linie(teile, [(anfang.X, anfang.Y), (mitteE.X, mitteE.Y)], Hoehe.Weg, WegBreite, Mischen(Farben.Strasse, Weiss, 0.45f));
                }
                // Sammelliste 267: die Glastuer eines Gebaeudes aus dem Editor wird als Tuer erkennbar (Tuerblatt und Klinke)
                if (StadtTueren.MitGlasTuer(stuecke[eigene.Stueck].Name))
                {
                    StadtTueren.TuerblattSetzen(teile, stuecke, i => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name), umgefaerbt, eigene, wand, aussen,
                        Gebaeudefarben.Tuer(stadt.Daten, ort));
                }
                // Sammelliste 292 (Nico 4a): eine gewaehlte Tuer-Farbe faerbt auch die Tuer eines Stadt-Modells (nur das Tuerblatt)
                else if (Gebaeudefarben.Tuer(stadt.Daten, ort) is { } tuerFarbe)
                {
                    StadtTueren.ModellTuerFaerben(stuecke, i => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name), umgefaerbt, eigene, tuerFarbe);
                }
                return new StadtTuer(ort, mitteE, aussen, anfang, true, hoehe, 0f);
            }
            var (tiefe, hinten) = grafik is not null && stueckeBis > stueckeVon
                ? StadtTueren.Wandtiefe(stuecke.GetRange(stueckeVon, stueckeBis - stueckeVon), grafik, wand, aussen, hoehe) ?? (0f, 0f)
                : (0f, 0f);
            var mitte = wand - (aussen * tiefe);
            if (gehweg && Vector2.Distance(mitte, anfang) > 0.02f && Vector2.Dot(anfang - mitte, aussen) > 0)
            {
                Linie(teile, [(anfang.X, anfang.Y), (mitte.X, mitte.Y)], Hoehe.Weg, WegBreite, Mischen(Farben.Strasse, Weiss, 0.45f));
            }
            if (modellTuer)
            {
                // Sammelliste 292 (Nico 4a): gewaehlte Tuer-Farbe auch auf der Tuer des Modells (Ort mit modell_tuer)
                if (grafik is not null && stueckeBis > stueckeVon && Gebaeudefarben.Tuer(stadt.Daten, ort) is { } farbeM
                    && StadtTueren.ModellTueren(stuecke, stueckeVon, stueckeBis, i => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name), wand, aussen,
                        weite: StadtTueren.EigeneTuerFang + StadtTueren.GlasTuerBreit).Where(t => MathF.Abs(t.Mitte) <= StadtTueren.HalbRahmen).MinBy(t => MathF.Abs(t.Mitte)) is { } mtF)
                {
                    StadtTueren.ModellTuerFaerben(stuecke, i => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name), umgefaerbt, mtF, farbeM);
                }
                return new StadtTuer(ort, mitte, aussen, anfang, true, hoehe, 0f);
            }
            var vordach = grafik is null || stueckeBis == stueckeVon
                || !StadtTueren.Ueberdacht(stuecke.GetRange(stueckeVon, stueckeBis - stueckeVon), grafik, mitte, aussen, hoehe);
            StadtTueren.Tuer(teile, mitte.X + (aussen.X * 0.005f), mitte.Y + (aussen.Y * 0.005f), aussen, hoehe, hinten, vordach,
                Gebaeudefarben.Tuer(stadt.Daten, ort));
            if (grafik is not null)
            {
                StadtTueren.FensterWeg(stuecke, stueckeVon, stueckeBis, grafik, umgefaerbt, mitte, aussen, hoehe, hinten);
                StadtTueren.TuerenWeg(stuecke, stueckeVon, stueckeBis, grafik, umgefaerbt, mitte, aussen);
                StadtTueren.SeitenTuerenWeg(stuecke, stueckeVon, stueckeBis, grafik, umgefaerbt, aussen, hausMin, hausMax);
            }
            foreach (var (nr, wandFarbe) in fenster)
            {
                var f = teile[nr];
                // Kasten: die zwei Ecken decken quer, Hoehe und Tiefe ganz ab (Waende laufen in x oder z)
                if (StadtTueren.AufTuerFlaeche(new(f.X1, f.Y1, f.Z1), new(f.X2, f.Y2, f.Z2), new(f.X1, f.Y1, f.Z1), mitte, aussen, hoehe, hinten))
                {
                    teile[nr] = f with { Farbe = wandFarbe };
                }
            }
            return new StadtTuer(ort, mitte, aussen, anfang, false, hoehe, hinten);
        }
        foreach (var zier in StadtTueren.Zierwege(stadt))
        {
            var weg = Vektoren(zier["weg"]);
            if (StadtTuerweg.Gezeichnet(zier, WegBreite) is { } rund)
            {
                streifen.Add(new Streifen(rund, WegBreite, Hoehe.Weg, Hoehe.Weg, Mischen(Farben.Strasse, Weiss, 0.45f)));
            }
            else
            {
                Linie(teile, [.. weg.Select(v => (v.X, v.Y))], Hoehe.Weg, WegBreite, Mischen(Farben.Strasse, Weiss, 0.45f));
            }
            if (haeuser.FirstOrDefault(h => h.Id.StartsWith(Py.Str(zier["modell"]) + "#", StringComparison.Ordinal)) is not { } haus)
            {
                continue;
            }
            // Welle A 0.70.0: Tuer eines fein gedrehten Gebaeudes im Rahmen des Hauses (Tuer und Wegpunkt zurueckgedreht)
            var zierFein = feinOffen.FirstOrDefault(f => f.Haus == haeuser.IndexOf(haus));
            var zierLage = zierFein?.Lage ?? default;
            var (zierTeile, zierTueren) = (teile.Count, tueren.Count);
            var zierAnfang = zierLage.Zurueck(weg[0]);
            var zierTuer = zierLage.Zurueck(Vektoren(new List<object?> { zier["tuer"] })[0]);
            var (wand, aussen) = StadtTueren.Wand(haus.Min, haus.Max, zierTuer);
            // Welle A 0.70.0 (Haltepunkt mit freiem Bahnsteig): liegt die Tuer innen im Kasten des Hauses (ein Bahnsteig gehoert
            // fest zum Modell), sitzt sie auf der naechsten Wand der Bloecke wie bei einem Ort; Tueren auf dem Kasten wie bisher
            // (Fassung 0 bitgleich), Gegenprobe ZierKastenAltFuerTests
            if (!ZierKastenAlt.Value && ImKastenInnen(haus.Min, haus.Max, zierTuer)
                && hausBloecke.GetValueOrDefault(haus.Id) is { Count: > 0 } zb)
            {
                var block = zb.MinBy(bk => Kurve.Abstand(zierTuer, [new((float)bk.Item1, (float)bk.Item2), new((float)bk.Item3, (float)bk.Item2),
                    new((float)bk.Item3, (float)bk.Item4), new((float)bk.Item1, (float)bk.Item4), new((float)bk.Item1, (float)bk.Item2)]));
                (wand, aussen) = StadtTueren.Wand(new Vector3((float)block.Item1, 0, (float)block.Item2), new Vector3((float)block.Item3, haus.Max.Y, (float)block.Item4), zierTuer);
            }
            // Sammelliste 312 (Weisung P12-207): ein Zierweg mit „modell_tuer“: true fuehrt auf die eigene Tuer des Modells (wie ein Ort
            // mit darstellung.modell_tuer, z. B. das helle Rolltor der Betriebswerkstatt); ohne das Feld wie bisher (Fassung 0 bitgleich)
            var zierModellTuer = !ZierModellTuerAlt.Value && Py.Wahr(zier["modell_tuer"]) && haus.StueckeBis > haus.StueckeVon;
            tueren.Add(TuerSetzen(Py.Str(zier["modell"]), wand, aussen, zierAnfang, MathF.Min(TuerHoehe, haus.Max.Y * 0.7f), zierModellTuer, true,
                haus.StueckeVon, haus.StueckeBis, fensterJeHaus.GetValueOrDefault(haus.Id) ?? [], haus.Min, haus.Max, Py.Wahr(zier["windfang"])));
            if (grafik is not null)
            {
                // Phase 8 W11 (Sammelliste 156): die Tuer des Modells auf der Rueckseite (ohne Gehweg) samt Glas und
                // Rahmen in Wandfarbe; die vordere Tuer mit Gehweg bleibt
                var c = new Vector2((haus.Min.X + haus.Max.X) / 2, (haus.Min.Z + haus.Max.Z) / 2);
                var tief = MathF.Abs(aussen.X) > 0.5f ? (haus.Max.X - haus.Min.X) / 2 : (haus.Max.Z - haus.Min.Z) / 2;
                var lang = MathF.Abs(aussen.X) > 0.5f ? (haus.Max.Z - haus.Min.Z) / 2 : (haus.Max.X - haus.Min.X) / 2;
                var rm = c - (aussen * tief);
                var querR = new Vector2(aussen.Y, -aussen.X);
                // Sammelliste 292 (Weisung P12-196): nur bis zur Hausmitte suchen (vorher 2 * tief = bis zur Vorderwand, das malte
                // die benutzte Modelltuer und die Fenster daneben zu); Gegenprobe RueckseiteAltFuerTests
                var suchTief = RueckseiteAlt.Value ? 2 * tief : tief;
                var gefunden = StadtTueren.ModellTueren(stuecke, haus.StueckeVon, haus.StueckeBis, i => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name), rm, -aussen, weite: lang, tief: suchTief);
                foreach (var mt in gefunden)
                {
                    StadtTueren.FensterWeg(stuecke, haus.StueckeVon, haus.StueckeBis, grafik, umgefaerbt, rm + (querR * mt.Mitte), -aussen, mt.Oben, mt.Tiefe + 0.1f);
                }
                StadtTueren.TuerenWeg(stuecke, haus.StueckeVon, haus.StueckeBis, grafik, umgefaerbt, rm, -aussen, lang, suchTief);
            }
            var ende = weg[^1];
            var (zMin, zMax) = zierFein is null ? (haus.Min, haus.Max) : zierLage.Kasten(haus.Min, haus.Max);
            zierhaeuser.Add(new StadtZierhaus(Py.Str(zier["name"]), new Vector3((zMin.X + zMax.X) / 2, zMax.Y + 0.55f, (zMin.Z + zMax.Z) / 2),
                zMin, zMax, ende.X, ende.Y));
            if (zierFein is not null)
            {
                zierFein.Weitere.Add((zierTeile, teile.Count));
                zierFein.Tueren.AddRange(Enumerable.Range(zierTueren, tueren.Count - zierTueren));
            }
        }
        foreach (var f in feinOffen)
        {
            FeinDrehen(f, teile, stuecke, hindernisse, tueren);
            foreach (var (bx, bz, br) in f.Baeume)
            {
                var p = f.Lage.Punkt(new Vector2(bx, bz));
                Baum(teile, stuecke, grafik, baeume, p.X, p.Y, br);
            }
            if (f.Haus >= 0)
            {
                var h = haeuser[f.Haus];
                var (wMin, wMax) = f.Lage.Kasten(h.Min, h.Max);
                haeuser[f.Haus] = h with { Min = wMin, Max = wMax, Fein = f.Lage, LokalMin = h.Min, LokalMax = h.Max };
            }
        }
        var orte = new List<StadtOrt>();
        // Phase 13 W3 (Weisung P12-21): mit Vorrat (nur Werkzeug) kommt ein Ort mit genau gleichen Eingaben aus dem letzten Bau
        var sperrenText = vorrat is null ? "" : SperrenText(baeume.Sperren);
        var orteNeu = new Dictionary<string, OrtBau>();
        // Welle A 0.58.0 (Weisung P12-148 Frage 1 = a): Bloecke je Ort, auf deren Wand das Spiel die Tuer setzt (nur lesend fuer das Werkzeug)
        var ortBloecke = new Dictionary<string, IReadOnlyList<(double X1, double Z1, double X2, double Z2)>>();
        foreach (var ort in stadt.Orte(stand))
        {
            var id = Py.Str(ort["id"]);
            var modell = Py.Str(ort["modell"]);
            var sichtbar = StadtModelle.Teile(modelle, modell, k);
            string? ortSchluessel = null;
            var (ortTv, ortSv, ortHv, ortBv, ortFv, ortTuer, ortHaus) = (teile.Count, stuecke.Count, hindernisse.Count, baeume.Gesetzt.Count, alleFenster.Count, tueren.Count, haeuser.Count);
            // Welle A 0.70.0 (Sammelliste 314): ein fein gedrehter Ort wird im Rahmen des Hauses gebaut (mit Tuer) und danach gedreht;
            // er kommt nie aus dem Vorrat des Werkzeugs (seine Bloecke liegen im Rahmen des Hauses, der Vorrat kennt nur die Welt)
            var ortFein = new Hausdrehung.Feinlage(new Vector2((float)Py.F(ort["x"]), (float)Py.F(ort["y"])) + Hausdrehung.Mitte(Get(modelle, modell)),
                Hausdrehung.GradVonOrt(stadt.Daten, id));
            if (vorrat is not null && !ortFein.Aktiv)
            {
                ortSchluessel = OrtSchluessel(stadt, ort, id, sichtbar, StadtTueren.ModellVersatz(stadt, id), sichtbareOrte.Contains(id), sperrenText);
                if (vorrat.Orte.TryGetValue(id, out var alt) && (alt.Schluessel == ortSchluessel || (vorrat.FalschUebernehmenFuerTests && orteNeu.Count == 0)))
                {
                    OrtEinsetzen(alt, teile, stuecke, hindernisse, baeume, alleFenster, umgefaerbt, tueren, haeuser, orte, signatur);
                    orteNeu[id] = alt;
                    ortBloecke[id] = [.. hindernisse.Skip(ortHv)];
                    vorrat.Uebernommen++;
                    continue;
                }
                vorrat.NeuGebaut++;
            }
            float ox = (float)Py.F(ort["x"]), oz = (float)Py.F(ort["y"]);
            var rolle = ort["rolle"] as string ?? "";
            var vorher = stuecke.Count;
            var teileVorher = teile.Count;
            var feinBau = ortFein.Aktiv ? new FeinBau(ortFein, teileVorher, vorher) : null;
            var ortHindernisse = feinBau?.Hindernisse ?? hindernisse;
            var hindernisVorher = ortHindernisse.Count;
            var modellTuer = StadtTueren.ModellVersatz(stadt, id);
            var fenster = new List<(int, Vector3)>();
            var (min, max) = Modell(teile, stuecke, grafik, modell, sichtbar, ox, oz, RollenFarbe(rolle), id, ortHindernisse, baeume, modellTuer ?? 0f, fenster,
                Hausdrehung.VonOrt(stadt.Daten, id), Hausdrehung.Mitte(Get(modelle, modell)), KopieGrund(modelle, modell), feinBau?.Baeume);
            alleFenster.AddRange(fenster);
            var mitGrafik = stuecke.Count > vorher;
            var (tx, tz) = stadt.Tuer(id) is { } zelle
                ? ((float)((zelle.Item1 + 0.5) * stadt.Zelle), (float)((zelle.Item2 + 0.5) * stadt.Zelle))
                : ((min.X + max.X) / 2, max.Z);
            if (Get(stadt.Zugaenge, id) is PyObjekt zugang)
            {
                // W8b (Nico 4a): die Tuer sitzt genau am Anfang des Fusswegs auf der naechsten Wand des Hauses
                // (Bloecke dieses Orts); bleibt eine Luecke, laeuft der Gehweg bis an die Wand
                var anfang = Vektoren(zugang["weg"])[0];
                (tx, tz) = (anfang.X, anfang.Y);
                anfang = ortFein.Zurueck(anfang);
                var bloecke = ortHindernisse.Skip(hindernisVorher).ToList();
                if (bloecke.Count > 0)
                {
                    var block = bloecke.MinBy(bk => Kurve.Abstand(anfang, [new((float)bk.Item1, (float)bk.Item2), new((float)bk.Item3, (float)bk.Item2),
                        new((float)bk.Item3, (float)bk.Item4), new((float)bk.Item1, (float)bk.Item4), new((float)bk.Item1, (float)bk.Item2)]));
                    var (wand, aussen) = StadtTueren.Wand(new Vector3((float)block.Item1, 0, (float)block.Item2), new Vector3((float)block.Item3, max.Y, (float)block.Item4), anfang);
                    // niedrige Haeuser (Kiosk, Bahnhof) bekommen eine niedrigere Tuer
                    var hBlock = sichtbar.Where(t => Py.Gleich(t.Teil["art"], "block")).Select(t => Zahl(t.Teil, "h")).DefaultIfEmpty(1f).Max();
                    tueren.Add(TuerSetzen(id, wand, aussen, anfang, MathF.Min(TuerHoehe, hBlock * 0.7f), modellTuer is not null && mitGrafik,
                        sichtbareOrte.Contains(id), vorher, stuecke.Count, fenster, min, max, StadtTueren.MitWindfang(stadt, id)));
                }
            }
            else if (!mitGrafik)
            {
                // ohne Fussweg: kleines Rechteck vorne oder hinten an der Front
                var lt = ortFein.Zurueck(new Vector2(tx, tz));
                var vorne = lt.Y >= (min.Z + max.Z) / 2;
                var seite = vorne ? max.Z : min.Z;
                teile.Add(Quader(lt.X - (TuerBreite / 2), 0f, vorne ? seite - 0.02f : seite - 0.03f, lt.X + (TuerBreite / 2), TuerHoehe, vorne ? seite + 0.03f : seite + 0.02f, Farben.Tuer));
            }
            var (lokalMin, lokalMax) = (min, max);
            if (feinBau is not null)
            {
                feinBau.TeileBis = teile.Count;
                feinBau.StueckeBis = stuecke.Count;
                feinBau.Tueren.AddRange(Enumerable.Range(ortTuer, tueren.Count - ortTuer));
                FeinDrehen(feinBau, teile, stuecke, hindernisse, tueren);
                foreach (var (bx, bz, br) in feinBau.Baeume)
                {
                    var p = ortFein.Punkt(new Vector2(bx, bz));
                    Baum(teile, stuecke, grafik, baeume, p.X, p.Y, br);
                }
                if (min.X <= max.X)
                {
                    (min, max) = ortFein.Kasten(min, max);
                }
            }
            if (min.X <= max.X)
            {
                haeuser.Add(feinBau is null
                    ? new StadtHaus(id, teileVorher, teile.Count, vorher, stuecke.Count, min, max)
                    : new StadtHaus(id, teileVorher, teile.Count, vorher, stuecke.Count, min, max) { Fein = ortFein, LokalMin = lokalMin, LokalMax = lokalMax });
            }
            ortBloecke[id] = [.. ortHindernisse.Skip(hindernisVorher)];
            var schild = new Vector3((min.X + max.X) / 2, max.Y + 0.55f, (min.Z + max.Z) / 2);
            var (sx, sz) = Stehplatz(stadt, id, tx, tz);
            orte.Add(new StadtOrt(id, ort["name"] as string ?? id, ort["kurz"] as string ?? ort["name"] as string ?? id, rolle,
                tx, tz, min, max, schild) { StehX = sx, StehZ = sz, Ansicht = ort["ansicht"] as string });
            signatur.Add(id + ":" + string.Join(",", sichtbar.Select(t => t.Nr)));
            if (ortSchluessel is not null)
            {
                orteNeu[id] = OrtFesthalten(ortSchluessel, ortTv, ortSv, ortHv, ortBv, ortFv, ortTuer, ortHaus, teile, stuecke, hindernisse, baeume,
                    alleFenster, umgefaerbt, tueren, haeuser, orte, signatur);
            }
        }
        if (vorrat is not null)
        {
            vorrat.Orte = orteNeu;
        }
        if (grafik is not null)
        {
            // Welle A 0.70.0 (Weisung P12-220): ist ein Bahnsteig mit einem Bahnhof verknuepft, haelt der Zug an ihm (ohne
            // Verknuepfung wie bisher, Fassung 0 bitgleich)
            Zug(stuecke, grafik, gleise, ZugHaltAlt.Value ? null : VerknuepfterBahnsteig(landschaft, modelle));
        }
        // Phase 13 Welle 4a (Weisung P12-28): Stuecke aus dem Katalog des Stadtbau-Werkzeugs (stadtdaten.katalog) mit den
        // mitgelieferten Modellen; ein Modell versperrt den Weg mit seinem Rechteck, ein Effekt (Stern) nichts. Fehlt ein
        // Modell, zeichnet die Stadt nichts dafuer (die Ladepruefung StadtbauPruefung.Verbaut meldet das vorher rot).
        foreach (var k2 in Katalogstueck.AusStadtdaten(stadt.Daten))
        {
            if (grafik?.Modell(k2.Typ) is not { } km)
            {
                continue;
            }
            stuecke.Add(new ModellStueck(k2.Typ, Katalogstueck.Lage(km, k2.X, k2.Z, k2.W, k2.T, k2.Dreh, 0f, k2.Effekt)));
            if (!k2.Effekt)
            {
                var (x1, z1, x2, z2) = k2.Rechteck;
                hindernisse.Add((x1, z1, x2, z2));
            }
        }
        // Welle A 0.61.0 (Nico 3a): geparkte Autos (je Spieltag gezogen, im Werkzeug alle Plaetze belegt) als Stuecke der Stadt;
        // jeder Stellplatz versperrt den Weg (die Fahrgasse bleibt frei)
        var parkTag = stadt.VerkehrTag ?? stand?.Tag ?? 1;
        foreach (var pp in parkplaetze)
        {
            var nr = 0;
            foreach (var (mitte, blick) in pp.Stellplaetze())
            {
                var quer = Bahnuebergang.Rechts(blick);
                var t = blick * (Probe.Parkplaetze.PlatzTief / 2);
                var q = quer * (Probe.Parkplaetze.PlatzBreit / 2);
                hindernisse.Add(Rechteck([mitte - t - q, mitte + t - q, mitte + t + q, mitte - t + q]));
                if (Probe.Parkplaetze.Belegung(pp.Nr, nr++, parkTag, stadt.ParkplaetzeVoll) is { } auto
                    && Fahrzeugmodelle.Zeichnen(auto) is var am && grafik?.Modell(am) is { } pm)
                {
                    var anker = new Vector3((pm.Min.X + pm.Max.X) / 2, pm.Min.Y, (pm.Min.Z + pm.Max.Z) / 2);
                    stuecke.Add(new ModellStueck(am, Lage(pm, anker, new Vector3(Meter * 1.1f), new Vector3(mitte.X, Probe.Parkplaetze.LinieHoehe, mitte.Y), blick)));
                }
            }
        }
        // Welle A 0.70.0 (Weisung P12-218 Punkt 3, Nico b, „Prellbock wie im Entwurf“): ein frei endendes Gleis aus dem Werkzeug
        // (Feld radien) bekommt einen Prellbock; nicht am Kartenrand, nicht an einem anderen Gleis (Stoss, Weiche), nicht an
        // einem Haus (Abstellgleis im Schuppen). Das Gleis der Stadt 1 (Fassung 0) hat kein Feld radien: dort bleibt alles gleich.
        if (!PrellbockAlt.Value)
        {
            for (var g = 0; g < gleise.Count; g++)
            {
                if (!gleisWerkzeug[g] || gleise[g].Count < 2)
                {
                    continue;
                }
                foreach (var (ende, hinaus) in GleisEnden(gleise[g]))
                {
                    if (ende.X <= PrellbockRand || ende.Y <= PrellbockRand || ende.X >= breite - PrellbockRand || ende.Y >= tiefe - PrellbockRand
                        || gleise.Where((_, i) => i != g).Any(o => Kurve.Abstand(ende, o) <= PrellbockRand)
                        || Vector2.Distance(ende, Vector2.Distance(gleise[g][0], ende) < 1e-4f ? gleise[g][^1] : gleise[g][0]) <= PrellbockRand
                        || haeuser.Any(h => h.Abstand(ende) <= PrellbockHaus))
                    {
                        continue;
                    }
                    foreach (var t in Prellbock(ende, hinaus))
                    {
                        teile.Add(gelaende is null ? t : Anheben(gelaende, t) with { Dreh = t.Dreh });
                    }
                    var quer = new Vector2(-hinaus.Y, hinaus.X) * 0.25f;
                    var vorn = ende - (hinaus * 0.1f);
                    var hinten = ende + (hinaus * 0.16f);
                    hindernisse.Add(Rechteck([vorn - quer, hinten - quer, hinten + quer, vorn + quer]));
                }
            }
        }
        // Hindernisse vor der Fest-Deko: das Gruen haengt so nicht an der heutigen Festzeit
        var hindernisseVorFest = hindernisse.ToList();
        var festTeileVon = teile.Count;
        var festStueckeVon = stuecke.Count;
        // Weisung P8-56 (Nico, Bild 4): auch das Stellwerk (Haus ohne Ort mit Tuer) bekommt die Deko an der Tuer wie die
        // Haeuser der Orte; es steht hinten in der Liste, die Deko der Orte bleibt so an ihrem Platz
        var festOrte = orte.Concat(tueren.Where(t => !t.Modell && orte.All(o => o.Id != t.Ort))
            .Select(t => (Tuer: t, Haus: zierhaeuser.FirstOrDefault(z => t.Mitte.X >= z.KastenMin.X - 0.3f && t.Mitte.X <= z.KastenMax.X + 0.3f
                && t.Mitte.Y >= z.KastenMin.Z - 0.3f && t.Mitte.Y <= z.KastenMax.Z + 0.3f)))
            .Where(p => p.Haus is not null)
            .Select(p => new StadtOrt(p.Tuer.Ort, p.Haus!.Name, p.Haus.Name, "zier", p.Tuer.Mitte.X, p.Tuer.Mitte.Y, p.Haus.KastenMin, p.Haus.KastenMax, p.Haus.Schild)
            {
                StehX = p.Tuer.WegAnfang.X,
                StehZ = p.Tuer.WegAnfang.Y,
            })).ToList();
        var festGesetzt = FestDeko.Stadt(teile, stuecke, grafik, FestDeko.Muster(feste, fest), festOrte, baeume.Sperren, hindernisse, tueren);
        if (fest is not null)
        {
            signatur.Add("fest:" + fest);
        }
        // Phase 8 W7 (Sammelliste 129): Gruen nach der Festzeit-Deko; frei bleiben auch die Plaetze JEDER Festzeit,
        // damit das Gruen an jedem Tag gleich steht und die Deko aus W6 unveraendert bleibt
        Gruen.Ergebnis? gruen = null;
        var festPlaetze = new List<(Vector2 P, float R)>();
        if (grafik is not null)
        {
            foreach (var f in Festzeit.Alle)
            {
                foreach (var g in FestDeko.Stadt([], [], grafik, FestDeko.Muster(feste, f), festOrte, baeume.Sperren, hindernisseVorFest.ToList(), tueren))
                {
                    festPlaetze.Add((new Vector2(g.X, g.Z), Math.Max(g.R, 0.12f)));
                }
            }
            var haeuserKaesten = haeuser.Select(h => (h.Min, h.Max)).ToList();
            // W11 (Weisung P8-47): die Laternenplaetze aus Nicos Skizze bleiben frei von Kleingruen
            var laternenPlaetze = Nachtlicht.Wunschplaetze(strassen, breite, tiefe, laternenSkizze).ToList();
            bool Frei(float x, float z, float r) =>
                baeume.Sperren.All(sp => Kurve.Abstand(new Vector2(x, z), sp.Linie) >= sp.Halb + r + 0.02f)
                && gehwege.All(g => Kurve.Abstand(new Vector2(x, z), g.Linie) >= g.Halb + r + 0.02f)
                && orte.All(o => Vector2.Distance(new Vector2(x, z), new Vector2(o.StehX, o.StehZ)) >= 0.45f + r)
                && haeuserKaesten.All(k => x + r < k.Min.X - 0.03f || x - r > k.Max.X + 0.03f || z + r < k.Min.Z - 0.03f || z - r > k.Max.Z + 0.03f)
                && hindernisseVorFest.All(h => x + r < h.Item1 || x - r > h.Item3 || z + r < h.Item2 || z - r > h.Item4)
                && festPlaetze.All(fp => Vector2.Distance(fp.P, new Vector2(x, z)) >= fp.R + r + 0.05f)
                && laternenPlaetze.All(lp => Kurve.Abstand(new Vector2(x, z), [lp.A, lp.B]) >= Nachtlicht.AbstandModell + r + 0.05f)
                && andreaskreuze.All(k => Vector2.Distance(k.Fuss, new Vector2(x, z)) >= Bahnuebergang.Grundriss + r + 0.05f)
                && teiche.All(t => !Wasser.Drin(t.MitUfer, new Vector2(x, z)) && Wasser.RandAbstand(t.MitUfer, new Vector2(x, z)) >= r + 0.02f)
                // Welle A 0.66.0: Kleingruen nur, wo sein Fuss rundum hoechstens 0,02 von der Hoehe der Mitte abweicht
                && (gelaende is null || gelaende.Flach(x, z, r));
            var parks = landschaft.Where(t => Py.Gleich(t["typ"], "park")).Select(t => (Zahl(t, "x"), Zahl(t, "y"), Zahl(t, "w"), Zahl(t, "h")));
            // die Parkbaeume stehen nicht in den Stuecken (die bleiben wie in W6), sie kommen in NetzBauen dazu
            gruen = Gruen.Verteilen(grafik, breite, tiefe, parks, strassen, Frei, baeume.Gesetzt, beeteFest, KleingruenAn(stadt.Daten));
            // Bueschel, Steine, Beete und Parkbaeume sind Hindernisse des freien Gehens (wie die Fest-Deko, E2 P8-14);
            // ueber Gras geht die Figur
            hindernisse.AddRange(gruen.Hindernisse.Select(h => ((double)(h.X - h.R), (double)(h.Z - h.R), (double)(h.X + h.R), (double)(h.Z + h.R))));
            if (gelaende is not null)
            {
                Anheben(gelaende, gruen.Stuecke, 0, gruen.Stuecke.Count);
                Anheben(gelaende, gruen.Baeume, 0, gruen.Baeume.Count);
                for (var i = 0; i < gruen.Teile.Count; i++)
                {
                    gruen.Teile[i] = Anheben(gelaende, gruen.Teile[i]);
                }
            }
        }
        // Welle A 0.66.0 (Weisung P12-192): jedes Stueck ausserhalb der Haeuser (Baum, Katalog-Stueck, Effekt) steht auf der
        // Hoehe des Gelaendes an seiner Mitte und kippt nicht; ohne Gelaende nichts
        if (gelaende is not null)
        {
            var imHaus = new bool[stuecke.Count];
            foreach (var h in haeuser)
            {
                Array.Fill(imHaus, true, h.StueckeVon, Math.Min(h.StueckeBis, stuecke.Count) - h.StueckeVon);
            }
            for (var i = 0; i < stuecke.Count; i++)
            {
                if (!imHaus[i])
                {
                    Anheben(gelaende, stuecke, i, i + 1);
                }
            }
        }
        // Sammelliste 264 (Weisung P12-130 Frage 3 = c): Gebaeude faerben (stadtdaten.farben, ohne Feld nichts)
        if (grafik is not null)
        {
            FarbenAnwenden(stadt.Daten, grafik, stuecke, haeuser, umgefaerbt);
        }
        var laufraster = LaufrasterBauen(breite, tiefe, hindernisse, wasser, gleise, strassen, zugangswege, orte, gehwege, teiche, stege, inseln, [.. zebras, .. furten], gelaende);
        return new StadtSzene(teile, streifen, stuecke, grafik, orte, string.Join(";", signatur), breite, tiefe, laufraster, haeuser)
        {
            _vorrat = vorrat,
            Bruecken = bruecken,
            BrueckenKaesten = brueckenKaesten,
            StrassenEnden = strassenEnden,
            Baeume = baeume.Gesetzt,
            BaumSperren = baeume.Sperren,
            Uebergaenge = uebergaenge,
            Andreaskreuze = andreaskreuze,
            Gleise = gleise,
            Fest = fest,
            FestGesetzt = festGesetzt,
            FestPlaetzeAlle = festPlaetze,
            FestTeileVon = festTeileVon,
            FestStueckeVon = festStueckeVon,
            Jahreszeit = jahreszeit,
            Spieltag = stadt.VerkehrTag ?? stand?.Tag ?? 1,
            GruenKlein = gruen,
            Strassen = strassen,
            LaternenSkizze = laternenSkizze,
            // W10 (Nico 3b): Fenster mit Licht (nicht die dunklen und nicht die an der Tuer in Wandfarbe)
            // W11: dazu das Zifferblatt der Bahnhofsuhr
            LichtTeile = alleFenster.Where(f => teile[f.Teil].Farbe != Farben.Fenster && teile[f.Teil].Farbe != f.Wand).Select(f => f.Teil)
                .Concat(Enumerable.Range(0, teile.Count).Where(i => teile[i].Farbe == UhrBlatt || teile[i].Farbe == AnzeigeSchrift)).ToHashSet(),
            Zierhaeuser = zierhaeuser,
            Tueren = tueren,
            OrtBloecke = ortBloecke,
            Umgefaerbt = umgefaerbt,
            Parks = parkFlaechen,
            Fluesse = [.. wasser],
            Teiche = teiche,
            Stege = stege,
            Flaechen = flaechen,
            Eis = Wasser.EisAn(stadt.Daten),
            Zebras = zebras,
            Ampeln = ampeln,
            Haltestellen = haltestellen,
            Questgeber = Stadtbau.Questebene.DraussenListe(inhalt),
            Parkplaetze = parkplaetze,
            MitGehweg = gehwege.Count > 0,
            Gelaende = gelaende,
            LaternenOhnePlatzAuslassen = Stadtbau.Nachsicht.An,
        };
    }

    /// <summary>Phase 13 Welle 4a: Breite des Gehwegs je Seite einer Strasse (Feld "gehweg", fehlt = 0).</summary>
    public static float GehwegBreite(PyObjekt strasse) => Py.Hat(strasse, "gehweg") ? (float)Py.F(strasse["gehweg"]) : 0f;

    /// <summary>
    /// Welle K1 (A 0.44.0, Weisung P12-55): Wunschradius je Eckpunkt eines Flusses oder Gleises (Feld "radien", gleich viele
    /// Zahlen wie Punkte, nur aus dem Stadtbau-Werkzeug); ohne Feld null (dann gilt <see cref="Kurve.Radius"/> wie bisher).
    /// </summary>
    public static List<float>? Radien(PyObjekt linie) =>
        Py.Hat(linie, "radien") && linie["radien"] is List<object?> r ? [.. r.Select(w => (float)Py.F(w))] : null;

    /// <summary>Welle K1: der geglaettete Linienzug eines Flusses oder Gleises (Boegen mit dem Radius je Eckpunkt).</summary>
    public static List<Vector2> Linienzug(PyObjekt linie) => Kurve.Glatt(Vektoren(linie["punkte"]), Radien(linie));

    /// <summary>
    /// Welle K1: eine Strasse mit mehr als zwei Punkten (Kurve aus dem Stadtbau-Werkzeug, Feld "kurve") zeichnet das Spiel
    /// als ein Band mit Gehrung (wie Fluss und Gleis), damit zwischen den kurzen Stuecken kein Spalt bleibt; eine Strasse
    /// mit zwei Punkten (alle Strassen der Fassung 0) bleibt ein gerades Band wie bisher.
    /// </summary>
    public static bool AlsBand(IReadOnlyList<Vector2> strasse) => strasse.Count > 2;

    /// <summary>
    /// Welle K1: so weit reicht eine stumpf angesetzte Kurve in die anschliessende Strasse (auf gleicher Hoehe und in
    /// gleicher Farbe). Mehr als der Versatz der Gehrung am ersten Punkt (halbe Breite samt Gehweg mal tan 2,5 Grad, unter
    /// 0,04), sonst ueberschlaegt sich das erste Viereck.
    /// </summary>
    public const float StumpfZugabe = 0.05f;

    /// <summary>So weit duerfen die Richtungen zweier aneinander stossender Strassen abweichen (Grad, <see cref="Anschluss"/>).</summary>
    public const float AnschlussGrad = 8f;

    /// <summary>
    /// Welle K1: trifft ein Strassenende genau das Ende einer anderen Strasse in gleicher Richtung (Kurve an gerade Strasse,
    /// so setzt das Werkzeug sie), stossen beide Baender stumpf aneinander (keine Verlaengerung um die halbe Breite, die
    /// sonst als Ecke ueber den Bogen ragt). Nur an Kurven (mehr als zwei Punkte auf einer der beiden Seiten).
    /// </summary>
    /// <param name="richtung">Richtung aus der eigenen Strasse heraus (Laenge 1).</param>
    public static bool Fortgesetzt(Vector2 ende, Vector2 richtung, IReadOnlyList<Vector2> eigene, IEnumerable<List<Vector2>> alle) =>
        Weiter(ende, richtung, eigene, alle) is not null;

    /// <summary>
    /// Die Richtung, in der die anschliessende Strasse vom gemeinsamen Ende weg laeuft (<see cref="Fortgesetzt"/>), sonst
    /// null. Erlaubt sind bis <see cref="AnschlussGrad"/> Grad Unterschied: das erste und das letzte Stueck einer Kurve
    /// sind Sehnen von 7,5 Grad und weichen um die Haelfte von der Tangente ab; treffen zwei Kurven mit gleicher
    /// Drehrichtung aufeinander, liegen ihre Sehnen am Stoss 7,5 Grad auseinander (Welle K3, vorher 5 Grad: dort fehlte
    /// der Anschluss, die Baender hatten Endkappen und die Mittellinie eine Luecke).
    /// </summary>
    public static Vector2? Weiter(Vector2 ende, Vector2 richtung, IReadOnlyList<Vector2> eigene, IEnumerable<List<Vector2>> alle) =>
        Anschluss(ende, richtung, eigene, [.. alle]) is { } t ? t.Richtung : null;

    /// <summary>
    /// Welle K3: wie <see cref="Weiter"/>, dazu welche Strasse (Stelle in <paramref name="alle"/>) und ob sie mit ihrem
    /// Anfang (true) oder ihrem Ende anschliesst. Gleiche Regeln, darum gibt <see cref="Weiter"/> genau diese Richtung.
    /// </summary>
    public static (int Strasse, bool AmAnfang, Vector2 Richtung)? Anschluss(Vector2 ende, Vector2 richtung, IReadOnlyList<Vector2> eigene,
        IReadOnlyList<List<Vector2>> alle, Func<int, bool>? erlaubt = null)
    {
        for (var k = 0; k < alle.Count; k++)
        {
            var a = alle[k];
            if (a.Count < 2 || ReferenceEquals(a, eigene) || a.SequenceEqual(eigene) || (!AlsBand(a) && !AlsBand(eigene))
                || (erlaubt is not null && !erlaubt(k)))
            {
                continue;
            }
            foreach (var (p, weiter, anfang) in new[] { (a[0], a[1] - a[0], true), (a[^1], a[^2] - a[^1], false) })
            {
                if (Vector2.Distance(p, ende) <= 0.02f && weiter.LengthSquared() > 1e-10f
                    && Vector2.Dot(Vector2.Normalize(weiter), richtung) >= MathF.Cos(AnschlussGrad * MathF.PI / 180))
                {
                    return (k, anfang, Vector2.Normalize(weiter));
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Die Striche der Mittellinie aller Strassen (Reihenfolge wie gezeichnet). Kein Strich, dessen Mitte bis zur halben
    /// Strassenbreite + 0,05 an einer Strasse ausserhalb der eigenen Kette liegt (sonst lag auf Kreuzungen ein "+").
    /// <paramref name="ohneKette"/> = Stand vor K3 (jede Strasse eigener Takt), nur fuer die Gegenprobe in Tests.
    /// </summary>
    public static List<(float X1, float Z1, float X2, float Z2)> MittellinienStriche(IReadOnlyList<List<Vector2>> strassen, bool ohneKette = false)
    {
        var ergebnis = new List<(float, float, float, float)>();
        foreach (var zug in MittellinienZuege(strassen, ohneKette))
        {
            var eigene = zug.Strassen.Select(k => strassen[k]).ToList();
            var andere = strassen.Where((s2, k) => !zug.Strassen.Contains(k) && !eigene.Any(e2 => s2.SequenceEqual(e2))).ToList();
            foreach (var (x1, z1, x2, z2) in Kreisverkehr.Geschlossen([.. zug.Linie.Select(q => new Vector2(q.X, q.Z))])
                ? RingStriche(zug.Linie) : Striche(zug.Linie, 0.2f, 0.25f))
            {
                var mitte = new Vector2((x1 + x2) / 2, (z1 + z2) / 2);
                if (andere.Any(a => Kurve.Abstand(mitte, a) <= (StrassenBreite / 2) + 0.05f))
                {
                    continue;
                }
                ergebnis.Add((x1, z1, x2, z2));
            }
        }
        return ergebnis;
    }

    /// <summary>
    /// Welle K3 (A 0.45.0, Sammelliste 231): die Linienzuege der Mittellinie. Strassen, die nach <see cref="Anschluss"/>
    /// aneinander anschliessen, bilden eine Kette in Reihenfolge und Richtung (gemeinsamer Punkt nur einmal); jede Kette
    /// kommt an der Stelle ihrer ersten Strasse (Reihenfolge der Landschaft). Eine Strasse ohne Anschluss ist eine Kette
    /// mit ihren eigenen Punkten, unveraendert (Fassung 0 hat nur solche, darum bytegleich).
    /// </summary>
    public static List<(List<int> Strassen, List<(float X, float Z)> Linie)> MittellinienZuege(IReadOnlyList<List<Vector2>> strassen, bool ohneKette = false)
    {
        var ergebnis = new List<(List<int>, List<(float X, float Z)>)>();
        var vergeben = new bool[strassen.Count];
        for (var i = 0; i < strassen.Count; i++)
        {
            if (vergeben[i])
            {
                continue;
            }
            vergeben[i] = true;
            // Kette als Liste (Strasse, umgedreht)
            var kette = new LinkedList<(int K, bool Um)>();
            kette.AddFirst((i, false));
            var s0 = strassen[i];
            if (!ohneKette && s0.Count >= 2)
            {
                // nach vorn: vom Ende der letzten Strasse weiter
                while (true)
                {
                    var (k, um) = kette.Last!.Value;
                    var p = Ausgerichtet(strassen[k], um);
                    if (p.Count < 2 || Vector2.DistanceSquared(p[^1], p[^2]) < 1e-12f
                        || Anschluss(p[^1], Vector2.Normalize(p[^1] - p[^2]), strassen[k], strassen, j => !vergeben[j]) is not { } t)
                    {
                        break;
                    }
                    vergeben[t.Strasse] = true;
                    kette.AddLast((t.Strasse, !t.AmAnfang));
                }
                // nach hinten: vom Anfang der ersten Strasse zurueck
                while (true)
                {
                    var (k, um) = kette.First!.Value;
                    var p = Ausgerichtet(strassen[k], um);
                    if (p.Count < 2 || Vector2.DistanceSquared(p[0], p[1]) < 1e-12f
                        || Anschluss(p[0], Vector2.Normalize(p[0] - p[1]), strassen[k], strassen, j => !vergeben[j]) is not { } t)
                    {
                        break;
                    }
                    vergeben[t.Strasse] = true;
                    // schliesst mit dem Ende an: in eigener Richtung davor; mit dem Anfang: umgedreht davor
                    kette.AddFirst((t.Strasse, t.AmAnfang));
                }
            }
            if (kette.Count == 1)
            {
                ergebnis.Add(([i], [.. s0.Select(q => (q.X, q.Y))]));
                continue;
            }
            var linie = new List<(float X, float Z)>();
            foreach (var (k, um) in kette)
            {
                var p = Ausgerichtet(strassen[k], um);
                // der gemeinsame Punkt (bis 0,02 auseinander) nur einmal
                linie.AddRange(p.Skip(linie.Count == 0 ? 0 : 1).Select(q => (q.X, q.Y)));
            }
            ergebnis.Add(([.. kette.Select(e => e.K)], linie));
        }
        return ergebnis;
    }

    /// <summary>
    /// Welle K3: Striche auf einem geschlossenen Ring im Takt 0,45 (Luecke so angepasst, dass eine ganze Zahl Striche
    /// rundum passt; sonst laege am Stoss eine kurze Luecke oder zwei Striche aneinander).
    /// </summary>
    public static List<(float X1, float Z1, float X2, float Z2)> RingStriche(List<(float X, float Z)> ring)
    {
        var laenge = 0f;
        for (var i = 0; i + 1 < ring.Count; i++)
        {
            laenge += MathF.Sqrt(((ring[i + 1].X - ring[i].X) * (ring[i + 1].X - ring[i].X)) + ((ring[i + 1].Z - ring[i].Z) * (ring[i + 1].Z - ring[i].Z)));
        }
        var anzahl = Math.Max(1, (int)MathF.Round(laenge / 0.45f));
        return [.. Striche(ring, 0.2f, (laenge / anzahl) - 0.2f).Take(anzahl)];
    }

    private static List<Vector2> Ausgerichtet(List<Vector2> p, bool umgedreht) => umgedreht ? [.. Enumerable.Reverse(p)] : p;

    internal static List<Vector2> Vektoren(object? punkte) => [.. Punkte(punkte).Select(p => new Vector2(p.X, p.Z))];

    /// <summary>Stehplatz vor der Tuer: 0,3 den Fussweg (Zugang aus stadt.json) entlang, hoechstens bis zu seinem ersten Knick.</summary>
    private static (float X, float Z) Stehplatz(IStadtSicht stadt, string id, float tx, float tz)
    {
        if (Get(stadt.Zugaenge, id) is not PyObjekt zugang)
        {
            return (tx, tz);
        }
        var weg = Vektoren(zugang["weg"]);
        if (weg.Count < 2)
        {
            return (tx, tz);
        }
        var d = weg[1] - weg[0];
        var l = d.Length();
        if (l < 1e-4f)
        {
            return (tx, tz);
        }
        var p = weg[0] + (d / l * MathF.Min(0.3f, l));
        return (p.X, p.Y);
    }

    /// <summary>
    /// Laufraster des freien Gehens: frei ist eine Zelle, deren Mitte <see cref="LaufKoerper"/> von jedem
    /// Haus (Bloecke der Modelle) weg ist und nicht im Wasser oder auf dem Gleis liegt; Strassen (auch
    /// ueber Fluss und Gleis), Fusswege und die Stehplaetze vor den Tueren sind immer frei.
    /// </summary>
    private static Wegsuche LaufrasterBauen(float breite, float tiefe, List<(double, double, double, double)> hindernisse,
        List<List<Vector2>> wasser, List<List<Vector2>> gleise, List<List<Vector2>> strassen, List<List<Vector2>> wege, List<StadtOrt> orte,
        List<(List<Vector2> Linie, float Halb)>? gehwege = null, List<Teich>? teiche = null, List<Steg>? stege = null, List<List<Vector2>>? inseln = null,
        List<Zebra>? zebras = null, Gelaende? gelaende = null)
    {
        var spalten = (int)(breite / LaufSchritt);
        var zeilen = (int)(tiefe / LaufSchritt);
        var grenze = LaufKoerper * LaufKoerper;
        // Phase 13 Welle 3 (Weisung P12-21): gleiches Ergebnis wie die Pruefung jeder Zelle gegen jede Linie und jedes
        // Hindernis, aber jede Linie und jedes Hindernis prueft nur die Zellen in seinem Umkreis (die Bedingungen je Zelle
        // sind dieselben Ausdruecke; ausserhalb des Umkreises kann keine zutreffen)
        Vector2 Mitte(int s, int z) => new((float)((s + 0.5) * LaufSchritt), (float)((z + 0.5) * LaufSchritt));
        (int Von, int Bis) Bereich(double min, double max, int anzahl) =>
            (Math.Max(0, (int)Math.Floor((min / LaufSchritt) - 0.5) - 1), Math.Min(anzahl - 1, (int)Math.Ceiling((max / LaufSchritt) - 0.5) + 1));
        var immer = new bool[spalten * zeilen];
        var gesperrt = new bool[spalten * zeilen];
        void Linien(List<List<Vector2>> linien, float weite, bool[] ziel)
        {
            foreach (var l in linien)
            {
                if (l.Count == 0)
                {
                    continue;
                }
                float x1 = l.Min(p => p.X), x2 = l.Max(p => p.X), z1 = l.Min(p => p.Y), z2 = l.Max(p => p.Y);
                var (s0, s1) = Bereich(x1 - weite, x2 + weite, spalten);
                var (r0, r1) = Bereich(z1 - weite, z2 + weite, zeilen);
                for (var s = s0; s <= s1; s++)
                {
                    for (var z = r0; z <= r1; z++)
                    {
                        if (!ziel[(s * zeilen) + z] && Kurve.Abstand(Mitte(s, z), l) <= weite)
                        {
                            ziel[(s * zeilen) + z] = true;
                        }
                    }
                }
            }
        }
        Linien(strassen, StrassenBreite / 2, immer);
        foreach (var (linie, halb) in gehwege ?? [])
        {
            Linien([linie], halb, immer);
        }
        Linien(wege, 0.25f, immer);
        foreach (var o in orte)
        {
            var steh = new Vector2(o.StehX, o.StehZ);
            var (s0, s1) = Bereich(o.StehX - 0.25, o.StehX + 0.25, spalten);
            var (r0, r1) = Bereich(o.StehZ - 0.25, o.StehZ + 0.25, zeilen);
            for (var s = s0; s <= s1; s++)
            {
                for (var z = r0; z <= r1; z++)
                {
                    if (Vector2.Distance(Mitte(s, z), steh) <= 0.25f)
                    {
                        immer[(s * zeilen) + z] = true;
                    }
                }
            }
        }
        Linien(wasser, (FlussBreite / 2) + 0.1f, gesperrt);
        Linien(gleise, (GleisBreite / 2) + 0.1f, gesperrt);
        // S 0.8.0 (Weisung P12-47): Teich und Ufer gesperrt, Stege frei (laengs bis eine halbe Zelle ueber die Enden,
        // damit der Steg an das Land anschliesst)
        foreach (var teich in teiche ?? [])
        {
            var u = teich.MitUfer;
            var (s0, s1) = Bereich(u.Min(p => p.X), u.Max(p => p.X), spalten);
            var (r0, r1) = Bereich(u.Min(p => p.Y), u.Max(p => p.Y), zeilen);
            for (var s = s0; s <= s1; s++)
            {
                for (var z = r0; z <= r1; z++)
                {
                    if (Wasser.Drin(u, Mitte(s, z)))
                    {
                        gesperrt[(s * zeilen) + z] = true;
                    }
                }
            }
        }
        foreach (var steg in stege ?? [])
        {
            var (x1, z1, x2, z2) = steg.Rechteck;
            var zug = (float)(LaufSchritt / 2);
            if (steg.Quer)
            {
                (z1, z2) = (z1 - zug, z2 + zug);
            }
            else
            {
                (x1, x2) = (x1 - zug, x2 + zug);
            }
            var (s0, s1) = Bereich(x1, x2, spalten);
            var (r0, r1) = Bereich(z1, z2, zeilen);
            for (var s = s0; s <= s1; s++)
            {
                for (var z = r0; z <= r1; z++)
                {
                    var c = Mitte(s, z);
                    if (c.X >= x1 && c.X <= x2 && c.Y >= z1 && c.Y <= z2)
                    {
                        immer[(s * zeilen) + z] = true;
                    }
                }
            }
        }
        foreach (var (x1, z1, x2, z2) in hindernisse)
        {
            var (s0, s1) = Bereich(Math.Min(x1, x2) - LaufKoerper, Math.Max(x1, x2) + LaufKoerper, spalten);
            var (r0, r1) = Bereich(Math.Min(z1, z2) - LaufKoerper, Math.Max(z1, z2) + LaufKoerper, zeilen);
            for (var s = s0; s <= s1; s++)
            {
                for (var z = r0; z <= r1; z++)
                {
                    var c = Mitte(s, z);
                    var dx = Math.Max(Math.Max(x1 - c.X, 0), c.X - x2);
                    var dz = Math.Max(Math.Max(z1 - c.Y, 0), c.Y - z2);
                    if ((dx * dx) + (dz * dz) < grenze)
                    {
                        gesperrt[(s * zeilen) + z] = true;
                    }
                }
            }
        }
        // Welle K3: die Mittelinsel eines Kreisverkehrs ist nie begehbar (auch nicht der innere Gehweg-Rand des Rings),
        // die Figur geht aussen herum
        foreach (var insel in inseln ?? [])
        {
            var (s0, s1) = Bereich(insel.Min(p => p.X), insel.Max(p => p.X), spalten);
            var (r0, r1) = Bereich(insel.Min(p => p.Y), insel.Max(p => p.Y), zeilen);
            for (var s = s0; s <= s1; s++)
            {
                for (var z = r0; z <= r1; z++)
                {
                    if (Wasser.Drin(insel, Mitte(s, z)))
                    {
                        immer[(s * zeilen) + z] = false;
                        gesperrt[(s * zeilen) + z] = true;
                    }
                }
            }
        }
        // Welle A 0.66.0 (Weisung P12-192, E1): ein Hang steiler als 45 Grad ist gesperrt (auch gegen "immer frei"); ohne
        // Gelaende nichts
        if (gelaende is not null)
        {
            foreach (var (gs, gz) in gelaende.Zellen((float)LaufSchritt, spalten, zeilen))
            {
                var m = Mitte(gs, gz);
                if (gelaende.ZuSteil(m.X, m.Y))
                {
                    immer[(gs * zeilen) + gz] = false;
                    gesperrt[(gs * zeilen) + gz] = true;
                }
            }
        }
        // Welle A 0.54.0 (Weisung P12-115, Nico P12-75 Fragen 3 = a, 4 = a): auf einer Strasse mit Gehweg geht die Figur
        // nur auf dem Gehweg; die Fahrbahn ist gesperrt (auch gegen "immer frei"), ausser auf einem Zebrastreifen (quer bis
        // an die Gehwege). Strassen ohne Gehweg (Fassung 0, alte Projekte) bleiben begehbar wie bisher.
        var fahrbahn = new bool[spalten * zeilen];
        if (FahrbahnFreiAltFuerTests is false)
        {
            Linien([.. (gehwege ?? []).Select(g => g.Linie)], StrassenBreite / 2, fahrbahn);
        }
        foreach (var zebra in zebras ?? [])
        {
            var weite = (StrassenBreite / 2) + 0.2f;
            var (s0, s1) = Bereich(zebra.Mitte.X - weite, zebra.Mitte.X + weite, spalten);
            var (r0, r1) = Bereich(zebra.Mitte.Y - weite, zebra.Mitte.Y + weite, zeilen);
            for (var s = s0; s <= s1; s++)
            {
                for (var z = r0; z <= r1; z++)
                {
                    if (zebra.Drauf(Mitte(s, z), 0f, 0.2f))
                    {
                        fahrbahn[(s * zeilen) + z] = false;
                        immer[(s * zeilen) + z] = true;
                    }
                }
            }
        }
        var frei = new List<(int, int)>();
        for (var s = 0; s < spalten; s++)
        {
            for (var z = 0; z < zeilen; z++)
            {
                if (fahrbahn[(s * zeilen) + z])
                {
                    continue;
                }
                if (immer[(s * zeilen) + z] || !gesperrt[(s * zeilen) + z])
                {
                    frei.Add((s, z));
                }
            }
        }
        return new Wegsuche(frei, LaufSchritt);
    }

    /// <summary>Gegenprobe Welle A 0.54.0: Laufraster wie bis A 0.53.2 (Fahrbahn jeder Strasse frei zum Gehen).</summary>
    public static bool FahrbahnFreiAltFuerTests
    {
        get => FahrbahnAlt.Value;
        set => FahrbahnAlt.Value = value;
    }

    private static readonly AsyncLocal<bool> FahrbahnAlt = new();

    /// <summary>Gegenprobe Welle A 0.55.0: Teil „schild“ wie bis A 0.54.0 zeichnen (Platte in der Farbe der Rolle).</summary>
    public static bool SchildAltFuerTests
    {
        get => SchildAlt.Value;
        set => SchildAlt.Value = value;
    }

    private static readonly AsyncLocal<bool> SchildAlt = new();

    /// <summary>Gegenprobe Sammelliste 315: Teil „kreis“ auch ganz ueber dem Block zeichnen (wie bis A 0.68.0, nur Tests, je Ablauf).</summary>
    public static bool KreisAltFuerTests { get => KreisAlt.Value; set => KreisAlt.Value = value; }
    private static readonly AsyncLocal<bool> KreisAlt = new();

    /// <summary>Gegenprobe Weisung P12-218 Punkt 1: Feld „windfang“ nicht beachten (wie bis A 0.69.0, nur Tests, je Ablauf).</summary>
    public static bool WindfangAltFuerTests { get => WindfangAlt.Value; set => WindfangAlt.Value = value; }
    private static readonly AsyncLocal<bool> WindfangAlt = new();

    /// <summary>Gegenprobe Welle A 0.70.0: Tuer eines Zierwegs immer auf dem Kasten des Hauses (wie bis A 0.69.0, nur Tests, je Ablauf).</summary>
    public static bool ZierKastenAltFuerTests { get => ZierKastenAlt.Value; set => ZierKastenAlt.Value = value; }
    private static readonly AsyncLocal<bool> ZierKastenAlt = new();

    /// <summary>Punkt liegt mehr als <see cref="ZierInnen"/> innerhalb aller vier Waende des Kastens.</summary>
    private static bool ImKastenInnen(Vector3 min, Vector3 max, Vector2 p) =>
        p.X > min.X + ZierInnen && p.X < max.X - ZierInnen && p.Y > min.Z + ZierInnen && p.Y < max.Z - ZierInnen;

    /// <summary>Abstand zur Wand des Kastens, ab dem eine Zierweg-Tuer als „innen“ gilt.</summary>
    public const float ZierInnen = 0.05f;

    /// <summary>Gegenprobe Sammelliste 312: Feld <c>modell_tuer</c> eines Zierwegs nicht beachten (wie bis A 0.68.0, nur Tests, je Ablauf).</summary>
    public static bool ZierModellTuerAltFuerTests { get => ZierModellTuerAlt.Value; set => ZierModellTuerAlt.Value = value; }
    private static readonly AsyncLocal<bool> ZierModellTuerAlt = new();

    /// <summary>Gegenprobe Sammelliste 292: Rueckseite eines Gebaeudes ohne Ort bis zur Vorderwand absuchen (wie bis A 0.65.0, nur Tests, je Ablauf).</summary>
    public static bool RueckseiteAltFuerTests { get => RueckseiteAlt.Value; set => RueckseiteAlt.Value = value; }
    private static readonly AsyncLocal<bool> RueckseiteAlt = new();

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

    /// <summary>Welle A 0.70.0: ein Gleisende so nah am Kartenrand oder an einem anderen Gleis bekommt keinen Prellbock.</summary>
    public const float PrellbockRand = 0.05f;

    /// <summary>Welle A 0.70.0: ein Gleisende so nah an einem Haus (Abstellgleis bis ins Tor) bekommt keinen Prellbock.</summary>
    public const float PrellbockHaus = 0.1f;

    /// <summary>Welle A 0.70.0: Rot des Prellbocks (Pufferbohle).</summary>
    public static readonly Vector3 PrellbockRot = Netz.Farbe("#B3261E");

    /// <summary>Gegenprobe Weisung P12-218 Punkt 3: kein Prellbock (wie bis A 0.69.0, nur Tests, je Ablauf).</summary>
    public static bool PrellbockAltFuerTests { get => PrellbockAlt.Value; set => PrellbockAlt.Value = value; }
    private static readonly AsyncLocal<bool> PrellbockAlt = new();

    /// <summary>Beide Enden eines Gleises mit der Richtung aus dem Gleis hinaus.</summary>
    private static IEnumerable<(Vector2 Ende, Vector2 Hinaus)> GleisEnden(List<Vector2> p)
    {
        var a = p.FindIndex(q => Vector2.DistanceSquared(q, p[0]) > 1e-8f);
        var b = p.FindLastIndex(q => Vector2.DistanceSquared(q, p[^1]) > 1e-8f);
        if (a < 0 || b < 0)
        {
            yield break;
        }
        yield return (p[0], Vector2.Normalize(p[0] - p[a]));
        yield return (p[^1], Vector2.Normalize(p[^1] - p[b]));
    }

    /// <summary>
    /// Welle A 0.70.0: Prellbock am Gleisende (Pufferbohle rot-weiss quer ueber beiden Schienen, dahinter zwei Stuetzen aus
    /// Stahl); im Rahmen x = quer, z = laengs hinaus, gedreht um das Ende (<see cref="Teil.Dreh"/>).
    /// </summary>
    public static IEnumerable<Teil> Prellbock(Vector2 ende, Vector2 hinaus)
    {
        var grad = (float)(Math.Atan2(-hinaus.X, hinaus.Y) * 180 / Math.PI);
        var dreh = new Vector3(ende.X, ende.Y, grad);
        Teil T(float x1, float y1, float z1, float x2, float y2, float z2, Vector3 farbe) =>
            new(TeilArt.Quader, ende.X + x1, y1, ende.Y + z1, ende.X + x2, y2, ende.Y + z2, farbe) { Dreh = dreh };
        var unten = Hoehe.Schiene;
        // Pufferbohle: aussen rot, in der Mitte weiss
        yield return T(-0.24f, unten + 0.08f, -0.06f, -0.08f, unten + 0.17f, 0f, PrellbockRot);
        yield return T(-0.08f, unten + 0.08f, -0.06f, 0.08f, unten + 0.17f, 0f, Weiss);
        yield return T(0.08f, unten + 0.08f, -0.06f, 0.24f, unten + 0.17f, 0f, PrellbockRot);
        // Stuetzen ueber den Schienen, schraeg nach hinten als Stufe angedeutet
        foreach (var x in (float[])[-SpurHalb, SpurHalb])
        {
            yield return T(x - 0.025f, Hoehe.Gleisbett, 0f, x + 0.025f, unten + 0.17f, 0.05f, Farben.Stahl);
            yield return T(x - 0.025f, Hoehe.Gleisbett, 0.05f, x + 0.025f, unten + 0.1f, 0.12f, Farben.Stahl);
        }
    }

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

    /// <summary>
    /// Gleis als glatte Baender (Nachtrag 1, Wunsch 1): Schotterbett, Schwellen alle 0,14 quer zur
    /// Fahrtrichtung, zwei Schienen im festen Abstand; die Boegen kommen aus <see cref="Kurve.Glatt"/>,
    /// darum ohne Knick und Versatz. Bahnuebergaenge (Wunsch Nico 25.10.2026): wo eine Strasse oder ein
    /// Fussweg das Gleis kreuzt, liegt ein Belag in Richtung der Strasse (so breit wie sie) ueber dem
    /// Schotter, die Schienen schauen heraus; an Strassen stehen davor Warnkreuze.
    /// </summary>
    private static List<(Vector2 Mitte, bool Strasse)> Gleis(List<Teil> teile, List<Streifen> streifen, List<Bahnuebergang.Kreuz> andreaskreuze, List<Vector2> p, List<List<Vector2>> strassen, List<List<Vector2>> wege)
    {
        streifen.Add(new Streifen(p, GleisBreite, 0.004f, Hoehe.Gleisbett, Farben.Gleisbett));
        var uebergaenge = new List<(Vector2 Mitte, Vector2 Richtung, float Breite)>();
        foreach (var (linien, breite, farbe, kreuze) in (IEnumerable<(List<List<Vector2>>, float, Vector3, bool)>)
            [(strassen, StrassenBreite, Farben.Strasse, true), (wege, WegBreite, Mischen(Farben.Strasse, Weiss, 0.45f), false)])
        {
            foreach (var linie in linien)
            {
                foreach (var (mitte, richtung, sinus) in Kreuzungen(p, linie))
                {
                    Uebergang(streifen, andreaskreuze, p, mitte, richtung, sinus, breite, farbe, kreuze);
                    uebergaenge.Add((mitte, richtung, breite));
                }
            }
        }
        bool AufUebergang(Vector2 q) => uebergaenge.Any(u =>
        {
            var d = q - u.Mitte;
            var quer = new Vector2(-u.Richtung.Y, u.Richtung.X);
            return MathF.Abs(Vector2.Dot(d, quer)) <= (u.Breite / 2) + 0.02f && MathF.Abs(Vector2.Dot(d, u.Richtung)) <= 0.6f;
        });
        foreach (var (ort, richtung) in Kurve.Entlang(p, 0.14f, 0.07f))
        {
            if (AufUebergang(ort))
            {
                continue;
            }
            var quer = new Vector2(-richtung.Y, richtung.X) * 0.17f;
            streifen.Add(new Streifen([ort - quer, ort + quer], 0.06f, Hoehe.Gleisbett, Hoehe.Schwelle, Farben.Schwelle));
        }
        foreach (var seite in (float[])[-SpurHalb, SpurHalb])
        {
            streifen.Add(new Streifen(Kurve.Versetzt(p, seite), 0.03f, Hoehe.Schwelle, Hoehe.Schiene, Farben.Stahl));
        }
        return [.. uebergaenge.Select(u => (u.Mitte, u.Breite == StrassenBreite))];
    }

    /// <summary>
    /// Schnittpunkte der Linie <paramref name="b"/> (Strasse, Weg) mit <paramref name="a"/> (Gleis, Fluss):
    /// Ort, Richtung von <paramref name="b"/> dort und |sin| des Winkels zwischen beiden.
    /// </summary>
    public static List<(Vector2 Mitte, Vector2 Richtung, float Sinus)> Kreuzungen(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
    {
        var liste = new List<(Vector2, Vector2, float)>();
        for (var i = 0; i + 1 < a.Count; i++)
        {
            for (var j = 0; j + 1 < b.Count; j++)
            {
                var r = a[i + 1] - a[i];
                var s = b[j + 1] - b[j];
                var nenner = (r.X * s.Y) - (r.Y * s.X);
                if (MathF.Abs(nenner) < 1e-9f)
                {
                    continue;
                }
                var q = b[j] - a[i];
                var t = ((q.X * s.Y) - (q.Y * s.X)) / nenner;
                var u = ((q.X * r.Y) - (q.Y * r.X)) / nenner;
                if (t < 0 || t > 1 || u < 0 || u > 1)
                {
                    continue;
                }
                var mitte = a[i] + (r * t);
                // dieselbe Kreuzung nicht doppelt (Schnitt genau auf einer Ecke)
                if (liste.Any(k => Vector2.Distance(k.Item1, mitte) < 0.05f))
                {
                    continue;
                }
                var rn = Vector2.Normalize(r);
                var sn = Vector2.Normalize(s);
                liste.Add((mitte, sn, MathF.Max(0.2f, MathF.Abs((rn.X * sn.Y) - (rn.Y * sn.X)))));
            }
        }
        return liste;
    }

    /// <summary>Bahnuebergang: Belag in Richtung der Strasse ueber dem Gleisbett, an Strassen Warnkreuze davor.</summary>
    private static void Uebergang(List<Streifen> streifen, List<Bahnuebergang.Kreuz> kreuze, IReadOnlyList<Vector2> gleis, Vector2 mitte, Vector2 richtung, float sinus, float breite, Vector3 farbe, bool mitKreuzen)
    {
        var halb = ((GleisBreite / 2) + 0.12f) / sinus;
        streifen.Add(new Streifen([mitte - (richtung * halb), mitte + (richtung * halb)], breite, Hoehe.Gleisbett, Hoehe.Uebergang, farbe));
        if (!mitKreuzen)
        {
            return;
        }
        // P8-49 (Nico, Variante c): Andreaskreuz mit Lichtern und Halbschranke je Fahrtrichtung rechts vor dem Gleis (P8-50),
        // diagonal gegenueber, ausserhalb des Lichtraums (mit den Laternen im Paket-Netz, Bahnuebergang.Hinzu in PaketNetzBauen; im Netz der Stadt leuchtet nichts)
        kreuze.AddRange(Bahnuebergang.Plaetze(mitte, richtung, sinus, gleis));
    }

    /// <summary>
    /// Bruecke (Wunsch Nico 25.10.2026): wo eine Strasse den Fluss kreuzt, ein Steinbett unter der
    /// Fahrbahn bis ueber beide Ufer, Bruestungen an beiden Seiten und Pfosten an den Enden.
    /// </summary>
    private static float BrueckeHalb(float sinus) => (((FlussBreite + 0.3f) / 2) + 0.18f) / sinus;

    /// <summary>
    /// Weisung P12-214 (Nico: „das komische Geruest sollte bei dem alten Modell entfernt werden“): ein Gebaeude ohne Ort
    /// zeichnet keine Teile „geruest“ (Baugeruest des Orts Talheim-Nord, solange seine Abnahme offen ist). Sonst bleibt das
    /// Aussehen gleich (die fertige Form mit Glasfront wurde bewusst nicht gewaehlt, E1). Fassung 0 hat nur das Stellwerk
    /// ohne Geruest, darum bitgleich; der Ort Talheim-Nord selbst bleibt wie bisher.
    /// </summary>
    internal static List<(int Nr, PyObjekt Teil)> OhneGeruest(List<(int Nr, PyObjekt Teil)> teile) =>
        GeruestAltFuerTests ? teile : [.. teile.Where(t => !Py.Gleich(t.Teil["art"], "geruest"))];

    /// <summary>Gegenprobe P12-214: Gebaeude ohne Ort zeigen das Geruest wie bis A 0.68.1 (nur Tests, je Ablauf).</summary>
    public static bool GeruestAltFuerTests { get => GeruestAlt.Value; set => GeruestAlt.Value = value; }
    private static readonly AsyncLocal<bool> GeruestAlt = new();

    /// <summary>Gegenprobe Weisung P12-214: Bruecke nur so breit wie die Fahrbahn (wie bis A 0.68.1, Gehweg lief aussen am Gelaender vorbei; nur Tests, je Ablauf).</summary>
    public static bool BrueckeSchmalAltFuerTests { get => BrueckeSchmalAlt.Value; set => BrueckeSchmalAlt.Value = value; }
    private static readonly AsyncLocal<bool> BrueckeSchmalAlt = new();

    private static void Bruecke(List<Teil> teile, List<Streifen> streifen, Vector2 mitte, Vector2 richtung, float sinus, float gehweg)
    {
        var halb = BrueckeHalb(sinus);
        var a = mitte - (richtung * halb);
        var b = mitte + (richtung * halb);
        var stein = Netz.Farbe("#9A94A8");
        // Steinbett breiter als Fahrbahn und Gehweg: sein Rand zeigt die Bruecke, Fahrbahn und Gehweg liegen darauf
        streifen.Add(new Streifen([a, b], StrassenBreite + (2 * gehweg) + 0.2f, 0f, Hoehe.Strasse - 0.012f, stein));
        var quer = new Vector2(-richtung.Y, richtung.X);
        foreach (var seite in (float[])[-1f, 1f])
        {
            // Bruestung aussen an der Gehwegkante (P12-214), ohne Gehweg an der Fahrbahnkante
            var versatz = quer * seite * ((StrassenBreite / 2) + gehweg + 0.06f);
            streifen.Add(new Streifen([a + versatz, b + versatz], 0.07f, Hoehe.Strasse - 0.012f, Hoehe.Strasse + 0.16f, stein * 0.92f));
            foreach (var ende in (Vector2[])[a + versatz, b + versatz])
            {
                teile.Add(Quader(ende.X - 0.07f, 0f, ende.Y - 0.07f, ende.X + 0.07f, Hoehe.Strasse + 0.24f, ende.Y + 0.07f, stein * 0.85f));
            }
        }
    }

    /// <summary>Halber Abstand der Schienen (Normalspur 1,435 m).</summary>
    public const float SpurHalb = 1.435f / 2 * Meter;

    /// <summary>Tuer der eigenen Haeuser: 1,8 m breit (zweifluegelig), 2,2 m hoch.</summary>
    public const float TuerBreite = 1.8f * Meter;

    public const float TuerHoehe = 2.2f * Meter;

    /// <summary>Breite der Zuege (Wagenkasten 2,9 m).</summary>
    public const float ZugBreite = 2.9f * Meter;

    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 1): schiebt einen Baum, dessen Krone eine gesperrte Linie beruehrt, quer
    /// von ihr weg, bis er frei steht (z. B. der Baum der Wohnung, der sonst auf der Kreuzung stand).
    /// </summary>
    private sealed class BaumPlaner(List<(IReadOnlyList<Vector2> Linie, float Halb)> sperren)
    {
        public List<(IReadOnlyList<Vector2> Linie, float Halb)> Sperren { get; } = sperren;

        public List<(Vector2 Mitte, float R)> Gesetzt { get; } = [];

        public Vector2 Setzen(float x, float z, float r)
        {
            var p = new Vector2(x, z);
            for (var runde = 0; runde < 20; runde++)
            {
                var schlimmste = 0f;
                var weg = Vector2.Zero;
                foreach (var (linie, halb) in Sperren)
                {
                    var (naechster, richtung) = Naechster(p, linie);
                    var fehlt = halb + r + 0.02f - Vector2.Distance(p, naechster);
                    if (fehlt > schlimmste)
                    {
                        var weg1 = p - naechster;
                        weg = (weg1.LengthSquared() > 1e-8f ? Vector2.Normalize(weg1) : new Vector2(-richtung.Y, richtung.X)) * fehlt;
                        schlimmste = fehlt;
                    }
                }
                if (schlimmste <= 0)
                {
                    break;
                }
                p += weg;
            }
            Gesetzt.Add((p, r));
            return p;
        }

        /// <summary>Naechster Punkt der Linie und die Richtung ihres Abschnitts dort.</summary>
        private static (Vector2 Punkt, Vector2 Richtung) Naechster(Vector2 p, IReadOnlyList<Vector2> linie)
        {
            var best = (Punkt: linie[0], Richtung: Vector2.UnitX);
            var abstand = float.MaxValue;
            for (var i = 1; i < linie.Count; i++)
            {
                var (a, b) = (linie[i - 1], linie[i]);
                var ab = b - a;
                var t = ab.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f) : 0f;
                var q = a + (ab * t);
                if (Vector2.Distance(p, q) < abstand)
                {
                    abstand = Vector2.Distance(p, q);
                    best = (q, ab.LengthSquared() > 0 ? Vector2.Normalize(ab) : Vector2.UnitX);
                }
            }
            return best;
        }
    }

    /// <summary>
    /// A 0.43.1 (Weisung P12-39, Nico P12-37/P12-38: leere Stadt ganz frei): <c>stadtdaten.darstellung.kleingruen</c>.
    /// Nur der Wert false schaltet das Kleingruen an Strassenraendern und auf freien Flaechen ab; fehlt er (Fassung 0 und
    /// jede Stadt vor A 0.43.1), gilt "an" wie bisher. Einen anderen Wert meldet <c>StadtbauPruefung.Aussen</c> rot.
    /// </summary>
    public static bool KleingruenAn(PyObjekt? stadtDaten) =>
        !(stadtDaten?["darstellung"] is PyObjekt dar && dar["kleingruen"] is false);

    /// <summary>Phase 8 W7: Kleingruen (Buesche, Graeser, Steine, Beete; eigenes Netz), null ohne Grafik.</summary>
    public Gruen.Ergebnis? GruenKlein { get; private init; }

    /// <summary>Phase 8 W10 (Nico 3b): Nummern der eigenen Fenster in <see cref="Teile"/>, die nachts leuchten.</summary>
    public IReadOnlySet<int> LichtTeile { get; private init; } = new HashSet<int>();

    /// <summary>Phase 8 W11 (Nico P8-45): die 21 Strassenlaternen nach Nicos Skizze (<see cref="Nachtlicht.Plaetze"/>), einmal je Szene.</summary>
    public IReadOnlyList<Nachtlicht.Laterne> Laternen => _laternen ??= Nachtlicht.Plaetze(this);

    private List<Nachtlicht.Laterne>? _laternen;

    /// <summary>Fund 305: Laterne ohne freien Platz auslassen statt abbrechen (nur Ansichten des Werkzeugs, <see cref="Stadtbau.Nachsicht"/>).</summary>
    public bool LaternenOhnePlatzAuslassen { get; private init; }

    /// <summary>Phase 13 Welle 1: Wunschplaetze der Laternen (x, z, doppelt) aus der Stadt-Fassung, Reihenfolge = Nummer.</summary>
    public IReadOnlyList<(float X, float Z, bool Doppelt)> LaternenSkizze { get; private init; } = Nachtlicht.Skizze;

    /// <summary>Phase 8 W7/W11: Plaetze der Fest-Deko aller Festzeiten (Mitte, Radius); frei fuer Gruen und Laternen.</summary>
    public IReadOnlyList<(Vector2 P, float R)> FestPlaetzeAlle { get; private init; } = [];

    /// <summary>Mittellinien der Strassen (fuer die Autos, Phase 8 W7).</summary>
    public IReadOnlyList<IReadOnlyList<Vector2>> Strassen { get; private init; } = [];

    /// <summary>P8-28: Stuecke, deren Fenster an einer Tuer die Wandfarbe bekommen haben (Nummer in <see cref="Stuecke"/>).</summary>
    public IReadOnlyDictionary<int, GrafikModell> Umgefaerbt { get; private init; } = new Dictionary<int, GrafikModell>();

    /// <summary>
    /// Sammelliste 264: Haeuser mit Eintrag in <c>stadtdaten.farben</c> (Kennung = Ort oder Modellname eines Gebaeudes ohne
    /// Ort) bekommen ihre Farben; nur die Eckfarben aendern sich (<see cref="Gebaeudefarben"/>).
    /// </summary>
    internal static void FarbenAnwenden(PyObjekt? stadtDaten, GrafikBibliothek grafik, List<ModellStueck> stuecke, List<StadtHaus> haeuser,
        Dictionary<int, GrafikModell> umgefaerbt)
    {
        var farben = Gebaeudefarben.Lesen(stadtDaten);
        if (farben.Count == 0)
        {
            return;
        }
        foreach (var haus in haeuser)
        {
            var kennung = haus.Id.Contains('#', StringComparison.Ordinal) ? haus.Id[..haus.Id.LastIndexOf('#')] : haus.Id;
            if (!farben.TryGetValue(kennung, out var ziele))
            {
                continue;
            }
            var liste = new List<(int I, GrafikModell M, float Faktor, float Unten)>();
            for (var i = haus.StueckeVon; i < haus.StueckeBis; i++)
            {
                if (!ImPaket(stuecke[i].Name) && (umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name)) is { } m)
                {
                    var l = stuecke[i].Lage;
                    liste.Add((i, m, new Vector3(l.M11, l.M12, l.M13).Length(), l.M42));
                }
            }
            var toene = Gebaeudefarben.Bestimmen([.. liste.Select(x => (x.M, x.Faktor, x.Unten))]);
            var oben = liste.Count == 0 ? 0 : liste.Max(x => (x.M.Max.Y * x.Faktor) + x.Unten);
            foreach (var (i, m, faktor, unten) in liste)
            {
                var neu = Gebaeudefarben.Umfaerben(m, toene, ziele, faktor, unten, oben);
                if (!ReferenceEquals(neu, m))
                {
                    umgefaerbt[i] = neu;
                }
            }
        }
    }

    /// <summary>Modell des Stuecks Nummer i (umgefaerbt oder aus der Bibliothek).</summary>
    public GrafikModell? ModellAn(int i) => Umgefaerbt.GetValueOrDefault(i) ?? Grafik?.Modell(Stuecke[i].Name);

    /// <summary>W8b: Tueren der Orte mit Fussweg (eigene aus Kaesten oder die des CC0-Modells).</summary>
    public IReadOnlyList<StadtTuer> Tueren { get; private init; } = [];

    /// <summary>
    /// Welle A 0.58.0 (Weisung P12-148 Frage 1 = a): Bloecke (Grundflaechen x1, z1, x2, z2) je Ort, wie sie das Spiel fuer die
    /// Tuer nimmt (naechster Block zum Anfang des Fusswegs). Nur Auskunft fuer das Werkzeug, aendert nichts an der Stadt.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<(double X1, double Z1, double X2, double Z2)>> OrtBloecke { get; private init; } =
        new Dictionary<string, IReadOnlyList<(double X1, double Z1, double X2, double Z2)>>();

    /// <summary>W8b: Haeuser ohne Ort mit Gehweg, Tuer und Namensschild (nicht betretbar).</summary>
    public IReadOnlyList<StadtZierhaus> Zierhaeuser { get; private init; } = [];

    /// <summary>Phase 8 W9: Jahreszeit der Farben (nur Darstellung, Sommer = Farben wie vorher).</summary>
    public Jahreszeit Jahreszeit { get; private init; } = Jahreszeit.Sommer;

    /// <summary>
    /// Welle A 0.51.0 (Weisung P12-84, Sammelliste 237): Spieltag fuer die Wege der automatischen Autostrecken
    /// (<see cref="Autostrecken.Anlegen"/>): <see cref="Stadt.VerkehrTag"/> (Werkzeug: 1), sonst der Tag des Spielstands,
    /// ohne Spielstand 1. Nur Darstellung.
    /// </summary>
    public long Spieltag { get; private init; } = 1;

    /// <summary>Phase 8 W9: ab diesem Teil und diesem Modell steht die Fest-Deko (behaelt ihre Farben).</summary>
    public int FestTeileVon { get; private init; } = int.MaxValue;

    public int FestStueckeVon { get; private init; } = int.MaxValue;

    /// <summary>Netz des Kleingruens (nur in der Naehe gezeichnet), null = keins.</summary>
    public Netz? GruenNetzBauen() => GruenNetzBauen(null);

    /// <summary>Welle D2: wie <see cref="GruenNetzBauen()"/>, ohne die Stuecke aus <paramref name="plan"/>.</summary>
    public Netz? GruenNetzBauen(Instanzplan? plan)
    {
        if (GruenKlein is not { } g || Grafik is null)
        {
            return null;
        }
        // Phase 8 W9: Kleingruen nach der Jahreszeit, im Winter Schnee auf Bueschel, Steinen und Beeten
        var netz = new Netz { Faerbung = Jahreszeitfarben.FuerTeile(Jahreszeit, Faerbeart.Klein) };
        foreach (var t in g.Teile)
        {
            netz.Hinzu(t);
        }
        netz.Faerbung = null;
        var ohne = plan?.Gruen ?? Instanzplan.Leer.Gruen;
        var nr = -1;
        foreach (var st in g.Stuecke)
        {
            nr++;
            if (!ohne.Contains(nr) && Grafik.Modell(st.Name) is { } m)
            {
                netz.Hinzu(m, st.Lage, ModellFaerbung(m, st.Lage, Faerbeart.Klein));
            }
        }
        return netz;
    }

    /// <summary>Phase 8 W11: Tanne an (x, z)? Fest nach Lage, gerade und krumm im Wechsel; null = Laubbaum wie bisher.</summary>
    public static string? Tanne(float x, float z) =>
        ((((int)MathF.Round((x * 5) + (z * 11)) % 3) + 3) % 3) != 0 ? null
        : ((int)MathF.Round((x * 3) + (z * 7)) & 1) == 1 ? "gy-pine-crooked" : "gy-pine";

    /// <summary>Phase 8 W11: Name der Tanne in der Jahreszeit (im Herbst das orange Modell).</summary>
    public static string TanneIn(string name, Jahreszeit j) =>
        j == Jahreszeit.Herbst && name.StartsWith("gy-pine", StringComparison.Ordinal) ? name.Replace("gy-pine", "gy-pine-fall", StringComparison.Ordinal) : name;

    private static void Baum(List<Teil> teile, List<ModellStueck> stuecke, GrafikBibliothek? grafik, BaumPlaner planer, float x, float z, float r)
    {
        (x, z) = planer.Setzen(x, z, r) is var p ? (p.X, p.Y) : default;
        if (grafik is not null && Tanne(x, z) is { } tname)
        {
            // Phase 8 W11 (Sammelliste 160): etwa jeder dritte Baum eine Tanne aus dem Graveyard Kit (eigenes Netz,
            // PaketNetzBauen; im Herbst das orange Modell)
            if (grafik.Modell(tname) is { } tanne)
            {
                var s = (0.4f + (2.2f * r)) / (tanne.Max.Y - tanne.Min.Y) * 1.25f;
                var anker = new Vector3((tanne.Min.X + tanne.Max.X) / 2, tanne.Min.Y, (tanne.Min.Z + tanne.Max.Z) / 2);
                stuecke.Add(new ModellStueck(tname, Lage(tanne, anker, new Vector3(s), new Vector3(x, 0, z), Vector2.UnitY)));
                return;
            }
        }
        if (grafik is not null && Gruen.Baum(stuecke, grafik, x, z, r))
        {
            return;
        }
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

    /// <summary>Gegenprobe Sammelliste 294: Kopien zeichnen sich nur ueber ihren eigenen Namen (wie bis A 0.65.0, nur Tests, je Ablauf).</summary>
    public static bool KopieAltFuerTests { get => KopieAlt.Value; set => KopieAlt.Value = value; }
    private static readonly AsyncLocal<bool> KopieAlt = new();

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

    /// <summary>Gegenprobe Weisung P12-220: der Zug steht wie bisher auf dem laengsten geraden Stueck (nur Tests, je Ablauf).</summary>
    public static bool ZugHaltAltFuerTests { get => ZugHaltAlt.Value; set => ZugHaltAlt.Value = value; }
    private static readonly AsyncLocal<bool> ZugHaltAlt = new();

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
    public StadtOrt? OrtBei(Vector3 start, Vector3 richtung, float tuerWeite = 0.8f)
    {
        StadtOrt? bester = null;
        var besterT = float.MaxValue;
        foreach (var ort in Orte)
        {
            var rand = new Vector3(0.2f, 0.2f, 0.2f);
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
