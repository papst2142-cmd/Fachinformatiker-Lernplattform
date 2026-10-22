using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Pruefung;

/// <summary>
/// Phase 4, W2: laufende bzw. noch nicht bewertete Pruefung dieses Geraets in einstellungen.json
/// (Schluessel "laufende_pruefung", wie fisi_pruefung load_running, save_running, clear_running,
/// migrate_running). Neu in C# (E6, Weisung P4-6): Schluessel "kennung" = uid der Ergebniszeile,
/// beim Start erzeugt; damit bucht ein wiederholter Abschluss nie doppelt.
/// laden liefert die Einstellungen frisch von der Platte, aendern speichert eine Aenderung
/// (false: nicht gespeichert, die Datei bleibt unveraendert).
/// </summary>
public sealed class LaufendePruefung(Func<JsonObject> laden, Func<Action<JsonObject>, bool> aendern)
{
    public const string Schluessel = "laufende_pruefung";
    public const string KennungSchluessel = "kennung";

    /// <summary>load_running: die Pruefung (an heutige Inhalte angepasst) oder null.</summary>
    public JsonObject? Laden(KlausurInhalte inhalte)
    {
        if (laden()[Schluessel] is not JsonObject gespeichert || Klausur.Art(Json.Text(gespeichert["art"])) is null)
        {
            return null;
        }
        var zustand = gespeichert.DeepClone().AsObject();
        Migrieren(inhalte, zustand);
        return zustand;
    }

    /// <summary>save_running.</summary>
    public bool Speichern(JsonObject zustand)
    {
        var kopie = zustand.DeepClone();
        return aendern(werte => werte[Schluessel] = kopie);
    }

    /// <summary>clear_running.</summary>
    public bool Loeschen() => aendern(werte => werte.Remove(Schluessel));

    /// <summary>Neue Pruefung mit Kennung der Ergebniszeile anlegen und speichern; false: nicht gespeichert.</summary>
    public bool Starten(JsonObject zustand, Func<string>? kennung = null)
    {
        zustand[KennungSchluessel] = (kennung ?? LernstandSchreiber.NeueKennung)();
        return Speichern(zustand);
    }

    // -- migrate_running ----------------------------------------------------------------------

    /// <summary>
    /// migrate_running: WiSo-Fragetexte nach umbenennungen.json, genau eine umformulierte
    /// Antwortmoeglichkeit; Haken offener Aufgaben an geaenderte Kriterien anpassen. Im Zweifel
    /// bleibt die Pruefung, wie sie war (Python: except Exception: pass).
    /// </summary>
    public static JsonObject Migrieren(KlausurInhalte inhalte, JsonObject zustand)
    {
        try
        {
            if (Json.Text(zustand["art"]) == Klausur.Wiso)
            {
                MigriereWiso(inhalte, zustand);
            }
            else
            {
                MigriereAufgaben(inhalte, zustand);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidCastException or IndexOutOfRangeException
            or ArgumentException or InvalidDataException or FormatException or NullReferenceException)
        {
            // wie Python: die Pruefung so lassen (bis dahin geaenderte Teile bleiben geaendert)
        }
        return zustand;
    }

    private static void MigriereWiso(KlausurInhalte inhalte, JsonObject zustand)
    {
        // state.get(...) or {}: ein vorhandenes, nicht leeres Woerterbuch wird an Ort und Stelle geaendert
        var antworten = zustand["antworten"] is JsonObject a && a.Count > 0 ? a : null;
        var optionen = zustand["optionen"] is JsonObject o && o.Count > 0 ? o : null;
        if ((antworten is null && Wahr(zustand["antworten"])) || (optionen is null && Wahr(zustand["optionen"])))
        {
            throw new InvalidDataException("Antworten oder Optionen sind kein Woerterbuch");
        }
        var neueAntworten = antworten is null;
        var neueOptionen = optionen is null;
        antworten ??= [];
        optionen ??= [];
        var fragen = new JsonArray();
        var alte = zustand["fragen"] is JsonArray f ? f.ToList() : [];
        foreach (var knoten in alte)
        {
            var alt = Json.Text(knoten) ?? throw new InvalidDataException("Frage ist kein Text");
            var neu = inhalte.AktuelleFrage(alt)!;
            if (neu != alt && !antworten.ContainsKey(neu) && antworten.ContainsKey(alt))
            {
                var wert = antworten[alt]?.DeepClone();
                antworten.Remove(alt);
                antworten[neu] = wert;
            }
            if (neu != alt && !optionen.ContainsKey(neu) && optionen.ContainsKey(alt))
            {
                var wert = optionen[alt]?.DeepClone();
                optionen.Remove(alt);
                optionen[neu] = wert;
            }
            fragen.Add(neu);
            var frage = inhalte.Quizfrage(neu);
            if (frage is null || optionen[neu] is not JsonArray gespeichert)
            {
                continue;
            }
            var heute = frage.Optionen;
            var weg = gespeichert.Where(t => Json.Text(t) is not { } x || !heute.Contains(x)).ToList();
            var dazu = heute.Where(x => !gespeichert.Any(t => Json.Text(t) == x)).ToList();
            if (weg.Count == 1 && dazu.Count == 1)
            {
                var alterText = weg[0];
                optionen[neu] = new JsonArray([.. gespeichert.Select(t => JsonNode.DeepEquals(t, alterText)
                    ? JsonValue.Create(dazu[0]) : t?.DeepClone())]);
                if (JsonNode.DeepEquals(antworten[neu], alterText) && antworten.ContainsKey(neu))
                {
                    antworten[neu] = dazu[0];
                }
            }
        }
        zustand["fragen"] = fragen;
        if (neueAntworten)
        {
            zustand["antworten"] = antworten;
        }
        if (neueOptionen)
        {
            zustand["optionen"] = optionen;
        }
    }

    private static void MigriereAufgaben(KlausurInhalte inhalte, JsonObject zustand)
    {
        if (zustand["kriterien"] is not JsonObject haken)
        {
            return;
        }
        var aufgaben = zustand["aufgaben"] is JsonArray l ? l : [];
        for (var nummer = 0; nummer < aufgaben.Count; nummer++)
        {
            var details = Klausur.Details(inhalte, zustand, nummer);
            if (details is null)
            {
                continue;
            }
            var aufgabe = (JsonObject)aufgaben[nummer]!;
            for (var teil = 0; teil < details.Teile.Count; teil++)
            {
                var kriterien = details.Teile[teil].Kriterien;
                var schluessel = Klausur.Schluessel(nummer, teil);
                if (haken[schluessel] is not JsonArray gesetzt || gesetzt.Count == kriterien.Count)
                {
                    continue;
                }
                var quelle = Json.Text(aufgabe["quelle"]);
                var index = Json.Zahl(aufgabe["index"]);
                if (quelle is not null && index is { } i && KriterienVorher.TryGetValue((quelle, (int)i, teil), out var alt)
                    && alt.Count == gesetzt.Count && i == Math.Floor(i))
                {
                    var behalten = gesetzt.Zip(alt).Where(p => !Bruchstuecke.Contains(p.Second)).ToList();
                    var neu = Ausrichten(behalten.Select(p => Wahr(p.First)).ToList(), behalten.Select(p => p.Second).ToList(), kriterien);
                    haken[schluessel] = new JsonArray([.. neu.Select(h => (JsonNode?)h)]);
                }
                else if (gesetzt.Count < kriterien.Count)
                {
                    for (var n = gesetzt.Count; n < kriterien.Count; n++)
                    {
                        gesetzt.Add(false);
                    }
                }
            }
        }
    }

    /// <summary>bool(x) fuer einen gespeicherten Haken.</summary>
    private static bool Wahr(JsonNode? wert) => Json.Zahl(wert) is { } z ? z != 0 : Json.Text(wert) is { Length: > 0 }
        || (wert is JsonArray a && a.Count > 0) || (wert is JsonObject o && o.Count > 0);

    /// <summary>
    /// CRITERIA_BEFORE (Python v0.63.2 woertlich): Kriterien im Stand 0.63.1 fuer Musterloesungen,
    /// deren Kriterien sich in 0.63.2 geaendert haben (Quelle, Index, Teilaufgabe).
    /// </summary>
    public static readonly IReadOnlyDictionary<(string Quelle, int Index, int Teil), IReadOnlyList<string>> KriterienVorher =
        new Dictionary<(string, int, int), IReadOnlyList<string>>
        {
            [("ap1", 4, 2)] =
            [
                "Damit ein lokales Ereignis wie Brand, Diebstahl oder ein Wasserschaden nicht "
                + "gleichzeitig Originaldaten und alle Sicherungskopien vernichtet (Prinzip des Air Gap).",
            ],
            [("ap1", 58, 2)] =
            [
                "Nach Arbeitsende gilt eine ununterbrochene Ruhezeit von mindestens 11 Stunden, also "
                + "frühestens am nächsten Tag um 7:00 Uhr.",
            ],
            [("ap2", 134, 0)] =
            [
                "Erste Meldung: 24 Stunden nach Kenntnis, also bis Samstag, 03.10.2026, 22:40 Uhr (dass der 3.",
                "Oktober ein Feiertag ist, spielt keine Rolle).",
                "Meldung: 72 Stunden nach Kenntnis, bis Montag, 05.10.2026, 22:40 Uhr.",
                "Die Meldung um 15:00 Uhr ist rechtzeitig.",
                "Abschlussmeldung: einen Monat nach der Meldung, also bis 05.11.2026.",
            ],
            [("ap1", 24, 1)] =
            [
                "Z.B.",
                "\"Standard-Image freigegeben\" und \"Alle 40 Arbeitsplätze umgestellt\".",
                "Meilensteine haben keine Dauer, sie markieren einen wichtigen Zwischenstand.",
            ],
            [("ap1", 38, 2)] =
            [
                "Z.B.",
                "TCP 3389 (RDP), TCP 445 (SMB), TCP 22 (SSH ohne Einschränkung), Datenbankports wie 3306.",
                "Fernzugriff gehört hinter ein VPN.",
            ],
            [("ap2", 35, 0)] =
            [
                "Z.B.",
                "\"Bei welchen Tätigkeiten merken Sie die Langsamkeit besonders?\", \"Mit welchen "
                + "Programmen arbeiten Sie täglich?\", \"Wie viele Mitarbeiter arbeiten an den Geräten?\", "
                + "\"Welche Pläne haben Sie für die nächsten Jahre?\"",
            ],
        };

    /// <summary>_SPLIT_FRAGMENTS: abgetrennte Stuecke ohne eigene Aussage, ihr Haken faellt weg.</summary>
    public static readonly IReadOnlyList<string> Bruchstuecke = ["Z.B."];

    /// <summary>
    /// align_marks: Haken (zu den Kriterien alt) auf die Kriterien neu uebertragen, mit den
    /// Bloecken von difflib.SequenceMatcher(None, alt, neu, autojunk=False).get_opcodes().
    /// </summary>
    public static List<bool> Ausrichten(IReadOnlyList<bool> haken, IReadOnlyList<string> alt, IReadOnlyList<string> neu)
    {
        var ergebnis = new List<bool>();
        foreach (var (art, i1, i2, j1, j2) in Abschnitte(alt, neu))
        {
            if (art == "insert")
            {
                ergebnis.AddRange(Enumerable.Repeat(false, j2 - j1));
            }
            else if (art is "equal" or "replace")
            {
                var breite = j2 - j1;
                var block = haken.Skip(i1).Take(Math.Max(0, Math.Min(i2, haken.Count) - i1)).Take(breite).ToList();
                ergebnis.AddRange(block);
                ergebnis.AddRange(Enumerable.Repeat(false, breite - block.Count));
            }
        }
        return ergebnis;
    }

    /// <summary>difflib.SequenceMatcher(None, a, b, autojunk=False).get_opcodes() (Python 3.12).</summary>
    public static List<(string Art, int I1, int I2, int J1, int J2)> Abschnitte(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var b2j = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var j = 0; j < b.Count; j++)
        {
            if (!b2j.TryGetValue(b[j], out var stellen))
            {
                b2j[b[j]] = stellen = [];
            }
            stellen.Add(j);
        }
        (int I, int J, int K) Laengster(int alo, int ahi, int blo, int bhi)
        {
            var (besti, bestj, bestsize) = (alo, blo, 0);
            var j2len = new Dictionary<int, int>();
            for (var i = alo; i < ahi; i++)
            {
                var neu = new Dictionary<int, int>();
                if (b2j.TryGetValue(a[i], out var stellen))
                {
                    foreach (var j in stellen)
                    {
                        if (j < blo)
                        {
                            continue;
                        }
                        if (j >= bhi)
                        {
                            break;
                        }
                        var k = neu[j] = j2len.GetValueOrDefault(j - 1) + 1;
                        if (k > bestsize)
                        {
                            (besti, bestj, bestsize) = (i - k + 1, j - k + 1, k);
                        }
                    }
                }
                j2len = neu;
            }
            while (besti > alo && bestj > blo && a[besti - 1] == b[bestj - 1])
            {
                (besti, bestj, bestsize) = (besti - 1, bestj - 1, bestsize + 1);
            }
            while (besti + bestsize < ahi && bestj + bestsize < bhi && a[besti + bestsize] == b[bestj + bestsize])
            {
                bestsize++;
            }
            return (besti, bestj, bestsize);
        }
        var bloecke = new List<(int I, int J, int K)>();
        var offen = new Stack<(int, int, int, int)>();
        offen.Push((0, a.Count, 0, b.Count));
        while (offen.Count > 0)
        {
            var (alo, ahi, blo, bhi) = offen.Pop();
            var (i, j, k) = Laengster(alo, ahi, blo, bhi);
            if (k > 0)
            {
                bloecke.Add((i, j, k));
                if (alo < i && blo < j)
                {
                    offen.Push((alo, i, blo, j));
                }
                if (i + k < ahi && j + k < bhi)
                {
                    offen.Push((i + k, ahi, j + k, bhi));
                }
            }
        }
        bloecke.Sort();
        var zusammen = new List<(int I, int J, int K)>();
        var (i1, j1, k1) = (0, 0, 0);
        foreach (var (i2, j2, k2) in bloecke)
        {
            if (i1 + k1 == i2 && j1 + k1 == j2)
            {
                k1 += k2;
            }
            else
            {
                if (k1 > 0)
                {
                    zusammen.Add((i1, j1, k1));
                }
                (i1, j1, k1) = (i2, j2, k2);
            }
        }
        if (k1 > 0)
        {
            zusammen.Add((i1, j1, k1));
        }
        zusammen.Add((a.Count, b.Count, 0));
        var abschnitte = new List<(string, int, int, int, int)>();
        var (x, y) = (0, 0);
        foreach (var (ai, bj, groesse) in zusammen)
        {
            var art = x < ai && y < bj ? "replace" : x < ai ? "delete" : y < bj ? "insert" : "";
            if (art.Length > 0)
            {
                abschnitte.Add((art, x, ai, y, bj));
            }
            (x, y) = (ai + groesse, bj + groesse);
            if (groesse > 0)
            {
                abschnitte.Add(("equal", ai, x, bj, y));
            }
        }
        return abschnitte;
    }
}
