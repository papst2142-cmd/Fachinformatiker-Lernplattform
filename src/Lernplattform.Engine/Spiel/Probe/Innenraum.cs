using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Eine Person im Innenraum (Platz wie Python "platz", Aussehen fuer die Figur).</summary>
/// <summary>
/// Nachtrag 1 zu P7-13 (Wunsch 3): ein Wandstueck des Innenraums (Teile <see cref="Von"/> bis vor
/// <see cref="Bis"/> in <see cref="Innenraum.Teile"/>) mit Kasten; <see cref="Aussen"/> zeigt bei einer
/// Aussenwand nach draussen, bei einer Innenwand ist es null.
/// </summary>
public sealed record InnenWand(int Von, int Bis, Vector3 Min, Vector3 Max, Vector2 Aussen)
{
    /// <summary>
    /// Welle A 1.6.0 (Sammelliste 443): Zarge und Sturz einer Raumtuer (null = Wandstueck). Der Kasten reicht ueber die ganze
    /// Oeffnung; im Durchgang stehen unten nur die zwei Pfosten (<see cref="Innenraum.TuerPfosten"/>), der Rest liegt ueber dem Kopf.
    /// </summary>
    public RaumTuer? Tuer { get; init; }
}

/// <summary>Phase 9 W1 (Weisung P9-3): Stelle (X, Z) und Blick (Bogenmass wie die Figur) einer sitzenden Person, Stuhl = Mitte des Stuhls.</summary>
/// <summary>Weisung P12-6: gezeichnete Teile eines Einrichtungsstuecks (Pruefung auf Ueberschneidung und Verdecken).</summary>
public sealed record DekoUmriss(PyObjekt Deko, IReadOnlyList<Teil> Teile)
{
    /// <summary>Sammelliste 347 (nur fuer Pruefungen): die eigenen Quader des Stuecks (ohne Modelle).</summary>
    public IReadOnlyList<Teil> Kaesten { get; init; } = [];

    /// <summary>Sammelliste 347 (nur fuer Pruefungen): die Modelle des Stuecks mit ihrer Lage (echte Ecken).</summary>
    public IReadOnlyList<ModellStueck> Modelle { get; init; } = [];

    /// <summary>Grundflaeche aller Teile (x1, z1, x2, z2) und die groesste Hoehe.</summary>
    public (float X1, float Z1, float X2, float Z2, float Oben) Flaeche => Teile.Count == 0
        ? (0, 0, 0, 0, 0)
        : (Teile.Min(t => Math.Min(t.X1, t.X2)), Teile.Min(t => Math.Min(t.Z1, t.Z2)), Teile.Max(t => Math.Max(t.X1, t.X2)), Teile.Max(t => Math.Max(t.Z1, t.Z2)), Teile.Max(t => Math.Max(t.Y1, t.Y2)));
}

public sealed record Sitzplatz(float X, float Z, float Blick, Vector2 Stuhl);

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
public sealed partial class Innenraum
{
    /// <summary>
    /// Eine Einheit des Grundrisses sind 0,6 m (Tuer 1,2 = 0,72 m, Schreibtisch 2,4 x 1,0 = 1,44 x 0,6 m). Welle A 1.1.0
    /// (Sammelliste 409) hatte 0,75 m: der ganze Raum wurde groesser, die Flure vor allem laenger (Nico: „Der Flur wurde laenger,
    /// aber nicht breiter“). Welle A 1.2.0 (Weisung P12-332, Nico 2c): wieder 0,6 m, dafuer sind die Flure wirklich breiter
    /// (neue Grundrisse als Daten, <see cref="GrundrisseBreit"/>: Diele 1,32 m, Flure 1,80 m), die Laengen wie bis A 1.0.0.
    /// </summary>
    public const float Meter = 1f / 0.6f;

    /// <summary>Welle A 1.2.0: Massstab von A 1.1.0 (nur fuer die Gegenprobe der Gangbreiten und -laengen in den Tests).</summary>
    public const float MeterA110 = 1f / 0.75f;

    /// <summary>Hoehe der Waende (1,8 m): niedriger als echt, damit die schraege Kamera in die Raeume sieht.</summary>
    public const float WandHoehe = 1.8f * Meter;

    /// <summary>
    /// Welle A 0.92.0 (Weisung P12-301 Punkt 4, Nico 4a): in der Ich-Sicht sind die Waende 2,5 m hoch (die Spielflaeche streckt
    /// die Wandstuecke senkrecht um <see cref="IchWandStreckung"/>), und eine Decke liegt darauf (<see cref="DeckeNetz"/>).
    /// Nur Darstellung: Grundriss, Wandlinie und Wege bleiben dieselben.
    /// </summary>
    public const float IchWandHoehe = 2.5f * Meter;

    /// <summary>Welle A 0.92.0: Streckung der Wandstuecke in der Ich-Sicht (2,5 m statt 1,8 m).</summary>
    public const float IchWandStreckung = IchWandHoehe / WandHoehe;

    /// <summary>Welle A 0.92.0: Farbe der Decke (hell, wie die Wand innen).</summary>
    public static readonly Vector3 DeckeFarbe = Netz.Farbe("#F4F1FA");

    /// <summary>Welle A 0.92.0: Decke ueber dem ganzen Innenraum in Hoehe der gestreckten Waende (nur in der Ich-Sicht gezeichnet).</summary>
    public Netz DeckeNetz() => Netz.Aus([new Teil(TeilArt.Quader, -0.5f, IchWandHoehe, -0.5f, Breite + 0.5f, IchWandHoehe + 0.05f, Tiefe + 0.5f, DeckeFarbe)]);

    /// <summary>
    /// Welle A 0.92.0: Huelle der Ich-Sicht in einem Netz (ein Zeichenaufruf): alle Wandstuecke um <see cref="IchWandStreckung"/>
    /// gestreckt und die Decke. In der Ich-Sicht wird nichts halb durchsichtig, darum braucht es die Waende nicht einzeln.
    /// </summary>
    public Netz IchHuelle()
    {
        var netz = DeckeNetz();
        var lage = Matrix4x4.CreateScale(1f, IchWandStreckung, 1f);
        foreach (var wand in Waende)
        {
            netz.Hinzu(WandNetzFertig(wand), lage);
        }
        return netz;
    }

    /// <summary>Groesse der Figuren im Innenraum (1,75 m).</summary>
    public const float FigurHoehe = 1.75f * Meter;

    /// <summary>Ansicht der Serverfarm (orte.json "ansicht" des Lokschuppens).</summary>
    public const string Serverfarm = "serverfarm";

    /// <summary>Alle CC0-Modelle, die Innenraeume nutzen (Pruefung: jedes Modell in modelle.bin wird gebraucht).</summary>
    public static readonly IReadOnlyList<string> Modelle = ["bathroomSink", "bathtub", "bedDouble", "bedSingle", "bench", "bookcaseClosedDoors", "bookcaseClosedWide", "bookcaseOpen", "cabinetTelevision", "cardboardBoxClosed", "chair", "chairDesk", "computerScreen", "desk", "kitchenBar", "kitchenCabinet", "kitchenCoffeeMachine", "kitchenFridge", "lampRoundFloor", "loungeChair", "loungeChairRelax", "loungeSofa", "loungeSofaOttoman", "pottedPlant", "rugRectangle", "shower", "sideTableDrawers", "table", "tableCoffee", "televisionModern", "toilet", "train-electric-city-a", "washer", "kkArmchair", "kkChairA", "kkPictureframeLargeB"];

    private readonly List<Teil> _teile = [];
    private readonly List<ModellStueck> _stuecke = [];
    private readonly List<(Netz Netz, Matrix4x4 Lage)> _puppen = [];

    private Innenraum(string ansicht, string ort, PyObjekt gebaeude, List<InnenPerson> personen, GrafikBibliothek? grafik, PyObjekt? boeden,
        PyObjekt? kleider = null, string? fest = null, PyObjekt? feste = null)
    {
        Fest = fest;
        _festDaten = feste;
        Kleiderdaten = kleider;
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
        var leute = personen.Select(p => (object?)p.Daten).ToList();
        // Phase 8 W6: Fest-Deko als Hindernis in einer Kopie des Grundrisses (nur Laufwege der Darstellung)
        var muster = FestDeko.Muster(feste, fest);
        if (muster is not null)
        {
            var ziele = SpielKern.Laeden.Contains(ort)
                ? SpielKern.Auslage(new PyObjekt { { "gebaeude", gebaeude } })
                    .Select(d => (Py.F(d["x"]) + (Py.F(d["w"]) / 2), Py.F(d["y"]) + (Py.F(d["h"]) / 2),
                        SpielKern.MoebelReichweite + 0.3 + (Math.Max(Py.F(d["w"]), Py.F(d["h"])) / 2))).ToList()
                : [];
            _festPlan = FestDeko.InnenPlanen(ort, gebaeude, leute, grafik, feste, muster, ort == OrtZuhause, ziele);
            _festMuster = muster;
        }
        Lauf = _festPlan?.Lauf ?? new LaufOrt(gebaeude, leute);
        Aufbauen(boeden);
        _sitze = SitzeFinden();
    }

    private readonly FestDeko.InnenPlan? _festPlan;
    private readonly PyObjekt? _festDaten;
    private readonly PyObjekt? _festMuster;

    /// <summary>Phase 8 W6: die Festzeit, mit der dieser Innenraum gebaut wurde (null = keine).</summary>
    public string? Fest { get; }

    /// <summary>Phase 8 W6: die gesetzte Fest-Deko (leer ohne Festzeit oder ohne freien Platz).</summary>
    public IReadOnlyList<FestDeko.Gesetzt> FestGesetzt => _festPlan?.Gesetzt ?? [];

    /// <summary>"buero", "zuhause", "filiale", "kunde:&lt;id&gt;" oder "serverfarm".</summary>
    public string Ansicht { get; }

    /// <summary>Ort wie Python site (buero, zuhause, filiale, Kundenort-id, serverfarm).</summary>
    public string Ort { get; }

    public PyObjekt Gebaeude { get; }

    public IReadOnlyList<InnenPerson> Personen { get; }

    /// <summary>Raster und Personen fuer die Wege (wie fisi_game.walk_path).</summary>
    public LaufOrt Lauf { get; }

    public GrafikBibliothek? Grafik { get; }

    /// <summary>Phase 8 W5: Inhalt von geschaefte.json fuer die Kleidung im Kaufhaus (Farben der Auslage, Puppen), null = grau.</summary>
    public PyObjekt? Kleiderdaten { get; }

    public float Breite { get; }

    public float Tiefe { get; }

    public float StartX { get; }

    public float StartZ { get; }

    public IReadOnlyList<Teil> Teile => _teile;

    public IReadOnlyList<ModellStueck> Stuecke => _stuecke;

    /// <summary>
    /// Weisung P12-6 (nur fuer Pruefungen): je Einrichtungsstueck die gezeichneten Teile als achsparallele Quader
    /// (Modelle mit ihrer Lage umgerechnet, Ersatz-Quader ohne Grafik). Aendert nichts an der Darstellung.
    /// </summary>
    public IReadOnlyList<DekoUmriss> DekoUmrisse => _dekoUmrisse;

    /// <summary>Sammelliste 347 (nur fuer Pruefungen): Quader und Modelle der Fest-Deko innen (leer ohne Fest).</summary>
    public IReadOnlyList<Teil> FestKaesten { get; private set; } = [];

    /// <inheritdoc cref="FestKaesten"/>
    public IReadOnlyList<ModellStueck> FestModelle { get; private set; } = [];

    private readonly List<DekoUmriss> _dekoUmrisse = [];

    private IEnumerable<Teil> StueckeAlsTeile(IEnumerable<ModellStueck> stuecke)
    {
        foreach (var st in stuecke)
        {
            if (Grafik?.Modell(st.Name) is not { } gm)
            {
                continue;
            }
            Vector3 min = new(float.MaxValue), max = new(float.MinValue);
            foreach (var x in new[] { gm.Min.X, gm.Max.X })
            {
                foreach (var y in new[] { gm.Min.Y, gm.Max.Y })
                {
                    foreach (var z in new[] { gm.Min.Z, gm.Max.Z })
                    {
                        var p = Vector3.Transform(new Vector3(x, y, z), st.Lage);
                        min = Vector3.Min(min, p);
                        max = Vector3.Max(max, p);
                    }
                }
            }
            yield return new Teil(TeilArt.Quader, min.X, min.Y, min.Z, max.X, max.Y, max.Z, Vector3.Zero);
        }
    }

    /// <summary>Welche Moebel wo stehen (aendert sie sich, laedt die Darstellung neu).</summary>
    public string Signatur { get; private set; } = "";

    /// <summary>Alle Wandstuecke (innen und aussen), jedes fuer sich durchsichtig zu zeichnen.</summary>
    public IReadOnlyList<InnenWand> Waende => _waende;

    private readonly List<InnenWand> _waende = [];

    /// <summary>Das ganze Netz; mit <paramref name="ohneWaende"/> ohne die Wandstuecke (die zeichnet die Flaeche einzeln).</summary>
    public Netz NetzBauen(bool ohneWaende = false)
    {
        var netz = Netz.Aus(ohneWaende ? _teile.Where((_, i) => !_waende.Any(w => i >= w.Von && i < w.Bis)) : _teile);
        foreach (var stueck in _stuecke)
        {
            if (Grafik?.Modell(stueck.Name) is { } modell)
            {
                netz.Hinzu(modell, stueck.Lage);
            }
        }
        foreach (var (puppe, lage) in _puppen)
        {
            netz.Hinzu(puppe, lage);
        }
        return netz;
    }

    /// <summary>Netz eines Wandstuecks.</summary>
    public Netz WandNetz(InnenWand wand) => Netz.Aus(_teile.Skip(wand.Von).Take(wand.Bis - wand.Von));

    private Netz? _netzOhneWaende;
    private Dictionary<InnenWand, Netz>? _wandNetze;

    /// <summary>
    /// Weisung P7-16: Netze und Laufraster vorab bauen (auch ausserhalb des Oberflaechen-Threads, bevor der
    /// Innenraum gezeigt wird). Danach liefern <see cref="NetzOhneWaende"/> und <see cref="WandNetzFertig"/>
    /// dieselben Netze ohne neue Arbeit im Bild.
    /// </summary>
    public void Vorbereiten()
    {
        _netzOhneWaende ??= NetzBauen(ohneWaende: true);
        _wandNetze ??= _waende.ToDictionary(w => w, WandNetz);
        _personNetze ??= Personen.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => (g.First().Aussehen, Figur.Netz(Grafik, g.First().Aussehen, 1f)));
        _ = Lauf.Feld;
    }

    private Dictionary<string, (PyObjekt? Aussehen, Netz Netz)>? _personNetze;

    /// <summary>
    /// Phase 9 W1 (Weisung P9-3, Wunsch 4): eine Person, deren Platz hoechstens so weit von der Mitte eines
    /// Schreibtischstuhls entfernt ist, sitzt auf ihm (vorher stand sie "im" Stuhl).
    /// </summary>
    public const float SitzAbstand = 0.45f;

    /// <summary>
    /// Phase 9 W1: so weit sitzt die Figur hinter der Stuhlmitte (vom Tisch weg); damit beruehren sich Stuhl,
    /// Tisch und sitzende Figur nicht (Test SpielSitzenTests prueft 0,03 bis 0,07: frei an allen Tischbreiten).
    /// </summary>
    public const float SitzZurueck = 0.05f;

    private readonly List<Sitzplatz> _stuehle = [];

    private readonly Dictionary<InnenPerson, Sitzplatz> _sitze;

    /// <summary>Phase 9 W1: alle Schreibtischstuehle des Raums (Stelle der sitzenden Figur, Blick zum Tisch).</summary>
    public IReadOnlyList<Sitzplatz> Stuehle => _stuehle;

    private void StuhlMerken((float X, float Z, float W, float T) stuhl, (float X, float Z) tisch)
    {
        var mitte = new Vector2(stuhl.X + (stuhl.W / 2), stuhl.Z + (stuhl.T / 2));
        var zumTisch = new Vector2(tisch.X, tisch.Z) - mitte;
        // der Stuhl steht immer an einer Seite des Tischs: Blick genau senkrecht zu dieser Seite
        zumTisch = MathF.Abs(zumTisch.X) > MathF.Abs(zumTisch.Y) ? new Vector2(MathF.Sign(zumTisch.X), 0) : new Vector2(0, MathF.Sign(zumTisch.Y));
        var stelle = mitte - (zumTisch * SitzZurueck);
        _stuehle.Add(new Sitzplatz(stelle.X, stelle.Y, MathF.Atan2(zumTisch.X, zumTisch.Y), mitte));
    }

    /// <summary>Phase 9 W1: der Stuhl, auf dem die Person sitzt (naechster Stuhl bis <see cref="SitzAbstand"/>), sonst null (sie steht).</summary>
    public Sitzplatz? Sitz(InnenPerson person) => _sitze.TryGetValue(person, out var sitz) ? sitz : null;

    private Dictionary<InnenPerson, Sitzplatz> SitzeFinden()
    {
        var sitze = new Dictionary<InnenPerson, Sitzplatz>(ReferenceEqualityComparer.Instance);
        foreach (var p in Personen)
        {
            var platz = new Vector2(p.X, p.Z);
            if (_stuehle.Where(s => Vector2.Distance(s.Stuhl, platz) <= SitzAbstand).MinBy(s => Vector2.Distance(s.Stuhl, platz)) is { } naechster)
            {
                sitze[p] = naechster;
            }
        }
        return sitze;
    }

    /// <summary>Vorbereitetes Netz der Person (Figur der Hoehe 1) zu genau diesem Aussehen, sonst null.</summary>
    public Netz? PersonNetz(string id, PyObjekt? aussehen) =>
        _personNetze is { } fertig && fertig.TryGetValue(id, out var p) && ReferenceEquals(p.Aussehen, aussehen) ? p.Netz : null;

    /// <summary>Netz ohne Wandstuecke (vorbereitet oder jetzt gebaut).</summary>
    public Netz NetzOhneWaende => _netzOhneWaende ??= NetzBauen(ohneWaende: true);

    /// <summary>Netz eines Wandstuecks (vorbereitet oder jetzt gebaut).</summary>
    public Netz WandNetzFertig(InnenWand wand) => _wandNetze is { } fertig && fertig.TryGetValue(wand, out var netz) ? netz : WandNetz(wand);

    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 3, Sammelliste 102): Wandstuecke, die halb durchsichtig gezeichnet
    /// werden: jede Aussenwand, die zur Kamera zeigt (die vorderen Waende), und jede Wand, die zwischen
    /// Auge und Figur steht (Fuesse, Mitte, Kopf wie <see cref="StadtSzene.Verdeckt"/>). Fuellt
    /// <paramref name="ergebnis"/> (ohne neue Liste je Bild).
    /// </summary>
    public void Durchsichtig(Vector3 auge, float figurX, float figurZ, float figurHoehe, HashSet<InnenWand> ergebnis)
    {
        ergebnis.Clear();
        foreach (var w in _waende)
        {
            var mitte = (w.Min + w.Max) / 2;
            var blick = new Vector2(auge.X - mitte.X, auge.Z - mitte.Z);
            if (w.Aussen != Vector2.Zero && blick.LengthSquared() > 1e-6f && Vector2.Dot(w.Aussen, Vector2.Normalize(blick)) > 0.2f)
            {
                ergebnis.Add(w);
                continue;
            }
            foreach (var anteil in (ReadOnlySpan<float>)[0.15f, 0.5f, 0.95f])
            {
                if (StadtSzene.Schneidet(auge, new Vector3(figurX, figurHoehe * anteil, figurZ), w.Min, w.Max))
                {
                    ergebnis.Add(w);
                    break;
                }
            }
        }
    }

    /// <summary>Innenraum aus einem beliebigen Grundriss (Pruefungen, Fotos): ohne Personen und Bodenwahl.</summary>
    public static Innenraum AusGrundriss(string ansicht, PyObjekt gebaeude, GrafikBibliothek? grafik) =>
        new(ansicht, OrtFuer(ansicht) ?? ansicht, gebaeude, [], grafik, null);

    /// <summary>
    /// Phase 13 Welle 2 (Weisung P12-17): Innenraum aus einem bearbeiteten Grundriss mit den Personen des Orts (Daten mit
    /// "platz") und der Kleidung des Kaufhauses (<paramref name="kleider"/> = geschaefte.json), wie das Spiel ihn beim
    /// Betreten eines Geschaefts oder Schalters baut (Stadtbau-Werkzeug, gemeinsame Pruefung). Aendert nichts am Spiel.
    /// </summary>
    public static Innenraum AusGrundrissMitPersonen(string ansicht, PyObjekt gebaeude, IEnumerable<PyObjekt> personen, GrafikBibliothek? grafik,
        PyObjekt? kleider = null) =>
        new(ansicht, OrtFuer(ansicht) ?? ansicht, gebaeude, [.. personen.Select(p => Person(p, false))], grafik, null, kleider);

    /// <summary>Weg der Figur zu einem Punkt (reichweite &gt; 0: bis auf diesen Abstand heran), wie walk_path.</summary>
    public List<(double X, double Y)> Weg(double vonX, double vonZ, double nachX, double nachZ, double reichweite = 0) =>
        Wege.Weg((vonX, vonZ), (nachX, nachZ), reichweite, Lauf);

    /// <summary>Person an der Stelle (Tippen), wie person_at.</summary>
    public InnenPerson? PersonBei(double x, double z) =>
        Wege.PersonBei(x, z, Lauf) is { } p ? Personen.FirstOrDefault(q => ReferenceEquals(q.Daten, p)) : null;

    /// <summary>Person in Reichweite der Figur (zum Ansprechen), wie person_near.</summary>
    /// <summary>
    /// Phase 12 Welle 2: Einrichtungsstueck des Grundrisses (Raeume und Flur) mit diesem Typ in Reichweite der Figur
    /// (<see cref="Wohnung.MoebelReichweite"/> vom Rand), das naechste; sonst null. Nur Lesen, die Darstellung bleibt.
    /// </summary>
    public PyObjekt? DekoNahe(string typ, double x, double z)
    {
        var alle = ListeOderLeer(Gebaeude, "raeume").Select(Objekt).SelectMany(r => ListeOderLeer(r, "deko").Select(Objekt))
            .Concat(Gebaeude["flur"] is PyObjekt flur ? ListeOderLeer(flur, "deko").Select(Objekt) : []);
        return alle.Where(d => Py.Gleich(d["typ"], typ))
            .Select(d => (d, a: SpielKern.Abstand(Py.F(d["x"]), Py.F(d["y"]), Py.F(d["w"]), Py.F(d["h"]), x, z)))
            .Where(p => p.a <= SpielKern.MoebelReichweite).OrderBy(p => p.a).Select(p => p.d).FirstOrDefault();
    }

    public InnenPerson? PersonNahe(double x, double z) =>
        Wege.PersonNahe(x, z, Lauf) is { } p ? Personen.FirstOrDefault(q => ReferenceEquals(q.Daten, p)) : null;

    /// <summary>Text unter der Ansicht (Ueberschrift, Text, Knoepfe) wie place_message/home_message.</summary>
    public (string Titel, string Text, List<(string Aktion, string Name)> Knoepfe) Nachricht(double x, double z, ISpielSicht stand)
    {
        if (PersonNahe(x, z) is { } questgeber && Stadtbau.Questebene.IstQuestgeber(questgeber.Daten))
        {
            // Welle Q2: ein Questgeber im Geschaeft oder am Schalter spricht wie eine Person im Buero („Auftrag annehmen“)
            return SpielKern.Regeln.OrtNachricht(Ort, x, z, questgeber.Daten, stand, Gebaeude);
        }
        if (PersonNahe(x, z) is null && ArbeitsortNachricht(x, z, stand) is { } arbeit)
        {
            // Welle E4: Auftrag am Arbeitsort, der Questgeber wartet nicht dort (Schalter „wartet“ aus)
            return arbeit;
        }
        if (SpielKern.Laeden.Contains(Ort))
        {
            return LadenNachricht(x, z, stand);
        }
        if (SpielKern.SchalterOrte.Contains(Ort))
        {
            return SchalterNachricht(x, z, stand);
        }
        if (Ort == Serverfarm)
        {
            if (PersonNahe(x, z) is { } p)
            {
                return (PyFormat.Prozent("%s · %s", p.Name, Get(p.Daten, "rolle", "")), Py.Str(Get(p.Daten, "text", "")), []);
            }
            var raum = Wege.RaumBei(x, z, Gebaeude);
            var wo = raum is not null ? PyFormat.Prozent("Du bist im Raum %s.", raum["name"]) : "Du bist im Flur.";
            return (wo, SpielKern.Regeln.FarmOrtText(stand, stand.Inhalt), []);
        }
        return SpielKern.Regeln.OrtNachricht(Ort, x, z, PersonNahe(x, z)?.Daten, stand, Gebaeude);
    }

    /// <summary>Weisung P12-74: Hinweis auf einer Etage ohne Verkaufsperson (<c>SpielProbeTexte.EtageOhneVerkauf</c> verweist hierher).</summary>
    public const string EtageOhneVerkauf = "Tippe auf eine Ware, um sie anzusehen.";

    /// <summary>W7: Aktion "Sortiment" (alle Waren des Geschaefts) und "Sortiment" eines Teile-Typs.</summary>
    public const string AktionSortiment = "sortiment";

    /// <summary>W7: Aktion "Kaufen" eines Moebelstuecks ("kaufen:&lt;moebel&gt;").</summary>
    public const string AktionKaufen = "kaufen:";

    /// <summary>W7: Ware der Auslage an der Stelle (Tippen), etwas grosszuegiger als ihr Umriss.</summary>
    public PyObjekt? WareBei(double x, double z) =>
        SpielKern.Laeden.Contains(Ort)
            ? SpielKern.Auslage(new PyObjekt { { "gebaeude", Gebaeude } }).FirstOrDefault(d => Abstand(d, x, z) <= 0.2)
            : null;

    /// <summary>W7: naechste Ware der Auslage in Reichweite der Figur (wie furniture_near, 0,9 vom Rand).</summary>
    public PyObjekt? WareNahe(double x, double z) =>
        SpielKern.Laeden.Contains(Ort)
            ? SpielKern.Auslage(new PyObjekt { { "gebaeude", Gebaeude } })
                .Select(d => (d, a: Abstand(d, x, z))).Where(p => p.a <= SpielKern.MoebelReichweite + 0.3).OrderBy(p => p.a).Select(p => p.d).FirstOrDefault()
            : null;

    private static double Abstand(PyObjekt d, double x, double z)
    {
        double x1 = Py.F(d["x"]), z1 = Py.F(d["y"]), x2 = x1 + Py.F(d["w"]), z2 = z1 + Py.F(d["h"]);
        var dx = Math.Max(Math.Max(x1 - x, 0), x - x2);
        var dz = Math.Max(Math.Max(z1 - z, 0), z - z2);
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    private (string, string, List<(string, string)>) LadenNachricht(double x, double z, ISpielSicht stand)
    {
        var name = Ort switch { "moebelhaus" => "Möbelhaus", "kaufhaus" => "Kaufhaus", _ => "Elektromarkt" };
        var sortiment = (AktionSortiment, "Sortiment");
        if (PersonNahe(x, z) is { } p)
        {
            return (PyFormat.Prozent("%s · %s", p.Name, Get(p.Daten, "rolle", "")), Py.Str(Get(p.Daten, "text", "")), [sortiment]);
        }
        if (WareNahe(x, z) is { } d)
        {
            var ware = Py.Str(d["ware"]);
            if (ware.StartsWith("kleidung:", StringComparison.Ordinal))
            {
                return (SpielKern.Regeln.Abteilung(Kleiderdaten, ware["kleidung:".Length..]), SpielKern.KleidungFuerDieFigur, [(AktionSortiment + ":" + ware["kleidung:".Length..], "Ansehen")]);
            }
            if (ware.StartsWith("teile:", StringComparison.Ordinal))
            {
                var typ = ware["teile:".Length..];
                return (SpielKern.Regeln.TeilName(typ, stand.Inhalt), Py.Wahr(stand.Firma) ? "Teile für die Firma, sofort im Lager." : SpielKern.NurMitFirma,
                    [(AktionSortiment + ":" + typ, "Ansehen")]);
            }
            if (SpielKern.Regeln.MoebelStueck(ware, stand.Inhalt) is { } m)
            {
                var texte = ListeOderLeer(m, "texte");
                var zeilen = PyFormat.Prozent("Preis: %s", SpielKern.Regeln.GanzeEuro(m["preis"]))
                    + (texte.Count > 0 ? "\n" + Py.Str(texte[0]) : "");
                if (Py.Gleich(Get(m, "typ"), SpielKern.SchrankTyp))
                {
                    // Phase 12 Welle 1: Plaetze fuer Kleidung und wozu der Schrank da ist
                    zeilen += "\n" + SpielKern.Regeln.SchrankPlaetzeText(m) + ". " + SpielKern.ImMoebelhaus;
                }
                var knoepfe = new List<(string, string)>();
                if (stand.Geld >= Py.Int(m["preis"]))
                {
                    knoepfe.Add((AktionKaufen + ware, "Kaufen"));
                }
                knoepfe.Add(sortiment);
                return (Py.Str(m["name"]), zeilen, knoepfe);
            }
        }
        // Weisung P12-74: auf einer Etage ohne Verkaufsperson nur der Teil zu den Waren (Erdgeschoss unveraendert)
        var hinweis = Personen.Count == 0 && Stadtbau.Etagen.Teilen(Ansicht).Etage > 0 ? EtageOhneVerkauf
            : "Tippe auf eine Ware oder sprich mit dem Verkauf.";
        return (PyFormat.Prozent("Du bist im %s.", name), hinweis, [sortiment]);
    }

    /// <summary>W7b: Aktion "am Schalter" (Bank: Kredite, Amt: Gruendung oder Steuern).</summary>
    public const string AktionSchalter = "schalter";

    private (string, string, List<(string, string)>) SchalterNachricht(double x, double z, ISpielSicht stand)
    {
        var firma = Py.Wahr(stand.Firma);
        var knopf = (AktionSchalter, Ort == SpielKern.Bank ? "Kredite" : firma ? "Steuern" : "Gründung");
        if (PersonNahe(x, z) is { } p)
        {
            return (PyFormat.Prozent("%s · %s", p.Name, Get(p.Daten, "rolle", "")), Py.Str(Get(p.Daten, "text", "")), [knopf]);
        }
        var person = Personen.Count > 0 ? Personen[0].Name : "";
        return Ort == SpielKern.Bank
            ? ("Du bist in der Bank.", PyFormat.Prozent("Am Schalter hilft dir %s mit Krediten für deine Firma.", person), [knopf])
            : ("Du bist auf dem Amt.", PyFormat.Prozent("Am Schalter hilft dir %s mit der Gründung und der Umsatzsteuer.", person), [knopf]);
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
        /// <summary>Weisung P12-74: Schacht unter einer Treppe nach unten (fast schwarz).</summary>
        public static readonly Vector3 Schacht = Netz.Farbe("#211E2A");
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

    /// <summary>Farbe der Innenwaende (Grundriss "wandfarbe", sonst die Palette).</summary>
    public Vector3 WandFarbe { get; private set; } = Farben.Wand;

    /// <summary>Farbe der Aussenwaende (Grundriss "aussenfarbe", sonst die Palette).</summary>
    public Vector3 AussenFarbe { get; private set; } = Farben.Aussen;

    /// <summary>Bodenfarbe je Raum-id und "flur".</summary>
    public IReadOnlyDictionary<string, Vector3> BodenFarben => _bodenFarben;

    private readonly Dictionary<string, Vector3> _bodenFarben = [];

    private static Vector3? Hex(PyObjekt o, string k) => Get(o, k) is string hex ? Netz.Farbe(hex) : null;
}
