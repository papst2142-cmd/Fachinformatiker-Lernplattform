using System.Globalization;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.73.0 (Weisung P12-239, Sammelliste 335): selbst mit dem Bus fahren, in Nicos Stadt 1 mit echtem Tippen. Auf das
/// Wartehaeuschen einer Haltestelle tippen (die Figur geht hin und wartet), warten bis der Bus haelt und der Knopf
/// „Einsteigen“ unten steht, ihn druecken (der Bus faehrt waehrenddessen nicht ab), im Bus mitfahren (der Bus ist gezeichnet,
/// die Figur nicht, die Kamera folgt), an der naechsten Haltestelle ohne Druecken vorbei (Gegenprobe), dann „Aussteigen“
/// druecken und an der darauf folgenden Haltestelle neben ihr stehen; die Fahrt ist eine Zeile <c>stadt_gelaufen</c> mit
/// <c>mit: bus</c>. Im Bus gehen Antippen und Ortsliste nicht. Der Verkehr laeuft dabei mit festem Schritt (schneller als
/// echt), die Pruefung reicht ueber zwei ganze Runden des Busses plus Grenze.
/// Layout: Zeile und Knopf der drei Zustaende (Einsteigen, Aussteigen, Haelt an der naechsten) in jeder Schriftstufe und Groesse.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Fester Schritt des Verkehrs je Bild in dieser Pruefung (Sekunden Fahrzeit).</summary>
    private const double MitfahrtSchritt = 0.25;

    /// <summary>Sichtbarer Knopf unten mit dieser Beschriftung (oder null).</summary>
    private static Button? KnopfMit(SpielSeite seite, string text) =>
        seite.Ebene.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible
            && ((b.Content as TextBlock)?.Text ?? b.Content?.ToString()) == text);

    private static async Task<int> MitfahrtAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Mitfahrt " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Mitfahrt " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Mitfahrt: Spielseite nicht gebaut");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Spiel: Mitfahrt nicht geprueft (ohne 3D faehrt kein Bus; die Ortsliste mit „Mit dem Bus“ bleibt)");
            return 0;
        }
        var vorher = seite.Spiel;
        var schrittVorher = StadtFlaeche.AutoSchritt;
        try
        {
            var spiel = Frisch(seite);
            if (FassungVon(spiel) == 0 || spiel.Linie is not { } linie || spiel.Szene.Haltestellen.Count < 2)
            {
                Pruefe("Stadt 1 mit Haltestellen", false, "Fassung " + FassungVon(spiel) + ", " + spiel.Szene.Haltestellen.Count + " Haltestellen");
                return funde;
            }
            await ansicht.BildFertigAsync();
            StadtFlaeche.AutoSchritt = MitfahrtSchritt;
            async Task<bool> Bis(Func<bool> fertig, double sekunden)
            {
                for (var i = 0; i < sekunden / MitfahrtSchritt; i++)
                {
                    if (fertig())
                    {
                        return true;
                    }
                    await seite.Flaeche.NaechstesBildAsync();
                }
                return fertig();
            }
            var verkehr = seite.Flaeche.Verkehr;
            var busse = verkehr?.Autos.Where(a => a.Nummer >= Buslinie.ErsteNummer).ToList() ?? [];
            var runde = busse.Select(a => (a.Laenge / a.Tempo) + (a.Halte.Count * StadtSpiel.WartetMitFigur)).DefaultIfEmpty(0).Max() + 20;
            Pruefe("Bus faehrt", busse.Count > 0, busse.Count + " Busse, Runde " + runde.ToString("F0", inv) + " s");

            // 1. auf das Wartehaeuschen tippen: die Figur geht hin und wartet
            var h = spiel.Szene.Haltestellen.First(x => linie.Halte.Any(l => l.Haltestelle == x.Nr));
            var kamera = seite.Flaeche.AktuelleKamera!;
            kamera.Setzen(h.Haeuschen.X, h.Haeuschen.Y);
            await seite.Flaeche.NaechstesBildAsync();
            var boden = new Vector3(h.Haeuschen.X, StadtSzene.Hoehe.Gehweg, h.Haeuschen.Y);
            if (kamera.Bildpunkt(boden, (float)seite.Flaeche.Bounds.Width, (float)seite.Flaeche.Bounds.Height) is { } p)
            {
                seite.Flaeche.Tippen(new Avalonia.Point(p.X, p.Y));
            }
            // die Figur geht in Echtzeit (der feste Schritt gilt nur fuer den Verkehr): bis 120 s Uhrzeit warten
            var gehen = System.Diagnostics.Stopwatch.StartNew();
            while (spiel.Geht && gehen.Elapsed.TotalSeconds < 120)
            {
                await seite.Flaeche.NaechstesBildAsync();
            }
            var angekommen = !spiel.Geht;
            await seite.Flaeche.NaechstesBildAsync();
            Pruefe("Hingehen und warten", angekommen && spiel.WartetAn == h.Nr && spiel.ImFreien,
                string.Create(inv, $"Haltestelle {h.Nr}, wartet an {spiel.WartetAn?.ToString(inv) ?? "keiner"}, Figur {spiel.FigurX:F2}/{spiel.FigurZ:F2} ({spiel.Zeile})"));

            // 2. der Bus kommt: Knopf „Einsteigen“ unten
            var da = await Bis(() => spiel.BusZumEinsteigen is not null, runde);
            await ansicht.BildFertigAsync();
            var einsteigen = KnopfMit(seite, SpielProbeTexte.Einsteigen);
            Pruefe("Bus kommt, Knopf Einsteigen", da && einsteigen is not null, da ? "Bus " + spiel.BusZumEinsteigen + ", Knopf " + (einsteigen is null ? "fehlt" : "da") : "kein Bus in einer Runde");
            if (einsteigen is null || verkehr is null)
            {
                return funde;
            }
            var bus = verkehr.Autos.Single(a => a.Nummer == spiel.BusZumEinsteigen);
            var weg = bus.Weg;
            einsteigen.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var abgefahren = false;
            var imBus = await Bis(() =>
            {
                abgefahren |= spiel.Mitfahrt == StadtSpiel.MitfahrtStufe.SteigtEin && bus.Weg != weg;
                return spiel.SitztImBus;
            }, 10);
            await ansicht.BildFertigAsync();
            Pruefe("Einsteigen", imBus && !abgefahren && seite.Flaeche.BusMitFigur && KnopfMit(seite, SpielProbeTexte.Aussteigen) is not null,
                $"im Bus {imBus}, Bus waehrend des Einsteigens abgefahren {abgefahren}, Figur im Bus {seite.Flaeche.BusMitFigur}");

            // 3. Fahrt: Kamera folgt dem Bus; ohne Druecken vorbei an der naechsten Haltestelle (Gegenprobe)
            await Bis(() => bus.HaeltAn is null, 30);
            // Kamera: 3 Sekunden in Echtzeit fahren (mit festem Schritt faehrt der Bus schneller, als die Kamera weich folgt)
            StadtFlaeche.AutoSchritt = 0;
            var echt = System.Diagnostics.Stopwatch.StartNew();
            while (echt.Elapsed.TotalSeconds < 3)
            {
                await seite.Flaeche.NaechstesBildAsync();
            }
            StadtFlaeche.AutoSchritt = MitfahrtSchritt;
            var abstand = Vector2.Distance(new Vector2(kamera.ZielX, kamera.ZielZ), bus.Lage().Ort);
            Pruefe("Kamera folgt dem Bus", abstand < 1.5f, string.Create(inv, $"Abstand Kamera zum Bus {abstand:F2}"));
            var zeilen = spiel.Ablauf.Stand.Verlauf.Count;
            spiel.GeheZu(spiel.FigurX + 3, spiel.FigurZ);
            var ortsliste = spiel.MitBusZu(spiel.Szene.Orte[0].Id);
            Pruefe("Antippen und Ortsliste im Bus", !spiel.Geht && ortsliste != StadtGrund.Ok && spiel.SitztImBus && spiel.Ablauf.Stand.Verlauf.Count == zeilen,
                spiel.Zeile);
            var erste = 0;
            var vorbei = await Bis(() =>
            {
                if (bus.HaeltAn is { } nr && erste == 0)
                {
                    erste = nr;
                }
                return erste != 0 && bus.HaeltAn is null;
            }, runde);
            Pruefe("Ohne Druecken weiter (Gegenprobe)", vorbei && spiel.SitztImBus && spiel.LetzteMitfahrt is null,
                $"an Haltestelle {erste} vorbei, im Bus {spiel.SitztImBus}");

            // 4. „Aussteigen“: der Bus haelt an der naechsten Haltestelle, die Figur steht neben ihr
            var front = (bus.Weg + bus.Halb) % bus.Laenge;
            var halte = bus.Halte.OrderBy(x => x.Front).ToList();
            var soll = (halte.FirstOrDefault(x => x.Front > front + 0.1f) is { Haltestelle: > 0 } n ? n : halte[0]).Haltestelle;
            var uhr = spiel.Jetzt.Uhr;
            KnopfMit(seite, SpielProbeTexte.Aussteigen)?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await ansicht.BildFertigAsync();
            Pruefe("Knopf nach dem Druecken", KnopfMit(seite, SpielProbeTexte.HaeltGleich) is not null && spiel.AussteigenGewuenscht, spiel.Zeile);
            var aus = await Bis(() => spiel.Mitfahrt == StadtSpiel.MitfahrtStufe.Keine, runde);
            await ansicht.BildFertigAsync();
            var ziel = spiel.Szene.Haltestellen.FirstOrDefault(x => x.Nr == soll);
            var platz = ziel is null ? default : StadtSpiel.Wartplatz(ziel);
            var e = spiel.Ablauf.Stand.Verlauf[^1];
            var fahrt = spiel.LetzteMitfahrt;
            Pruefe("Aussteigen an der naechsten Haltestelle", aus && fahrt?.Aus == soll && ziel is not null
                && Vector2.Distance(new Vector2(spiel.FigurX, spiel.FigurZ), platz) < 0.01f && spiel.WartetAn == soll && !seite.Flaeche.BusMitFigur,
                string.Create(inv, $"ausgestiegen an {fahrt?.Aus}, soll {soll}, Figur {spiel.FigurX:F2}/{spiel.FigurZ:F2}"));
            Pruefe("Fahrt im Spielstand", fahrt is { } f && e.Art == Stadt.Gelaufen && Py.Gleich(SpielDaten.Get(e.Daten, Stadt.Mit, null), Stadt.MitBus)
                && spiel.Jetzt.Uhr == uhr + Py.Int(e.Daten["minuten"]) && Py.Int(e.Daten["minuten"]) == Math.Min(f.Minuten, Stadt.UhrSteht - uhr),
                string.Create(inv, $"{fahrt?.Strecke:F1} Feld, {fahrt?.Minuten} Min., Uhr {Stadt.Uhrzeit(uhr)} -> {Stadt.Uhrzeit(spiel.Jetzt.Uhr)}"));
        }
        finally
        {
            StadtFlaeche.AutoSchritt = schrittVorher;
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    /// <summary>Layout: Zeile und Knopf der drei Zustaende der Fahrt (nur gezeigt, siehe <see cref="StadtSpiel.KnopfZeigenFuerTests"/>).</summary>
    private static async IAsyncEnumerable<string> MitfahrtAnsichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            P7W1AnsichtFehler.Add("spiel/bus-einsteigen: Spielseite nicht gebaut");
            yield break;
        }
        var vorher = seite.Spiel;
        var spiel = Frisch(seite);
        foreach (var (aktion, name) in new[]
        {
            (StadtSpiel.AktionEinsteigen, "spiel/bus-einsteigen"), (StadtSpiel.AktionAussteigen, "spiel/bus-aussteigen"),
            (StadtSpiel.AktionHaeltGleich, "spiel/bus-haelt-gleich"),
        })
        {
            spiel.KnopfZeigenFuerTests(aktion);
            await ansicht.BildFertigAsync();
            P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, name));
            yield return name;
        }
        seite.SpielSetzen(vorher);
        await ansicht.BildFertigAsync();
    }
}
