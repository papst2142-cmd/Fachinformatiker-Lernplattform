using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W7 (Weisung P8-14, Teilplan P8-13 Abschnitt 5): Schritte, Autos, Gruen. Misst "Spiel: Figur geht (16 Bilder
/// gebaut)" (die 16 Netze der Bewegung fuer das Aussehen der Figur, wiederholbar, Grenze 400 ms), zeichnet jedes
/// Schrittbild (fester Weg ueber <see cref="StadtFlaeche.ProbeBild"/>, gleiche Dreiecke wie stehend), laesst die Autos
/// mit fester Fahrzeit fahren (<see cref="StadtFlaeche.AutoSchritt"/>) und notiert Zeichenaufrufe und Dreiecke der
/// Gesamtansicht und nah (Abstand 6) ohne Festzeit und zu Weihnachten (Grenzen der Gesamtansicht wie in W1).
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> P8W7MesspunkteAsync(MainView ansicht)
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
            Messwerte.Notiere("  FEHLER: Spiel Schritte, Autos, Gruen: Spielseite nicht gebaut");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Spiel: Schritte, Autos, Gruen ohne GL nicht gemessen");
            return 0;
        }
        var vorher = seite.Spiel;
        var flaeche = seite.Flaeche;
        try
        {
            // 1. Figur geht: die 16 Netze der Bewegung (Stadt: Rennen) fuer das Aussehen und die Kleidung der Figur
            var spiel = Frisch(seite);
            await flaeche.NaechstesBildAsync();
            var aussehen = spiel.Ablauf.Stand.Profil is { } profil ? SpielDaten.Get(profil, "aussehen") as PyObjekt : null;
            double? Bauen()
            {
                var uhr = Stopwatch.StartNew();
                for (var i = 0; i < Figur.Bilder; i++)
                {
                    _ = Figur.Netz(spiel.Grafik, aussehen, 1f, spiel.Kleidung, spiel.Bewegung, i);
                }
                return uhr.Elapsed.TotalMilliseconds;
            }
            funde += Ergebnis("Spiel: Figur geht (16 Bilder gebaut)",
                await Messwiederholung.MessenAsync(Bauen(), Geraetegrenze, () => Task.FromResult(Bauen()), null, Task.Delay));

            // 2. jedes Schrittbild zeichnen: gleiche Dreiecke und Aufrufe wie stehend
            await flaeche.NaechstesBildAsync();
            var (stehendAufrufe, stehendDreiecke) = (flaeche.LetzteZeichenaufrufe, flaeche.LetzteDreiecke);
            var abweichend = new List<string>();
            for (var i = 0; i < Figur.Bilder; i++)
            {
                StadtFlaeche.ProbeBild = i;
                await flaeche.NaechstesBildAsync();
                if (flaeche.LetzteDreiecke != stehendDreiecke || flaeche.LetzteZeichenaufrufe != stehendAufrufe)
                {
                    abweichend.Add(string.Create(inv, $"Bild {i}: {flaeche.LetzteZeichenaufrufe} Aufrufe, {flaeche.LetzteDreiecke} Dreiecke"));
                }
            }
            StadtFlaeche.ProbeBild = -1;
            Pruefe("Schrittbilder wie die stehende Figur", abweichend.Count == 0,
                abweichend.Count == 0 ? string.Create(inv, $"16 Bilder, je {stehendAufrufe} Aufrufe, {stehendDreiecke} Dreiecke") : string.Join("; ", abweichend));

            // 3. Autos mit fester Fahrzeit: sechs Autos, sie fahren
            var verkehr = flaeche.Verkehr;
            Pruefe("Autos in der Stadt", verkehr?.Autos.Count == 6, string.Create(inv, $"{verkehr?.Autos.Count ?? 0} Autos, {verkehr?.Kreuzungen.Count ?? 0} Kreuzungen"));
            if (verkehr is not null)
            {
                var wege = verkehr.Autos.Select(a => a.Weg).ToList();
                StadtFlaeche.AutoSchritt = 1.0 / 60;
                for (var i = 0; i < 30; i++)
                {
                    await flaeche.NaechstesBildAsync();
                }
                StadtFlaeche.AutoSchritt = 0;
                var gefahren = verkehr.Autos.Count(a => a.Weg != wege[verkehr.Autos.IndexOf(a)]);
                Pruefe("Autos fahren (30 Bilder zu 1/60 s)", gefahren > 0, string.Create(inv, $"{gefahren} von {verkehr.Autos.Count} gefahren"));
            }

            // 4. Zeichenaufrufe und Dreiecke ohne Festzeit und zu Weihnachten, Gesamtansicht und nah
            foreach (var fest in new[] { (string?)null, Festzeit.Weihnachten })
            {
                var name = fest ?? "ohne Festzeit";
                Festzeit.Probe(fest);
                spiel = Frisch(seite);
                await flaeche.NaechstesBildAsync();
                var kamera = flaeche.AktuelleKamera!;
                var abstand = kamera.Abstand;
                kamera.Zoomen(kamera.Abstand / Kamera.AbstandMax);
                kamera.Setzen(spiel.Szene.Breite / 2, spiel.Szene.Tiefe / 2);
                await flaeche.NaechstesBildAsync();
                await flaeche.NaechstesBildAsync();
                var (gAufrufe, gDreiecke) = (flaeche.LetzteZeichenaufrufe, flaeche.LetzteDreiecke);
                Pruefe("W7 Gesamtansicht (" + name + ")", gAufrufe <= GesamtAufrufeGrenze && gDreiecke <= GesamtDreieckeGrenze && gAufrufe > 0,
                    string.Create(inv, $"{gAufrufe} Zeichenaufrufe, {gDreiecke} Dreiecke (Grenzen {GesamtAufrufeGrenze}, {GesamtDreieckeGrenze})"));
                kamera.Zoomen(kamera.Abstand / 6f);
                kamera.Setzen(spiel.FigurX, spiel.FigurZ);
                await flaeche.NaechstesBildAsync();
                await flaeche.NaechstesBildAsync();
                Messwerte.Notiere(string.Create(inv, $"Spiel: W7 nah, Abstand {kamera.Abstand:F0} ({name}): {flaeche.LetzteZeichenaufrufe} Zeichenaufrufe, {flaeche.LetzteDreiecke} Dreiecke"));
                var gruen = spiel.Szene.GruenKlein;
                Pruefe("Gruen in der Stadt (" + name + ")", gruen is { Plaetze.Count: > 0 },
                    gruen is null ? "keins" : string.Join(", ", gruen.Anzahl.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + p.Value.ToString(inv))));
                kamera.Zoomen(kamera.Abstand / abstand);
            }
        }
        finally
        {
            StadtFlaeche.ProbeBild = -1;
            StadtFlaeche.AutoSchritt = 0;
            Festzeit.ProbeEnde();
            seite.SpielSetzen(vorher);
            await flaeche.NaechstesBildAsync();
        }
        return funde;
    }
}
