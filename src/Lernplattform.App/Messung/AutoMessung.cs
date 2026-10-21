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

        funde += await W3MesspunkteAsync(ansicht, inhalte);
        funde += await FarbreglerAsync(ansicht);

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
                    if (ansicht.GebauteSeiten[id] is FortschrittSeite fortschritt)
                    {
                        await fortschritt.Geladen;
                        await ansicht.BildFertigAsync();
                    }
                    if (ansicht.GebauteSeiten[id] is SucheSeite suche)
                    {
                        await AlleZeilenAsync(ansicht, suche);
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
        Messwerte.Notiere("  darin Welle 3: Fortschritt (je Ebene der Abdeckung die erste Zeile aufgeklappt) und Suche mit den Treffern zu \"Subnetz\"");
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

    // Welle 3: Prozent der Abdeckung (rot, orange, gruen) auf Karte und Zeile, Hinweis "aktuelles Lernfeld"
    private static readonly (string Text, string Flaeche)[] W3Paare =
    [
        ("text", "card_alt"), ("muted", "card_alt"), ("red", "card_alt"), ("orange", "card"), ("orange", "card_alt"),
        ("green", "card_alt"), ("accent", "card_alt"),
    ];

    /// <summary>Wartet, bis die Suche alle geplanten Zeilen gezeigt hat.</summary>
    private static async Task AlleZeilenAsync(MainView ansicht, SucheSeite suche)
    {
        for (var i = 0; i < 200 && !suche.Fertig; i++)
        {
            await ansicht.BildFertigAsync();
        }
        await ansicht.BildFertigAsync();
    }

    /// <summary>
    /// Welle 3 (Plan Abschnitt 11): Fortschritt bis Werte und Abdeckung stehen, Suche "Subnetz"
    /// bis zu den ersten Treffern, Faelligkeit bei 100 000 synthetischen Antworten. Fund ab 1 000 ms.
    /// </summary>
    private static async Task<int> W3MesspunkteAsync(MainView ansicht, Lerninhalte? inhalte)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Messpunkt(string titel, double? ms)
        {
            Messwerte.Notiere(string.Create(inv, $"{titel}: {ms ?? -1:F0} ms{(ms > Geraetegrenze ? " (ueber 400)" : "")}"));
            if (ms is null or > Abbruch)
            {
                Messwerte.Notiere("  FEHLER: nicht fertig oder ueber 1000 ms");
                funde++;
            }
        }
        if (ansicht.GebauteSeiten[Seiten.Fortschritt] is FortschrittSeite fortschritt)
        {
            await fortschritt.Fertig.Task;
            Messpunkt("Fortschritt bis Werte und Abdeckung", fortschritt.FertigMs);
        }
        await ansicht.ZeigeAsync(Seiten.Suche);
        if (ansicht.GebauteSeiten[Seiten.Suche] is SucheSeite suche)
        {
            await Zustand.Suchtext;
            var uhr = Stopwatch.StartNew();
            var ms = await suche.SuchenAsync("Subnetz");
            await ansicht.BildFertigAsync();
            Messpunkt("Suche \"Subnetz\" bis zu den ersten Treffern (gezeichnet)", ms is null ? null : uhr.Elapsed.TotalMilliseconds);
            await AlleZeilenAsync(ansicht, suche);
        }
        if (inhalte is not null)
        {
            // 100 000 Antworten auf Karteikarten und Pruefungsfragen, feste Zufallsfolge, seit dem 01.10.2026
            var zufall = new Core.Zufall.PythonZufall(100000);
            var schluessel = inhalte.Karteikarten.Select(k => (Core.Lernen.Quellen.Karte, k.Frage!))
                .Concat(inhalte.Quizfragen.Select(q => (Core.Lernen.Quellen.Quiz, q.Frage!))).ToList();
            var heute = DateOnly.FromDateTime(DateTime.Now);
            var tage = Math.Max(1, heute.DayNumber - Core.Lernen.Wiederholung.Start.DayNumber + 1);
            var zeilen = new List<(string Quelle, string Schluessel, string Zeit, bool Richtig)>();
            for (var i = 0; i < 100000; i++)
            {
                var (quelle, frage) = schluessel[(int)zufall.RandRange(schluessel.Count)];
                var tag = Core.Lernen.Wiederholung.Start.AddDays((int)zufall.RandRange(tage));
                zeilen.Add((quelle, frage, tag.ToString("yyyy-MM-dd", inv) + " 12:00:00", (int)zufall.RandRange(3) != 0));
            }
            zeilen.Sort((a, b) => string.CompareOrdinal(a.Zeit, b.Zeit));
            var uhr = Stopwatch.StartNew();
            var buch = new Core.Lernen.Antwortbuch();
            foreach (var z in zeilen)
            {
                buch.Hinzufuegen(z.Quelle, z.Schluessel, z.Zeit, z.Richtig);
            }
            var msBuch = uhr.Elapsed.TotalMilliseconds;
            var plan = Core.Lernen.Wiederholungsplan.Aus(new Core.Lernen.StatusBuch(buch), heute, inhalte);
            var msPlan = uhr.Elapsed.TotalMilliseconds - msBuch;
            var quelleErst = plan.ErsteQuelle();
            if (quelleErst is not null)
            {
                _ = plan.Sitzung(quelleErst);
            }
            var msGesamt = uhr.Elapsed.TotalMilliseconds;
            Messpunkt("Faelligkeit bei 100 000 synthetischen Antworten (Buch, Plan, erste Sitzung)", msGesamt);
            Messwerte.Notiere(string.Create(inv, $"  davon Antworten einlesen {msBuch:F0} ms, Plan {msPlan:F0} ms, erste Sitzung {msGesamt - msBuch - msPlan:F0} ms"));
        }
        return funde;
    }

    /// <summary>
    /// Welle 5, L12 (Weisung P2-14 A4): Farbton-Regler "Akzent 1" einmal ueber die ganze Spur
    /// ziehen (360 Schritte). Je Schritt Neuzeichnen der Gruppe (3 Spuren mit 360/101/101
    /// Stuetzfarben, Knoepfe, Vorschau) bis zum fertigen Bild. Fund, wenn ein Schritt ueber
    /// 100 ms braucht. Der Regler steht danach wieder auf dem alten Wert; gespeichert wird nichts.
    /// </summary>
    private static async Task<int> FarbreglerAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        await ansicht.ZeigeAsync(Seiten.Optionen);
        var optionen = (OptionenSeite)ansicht.GebauteSeiten[Seiten.Optionen];
        optionen.OeffneBereich(Optionen.Optik, false);
        await ansicht.BildFertigAsync();
        var regler = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(optionen).OfType<Slider>().FirstOrDefault();
        if (regler is null)
        {
            Messwerte.Notiere("Farbregler Neuzeichnen: FEHLER, Regler nicht gefunden");
            return 1;
        }
        var alt = regler.Value;
        var zeiten = new List<double>();
        for (var wert = 0; wert <= 359; wert++)
        {
            var uhr = Stopwatch.StartNew();
            regler.Value = wert;
            await ansicht.BildFertigAsync();
            zeiten.Add(uhr.Elapsed.TotalMilliseconds);
        }
        regler.Value = alt;
        await ansicht.BildFertigAsync();
        var hoechst = zeiten.Max();
        Messwerte.Notiere(string.Create(inv, $"Farbregler Neuzeichnen (Farbton Akzent 1, {zeiten.Count} Schritte bis zum Bild): Mittel {zeiten.Average():F1} ms, Median {zeiten.Order().ElementAt(zeiten.Count / 2):F1} ms, Hoechstwert {hoechst:F1} ms (Grenze 100 ms), erster Schritt {zeiten[0]:F1} ms"));
        if (hoechst > 100)
        {
            Messwerte.Notiere("  FEHLER: ein Neuzeichnen ueber 100 ms");
            return 1;
        }
        return 0;
    }

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
                    foreach (var seite in (string[])[Seiten.Fortschritt, Seiten.Suche])
                    {
                        await ansicht.ZeigeAsync(seite);
                        if (ansicht.GebauteSeiten[seite] is FortschrittSeite f)
                        {
                            await f.Geladen;
                            await ansicht.BildFertigAsync();
                        }
                        foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                        {
                            layoutFunde.Add($"{welt} {seite}: {fund}");
                        }
                    }
                    foreach (var (text, flaeche) in W2Paare.Concat(W3Paare))
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
        Messwerte.Notiere(string.Create(inv, $"Farbwelten mit den Ansichten der Wellen 2 und 3 geprueft: {anzahl}"));
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
