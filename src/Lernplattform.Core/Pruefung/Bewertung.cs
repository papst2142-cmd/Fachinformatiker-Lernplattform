using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Pruefung;

/// <summary>Eine WiSo-Zeile (wiso_result): heutiger Fragetext, gewaehlt, richtig, ist richtig.</summary>
public sealed record WisoZeile(string Frage, string Gewaehlt, string Richtig, bool Ok);

/// <summary>evaluate: Punkte (0-100), Note, Daten (wird als JSON gespeichert), Dauer in Sekunden.</summary>
public sealed record KlausurErgebnis(double Punkte, string Note, PyObjekt Daten, long Dauer)
{
    /// <summary>Themen {thema: [erreicht, moeglich]} in Reihenfolge.</summary>
    public IReadOnlyList<(string Thema, double Erreicht, double Moeglich)> Themen =>
        ((PyObjekt)Daten["themen"]!).Select(p => (p.Key, Zahl(((List<object?>)p.Value!)[0]), Zahl(((List<object?>)p.Value!)[1]))).ToList();

    internal static double Zahl(object? wert) => wert switch
    {
        long l => l,
        int i => i,
        double d => d,
        bool b => b ? 1 : 0,
        _ => 0,
    };
}

/// <summary>Gesamtergebnis (overall) mit den Regeln zum Bestehen.</summary>
public sealed record GesamtErgebnis(bool Vollstaendig, IReadOnlyList<string> Fehlend, double Gesamt, double Teil2, string Note,
    IReadOnlyList<(string Text, bool Ok)> Regeln, bool Bestanden, string Ergaenzung, int Ausreichend, int ErgaenzungArt,
    string? ErgaenzungRolle, int? Muendlich);

/// <summary>
/// Phase 4, W2 (PB6, PB7): Bewertung und Gesamtergebnis wie fisi_pruefung v0.63.2 (wiso_result,
/// evaluate, weak_topics, latest_results, _abrunden, overall, _rules, _supplement_hint),
/// Vergleichswerte G32 und G36. Gesamtergebnis je Fachrichtung (§ 16/24/32/40 Abs. 2 Bestehen,
/// § 17/25/33/41 muendliche Ergaenzung in b1, b2 oder WiSo, alt zu muendlich 2:1); die Zahlen
/// haengen nicht von der Fachrichtung ab, nur die Namen. Summen mit PythonSumme (sum() von
/// Python 3.12), Note aus dem ungerundeten Wert, Teil 2 gewichtet durch 80.
/// </summary>
public static class Bewertung
{
    /// <summary>wiso_result: alte Fragetexte zaehlen beim neuen; eine unbekannte Frage zaehlt als nicht richtig.</summary>
    public static List<WisoZeile> Wiso(KlausurInhalte inhalte, JsonObject zustand)
    {
        var antworten = zustand["antworten"] as JsonObject;
        var zeilen = new List<WisoZeile>();
        foreach (var knoten in (JsonArray)zustand["fragen"]!)
        {
            var frage = Json.Text(knoten) ?? "";
            var roh = antworten?[frage];
            var heute = inhalte.AktuelleFrage(frage) ?? frage;
            var richtig = inhalte.Quizfrage(heute)?.Antwort ?? "";
            zeilen.Add(new WisoZeile(heute, roh is null ? "" : Json.Text(roh) ?? roh.ToJsonString(), richtig,
                richtig.Length > 0 && Json.Text(roh) == richtig));
        }
        return zeilen;
    }

    /// <summary>evaluate: Ergebnis einer bewerteten Pruefung.</summary>
    public static KlausurErgebnis Auswerten(KlausurInhalte inhalte, JsonObject zustand)
    {
        var themen = new PyObjekt();
        PyObjekt daten;
        double punkte;
        if (Json.Text(zustand["art"]) == Klausur.Wiso)
        {
            var alle = Wiso(inhalte, zustand);
            var zeilen = alle.Where(z => inhalte.Quizfrage(z.Frage) is not null).ToList();
            var unbekannt = alle.Where(z => inhalte.Quizfrage(z.Frage) is null).Select(z => (object?)z.Frage).ToList();
            var richtig = zeilen.Count(z => z.Ok);
            punkte = WisoPunkte(richtig, zeilen.Count);
            var werte = new Dictionary<string, (long R, long N)>();
            var reihenfolge = new List<string>();
            foreach (var z in zeilen)
            {
                var name = inhalte.Quizfrage(z.Frage)!.Thema == "recht" ? PruefungTexte.ThemaRecht : PruefungTexte.ThemaArbeitswelt;
                if (!werte.TryGetValue(name, out var w))
                {
                    reihenfolge.Add(name);
                }
                werte[name] = (w.R + (z.Ok ? 1 : 0), w.N + 1);
            }
            foreach (var name in reihenfolge)
            {
                themen.Add(name, new List<object?> { werte[name].R, werte[name].N });
            }
            daten = new PyObjekt
            {
                { "fragen", zeilen.Select(z => (object?)new PyObjekt { { "q", z.Frage }, { "richtig", z.Ok } }).ToList() },
                { "richtig", (long)richtig },
            };
            if (unbekannt.Count > 0)
            {
                daten.Add("unbekannt", unbekannt);
            }
        }
        else
        {
            var aufgaben = new List<object?>();
            var bewertet = zustand["punkte"] as JsonObject;
            long gesamt = 0;
            var werte = new Dictionary<string, (long R, long N)>();
            var reihenfolge = new List<string>();
            var liste = (JsonArray)zustand["aufgaben"]!;
            for (var nummer = 0; nummer < liste.Count; nummer++)
            {
                var teile = Klausur.Details(inhalte, zustand, nummer) ?? throw new InvalidDataException("Aufgabe passt nicht mehr");
                long erreicht = 0;
                for (var t = 0; t < teile.Teile.Count; t++)
                {
                    erreicht += Erreicht(teile.Teile[t].Punkte, bewertet?[Klausur.Schluessel(nummer, t)]);
                }
                long maximum = teile.Teile.Sum(t => t.Punkte);
                gesamt += erreicht;
                if (!werte.TryGetValue(teile.Thema, out var w))
                {
                    reihenfolge.Add(teile.Thema);
                }
                werte[teile.Thema] = (w.R + erreicht, w.N + maximum);
                var aufgabe = (JsonObject)liste[nummer]!;
                aufgaben.Add(new PyObjekt
                {
                    { "quelle", Json.Wert(aufgabe["quelle"]) }, { "index", Json.Wert(aufgabe["index"]) },
                    { "titel", teile.Titel }, { "thema", teile.Thema }, { "punkte", erreicht }, { "max", maximum },
                });
            }
            foreach (var name in reihenfolge)
            {
                themen.Add(name, new List<object?> { werte[name].R, werte[name].N });
            }
            punkte = gesamt;
            daten = new PyObjekt { { "aufgaben", aufgaben } };
        }
        daten.Add("themen", themen);
        daten.Add("minuten", Json.Wert(zustand["minuten"]));
        var dauer = zustand["dauer"] is { } d ? (long)Math.Truncate(Json.Zahl(d) ?? 0) : 0;
        return new KlausurErgebnis(punkte, Trainer.Note(punkte), daten, dauer);
    }

    /// <summary>WiSo-Punkte: round(100.0 * richtig / max(1, anzahl), 1).</summary>
    public static double WisoPunkte(int richtig, int anzahl) => PythonRundenStellen.Runde(100.0 * richtig / Math.Max(1, anzahl), 1);

    /// <summary>Punkte einer Teilaufgabe: max(0, min(teilmax, int(wert))).</summary>
    public static long Erreicht(int teilmax, JsonNode? wert) => Math.Max(0, Math.Min(teilmax, Ganzzahl(wert)));

    /// <summary>int(value) wie Python (Zahl abgeschnitten, True = 1, Text als ganze Zahl); fehlt der Wert: 0.</summary>
    private static long Ganzzahl(JsonNode? wert)
    {
        if (wert is null)
        {
            return 0;
        }
        if (Json.Text(wert) is { } text)
        {
            return long.Parse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }
        var zahl = Json.Zahl(wert) ?? throw new InvalidDataException("Punkte sind keine Zahl");
        return (long)Math.Truncate(zahl);
    }

    /// <summary>weak_topics: Themen unter 50 %, schwaechstes zuerst (bei Gleichstand nach Name).</summary>
    public static List<string> Schwach(IEnumerable<(string Thema, double Erreicht, double Moeglich)> themen) =>
        themen.Select(t => (Anteil: t.Moeglich != 0 ? t.Erreicht / t.Moeglich : 0.0, t.Thema))
            .OrderBy(t => t.Anteil).ThenBy(t => t.Thema, StringComparer.Ordinal)
            .Where(t => t.Anteil < 0.5).Select(t => t.Thema).ToList();

    /// <summary>latest_results: Punkte der jeweils letzten Pruefung je Bereich (Verlauf neueste zuerst).</summary>
    public static Dictionary<string, double> Letzte(IEnumerable<(string Art, double Punkte)> verlauf)
    {
        var letzte = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (art, punkte) in verlauf)
        {
            letzte.TryAdd(art, punkte);
        }
        return letzte;
    }

    /// <summary>_abrunden: auf eine Stelle abrunden, vorher auf 9 Stellen runden; -0 wird 0 (math.floor gibt int).</summary>
    public static double Abrunden(double wert) => (Math.Floor(PythonRundenStellen.Runde(wert, 9) * 10) + 0.0) / 10;

    /// <summary>overall: Gesamtergebnis aus {rolle: punkte}. Fehlt eine Rolle, ist es unvollstaendig.</summary>
    public static GesamtErgebnis Gesamt(Fachrichtung fachrichtung, IReadOnlyDictionary<string, double> werte)
    {
        var fehlend = fachrichtung.Bereiche.Where(b => !werte.ContainsKey(b.Rolle)).Select(b => b.Kurz).ToList();
        if (fehlend.Count > 0)
        {
            return new GesamtErgebnis(false, fehlend, 0, 0, "", [], false, "", 0, 0, null, null);
        }
        var w = Fachrichtung.Rollen.Select(r => werte[r]).ToArray();
        var (gesamt, teil2) = Summen(fachrichtung, w);
        var regeln = Regeln(fachrichtung, w);
        var bestanden = regeln.All(r => r.Ok);
        var (text, art, rolle, muendlich) = bestanden ? ("", 0, null, null) : Ergaenzung(fachrichtung, w);
        return new GesamtErgebnis(true, [], Abrunden(gesamt), Abrunden(teil2), Trainer.Note(gesamt), regeln, bestanden, text,
            Ausreichend(w), art, rolle, muendlich);
    }

    private static (double Gesamt, double Teil2) Summen(Fachrichtung f, double[] w)
    {
        var gesamt = PythonSumme.Summe(f.Bereiche.Select((b, i) => w[i] * b.Gewicht)) / 100.0;
        var gewicht2 = f.Bereiche.Skip(1).Sum(b => b.Gewicht);
        var teil2 = PythonSumme.Summe(f.Bereiche.Skip(1).Select((b, i) => w[i + 1] * b.Gewicht)) / gewicht2;
        return (gesamt, teil2);
    }

    private static int Ausreichend(double[] w) => w.Skip(1).Count(x => x >= 50);

    /// <summary>_rules: die vier Regeln mit den ungerundeten Werten.</summary>
    public static List<(string Text, bool Ok)> Regeln(Fachrichtung f, double[] w)
    {
        var (gesamt, teil2) = Summen(f, w);
        var ausreichend = Ausreichend(w);
        return
        [
            (PruefungTexte.Regel1, gesamt >= 50),
            (PruefungTexte.Regel2, teil2 >= 50),
            (Beschriftungen.Einsetzen(PruefungTexte.Regel3, ausreichend), ausreichend >= 3),
            (PruefungTexte.Regel4, w.Skip(1).All(x => x >= 30)),
        ];
    }

    private static bool AlleErfuellt(Fachrichtung f, double[] w)
    {
        var (gesamt, teil2) = Summen(f, w);
        return gesamt >= 50 && teil2 >= 50 && Ausreichend(w) >= 3 && w.Skip(1).All(x => x >= 30);
    }

    /// <summary>_supplement_hint: reicht die bestmoegliche muendliche Ergaenzung (nur b1, b2, WiSo; 2:1)?</summary>
    private static (string Text, int Art, string? Rolle, int? Muendlich) Ergaenzung(Fachrichtung f, double[] w)
    {
        var moeglich = new List<(int Muendlich, string Kurz, string Rolle)>();
        for (var i = 0; i < f.Bereiche.Count; i++)
        {
            var bereich = f.Bereiche[i];
            if (!bereich.Ergaenzung || w[i] >= 50)
            {
                continue;
            }
            var probe = (double[])w.Clone();
            probe[i] = (2 * w[i] + 100) / 3.0;
            if (!AlleErfuellt(f, probe))
            {
                continue;
            }
            for (var muendlich = 0; muendlich <= 100; muendlich++)
            {
                probe[i] = (2 * w[i] + muendlich) / 3.0;
                if (AlleErfuellt(f, probe))
                {
                    moeglich.Add((muendlich, bereich.Kurz, bereich.Rolle));
                    break;
                }
            }
        }
        if (moeglich.Count == 0)
        {
            return (Beschriftungen.Einsetzen(PruefungTexte.ErgaenzungNein, f.Bereich("b1").Kurz, f.Bereich("b2").Kurz), 1, null, null);
        }
        var best = moeglich.OrderBy(m => m.Muendlich).ThenBy(m => m.Kurz, StringComparer.Ordinal).First();
        return (Beschriftungen.Einsetzen(PruefungTexte.ErgaenzungJa, best.Kurz,
            Dashboard.DashboardTexte.Plural(best.Muendlich, PruefungTexte.PunktEinzahl, PruefungTexte.PunktMehrzahl)),
            2, best.Rolle, best.Muendlich);
    }
}

/// <summary>
/// json.dumps(wert, ensure_ascii=False) mit Pythons Standard-Trennzeichen ", " und ": " (so speichert
/// save_exam die Daten einer Pruefung). Werte wie bei PythonJson; Zeichenketten gleich umgeschrieben.
/// </summary>
public static class PythonDump
{
    public static string Text(object? wert)
    {
        var text = new StringBuilder();
        Schreiben(text, wert);
        return text.ToString();
    }

    private static void Schreiben(StringBuilder text, object? wert)
    {
        switch (wert)
        {
            case null or bool or long or int or double:
                text.Append(PythonJson.Text(wert));
                break;
            case string s:
                text.Append(PythonJson.Text(s));
                break;
            case PyObjekt objekt:
                text.Append('{');
                var erstes = true;
                foreach (var (schluessel, inhalt) in objekt)
                {
                    text.Append(erstes ? "" : ", ").Append(PythonJson.Text(schluessel)).Append(": ");
                    erstes = false;
                    Schreiben(text, inhalt);
                }
                text.Append('}');
                break;
            case System.Collections.IEnumerable liste:
                text.Append('[');
                var erster = true;
                foreach (var inhalt in liste)
                {
                    text.Append(erster ? "" : ", ");
                    erster = false;
                    Schreiben(text, inhalt);
                }
                text.Append(']');
                break;
            default:
                throw new FormatException("Wert nicht als JSON darstellbar: " + wert.GetType().Name);
        }
    }
}
