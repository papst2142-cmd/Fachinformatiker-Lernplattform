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
public sealed record InnenWand(int Von, int Bis, Vector3 Min, Vector3 Max, Vector2 Aussen);

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
            var ziele = Geschaefte.Laeden.Contains(ort)
                ? Geschaefte.Auslage(new PyObjekt { { "gebaeude", gebaeude } })
                    .Select(d => (Py.F(d["x"]) + (Py.F(d["w"]) / 2), Py.F(d["y"]) + (Py.F(d["h"]) / 2),
                        Wohnung.MoebelReichweite + 0.3 + (Math.Max(Py.F(d["w"]), Py.F(d["h"])) / 2))).ToList()
                : [];
            _festPlan = FestDeko.InnenPlanen(ort, gebaeude, leute, grafik, feste, muster, ort == OrtZuhause, ziele);
            _festMuster = muster;
        }
        Lauf = _festPlan?.Lauf ?? new LaufOrt(gebaeude, leute);
        Aufbauen(boeden);
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

    /// <summary>Ort (Python site) zur Ansicht eines Stadt-Orts; null = (noch) kein Innenraum.</summary>
    public static string? OrtFuer(string? ansicht) => ansicht switch
    {
        "buero" => OrtBuero,
        "zuhause" => OrtZuhause,
        "filiale" => OrtFiliale,
        Serverfarm => Serverfarm,
        not null when ansicht.StartsWith("kunde:", StringComparison.Ordinal) => ansicht["kunde:".Length..],
        not null when ansicht.StartsWith("geschaeft:", StringComparison.Ordinal) && Geschaefte.Laeden.Contains(ansicht["geschaeft:".Length..]) => ansicht["geschaeft:".Length..],
        not null when ansicht.StartsWith("schalter:", StringComparison.Ordinal) && Schalter.Orte.Contains(ansicht["schalter:".Length..]) => ansicht["schalter:".Length..],
        _ => null,
    };

    /// <summary>
    /// Innenraum zur Ansicht fuer den Spielstand; null, wenn es ihn (noch) nicht gibt (keine Wohnung,
    /// Filiale nicht eroeffnet, Serverfarm ohne innenraeume.json). <paramref name="innenraeume"/>:
    /// Inhalt von innenraeume.json; <paramref name="geschaefte"/>: Inhalt von geschaefte.json (Kleidung im Kaufhaus).
    /// </summary>
    /// <remarks>Phase 8 W9: <paramref name="jahreszeit"/> faerbt die Wiese um das Haus (Sommer = wie vorher).</remarks>
    public static Innenraum? Bauen(string? ansicht, Spielstand stand, PyObjekt? innenraeume, GrafikBibliothek? grafik, PyObjekt? geschaefte = null,
        string? fest = null, PyObjekt? feste = null, Jahreszeit jahreszeit = Jahreszeit.Sommer)
    {
        var raum = BauenOhneJahreszeit(ansicht, stand, innenraeume, grafik, geschaefte, fest, feste);
        raum?.WieseFaerben(jahreszeit);
        return raum;
    }

    /// <summary>Phase 8 W9: Jahreszeit der Wiese um das Haus.</summary>
    public Jahreszeit Jahreszeit { get; private set; } = Jahreszeit.Sommer;

    private int _wiese = -1;

    private void WieseFaerben(Jahreszeit jahreszeit)
    {
        Jahreszeit = jahreszeit;
        if (jahreszeit == Jahreszeit.Sommer || _wiese < 0)
        {
            return;
        }
        var neu = Jahreszeitfarben.Faerben(jahreszeit, Faerbeart.Stadt, Farben.Wiese, 1f, 0f);
        _teile[_wiese] = _teile[_wiese] with { Farbe = Jahreszeitfarben.IstSchnee(neu) ? Jahreszeitfarben.Schnee(0.9f) : Mischen(neu, Vector3.Zero, 0.1f) };
    }

    private static Innenraum? BauenOhneJahreszeit(string? ansicht, Spielstand stand, PyObjekt? innenraeume, GrafikBibliothek? grafik, PyObjekt? geschaefte,
        string? fest, PyObjekt? feste) =>
        EingabenFuer(ansicht, stand, innenraeume, geschaefte) is { } e
            ? new Innenraum(e.Ansicht, e.Ort, e.Gebaeude, e.Personen, grafik, e.Boeden, e.Kleider, fest, feste)
            : null;

    /// <summary>Weisung P8-55 (W12): alles, was ein Innenraum aus dem Spielstand liest (Grundriss, Personen, Boeden, Kleidung).</summary>
    private sealed record Eingaben(string Ansicht, string Ort, PyObjekt Gebaeude, List<InnenPerson> Personen, PyObjekt? Boeden, PyObjekt? Kleider);

    /// <summary>
    /// Weisung P8-55 (W12, Selbstfund 3.4 im Teilplan): Inhalt des Innenraums zur Ansicht als Text, ohne ihn zu bauen
    /// (Grundriss, Personen mit Auftrag, Boeden, Festzeit, Jahreszeit). Gleicher Text = gleicher Innenraum: so erkennt
    /// das Spiel einen vorgeladenen Innenraum wieder, auch wenn ein Ereignis den Spielstand ersetzt hat (vorher galt nur
    /// derselbe Spielstand). null = kein Innenraum (oder nicht als Text darstellbar, dann wird immer neu gebaut).
    /// </summary>
    public static string? InhaltSignatur(string? ansicht, Spielstand stand, PyObjekt? innenraeume, PyObjekt? geschaefte, string? fest, Jahreszeit jahreszeit)
    {
        if (EingabenFuer(ansicht, stand, innenraeume, geschaefte) is not { } e)
        {
            return null;
        }
        try
        {
            return PythonJson.Text(new List<object?>
            {
                e.Ansicht, e.Ort, e.Gebaeude, e.Personen.Select(p => (object?)new List<object?> { p.Daten, p.Auftrag }).ToList(),
                e.Boeden, e.Kleider is not null, fest, jahreszeit.ToString(),
            });
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static Eingaben? EingabenFuer(string? ansicht, Spielstand stand, PyObjekt? innenraeume, PyObjekt? geschaefte)
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
            return new Eingaben(ansicht!, ort, Objekt(farm["gebaeude"]), personen, null, null);
        }
        if (Geschaefte.Laeden.Contains(ort) || Schalter.Orte.Contains(ort))
        {
            // W7: Moebelhaus und Elektromarkt (Grundriss, Auslage und Verkaeufer aus innenraeume.json),
            // W7b: Bank und Amt (Grundriss, Schalter und Person ebenso)
            if ((innenraeume is null ? null : Get(innenraeume, ort)) is not PyObjekt laden)
            {
                return null;
            }
            var verkauf = ListeOderLeer(laden, "personen").Select(p => Person(Objekt(p), false)).ToList();
            var zusatz = Get(innenraeume!, "zusatz") is PyObjekt z ? Get(z, ort) as PyObjekt : null;
            return new Eingaben(ansicht!, ort, MitZusatz(Objekt(laden["gebaeude"]), zusatz), verkauf, null, geschaefte);
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
        // W8b (Nico 2a): Zusatz fuer Kundenorte (Bahnhof Talheim innen als Wartehalle), angehaengt in innenraeume.json
        if (innenraeume is not null && Get(innenraeume, "zusatz_kunden") is PyObjekt zk && Get(zk, ort) is PyObjekt zo)
        {
            gebaeude = MitZusatz(gebaeude, zo);
        }
        var auftraege = ort == OrtZuhause ? new PyDict() : stand.Auftraege(ort);
        var leute = ort == OrtZuhause ? [] : SpielOrte.Personen(ort, stand)
            .Select(p => Person(p, auftraege.Get(p["id"]) is List<object?> { Count: > 0 })).ToList();
        var boeden = ort == OrtZuhause ? Objekt(Wohnung.Einrichtung(stand)["boeden"]) : null;
        return new Eingaben(ansicht!, ort, gebaeude, leute, boeden, null);
    }

    /// <summary>
    /// Grundriss mit dem angehaengten Zusatz aus innenraeume.json ("zusatz" je Ort, P7-15): Werte wie
    /// wandfarbe, aussenfarbe, boden, bodenfarbe ersetzen die des Grundrisses, Einrichtung ("deko") kommt
    /// hinzu, Raeume nach ihrer id; "ohne" nimmt Einrichtung dieser Typen heraus (W8b). Der Grundriss selbst
    /// bleibt unveraendert (Kopie).
    /// </summary>
    public static PyObjekt MitZusatz(PyObjekt gebaeude, PyObjekt? zusatz)
    {
        if (zusatz is null)
        {
            return gebaeude;
        }
        var neu = (PyObjekt)Py.Kopie(gebaeude)!;
        static void Einmischen(PyObjekt ziel, PyObjekt dazu)
        {
            foreach (var (k, v) in dazu)
            {
                if (k == "ohne")
                {
                    var weg = Liste(v).Select(Py.Str).ToHashSet();
                    Py.Setzen(ziel, "deko", (Get(ziel, "deko") as List<object?> ?? []).Where(d => !weg.Contains(Py.Str(Objekt(d)["typ"]))).ToList());
                }
                else if (k == "deko")
                {
                    var liste = Get(ziel, "deko") as List<object?> ?? [];
                    liste.AddRange(Liste(Py.Kopie(v)));
                    Py.Setzen(ziel, "deko", liste);
                }
                else if (!k.StartsWith('_'))
                {
                    Py.Setzen(ziel, k, Py.Kopie(v));
                }
            }
        }
        foreach (var (k, v) in zusatz)
        {
            if (k == "flur")
            {
                Einmischen(Objekt(neu["flur"]), Objekt(v));
            }
            else if (k == "raeume")
            {
                foreach (var (id, raumZusatz) in Objekt(v))
                {
                    var raum = Liste(neu["raeume"]).Select(Objekt).First(r => Py.Gleich(r["id"], id));
                    Einmischen(raum, Objekt(raumZusatz));
                }
            }
            else if (!k.StartsWith('_'))
            {
                Py.Setzen(neu, k, Py.Kopie(v));
            }
        }
        return neu;
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
        if (Geschaefte.Laeden.Contains(Ort))
        {
            return LadenNachricht(x, z, stand);
        }
        if (Schalter.Orte.Contains(Ort))
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
            return (wo, Betrieb.FarmOrtText(stand, stand.Inhalt), []);
        }
        return SpielOrte.Nachricht(Ort, x, z, PersonNahe(x, z)?.Daten, stand, Gebaeude);
    }

    /// <summary>W7: Aktion "Sortiment" (alle Waren des Geschaefts) und "Sortiment" eines Teile-Typs.</summary>
    public const string AktionSortiment = "sortiment";

    /// <summary>W7: Aktion "Kaufen" eines Moebelstuecks ("kaufen:&lt;moebel&gt;").</summary>
    public const string AktionKaufen = "kaufen:";

    /// <summary>W7: Ware der Auslage an der Stelle (Tippen), etwas grosszuegiger als ihr Umriss.</summary>
    public PyObjekt? WareBei(double x, double z) =>
        Geschaefte.Laeden.Contains(Ort)
            ? Geschaefte.Auslage(new PyObjekt { { "gebaeude", Gebaeude } }).FirstOrDefault(d => Abstand(d, x, z) <= 0.2)
            : null;

    /// <summary>W7: naechste Ware der Auslage in Reichweite der Figur (wie furniture_near, 0,9 vom Rand).</summary>
    public PyObjekt? WareNahe(double x, double z) =>
        Geschaefte.Laeden.Contains(Ort)
            ? Geschaefte.Auslage(new PyObjekt { { "gebaeude", Gebaeude } })
                .Select(d => (d, a: Abstand(d, x, z))).Where(p => p.a <= Wohnung.MoebelReichweite + 0.3).OrderBy(p => p.a).Select(p => p.d).FirstOrDefault()
            : null;

    private static double Abstand(PyObjekt d, double x, double z)
    {
        double x1 = Py.F(d["x"]), z1 = Py.F(d["y"]), x2 = x1 + Py.F(d["w"]), z2 = z1 + Py.F(d["h"]);
        var dx = Math.Max(Math.Max(x1 - x, 0), x - x2);
        var dz = Math.Max(Math.Max(z1 - z, 0), z - z2);
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    private (string, string, List<(string, string)>) LadenNachricht(double x, double z, Spielstand stand)
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
                return (Kleidung.Abteilung(Kleiderdaten, ware["kleidung:".Length..]), Kleidung.FuerDieFigur, [(AktionSortiment + ":" + ware["kleidung:".Length..], "Ansehen")]);
            }
            if (ware.StartsWith("teile:", StringComparison.Ordinal))
            {
                var typ = ware["teile:".Length..];
                return (Geschaefte.TypName(typ, stand.Inhalt), Py.Wahr(stand.Firma) ? "Teile für die Firma, sofort im Lager." : Geschaefte.NurMitFirma,
                    [(AktionSortiment + ":" + typ, "Ansehen")]);
            }
            if (Wohnung.MoebelStueck(ware, stand.Inhalt) is { } m)
            {
                var texte = ListeOderLeer(m, "texte");
                var zeilen = PyFormat.Prozent("Preis: %s", Wohnung.GanzeEuro(m["preis"]))
                    + (texte.Count > 0 ? "\n" + Py.Str(texte[0]) : "");
                var knoepfe = new List<(string, string)>();
                if (stand.Geld >= Py.Int(m["preis"]))
                {
                    knoepfe.Add((AktionKaufen + ware, "Kaufen"));
                }
                knoepfe.Add(sortiment);
                return (Py.Str(m["name"]), zeilen, knoepfe);
            }
        }
        return (PyFormat.Prozent("Du bist im %s.", name), "Tippe auf eine Ware oder sprich mit dem Verkauf.", [sortiment]);
    }

    /// <summary>W7b: Aktion "am Schalter" (Bank: Kredite, Amt: Gruendung oder Steuern).</summary>
    public const string AktionSchalter = "schalter";

    private (string, string, List<(string, string)>) SchalterNachricht(double x, double z, Spielstand stand)
    {
        var firma = Py.Wahr(stand.Firma);
        var knopf = (AktionSchalter, Ort == Schalter.Bank ? "Kredite" : firma ? "Steuern" : "Gründung");
        if (PersonNahe(x, z) is { } p)
        {
            return (PyFormat.Prozent("%s · %s", p.Name, Get(p.Daten, "rolle", "")), Py.Str(Get(p.Daten, "text", "")), [knopf]);
        }
        var person = Personen.Count > 0 ? Personen[0].Name : "";
        return Ort == Schalter.Bank
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

    private void Aufbauen(PyObjekt? boeden)
    {
        var signatur = new List<string>();
        WandFarbe = Hex(Gebaeude, "wandfarbe") ?? Farben.Wand;
        AussenFarbe = Hex(Gebaeude, "aussenfarbe") ?? Farben.Aussen;
        // Wiese um das Haus, damit die geneigte Kamera keinen leeren Himmel zeigt
        _wiese = _teile.Count;
        _teile.Add(Quader(-20, -0.08f, -20, Breite + 20, -0.02f, Tiefe + 20, Mischen(Farben.Wiese, Vector3.Zero, 0.1f)));
        var flur = Objekt(Gebaeude["flur"]);
        _teile.Add(Quader(0, -0.02f, 0, Breite, 0.02f, Tiefe, Farben.Boden));
        _bodenFarben["flur"] = Flaeche(0, (float)Py.F(flur["y"]), Breite, (float)Py.F(flur["h"]), Get(flur, "boden") as string, null, null, 0.03f, Hex(flur, "bodenfarbe"));
        foreach (var raum in Liste(Gebaeude["raeume"]).Select(Objekt))
        {
            var boden = Get(raum, "boden") as string;
            Vector3? akzent = Get(raum, "farbe") is string hex ? Netz.Farbe(hex) : null;
            Vector3? wahl = null;
            if (boeden is not null && Get(boeden, Py.Str(raum["id"])) is List<object?> eintrag && eintrag.Count > 1)
            {
                wahl = BodenAkzent(eintrag[1]);
            }
            _bodenFarben[Py.Str(raum["id"])] = Flaeche((float)Py.F(raum["x"]), (float)Py.F(raum["y"]), (float)Py.F(raum["w"]), (float)Py.F(raum["h"]), boden, akzent, wahl, 0.035f, Hex(raum, "bodenfarbe"));
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
        if (Fest is not null && Ort == Kleidung.Laden)
        {
            // Phase 8 W6: die ersten Puppen im Flur tragen die Festkleidung
            _festPuppen = [.. ListeOderLeer(flur, "deko").Select(Objekt).Where(d => Py.Gleich(d["typ"], "puppe")).Take(FestDeko.Puppen(_festDaten))];
        }
        foreach (var d in deko)
        {
            Deko(d);
            signatur.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{d["typ"]}@{Py.F(d["x"])},{Py.F(d["y"])},{Py.Int(Get(d, "dreh", 0L))}"));
        }
        if (_festPlan is not null && _festMuster is not null)
        {
            FestDeko.InnenZeichnen(_festPlan, _festMuster, _teile, _stuecke, Grafik, Lauf.Basis);
        }
        if (Fest is not null)
        {
            // Phase 8 W6: nur angehaengt, damit ein Wechsel der Festzeit neu laedt
            signatur.Add("fest:" + Fest + string.Concat(_festPlan?.Plaetze.Select(p => string.Create(System.Globalization.CultureInfo.InvariantCulture, $",{p.Platz}@{p.X}/{p.Z}")) ?? []));
        }
        Signatur = string.Join(";", signatur);
    }

    /// <summary>
    /// Boden einer Flaeche: Art (parkett, teppichboden, fliesen, beton), Raumfarbe als Hauch, Fliesen mit
    /// Fugen; <paramref name="fest"/> (bodenfarbe) ersetzt die Farbe der Art. Gibt die Bodenfarbe zurueck.
    /// </summary>
    private Vector3 Flaeche(float x, float z, float w, float t, string? boden, Vector3? raumFarbe, Vector3? wahl, float hoehe, Vector3? fest = null)
    {
        var farbe = boden switch
        {
            "parkett" => Farben.Parkett,
            "teppichboden" => raumFarbe is { } r ? Mischen(Farben.Teppich, r, 0.35f) : Farben.Teppich,
            "fliesen" => Farben.Fliesen,
            "beton" => Farben.Beton,
            _ => raumFarbe is { } r2 ? Mischen(Farben.Boden, r2, 0.18f) : Farben.Boden,
        };
        if (fest is { } eigene)
        {
            farbe = eigene;
        }
        if (wahl is { } akzent)
        {
            farbe = Mischen(farbe, akzent, 0.3f);
        }
        _teile.Add(Quader(x, 0.02f, z, x + w, hoehe, z + t, farbe));
        if (boden == "fliesen")
        {
            for (var fx = x + 0.6f; fx < x + w - 0.05f; fx += 0.6f)
            {
                _teile.Add(Quader(fx - 0.012f, hoehe, z, fx + 0.012f, hoehe + 0.002f, z + t, fest is null ? Farben.Fuge : Mischen(farbe, Vector3.Zero, 0.12f)));
            }
            for (var fz = z + 0.6f; fz < z + t - 0.05f; fz += 0.6f)
            {
                _teile.Add(Quader(x, hoehe, fz - 0.012f, x + w, hoehe + 0.002f, fz + 0.012f, fest is null ? Farben.Fuge : Mischen(farbe, Vector3.Zero, 0.12f)));
            }
        }
        else if (boden == "parkett")
        {
            for (var fz = z + 0.35f; fz < z + t - 0.05f; fz += 0.35f)
            {
                _teile.Add(Quader(x, hoehe, fz - 0.008f, x + w, hoehe + 0.002f, fz + 0.008f, Mischen(farbe, Vector3.Zero, 0.15f)));
            }
        }
        return farbe;
    }

    private void Wand(float x1, float z1, float x2, float z2)
    {
        if (x2 - x1 < 1e-4f || z2 - z1 < 1e-4f)
        {
            return;
        }
        _teile.Add(Quader(x1, 0, z1, x2, WandHoehe - 0.06f, z2, WandFarbe));
        _teile.Add(Quader(x1, WandHoehe - 0.06f, z1, x2, WandHoehe, z2, WandFarbe == Farben.Wand ? Farben.WandKante : Mischen(WandFarbe, Vector3.Zero, 0.2f)));
        _waende.Add(new InnenWand(_teile.Count - 2, _teile.Count, new Vector3(x1, 0, z1), new Vector3(x2, WandHoehe, z2), Vector2.Zero));
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
            var erstes = _teile.Count;
            foreach (var (von, bis, art) in oeffnungen)
            {
                Stueck(pos, Math.Max(pos, von), 0, WandHoehe, AussenFarbe);
                switch (art)
                {
                    case 'f':
                        Stueck(von, bis, 0, 0.9f * Meter, AussenFarbe);
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
            Stueck(pos, ende, 0, WandHoehe, AussenFarbe);
            // die ganze Seite als ein Wandstueck; nach draussen zeigt -innenSeite (oben/links: innenSeite 1)
            var aussen = waagerecht ? new Vector2(0, -innenSeite) : new Vector2(-innenSeite, 0);
            _waende.Add(new InnenWand(erstes, _teile.Count,
                waagerecht ? new Vector3(-d, 0, a0) : new Vector3(a0, 0, 0),
                waagerecht ? new Vector3(laenge + d, WandHoehe, a1) : new Vector3(a1, WandHoehe, laenge), aussen));
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
        "werkbank", "leitstand", "bank", "glaswand", "kleiderregal", "spiegel", "schliessfaecher", "tresor", "geldautomat", "wartemarken", "wappen", "gemaelde",
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
            case "kassentisch":
                // Kaufhaus (Weisung P8-8c): freistehende kleine Kasse (Tisch mit Kasse und Bildschirm), keine Tresenwand
                Modell(r, "table", 0, 0, b, tt, 0.95f * M, 0, Farben.Hell);
                Kasten(r, (b / 2) - 0.25f, (tt / 2) - 0.2f, 0.5f, 0.4f, 0.95f * M, 1.1f * M, Farben.Geraet);
                Kasten(r, (b / 2) - 0.2f, (tt / 2) - 0.25f, 0.4f, 0.05f, 1.1f * M, 1.35f * M, Farben.Geraet);
                break;
            case "kleiderstaender":
                Kleiderstaender(r, b, tt, Get(d, "ware") as string);
                break;
            case "kleiderregal":
                Modell(r, "bookcaseOpen", 0, 0, b, tt, 1.8f * M, 0, Farben.Hell);
                Kleiderfaecher(r, b, tt, Get(d, "ware") as string);
                break;
            case "spiegel":
                Kasten(r, 0, 0, b, tt, 0.1f * M, 1.9f * M, Farben.HolzDunkel);
                Kasten(r, 0.06f, tt, b - 0.12f, 0.01f, 0.15f * M, 1.85f * M, Netz.Farbe("#CFE8F5"));
                break;
            case "umkleide":
                Umkleide(r, b, tt);
                break;
            case "puppe":
                Puppe(x, z, w, t, dreh, PuppenKleidung(d));
                break;
            case "aktenregal":
                Modell(r, "bookcaseClosedWide", 0, 0, b, tt, 1.8f * M, 0, Farben.HolzDunkel);
                break;
            case "regal":
                Modell(r, "bookcaseOpen", 0, 0, b, tt, 1.8f * M, 0, Farben.HolzDunkel);
                // Nachtrag 1 zu P7-13 (Wunsch 5): volle Regale im Laden, die Ware je Art sichtbar
                Regalware(r, b, tt, Get(d, "ware") as string);
                break;
            case "auslage":
                // Praesentationstisch im Laden (Nachtrag 1 zu P7-13, Wunsch 5): Ware obenauf
                Modell(r, "table", 0, 0, b, tt, 0.75f * M, 0, Farben.Hell);
                Auslage(r, b, tt, Get(d, "ware") as string);
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
            case "stuhlreihe":
            {
                // Wartestuehle nebeneinander (KayKit, P7-15 Nachtrag 1), so viele, wie in die Breite passen
                var n = Math.Max(1, (int)(b / 0.8f));
                for (var i = 0; i < n; i++)
                {
                    Modell(r, "kkChairA", (b * i / n) + (b / n / 2) - 0.32f, 0, 0.64f, tt, 0.95f * M, 0, Farben.Polster);
                }
                break;
            }
            case "wartesessel":
                Modell(r, "kkArmchair", 0, 0, b, tt, 0.85f * M, 0, Farben.Polster);
                break;
            case "bahnhofsuhr":
                Bahnhofsuhr(r, b, tt);
                break;
            case "gemaelde":
                Modell(r, "kkPictureframeLargeB", 0, 0, b, Math.Min(tt, 0.12f), 0.6f * M, 1.0f * M, Netz.Farbe("#F472B6"));
                break;
            case "platznummer":
                Platznummer(r, b, tt, (int)Py.Int(Get(d, "nummer", 1L)), (float)Py.F(Get(d, "unten", 0.75)) * M);
                break;
            default:
                Eigen(typ, r, b, tt);
                break;
        }
    }

    /// <summary>Aussehen einer Teile-Art im Laden: Breite und Hoehe eines Stuecks, zwei Farben im Wechsel.</summary>
    private static (float Breite, float Hoehe, Vector3 Farbe, Vector3 Zweite) Warenform(string? ware) => ware switch
    {
        "teile:mainboard" => (0.42f, 0.1f, Netz.Farbe("#166534"), Netz.Farbe("#1E3A8A")),
        "teile:cpu" => (0.2f, 0.22f, Netz.Farbe("#DC2626"), Netz.Farbe("#2563EB")),
        "teile:ram" => (0.28f, 0.12f, Netz.Farbe("#15803D"), Netz.Farbe("#111827")),
        "teile:ssd" => (0.16f, 0.1f, Netz.Farbe("#1F2937"), Netz.Farbe("#7C3AED")),
        "teile:gpu" => (0.5f, 0.2f, Netz.Farbe("#111827"), Netz.Farbe("#4B5563")),
        "teile:netzteil" => (0.28f, 0.24f, Netz.Farbe("#374151"), Netz.Farbe("#1F2937")),
        "teile:gehaeuse" => (0.42f, 0.7f, Netz.Farbe("#1F2937"), Netz.Farbe("#E5E7EB")),
        "teile:notebook" => (0.5f, 0.06f, Netz.Farbe("#9CA3AF"), Netz.Farbe("#1F2937")),
        "teile:usv_akku" => (0.24f, 0.26f, Netz.Farbe("#111827"), Netz.Farbe("#B91C1C")),
        "teile:monitor" => (0.55f, 0.42f, Netz.Farbe("#1F2937"), Netz.Farbe("#374151")),
        "teile:patchkabel" => (0.2f, 0.16f, Netz.Farbe("#FACC15"), Netz.Farbe("#2563EB")),
        "teile:luefter" => (0.2f, 0.2f, Netz.Farbe("#1F2937"), Netz.Farbe("#0EA5E9")),
        "teile:toner" => (0.38f, 0.18f, Netz.Farbe("#111827"), Netz.Farbe("#DB2777")),
        "teile:sfp" => (0.12f, 0.06f, Netz.Farbe("#9CA3AF"), Netz.Farbe("#2563EB")),
        "teile:drucker" => (0.55f, 0.38f, Netz.Farbe("#E5E7EB"), Netz.Farbe("#6B7280")),
        "regal" => (0.07f, 0.42f, Netz.Farbe("#B91C1C"), Netz.Farbe("#1D4ED8")),
        _ => (0.3f, 0.26f, Farben.Karton, Mischen(Farben.Karton, Vector3.Zero, 0.2f)),
    };

    /// <summary>Ware in den drei Boeden eines offenen Regals (ohne Ware bleibt es leer, z. B. im Buero).</summary>
    private void Regalware(Rahmen r, float b, float t, string? ware)
    {
        if (ware is null)
        {
            return;
        }
        var (breite, hoehe, farbe, zweite) = Warenform(ware);
        var tief = Math.Min(0.42f, t * 0.7f);
        var boden = 1.8f * Meter;
        var anzahl = Math.Max(1, (int)((b - 0.12f) / (breite + 0.05f)));
        var abstand = (b - 0.12f - (anzahl * breite)) / Math.Max(1, anzahl);
        var nr = 0;
        foreach (var anteil in (ReadOnlySpan<float>)[0.03f, 0.36f, 0.69f])
        {
            var unten = boden * anteil;
            var oben = Math.Min(unten + hoehe, unten + (boden * 0.3f));
            for (var i = 0; i < anzahl; i++)
            {
                var lx = 0.06f + (abstand / 2) + (i * (breite + abstand));
                Kasten(r, lx, t - tief - 0.04f, breite, tief, unten, oben, nr++ % 3 == 1 ? zweite : farbe);
            }
        }
    }

    /// <summary>Ware auf einem Auslagetisch: eine Reihe (Notebooks aufgeklappt, Monitore mit Bild).</summary>
    private void Auslage(Rahmen r, float b, float t, string? ware)
    {
        var (breite, hoehe, farbe, zweite) = Warenform(ware);
        var tisch = 0.75f * Meter;
        if (ware is not null && ware.StartsWith("kleidung:", StringComparison.Ordinal))
        {
            // Kaufhaus: Kappen und Muetzen in den Farben des Sortiments
            var farben = Kleidung.Farben(Kleiderdaten, ware);
            var n = Math.Max(1, (int)((b - 0.2f) / 0.32f));
            for (var i = 0; i < n; i++)
            {
                var lx = 0.1f + (i * (b - 0.2f) / n);
                Kasten(r, lx, (t / 2) - 0.12f, 0.24f, 0.24f, tisch, tisch + 0.12f, farben[i % farben.Count]);
                Kasten(r, lx + 0.04f, (t / 2) + 0.12f, 0.16f, 0.1f, tisch, tisch + 0.03f, farben[i % farben.Count] * 0.75f);
            }
            return;
        }
        var anzahl = Math.Max(1, (int)((b - 0.2f) / (breite + 0.15f)));
        var abstand = (b - 0.2f - (anzahl * breite)) / Math.Max(1, anzahl);
        for (var i = 0; i < anzahl; i++)
        {
            var lx = 0.1f + (abstand / 2) + (i * (breite + abstand));
            if (ware == "teile:notebook")
            {
                // Unterteil flach, Deckel hochkant mit Bildschirm
                Kasten(r, lx, (t / 2) - 0.05f, breite, 0.32f, tisch, tisch + 0.04f, i % 2 == 0 ? farbe : zweite);
                Kasten(r, lx, (t / 2) - 0.09f, breite, 0.04f, tisch, tisch + 0.32f, i % 2 == 0 ? farbe : zweite);
                Kasten(r, lx + 0.03f, (t / 2) - 0.05f, breite - 0.06f, 0.005f, tisch + 0.05f, tisch + 0.29f, Farben.Bildschirm);
            }
            else if (ware == "teile:monitor")
            {
                Kasten(r, lx + (breite * 0.4f), (t / 2) - 0.05f, breite * 0.2f, 0.12f, tisch, tisch + 0.12f, zweite);
                Kasten(r, lx, (t / 2) - 0.05f, breite, 0.05f, tisch + 0.12f, tisch + 0.12f + hoehe, farbe);
                Kasten(r, lx + 0.03f, t / 2, breite - 0.06f, 0.005f, tisch + 0.15f, tisch + 0.09f + hoehe, Farben.Bildschirm);
            }
            else
            {
                Kasten(r, lx, (t / 2) - 0.15f, breite, 0.3f, tisch, tisch + hoehe, i % 3 == 1 ? zweite : farbe);
            }
        }
    }

    /// <summary>Kaufhaus (Phase 8 W5): Kleiderstange auf zwei Fuessen, Oberteile (Hosen) haengend in den Farben des Sortiments.</summary>
    private void Kleiderstaender(Rahmen r, float b, float t, string? ware)
    {
        var stange = 1.5f * Meter;
        Kasten(r, 0.02f, (t / 2) - 0.03f, 0.06f, 0.06f, 0, stange, Farben.Metall);
        Kasten(r, b - 0.08f, (t / 2) - 0.03f, 0.06f, 0.06f, 0, stange, Farben.Metall);
        Kasten(r, 0, (t / 2) - 0.2f, 0.1f, 0.4f, 0, 0.04f, Farben.Metall);
        Kasten(r, b - 0.1f, (t / 2) - 0.2f, 0.1f, 0.4f, 0, 0.04f, Farben.Metall);
        Kasten(r, 0, (t / 2) - 0.02f, b, 0.04f, stange - 0.04f, stange, Farben.Metall);
        var farben = Kleidung.Farben(Kleiderdaten, ware);
        var anzahl = Math.Max(1, (int)((b - 0.3f) / 0.12f));
        for (var i = 0; i < anzahl; i++)
        {
            var lx = 0.15f + (i * (b - 0.3f) / anzahl);
            var lang = ware == "kleidung:hose" ? 0.95f : 0.7f;
            Kasten(r, lx, (t / 2) - 0.22f, 0.05f, 0.44f, stange - (lang * Meter), stange - 0.06f, farben[i % farben.Count]);
        }
    }

    /// <summary>Kaufhaus (Phase 8 W5): gefaltete Stapel (Hosen) oder Schuhkartons und Schuhe in drei Boeden.</summary>
    private void Kleiderfaecher(Rahmen r, float b, float t, string? ware)
    {
        var farben = Kleidung.Farben(Kleiderdaten, ware);
        var schuhe = ware == "kleidung:schuhe";
        var (breite, hoehe) = schuhe ? (0.28f, 0.12f) : (0.36f, 0.16f);
        var tief = Math.Min(0.4f, t * 0.7f);
        var boden = 1.8f * Meter;
        var anzahl = Math.Max(1, (int)((b - 0.12f) / (breite + 0.06f)));
        var abstand = (b - 0.12f - (anzahl * breite)) / anzahl;
        var nr = 0;
        foreach (var anteil in (ReadOnlySpan<float>)[0.03f, 0.36f, 0.69f])
        {
            var unten = boden * anteil;
            for (var i = 0; i < anzahl; i++)
            {
                var lx = 0.06f + (abstand / 2) + (i * (breite + abstand));
                var f = farben[nr++ % farben.Count];
                Kasten(r, lx, t - tief - 0.04f, breite, tief, unten, unten + hoehe, f);
                if (!schuhe)
                {
                    Kasten(r, lx, t - tief - 0.04f, breite, tief, unten + hoehe + 0.01f, unten + (hoehe * 2), f * 0.85f);
                }
            }
        }
    }

    /// <summary>Kaufhaus (Phase 8 W5): Umkleidekabine mit drei Waenden und Vorhang (halb offen).</summary>
    private void Umkleide(Rahmen r, float b, float t)
    {
        var hoch = 2.0f * Meter;
        var wand = Netz.Farbe("#E8E2F4");
        Kasten(r, 0, 0, b, 0.06f, 0, hoch, wand);
        Kasten(r, 0, 0, 0.06f, t, 0, hoch, wand);
        Kasten(r, b - 0.06f, 0, 0.06f, t, 0, hoch, wand);
        Kasten(r, 0, t - 0.04f, b, 0.04f, hoch - 0.05f, hoch, Farben.Metall);
        Kasten(r, 0.06f, t - 0.06f, b * 0.55f, 0.04f, 0.15f * Meter, hoch - 0.05f, Netz.Farbe("#DB2777"));
        Kasten(r, 0.12f, 0.08f, 0.5f, 0.3f, 0, 0.45f * Meter, Farben.Holz);
    }

    private List<PyObjekt> _festPuppen = [];

    /// <summary>
    /// Kleidung einer Puppe; in der Festzeit tragen die ersten Puppen im Flur (festzeiten.json innen.puppen) Kopf und
    /// Oberteil der Festkleidung, der Rest bleibt.
    /// </summary>
    private List<string> PuppenKleidung(PyObjekt d)
    {
        var kleidung = ListeOderLeer(d, "kleidung").Select(Py.Str).ToList();
        if (Kleiderdaten is null || !_festPuppen.Any(p => ReferenceEquals(p, d)))
        {
            return kleidung;
        }
        var fest = Kleidung.Alle(Kleiderdaten).Where(k => k.Festzeit == Fest && k.Slot is ("oberteil" or "kopf")).ToList();
        return [.. fest.Select(k => k.Id), .. kleidung.Where(id => Kleidung.Finden(Kleiderdaten, id) is { } k && !fest.Any(f => f.Slot == k.Slot))];
    }

    /// <summary>Kaufhaus (Phase 8 W5): Schaufensterpuppe (die Figur ohne Gesichtsfarbe) mit Kleidung aus dem Sortiment.</summary>
    private void Puppe(float x, float z, float w, float t, int dreh, List<string> kleidung)
    {
        var stuecke = kleidung.Select(id => Kleidung.FigurStueck(Kleiderdaten, id)).OfType<Figur.Stueck>().ToList();
        var aussehen = new PyObjekt { { "haut", "hell" }, { "frisur", "glatze" }, { "haarfarbe", "grau" }, { "oberteil", "cyan" } };
        var netz = Figur.Netz(Grafik, aussehen, FigurHoehe * 0.98f, stuecke);
        var lage = Matrix4x4.CreateRotationY(-MathF.PI / 2 * dreh) * Matrix4x4.CreateTranslation(x + (w / 2), 0.1f, z + (t / 2));
        _puppen.Add((netz, lage));
        _teile.Add(Quader(x + (w / 2) - 0.25f, 0, z + (t / 2) - 0.25f, x + (w / 2) + 0.25f, 0.1f, z + (t / 2) + 0.25f, Farben.Metall));
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

    /// <summary>Ziffern als Siebensegment (a oben, b rechts oben, c rechts unten, d unten, e links unten, f links oben, g Mitte).</summary>
    private static readonly string[] Segmente = ["abcdef", "bc", "abdeg", "abcdg", "bcfg", "acdfg", "acdefg", "abc", "abcdefg", "abcdfg"];

    /// <summary>Nummernschild eines Schalterplatzes (P7-15): Schild auf dem Tisch, Ziffer zur Vorderseite.</summary>
    private void Platznummer(Rahmen r, float b, float t, int nummer, float unten)
    {
        var tafel = Netz.Farbe("#1D4ED8");
        Kasten(r, b * 0.3f, 0, b * 0.4f, t, unten, unten + 0.06f, Farben.Geraet);
        float hoehe = 0.42f, tafelUnten = unten + 0.06f;
        Kasten(r, 0, 0, b, t, tafelUnten, tafelUnten + hoehe, tafel);
        float s = 0.035f, zb = Math.Min(b * 0.5f, 0.2f), zh = hoehe * 0.7f;
        float lx = (b - zb) / 2, y0 = tafelUnten + ((hoehe - zh) / 2), y1 = y0 + (zh / 2), y2 = y0 + zh;
        foreach (var seg in Segmente[((nummer % 10) + 10) % 10])
        {
            var (a, bis, von, ober) = seg switch
            {
                'a' => (lx, lx + zb, y2 - s, y2),
                'b' => (lx + zb - s, lx + zb, y1, y2),
                'c' => (lx + zb - s, lx + zb, y0, y1),
                'd' => (lx, lx + zb, y0, y0 + s),
                'e' => (lx, lx + s, y0, y1),
                'f' => (lx, lx + s, y1, y2),
                _ => (lx, lx + zb, y1 - (s / 2), y1 + (s / 2)),
            };
            Kasten(r, a, t, bis - a, 0.015f, von, ober, Farben.Weiss);
        }
    }

    /// <summary>Grosse Bahnhofsuhr am Mast (W8b): runde Scheibe aus Streifen, beidseitig, Zeiger auf 10:10.</summary>
    private void Bahnhofsuhr(Rahmen r, float b, float t)
    {
        const float M = Meter;
        var mitteX = b / 2;
        var mitteZ = t / 2;
        var radius = 0.9f * M;
        var hoehe = 3.0f * M;
        Kasten(r, mitteX - 0.06f, mitteZ - 0.06f, 0.12f, 0.12f, 0, hoehe - radius, Farben.MetallDunkel);
        const int streifen = 14;
        for (var i = 0; i < streifen; i++)
        {
            var y1 = -radius + (2 * radius * i / streifen);
            var y2 = -radius + (2 * radius * (i + 1) / streifen);
            var ym = (y1 + y2) / 2;
            var halb = MathF.Sqrt(MathF.Max(0, (radius * radius) - (ym * ym)));
            var innen = MathF.Sqrt(MathF.Max(0, (radius * 0.86f * (radius * 0.86f)) - (ym * ym)));
            Kasten(r, mitteX - halb, mitteZ - 0.07f, 2 * halb, 0.14f, hoehe + y1, hoehe + y2, Farben.MetallDunkel);
            if (innen > 0)
            {
                Kasten(r, mitteX - innen, mitteZ - 0.085f, 2 * innen, 0.17f, hoehe + y1, hoehe + y2, Farben.Weiss);
            }
        }
        // Zeiger (10:10), roter Sekundenzeiger und Striche bei 12, 3, 6, 9
        Kasten(r, mitteX - 0.03f, mitteZ - 0.1f, 0.06f, 0.2f, hoehe, hoehe + (radius * 0.75f), Farben.Geraet);
        Kasten(r, mitteX - (radius * 0.55f), mitteZ - 0.1f, radius * 0.55f, 0.2f, hoehe - 0.03f, hoehe + 0.03f, Farben.Geraet);
        Kasten(r, mitteX - 0.02f, mitteZ - 0.11f, 0.04f, 0.22f, hoehe - (radius * 0.8f), hoehe, Farben.Rot);
        foreach (var (dx, dy) in new[] { (0f, 0.78f), (0.78f, 0f), (0f, -0.78f), (-0.78f, 0f) })
        {
            Kasten(r, mitteX + (dx * radius) - 0.025f, mitteZ - 0.095f, 0.05f, 0.19f, hoehe + (dy * radius) - 0.025f, hoehe + (dy * radius) + 0.025f, Farben.Geraet);
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
            case "glaswand":
            {
                // Glas auf dem Tresen (P7-15): nur Rahmen und Glaskante, damit die Person dahinter sichtbar bleibt
                float unten = 1.05f * M, oben = 1.6f * M;
                Kasten(r, 0, 0, b, t, unten, unten + 0.04f, Farben.Glas);
                Kasten(r, 0, 0, b, t, oben - 0.05f, oben, Farben.Rahmen);
                var n = Math.Max(2, (int)MathF.Round(b / 1.1f));
                for (var i = 0; i <= n; i++)
                {
                    Kasten(r, (b - 0.06f) * i / n, 0, 0.06f, t, unten, oben, Farben.Rahmen);
                }
                // Durchreiche in der Mitte
                Kasten(r, (b / 2) - 0.4f, 0, 0.8f, t + 0.06f, unten, unten + 0.02f, Farben.MetallDunkel);
                break;
            }
            case "schliessfaecher":
            {
                var koerper = Netz.Farbe("#94A3B8");
                var tuer = Netz.Farbe("#CBD5E1");
                Kasten(r, 0, 0, b, t, 0, 1.7f * M, koerper);
                var spalten = Math.Max(2, (int)(b / 0.45f));
                var breite = b / spalten;
                var hoehe = (1.6f * M - 0.1f) / 5;
                for (var i = 0; i < spalten; i++)
                {
                    for (var j = 0; j < 5; j++)
                    {
                        var lx = i * breite;
                        var unten = 0.1f + (j * hoehe);
                        Kasten(r, lx + 0.03f, t - 0.01f, breite - 0.06f, 0.03f, unten + 0.03f, unten + hoehe - 0.03f, tuer);
                        Kasten(r, lx + breite - 0.12f, t, 0.05f, 0.03f, unten + (hoehe / 2) - 0.03f, unten + (hoehe / 2) + 0.03f, Farben.Gelb);
                    }
                }
                break;
            }
            case "tresor":
            {
                var stahl = Netz.Farbe("#475569");
                var hell = Netz.Farbe("#E2E8F0");
                Kasten(r, 0, 0, b, t, 0, 1.75f * M, stahl);
                Kasten(r, 0.15f, t - 0.01f, b - 0.3f, 0.04f, 0.15f * M, 1.6f * M, Netz.Farbe("#64748B"));
                // Rad in der Mitte (Kreuz mit Nabe) und Scharniere links
                var mitte = 0.9f * M;
                Kasten(r, (b / 2) - 0.35f, t + 0.02f, 0.7f, 0.04f, mitte - 0.03f, mitte + 0.03f, hell);
                Kasten(r, (b / 2) - 0.03f, t + 0.02f, 0.06f, 0.04f, mitte - 0.35f, mitte + 0.35f, hell);
                Kasten(r, (b / 2) - 0.1f, t + 0.03f, 0.2f, 0.05f, mitte - 0.1f, mitte + 0.1f, Farben.Gelb);
                Kasten(r, 0.12f, t, 0.06f, 0.06f, 0.4f * M, 0.6f * M, hell);
                Kasten(r, 0.12f, t, 0.06f, 0.06f, 1.15f * M, 1.35f * M, hell);
                break;
            }
            case "geldautomat":
                Kasten(r, 0, 0, b, t, 0, 1.5f * M, Netz.Farbe("#334155"));
                Kasten(r, 0, 0, b, t, 1.5f * M, 1.7f * M, Netz.Farbe("#2563EB"));
                Kasten(r, 0.12f, t - 0.02f, b - 0.24f, 0.04f, 1.0f * M, 1.35f * M, Farben.Bildschirm);
                Kasten(r, 0.1f, t - 0.02f, b - 0.2f, 0.14f, 0.85f * M, 0.92f * M, Netz.Farbe("#94A3B8"));
                Kasten(r, 0.25f, t - 0.01f, b - 0.5f, 0.03f, 0.7f * M, 0.75f * M, Farben.Geraet);
                break;
            case "wartemarken":
                Kasten(r, 0.15f, 0.1f, b - 0.3f, t - 0.2f, 0, 1.0f * M, Netz.Farbe("#B45309"));
                Kasten(r, 0, 0, b, t, 1.0f * M, 1.5f * M, Netz.Farbe("#F59E0B"));
                Kasten(r, 0.1f, t - 0.01f, b - 0.2f, 0.03f, 1.2f * M, 1.42f * M, Farben.Bildschirm);
                Kasten(r, 0.2f, t - 0.01f, b - 0.4f, 0.04f, 1.06f * M, 1.1f * M, Farben.Weiss);
                break;
            case "wappen":
            {
                var gold = Netz.Farbe("#FBBF24");
                var tief = Math.Min(t, 0.1f);
                Kasten(r, b * 0.15f, 0, b * 0.7f, tief, 1.0f * M, 1.5f * M, gold);
                Kasten(r, b * 0.32f, 0, b * 0.36f, tief, 0.9f * M, 1.0f * M, gold);
                Kasten(r, b * 0.25f, 0, b * 0.5f, tief + 0.02f, 1.08f * M, 1.42f * M, Netz.Farbe("#B91C1C"));
                Kasten(r, b * 0.45f, 0, b * 0.1f, tief + 0.03f, 1.12f * M, 1.38f * M, Farben.Geraet);
                break;
            }
            case "fahne":
            {
                // Fahne im Staender: Fuss, Mast, Tuch in drei Streifen
                Kasten(r, 0, 0, b, t, 0, 0.06f * M, Netz.Farbe("#475569"));
                Kasten(r, 0.06f, (t / 2) - 0.03f, 0.06f, 0.06f, 0, 1.75f * M, Farben.Metall);
                var streifen = new[] { Farben.Geraet, Netz.Farbe("#DC2626"), Netz.Farbe("#FACC15") };
                for (var i = 0; i < 3; i++)
                {
                    var oben = (1.68f - (i * 0.22f)) * M;
                    Kasten(r, 0.12f, (t / 2) - 0.01f, b - 0.14f, 0.02f, oben - (0.22f * M), oben, streifen[i]);
                }
                break;
            }
            default:
                Kasten(r, 0, 0, b, t, 0, 0.6f * M, Farben.Karton);
                break;
        }
    }
}
