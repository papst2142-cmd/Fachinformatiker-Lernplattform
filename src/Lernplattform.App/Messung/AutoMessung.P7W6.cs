using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 7, W6 (Weisung P7-11): die Spielansichten ueber der Stadt (Platzwahl, Figur, Uebersicht,
/// Meine Reise, Erfolge, Meilenstein, Ticket-Bretter der 9 Typen, Firma mit 9 Reitern) fuer die
/// Layoutpruefung. Beim ersten Durchgang legt die Tafel im leeren Datenordner des Start-Tests einen
/// Durchgang mit eigener Firma an (nur angehaengte Zeilen). Danach zeigt die Seite wieder die Probe.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Die 9 Aufgabentypen (TYPES in fisi_game) fuer je ein Ticket-Brett.</summary>
    private static readonly string[] P7W6Typen =
        ["auswahl", "zuordnung", "bauteile", "bestellung", "rack", "formular", "terminal", "diagnose", "wartung"];

    private static async IAsyncEnumerable<string> P7W6Ansichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Tafel is not { } tafel)
        {
            P7W1AnsichtFehler.Add("spiel/tafel: Spielansichten nicht gebaut");
            yield break;
        }
        tafel.OhneFenster = true;
        seite.TafelZeigen();
        tafel.ProbePlatzwahl();
        await ansicht.BildFertigAsync();
        yield return "spiel/platzwahl";
        string? fehler = null;
        try
        {
            tafel.ProbeFirmaAnlegen();
        }
        catch (Exception e) when (e is Core.Spiel.SpielFehler or Core.Spiel.SpielBelegtAusnahme or InvalidOperationException)
        {
            fehler = e.Message;
        }
        if (fehler is not null)
        {
            P7W1AnsichtFehler.Add("spiel/firma: Durchgang mit Firma nicht angelegt (" + fehler + ")");
            seite.SpielSetzen(Frisch(seite));
            yield break;
        }
        seite.TafelZeigen();
        tafel.ProbeFigur();
        await ansicht.BildFertigAsync();
        yield return "spiel/figur";
        tafel.ProbeUebersicht();
        await ansicht.BildFertigAsync();
        yield return "spiel/uebersicht";
        foreach (var reiter in new[] { "rueckblick", "erfolge" })
        {
            tafel.ProbeReise(reiter);
            await ansicht.BildFertigAsync();
            yield return "spiel/reise-" + reiter;
        }
        if (tafel.ProbeMeilenstein())
        {
            await ansicht.BildFertigAsync();
            yield return "spiel/meilenstein";
        }
        else
        {
            P7W1AnsichtFehler.Add("spiel/meilenstein: kein Meilenstein freigeschaltet");
        }
        foreach (var typ in P7W6Typen)
        {
            if (!tafel.ProbeTicket(typ))
            {
                P7W1AnsichtFehler.Add("spiel/ticket-" + typ + ": kein Ticket dieses Typs");
                continue;
            }
            await ansicht.BildFertigAsync();
            yield return "spiel/ticket-" + typ;
        }
        var reiterListe = Core.Spiel.Betrieb.Reiter(tafel.Sitzung.Stand);
        if (reiterListe.Count != 9)
        {
            P7W1AnsichtFehler.Add("spiel/firma: " + reiterListe.Count + " Reiter statt 9");
        }
        foreach (var (id, _) in reiterListe)
        {
            tafel.ProbeFirma(id);
            await ansicht.BildFertigAsync();
            yield return "spiel/firma-" + id;
        }
        tafel.AllesSchliessen();
        seite.SpielSetzen(Frisch(seite));
        await ansicht.BildFertigAsync();
    }
}
