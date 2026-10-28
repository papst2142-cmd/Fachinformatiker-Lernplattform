using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Spiel.Ton;
using Lernplattform.Core.Texte;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W9 (Weisung P8-32): Jahreszeiten-Faerbung und Hoerprobe. Spielteil: die Stadt je Jahreszeit (Szene in
/// der gesetzten Jahreszeit, Bauzeit der Netze gegenueber dem Sommer als Info-Zeile, Gesamtansicht im Winter mit
/// Weihnachten mit den Grenzen der Gesamtansicht). Lernteil: jeden Ton-Regler verstellen, je Regler genau ein Beispiel
/// (im stillen Dienst gezaehlt), derselbe Regler stapelt nicht.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Alle Netze der Stadt einmal bauen (wie die 3D-Flaeche beim Laden), Zeit in Millisekunden.</summary>
    private static double NetzeBauenMs(StadtSzene szene)
    {
        var uhr = Stopwatch.StartNew();
        szene.NetzBauen();
        szene.GruenNetzBauen();
        foreach (var haus in szene.Haeuser)
        {
            szene.HausNetz(haus);
        }
        return uhr.Elapsed.TotalMilliseconds;
    }

    private static async Task<int> P8W9JahreszeitenAsync(MainView ansicht)
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
            Messwerte.Notiere("  FEHLER: Spiel Jahreszeiten: Spielseite nicht gebaut");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Spiel: Jahreszeiten ohne GL nicht gemessen");
            return 0;
        }
        var vorher = seite.Spiel;
        try
        {
            var szenen = new Dictionary<Jahreszeit, StadtSzene>();
            foreach (var zeit in Enum.GetValues<Jahreszeit>())
            {
                Jahreszeiten.Probe(zeit);
                var spiel = Frisch(seite);
                await seite.Flaeche.NaechstesBildAsync();
                await ansicht.BildFertigAsync();
                Pruefe("Stadt in der Jahreszeit " + zeit, spiel.Szene.Jahreszeit == zeit && seite.Flaeche.LetzteDreiecke > 0,
                    string.Create(inv, $"Jahreszeit {spiel.Szene.Jahreszeit}, {seite.Flaeche.LetzteDreiecke} Dreiecke, {seite.Flaeche.LetzteZeichenaufrufe} Zeichenaufrufe"));
                szenen[zeit] = spiel.Szene;
            }
            // Bauzeit der Netze je Jahreszeit gegenueber dem Sommer (Weisung P8-32: hoechstens 10 ms mehr), abwechselnd
            // gemessen, je die kuerzeste von 7 Runden nach 2 zum Vorwaermen; nur Info (Rechner der CI schwanken)
            var beste = Enum.GetValues<Jahreszeit>().ToDictionary(z => z, _ => double.MaxValue);
            for (var runde = 0; runde < 9; runde++)
            {
                foreach (var (zeit, szene) in szenen)
                {
                    var ms = NetzeBauenMs(szene);
                    if (runde >= 2)
                    {
                        beste[zeit] = Math.Min(beste[zeit], ms);
                    }
                }
            }
            var sommer = beste[Jahreszeit.Sommer];
            Messwerte.Notiere("Spiel: Bauzeit der Netze (Info, Ziel hoechstens 10 ms mehr als im Sommer): "
                + string.Join(", ", beste.Select(b => string.Create(inv, $"{b.Key} {b.Value:F1} ms ({b.Value - sommer:+0.0;-0.0;0.0})"))));

            // Winter mit Weihnachten: die meisten Dreiecke (Schnee, Fest-Deko) in der Gesamtansicht
            Jahreszeiten.Probe(Jahreszeit.Winter);
            Festzeit.Probe(Festzeit.Weihnachten);
            var winter = Frisch(seite);
            await seite.Flaeche.NaechstesBildAsync();
            Pruefe("Winter mit Weihnachten", winter.Szene.Jahreszeit == Jahreszeit.Winter && winter.Szene.Fest == Festzeit.Weihnachten,
                $"Jahreszeit {winter.Szene.Jahreszeit}, Festzeit {winter.Szene.Fest ?? "ohne"}");
            Messwerte.Notiere("Spiel: Gesamtansicht im Winter mit Weihnachten");
            funde += await GesamtansichtAsync(seite);
        }
        finally
        {
            Jahreszeiten.ProbeEnde();
            Festzeit.ProbeEnde();
            seite.SpielSetzen(vorher);
            // die Kamera wieder auf die Figur des Spiels von vorher (die Layoutpruefung sieht dieselbe Ansicht wie ohne diese Messung)
            seite.Flaeche.KameraAufFigur();
            await seite.Flaeche.NaechstesBildAsync();
        }
        return funde;
    }

    /// <summary>Lernteil: jeden Ton-Regler verstellen und zurueck; je Regler ein Beispiel, kein Stapeln (danach wie vorher).</summary>
    private static async Task<int> P8W9HoerprobeAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Optionen: " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Optionen " + punkt + ": " + wert);
                funde++;
            }
        }
        await ansicht.ZeigeAsync(Seiten.Optionen);
        if (ansicht.GebauteSeiten.GetValueOrDefault(Seiten.Optionen) is not OptionenSeite optionen)
        {
            Messwerte.Notiere("  FEHLER: Optionen W9: Optionen nicht gebaut");
            return 1;
        }
        if (optionen.Klappe(Optionen.Ton) is { } klappe)
        {
            klappe.IsExpanded = true;
        }
        await ansicht.BildFertigAsync();
        Pruefe("Satz zur Hörprobe", TonTexte.ReglerSatz.EndsWith("Beim Verstellen hörst du ein kurzes Beispiel.", StringComparison.Ordinal), TonTexte.ReglerSatz);
        var startsVorher = TonDienst.ProbeStarts;
        var zeilen = new List<string>();
        var ok = true;
        foreach (var schluessel in (string[])[TonEinstellungen.Gesamt, TonEinstellungen.Musik, TonEinstellungen.Umgebung])
        {
            if (optionen.TonRegler.GetValueOrDefault(schluessel) is not { } regler || Hoerprobe.Fuer(schluessel) is not { } probe)
            {
                Pruefe("Hörprobe " + schluessel, false, "Regler fehlt");
                continue;
            }
            var anlass = Hoerprobe.Schluessel(probe);
            var vorher = TonDienst.Anlaesse.GetValueOrDefault(anlass);
            var wert = regler.Value;
            // zweimal verstellen (hin und zurueck): ein Beispiel, das zweite Verstellen klingt im selben weiter
            regler.Value = wert >= 50 ? wert - 10 : wert + 10;
            await ansicht.BildFertigAsync();
            regler.Value = wert;
            await ansicht.BildFertigAsync();
            var neu = TonDienst.Anlaesse.GetValueOrDefault(anlass) - vorher;
            ok &= neu == 1;
            zeilen.Add(string.Create(inv, $"{anlass} +{neu}"));
        }
        var starts = TonDienst.ProbeStarts - startsVorher;
        Pruefe("Hörprobe je Regler", ok && starts == 3 && TonDienst.ProbeStimmenAnzahl <= 2,
            string.Join(", ", zeilen) + string.Create(inv, $"; {starts} Beispiele, {TonDienst.ProbeStimmenAnzahl} Stimmen zugleich"));
        if (optionen.Klappe(Optionen.Ton) is { } zu)
        {
            zu.IsExpanded = false;
        }
        await ansicht.BildFertigAsync();
        return funde;
    }
}
