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

/// <summary>Ein Baustein eines Diagramms (UML-Aktivitaet oder ER in Chen) an einer Stelle im Raster.</summary>
public sealed record EchtElement(string Id, string Typ, string Text, int X, int Y, bool Schluessel);

/// <summary>Eine Verbindung: UML ein Pfeil (Text = Bedingung), ER eine Linie (Kard = 1, n oder m an der Entitaet).</summary>
public sealed record EchtVerbindung(string Von, string Nach, string Text, string Kard);

/// <summary>Diagramm (Vorgabe oder Musterloesung einer Zeichenaufgabe): Art "uml" oder "er".</summary>
public sealed record EchtDiagramm(string Art, IReadOnlyList<EchtElement> Elemente, IReadOnlyList<EchtVerbindung> Verbindungen)
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

    /// <summary>Bausteine je Diagrammart (Plan P12-152, Nicos Antwort 1b).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Bausteine = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        [Uml] = ["start", "aktion", "verzweigung", "zusammenfuehrung", "ende"],
        [Er] = ["entitaet", "beziehung", "attribut"],
    };

    /// <summary>Erlaubte Kardinalitaeten in Chen.</summary>
    public static readonly IReadOnlyList<string> Kardinalitaeten = ["1", "n", "m"];

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
            return new EchtElement(id, typ, text, Stelle(e, "x", RasterBreite, wo), Stelle(e, "y", RasterHoehe, wo), schluessel);
        }).ToList();
        Eindeutig(elemente.Select(e => e.Id), wo, "Baustein");
        Frei(elemente.Select(e => (e.X, e.Y)), wo);
        var verbindungen = d["verbindungen"] is null ? [] : Objekte(d["verbindungen"], wo + " verbindungen", leerErlaubt: true)
            .Select(v => new EchtVerbindung(Pflicht(v, "von", wo), Pflicht(v, "nach", wo), Text(v["text"]) ?? "", Text(v["kard"]) ?? "")).ToList();
        foreach (var v in verbindungen)
        {
            Verbunden(v.Von, v.Nach, elemente.Select(e => e.Id), wo);
        }
        Paare(verbindungen.Select(v => (v.Von, v.Nach)), wo);
        var diagramm = new EchtDiagramm(art, elemente, verbindungen);
        if (art == Uml)
        {
            UmlPruefen(diagramm, vollstaendig, wo);
        }
        else
        {
            ErPruefen(diagramm, vollstaendig, wo);
        }
        return diagramm;
    }

    private static void UmlPruefen(EchtDiagramm d, bool vollstaendig, string wo)
    {
        foreach (var v in d.Verbindungen)
        {
            var von = d.Element(v.Von);
            if (d.Element(v.Nach).Typ == "start" || von.Typ == "ende")
            {
                throw new EchtLadefehler(wo + ": kein Pfeil führt in den Start oder aus dem Ende");
            }
            if (v.Kard.Length > 0)
            {
                throw new EchtLadefehler(wo + ": Kardinalitäten gibt es nur im ER-Modell");
            }
            if (v.Text.Length > 0 && von.Typ != "verzweigung")
            {
                throw new EchtLadefehler(wo + ": eine Bedingung steht nur an Pfeilen aus einer Verzweigung");
            }
        }
        if (!vollstaendig)
        {
            return;
        }
        if (d.Elemente.Count(e => e.Typ == "start") != 1 || !d.Elemente.Any(e => e.Typ == "ende"))
        {
            throw new EchtLadefehler(wo + ": genau ein Start und mindestens ein Ende");
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
                throw new EchtLadefehler(wo + ": " + Baustein(e.Typ) + " " + e.Id + ": " + regel);
            }
        }
    }

    private static void ErPruefen(EchtDiagramm d, bool vollstaendig, string wo)
    {
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
                throw new EchtLadefehler(wo + ": Linie " + v.Von + "–" + v.Nach
                    + ": erlaubt sind Attribut zu Entität oder Beziehung und Beziehung zu Entität (Kardinalität 1, n oder m nur dort)");
            }
            if (v.Text.Length > 0)
            {
                throw new EchtLadefehler(wo + ": Linien im ER-Modell tragen keinen Text, nur eine Kardinalität");
            }
        }
        foreach (var attribut in d.Elemente.Where(e => e.Typ == "attribut"))
        {
            if (d.Verbindungen.Count(v => v.Von == attribut.Id || v.Nach == attribut.Id) != 1)
            {
                throw new EchtLadefehler(wo + ": Attribut " + attribut.Text + " hängt an genau einer Entität oder Beziehung");
            }
        }
        if (!vollstaendig)
        {
            return;
        }
        foreach (var e in d.Elemente)
        {
            var nachbarn = d.Verbindungen.Where(v => v.Von == e.Id || v.Nach == e.Id).Select(v => d.Element(v.Von == e.Id ? v.Nach : v.Von)).ToList();
            if (e.Typ == "entitaet" && nachbarn.Count(n => n.Typ == "attribut" && n.Schluessel) != 1)
            {
                throw new EchtLadefehler(wo + ": Entität " + e.Text + " braucht genau ein Schlüsselattribut");
            }
            if (e.Typ == "beziehung" && nachbarn.Count(n => n.Typ == "entitaet") < 2)
            {
                throw new EchtLadefehler(wo + ": Beziehung " + e.Text + " verbindet mindestens zwei Entitäten");
            }
        }
        if (d.Verbindungen.FirstOrDefault(v => v.Kard.Length == 0 && d.Element(v.Von).Typ != "attribut" && d.Element(v.Nach).Typ != "attribut") is { } ohne)
        {
            throw new EchtLadefehler(wo + ": Linie " + ohne.Von + "–" + ohne.Nach + " ohne Kardinalität");
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
        _ => typ,
    };

    public static string Name(string art) => art == Uml ? EchtTexte.UmlName : EchtTexte.ErName;

    private static bool MitText(string typ) => typ is "aktion" or "entitaet" or "beziehung" or "attribut";

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
