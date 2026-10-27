using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W6 (Weisung P8-12, Teilplan P8-11 Abschnitt 4): Festzeiten. Der Start-Test setzt die Festzeit ueber
/// <see cref="Festzeit.Probe"/> (nie ueber das Datum; <c>lauf.sh</c> setzt <see cref="Festzeit.Schalter"/> fest auf einen
/// Tag ohne Festzeit) und misst je Zustand (ohne Festzeit, Ostern, Halloween, Weihnachten, Silvester):
/// "Spiel Stadt aufbauen (&lt;Fest&gt;)" (frisches Spiel bis zum Bild), "Spiel Blick auf den Park (&lt;Fest&gt;)" (Kamera auf
/// den Park bis zum Bild), "Spiel Stadt -&gt; Kaufhaus (&lt;Fest&gt;)" (Betreten bis zum Bild), alle wiederholbar mit den
/// Grenzen der Spielwechsel (ueber 400 ms vermerkt, rot ab 1 000 ms); je Festzeit dazu die Gesamtansicht (Zeichenaufrufe,
/// Dreiecke, Zeit je Bild, Grenzen wie in W1). Prueft: Stadt und Kaufhaus tragen die Festzeit, mit Festzeit steht
/// Deko, ohne keine. Danach wieder ohne Probe.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> P8W6FestzeitenAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Festzeiten: Spielseite nicht gebaut");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            // ohne GL zaehlt der Fund schon in der 3D-Stichprobe (macOS: ohne 3D, kein Fehler)
            Messwerte.Notiere("Spiel: Festzeiten ohne GL nicht gemessen");
            return 0;
        }
        var feste = seite.Spiel.Feste;
        Pruefe("Festzeiten geladen", feste is not null, feste is null ? "nein" : "ja");
        if (feste is null)
        {
            return funde;
        }
        // Mitte des Parks: die festen Plaetze von Weihnachten (Baum, Geschenke, Schneemann)
        var park = SpielDaten.ListeOderLeer(SpielDaten.Objekt(SpielDaten.Objekt(SpielDaten.Objekt(feste["staedte"])["standard"])[Festzeit.Weihnachten]), "plaetze")
            .Select(SpielDaten.Objekt).Select(p => (X: (float)Py.F(p["x"]), Z: (float)Py.F(p["z"]))).ToList();
        var (parkX, parkZ) = (park.Average(p => p.X), park.Average(p => p.Z));
        // danach wieder das Spiel von vorher (die Layoutpruefung sieht dieselbe Spielseite wie ohne diese Messung)
        var vorher = seite.Spiel;
        try
        {
            foreach (var fest in ((string?[])[null]).Concat(Festzeit.Alle))
            {
                var name = fest ?? "ohne";
                Festzeit.Probe(fest);
                async Task<double?> Aufbauen()
                {
                    var uhr = Stopwatch.StartNew();
                    Frisch(seite);
                    await seite.Flaeche.NaechstesBildAsync();
                    await ansicht.BildFertigAsync();
                    return uhr.Elapsed.TotalMilliseconds;
                }
                funde += Ergebnis("Spiel Stadt aufbauen (" + name + ")",
                    await Messwiederholung.MessenAsync(await Aufbauen(), Abbruch, Aufbauen, null, Task.Delay));
                var spiel = seite.Spiel!;
                var stadtDeko = spiel.Szene.FestGesetzt.Count;
                Pruefe("Stadt in der Festzeit " + name, spiel.Szene.Fest == fest && (fest is null ? stadtDeko == 0 : stadtDeko > 0),
                    string.Create(inv, $"Festzeit {spiel.Szene.Fest ?? "ohne"}, {stadtDeko} Stuecke"));

                // Park: Kamera hin (bis zum Bild), dann zurueck zur Figur
                var kamera = seite.Flaeche.AktuelleKamera;
                async Task<double?> Park()
                {
                    var uhr = Stopwatch.StartNew();
                    kamera?.Setzen(parkX, parkZ);
                    await seite.Flaeche.NaechstesBildAsync();
                    await ansicht.BildFertigAsync();
                    return uhr.Elapsed.TotalMilliseconds;
                }
                async Task ZurFigur()
                {
                    kamera?.Setzen(spiel.FigurX, spiel.FigurZ);
                    await seite.Flaeche.NaechstesBildAsync();
                }
                funde += Ergebnis("Spiel Blick auf den Park (" + name + ")",
                    await Messwiederholung.MessenAsync(await Park(), Abbruch, Park, ZurFigur, Task.Delay));
                await ZurFigur();

                if (fest is not null)
                {
                    Messwerte.Notiere("Spiel: Gesamtansicht in der Festzeit " + name);
                    funde += await GesamtansichtAsync(seite);
                }

                // Kaufhaus: hingehen, bis zur Oeffnung warten, Betreten bis zum Bild
                spiel.GeheZu(Kleidung.Laden);
                spiel.SofortAnkommen();
                if (!spiel.KannBetreten && spiel.KannWarten)
                {
                    spiel.Warten();
                }
                if (!spiel.KannBetreten)
                {
                    Pruefe("Kaufhaus betretbar (" + name + ")", false, spiel.Zeile);
                    continue;
                }
                await seite.Flaeche.NaechstesBildAsync();
                async Task<double?> Hinein()
                {
                    var uhr = Stopwatch.StartNew();
                    spiel.Betreten();
                    await seite.Flaeche.NaechstesBildAsync();
                    await ansicht.BildFertigAsync();
                    return uhr.Elapsed.TotalMilliseconds;
                }
                async Task Hinaus()
                {
                    spiel.Verlassen();
                    await seite.Flaeche.NaechstesBildAsync();
                }
                funde += Ergebnis("Spiel Stadt -> Kaufhaus (" + name + ")",
                    await Messwiederholung.MessenAsync(await Hinein(), Abbruch, Hinein, Hinaus, Task.Delay));
                var innenDeko = spiel.Innen?.FestGesetzt.Count ?? -1;
                Pruefe("Kaufhaus in der Festzeit " + name,
                    spiel.Innen?.Ort == Kleidung.Laden && spiel.Innen.Fest == fest && (fest is null ? innenDeko == 0 : innenDeko > 0),
                    string.Create(inv, $"Ort {spiel.Innen?.Ort ?? "keiner"}, Festzeit {spiel.Innen?.Fest ?? "ohne"}, {innenDeko} Stuecke"));
                if (spiel.ImInnenraum)
                {
                    await Hinaus();
                }
            }
        }
        finally
        {
            Festzeit.ProbeEnde();
            seite.SpielSetzen(vorher);
            await seite.Flaeche.NaechstesBildAsync();
        }
        return funde;
    }
}
