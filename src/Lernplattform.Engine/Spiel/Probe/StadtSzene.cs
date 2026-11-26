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
        IReadOnlyList<StadtOrt> orte, string signatur, float breite, float tiefe, Spaeter<Wegsuche> laufraster, IReadOnlyList<StadtHaus> haeuser)
    {
        Haeuser = haeuser;
        Teile = teile;
        Streifen = streifen;
        _laufraster = laufraster;
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

    /// <summary>
    /// Welle A 0.83.0 (Weisung P12-282): gezogene Stadtteile mit Namen (nur Anzeige: Karte im Spiel-Handy, Schild beim
    /// Betreten); Stadt 1 und Fassung 0 haben keine (leer, alles wie bisher).
    /// </summary>
    public IReadOnlyList<Stadtbau.Stadtteile.Bereich> StadtteilNamen { get; private init; } = [];

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
    public Wegsuche Laufraster => _laufraster.Wert;

    private readonly Spaeter<Wegsuche> _laufraster;

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
    public Netz NetzBauen(bool fern, Instanzplan? plan) => NetzBauen(fern, plan, null);

    /// <summary>
    /// Welle Nachladen (Weisung P12-280): wie <see cref="NetzBauen(bool, Instanzplan?)"/>, mit <paramref name="auswahl"/> nur
    /// der Boden (die zwei Bodenquader und das Gelaende, immer geladen) oder nur eine Kachel (alles andere, zugeschnitten mit
    /// <see cref="Netz.NurKachel"/>). Ohne Auswahl bitgleich wie vorher.
    /// </summary>
    private Netz NetzBauen(bool fern, Instanzplan? plan, Auswahl? auswahl)
    {
        var netzAuswahl = NetzBauenRoh(fern, plan, auswahl);
        return auswahl is { Kachel: { } k } ? netzAuswahl.NurKachel(k, Breite, Tiefe) : netzAuswahl;
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
