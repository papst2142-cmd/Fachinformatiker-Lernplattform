using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="Innenraum"/> mit dem Bau aus dem Spielstand
/// (Ort, Eingaben, Questgeber, Moebel und Zusaetze der Innenraeume). Reiner Umzug aus Innenraum.cs, nichts geaendert.
/// </summary>
public sealed partial class Innenraum
{
    /// <summary>Ort (Python site) zur Ansicht eines Stadt-Orts; null = (noch) kein Innenraum.</summary>
    public static string? OrtFuer(string? ansicht) => ansicht is not null && Stadtbau.Etagen.Teilen(ansicht) is ({ } basis, > 0)
        // Phase 13 Welle 5 (Weisung P12-73): Etage eines Geschaefts oder Schalters ("geschaeft:moebelhaus@1"), Ort wie unten
        ? (basis.StartsWith("geschaeft:", StringComparison.Ordinal) || basis.StartsWith("schalter:", StringComparison.Ordinal) ? OrtFuer(basis) : null)
        : ansicht switch
    {
        "buero" => OrtBuero,
        "zuhause" => OrtZuhause,
        "filiale" => OrtFiliale,
        Serverfarm => Serverfarm,
        not null when ansicht.StartsWith("kunde:", StringComparison.Ordinal) => ansicht["kunde:".Length..],
        not null when ansicht.StartsWith("geschaeft:", StringComparison.Ordinal) && SpielKern.Laeden.Contains(ansicht["geschaeft:".Length..]) => ansicht["geschaeft:".Length..],
        not null when ansicht.StartsWith("schalter:", StringComparison.Ordinal) && SpielKern.SchalterOrte.Contains(ansicht["schalter:".Length..]) => ansicht["schalter:".Length..],
        _ => null,
    };

    /// <summary>
    /// Innenraum zur Ansicht fuer den Spielstand; null, wenn es ihn (noch) nicht gibt (keine Wohnung,
    /// Filiale nicht eroeffnet, Serverfarm ohne innenraeume.json). <paramref name="innenraeume"/>:
    /// Inhalt von innenraeume.json; <paramref name="geschaefte"/>: Inhalt von geschaefte.json (Kleidung im Kaufhaus).
    /// </summary>
    /// <remarks>Phase 8 W9: <paramref name="jahreszeit"/> faerbt die Wiese um das Haus (Sommer = wie vorher).</remarks>
    public static Innenraum? Bauen(string? ansicht, ISpielSicht stand, PyObjekt? innenraeume, GrafikBibliothek? grafik, PyObjekt? geschaefte = null,
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

    private static Innenraum? BauenOhneJahreszeit(string? ansicht, ISpielSicht stand, PyObjekt? innenraeume, GrafikBibliothek? grafik, PyObjekt? geschaefte,
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
    public static string? InhaltSignatur(string? ansicht, ISpielSicht stand, PyObjekt? innenraeume, PyObjekt? geschaefte, string? fest, Jahreszeit jahreszeit)
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

    private static Eingaben? EingabenFuer(string? ansicht, ISpielSicht stand, PyObjekt? innenraeume, PyObjekt? geschaefte)
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
            foreach (var p in ListeOderLeer(SpielKern.Regeln.FarmInhalt(stand.Inhalt), "personen").Select(Objekt))
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
        if (SpielKern.Laeden.Contains(ort) || SpielKern.SchalterOrte.Contains(ort))
        {
            // W7: Moebelhaus und Elektromarkt (Grundriss, Auslage und Verkaeufer aus innenraeume.json),
            // W7b: Bank und Amt (Grundriss, Schalter und Person ebenso)
            if ((innenraeume is null ? null : Get(innenraeume, ort)) is not PyObjekt laden)
            {
                return null;
            }
            var etage = Stadtbau.Etagen.Teilen(ansicht!).Etage;
            if (etage > 0)
            {
                // Phase 13 Welle 5 (Weisung P12-73): ein Obergeschoss steht so im Grundriss des Erdgeschosses, wie das
                // Werkzeug es gebaut hat (ohne Zusaetze, ohne Personen); fehlt es, gibt es keinen Innenraum
                var eg = Objekt(laden["gebaeude"]);
                // Weisung P12-74: eine Treppe nur nach unten traegt in der Kopie fuer die Darstellung "richtung": "unten"
                return Stadtbau.Etagen.Gibt(eg, etage) ? new Eingaben(ansicht!, ort, Stadtbau.Etagen.FuerDarstellung(eg, ort, etage), Questgeber(ort, etage, stand), null, geschaefte) : null;
            }
            var verkauf = ListeOderLeer(laden, "personen").Select(p => Person(Objekt(p), false)).Concat(Questgeber(ort, 0, stand)).ToList();
            var zusatz = Get(innenraeume!, "zusatz") is PyObjekt z ? Get(z, ort) as PyObjekt : null;
            // Weisung P8-56: zweiter angehaengter Zusatz (Fahne, Sessel, Puppen, Kassen), wirkt nach dem ersten
            var nachtrag = Get(innenraeume!, "zusatz_nachtrag") is PyObjekt zn ? Get(zn, ort) as PyObjekt : null;
            return MitMoebeln(innenraeume, new Eingaben(ansicht!, ort, MitZusatz(MitZusatz(Objekt(laden["gebaeude"]), zusatz), nachtrag), verkauf, null, geschaefte));
        }
        if (ort == OrtZuhause && SpielKern.Regeln.Bleibe(stand.Wohnung, stand.Inhalt) is null)
        {
            return null;
        }
        if (ort == OrtFiliale && SpielKern.Regeln.FilialGebaeude(stand) is null)
        {
            return null;
        }
        if (ort != OrtBuero && ort != OrtZuhause && ort != OrtFiliale && !Kundenorte(stand.Inhalt).Any(k => Py.Gleich(k["id"], ort)))
        {
            return null;
        }
        var gebaeude = SpielKern.Regeln.OrtGebaeude(ort, stand);
        // W8b (Nico 2a): Zusatz fuer Kundenorte (Bahnhof Talheim innen als Wartehalle), angehaengt in innenraeume.json
        if (innenraeume is not null && Get(innenraeume, "zusatz_kunden") is PyObjekt zk && Get(zk, ort) is PyObjekt zo)
        {
            gebaeude = MitZusatz(gebaeude, zo);
        }
        var auftraege = ort == OrtZuhause ? new PyDict() : stand.Auftraege(ort);
        var leute = ort == OrtZuhause ? [] : SpielKern.Regeln.OrtPersonen(ort, stand)
            .Select(p => Person(p, auftraege.Get(p["id"]) is List<object?> { Count: > 0 })).ToList();
        var boeden = ort == OrtZuhause ? Objekt(SpielKern.Regeln.Einrichtung(stand)["boeden"]) : null;
        return MitMoebeln(innenraeume, new Eingaben(ansicht!, ort, gebaeude, leute, boeden, null));
    }

    /// <summary>
    /// Phase 9 W1 (Weisung P9-3): letzter angehaengter Zusatz "zusatz_moebel" je Ort, wie <see cref="MitZusatz"/>, aber nur
    /// fuer Raeume, die der Grundriss hat (das Buero ist erst das Haus der Bitweiche, spaeter die eigene Firma). "plaetze" in
    /// einem Raum setzt den Platz einer Person nur fuer die Darstellung (Kopie ihrer Daten; der Spielstand bleibt unveraendert).
    /// </summary>
    /// <summary>
    /// Welle Q2 (Weisung P12-190): Questgeber der Quest-Ebene in einem Geschaeft oder an einem Schalter (Erdgeschoss oder
    /// Etage), mit gruenem „!“, wenn sie heute einen Auftrag haben. Ohne Quest-Ebene leer (Fassung 0 unveraendert).
    /// </summary>
    private static List<InnenPerson> Questgeber(string ort, int etage, ISpielSicht stand)
    {
        if (stand.Inhalt["kollegen"] is not List<object?> kollegen || !kollegen.OfType<PyObjekt>().Any(Stadtbau.Questebene.IstQuestgeber))
        {
            return [];
        }
        var auftraege = stand.Auftraege(ort);
        return [.. SpielKern.Regeln.OrtPersonen(ort, stand)
            .Where(p => Stadtbau.Questebene.IstQuestgeber(p) && Stadtbau.Questebene.Etage(p) == etage)
            .Select(p => Person(p, auftraege.Get(p["id"]) is List<object?> { Count: > 0 }))];
    }

    private static Eingaben MitMoebeln(PyObjekt? innenraeume, Eingaben e)
    {
        var (gebaeude, plaetze) = MoebelZusatz(innenraeume, e.Ort, e.Gebaeude);
        gebaeude = MitZusatz(gebaeude, SchrankZusatz(innenraeume, e.Ort));
        if (plaetze.Count == 0)
        {
            return ReferenceEquals(gebaeude, e.Gebaeude) ? e : e with { Gebaeude = gebaeude };
        }
        var personen = e.Personen.Select(p =>
        {
            if (!plaetze.TryGetValue(p.Id, out var platz))
            {
                return p;
            }
            var kopie = Py.Flach(p.Daten);
            Py.Setzen(kopie, "platz", new List<object?> { Py.F(platz[0]), Py.F(platz[1]) });
            return Person(kopie, p.Auftrag);
        }).ToList();
        return e with { Gebaeude = gebaeude, Personen = personen };
    }

    /// <summary>
    /// Phase 12 Welle 1 (Weisung P12-5): Zusatz "zusatz_schraenke" je Ort, nur angehaengt, wirkt nach "zusatz_moebel"
    /// wie <see cref="MitZusatz"/> (Moebelhaus: der alte Kleiderschrank weg, die vier neuen als Auslage).
    /// </summary>
    private static PyObjekt? SchrankZusatz(PyObjekt? innenraeume, string ort) =>
        innenraeume is not null && Get(innenraeume, "zusatz_schraenke") is PyObjekt zs ? Get(zs, ort) as PyObjekt : null;

    /// <summary>
    /// Phase 12 Welle 1: der Grundriss eines Geschaefts (Moebelhaus, Elektromarkt, Kaufhaus) mit allen angehaengten
    /// Zusaetzen in derselben Reihenfolge wie beim Betreten (Ladepruefung der Auslage in <see cref="Geschaefte.Pruefen"/>).
    /// </summary>
    public static PyObjekt LadenGebaeude(PyObjekt innenraeume, string ort)
    {
        var laden = Objekt(innenraeume[ort]);
        var zusatz = Get(innenraeume, "zusatz") is PyObjekt z ? Get(z, ort) as PyObjekt : null;
        var nachtrag = Get(innenraeume, "zusatz_nachtrag") is PyObjekt zn ? Get(zn, ort) as PyObjekt : null;
        var (gebaeude, _) = MoebelZusatz(innenraeume, ort, MitZusatz(MitZusatz(Objekt(laden["gebaeude"]), zusatz), nachtrag));
        return MitZusatz(gebaeude, SchrankZusatz(innenraeume, ort));
    }

    /// <summary>"zusatz_moebel" je Ort: Grundriss mit dem Zusatz und die Plaetze der Personen (leer = keine).</summary>
    private static (PyObjekt Gebaeude, Dictionary<string, List<object?>> Plaetze) MoebelZusatz(PyObjekt? innenraeume, string ort, PyObjekt gebaeude)
    {
        if (innenraeume is null || Get(innenraeume, "zusatz_moebel") is not PyObjekt zm || Get(zm, ort) is not PyObjekt zusatz)
        {
            return (gebaeude, []);
        }
        var vorhanden = Liste(gebaeude["raeume"]).Select(r => Py.Str(Objekt(r)["id"])).ToHashSet();
        var einrichtung = new PyObjekt();
        var plaetze = new Dictionary<string, List<object?>>();
        foreach (var (k, v) in zusatz)
        {
            if (k != "raeume")
            {
                Py.Setzen(einrichtung, k, v);
                continue;
            }
            var raeume = new PyObjekt();
            foreach (var (id, raumZusatz) in Objekt(v))
            {
                if (!vorhanden.Contains(id))
                {
                    continue;
                }
                var ohnePlaetze = new PyObjekt();
                foreach (var (rk, rv) in Objekt(raumZusatz))
                {
                    if (rk == "plaetze")
                    {
                        foreach (var (person, platz) in Objekt(rv))
                        {
                            plaetze[person] = Liste(platz);
                        }
                    }
                    else
                    {
                        Py.Setzen(ohnePlaetze, rk, rv);
                    }
                }
                Py.Setzen(raeume, id, ohnePlaetze);
            }
            Py.Setzen(einrichtung, k, raeume);
        }
        return (MitZusatz(gebaeude, einrichtung), plaetze);
    }

    /// <summary>
    /// Grundriss mit dem angehaengten Zusatz aus innenraeume.json ("zusatz" je Ort, P7-15): Werte wie
    /// wandfarbe, aussenfarbe, boden, bodenfarbe ersetzen die des Grundrisses, Einrichtung ("deko") kommt
    /// hinzu, Raeume nach ihrer id; "ohne" nimmt Einrichtung dieser Typen heraus (W8b). Der Grundriss selbst
    /// bleibt unveraendert (Kopie).
    /// </summary>
    public static PyObjekt MitZusatz(PyObjekt gebaeude, PyObjekt? zusatz)
    {
        // Welle A 1.10.0: ein im Werkzeug geaenderter Raum des Spiels traegt seine Zusaetze schon (nie doppelt einmischen)
        if (zusatz is null || ZusaetzeEingebaut(gebaeude))
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
}
