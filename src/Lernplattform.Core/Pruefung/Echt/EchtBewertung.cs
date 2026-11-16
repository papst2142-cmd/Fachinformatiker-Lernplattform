using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Suche;

namespace Lernplattform.Core.Pruefung.Echt;

/// <summary>Bewertung einer Teilaufgabe: erreicht, hoechstens, davon vom Programm, und je Feld richtig/falsch.</summary>
public sealed record EchtTeilwertung(int Erreicht, int Hoechst, int Programm, IReadOnlyList<bool> Felder);

/// <summary>Ergebnis einer Pruefung im echten Format (ganze Punkte 0 bis 100, Note nach IHK-Schluessel).</summary>
public sealed record EchtErgebnis(int Punkte, string Note, long Dauer, IReadOnlyList<(string Titel, int Erreicht, int Hoechst)> Aufgaben,
    JsonObject Daten);

/// <summary>
/// Welle 1 "Pruefung im echten Format": Schluessel der Antworten, Vergleich der Felder, die das Programm selbst
/// bewertet (Rechenergebnis, Lueckenfelder, Tabellenzellen, Auswahl), Selbstbewertung mit Haekchen und das
/// Ergebnis. Die Note ist <see cref="Trainer.Note"/> (IHK-Schluessel 92/81/67/50/30), keine neue Rechnung.
/// </summary>
public static class EchtBewertung
{
    /// <summary>Schluessel einer Teilaufgabe im Zustand: "2.ba" (Aufgabe ab 1, Nummer).</summary>
    public static string Schluessel(int aufgabe, string nr) => string.Create(CultureInfo.InvariantCulture, $"{aufgabe}.{nr}");

    /// <summary>Schluessel eines Felds einer Teilaufgabe: "2.ba/weg", "2.ba/1", "2.ba/z0s1".</summary>
    public static string Feld(int aufgabe, string nr, string feld) => Schluessel(aufgabe, nr) + "/" + feld;

    public const string Weg = "weg";
    public const string Ergebnis = "ergebnis";
    public const string Wahl = "wahl";
    public const string Text = "text";

    public static string Angabe(int nummer) => nummer.ToString(CultureInfo.InvariantCulture);

    public static string Zelle(int zeile, int spalte) => string.Create(CultureInfo.InvariantCulture, $"z{zeile}s{spalte}");

    public static string Luecke(int feld) => string.Create(CultureInfo.InvariantCulture, $"f{feld}");

    /// <summary>Text einer Antwort (leer, wenn nichts eingetragen).</summary>
    public static string Antwort(JsonObject zustand, string schluessel) =>
        (zustand["antworten"] as JsonObject)?[schluessel] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

    // -- Vergleich ----------------------------------------------------------------------------

    /// <summary>
    /// Ein Wert in genormter Form zum Vergleich, null = passt nicht zur Art. ip: vier Zahlen 0 bis 255;
    /// maske: wie ip oder "/27" (wird zur Punktschreibweise); zahl: Komma oder Punkt als Dezimalzeichen;
    /// text: klein, Leerraum zusammengefasst.
    /// </summary>
    public static string? Normal(string art, string? wert)
    {
        var roh = (wert ?? "").Trim();
        switch (art)
        {
            case "ip":
                return Ip(roh);
            case "maske":
                if (roh.StartsWith('/') && int.TryParse(roh.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var laenge)
                    && laenge is >= 0 and <= 32)
                {
                    var bits = laenge == 0 ? 0u : uint.MaxValue << (32 - laenge);
                    return string.Create(CultureInfo.InvariantCulture, $"{bits >> 24}.{(bits >> 16) & 255}.{(bits >> 8) & 255}.{bits & 255}");
                }
                return Ip(roh) is { } maske && IstMaske(maske) ? maske : null;
            case "zahl":
                return Dezimal(roh) is { } zahl ? zahl.ToString("R", CultureInfo.InvariantCulture) : null;
            default:
                return roh.Length == 0 ? null : TextNormal(roh);
        }
    }

    private static string? Ip(string roh)
    {
        var teile = roh.Replace(" ", "", StringComparison.Ordinal).Split('.');
        if (teile.Length != 4)
        {
            return null;
        }
        var zahlen = new List<int>();
        foreach (var teil in teile)
        {
            if (teil.Length is 0 or > 3 || !int.TryParse(teil, NumberStyles.None, CultureInfo.InvariantCulture, out var z) || z > 255)
            {
                return null;
            }
            zahlen.Add(z);
        }
        return string.Join('.', zahlen.Select(z => z.ToString(CultureInfo.InvariantCulture)));
    }

    private static bool IstMaske(string maske)
    {
        var bits = maske.Split('.').Select(t => uint.Parse(t, CultureInfo.InvariantCulture)).Aggregate(0u, (a, b) => (a << 8) | b);
        var umgekehrt = ~bits;
        return (umgekehrt & (umgekehrt + 1)) == 0;    // nur Einsen links, dann nur Nullen
    }

    private static string OhneLeerraum(string? eingabe) => (eingabe ?? "").Trim().Replace(" ", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);

    private static readonly AsyncLocal<bool> TausenderAlt = new();

    /// <summary>Gegenprobe (nur Tests): Zahlenfelder wie bis A 0.81.1, der Punkt ist dort immer ein Dezimalzeichen.</summary>
    public static void TausenderAltFuerTests(bool an) => TausenderAlt.Value = an;

    /// <summary>
    /// Sammelliste 346 (A 0.81.2): Zahl aus der Eingabe eines Zahlenfelds, dessen Musterloesung <paramref name="loesung"/>
    /// ist. Ab einer Musterloesung von 1000 gilt ein Punkt als Tausenderpunkt, wenn danach genau Dreiergruppen folgen
    /// ("1.340", "20.508", "1.340.000"; gleiche Regel wie im Rechen-Trainer, <see cref="Zahlen.PasstGruppen"/>).
    /// Alles andere liest <see cref="Dezimal"/> wie bisher ("1.34", "1.3400", "1.340,00", unter 1000 auch "1.340").
    /// </summary>
    public static double? Tausenderpunkt(string? eingabe, double loesung)
    {
        var roh = OhneLeerraum(eingabe);
        if (!TausenderAlt.Value && loesung >= 1000 && Zahlen.PasstGruppen(roh))
        {
            return double.Parse(roh.Replace(".", "", StringComparison.Ordinal), NumberStyles.None, CultureInfo.InvariantCulture);
        }
        return Dezimal(eingabe);
    }

    /// <summary>Zahl aus einer Eingabe: Komma oder Punkt als Dezimalzeichen; mit beiden ist der Punkt ein Tausenderpunkt.</summary>
    public static double? Dezimal(string? eingabe)
    {
        var roh = OhneLeerraum(eingabe);
        if (roh.Contains(',', StringComparison.Ordinal) && roh.Contains('.', StringComparison.Ordinal))
        {
            roh = roh.Replace(".", "", StringComparison.Ordinal);
        }
        roh = roh.Replace(',', '.');
        return double.TryParse(roh, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var zahl)
            && double.IsFinite(zahl) ? zahl : null;
    }

    private static string TextNormal(string roh)
    {
        var text = new StringBuilder();
        var leer = false;
        foreach (var zeichen in PythonKlein.Klein(roh.Trim()))
        {
            if (char.IsWhiteSpace(zeichen))
            {
                leer = true;
                continue;
            }
            if (leer && text.Length > 0)
            {
                text.Append(' ');
            }
            leer = false;
            text.Append(zeichen);
        }
        return text.ToString();
    }

    /// <summary>
    /// Passt die Eingabe zu einer der erlaubten Loesungen? Zahlenfelder lesen die Eingabe je Loesung ueber
    /// <see cref="Tausenderpunkt"/> (Sammelliste 346).
    /// </summary>
    public static bool Passt(string art, string? eingabe, IEnumerable<string> loesungen)
    {
        if (art == "zahl")
        {
            return loesungen.Any(l => Dezimal(l) is { } ziel && Tausenderpunkt(eingabe, ziel) is { } zahl
                && ZahlText(zahl) == ZahlText(ziel));
        }
        return Normal(art, eingabe) is { } wert && loesungen.Any(l => Normal(art, l) == wert);
    }

    private static string ZahlText(double zahl) => zahl.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// Rechenergebnis richtig: nahe der Loesung oder (bei Folgerechnung) nahe dem Ergebnis mit Ersatzwert; die Eingabe
    /// liest je Wert <see cref="Tausenderpunkt"/> (Sammelliste 346).
    /// </summary>
    public static bool ErgebnisRichtig(EchtAntwort antwort, string? eingabe)
    {
        var grenze = antwort.Toleranz + 1e-9;
        bool Nahe(double ziel) => Tausenderpunkt(eingabe, ziel) is { } zahl && Math.Abs(zahl - ziel) <= grenze;
        return Nahe(antwort.Ergebnis) || (antwort.ErsatzErgebnis is { } ersatz && Nahe(ersatz));
    }

    // -- Bewertung je Teil --------------------------------------------------------------------

    /// <summary>Haekchen der Selbstbewertung einer Teilaufgabe (fehlende gelten als nicht gesetzt).</summary>
    public static List<bool> Haken(JsonObject zustand, string schluessel) =>
        (zustand["haken"] as JsonObject)?[schluessel] is JsonArray liste
            ? liste.Select(h => h is JsonValue v && v.TryGetValue<bool>(out var b) && b).ToList() : [];

    /// <summary>Zahl der Haekchen einer Teilaufgabe: Hinweise, bei "angaben" je Feld eines.</summary>
    public static int HakenAnzahl(EchtTeil teil) => teil.Antwort.Form == EchtSaetze.FormAngaben ? teil.Angaben : teil.Hinweise.Count;

    /// <summary>Punkte eines Haekchens.</summary>
    public static int HakenPunkte(EchtTeil teil, int index) =>
        teil.Antwort.Form == EchtSaetze.FormAngaben ? teil.Punkte / teil.Angaben : teil.Hinweise[index].Punkte;

    /// <summary>Teilaufgabe bewerten: Programmteil aus den Antworten, Selbstteil aus den Haekchen.</summary>
    public static EchtTeilwertung Teil(JsonObject zustand, int aufgabe, EchtTeil teil)
    {
        var a = teil.Antwort;
        var felder = new List<bool>();
        var programm = 0;
        switch (a.Form)
        {
            case EchtSaetze.FormRechnen:
                var richtig = ErgebnisRichtig(a, Antwort(zustand, Feld(aufgabe, teil.Nr, Ergebnis)));
                felder.Add(richtig);
                programm = richtig ? a.PunkteErgebnis : 0;
                break;
            case EchtSaetze.FormTabelle:
                for (var z = 0; z < a.Tabelle.Count; z++)
                {
                    for (var s = 0; s < a.Tabelle[z].Count; s++)
                    {
                        if (a.Tabelle[z][s] is { Offen: true } zelle)
                        {
                            var ok = Passt("text", Antwort(zustand, Feld(aufgabe, teil.Nr, Zelle(z, s))), zelle.Loesung);
                            felder.Add(ok);
                            programm += ok ? zelle.Punkte : 0;
                        }
                    }
                }
                break;
            case EchtSaetze.FormLuecke:
                for (var f = 0; f < a.Felder.Count; f++)
                {
                    var ok = Passt(a.Felder[f].Art, Antwort(zustand, Feld(aufgabe, teil.Nr, Luecke(f))), a.Felder[f].Loesung);
                    felder.Add(ok);
                    programm += ok ? a.Felder[f].Punkte : 0;
                }
                break;
            case EchtSaetze.FormAuswahl:
                var gewaehlt = Gewaehlt(zustand, aufgabe, teil.Nr);
                var ganz = gewaehlt.Order().SequenceEqual(a.Richtig.Order());
                felder.Add(ganz);
                programm = ganz ? teil.Punkte : 0;
                break;
        }
        var haken = Haken(zustand, Schluessel(aufgabe, teil.Nr));
        var selbst = 0;
        for (var i = 0; i < HakenAnzahl(teil); i++)
        {
            if (i < haken.Count && haken[i])
            {
                selbst += HakenPunkte(teil, i);
            }
        }
        selbst = Math.Min(selbst, teil.SelbstPunkte);
        return new EchtTeilwertung(programm + selbst, teil.Punkte, programm, felder);
    }

    /// <summary>Gewaehlte Optionen einer Auswahl ("0,2").</summary>
    public static List<int> Gewaehlt(JsonObject zustand, int aufgabe, string nr) => Antwort(zustand, Feld(aufgabe, nr, Wahl))
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(t => int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var i) ? i : -1)
        .Where(i => i >= 0).Distinct().ToList();

    /// <summary>Hat man in einer Teilaufgabe etwas eingetragen (nicht nur Leerraum)?</summary>
    public static bool Bearbeitet(JsonObject zustand, int aufgabe, string nr)
    {
        var praefix = Schluessel(aufgabe, nr) + "/";
        return (zustand["antworten"] as JsonObject)?.Any(p => p.Key.StartsWith(praefix, StringComparison.Ordinal)
            && p.Value is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim().Length > 0) == true;
    }

    // -- Ergebnis -----------------------------------------------------------------------------

    /// <summary>
    /// Ergebnis der ganzen Pruefung und die Daten fuer die Zeile in "pruefungen": Satz, Antworten, Haekchen,
    /// Punkte je Teil, Zeitfrage, "themen" je Aufgabe (fuer "Schwaechen" im Fortschritt). Kein Schluessel
    /// "aufgaben" (der gehoert der heutigen Klausur).
    /// </summary>
    public static EchtErgebnis Auswerten(EchtSatz satz, JsonObject zustand)
    {
        var punkteJeTeil = new JsonObject();
        var themen = new JsonObject();
        var aufgaben = new List<(string, int, int)>();
        var gesamt = 0;
        for (var n = 0; n < satz.Aufgaben.Count; n++)
        {
            var aufgabe = satz.Aufgaben[n];
            var erreicht = 0;
            foreach (var teil in aufgabe.Teile)
            {
                var wertung = Teil(zustand, n + 1, teil);
                punkteJeTeil[Schluessel(n + 1, teil.Nr)] = wertung.Erreicht;
                erreicht += wertung.Erreicht;
            }
            gesamt += erreicht;
            aufgaben.Add((aufgabe.Titel, erreicht, aufgabe.Punkte));
            themen[aufgabe.Titel] = new JsonArray(erreicht, aufgabe.Punkte);
        }
        var dauer = (long)(Json.Zahl(zustand["dauer"]) ?? 0);
        var daten = new JsonObject
        {
            ["format"] = "echt",
            ["satz"] = satz.Kennung,
            ["firma"] = satz.Firma,
            ["punkte_je_teil"] = punkteJeTeil,
            ["themen"] = themen,
            ["antworten"] = zustand["antworten"]?.DeepClone() ?? new JsonObject(),
            ["haken"] = zustand["haken"]?.DeepClone() ?? new JsonObject(),
            ["zeitfrage"] = zustand["zeitfrage"]?.DeepClone(),
        };
        return new EchtErgebnis(gesamt, Trainer.Note(gesamt), dauer, aufgaben, daten);
    }
}
