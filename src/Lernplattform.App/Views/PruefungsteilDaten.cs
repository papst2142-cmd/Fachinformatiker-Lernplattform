using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Welle A 1.15.0 (Weisungen P12-406/P12-407): was die Seiten der Pruefungsteile zeigen, je Sicht (Fachrichtung) einmal im
/// Hintergrund gerechnet (<see cref="TeilUeben.Inhalte"/> fuer Teil 1 und die drei schriftlichen Bereiche, Saetze im echten
/// Format je Teil). Die Zahlen stehen dadurch beim ersten Oeffnen sofort; der Stand (Lernstand) kommt bei jedem Zeigen neu.
/// </summary>
public static class PruefungsteilDaten
{
    public sealed record Daten(Fachrichtung Fachrichtung, IReadOnlyDictionary<string, TeilInhalte> Teile,
        IReadOnlyDictionary<string, IReadOnlyList<EchtSatz>> Saetze, int Testprojekte);

    private static readonly Dictionary<string, Task<Daten?>> Gemerkt = new(StringComparer.Ordinal);
    private static readonly Lock Sperre = new();

    /// <summary>Daten der aktuellen Sicht (gemerkt, bis sich die Inhalte aendern).</summary>
    public static Task<Daten?> Fuer(string sicht, Task<Lerninhalte?> inhalte)
    {
        lock (Sperre)
        {
            if (!Gemerkt.TryGetValue(sicht, out var aufgabe) || (aufgabe.IsCompleted && aufgabe.Result is null))
            {
                aufgabe = Task.Run(() => RechnenAsync(sicht, inhalte));
                Gemerkt[sicht] = aufgabe;
            }
            return aufgabe;
        }
    }

    /// <summary>Nach dem Laden oder Entfernen eines Pakets: neu rechnen.</summary>
    public static void Vergessen()
    {
        lock (Sperre)
        {
            Gemerkt.Clear();
        }
    }

    /// <summary>Im Hintergrund vorab rechnen (nach dem Start bzw. nach einem Wechsel der Fachrichtung).</summary>
    public static void Vorab() => _ = Fuer(Zustand.Sicht, Zustand.Inhalte);

    private static async Task<Daten?> RechnenAsync(string sicht, Task<Lerninhalte?> laden)
    {
        try
        {
            if (await laden is not { } inhalte)
            {
                return null;
            }
            var gewicht = await Zustand.Gewicht;
            var fachrichtung = Fachrichtung.Nach(sicht);
            var klausur = new KlausurInhalte(inhalte, Zustand.Umbenennungen);
            var teile = new Dictionary<string, TeilInhalte>(StringComparer.Ordinal);
            var saetze = new Dictionary<string, IReadOnlyList<EchtSatz>>(StringComparer.Ordinal);
            foreach (var seite in Pruefungsteile.ReiterSeiten)
            {
                var rolle = Pruefungsteile.Rolle(seite);
                teile[seite] = TeilUeben.Inhalte(inhalte, gewicht, klausur, fachrichtung, rolle);
                saetze[seite] = rolle == Pruefungsteile.Wiso ? [] : Zustand.EchtBestand.Fuer(rolle, fachrichtung.Id);
            }
            return new Daten(fachrichtung, teile, saetze, inhalte.Testprojekte.Count);
        }
        catch (Exception fehler)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben(fehler);
            return null;
        }
    }

    /// <summary>Kennungen der Saetze eines Teils, die man schon einmal im echten Format bearbeitet hat.</summary>
    public static HashSet<string> Bearbeitet(Fachrichtung fachrichtung, string seite, IEnumerable<KlausurEintrag> verlauf)
    {
        var art = EchtArten.Art(fachrichtung, Pruefungsteile.Rolle(seite));
        return [.. EchtArten.Nur(verlauf).Where(e => e.Art == art && e.Daten is System.Text.Json.Nodes.JsonObject)
            .Select(e => Json.Text(((System.Text.Json.Nodes.JsonObject)e.Daten!)["satz"]) ?? "")];
    }
}
