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
/// echten Testdateien <c>lernen_ae_2</c> (A 1.8.0, vorher <c>lernen_ae_1</c>), <c>lernen_dpa_1</c>, <c>lernen_dv_1</c>. Seit A 1.5.0 (Weisung P12-358) bringt die App
/// AE, DPA und DV nicht mehr mit: je Fachrichtung Sperre beim offenen Pruefungssatz, Entfernen (danach fehlen die Inhalte, auf der
/// Startseite genau eine Karte mit „Laden“), der Messpunkt „Fachrichtungs-Paket einsetzen &lt;FR&gt;“ (Pruefen bis die
/// Sicht steht, ohne Download, einmalig und streng 400 ms), danach dieselben Inhalte wie vorher (das Paket ist der Stand von
/// A 1.4.1). Der Lernstand bleibt bytegleich.
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
            var datei = Path.Combine(EchtOrdner, Paket.Dateiname(FachrichtungsPaket.Kennung(fr), EchtStand(fr)));
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
                // Welle A 1.5.0 (Weisung P12-358): die App bringt AE, DPA und DV nicht mehr mit; der Start-Test hat die drei Pakete
                // zu Beginn eingesetzt (FachrichtungsPaketeVorabAsync), vorher = Sicht mit Paket wie bei einem Nutzer nach „Laden“
                var vorher = await Zustand.AlleInhalte is { } a ? Abdruck(a, fr) : "";
                Pruefe(name + " Paket da (seit A 1.5.0 nur als Paket)", Zustand.Aktuell.Pakete.ContainsKey(fr) && Zustand.Eingebaut.HatPlatzhalter(fr)
                    && !Zustand.Aktuell.Fehlt(fr), "Paket " + Zustand.Aktuell.Pakete.ContainsKey(fr) + ", Platzhalter in der App " + Zustand.Eingebaut.HatPlatzhalter(fr));
                // Sperre: ein offener Pruefungssatz dieser Fachrichtung
                var satz = Zustand.EchtBestand.Saetze.FirstOrDefault(s => s.Fachrichtung == fr);
                Zustand.Aendere(w => w[EchtAblage.Schluessel] = new System.Text.Json.Nodes.JsonObject { ["art"] = "echt_probe", ["satz"] = satz?.Kennung });
                var gesperrt = v.EntfernenGesperrt(kennung);
                Pruefe(name + " Entfernen gesperrt bei offenem Satz", satz is not null && gesperrt == PaketTexte.Format(PaketTexte.FrEntfernenGesperrt, name) && !v.Entfernen(kennung),
                    (satz?.Kennung ?? "kein Satz") + ": " + (gesperrt ?? "NICHT gesperrt"));
                Zustand.Aendere(w => w.Remove(EchtAblage.Schluessel));
                // Entfernen: danach fehlen der Fachrichtung alle eigenen Inhalte (nur die gemeinsamen), der Lernstand bleibt
                Pruefe(name + " entfernen", v.Entfernen(kennung), "Paket entfernt");
                await Zustand.PaketeNeu(fr);
                var ohne = await Zustand.AlleInhalte is { } c ? Abdruck(c, fr) : "";
                var zeileOhne = v.Zeilen().FirstOrDefault(z => z.Kennung == kennung);
                Pruefe(name + " ohne Paket fehlen die Inhalte", Zustand.Aktuell.Fehlt(fr) && ohne != vorher && zeileOhne?.ZustandText == PaketTexte.ZustandNicht
                    && zeileOhne.Knoepfe.Contains(PaketKnopf.Laden), ohne + ", Zeile " + zeileOhne?.ZustandText + " " + string.Join(",", zeileOhne?.Knoepfe ?? []));
                // Nico 2a (Abnahmebild P12-225 Bild 4): auf der Startseite genau EINE Karte „Für … fehlt“ mit „Laden“, kein zweiter Hinweis
                await ansicht.ZeigeAsync(Seiten.Dashboard);
                if (ansicht.GebauteSeiten.GetValueOrDefault(Seiten.Dashboard) is DashboardSeite dashboard)
                {
                    dashboard.Neu();
                    await dashboard.Geladen;
                    await ansicht.BildFertigAsync();
                    var karten = dashboard.GetLogicalDescendants().OfType<Control>().Where(c => c.Name is "FachrichtungFehlt" or "FachrichtungsHinweis").ToList();
                    var texte = TexteVon(karten);
                    var knopf = karten.SelectMany(k => k.GetLogicalDescendants().OfType<Button>()).ToList();
                    Pruefe(name + " Startseite eine Karte mit Laden", karten.Count == 1 && karten[0].Name == "FachrichtungFehlt"
                        && texte.Contains(PaketTexte.Format(PaketTexte.FrFehltSicht, name)) && knopf.Count == 1
                        && Avalonia.Automation.AutomationProperties.GetName(knopf[0]) == PaketTexte.Laden + ": " + PaketTexte.Format(PaketTexte.FrFehltSicht, name),
                        karten.Count + " Karten: " + string.Join(" | ", texte) + ", Knoepfe " + knopf.Count);
                }
                // Messpunkt (Nico 4a): Pruefen und Einsetzen der geladenen Datei bis die Sicht steht, ohne Download, einmalig, streng
                var ziel = v.Ablage.Ladedatei(kennung, EchtStand(kennung));
                File.Copy(Path.Combine(quelle, Paket.Dateiname(kennung, EchtStand(kennung))), ziel, true);
                var uhr = Stopwatch.StartNew();
                var ergebnis = v.Ablage.Einsetzen(ziel, kennung, EchtStand(kennung), Aktualisierung.Aktualisierung.Anzeige);
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
                Pruefe(name + " Zeile", zeile?.ZustandText == PaketTexte.Format(PaketTexte.ZustandGeladen, EchtStand(kennung)) && zeile.Name == "Fachrichtungs-Paket " + name,
                    (zeile?.Name ?? "keine Zeile") + ": " + zeile?.ZustandText);
                if (ansicht.GebauteSeiten.GetValueOrDefault(Seiten.Dashboard) is DashboardSeite mitPaket)
                {
                    mitPaket.Neu();
                    await mitPaket.Geladen;
                    await ansicht.BildFertigAsync();
                    Pruefe(name + " Startseite ohne Karte mit Paket", !mitPaket.GetLogicalDescendants().OfType<Control>().Any(c => c.Name is "FachrichtungFehlt" or "FachrichtungsHinweis"),
                        "nach dem Laden");
                }
            }
            Pruefe("Lernstand bytegleich", Lernstand() == vorherLernstand, Lernstand() == vorherLernstand ? "gleich" : "GEAENDERT");
            Pruefe("Datenordner mit den drei Paketen", FachrichtungsPaket.Fachrichtungen.All(f => Directory.Exists(Path.Combine(daten, PaketAblage.OrdnerName, FachrichtungsPaket.Kennung(f), EchtStand(f).ToString(System.Globalization.CultureInfo.InvariantCulture)))),
                "lernen_ae, lernen_dpa, lernen_dv wieder eingesetzt");
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

    /// <summary>
    /// Welle A 1.5.0 (Weisung P12-358): die App bringt AE, DPA und DV nur noch als Paket. Zu Beginn jedes Start-Tests setzt der
    /// Test die drei echten Pakete (Testdateien, gleich den Releases) in den Datenordner ein, wie ein Nutzer nach „Laden“; alle
    /// spaeteren Schritte (Sichten, Layout, Pruefung im echten Format, Messpunkte je Fachrichtung) sehen damit die Inhalte wie
    /// bisher. Fehlt eine Testdatei oder ist ein Paket rot, ist das ein Fund (nie still uebersprungen).
    /// </summary>
    internal static async Task<int> FachrichtungsPaketeVorabAsync()
    {
        if (!FachrichtungsPaket.Fachrichtungen.Any(Zustand.Eingebaut.HatPlatzhalter))
        {
            Messwerte.Notiere("  FEHLER: Fachrichtungs-Pakete vorab: die App traegt keine Platzhalter (seit A 1.5.0 erwartet)");
            return 1;
        }
        var funde = 0;
        var v = PaketDienst.Verwaltung;
        foreach (var fr in FachrichtungsPaket.Fachrichtungen)
        {
            var kennung = FachrichtungsPaket.Kennung(fr);
            if (Zustand.Aktuell.Pakete.ContainsKey(fr))
            {
                continue;
            }
            var grund = EinsetzenAus(v.Ablage, kennung);
            Messwerte.Notiere("Fachrichtungs-Pakete vorab: " + kennung + (grund is null ? " eingesetzt" : ": " + grund));
            if (grund is not null)
            {
                Messwerte.Notiere("  FEHLER: Fachrichtungs-Pakete vorab " + kennung + ": " + grund);
                funde++;
            }
        }
        await Zustand.PaketeNeu(null);
        return funde;
    }

    /// <summary>
    /// A 1.8.0 (Weisung P12-372): Stand der echten Testdatei je Fachrichtungs-Paket, gleich paket_bauen.LERNEN_TESTDATEIEN (AE Stand 2
    /// mit den neuen Fassungen der Pruefungssaetze, DPA und DV Stand 1); Test A1_EchteTestdateien_JeFachrichtung_Gut prueft beides.
    /// </summary>
    internal static long EchtStand(string kennungOderFr) => kennungOderFr is "ae" or "lernen_ae" ? 2 : 1;

    /// <summary>Echte Testdatei einsetzen (ohne Download); null = gut, sonst der Grund.</summary>
    private static string? EinsetzenAus(PaketAblage ablage, string kennung)
    {
        var datei = Path.Combine(EchtOrdner, Paket.Dateiname(kennung, EchtStand(kennung)));
        if (!File.Exists(datei))
        {
            return datei + " fehlt";
        }
        var ziel = ablage.Ladedatei(kennung, EchtStand(kennung));
        File.Copy(datei, ziel, true);
        var ergebnis = ablage.Einsetzen(ziel, kennung, EchtStand(kennung), Aktualisierung.Aktualisierung.Anzeige);
        return ergebnis.Gut ? null : ergebnis.Grund ?? "?";
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
