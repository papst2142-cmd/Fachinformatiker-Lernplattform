using System.Globalization;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Patch A 0.29.1 (Weisung P10-3): an Festtagen wechseln Autos und Gestalten mit der Uhr, ohne Hausbesuch, nach und
/// nach und nur unsichtbar. Je Fest die Uhr ueber die Daemmerung und zurueck: der Uebergang beginnt in der Stadt, die
/// Gesamtansicht bleibt im Uebergang in den Grenzen (160 000 Dreiecke, 40 Aufrufe), danach ist der richtige Satz ganz
/// da (Satz, Zahl der Gestalten bzw. Autos), keine Strecke brauchte die Hoechstzeit.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> P10P1SpielAsync(MainView ansicht)
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
            Messwerte.Notiere("  FEHLER: Spiel Patch A 0.29.1: Spielseite nicht gebaut");
            return 1;
        }
        var vorher = seite.Spiel;
        var mitGl = false;
        try
        {
            mitGl = await GlAbwartenAsync(seite);
            if (!mitGl)
            {
                Messwerte.Notiere("Spiel: Festverkehr mit der Uhr ohne GL nicht gemessen");
                return funde;
            }
            var flaeche = seite.Flaeche;
            foreach (var (fest, jz, _) in W11Feste)
            {
                Jahreszeiten.Probe(jz);
                Festzeit.Probe(fest);
                var ab = Tageslicht.DaemmerungAb(jz);
                StadtFlaeche.UhrSanft = false;
                Tageslicht.FesteUhr = ab - 30;
                Frisch(seite);
                seite.Flaeche.KameraAufFigur();
                await flaeche.NaechstesBildAsync();
                await flaeche.NaechstesBildAsync();
                StadtFlaeche.UhrSanft = true;
                foreach (var (uhr, richtung) in new[] { (ab, "hin"), (ab - 30, "zurueck") })
                {
                    var von = flaeche.Satz;
                    var soll = FestVerkehr.Fuer(fest, uhr, jz);
                    var punkt = fest + " " + richtung + " (" + Stadt.Uhrzeit(uhr) + ")";
                    if (flaeche.AktuelleKamera is not { } kamera || seite.Spiel is not { } spiel)
                    {
                        Pruefe(punkt + ": Kamera", false, "keine Kamera");
                        continue;
                    }
                    // in der Gesamtansicht beginnen (alles im Bild: getauscht wird nur am Kartenrand, der Uebergang
                    // laeuft also sichtbar lange); beide Saetze in ihren Zeichenaufrufen, die Grenzen gelten
                    var abstand = kamera.Abstand;
                    kamera.Zoomen(kamera.Abstand / Kamera.AbstandMax);
                    kamera.Setzen(spiel.Szene.Breite / 2, spiel.Szene.Tiefe / 2);
                    await flaeche.NaechstesBildAsync();
                    Tageslicht.FesteUhr = uhr;
                    FestUebergang? u = null;
                    for (var i = 0; i < 240 && u is null; i++)
                    {
                        await flaeche.NaechstesBildAsync();
                        u = flaeche.Uebergang;
                    }
                    if (u is null)
                    {
                        Pruefe(punkt + ": Uebergang beginnt ohne Hausbesuch", false, string.Create(inv, $"Satz {flaeche.Satz} (soll {soll})"));
                        kamera.Zoomen(kamera.Abstand / abstand);
                        kamera.Setzen(spiel.FigurX, spiel.FigurZ);
                        continue;
                    }
                    Pruefe(punkt + ": Uebergang beginnt ohne Hausbesuch", u.Alt == von && u.Neu == soll && von != soll, string.Create(inv, $"{u.Alt} -> {u.Neu}"));
                    await flaeche.NaechstesBildAsync();
                    var aufrufe = flaeche.LetzteZeichenaufrufe;
                    var dreiecke = flaeche.LetzteDreiecke;
                    var gemischt = flaeche.Uebergang is not null;
                    Pruefe(punkt + ": Gesamtansicht im Uebergang", gemischt && aufrufe <= GesamtAufrufeGrenze && dreiecke <= GesamtDreieckeGrenze && aufrufe > 0,
                        string.Create(inv, $"{aufrufe} Zeichenaufrufe, {dreiecke} Dreiecke (Grenzen {GesamtAufrufeGrenze}, {GesamtDreieckeGrenze}), Uebergang laeuft: {gemischt}"));
                    kamera.Zoomen(kamera.Abstand / abstand);
                    kamera.Setzen(spiel.FigurX, spiel.FigurZ);
                    // nah an der Figur fahren lassen (feste Fahrzeit je Bild), bis der neue Satz ganz da ist
                    StadtFlaeche.AutoSchritt = 0.25;
                    var bilder = 0;
                    while (flaeche.Uebergang is not null && bilder < 800)
                    {
                        await flaeche.NaechstesBildAsync();
                        bilder++;
                    }
                    StadtFlaeche.AutoSchritt = 0;
                    var zahl = FestVerkehr.Plaetze(soll).Count;
                    var autos = flaeche.Verkehr?.Autos.Count ?? 0;
                    var gruende = string.Join(", ", u.Tausche.GroupBy(t => t.Grund).Select(g => string.Create(inv, $"{g.Key} {g.Count()}")));
                    Pruefe(punkt + ": neuer Satz ganz da", flaeche.Uebergang is null && flaeche.Satz == soll
                        && (soll == FestVerkehr.Satz.Autos ? flaeche.Gestalten is null && autos == 6 : flaeche.Gestalten?.Count == zahl),
                        string.Create(inv, $"Satz {flaeche.Satz}, {flaeche.Gestalten?.Count ?? 0} Gestalten, {autos} Fahrzeuge, nach {u.Fahrzeit:F1} s Fahrzeit ({gruende})"));
                    Pruefe(punkt + ": ohne Hoechstzeit", u.Tausche.All(t => t.Grund != FestUebergang.Grund.Hoechstzeit) && u.Fahrzeit <= FestUebergang.Hoechstzeit,
                        string.Create(inv, $"{u.Tausche.Count(t => t.Grund == FestUebergang.Grund.Hoechstzeit)} Strecken nach der Hoechstzeit {FestUebergang.Hoechstzeit:F0} s"));
                }
            }
        }
        finally
        {
            StadtFlaeche.AutoSchritt = 0;
            StadtFlaeche.UhrSanft = false;
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
