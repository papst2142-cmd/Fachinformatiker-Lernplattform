using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Pakete;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Programm;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.88.0 (Inhaltspakete W2, Weisung P12-296, Abnahmesaetze W2-1 bis W2-5) im Start-Test mit dem echten Spielstand der
/// Sitzung (eigener Durchgang auf einem freien Platz, danach geloescht) und einer Testquelle mit dem Probe-Paket <c>probe_klein</c>:
/// am Bahnhof von Stadt 1 der Fahrplan (Suchen, „Paket fehlt (…) – Laden“, „Zug nach …“), Zugfahrt, in der Paket-Stadt gehen und
/// ein Geschaeft betreten, Entfernen in den Optionen gesperrt (Text rot), zweites Geraet ohne Paket (Hinweis, kein Schritt, „Paket
/// laden“ macht weiter), zurueck nach Stadt 1. Jeder Knopf mit einer anderen Bedienung (Maus, Finger, Eingabetaste, Screenreader).
/// </summary>
public static partial class AutoMessung
{
    private const string ZugPaket = "probe_klein";

    private static async Task<int> PaketStaedteAsync(MainView ansicht)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Paket-Staedte: " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Paket-Staedte " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null || seite.Tafel is null || SpielStart.Aktiv is not { } sitzung)
        {
            Pruefe("Spielseite", false, "nicht gebaut");
            return funde;
        }
        var quelle = PaketQuelleAnlegen(mitVarianten: true);
        if (quelle is null)
        {
            Pruefe("Testquelle", false, "Probe-Pakete nicht gefunden");
            return funde;
        }
        var verwaltung = sitzung.Verwaltung;
        var alterLauf = verwaltung.Lauf;
        var alteWahl = sitzung.PlatzGewaehlt;
        string? lauf = null;
        try
        {
            // eigener Durchgang mit Spielfigur (der Start-Test hat sonst nur die Probe im Arbeitsspeicher)
            var platz = Enumerable.Range(1, 9).Select(p => (long)p).First(p => verwaltung.Belegung().LaufAuf(p) is null);
            lauf = verwaltung.NeuerLauf(platz);
            verwaltung.ZumSchreiben().Profil("Nico", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
            sitzung.PlatzGewaehlt = true;
            sitzung.Abgleichen();
            seite.TafelZeigen();
            seite.Tafel.IsVisible = false;
            await ansicht.BildFertigAsync();
            var spiel = sitzung.Spiel;
            Pruefe("Durchgang", !sitzung.IstProbe && ReferenceEquals(seite.Spiel, spiel), sitzung.IstProbe ? "nur Probe" : "Spielfigur angelegt, Platz " + platz);
            // W2-2: am Bahnhof der Fahrplan; noch nie gesucht = „Weitere Städte suchen“, nichts geladen
            spiel.GeheZu(Stadt.Bahnhof);
            spiel.SofortAnkommen();
            await ansicht.BildFertigAsync();
            Pruefe("Fahrplan vor der Suche", spiel.AmBahnhof && spiel.Zeile.Contains(ZugTexte.Format(ZugTexte.Fahrplan, ZugTexte.Stadt1), StringComparison.Ordinal)
                && seite.OrtKnoepfe.ContainsKey(StadtSpiel.AktionPaketSuchen) && PaketDienst.Verwaltung.Staedte().Count == 0,
                spiel.Zeile.Replace('\n', ' ') + " | Knoepfe: " + string.Join(", ", spiel.Knoepfe.Select(k => k.Name)));
            if (seite.OrtKnoepfe.GetValueOrDefault(StadtSpiel.AktionPaketSuchen) is { } suchen)
            {
                await BedienenAsync(ansicht, suchen, "Maus");
            }
            await WartenBisAsync(ansicht, () => PaketDienst.Verwaltung.Gesucht is not null && !SpielZug.Laedt && seite.OrtKnoepfe.ContainsKey(StadtSpiel.AktionPaketLaden + ZugPaket));
            var groesse = PaketDienst.Verwaltung.Zeilen().FirstOrDefault(z => z.Kennung == ZugPaket)?.Groesse ?? 0;
            var fehltText = ZugTexte.Format(ZugTexte.PaketFehlt, "Probe: kleine Stadt", PaketTexte.Mb(groesse));
            var laden = seite.OrtKnoepfe.GetValueOrDefault(StadtSpiel.AktionPaketLaden + ZugPaket);
            Pruefe("Fahrplan nennt alle Staedte", KnopfName(laden) == fehltText && spiel.Zeile.Contains("Probe: kleine Stadt", StringComparison.Ordinal)
                && spiel.Zeile.Contains("Probe: Testkarte 256", StringComparison.Ordinal)
                && PaketDienst.Verwaltung.Staedte().Count == 0,
                spiel.Zeile.Replace('\n', ' ') + " | Knopf: " + (KnopfName(laden) ?? "FEHLT") + " (soll " + fehltText + "), geladen: " + PaketDienst.Verwaltung.Staedte().Count);
            Pruefe("Knoepfe im Bild", KnoepfeImBild(seite, "fahrplan").Count == 0, string.Join("; ", KnoepfeImBild(seite, "fahrplan")));
            // Sammelliste 376: das Paket einer anderen Art (Grafik-Paket, unbekannte Art) steht schon vor dem Laden nicht im Fahrplan, nur in den Optionen
            // A 0.91.0: auch die Fachrichtungs-Pakete (Art lernen) stehen nie im Fahrplan
            Pruefe("376 Paket anderer Art vor dem Laden nicht im Fahrplan", !spiel.Zeile.Contains("Grafik-Paket", StringComparison.Ordinal)
                && !spiel.Zeile.Contains("Fachrichtungs-Paket", StringComparison.Ordinal)
                && !seite.OrtKnoepfe.Keys.Any(k => k.EndsWith("probe_grafik", StringComparison.Ordinal))
                && PaketDienst.Verwaltung.Zeilen().Any(z => z.Kennung == "probe_grafik"),
                spiel.Zeile.Replace('\n', ' ') + " | Knoepfe: " + string.Join(", ", seite.OrtKnoepfe.Keys));
            // Sammelliste 421 (Lauf 1126, Mac): der Knopf wird nach den Pruefungen oben frisch geholt; die Knoepfe des Orts entstehen
            // nach einem neuen Bild der Tafel neu, ein Tippen auf den alten (nicht mehr angezeigten) Knopf laedt nichts
            await ansicht.BildFertigAsync();
            laden = seite.OrtKnoepfe.GetValueOrDefault(StadtSpiel.AktionPaketLaden + ZugPaket);
            var ladenAngezeigt = laden is not null && TopLevel.GetTopLevel(laden) is not null;
            if (laden is not null)
            {
                await BedienenAsync(ansicht, laden, "Finger");
            }
            await WartenBisAsync(ansicht, () => !SpielZug.Laedt && seite.OrtKnoepfe.ContainsKey(StadtSpiel.AktionZug + ZugPaket));
            var zugKnopf = seite.OrtKnoepfe.GetValueOrDefault(StadtSpiel.AktionZug + ZugPaket);
            Pruefe("Paket am Bahnhof geladen", KnopfName(zugKnopf) == ZugTexte.Format(ZugTexte.ZugNach, "Probe: kleine Stadt")
                && PaketDienst.Verwaltung.Staedte().Any(s => s.Kopf.Kennung == ZugPaket),
                (KnopfName(zugKnopf) ?? "kein Knopf Zug") + (ladenAngezeigt ? "" : " (Knopf Laden war nicht angezeigt)") + " | " + spiel.Zeile.Replace('\n', ' '));

            // ein Paket unbekannter Art (Grafik-Paket): auch geladen und als „unbekannte Art“ erkannt faehrt kein Zug dorthin
            var lern = await SpielZug.LadenAsync(sitzung, "probe_grafik");
            spiel.FahrplanNeu();
            await ansicht.BildFertigAsync();
            Pruefe("Paket unbekannter Art nicht im Fahrplan", lern == PaketTexte.ArtUnbekannt && !spiel.Zeile.Contains("Grafik-Paket", StringComparison.Ordinal)
                && !seite.OrtKnoepfe.Keys.Any(k => k.EndsWith("probe_grafik", StringComparison.Ordinal)),
                "Laden: " + (lern ?? "ok") + " | " + spiel.Zeile.Replace('\n', ' '));

            // W2-1: mit dem Zug in die Paket-Stadt (Eingabetaste)
            var zeilen = spiel.Ablauf.Stand.Verlauf.Count;
            if (zugKnopf is not null)
            {
                await BedienenAsync(ansicht, zugKnopf, "Eingabetaste");
            }
            await ansicht.BildFertigAsync();
            var dort = sitzung.Spiel;
            var letzte = dort.Ablauf.Stand.Verlauf[^1];
            Pruefe("Zugfahrt", dort.Stadt.Kennung == ZugPaket && ReferenceEquals(seite.Spiel, dort) && dort.Jetzt.Ort == Stadt.Bahnhof
                && dort.Ablauf.Stand.Verlauf.Count == zeilen + 1 && letzte.Art == Ereignisse.ZugGefahren
                && dort.Zeile.Contains(ZugTexte.Format(ZugTexte.Angekommen, "Probe: kleine Stadt"), StringComparison.Ordinal),
                "Stadt " + (dort.Stadt.Kennung ?? "Stadt 1") + ", Ort " + dort.Jetzt.Ort + ", letzte Zeile " + letzte.Art + " " + PythonJson.Standard(letzte.Daten)
                + " | " + dort.Zeile.Replace('\n', ' '));
            Pruefe("Bahnhof der Paket-Stadt", !dort.KannBetreten && seite.OrtKnoepfe.ContainsKey(StadtSpiel.AktionZug + StadtSpiel.ZugStadt1),
                "Knoepfe: " + string.Join(", ", dort.Knoepfe.Select(k => k.Name)));
            // in der Paket-Stadt: zum Moebelhaus, (warten,) hinein und hinaus; die Zeilen tragen den Vorsatz
            dort.GeheZu("moebelhaus");
            dort.SofortAnkommen();
            if (!dort.KannBetreten && dort.KannWarten)
            {
                dort.Warten();
            }
            var hinein = dort.KannBetreten && dort.Betreten();
            await ansicht.BildFertigAsync();
            var innen = dort.Innen?.Ansicht;
            dort.Verlassen();
            await ansicht.BildFertigAsync();
            var vorsatz = dort.Ablauf.Stand.Verlauf.Skip(zeilen + 1).Where(e => e.Art is Stadt.Gegangen or Stadt.Gewartet)
                .All(e => new[] { "von", "nach", "ort" }.Select(k => SpielDaten.Get(e.Daten, k) as string).OfType<string>().All(o => o.StartsWith(ZugPaket + ":", StringComparison.Ordinal)));
            Pruefe("Paket-Stadt spielbar", hinein && innen is not null && vorsatz && dort.Jetzt.Ort == "moebelhaus",
                (hinein ? "Moebelhaus betreten (" + innen + ")" : "NICHT betreten") + (vorsatz ? ", Zeilen mit Vorsatz" : ", Zeile OHNE Vorsatz"));

            // W2-4: Entfernen in den Optionen gesperrt (rot, mit Namen), Paket bleibt
            await ansicht.ZeigeAsync(Seiten.Optionen);
            if (ansicht.GebauteSeiten[Seiten.Optionen] is OptionenSeite optionen)
            {
                optionen.PaketeNeu();
                await ansicht.BildFertigAsync();
                var sperre = ZugTexte.Format(ZugTexte.EntfernenGesperrt, "Probe: kleine Stadt");
                if (optionen.PaketKnoepfe.GetValueOrDefault(ZugPaket)?.GetValueOrDefault(PaketKnopf.Entfernen) is { } weg)
                {
                    await BedienenAsync(ansicht, weg, "Screenreader");
                }
                await ansicht.BildFertigAsync();
                Pruefe("Entfernen gesperrt", optionen.PaketStatus?.Text == sperre && Dialog.Offen is null && PaketDienst.Verwaltung.Staedte().Any(s => s.Kopf.Kennung == ZugPaket),
                    "Status: " + optionen.PaketStatus?.Text + (Dialog.Offen is null ? "" : ", RUECKFRAGE offen"));
                // Gegenprobe: ohne Sperre waere Entfernen erlaubt
                var gesetzt = PaketDienst.Verwaltung.EntfernenSperre;
                PaketDienst.Verwaltung.EntfernenSperre = null;
                var ohne = PaketDienst.Verwaltung.EntfernenGesperrt(ZugPaket);
                PaketDienst.Verwaltung.EntfernenSperre = gesetzt;
                Pruefe("Gegenprobe Sperre", ohne is null && PaketDienst.Verwaltung.EntfernenGesperrt(ZugPaket) == sperre, ohne is null ? "ohne Sperre frei (erkannt)" : "ohne Sperre gesperrt");
            }
            await ansicht.ZeigeAsync(Seiten.Spiel);
            await ansicht.BildFertigAsync();

            // W2-5: zweites Geraet ohne das Paket (hier: Paket aus dem Datenordner genommen, wie auf einem Geraet ohne Paket)
            PaketDienst.Verwaltung.Ablage.Entfernen(ZugPaket);
            sitzung.Abgleichen();
            await ansicht.BildFertigAsync();
            var ohnePaket = sitzung.Spiel;
            var fehltHier = ZugTexte.Format(ZugTexte.PaketFehltHier, "Probe: kleine Stadt");
            var anzahl = ohnePaket.Ablauf.Stand.Verlauf.Count;
            var hinweis = ohnePaket.Zeile;
            var gehen = ohnePaket.GeheZu("kaufhaus");
            string? gesperrt = null;
            try
            {
                verwaltung.ZumSchreiben().Ausfuehren([(Stadt.Gelaufen, new PyObjekt { { "tag", 1L }, { "ort", "x" }, { "schritte", 1L }, { "minuten", 1L } })]);
            }
            catch (SpielFehler f)
            {
                gesperrt = f.Message;
            }
            Pruefe("Zweites Geraet ohne Paket", sitzung.Fehlt is not null && hinweis.Contains(fehltHier, StringComparison.Ordinal)
                && seite.OrtKnoepfe.ContainsKey(StadtSpiel.AktionPaketLaden + ZugPaket) && gehen != StadtGrund.Ok && gesperrt is not null
                && ohnePaket.Ablauf.Stand.Verlauf.Count == anzahl,
                hinweis.Replace('\n', ' ') + " | danach: " + ohnePaket.Zeile.Replace('\n', ' ') + " | Gehen: " + gehen + " | Schritt: " + (gesperrt ?? "GESCHRIEBEN") + " | Zeilen " + anzahl + " -> " + ohnePaket.Ablauf.Stand.Verlauf.Count);
            if (seite.OrtKnoepfe.GetValueOrDefault(StadtSpiel.AktionPaketLaden + ZugPaket) is { } nachladen)
            {
                await BedienenAsync(ansicht, nachladen, "Maus");
            }
            await WartenBisAsync(ansicht, () => !SpielZug.Laedt && sitzung.Fehlt is null);
            var wieder = sitzung.Spiel;
            Pruefe("Paket laden macht weiter", sitzung.Fehlt is null && wieder.Stadt.Kennung == ZugPaket && wieder.Jetzt.Ort == "moebelhaus" && ReferenceEquals(seite.Spiel, wieder),
                "Stadt " + (wieder.Stadt.Kennung ?? "Stadt 1") + ", Ort " + wieder.Jetzt.Ort);

            // zurueck nach Stadt 1 (Finger), danach ist Entfernen wieder frei
            wieder.GeheZu(Stadt.Bahnhof);
            wieder.SofortAnkommen();
            await ansicht.BildFertigAsync();
            if (seite.OrtKnoepfe.GetValueOrDefault(StadtSpiel.AktionZug + StadtSpiel.ZugStadt1) is { } zurueck)
            {
                await BedienenAsync(ansicht, zurueck, "Finger");
            }
            await ansicht.BildFertigAsync();
            var heim = sitzung.Spiel;
            Pruefe("Zurueck in Stadt 1", heim.Stadt.Kennung is null && heim.Jetzt.Ort == Stadt.Bahnhof && !heim.InAndererStadt && PaketDienst.Verwaltung.EntfernenGesperrt(ZugPaket) is null,
                "Stadt " + (heim.Stadt.Kennung ?? "Stadt 1") + ", Ort " + heim.Jetzt.Ort);
        }
        catch (Exception x) when (x is SpielFehler or InvalidOperationException or IOException)
        {
            Pruefe("Ablauf", false, x.GetType().Name + ": " + x.Message);
        }
        finally
        {
            if (lauf is not null)
            {
                verwaltung.LaufLoeschen(lauf);
                if (alterLauf is not null)
                {
                    verwaltung.LaufWaehlen(alterLauf);
                }
            }
            sitzung.PlatzGewaehlt = alteWahl;
            sitzung.Abgleichen();
            PaketQuelleWeg(quelle);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    /// <summary>Name des Knopfs, wie ihn der Screenreader liest (der Inhalt ist ein umbrechender TextBlock).</summary>
    private static string? KnopfName(Button? k) => k is null ? null
        : Avalonia.Automation.AutomationProperties.GetName(k) is { Length: > 0 } n ? n : k.Content switch { string s => s, TextBlock t => t.Text, _ => null };

    private static async Task WartenBisAsync(MainView ansicht, Func<bool> fertig)
    {
        for (var i = 0; i < 400 && !fertig(); i++)
        {
            await Task.Delay(50);
            await ansicht.BildFertigAsync();
        }
    }
}
