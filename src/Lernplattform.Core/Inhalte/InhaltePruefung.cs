using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Inhalte;

/// <summary>
/// Formale Pruefung aller Lerninhalte wie fisi_core.validate_content und
/// fisi_rahmenplan.validate_rahmenplan (Vergleichswerte G12). Liefert die
/// Meldungen in derselben Reihenfolge und mit demselben Wortlaut wie Python;
/// leere Liste = alles in Ordnung.
/// Abweichung (Plan Phase 1): die Pruefung der Spielinhalte
/// (fisi_game.validate_game_content) gehoert zum Spiel und kommt mit dem
/// Spiel-Kern. Bei fehlerhaften Stichworten der Rahmenplan-Zuordnung nennt
/// die Meldung den Fehlertext von .NET statt den von Python.
/// </summary>
public static partial class InhaltePruefung
{
    /// <summary>Bereiche der AP2 im Rahmenplan (fisi_rahmenplan.AP2_AREAS).</summary>
    public static readonly IReadOnlyList<string> Ap2Bereiche = ["Konzeption", "Netzwerke", "Projekt", "WiSo"];

    private static readonly string[] KartenFelder = ["cat", "q", "a", "a_full", "options"];
    private static readonly string[] QuizFelder = ["cat", "q", "a", "exp", "options"];
    private static readonly string[] SzenarioFelder = ["cat", "title", "theme", "text", "solution"];
    private static readonly string[] ProjektFelder =
        ["cat", "title", "branche", "schwierigkeit", "ausgangssituation", "auftrag", "rahmenbedingungen", "aufgaben", "hinweise"];

    public static List<string> Pruefen(Lerninhalte inhalte)
    {
        var meldungen = new List<string>();
        Pruefe(meldungen, inhalte.Karteikarten, "Karteikarte", KartenFelder, true);
        Pruefe(meldungen, inhalte.Quizfragen, "Quizfrage", QuizFelder, true);
        Pruefe(meldungen, inhalte.Ap2Szenarien, "AP2-Szenario", SzenarioFelder, false);
        Pruefe(meldungen, inhalte.Ap1Szenarien, "AP1-Szenario", SzenarioFelder, false);
        Pruefe(meldungen, inhalte.Testprojekte, "Testprojekt", ProjektFelder, false);

        foreach (var (name, liste) in Listen(inhalte))
        {
            foreach (var eintrag in liste)
            {
                var thema = eintrag.Daten["thema"];
                var passend = eintrag.Fachbereich is { } fb && inhalte.Themen.TryGetValue(fb, out var themen)
                    && thema is JsonValue t && t.GetValueKind() == JsonValueKind.String
                    && themen.Any(x => x.Id == t.GetValue<string>());
                if (!passend)
                {
                    meldungen.Add($"{name} Nr. {eintrag.Nummer}: Thema '{PythonText(thema)}' fehlt oder gehoert nicht zum "
                        + "Fachbereich (inhalte/themen.json)");
                }
            }
        }

        foreach (var szenario in inhalte.Ap2Szenarien)
        {
            if (!InBloecken(szenario, Fachbereiche.Ap2Themen))
            {
                meldungen.Add($"AP2-Szenario Nr. {szenario.Nummer}: unbekanntes Thema '{PythonText(szenario.Daten["theme"])}' "
                    + "(AP2_THEMES oder THEME_BLOCK ergaenzen)");
            }
        }
        foreach (var szenario in inhalte.Ap1Szenarien)
        {
            if (!InBloecken(szenario, Fachbereiche.Ap1Themen))
            {
                meldungen.Add($"AP1-Szenario Nr. {szenario.Nummer}: unbekanntes Thema '{PythonText(szenario.Daten["theme"])}'");
            }
        }
        foreach (var projekt in inhalte.Testprojekte)
        {
            if (Laenge(projekt.Daten["aufgaben"]) != Laenge(projekt.Daten["hinweise"]))
            {
                meldungen.Add($"Testprojekt Nr. {projekt.Nummer}: je Aufgabe wird genau ein Hinweis gebraucht");
            }
        }

        meldungen.AddRange(PruefeRahmenplan(inhalte));
        return meldungen;
    }

    /// <summary>Schluessel fuer Dubletten: ohne Leerraum aussen, klein geschrieben (Python strip().lower()).</summary>
    public static string Schluessel(string text) => text.Trim().ToLowerInvariant();

    private static void Pruefe(List<string> meldungen, IReadOnlyList<Lerninhalt> liste, string name, string[] felder, bool auswahl)
    {
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var eintrag in liste)
        {
            var wo = $"{name} Nr. {eintrag.Nummer}";
            foreach (var feld in felder)
            {
                if (!Einstellungen.PythonBool(eintrag.Daten[feld]))
                {
                    meldungen.Add($"{wo}: Feld '{feld}' fehlt oder ist leer");
                }
            }
            var schluessel = Schluessel(ErsterText(eintrag.Daten["q"]) ?? ErsterText(eintrag.Daten["title"]) ?? "");
            if (!gesehen.Add(schluessel))
            {
                meldungen.Add($"{wo}: doppelt vorhanden ({Anfang(schluessel, 60)})");
            }
            if (auswahl)
            {
                var optionen = eintrag.Daten["options"] is JsonArray a && a.Count > 0 ? a.ToList() : [];
                var verschieden = optionen.Select(o => o?.ToJsonString() ?? "null").Distinct(StringComparer.Ordinal).Count();
                if (optionen.Count != 4 || verschieden != 4)
                {
                    meldungen.Add($"{wo}: braucht genau 4 verschiedene Antworten");
                }
                var antwort = eintrag.Daten["a"];
                if (!optionen.Any(o => JsonNode.DeepEquals(o, antwort)))
                {
                    meldungen.Add($"{wo}: richtige Antwort steht nicht in den Optionen");
                }
            }
        }
    }

    private static List<string> PruefeRahmenplan(Lerninhalte inhalte)
    {
        var meldungen = new List<string>();
        var plan = inhalte.Rahmenplan;
        var abschnitte = Objekte(plan["abschnitte"]).Select(a => PythonText(a["id"])).ToHashSet(StringComparer.Ordinal);
        var lernfelder = Objekte(plan["lernfelder"]).Select(l => PythonText(l["id"])).ToHashSet(StringComparer.Ordinal);
        var punkte = Objekte(plan["punkte"]).ToList();
        var punktIds = punkte.Select(p => PythonText(p["id"])).ToHashSet(StringComparer.Ordinal);

        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var punkt in punkte)
        {
            var id = PythonText(punkt["id"]);
            if (!gesehen.Add(id))
            {
                meldungen.Add($"Rahmenplan: Punkt {id} doppelt");
            }
            if (!abschnitte.Contains(PythonText(punkt["abschnitt"])))
            {
                meldungen.Add($"Rahmenplan {id}: unbekannter Abschnitt");
            }
            foreach (var feld in Texte(punkt["lernfelder"]))
            {
                if (!lernfelder.Contains(feld))
                {
                    meldungen.Add($"Rahmenplan {id}: unbekanntes Lernfeld {feld}");
                }
            }
            foreach (var bereich in Texte(punkt["ap2"]))
            {
                if (!Ap2Bereiche.Contains(bereich))
                {
                    meldungen.Add($"Rahmenplan {id}: unbekannter Prüfungsbereich {bereich}");
                }
            }
        }

        var zuordnung = inhalte.RahmenplanZuordnung;
        foreach (var regel in Objekte(zuordnung["punkte"]))
        {
            var id = PythonText(regel["id"]);
            if (!punktIds.Contains(id))
            {
                meldungen.Add($"Rahmenplan-Zuordnung: unbekannter Punkt {id}");
            }
            foreach (var thema in Texte(regel["themen"]))
            {
                if (!inhalte.ThemaNach.ContainsKey(thema))
                {
                    meldungen.Add($"Rahmenplan-Zuordnung {id}: unbekanntes Thema {thema}");
                }
            }
            try
            {
                _ = new Regex(PythonText(regel["stichworte"]), RegexOptions.CultureInvariant);
            }
            catch (ArgumentException e)
            {
                meldungen.Add($"Rahmenplan-Zuordnung {id}: Stichworte fehlerhaft ({e.Message})");
            }
        }
        var standard = zuordnung["standard_punkt"] as JsonObject ?? [];
        var themenLernfelder = zuordnung["themen_lernfelder"] as JsonObject ?? [];
        foreach (var thema in inhalte.ThemenReihenfolge)
        {
            if (!standard.ContainsKey(thema) || !punktIds.Contains(PythonText(standard[thema])))
            {
                meldungen.Add($"Rahmenplan-Zuordnung: Standard-Punkt fuer Thema {thema} fehlt");
            }
            foreach (var feld in Texte(themenLernfelder[thema]))
            {
                if (!lernfelder.Contains(feld))
                {
                    meldungen.Add($"Rahmenplan-Zuordnung: Thema {thema} mit unbekanntem Lernfeld {feld}");
                }
            }
        }

        foreach (var (name, liste) in new[] { ("Karteikarte", inhalte.Karteikarten), ("Quizfrage", inhalte.Quizfragen) })
        {
            foreach (var eintrag in liste)
            {
                foreach (var punkt in Texte(eintrag.Daten["rahmenplan"]))
                {
                    if (!punktIds.Contains(punkt))
                    {
                        meldungen.Add($"{name} Nr. {eintrag.Nummer}: unbekannter Rahmenplan-Punkt {punkt}");
                    }
                }
            }
        }
        return meldungen;
    }

    private static IEnumerable<(string, IReadOnlyList<Lerninhalt>)> Listen(Lerninhalte inhalte) =>
    [
        ("Karteikarte", inhalte.Karteikarten), ("Quizfrage", inhalte.Quizfragen),
        ("AP2-Szenario", inhalte.Ap2Szenarien), ("AP1-Szenario", inhalte.Ap1Szenarien),
        ("Testprojekt", inhalte.Testprojekte),
    ];

    private static bool InBloecken(Lerninhalt szenario, IReadOnlyList<string> bloecke) =>
        szenario.Daten["theme"] is JsonValue v && v.GetValueKind() == JsonValueKind.String
        && bloecke.Contains(Fachbereiche.Block(v.GetValue<string>())!);

    /// <summary>Python "a or b": erster Text, der nicht leer ist.</summary>
    private static string? ErsterText(JsonNode? wert) =>
        wert is JsonValue v && v.GetValueKind() == JsonValueKind.String && v.GetValue<string>().Length > 0 ? v.GetValue<string>() : null;

    /// <summary>Die ersten n Zeichen (Unicode-Zeichen wie in Python, nicht UTF-16-Einheiten).</summary>
    private static string Anfang(string text, int n)
    {
        var ergebnis = new StringBuilder();
        foreach (var zeichen in text.EnumerateRunes().Take(n))
        {
            ergebnis.Append(zeichen.ToString());
        }
        return ergebnis.ToString();
    }

    /// <summary>Wert wie Pythons "%s" (fehlend = None).</summary>
    private static string PythonText(JsonNode? wert) =>
        wert is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>()
        : wert is JsonValue z && z.GetValueKind() == JsonValueKind.Number ? z.ToJsonString()
        : wert is JsonValue b && b.GetValueKind() is JsonValueKind.True or JsonValueKind.False
            ? (b.GetValueKind() == JsonValueKind.True ? "True" : "False")
        : wert?.ToJsonString() ?? "None";

    private static IEnumerable<JsonObject> Objekte(JsonNode? wert) =>
        wert is JsonArray a ? a.OfType<JsonObject>() : [];

    private static IEnumerable<string> Texte(JsonNode? wert) =>
        wert is JsonArray a ? a.Select(PythonText) : [];

    private static int Laenge(JsonNode? wert) => wert switch
    {
        JsonArray a => a.Count,
        JsonObject o => o.Count,
        JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>().Length,
        _ => 0,
    };
}
