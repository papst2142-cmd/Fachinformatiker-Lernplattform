using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 1 (Plan Abschnitt 4, Weisung P1-2 3.2/3.3): Start ohne Bedienung misst erstes
/// Oeffnen jeder Seite, Wechsel Hell/Dunkel und Wechsel der Schriftstufe und prueft
/// jede Seite in jeder Schriftstufe schmal (384 x 832 Punkte wie das S24 hochkant), an der
/// Grenze zum breiten Aufbau und in der PC-Startgroesse 1360 x 880 mit der Layoutpruefung.
/// Nichts wird gespeichert. Rueckgabe: Anzahl Funde (ueber 1 000 ms oder Layout).
/// </summary>
public static class AutoMessung
{
    public const double Abbruch = 1000;
    public const double Geraetegrenze = 400;
    public const double SchmalBreite = 384;
    public const double SchmalHoehe = 832;
    public const double PcBreite = 1360;
    public const double PcHoehe = 880;

    public static async Task<int> LaufAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        var inhalte = await Zustand.Inhalte;
        // Erstes Oeffnen jeder Seite (Seitenleiste, untere Leiste, Kopfleiste)
        var ids = Texte.NavigationPc.SelectMany(p => p.Unter.Prepend(p)).Select(p => p.Id)
            .Concat(Texte.NavigationHandy.Select(p => p.Id))
            .Concat([Seiten.Lernen, Seiten.Hilfe, Seiten.Suche])
            .Distinct().ToList();
        foreach (var id in ids)
        {
            await ansicht.ZeigeAsync(id);
        }
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        if (ansicht.GebauteSeiten[Seiten.Dashboard] is DashboardSeite dashboard)
        {
            await dashboard.Fertig.Task;
            await ansicht.BildFertigAsync();
            Messwerte.Notiere(string.Create(inv, $"Dashboard mit Werten fertig: {Messwerte.SeitStart.Elapsed.TotalMilliseconds:F0} ms nach Start"));
        }

        // Phase 2: Karteikarten und Pruefungstrainer lesen Lernstand und Reihenfolge im Hintergrund;
        // gemessen wird vom Bauen der Seite bis die erste Karte bzw. die Vorbereitung steht
        if (ansicht.GebauteSeiten[Seiten.Karteikarten] is KarteikartenSeite kartenSeite
            && ansicht.GebauteSeiten[Seiten.Trainer] is TrainerSeite trainerSeite)
        {
            await kartenSeite.Geladen;
            await trainerSeite.Geladen;
            foreach (var (titel, ms) in new[] { ("Karteikarten bis erste Karte", kartenSeite.InhaltMs), ("Pruefungstrainer bis Vorbereitung", trainerSeite.InhaltMs) })
            {
                Messwerte.Notiere(string.Create(inv, $"{titel}: {ms ?? -1:F0} ms"));
                if (ms is null or > Abbruch)
                {
                    Messwerte.Notiere("  FEHLER: nicht fertig oder ueber 1000 ms");
                    funde++;
                }
            }
        }

        // Wechsel Hell/Dunkel und Schriftstufe (je 3 Durchgaenge)
        var werte = Zustand.Werte;
        for (var runde = 0; runde < 3; runde++)
        {
            foreach (var darstellung in (string[])[Farbwelt.Hell, Farbwelt.Dunkel])
            {
                var uhr = Stopwatch.StartNew();
                FarbenAnwenden.Anwenden(Farbwelt.Berechne(darstellung, Einstellungen.Grundfarbe(werte), Einstellungen.Hintergrund(werte)));
                await ansicht.BildFertigAsync();
                Messwerte.Reihe("Wechsel Hell/Dunkel", uhr.Elapsed.TotalMilliseconds);
            }
            foreach (var stufe in Einstellungen.Schriftgroessen.Append(Einstellungen.SchriftNormal))
            {
                var uhr = Stopwatch.StartNew();
                Schrift.Anwenden(Zustand.SchriftFaktor(stufe));
                await ansicht.BildFertigAsync();
                Messwerte.Reihe("Wechsel Schriftstufe", uhr.Elapsed.TotalMilliseconds);
            }
        }

        // Layoutpruefung je Seite und Schriftstufe in festen Fenstergroessen: schmal (Handy),
        // an der Grenze zum breiten Aufbau (MainView.BreitGrenze, engster PC-Aufbau) und
        // Startgroesse der Python-Linie (1360 x 880). Ohne Fenster (Handy) nur wie gestartet.
        var fenster = TopLevel.GetTopLevel(ansicht) as Window;
        var layoutFunde = new List<string>();
        var gepruefteGroessen = new List<string>();
        var groessenJeStufe = 0;
        foreach (var stufe in Einstellungen.Schriftgroessen)
        {
            var faktor = Zustand.SchriftFaktor(stufe);
            Schrift.Anwenden(faktor);
            var groessen = fenster is null
                ? new List<(string Name, double Breite, double Hoehe)> { ("wie gestartet", 0, 0) }
                : [("schmal", SchmalBreite, SchmalHoehe), ("grenze", Math.Ceiling(MainView.BreitGrenze), PcHoehe), ("pc", PcBreite, PcHoehe)];
            groessenJeStufe = groessen.Count;
            foreach (var (name, breite, hoehe) in groessen)
            {
                if (fenster is not null)
                {
                    // Nie groesser als der Bildschirm (Windows-CI: 1024 x 768): sonst rechnet
                    // Avalonia mit der verlangten Breite, das System zeigt aber weniger
                    var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
                    fenster.Width = Math.Min(breite, hoechstB);
                    fenster.Height = Math.Min(hoehe, hoechstH);
                    await ansicht.BildFertigAsync();
                    await ansicht.BildFertigAsync();
                }
                var ist = string.Create(inv, $"{name} {ansicht.Bounds.Width:0}x{ansicht.Bounds.Height:0}");
                gepruefteGroessen.Add(stufe + " " + ist);
                foreach (var id in ansicht.GebauteSeiten.Keys.ToList())
                {
                    await ansicht.ZeigeAsync(id);
                    if (ansicht.GebauteSeiten[id] is DashboardSeite neuGerechnet)
                    {
                        // Dashboard rechnet beim erneuten Zeigen neu: erst die neuen Karten pruefen
                        await neuGerechnet.Geladen;
                        await ansicht.BildFertigAsync();
                    }
                    if (ansicht.GebauteSeiten[id] is IAufklappbar klappbar)
                    {
                        klappbar.AllesAufklappen();
                        await ansicht.BildFertigAsync();
                    }
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {id}: {fund}");
                    }
                }
                // Phase 2: Ansichten nach dem Pruefen, Ergebnis und Fenster der Uebernahme
                await foreach (var (ansichtName, _) in W2Ansichten(ansicht, inhalte))
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
            }
        }

        // Phase 2 (Weisung P2-4): alle 72 Farbwelten mit den neuen Ansichten (Layout und Lesbarkeit)
        funde += await FarbweltenAsync(ansicht, inhalte, layoutFunde);
        Zustand.DesignAnwenden();

        Messwerte.Notiere(string.Create(inv, $"Layoutpruefung (Woerter, Knoepfe, Rand), {Einstellungen.Schriftgroessen.Count} Schriftstufen x {ansicht.GebauteSeiten.Count} Seiten x {groessenJeStufe} Groessen: {layoutFunde.Count} Funde"));
        Messwerte.Notiere("  dazu je Stufe und Groesse 9 Ansichten der Welle 2: Karteikarten nach dem Pruefen (3 Modi, laengster Themenname), Pruefungstrainer Frage und Ergebnis, Uebernahme Vorschau, Ergebnis und Fehler, Rueckfrage Abbrechen");
        Messwerte.Notiere("  Groessen: " + string.Join(", ", gepruefteGroessen));
        foreach (var fund in layoutFunde.Distinct().Take(60))
        {
            Messwerte.Notiere("  " + fund);
        }
        funde += layoutFunde.Count;

        // Weisung P1-3: Optionen zweimal oeffnen baut die Seite nicht neu, die
        // Sicherung (geteilte Felder _zustand, _ergebnis) bleibt an ihrem Platz
        var optionen = ansicht.GebauteSeiten[Seiten.Optionen];
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        await ansicht.ZeigeAsync(Seiten.Optionen);
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        await ansicht.ZeigeAsync(Seiten.Optionen);
        var einmal = ReferenceEquals(optionen, ansicht.GebauteSeiten[Seiten.Optionen]) && ansicht.SicherungInOptionen(optionen);
        Messwerte.Notiere(einmal
            ? "Optionen zweimal geoeffnet: dieselbe Seite, Sicherung an ihrem Platz"
            : "Optionen zweimal geoeffnet: FEHLER (Seite neu gebaut oder Sicherung nicht in Optionen)");
        funde += einmal ? 0 : 1;
        var zuLang = Messwerte.ErstesOeffnen.Where(e => e.Value > Abbruch).Select(e => e.Key).ToList();
        foreach (var (reihe, ms) in Messwerte.Hoechstwerte().Where(r => r.Value > Abbruch))
        {
            zuLang.Add(reihe);
        }
        Messwerte.Notiere(zuLang.Count == 0
            ? "Zeitgrenze CI: alle Werte unter 1000 ms"
            : "Zeitgrenze CI ueberschritten (ueber 1000 ms): " + string.Join(", ", zuLang));
        funde += zuLang.Count;
        Messwerte.Notiere(funde == 0 ? "URTEIL: gruen" : string.Create(inv, $"URTEIL: rot ({funde} Funde)"));
        return funde;
    }

    /// <summary>
    /// Die neuen Ansichten der Welle 2 nacheinander zeigen (je nach dem Zeigen pruefen):
    /// Karteikarten je Modus nach dem Pruefen (laengster Themenname), Pruefungstrainer mit
    /// Frage und mit Ergebnis, Vorschau, Ergebnis und laengste Fehlermeldung der Uebernahme.
    /// Nichts wird gespeichert.
    /// </summary>
    private static async IAsyncEnumerable<(string Name, int Nr)> W2Ansichten(MainView ansicht, Lerninhalte? inhalte)
    {
        if (inhalte is null || ansicht.GebauteSeiten[Seiten.Karteikarten] is not KarteikartenSeite karten
            || ansicht.GebauteSeiten[Seiten.Trainer] is not TrainerSeite trainer)
        {
            yield break;
        }
        await ansicht.ZeigeAsync(Seiten.Karteikarten);
        for (var art = 0; art < 3; art++)
        {
            karten.Probe(art);
            await ansicht.BildFertigAsync();
            yield return ("cards/" + KartenTexte.Modi[art].Wert, art);
        }
        karten.Probe(-1);
        await ansicht.ZeigeAsync(Seiten.Trainer);
        for (var art = 0; art < 2; art++)
        {
            trainer.Probe(art, inhalte);
            await ansicht.BildFertigAsync();
            yield return (art == 0 ? "quiz/frage" : "quiz/ergebnis", art);
        }
        trainer.Probe(-1, inhalte);
        var fenster = new (string Name, Func<Task<int>> Zeigen)[]
        {
            ("uebernahme/vorschau", () => Dialog.Zeigen(UebernahmeTexte.Titel, UebernahmeTeil.VorschauInhalt(ProbeBericht(), ProbeSicherung()),
                UebernahmeTexte.Zusammenfuehren, UebernahmeTexte.Abbrechen)),
            ("uebernahme/ergebnis", () => Dialog.Zeigen(UebernahmeTexte.Titel,
                UebernahmeTexte.Ergebnis(ProbeBericht(), UebernahmeTexte.KopieName("vor_uebernahme_2026-10-20_120000_2.db", !ansicht.Breit)), Dialog.Ok)),
            ("uebernahme/fehler", () => Dialog.Zeigen(UebernahmeTexte.Titel, UebernahmeTexte.SchreibenGescheitert, Dialog.Ok)),
            ("trainer/rueckfrage", () => Dialog.Zeigen(TrainerTexte.Abbrechen, TrainerTexte.AbbrechenFrage, TrainerTexte.Ja, TrainerTexte.Nein)),
        };
        for (var i = 0; i < fenster.Length; i++)
        {
            _ = fenster[i].Zeigen();
            await ansicht.BildFertigAsync();
            yield return (fenster[i].Name, 10 + i);
            Dialog.AlleSchliessen();
        }
        await ansicht.BildFertigAsync();
    }

    /// <summary>Bericht mit allen Zeilen und Hinweisen (grosse Zahlen), nur fuer die Anzeige.</summary>
    private static UebernahmeBericht ProbeBericht() =>
        new(UebernahmeQuelle.Sicherung, true, 12345, 12345, 23456, 1234, 0, 2345, 0, 0, 1, 1, 0,
            new Dictionary<string, string>(), [], null, 1);

    /// <summary>Kleine Sicherung (gzip-JSON) fuer die Zeilen der Vorschau.</summary>
    private static byte[] ProbeSicherung()
    {
        var json = "{\"art\":\"fisi-lernstand\",\"format\":1,\"app_version\":\"0.63.2\",\"erstellt\":\"2026-10-20T12:00:00\","
            + "\"geraet\":\"Testgeraet mit einem recht langen Namen\",\"daten\":{}}";
        using var puffer = new MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(puffer, System.IO.Compression.CompressionLevel.Fastest, true))
        {
            gz.Write(System.Text.Encoding.UTF8.GetBytes(json));
        }
        return puffer.ToArray();
    }

    // Vordergrund-Schluessel der neuen Ansichten und ihre Flaechen (Lesbarkeit je Farbwelt)
    private static readonly (string Text, string Flaeche)[] W2Paare =
    [
        ("text", "card"), ("text_soft", "card"), ("text_soft", "card_alt"), ("text_dim", "card"), ("text_dim", "card_alt"),
        ("muted", "card"), ("accent", "card"), ("green", "card"), ("red", "card"), ("yellow", "card"),
    ];

    /// <summary>
    /// Alle 72 Farbwelten (6 Grundfarben x 6 Hintergruende x hell/dunkel): neue Ansichten in der
    /// Groesse wie gerade eingestellt zeigen und pruefen; Kontrast der verwendeten Textfarben auf
    /// ihrer Flaeche festhalten (Fund nur bei Text unter 4,6 bzw. Farbe unter 3,0).
    /// </summary>
    private static async Task<int> FarbweltenAsync(MainView ansicht, Lerninhalte? inhalte, List<string> layoutFunde)
    {
        var inv = CultureInfo.InvariantCulture;
        Schrift.Anwenden(1.0);
        var funde = 0;
        var anzahl = 0;
        var tiefst = new Dictionary<string, (double Wert, string Welt)>(StringComparer.Ordinal);
        foreach (var darstellung in (string[])[Farbwelt.Hell, Farbwelt.Dunkel])
        {
            foreach (var grund in Farbwelt.Grundfarben)
            {
                foreach (var hinter in Farbwelt.Hintergruende)
                {
                    var palette = Farbwelt.Berechne(darstellung, grund.Id, hinter.Id);
                    FarbenAnwenden.Anwenden(palette);
                    anzahl++;
                    var welt = darstellung + "/" + grund.Id + "/" + hinter.Id;
                    await foreach (var (name, nr) in W2Ansichten(ansicht, inhalte))
                    {
                        if (nr is 1 or 10)
                        {
                            foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                            {
                                layoutFunde.Add($"{welt} {name}: {fund}");
                            }
                        }
                    }
                    foreach (var (text, flaeche) in W2Paare)
                    {
                        var wert = Farbwelt.Kontrast(palette.Farben[text], palette.Farben[flaeche]);
                        var schluessel = text + " auf " + flaeche;
                        if (!tiefst.TryGetValue(schluessel, out var alt) || wert < alt.Wert)
                        {
                            tiefst[schluessel] = (wert, welt);
                        }
                    }
                }
            }
        }
        Messwerte.Notiere(string.Create(inv, $"Farbwelten mit den Ansichten der Welle 2 geprueft: {anzahl}"));
        foreach (var (schluessel, (wert, welt)) in tiefst)
        {
            var grenze = schluessel.StartsWith("text", StringComparison.Ordinal) || schluessel.StartsWith("muted", StringComparison.Ordinal)
                ? Farbwelt.MindestKontrast : Farbwelt.BedienKontrast;
            var zuWenig = wert < grenze;
            funde += zuWenig ? 1 : 0;
            Messwerte.Notiere(string.Create(inv, $"  Kontrast {schluessel}: kleinster Wert {wert:F2} ({welt}){(zuWenig ? " UNTER " + grenze.ToString("F1", inv) : "")}"));
        }
        return funde;
    }

    // Groesste Innenflaeche des Fensters auf seinem Bildschirm (Arbeitsbereich ohne Rahmen)
    private static (double Breite, double Hoehe) Hoechstgroesse(Window fenster)
    {
        if (fenster.Screens.ScreenFromVisual(fenster) is not { } bildschirm)
        {
            return (double.MaxValue, double.MaxValue);
        }
        var rahmen = fenster.FrameSize is { } aussen ? aussen - fenster.ClientSize : default;
        return (Math.Floor(bildschirm.WorkingArea.Width / bildschirm.Scaling - rahmen.Width),
            Math.Floor(bildschirm.WorkingArea.Height / bildschirm.Scaling - rahmen.Height));
    }
}

/// <summary>Seite mit Klappbereichen; fuer die Layoutpruefung alles aufklappen.</summary>
public interface IAufklappbar
{
    void AllesAufklappen();
}
