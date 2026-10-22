using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Core.Pruefung;

/// <summary>Was die Oberflaeche zeigt (ExamPanel: show_choice, show_running, show_grading, show_result).</summary>
public enum KlausurAnsicht
{
    Auswahl,
    Lauf,
    Bewertung,
    Ergebnis,
}

/// <summary>Ergebnis eines Schritts: neue Ansicht und ggf. ein Hinweis (Zeit abgelaufen, nicht gespeichert).</summary>
public sealed record KlausurSchritt(KlausurAnsicht Ansicht, string? HinweisTitel = null, string? Hinweis = null);

/// <summary>
/// Phase 4, W2 (PB5/PB6): Ablauf der Klausursimulation wie app_gui.ExamPanel v0.63.2 ohne
/// Oberflaeche (Belege der Wortliste W2 Abschnitt 4). Die Zeit laeuft ueber start_utc und kennt
/// keine Pause; die laufende Pruefung steht in einstellungen.json (laufende_pruefung) und wird bei
/// jeder Antwort gespeichert. Abgabe: Phase "bewertung", Dauer fest. WiSo wird gleich
/// ausgewertet; anders als Python ist die Abgabe vorher gespeichert, damit ein misslungener
/// Abschluss sie nicht verliert. Abschluss in einer Buchung (E6); bei false bleibt die Pruefung
/// in Phase "bewertung" (Weisung P4-6, Frage 4 a). Mit <see cref="Probe"/> wird nichts gespeichert.
/// </summary>
public sealed class KlausurAblauf(KlausurInhalte inhalte, LaufendePruefung ablage, Func<LernstandSchreiber> schreiber,
    Func<DateTimeOffset> uhr, TimeZoneInfo zone, Func<PythonZufall>? zufall = null)
{
    public KlausurInhalte Inhalte => inhalte;

    /// <summary>Die gezeigte Pruefung (null: keine).</summary>
    public JsonObject? Zustand { get; private set; }

    /// <summary>Ergebnis nach dem Abschluss (Ansicht Ergebnis) und seine Art.</summary>
    public KlausurErgebnis? Ergebnis { get; private set; }

    public string? ErgebnisArt { get; private set; }

    /// <summary>Start ohne Bedienung: nichts speichern (Zustand von aussen gesetzt).</summary>
    public bool Probe { get; set; }

    private double Jetzt => Klausur.Sekunden(uhr());

    /// <summary>fp.load_running() is not None (auch Phase "bewertung").</summary>
    public bool Laeuft => ablage.Laden(inhalte) is not null;

    /// <summary>Gezielte Uebung (Notizblock, "Jetzt üben") bei laufender Pruefung: nur ein Hinweis.</summary>
    public static bool UebungGesperrt(bool pruefungGewaehlt, bool laeuft) => pruefungGewaehlt && laeuft;

    public string? Art => Zustand is { } z ? Json.Text(z["art"]) : null;

    public bool Wiso => Art == Klausur.Wiso;

    public bool ImLauf => Zustand is { } z && Json.Text(z["phase"]) == Klausur.Laeuft;

    /// <summary>Restzeit in Sekunden (seconds_left).</summary>
    public long Rest => Zustand is { } z ? Klausur.Rest(z, Jetzt, zone) : 0;

    private bool Speichern() => Probe || ablage.Speichern(Zustand!);

    /// <summary>Fuer den Start ohne Bedienung: eine Pruefung zeigen, ohne sie zu speichern.</summary>
    public void ProbeSetzen(JsonObject? zustand, KlausurErgebnis? ergebnis = null)
    {
        Probe = true;
        Zustand = zustand;
        Ergebnis = ergebnis;
        ErgebnisArt = zustand is null ? null : Json.Text(zustand["art"]);
    }

    /// <summary>show: gespeicherte Pruefung fortsetzen; abgelaufen: abgeben.</summary>
    public KlausurSchritt Zeigen()
    {
        Probe = false;
        Zustand = ablage.Laden(inhalte);
        if (Zustand is null)
        {
            return new(KlausurAnsicht.Auswahl);
        }
        if (ImLauf)
        {
            return Rest <= 0 ? Abgeben(auto: true) : new(KlausurAnsicht.Lauf);
        }
        return new(KlausurAnsicht.Bewertung);
    }

    /// <summary>
    /// start (nach "Ja"): laeuft schon eine Pruefung (auch "bewertung"), wird sie gezeigt und nie
    /// ueberschrieben. Ohne gespeicherte Startzeit (Speichern misslingt) startet nichts.
    /// </summary>
    public KlausurSchritt Starten(string art)
    {
        if (Laeuft)
        {
            return Zeigen();
        }
        var zustand = Klausur.Neu(inhalte, art, Gesamtstand.Verlauf(schreiber().Lernstand), (zufall ?? Klausur.Zufall)(), uhr(), zone);
        if (!ablage.Starten(zustand))
        {
            return new(KlausurAnsicht.Auswahl, Texte.Optionen.SpeichernFehlgeschlagenTitel, Texte.Optionen.SpeichernFehlgeschlagen);
        }
        Probe = false;
        Zustand = zustand;
        return new(KlausurAnsicht.Lauf);
    }

    /// <summary>_choose: WiSo-Antwort sofort speichern (nur im Lauf).</summary>
    public void Waehlen(string frage, string wert)
    {
        if (!ImLauf)
        {
            return;
        }
        ((JsonObject)Zustand!["antworten"]!)[frage] = wert;
        Speichern();
    }

    /// <summary>_flush_answers: geaenderte Antworten offener Aufgaben speichern; true: etwas geaendert.</summary>
    public bool Festhalten(IEnumerable<(string Schluessel, string? Text)> felder)
    {
        if (!ImLauf || Wiso)
        {
            return false;
        }
        var antworten = (JsonObject)Zustand!["antworten"]!;
        var geaendert = false;
        foreach (var (schluessel, text) in felder)
        {
            var wert = Klausur.Feldtext(text);
            // antworten.get(key, "") != value
            var gleich = antworten.ContainsKey(schluessel) ? Json.Text(antworten[schluessel]) == wert : wert.Length == 0;
            if (!gleich)
            {
                antworten[schluessel] = wert;
                geaendert = true;
            }
        }
        if (geaendert)
        {
            Speichern();
        }
        return geaendert;
    }

    /// <summary>cancel nach "Ja": laufende Pruefung verwerfen, kein Ergebnis.</summary>
    public KlausurSchritt Abbrechen()
    {
        if (!Probe)
        {
            ablage.Loeschen();
        }
        Zustand = null;
        return new(KlausurAnsicht.Auswahl);
    }

    /// <summary>
    /// _submit (nach "Ja" bzw. bei abgelaufener Zeit): Dauer fest, Phase "bewertung", gespeichert.
    /// WiSo wird gleich ausgewertet und gespeichert (Hinweis "Zeit abgelaufen" nur bei auto und
    /// Erfolg); offene Aufgaben gehen in die Bewertung (Hinweis bei auto).
    /// </summary>
    public KlausurSchritt Abgeben(bool auto)
    {
        if (!ImLauf)
        {
            return new(Zustand is null ? KlausurAnsicht.Auswahl : KlausurAnsicht.Bewertung);
        }
        Klausur.Abgeben(Zustand!, Jetzt, zone);
        Speichern();
        if (Wiso)
        {
            var schritt = Abschliessen();
            return schritt.Ansicht == KlausurAnsicht.Ergebnis && auto
                ? schritt with { HinweisTitel = Texte.PruefungTexte.ZeitTitel, Hinweis = Texte.PruefungTexte.ZeitWiso }
                : schritt;
        }
        return auto
            ? new(KlausurAnsicht.Bewertung, Texte.PruefungTexte.ZeitTitel, Texte.PruefungTexte.ZeitOffen)
            : new(KlausurAnsicht.Bewertung);
    }

    /// <summary>Haken (toggled): merken, Punkte vorschlagen (suggested_points), speichern. Rueckgabe: neue Punkte.</summary>
    public int Haken(int aufgabe, int teil, int kriterium, bool an)
    {
        var details = Klausur.Details(inhalte, Zustand!, aufgabe)!.Teile[teil];
        var haken = Haken(aufgabe, teil);
        while (haken.Count <= kriterium)
        {
            haken.Add(false);
        }
        haken[kriterium] = an;
        var punkte = Klausur.Vorschlag(details.Punkte, haken.Select(h => Json.Zahl(h) is > 0).ToList());
        ((JsonObject)Zustand!["punkte"]!)[Klausur.Schluessel(aufgabe, teil)] = punkte;
        Speichern();
        return punkte;
    }

    /// <summary>Haken einer Teilaufgabe (marks = setdefault(key, [False] * len(kriterien))).</summary>
    public JsonArray Haken(int aufgabe, int teil)
    {
        var kriterien = (JsonObject)Zustand!["kriterien"]!;
        var schluessel = Klausur.Schluessel(aufgabe, teil);
        if (kriterien[schluessel] is not JsonArray haken)
        {
            var anzahl = Klausur.Details(inhalte, Zustand, aufgabe)!.Teile[teil].Kriterien.Count;
            haken = new JsonArray([.. Enumerable.Range(0, anzahl).Select(_ => (JsonNode?)false)]);
            kriterien[schluessel] = haken;
        }
        return haken;
    }

    /// <summary>Punkte einer Teilaufgabe (Punktefeld 0 bis Maximum, ganzzahlig).</summary>
    public long Punkte(int aufgabe, int teil) =>
        (long)Math.Truncate(Json.Zahl(((JsonObject)Zustand!["punkte"]!)[Klausur.Schluessel(aufgabe, teil)]) ?? 0);

    /// <summary>Punktefeld geaendert (changed): auf 0 bis Maximum begrenzt, speichern.</summary>
    public void PunkteSetzen(int aufgabe, int teil, int wert)
    {
        var maximum = Klausur.Details(inhalte, Zustand!, aufgabe)!.Teile[teil].Punkte;
        ((JsonObject)Zustand!["punkte"]!)[Klausur.Schluessel(aufgabe, teil)] = Math.Max(0, Math.Min(maximum, wert));
        Speichern();
    }

    /// <summary>
    /// _finish (nach "Ja"): in einer Buchung speichern, danach erst die laufende Pruefung entfernen.
    /// false: die Pruefung bleibt in Phase "bewertung", die Bewertung bleibt stehen, Hinweis.
    /// </summary>
    public KlausurSchritt Abschliessen()
    {
        var art = Art!;
        if (Probe || Abschluss.Abschliessen(inhalte, schreiber(), ablage, Zustand!) is not { } ergebnis)
        {
            return new(KlausurAnsicht.Bewertung, Texte.KartenTexte.HinweisTitel, Texte.TrainerTexte.NichtGespeichert);
        }
        Zustand = null;
        Ergebnis = ergebnis;
        ErgebnisArt = art;
        return new(KlausurAnsicht.Ergebnis);
    }
}
