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
}

/// <summary>Eine Teilaufgabe (a, b, ba, bb ...).</summary>
public sealed record EchtTeil(string Nr, string Operator, IReadOnlyList<string> Text, int Punkte, int Angaben,
    EchtAntwort Antwort, IReadOnlyList<string> Anlagen, string? ErsatzBezug, string? ErsatzText, IReadOnlyList<string> Loesung,
    IReadOnlyList<EchtHinweis> Hinweise, bool Englisch)
{
    /// <summary>Punkte, die man selbst mit Haekchen vergibt (der Rest bewertet das Programm).</summary>
    public int SelbstPunkte => Antwort.Form switch
    {
        EchtSaetze.FormZeilen or EchtSaetze.FormAngaben or EchtSaetze.FormZeichnen => Punkte,
        EchtSaetze.FormRechnen => Punkte - Antwort.PunkteErgebnis,
        _ => 0,
    };
}

/// <summary>Eine Aufgabe eines Satzes (24 bis 26 Punkte).</summary>
public sealed record EchtAufgabe(string Titel, int Punkte, IReadOnlyList<string> Einleitung, IReadOnlyList<EchtTeil> Teile);

/// <summary>Unbewertete Frage zur Pruefungszeit am Ende.</summary>
public sealed record EchtZeitfrage(string Text, IReadOnlyList<string> Optionen);

/// <summary>Ein Pruefungssatz: eine erfundene Firma, vier Aufgaben, 90 Minuten, 100 Punkte.</summary>
public sealed record EchtSatz(string Kennung, string Fachrichtung, string Bereich, string Firma, int Minuten,
    IReadOnlyList<string> Ausgangssituation, IReadOnlyList<EchtAufgabe> Aufgaben, IReadOnlyList<EchtAnlage> Anlagen,
    EchtZeitfrage Zeitfrage)
{
    public EchtAnlage? Anlage(string nr) => Anlagen.FirstOrDefault(a => a.Nr == nr);
}

/// <summary>Ein Satz, den die Ladepruefung abgewiesen hat, mit dem Grund (nie still weglassen).</summary>
public sealed record EchtFehler(string Kennung, string Meldung);

/// <summary>Ergebnis des Ladens: die gueltigen Saetze und alle Fehler (auch der ganzen Datei).</summary>
public sealed record EchtBestand(IReadOnlyList<EchtSatz> Saetze, IReadOnlyList<EchtFehler> Fehler)
{
    public static readonly EchtBestand Leer = new([], []);

    /// <summary>Gueltige Saetze eines Bereichs (ap1, b1, b2) fuer eine Fachrichtung; AP1 gilt fuer alle.</summary>
    public IReadOnlyList<EchtSatz> Fuer(string bereich, string fachrichtung) => Saetze
        .Where(s => s.Bereich == bereich && (bereich == EchtSaetze.Ap1 ? s.Fachrichtung == EchtSaetze.Alle : s.Fachrichtung == fachrichtung))
        .ToList();

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
    /// <summary>Hoechstes bekanntes Format: 1 = Welle 1 (A 0.76.0), 2 = gezeichnete Anlagen und Zeichenaufgaben (A 0.77.0).</summary>
    public const int Format = 2;
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

    /// <summary>Datei lesen und pruefen; null = Datei fehlt (keine Saetze, kein Fehler).</summary>
    public static EchtBestand Laden(byte[]? datei)
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
        var kennungen = new HashSet<string>(StringComparer.Ordinal);
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
                if (!kennungen.Add(kennung))
                {
                    throw new EchtLadefehler("Kennung doppelt");
                }
                saetze.Add(Satz(kennung, knoten, format.Value));
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
        return new(saetze, fehler);
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
        var aufgaben = Liste(s["aufgaben"], "aufgaben").Select((a, n) => Aufgabe(a, n + 1, anlagen, format)).ToList();
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
        if (englisch != 1)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{englisch} englische Texte statt genau einem"));
        }
        if (!alleTeile.Any(t => t.Englisch))
        {
            throw new EchtLadefehler("keine Teilaufgabe zum englischen Text");
        }
        var zeit = s["zeitfrage"] as JsonObject ?? throw new EchtLadefehler("\"zeitfrage\" fehlt");
        var zeitfrage = new EchtZeitfrage(Pflicht(zeit, "text"), Zeilen(zeit["optionen"], "zeitfrage.optionen"));
        if (zeitfrage.Optionen.Count < 2)
        {
            throw new EchtLadefehler("die Zeitfrage braucht mindestens zwei Antworten");
        }
        return new EchtSatz(kennung, fachrichtung, bereich, firma, minuten, ausgang, aufgaben, anlagen, zeitfrage);
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

    private static EchtAufgabe Aufgabe(JsonObject a, int nummer, List<EchtAnlage> anlagen, int format)
    {
        var wo = string.Create(CultureInfo.InvariantCulture, $"Aufgabe {nummer}");
        var titel = Pflicht(a, "titel");
        var punkte = Ganz(a["punkte"]) ?? throw new EchtLadefehler(wo + ": \"punkte\" fehlt");
        if (punkte is < 24 or > 26)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: {punkte} Punkte, erlaubt sind 24 bis 26"));
        }
        var teile = Liste(a["teile"], wo + " teile").Select(t => Teil(t, nummer, anlagen, format)).ToList();
        if (teile.Count is < 5 or > 10)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: {teile.Count} Teilaufgaben, erlaubt sind 5 bis 10"));
        }
        if (teile.Select(t => t.Nr).Distinct(StringComparer.Ordinal).Count() != teile.Count)
        {
            throw new EchtLadefehler(wo + ": Teilaufgabe doppelt");
        }
        var summe = teile.Sum(t => t.Punkte);
        if (summe != punkte)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: Teilaufgaben ergeben {summe} statt {punkte} Punkte"));
        }
        return new EchtAufgabe(titel, punkte, a["einleitung"] is null ? [] : Zeilen(a["einleitung"], wo + " einleitung"), teile);
    }

    private static EchtTeil Teil(JsonObject t, int aufgabe, List<EchtAnlage> anlagen, int format)
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
        if (punkte is < 2 or > 9)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: {punkte} Punkte, erlaubt sind 2 bis 9"));
        }
        var angaben = Ganz(t["angaben"]) ?? 1;
        OperatorPruefen(wo, op, punkte, angaben);
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
        var teil = new EchtTeil(nr, op, text, punkte, angaben, antwort, verweise, bezug, ersatzText, loesung, hinweise, englisch);
        FormPruefen(wo, teil);
        return teil;
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
                if (!EchtZeichnung.Bausteine.ContainsKey(art))
                {
                    throw new EchtLadefehler(wo + ": Diagramm muss \"uml\" oder \"er\" sein");
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
