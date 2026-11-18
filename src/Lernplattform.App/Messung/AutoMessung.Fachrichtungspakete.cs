using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pakete;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.91.0 (Weisung P12-300 Punkte 1, 3, 5, 6; Nico 3a 4a): Fachrichtungs-Pakete im Start-Test mit der Testquelle und den
/// echten Testdateien <c>lernen_ae_1</c>, <c>lernen_dpa_1</c>, <c>lernen_dv_1</c>. Je Fachrichtung: der Hinweis oben in der
/// Lernansicht nach einer Suche (nur auf Knopfdruck), der Messpunkt „Fachrichtungs-Paket einsetzen &lt;FR&gt;“ (Pruefen bis die
/// Sicht steht, ohne Download, einmalig und streng 400 ms), danach dieselben Inhalte wie vorher (das Paket ist der heutige Stand),
/// die Sperre beim offenen Pruefungssatz, Entfernen und zurueck zum Grundstand. Der Lernstand bleibt bytegleich.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> FachrichtungsPaketeAsync(MainView ansicht)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Fachrichtungs-Pakete: " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Fachrichtungs-Pakete " + punkt + ": " + wert);
                funde++;
            }
        }
        var daten = Core.Daten.Ablage.Datenordner();
        var lernstand = Path.Combine(daten, Core.Daten.Lernstand.DateiName);
        string Lernstand() => File.Exists(lernstand) ? Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(lernstand))) : "";
        var quelle = Path.Combine(Path.GetTempPath(), "lernplattform_frpakete_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(quelle);
        foreach (var fr in FachrichtungsPaket.Fachrichtungen)
        {
            var datei = Path.Combine(EchtOrdner, Paket.Dateiname(FachrichtungsPaket.Kennung(fr), 1));
            if (!File.Exists(datei))
            {
                Pruefe("Testdatei " + fr, false, datei + " fehlt");
                return funde;
            }
            File.Copy(datei, Path.Combine(quelle, Path.GetFileName(datei)));
        }
        var sichtVorher = Zustand.Sicht;
        var seiteVorher = ansicht.AktuelleSeite;
        var gebaut = ansicht.GebauteSeiten.Keys.ToList();
        var vorherLernstand = Lernstand();
        Environment.SetEnvironmentVariable(PaketQuelle.Umgebung, quelle);
        PaketDienst.NeuFuerTests();
        try
        {
            var v = PaketDienst.Verwaltung;
            await v.SuchenAsync();
            foreach (var fr in FachrichtungsPaket.Fachrichtungen)
            {
                var name = PaketTexte.Fr(fr);
                var kennung = FachrichtungsPaket.Kennung(fr);
                // die Lernansicht wird nach dem Wechsel frisch gebaut (wie nach einem Wechsel in den Optionen)
                await ansicht.ZeigeAsync(Seiten.Optionen);
                ansicht.SichtAnwenden(fr);
                await Zustand.Inhalte;
                // was der Wechsel und das letzte Entfernen auf der Oberflaeche angestossen haben, erst ablaufen lassen
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
                var vorher = await Zustand.AlleInhalte is { } a ? Abdruck(a, fr) : "";
                // Nico 3a: nach der Suche einmal oben in der Lernansicht der Hinweis auf den neuen Stand (nichts geladen)
                await ansicht.ZeigeAsync(Seiten.Dashboard);
                if (ansicht.GebauteSeiten.GetValueOrDefault(Seiten.Dashboard) is DashboardSeite dashboard)
                {
                    await dashboard.Geladen;
                    await ansicht.BildFertigAsync();
                    var hinweis = dashboard.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "FachrichtungsHinweis");
                    var texte = hinweis is null ? [] : TexteVon([hinweis]);
                    Pruefe(name + " Hinweis oben in der Lernansicht", hinweis is not null && texte.Any(t => t.StartsWith("Neue Inhalte für " + name + ": Fachrichtungs-Paket " + name, StringComparison.Ordinal))
                        && v.Fachrichtungen().Count == 0, hinweis is null ? "kein Hinweis (Sicht " + Zustand.Sicht + ", neue Staende "
                        + string.Join(",", v.NeueFachrichtungsStaende().Select(n => n.Fachrichtung + ":" + n.Angebot.Stand)) + ", offen "
                        + (Programm.FachrichtungsPaketDienst.Hinweis()?.Fachrichtung ?? "-") + ")" : string.Join(" | ", texte));
                    dashboard.Neu();
                    await dashboard.Geladen;
                    await ansicht.BildFertigAsync();
                    Pruefe(name + " Hinweis nur einmal", !dashboard.GetLogicalDescendants().OfType<Control>().Any(c => c.Name == "FachrichtungsHinweis"), "nach dem Neuzeichnen");
                }
                // Messpunkt (Nico 4a): Pruefen und Einsetzen der geladenen Datei bis die Sicht steht, ohne Download, einmalig, streng
                var ziel = v.Ablage.Ladedatei(kennung, 1);
                File.Copy(Path.Combine(quelle, Paket.Dateiname(kennung, 1)), ziel, true);
                var uhr = Stopwatch.StartNew();
                var ergebnis = v.Ablage.Einsetzen(ziel, kennung, 1, Aktualisierung.Aktualisierung.Anzeige);
                if (!ergebnis.Gut)
                {
                    Pruefe(name + " einsetzen", false, ergebnis.Grund ?? "?");
                    continue;
                }
                await Zustand.PaketeNeu(fr, uhr);
                await ansicht.BildFertigAsync();
                funde += Ergebnis("Fachrichtungs-Paket einsetzen " + name + " (Pruefen bis die Sicht steht, einmalig)",
                    Messwiederholung.Einmal(Zustand.EinsetzenMs.TryGetValue(fr, out var ms) ? ms : null, Geraetegrenze));
                var mit = await Zustand.AlleInhalte is { } b ? Abdruck(b, fr) : "";
                Pruefe(name + " gleiche Inhalte mit Paket", mit == vorher && mit.Length > 0 && Zustand.Aktuell.Pakete.ContainsKey(fr),
                    mit == vorher ? "gleich (" + vorher + ")" : "ANDERS: vorher " + vorher + ", mit Paket " + mit);
                var zeile = v.Zeilen().FirstOrDefault(z => z.Kennung == kennung);
                Pruefe(name + " Zeile", zeile?.ZustandText == PaketTexte.Format(PaketTexte.ZustandGeladen, 1) && zeile.Name == "Fachrichtungs-Paket " + name,
                    (zeile?.Name ?? "keine Zeile") + ": " + zeile?.ZustandText);
                // Sperre: ein offener Pruefungssatz dieser Fachrichtung
                var satz = Zustand.EchtBestand.Saetze.FirstOrDefault(s => s.Fachrichtung == fr);
                Zustand.Aendere(w => w[EchtAblage.Schluessel] = new System.Text.Json.Nodes.JsonObject { ["art"] = "echt_probe", ["satz"] = satz?.Kennung });
                var gesperrt = v.EntfernenGesperrt(kennung);
                Pruefe(name + " Entfernen gesperrt bei offenem Satz", satz is not null && gesperrt == PaketTexte.Format(PaketTexte.FrEntfernenGesperrt, name) && !v.Entfernen(kennung),
                    (satz?.Kennung ?? "kein Satz") + ": " + (gesperrt ?? "NICHT gesperrt"));
                Zustand.Aendere(w => w.Remove(EchtAblage.Schluessel));
                // Entfernen: danach gilt der Grundstand, dieselben Inhalte
                Pruefe(name + " entfernen", v.Entfernen(kennung), "Paket entfernt");
                await Zustand.PaketeNeu(fr);
                var nachher = await Zustand.AlleInhalte is { } c ? Abdruck(c, fr) : "";
                Pruefe(name + " Grundstand nach dem Entfernen", nachher == vorher && !Zustand.Aktuell.Pakete.ContainsKey(fr)
                    && v.Zeilen().FirstOrDefault(z => z.Kennung == kennung)?.ZustandText == PaketTexte.Format(PaketTexte.ZustandEingebautNeu, 1), nachher);
            }
            Pruefe("Lernstand bytegleich", Lernstand() == vorherLernstand, Lernstand() == vorherLernstand ? "gleich" : "GEAENDERT");
            Pruefe("Datenordner ohne Pakete", !Directory.Exists(Path.Combine(daten, PaketAblage.OrdnerName, "lernen_ae")), "kein Paket mehr im Datenordner");
        }
        finally
        {
            Environment.SetEnvironmentVariable(PaketQuelle.Umgebung, null);
            PaketDienst.NeuFuerTests();
            try
            {
                Directory.Delete(quelle, true);
            }
            catch (IOException)
            {
            }
            ansicht.SichtAnwenden(sichtVorher);
            await Zustand.Inhalte;
            // die vorher gebauten Seiten wieder bauen (wie P9W2), spaetere Schritte erwarten sie
            foreach (var id in gebaut.Where(id => !ansicht.GebauteSeiten.ContainsKey(id)))
            {
                await ansicht.ZeigeAsync(id);
                switch (ansicht.GebauteSeiten.GetValueOrDefault(id))
                {
                    case KarteikartenSeite karten:
                        await karten.Geladen;
                        break;
                    case TrainerSeite trainer:
                        await trainer.Geladen;
                        break;
                    case SzenarioSeite szenarien:
                        await szenarien.Geladen;
                        break;
                }
            }
            await ansicht.ZeigeAsync(seiteVorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    /// <summary>Kurzer Abdruck der Sicht einer Fachrichtung (Zahlen je Art und Pruefsumme ueber Nummern und Texte).</summary>
    private static string Abdruck(Lerninhalte alle, string fr)
    {
        var sicht = alle.Fuer(fr);
        var teile = sicht.Karteikarten.Concat(sicht.Quizfragen).Concat(sicht.Ap2Szenarien).Concat(sicht.Testprojekte)
            .Select(e => e.Nummer + "|" + e.Daten.ToJsonString());
        var summe = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", teile))))[..12];
        return $"{sicht.Karteikarten.Count}/{sicht.Quizfragen.Count}/{sicht.Ap2Szenarien.Count}/{sicht.Testprojekte.Count}/"
            + $"{sicht.NeueFachfragen.Count(f => f.Zugehoerigkeit == fr)} {summe}";
    }
}
