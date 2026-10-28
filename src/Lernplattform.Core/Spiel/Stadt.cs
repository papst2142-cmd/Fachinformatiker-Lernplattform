using System.Globalization;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>Warum eine Aktion in der Stadt nicht geht (Ok = sie geht). Die Texte dazu kommt erst mit der Oberflaeche.</summary>
public enum StadtGrund
{
    Ok,
    KeinProfil,        // noch keine Spielfigur
    Unbekannt,         // diesen Ort gibt es nicht
    Gesperrt,          // der Ort ist noch nicht auf der Karte (frei)
    SchonDa,           // die Figur steht schon dort
    KeinWeg,           // kein Weg auf dem Raster
    Abend,             // bis A 0.20.0: abends nur noch heimgehen (seit W10 nicht mehr erzeugt)
    Geschlossen,       // der Ort hat gerade zu
    OeffnetNichtMehr,  // zu, und er oeffnet heute nicht mehr vor dem Abend
    NichtGeschlossen,  // Warten geht nur vor einem geschlossenen Ort
}

/// <summary>Ergebnis einer Aktion in der Stadt; bei Ok das geschriebene Ereignis (Art, Daten).</summary>
public sealed record StadtErgebnis(StadtGrund Grund, string? Art = null, PyObjekt? Daten = null)
{
    public bool Ok => Grund == StadtGrund.Ok;
}

/// <summary>Wo die Figur heute steht und wie spaet es ist (Spielminuten ab Mitternacht).</summary>
public sealed record StadtStand(long Tag, string Ort, long Uhr);

/// <summary>Ein Weg auf dem Stadt-Raster: die Zellen von Tuer zu Tuer (Schritte = Zellen - 1).</summary>
public sealed record StadtWeg(IReadOnlyList<(long X, long Y)> Zellen)
{
    public long Schritte => Zellen.Count - 1;
}

/// <summary>
/// Phase 7, Weisung P7-6 (Plan Phase 7 W4/W7, Frage 2a): die begehbare Stadt als Kern ohne Darstellung.
/// Aus <c>inhalte/spiel/orte.json</c> (unveraendert wie Python 0.63.2) und
/// <c>inhalte_csharp/spiel/stadt.json</c> (nur C#-Linie): Raster aus Strassen und Fusswegen, Wegsuche
/// (Breitensuche von Tuer zu Tuer), Tagesuhr in Spielschritten (nie die echte Uhr), Oeffnungszeiten je
/// Ort und die neuen Orte Moebelhaus und Elektromarkt.
/// Die Stadt wirkt nur ueber neue, angehaengte Ereignisse (<see cref="Gegangen"/>,
/// <see cref="Gewartet"/>, seit P7-10 Nachtrag 1 <see cref="Gelaufen"/>); der Spielstand des alten Spiels (Tickets, Bewertung, Geld, Arbeitstag,
/// Fingerabdruck f2b59ce8) bleibt davon unberuehrt. Wann ein Ort auf der Karte ist und wie er heisst,
/// rechnet <see cref="OrtFrei"/> und <see cref="Orte"/> wie Python (place_unlocked, map_places,
/// Vergleichswerte G48 Teil Stadt).
/// Regeln der Tagesuhr: Jeder Arbeitstag beginnt zu Hause um <see cref="Beginn"/>. Ein Weg kostet
/// Schritte mal <see cref="MinutenJeSchritt"/>. Seit W10 (Weisung P8-34, Nico 2c) geht jeder Weg auch abends
/// weiter; um <see cref="UhrSteht"/> (23:00) bleibt die Uhr stehen (Minuten eines Wegs werden darauf gekappt),
/// bis die Figur zu Hause schlafen geht. Betreten geht nur, solange der Ort offen ist; vor einem Ort, der heute
/// noch (bis zum <see cref="Abend"/>) oeffnet, kann man bis zur Oeffnung warten.
/// </summary>
public sealed class Stadt
{
    /// <summary>Ereignis: ein Weg in der Stadt {"tag", "von", "nach", "schritte", "minuten"}.</summary>
    public const string Gegangen = "stadt_gegangen";

    /// <summary>Ereignis: vor einem geschlossenen Ort bis zur Oeffnung gewartet {"tag", "ort", "minuten"}.</summary>
    public const string Gewartet = "stadt_gewartet";

    /// <summary>
    /// Ereignis: frei in der Stadt gegangen, ohne an einem anderen Ort anzukommen
    /// {"tag", "ort", "schritte", "minuten"} (Weisung P7-10 Nachtrag 1, Wunsch 3). Der Ort bleibt der
    /// zuletzt erreichte, nur die Uhr laeuft weiter.
    /// </summary>
    public const string Gelaufen = "stadt_gelaufen";

    /// <summary>Minuten eines Tages; laenger dauert kein Weg und kein Warten.</summary>
    public const long TagMinuten = 1440;

    private static readonly (long X, long Y)[] Nachbarn = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    private readonly HashSet<(long, long)> begehbar = [];
    private readonly Dictionary<string, (long, long)> tueren = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (long Auf, long Zu)> zeiten = new(StringComparer.Ordinal);

    public Stadt(PyObjekt inhalt, PyObjekt stadt)
    {
        Inhalt = inhalt;
        Daten = stadt;
        var raster = Objekt(stadt["raster"]);
        Zelle = Py.F(raster["zelle"]);
        var radius = Py.F(raster["breite"]) / 2;
        var uhr = Objekt(stadt["uhr"]);
        Beginn = Py.Int(uhr["beginn"]);
        Abend = Py.Int(uhr["abend"]);
        MinutenJeSchritt = Py.Int(uhr["minuten_je_schritt"]);
        Heim = Py.Str(uhr["heim"]);
        var karte = Objekt(inhalt["orte"]);
        var welt = Objekt(karte["welt"]);
        Breite = (long)Math.Ceiling(Py.F(welt["breite"]) / Zelle);
        Hoehe = (long)Math.Ceiling(Py.F(welt["hoehe"]) / Zelle);
        Regeln = [.. ListeOderLeer(karte, "orte").Select(Objekt), .. ListeOderLeer(stadt, "neue_orte").Select(Objekt)];

        var strecken = new List<((double, double), (double, double))>();
        void Linie(object? punkte)
        {
            var p = Liste(punkte).Select(Punkt).ToList();
            for (var i = 0; i + 1 < p.Count; i++)
            {
                strecken.Add((p[i], p[i + 1]));
            }
        }
        foreach (var teil in ListeOderLeer(welt, "landschaft").Select(Objekt).Where(t => Py.Gleich(t["typ"], "strasse")))
        {
            Linie(teil["punkte"]);
        }
        Zugaenge = ZugaengeMit(stadt);
        foreach (var (id, zugang) in Zugaenge)
        {
            var z = Objekt(zugang);
            Linie(z["weg"]);
            var (x, y) = Punkt(z["tuer"]);
            tueren[id] = ZelleBei(x, y);
        }
        for (long i = 0; i < Breite; i++)
        {
            for (long j = 0; j < Hoehe; j++)
            {
                var mitte = ((i + 0.5) * Zelle, (j + 0.5) * Zelle);
                if (strecken.Any(s => Abstand(mitte, s.Item1, s.Item2) <= radius))
                {
                    begehbar.Add((i, j));
                }
            }
        }
        foreach (var (id, wert) in Objekt(stadt["oeffnungszeiten"]))
        {
            var paar = Liste(wert);
            zeiten[id] = (Py.Int(paar[0]), Py.Int(paar[1]));
        }
    }

    /// <summary>
    /// Die Zugaenge je Ort (Tuer und Fussweg zur Strasse): "zugaenge" aus stadt.json, darin ersetzt durch
    /// den angehaengten Abschnitt "zugaenge_neu" (Phase 8 W8b, Weisung P8-20, Nico 1a: Bahnhof und
    /// Gewerbehof). Reihenfolge wie "zugaenge"; ein Ort nur in "zugaenge_neu" kommt hinten dazu.
    /// </summary>
    public PyObjekt Zugaenge { get; }

    private static PyObjekt ZugaengeMit(PyObjekt stadt)
    {
        var neu = ZugaengeNeu(stadt);
        var ergebnis = new PyObjekt();
        foreach (var (id, zugang) in Objekt(stadt["zugaenge"]))
        {
            ergebnis.Add(id, Get(neu, id) ?? zugang);
        }
        foreach (var (id, zugang) in neu)
        {
            if (!Py.Hat(ergebnis, id))
            {
                ergebnis.Add(id, zugang);
            }
        }
        return ergebnis;
    }

    /// <summary>Abschnitt "zugaenge_neu" ohne Hinweise (Schluessel mit "_").</summary>
    private static PyObjekt ZugaengeNeu(PyObjekt stadt)
    {
        var neu = new PyObjekt();
        foreach (var (id, zugang) in Py.Wahr(Get(stadt, "zugaenge_neu")) ? Objekt(stadt["zugaenge_neu"]) : [])
        {
            if (!id.StartsWith('_'))
            {
                neu.Add(id, zugang);
            }
        }
        return neu;
    }

    /// <summary>stadt.json aus einem Ordner (inhalte_csharp/spiel).</summary>
    public static PyObjekt Laden(string ordner) =>
        Py.Lesen(File.ReadAllBytes(Path.Combine(ordner, "stadt.json"))) as PyObjekt ?? throw new InvalidDataException("stadt.json ist kein Objekt");

    public PyObjekt Inhalt { get; }

    public PyObjekt Daten { get; }

    /// <summary>Kantenlaenge einer Zelle in Karteneinheiten.</summary>
    public double Zelle { get; }

    /// <summary>Groesse des Rasters in Zellen.</summary>
    public long Breite { get; }

    public long Hoehe { get; }

    public long Beginn { get; }

    /// <summary>
    /// Abend (20:00): bis hierhin kann man vor einem geschlossenen Ort auf die Oeffnung warten. Seit W10 (Weisung
    /// P8-34, Nico 2c) geht danach jeder Weg weiter, die Orte folgen ihren Oeffnungszeiten.
    /// </summary>
    public long Abend { get; }

    /// <summary>
    /// W10 (Weisung P8-34, Nico 2c): um 23:00 bleibt die Uhr stehen, bis die Figur zu Hause schlafen geht
    /// (Feierabend, neuer Tag um <see cref="Beginn"/>). Wege danach gehen weiter und kosten keine Minuten.
    /// </summary>
    public const long UhrSteht = 1380;

    /// <summary>Minuten eines Wegs, gekappt auf die Zeit bis <see cref="UhrSteht"/> (danach 0).</summary>
    private static long Gekappt(long uhr, long minuten) => Math.Max(0, Math.Min(minuten, UhrSteht - uhr));

    public long MinutenJeSchritt { get; }

    /// <summary>Der Ort, an dem jeder Tag beginnt und der abends noch erreichbar ist.</summary>
    public string Heim { get; }

    /// <summary>Alle Orte: die aus orte.json, danach die neuen aus stadt.json.</summary>
    public IReadOnlyList<PyObjekt> Regeln { get; }

    public IReadOnlyCollection<(long, long)> Begehbar => begehbar;

    private static (double, double) Punkt(object? wert)
    {
        var p = Liste(wert);
        return (Py.F(p[0]), Py.F(p[1]));
    }

    private static double Abstand((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var laenge = dx * dx + dy * dy;
        var t = laenge == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / laenge, 0, 1);
        var (nx, ny) = (a.X + t * dx - p.X, a.Y + t * dy - p.Y);
        return Math.Sqrt(nx * nx + ny * ny);
    }

    /// <summary>Die Zelle zu einem Punkt der Karte.</summary>
    public (long, long) ZelleBei(double x, double y) => ((long)Math.Floor(x / Zelle), (long)Math.Floor(y / Zelle));

    /// <summary>Die Regel eines Ortes (orte.json oder neu), null = unbekannt.</summary>
    public PyObjekt? Regel(string? id) => Regeln.FirstOrDefault(r => Py.Gleich(r["id"], id));

    /// <summary>Die Zelle der Tuer eines Ortes.</summary>
    public (long, long)? Tuer(string id) => tueren.TryGetValue(id, out var zelle) ? zelle : null;

    // -- Karte wie Python (place_unlocked, map_places) ------------------------

    /// <summary>place_unlocked: ist der Ort laut "frei" schon zu sehen? (alle Bedingungen muessen gelten)</summary>
    public static bool OrtFrei(PyObjekt regel, Spielstand? stand, PyObjekt inhalt)
    {
        var braucht = Py.Wahr(Get(regel, "frei")) ? Objekt(regel["frei"]) : [];
        var tag = stand?.Tag ?? 1;
        if (Py.Hat(braucht, "ab_tag") && tag < Py.Int(braucht["ab_tag"]))
        {
            return false;
        }
        if (Py.Hat(braucht, "kundenort"))
        {
            var ort = Kundenorte(inhalt).FirstOrDefault(o => Py.Gleich(o["id"], braucht["kundenort"]));
            if (ort is null || tag < Py.Int(Get(ort, "ab_tag", 1L)))
            {
                return false;
            }
        }
        if (Py.Hat(braucht, "aufgabe") && (stand is null || !Py.Enthaelt(stand.Geloest, braucht["aufgabe"])))
        {
            return false;
        }
        if (Py.Wahr(Get(braucht, "gruendung_moeglich")) && (stand is null || !(Py.Wahr(stand.Firma) || stand.GruendungBereit())))
        {
            return false;
        }
        if (Py.Wahr(Get(braucht, "gegruendet")) && (stand is null || !Py.Wahr(stand.Firma)))
        {
            return false;
        }
        if (Py.Hat(braucht, "stufe_min") && (stand is null || !Py.Wahr(stand.Firma)
            || Py.Kleiner(Objekt(stand.Firma)["stufe"], Py.Int(braucht["stufe_min"]))))
        {
            return false;
        }
        if (Py.Wahr(Get(braucht, "filiale_moeglich")) && !Betrieb.FilialeMoeglich(stand, inhalt))
        {
            return false;
        }
        if (Py.Wahr(Get(braucht, "filiale_gebaut")) && (stand is null || !Py.Wahr(stand.Zweigstelle)))
        {
            return false;
        }
        if (Py.Wahr(Get(braucht, "serverfarm")) && (stand is null || stand.FarmFrei is null))
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// map_places (nur die Felder ohne Zeichnung): die sichtbaren Orte mit id, name, kurz, rolle,
    /// ansicht, hinweis, x, y, schild, modell; Name nach Gruendung und Filiale wie Python.
    /// </summary>
    public static List<PyObjekt> Orte(Spielstand? stand, PyObjekt inhalt, IEnumerable<PyObjekt> regeln)
    {
        var ergebnis = new List<PyObjekt>();
        var gegruendet = stand is not null && Py.Wahr(stand.Firma);
        foreach (var regel in regeln)
        {
            if (!OrtFrei(regel, stand, inhalt))
            {
                continue;
            }
            var ort = new PyObjekt();
            foreach (var k in new[] { "id", "name", "kurz", "rolle", "ansicht", "x", "y", "schild", "modell" })
            {
                ort.Add(k, Get(regel, k));
            }
            Py.Setzen(ort, "kurz", Py.Wahr(ort["kurz"]) ? ort["kurz"] : ort["name"]);
            Py.Setzen(ort, "hinweis", "");
            void Uebernehmen(object? werte)
            {
                foreach (var (k, v) in Objekt(werte))
                {
                    Py.Setzen(ort, k, v);
                }
            }
            if (gegruendet && Py.Wahr(Get(regel, "nach_gruendung")))
            {
                Uebernehmen(regel["nach_gruendung"]);
            }
            if (!gegruendet && Py.Wahr(Get(regel, "vor_gruendung")))
            {
                Uebernehmen(regel["vor_gruendung"]);
            }
            if (Py.Wahr(Get(regel, "vor_filiale")) && !(stand is not null && Py.Wahr(stand.Zweigstelle)))
            {
                Uebernehmen(regel["vor_filiale"]);
            }
            if (Py.Wahr(Get(regel, "name_firma")) && gegruendet)
            {
                Py.Setzen(ort, "name", Objekt(stand!.Firma)["name"]);
            }
            if (Py.Wahr(Get(regel, "name_filiale")))
            {
                Py.Setzen(ort, "name", stand is not null && Py.Wahr(stand.Zweigstelle) ? Objekt(stand.Zweigstelle)["name"]
                    : Get(Betrieb.FilialRegeln(inhalt), "name_vorschlag", ort["name"]));
            }
            ergebnis.Add(ort);
        }
        return ergebnis;
    }

    /// <summary>Die sichtbaren Orte der Stadt (orte.json und die neuen).</summary>
    public List<PyObjekt> Orte(Spielstand? stand) => Orte(stand, Inhalt, Regeln);

    // -- Raster und Wege -----------------------------------------------------

    /// <summary>Breitensuche von Tuer zu Tuer (Nachbarn rechts, links, unten, oben); null = kein Weg.</summary>
    public StadtWeg? Weg(string von, string nach)
    {
        if (Tuer(von) is not { } start || Tuer(nach) is not { } ziel || !begehbar.Contains(start) || !begehbar.Contains(ziel))
        {
            return null;
        }
        var vorher = new Dictionary<(long, long), (long, long)> { [start] = start };
        var schlange = new Queue<(long, long)>();
        schlange.Enqueue(start);
        while (schlange.Count > 0)
        {
            var zelle = schlange.Dequeue();
            if (zelle == ziel)
            {
                var weg = new List<(long, long)> { zelle };
                while (weg[^1] != start)
                {
                    weg.Add(vorher[weg[^1]]);
                }
                weg.Reverse();
                return new StadtWeg(weg);
            }
            foreach (var (dx, dy) in Nachbarn)
            {
                var neu = (zelle.Item1 + dx, zelle.Item2 + dy);
                if (begehbar.Contains(neu) && vorher.TryAdd(neu, zelle))
                {
                    schlange.Enqueue(neu);
                }
            }
        }
        return null;
    }

    // -- Tagesuhr und Oeffnungszeiten ----------------------------------------

    /// <summary>[oeffnet, schliesst) eines Ortes; null = immer offen.</summary>
    public (long Auf, long Zu)? Oeffnungszeit(string id) => zeiten.TryGetValue(id, out var z) ? z : null;

    public bool Offen(string id, long uhr) => Oeffnungszeit(id) is not { } z || (z.Auf <= uhr && uhr < z.Zu);

    /// <summary>
    /// Stand der Stadt aus dem Ereignisprotokoll: Ort und Uhr des laufenden Arbeitstags. Weisung P8-5 (L3,
    /// Nicos Antwort 1a): haben mehrere Geraete am selben Tag gespielt (nach einem Abgleich), zaehlt das
    /// Geraet, das an diesem Tag am weitesten gespielt hat (hoechste Uhr, bei Gleichstand der spaetere
    /// Schritt), mit seinem Ort; die Minuten der Geraete werden nicht addiert. Ein Schritt rechnet ab dem
    /// Stand weiter, von dem er ausging: steht im Schritt ("von" beim Gehen, "ort" beim Warten und Laufen) der
    /// Ort des bis dahin gezeigten Stands, ab diesem (das Geraet hatte ihn nach dem Abgleich gesehen), sonst
    /// ab dem eigenen Stand des Geraets (ohne Abgleich dazwischen gespielt). Ein Geraet: wie bisher.
    /// </summary>
    public StadtStand Stand(Spielstand stand)
    {
        var tag = stand.Tag;
        var geraete = SpielGeraete.Von(stand);
        var je = new Dictionary<string, (string Ort, long Uhr, int Letzter)>(StringComparer.Ordinal);
        var nummer = 0;
        foreach (var e in stand.Verlauf)
        {
            nummer++;
            if ((e.Art != Gegangen && e.Art != Gewartet && e.Art != Gelaufen) || !Py.IstNurInt(Get(e.Daten, "tag")) || Py.Int(e.Daten["tag"]) != tag)
            {
                continue;
            }
            var minuten = Get(e.Daten, "minuten");
            if (!Py.IstNurInt(minuten) || Py.Int(minuten) < 0 || Py.Int(minuten) > TagMinuten)
            {
                continue;
            }
            var geraet = geraete?.Von(e) ?? "";
            var (ort, uhr, _) = je.TryGetValue(geraet, out var bisher) ? bisher : (Heim, Beginn, 0);
            if (je.Count > 0 && Get(e.Daten, e.Art == Gegangen ? "von" : "ort") is string ab)
            {
                var gezeigt = je.Values.MaxBy(g => (g.Uhr, g.Letzter));
                if (gezeigt.Ort == ab && gezeigt.Uhr > uhr)
                {
                    (ort, uhr) = (gezeigt.Ort, gezeigt.Uhr);
                }
            }
            if (e.Art == Gegangen)
            {
                // ein Ort, den diese Fassung nicht kennt (z. B. aus einer neueren per Abgleich), zaehlt nicht
                if (Get(e.Daten, "nach") is not string nach || Tuer(nach) is null)
                {
                    continue;
                }
                ort = nach;
            }
            je[geraet] = (ort, uhr + Py.Int(minuten), nummer);
        }
        if (je.Count == 0)
        {
            return new StadtStand(tag, Heim, Beginn);
        }
        var weitester = je.Values.MaxBy(g => (g.Uhr, g.Letzter));
        return new StadtStand(tag, weitester.Ort, weitester.Uhr);
    }

    /// <summary>Uhrzeit zur Anzeige: 425 = "07:05".</summary>
    public static string Uhrzeit(long minuten) =>
        (minuten / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (minuten % 60).ToString("00", CultureInfo.InvariantCulture);

    // -- Aktionen ------------------------------------------------------------

    /// <summary>
    /// Kann die Figur jetzt zu <paramref name="ziel"/> gehen? Bei Ok das Ereignis dazu (noch nicht
    /// geschrieben). Mit <paramref name="schritte"/> kommt sie von einem freien Punkt der Stadt (die
    /// Wegsuche der Darstellung hat die Schritte gezaehlt); dann darf das Ziel auch der Ort sein, an
    /// dem sie zuletzt war.
    /// </summary>
    public StadtErgebnis GehenPruefen(Spielstand stand, string ziel, long? schritte = null)
    {
        if (stand.Profil is null)
        {
            return new(StadtGrund.KeinProfil);
        }
        if (Regel(ziel) is not { } regel)
        {
            return new(StadtGrund.Unbekannt);
        }
        if (!OrtFrei(regel, stand, Inhalt))
        {
            return new(StadtGrund.Gesperrt);
        }
        var jetzt = Stand(stand);
        long anzahl;
        if (schritte is { } frei)
        {
            if (frei < 0 || frei * MinutenJeSchritt > TagMinuten || Tuer(ziel) is null)
            {
                return new(StadtGrund.KeinWeg);
            }
            anzahl = frei;
        }
        else
        {
            if (jetzt.Ort == ziel)
            {
                return new(StadtGrund.SchonDa);
            }
            if (Weg(jetzt.Ort, ziel) is not { } weg)
            {
                return new(StadtGrund.KeinWeg);
            }
            anzahl = weg.Schritte;
        }
        var minuten = Gekappt(jetzt.Uhr, anzahl * MinutenJeSchritt);
        return new(StadtGrund.Ok, Gegangen, new PyObjekt
        {
            { "tag", jetzt.Tag }, { "von", jetzt.Ort }, { "nach", ziel }, { "schritte", anzahl }, { "minuten", minuten },
        });
    }

    /// <summary>
    /// Kann die Figur <paramref name="schritte"/> frei durch die Stadt gehen (nicht zu einem Ort)? Wie
    /// ein Weg kostet jeder Schritt <see cref="MinutenJeSchritt"/>, ab <see cref="UhrSteht"/> nichts mehr.
    /// </summary>
    public StadtErgebnis LaufenPruefen(Spielstand stand, long schritte)
    {
        if (stand.Profil is null)
        {
            return new(StadtGrund.KeinProfil);
        }
        if (schritte <= 0 || schritte * MinutenJeSchritt > TagMinuten)
        {
            return new(StadtGrund.KeinWeg);
        }
        var jetzt = Stand(stand);
        var minuten = Gekappt(jetzt.Uhr, schritte * MinutenJeSchritt);
        return new(StadtGrund.Ok, Gelaufen, new PyObjekt
        {
            { "tag", jetzt.Tag }, { "ort", jetzt.Ort }, { "schritte", schritte }, { "minuten", minuten },
        });
    }

    /// <summary>Kann die Figur vor dem geschlossenen Ort warten, bis er heute oeffnet?</summary>
    public StadtErgebnis WartenPruefen(Spielstand stand)
    {
        if (stand.Profil is null)
        {
            return new(StadtGrund.KeinProfil);
        }
        var jetzt = Stand(stand);
        if (Offen(jetzt.Ort, jetzt.Uhr))
        {
            return new(StadtGrund.NichtGeschlossen);
        }
        var zeit = Oeffnungszeit(jetzt.Ort)!.Value;
        if (zeit.Auf <= jetzt.Uhr || zeit.Auf > Abend)
        {
            return new(StadtGrund.OeffnetNichtMehr);
        }
        return new(StadtGrund.Ok, Gewartet, new PyObjekt { { "tag", jetzt.Tag }, { "ort", jetzt.Ort }, { "minuten", zeit.Auf - jetzt.Uhr } });
    }

    /// <summary>Darf die Figur den Ort betreten, vor dem sie steht? (schreibt nichts)</summary>
    public StadtGrund Betreten(Spielstand stand)
    {
        if (stand.Profil is null)
        {
            return StadtGrund.KeinProfil;
        }
        var jetzt = Stand(stand);
        if (Regel(jetzt.Ort) is not { } regel)
        {
            return StadtGrund.Unbekannt;
        }
        if (!OrtFrei(regel, stand, Inhalt))
        {
            return StadtGrund.Gesperrt;
        }
        if (Offen(jetzt.Ort, jetzt.Uhr))
        {
            return StadtGrund.Ok;
        }
        var zeit = Oeffnungszeit(jetzt.Ort)!.Value;
        return zeit.Auf > jetzt.Uhr && zeit.Auf <= Abend ? StadtGrund.Geschlossen : StadtGrund.OeffnetNichtMehr;
    }

    /// <summary>Zu einem Ort gehen und das Ereignis schreiben (nur wenn es geht).</summary>
    public StadtErgebnis Gehen(Spielablauf spiel, string ziel, long? schritte = null) => Ausfuehren(spiel, GehenPruefen(spiel.Stand, ziel, schritte));

    /// <summary>Frei durch die Stadt gehen und das Ereignis schreiben (nur wenn es geht).</summary>
    public StadtErgebnis Laufen(Spielablauf spiel, long schritte) => Ausfuehren(spiel, LaufenPruefen(spiel.Stand, schritte));

    /// <summary>Bis zur Oeffnung warten und das Ereignis schreiben (nur wenn es geht).</summary>
    public StadtErgebnis Warten(Spielablauf spiel) => Ausfuehren(spiel, WartenPruefen(spiel.Stand));

    private static StadtErgebnis Ausfuehren(Spielablauf spiel, StadtErgebnis ergebnis)
    {
        if (ergebnis.Ok)
        {
            spiel.Ausfuehren([(ergebnis.Art!, ergebnis.Daten!)]);
        }
        return ergebnis;
    }

    // -- Pruefung der Inhalte ------------------------------------------------

    /// <summary>Fehler in stadt.json (leer = alles gut): Orte, Tueren, Wege, Zeiten, Uhr.</summary>
    /// <summary>Felder eines Eintrags in neue_orte (Aufbau wie orte.json, dazu sortiment).</summary>
    public static readonly IReadOnlyList<string> NeueOrtFelder = ["id", "name", "x", "y", "schild", "modell", "rolle", "ansicht", "sortiment", "frei"];

    /// <summary>Rollen der neuen Orte: Geschaefte (W7) und Schalter (Bank, Amt; W7b).</summary>
    public static readonly IReadOnlyList<string> NeueOrtRollen = ["geschaeft", "schalter"];

    /// <summary>Sortimente der Geschaefte.</summary>
    public static readonly IReadOnlyList<string> Sortimente = ["moebel", "elektro", Kleidung.Art];   // Kleidung: Phase 8 W5 (Kaufhaus)

    public List<string> Pruefen()
    {
        var fehler = new List<string>();
        var alt = ListeOderLeer(Objekt(Inhalt["orte"]), "orte").Select(o => Py.Str(Objekt(o)["id"])).ToList();
        var neu = ListeOderLeer(Daten, "neue_orte").Select(Objekt).ToList();
        var modelle = Py.Wahr(Get(Daten, "neue_modelle")) ? Objekt(Daten["neue_modelle"]) : [];
        var ids = Regeln.Select(r => Py.Str(r["id"])).ToList();
        foreach (var doppelt in ids.GroupBy(i => i).Where(g => g.Count() > 1))
        {
            fehler.Add("Ort doppelt: " + doppelt.Key);
        }
        foreach (var ort in neu)
        {
            var id = Py.Str(ort["id"]);
            if (!Py.Wahr(Get(ort, "name")))
            {
                fehler.Add(id + ": kein Name");
            }
            if (!Py.Hat(modelle, Py.Str(Get(ort, "modell"))))
            {
                fehler.Add(id + ": Modell fehlt in neue_modelle");
            }
            // Sammelliste 99 (Weisung P7-7): Rolle, Ansicht, Sortiment und Felder nur aus festen Listen
            foreach (var (k, _) in ort)
            {
                if (!NeueOrtFelder.Contains(k))
                {
                    fehler.Add(id + ": unbekanntes Feld " + k);
                }
            }
            if (!NeueOrtRollen.Contains(Py.Str(Get(ort, "rolle"))))
            {
                fehler.Add(id + ": unbekannte Rolle " + Py.Str(Get(ort, "rolle")));
            }
            else if (Py.Str(Get(ort, "ansicht")) != Py.Str(Get(ort, "rolle")) + ":" + id)
            {
                fehler.Add(id + ": Ansicht muss " + Py.Str(Get(ort, "rolle")) + ":" + id + " sein");
            }
            if (Py.Str(Get(ort, "rolle")) != "geschaeft")
            {
                // W7b: nur Geschaefte haben ein Sortiment
                if (Py.Hat(ort, "sortiment"))
                {
                    fehler.Add(id + ": Sortiment nur fuer Geschaefte");
                }
            }
            else if (!Sortimente.Contains(Py.Str(Get(ort, "sortiment"))))
            {
                fehler.Add(id + ": unbekanntes Sortiment " + Py.Str(Get(ort, "sortiment")));
            }
            foreach (var (k, _) in Py.Wahr(Get(ort, "frei")) ? Objekt(ort["frei"]) : [])
            {
                if (!SpielPruefung.KartenFreischaltungen.Contains(k))
                {
                    fehler.Add(id + ": unbekannte Freischaltung " + k);
                }
            }
        }
        var welt = Objekt(Objekt(Inhalt["orte"])["welt"]);
        foreach (var id in ids)
        {
            if (Tuer(id) is not { } tuer)
            {
                fehler.Add(id + ": kein Zugang");
                continue;
            }
            if (!begehbar.Contains(tuer))
            {
                fehler.Add(id + ": Tuer liegt nicht auf einem Weg");
            }
            if (id != Heim && Weg(Heim, id) is null)
            {
                fehler.Add(id + ": von " + Heim + " aus nicht erreichbar");
            }
        }
        foreach (var (id, _) in ZugaengeNeu(Daten))
        {
            if (!Py.Hat(Objekt(Daten["zugaenge"]), id))
            {
                fehler.Add("zugaenge_neu ersetzt keinen Zugang: " + id);
            }
        }
        foreach (var (id, zugang) in Zugaenge)
        {
            if (!ids.Contains(id))
            {
                fehler.Add("Zugang zu unbekanntem Ort: " + id);
            }
            foreach (var p in Liste(Objekt(zugang)["weg"]).Prepend(Objekt(zugang)["tuer"]).Select(Punkt))
            {
                if (p.Item1 < 0 || p.Item2 < 0 || p.Item1 > Py.F(welt["breite"]) || p.Item2 > Py.F(welt["hoehe"]))
                {
                    fehler.Add(id + ": Punkt ausserhalb der Karte");
                }
            }
        }
        foreach (var (id, (auf, zu)) in zeiten)
        {
            if (!ids.Contains(id))
            {
                fehler.Add("Oeffnungszeit fuer unbekannten Ort: " + id);
            }
            if (auf < 0 || auf >= zu || zu > 1440)
            {
                fehler.Add(id + ": Oeffnungszeit ungueltig");
            }
        }
        if (!ids.Contains(Heim) || Oeffnungszeit(Heim) is not null)
        {
            fehler.Add("heim muss ein Ort ohne Oeffnungszeit sein");
        }
        if (Beginn < 0 || Beginn >= Abend || Abend > TagMinuten || MinutenJeSchritt < 1)
        {
            fehler.Add("uhr ungueltig");
        }
        else
        {
            // auch der laengste Heimweg nach dem Abend endet vor Mitternacht
            var weiteste = ids.Where(id => id != Heim).Select(id => Weg(id, Heim)?.Schritte ?? 0).DefaultIfEmpty(0).Max();
            if (Abend + weiteste * MinutenJeSchritt >= TagMinuten)
            {
                fehler.Add("Heimweg nach dem Abend endet nach Mitternacht");
            }
        }
        if (neu.Any(o => alt.Contains(Py.Str(o["id"]))))
        {
            fehler.Add("neue_orte darf keinen Ort aus orte.json wiederholen");
        }
        return fehler;
    }
}
