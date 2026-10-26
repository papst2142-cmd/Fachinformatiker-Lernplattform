using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W3 (Weisung P8-5, L1): die Spielseite nach einem Abgleich auffrischen. Im leeren Datenordner des
/// Start-Tests legt die Tafel wie in W6 einen Durchgang mit eigener Firma an; dann kommt eine Zeile wie von
/// einem anderen Geraet dazu (eine Wartezeit von 0 Minuten in der Stadt, aendert nichts am Spiel) und die
/// Seite frischt auf wie nach einem Abgleich. Messpunkt (wiederholbar, Grenze 400 ms): "Spiel: Spielseite nach
/// Abgleich auffrischen" bis zum Bild. Prueft: danach steht die neue Zeile im Spielstand; bei offenem Ticket
/// frischt die Seite nicht auf (wie Python), erst nach dem Schliessen des Tickets.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> P8W3MesspunkteAsync(MainView ansicht)
    {
        var funde = 0;
        void Fehler(string text)
        {
            Messwerte.Notiere("  FEHLER: " + text);
            funde++;
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Tafel is not { } tafel)
        {
            Fehler("Spiel nach Abgleich: Spielansichten nicht gebaut");
            return funde;
        }
        tafel.OhneFenster = true;
        seite.TafelZeigen();
        try
        {
            tafel.ProbeFirmaAnlegen();
        }
        catch (Exception e) when (e is SpielFehler or SpielBelegtAusnahme or InvalidOperationException)
        {
            Fehler("Spiel nach Abgleich: Durchgang nicht angelegt (" + e.Message + ")");
            return funde;
        }
        seite.TafelZeigen();
        tafel.AllesSchliessen();
        await ansicht.BildFertigAsync();
        var verwaltung = tafel.Sitzung.Verwaltung;
        var lauf = verwaltung.Lauf;
        var nummer = 0;
        int Zeilen() => verwaltung.Stand.Verlauf.Count;
        void VomAnderenGeraet()
        {
            tafel.Sitzung.Schlange.Leeren();
            var daten = new PyObjekt { { "tag", verwaltung.Stand.Tag }, { "ort", "zuhause" }, { "minuten", 0L }, { "messung", (long)++nummer } };
            if (Zustand.Schreiber.SpielEreignisSpeichern(Stadt.Gewartet, PythonJson.Standard(daten), "Messung", lauf) is null)
            {
                Fehler("Spiel nach Abgleich: Zeile des anderen Geraets nicht geschrieben");
            }
        }

        var vorher = Zeilen();
        VomAnderenGeraet();
        funde += Ergebnis("Spiel: Spielseite nach Abgleich auffrischen (bis zum Bild)",
            await BildMessenAsync(ansicht, seite.NachAbgleich, wiederholbar: true, grenze: Geraetegrenze));
        Messwerte.Notiere("Spiel nach Abgleich: Zeilen im Spielstand " + vorher + " -> " + Zeilen());
        if (Zeilen() != vorher + 1)
        {
            Fehler("Spiel nach Abgleich: die Zeile des anderen Geraets fehlt im Spielstand");
        }

        // Offenes Ticket: nicht auffrischen (refresh_after_sync: not self.views["game"].ticket), erst nach dem Schliessen
        if (!tafel.ProbeTicket("auswahl"))
        {
            Fehler("Spiel nach Abgleich: kein Ticket zum Oeffnen");
        }
        else
        {
            var offen = Zeilen();
            VomAnderenGeraet();
            seite.NachAbgleich();
            await ansicht.BildFertigAsync();
            if (Zeilen() != offen || !tafel.TicketOffen)
            {
                Fehler("Spiel nach Abgleich: bei offenem Ticket aufgefrischt");
            }
            tafel.Zurueck();
            await ansicht.BildFertigAsync();
            if (Zeilen() != offen + 1)
            {
                Fehler("Spiel nach Abgleich: nach dem Schliessen des Tickets nicht nachgeholt");
            }
        }
        tafel.AllesSchliessen();
        seite.SpielSetzen(Frisch(seite));
        await ansicht.BildFertigAsync();
        return funde;
    }
}
