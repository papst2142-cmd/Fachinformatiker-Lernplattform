using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Pruefung;

/// <summary>
/// Phase 4, W2 (E6, Weisung P4-6): finish in EINER Buchung. Wie Python werden das Ergebnis
/// (pruefungen), die WiSo-Fragen (quiz_answers, nur bekannte) bzw. die Aufgaben ("Gewusst" ab
/// 50 % der Punkte, ap1_events oder scenario_events) gespeichert und danach die laufende Pruefung
/// entfernt. Anders als Python steht alles in einer Transaktion mit der Kennung aus der laufenden
/// Pruefung: ein zweiter Abschluss (Absturz vor clear_running, erneuter Versuch) bucht nichts mehr.
/// Bei false (nicht gespeichert) bleibt die Pruefung in Phase "bewertung" erhalten (Frage 4 a).
/// </summary>
public static class Abschluss
{
    /// <summary>Zeilen fuer die Antworttabellen (wie die log_*-Aufrufe in finish).</summary>
    public static List<Abschlusszeile> Antworten(KlausurInhalte inhalte, JsonObject zustand, KlausurErgebnis ergebnis)
    {
        var zeilen = new List<Abschlusszeile>();
        if (Json.Text(zustand["art"]) == Klausur.Wiso)
        {
            foreach (var zeile in Bewertung.Wiso(inhalte, zustand))
            {
                if (inhalte.Quizfrage(zeile.Frage) is { } frage)
                {
                    zeilen.Add(new QuizAntwort(frage.Fachbereich ?? "", zeile.Frage, zeile.Ok));
                }
            }
            return zeilen;
        }
        foreach (var aufgabe in ((List<object?>)ergebnis.Daten["aufgaben"]!).Cast<PyObjekt>())
        {
            var punkte = KlausurErgebnis.Zahl(aufgabe["punkte"]);
            var maximum = KlausurErgebnis.Zahl(aufgabe["max"]);
            var quelle = aufgabe["quelle"] as string;
            var index = (int)KlausurErgebnis.Zahl(aufgabe["index"]);
            var szenario = inhalte.Quelle(quelle)[index];
            zeilen.Add(new AufgabeAntwort(quelle == "ap1", index, szenario.Titel ?? "", szenario.SzenarioThema ?? "",
                punkte * 2 >= maximum));
        }
        return zeilen;
    }

    /// <summary>
    /// Bewertete Pruefung speichern und die laufende entfernen. Ohne Kennung (Pruefung aus einer
    /// aelteren Fassung) wird sie zuerst erzeugt und in der laufenden Pruefung gespeichert.
    /// Rueckgabe: das Ergebnis, wenn es jetzt in pruefungen steht; sonst null.
    /// </summary>
    public static KlausurErgebnis? Abschliessen(KlausurInhalte inhalte, LernstandSchreiber schreiber, LaufendePruefung ablage,
        JsonObject zustand, Func<string>? neueKennung = null)
    {
        var ergebnis = Bewertung.Auswerten(inhalte, zustand);
        if (Json.Text(zustand[LaufendePruefung.KennungSchluessel]) is not { Length: > 0 } kennung)
        {
            if (!ablage.Starten(zustand, neueKennung))
            {
                return null;
            }
            kennung = Json.Text(zustand[LaufendePruefung.KennungSchluessel])!;
        }
        var gespeichert = schreiber.PruefungAbschliessen(kennung, Json.Text(zustand["art"]) ?? "", ergebnis.Punkte, ergebnis.Note,
            (int)ergebnis.Dauer, PythonDump.Text(ergebnis.Daten), Antworten(inhalte, zustand, ergebnis));
        if (!gespeichert)
        {
            return null;
        }
        ablage.Loeschen();      // misslingt es, bleibt die Pruefung stehen; ein neuer Abschluss bucht nichts doppelt
        return ergebnis;
    }
}
