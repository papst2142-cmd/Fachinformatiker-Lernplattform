using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lernplattform.Core.Pruefung.Echt;

/// <summary>
/// Eine Anlage eines Pruefungssatzes (Welle 1: Text, Tabelle, Code, englischer Text; ab Format 2 gezeichnet:
/// Netzplan und Monitor-Bild, immer aus Daten, nie als Foto).
/// </summary>
public sealed record EchtAnlage(string Nr, string Titel, string Art, IReadOnlyList<string> Zeilen,
    IReadOnlyList<string> Spalten, IReadOnlyList<IReadOnlyList<string>> Tabelle)
{
    public EchtNetzplan? Netzplan { get; init; }
    public EchtMonitor? Monitor { get; init; }
}

/// <summary>Bewertungshinweis zum Selbstbewerten (ein Haekchen mit Punkten).</summary>
public sealed record EchtHinweis(string Text, int Punkte);

/// <summary>Eine Zelle einer Tabelle zum Ergaenzen: fester Text oder offen mit erlaubten Loesungen und Punkten.</summary>
public sealed record EchtZelle(string? Fest, IReadOnlyList<string> Loesung, int Punkte)
{
    public bool Offen => Fest is null;
}

/// <summary>Ein Lueckenfeld (IP-Adresse, Maske, Zahl, Text) mit erlaubten Loesungen und Punkten.</summary>
public sealed record EchtFeld(string Name, string Art, IReadOnlyList<string> Loesung, int Punkte);

/// <summary>
/// Antwortform einer Teilaufgabe. Welle 1: zeilen, angaben, rechnen, tabelle, luecke, auswahl; ab Format 2 (A 0.77.0)
/// zeichnen: Diagramm auf Papier, Vorgabe (vorgegebener Anfang) und Musterbild mit Haekchen.
/// </summary>
public sealed record EchtAntwort(string Form)
{
    public int Zeilen { get; init; } = 4;
    public string Einheit { get; init; } = "";
    public double Ergebnis { get; init; }
    public double Toleranz { get; init; }
    public double? ErsatzErgebnis { get; init; }
    public int PunkteErgebnis { get; init; }
    public IReadOnlyList<string> Spalten { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<EchtZelle>> Tabelle { get; init; } = [];
    public IReadOnlyList<EchtFeld> Felder { get; init; } = [];
    public IReadOnlyList<string> Optionen { get; init; } = [];
    public IReadOnlyList<int> Richtig { get; init; } = [];
    public string Diagramm { get; init; } = "";
    public EchtDiagramm? Vorgabe { get; init; }
    public EchtDiagramm? Muster { get; init; }

    /// <summary>
    /// A 1.12.0 (Weisung P12-390): Musterbild einer Struktogramm-Aufgabe (Diagramm "struktogramm", ab Format 5), sonst null.
    /// Ein Struktogramm hat keine Vorgabe und kein <see cref="Muster"/> im Raster.
    /// </summary>
    public IReadOnlyList<EchtBlock>? Struktogramm { get; init; }
}

/// <summary>Eine Teilaufgabe (a, b, ba, bb ...).</summary>
public sealed record EchtTeil(string Nr, string Operator, IReadOnlyList<string> Text, int Punkte, int Angaben,
    EchtAntwort Antwort, IReadOnlyList<string> Anlagen, string? ErsatzBezug, string? ErsatzText, IReadOnlyList<string> Loesung,
    IReadOnlyList<EchtHinweis> Hinweise, bool Englisch)
{
    /// <summary>
    /// A 0.94.0 (Satzpaar): Gebiet fuer die Gewichtung ("diagramm", "pseudocode", "sql"), sonst null. Nur Auskunft fuer
    /// Pruefungen der Inhalte, die Oberflaeche zeigt es nicht.
    /// </summary>
    public string? Gebiet { get; init; }

    /// <summary>
    /// A 1.8.0 (Weisung P12-372): Fassung des Satzes, in der diese Teilaufgabe zuletzt geaendert wurde (1 = seit der ersten
    /// unveraendert). Ein offener Lauf einer aelteren Fassung legt die Antworten genau dieser Teile beiseite
    /// (<see cref="EchtAblauf"/>), die der uebrigen bleiben.
    /// </summary>
    public int Fassung { get; init; } = 1;

    /// <summary>Punkte, die man selbst mit Haekchen vergibt (der Rest bewertet das Programm).</summary>
    public int SelbstPunkte => Antwort.Form switch
    {
        EchtSaetze.FormZeilen or EchtSaetze.FormAngaben or EchtSaetze.FormZeichnen => Punkte,
        EchtSaetze.FormRechnen => Punkte - Antwort.PunkteErgebnis,
        _ => 0,
    };
}

/// <summary>Eine Aufgabe eines Satzes (24 bis 26 Punkte; im Satzpaar 20 bis 30).</summary>
public sealed record EchtAufgabe(string Titel, int Punkte, IReadOnlyList<string> Einleitung, IReadOnlyList<EchtTeil> Teile);

/// <summary>Unbewertete Frage zur Pruefungszeit am Ende.</summary>
public sealed record EchtZeitfrage(string Text, IReadOnlyList<string> Optionen);

/// <summary>Ein Pruefungssatz: eine erfundene Firma, vier Aufgaben, 90 Minuten, 100 Punkte.</summary>
public sealed record EchtSatz(string Kennung, string Fachrichtung, string Bereich, string Firma, int Minuten,
    IReadOnlyList<string> Ausgangssituation, IReadOnlyList<EchtAufgabe> Aufgaben, IReadOnlyList<EchtAnlage> Anlagen,
    EchtZeitfrage Zeitfrage)
{
    public EchtAnlage? Anlage(string nr) => Anlagen.FirstOrDefault(a => a.Nr == nr);

    /// <summary>
    /// A 0.94.0 (Weisung P12-304, Strukturblatt Abschnitt 7): Aufbau "paar" = Satz eines AE-Satzpaars (Bereich 1 und 2
    /// derselben Firma, Kennungen mit gleicher Nummer); null = bisheriger Aufbau.
    /// </summary>
    public string? Aufbau { get; init; }

    public bool Paar => Aufbau == EchtSaetze.AufbauPaar;

    /// <summary>
    /// A 1.8.0 (Weisung P12-372): Fassung des Satzes (ohne Feld 1). Eine hoehere Fassung derselben Kennung steht weiter hinten
    /// in der Datei und ersetzt beim Laden die alte (die alte bleibt Byte fuer Byte in der Datei).
    /// </summary>
    public int Fassung { get; init; } = 1;
}

/// <summary>Ein Satz, den die Ladepruefung abgewiesen hat, mit dem Grund (nie still weglassen).</summary>
public sealed record EchtFehler(string Kennung, string Meldung);

/// <summary>Ergebnis des Ladens: die gueltigen Saetze und alle Fehler (auch der ganzen Datei).</summary>
public sealed record EchtBestand(IReadOnlyList<EchtSatz> Saetze, IReadOnlyList<EchtFehler> Fehler)
{
    public static readonly EchtBestand Leer = new([], []);

    /// <summary>
    /// A 0.94.0 (Weisung P12-304): Kennungen der Saetze, die laut ausschluss.json nicht mehr angeboten werden. Sie bleiben
    /// im Bestand (<see cref="Satz"/> findet sie), damit ein gespeicherter, noch offener Lauf und alte Ergebnisse weiter
    /// gehen; neu gestartet werden sie nie (<see cref="Fuer"/>).
    /// </summary>
    public IReadOnlySet<string> Ausgeschlossen { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Gueltige Saetze eines Bereichs (ap1, b1, b2) fuer eine Fachrichtung; AP1 gilt fuer alle; ohne ausgeschlossene.</summary>
    public IReadOnlyList<EchtSatz> Fuer(string bereich, string fachrichtung) => Saetze
        .Where(s => s.Bereich == bereich && (bereich == EchtSaetze.Ap1 ? s.Fachrichtung == EchtSaetze.Alle : s.Fachrichtung == fachrichtung)
            && !Ausgeschlossen.Contains(s.Kennung))
        .ToList();

    /// <summary>Wird dieser Satz nicht mehr angeboten (ersetzt)?</summary>
    public bool IstAusgeschlossen(string? kennung) => kennung is not null && Ausgeschlossen.Contains(kennung);

    /// <summary>Fehler, die einen Bereich betreffen (Satz dieses Bereichs oder die ganze Datei).</summary>
    public IReadOnlyList<EchtFehler> FehlerFuer(string bereich, string fachrichtung) => Fehler
        .Where(f => f.Kennung == EchtSaetze.Datei || EchtSaetze.BereichAusKennung(f.Kennung) is not { } b
            || (b.Bereich == bereich && (bereich == EchtSaetze.Ap1 || b.Fachrichtung == fachrichtung)))
        .ToList();

    public EchtSatz? Satz(string? kennung) => Saetze.FirstOrDefault(s => s.Kennung == kennung);
}

/// <summary>
/// Welle 1 "Pruefung im echten Format" (Weisung P12-262, Plan P12-152 Abschnitt 2): Lader und Ladepruefung
/// fuer inhalte_csharp/lernen/pruefungssaetze.json (nur angehaengt). Format 1 = Welle 1; Format 2 (Welle 2, A 0.77.0)
/// erlaubt dazu gezeichnete Anlagen und Zeichenaufgaben (<see cref="EchtZeichnung"/>); im Format 1 sind diese ein Fehler. Jeder Satz, der eine Regel
/// verletzt, wird mit klarer Meldung abgewiesen; das Programm stuerzt nie ab und laesst nie still etwas weg.
/// Regeln nach dem Strukturblatt P12-260 (Abschnitte 1 bis 3).
/// </summary>
public static class EchtSaetze
{
    public const string DateiName = "pruefungssaetze";
    public const string Datei = "pruefungssaetze.json";
    /// <summary>
    /// Hoechstes bekanntes Format: 1 = Welle 1 (A 0.76.0), 2 = gezeichnete Anlagen und Zeichenaufgaben (A 0.77.0),
    /// 3 = AE-Satzpaare mit Feld "aufbau" und Feld "gebiet" je Teil (A 0.94.0, Weisung P12-304), 4 = Klassen- und
    /// Use-Case-Diagramm im Baukasten und neue Fassung eines Satzes mit Feld "fassung" (A 1.8.0, Weisungen P12-371/372),
    /// 5 = Struktogramm im Baukasten (Diagramm "struktogramm" mit Baum "bloecke", A 1.12.0, Weisung P12-390).
    /// </summary>
    public const int Format = 5;

    /// <summary>Aufbau eines Satzes im AE-Satzpaar (Strukturblatt Abschnitt 7).</summary>
    public const string AufbauPaar = "paar";

    /// <summary>Gebiete eines Teils im Satzpaar (Gewicht: Diagramme etwa ein Viertel, Pseudocode und SQL etwa ein Drittel).</summary>
    public static readonly IReadOnlyList<string> Gebiete = ["diagramm", "pseudocode", "sql"];
    public const string Ap1 = "ap1";
    public const string B1 = "b1";
    public const string B2 = "b2";
    public const string Alle = "alle";

    public const string FormZeilen = "zeilen";
    public const string FormAngaben = "angaben";
    public const string FormRechnen = "rechnen";
    public const string FormTabelle = "tabelle";
    public const string FormLuecke = "luecke";
    public const string FormAuswahl = "auswahl";
    public const string FormZeichnen = "zeichnen";

    /// <summary>Antwortformen der Welle 1 (Format 1).</summary>
    public static readonly IReadOnlyList<string> Formen = [FormZeilen, FormAngaben, FormRechnen, FormTabelle, FormLuecke, FormAuswahl];

    /// <summary>Antwortformen ab Format 2.</summary>
    public static readonly IReadOnlyList<string> Formen2 = [.. Formen, FormZeichnen];

    /// <summary>Arten der Lueckenfelder.</summary>
    public static readonly IReadOnlyList<string> FeldArten = ["ip", "maske", "zahl", "text"];

    /// <summary>Arten der Anlagen in Welle 1 (Format 1).</summary>
    public static readonly IReadOnlyList<string> AnlagenArten = ["text", "tabelle", "code", "englisch"];

    /// <summary>Arten der Anlagen ab Format 2: dazu Netzplan und Monitor-Bild, gezeichnet aus Daten.</summary>
    public static readonly IReadOnlyList<string> AnlagenArten2 = [.. AnlagenArten, "netzplan", "monitor"];

    /// <summary>
    /// Operatoren mit erlaubten Punkten (Strukturblatt Abschnitt 3): Nennen 1 je Angabe (2 bis 3 Angaben),
    /// Beschreiben/Erlaeutern 2 bis 3 je Angabe (1 bis 2 Angaben, 2 bis 6), Begruenden 3, Ermitteln/Berechnen
    /// 2 bis 4, Ergaenzen 4 bis 6, Erstellen/Zeichnen 8 bis 9, Korrigieren 2 bis 6, Vergleichen 4 bis 6.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Operatoren = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["nennen"] = "Nennen",
        ["beschreiben"] = "Beschreiben",
        ["erlaeutern"] = "Erläutern",
        ["begruenden"] = "Begründen",
        ["ermitteln"] = "Ermitteln",
        ["berechnen"] = "Berechnen",
        ["ergaenzen"] = "Ergänzen",
        ["erstellen"] = "Erstellen",
        ["zeichnen"] = "Zeichnen",
        ["korrigieren"] = "Korrigieren",
        ["vergleichen"] = "Vergleichen",
    };

    private static readonly Regex KennungMuster = new("^(alle|si|ae|dpa|dv)_(ap1|b1|b2)_[0-9]{2}$", RegexOptions.CultureInvariant);
    private static readonly Regex NrMuster = new("^[a-j][a-j]?$", RegexOptions.CultureInvariant);
    private static readonly Regex BezugMuster = new("^([1-4])([a-j][a-j]?)$", RegexOptions.CultureInvariant);

    /// <summary>Bereich und Fachrichtung aus der Kennung (z. B. "si_b2_03"); null, wenn sie nicht passt.</summary>
    public static (string Fachrichtung, string Bereich)? BereichAusKennung(string kennung)
    {
        var treffer = KennungMuster.Match(kennung);
        return treffer.Success ? (treffer.Groups[1].Value, treffer.Groups[2].Value) : null;
    }

    /// <summary>
    /// Datei lesen und pruefen; null = Datei fehlt (keine Saetze, kein Fehler). <paramref name="ausgeschlossen"/>: Kennungen,
    /// die laut ausschluss.json nicht mehr angeboten werden (A 0.94.0); sie werden trotzdem geprueft und geladen.
    /// </summary>
    public static EchtBestand Laden(byte[]? datei, IEnumerable<string>? ausgeschlossen = null)
    {
        var bestand = LadenOhneAusschluss(datei);
        return ausgeschlossen is null ? bestand
            : bestand with { Ausgeschlossen = new HashSet<string>(ausgeschlossen, StringComparer.Ordinal) };
    }

    private static EchtBestand LadenOhneAusschluss(byte[]? datei)
    {
        if (datei is null)
        {
            return EchtBestand.Leer;
        }
        JsonNode? wurzel;
        try
        {
            wurzel = JsonNode.Parse(datei);
        }
        catch (JsonException kaputt)
        {
            return new([], [new(Datei, "kein gültiges JSON: " + kaputt.Message)]);
        }
        if (wurzel is not JsonObject objekt)
        {
            return new([], [new(Datei, "die Datei ist kein Objekt mit \"format\" und \"saetze\"")]);
        }
        var format = Ganz(objekt["format"]);
        if (format is null)
        {
            return new([], [new(Datei, "\"format\" fehlt")]);
        }
        if (format > Format)
        {
            return new([], [new(Datei, string.Create(CultureInfo.InvariantCulture,
                $"Format {format} ist neuer als dieses Programm (kennt Format {Format}); bitte das Programm aktualisieren"))]);
        }
        if (format < 1)
        {
            return new([], [new(Datei, string.Create(CultureInfo.InvariantCulture, $"unbekanntes Format {format}"))]);
        }
        if (objekt["saetze"] is not JsonArray liste)
        {
            return new([], [new(Datei, "\"saetze\" ist keine Liste")]);
        }
        var saetze = new List<EchtSatz>();
        var fehler = new List<EchtFehler>();
        // A 1.8.0 (Weisung P12-372): hoechste Fassung je Kennung bisher; eine Kennung doppelt nur mit hoeherer Fassung
        var fassungen = new Dictionary<string, int>(StringComparer.Ordinal);
        var roh = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        for (var i = 0; i < liste.Count; i++)
        {
            var knoten = liste[i] as JsonObject;
            var kennung = Text(knoten?["kennung"]) ?? string.Create(CultureInfo.InvariantCulture, $"Satz Nr. {i + 1}");
            try
            {
                if (knoten is null)
                {
                    throw new EchtLadefehler("Eintrag ist kein Objekt");
                }
                var fassung = 1;
                if (knoten["fassung"] is not null)
                {
                    if (format < 4)
                    {
                        throw new EchtLadefehler("\"fassung\" erst ab Format 4");
                    }
                    fassung = Ganz(knoten["fassung"]) is { } f && f >= 1 ? f
                        : throw new EchtLadefehler("\"fassung\" muss eine ganze Zahl ab 1 sein");
                }
                if (fassungen.TryGetValue(kennung, out var bisher) && fassung <= bisher)
                {
                    throw new EchtLadefehler(fassung > 1
                        ? string.Create(CultureInfo.InvariantCulture, $"Fassung {fassung} ist nicht höher als die vorige Fassung {bisher}")
                        : "Kennung doppelt");
                }
                fassungen[kennung] = fassung;
                var satz = Satz(kennung, knoten, format.Value) with { Fassung = fassung };
                // die neue Fassung tritt an die Stelle der alten (Reihenfolge der Saetze bleibt); die alte bleibt in der Datei
                var alt = saetze.FindIndex(x => x.Kennung == kennung);
                if (alt >= 0)
                {
                    saetze[alt] = TeileMitFassung(satz, saetze[alt], knoten, roh[kennung]);
                    roh[kennung] = knoten;
                }
                else
                {
                    saetze.Add(satz);
                    roh[kennung] = knoten;
                }
            }
            catch (EchtLadefehler f)
            {
                fehler.Add(new(kennung, f.Message));
            }
            catch (Exception f) when (f is not OutOfMemoryException)
            {
                // nie abstuerzen (P12-261): auch ein unerwarteter Fehler weist nur diesen Satz ab, mit Meldung
                fehler.Add(new(kennung, "unerwarteter Fehler: " + f.Message));
            }
        }
        PaarePruefen(saetze, fehler);
        return new(saetze, fehler);
    }

    /// <summary>
    /// A 1.8.0 (Weisung P12-372): Fassung je Teilaufgabe einer neuen Fassung. Ein Teil, dessen JSON gleich dem Teil mit
    /// derselben Aufgabe und Nummer der vorigen Fassung ist, behaelt dessen Fassung; jeder andere (geaendert oder neu) bekommt
    /// die neue.
    /// </summary>
    private static EchtSatz TeileMitFassung(EchtSatz neu, EchtSatz vorher, JsonObject neuRoh, JsonObject vorherRoh)
    {
        static Dictionary<(int, string), string> Teile(JsonObject satz) =>
            (satz["aufgaben"] as JsonArray ?? []).Select((a, n) => (a, n))
                .SelectMany(x => ((x.a as JsonObject)?["teile"] as JsonArray ?? []).OfType<JsonObject>()
                    .Select(t => (Schluessel: (x.n, Text(t["nr"]) ?? ""), Json: t.ToJsonString())))
                .GroupBy(t => t.Schluessel).ToDictionary(g => g.Key, g => g.First().Json);
        var alt = Teile(vorherRoh);
        var jetzt = Teile(neuRoh);
        var aufgaben = neu.Aufgaben.Select((a, n) => a with
        {
            Teile = a.Teile.Select(t =>
            {
                var gleich = alt.TryGetValue((n, t.Nr), out var j) && jetzt.TryGetValue((n, t.Nr), out var k) && j == k;
                var frueher = gleich && n < vorher.Aufgaben.Count ? vorher.Aufgaben[n].Teile.FirstOrDefault(v => v.Nr == t.Nr) : null;
                return t with { Fassung = frueher?.Fassung ?? neu.Fassung };
            }).ToList(),
        }).ToList();
        return neu with { Aufgaben = aufgaben };
    }

    /// <summary>Kennung des Partners im Satzpaar (ae_b1_06 &lt;-&gt; ae_b2_06).</summary>
    public static string Partner(string kennung) =>
        kennung.Contains("_b1_", StringComparison.Ordinal) ? kennung.Replace("_b1_", "_b2_", StringComparison.Ordinal)
            : kennung.Replace("_b2_", "_b1_", StringComparison.Ordinal);

    /// <summary>
    /// A 0.94.0 (Strukturblatt Abschnitt 7): jeder Satz eines Paars braucht seinen gueltigen Partner mit derselben Firma, und
    /// das Paar hat genau einen englischen Text. Sonst werden beide mit Meldung abgewiesen (bis nichts mehr faellt).
    /// </summary>
    private static void PaarePruefen(List<EchtSatz> saetze, List<EchtFehler> fehler)
    {
        bool weiter;
        do
        {
            weiter = false;
            foreach (var s in saetze.Where(s => s.Paar).ToList())
            {
                var p = saetze.FirstOrDefault(x => x.Kennung == Partner(s.Kennung));
                string? grund = p is null || !p.Paar ? "der Partner " + Partner(s.Kennung) + " des Satzpaars fehlt oder ist ungültig"
                    : p.Firma != s.Firma ? "der Partner " + p.Kennung + " hat eine andere Firma"
                    : s.Anlagen.Concat(p.Anlagen).Count(a => a.Art == "englisch") != 1 ? "das Satzpaar braucht genau einen englischen Text"
                    : null;
                if (grund is not null)
                {
                    saetze.Remove(s);
                    fehler.Add(new(s.Kennung, grund));
                    weiter = true;
                }
            }
        }
        while (weiter);
    }

    private static EchtSatz Satz(string kennung, JsonObject s, int format)
    {
        if (BereichAusKennung(kennung) is not { } aus)
        {
            throw new EchtLadefehler("Kennung muss wie \"si_b2_01\" oder \"alle_ap1_01\" aufgebaut sein");
        }
        var fachrichtung = Pflicht(s, "fachrichtung");
        var bereich = Pflicht(s, "bereich");
        if (fachrichtung != aus.Fachrichtung || bereich != aus.Bereich)
        {
            throw new EchtLadefehler("Fachrichtung und Bereich passen nicht zur Kennung");
        }
        if ((bereich == Ap1) != (fachrichtung == Alle))
        {
            throw new EchtLadefehler("AP1 gilt für alle Fachrichtungen (\"alle\"), AP2 für genau eine");
        }
        var minuten = Ganz(s["minuten"]) ?? throw new EchtLadefehler("\"minuten\" fehlt");
        if (minuten != 90)
        {
            throw new EchtLadefehler("eine Prüfung dauert 90 Minuten");
        }
        var firma = Pflicht(s, "firma");
        string? aufbau = null;
        if (s["aufbau"] is not null)
        {
            aufbau = Text(s["aufbau"]);
            if (format < 3)
            {
                throw new EchtLadefehler("\"aufbau\" erst ab Format 3");
            }
            if (aufbau != AufbauPaar || fachrichtung != "ae" || bereich == Ap1)
            {
                throw new EchtLadefehler("\"aufbau\" kennt nur \"paar\" und gilt nur für AE Bereich 1 und 2");
            }
        }
        var paar = aufbau == AufbauPaar;
        var ausgang = Zeilen(s["ausgangssituation"], "ausgangssituation");
        var anlagen = new List<EchtAnlage>();
        foreach (var a in Liste(s["anlagen"], "anlagen"))
        {
            anlagen.Add(Anlage(a, format));
        }
        if (anlagen.Select(a => a.Nr).Distinct(StringComparer.Ordinal).Count() != anlagen.Count)
        {
            throw new EchtLadefehler("Anlagen-Nummer doppelt");
        }
        var aufgaben = Liste(s["aufgaben"], "aufgaben").Select((a, n) => Aufgabe(a, n + 1, anlagen, format, paar)).ToList();
        if (aufgaben.Count != 4)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{aufgaben.Count} Aufgaben statt 4"));
        }
        var summe = aufgaben.Sum(a => a.Punkte);
        if (summe != 100)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"die Aufgaben ergeben {summe} statt 100 Punkte"));
        }
        // Ersatzwert nur bei Folgerechnungen: der Bezug ist ein frueherer Teil desselben Satzes
        var frueher = new HashSet<string>(StringComparer.Ordinal);
        for (var n = 0; n < aufgaben.Count; n++)
        {
            foreach (var teil in aufgaben[n].Teile)
            {
                var wo = Wo(n + 1, teil.Nr);
                if (teil.ErsatzBezug is { } bezug && !frueher.Contains(bezug))
                {
                    throw new EchtLadefehler(wo + ": der Ersatzwert bezieht sich auf " + bezug + ", das ist kein früherer Teil");
                }
                frueher.Add(string.Create(CultureInfo.InvariantCulture, $"{n + 1}{teil.Nr}"));
            }
        }
        var alleTeile = aufgaben.SelectMany(a => a.Teile).ToList();
        var benutzt = alleTeile.SelectMany(t => t.Anlagen).ToHashSet(StringComparer.Ordinal);
        if (anlagen.FirstOrDefault(a => !benutzt.Contains(a.Nr)) is { } frei)
        {
            throw new EchtLadefehler("Anlage " + frei.Nr + " wird von keiner Teilaufgabe benutzt");
        }
        var englisch = anlagen.Count(a => a.Art == "englisch");
        if (paar)
        {
            PaarRegeln(englisch, aufgaben, alleTeile);
        }
        else
        {
            if (englisch != 1)
            {
                throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{englisch} englische Texte statt genau einem"));
            }
            if (!alleTeile.Any(t => t.Englisch))
            {
                throw new EchtLadefehler("keine Teilaufgabe zum englischen Text");
            }
        }
        var zeit = s["zeitfrage"] as JsonObject ?? throw new EchtLadefehler("\"zeitfrage\" fehlt");
        var zeitfrage = new EchtZeitfrage(Pflicht(zeit, "text"), Zeilen(zeit["optionen"], "zeitfrage.optionen"));
        if (zeitfrage.Optionen.Count < 2)
        {
            throw new EchtLadefehler("die Zeitfrage braucht mindestens zwei Antworten");
        }
        return new EchtSatz(kennung, fachrichtung, bereich, firma, minuten, ausgang, aufgaben, anlagen, zeitfrage) { Aufbau = aufbau };
    }

    /// <summary>
    /// Teilaufgaben eines Satzpaars: Eintraege mit gleichem ersten Buchstaben (b, ba, bb) bilden eine Teilaufgabe; ein Buchstabe
    /// steht entweder allein oder nur mit Unterpunkten. Ergebnis: Punkte je Teilaufgabe in der Reihenfolge der Aufgabe.
    /// </summary>
    public static IReadOnlyList<(char Buchstabe, int Punkte)> Teilaufgaben(IReadOnlyList<EchtTeil> teile) => teile
        .GroupBy(t => t.Nr[0]).Select(g => (g.Key, g.Sum(t => t.Punkte))).ToList();

    /// <summary>
    /// Regeln je Satz im Satzpaar (Strukturblatt Abschnitt 7): hoechstens ein englischer Text (genau einer je Paar, siehe
    /// <see cref="PaarePruefen"/>), Teile dazu hoechstens 12 Punkte; hoechstens ein Rechenteil; 2 bis 3 grosse Teilaufgaben
    /// (8 bis 14 Punkte) je Bereich.
    /// </summary>
    private static void PaarRegeln(int englisch, List<EchtAufgabe> aufgaben, List<EchtTeil> alleTeile)
    {
        if (englisch > 1)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{englisch} englische Texte, im Satzpaar höchstens einer je Satz"));
        }
        if (englisch == 1 && !alleTeile.Any(t => t.Englisch))
        {
            throw new EchtLadefehler("keine Teilaufgabe zum englischen Text");
        }
        var englischPunkte = alleTeile.Where(t => t.Englisch).Sum(t => t.Punkte);
        if (englischPunkte > 12)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"die Teile zum englischen Text ergeben {englischPunkte} Punkte, höchstens 12"));
        }
        var rechnen = alleTeile.Count(t => t.Antwort.Form == FormRechnen || t.Operator == "berechnen");
        if (rechnen > 1)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{rechnen} Rechenteile, im Satzpaar höchstens einer"));
        }
        var gross = aufgaben.Sum(a => Teilaufgaben(a.Teile).Count(t => t.Punkte >= 8));
        if (gross is < 2 or > 3)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{gross} große Teilaufgaben (8 bis 14 Punkte), erlaubt sind 2 bis 3"));
        }
    }

    private static EchtAnlage Anlage(JsonObject a, int format)
    {
        var nr = Pflicht(a, "nr");
        var art = Pflicht(a, "art");
        if (!(format >= 2 ? AnlagenArten2 : AnlagenArten).Contains(art))
        {
            throw new EchtLadefehler("Anlage " + nr + ": unbekannte Art \"" + art + "\""
                + (AnlagenArten2.Contains(art) ? " (erst ab Format 2)" : ""));
        }
        var titel = Pflicht(a, "titel");
        if (art is "netzplan" or "monitor")
        {
            // gezeichnet; "zeilen" ist hier ein freiwilliger Text unter dem Bild (z. B. eine Legende)
            var zusatz = a["zeilen"] is null ? [] : Zeilen(a["zeilen"], "Anlage " + nr + " zeilen");
            return art == "netzplan"
                ? new EchtAnlage(nr, titel, art, zusatz, [], []) { Netzplan = EchtZeichnung.Netzplan(a, "Anlage " + nr) }
                : new EchtAnlage(nr, titel, art, zusatz, [], []) { Monitor = EchtZeichnung.Monitor(a, "Anlage " + nr) };
        }
        if (art == "tabelle")
        {
            var spalten = Zeilen(a["spalten"], "Anlage " + nr + " spalten");
            var tabelle = Liste(a["zeilen"], "Anlage " + nr + " zeilen", ohneObjekt: true)
                .Select(z => (IReadOnlyList<string>)Zeilen(z, "Anlage " + nr + " Zeile")).ToList();
            if (tabelle.Count == 0 || tabelle.Any(z => z.Count != spalten.Count))
            {
                throw new EchtLadefehler("Anlage " + nr + ": jede Zeile braucht so viele Zellen wie Spalten");
            }
            return new EchtAnlage(nr, titel, art, [], spalten, tabelle);
        }
        return new EchtAnlage(nr, titel, art, Zeilen(a["zeilen"], "Anlage " + nr + " zeilen"), [], []);
    }

    private static EchtAufgabe Aufgabe(JsonObject a, int nummer, List<EchtAnlage> anlagen, int format, bool paar)
    {
        var wo = string.Create(CultureInfo.InvariantCulture, $"Aufgabe {nummer}");
        var titel = Pflicht(a, "titel");
        var punkte = Ganz(a["punkte"]) ?? throw new EchtLadefehler(wo + ": \"punkte\" fehlt");
        var (min, max) = paar ? (20, 30) : (24, 26);
        if (punkte < min || punkte > max)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: {punkte} Punkte, erlaubt sind {min} bis {max}"));
        }
        var teile = Liste(a["teile"], wo + " teile").Select(t => Teil(t, nummer, anlagen, format, paar)).ToList();
        if (teile.Select(t => t.Nr).Distinct(StringComparer.Ordinal).Count() != teile.Count)
        {
            throw new EchtLadefehler(wo + ": Teilaufgabe doppelt");
        }
        if (paar)
        {
            // Strukturblatt Abschnitt 7: 3 bis 6 Teilaufgaben (auch mit Unterpunkten ba, bb), je 2 bis 14 Punkte
            var einzeln = teile.Where(t => t.Nr.Length == 1).Select(t => t.Nr[0]).ToHashSet();
            if (teile.Any(t => t.Nr.Length == 2 && einzeln.Contains(t.Nr[0])))
            {
                throw new EchtLadefehler(wo + ": ein Buchstabe steht allein und mit Unterpunkten (z. B. b und ba)");
            }
            var gruppen = Teilaufgaben(teile);
            if (gruppen.Count is < 3 or > 6 || teile.Count > 10)
            {
                throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture,
                    $"{wo}: {gruppen.Count} Teilaufgaben mit {teile.Count} Einträgen, erlaubt sind 3 bis 6 (höchstens 10 Einträge)"));
            }
            if (gruppen.FirstOrDefault(g => g.Punkte is < 2 or > 14) is { Buchstabe: not '\0' } falsch)
            {
                throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: Teilaufgabe {falsch.Buchstabe} hat {falsch.Punkte} Punkte, erlaubt sind 2 bis 14"));
            }
        }
        else if (teile.Count is < 5 or > 10)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: {teile.Count} Teilaufgaben, erlaubt sind 5 bis 10"));
        }
        var summe = teile.Sum(t => t.Punkte);
        if (summe != punkte)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: Teilaufgaben ergeben {summe} statt {punkte} Punkte"));
        }
        return new EchtAufgabe(titel, punkte, a["einleitung"] is null ? [] : Zeilen(a["einleitung"], wo + " einleitung"), teile);
    }

    private static EchtTeil Teil(JsonObject t, int aufgabe, List<EchtAnlage> anlagen, int format, bool paar)
    {
        var nr = Pflicht(t, "nr");
        var wo = Wo(aufgabe, nr);
        if (!NrMuster.IsMatch(nr))
        {
            throw new EchtLadefehler(wo + ": Nummer muss a bis j oder zwei Buchstaben wie \"ba\" sein");
        }
        var op = Pflicht(t, "operator");
        if (!Operatoren.ContainsKey(op))
        {
            throw new EchtLadefehler(wo + ": unbekannter Operator \"" + op + "\"");
        }
        var text = Zeilen(t["text"], wo + " text");
        var punkte = Ganz(t["punkte"]) ?? throw new EchtLadefehler(wo + ": \"punkte\" fehlt");
        var hoechstens = paar ? 14 : 9;
        if (punkte < 2 || punkte > hoechstens)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: {punkte} Punkte, erlaubt sind 2 bis {hoechstens}"));
        }
        var angaben = Ganz(t["angaben"]) ?? 1;
        if (paar)
        {
            OperatorPaarPruefen(wo, op, punkte, angaben);
        }
        else
        {
            OperatorPruefen(wo, op, punkte, angaben);
        }
        string? gebiet = null;
        if (t["gebiet"] is not null)
        {
            gebiet = Text(t["gebiet"]);
            if (!paar || gebiet is null || !Gebiete.Contains(gebiet))
            {
                throw new EchtLadefehler(wo + ": \"gebiet\" gibt es nur im Satzpaar (diagramm, pseudocode, sql)");
            }
        }
        var antwort = Antwort(t["antwort"] as JsonObject ?? throw new EchtLadefehler(wo + ": \"antwort\" fehlt"), wo, format);
        var verweise = t["anlagen"] is null ? [] : Zeilen(t["anlagen"], wo + " anlagen");
        if (verweise.FirstOrDefault(v => anlagen.All(a => a.Nr != v)) is { } fehlt)
        {
            throw new EchtLadefehler(wo + ": Anlage " + fehlt + " gibt es nicht");
        }
        var englisch = t["englisch"] is JsonValue e && e.TryGetValue<bool>(out var ja) && ja;
        if (englisch && !verweise.Any(v => anlagen.First(a => a.Nr == v).Art == "englisch"))
        {
            throw new EchtLadefehler(wo + ": Teil zum englischen Text ohne Verweis auf die englische Anlage");
        }
        string? bezug = null;
        string? ersatzText = null;
        if (t["ersatzwert"] is JsonObject ersatz)
        {
            bezug = Pflicht(ersatz, "bezug");
            ersatzText = Pflicht(ersatz, "text");
            if (!BezugMuster.IsMatch(bezug))
            {
                throw new EchtLadefehler(wo + ": Bezug des Ersatzwerts wie \"1c\" angeben");
            }
            if (antwort.Form != FormRechnen || antwort.ErsatzErgebnis is null)
            {
                throw new EchtLadefehler(wo + ": ein Ersatzwert gehört zu einer Rechnung mit \"ersatz_ergebnis\"");
            }
        }
        else if (antwort.ErsatzErgebnis is not null)
        {
            throw new EchtLadefehler(wo + ": \"ersatz_ergebnis\" ohne Ersatzwert");
        }
        var loesung = Zeilen(t["loesung"], wo + " loesung");
        var hinweise = t["hinweise"] is null ? new List<EchtHinweis>()
            : Liste(t["hinweise"], wo + " hinweise").Select(h => new EchtHinweis(Pflicht(h, "text"),
                Ganz(h["punkte"]) ?? throw new EchtLadefehler(wo + ": Hinweis ohne Punkte"))).ToList();
        if (hinweise.Any(h => h.Punkte < 1))
        {
            throw new EchtLadefehler(wo + ": ein Hinweis braucht mindestens 1 Punkt");
        }
        var teil = new EchtTeil(nr, op, text, punkte, angaben, antwort, verweise, bezug, ersatzText, loesung, hinweise, englisch) { Gebiet = gebiet };
        FormPruefen(wo, teil);
        return teil;
    }

    /// <summary>
    /// A 0.94.0 (Strukturblatt Abschnitt 7): Operatoren im Satzpaar mit groesseren Teilaufgaben: Nennen 1 je Angabe (2 bis 6),
    /// Beschreiben/Erlaeutern 1 bis 3 Angaben mit 2 bis 3 je Angabe, Begruenden 3 je Angabe (1 bis 2), Ermitteln/Berechnen
    /// 2 bis 6, Ergaenzen 4 bis 12, Erstellen/Zeichnen 4 bis 14, Korrigieren 2 bis 8, Vergleichen 4 bis 8.
    /// </summary>
    private static void OperatorPaarPruefen(string wo, string op, int punkte, int angaben)
    {
        var (ok, regel) = op switch
        {
            "nennen" => (angaben is >= 2 and <= 6 && punkte == angaben, "Nennen: 2 bis 6 Angaben, 1 Punkt je Angabe"),
            "beschreiben" or "erlaeutern" => (angaben is >= 1 and <= 3 && punkte >= 2 * angaben && punkte <= 3 * angaben,
                "Beschreiben/Erläutern: 1 bis 3 Angaben, 2 bis 3 Punkte je Angabe"),
            "begruenden" => (angaben is 1 or 2 && punkte == 3 * angaben, "Begründen: 3 Punkte je Angabe, 1 bis 2 Angaben"),
            "ermitteln" or "berechnen" => (punkte is >= 2 and <= 6, "Ermitteln/Berechnen: 2 bis 6 Punkte"),
            "ergaenzen" => (punkte is >= 4 and <= 12, "Ergänzen: 4 bis 12 Punkte"),
            "erstellen" or "zeichnen" => (punkte is >= 4 and <= 14, "Erstellen/Zeichnen: 4 bis 14 Punkte"),
            "korrigieren" => (punkte is >= 2 and <= 8, "Korrigieren: 2 bis 8 Punkte"),
            "vergleichen" => (punkte is >= 4 and <= 8, "Vergleichen: 4 bis 8 Punkte"),
            _ => (false, "unbekannter Operator"),
        };
        if (!ok)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture,
                $"{wo}: {punkte} Punkte bei {angaben} Angabe(n) passen nicht zur Regel ({regel})"));
        }
    }

    private static void OperatorPruefen(string wo, string op, int punkte, int angaben)
    {
        var (ok, regel) = op switch
        {
            "nennen" => (angaben is 2 or 3 && punkte == angaben, "Nennen: 2 bis 3 Angaben, 1 Punkt je Angabe"),
            "beschreiben" or "erlaeutern" => (angaben is 1 or 2 && punkte >= 2 * angaben && punkte <= 3 * angaben,
                "Beschreiben/Erläutern: 1 bis 2 Angaben, 2 bis 3 Punkte je Angabe"),
            "begruenden" => (punkte == 3, "Begründen: 3 Punkte"),
            "ermitteln" or "berechnen" => (punkte is >= 2 and <= 4, "Ermitteln/Berechnen: 2 bis 4 Punkte"),
            "ergaenzen" => (punkte is >= 4 and <= 6, "Ergänzen: 4 bis 6 Punkte"),
            "erstellen" or "zeichnen" => (punkte is 8 or 9, "Erstellen/Zeichnen: 8 bis 9 Punkte"),
            "korrigieren" => (punkte is >= 2 and <= 6, "Korrigieren: 2 bis 6 Punkte"),
            "vergleichen" => (punkte is >= 4 and <= 6, "Vergleichen: 4 bis 6 Punkte"),
            _ => (false, "unbekannter Operator"),
        };
        if (!ok)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture,
                $"{wo}: {punkte} Punkte bei {angaben} Angabe(n) passen nicht zur Regel ({regel})"));
        }
    }

    private static EchtAntwort Antwort(JsonObject a, string wo, int format)
    {
        var form = Pflicht(a, "form");
        if (!(format >= 2 ? Formen2 : Formen).Contains(form))
        {
            throw new EchtLadefehler(wo + ": unbekannte Antwortform \"" + form + "\"" + (Formen2.Contains(form) ? " (erst ab Format 2)" : ""));
        }
        switch (form)
        {
            case FormZeilen:
                var zeilen = Ganz(a["zeilen"]) ?? 4;
                if (zeilen is < 1 or > 20)
                {
                    throw new EchtLadefehler(wo + ": 1 bis 20 Zeilen");
                }
                return new EchtAntwort(form) { Zeilen = zeilen };
            case FormRechnen:
                return new EchtAntwort(form)
                {
                    Einheit = Text(a["einheit"]) ?? "",
                    Ergebnis = Zahl(a["ergebnis"]) ?? throw new EchtLadefehler(wo + ": \"ergebnis\" fehlt"),
                    Toleranz = Zahl(a["toleranz"]) ?? 0,
                    ErsatzErgebnis = Zahl(a["ersatz_ergebnis"]),
                    PunkteErgebnis = Ganz(a["punkte_ergebnis"]) ?? throw new EchtLadefehler(wo + ": \"punkte_ergebnis\" fehlt"),
                };
            case FormTabelle:
                var spalten = Zeilen(a["spalten"], wo + " spalten");
                var tabelle = Liste(a["zeilen"], wo + " zeilen", ohneObjekt: true).Select(z => (IReadOnlyList<EchtZelle>)(z is JsonArray zellen
                    ? zellen.Select(c => Zelle(c, wo)).ToList()
                    : throw new EchtLadefehler(wo + ": Tabellenzeile ist keine Liste"))).ToList();
                if (tabelle.Count == 0 || tabelle.Any(z => z.Count != spalten.Count))
                {
                    throw new EchtLadefehler(wo + ": jede Zeile braucht so viele Zellen wie Spalten");
                }
                return new EchtAntwort(form) { Spalten = spalten, Tabelle = tabelle };
            case FormLuecke:
                var felder = Liste(a["felder"], wo + " felder").Select(f =>
                {
                    var art = Pflicht(f, "art");
                    if (!FeldArten.Contains(art))
                    {
                        throw new EchtLadefehler(wo + ": unbekannte Feldart \"" + art + "\"");
                    }
                    var feld = new EchtFeld(Pflicht(f, "name"), art, Zeilen(f["loesung"], wo + " loesung"), Ganz(f["punkte"]) ?? 1);
                    if (feld.Loesung.Count == 0 || feld.Punkte < 1)
                    {
                        throw new EchtLadefehler(wo + ": Feld " + feld.Name + " ohne Lösung oder Punkte");
                    }
                    if (feld.Loesung.FirstOrDefault(l => EchtBewertung.Normal(art, l) is null) is { } kaputt)
                    {
                        throw new EchtLadefehler(wo + ": Lösung \"" + kaputt + "\" passt nicht zur Feldart " + art);
                    }
                    return feld;
                }).ToList();
                return new EchtAntwort(form) { Felder = felder };
            case FormAuswahl:
                var optionen = Zeilen(a["optionen"], wo + " optionen");
                var richtig = a["richtig"] is JsonArray r ? r.Select(x => Ganz(x) ?? -1).ToList() : [];
                if (optionen.Count < 2 || richtig.Count == 0 || richtig.Any(x => x < 0 || x >= optionen.Count)
                    || richtig.Distinct().Count() != richtig.Count)
                {
                    throw new EchtLadefehler(wo + ": Auswahl braucht mindestens 2 Optionen und gültige richtige Nummern");
                }
                return new EchtAntwort(form) { Optionen = optionen, Richtig = richtig };
            case FormZeichnen:
                var art = Pflicht(a, "diagramm");
                if (art == EchtStruktogramm.Art)
                {
                    // A 1.12.0 (Weisung P12-390): Struktogramm als Baum, keine Vorgabe
                    if (format < 5)
                    {
                        throw new EchtLadefehler(wo + ": Diagramm \"" + art + "\" erst ab Format 5");
                    }
                    if (a["vorgabe"] is not null)
                    {
                        throw new EchtLadefehler(wo + ": ein Struktogramm hat keine Vorgabe");
                    }
                    return new EchtAntwort(form)
                    {
                        Diagramm = art,
                        Struktogramm = EchtStruktogramm.Lesen(a["muster"], wo + " Musterbild", vollstaendig: true),
                    };
                }
                if (!EchtZeichnung.Bausteine.ContainsKey(art))
                {
                    throw new EchtLadefehler(wo + ": Diagramm muss \"uml\" oder \"er\" sein");
                }
                if (EchtZeichnung.ArtenFormat4.Contains(art) && format < 4)
                {
                    throw new EchtLadefehler(wo + ": Diagramm \"" + art + "\" erst ab Format 4");
                }
                var muster = EchtZeichnung.Diagramm(a["muster"], art, vollstaendig: true, wo + " Musterbild");
                var vorgabe = a["vorgabe"] is null ? null : EchtZeichnung.Diagramm(a["vorgabe"], art, vollstaendig: false, wo + " Vorgabe");
                if (vorgabe?.Elemente.FirstOrDefault(v => !muster.Elemente.Contains(v)) is { } fremd)
                {
                    throw new EchtLadefehler(wo + ": Baustein " + fremd.Id + " der Vorgabe steht nicht gleich im Musterbild");
                }
                if (vorgabe?.Verbindungen.FirstOrDefault(v => !muster.Verbindungen.Any(m => (m.Von == v.Von && m.Nach == v.Nach) || (m.Von == v.Nach && m.Nach == v.Von))) is { } linie)
                {
                    throw new EchtLadefehler(wo + ": Verbindung " + linie.Von + "–" + linie.Nach + " der Vorgabe fehlt im Musterbild");
                }
                // A 1.8.0: Beziehungen der Vorgabe mit gleicher Art, Richtung und (wo angegeben) Multiplizitaet; Use-Case mit Grenze
                if (EchtZeichnung.ArtenFormat4.Contains(art) && vorgabe?.Verbindungen.FirstOrDefault(v => !muster.Verbindungen.Any(m =>
                    m.Typ == v.Typ && ((m.Von == v.Von && m.Nach == v.Nach && (v.MultVon.Length == 0 || v.MultVon == m.MultVon)
                        && (v.MultNach.Length == 0 || v.MultNach == m.MultNach))
                        || (!EchtZeichnung.Gerichtet(v.Typ) && m.Von == v.Nach && m.Nach == v.Von && (v.MultVon.Length == 0 || v.MultVon == m.MultNach)
                        && (v.MultNach.Length == 0 || v.MultNach == m.MultVon))))) is { } anders)
                {
                    throw new EchtLadefehler(wo + ": Beziehung " + anders.Von + "–" + anders.Nach + " der Vorgabe steht nicht gleich im Musterbild");
                }
                if (art == EchtZeichnung.UseCase && (vorgabe is null || vorgabe.Grenze != muster.Grenze))
                {
                    throw new EchtLadefehler(wo + ": ein Use-Case-Diagramm braucht eine Vorgabe mit derselben Systemgrenze wie das Musterbild");
                }
                return new EchtAntwort(form) { Diagramm = art, Vorgabe = vorgabe, Muster = muster };
            default:
                return new EchtAntwort(form);
        }
    }

    private static EchtZelle Zelle(JsonNode? c, string wo) => c switch
    {
        JsonValue v when v.TryGetValue<string>(out var fest) => new EchtZelle(fest, [], 0),
        JsonObject o => new EchtZelle(null, Zeilen(o["loesung"], wo + " Zelle"), Ganz(o["punkte"]) ?? 1) is { } z
            && z.Loesung.Count > 0 && z.Punkte >= 1 ? z : throw new EchtLadefehler(wo + ": offene Zelle ohne Lösung oder Punkte"),
        _ => throw new EchtLadefehler(wo + ": Zelle ist weder Text noch offen"),
    };

    /// <summary>Punkte der Antwortform muessen genau die Punkte der Teilaufgabe ergeben.</summary>
    private static void FormPruefen(string wo, EchtTeil t)
    {
        var a = t.Antwort;
        var hinweise = t.Hinweise.Sum(h => h.Punkte);
        string? fehler = a.Form switch
        {
            FormZeilen => hinweise == t.Punkte ? null : "die Hinweise ergeben nicht die Punkte der Teilaufgabe",
            FormAngaben => t.Angaben >= 1 && t.Punkte % t.Angaben == 0 && t.Hinweise.Count == 0 ? null
                : "Angaben: Punkte müssen durch die Zahl der Angaben teilbar sein, Hinweise sind die Felder selbst",
            FormRechnen => a.PunkteErgebnis >= 1 && a.PunkteErgebnis <= t.Punkte && hinweise == t.Punkte - a.PunkteErgebnis
                ? null : "Rechnen: Punkte des Ergebnisses plus Hinweise zum Rechenweg ergeben nicht die Punkte",
            FormTabelle => a.Tabelle.SelectMany(z => z).Where(z => z.Offen).Sum(z => z.Punkte) == t.Punkte && t.Hinweise.Count == 0
                ? null : "Tabelle: die offenen Zellen ergeben nicht die Punkte",
            FormLuecke => a.Felder.Sum(f => f.Punkte) == t.Punkte && t.Hinweise.Count == 0 ? null : "Lücken: die Felder ergeben nicht die Punkte",
            FormAuswahl => t.Hinweise.Count == 0 ? null : "Auswahl hat keine Hinweise",
            FormZeichnen => t.Operator is not ("zeichnen" or "erstellen") ? "eine Zeichenaufgabe hat den Operator Zeichnen oder Erstellen"
                : hinweise == t.Punkte ? null : "die Hinweise zum Musterbild ergeben nicht die Punkte der Teilaufgabe",
            _ => "unbekannte Antwortform",
        };
        if (fehler is not null)
        {
            throw new EchtLadefehler(wo + ": " + fehler);
        }
        if (t.Loesung.Count == 0)
        {
            throw new EchtLadefehler(wo + ": Musterlösung fehlt");
        }
    }

    internal static string Wo(int aufgabe, string nr) => string.Create(CultureInfo.InvariantCulture, $"Aufgabe {aufgabe} {nr})");

    // -- kleine Lesehilfen --------------------------------------------------------------------

    private static string Pflicht(JsonObject o, string name) =>
        Text(o[name]) is { Length: > 0 } wert ? wert : throw new EchtLadefehler("\"" + name + "\" fehlt oder ist leer");

    private static string? Text(JsonNode? knoten) => knoten is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static int? Ganz(JsonNode? knoten) => knoten is JsonValue v && v.GetValueKind() == JsonValueKind.Number
        && v.TryGetValue<int>(out var i) ? i : null;

    private static double? Zahl(JsonNode? knoten) => knoten is JsonValue v && v.GetValueKind() == JsonValueKind.Number
        && v.TryGetValue<double>(out var d) ? d : null;

    private static List<string> Zeilen(JsonNode? knoten, string wo)
    {
        if (knoten is not JsonArray liste)
        {
            throw new EchtLadefehler("\"" + wo + "\" ist keine Liste von Texten");
        }
        return liste.Select(z => Text(z) ?? throw new EchtLadefehler("\"" + wo + "\" enthält etwas anderes als Text")).ToList();
    }

    private static List<JsonObject> Liste(JsonNode? knoten, string wo) => Liste(knoten, wo, false).Cast<JsonObject>().ToList();

    private static List<JsonNode?> Liste(JsonNode? knoten, string wo, bool ohneObjekt)
    {
        if (knoten is not JsonArray liste || liste.Count == 0)
        {
            throw new EchtLadefehler("\"" + wo + "\" fehlt oder ist leer");
        }
        if (!ohneObjekt && liste.Any(x => x is not JsonObject))
        {
            throw new EchtLadefehler("\"" + wo + "\" enthält etwas anderes als Objekte");
        }
        return [.. liste];
    }
}

/// <summary>Ein Satz verletzt eine Regel der Ladepruefung (Meldung fuer Mensch und Test).</summary>
public sealed class EchtLadefehler(string meldung) : Exception(meldung);
