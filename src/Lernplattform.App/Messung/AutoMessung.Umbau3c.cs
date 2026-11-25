using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 1.10.0 (Weisung P12-381, Nico 3c mit Hinweis 1a): nach einem Umbau der Wohnung steht ein Moebel, dessen Platz nicht
/// mehr passt, im Karton, und beim ersten Betreten steht einmal der Hinweis mit Nicos Wortlaut. Die Probe baut eine Wohnung mit
/// einem Regal auf dem Platz der Startmatratze (wie eine im Werkzeug gezogene Wand, nur auf einer Kopie der Inhalte), betritt
/// sie: Hinweis da, Text gleich <see cref="Wohnung.UmbauText"/>, Matratze im Karton. Zweites Betreten: kein Hinweis (einmal).
/// Gegenprobe: dieselbe Wohnung ohne Umbau zeigt keinen Hinweis. Spiel und Merker werden danach zurueckgesetzt.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> Umbau3cAsync(MainView ansicht)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Umbau-Karton " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Umbau-Karton " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is not { } vorher)
        {
            Messwerte.Notiere("  FEHLER: Spiel Umbau-Karton: Spielseite nicht gebaut");
            return 1;
        }
        var merker = Programm.Zustand.Werte[Wohnung.Merker]?.DeepClone();
        try
        {
            foreach (var umgebaut in new[] { false, true })
            {
                Programm.Zustand.Aendere(w => w.Remove(Wohnung.Merker));
                var inhalt = (PyObjekt)Py.Kopie(vorher.Stadt.Inhalt)!;
                if (umgebaut)
                {
                    var bleibe = Wohnung.Bleibe("apartment", inhalt)!;
                    var raum = ((List<object?>)((PyObjekt)bleibe["gebaeude"]!)["raeume"]!).OfType<PyObjekt>().First(r => Py.Str(r["id"]) == "wohnraum");
                    if (SpielDaten.Get(raum, "deko") is not List<object?>)
                    {
                        Py.Setzen(raum, "deko", new List<object?>());
                    }
                    ((List<object?>)raum["deko"]!).Add(new PyObjekt { { "typ", "regal" }, { "x", 0.6 }, { "y", 0.6 }, { "w", 1.0 }, { "h", 1.0 } });
                }
                var stadt = new Stadt(inhalt, vorher.Stadt.Daten) { Fassung = vorher.Stadt.Fassung };
                var spiel = SpielStart.Neu(stadt, vorher.Grafik, vorher.Innenraeume, vorher.GeschaefteDaten, vorher.Feste,
                    Festzeit.Heute, Jahreszeiten.Heute);
                seite.SpielSetzen(spiel);
                await ansicht.BildFertigAsync();
                var name = umgebaut ? "umgebaut" : "Gegenprobe ohne Umbau";
                if (!spiel.KannBetreten || !spiel.Betreten())
                {
                    Pruefe(name + " Wohnung", false, spiel.Zeile.Replace('\n', ' '));
                    continue;
                }
                seite.Beschriften();
                await ansicht.BildFertigAsync();
                var soll = Wohnung.UmbauText(spiel.Ablauf.Stand);
                if (!umgebaut)
                {
                    Pruefe(name, !seite.UmbauHinweisSichtbar && soll is null,
                        seite.UmbauHinweisSichtbar ? "Hinweis da (falsch): " + seite.UmbauHinweisText : "kein Hinweis");
                    continue;
                }
                var imKarton = Wohnung.ImKarton(spiel.Ablauf.Stand).Any(k => Py.Str(k.Stueck) == "start-matratze");
                Pruefe("erstes Betreten", seite.UmbauHinweisSichtbar && soll is not null && seite.UmbauHinweisText == Wohnung.UmbauTitel + " " + soll && imKarton,
                    (seite.UmbauHinweisSichtbar ? "Hinweis: " + seite.UmbauHinweisText : "KEIN Hinweis") + (imKarton ? " | Matratze im Karton" : " | Matratze NICHT im Karton"));
                // einmal: hinaus und wieder hinein
                spiel.Verlassen();
                seite.Beschriften();
                await ansicht.BildFertigAsync();
                if (!spiel.KannBetreten || !spiel.Betreten())
                {
                    Pruefe("zweites Betreten", false, spiel.Zeile.Replace('\n', ' '));
                    continue;
                }
                seite.Beschriften();
                await ansicht.BildFertigAsync();
                Pruefe("zweites Betreten (einmal)", !seite.UmbauHinweisSichtbar,
                    seite.UmbauHinweisSichtbar ? "Hinweis noch einmal da" : "kein Hinweis");
            }
        }
        finally
        {
            Programm.Zustand.Aendere(w =>
            {
                w.Remove(Wohnung.Merker);
                if (merker is not null)
                {
                    w[Wohnung.Merker] = merker;
                }
            });
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }
}
