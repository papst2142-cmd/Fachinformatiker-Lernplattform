using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W10 (Weisung P8-34): Hinweisfeld zuklappbar (Nico 1b), Uhr steht um 23 Uhr (2c), Tag und Nacht mit 30
/// Laternen (3b), Dashboard breit (4a/5a). Spielteil: Stadt um 07:00, 12:00, 19:00 und 21:00 (Dreiecke und
/// Zeichenaufrufe zu jeder Uhrzeit gleich, Grenzen der Gesamtansicht auch nachts im Winter mit Weihnachten), Laternen
/// im Netz der Stadt, Hinweisfeld zu und mit kurzer Meldung. Lernteil: Dashboard breit bei PC-Groesse, schmal am
/// Handy, die Ueberschriften der fuenf Kacheln auf einer Hoehe.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Ansichten der Welle W10 im Spiel je Schriftstufe und Groesse.</summary>
    public const int P8W10AnsichtenAnzahl = 4;

    /// <summary>Ansichten der Welle W10 im Lernteil je Schriftstufe (Dashboard 1280 x 720 und 1920 x 1080).</summary>
    public const int P8W10DashboardAnsichtenAnzahl = 2;

    /// <summary>Uhrzeiten der Tag-Nacht-Ansichten (Spielminuten): Morgen, Tag, Daemmerung (Herbst), Nacht.</summary>
    private static readonly long[] W10Uhren = [420, 720, 1140, 1260];

    private static async Task<int> P8W10SpielAsync(MainView ansicht)
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
            Messwerte.Notiere("  FEHLER: Spiel W10: Spielseite nicht gebaut");
            return 1;
        }
        var vorher = seite.Spiel;
        var mitGl = false;
        try
        {
            // Hinweisfeld (1b): zuklappen, gemerkt, kurze Meldung neben dem Pfeil, wieder auf
            var spiel = Frisch(seite);
            await ansicht.BildFertigAsync();
            seite.HinweisUmschalten();
            await ansicht.BildFertigAsync();
            Pruefe("Hinweisfeld zu", seite.HinweisZu && Core.Spiel.Probe.HinweisWahl.Zugeklappt(Programm.Zustand.Werte) && seite.KurzeMeldung.Length == 0,
                $"zu {seite.HinweisZu}, gemerkt {Programm.Zustand.Werte[Core.Spiel.Probe.HinweisWahl.Schluessel]?.ToString() ?? "-"}, Meldung \"{seite.KurzeMeldung}\"");
            spiel.GeheZu("moebelhaus");
            spiel.SofortAnkommen();
            await ansicht.BildFertigAsync();
            Pruefe("Hinweisfeld zu mit Meldung", seite.KurzeMeldung == spiel.Zeile && spiel.Zeile.Length > 0, "\"" + seite.KurzeMeldung.Replace('\n', ' ') + "\"");
            seite.HinweisUmschalten();
            await ansicht.BildFertigAsync();
            Pruefe("Hinweisfeld wieder offen", !seite.HinweisZu && !Core.Spiel.Probe.HinweisWahl.Zugeklappt(Programm.Zustand.Werte) && seite.KurzeMeldung.Length == 0,
                $"zu {seite.HinweisZu}");

            mitGl = await GlAbwartenAsync(seite);
            if (!mitGl)
            {
                Messwerte.Notiere("Spiel: Tag und Nacht ohne GL nicht gemessen");
                return funde;
            }
            // Laternen (3b; seit W11 21, seit P8-56 22 Laternen nach Nicos Skizze im eigenen Netz mit Ferne-Form, P8-45)
            spiel = Frisch(seite);
            var uhr = Stopwatch.StartNew();
            var plaetze = Nachtlicht.Plaetze(spiel.Szene);
            var platzMs = uhr.Elapsed.TotalMilliseconds;
            Pruefe("Laternen", spiel.Szene.Laternen.Count == Nachtlicht.Anzahl && plaetze.Count == Nachtlicht.Anzahl,
                string.Create(inv, $"{spiel.Szene.Laternen.Count} Laternen ({spiel.Szene.Laternen.Count(l => l.Doppelt)} doppelt), Plaetze suchen {platzMs:F1} ms"));
            Messwerte.Notiere(string.Create(inv, $"Spiel: Bauzeit der Netze mit Laternen und Lichtmarken (Info): {NetzeBauenMs(spiel.Szene):F1} ms"));

            // Tag und Nacht: dieselben Dreiecke und Aufrufe zu jeder Uhrzeit (nur Werte im Shader)
            seite.Flaeche.KameraAufFigur();
            var werte = new List<(long Uhr, long Dreiecke, int Aufrufe, Tageslicht.Licht Licht, double Ms)>();
            foreach (var u in W10Uhren)
            {
                Tageslicht.FesteUhr = u;
                await seite.Flaeche.NaechstesBildAsync();
                await seite.Flaeche.NaechstesBildAsync();
                var f = seite.Flaeche;
                werte.Add((u, f.LetzteDreiecke, f.LetzteZeichenaufrufe, f.LichtJetzt, f.LetzteZeichenzeitMs));
            }
            Pruefe("Tag und Nacht gleiche Dreiecke und Aufrufe",
                werte.All(w => w.Dreiecke == werte[0].Dreiecke && w.Aufrufe == werte[0].Aufrufe && w.Dreiecke > 0),
                string.Join("; ", werte.Select(w => string.Create(inv, $"{Stadt.Uhrzeit(w.Uhr)} {w.Dreiecke} Dreiecke, {w.Aufrufe} Aufrufe, Hell {w.Licht.Hell:F2}, Nacht {w.Licht.Nacht:F2}, {w.Ms:F1} ms"))));
            var jz = Jahreszeiten.Heute();
            Pruefe("Licht folgt der Uhr", werte.Select((w, i) => w.Licht == Tageslicht.Fuer(W10Uhren[i], jz)).All(x => x) && werte[1].Licht == Tageslicht.Tag,
                "Jahreszeit " + jz);

            // Gesamtansicht nachts (21:00) im Winter mit Weihnachten: die meisten Dreiecke, Grenzen wie immer
            Jahreszeiten.Probe(Jahreszeit.Winter);
            Festzeit.Probe(Festzeit.Weihnachten);
            Tageslicht.FesteUhr = 1260;
            Frisch(seite);
            await seite.Flaeche.NaechstesBildAsync();
            Messwerte.Notiere("Spiel: Gesamtansicht nachts im Winter mit Weihnachten und Laternen");
            funde += await GesamtansichtAsync(seite);
        }
        finally
        {
            Tageslicht.FesteUhr = null;
            Jahreszeiten.ProbeEnde();
            Festzeit.ProbeEnde();
            if (seite.HinweisZu)
            {
                seite.HinweisUmschalten();
            }
            seite.SpielSetzen(vorher);
            seite.Flaeche.KameraAufFigur();
            // ohne GL (macOS in der CI) zeichnet die Flaeche nie ein Bild: dann nur auf die Oberflaeche warten
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

    /// <summary>Spielansichten fuer die Layoutpruefung: Hinweis zu, zu mit Meldung, Nacht 21:00, Uhr steht (23:00).</summary>
    private static async IAsyncEnumerable<string> P8W10Ansichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            P7W1AnsichtFehler.Add("spiel/w10: Spielseite nicht gebaut");
            yield break;
        }
        var vorher = seite.Spiel;
        var spiel = Frisch(seite);
        seite.HinweisUmschalten();
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/hinweis/zu"));
        yield return "spiel/hinweis/zu";
        spiel.GeheZu("moebelhaus");
        spiel.SofortAnkommen();
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/hinweis/zu-meldung"));
        yield return "spiel/hinweis/zu-meldung";
        seite.HinweisUmschalten();
        Tageslicht.FesteUhr = 1260;
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/nacht"));
        yield return "spiel/nacht";
        Tageslicht.FesteUhr = null;
        // bis 23:00 hin und her gehen: der Hinweis "Es ist 23 Uhr" steht vor der Zeile des Orts
        for (var i = 0; i < 300 && spiel.Jetzt.Uhr < Stadt.UhrSteht; i++)
        {
            spiel.GeheZu(spiel.Jetzt.Ort == "elektromarkt" ? "talheim" : "elektromarkt");
            spiel.SofortAnkommen();
        }
        await ansicht.BildFertigAsync();
        if (!spiel.Zeile.StartsWith(StadtTexte.UhrSteht, StringComparison.Ordinal))
        {
            P7W1AnsichtFehler.Add("spiel/uhr-steht: kein Hinweis um 23 Uhr (" + spiel.Zeile.Replace('\n', ' ') + ")");
        }
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/uhr-steht"));
        yield return "spiel/uhr-steht";
        seite.SpielSetzen(vorher);
        seite.Flaeche.KameraAufFigur();
        await ansicht.BildFertigAsync();
    }

    /// <summary>Lernteil: Dashboard breit bei PC-Groesse, schmal am Handy; Ueberschriften der fuenf Kacheln auf einer Hoehe.</summary>
    private static async Task<int> P8W10DashboardAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Dashboard: " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Dashboard " + punkt + ": " + wert);
                funde++;
            }
        }
        if (TopLevel.GetTopLevel(ansicht) is not Window fenster)
        {
            Messwerte.Notiere("Dashboard: breit ohne Fenster (Handy) nicht gemessen");
            return 0;
        }
        var (b0, h0) = (fenster.Width, fenster.Height);
        try
        {
            foreach (var (name, breite, hoehe, breitSoll) in ((string, double, double, bool)[])[("schmal", SchmalBreite, SchmalHoehe, false), ("pc", PcBreite, PcHoehe, true)])
            {
                var (hb, hh) = Hoechstgroesse(fenster);
                fenster.Width = Math.Min(breite, hb);
                fenster.Height = Math.Min(hoehe, hh);
                await ansicht.ZeigeAsync(Seiten.Dashboard);
                if (ansicht.GebauteSeiten[Seiten.Dashboard] is not DashboardSeite dash)
                {
                    Pruefe("gebaut", false, "nein");
                    return funde;
                }
                await dash.Geladen;
                await ansicht.BildFertigAsync();
                await ansicht.BildFertigAsync();
                var inhalt = dash.Bounds.Width - 32;
                var soll = breitSoll && DashboardAnordnung.Breit(inhalt, Lernplattform.Design.Schrift.Faktor, Lernplattform.Design.Schrift.Gross);
                Pruefe("Anordnung " + name, dash.Breit == soll && (breitSoll || !dash.Breit),
                    string.Create(inv, $"Inhalt {inhalt:F0} Punkte, breit {dash.Breit}, Seite {dash.Hoehe:F0} Punkte hoch"));
                if (dash.Breit)
                {
                    // Ueberschriften der fuenf Kacheln (vier Ringe und Erfolgsquote) auf einer Hoehe
                    var titel = dash.KachelTitel();
                    var hoehen = titel.Select(t => t.TranslatePoint(new Point(0, 0), dash)?.Y ?? double.NaN).ToList();
                    Pruefe("Ueberschriften der Kacheln auf einer Hoehe", hoehen.Count == 5 && hoehen.All(y => !double.IsNaN(y)) && hoehen.Max() - hoehen.Min() <= 1,
                        string.Join(", ", hoehen.Select(y => y.ToString("F1", inv))));
                }
            }
        }
        finally
        {
            fenster.Width = b0;
            fenster.Height = h0;
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    /// <summary>Layoutpruefung: Dashboard bei 1280 x 720 und 1920 x 1080 (hoechstens Bildschirmgroesse), danach die Groesse von vorher.</summary>
    private static async IAsyncEnumerable<string> P8W10DashboardAnsichten(MainView ansicht)
    {
        if (TopLevel.GetTopLevel(ansicht) is not Window fenster)
        {
            yield break;
        }
        var (b0, h0) = (fenster.Width, fenster.Height);
        foreach (var (breite, hoehe) in ((double, double)[])[(1280, 720), (1920, 1080)])
        {
            var (hb, hh) = Hoechstgroesse(fenster);
            fenster.Width = Math.Min(breite, hb);
            fenster.Height = Math.Min(hoehe, hh);
            await ansicht.ZeigeAsync(Seiten.Dashboard);
            if (ansicht.GebauteSeiten[Seiten.Dashboard] is DashboardSeite dash)
            {
                await dash.Geladen;
            }
            await ansicht.BildFertigAsync();
            await ansicht.BildFertigAsync();
            yield return string.Create(CultureInfo.InvariantCulture, $"dashboard/{fenster.Width:0}x{fenster.Height:0}");
        }
        fenster.Width = b0;
        fenster.Height = h0;
        await ansicht.BildFertigAsync();
    }
}
