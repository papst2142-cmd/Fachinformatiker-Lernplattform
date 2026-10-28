using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Spiel.Ton;
using Lernplattform.Core.Texte;
using Lernplattform.Programm;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W8 (Weisungen P8-17, P8-17b, P8-17c, P8-17d): Musik und Geraeusche, Vollbild, Tastenbelegung. Im Pruefstand
/// ist der Ton still (<c>LERNPLATTFORM_TON=aus</c>), der Dienst zaehlt nur die Anlaesse. Spielteil: Schritte, Tueren und
/// Feierabend-Melodie zaehlen; bei Gesamt 0 klingt nichts; rechte Maustaste dreht; eine neue Taste wirkt im Spiel; ein Sinuston als
/// eigenes Lied laeuft je nach Wahl "Eigene Lieder spielen" nur innen, nur in der Stadt oder ueberall. Lernteil: die
/// drei Karten in den Optionen (Taste neu belegen, doppelt, Esc, Standard; Vollbild ein und aus) und ihre Ansichten.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Anzahl der Ansichten der Welle W8 je Groesse und Stufe (fuer den Bericht).</summary>
    public const int P8W8AnsichtenAnzahl = 3;

    /// <summary>Name des Sinustons, den der Start-Test als eigenes Lied in den Ordner musik legt (und wieder entfernt).</summary>
    private const string ProbeLied = "Probe Sinus.wav";

    private static int Zahl(System.Collections.Concurrent.ConcurrentDictionary<string, int> zaehler, string schluessel) =>
        zaehler.GetValueOrDefault(schluessel);

    private static void TonWerteSetzen(Func<TonWerte, TonWerte> aenderung)
    {
        var werte = aenderung(TonEinstellungen.Lesen(Zustand.Werte));
        Zustand.Aendere(w =>
        {
            TonEinstellungen.Wert(w, TonEinstellungen.Gesamt, werte.Gesamt);
            TonEinstellungen.OrteWert(w, werte.MusikOrte);
        });
        TonDienst.Regler(TonEinstellungen.Lesen(Zustand.Werte));
    }

    private static async Task<int> P8W8TonAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Ton: " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Ton " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Ton: Spielseite nicht gebaut");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Ton: ohne GL nicht gemessen");
            return 0;
        }
        var vorher = seite.Spiel;
        var flaeche = seite.Flaeche;
        var tastenVorher = Tastenbelegung.Aktuell;
        var werteVorher = TonEinstellungen.Lesen(Zustand.Werte);
        try
        {
            var still = Tonwahl.Aus(Environment.GetEnvironmentVariable(Tonwahl.Schalter));
            Pruefe("Ausgabe", !still || TonDienst.Still, TonDienst.Zustand + (TonDienst.Still ? " (still, nur gezaehlt)" : ""));

            // 1. Schritte in der Stadt (gezaehlt und bei Gesamt ueber 0 "gespielt")
            async Task<bool> GehenAsync(StadtSpiel s, float dx)
            {
                if (!s.GeheZu(s.FigurX + dx, s.FigurZ))
                {
                    return false;
                }
                for (var i = 0; i < 600 && s.Geht; i++)
                {
                    await flaeche.NaechstesBildAsync();
                }
                return !s.Geht;
            }
            var spiel = Frisch(seite);
            await flaeche.NaechstesBildAsync();
            var boden = Tonplan.Schluessel(new Tonanlass(Anlass.Schritt, spiel.Boden));
            var (a0, g0) = (Zahl(TonDienst.Anlaesse, boden), Zahl(TonDienst.Gespielt, boden));
            var gegangen = await GehenAsync(spiel, 3) || await GehenAsync(spiel, -3);
            var (a1, g1) = (Zahl(TonDienst.Anlaesse, boden), Zahl(TonDienst.Gespielt, boden));
            Pruefe("Schritte in der Stadt (" + boden + ")", gegangen && a1 - a0 >= 2 && g1 - g0 == a1 - a0,
                string.Create(inv, $"{a1 - a0} Schritte, davon {g1 - g0} gespielt, Weg {spiel.Schrittweg:F2}"));

            // 2. Gesamt 0: gezaehlt, aber nichts gespielt
            TonWerteSetzen(w => w with { Gesamt = 0 });
            (a0, g0) = (Zahl(TonDienst.Anlaesse, boden), Zahl(TonDienst.Gespielt, boden));
            gegangen = await GehenAsync(spiel, -3) || await GehenAsync(spiel, 3);
            (a1, g1) = (Zahl(TonDienst.Anlaesse, boden), Zahl(TonDienst.Gespielt, boden));
            Pruefe("Gesamt 0 ist still", gegangen && a1 - a0 >= 2 && g1 == g0 && !TonDienst.Aktiv,
                string.Create(inv, $"{a1 - a0} Schritte, davon {g1 - g0} gespielt, Ton {(TonDienst.Aktiv ? "an" : "aus")}"));
            TonWerteSetzen(w => w with { Gesamt = werteVorher.Gesamt == 0 ? TonEinstellungen.GesamtVorgabe : werteVorher.Gesamt });

            // 2b. Hintergrund und zurueck (Weisung P8-17e): Ton aus, dann von selbst wieder an, Schritte klingen wieder
            var wieder = TonDienst.Wiederanlaeufe;
            TonDienst.Hintergrund(true);
            var ausImHintergrund = !TonDienst.Aktiv;
            TonDienst.Hintergrund(false);
            (a0, g0) = (Zahl(TonDienst.Anlaesse, boden), Zahl(TonDienst.Gespielt, boden));
            gegangen = await GehenAsync(spiel, 3) || await GehenAsync(spiel, -3);
            (a1, g1) = (Zahl(TonDienst.Anlaesse, boden), Zahl(TonDienst.Gespielt, boden));
            Pruefe("Nach dem Hintergrund von selbst wieder an", ausImHintergrund && TonDienst.Aktiv && TonDienst.Wiederanlaeufe == wieder + 1
                && gegangen && g1 - g0 == a1 - a0 && a1 > a0,
                string.Create(inv, $"im Hintergrund {(ausImHintergrund ? "aus" : "an")}, danach {(TonDienst.Aktiv ? "an" : "aus")}, Wiederanläufe {TonDienst.Wiederanlaeufe - wieder}, {g1 - g0} von {a1 - a0} Schritten gespielt"));

            // 2c. Grundklang je Jahreszeit (Weisung P8-24): gewechselt beim Betreten der Spielseite, Winter ohne Einzelrufe
            var nachDatum = Jahreszeiten.Heute();
            var jahreszeiten = new List<string>();
            var jahreszeitenOk = true;
            try
            {
                foreach (var zeit in Enum.GetValues<Jahreszeit>())
                {
                    Jahreszeiten.Probe(zeit);
                    var schluessel = Tonplan.StadtGrundFuer(zeit);
                    var grundVorher = Zahl(TonDienst.Anlaesse, schluessel);
                    TonDienst.SpielVerlassen();
                    TonDienst.SpielBetreten();
                    await flaeche.NaechstesBildAsync();
                    await flaeche.NaechstesBildAsync();
                    var rufe = Tonplan.RufeFuer(zeit);
                    var ok = TonDienst.Zeit == zeit && Zahl(TonDienst.Anlaesse, schluessel) == grundVorher + 1 && (zeit == Jahreszeit.Winter) == (rufe is null);
                    jahreszeitenOk &= ok;
                    jahreszeiten.Add(string.Create(inv, $"{zeit} {schluessel} +{Zahl(TonDienst.Anlaesse, schluessel) - grundVorher}, Rufe {(rufe is null ? "keine" : $"{rufe.Min:F0}-{rufe.Max:F0} s")}"));
                }
            }
            finally
            {
                Jahreszeiten.ProbeEnde();
                TonDienst.SpielVerlassen();
                TonDienst.SpielBetreten();
            }
            await flaeche.NaechstesBildAsync();
            Pruefe("Grundklang je Jahreszeit", jahreszeitenOk && TonDienst.Zeit == nachDatum,
                string.Join("; ", jahreszeiten) + "; nach Datum wieder " + nachDatum);

            // 3. Tueren (Wohnung) und Melodie (Feierabend)
            var auf = Zahl(TonDienst.Anlaesse, "tuer_auf");
            var zu = Zahl(TonDienst.Anlaesse, "tuer_zu");
            spiel.GeheZu("zuhause");
            spiel.SofortAnkommen();
            var drin = spiel.Betreten();
            await flaeche.NaechstesBildAsync();
            Pruefe("Tuer auf beim Hineingehen", drin && Zahl(TonDienst.Anlaesse, "tuer_auf") == auf + 1 && spiel.Boden == Untergrund.Teppich,
                string.Create(inv, $"{Zahl(TonDienst.Anlaesse, "tuer_auf") - auf} Mal, Boden {spiel.Boden}"));
            spiel.Verlassen();
            await flaeche.NaechstesBildAsync();
            Pruefe("Tuer zu beim Hinausgehen", Zahl(TonDienst.Anlaesse, "tuer_zu") == zu + 1, string.Create(inv, $"{Zahl(TonDienst.Anlaesse, "tuer_zu") - zu} Mal"));
            // Melodie nach dem geschriebenen Feierabend (die Kasse prueft SpielTonTests: der Probe-Figur fehlt das Geld)
            var melodie = Zahl(TonDienst.Anlaesse, "melodie_feierabend");
            var feierabend = "kein Feierabend";
            try
            {
                spiel.Ablauf.SchlafenGehen();
                feierabend = "Feierabend geschrieben";
            }
            catch (SpielFehler fehler)
            {
                feierabend = "kein Feierabend (" + fehler.Message + ")";
            }
            Pruefe("Melodie nach dem Feierabend", Zahl(TonDienst.Anlaesse, "melodie_feierabend") == melodie + 1,
                string.Create(inv, $"{feierabend}, {Zahl(TonDienst.Anlaesse, "melodie_feierabend") - melodie} Mal"));

            // 4. rechte Maustaste dreht (wie Q und E), kein Tippen
            spiel = Frisch(seite);
            await flaeche.NaechstesBildAsync();
            var kamera = flaeche.AktuelleKamera!;
            var drehung = kamera.Drehung;
            var keinTippen = flaeche.ProbeRechtsZiehen(100, 150, 200);
            var gedreht = ((kamera.Drehung - drehung) % 360f + 360f) % 360f;
            Pruefe("Rechte Maustaste dreht", keinTippen && Math.Abs(gedreht - (50 * Kameragriff.GradJePunkt)) < 0.01f && !spiel.Geht,
                string.Create(inv, $"um {gedreht:F1} Grad bei 50 Punkten"));

            // 5. eine neu belegte Taste wirkt im Spiel (W statt - fuer weiter weg), die alte nicht mehr; danach wieder wie vorher
            var neu = new Tastenbelegung();
            neu.Setzen(Tastenbelegung.Weiter, "W", out _);
            Tastenbelegung.Aktuell = neu;
            var abstand = kamera.Abstand;
            flaeche.Taste(Key.W);
            var wirkt = kamera.Abstand > abstand;
            abstand = kamera.Abstand;
            flaeche.Taste(Key.Subtract);
            var plusFrei = kamera.Abstand == abstand;
            Tastenbelegung.Aktuell = tastenVorher;
            Pruefe("Neue Taste wirkt im Spiel", wirkt && plusFrei, wirkt ? (plusFrei ? "W zoomt, − nicht mehr" : "− zoomt noch") : "W zoomt nicht");

            // 6. Sinuston als eigenes Lied, je Wahl "Eigene Lieder spielen" gezaehlt je Ort
            var lieder = TonDienst.Lieder;
            if (lieder is null)
            {
                Pruefe("Eigene Lieder", false, "kein Ordner");
                return funde;
            }
            using (var wav = new MemoryStream())
            {
                Tondekoder.WavSchreiben(wav, Tondekoder.Sinus(440, 3, Mischer.Rate), Mischer.Rate);
                wav.Position = 0;
                lieder.Hinzufuegen(wav, ProbeLied);
            }
            TonDienst.LiederGeaendert();
            async Task<int> OrtZaehlenAsync(string ort)
            {
                var start = Zahl(TonDienst.Anlaesse, "musik:" + ort);
                // die Pflege prueft jede Sekunde; das Lied oeffnet im Hintergrund
                for (var i = 0; i < 25; i++)
                {
                    await flaeche.NaechstesBildAsync();
                    await Task.Delay(100);
                    if (Zahl(TonDienst.Anlaesse, "musik:" + ort) > start)
                    {
                        break;
                    }
                }
                return Zahl(TonDienst.Anlaesse, "musik:" + ort) - start;
            }
            foreach (var orte in TonEinstellungen.Orte)
            {
                spiel = Frisch(seite);
                TonWerteSetzen(w => w with { MusikOrte = orte });
                var stadt = await OrtZaehlenAsync("stadt");
                spiel.GeheZu("zuhause");
                spiel.SofortAnkommen();
                spiel.Betreten();
                var innen = await OrtZaehlenAsync("innen");
                spiel.Verlassen();
                var sollInnen = TonEinstellungen.MusikHier(orte, true) ? 1 : 0;
                var sollStadt = TonEinstellungen.MusikHier(orte, false) ? 1 : 0;
                Pruefe("Eigenes Lied bei \"" + TonTexte.Ort(orte) + "\"", innen == sollInnen && stadt == sollStadt,
                    string.Create(inv, $"in den Räumen {innen}, in der Stadt {stadt} (soll {sollInnen}, {sollStadt})"));
            }
            lieder.Entfernen(ProbeLied);
            TonDienst.LiederGeaendert();
            // Oeffnungszeit eines echten Geraets (P8-17e Punkt 3), nur zur Ansicht und nur im Hintergrund; ohne Geraet eine Zeile
            var probe = await Task.Run(() =>
            {
                var uhr = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    using var geraet = new SoundFlowAusgabe();
                    geraet.Starten(new Mischer());
                    var ms = uhr.Elapsed.TotalMilliseconds;
                    return string.Create(inv, $"{ms:F0} ms ({geraet.Name})");
                }
                catch (Exception fehler)
                {
                    return string.Create(inv, $"kein Gerät nach {uhr.Elapsed.TotalMilliseconds:F0} ms ({fehler.GetType().Name})");
                }
            }).WaitAsync(TimeSpan.FromSeconds(10)).ContinueWith(t => t.IsCompletedSuccessfully ? t.Result : "kein Ergebnis nach 10 s", TaskScheduler.Default);
            Messwerte.Notiere("Ton: Gerät öffnen (im Hintergrund, Probe): " + probe);
            Messwerte.Notiere(string.Create(inv, $"Ton: Gerät geöffnet im Spiel: {(TonDienst.GeoeffnetMs < 0 ? "nie (still)" : TonDienst.GeoeffnetMs.ToString("F0", inv) + " ms")}"));
            Messwerte.Notiere(string.Create(inv, $"Ton: Töne geladen (im Hintergrund): {(TonDienst.GeladenMs < 0 ? "nicht geladen (still)" : TonDienst.GeladenMs.ToString("F0", inv) + " ms")}"));
            Messwerte.Notiere("Ton: Anlässe " + string.Join(", ", TonDienst.Anlaesse.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + p.Value.ToString(inv))));
        }
        finally
        {
            Tastenbelegung.Aktuell = tastenVorher;
            Zustand.Aendere(w =>
            {
                TonEinstellungen.Wert(w, TonEinstellungen.Gesamt, werteVorher.Gesamt);
                TonEinstellungen.OrteWert(w, werteVorher.MusikOrte);
            });
            TonDienst.Regler(TonEinstellungen.Lesen(Zustand.Werte));
            TonDienst.Lieder?.Entfernen(ProbeLied);
            seite.SpielSetzen(vorher);
            await flaeche.NaechstesBildAsync();
        }
        return funde;
    }

    /// <summary>Lernteil: die Karten "Tastenbelegung" und "Vollbild" bedienen (gespeichert, danach wie vorher).</summary>
    private static async Task<int> P8W8OptionenAsync(MainView ansicht)
    {
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
            Messwerte.Notiere("  FEHLER: Optionen W8: Optionen nicht gebaut");
            return 1;
        }
        foreach (var id in (string[])[Optionen.Ton, Optionen.Vollbild, Optionen.Tasten])
        {
            if (optionen.Klappe(id) is { } klappe)
            {
                klappe.IsExpanded = true;
            }
            else
            {
                Pruefe("Karte " + id, false, "fehlt");
            }
        }
        await ansicht.BildFertigAsync();
        Pruefe("Musik und Geräusche", optionen.TonRegler.Count == 3 && optionen.LiedOrte.Count == 3
            && optionen.LiedOrte.GetValueOrDefault(TonEinstellungen.Innen)?.IsChecked == true,
            $"{optionen.TonRegler.Count} Regler, {optionen.LiedOrte.Count} Orte, Vorgabe {TonEinstellungen.Lesen(Zustand.Werte).MusikOrte}");
        try
        {
            // Taste neu belegen
            optionen.TasteWarten(Tastenbelegung.Naeher);
            var ok = optionen.TasteNehmen("W");
            var knopf = optionen.TastenKnoepfe[Tastenbelegung.Naeher].GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text;
            Pruefe("Taste neu belegen", ok == Tastenbelegung.Ergebnis.Ok && Tastenbelegung.Aktuell.AufgabeFuer("W") == Tastenbelegung.Naeher
                && Zustand.Werte[Tastenbelegung.Schluessel]?.ToJsonString() == "{\"naeher\":\"W\"}" && knopf == "W",
                $"{ok}, Knopf {knopf}, gespeichert {Zustand.Werte[Tastenbelegung.Schluessel]?.ToJsonString()}");
            // doppelt: Meldung, nichts geaendert
            optionen.TasteWarten(Tastenbelegung.Hinein);
            var doppelt = optionen.TasteNehmen("Q");
            await ansicht.BildFertigAsync();
            Pruefe("Taste doppelt", doppelt == Tastenbelegung.Ergebnis.Doppelt && optionen.TastenMeldung is { IsVisible: true }
                && Tastenbelegung.Aktuell.AufgabeFuer("Q") == Tastenbelegung.DrehLinks, optionen.TastenMeldung?.Text ?? "keine Meldung");
            // Esc bricht ab
            optionen.TasteWarten(Tastenbelegung.Links);
            var abbruch = optionen.TasteNehmen(Tastenbelegung.Abbrechen);
            Pruefe("Esc bricht ab", abbruch is null && Tastenbelegung.Aktuell.Tasten(Tastenbelegung.Links).SequenceEqual(["Left"]), abbruch?.ToString() ?? "abgebrochen");
            // Standard
            var standard = optionen.TastenStandard();
            Pruefe("Standard wiederherstellen", standard && Tastenbelegung.Aktuell.IstStandard && Zustand.Werte[Tastenbelegung.Schluessel] is null,
                Tastenbelegung.Aktuell.IstStandard ? "Standard" : "noch eigene Tasten");

            // Vollbild ein und aus (gespeichert)
            if (optionen.VollbildSchalter is { } schalter)
            {
                var war = Vollbild.Dauerhaft;
                schalter.IsChecked = !war;
                await ansicht.BildFertigAsync();
                var umgeschaltet = Vollbild.Dauerhaft == !war && VollbildWahl.Lesen(Zustand.Werte) == !war;
                schalter.IsChecked = war;
                await ansicht.BildFertigAsync();
                Pruefe("Vollbild ein und aus", umgeschaltet && Vollbild.Dauerhaft == war && VollbildWahl.Lesen(Zustand.Werte) == war,
                    umgeschaltet ? "umgeschaltet und zurück" : "nicht umgeschaltet");
            }
            else
            {
                Pruefe("Vollbild", false, "kein Schalter");
            }
        }
        finally
        {
            optionen.TastenStandard();
            foreach (var id in (string[])[Optionen.Ton, Optionen.Vollbild, Optionen.Tasten])
            {
                if (optionen.Klappe(id) is { } klappe)
                {
                    klappe.IsExpanded = false;
                }
            }
        }
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        return funde;
    }

    /// <summary>Ansichten: "Musik und Geräusche" mit einem eigenen Lied, "Vollbild", "Tastenbelegung" mit Meldung (ohne Speichern).</summary>
    private static async IAsyncEnumerable<string> P8W8Ansichten(MainView ansicht)
    {
        await ansicht.ZeigeAsync(Seiten.Optionen);
        if (ansicht.GebauteSeiten.GetValueOrDefault(Seiten.Optionen) is not OptionenSeite optionen)
        {
            yield break;
        }
        void Nur(string id)
        {
            foreach (var bereich in optionen.Angezeigt)
            {
                if (optionen.Klappe(bereich.Id) is { } klappe)
                {
                    klappe.IsExpanded = bereich.Id == id;
                }
            }
        }
        async Task ObenAsync(string id)
        {
            await ansicht.BildFertigAsync();
            optionen.Klappe(id)?.BringIntoView();
            await ansicht.BildFertigAsync();
        }
        var lieder = TonDienst.Lieder;
        if (lieder is not null)
        {
            using var wav = new MemoryStream();
            Tondekoder.WavSchreiben(wav, Tondekoder.Sinus(440, 0.2, Mischer.Rate), Mischer.Rate);
            wav.Position = 0;
            lieder.Hinzufuegen(wav, ProbeLied);
        }
        try
        {
            optionen.LiederNeu();
            Nur(Optionen.Ton);
            await ObenAsync(Optionen.Ton);
            yield return "optionen/musik-und-geraeusche";
        }
        finally
        {
            lieder?.Entfernen(ProbeLied);
            optionen.LiederNeu();
        }
        Nur(Optionen.Vollbild);
        await ObenAsync(Optionen.Vollbild);
        yield return "optionen/vollbild";
        Nur(Optionen.Tasten);
        optionen.TasteWarten(Tastenbelegung.Hinein);
        optionen.TasteNehmen("Q");
        await ObenAsync(Optionen.Tasten);
        yield return "optionen/tastenbelegung";
        optionen.TastenStandard();
        Nur("");
        await ansicht.BildFertigAsync();
    }
}
