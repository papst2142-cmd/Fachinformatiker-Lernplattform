using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
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
/// Welle A 0.89.0 (Inhaltspakete W3, Weisung P12-297 W3-4 und B) im Start-Test: echtes Laden ueber die GitHub-Quelle von einem
/// nachgestellten GitHub (<see cref="ProbeGithub"/>, Release-Liste wie <c>paket_release.yml</c> sie anlegt). In den Optionen:
/// Suchen zeigt nur die Pakete (kein Entwurf, kein Vorab-Release, kein Programm-Release), ein Paket mit falscher Pruefsumme ist rot
/// und laesst den Datenordner gleich, das Probe-Paket <c>probe_quests</c> laedt gegen den digest. Danach im Spiel: Zug in die
/// Paket-Stadt, vor dem Moebelhaus „Auftrag annehmen“ bei Greta Sommer, Ticket loesen; die Zeile <c>ticket_erledigt</c> traegt den
/// Vorsatz, die Tickets von Stadt 1 bleiben. Jeder Knopf mit einer anderen Bedienung (Maus, Finger, Eingabetaste, Screenreader).
/// </summary>
public static partial class AutoMessung
{
    private const string QuestPaket = "probe_quests";

    private static async Task<int> PaketGithubAsync(MainView ansicht)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Paket GitHub: " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Paket GitHub " + punkt + ": " + wert);
                funde++;
            }
        }
        if (!File.Exists(Path.Combine(EchtOrdner, "github_releases.json")) || !File.Exists(Path.Combine(EchtOrdner, "probe_quests_1.fipaket")))
        {
            Pruefe("Probe-Liste", false, "github_releases.json oder probe_quests_1.fipaket fehlt in " + EchtOrdner);
            return funde;
        }
        var daten = Ablage.Datenordner();
        using var github = new ProbeGithub(EchtOrdner);
        Environment.SetEnvironmentVariable(PaketQuelle.Umgebung, github.Liste);
        PaketDienst.NeuFuerTests();
        string? lauf = null;
        SpielSitzung? sitzung = null;
        SpielSeite? spielSeite = null;
        string? alterLauf = null;
        var alteWahl = false;
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
            var vorher = DatenAbdruck(daten);
            if (optionen.PaketSuchenKnopf is { } suchen)
            {
                await BedienenAsync(ansicht, suchen, "Maus");
                for (var i = 0; i < 200 && !suchen.IsEnabled; i++)
                {
                    await Task.Delay(20);
                    await ansicht.BildFertigAsync();
                }
            }
            var karten = optionen.PaketKarten.Keys.Order(StringComparer.Ordinal).ToList();
            Pruefe("Suchen nur Pakete", karten.SequenceEqual([.. FachrichtungsPaket.Fachrichtungen.Select(FachrichtungsPaket.Kennung), "probe_kaputt", QuestPaket]) && github.Abfragen.Contains("/releases"),
                string.Join(", ", karten) + " (Entwurf, Vorab und Programm-Release nicht dabei; die drei Fachrichtungs-Zeilen sind in der App enthalten) | Abfragen: " + string.Join(", ", github.Abfragen));

            // falsche Pruefsumme (Eingabetaste): roter Grund, nichts geladen, Datenordner gleich
            if (optionen.PaketKnoepfe.GetValueOrDefault("probe_kaputt")?.GetValueOrDefault(PaketKnopf.Laden) is { } kaputt)
            {
                await BedienenAsync(ansicht, kaputt, "Eingabetaste");
                await PaketWartenAsync(ansicht, optionen);
            }
            var soll = PaketTexte.Format(PaketTexte.ZustandFehler, PaketTexte.Format(PaketTexte.Pruefsumme, "probe_kaputt_1.fipaket"));
            var text = optionen.PaketKarten.TryGetValue("probe_kaputt", out var karte) ? TexteVon([karte]) : [];
            Pruefe("Falsche Pruefsumme rot", text.Contains(soll) && DatenAbdruck(daten) == vorher && github.Abfragen.Contains("/probe_quests_1.fipaket"),
                (text.Contains(soll) ? soll : "Grund FEHLT: " + string.Join(" | ", text)) + (DatenAbdruck(daten) == vorher ? ", Datenordner unveraendert" : ", Datenordner GEAENDERT"));

            // Probe-Paket laden (Finger), gegen den digest geprueft
            if (optionen.PaketKnoepfe.GetValueOrDefault(QuestPaket)?.GetValueOrDefault(PaketKnopf.Laden) is { } laden)
            {
                await BedienenAsync(ansicht, laden, "Finger");
                await PaketWartenAsync(ansicht, optionen);
            }
            var geladen = PaketDienst.Verwaltung.Zeilen().FirstOrDefault(z => z.Kennung == QuestPaket);
            Pruefe("Laden von GitHub", geladen?.Zustand == PaketZustand.Geladen && PaketDienst.Verwaltung.Staedte().Any(s => s.Kopf.Kennung == QuestPaket)
                && geladen.Angebot?.Sha256 is { Length: 64 },
                (geladen?.ZustandText ?? "keine Zeile") + ", digest " + (geladen?.Angebot?.Sha256 ?? "FEHLT"));

            // B: Quest in der Paket-Stadt mit einem eigenen Durchgang
            var seite = await SpielSeiteAsync(ansicht);
            if (seite?.Spiel is null || seite.Tafel is null || SpielStart.Aktiv is not { } s)
            {
                Pruefe("Spielseite", false, "nicht gebaut");
                return funde;
            }
            sitzung = s;
            spielSeite = seite;
            var verwaltung = sitzung.Verwaltung;
            alterLauf = verwaltung.Lauf;
            alteWahl = sitzung.PlatzGewaehlt;
            var platz = Enumerable.Range(1, 9).Select(p => (long)p).First(p => verwaltung.Belegung().LaufAuf(p) is null);
            lauf = verwaltung.NeuerLauf(platz);
            verwaltung.ZumSchreiben().Profil("Nico", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
            sitzung.PlatzGewaehlt = true;
            sitzung.Abgleichen();
            seite.TafelZeigen();
            seite.Tafel.IsVisible = false;
            await ansicht.BildFertigAsync();
            var spiel = sitzung.Spiel;
            spiel.GeheZu(Stadt.Bahnhof);
            spiel.SofortAnkommen();
            spiel.FahrplanNeu();
            await ansicht.BildFertigAsync();
            if (seite.OrtKnoepfe.GetValueOrDefault(StadtSpiel.AktionZug + QuestPaket) is { } zug)
            {
                await BedienenAsync(ansicht, zug, "Screenreader");
            }
            await ansicht.BildFertigAsync();
            var dort = sitzung.Spiel;
            Pruefe("Zug in die Paket-Stadt", dort.Stadt.Kennung == QuestPaket && dort.Jetzt.Ort == Stadt.Bahnhof,
                "Stadt " + (dort.Stadt.Kennung ?? "Stadt 1") + ", Ort " + dort.Jetzt.Ort + " | Knoepfe: " + string.Join(", ", seite.OrtKnoepfe.Keys));
            var ticketsVorher = dort.Ablauf.Stand.OffeneTickets().Count;
            dort.GeheZu("moebelhaus");
            dort.SofortAnkommen();
            await ansicht.BildFertigAsync();
            var aufgabe = QuestPaket + ":neu-q-1";
            var annehmen = seite.OrtKnoepfe.GetValueOrDefault("auftrag:" + aufgabe);
            Pruefe("Greta bietet den Auftrag an", KnopfName(annehmen) == "Auftrag annehmen" && dort.Zeile.Contains("Greta hat einen Auftrag", StringComparison.Ordinal),
                dort.Zeile.Replace('\n', ' ') + " | Knoepfe: " + string.Join(", ", seite.OrtKnoepfe.Keys));
            if (annehmen is not null)
            {
                await BedienenAsync(ansicht, annehmen, "Maus");
            }
            await ansicht.BildFertigAsync();
            var tafel = seite.Tafel;
            var auftrag = PaketQuests.Auftrag(dort.Stadt, aufgabe);
            var richtig = auftrag is null ? null : Py.Str(auftrag["antwort"]);
            Pruefe("Ticket offen", tafel.TicketOffen && tafel.IsVisible && richtig is not null, tafel.TicketOffen ? "Ticket „" + Py.Str(auftrag?["titel"]) + "“" : "KEIN Ticket");
            var option = tafel.GetLogicalDescendants().OfType<Klickflaeche>().FirstOrDefault(k => AutomationProperties.GetName(k) == richtig);
            if (option is not null)
            {
                await TippenAsync(ansicht, option, PointerType.Touch);
            }
            var einreichen = tafel.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => KnopfName(b) == "Lösung einreichen");
            if (einreichen is not null)
            {
                await BedienenAsync(ansicht, einreichen, "Eingabetaste");
            }
            await ansicht.BildFertigAsync();
            var stand = sitzung.Spiel.Ablauf.Stand;
            var letzte = stand.Verlauf[^1];
            Pruefe("Quest geloest", letzte.Art == Ereignisse.Erledigt && Py.Gleich(SpielDaten.Get(letzte.Daten, "aufgabe"), aufgabe)
                && Py.Wahr(SpielDaten.Get(letzte.Daten, "richtig")) && stand.Geloest.Contains(aufgabe) && stand.OffeneTickets().Count == ticketsVorher,
                "letzte Zeile " + letzte.Art + " " + PythonJson.Standard(letzte.Daten) + ", Tickets des Tages " + ticketsVorher + " -> " + stand.OffeneTickets().Count
                + (option is null ? " (Antwort nicht gefunden)" : "") + (einreichen is null ? " (Knopf Einreichen fehlt)" : ""));
            Pruefe("Kette geht weiter", !PaketQuests.Offen(sitzung.Spiel.Stadt, stand).Any(a => Py.Gleich(a["id"], aufgabe)),
                "offen: " + string.Join(", ", PaketQuests.Offen(sitzung.Spiel.Stadt, stand).Select(a => Py.Str(a["id"]))));
        }
        catch (Exception x) when (x is SpielFehler or InvalidOperationException or IOException or KeyNotFoundException)
        {
            Pruefe("Ablauf", false, x.GetType().Name + ": " + x.Message);
        }
        finally
        {
            if (sitzung is not null)
            {
                spielSeite?.Tafel?.AllesSchliessen();
                if (lauf is not null)
                {
                    sitzung.Verwaltung.LaufLoeschen(lauf);
                    if (alterLauf is not null)
                    {
                        sitzung.Verwaltung.LaufWaehlen(alterLauf);
                    }
                }
                sitzung.PlatzGewaehlt = alteWahl;
                sitzung.Abgleichen();
            }
            PaketQuelleWeg(null);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }
}
