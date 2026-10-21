using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Core.Diagnose;
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
        funde += Festbreitenschrift();
        funde += await RechnerMesspunkteAsync(ansicht);

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
                // Phase 3, W2: Ansichten der Rechner-Seite
                await foreach (var ansichtName in RechnerAnsichten(ansicht))
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
        Messwerte.Notiere("  und 6 Ansichten der Rechner-Seite (Phase 3), jeweils mit offenem Rechenweg: IPv6 Netzdaten, 2001:db8::/32 in 4.294.967.296 Subnetze (letzte Seite), Hostbedarf mit 12 Zeilen, Fehleingabe RAID, USV zu klein, Reiter Trainer");
        Messwerte.Notiere("  und 9 weitere Ansichten des Rechen-Trainers (Phase 3, W3, ohne Speichern): je IPv6, Nach Hostbedarf, Netz aufteilen, IPv4 Schwer offen und geprueft, Auswertung");
        Messwerte.Notiere("  und 13 Ansichten der Welle W4a (Phase 3): Datenmengen 4,7 GB, 10^12 TiB, 0,001 Bit, Fehleingabe; Uebertragung Vorgabe, 10^12 TiB mit 0,001 kbit/s bei 0,1 %, 0,001 Byte mit 1.000.000 Gbit/s; Bild und Video; Trainer Datenmengen und Uebertragungsdauer Schwer offen und geprueft, Eingabefehler");
        Messwerte.Notiere("  und 8 Ansichten der Welle W4b (Phase 3): Bild und Video mit Video-Ergebnis, Rechenweg und aktivem Uebernahme-Knopf; groesste Werte (100.000 x 100.000, 32 Bit, 1.000 fps, 10.000 Minuten); Fehler Dauer ohne Bildrate; Uebernahme-Ergebnis in Uebertragung (MiB und Byte); Trainer Bild und Video Schwer offen und geprueft, Eingabefehler");
        Messwerte.Notiere("  und 26 Ansichten der Welle W4c (Phase 3): WiSo-Rechner je Rechner mit Vorgaben und offenem Rechenweg (6), groesste Werte (Verkauf 999.999.999,99 mit 99,99 %, Deckung 10.000.000 Stueck, Abschreibung 50 Jahre ab Dezember, Zinsen 600 Monate, Amortisation 20.000/6.000/500), Fehler zu viele Nachkommastellen, Zinsen mit Pille Monate; Trainer WiSo je Art Schwer offen und geprueft (12), Eingabefehler");
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

    // Phase 3, W2: Titel der RAID-Karte (purple); die uebrigen Farben der Rechner-Seite
    // (accent, green, orange, red, text_dim auf card_alt, muted auf card_alt) stehen schon oben
    private static readonly (string Text, string Flaeche)[] RechnerPaare =
    [
        ("purple", "card"),
        ("cyan", "card"),      // W4a: Titel der Karte "Übertragungsdauer"
    ];

    private static readonly string[] Hostbedarf12 =
    [
        "Verwaltung 500", "Vertrieb 250", "Lager 120", "Produktion 400", "Entwicklung 200", "Einkauf 60",
        "Buchhaltung 30", "Empfang 10", "Server 14", "Drucker 6", "WLAN-Gaeste 100", "Technik 2",
    ];

    /// <summary>
    /// Phase 3, W2 (Weisung P3-4 A2): die Festbreitenschrift der Ergebniskaesten ist JetBrains
    /// Mono aus dem Programm (nicht eine Ersatzschrift des Systems), und i, W und 0 sind gleich breit.
    /// </summary>
    private static int Festbreitenschrift()
    {
        var inv = CultureInfo.InvariantCulture;
        var familie = Avalonia.Media.FontManager.Current.TryGetGlyphTypeface(new Avalonia.Media.Typeface(Festbreite.Schrift), out var schrift)
            ? schrift.FamilyName : "(nicht gefunden)";
        var breiten = new[] { "i", "W", "0" }.Select(z => Festbreite.Breite(z, 14)).ToList();
        var gleich = breiten.Max() - breiten.Min() < 0.01;
        var richtig = familie == "JetBrains Mono" && gleich;
        Messwerte.Notiere(string.Create(inv, $"Festbreitenschrift: {familie}, Breite i/W/0 bei 14 pt: {breiten[0]:F2}/{breiten[1]:F2}/{breiten[2]:F2}{(richtig ? "" : " FEHLER")}"));
        return richtig ? 0 : 1;
    }

    /// <summary>
    /// Phase 3, W2 (Plan Abschnitt 7, Weisung P3-4 A5): Wechsel IPv4 -> IPv6, Aufteilen von
    /// 2001:db8::/32 in 2^32 und von 2001:db8::/64 in 2^64 Subnetze (erste und letzte Seite),
    /// Hostbedarf mit 12 Zeilen und Rechenweg, je bis zum fertigen Bild. Fund ab 1 000 ms oder
    /// wenn die Rechnung einen Fehler zeigt. Das erste Oeffnen der Seite steht bei den Seiten (calc).
    /// </summary>
    private static async Task<int> RechnerMesspunkteAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        await ansicht.ZeigeAsync(Seiten.Rechner);
        if (ansicht.GebauteSeiten[Seiten.Rechner] is not RechnerSeite rechner)
        {
            Messwerte.Notiere("Rechner: FEHLER, Seite nicht gebaut");
            return 1;
        }
        var s = rechner.Subnet;
        async Task Messpunkt(string titel, Action schritt, bool ohneFehler = true)
        {
            var uhr = Stopwatch.StartNew();
            schritt();
            await ansicht.BildFertigAsync();
            var ms = uhr.Elapsed.TotalMilliseconds;
            var fehler = s.Aktuell.Fehler.IsVisible ? s.Aktuell.Fehler.Text : null;
            Messwerte.Notiere(string.Create(inv, $"{titel}: {ms:F0} ms{(ms > Geraetegrenze ? " (ueber 400)" : "")}"));
            if (ms > Abbruch || (ohneFehler && fehler is not null))
            {
                Messwerte.Notiere("  FEHLER: " + (fehler ?? "ueber 1000 ms"));
                funde++;
            }
        }
        rechner.Waehle("subnetting");
        s.ModusSetzen("ipv4");
        await ansicht.BildFertigAsync();
        await Messpunkt("Rechner Wechsel IPv4 -> IPv6", () => s.ModusSetzen("ipv6"));
        s.AnsichtSetzen("ipv6", "aufteilen");
        await ansicht.BildFertigAsync();
        foreach (var (netz, anzahl) in new[] { ("2001:db8::/32", "4294967296"), ("2001:db8::/64", "18446744073709551616") })
        {
            s.Aktuell.Netz.Text = netz;
            s.Aktuell.Zweites!.Text = anzahl;
            await Messpunkt($"Rechner {netz} in {anzahl} Subnetze, erste Seite", s.Aktuell.Rechnen);
            await Messpunkt($"Rechner {netz} in {anzahl} Subnetze, letzte Seite", () => s.Aktuell.Blaettern("last"));
            Messwerte.Notiere("  " + s.Aktuell.SeiteText);
        }
        s.ModusSetzen("ipv4");
        s.AnsichtSetzen("ipv4", "bedarf");
        await ansicht.BildFertigAsync();
        s.Aktuell.Netz.Text = "10.0.0.0/16";
        s.Aktuell.Zweites!.Text = string.Join("\n", Hostbedarf12);
        await Messpunkt("Rechner Hostbedarf mit 12 Zeilen", s.Aktuell.Rechnen);
        await Messpunkt("Rechner Rechenweg (Hostbedarf) oeffnen", () => s.RechenwegZeigen(true));
        s.RechenwegZeigen(false);
        await ansicht.BildFertigAsync();
        // W4a (Weisung P3-17): Auswahl-Pillen, Datenmengen und Uebertragung mit Rechenweg
        var d = rechner.Daten;
        async Task DatenMesspunkt(string titel, Action schritt, Func<string> fehler)
        {
            var uhr = Stopwatch.StartNew();
            schritt();
            await ansicht.BildFertigAsync();
            var ms = uhr.Elapsed.TotalMilliseconds;
            Messwerte.Notiere(string.Create(inv, $"{titel}: {ms:F0} ms{(ms > Geraetegrenze ? " (ueber 400)" : "")}"));
            if (ms > Abbruch || fehler().Length > 0)
            {
                Messwerte.Notiere("  FEHLER: " + (fehler().Length > 0 ? fehler() : "ueber 1000 ms"));
                funde++;
            }
        }
        await DatenMesspunkt("Rechner Auswahl Datenmengen", () => rechner.Waehle("datenmengen"), () => "");
        d.DatenEingabe("1.000.000.000.000", "TiB");
        await DatenMesspunkt("Datenmengen 10^12 TiB berechnen", d.DatenRechnen, () => d.DatenFehler);
        await DatenMesspunkt("Rechenweg (Datenmengen) oeffnen", () => d.DatenWeg.Zeigen(true), () => d.DatenFehler);
        d.DatenWeg.Zeigen(false);
        await DatenMesspunkt("Rechner Auswahl Uebertragung", () => rechner.Waehle("uebertragung"), () => "");
        d.DauerEingabe("1.000.000.000.000", "TiB", "0,001", "kbit/s", "0,1");
        await DatenMesspunkt("Uebertragung 10^12 TiB mit 0,001 kbit/s bei 0,1 % berechnen", d.DauerRechnen, () => d.DauerFehler);
        await DatenMesspunkt("Rechenweg (Uebertragung) oeffnen", () => d.DauerWeg.Zeigen(true), () => d.DauerFehler);
        d.DauerWeg.Zeigen(false);
        d.DatenEingabe(RechnerTexte.DataDefault, RechnerTexte.DataDefaultUnit);
        d.DauerEingabe(RechnerTexte.TransferDefaultSize, RechnerTexte.DataDefaultUnit, RechnerTexte.TransferDefaultRate, "Mbit/s", RechnerTexte.TransferDefaultPercent);
        funde += await VideoMesspunkteAsync(ansicht, rechner, DatenMesspunkt);
        funde += await WisoMesspunkteAsync(ansicht, rechner, DatenMesspunkt);
        rechner.Waehle("subnetting");
        await ansicht.BildFertigAsync();
        // W3 (R7): Reiter Trainer (erstes Zeigen liest die Statistik), Runde und Pruefen ohne Speichern
        await Messpunkt("Rechner Reiter Trainer (erstes Zeigen)", () => rechner.Tab("trainer"), ohneFehler: false);
        await Messpunkt("Trainer Runde starten (ohne Speichern)", () => rechner.Trainer.Probe("bedarf", 3, pruefen: false), ohneFehler: false);
        await Messpunkt("Trainer Pruefen (ohne Speichern)", rechner.Trainer.Druecken, ohneFehler: false);
        await Messpunkt("Trainer Naechste Aufgabe (ohne Speichern)", rechner.Trainer.Druecken, ohneFehler: false);
        await Messpunkt("Trainer Runde Uebertragungsdauer Schwer (ohne Speichern)", () => rechner.Trainer.Probe("dauer", 3, pruefen: false), ohneFehler: false);
        await Messpunkt("Trainer Pruefen Uebertragungsdauer (ohne Speichern)", () => rechner.Trainer.Probe("dauer", 3, pruefen: true), ohneFehler: false);
        await Messpunkt("Trainer Runde Bild und Video Schwer (ohne Speichern)", () => rechner.Trainer.Probe("video", 3, pruefen: false), ohneFehler: false);
        await Messpunkt("Trainer Pruefen Bild und Video (ohne Speichern)", () => rechner.Trainer.Probe("video", 3, pruefen: true), ohneFehler: false);
        // W4c (Weisung P3-23): Trainer WiSo
        await Messpunkt("Trainer Runde Verkaufskalkulation Schwer (ohne Speichern)", () => rechner.Trainer.Probe("verkauf", 3, pruefen: false), ohneFehler: false);
        await Messpunkt("Trainer Pruefen Verkaufskalkulation richtig (ohne Speichern)", () => rechner.Trainer.Probe("verkauf", 3, pruefen: true, richtig: true), ohneFehler: false);
        await Messpunkt("Trainer Runde Amortisation Schwer (ohne Speichern)", () => rechner.Trainer.Probe("amort", 3, pruefen: false), ohneFehler: false);
        await Messpunkt("Trainer Pruefen Amortisation falsch (ohne Speichern)", () => rechner.Trainer.Probe("amort", 3, pruefen: true), ohneFehler: false);
        rechner.Trainer.ProbeEnde();
        rechner.Tab("rechner");
        await ansicht.BildFertigAsync();
        return funde;
    }

    /// <summary>
    /// Phase 3 W4b (Weisung P3-21, Auflagen a bis c): Karte "Bild und Video" mit Video berechnen,
    /// Uebernahme (Wechsel auf "Übertragung" und Rechnen) messen und die Zustaende des
    /// Uebernahme-Knopfs, das Ende-zu-Ende-Beispiel (355,957 MiB bei 16 Mbit/s und 100 %), den
    /// Grenzfall in Byte und die Uebernahme bei leerer Datenrate in der echten Oberflaeche pruefen.
    /// Jede Abweichung ist ein Fund. Danach stehen beide Karten wieder auf ihren Vorgaben.
    /// </summary>
    private static async Task<int> VideoMesspunkteAsync(MainView ansicht, RechnerSeite rechner,
        Func<string, Action, Func<string>, Task> messpunkt)
    {
        var funde = 0;
        var v = rechner.Video;
        var d = rechner.Daten;
        void Pruefe(bool richtig, string was)
        {
            if (!richtig)
            {
                Messwerte.Notiere("  FEHLER (Bild und Video): " + was);
                funde++;
            }
        }
        await messpunkt("Rechner Auswahl Bild und Video", () => rechner.Waehle("bild"), () => "");
        Pruefe(!v.KnopfAktiv, "Uebernahme-Knopf vor der ersten Berechnung aktiv");
        v.Rechnen();
        Pruefe(!v.KnopfAktiv, "Uebernahme-Knopf ohne Dauer aktiv");
        v.Eingabe("1920", "1080", "25", "24", "2", "50");
        await messpunkt("Bild und Video mit Video berechnen", v.Rechnen, () => v.Fehler);
        Pruefe(v.KnopfAktiv, "Uebernahme-Knopf mit Video-Ergebnis gesperrt");
        await messpunkt("Rechenweg (Bild und Video) oeffnen", () => v.Weg.Zeigen(true), () => v.Fehler);
        v.Weg.Zeigen(false);
        v.Eingabe("1920", "1080", "25", "24", "3", "50");
        Pruefe(!v.KnopfAktiv, "Uebernahme-Knopf nach Feldaenderung aktiv");
        // Weg ueber TextChanged der Oberflaeche (ohne eigenen Vergleich): nur die Dauer aendern
        v.Eingabe("1920", "1080", "25", "24", "2", "50");
        v.Rechnen();
        v.NurDauerSetzen("4");
        await ansicht.BildFertigAsync();
        Pruefe(!v.KnopfAktiv, "Uebernahme-Knopf nach Aenderung der Dauer (TextChanged) aktiv");
        v.Eingabe("1920", "1080", "", "24", "2", "50");
        v.Rechnen();
        Pruefe(!v.KnopfAktiv && v.Fehler == RechnerTexte.VideoNeedsFps, "Fehler Dauer ohne Bildrate: Knopf aktiv oder falscher Hinweis");
        v.Eingabe("1920", "1080", "25", "24", "2", "50");
        v.Rechnen();
        await messpunkt("Uebernahme in Uebertragung (Wechsel und Rechnen)", v.Uebernehmen, () => d.DauerFehler);
        var f = d.DauerFelder;
        Pruefe(rechner.Gewaehlt == "uebertragung", "Auswahl nicht auf Uebertragung");
        Pruefe(f.Groesse == "355,957" && f.Einheit == "MiB", $"Dateigroesse {f.Groesse} {f.Einheit} statt 355,957 MiB");
        Pruefe(f.Rate == RechnerTexte.TransferDefaultRate && f.RateEinheit == "Mbit/s" && f.Prozent == RechnerTexte.TransferDefaultPercent, "Datenrate oder Leistung geaendert");
        // Dauer und Aufteilung stehen in zwei Zeilen der Uebertragung (W4a)
        var dauerZeilen = d.DauerKasten.Roh.Split('\n');
        Pruefe(dauerZeilen.Any(z => z.EndsWith(": 186,62 Sekunden", StringComparison.Ordinal))
            && dauerZeilen.Any(z => z.EndsWith(": 0 Std. 3 Min. 6,62 Sek.", StringComparison.Ordinal)), "Ergebnis ohne 186,62 Sekunden = 0 Std. 3 Min. 6,62 Sek.");
        Pruefe(d.UebernahmeHinweis == RechnerTexte.VideoTakenMiB, "Hinweiszeile MiB fehlt");
        d.DauerRechnen();
        Pruefe(d.UebernahmeHinweis.Length == 0, "Hinweiszeile nach normalem Berechnen noch sichtbar");
        // Grenzfall Byte (winziger Wert)
        rechner.Waehle("bild");
        v.Eingabe("1", "1", "1", "8", "0,001", "1000");
        v.Rechnen();
        v.Uebernehmen();
        f = d.DauerFelder;
        Pruefe(f.Groesse == "1" && f.Einheit == "Byte" && d.UebernahmeHinweis == RechnerTexte.VideoTakenByte, $"Grenzfall Byte: {f.Groesse} {f.Einheit}, Hinweis \"{d.UebernahmeHinweis}\"");
        // Auflage b: leere Datenrate -> nur der rote Hinweis der Uebertragung
        rechner.Waehle("bild");
        v.Eingabe("1920", "1080", "25", "24", "2", "50");
        v.Rechnen();
        d.DauerEingabe(RechnerTexte.TransferDefaultSize, RechnerTexte.DataDefaultUnit, "", "Mbit/s", RechnerTexte.TransferDefaultPercent);
        v.Uebernehmen();
        Pruefe(d.DauerFehler.Length > 0 && d.UebernahmeHinweis.Length == 0, "leere Datenrate: roter Hinweis fehlt oder Hinweiszeile sichtbar");
        d.DauerEingabe(RechnerTexte.TransferDefaultSize, RechnerTexte.DataDefaultUnit, RechnerTexte.TransferDefaultRate, "Mbit/s", RechnerTexte.TransferDefaultPercent);
        d.DauerRechnen();
        v.Eingabe("1920", "1080", "30", "24", "", RechnerTexte.VideoDefaultCompression);
        v.Rechnen();
        await ansicht.BildFertigAsync();
        return funde;
    }

    /// <summary>W4c: groesste Werte je Rechner (Laufzeit-Einheit nur bei Zinsen).</summary>
    private static readonly (string Rechner, string[] Werte, string? Einheit)[] WisoGross =
    [
        ("verkauf", ["999.999.999,99", "99,99", "99,999", "99,999", "99,999", "100"], null),
        ("deckung", ["1.000.000.000", "0,01", "1.000.000.000", "10.000.000"], null),
        ("afa", ["1.000.000.000", "50", "12"], null),
        ("zinsen", ["1.000.000.000", "100", "600"], "monate"),
        ("amort", ["20.000", "6.000", "500"], null),
    ];

    /// <summary>W4c: alle WiSo-Rechner zurueck auf ihre Vorgaben und Tage, Bezug gewaehlt, Ergebnisse neu.</summary>
    private static void WisoVorgaben(WisoKarte w)
    {
        foreach (var (rechner, _) in RechnerTexte.WisoAuswahl)
        {
            w.Eingabe(rechner, [.. RechnerTexte.WisoFelder[rechner].Select(f => f.Vorgabe)], rechner == "zinsen" ? "tage" : null);
            w.Rechnen(rechner);
        }
        w.Waehle("bezug");
    }

    /// <summary>
    /// Phase 3 W4c (Weisung P3-23 Punkt 3c): Karte "WiSo-Rechner" waehlen, je Rechner mit den Vorgaben
    /// rechnen und den Rechenweg oeffnen (Messpunkte wie W4b) und in der echten Oberflaeche die
    /// Ergebniszeile der Wortliste, den Fehler "zu viele Nachkommastellen" und die Pille Monate
    /// pruefen. Jede Abweichung ist ein Fund. Danach stehen alle Rechner wieder auf ihren Vorgaben.
    /// </summary>
    private static async Task<int> WisoMesspunkteAsync(MainView ansicht, RechnerSeite rechner,
        Func<string, Action, Func<string>, Task> messpunkt)
    {
        var funde = 0;
        var w = rechner.Wiso;
        void Pruefe(bool richtig, string was)
        {
            if (!richtig)
            {
                Messwerte.Notiere("  FEHLER (WiSo-Rechner): " + was);
                funde++;
            }
        }
        await messpunkt("Rechner Auswahl WiSo", () => rechner.Waehle("wiso"), () => "");
        Pruefe(rechner.Gewaehlt == "wiso" && w.Gewaehlt == "bezug", "Auswahl nicht WiSo / Bezug");
        var letzte = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bezug"] = "= Bezugspreis", ["verkauf"] = "= Bruttoverkaufspreis", ["deckung"] = "Gewinnschwelle",
            ["afa"] = "Jahr 4 (3 Monate)", ["zinsen"] = "Kapital mit Zinsen", ["amort"] = "In Jahren und Monaten",
        };
        var soll = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bezug"] = "1.024,60 €", ["verkauf"] = "1.976,83 €", ["deckung"] = "400 Stück",
            ["afa"] = "150,00 €, Restbuchwert 0,00 €", ["zinsen"] = "5.056,25 €", ["amort"] = "3 Jahre",
        };
        foreach (var (r, name) in RechnerTexte.WisoAuswahl)
        {
            await messpunkt("WiSo Auswahl " + name, () => w.Waehle(r), () => "");
            await messpunkt("WiSo " + name + " berechnen", () => w.Rechnen(r), () => w.Fehler(r));
            Pruefe(w.Kasten(r).Roh.Split('\n').Any(z => z.StartsWith(letzte[r], StringComparison.Ordinal) && z.EndsWith(": " + soll[r], StringComparison.Ordinal)),
                name + ": Ergebnis ohne \"" + letzte[r] + " : " + soll[r] + "\"");
            await messpunkt("Rechenweg (WiSo " + name + ") oeffnen", () => w.Weg(r).Zeigen(true), () => w.Fehler(r));
            w.Weg(r).Zeigen(false);
        }
        w.Waehle("bezug");
        w.Eingabe("bezug", ["1.200,001", "15", "2", "25"]);
        w.Rechnen("bezug");
        Pruefe(w.Fehler("bezug") == "Bitte höchstens 2 Nachkommastellen (volle Cent) eingeben: Listeneinkaufspreis (€)." && w.Kasten("bezug").Roh.Length == 0,
            "Fehler zu viele Nachkommastellen fehlt oder Ergebnis nicht leer");
        w.Waehle("zinsen");
        w.Eingabe("zinsen", ["5.000", "4,5", "3"], "monate");
        await messpunkt("WiSo Zinsen 3 Monate berechnen", () => w.Rechnen("zinsen"), () => w.Fehler("zinsen"));
        Pruefe(w.Kasten("zinsen").Roh.Split('\n').Any(z => z.EndsWith(": 3 Monate = 90 Tage", StringComparison.Ordinal)), "Pille Monate: Laufzeit nicht \"3 Monate = 90 Tage\"");
        WisoVorgaben(w);
        await ansicht.BildFertigAsync();
        return funde;
    }

    /// <summary>
    /// Ansichten der Rechner-Seite nacheinander zeigen (je nach dem Zeigen pruefen), alle
    /// Rechenwege offen. Danach sind die Rechenwege zu und Subnetting zeigt IPv4 Netzdaten;
    /// die Ergebnisse der letzten Rechnungen bleiben stehen (gespeichert wird nichts).
    /// </summary>
    private static async IAsyncEnumerable<string> RechnerAnsichten(MainView ansicht)
    {
        await ansicht.ZeigeAsync(Seiten.Rechner);
        if (ansicht.GebauteSeiten[Seiten.Rechner] is not RechnerSeite r)
        {
            yield break;
        }
        var s = r.Subnet;
        r.Waehle("subnetting");
        r.AllesAufklappen();
        s.ModusSetzen("ipv6");
        s.AnsichtSetzen("ipv6", "netzdaten");
        s.Aktuell.Rechnen();
        s.RechenwegZeigen(true);
        await ansicht.BildFertigAsync();
        yield return "calc/ipv6";
        s.AnsichtSetzen("ipv6", "aufteilen");
        s.Aktuell.Netz.Text = "2001:db8::/32";
        s.Aktuell.Zweites!.Text = "4294967296";
        s.Aktuell.Rechnen();
        s.Aktuell.Blaettern("last");
        s.RechenwegZeigen(true);
        await ansicht.BildFertigAsync();
        yield return "calc/ipv6-aufteilen";
        s.ModusSetzen("ipv4");
        s.AnsichtSetzen("ipv4", "bedarf");
        s.Aktuell.Netz.Text = "10.0.0.0/16";
        s.Aktuell.Zweites!.Text = string.Join("\n", Hostbedarf12);
        s.Aktuell.Rechnen();
        s.RechenwegZeigen(true);
        await ansicht.BildFertigAsync();
        yield return "calc/hostbedarf";
        // W4a: Datenmengen, Uebertragung, Bild und Video (Zwischenstand: Bildschirm-Karte)
        var d = r.Daten;
        r.Waehle("datenmengen");
        d.DatenEingabe(RechnerTexte.DataDefault, RechnerTexte.DataDefaultUnit);
        d.DatenRechnen();
        await ansicht.BildFertigAsync();
        yield return "calc/datenmengen";
        d.DatenEingabe("1.000.000.000.000", "TiB");
        d.DatenRechnen();
        await ansicht.BildFertigAsync();
        yield return "calc/datenmengen-gross";
        d.DatenEingabe("0,001", "Bit");
        d.DatenRechnen();
        await ansicht.BildFertigAsync();
        yield return "calc/datenmengen-klein";
        d.DatenEingabe("4,7 GB", "GB");
        d.DatenRechnen();
        await ansicht.BildFertigAsync();
        yield return "calc/datenmengen-fehler";
        r.Waehle("uebertragung");
        d.DauerEingabe(RechnerTexte.TransferDefaultSize, RechnerTexte.DataDefaultUnit, RechnerTexte.TransferDefaultRate, "Mbit/s", RechnerTexte.TransferDefaultPercent);
        d.DauerRechnen();
        await ansicht.BildFertigAsync();
        yield return "calc/uebertragung";
        d.DauerEingabe("1.000.000.000.000", "TiB", "0,001", "kbit/s", "0,1");
        d.DauerRechnen();
        await ansicht.BildFertigAsync();
        yield return "calc/uebertragung-lang";
        d.DauerEingabe("0,001", "Byte", "1.000.000", "Gbit/s", "100");
        d.DauerRechnen();
        await ansicht.BildFertigAsync();
        yield return "calc/uebertragung-kurz";
        // W4b: Bild und Video mit Video-Ergebnis, Rechenweg (offen) und aktivem Uebernahme-Knopf
        var v = r.Video;
        r.Waehle("bild");
        v.Eingabe("1920", "1080", "25", "24", "2", "50");
        v.Rechnen();
        await ansicht.BildFertigAsync();
        yield return "calc/bild";
        v.Eingabe("100000", "100000", "1000", "32", "10000", "1");
        v.Rechnen();
        await ansicht.BildFertigAsync();
        yield return "calc/bild-gross";
        v.Eingabe("1920", "1080", "", "24", "2", "50");
        v.Rechnen();
        await ansicht.BildFertigAsync();
        yield return "calc/bild-fehler";
        v.Eingabe("1920", "1080", "25", "24", "2", "50");
        v.Rechnen();
        d.DauerEingabe(RechnerTexte.TransferDefaultSize, RechnerTexte.DataDefaultUnit, RechnerTexte.TransferDefaultRate, "Mbit/s", RechnerTexte.TransferDefaultPercent);
        v.Uebernehmen();
        await ansicht.BildFertigAsync();
        yield return "calc/uebernahme";
        r.Waehle("bild");
        v.Eingabe("1", "1", "1", "8", "0,001", "1000");
        v.Rechnen();
        v.Uebernehmen();
        await ansicht.BildFertigAsync();
        yield return "calc/uebernahme-byte";
        // W4c: WiSo-Rechner je Rechner mit Vorgaben (Rechenweg offen), groesste Werte, Fehler, Pille Monate
        r.Waehle("wiso");
        var w = r.Wiso;
        foreach (var (rechner, _) in RechnerTexte.WisoAuswahl)
        {
            w.Waehle(rechner);
            w.Rechnen(rechner);
            await ansicht.BildFertigAsync();
            yield return "calc/wiso-" + rechner;
        }
        foreach (var (rechner, werte, einheit) in WisoGross)
        {
            w.Waehle(rechner);
            w.Eingabe(rechner, werte, einheit);
            w.Rechnen(rechner);
            await ansicht.BildFertigAsync();
            yield return "calc/wiso-" + rechner + "-gross";
        }
        w.Waehle("bezug");
        w.Eingabe("bezug", ["1.200,001", "15", "2", "25"]);
        w.Rechnen("bezug");
        await ansicht.BildFertigAsync();
        yield return "calc/wiso-fehler";
        w.Waehle("zinsen");
        w.Eingabe("zinsen", ["5.000", "4,5", "3"], "monate");
        w.Rechnen("zinsen");
        await ansicht.BildFertigAsync();
        yield return "calc/wiso-zinsen-monate";
        WisoVorgaben(w);
        r.Waehle("raid");
        r.RaidEingabe("abc", "1000");
        r.RaidRechnen();
        await ansicht.BildFertigAsync();
        yield return "calc/fehler";
        r.Waehle("usv");
        r.UsvFeld("minuten", "90");
        r.UsvRechnen("empfehlung");
        r.AllesAufklappen();
        await ansicht.BildFertigAsync();
        yield return "calc/usv-zu-klein";
        r.Tab("trainer");
        await ansicht.BildFertigAsync();
        yield return "calc/trainer";
        // W3 (R7): Trainer ohne Speichern (Probe): laengste Rechenwege und Rueckmeldungen
        foreach (var (art, stufe) in new[]
        {
            ("ipv6", 3), ("bedarf", 3), ("teilen", 3), ("ipv4", 3), ("einheiten", 3), ("dauer", 3), ("video", 3),
            ("bezug", 3), ("verkauf", 3), ("deckung", 3), ("afa", 3), ("zinsen", 3), ("amort", 3),
        })
        {
            r.Trainer.Probe(art, stufe, pruefen: false);
            await ansicht.BildFertigAsync();
            yield return $"calc/trainer-{art}";
            r.Trainer.Probe(art, stufe, pruefen: true);
            await ansicht.BildFertigAsync();
            yield return $"calc/trainer-{art}-geprueft";
        }
        r.Trainer.Probe("dauer", 3, pruefen: true, eingabefehler: true);
        await ansicht.BildFertigAsync();
        yield return "calc/trainer-dauer-eingabefehler";
        r.Trainer.Probe("video", 3, pruefen: true, eingabefehler: true);
        await ansicht.BildFertigAsync();
        yield return "calc/trainer-video-eingabefehler";
        r.Trainer.Probe("amort", 3, pruefen: true, eingabefehler: true);
        await ansicht.BildFertigAsync();
        yield return "calc/trainer-amort-eingabefehler";
        r.Trainer.Probe("vlsm", 3, pruefen: false, beenden: true);
        await ansicht.BildFertigAsync();
        yield return "calc/trainer-auswertung";
        r.Trainer.ProbeEnde();
        r.Tab("rechner");
        d.DatenEingabe(RechnerTexte.DataDefault, RechnerTexte.DataDefaultUnit);
        d.DatenRechnen();
        d.DauerEingabe(RechnerTexte.TransferDefaultSize, RechnerTexte.DataDefaultUnit, RechnerTexte.TransferDefaultRate, "Mbit/s", RechnerTexte.TransferDefaultPercent);
        d.DauerRechnen();
        r.RaidEingabe("4", "1000");
        r.RaidRechnen();
        v.Eingabe("1920", "1080", "30", "24", "", RechnerTexte.VideoDefaultCompression);
        v.Rechnen();
        WisoVorgaben(r.Wiso);
        r.UsvFeld("minuten", "15");
        r.UsvRechnen("empfehlung", leise: true);
        foreach (var weg in r.Rechenwege)
        {
            weg.Zeigen(false);
        }
        s.RechenwegZeigen(false);
        s.AnsichtSetzen("ipv4", "netzdaten");
        r.Waehle("subnetting");
        await ansicht.BildFertigAsync();
    }

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
    /// Stuetzfarben, Knoepfe, Vorschau) bis zum fertigen Bild. Fund nach Farbreglerregel
    /// (Weisung P2-17): Schritt 1 ueber 150 ms oder ein spaeterer Schritt ueber 100 ms.
    /// Der Regler steht danach wieder auf dem alten Wert; gespeichert wird nichts.
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
        var weitere = zeiten.Skip(1).ToList();
        Messwerte.Notiere(string.Create(inv, $"Farbregler Neuzeichnen (Farbton Akzent 1, {zeiten.Count} Schritte bis zum Bild): erster Schritt {zeiten[0]:F1} ms (Grenze {Farbreglerregel.GrenzeErsterSchritt:F0} ms); ab Schritt 2: Mittel {weitere.Average():F1} ms, Median {weitere.Order().ElementAt(weitere.Count / 2):F1} ms, Hoechstwert {weitere.Max():F1} ms (Grenze {Farbreglerregel.GrenzeWeitereSchritte:F0} ms)"));
        var funde = Farbreglerregel.Funde(zeiten);
        foreach (var fund in funde)
        {
            Messwerte.Notiere("  FEHLER: " + fund);
        }
        if (funde.Count > 0)
        {
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
                    await foreach (var name in RechnerAnsichten(ansicht))
                    {
                        foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                        {
                            layoutFunde.Add($"{welt} {name}: {fund}");
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
                    foreach (var (text, flaeche) in W2Paare.Concat(W3Paare).Concat(RechnerPaare))
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
        Messwerte.Notiere(string.Create(inv, $"Farbwelten mit den Ansichten der Wellen 2 und 3 und der Rechner-Seite geprueft: {anzahl}"));
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
