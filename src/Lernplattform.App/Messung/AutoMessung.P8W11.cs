using System.Globalization;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W11 (Weisung P8-45): Festzeiten-Verkehr und Stadt-Feinschliff. Je Fest (Halloween abends, Weihnachten
/// abends, Ostern tagsueber) die Gestalten statt der Autos: Satz, Anzahl, sie bewegen sich, die Gesamtansicht bleibt
/// in den Grenzen (100 000 Dreiecke, 20 Aufrufe). Der Satz wechselt mit der Uhr (Halloween tagsueber Autos, ab der
/// Daemmerung Gestalten). 21 Laternen (16 einfach, 5 doppelt) im eigenen Netz.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Feste der Welle W11 mit Jahreszeit und Uhrzeit (Spielminuten), zu der die Gestalten unterwegs sind.</summary>
    private static readonly (string Fest, Jahreszeit Jahreszeit, long Uhr)[] W11Feste =
    [
        (Festzeit.Halloween, Jahreszeit.Herbst, 1260),
        (Festzeit.Weihnachten, Jahreszeit.Winter, 1200),
        (Festzeit.Ostern, Jahreszeit.Fruehling, 720),
    ];

    private static async Task<int> P8W11SpielAsync(MainView ansicht)
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
            Messwerte.Notiere("  FEHLER: Spiel W11: Spielseite nicht gebaut");
            return 1;
        }
        var vorher = seite.Spiel;
        var mitGl = false;
        try
        {
            mitGl = await GlAbwartenAsync(seite);
            if (!mitGl)
            {
                Messwerte.Notiere("Spiel: Festzeiten-Verkehr ohne GL nicht gemessen");
                return funde;
            }
            var flaeche = seite.Flaeche;
            foreach (var (fest, jz, uhr) in W11Feste)
            {
                Jahreszeiten.Probe(jz);
                Festzeit.Probe(fest);
                Tageslicht.FesteUhr = uhr;
                var spiel = Frisch(seite);
                await flaeche.NaechstesBildAsync();
                await flaeche.NaechstesBildAsync();
                var soll = FestVerkehr.Fuer(fest, uhr, jz);
                var anzahl = FestVerkehr.Plaetze(soll).Count;
                Pruefe(fest + ": Gestalten", flaeche.Satz == soll && soll != FestVerkehr.Satz.Autos && flaeche.Gestalten?.Count == anzahl,
                    string.Create(inv, $"Satz {flaeche.Satz} (soll {soll}), {flaeche.Gestalten?.Count ?? 0} Gestalten (soll {anzahl}), {Stadt.Uhrzeit(uhr)}"));
                Pruefe(fest + ": Laternen", spiel.Szene.Laternen.Count == Nachtlicht.Anzahl && spiel.Szene.Laternen.Count(l => l.Doppelt) == Nachtlicht.Doppelt,
                    string.Create(inv, $"{spiel.Szene.Laternen.Count} Laternen, {spiel.Szene.Laternen.Count(l => l.Doppelt)} doppelt"));
                if (flaeche.Verkehr is { } verkehr)
                {
                    var wege = verkehr.Autos.Select(a => a.Weg).ToList();
                    StadtFlaeche.AutoSchritt = 1.0 / 60;
                    for (var i = 0; i < 30; i++)
                    {
                        await flaeche.NaechstesBildAsync();
                    }
                    StadtFlaeche.AutoSchritt = 0;
                    var gegangen = verkehr.Autos.Where((a, i) => a.Weg != wege[i]).Count();
                    Pruefe(fest + ": Gestalten bewegen sich (30 Bilder zu 1/60 s)", gegangen > 0, string.Create(inv, $"{gegangen} von {verkehr.Autos.Count} Strecken"));
                }
                else
                {
                    Pruefe(fest + ": Gestalten bewegen sich", false, "kein Verkehr");
                }
                Messwerte.Notiere("Spiel: Gesamtansicht " + fest + " mit Gestalten, Paket-Netz und Laternen");
                funde += await GesamtansichtAsync(seite);
                seite.Flaeche.KameraAufFigur();
                await flaeche.NaechstesBildAsync();
                Messwerte.Notiere(string.Create(inv, $"Spiel: {fest} nah (Info): {flaeche.LetzteZeichenaufrufe} Aufrufe, {flaeche.LetzteDreiecke} Dreiecke, {flaeche.LetzteZeichenzeitMs:F1} ms"));
            }

            // Halloween tagsueber Autos, ab der Daemmerung Gestalten: der Satz wechselt mit der Uhr
            Jahreszeiten.Probe(Jahreszeit.Herbst);
            Festzeit.Probe(Festzeit.Halloween);
            Tageslicht.FesteUhr = 720;
            Frisch(seite);
            await flaeche.NaechstesBildAsync();
            await flaeche.NaechstesBildAsync();
            var tag = flaeche.Satz;
            var autos = flaeche.Verkehr?.Autos.Count ?? 0;
            Tageslicht.FesteUhr = Tageslicht.DaemmerungAb(Jahreszeit.Herbst);
            await flaeche.NaechstesBildAsync();
            await flaeche.NaechstesBildAsync();
            Pruefe("Satz wechselt mit der Uhr", tag == FestVerkehr.Satz.Autos && autos == 6 && flaeche.Satz == FestVerkehr.Satz.Halloween,
                string.Create(inv, $"12:00 {tag} ({autos} Autos), {Stadt.Uhrzeit(Tageslicht.DaemmerungAb(Jahreszeit.Herbst))} {flaeche.Satz} ({flaeche.Gestalten?.Count ?? 0} Gestalten)"));
        }
        finally
        {
            StadtFlaeche.AutoSchritt = 0;
            Tageslicht.FesteUhr = null;
            Jahreszeiten.ProbeEnde();
            Festzeit.ProbeEnde();
            seite.SpielSetzen(vorher);
            seite.Flaeche.KameraAufFigur();
            if (mitGl)
            {
                await seite.Flaeche.NaechstesBildAsync();
            }
            else
            {
                await ansicht.BildFertigAsync();
            }
        }
        return funde;
    }
}
