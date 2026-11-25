using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Pakete;
using Lernplattform.Core.Texte;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.87.0 (Weisung P12-294, Abnahmesaetze A1–A3, A6): Karte „Inhaltspakete“ im Start-Test mit einer Testquelle
/// (<see cref="PaketQuelle.Umgebung"/>, Ordner ausserhalb des Datenordners). A1 das Probe-Paket (Testkarte 256) mit Maus, Finger,
/// Eingabetaste und Screenreader suchen, laden, anzeigen, entfernen; danach ist der Datenordner wie vorher (Abdruck). A2 kaputt,
/// abgeschnitten, unbekannte Art, zu neues Format = roter Grund, nichts im Datenordner; Abbruch mitten im Laden laesst den alten
/// Stand. A3 ohne Quelle „Keine Verbindung“, Geladenes bleibt; Rueckfrage ohne WLAN kommt vor dem Balken. Die Ansichten fuer die
/// Layoutpruefung (<see cref="PaketeAnsichten"/>) zeigen alle Zustaende, den Balken und die Rueckfrage.
/// </summary>
public static partial class AutoMessung
{
    public const int PaketeAnsichtenAnzahl = 7;

    private const string ProbePaket = "tests/Lernplattform.Tests/Testdaten/pakete/probe_testkarte_1.fipaket";
    private const string EchtOrdner = "tests/Lernplattform.Tests/Testdaten/pakete_echt";

    /// <summary>Paket mit geaendertem Kopf neu packen (Pruefsummen bleiben gleich, Inhalt unveraendert).</summary>
    internal static byte[] PaketUmbauen(byte[] zip, Action<JsonObject> aendern)
    {
        using var ein = new MemoryStream(zip);
        var (dateien, _) = Paket.Entpacken(ein);
        var kopf = (JsonObject)JsonNode.Parse(dateien![Paket.KopfDatei])!;
        aendern(kopf);
        dateien[Paket.KopfDatei] = Encoding.UTF8.GetBytes(kopf.ToJsonString());
        using var aus = new MemoryStream();
        using (var neu = new ZipArchive(aus, ZipArchiveMode.Create, true))
        {
            foreach (var (name, inhalt) in dateien.OrderBy(d => d.Key == Paket.KopfDatei ? "" : d.Key, StringComparer.Ordinal))
            {
                using var s = neu.CreateEntry(name).Open();
                s.Write(inhalt);
            }
        }
        return aus.ToArray();
    }

    /// <summary>Legt die Testquelle an (Probe-Paket und Varianten) und setzt sie fuer das Programm; null, wenn die Dateien fehlen.</summary>
    private static string? PaketQuelleAnlegen(bool mitVarianten)
    {
        var echt = Path.Combine(EchtOrdner, "probe_klein_1.fipaket");
        if (!File.Exists(ProbePaket) || !File.Exists(echt))
        {
            return null;
        }
        var quelle = Path.Combine(Path.GetTempPath(), "lernplattform_paketquelle_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(quelle);
        File.Copy(ProbePaket, Path.Combine(quelle, Path.GetFileName(ProbePaket)));
        if (mitVarianten)
        {
            var klein = File.ReadAllBytes(echt);
            File.WriteAllBytes(Path.Combine(quelle, "probe_klein_1.fipaket"), klein);
            File.WriteAllBytes(Path.Combine(quelle, "probe_grafik_1.fipaket"), File.ReadAllBytes(Path.Combine(EchtOrdner, "probe_grafik_1.fipaket")));
            File.WriteAllBytes(Path.Combine(quelle, "probe_halb_1.fipaket"), klein[..(klein.Length / 2)]);
            File.WriteAllBytes(Path.Combine(quelle, "probe_neu_1.fipaket"), PaketUmbauen(klein, k =>
            {
                k["kennung"] = "probe_neu";
                k["name"] = "Probe: Format 2";
                k["format"] = 2;
            }));
            var kaputt = PaketUmbauen(klein, k =>
            {
                k["kennung"] = "probe_kaputt";
                k["name"] = "Probe: Prüfsumme falsch";
                k["dateien"]![Paket.StadtDatei]!["sha256"] = new string('0', 64);
            });
            File.WriteAllBytes(Path.Combine(quelle, "probe_kaputt_1.fipaket"), kaputt);
        }
        Environment.SetEnvironmentVariable(PaketQuelle.Umgebung, quelle);
        PaketDienst.NeuFuerTests();
        return quelle;
    }

    private static void PaketQuelleWeg(string? quelle)
    {
        // A 1.5.0: die vorab eingesetzten Fachrichtungs-Pakete (AE, DPA, DV gibt es nur noch als Paket) bleiben fuer die
        // folgenden Teile; entfernt werden nur die Probe-Pakete dieser Probe
        foreach (var z in PaketDienst.Verwaltung.Zeilen().Where(z => FachrichtungsPaket.FachrichtungAus(z.Kennung) is null))
        {
            PaketDienst.Verwaltung.Entfernen(z.Kennung);
        }
        Environment.SetEnvironmentVariable(PaketQuelle.Umgebung, null);
        OrdnerQuelle.Bremse = TimeSpan.Zero;
        OrdnerQuelle.HaltNachErstemBlock = TimeSpan.Zero;
        OptionenSeite.PaketMobilFuerTests = false;
        PaketDienst.NeuFuerTests();
        try
        {
            if (quelle is not null && Directory.Exists(quelle))
            {
                Directory.Delete(quelle, true);
            }
        }
        catch (IOException)
        {
        }
    }

    // Welle „Ablauf stabil“ (Sammelliste 413): ohne haenger.log (macOS schreibt dort bei verdecktem Fenster „verdeckt“);
    // jeder neue Eintrag dort wird am Ende der Probe einzeln geprueft (nur „verdeckt“ erlaubt)
    private static string DatenAbdruck(string ordner) => Lernplattform.Core.Diagnose.OrdnerAbdruck.Abdruck(ordner);

    private static Button? DialogKnopf(string text) => Dialog.Offen?.GetLogicalDescendants().OfType<Button>()
        .FirstOrDefault(b => b.Content is string s ? s == text : b.Content is TextBlock t && t.Text == text);

    private static async Task BedienenAsync(MainView ansicht, Button knopf, string art)
    {
        // Knoepfe der Karte entstehen nach jedem Schritt neu: erst sichtbar rollen und anordnen, sonst trifft ein Tippen daneben
        knopf.BringIntoView();
        await ansicht.BildFertigAsync();
        await ansicht.BildFertigAsync();
        switch (art)
        {
            case "Maus":
                await TippenAsync(ansicht, knopf, PointerType.Mouse);
                break;
            case "Finger":
                await TippenAsync(ansicht, knopf, PointerType.Touch);
                break;
            case "Eingabetaste":
                await TasteAsync(ansicht, knopf, Key.Enter);
                break;
            default:
                ControlAutomationPeer.CreatePeerForElement(knopf).GetProvider<IInvokeProvider>()?.Invoke();
                await ansicht.BildFertigAsync();
                break;
        }
    }

    private static async Task PaketWartenAsync(MainView ansicht, OptionenSeite optionen)
    {
        for (var i = 0; i < 600 && optionen.PaketLaedt; i++)
        {
            await Task.Delay(50);
            await ansicht.BildFertigAsync();
        }
        await ansicht.BildFertigAsync();
    }

    private static async Task<int> PaketeAsync(MainView ansicht)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Pakete: " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Pakete " + punkt + ": " + wert);
                funde++;
            }
        }
        var daten = Ablage.Datenordner();
        var vorher = DatenAbdruck(daten);
        var haengerVorher = Lernplattform.Core.Diagnose.OrdnerAbdruck.HaengerText(daten);
        var quelle = PaketQuelleAnlegen(mitVarianten: true);
        if (quelle is null)
        {
            Pruefe("Testquelle", false, "Probe-Pakete nicht gefunden (" + ProbePaket + ")");
            return funde;
        }
        try
        {
            await ansicht.ZeigeAsync(Seiten.Optionen);
            if (ansicht.GebauteSeiten.GetValueOrDefault(Seiten.Optionen) is not OptionenSeite optionen)
            {
                Pruefe("Optionen", false, "nicht gebaut");
                return funde;
            }
            optionen.OeffneBereich(Optionen.Pakete, false);
            optionen.PaketeNeu();
            await ansicht.BildFertigAsync();
            // A 0.91.0: vor der Suche stehen nur die drei Fachrichtungs-Pakete der App da („In der App enthalten“)
            Pruefe("Karte vor der Suche", optionen.PaketKarten.Count == 3 && FachrichtungsPaket.Fachrichtungen.All(fr => optionen.PaketKarten.ContainsKey(FachrichtungsPaket.Kennung(fr))),
                optionen.PaketKarten.Count + " Zeilen (nichts gesucht, nichts geladen; erwartet die drei Fachrichtungs-Pakete)");
            // A1 mit jeder Bedienart: suchen, laden, anzeigen, entfernen
            foreach (var art in new[] { "Maus", "Finger", "Eingabetaste", "Screenreader" })
            {
                if (optionen.PaketSuchenKnopf is not { } suchen)
                {
                    Pruefe(art + " Suchen", false, "Knopf fehlt");
                    break;
                }
                var nameSuchen = ControlAutomationPeer.CreatePeerForElement(suchen).GetName();
                Pruefe(art + " Name Suchen", nameSuchen == PaketTexte.Suchen, "„" + nameSuchen + "“");
                await BedienenAsync(ansicht, suchen, art);
                for (var i = 0; i < 100 && !suchen.IsEnabled; i++)
                {
                    await Task.Delay(20);
                    await ansicht.BildFertigAsync();
                }
                var gefunden = optionen.PaketKnoepfe.TryGetValue("probe_testkarte", out var k) && k.ContainsKey(PaketKnopf.Laden);
                Pruefe(art + " Suchen", gefunden && optionen.PaketKarten.Count == 9, optionen.PaketKarten.Count + " Pakete angeboten" + (gefunden ? ", Probe-Paket mit „Laden“" : ", Probe-Paket FEHLT"));
                if (!gefunden)
                {
                    continue;
                }
                var laden = k![PaketKnopf.Laden];
                Pruefe(art + " Name Laden", AutomationProperties.GetName(laden) == PaketTexte.Laden + ": Probe: Testkarte 256", AutomationProperties.GetName(laden) ?? "");
                await BedienenAsync(ansicht, laden, art);
                await PaketWartenAsync(ansicht, optionen);
                var ordner = Path.Combine(daten, PaketAblage.OrdnerName, "probe_testkarte", "1");
                var geladen = PaketDienst.Verwaltung.Zeilen().FirstOrDefault(z => z.Kennung == "probe_testkarte");
                Pruefe(art + " Laden", geladen?.Zustand == PaketZustand.Geladen && File.Exists(Path.Combine(ordner, Paket.StadtDatei))
                    && !Directory.Exists(Path.Combine(daten, PaketAblage.OrdnerName, PaketAblage.LadenName)),
                    (geladen?.ZustandText ?? "keine Zeile") + ", Ordner " + (Directory.Exists(ordner) ? "da" : "FEHLT"));
                if (!optionen.PaketKnoepfe.TryGetValue("probe_testkarte", out var k2) || !k2.TryGetValue(PaketKnopf.Entfernen, out var entfernen))
                {
                    Pruefe(art + " Entfernen", false, "Knopf fehlt");
                    continue;
                }
                await BedienenAsync(ansicht, entfernen, art);
                await ansicht.BildFertigAsync();
                var ja = DialogKnopf(TrainerTexte.Ja);
                Pruefe(art + " Rueckfrage Entfernen", ja is not null, ja is null ? "keine Rueckfrage" : "Rueckfrage da");
                ja?.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                for (var i = 0; i < 20 && Dialog.Offen is not null; i++)
                {
                    await ansicht.BildFertigAsync();
                }
                await ansicht.BildFertigAsync();
                var nachher = DatenAbdruck(daten);
                Pruefe(art + " Entfernen, Datenordner wie vorher", nachher == vorher && PaketDienst.Verwaltung.Zeilen().First(z => z.Kennung == "probe_testkarte").Zustand == PaketZustand.NichtGeladen,
                    nachher == vorher ? "gleich" : "ANDERS: " + string.Join(", ", nachher.Split('\n').Except(vorher.Split('\n')).Take(5)));
            }

            // A2: kaputte, abgeschnittene, fremde Art, zu neues Format: roter Grund, nichts halb
            foreach (var (kennung, grund) in new[]
            {
                ("probe_kaputt", PaketTexte.Format(PaketTexte.Pruefsumme, Paket.StadtDatei)),
                ("probe_halb", PaketTexte.KeinArchiv),
                ("probe_grafik", PaketTexte.ArtUnbekannt),
                ("probe_neu", PaketTexte.Format(PaketTexte.FormatNeu, 2, 1)),
            })
            {
                if (!optionen.PaketKnoepfe.TryGetValue(kennung, out var k) || !k.TryGetValue(PaketKnopf.Laden, out var laden))
                {
                    Pruefe("rot " + kennung, false, "Knopf Laden fehlt");
                    continue;
                }
                await BedienenAsync(ansicht, laden, "Maus");
                await PaketWartenAsync(ansicht, optionen);
                var zeile = PaketDienst.Verwaltung.Zeilen().FirstOrDefault(z => z.Kennung == kennung);
                var text = optionen.PaketKarten.TryGetValue(kennung, out var karte) ? TexteVon([karte]) : [];
                var soll = PaketTexte.Format(PaketTexte.ZustandFehler, grund);
                Pruefe("rot " + kennung, zeile?.Zustand == PaketZustand.Fehlerhaft && text.Contains(soll) && DatenAbdruck(daten) == vorher,
                    (zeile?.ZustandText ?? "keine Zeile") + (text.Contains(soll) ? "" : " (NICHT in der Karte)") + (DatenAbdruck(daten) == vorher ? ", Datenordner unveraendert" : ", Datenordner GEAENDERT"));
            }

            // A2: Abbruch mitten im Laden laesst den alten Stand (hier: nicht geladen) und keine Reste
            OrdnerQuelle.Bremse = TimeSpan.FromMilliseconds(250);   // 4 Bloecke je 16 KB: rund 1 s Laden, Zeit fuer den Knopf
            // Sammelliste 413: nach dem ersten Block haelt das Laden an, bis der Abbruch kommt (hoechstens 60 s); vorher war
            // das Paket auf einem langsamen Rechner schon fertig, bevor der Finger den Knopf traf (Lauf 1147, 2. Lauf)
            OrdnerQuelle.HaltNachErstemBlock = TimeSpan.FromSeconds(60);
            if (optionen.PaketKnoepfe.TryGetValue("probe_testkarte", out var kt) && kt.TryGetValue(PaketKnopf.Laden, out var ladenT))
            {
                await BedienenAsync(ansicht, ladenT, "Maus");
                for (var i = 0; i < 200 && (optionen.PaketBalken is not { Value: > 0 }); i++)
                {
                    await Task.Delay(20);
                    await ansicht.BildFertigAsync();
                }
                var balken = optionen.PaketBalken is { Value: > 0 } b ? b.Value : 0;
                var abbrechen = optionen.PaketKarten.TryGetValue("probe_testkarte", out var karte)
                    ? karte.GetLogicalDescendants().OfType<Button>().FirstOrDefault(x => AutomationProperties.GetName(x) == PaketTexte.Abbrechen + ": Probe: Testkarte 256") : null;
                if (abbrechen is not null)
                {
                    await BedienenAsync(ansicht, abbrechen, "Finger");
                }
                await PaketWartenAsync(ansicht, optionen);
                Pruefe("Abbruch mitten im Laden", balken > 0 && abbrechen is not null && optionen.PaketStatus?.Text == PaketTexte.Abgebrochen && DatenAbdruck(daten) == vorher,
                    $"Balken {balken:0.00}, " + (abbrechen is null ? "Knopf Abbrechen FEHLT" : "abgebrochen") + ", Status „" + optionen.PaketStatus?.Text + "“"
                    + (DatenAbdruck(daten) == vorher ? ", Datenordner wie vorher" : ", Datenordner GEAENDERT"));
            }
            OrdnerQuelle.Bremse = TimeSpan.Zero;
            OrdnerQuelle.HaltNachErstemBlock = TimeSpan.Zero;

            // A3: Rueckfrage ohne WLAN kommt VOR dem Laden (kein Balken), „Später“ laedt nichts, „Jetzt laden“ laedt
            OptionenSeite.PaketMobilFuerTests = true;
            optionen.PaketeNeu();
            await ansicht.BildFertigAsync();     // neue Knoepfe erst angeordnet, sonst trifft der Finger daneben
            await ansicht.BildFertigAsync();
            if (optionen.PaketKnoepfe.TryGetValue("probe_testkarte", out var km) && km.TryGetValue(PaketKnopf.Laden, out var ladenM))
            {
                ladenM.BringIntoView();
                await ansicht.BildFertigAsync();
                await BedienenAsync(ansicht, ladenM, "Finger");
                var spaeter = DialogKnopf(PaketTexte.MobilNein);
                for (var i = 0; i < 100 && spaeter is null; i++)
                {
                    await Task.Delay(20);
                    await ansicht.BildFertigAsync();
                    spaeter = DialogKnopf(PaketTexte.MobilNein);
                }
                Pruefe("Rueckfrage ohne WLAN vor dem Laden", spaeter is not null && optionen.PaketBalken is null && !optionen.PaketLaedt,
                    (spaeter is null ? "keine Rueckfrage" : "Rueckfrage da") + (optionen.PaketBalken is null ? ", kein Balken" : ", Balken SCHON DA"));
                spaeter?.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await ansicht.BildFertigAsync();
                await ansicht.BildFertigAsync();
                Pruefe("Spaeter im WLAN", !optionen.PaketLaedt && DatenAbdruck(daten) == vorher, DatenAbdruck(daten) == vorher ? "nichts geladen" : "GELADEN");
                if (optionen.PaketKnoepfe.TryGetValue("probe_testkarte", out var km2) && km2.TryGetValue(PaketKnopf.Laden, out var ladenM2))
                {
                    await BedienenAsync(ansicht, ladenM2, "Maus");
                    await ansicht.BildFertigAsync();
                    DialogKnopf(PaketTexte.MobilJa)?.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    await ansicht.BildFertigAsync();
                    await PaketWartenAsync(ansicht, optionen);
                    Pruefe("Jetzt laden", PaketDienst.Verwaltung.Zeilen().First(z => z.Kennung == "probe_testkarte").Zustand == PaketZustand.Geladen, "geladen");
                }
            }
            OptionenSeite.PaketMobilFuerTests = false;

            // A3: ohne Netz (Quelle nicht erreichbar) „Keine Verbindung“, das Geladene bleibt in der Liste
            Environment.SetEnvironmentVariable(PaketQuelle.Umgebung, Path.Combine(quelle, "gibt_es_nicht"));
            if (optionen.PaketSuchenKnopf is { } s2)
            {
                await BedienenAsync(ansicht, s2, "Maus");
                for (var i = 0; i < 100 && !s2.IsEnabled; i++)
                {
                    await Task.Delay(20);
                    await ansicht.BildFertigAsync();
                }
            }
            var bleibt = optionen.PaketKnoepfe.TryGetValue("probe_testkarte", out var kb) && kb.ContainsKey(PaketKnopf.Entfernen);
            Pruefe("ohne Netz", optionen.PaketStatus?.Text == PaketTexte.KeineVerbindung && bleibt,
                "Status „" + optionen.PaketStatus?.Text + "“" + (bleibt ? ", Probe-Paket weiter geladen" : ", Probe-Paket FEHLT"));
        }
        finally
        {
            Dialog.AlleSchliessen();
            PaketQuelleWeg(quelle);
            await ansicht.BildFertigAsync();
        }
        var ende = DatenAbdruck(daten);
        Pruefe("Datenordner am Ende wie vorher", ende == vorher, ende == vorher ? "gleich" : "ANDERS");
        var haengerNeu = Lernplattform.Core.Diagnose.OrdnerAbdruck.NeueHaengerEintraege(haengerVorher, Lernplattform.Core.Diagnose.OrdnerAbdruck.HaengerText(daten));
        Pruefe("Haenger-Diagnose waehrend der Probe nur verdeckt", Lernplattform.Core.Diagnose.OrdnerAbdruck.NurVerdeckt(haengerNeu),
            haengerNeu.Count == 0 ? "keine neuen Eintraege" : haengerNeu.Count + " neue Eintraege: " + string.Join(" | ", haengerNeu));
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        return funde;
    }

    /// <summary>
    /// Ansichten fuer die Layoutpruefung: Karte mit allen Zustaenden (geladen, neuer Stand, nicht geladen, rot fehlerhaft),
    /// „Keine Verbindung“, Laden mit Balken, Rueckfrage ohne WLAN.
    /// </summary>
    private static async IAsyncEnumerable<string> PaketeAnsichten(MainView ansicht)
    {
        await ansicht.ZeigeAsync(Seiten.Optionen);
        if (ansicht.GebauteSeiten.GetValueOrDefault(Seiten.Optionen) is not OptionenSeite optionen)
        {
            yield break;
        }
        var quelle = await PaketeZustaendeAsync();
        try
        {
            foreach (var bereich in optionen.Angezeigt)
            {
                if (optionen.Klappe(bereich.Id) is { } klappe)
                {
                    klappe.IsExpanded = bereich.Id == Optionen.Pakete;
                }
            }
            optionen.OeffneBereich(Optionen.Pakete, false);
            optionen.PaketeNeu();
            await ansicht.BildFertigAsync();
            optionen.Klappe(Optionen.Pakete)?.BringIntoView();
            await ansicht.BildFertigAsync();
            yield return "optionen/pakete";
            optionen.PaketStatusZeigen(PaketTexte.KeineVerbindung, true);
            await ansicht.BildFertigAsync();
            yield return "optionen/pakete-ohne-netz";
            optionen.PaketLaedtVorschau("probe_halb", 2048);
            await ansicht.BildFertigAsync();
            optionen.PaketKarten.GetValueOrDefault("probe_halb")?.BringIntoView();
            await ansicht.BildFertigAsync();
            yield return "optionen/pakete-laedt";
            OptionenSeite.PaketMobilFuerTests = true;
            optionen.PaketeNeu();
            var frage = PaketDienst.Verwaltung.Zeilen().FirstOrDefault(z => z.Kennung == "probe_halb") is { } z ? optionen.PaketLadenAsync(z) : Task.CompletedTask;
            await ansicht.BildFertigAsync();
            yield return "optionen/pakete-mobil";
            Dialog.AlleSchliessen();
            await Task.WhenAny(frage, Task.Delay(3000));
            OptionenSeite.PaketMobilFuerTests = false;
            // Welle A 0.91.0: Zeile eines Fachrichtungs-Pakets mit neuem Stand, Sperre beim offenen Pruefungssatz, Rueckfrage
            if (quelle is not null && File.Exists(Path.Combine(EchtOrdner, "lernen_ae_2.fipaket")))
            {
                File.Copy(Path.Combine(EchtOrdner, "lernen_ae_2.fipaket"), Path.Combine(quelle, "lernen_ae_2.fipaket"), true);
                await PaketDienst.Verwaltung.SuchenAsync();
                optionen.PaketeNeu();
                await ansicht.BildFertigAsync();
                optionen.PaketKarten.GetValueOrDefault("lernen_ae")?.BringIntoView();
                await ansicht.BildFertigAsync();
                yield return "optionen/pakete-fachrichtung";
                optionen.PaketStatusZeigen(PaketTexte.Format(PaketTexte.FrEntfernenGesperrt, PaketTexte.Fr("ae")), true);
                await ansicht.BildFertigAsync();
                yield return "optionen/pakete-fachrichtung-gesperrt";
                optionen.PaketStatusZeigen("", false);
                var fehlt = Programm.FachrichtungsPaketDienst.FehltAsync("ae");
                await ansicht.BildFertigAsync();
                yield return "optionen/pakete-fachrichtung-fehlt";
                Dialog.AlleSchliessen();
                await Task.WhenAny(fehlt, Task.Delay(3000));
            }
        }
        finally
        {
            Dialog.AlleSchliessen();
            PaketQuelleWeg(quelle);
            optionen.PaketeNeu();
            optionen.PaketStatusZeigen("", false);
        }
        await ansicht.BildFertigAsync();
    }

    /// <summary>
    /// Zustaende fuer Ansichten und Fotos: Probe-Paket geladen, „kleine Stadt“ geladen mit neuem Stand 2, unbekannte Art rot,
    /// abgeschnittenes Paket nicht geladen. Gibt den Ordner der Testquelle zurueck.
    /// </summary>
    internal static async Task<string?> PaketeZustaendeAsync()
    {
        var quelle = PaketQuelleAnlegen(mitVarianten: true);
        if (quelle is null)
        {
            return null;
        }
        File.Delete(Path.Combine(quelle, "probe_neu_1.fipaket"));
        File.Delete(Path.Combine(quelle, "probe_kaputt_1.fipaket"));
        var v = PaketDienst.Verwaltung;
        await v.SuchenAsync();
        foreach (var kennung in new[] { "probe_testkarte", "probe_klein", "probe_grafik" })
        {
            if (v.Angebote.FirstOrDefault(a => a.Kennung == kennung) is { } a)
            {
                await v.LadenAsync(a, null, default);
            }
        }
        File.WriteAllBytes(Path.Combine(quelle, "probe_klein_2.fipaket"),
            PaketUmbauen(File.ReadAllBytes(Path.Combine(quelle, "probe_klein_1.fipaket")), k => k["stand"] = 2));
        await v.SuchenAsync();
        return quelle;
    }
}
