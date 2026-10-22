using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Hilfe;

/// <summary>Was beim Start von selbst erscheint (Plan W4 2.3), in dieser Reihenfolge.</summary>
/// <param name="Rundgang">RundgangFaellig, bestimmt VOR der Ausbildungsfrage (die Frage legt eine Zeile an).</param>
/// <param name="Frage">Ausbildungsfrage (danach der Warnhinweis, wenn noetig).</param>
/// <param name="VonAnderswo">Fachrichtung kam von einem anderen Geraet (Merker ausbildung_zuletzt weicht ab).</param>
public sealed record StartSchritte(bool Rundgang, bool Frage, bool VonAnderswo)
{
    public static readonly StartSchritte Keine = new(false, false, false);
}

/// <summary>Was der Startablauf liest (in der App im Hintergrund).</summary>
public sealed record StartLage(bool HatDaten, bool Lesbar, bool Gewaehlt, string? Gespeichert);

/// <summary>
/// Phase 4, W4 (nur C#, Nico P4-9): einmalige Frage nach der Fachrichtung und Warnhinweis Rahmenplan
/// (Plan W4 2.3-2.5, Wortliste N3-N8, Weisung P4-11). Merker nur in einstellungen.json (dieses Geraet):
/// ausbildung_gefragt (Datum wie rundgang_gesehen) und ausbildung_zuletzt (die Fachrichtung, die dieses
/// Geraet zuletzt hatte; schaltet nichts). Die Fachrichtung selbst steht in Pruefung.Ausbildung.
/// </summary>
public static class AusbildungFrage
{
    public const string GefragtSchluessel = "ausbildung_gefragt";
    public const string ZuletztSchluessel = "ausbildung_zuletzt";

    /// <summary>Abschnitt des Rahmenplans je Fachrichtung (B fuer AE, D fuer DPA, E fuer DV), sonst null.</summary>
    public static string? Abschnitt(string? fachrichtung) => fachrichtung switch
    {
        "ae" => "B",
        "dpa" => "D",
        "dv" => "E",
        _ => null,
    };

    /// <summary>Schluessel des Abschnitts (rp_abschnitt_b/d/e) oder null.</summary>
    public static string? Schluessel(string? fachrichtung) =>
        Abschnitt(fachrichtung) is { } a ? Einrichten.Abschnitte.First(x => x.Abschnitt == a).Schluessel : null;

    /// <summary>Ist die Frage faellig? Keine Fachrichtung gespeichert, kein Merker, Lernstand und Einstellungen lesbar.</summary>
    public static bool Faellig(bool gewaehlt, JsonObject werte, bool lesbar) =>
        lesbar && !gewaehlt && !Einstellungen.PythonBool(werte[GefragtSchluessel]);

    /// <summary>Warnhinweis noetig: AE, DPA oder DV und deren Abschnitt ist auf diesem Geraet aus.</summary>
    public static bool WarnungNoetig(string? fachrichtung, JsonObject werte) =>
        Schluessel(fachrichtung) is { } s && !Einstellungen.PythonBool(werte[s]);

    /// <summary>"Ja": schaltet genau den einen Abschnitt ein, sonst nichts.</summary>
    public static void Einschalten(JsonObject werte, string fachrichtung)
    {
        if (Schluessel(fachrichtung) is { } s)
        {
            werte[s] = true;
        }
    }

    /// <summary>N7 mit Fachrichtung (PruefungTexte.Fachrichtungen) und Abschnitt (SECTION_OPTION_TEXT).</summary>
    public static string WarnungText(string fachrichtung)
    {
        var name = PruefungTexte.Fachrichtungen.First(f => f.Wert == fachrichtung).Text;
        var teile = HilfeTexte.Warnung.Split("%s");
        return teile[0] + name + teile[1] + RahmenplanTexte.AbschnittText(Abschnitt(fachrichtung)!) + teile[2];
    }

    /// <summary>Gespeicherter Merker ausbildung_zuletzt (nur ein bekannter Wert) oder null.</summary>
    public static string? Zuletzt(JsonObject werte) =>
        werte[ZuletztSchluessel] is JsonValue v && v.TryGetValue<string>(out var text) && Array.IndexOf(Pruefung.Ausbildung.Werte, text) >= 0 ? text : null;

    /// <summary>
    /// Fachrichtung von anderswo: eine Fachrichtung ist gespeichert und der Merker weicht ab (auch: Merker fehlt,
    /// Auflage P4-11). Danach setzt der Aufrufer den Merker (<see cref="Merken"/>), mit oder ohne Hinweis.
    /// </summary>
    public static bool VonAnderswo(string? gespeichert, JsonObject werte) => gespeichert is not null && Zuletzt(werte) != gespeichert;

    /// <summary>Merker ausbildung_zuletzt setzen (nach Antwort, Wechsel in den Optionen oder Vergleich).</summary>
    public static void Merken(JsonObject werte, string fachrichtung) => werte[ZuletztSchluessel] = fachrichtung;

    /// <summary>Frage beantwortet oder uebersprungen: Merker gefragt (Datum), bei einer Wahl auch zuletzt (kein zweiter Hinweis).</summary>
    public static void Beantwortet(JsonObject werte, string? fachrichtung, DateOnly heute)
    {
        werte[GefragtSchluessel] = Einrichten.Iso(heute);
        if (fachrichtung is not null)
        {
            Merken(werte, fachrichtung);
        }
    }

    /// <summary>
    /// Antwort auf die Frage (Knopf 0 = "Speichern", 1 = "Überspringen", -1 = Esc/Zurueck-Geste): nur
    /// "Speichern" mit einer Wahl speichert die Fachrichtung; sonst bleibt der Lernstand unberuehrt (es gilt
    /// weiter SI). Merker ist die Aenderung fuer einstellungen.json (gefragt, bei Erfolg auch zuletzt; misslingt
    /// das Speichern, aendert sie nichts).
    /// Gewaehlt = die gespeicherte Fachrichtung (dann folgt der Warnhinweis), Fehler = Speichern ging nicht (N9).
    /// </summary>
    public static (string? Gewaehlt, bool Fehler, Action<JsonObject> Merker) FrageAntwort(LernstandSchreiber schreiber, int knopf,
        string? wahl, DateOnly heute)
    {
        if (knopf != 0 || wahl is null)
        {
            return (null, false, w => Beantwortet(w, null, heute));
        }
        // Misslingt das Speichern, bleibt auch der Merker weg: die Frage kommt beim naechsten Start wieder
        // (sonst gaelte still SI, Gegenpruefung W4 F4)
        return Pruefung.Ausbildung.Waehlen(schreiber, wahl) ? (wahl, false, w => Beantwortet(w, wahl, heute)) : (null, true, _ => { });
    }

    /// <summary>Antwort auf den Warnhinweis: nur "Ja" (Knopf 0) aendert etwas, "Nein" (1) und Esc (-1) nichts.</summary>
    public static Action<JsonObject>? WarnungAntwort(int knopf, string fachrichtung) =>
        knopf == 0 ? w => Einschalten(w, fachrichtung) : null;

    /// <summary>
    /// Startablauf (Plan 2.3): Bei nur lesbaren Einstellungen oder im Start ohne Bedienung erscheint nichts von selbst.
    /// Der Rundgang wird vor der Frage bestimmt. Die Frage hat Vorrang vor dem Vergleich des Merkers (sie setzt ihn).
    /// </summary>
    public static StartSchritte Start(bool nurAnzeige, bool autoModus, bool lernstandLesbar, JsonObject werte, bool hatDaten,
        bool gewaehlt, string? gespeichert)
    {
        if (nurAnzeige || autoModus)
        {
            return StartSchritte.Keine;
        }
        var frage = Faellig(gewaehlt, werte, lernstandLesbar);
        return new StartSchritte(Einrichten.RundgangFaellig(werte, hatDaten), frage, !frage && VonAnderswo(gespeichert, werte));
    }

    /// <summary>
    /// Der ganze Startablauf (Plan 2.3): lesen, Schritte bestimmen (der Rundgang VOR der Frage), dann Frage
    /// bzw. Vergleich des Merkers, danach der Rundgang. Die Fenster liefert der Aufrufer.
    /// </summary>
    public static async Task<StartSchritte> Ablauf(bool nurAnzeige, bool autoModus, Func<JsonObject> werte, Func<Task<StartLage>> lesen,
        Func<Task> frage, Func<Task> anderswo, Func<Task> rundgang)
    {
        if (nurAnzeige || autoModus)
        {
            return StartSchritte.Keine;
        }
        var lage = await lesen();
        var schritte = Start(nurAnzeige, autoModus, lage.Lesbar, werte(), lage.HatDaten, lage.Gewaehlt, lage.Gespeichert);
        if (schritte.Frage)
        {
            await frage();
        }
        else if (schritte.VonAnderswo)
        {
            await anderswo();
        }
        if (schritte.Rundgang)
        {
            await rundgang();
        }
        return schritte;
    }
}
