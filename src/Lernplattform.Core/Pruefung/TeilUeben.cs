using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;

namespace Lernplattform.Core.Pruefung;

/// <summary>Was ein Pruefungsteil zum Ueben anbietet: Fragen (Fragetexte), Szenarien (Quelle und Index) und der Stand dazu.</summary>
public sealed record TeilInhalte(IReadOnlyList<string> Fragen, string SzenarienQuelle, IReadOnlyList<Lerninhalt> Szenarien);

/// <summary>Stand eines Pruefungsteils: bearbeitete Fragen, richtig in Prozent (alle Antworten), bearbeitete Szenarien.</summary>
public sealed record TeilStand(int Fragen, int Bearbeitet, int Richtig, int Szenarien, int SzenarienBearbeitet);

/// <summary>
/// Welle A 1.15.0 (Weisung P12-406/407, Entwurf Variante B): Inhalte zum „Üben“ je Pruefungsteil, nur aus vorhandenen Zuordnungen
/// (nichts Neues erfunden, keine Inhalte verschoben):
/// <list type="bullet">
/// <item>Fragen: eine Quizfrage gehoert zu einem Teil, wenn einer ihrer Rahmenplan-Punkte (Zuordnung wie die Abdeckung im
/// Fortschritt, <see cref="RahmenplanGewicht.Punkte"/>) zu diesem Teil zaehlt: Teil 1 = Punkt mit „ap1“, ein Bereich von Teil 2 =
/// Punkt mit dem Kurznamen des Bereichs in „ap2“ (FISI „Konzeption“, „Netzwerke“, „WiSo“; FIAE/FIDP/FIDV ihre Bereiche aus
/// rahmenplan_zusatz.json). Die WiSo-Fragen der Probeklausur (Themen recht und arbeitswelt) zaehlen immer zu WiSo.</item>
/// <item>Szenarien: die Aufgaben, aus denen die Probeklausur dieses Teils zieht (<see cref="KlausurInhalte.Nutzbar"/> je Thema).</item>
/// </list>
/// Eine Frage kann zu mehreren Teilen gehoeren (ein Rahmenplan-Punkt zaehlt oft fuer mehrere Bereiche).
/// </summary>
public static class TeilUeben
{
    public static TeilInhalte Inhalte(Lerninhalte inhalte, RahmenplanGewicht? gewicht, KlausurInhalte klausur, Fachrichtung fachrichtung, string rolle)
    {
        var bereich = fachrichtung.Bereich(rolle);
        var punkte = PunkteFuer(inhalte, bereich);
        var art = klausur.Art(bereich.Art);
        var wisoThemen = art?.QuizThemen ?? (rolle == "wiso" ? Klausur.Art(Klausur.Wiso)!.QuizThemen : null);
        var fragen = new List<string>();
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var frage in inhalte.Quizfragen)
        {
            if (frage.Frage is not { } text || gesehen.Contains(text))
            {
                continue;
            }
            var dazu = wisoThemen is not null && frage.Thema is { } t && wisoThemen.Contains(t);
            if (!dazu && gewicht is not null)
            {
                dazu = gewicht.Punkte(Quellen.Quiz, text).Any(punkte.Contains);
            }
            if (dazu)
            {
                gesehen.Add(text);
                fragen.Add(text);
            }
        }
        if (art is { } a && a.Quelle is not ("keine" or "quiz"))
        {
            var themen = a.Themen?.SelectMany(w => w) ?? a.Pool ?? [];
            var liste = klausur.Quelle(a.Quelle);
            var indizes = themen.Distinct(StringComparer.Ordinal).SelectMany(t => klausur.Nutzbar(a.Quelle, t)).Distinct().Order();
            return new TeilInhalte(fragen, a.Quelle == "ap1" ? Quellen.Ap1 : Quellen.Ap2, [.. indizes.Select(i => liste[i])]);
        }
        return new TeilInhalte(fragen, "", []);
    }

    /// <summary>Rahmenplan-Punkte, die zu einem Bereich zaehlen (Teil 1: „ap1“, sonst Kurzname in „ap2“).</summary>
    public static HashSet<string> PunkteFuer(Lerninhalte inhalte, Pruefungsbereich bereich)
    {
        var ergebnis = new HashSet<string>(StringComparer.Ordinal);
        foreach (var punkt in inhalte.Rahmenplan["punkte"]!.AsArray())
        {
            if (punkt?["id"]?.GetValue<string>() is not { } id)
            {
                continue;
            }
            var zaehlt = bereich.Rolle == "ap1"
                ? Daten.Einstellungen.PythonBool(punkt["ap1"])
                : punkt["ap2"] is System.Text.Json.Nodes.JsonArray ap2 && ap2.Any(b => b?.GetValue<string>() == bereich.Kurz);
            if (zaehlt)
            {
                ergebnis.Add(id);
            }
        }
        return ergebnis;
    }

    /// <summary>Stand: Fragen mit mindestens einer Antwort, Anteil richtiger Antworten (wie die Erfolgsquote), Szenarien mit Status.</summary>
    public static TeilStand Stand(TeilInhalte teil, StatusBuch buch)
    {
        var bearbeitet = 0;
        long richtig = 0, alle = 0;
        foreach (var frage in teil.Fragen)
        {
            if (!buch.Antworten.Ergebnisse.TryGetValue(new FrageKennung(Quellen.Quiz, frage), out var antworten) || antworten.Count == 0)
            {
                continue;
            }
            bearbeitet++;
            foreach (var antwort in antworten)
            {
                if (antwort.Wert is { } w)
                {
                    alle++;
                    richtig += w ? 1 : 0;
                }
            }
        }
        // der Lernstand speichert die Position in der ganzen Liste (Nummer - 1, wie Szenarioablauf)
        var szenarien = teil.Szenarien.Count(e => buch.Beruehrt(teil.SzenarienQuelle, (long)(e.Nummer - 1)));
        var prozent = alle == 0 ? 0 : (int)Math.Round(richtig * 100.0 / alle, MidpointRounding.ToEven);
        return new TeilStand(teil.Fragen.Count, bearbeitet, prozent, teil.Szenarien.Count, szenarien);
    }
}
