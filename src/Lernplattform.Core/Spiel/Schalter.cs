using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W7b (Weisung P7-13): Bank und Amt als Orte der Stadt mit Schalter und Person (nur C#,
/// ohne Vorlage in Python). Es gibt keine neuen Ereignisse: in der Bank nimmt die Firma Kredite auf
/// und loest sie ab (take_loan/repay_loan wie die Firmen-Tafel, Logik Wirtschaft.cs), auf dem Amt
/// wird die Firma gegruendet (found_firm); die Umsatzsteuer zeigt das Amt nur an, sie bucht weiter der
/// Feierabend. Neu ist nur die Regel: am Schalter geht es nur im geoeffneten Ort, in dem die Figur
/// steht (Tagesuhr der Stadt). Grundriss, Schalter und Person stehen in <c>innenraeume.json</c>.
/// </summary>
public static class Schalter
{
    public const string Bank = "bank";
    public const string Amt = "amt";

    /// <summary>Die Orte mit Schalter (Ids wie in stadt.json neue_orte).</summary>
    public static readonly IReadOnlyList<string> Orte = [Bank, Amt];

    public const string NurInBank = "Das geht nur in der geöffneten Bank.";
    public const string NurAufAmt = "Das geht nur auf dem geöffneten Amt.";

    public static string Name(string ort) => ort == Bank ? "Bank" : "Amt";

    /// <summary>Steht die Figur im geoeffneten Ort <paramref name="ort"/>? (leer = ja, sonst der Grund)</summary>
    public static string ImOrt(Stadt stadt, Spielstand stand, string ort) =>
        Orte.Contains(ort) && stadt.Stand(stand).Ort == ort && stadt.Betreten(stand) == StadtGrund.Ok ? "" : ort == Bank ? NurInBank : NurAufAmt;

    private static void Pruefen(Stadt stadt, Spielstand stand, string ort)
    {
        if (ImOrt(stadt, stand, ort) is { Length: > 0 } grund)
        {
            throw new SpielFehler(grund);
        }
    }

    /// <summary>Bank: Kredit aufnehmen (wie take_loan, Ereignis kredit).</summary>
    public static List<(string Art, PyObjekt Daten)> KreditAufnehmen(Stadt stadt, Spielstand stand, object? summe, object? laufzeit, object? paket = null)
    {
        Pruefen(stadt, stand, Bank);
        return SpielAktionen.KreditAufnehmen(stand, summe, laufzeit, paket);
    }

    /// <summary>Bank: laufenden Kredit abloesen (wie repay_loan, Ereignis kredit_abgeloest).</summary>
    public static List<(string Art, PyObjekt Daten)> KreditAbloesen(Stadt stadt, Spielstand stand, object? kredit)
    {
        Pruefen(stadt, stand, Bank);
        return SpielAktionen.KreditAbloesen(stand, kredit);
    }

    /// <summary>Amt: Firma gruenden (wie found_firm, Ereignis gegruendet).</summary>
    public static List<(string Art, PyObjekt Daten)> Gruenden(Stadt stadt, Spielstand stand, string? name)
    {
        Pruefen(stadt, stand, Amt);
        return SpielAktionen.Gruenden(stand, name);
    }

    /// <summary>Fehler bei Bank und Amt in stadt.json und innenraeume.json (leer = alles gut).</summary>
    public static List<string> PruefenInhalt(Stadt stadt, PyObjekt? innenraeume)
    {
        var fehler = new List<string>();
        foreach (var ort in Orte)
        {
            if (stadt.Regel(ort) is not { } regel)
            {
                fehler.Add("Ort fehlt: " + ort);
                continue;
            }
            if (Py.Str(Get(regel, "rolle")) != "schalter")
            {
                fehler.Add(ort + ": Rolle muss schalter sein");
            }
            if (stadt.Oeffnungszeit(ort) is null)
            {
                fehler.Add(ort + ": keine Oeffnungszeit");
            }
            if ((innenraeume is null ? null : Get(innenraeume, ort)) is not PyObjekt raum)
            {
                fehler.Add(ort + ": kein Grundriss in innenraeume.json");
                continue;
            }
            if (ListeOderLeer(raum, "personen").Count == 0)
            {
                fehler.Add(ort + ": keine Person am Schalter");
            }
            var gebaeude = Objekt(raum["gebaeude"]);
            if (!Liste(gebaeude["raeume"]).Select(Objekt).SelectMany(r => ListeOderLeer(r, "deko")).Select(Objekt).Any(d => Py.Str(Get(d, "typ")) == "tresen"))
            {
                fehler.Add(ort + ": kein Schalter (tresen)");
            }
        }
        return fehler;
    }
}
