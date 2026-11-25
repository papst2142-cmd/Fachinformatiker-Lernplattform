using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Pruefung.Echt;

/// <summary>Ein Geraet im Netzplan: Symbol (Typ), Name, Stelle im Raster (Spalte x, Zeile y) und kurze Angaben.</summary>
public sealed record EchtKnoten(string Id, string Typ, string Name, int X, int Y, IReadOnlyList<string> Info);

/// <summary>Eine Leitung im Netzplan; der Text (Schnittstelle, VLAN, Netz) steht als Nummer an der Leitung und in der Legende.</summary>
public sealed record EchtKante(string Von, string Nach, string Text);

/// <summary>Netzplan einer Anlage (Welle 2, A 0.77.0): immer aus Daten gezeichnet, nie als Foto.</summary>
public sealed record EchtNetzplan(IReadOnlyList<EchtKnoten> Knoten, IReadOnlyList<EchtKante> Kanten)
{
    public EchtKnoten Geraet(string id) => Knoten.First(k => k.Id == id);

    /// <summary>Leitungen mit Text in der gezeichneten Reihenfolge (Nummer = Index + 1).</summary>
    public IReadOnlyList<EchtKante> Beschriftet => Kanten.Where(k => k.Text.Length > 0).ToList();
}

/// <summary>Eine Messreihe eines Monitor-Bilds (z. B. CPU in %), ein Wert je Zeitmarke, 0 bis Hoechstwert.</summary>
public sealed record EchtReihe(string Name, string Einheit, double Max, IReadOnlyList<double> Werte);

/// <summary>Monitor-Bild einer Anlage: Verlaufslinien je Reihe ueber gemeinsamen Zeitmarken.</summary>
public sealed record EchtMonitor(string Achse, IReadOnlyList<string> Marken, IReadOnlyList<EchtReihe> Reihen);

/// <summary>
/// Ein Baustein eines Diagramms (UML-Aktivitaet, ER in Chen, ab A 1.8.0 Klasse, Akteur, Anwendungsfall) an einer Stelle im
/// Raster. <paramref name="Attribute"/> und <paramref name="Methoden"/> (nur Klasse, A 1.8.0): Zeilen mit "\n" getrennt, leer =
/// keine (als Text, damit zwei gleiche Bausteine gleich sind).
/// </summary>
public sealed record EchtElement(string Id, string Typ, string Text, int X, int Y, bool Schluessel, string Attribute = "", string Methoden = "")
{
    /// <summary>Attributzeilen einer Klasse (leer = keine).</summary>
    public IReadOnlyList<string> AttributZeilen => EchtZeichnung.Zeilen(Attribute);

    /// <summary>Methodenzeilen einer Klasse (leer = keine).</summary>
    public IReadOnlyList<string> MethodenZeilen => EchtZeichnung.Zeilen(Methoden);
}

/// <summary>
/// Eine Verbindung: UML ein Pfeil (Text = Bedingung), ER eine Linie (Kard = 1, n oder m an der Entitaet). Ab A 1.8.0 im
/// Klassen- und Use-Case-Diagramm mit <paramref name="Typ"/> (Art der Beziehung; bei gerichteten Arten zeigt sie von
/// <paramref name="Von"/> nach <paramref name="Nach"/>: Vererbung und Generalisierung zum Allgemeinen, Aggregation und
/// Komposition zum Ganzen, «include» zum eingebundenen Fall, «extend» zum erweiterten Basisfall) und Multiplizitaeten an
/// beiden Enden (<paramref name="MultVon"/> am Ende von <paramref name="Von"/>, <paramref name="MultNach"/> am Ende von
/// <paramref name="Nach"/>).
/// </summary>
public sealed record EchtVerbindung(string Von, string Nach, string Text, string Kard, string Typ = "", string MultVon = "", string MultNach = "");

/// <summary>Systemgrenze eines Use-Case-Diagramms (A 1.8.0): Name und Rechteck aus Feldern des Rasters (immer Vorgabe).</summary>
public sealed record EchtGrenze(string Name, int X, int Y, int Breite, int Hoehe)
{
    public bool Enthaelt(int x, int y) => x >= X && x < X + Breite && y >= Y && y < Y + Hoehe;
}

/// <summary>
/// Diagramm (Vorgabe oder Musterloesung einer Zeichenaufgabe): Art "uml" oder "er", ab A 1.8.0 (Format 4) auch "klasse" und
/// "usecase" (mit <see cref="Grenze"/>).
/// </summary>
public sealed record EchtDiagramm(string Art, IReadOnlyList<EchtElement> Elemente, IReadOnlyList<EchtVerbindung> Verbindungen,
    EchtGrenze? Grenze = null)
{
    public EchtElement Element(string id) => Elemente.First(e => e.Id == id);
}

/// <summary>
/// Welle 2 "Prüfung im echten Format" (Weisung P12-265, Plan P12-152 Abschnitt 3): Lesen und Pruefen der gezeichneten
/// Anlagen (Netzplan, Monitor-Bild) und der Diagramme fuer Zeichenaufgaben (UML-Aktivitaet, ER-Modell in Chen), dazu die
/// Textfassung fuer den Screenreader. Jede Regelverletzung ist ein <see cref="EchtLadefehler"/> mit klarer Meldung.
/// </summary>
public static class EchtZeichnung
{
    public const string Uml = "uml";
    public const string Er = "er";

    public const int RasterBreite = 8;
    public const int RasterHoehe = 12;

    /// <summary>Symbole im Netzplan.</summary>
    public static readonly IReadOnlyDictionary<string, string> Geraete = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["internet"] = "Internet",
        ["router"] = "Router",
        ["firewall"] = "Firewall",
        ["switch"] = "Switch",
        ["server"] = "Server",
        ["nas"] = "NAS",
        ["pc"] = "PC",
        ["notebook"] = "Notebook",
        ["drucker"] = "Drucker",
        ["wlan"] = "WLAN-Zugangspunkt",
    };

    /// <summary>A 1.8.0 (Weisung P12-371, Plan P12-232): UML-Klassendiagramm.</summary>
    public const string Klasse = "klasse";

    /// <summary>A 1.8.0 (Weisung P12-371, Plan P12-232): UML-Use-Case-Diagramm mit vorgegebener Systemgrenze.</summary>
    public const string UseCase = "usecase";

    /// <summary>Hoechstens so viele Zeichen je Text (Name, Attribut, Methode); gleich dem Baukasten.</summary>
    public const int TextLaenge = 60;

    /// <summary>Hoechstens so viele Attribute bzw. Methoden je Klasse.</summary>
    public const int ZeilenMax = 8;

    /// <summary>Bausteine je Diagrammart (Plan P12-152, Nicos Antwort 1b; A 1.8.0 Klasse und Use-Case).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Bausteine = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        [Uml] = ["start", "aktion", "verzweigung", "zusammenfuehrung", "ende"],
        [Er] = ["entitaet", "beziehung", "attribut"],
        [Klasse] = ["klasse"],
        [UseCase] = ["akteur", "anwendungsfall"],
    };

    /// <summary>Diagrammarten, die es erst ab Format 4 (A 1.8.0) gibt.</summary>
    public static readonly IReadOnlyList<string> ArtenFormat4 = [Klasse, UseCase];

    /// <summary>Arten der Beziehungen (A 1.8.0, Nicos Antworten 1a und 2a); die erste ist die beim Verbinden.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Beziehungen = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        [Klasse] = ["assoziation", "vererbung", "aggregation", "komposition"],
        [UseCase] = ["assoziation", "include", "extend", "generalisierung"],
    };

    /// <summary>Erlaubte Kardinalitaeten in Chen.</summary>
    public static readonly IReadOnlyList<string> Kardinalitaeten = ["1", "n", "m"];

    /// <summary>Multiplizitaeten im Klassendiagramm (feste Liste, Plan P12-232).</summary>
    public static readonly IReadOnlyList<string> Multiplizitaeten = ["1", "0..1", "*", "0..*", "1..*"];

    /// <summary>Gerichtete Beziehung (Spitze, Dreieck oder Raute am Ende <see cref="EchtVerbindung.Nach"/>)?</summary>
    public static bool Gerichtet(string typ) => typ is "vererbung" or "aggregation" or "komposition" or "include" or "extend" or "generalisierung";

    /// <summary>Traegt diese Beziehung Multiplizitaeten (Klassendiagramm ohne Vererbung)?</summary>
    public static bool MitMultiplizitaet(string art, string typ) => art == Klasse && typ is "assoziation" or "aggregation" or "komposition";

    /// <summary>Zeilen aus dem gespeicherten Text (mit "\n" getrennt; leer = keine).</summary>
    public static IReadOnlyList<string> Zeilen(string text) => text.Length == 0 ? [] : text.Split('\n');

    // -- Lesen ----------------------------------------------------------------------------------

    public static EchtNetzplan Netzplan(JsonObject a, string wo)
    {
        var knoten = Objekte(a["knoten"], wo + " knoten").Select(k =>
        {
            var id = Pflicht(k, "id", wo);
            var typ = Pflicht(k, "typ", wo);
            if (!Geraete.ContainsKey(typ))
            {
                throw new EchtLadefehler(wo + ": unbekanntes Gerät \"" + typ + "\"");
            }
            return new EchtKnoten(id, typ, Pflicht(k, "name", wo), Stelle(k, "x", RasterBreite, wo), Stelle(k, "y", RasterHoehe, wo),
                k["info"] is null ? [] : Texte(k["info"], wo + " info"));
        }).ToList();
        Eindeutig(knoten.Select(k => k.Id), wo, "Gerät");
        Frei(knoten.Select(k => (k.X, k.Y)), wo);
        var kanten = Objekte(a["kanten"], wo + " kanten").Select(k => new EchtKante(Pflicht(k, "von", wo), Pflicht(k, "nach", wo),
            Text(k["text"]) ?? "")).ToList();
        foreach (var kante in kanten)
        {
            Verbunden(kante.Von, kante.Nach, knoten.Select(k => k.Id), wo);
        }
        Paare(kanten.Select(k => (k.Von, k.Nach)), wo);
        var ohne = knoten.FirstOrDefault(k => kanten.All(l => l.Von != k.Id && l.Nach != k.Id));
        if (ohne is not null && knoten.Count > 1)
        {
            throw new EchtLadefehler(wo + ": " + ohne.Name + " hat keine Leitung");
        }
        return new EchtNetzplan(knoten, kanten);
    }

    public static EchtMonitor Monitor(JsonObject a, string wo)
    {
        var marken = Texte(a["marken"], wo + " marken");
        if (marken.Count < 2)
        {
            throw new EchtLadefehler(wo + ": ein Monitor-Bild braucht mindestens zwei Zeitmarken");
        }
        var reihen = Objekte(a["reihen"], wo + " reihen").Select(r =>
        {
            var name = Pflicht(r, "name", wo);
            var max = Zahl(r["max"]) ?? throw new EchtLadefehler(wo + ": Reihe " + name + " ohne \"max\"");
            if (r["werte"] is not JsonArray liste)
            {
                throw new EchtLadefehler(wo + ": Reihe " + name + " ohne \"werte\"");
            }
            var werte = liste.Select(w => Zahl(w) ?? throw new EchtLadefehler(wo + ": Reihe " + name + " enthält etwas anderes als Zahlen")).ToList();
            if (max <= 0 || werte.Count != marken.Count || werte.Any(w => w < 0 || w > max))
            {
                throw new EchtLadefehler(wo + ": Reihe " + name + " braucht je Zeitmarke einen Wert von 0 bis zum Höchstwert");
            }
            return new EchtReihe(name, Text(r["einheit"]) ?? "", max, werte);
        }).ToList();
        return new EchtMonitor(Pflicht(a, "achse", wo), marken, reihen);
    }

    /// <summary>Diagramm lesen; <paramref name="vollstaendig"/> = Musterloesung (strengere Regeln als die Vorgabe).</summary>
    public static EchtDiagramm Diagramm(JsonNode? knoten, string art, bool vollstaendig, string wo)
    {
        if (knoten is not JsonObject d)
        {
            throw new EchtLadefehler(wo + " fehlt");
        }
        if (Text(d["art"]) != art)
        {
            throw new EchtLadefehler(wo + ": Art muss \"" + art + "\" sein");
        }
        var typen = Bausteine[art];
        var elemente = Objekte(d["elemente"], wo + " elemente").Select(e =>
        {
            var id = Pflicht(e, "id", wo);
            var typ = Pflicht(e, "typ", wo);
            if (!typen.Contains(typ))
            {
                throw new EchtLadefehler(wo + ": Baustein \"" + typ + "\" gibt es im " + Name(art) + " nicht");
            }
            var text = Text(e["text"]) ?? "";
            if (MitText(typ) && text.Length == 0)
            {
                throw new EchtLadefehler(wo + ": " + Baustein(typ) + " " + id + " ohne Text");
            }
            var schluessel = e["schluessel"] is JsonValue s && s.TryGetValue<bool>(out var ja) && ja;
            if (schluessel && typ != "attribut")
            {
                throw new EchtLadefehler(wo + ": nur ein Attribut kann Schlüssel sein");
            }
            // A 1.8.0: Attribute und Methoden nur an einer Klasse, je Zeile Text bis zur Laenge des Baukastens
            if ((e["attribute"] is not null || e["methoden"] is not null) && typ != "klasse")
            {
                throw new EchtLadefehler(wo + ": Attribute und Methoden gibt es nur an einer Klasse");
            }
            var attribute = KlassenZeilen(e["attribute"], wo, id, "attribute");
            var methoden = KlassenZeilen(e["methoden"], wo, id, "methoden");
            if (ArtenFormat4.Contains(art) && text.Length > TextLaenge)
            {
                throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: Name von {id} länger als {TextLaenge} Zeichen"));
            }
            return new EchtElement(id, typ, text, Stelle(e, "x", RasterBreite, wo), Stelle(e, "y", RasterHoehe, wo), schluessel, attribute, methoden);
        }).ToList();
        Eindeutig(elemente.Select(e => e.Id), wo, "Baustein");
        Frei(elemente.Select(e => (e.X, e.Y)), wo);
        var verbindungen = d["verbindungen"] is null ? [] : Objekte(d["verbindungen"], wo + " verbindungen", leerErlaubt: true)
            .Select(v => new EchtVerbindung(Pflicht(v, "von", wo), Pflicht(v, "nach", wo), Text(v["text"]) ?? "", Text(v["kard"]) ?? "",
                Text(v["typ"]) ?? "", Text(v["mult_von"]) ?? "", Text(v["mult_nach"]) ?? "")).ToList();
        foreach (var v in verbindungen)
        {
            Verbunden(v.Von, v.Nach, elemente.Select(e => e.Id), wo);
        }
        Paare(verbindungen.Select(v => (v.Von, v.Nach)), wo);
        EchtGrenze? grenze = null;
        if (d["grenze"] is not null || art == UseCase)
        {
            if (art != UseCase)
            {
                throw new EchtLadefehler(wo + ": eine Systemgrenze gibt es nur im Use-Case-Diagramm");
            }
            if (d["grenze"] is not JsonObject g)
            {
                throw new EchtLadefehler(wo + ": ein Use-Case-Diagramm braucht die Systemgrenze (\"grenze\")");
            }
            grenze = new EchtGrenze(Pflicht(g, "name", wo), Stelle(g, "x", RasterBreite, wo), Stelle(g, "y", RasterHoehe, wo),
                Stelle(g, "breite", RasterBreite + 1, wo), Stelle(g, "hoehe", RasterHoehe + 1, wo));
            if (grenze.Breite < 1 || grenze.Hoehe < 1 || grenze.X + grenze.Breite > RasterBreite || grenze.Y + grenze.Hoehe > RasterHoehe)
            {
                throw new EchtLadefehler(wo + ": die Systemgrenze liegt nicht ganz im Raster");
            }
        }
        var diagramm = new EchtDiagramm(art, elemente, verbindungen, grenze);
        if (Verstoesse(diagramm, vollstaendig).FirstOrDefault() is { } fehler)
        {
            throw new EchtLadefehler(wo + ": " + fehler);
        }
        return diagramm;
    }

    /// <summary>
    /// Alle Regelverstoesse eines Diagramms in fester Reihenfolge (der erste ist die Meldung der Ladepruefung);
    /// <paramref name="vollstaendig"/> = Regeln eines fertigen Diagramms (Musterbild, Pruefung im Baukasten, A 0.78.0);
    /// <paramref name="name"/> benennt Bausteine in den Meldungen (ohne: Kennung bzw. Text wie in der Ladepruefung).
    /// </summary>
    public static IEnumerable<string> Verstoesse(EchtDiagramm d, bool vollstaendig, Func<EchtElement, string>? name = null) => d.Art switch
    {
        Uml => UmlVerstoesse(d, vollstaendig, name ?? (e => Baustein(e.Typ) + " " + e.Id)).Concat(NurNeueArten(d)),
        Klasse => KlassenVerstoesse(d, vollstaendig, name ?? Benannt),
        UseCase => UseCaseVerstoesse(d, vollstaendig, name ?? Benannt),
        _ => ErVerstoesse(d, vollstaendig, name).Concat(NurNeueArten(d)),
    };

    /// <summary>Name in Meldungen der Ladepruefung ohne Namensfunktion: "Klasse „Kunde“", ohne Text die Kennung.</summary>
    private static string Benannt(EchtElement e) => Baustein(e.Typ) + (e.Text.Length > 0 ? " „" + e.Text + "“" : " " + e.Id);

    /// <summary>Aktivitaet und ER (A 0.77.0) kennen keine Beziehungsarten und keine Multiplizitaeten (A 1.8.0).</summary>
    private static IEnumerable<string> NurNeueArten(EchtDiagramm d)
    {
        if (d.Verbindungen.Any(v => v.Typ.Length > 0 || v.MultVon.Length > 0 || v.MultNach.Length > 0))
        {
            yield return "Beziehungsarten und Multiplizitäten gibt es nur im Klassen- und Use-Case-Diagramm";
        }
    }

    private static IEnumerable<string> KlassenVerstoesse(EchtDiagramm d, bool vollstaendig, Func<EchtElement, string> name)
    {
        foreach (var doppelt in d.Elemente.GroupBy(e => NameVergleich(e.Text)).Where(g => g.Key.Length > 0 && g.Count() > 1))
        {
            yield return "zwei Klassen heißen „" + doppelt.First().Text + "“";
        }
        foreach (var v in d.Verbindungen)
        {
            var paar = name(d.Element(v.Von)) + "–" + name(d.Element(v.Nach));
            if (!Beziehungen[Klasse].Contains(v.Typ))
            {
                yield return paar + ": Art muss Assoziation, Vererbung, Aggregation oder Komposition sein";
                continue;
            }
            if (v.Text.Length > 0 || v.Kard.Length > 0)
            {
                yield return "Beziehungen im Klassendiagramm tragen keinen Text und keine Kardinalität, nur Multiplizitäten";
            }
            if (!Multiplizitaeten.Contains(v.MultVon) && v.MultVon.Length > 0 || !Multiplizitaeten.Contains(v.MultNach) && v.MultNach.Length > 0)
            {
                yield return paar + ": Multiplizität nur 1, 0..1, *, 0..* oder 1..*";
            }
            if (v.Typ == "vererbung" && (v.MultVon.Length > 0 || v.MultNach.Length > 0))
            {
                yield return paar + ": eine Vererbung hat keine Multiplizität";
            }
            if (v.Typ == "komposition" && v.MultNach is not ("" or "1" or "0..1"))
            {
                yield return paar + ": bei einer Komposition gehört ein Teil zu höchstens einem Ganzen (Multiplizität am Ganzen 1 oder 0..1)";
            }
        }
        if (ImKreis(d, "vererbung") is { } kreis)
        {
            yield return "Vererbung im Kreis bei " + name(kreis);
        }
        if (!vollstaendig)
        {
            yield break;
        }
        foreach (var v in d.Verbindungen.Where(v => MitMultiplizitaet(Klasse, v.Typ) && (v.MultVon.Length == 0 || v.MultNach.Length == 0)))
        {
            yield return name(d.Element(v.Von)) + "–" + name(d.Element(v.Nach)) + ": Multiplizität an beiden Enden fehlt";
        }
        foreach (var e in d.Elemente.Where(e => e.Text.Trim().Length == 0))
        {
            yield return name(e) + " ohne Namen";
        }
        if (d.Elemente.Count > 1)
        {
            foreach (var e in d.Elemente.Where(e => d.Verbindungen.All(v => v.Von != e.Id && v.Nach != e.Id)))
            {
                yield return name(e) + " hat keine Beziehung";
            }
        }
    }

    private static IEnumerable<string> UseCaseVerstoesse(EchtDiagramm d, bool vollstaendig, Func<EchtElement, string> name)
    {
        foreach (var doppelt in d.Elemente.GroupBy(e => e.Typ + "\n" + NameVergleich(e.Text)).Where(g => g.Count() > 1
            && NameVergleich(g.First().Text).Length > 0))
        {
            yield return "zwei Bausteine „" + doppelt.First().Text + "“ gleicher Art";
        }
        if (d.Grenze is { } g)
        {
            foreach (var e in d.Elemente)
            {
                if (e.Typ == "akteur" && g.Enthaelt(e.X, e.Y))
                {
                    yield return name(e) + ": ein Akteur steht außerhalb der Systemgrenze";
                }
                if (e.Typ == "anwendungsfall" && !g.Enthaelt(e.X, e.Y))
                {
                    yield return name(e) + ": ein Anwendungsfall steht innerhalb der Systemgrenze";
                }
            }
        }
        else
        {
            yield return "ein Use-Case-Diagramm braucht die Systemgrenze";
        }
        foreach (var v in d.Verbindungen)
        {
            var a = d.Element(v.Von);
            var b = d.Element(v.Nach);
            var paar = name(a) + "–" + name(b);
            if (!Beziehungen[UseCase].Contains(v.Typ))
            {
                yield return paar + ": Art muss Assoziation, «include», «extend» oder Generalisierung sein";
                continue;
            }
            if (v.Text.Length > 0 || v.Kard.Length > 0 || v.MultVon.Length > 0 || v.MultNach.Length > 0)
            {
                yield return "im Use-Case-Diagramm tragen Beziehungen keinen Text und keine Multiplizität";
            }
            var (ok, regel) = v.Typ switch
            {
                "assoziation" => (a.Typ != b.Typ, "eine Assoziation verbindet einen Akteur mit einem Anwendungsfall"),
                "include" or "extend" => (a.Typ == "anwendungsfall" && b.Typ == "anwendungsfall", "«include» und «extend» verbinden nur Anwendungsfälle"),
                _ => (a.Typ == b.Typ, "eine Generalisierung verbindet nur gleiche Bausteine (Akteur mit Akteur, Anwendungsfall mit Anwendungsfall)"),
            };
            if (!ok)
            {
                yield return paar + ": " + regel;
            }
        }
        if (ImKreis(d, "generalisierung") is { } kreis)
        {
            yield return "Generalisierung im Kreis bei " + name(kreis);
        }
        if (!vollstaendig)
        {
            yield break;
        }
        foreach (var e in d.Elemente.Where(e => e.Text.Trim().Length == 0))
        {
            yield return name(e) + " ohne Namen";
        }
        foreach (var e in d.Elemente)
        {
            var eigene = d.Verbindungen.Where(v => v.Von == e.Id || v.Nach == e.Id).ToList();
            if (e.Typ == "akteur" && !eigene.Any(v => v.Typ is "assoziation" or "generalisierung"))
            {
                yield return name(e) + ": ein Akteur ist mit einem Anwendungsfall verbunden";
            }
            if (e.Typ == "anwendungsfall" && eigene.Count == 0)
            {
                yield return name(e) + " hat keine Beziehung";
            }
        }
    }

    /// <summary>Erster Baustein in einem Kreis aus Beziehungen dieser Art (Vererbung, Generalisierung), sonst null.</summary>
    private static EchtElement? ImKreis(EchtDiagramm d, string typ)
    {
        var nach = d.Verbindungen.Where(v => v.Typ == typ).ToLookup(v => v.Von, v => v.Nach, StringComparer.Ordinal);
        foreach (var start in d.Elemente)
        {
            var gesehen = new HashSet<string>(StringComparer.Ordinal);
            var offen = new Stack<string>(nach[start.Id]);
            while (offen.Count > 0)
            {
                var id = offen.Pop();
                if (id == start.Id)
                {
                    return start;
                }
                if (gesehen.Add(id))
                {
                    foreach (var weiter in nach[id])
                    {
                        offen.Push(weiter);
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Namen tolerant vergleichen (Plan P12-232 Abschnitt 3): Gross/klein egal, Leerzeichen egal, ä = ae, ö = oe, ü = ue,
    /// ß = ss. Ergebnis ist der Vergleichsschluessel.
    /// </summary>
    public static string NameVergleich(string name)
    {
        var klein = Lernplattform.Core.Suche.PythonKlein.Klein(name);
        var sb = new System.Text.StringBuilder(klein.Length);
        foreach (var c in klein)
        {
            switch (c)
            {
                case 'ä':
                    sb.Append("ae");
                    break;
                case 'ö':
                    sb.Append("oe");
                    break;
                case 'ü':
                    sb.Append("ue");
                    break;
                case 'ß':
                    sb.Append("ss");
                    break;
                default:
                    if (!char.IsWhiteSpace(c))
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    private static IEnumerable<string> UmlVerstoesse(EchtDiagramm d, bool vollstaendig, Func<EchtElement, string> name)
    {
        foreach (var v in d.Verbindungen)
        {
            var von = d.Element(v.Von);
            if (d.Element(v.Nach).Typ == "start" || von.Typ == "ende")
            {
                yield return "kein Pfeil führt in den Start oder aus dem Ende";
            }
            if (v.Kard.Length > 0)
            {
                yield return "Kardinalitäten gibt es nur im ER-Modell";
            }
            if (v.Text.Length > 0 && von.Typ != "verzweigung")
            {
                yield return "eine Bedingung steht nur an Pfeilen aus einer Verzweigung";
            }
        }
        if (!vollstaendig)
        {
            yield break;
        }
        if (d.Elemente.Count(e => e.Typ == "start") != 1 || !d.Elemente.Any(e => e.Typ == "ende"))
        {
            yield return "genau ein Start und mindestens ein Ende";
        }
        foreach (var e in d.Elemente)
        {
            var aus = d.Verbindungen.Where(v => v.Von == e.Id).ToList();
            var ein = d.Verbindungen.Count(v => v.Nach == e.Id);
            var (ok, regel) = e.Typ switch
            {
                "start" => (aus.Count == 1, "der Start hat genau einen Pfeil"),
                "ende" => (ein >= 1, "zum Ende führt ein Pfeil"),
                "aktion" => (ein >= 1 && aus.Count == 1, "eine Aktion hat einen Pfeil hinein und genau einen hinaus"),
                "verzweigung" => (ein == 1 && aus.Count >= 2 && aus.All(v => v.Text.Length > 0),
                    "eine Verzweigung hat einen Pfeil hinein und mindestens zwei hinaus, jeder mit Bedingung"),
                _ => (ein >= 2 && aus.Count == 1, "eine Zusammenführung hat mindestens zwei Pfeile hinein und einen hinaus"),
            };
            if (!ok)
            {
                yield return name(e) + ": " + regel;
            }
        }
    }

    private static IEnumerable<string> ErVerstoesse(EchtDiagramm d, bool vollstaendig, Func<EchtElement, string>? name)
    {
        // ohne Namensfunktion wie die Ladepruefung: "Attribut Name", "Linie a–b"; mit ihr: "Attribut „Name“"
        string N(EchtElement e, bool text) => name is not null ? name(e) : text ? Baustein(e.Typ) + " " + e.Text : e.Id;
        foreach (var v in d.Verbindungen)
        {
            var typen = new[] { d.Element(v.Von).Typ, d.Element(v.Nach).Typ }.Order(StringComparer.Ordinal).ToArray();
            var ok = typen switch
            {
                ["attribut", "beziehung" or "entitaet"] => v.Kard.Length == 0,
                ["beziehung", "entitaet"] => v.Kard.Length == 0 || Kardinalitaeten.Contains(v.Kard),
                _ => false,
            };
            if (!ok)
            {
                yield return "Linie " + N(d.Element(v.Von), false) + "–" + N(d.Element(v.Nach), false)
                    + ": erlaubt sind Attribut zu Entität oder Beziehung und Beziehung zu Entität (Kardinalität 1, n oder m nur dort)";
            }
            if (v.Text.Length > 0)
            {
                yield return "Linien im ER-Modell tragen keinen Text, nur eine Kardinalität";
            }
        }
        foreach (var attribut in d.Elemente.Where(e => e.Typ == "attribut"))
        {
            if (d.Verbindungen.Count(v => v.Von == attribut.Id || v.Nach == attribut.Id) != 1)
            {
                yield return N(attribut, true) + " hängt an genau einer Entität oder Beziehung";
            }
        }
        if (!vollstaendig)
        {
            yield break;
        }
        foreach (var e in d.Elemente)
        {
            var nachbarn = d.Verbindungen.Where(v => v.Von == e.Id || v.Nach == e.Id).Select(v => d.Element(v.Von == e.Id ? v.Nach : v.Von)).ToList();
            if (e.Typ == "entitaet" && nachbarn.Count(n => n.Typ == "attribut" && n.Schluessel) != 1)
            {
                yield return N(e, true) + " braucht genau ein Schlüsselattribut";
            }
            if (e.Typ == "beziehung" && nachbarn.Count(n => n.Typ == "entitaet") < 2)
            {
                yield return N(e, true) + " verbindet mindestens zwei Entitäten";
            }
        }
        if (d.Verbindungen.FirstOrDefault(v => v.Kard.Length == 0 && d.Element(v.Von).Typ != "attribut" && d.Element(v.Nach).Typ != "attribut") is { } ohne)
        {
            yield return "Linie " + N(d.Element(ohne.Von), false) + "–" + N(d.Element(ohne.Nach), false) + " ohne Kardinalität";
        }
    }

    // -- Textfassung fuer den Screenreader (auch sichtbar ueber "Als Text zeigen") --------------------

    /// <summary>Netzplan als Zeilen: je Geraet Name, Symbol, Angaben und seine Leitungen.</summary>
    public static IReadOnlyList<string> Textfassung(EchtNetzplan plan)
    {
        var zeilen = new List<string>();
        foreach (var k in plan.Knoten.OrderBy(k => k.Y).ThenBy(k => k.X))
        {
            var satz = k.Name + " (" + Geraete[k.Typ] + ")" + (k.Info.Count > 0 ? ": " + string.Join(", ", k.Info) : "");
            var leitungen = plan.Kanten.Where(l => l.Von == k.Id || l.Nach == k.Id)
                .Select(l => plan.Geraet(l.Von == k.Id ? l.Nach : l.Von).Name + (l.Text.Length > 0 ? " über " + l.Text : "")).ToList();
            if (leitungen.Count > 0)
            {
                satz += "; " + EchtTexte.VerbundenMit + " " + string.Join("; ", leitungen);
            }
            zeilen.Add(satz + ".");
        }
        return zeilen;
    }

    /// <summary>Monitor-Bild als Zeilen: je Reihe alle Werte mit Zeitmarke, dazu der hoechste Wert.</summary>
    public static IReadOnlyList<string> Textfassung(EchtMonitor monitor)
    {
        var zeilen = new List<string>();
        foreach (var r in monitor.Reihen)
        {
            var einheit = r.Einheit.Length > 0 ? " " + r.Einheit : "";
            var werte = monitor.Marken.Select((m, i) => m + " " + Wert(r.Werte[i]) + einheit);
            var hoch = r.Werte.Max();
            var wann = monitor.Marken[r.Werte.ToList().IndexOf(hoch)];
            zeilen.Add(r.Name + ": " + string.Join(", ", werte) + ". "
                + Beschriftungen.Einsetzen(EchtTexte.Hoechstwert, Wert(hoch) + einheit, wann));
        }
        return zeilen;
    }

    /// <summary>Diagramm als Zeilen: je Baustein Art, Text und "verbunden mit …" (Grundlage fuer den Screenreader-Weg).</summary>
    public static IReadOnlyList<string> Textfassung(EchtDiagramm d)
    {
        if (ArtenFormat4.Contains(d.Art))
        {
            return [.. GrenzeText(d), .. d.Elemente.OrderBy(e => e.Y).ThenBy(e => e.X).Select(e => Beschreibung(d, e, "") + ".")];
        }
        var zeilen = new List<string>();
        foreach (var e in d.Elemente.OrderBy(e => e.Y).ThenBy(e => e.X))
        {
            if (d.Art == Er && e.Typ == "attribut")
            {
                continue;    // Attribute stehen bei ihrer Entitaet oder Beziehung
            }
            var satz = Bezeichnung(d, e);
            if (d.Art == Er)
            {
                var attribute = d.Verbindungen.Where(v => v.Von == e.Id || v.Nach == e.Id).Select(v => d.Element(v.Von == e.Id ? v.Nach : v.Von))
                    .Where(n => n.Typ == "attribut").Select(n => n.Text + (n.Schluessel ? " " + EchtTexte.SchluesselZusatz : "")).ToList();
                if (attribute.Count > 0)
                {
                    satz += "; " + EchtTexte.Attribute + ": " + string.Join(", ", attribute);
                }
                if (e.Typ == "beziehung")
                {
                    var seiten = d.Verbindungen.Where(v => v.Von == e.Id || v.Nach == e.Id)
                        .Select(v => (Element: d.Element(v.Von == e.Id ? v.Nach : v.Von), v.Kard)).Where(s => s.Element.Typ == "entitaet")
                        .Select(s => s.Element.Text + (s.Kard.Length > 0 ? " (" + s.Kard + ")" : "")).ToList();
                    if (seiten.Count > 0)
                    {
                        satz += "; " + EchtTexte.Zwischen + " " + string.Join(" " + EchtTexte.Und + " ", seiten);
                    }
                }
            }
            else
            {
                var ziele = d.Verbindungen.Where(v => v.Von == e.Id)
                    .Select(v => (v.Text.Length > 0 ? v.Text + " " : "") + Bezeichnung(d, d.Element(v.Nach))).ToList();
                if (ziele.Count > 0)
                {
                    satz += "; " + EchtTexte.WeiterZu + " " + string.Join("; ", ziele);
                }
            }
            zeilen.Add(satz + ".");
        }
        return zeilen;
    }

    /// <summary>Use-Case: Zeile zur Systemgrenze (Textfassung und Liste des Baukastens), sonst nichts.</summary>
    public static IEnumerable<string> GrenzeText(EchtDiagramm d)
    {
        if (d.Grenze is { } g)
        {
            yield return Beschriftungen.Einsetzen(EchtTexte.GrenzeZeile, g.Name, g.X + 1, g.X + g.Breite, g.Y + 1, g.Y + g.Hoehe);
        }
    }

    /// <summary>
    /// A 1.8.0 Klasse und Use-Case: Baustein mit Attributen, Methoden und allen Beziehungen aus seiner Sicht (Textfassung,
    /// Liste "Deine Zeichnung"); <paramref name="stelle"/> wird hinter der Bezeichnung eingefuegt (leer = ohne).
    /// </summary>
    public static string Beschreibung(EchtDiagramm d, EchtElement e, string stelle)
    {
        var satz = Bezeichnung(d, e) + stelle;
        if (e.Typ == "klasse")
        {
            var attribute = e.AttributZeilen.Where(z => z.Trim().Length > 0).ToList();
            var methoden = e.MethodenZeilen.Where(z => z.Trim().Length > 0).ToList();
            satz += "; " + EchtTexte.Attribute + ": " + (attribute.Count > 0 ? string.Join(", ", attribute) : EchtTexte.Keine);
            satz += "; " + EchtTexte.Methoden + ": " + (methoden.Count > 0 ? string.Join(", ", methoden) : EchtTexte.Keine);
        }
        var beziehungen = d.Verbindungen.Where(v => v.Von == e.Id || v.Nach == e.Id).Select(v => BeziehungAus(d, e, v)).ToList();
        return satz + "; " + (beziehungen.Count > 0 ? string.Join("; ", beziehungen) : EchtTexte.OhneVerbindung);
    }

    /// <summary>Eine Beziehung aus Sicht von <paramref name="e"/>: "erbt von Klasse „Fahrrad“", "«include» Anwendungsfall „Ausweis prüfen“".</summary>
    public static string BeziehungAus(EchtDiagramm d, EchtElement e, EchtVerbindung v)
    {
        var vorn = v.Von == e.Id;
        var anderer = Bezeichnung(d, d.Element(vorn ? v.Nach : v.Von));
        var muster = (v.Typ, vorn) switch
        {
            ("vererbung", true) => EchtTexte.ErbtVon,
            ("vererbung", false) => EchtTexte.OberklasseVon,
            ("aggregation", true) => EchtTexte.TeilVonAggregation,
            ("aggregation", false) => EchtTexte.GanzesAggregation,
            ("komposition", true) => EchtTexte.TeilVonKomposition,
            ("komposition", false) => EchtTexte.GanzesKomposition,
            ("include", true) => EchtTexte.BindetEin,
            ("include", false) => EchtTexte.EingebundenVon,
            ("extend", true) => EchtTexte.Erweitert,
            ("extend", false) => EchtTexte.ErweitertDurch,
            ("generalisierung", true) => EchtTexte.Spezialisiert,
            ("generalisierung", false) => EchtTexte.Verallgemeinert,
            _ => EchtTexte.AssoziationMit,
        };
        var text = Beschriftungen.Einsetzen(muster, anderer);
        if (MitMultiplizitaet(d.Art, v.Typ) && (v.MultVon.Length > 0 || v.MultNach.Length > 0))
        {
            var (hier, dort) = vorn ? (v.MultVon, v.MultNach) : (v.MultNach, v.MultVon);
            text += " " + Beschriftungen.Einsetzen(EchtTexte.MultHierDort, hier.Length > 0 ? hier : EchtTexte.OhneKardinalitaet,
                dort.Length > 0 ? dort : EchtTexte.OhneKardinalitaet);
        }
        return text;
    }

    /// <summary>"Aktion „Ticket anlegen“", "Verzweigung 1", "Start", "Entität Kunde".</summary>
    public static string Bezeichnung(EchtDiagramm d, EchtElement e)
    {
        if (e.Text.Length > 0)
        {
            return Baustein(e.Typ) + " „" + e.Text + "“";
        }
        var gleiche = d.Elemente.Where(x => x.Typ == e.Typ).OrderBy(x => x.Y).ThenBy(x => x.X).ToList();
        return gleiche.Count > 1 ? Baustein(e.Typ) + " " + (gleiche.IndexOf(e) + 1).ToString(CultureInfo.InvariantCulture) : Baustein(e.Typ);
    }

    public static string Baustein(string typ) => typ switch
    {
        "start" => "Start",
        "aktion" => "Aktion",
        "verzweigung" => "Verzweigung",
        "zusammenfuehrung" => "Zusammenführung",
        "ende" => "Ende",
        "entitaet" => "Entität",
        "beziehung" => "Beziehung",
        "attribut" => "Attribut",
        "klasse" => "Klasse",
        "akteur" => "Akteur",
        "anwendungsfall" => "Anwendungsfall",
        _ => typ,
    };

    /// <summary>Name einer Beziehungsart (A 1.8.0): "Assoziation", "«include»" ...</summary>
    public static string Beziehung(string typ) => typ switch
    {
        "assoziation" => "Assoziation",
        "vererbung" => "Vererbung",
        "aggregation" => "Aggregation",
        "komposition" => "Komposition",
        "include" => "«include»",
        "extend" => "«extend»",
        "generalisierung" => "Generalisierung",
        _ => typ,
    };

    public static string Name(string art) => art switch
    {
        Uml => EchtTexte.UmlName,
        Klasse => EchtTexte.KlasseName,
        UseCase => EchtTexte.UseCaseName,
        _ => EchtTexte.ErName,
    };

    private static bool MitText(string typ) => typ is "aktion" or "entitaet" or "beziehung" or "attribut" or "klasse" or "akteur" or "anwendungsfall";

    /// <summary>Attribut- oder Methodenzeilen einer Klasse lesen (Liste von Texten, jede nicht leer, bis 60 Zeichen).</summary>
    private static string KlassenZeilen(JsonNode? knoten, string wo, string id, string was)
    {
        if (knoten is null)
        {
            return "";
        }
        var zeilen = Texte(knoten, wo + " " + was);
        if (zeilen.Count > ZeilenMax || zeilen.Any(z => z.Trim().Length == 0 || z.Length > TextLaenge || z.Contains('\n') || z.Contains('\r')))
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture,
                $"{wo}: {id} {was}: höchstens {ZeilenMax} Zeilen, jede nicht leer und höchstens {TextLaenge} Zeichen"));
        }
        return string.Join('\n', zeilen);
    }

    private static string Wert(double wert) => wert.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));

    // -- kleine Lesehilfen ------------------------------------------------------------------------

    private static int Stelle(JsonObject o, string name, int grenze, string wo)
    {
        if (o[name] is JsonValue v && v.GetValueKind() == JsonValueKind.Number && v.TryGetValue<int>(out var i) && i >= 0 && i < grenze)
        {
            return i;
        }
        throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: \"{name}\" muss eine ganze Zahl von 0 bis {grenze - 1} sein"));
    }

    private static void Eindeutig(IEnumerable<string> ids, string wo, string was)
    {
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        if (ids.FirstOrDefault(id => !gesehen.Add(id)) is { } doppelt)
        {
            throw new EchtLadefehler(wo + ": " + was + " " + doppelt + " doppelt");
        }
    }

    private static void Frei(IEnumerable<(int X, int Y)> stellen, string wo)
    {
        var gesehen = new HashSet<(int, int)>();
        foreach (var s in stellen)
        {
            if (!gesehen.Add(s))
            {
                throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: zwei Teile an derselben Stelle ({s.X}, {s.Y})"));
            }
        }
    }

    private static void Verbunden(string von, string nach, IEnumerable<string> ids, string wo)
    {
        var alle = ids.ToHashSet(StringComparer.Ordinal);
        if (!alle.Contains(von) || !alle.Contains(nach) || von == nach)
        {
            throw new EchtLadefehler(wo + ": Verbindung " + von + "–" + nach + " passt zu keinem Teil");
        }
    }

    private static void Paare(IEnumerable<(string Von, string Nach)> paare, string wo)
    {
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (von, nach) in paare)
        {
            var schluessel = string.CompareOrdinal(von, nach) < 0 ? von + "\n" + nach : nach + "\n" + von;
            if (!gesehen.Add(schluessel))
            {
                throw new EchtLadefehler(wo + ": Verbindung " + von + "–" + nach + " doppelt");
            }
        }
    }

    private static string Pflicht(JsonObject o, string name, string wo) =>
        Text(o[name]) is { Length: > 0 } wert ? wert : throw new EchtLadefehler(wo + ": \"" + name + "\" fehlt oder ist leer");

    private static string? Text(JsonNode? knoten) => knoten is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static double? Zahl(JsonNode? knoten) => knoten is JsonValue v && v.GetValueKind() == JsonValueKind.Number
        && v.TryGetValue<double>(out var d) ? d : null;

    private static List<string> Texte(JsonNode? knoten, string wo) => knoten is JsonArray liste
        ? liste.Select(z => Text(z) ?? throw new EchtLadefehler("\"" + wo + "\" enthält etwas anderes als Text")).ToList()
        : throw new EchtLadefehler("\"" + wo + "\" ist keine Liste von Texten");

    private static List<JsonObject> Objekte(JsonNode? knoten, string wo, bool leerErlaubt = false)
    {
        if (knoten is not JsonArray liste || (liste.Count == 0 && !leerErlaubt))
        {
            throw new EchtLadefehler("\"" + wo + "\" fehlt oder ist leer");
        }
        if (liste.Any(x => x is not JsonObject))
        {
            throw new EchtLadefehler("\"" + wo + "\" enthält etwas anderes als Objekte");
        }
        return liste.Cast<JsonObject>().ToList();
    }
}
