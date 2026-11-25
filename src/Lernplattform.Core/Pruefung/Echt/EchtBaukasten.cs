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
    private readonly EchtGrenze? _grenze;

    private EchtBaukasten(string art, EchtDiagramm? vorgabe)
    {
        Art = art;
        _grenze = vorgabe?.Grenze;
        _vorgabeElemente = vorgabe?.Elemente ?? [];
        _elemente = [.. _vorgabeElemente];
        _verbindungen = [.. vorgabe?.Verbindungen ?? []];
        _fest = _elemente.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        _festeVerbindungen = _verbindungen.ToDictionary(v => Paar(v.Von, v.Nach), StringComparer.Ordinal);
    }

    /// <summary>"uml" oder "er", ab A 1.8.0 auch "klasse" und "usecase".</summary>
    public string Art { get; }

    /// <summary>Klassen- oder Use-Case-Diagramm (A 1.8.0, Beziehungen mit Art)?</summary>
    public bool MitBeziehungen => EchtZeichnung.Beziehungen.ContainsKey(Art);

    /// <summary>Systemgrenze (Use-Case, aus der Vorgabe), sonst null.</summary>
    public EchtGrenze? Grenze => _grenze;

    /// <summary>Konnte die gespeicherte Zeichnung nicht gelesen werden (der Baukasten beginnt dann mit der Vorgabe)?</summary>
    public bool Unlesbar { get; private set; }

    /// <summary>Die Zeichnung wie sie jetzt ist (zum Zeichnen und fuer die Textfassung).</summary>
    public EchtDiagramm Diagramm => new(Art, [.. _elemente], [.. _verbindungen], _grenze);

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
            var attribute = typ == "klasse" ? ZeilenLesen(e["attribute"]) : "";
            var methoden = typ == "klasse" ? ZeilenLesen(e["methoden"]) : "";
            if (!Passt(typ, x, y))
            {
                throw new EchtLadefehler("Grenze");
            }
            neu.Add(new EchtElement(id, typ, Kurz(Text(e["text"]) ?? ""), x, y, schluessel, attribute, methoden));
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
            var typ = Text(v["typ"]) ?? "";
            if ((MitBeziehungen ? VerbindenOhneFest(von, nach, typ) : Verbinden(von, nach)) is not null)
            {
                throw new EchtLadefehler("Verbindung");
            }
            var text = Kurz(Text(v["text"]) ?? "");
            var kard = Text(v["kard"]) ?? "";
            var multVon = Text(v["mult_von"]) ?? "";
            var multNach = Text(v["mult_nach"]) ?? "";
            if ((text.Length > 0 && !Bedingung(von, nach, text)) || (kard.Length > 0 && !Kardinalitaet(von, nach, kard))
                || (multVon.Length > 0 && !MultSetzen(von, nach, amVon: true, multVon)) || (multNach.Length > 0 && !MultSetzen(von, nach, amVon: false, multNach)))
            {
                throw new EchtLadefehler("Verbindung");
            }
        }
        if (_festeVerbindungen.Values.Any(f => Gleich(f) is not { } jetzt || (f.Text.Length > 0 && jetzt.Text != f.Text)
            || (f.Kard.Length > 0 && jetzt.Kard != f.Kard) || jetzt.Typ != f.Typ
            || (f.MultVon.Length > 0 && Mult(jetzt, f.Von) != f.MultVon) || (f.MultNach.Length > 0 && Mult(jetzt, f.Nach) != f.MultNach)))
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
            if (e.Attribute.Length > 0)
            {
                o["attribute"] = new JsonArray([.. e.AttributZeilen.Select(z => (JsonNode)JsonValue.Create(z))]);
            }
            if (e.Methoden.Length > 0)
            {
                o["methoden"] = new JsonArray([.. e.MethodenZeilen.Select(z => (JsonNode)JsonValue.Create(z))]);
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
            if (v.Typ.Length > 0)
            {
                o["typ"] = v.Typ;
            }
            if (v.MultVon.Length > 0)
            {
                o["mult_von"] = v.MultVon;
            }
            if (v.MultNach.Length > 0)
            {
                o["mult_nach"] = v.MultNach;
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
        ?? (Art == EchtZeichnung.Er || (MitBeziehungen && !EchtZeichnung.Gerichtet(f.Typ))
            ? _verbindungen.FirstOrDefault(v => v.Von == f.Nach && v.Nach == f.Von) : null);

    /// <summary>Multiplizitaet einer Verbindung am Ende des Bausteins <paramref name="id"/>.</summary>
    private static string Mult(EchtVerbindung v, string id) => v.Von == id ? v.MultVon : v.MultNach;

    public EchtElement? Element(string? id) => _elemente.FirstOrDefault(e => e.Id == id);

    public EchtElement? An(int x, int y) => _elemente.FirstOrDefault(e => e.X == x && e.Y == y);

    /// <summary>Verbindungen eines Bausteins (in beide Richtungen).</summary>
    public IReadOnlyList<EchtVerbindung> VerbindungenVon(string id) => _verbindungen.Where(v => v.Von == id || v.Nach == id).ToList();

    /// <summary>Hat der Baustein einen Text (Aktion, Entitaet, Beziehung, Attribut; ab A 1.8.0 Klasse, Akteur, Anwendungsfall)?</summary>
    public static bool MitText(string typ) => typ is "aktion" or "entitaet" or "beziehung" or "attribut" or "klasse" or "akteur" or "anwendungsfall";

    /// <summary>Use-Case: Akteure nur ausserhalb, Anwendungsfaelle nur innerhalb der Systemgrenze; sonst passt jedes Feld.</summary>
    public bool Passt(string typ, int x, int y) => _grenze is not { } g || typ switch
    {
        "akteur" => !g.Enthaelt(x, y),
        "anwendungsfall" => g.Enthaelt(x, y),
        _ => true,
    };

    /// <summary>Darf man an dieser Verbindung Multiplizitaeten waehlen (Klassendiagramm ohne Vererbung)?</summary>
    public bool MultMoeglich(EchtVerbindung v) => EchtZeichnung.MitMultiplizitaet(Art, v.Typ);

    /// <summary>Ist die Art (und Richtung) dieser Verbindung fest (Vorgabe)?</summary>
    public bool ArtFest(EchtVerbindung v) => IstFesteVerbindung(v);

    /// <summary>Multiplizitaet am Ende <paramref name="amVon"/> fest (in der Vorgabe schon gesetzt)?</summary>
    public bool MultFest(EchtVerbindung v, bool amVon) => _festeVerbindungen.TryGetValue(Paar(v.Von, v.Nach), out var f)
        && Mult(f, amVon ? v.Von : v.Nach).Length > 0;

    /// <summary>Beziehungsarten, die zwischen den beiden Bausteinen moeglich sind (Klasse, Use-Case), in fester Reihenfolge.</summary>
    public IReadOnlyList<string> ArtenZwischen(string von, string nach) => !MitBeziehungen || Element(von) is not { } a || Element(nach) is not { } b ? []
        : EchtZeichnung.Beziehungen[Art].Where(t => Erlaubt(a, b, t) is null).ToList();

    /// <summary>Darf man an diese Verbindung eine Bedingung schreiben (UML, Pfeil aus einer Verzweigung)?</summary>
    public bool BedingungMoeglich(EchtVerbindung v) => Art == EchtZeichnung.Uml && Element(v.Von)?.Typ == "verzweigung";

    /// <summary>Darf man an diese Verbindung eine Kardinalitaet schreiben (ER, Beziehung zu Entitaet)?</summary>
    public bool KardinalitaetMoeglich(EchtVerbindung v) => Art == EchtZeichnung.Er
        && new[] { Element(v.Von)?.Typ, Element(v.Nach)?.Typ }.Order(StringComparer.Ordinal).SequenceEqual(["beziehung", "entitaet"]);

    /// <summary>Die naechste freie Stelle: unter <paramref name="nahBei"/> (dann daneben), sonst die erste freie Zeile fuer Zeile.</summary>
    public (int X, int Y)? Frei(string? nahBei, string? typ = null)
    {
        if (Element(nahBei) is { } e)
        {
            for (var y = e.Y + 1; y < EchtZeichnung.RasterHoehe; y++)
            {
                foreach (var dx in new[] { 0, 1, -1, 2, -2 })
                {
                    var x = e.X + dx;
                    if (x >= 0 && x < EchtZeichnung.RasterBreite && An(x, y) is null && (typ is null || Passt(typ, x, y)))
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
                if (An(x, y) is null && (typ is null || Passt(typ, x, y)))
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
        if (!EchtZeichnung.Bausteine[Art].Contains(typ) || Frei(nahBei, typ) is not { } stelle)
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
            || An(x, y) is not null || !Passt(e.Typ, x, y))
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
        if (MitBeziehungen)
        {
            // A 1.8.0: erste erlaubte Art (Klasse: Assoziation; Use-Case: Akteur–Fall Assoziation, Fall–Fall «include», Akteur–Akteur Generalisierung)
            var typ = ArtenZwischen(von, nach).FirstOrDefault() ?? EchtZeichnung.Beziehungen[Art][0];
            return VerbindenOhneFest(von, nach, typ);
        }
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

    /// <summary>
    /// A 1.8.0 Klasse und Use-Case: Beziehung der Art <paramref name="typ"/> von <paramref name="von"/> nach
    /// <paramref name="nach"/> (gerichtete Arten zeigen nach <paramref name="nach"/>). null = verbunden, sonst der Grund.
    /// </summary>
    public string? Verbinden(string von, string nach, string typ) => MitBeziehungen ? VerbindenOhneFest(von, nach, typ) : Verbinden(von, nach);

    private string? VerbindenOhneFest(string von, string nach, string typ)
    {
        if (!MitBeziehungen)
        {
            return EchtTexte.ArtNichtErlaubt;
        }
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
        if (Erlaubt(a, b, typ) is { } grund)
        {
            return grund;
        }
        var neu = new EchtVerbindung(von, nach, "", "", typ);
        if (Kreis(neu, null))
        {
            return EchtTexte.KeinKreis;
        }
        _verbindungen.Add(neu);
        return null;
    }

    /// <summary>Grund, warum diese Art zwischen den Bausteinen nicht geht (Regeln wie die Ladepruefung), sonst null.</summary>
    private string? Erlaubt(EchtElement a, EchtElement b, string typ)
    {
        if (!EchtZeichnung.Beziehungen[Art].Contains(typ))
        {
            return EchtTexte.ArtNichtErlaubt;
        }
        if (Art == EchtZeichnung.Klasse)
        {
            return null;
        }
        return typ switch
        {
            "assoziation" => a.Typ != b.Typ ? null : EchtTexte.NurAkteurFall,
            "include" or "extend" => a.Typ == "anwendungsfall" && b.Typ == "anwendungsfall" ? null : EchtTexte.NurFallFall,
            _ => a.Typ == b.Typ ? null : EchtTexte.NurGleiche,
        };
    }

    /// <summary>Entstuende mit <paramref name="neu"/> (statt <paramref name="statt"/>) ein Kreis aus Vererbung bzw. Generalisierung?</summary>
    private bool Kreis(EchtVerbindung neu, EchtVerbindung? statt)
    {
        if (neu.Typ is not ("vererbung" or "generalisierung"))
        {
            return false;
        }
        // Kreis, wenn man vom Ziel der neuen Beziehung ueber Beziehungen derselben Art wieder zu ihrem Anfang kommt
        var kanten = _verbindungen.Where(v => v != statt && v.Typ == neu.Typ).ToLookup(v => v.Von, v => v.Nach, StringComparer.Ordinal);
        var offen = new Stack<string>();
        offen.Push(neu.Nach);
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        while (offen.Count > 0)
        {
            var id = offen.Pop();
            if (id == neu.Von)
            {
                return true;
            }
            if (gesehen.Add(id))
            {
                foreach (var w in kanten[id])
                {
                    offen.Push(w);
                }
            }
        }
        return false;
    }

    private EchtVerbindung? Zwischen(string a, string b) => _verbindungen.FirstOrDefault(v => Paar(v.Von, v.Nach) == Paar(a, b));

    /// <summary>Art einer eigenen Beziehung aendern (Klasse, Use-Case); Multiplizitaeten, die zur neuen Art nicht passen, fallen weg.</summary>
    public string? ArtSetzen(string von, string nach, string typ)
    {
        if (Zwischen(von, nach) is not { } v || IstFesteVerbindung(v))
        {
            return EchtTexte.NichtsGewaehlt;
        }
        if (v.Typ == typ)
        {
            return null;
        }
        if (Erlaubt(Element(v.Von)!, Element(v.Nach)!, typ) is { } grund)
        {
            return grund;
        }
        var neu = v with
        {
            Typ = typ,
            MultVon = EchtZeichnung.MitMultiplizitaet(Art, typ) ? v.MultVon : "",
            MultNach = !EchtZeichnung.MitMultiplizitaet(Art, typ) || (typ == "komposition" && v.MultNach is not ("" or "1" or "0..1")) ? "" : v.MultNach,
        };
        if (Kreis(neu, v))
        {
            return EchtTexte.KeinKreis;
        }
        _verbindungen[_verbindungen.IndexOf(v)] = neu;
        return null;
    }

    /// <summary>Richtung einer eigenen gerichteten Beziehung umdrehen (Multiplizitaeten wandern mit ihrem Ende).</summary>
    public string? Umkehren(string von, string nach)
    {
        if (Zwischen(von, nach) is not { } v || IstFesteVerbindung(v) || !EchtZeichnung.Gerichtet(v.Typ))
        {
            return EchtTexte.NichtsGewaehlt;
        }
        var neu = v with { Von = v.Nach, Nach = v.Von, MultVon = v.MultNach, MultNach = v.MultVon };
        if (neu.Typ == "komposition" && neu.MultNach is not ("" or "1" or "0..1"))
        {
            neu = neu with { MultNach = "" };
        }
        if (Kreis(neu, v))
        {
            return EchtTexte.KeinKreis;
        }
        _verbindungen[_verbindungen.IndexOf(v)] = neu;
        return null;
    }

    /// <summary>
    /// Multiplizitaet (1, 0..1, *, 0..*, 1..* oder leer) am Ende <paramref name="amVon"/> (true = am Baustein
    /// <paramref name="von"/>) der Beziehung zwischen <paramref name="von"/> und <paramref name="nach"/> (Reihenfolge wie gespeichert).
    /// </summary>
    public bool MultSetzen(string von, string nach, bool amVon, string wert)
    {
        if (Zwischen(von, nach) is not { } v || !MultMoeglich(v) || (wert.Length > 0 && !EchtZeichnung.Multiplizitaeten.Contains(wert)))
        {
            return false;
        }
        var ende = amVon ? von : nach;
        var amVonGespeichert = v.Von == ende;
        if (MultFest(v, amVonGespeichert))
        {
            return false;
        }
        if (v.Typ == "komposition" && !amVonGespeichert && wert is not ("" or "1" or "0..1"))
        {
            return false;
        }
        _verbindungen[_verbindungen.IndexOf(v)] = amVonGespeichert ? v with { MultVon = wert } : v with { MultNach = wert };
        return true;
    }

    /// <summary>Neue leere Attribut- oder Methodenzeile einer eigenen Klasse; Nummer der Zeile oder null (voll).</summary>
    public int? ZeileHinzufuegen(string id, bool methode)
    {
        if (IstFest(id) || Element(id) is not { Typ: "klasse" } e)
        {
            return null;
        }
        var zeilen = (methode ? e.MethodenZeilen : e.AttributZeilen).ToList();
        if (zeilen.Count >= EchtZeichnung.ZeilenMax)
        {
            return null;
        }
        zeilen.Add("");
        Ersetzen(e, methode ? e with { Methoden = Verbunden(zeilen) } : e with { Attribute = Verbunden(zeilen) });
        return zeilen.Count - 1;
    }

    /// <summary>Text einer Attribut- oder Methodenzeile (hoechstens <see cref="TextLaenge"/> Zeichen, einzeilig).</summary>
    public bool ZeileSetzen(string id, bool methode, int nummer, string text)
    {
        if (IstFest(id) || Element(id) is not { Typ: "klasse" } e)
        {
            return false;
        }
        var zeilen = (methode ? e.MethodenZeilen : e.AttributZeilen).ToList();
        if (nummer < 0 || nummer >= zeilen.Count)
        {
            return false;
        }
        zeilen[nummer] = Kurz(text);
        Ersetzen(e, methode ? e with { Methoden = Verbunden(zeilen) } : e with { Attribute = Verbunden(zeilen) });
        return true;
    }

    /// <summary>Attribut- oder Methodenzeile einer eigenen Klasse entfernen.</summary>
    public bool ZeileEntfernen(string id, bool methode, int nummer)
    {
        if (IstFest(id) || Element(id) is not { Typ: "klasse" } e)
        {
            return false;
        }
        var zeilen = (methode ? e.MethodenZeilen : e.AttributZeilen).ToList();
        if (nummer < 0 || nummer >= zeilen.Count)
        {
            return false;
        }
        zeilen.RemoveAt(nummer);
        Ersetzen(e, methode ? e with { Methoden = Verbunden(zeilen) } : e with { Attribute = Verbunden(zeilen) });
        return true;
    }

    /// <summary>
    /// Zeilen als gespeicherter Text. Eine einzelne leere Zeile wird als " " gemerkt (sonst hiesse leer "keine Zeile");
    /// <see cref="Kurz"/> und die Zaehlung behandeln sie wie leer.
    /// </summary>
    private static string Verbunden(List<string> zeilen) => zeilen.Count == 1 && zeilen[0].Length == 0 ? " " : string.Join('\n', zeilen);

    /// <summary>Gespeicherte Zeilen lesen (wie der Baukasten sie schreibt; jede Zeile gekuerzt, hoechstens 8).</summary>
    private static string ZeilenLesen(JsonNode? knoten)
    {
        if (knoten is null)
        {
            return "";
        }
        if (knoten is not JsonArray liste || liste.Count > EchtZeichnung.ZeilenMax)
        {
            throw new EchtLadefehler("Zeilen");
        }
        return Verbunden(liste.Select(z => Kurz(Text(z) ?? throw new EchtLadefehler("Zeilen"))).ToList());
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
            if (MitBeziehungen)
            {
                zeilen.Add((e.Id, EchtZeichnung.Beschreibung(d, e, ", " + StelleText(e)) + "."));
                continue;
            }
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
        if (EchtZeichnung.Beziehungen.ContainsKey(muster.Art))
        {
            return ZaehlenMitNamen(eigen, muster);
        }
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

    /// <summary>
    /// A 1.8.0 Klasse und Use-Case (Plan P12-232 Abschnitt 3, Nicos Antwort 3a): Anzahl je Baustein mit Namen (tolerant:
    /// <see cref="EchtZeichnung.NameVergleich"/>), bei Klassen die Zahl der Attribute und Methoden, dann jede Beziehung des
    /// Musterbilds mit Art, Richtung und Multiplizitaet, zuletzt Beziehungen, die das Musterbild nicht hat, ohne Text und die
    /// Regeln. Jede Abweichung ist eine rote Zeile in Klartext; die Punkte setzt man selbst (Haekchen).
    /// </summary>
    private static List<EchtZaehlung> ZaehlenMitNamen(EchtDiagramm eigen, EchtDiagramm muster)
    {
        var zeilen = new List<EchtZaehlung>();
        // eigener Baustein -> Baustein des Musterbilds (gleiche Art, gleicher Name), jeder hoechstens einmal
        var zuordnung = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var m in muster.Elemente)
        {
            if (eigen.Elemente.FirstOrDefault(e => e.Typ == m.Typ && !zuordnung.ContainsKey(e.Id) && e.Text.Trim().Length > 0
                && EchtZeichnung.NameVergleich(e.Text) == EchtZeichnung.NameVergleich(m.Text)) is { } passend)
            {
                zuordnung[passend.Id] = m.Id;
            }
        }
        foreach (var typ in EchtZeichnung.Bausteine[muster.Art])
        {
            var ich = eigen.Elemente.Where(e => e.Typ == typ).ToList();
            var soll = muster.Elemente.Where(e => e.Typ == typ).ToList();
            if (ich.Count == 0 && soll.Count == 0)
            {
                continue;
            }
            var fehlt = soll.Where(m => !zuordnung.ContainsValue(m.Id)).Select(m => m.Text).ToList();
            var zuViel = ich.Where(e => !zuordnung.ContainsKey(e.Id)).Select(e => e.Text.Trim().Length > 0 ? e.Text : EchtTexte.OhneName).ToList();
            var text = Beschriftungen.Einsetzen(EchtTexte.ZaehlVon, Mehrzahl(typ), ich.Count, soll.Count);
            if (fehlt.Count == 0 && zuViel.Count == 0)
            {
                zeilen.Add(new(text + " " + EchtTexte.ZaehlNamenOk, true));
                continue;
            }
            if (fehlt.Count > 0)
            {
                text += "; " + Beschriftungen.Einsetzen(EchtTexte.ZaehlFehlt, string.Join(", ", fehlt));
            }
            if (zuViel.Count > 0)
            {
                text += "; " + Beschriftungen.Einsetzen(EchtTexte.ZaehlZuViel, string.Join(", ", zuViel));
            }
            zeilen.Add(new(text, false));
        }
        if (muster.Art == EchtZeichnung.Klasse)
        {
            static int Zahl(EchtDiagramm d, bool methode) =>
                d.Elemente.Sum(e => (methode ? e.MethodenZeilen : e.AttributZeilen).Count(z => z.Trim().Length > 0));
            var (a, sa, m, sm) = (Zahl(eigen, false), Zahl(muster, false), Zahl(eigen, true), Zahl(muster, true));
            zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlAttributeMethoden, a, sa, m, sm), a == sa && m == sm));
        }
        string MName(string id) => muster.Element(id).Text;
        var gefunden = new HashSet<EchtVerbindung>();
        foreach (var m in muster.Verbindungen)
        {
            var beschrieben = Beschreiben(m, MName);
            var von = zuordnung.FirstOrDefault(z => z.Value == m.Von).Key;
            var nach = zuordnung.FirstOrDefault(z => z.Value == m.Nach).Key;
            var ich = von is null || nach is null ? null
                : eigen.Verbindungen.FirstOrDefault(v => Paar(v.Von, v.Nach) == Paar(von, nach));
            if (ich is null)
            {
                zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlBeziehungFehlt, beschrieben), false));
                continue;
            }
            gefunden.Add(ich);
            var paar = MName(m.Von) + " – " + MName(m.Nach);
            if (ich.Typ != m.Typ)
            {
                zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlArtFalsch, paar, EchtZeichnung.Beziehung(ich.Typ),
                    EchtZeichnung.Beziehung(m.Typ)), false));
                continue;
            }
            var gleicheRichtung = ich.Von == von;
            if (EchtZeichnung.Gerichtet(m.Typ) && !gleicheRichtung)
            {
                zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlRichtungFalsch, EchtZeichnung.Beziehung(m.Typ),
                    MName(m.Nach) + " → " + MName(m.Von), MName(m.Von) + " → " + MName(m.Nach)), false));
                continue;
            }
            if (EchtZeichnung.MitMultiplizitaet(muster.Art, m.Typ))
            {
                var (amVon, amNach) = gleicheRichtung ? (ich.MultVon, ich.MultNach) : (ich.MultNach, ich.MultVon);
                if (amVon != m.MultVon || amNach != m.MultNach)
                {
                    zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlMultFalsch, paar, Leer(amVon), Leer(amNach), Leer(m.MultVon),
                        Leer(m.MultNach)), false));
                    continue;
                }
            }
            zeilen.Add(new(beschrieben, true));
        }
        string EName(string id) => eigen.Element(id).Text is { Length: > 0 } t ? t : EchtTexte.OhneName;
        foreach (var v in eigen.Verbindungen.Where(v => !gefunden.Contains(v)))
        {
            zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlBeziehungZuViel, Beschreiben(v, EName)), false));
        }
        var ohneText = eigen.Elemente.Where(e => e.Text.Trim().Length == 0).ToList();
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

    /// <summary>"Assoziation Kunde – Ausleihe mit 1 und 0..*", "Vererbung E-Bike → Fahrrad", "«include» Medium ausleihen → Ausweis prüfen".</summary>
    private static string Beschreiben(EchtVerbindung v, Func<string, string> name)
    {
        var art = EchtZeichnung.Beziehung(v.Typ);
        if (EchtZeichnung.Gerichtet(v.Typ))
        {
            var text = art + " " + name(v.Von) + " → " + name(v.Nach);
            return v.MultVon.Length > 0 || v.MultNach.Length > 0
                ? text + " " + Beschriftungen.Einsetzen(EchtTexte.ZaehlMitMult, Leer(v.MultVon), Leer(v.MultNach)) : text;
        }
        var linie = art + " " + name(v.Von) + " – " + name(v.Nach);
        return v.MultVon.Length > 0 || v.MultNach.Length > 0
            ? linie + " " + Beschriftungen.Einsetzen(EchtTexte.ZaehlMitMult, Leer(v.MultVon), Leer(v.MultNach)) : linie;
    }

    private static string Leer(string mult) => mult.Length > 0 ? mult : EchtTexte.OhneKardinalitaet;

    private static string Mehrzahl(string typ) => typ switch
    {
        "klasse" => EchtTexte.Klassen,
        "akteur" => EchtTexte.Akteure,
        _ => EchtTexte.Anwendungsfaelle,
    };

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
