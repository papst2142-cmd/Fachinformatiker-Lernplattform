using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Suche;

namespace Lernplattform.Core.Inhalte;

/// <summary>
/// Phase 9 W2 (Plan Phase 9 Abschnitt 5): Ladepruefung der neuen Inhalte in inhalte_csharp/lernen
/// wie fisi_core.validate_content (Pflichtfelder, vier verschiedene Antworten, Thema im Fachbereich,
/// Hinweis je Aufgabe), dazu das Pflichtfeld "fachrichtung", eindeutige Fragetexte und Titel ueber
/// beide Ordner und gegen die alten Texte in umbenennungen.json, die Themen der Pruefungsbereiche und
/// der Rahmenplan-Zusatz. Unabhaengige Gegenkontrolle: werkzeuge/vergleich/pruefe_lerninhalte.py
/// (Vergleichswerte G52). Leere Liste = alles in Ordnung.
/// </summary>
public static partial class InhaltePruefung
{
    /// <summary>Abschnitt des Rahmenplans je Fachrichtung (FIAusbV Anlage: B AE, C SI, D DPA, E DV).</summary>
    public static readonly IReadOnlyDictionary<string, string> AbschnittJe = new Dictionary<string, string>
    {
        ["si"] = "C", ["ae"] = "B", ["dpa"] = "D", ["dv"] = "E",
    };

    private static readonly (InhaltArt Art, string Datei, string[] Felder, bool Auswahl, string? Tabelle)[] NeueListen =
    [
        (InhaltArt.Karteikarte, "karteikarten", KartenFelder, true, "card_events"),
        (InhaltArt.Quizfrage, "quizfragen", QuizFelder, true, "quiz_answers"),
        (InhaltArt.Ap2Szenario, "szenarien_ap2", ["cat", "title", "theme", "text", "solution"], false, null),
        (InhaltArt.Testprojekt, "projektarbeiten", ProjektFelder, false, null),
    ];

    /// <param name="alle">Gesamtinhalte aus <see cref="Lerninhalte.MitLernen"/>.</param>
    /// <param name="umbenennungen">inhalte/umbenennungen.json (Tabelle, alter Text, neuer Text).</param>
    public static List<string> PruefenLernen(Lerninhalte alle, IReadOnlyList<(string Tabelle, string Alt, string Neu)> umbenennungen)
    {
        var meldungen = new List<string>();
        var bisher = alle.Bisher;
        foreach (var (art, datei, felder, auswahl, tabelle) in NeueListen)
        {
            var alteListe = art switch
            {
                InhaltArt.Karteikarte => bisher.Karteikarten,
                InhaltArt.Quizfrage => bisher.Quizfragen,
                InhaltArt.Ap2Szenario => bisher.Ap2Szenarien,
                _ => bisher.Testprojekte,
            };
            var gesehen = alteListe.Select(e => DoppelSchluessel(e.Daten)).ToHashSet(StringComparer.Ordinal);
            var alteTexte = umbenennungen.Where(u => u.Tabelle == tabelle).Select(u => DoppelText(u.Alt)).ToHashSet(StringComparer.Ordinal);
            var neue = alle.Neue.TryGetValue(art, out var liste) ? liste : [];
            for (var k = 0; k < neue.Count; k++)
            {
                var eintrag = neue[k];
                var wo = $"lernen/{datei}.json Nr. {k + 1}";
                foreach (var feld in felder.Append(Fachrichtungen.Feld))
                {
                    if (!Einstellungen.PythonBool(eintrag.Daten[feld]))
                    {
                        meldungen.Add($"{wo}: Feld '{feld}' fehlt oder ist leer");
                    }
                }
                var fr = eintrag.Daten[Fachrichtungen.Feld];
                if (Einstellungen.PythonBool(fr) && eintrag.Zugehoerigkeit == Lerninhalte.Ungueltig)
                {
                    meldungen.Add($"{wo}: unbekannte Fachrichtung '{PythonText(fr)}'");
                }
                var schluessel = DoppelSchluessel(eintrag.Daten);
                if (!gesehen.Add(schluessel))
                {
                    meldungen.Add($"{wo}: doppelt vorhanden ({Anfang(schluessel, 60)})");
                }
                if (tabelle is not null && alteTexte.Contains(schluessel))
                {
                    meldungen.Add($"{wo}: Fragetext steht als alter Text in umbenennungen.json");
                }
                if (auswahl)
                {
                    var optionen = eintrag.Daten["options"] is JsonArray a && a.Count > 0 ? a.ToList() : [];
                    var verschieden = optionen.Select(o => o?.ToJsonString() ?? "null").Distinct(StringComparer.Ordinal).Count();
                    if (optionen.Count != 4 || verschieden != 4)
                    {
                        meldungen.Add($"{wo}: braucht genau 4 verschiedene Antworten");
                    }
                    if (!optionen.Any(o => JsonNode.DeepEquals(o, eintrag.Daten["a"])))
                    {
                        meldungen.Add($"{wo}: richtige Antwort steht nicht in den Optionen");
                    }
                }
                var thema = eintrag.Daten["thema"];
                var themaText = thema is JsonValue t && t.GetValueKind() == JsonValueKind.String ? t.GetValue<string>() : null;
                var passend = themaText is not null && eintrag.Fachbereich is { } fb && alle.Themen.TryGetValue(fb, out var themen)
                    && themen.Any(x => x.Id == themaText);
                if (!passend)
                {
                    meldungen.Add($"{wo}: Thema '{PythonText(thema)}' fehlt oder gehoert nicht zum Fachbereich");
                }
                else if (alle.NeueThemen.TryGetValue(themaText!, out var themaFr) && themaFr != eintrag.Zugehoerigkeit)
                {
                    meldungen.Add($"{wo}: Thema '{themaText}' gehoert zu einer anderen Fachrichtung");
                }
                if (art == InhaltArt.Ap2Szenario && !Ap2ThemaPasst(alle.RahmenplanZusatz, eintrag))
                {
                    meldungen.Add($"{wo}: unbekanntes Thema '{PythonText(eintrag.Daten["theme"])}' (Themen der Prüfungsbereiche)");
                }
                if (art == InhaltArt.Testprojekt && Laenge(eintrag.Daten["aufgaben"]) != Laenge(eintrag.Daten["hinweise"]))
                {
                    meldungen.Add($"{wo}: je Aufgabe wird genau ein Hinweis gebraucht");
                }
                var punkte = Objekte(alle.Rahmenplan["punkte"]).Select(p => PythonText(p["id"])).ToHashSet(StringComparer.Ordinal);
                foreach (var punkt in Texte(eintrag.Daten["rahmenplan"]))
                {
                    if (!punkte.Contains(punkt))
                    {
                        meldungen.Add($"{wo}: unbekannter Rahmenplan-Punkt {punkt}");
                    }
                }
            }
        }

        var themaGesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in alle.ThemenReihenfolge)
        {
            var neu = alle.NeueThemen.TryGetValue(id, out var fr);
            if (!themaGesehen.Add(id) && neu)
            {
                meldungen.Add($"lernen/themen.json {id}: Thema doppelt");
            }
            if (neu && (fr is null || !Fachrichtungen.Werte.Contains(fr)))
            {
                meldungen.Add($"lernen/themen.json {id}: unbekannte Fachrichtung '{fr ?? "None"}'");
            }
        }

        meldungen.AddRange(PruefeZusatz(alle));
        return meldungen;
    }

    /// <summary>Themen der Pruefungsbereiche b1/b2 einer Fachrichtung aus dem Zusatz (si: die AP2-Bloecke wie Python).</summary>
    public static IReadOnlyList<string> Ap2Themen(JsonObject zusatz, string fachrichtung)
    {
        if (fachrichtung == Fachrichtungen.Systemintegration)
        {
            return Fachbereiche.Ap2Themen;
        }
        var bereiche = zusatz["pruefungsbereiche"]?[fachrichtung] as JsonObject;
        return Pruefung.Fachrichtung.Teil2.Where(r => r is "b1" or "b2")
            .SelectMany(r => Texte(bereiche?[r]?["themen"])).ToList();
    }

    private static bool Ap2ThemaPasst(JsonObject zusatz, Lerninhalt szenario)
    {
        if (szenario.Zugehoerigkeit == Fachrichtungen.Systemintegration)
        {
            return InBloecken(szenario, Fachbereiche.Ap2Themen);
        }
        return szenario.Daten["theme"] is JsonValue v && v.GetValueKind() == JsonValueKind.String
            && Ap2Themen(zusatz, szenario.Zugehoerigkeit).Contains(v.GetValue<string>());
    }

    private static List<string> PruefeZusatz(Lerninhalte alle)
    {
        const string Datei = "rahmenplan_zusatz.json";
        var meldungen = new List<string>();
        var zusatz = alle.RahmenplanZusatz;
        var plan = alle.Rahmenplan;
        var lernfelder = Objekte(plan["lernfelder"]).Select(l => PythonText(l["id"])).ToHashSet(StringComparer.Ordinal);
        foreach (var feld in Objekte(zusatz["lernfelder"]))
        {
            var id = PythonText(feld["id"]);
            var fr = PythonText(feld[Fachrichtungen.Feld]);
            if (!lernfelder.Add(id))
            {
                meldungen.Add($"{Datei}: Lernfeld {id} doppelt");
            }
            if (fr == Fachrichtungen.Systemintegration || !AbschnittJe.TryGetValue(fr, out var abschnitt))
            {
                meldungen.Add($"{Datei}: Lernfeld {id}: unbekannte Fachrichtung {fr}");
            }
            else if (PythonText(feld["abschnitt"]) != abschnitt)
            {
                meldungen.Add($"{Datei}: Lernfeld {id}: Abschnitt passt nicht zur Fachrichtung");
            }
            if (!Einstellungen.PythonBool(feld["titel"]) || !Einstellungen.PythonBool(feld["beleg"]))
            {
                meldungen.Add($"{Datei}: Lernfeld {id}: Titel oder Beleg fehlt");
            }
        }

        var punkte = Objekte(plan["punkte"]).ToList();
        var zuordnung = zusatz["ap2"] as JsonObject ?? [];
        var pruefung = zusatz["pruefungsbereiche"] as JsonObject ?? [];
        foreach (var fachrichtung in Pruefung.Fachrichtung.Alle.Where(f => f.Id != Fachrichtungen.Systemintegration))
        {
            var fr = fachrichtung.Id;
            var abschnitt = AbschnittJe[fr];
            var erlaubt = new[] { fachrichtung.Bereich("b1").Kurz, fachrichtung.Bereich("b2").Kurz, "Projekt", "WiSo" };
            var eigene = zuordnung[fr] as JsonObject ?? [];
            foreach (var (id, eintrag) in eigene)
            {
                var punkt = punkte.FirstOrDefault(p => PythonText(p["id"]) == id);
                if (punkt is null || PythonText(punkt["abschnitt"]) != abschnitt)
                {
                    meldungen.Add($"{Datei}: AP2 {fr} {id}: Punkt fehlt oder gehoert nicht zu Abschnitt {abschnitt}");
                }
                var bereiche = Texte(eintrag?["bereiche"]).ToList();
                if (bereiche.Count == 0)
                {
                    meldungen.Add($"{Datei}: AP2 {fr} {id}: ohne Prüfungsbereich");
                }
                foreach (var bereich in bereiche.Where(b => !erlaubt.Contains(b)))
                {
                    meldungen.Add($"{Datei}: AP2 {fr} {id}: unbekannter Prüfungsbereich {bereich}");
                }
                if (!Einstellungen.PythonBool(eintrag?["beleg"]))
                {
                    meldungen.Add($"{Datei}: AP2 {fr} {id}: Beleg fehlt");
                }
            }
            foreach (var punkt in punkte.Where(p => PythonText(p["abschnitt"]) == abschnitt))
            {
                if (!eigene.ContainsKey(PythonText(punkt["id"])))
                {
                    meldungen.Add($"{Datei}: AP2 {fr}: Punkt {PythonText(punkt["id"])} ohne Zuordnung");
                }
            }

            var themen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rolle in new[] { "b1", "b2" })
            {
                var soll = fachrichtung.Bereich(rolle);
                if (pruefung[fr]?[rolle] is not JsonObject bereich)
                {
                    meldungen.Add($"{Datei}: Prüfungsbereich {fr} {rolle} fehlt");
                    continue;
                }
                if (PythonText(bereich["art"]) != soll.Art || PythonText(bereich["kurz"]) != soll.Kurz)
                {
                    meldungen.Add($"{Datei}: Prüfungsbereich {fr} {rolle}: Art oder Kurzname passt nicht");
                }
                var liste = Texte(bereich["themen"]).ToList();
                if (liste.Count != 4 || liste.Distinct(StringComparer.Ordinal).Count() != 4 || liste.Any(x => x.Length == 0))
                {
                    meldungen.Add($"{Datei}: Prüfungsbereich {fr} {rolle}: braucht genau 4 verschiedene Themen");
                }
                foreach (var thema in liste)
                {
                    if (!themen.Add(thema) || Fachbereiche.Ap2Themen.Contains(thema) || Fachbereiche.ThemenBlock.ContainsKey(thema))
                    {
                        meldungen.Add($"{Datei}: Prüfungsbereich {fr} {rolle}: Thema {thema} doppelt");
                    }
                }
                if (!Einstellungen.PythonBool(bereich["beleg"]))
                {
                    meldungen.Add($"{Datei}: Prüfungsbereich {fr} {rolle}: Beleg fehlt");
                }
            }
        }
        return meldungen;
    }

    /// <summary>Schluessel fuer Dubletten neuer Inhalte: (q oder title).strip().lower() wie Python.</summary>
    private static string DoppelSchluessel(JsonObject daten) =>
        DoppelText(ErsterText(daten["q"]) ?? ErsterText(daten["title"]) ?? "");

    private static string DoppelText(string text) => PythonKlein.Klein(PythonZeichen.Strip(text));
}
