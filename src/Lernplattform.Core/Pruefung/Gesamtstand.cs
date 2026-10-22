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
        var projekte = felder.Where(p => p.Key != Ausbildung.Zeile
                && !(p.Value.TryGetValue(GeloeschtFeld, out var weg) && weg == "1"))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (projekte.Count == 0)
        {
            return null;
        }
        var projekt = aktiv is not null && projekte.ContainsKey(aktiv) ? aktiv : projekte.Keys.Order(StringComparer.Ordinal).First();
        var roh = PythonZeichen.Strip(projekte[projekt].TryGetValue(SchaetzungFeld, out var s) ? s : "");
        if (roh.Length == 0)
        {
            return null;
        }
        // to_number: "€" und Leerzeichen weg, Leseregel der Rechner, sonst 0.0
        var text = PythonZeichen.Strip(roh).Replace("€", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        var wert = text.Length > 0 && Zahlen.Wert(text, out var zahl, out _) ? zahl : 0.0;
        return Math.Max(0.0, Math.Min(100.0, wert));
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
            // Nr 81 (Hinweis auf die Projektnote unter "Übersicht") erst mit W3 (Weisung P4-6, 3a)
            return new Gesamtkachel(zeilen, Beschriftungen.Einsetzen(PruefungTexte.FehltNoch, string.Join(", ", ergebnis.Fehlend)), ergebnis, null);
        }
        return new Gesamtkachel(zeilen, null, ergebnis, Beschriftungen.Einsetzen(PruefungTexte.GesamtZeile, PythonRunden.PunkteText(ergebnis.Gesamt),
            Navigation.NoteKurz, ergebnis.Note, ergebnis.Bestanden ? PruefungTexte.Bestanden : PruefungTexte.NichtBestanden));
    }

    /// <summary>Zeile einer Regel: "erfüllt:  " bzw. "offen:  " davor.</summary>
    public static string Regel((string Text, bool Ok) regel) => (regel.Ok ? PruefungTexte.Erfuellt : PruefungTexte.Offen) + regel.Text;

    /// <summary>_note_color: ab 67 gruen, ab 50 gelb, sonst rot.</summary>
    public static string Notenfarbe(double punkte) => punkte >= 67 ? "green" : punkte >= 50 ? "yellow" : "red";
}
