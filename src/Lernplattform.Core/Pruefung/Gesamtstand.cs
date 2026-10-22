using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Pruefung;

/// <summary>Die Kachel "Gesamtergebnis" (overall_card): Bereichszeilen, dann Hinweis oder Ergebnis mit Regeln.</summary>
public sealed record Gesamtkachel(IReadOnlyList<string> Zeilen, string? Hinweis, GesamtErgebnis Ergebnis, string? GesamtZeile);

/// <summary>Ein Balken der Karte "Nach Themen" (Anteil in Prozent, rot unter 50).</summary>
public sealed record Themenbalken(string Name, double Anteil, string Wert, bool Schwach);

/// <summary>Eine Zeile der Liste "Letzte Prüfungen".</summary>
public sealed record VerlaufZeile(string Datum, string Name, string Punkte, string Note, double Wert);

/// <summary>
/// Phase 4, W2 (PB7): Verlauf der Pruefungen (DBManager.exams) und Kachel "Gesamtergebnis" wie
/// app_gui.overall_card: letzte Pruefung je Bereich plus geschaetzte Projektnote des aktiven
/// Projekts (fisi_projekt projects, active_project, estimate). Die Bereiche und Namen kommen aus der
/// Fachrichtung; fuer AE, DPA und DV gibt es b1/b2 noch nicht ("noch offen").
/// </summary>
public static class Gesamtstand
{
    /// <summary>fisi_projekt: Feldnamen und Schluessel.</summary>
    public const string SchaetzungFeld = "schaetzung";
    public const string GeloeschtFeld = "_geloescht";
    public const string AktivSchluessel = "aktives_projekt";

    /// <summary>exams: alle Pruefungen, neueste zuerst; kaputte Daten gelten als leer.</summary>
    public static List<KlausurEintrag> Verlauf(Lernstand lernstand)
    {
        var liste = new List<KlausurEintrag>();
        foreach (var z in lernstand.Abfragen("SELECT timestamp, art, punkte, note, dauer, daten, uid FROM pruefungen ORDER BY timestamp DESC, id DESC"))
        {
            JsonNode? daten;
            try
            {
                daten = JsonNode.Parse(z[5] as string is { Length: > 0 } t ? t : "{}");
            }
            catch (JsonException)
            {
                daten = new JsonObject();
            }
            var punkte = z[2] switch
            {
                double d => d,
                long l => l,
                _ => (double?)null,
            };
            liste.Add(new KlausurEintrag(Convert.ToString(z[0], CultureInfo.InvariantCulture) ?? "", z[1] as string, punkte,
                z[3] as string, daten, z[6] as string));
        }
        return liste;
    }

    /// <summary>_german_time: "2026-09-27 21:40:05" wird "27.09.2026 21:40".</summary>
    public static string Datum(string zeit) => PythonZeichen.Teil(zeit, 8, 10) + "." + PythonZeichen.Teil(zeit, 5, 7) + "."
        + PythonZeichen.Teil(zeit, 0, 4) + " " + PythonZeichen.Teil(zeit, 11, 16);

    /// <summary>Zeilen der Karte "Letzte Prüfungen" (hoechstens 8).</summary>
    public static List<VerlaufZeile> Letzte(IEnumerable<KlausurEintrag> verlauf) => verlauf.Take(8).Select(e => new VerlaufZeile(
        Datum(e.Zeit), Klausur.Art(e.Art)?.Kurz ?? e.Art ?? "None", Beschriftungen.Einsetzen(PruefungTexte.PunkteMuster,
            PythonRunden.PunkteText(e.Punkte ?? double.NaN)), e.Note ?? "", e.Punkte ?? 0)).ToList();

    /// <summary>
    /// estimate(fields) des aktiven Projekts (active_project): gespeichertes aktives Projekt, sonst das
    /// erste nach Kennung; ohne Projekt null. Geloeschte Projekte und die Zeile der Ausbildung zaehlen nicht.
    /// </summary>
    public static double? Projektschaetzung(OrderedDictionary<string, OrderedDictionary<string, string>> felder, string? aktiv)
    {
        // W3: der Kern des Abschlussprojekts (projects, active_project, estimate); ohne Projekt null
        var projekte = Projekt.Abschlussprojekt.Projekte(felder);
        return projekte.Count == 0 ? null : Projekt.Abschlussprojekt.Schaetzung(projekte[Projekt.Abschlussprojekt.AktivesProjekt(projekte, aktiv)]);
    }

    /// <summary>overall_card fuer eine Fachrichtung.</summary>
    public static Gesamtkachel Kachel(Fachrichtung fachrichtung, IEnumerable<KlausurEintrag> verlauf, double? projekt)
    {
        var letzte = Bewertung.Letzte(verlauf.Where(e => e.Art is not null && e.Punkte is not null).Select(e => (e.Art!, e.Punkte!.Value)));
        var werte = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var bereich in fachrichtung.Bereiche)
        {
            if (bereich.Rolle == "projekt" ? projekt is not null : letzte.ContainsKey(bereich.Art))
            {
                werte[bereich.Rolle] = bereich.Rolle == "projekt" ? projekt!.Value : letzte[bereich.Art];
            }
        }
        var zeilen = fachrichtung.Bereiche.Select(b => Beschriftungen.Einsetzen(PruefungTexte.BereichZeile, b.Kurz, b.Gewicht,
            werte.TryGetValue(b.Rolle, out var w) ? Beschriftungen.Einsetzen(PruefungTexte.PunkteMuster, PythonRunden.PunkteText(w))
                : PruefungTexte.NochOffen)).ToList();
        var ergebnis = Bewertung.Gesamt(fachrichtung, werte);
        if (!ergebnis.Vollstaendig)
        {
            // Nr 81 (W3): Hinweis auf die Projektnote unter "Übersicht", nur wenn das Projekt fehlt (app_gui 4133)
            var hinweis = Beschriftungen.Einsetzen(PruefungTexte.FehltNoch, string.Join(", ", ergebnis.Fehlend));
            if (ergebnis.Fehlend.Contains(fachrichtung.Bereich("projekt").Kurz))
            {
                hinweis += PruefungTexte.ProjektHinweisW3;
            }
            return new Gesamtkachel(zeilen, hinweis, ergebnis, null);
        }
        return new Gesamtkachel(zeilen, null, ergebnis, Beschriftungen.Einsetzen(PruefungTexte.GesamtZeile, PythonRunden.PunkteText(ergebnis.Gesamt),
            Navigation.NoteKurz, ergebnis.Note, ergebnis.Bestanden ? PruefungTexte.Bestanden : PruefungTexte.NichtBestanden));
    }

    // -- Ergebnis (show_result) ---------------------------------------------------------------

    /// <summary>"%s Punkte" gross.</summary>
    public static string Punkte(KlausurErgebnis ergebnis) => Beschriftungen.Einsetzen(PruefungTexte.PunkteMuster, PythonRunden.PunkteText(ergebnis.Punkte));

    /// <summary>"Note (Orientierung) 2 (Gut)  ·  Dauer 12:34".</summary>
    public static string NoteDauer(KlausurErgebnis ergebnis) =>
        Beschriftungen.Einsetzen(PruefungTexte.NoteDauer, Navigation.NoteKurz, ergebnis.Note, Klausur.Zeittext(ergebnis.Dauer));

    /// <summary>WiSo "%d von %d Fragen richtig", sonst je Aufgabe "Aufgabe %d · %s: %d von %d Punkten".</summary>
    public static List<string> Ergebniszeilen(string art, KlausurErgebnis ergebnis)
    {
        var daten = ergebnis.Daten;
        if (art == Klausur.Wiso)
        {
            return [Beschriftungen.Einsetzen(PruefungTexte.FragenRichtig, Ganz(daten["richtig"]), ((List<object?>)daten["fragen"]!).Count)];
        }
        return ((List<object?>)daten["aufgaben"]!).Cast<PyObjekt>().Select((a, i) => Beschriftungen.Einsetzen(PruefungTexte.AufgabeErgebnis,
            i + 1, a["titel"] as string ?? "", Ganz(a["punkte"]), Ganz(a["max"]))).ToList();
    }

    /// <summary>Balken "Nach Themen", nach Namen sortiert wie sorted(themen.items()).</summary>
    public static List<Themenbalken> Themen(KlausurErgebnis ergebnis) => ergebnis.Themen
        .OrderBy(t => t.Thema, StringComparer.Ordinal)
        .Select(t =>
        {
            var anteil = t.Moeglich != 0 ? t.Erreicht / t.Moeglich * 100 : 0;
            return new Themenbalken(t.Thema, anteil, Beschriftungen.Einsetzen(PruefungTexte.BalkenWert,
                PythonRunden.PunkteText(t.Erreicht), PythonRunden.PunkteText(t.Moeglich)), anteil < 50);
        }).ToList();

    private static long Ganz(object? wert) => (long)Math.Truncate(KlausurErgebnis.Zahl(wert));

    /// <summary>Zeile einer Regel: "erfüllt:  " bzw. "offen:  " davor.</summary>
    public static string Regel((string Text, bool Ok) regel) => (regel.Ok ? PruefungTexte.Erfuellt : PruefungTexte.Offen) + regel.Text;

    /// <summary>_note_color: ab 67 gruen, ab 50 gelb, sonst rot.</summary>
    public static string Notenfarbe(double punkte) => punkte >= 67 ? "green" : punkte >= 50 ? "yellow" : "red";
}
