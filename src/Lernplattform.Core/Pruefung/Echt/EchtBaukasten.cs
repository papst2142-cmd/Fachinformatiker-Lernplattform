using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Pruefung.Echt;

/// <summary>Eine Zeile der Pruefung durch das Programm (Zaehlbares nach dem Abgeben).</summary>
public sealed record EchtZaehlung(string Text, bool Ok);

/// <summary>
/// A 0.78.0 "Zeichen-Baukasten" (Weisung P12-266, Plan P12-152 Abschnitt 3 Antwort b): die eigene Zeichnung einer
/// Zeichenaufgabe (UML-Aktivitaet oder ER in Chen) im Raster von <see cref="EchtZeichnung"/>, auf denselben Daten
/// (<see cref="EchtElement"/>, <see cref="EchtVerbindung"/>, <see cref="EchtDiagramm"/>) und denselben Regeln. Die Vorgabe
/// steht fest darin (nicht verschiebbar, nicht loeschbar). Jede Aenderung laeuft ueber eine Methode dieser Klasse, die
/// nur Zustaende zulaesst, die sich wieder lesen lassen (z. B. kein Pfeil in den Start, Attribut an hoechstens einer Stelle).
/// Gespeichert wird die Zeichnung als JSON-Text in der Antwort <c>&lt;aufgabe&gt;.&lt;nr&gt;/bau</c> (leer = nichts Eigenes).
/// </summary>
public sealed class EchtBaukasten
{
    /// <summary>Schluessel der Antwort "im Baukasten gezeichnet" einer Zeichenaufgabe.</summary>
    public const string Feld = "bau";

    /// <summary>Hoechstens so viele Zeichen je Text (Baustein, Bedingung).</summary>
    public const int TextLaenge = 60;

    private static readonly JsonSerializerOptions Ausgabe = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly List<EchtElement> _elemente;
    private readonly List<EchtVerbindung> _verbindungen;
    private readonly HashSet<string> _fest;
    private readonly Dictionary<string, EchtVerbindung> _festeVerbindungen;
    private readonly IReadOnlyList<EchtElement> _vorgabeElemente;

    private EchtBaukasten(string art, EchtDiagramm? vorgabe)
    {
        Art = art;
        _vorgabeElemente = vorgabe?.Elemente ?? [];
        _elemente = [.. _vorgabeElemente];
        _verbindungen = [.. vorgabe?.Verbindungen ?? []];
        _fest = _elemente.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        _festeVerbindungen = _verbindungen.ToDictionary(v => Paar(v.Von, v.Nach), StringComparer.Ordinal);
    }

    /// <summary>"uml" oder "er".</summary>
    public string Art { get; }

    /// <summary>Konnte die gespeicherte Zeichnung nicht gelesen werden (der Baukasten beginnt dann mit der Vorgabe)?</summary>
    public bool Unlesbar { get; private set; }

    /// <summary>Die Zeichnung wie sie jetzt ist (zum Zeichnen und fuer die Textfassung).</summary>
    public EchtDiagramm Diagramm => new(Art, [.. _elemente], [.. _verbindungen]);

    /// <summary>Hat man etwas Eigenes gesetzt, beschriftet, verbunden oder verschoben?</summary>
    public bool Eigenes => Json().Length > 0;

    /// <summary>Neuer Baukasten mit der Vorgabe (oder leer).</summary>
    public static EchtBaukasten Neu(string art, EchtDiagramm? vorgabe) => new(art, vorgabe);

    /// <summary>
    /// Gespeicherte Zeichnung lesen. Leer = nur die Vorgabe. Ein Text, der sich nicht lesen laesst (nur bei Eingriffen von
    /// aussen moeglich), stuerzt nie ab: der Baukasten beginnt mit der Vorgabe und <see cref="Unlesbar"/> ist wahr.
    /// </summary>
    public static EchtBaukasten Laden(string art, EchtDiagramm? vorgabe, string? text)
    {
        var bau = new EchtBaukasten(art, vorgabe);
        if (string.IsNullOrWhiteSpace(text))
        {
            return bau;
        }
        try
        {
            bau.Einlesen(JsonNode.Parse(text) as JsonObject ?? throw new EchtLadefehler("kein Objekt"));
        }
        catch (Exception e) when (e is JsonException or EchtLadefehler or InvalidOperationException or FormatException)
        {
            bau = new EchtBaukasten(art, vorgabe) { Unlesbar = true };
        }
        return bau;
    }

    private void Einlesen(JsonObject d)
    {
        if (Text(d["art"]) != Art || d["elemente"] is not JsonArray elemente || d["verbindungen"] is not JsonArray verbindungen)
        {
            throw new EchtLadefehler("Aufbau");
        }
        var typen = EchtZeichnung.Bausteine[Art];
        var neu = new List<EchtElement>();
        foreach (var knoten in elemente)
        {
            var e = knoten as JsonObject ?? throw new EchtLadefehler("Baustein");
            var id = Text(e["id"]) is { Length: > 0 } i ? i : throw new EchtLadefehler("id");
            var typ = Text(e["typ"]) is { } t && typen.Contains(t) ? t : throw new EchtLadefehler("typ");
            var x = Ganz(e["x"], EchtZeichnung.RasterBreite);
            var y = Ganz(e["y"], EchtZeichnung.RasterHoehe);
            var schluessel = e["schluessel"] is JsonValue s && s.TryGetValue<bool>(out var ja) && ja && typ == "attribut";
            neu.Add(new EchtElement(id, typ, Kurz(Text(e["text"]) ?? ""), x, y, schluessel));
        }
        if (neu.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != neu.Count || neu.Select(e => (e.X, e.Y)).Distinct().Count() != neu.Count)
        {
            throw new EchtLadefehler("doppelt");
        }
        // die Vorgabe bleibt, wie sie ist (gleiche Stelle, gleicher Text)
        foreach (var fest in _elemente)
        {
            if (!neu.Contains(fest))
            {
                throw new EchtLadefehler("Vorgabe");
            }
        }
        _elemente.Clear();
        _elemente.AddRange(neu);
        _verbindungen.Clear();
        foreach (var knoten in verbindungen)
        {
            var v = knoten as JsonObject ?? throw new EchtLadefehler("Verbindung");
            var von = Text(v["von"]) ?? "";
            var nach = Text(v["nach"]) ?? "";
            if (Verbinden(von, nach) is not null)
            {
                throw new EchtLadefehler("Verbindung");
            }
            var text = Kurz(Text(v["text"]) ?? "");
            var kard = Text(v["kard"]) ?? "";
            if ((text.Length > 0 && !Bedingung(von, nach, text)) || (kard.Length > 0 && !Kardinalitaet(von, nach, kard)))
            {
                throw new EchtLadefehler("Verbindung");
            }
        }
        if (_festeVerbindungen.Values.Any(f => Gleich(f) is not { } jetzt || (f.Text.Length > 0 && jetzt.Text != f.Text)
            || (f.Kard.Length > 0 && jetzt.Kard != f.Kard)))
        {
            throw new EchtLadefehler("Vorgabe");
        }
    }

    /// <summary>Gespeicherte Form (kompakt); leer, solange nur die Vorgabe darin steht.</summary>
    public string Json()
    {
        if (_elemente.SequenceEqual(_vorgabeElemente) && _verbindungen.ToHashSet().SetEquals(_festeVerbindungen.Values))
        {
            return "";
        }
        var elemente = new JsonArray();
        foreach (var e in _elemente)
        {
            var o = new JsonObject { ["id"] = e.Id, ["typ"] = e.Typ };
            if (e.Text.Length > 0)
            {
                o["text"] = e.Text;
            }
            o["x"] = e.X;
            o["y"] = e.Y;
            if (e.Schluessel)
            {
                o["schluessel"] = true;
            }
            elemente.Add(o);
        }
        var verbindungen = new JsonArray();
        foreach (var v in _verbindungen)
        {
            var o = new JsonObject { ["von"] = v.Von, ["nach"] = v.Nach };
            if (v.Text.Length > 0)
            {
                o["text"] = v.Text;
            }
            if (v.Kard.Length > 0)
            {
                o["kard"] = v.Kard;
            }
            verbindungen.Add(o);
        }
        return new JsonObject { ["art"] = Art, ["elemente"] = elemente, ["verbindungen"] = verbindungen }.ToJsonString(Ausgabe);
    }

    // -- Fragen -------------------------------------------------------------------------------

    public bool IstFest(string id) => _fest.Contains(id);

    public bool IstFesteVerbindung(EchtVerbindung v) => _festeVerbindungen.ContainsKey(Paar(v.Von, v.Nach));

    /// <summary>Bedingung oder Kardinalitaet einer Verbindung der Vorgabe, die schon in der Vorgabe steht, bleibt fest.</summary>
    private bool AngabeFest(EchtVerbindung v, bool kard) => _festeVerbindungen.TryGetValue(Paar(v.Von, v.Nach), out var f)
        && (kard ? f.Kard : f.Text).Length > 0;

    private EchtVerbindung? Gleich(EchtVerbindung f) => _verbindungen.FirstOrDefault(v => v.Von == f.Von && v.Nach == f.Nach)
        ?? (Art == EchtZeichnung.Er ? _verbindungen.FirstOrDefault(v => v.Von == f.Nach && v.Nach == f.Von) : null);

    public EchtElement? Element(string? id) => _elemente.FirstOrDefault(e => e.Id == id);

    public EchtElement? An(int x, int y) => _elemente.FirstOrDefault(e => e.X == x && e.Y == y);

    /// <summary>Verbindungen eines Bausteins (in beide Richtungen).</summary>
    public IReadOnlyList<EchtVerbindung> VerbindungenVon(string id) => _verbindungen.Where(v => v.Von == id || v.Nach == id).ToList();

    /// <summary>Hat der Baustein einen Text (Aktion, Entitaet, Beziehung, Attribut)?</summary>
    public static bool MitText(string typ) => typ is "aktion" or "entitaet" or "beziehung" or "attribut";

    /// <summary>Darf man an diese Verbindung eine Bedingung schreiben (UML, Pfeil aus einer Verzweigung)?</summary>
    public bool BedingungMoeglich(EchtVerbindung v) => Art == EchtZeichnung.Uml && Element(v.Von)?.Typ == "verzweigung";

    /// <summary>Darf man an diese Verbindung eine Kardinalitaet schreiben (ER, Beziehung zu Entitaet)?</summary>
    public bool KardinalitaetMoeglich(EchtVerbindung v) => Art == EchtZeichnung.Er
        && new[] { Element(v.Von)?.Typ, Element(v.Nach)?.Typ }.Order(StringComparer.Ordinal).SequenceEqual(["beziehung", "entitaet"]);

    /// <summary>Die naechste freie Stelle: unter <paramref name="nahBei"/> (dann daneben), sonst die erste freie Zeile fuer Zeile.</summary>
    public (int X, int Y)? Frei(string? nahBei)
    {
        if (Element(nahBei) is { } e)
        {
            for (var y = e.Y + 1; y < EchtZeichnung.RasterHoehe; y++)
            {
                foreach (var dx in new[] { 0, 1, -1, 2, -2 })
                {
                    var x = e.X + dx;
                    if (x >= 0 && x < EchtZeichnung.RasterBreite && An(x, y) is null)
                    {
                        return (x, y);
                    }
                }
            }
        }
        for (var y = 0; y < EchtZeichnung.RasterHoehe; y++)
        {
            for (var x = 0; x < EchtZeichnung.RasterBreite; x++)
            {
                if (An(x, y) is null)
                {
                    return (x, y);
                }
            }
        }
        return null;
    }

    // -- Aendern (jede Methode laesst nur lesbare Zustaende zu) ------------------------------------

    /// <summary>Neuen Baustein an die naechste freie Stelle setzen; Kennung oder null (Raster voll, falsche Art).</summary>
    public string? Setzen(string typ, string? nahBei = null)
    {
        if (!EchtZeichnung.Bausteine[Art].Contains(typ) || Frei(nahBei) is not { } stelle)
        {
            return null;
        }
        var nummer = 1;
        while (_elemente.Any(e => e.Id == "b" + nummer.ToString(CultureInfo.InvariantCulture)))
        {
            nummer++;
        }
        var id = "b" + nummer.ToString(CultureInfo.InvariantCulture);
        _elemente.Add(new EchtElement(id, typ, "", stelle.X, stelle.Y, false));
        return id;
    }

    /// <summary>Eigenen Baustein auf ein freies Feld setzen.</summary>
    public bool Verschieben(string id, int x, int y)
    {
        if (IstFest(id) || Element(id) is not { } e || x < 0 || y < 0 || x >= EchtZeichnung.RasterBreite || y >= EchtZeichnung.RasterHoehe
            || An(x, y) is not null)
        {
            return false;
        }
        Ersetzen(e, e with { X = x, Y = y });
        return true;
    }

    /// <summary>Text eines eigenen Bausteins (hoechstens <see cref="TextLaenge"/> Zeichen, Zeilenumbrueche werden Leerzeichen).</summary>
    public bool Beschriften(string id, string text)
    {
        if (IstFest(id) || Element(id) is not { } e || !MitText(e.Typ))
        {
            return false;
        }
        Ersetzen(e, e with { Text = Kurz(text) });
        return true;
    }

    /// <summary>Eigenes Attribut als Schluessel markieren (unterstrichen) oder nicht.</summary>
    public bool Schluessel(string id, bool ja)
    {
        if (IstFest(id) || Element(id) is not { Typ: "attribut" } e)
        {
            return false;
        }
        Ersetzen(e, e with { Schluessel = ja });
        return true;
    }

    /// <summary>Eigenen Baustein samt seinen Verbindungen entfernen.</summary>
    public bool Entfernen(string id)
    {
        if (IstFest(id) || Element(id) is not { } e)
        {
            return false;
        }
        _elemente.Remove(e);
        _verbindungen.RemoveAll(v => v.Von == id || v.Nach == id);
        return true;
    }

    /// <summary>
    /// Verbinden (UML: Pfeil von <paramref name="von"/> zu <paramref name="nach"/>; ER: Linie). null = verbunden, sonst
    /// der Grund, warum nicht (Text fuer die Anzeige). Es entsteht nur, was die Regeln der Ladepruefung erlauben.
    /// </summary>
    public string? Verbinden(string von, string nach)
    {
        if (Element(von) is not { } a || Element(nach) is not { } b)
        {
            return EchtTexte.NichtsGewaehlt;
        }
        if (von == nach)
        {
            return EchtTexte.NichtMitSich;
        }
        if (_verbindungen.Any(v => Paar(v.Von, v.Nach) == Paar(von, nach)))
        {
            return EchtTexte.SchonVerbunden;
        }
        if (Art == EchtZeichnung.Uml)
        {
            if (b.Typ == "start" || a.Typ == "ende")
            {
                return EchtTexte.NichtStartEnde;
            }
        }
        else
        {
            var typen = new[] { a.Typ, b.Typ }.Order(StringComparer.Ordinal).ToArray();
            if (typen is not (["attribut", "beziehung" or "entitaet"] or ["beziehung", "entitaet"]))
            {
                return EchtTexte.ErNichtErlaubt;
            }
            var attribut = a.Typ == "attribut" ? a : b.Typ == "attribut" ? b : null;
            if (attribut is not null && _verbindungen.Any(v => v.Von == attribut.Id || v.Nach == attribut.Id))
            {
                return EchtTexte.AttributBelegt;
            }
        }
        _verbindungen.Add(new EchtVerbindung(von, nach, "", ""));
        return null;
    }

    /// <summary>Eigene Verbindung entfernen (die der Vorgabe bleiben).</summary>
    public bool Trennen(string von, string nach)
    {
        var v = _verbindungen.FirstOrDefault(v => Paar(v.Von, v.Nach) == Paar(von, nach));
        if (v is null || IstFesteVerbindung(v))
        {
            return false;
        }
        _verbindungen.Remove(v);
        return true;
    }

    /// <summary>Bedingung an einem Pfeil aus einer Verzweigung (UML), leer = keine.</summary>
    public bool Bedingung(string von, string nach, string text)
    {
        var v = _verbindungen.FirstOrDefault(v => v.Von == von && v.Nach == nach);
        if (v is null || !BedingungMoeglich(v) || AngabeFest(v, kard: false))
        {
            return false;
        }
        _verbindungen[_verbindungen.IndexOf(v)] = v with { Text = Kurz(text) };
        return true;
    }

    /// <summary>Kardinalitaet (1, n, m oder leer) an der Linie Beziehung–Entitaet (ER).</summary>
    public bool Kardinalitaet(string von, string nach, string kard)
    {
        var v = _verbindungen.FirstOrDefault(v => Paar(v.Von, v.Nach) == Paar(von, nach));
        if (v is null || !KardinalitaetMoeglich(v) || (kard.Length > 0 && !EchtZeichnung.Kardinalitaeten.Contains(kard))
            || AngabeFest(v, kard: true))
        {
            return false;
        }
        _verbindungen[_verbindungen.IndexOf(v)] = v with { Kard = kard };
        return true;
    }

    private void Ersetzen(EchtElement alt, EchtElement neu) => _elemente[_elemente.IndexOf(alt)] = neu;

    // -- Screenreader-Liste ---------------------------------------------------------------------

    /// <summary>Bezeichnung mit Stelle: "Aktion „Ticket anlegen“, Spalte 2, Zeile 3" (Spalten und Zeilen ab 1).</summary>
    public string Name(EchtElement e) => EchtZeichnung.Bezeichnung(Diagramm, e) + ", " + StelleText(e);

    public static string StelleText(EchtElement e) => Beschriftungen.Einsetzen(EchtTexte.Stelle, e.X + 1, e.Y + 1);

    /// <summary>
    /// Je Baustein eine Zeile (Reihenfolge wie die Textfassung: Zeile fuer Zeile): Bezeichnung, Stelle und
    /// "verbunden mit …" (UML: "weiter zu …" und "kommt von …", ER mit Kardinalitaet).
    /// </summary>
    public IReadOnlyList<(string Id, string Zeile)> Zeilen()
    {
        var d = Diagramm;
        var zeilen = new List<(string, string)>();
        foreach (var e in d.Elemente.OrderBy(e => e.Y).ThenBy(e => e.X))
        {
            var satz = EchtZeichnung.Bezeichnung(d, e) + (e.Schluessel ? " " + EchtTexte.SchluesselZusatz : "") + ", " + StelleText(e);
            var teile = new List<string>();
            if (Art == EchtZeichnung.Uml)
            {
                var weiter = d.Verbindungen.Where(v => v.Von == e.Id)
                    .Select(v => (v.Text.Length > 0 ? v.Text + " " : "") + EchtZeichnung.Bezeichnung(d, d.Element(v.Nach))).ToList();
                var von = d.Verbindungen.Where(v => v.Nach == e.Id).Select(v => EchtZeichnung.Bezeichnung(d, d.Element(v.Von))).ToList();
                if (weiter.Count > 0)
                {
                    teile.Add(EchtTexte.WeiterZu + " " + string.Join("; ", weiter));
                }
                if (von.Count > 0)
                {
                    teile.Add(EchtTexte.KommtVon + " " + string.Join("; ", von));
                }
            }
            else
            {
                var mit = d.Verbindungen.Where(v => v.Von == e.Id || v.Nach == e.Id)
                    .Select(v => EchtZeichnung.Bezeichnung(d, d.Element(v.Von == e.Id ? v.Nach : v.Von)) + (v.Kard.Length > 0 ? " (" + v.Kard + ")" : ""))
                    .ToList();
                if (mit.Count > 0)
                {
                    teile.Add(EchtTexte.VerbundenMit + " " + string.Join("; ", mit));
                }
            }
            satz += "; " + (teile.Count > 0 ? string.Join("; ", teile) : EchtTexte.OhneVerbindung);
            zeilen.Add((e.Id, satz + "."));
        }
        return zeilen;
    }

    // -- Pruefung durch das Programm (nach dem Abgeben) -------------------------------------------

    /// <summary>
    /// Was sich zaehlen laesst, im Vergleich mit dem Musterbild: Anzahl je Bausteinart, Zahl der Verbindungen, bei ER die
    /// Kardinalitaeten der Beziehungen (z. B. 1:n) und die Schluesselattribute, dazu die Regeln eines fertigen Diagramms
    /// (wie beim Musterbild) und Bausteine ohne Text. Texte und Ablauf vergleicht man selbst (Haekchen).
    /// </summary>
    public static IReadOnlyList<EchtZaehlung> Zaehlen(EchtDiagramm eigen, EchtDiagramm muster)
    {
        var zeilen = new List<EchtZaehlung>();
        foreach (var typ in EchtZeichnung.Bausteine[muster.Art])
        {
            var ich = eigen.Elemente.Count(e => e.Typ == typ);
            var soll = muster.Elemente.Count(e => e.Typ == typ);
            if (ich > 0 || soll > 0)
            {
                zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlAnzahl, EchtZeichnung.Baustein(typ), ich, soll), ich == soll));
            }
        }
        zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlVerbindungen, eigen.Verbindungen.Count, muster.Verbindungen.Count),
            eigen.Verbindungen.Count == muster.Verbindungen.Count));
        if (muster.Art == EchtZeichnung.Er)
        {
            var ich = Arten(eigen);
            var soll = Arten(muster);
            foreach (var art in soll.Keys.Union(ich.Keys).Order(StringComparer.Ordinal))
            {
                var a = ich.GetValueOrDefault(art);
                var b = soll.GetValueOrDefault(art);
                zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlKard, art, a, b), a == b));
            }
            var schluessel = eigen.Elemente.Count(e => e.Schluessel);
            var sollSchluessel = muster.Elemente.Count(e => e.Schluessel);
            zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlSchluessel, schluessel, sollSchluessel), schluessel == sollSchluessel));
        }
        var ohneText = eigen.Elemente.Where(e => MitText(e.Typ) && e.Text.Trim().Length == 0).ToList();
        if (ohneText.Count > 0)
        {
            zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlOhneText,
                string.Join(", ", ohneText.Select(e => EchtZeichnung.Bezeichnung(eigen, e) + " (" + StelleText(e) + ")"))), false));
        }
        var verstoesse = EchtZeichnung.Verstoesse(eigen, vollstaendig: true, e => EchtZeichnung.Bezeichnung(eigen, e)).Distinct().ToList();
        if (verstoesse.Count == 0)
        {
            zeilen.Add(new(EchtTexte.ZaehlRegelnOk, true));
        }
        foreach (var v in verstoesse.Take(4))
        {
            zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlRegel, v), false));
        }
        if (verstoesse.Count > 4)
        {
            zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlWeitere, verstoesse.Count - 4), false));
        }
        return zeilen;
    }

    /// <summary>Beziehungen nach Art der Kardinalitaeten ("1:n", "m:n", "1:1"; ohne Angabe "?").</summary>
    private static Dictionary<string, int> Arten(EchtDiagramm d)
    {
        var arten = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var b in d.Elemente.Where(e => e.Typ == "beziehung"))
        {
            var kards = d.Verbindungen.Where(v => (v.Von == b.Id || v.Nach == b.Id) && d.Element(v.Von == b.Id ? v.Nach : v.Von).Typ == "entitaet")
                .Select(v => v.Kard.Length > 0 ? v.Kard : "?").ToList();
            if (kards.Count == 0)
            {
                continue;
            }
            // 1 vor n, n und m gleichwertig (m:n wie n:m): die erste Viel-Seite heisst n, jede weitere m
            var geordnet = kards.Select(k => k == "m" ? "n" : k).OrderBy(k => k switch { "1" => 0, "n" => 1, _ => 2 }).ToList();
            var viele = 0;
            var art = string.Join(":", geordnet.Select(k => k == "n" ? (viele++ == 0 ? "n" : "m") : k));
            arten[art] = arten.GetValueOrDefault(art) + 1;
        }
        return arten;
    }

    // -- kleine Hilfen ------------------------------------------------------------------------

    private static string Paar(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "\n" + b : b + "\n" + a;

    private static string Kurz(string text)
    {
        var einzeilig = string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return einzeilig.Length > TextLaenge ? einzeilig[..TextLaenge] : einzeilig;
    }

    private static string? Text(JsonNode? knoten) => knoten is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static int Ganz(JsonNode? knoten, int grenze) =>
        knoten is JsonValue v && v.GetValueKind() == JsonValueKind.Number && v.TryGetValue<int>(out var i) && i >= 0 && i < grenze
            ? i : throw new EchtLadefehler("Stelle");
}
