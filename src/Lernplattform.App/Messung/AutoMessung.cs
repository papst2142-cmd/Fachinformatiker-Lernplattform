using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Projekt;
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
public static partial class AutoMessung
{
    public const double Abbruch = 1000;
    public const double Geraetegrenze = 400;
    public const double SchmalBreite = 384;
    public const double SchmalHoehe = 832;
    public const double PcBreite = 1360;
    public const double PcHoehe = 880;

    /// <summary>
    /// Weisung P4-12 B6: einen Schritt bis zum fertigen Bild messen. Liegt die Messung ueber
    /// <paramref name="grenze"/> und ist der Messpunkt wiederholbar, wird dieselbe Handlung bis zu
    /// 3 Mal gemessen (vorher <paramref name="zurueck"/> = Zustand davor, nicht gemessen, und eine
    /// kurze Pause; kein Vorwaermen). Ohne <paramref name="zurueck"/> und ohne
    /// <paramref name="wiederholbar"/> bleibt der Messpunkt streng (eine Messung).
    /// </summary>
    private static async Task<Messreihe> BildMessenAsync(MainView ansicht, Action schritt, Action? zurueck = null,
        bool wiederholbar = false, double grenze = Abbruch)
    {
        async Task<double?> Einmal()
        {
            var uhr = Stopwatch.StartNew();
            schritt();
            await ansicht.BildFertigAsync();
            return uhr.Elapsed.TotalMilliseconds;
        }
        var erste = await Einmal();
        if (zurueck is null && !wiederholbar)
        {
            return Messwiederholung.Einmal(erste, grenze);
        }
        return await Messwiederholung.MessenAsync(erste, grenze, Einmal,
            zurueck is null ? null : async () => { zurueck(); await ansicht.BildFertigAsync(); }, Task.Delay);
    }

    /// <summary>Weisung P4-12 B6: eine reine Rechnung (ohne Bild) messen, ueber der Grenze bis zu 3 Mal.</summary>
    private static async Task<Messreihe> RechnungMessenAsync(Func<double> rechnung, double grenze = Abbruch) =>
        await Messwiederholung.MessenAsync(rechnung(), grenze, () => Task.FromResult<double?>(rechnung()), null, Task.Delay);

    /// <summary>
    /// Sammelliste 64: Messpunkte aus <see cref="Ergebnis"/> ueber 1 000 ms, nur fuer die Zeile "Zeitgrenze CI".
    /// Sammelliste 70: wird zu Beginn jedes Laufs geleert, jeder Punkt steht in der Zeile nur einmal.
    /// </summary>
    private static readonly List<string> UeberAbbruch = [];

    /// <summary>
    /// Ergebniszeile eines Messpunkts (Messwert = kleinste Einzelmessung; nach einer Wiederholung
    /// alle Einzelmessungen) und bei Rot die Fehlerzeile mit Messpunkt, Wert, Grenze und allen
    /// Einzelmessungen (Weisung P4-12 B5). Rueckgabe 1 = Fund.
    /// </summary>
    private static int Ergebnis(string titel, Messreihe reihe)
    {
        var inv = CultureInfo.InvariantCulture;
        var ms = reihe.Bester;
        if (ms > Abbruch)
        {
            UeberAbbruch.Add(titel);
        }
        Messwerte.Notiere(string.Create(inv, $"{titel}: {ms ?? -1:F0} ms{(ms > Geraetegrenze ? " (ueber 400)" : "")}")
            + (reihe.Wiederholt ? " (wiederholt, Einzelmessungen " + reihe.Einzelwerte() + ")" : ""));
        if (!reihe.Rot)
        {
            return 0;
        }
        Messwerte.Notiere("  FEHLER: " + reihe.Fehlertext(titel));
        return 1;
    }

    /// <summary>
    /// Weisung P4-12 B6: ein Wert einer Messreihe (Wechsel Hell/Dunkel, Schriftstufe); ueber
    /// 1 000 ms wird nach <paramref name="zurueck"/> wiederholt, die Reihe bekommt den kleinsten Wert.
    /// Rot bleibt rot ueber "Zeitgrenze CI" am Ende; die Einzelmessungen stehen dann in <paramref name="rot"/>.
    /// </summary>
    private static async Task ReihenwertAsync(MainView ansicht, string name, Action schritt, Action zurueck, List<string> rot)
    {
        var reihe = await BildMessenAsync(ansicht, schritt, zurueck);
        Messwerte.Reihe(name, reihe.Bester ?? double.PositiveInfinity);
        if (reihe.Wiederholt)
        {
            Messwerte.Notiere("Wiederholt " + name + ": Einzelmessungen " + reihe.Einzelwerte() + (reihe.Rot ? " (rot)" : " (gruen)"));
        }
        if (reihe.Rot)
        {
            rot.Add(reihe.Fehlertext(name));
        }
    }

    public static async Task<int> LaufAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        UeberAbbruch.Clear();
        // Phase 7 W1: eigener kurzer Lauf ohne OpenGL (CI-Schritt mit --software)
        if (Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_TEIL") == TeilErsatz)
        {
            return await ErsatzLaufAsync(ansicht);
        }
        // Sammelliste 106 (Weisung P8-2): die CI startet Lernteil und Spielansichten als eigene Schritte
        Messwerte.Notiere("Start-Test Teil: " + (MitLernen && MitSpiel ? "alles" : MitLernen ? TeilLernen : TeilSpiel));
        var inhalte = await Zustand.Inhalte;
        // Weisung P4-16: auch die W4-Messpunkte melden ihre roten Werte in reihenRot
        var reihenRot = new List<string>();
        if (MitLernen)
        {
            funde += await LernteilMessenAsync(ansicht, inhalte, reihenRot);
        }
        if (MitSpiel)
        {
            funde += await P7W1MesspunkteAsync(ansicht);       // Phase 7 W1: 3D-Stichprobe (GL-Pflicht)
            funde += await P8W3MesspunkteAsync(ansicht);       // Phase 8 W3: Spielseite nach Abgleich auffrischen
            funde += await P8W5MesspunkteAsync(ansicht);       // Phase 8 W5: Rueckfrage am Bett
        }
        if (MitLernen)
        {
            await WechselReihenAsync(ansicht, reihenRot);
        }
        funde += await LayoutAsync(ansicht, inhalte);
        if (MitLernen)
        {
            funde += await OptionenZweimalAsync(ansicht);
        }
        funde += AbschlussAuswerten(reihenRot);
        Messwerte.Notiere(funde == 0 ? "URTEIL: gruen" : string.Create(inv, $"URTEIL: rot ({funde} Funde)"));
        return funde;
    }

    /// <summary>Sammelliste 106: Wert von LERNPLATTFORM_MESSUNG_TEIL fuer den Lernteil (alles ausser den Spielansichten).</summary>
    public const string TeilLernen = "lernen";

    /// <summary>Sammelliste 106: Wert von LERNPLATTFORM_MESSUNG_TEIL nur fuer die Spielansichten (3D-Stichprobe und Spiel-Layout).</summary>
    public const string TeilSpiel = "spiel";

    private static string? Teil => Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_TEIL");

    /// <summary>Ohne Angabe (lokal) misst der Start-Test alles in einem Lauf wie bisher.</summary>
    private static bool MitLernen => Teil != TeilSpiel;

    private static bool MitSpiel => Teil != TeilLernen;

    /// <summary>Erstes Oeffnen jeder Seite und die Messpunkte des Lernteils (Phasen 1 bis 6).</summary>
    private static async Task<int> LernteilMessenAsync(MainView ansicht, Lerninhalte? inhalte, List<string> reihenRot)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
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
        funde += await P8W2bMesspunkteAsync(ansicht);   // Phase 8 W2b: Seitenleiste und Optionen

        // Phase 2: Karteikarten und Pruefungstrainer lesen Lernstand und Reihenfolge im Hintergrund;
        // gemessen wird vom Bauen der Seite bis die erste Karte bzw. die Vorbereitung steht
        if (ansicht.GebauteSeiten[Seiten.Karteikarten] is KarteikartenSeite kartenSeite
            && ansicht.GebauteSeiten[Seiten.Trainer] is TrainerSeite trainerSeite)
        {
            await kartenSeite.Geladen;
            await trainerSeite.Geladen;
            // einmalig (Laden gleich nach dem Start), nicht wiederholbar: streng
            foreach (var (titel, ms) in new[] { ("Karteikarten bis erste Karte", kartenSeite.InhaltMs), ("Pruefungstrainer bis Vorbereitung", trainerSeite.InhaltMs) })
            {
                funde += Ergebnis(titel, Messwiederholung.Einmal(ms, Abbruch));
            }
        }

        funde += await W3MesspunkteAsync(ansicht, inhalte);
        funde += await FarbreglerAsync(ansicht);
        funde += Festbreitenschrift();
        funde += await RechnerMesspunkteAsync(ansicht);
        funde += await W1MesspunkteAsync(ansicht, inhalte);
        funde += await KlausurMesspunkteAsync(ansicht);
        funde += await ProjektMesspunkteAsync(ansicht);
        funde += await W4MesspunkteAsync(ansicht, reihenRot);
        funde += await KalenderMesspunkteAsync(ansicht);   // A 0.12.2: Kalender blaettern
        funde += await LizenzenMesspunkteAsync(ansicht);   // Phase 8 W2: Knopf "Lizenzen"
        funde += RingVerlaufPruefen();   // Phase 8 W2 Nachtrag (P8-3b): Farbverlauf der Fortschrittsringe
        return funde;
    }

    /// <summary>Wechsel Hell/Dunkel und Schriftstufe, rote Reihen in <paramref name="reihenRot"/>.</summary>
    private static async Task WechselReihenAsync(MainView ansicht, List<string> reihenRot)
    {
        // Wechsel Hell/Dunkel und Schriftstufe (je 3 Durchgaenge); ueber 1 000 ms wird derselbe
        // Wechsel wiederholt (vorher die andere Darstellung bzw. die Stufe davor, Weisung P4-12 B6)
        var werte = Zustand.Werte;
        void Farben(string darstellung) => FarbenAnwenden.Anwenden(Farbwelt.Berechne(darstellung, Einstellungen.Grundfarbe(werte), Einstellungen.Hintergrund(werte)));
        var stufen = Einstellungen.Schriftgroessen.Append(Einstellungen.SchriftNormal).ToList();
        for (var runde = 0; runde < 3; runde++)
        {
            foreach (var darstellung in (string[])[Farbwelt.Hell, Farbwelt.Dunkel])
            {
                var andere = darstellung == Farbwelt.Hell ? Farbwelt.Dunkel : Farbwelt.Hell;
                await ReihenwertAsync(ansicht, "Wechsel Hell/Dunkel", () => Farben(darstellung), () => Farben(andere), reihenRot);
            }
            for (var i = 0; i < stufen.Count; i++)
            {
                var stufe = stufen[i];
                var davor = stufen[(i + stufen.Count - 1) % stufen.Count];
                if (davor == stufe)
                {
                    davor = stufen.First(x => x != stufe);
                }
                await ReihenwertAsync(ansicht, "Wechsel Schriftstufe", () => Schrift.Anwenden(Zustand.SchriftFaktor(stufe)), () => Schrift.Anwenden(Zustand.SchriftFaktor(davor)), reihenRot);
            }
        }
    }

    /// <summary>
    /// Layoutpruefung je Schriftstufe und Fenstergroesse; der Lernteil prueft alle gebauten Seiten und seine
    /// Ansichten, der Spielteil die Spielansichten (Sammelliste 106). Liefert die Zahl der Funde.
    /// </summary>
    private static async Task<int> LayoutAsync(MainView ansicht, Lerninhalte? inhalte)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        // Layoutpruefung je Seite und Schriftstufe in festen Fenstergroessen: schmal (Handy),
        // an der Grenze zum breiten Aufbau (MainView.BreitGrenze, engster PC-Aufbau) und
        // Startgroesse der Python-Linie (1360 x 880). Ohne Fenster (Handy) nur wie gestartet.
        var fenster = TopLevel.GetTopLevel(ansicht) as Window;
        var layoutFunde = new List<string>();
        var gepruefteGroessen = new List<string>();
        var groessenJeStufe = 0;
        var w4Gekuerzt = new List<string>();
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
                // Sammelliste 106: der Lernteil prueft alle Seiten ausser dem Spiel, der Spielteil nur das Spiel
                // (nach der 3D-Stichprobe wie im ganzen Lauf); zusammen genau die Seiten des ganzen Laufs
                foreach (var id in ansicht.GebauteSeiten.Keys.Where(id => id == Seiten.Spiel ? MitSpiel : MitLernen).ToList())
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
                await foreach (var (ansichtName, _) in MitLernen ? W2Ansichten(ansicht, inhalte) : Keine<(string, int)>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                // Phase 3, W2: Ansichten der Rechner-Seite
                await foreach (var ansichtName in MitLernen ? RechnerAnsichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                // Phase 4, W1: Ansichten der Lernseiten
                await foreach (var ansichtName in MitLernen ? W1Ansichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                // Phase 4, W2: Ansichten der Klausursimulation und ihre Rueckfragen
                await foreach (var ansichtName in MitLernen ? KlausurAnsichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                // Phase 4, W3: Reiter des Abschlussprojekts, Rueckfrage, Fehler beim Export, Kurzhinweis
                await foreach (var ansichtName in MitLernen ? ProjektAnsichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                // Phase 4, W4: Hilfe, Rundgang, Ausbildungsfrage, Warnhinweis, Optionen (ab 7,5 Minuten nur schmal und pc)
                if (name == "grenze" && Messwerte.SeitStart.Elapsed > W4Zeitgrenze)
                {
                    w4Gekuerzt.Add(stufe);
                    continue;
                }
                await foreach (var ansichtName in MitLernen ? W4Ansichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                // Phase 5, W1: Sicherung, Loeschen, Datenbank und ihre Fenster (gleiche Zeitgrenze wie W4)
                await foreach (var ansichtName in MitLernen ? P5Ansichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                // Phase 5, W2: Diagnose und Werkzeuge mit Bericht, Fenster und Kurzhinweis
                await foreach (var ansichtName in MitLernen ? P5W2Ansichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                // Phase 5, W3: Updates mit Fehlerzeile, Fenster "Update verfügbar" und "Update nicht abgeschlossen"
                await foreach (var ansichtName in MitLernen ? P5W3Ansichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                // Phase 6, W2: Updates mit "Verknüpfung anlegen", Rueckfrage beim Start und ihr Ergebnis
                await foreach (var ansichtName in MitLernen ? P6W2Ansichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                // Phase 8, W2: "Über das Programm" mit der Liste "Lizenzen" (zu und ein Bestandteil offen)
                await foreach (var ansichtName in MitLernen ? P8W2Ansichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                // Phase 8, W2b: Seitenleiste mit offenen Unterpunkten, Optionen in Gruppen (alle Karten zu)
                await foreach (var ansichtName in MitLernen ? P8W2bAnsichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                // Phase 7, W1: Spiel-Seite (Stadt, Ortsliste, Haus mit Oeffnungszeit, Buero, ohne 3D)
                P7W1AnsichtFehler.Clear();
                await foreach (var ansichtName in MitSpiel ? P7W1Ansichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                layoutFunde.AddRange(P7W1AnsichtFehler.Select(f => $"{ist} {stufe} {f}"));
                // Phase 7, W6: Spielansichten ueber der Stadt (Platzwahl, Figur, Uebersicht, Reise, Tickets, Firma)
                P7W1AnsichtFehler.Clear();
                await foreach (var ansichtName in MitSpiel ? P7W6Ansichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                layoutFunde.AddRange(P7W1AnsichtFehler.Select(f => $"{ist} {stufe} {f}"));
                // Phase 7, W7: Moebelhaus und Elektromarkt (innen in 3D, Sortiment in der Tafel)
                P7W1AnsichtFehler.Clear();
                await foreach (var ansichtName in MitSpiel ? P7W7Ansichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                layoutFunde.AddRange(P7W1AnsichtFehler.Select(f => $"{ist} {stufe} {f}"));
                // Phase 7, W7b: Bank und Amt (innen in 3D, Seiten am Schalter in der Tafel)
                P7W1AnsichtFehler.Clear();
                await foreach (var ansichtName in MitSpiel ? P7W7bAnsichten(ansicht) : Keine<string>())
                {
                    foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                    {
                        layoutFunde.Add($"{ist} {stufe} {ansichtName}: {fund}");
                    }
                }
                layoutFunde.AddRange(P7W1AnsichtFehler.Select(f => $"{ist} {stufe} {f}"));
            }
        }

        // Phase 2 (Weisung P2-4): alle 72 Farbwelten mit den neuen Ansichten (Layout und Lesbarkeit)
        if (MitLernen)
        {
            funde += await FarbweltenAsync(ansicht, inhalte, layoutFunde);
        }
        Zustand.DesignAnwenden();

        Messwerte.Notiere(string.Create(inv, $"Layoutpruefung (Woerter, Knoepfe, Rand), {Einstellungen.Schriftgroessen.Count} Schriftstufen x {ansicht.GebauteSeiten.Keys.Count(id => id == Seiten.Spiel ? MitSpiel : MitLernen)} Seiten x {groessenJeStufe} Groessen: {layoutFunde.Count} Funde"));
        if (MitLernen)
        {
            Messwerte.Notiere("  darin Welle 3: Fortschritt (je Ebene der Abdeckung die erste Zeile aufgeklappt) und Suche mit den Treffern zu \"Subnetz\"");
            Messwerte.Notiere(string.Create(inv, $"  dazu je Stufe und Groesse {9 + Core.Inhalte.Fachbereiche.Alle.Count} Ansichten der Welle 2: Karteikarten nach dem Pruefen (3 Modi, laengster Themenname), je Fachbereich die Karte mit dem laengsten Wort (Fix A 0.14.2), Pruefungstrainer Frage und Ergebnis, Uebernahme Vorschau, Ergebnis und Fehler, Rueckfrage Abbrechen"));
            Messwerte.Notiere("  und 6 Ansichten der Rechner-Seite (Phase 3), jeweils mit offenem Rechenweg: IPv6 Netzdaten, 2001:db8::/32 in 4.294.967.296 Subnetze (letzte Seite), Hostbedarf mit 12 Zeilen, Fehleingabe RAID, USV zu klein, Reiter Trainer");
            Messwerte.Notiere("  und 9 weitere Ansichten des Rechen-Trainers (Phase 3, W3, ohne Speichern): je IPv6, Nach Hostbedarf, Netz aufteilen, IPv4 Schwer offen und geprueft, Auswertung");
            Messwerte.Notiere("  und 13 Ansichten der Welle W4a (Phase 3): Datenmengen 4,7 GB, 10^12 TiB, 0,001 Bit, Fehleingabe; Uebertragung Vorgabe, 10^12 TiB mit 0,001 kbit/s bei 0,1 %, 0,001 Byte mit 1.000.000 Gbit/s; Bild und Video; Trainer Datenmengen und Uebertragungsdauer Schwer offen und geprueft, Eingabefehler");
            Messwerte.Notiere("  und 8 Ansichten der Welle W4b (Phase 3): Bild und Video mit Video-Ergebnis, Rechenweg und aktivem Uebernahme-Knopf; groesste Werte (100.000 x 100.000, 32 Bit, 1.000 fps, 10.000 Minuten); Fehler Dauer ohne Bildrate; Uebernahme-Ergebnis in Uebertragung (MiB und Byte); Trainer Bild und Video Schwer offen und geprueft, Eingabefehler");
            Messwerte.Notiere("  und 27 Ansichten der Welle W4c (Phase 3): WiSo-Rechner je Rechner mit Vorgaben und offenem Rechenweg (6), groesste Werte (Verkauf 999.999.999,99 mit 99,99 % in allen Prozentfeldern, Deckung 10.000.000 Stueck, Abschreibung 50 Jahre ab Dezember, Zinsen 600 Monate, Amortisation 20.000/6.000/500), Fehler zu viele Nachkommastellen (Geld und Prozent), Zinsen mit Pille Monate; Trainer WiSo je Art Schwer offen und geprueft (12), Eingabefehler");
            Messwerte.Notiere("  und 11 Ansichten der Welle W1 (Phase 4, ohne Speichern): AP1, AP2 und Testprojekt je laengste Aufgabe mit aufgedeckter Loesung und \"Wusstest du es?\" sowie letzte Seite der Liste; Notizblock Status Offen mit allen Musterantworten, Suche ohne Treffer, Suche \"netz\"");
            Messwerte.Notiere(string.Create(inv, $"  und {KlausurTeil.ProbeNamen.Length + KlausurFenster.Length} Ansichten der Welle W2 (Phase 4, ohne Speichern): Pruefung Auswahl SI mit 9 Pruefungen und vollstaendigem Gesamtergebnis, Auswahl AE (gesperrt, noch offen), laufende Pruefung Aufgabe und WiSo, Bewertung, Ergebnis Aufgabe und WiSo mit \"Nach Themen\", WiSo nicht gespeichert; Rueckfragen Starten, Abbrechen, Abgeben, Zeit abgelaufen (2), Abschliessen, Pruefung laeuft"));
            Messwerte.Notiere(string.Create(inv, $"  und {Abschlussprojekt.Reiter.Count + ProjektFenster.Length + 1} Ansichten der Welle W3 (Phase 4, ohne Speichern): Abschlussprojekt je Reiter (6), Fachgespraech mit Hinweis und \"Wusstest du es?\", Rueckfrage Loeschen mit langem Titel, Export misslingt, Kurzhinweis \"Gespeichert: …\""));
            Messwerte.Notiere(string.Create(inv, $"  und {W4AnsichtenAnzahl} Ansichten der Welle W4 (Phase 4, ohne Speichern): Hilfe eingeklappt und mit dem laengsten Abschnitt offen, Rundgang Seiten 1 bis 5 und Seite 5 mit Fehlerzeile, Ausbildungsfrage, Warnhinweis DPA, Optionen \"Rundgang und Hilfe\" und \"Lerninhalte\""));
            Messwerte.Notiere(string.Create(inv, $"  und {P5AnsichtenAnzahl} Ansichten der Welle W1 (Phase 5, ohne Speichern): Optionen \"Sicherung\", \"Löschen und zurücksetzen\", \"Datenbank\"; Vorschau der Sicherung, beide Rueckfragen zu \"Alles ersetzen\", Ersetzt, Zusammengefuehrt, Kopie gescheitert, Einspielen gescheitert, Rueckfrage Historie, Nichts geloescht, Geloescht mit Uhrhinweis, Bitte warten beim Ersetzen"));
            Messwerte.Notiere(string.Create(inv, $"  und {P5W2AnsichtenAnzahl} Ansichten der Welle W2 (Phase 5, ohne Speichern): Optionen \"Diagnose und Werkzeuge\" mit Problembericht, Fehler Datenordner, Fehler Speichern, Kurzhinweis \"Bericht in die Zwischenablage kopiert\""));
            Messwerte.Notiere(string.Create(inv, $"  und {P5W3AnsichtenAnzahl} Ansichten der Welle W3 (Phase 5, ohne Netz und ohne Speichern): Optionen \"Updates\" mit Fehlerzeile; Fenster \"Update verfügbar\" installiert, als Ordner-Programm, am Handy, beim Laden mit Balken, nach einem Fehler; \"Update nicht abgeschlossen\""));
            Messwerte.Notiere(string.Create(inv, $"  und {P6W2AnsichtenAnzahl} Ansichten der Welle W2 (Phase 6, ohne Anlegen und ohne Speichern): Optionen \"Updates\" mit \"Verknüpfung anlegen\" (Stand, langes Ergebnis mit zwei Pfaden und Hinweis \"Start erlauben\"), Rueckfrage \"Verknüpfung\" beim Start, Ergebnis ohne Desktop-Ordner"));
            Messwerte.Notiere(string.Create(inv, $"  und {P8W2AnsichtenAnzahl} Ansichten der Welle W2 (Phase 8): Optionen \"Über das Programm\" mit der Liste \"Lizenzen\" (alle zu, erster Bestandteil offen)"));
            Messwerte.Notiere(string.Create(inv, $"  und {P8W2bAnsichtenAnzahl} Ansichten der Welle W2b (Phase 8): Seitenleiste mit offenen Unterpunkten Karteikarten und Spiel, Optionen in Gruppen mit allen Karten zu"));
        }
        if (MitSpiel)
        {
            Messwerte.Notiere(string.Create(inv, $"  und {P7W1AnsichtenAnzahl} Ansichten der Wellen W1/W4/W5 (Phase 7, Stadt in 3D im Vollbild, ohne Speichern): Stadt mit Namensschildern, Ortsliste offen, Moebelhaus geschlossen, Buero innen, Kunde innen, Wohnung beim Einrichten mit gewaehltem Moebel, ohne 3D (Hinweis und Ortsliste)"));
            Messwerte.Notiere("  und die Spielansichten der Wellen W6/W7 (Phase 7): Platzwahl, Figur, Uebersicht, Meine Reise, Meilenstein, 9 Ticket-Bretter, 9 Firmen-Reiter; Moebelhaus und Elektromarkt innen (Eingang, Verkauf, Ware), Sortiment Moebelhaus und Elektromarkt mit zwei Teile-Typen (ohne Kaufen); W7b: Bank und Amt innen (Eingang, Schalter), Seiten Bank und Amt (ohne Schreiben)");
        }
        Messwerte.Notiere(w4Gekuerzt.Count == 0
            ? "  W4-Ansichten in allen Groessen geprueft (Zeitgrenze 7,5 Minuten nicht erreicht)"
            : "  W4-Ansichten ab 7,5 Minuten nur schmal und pc, ohne grenze bei: " + string.Join(", ", w4Gekuerzt));
        Messwerte.Notiere("  Groessen: " + string.Join(", ", gepruefteGroessen));
        foreach (var fund in layoutFunde.Distinct().Take(60))
        {
            Messwerte.Notiere("  " + fund);
        }
        funde += layoutFunde.Count;
        return funde;
    }

    /// <summary>Sammelliste 106: leere Folge fuer die Ansichten des anderen Teils.</summary>
    private static async IAsyncEnumerable<T> Keine<T>()
    {
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>Weisung P1-3 (nur Lernteil): Optionen zweimal oeffnen.</summary>
    private static async Task<int> OptionenZweimalAsync(MainView ansicht)
    {
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
        return einmal ? 0 : 1;
    }

    /// <summary>Alle Werte ueber der Abbruchgrenze als FEHLER-Zeilen; liefert die Zahl der Funde.</summary>
    private static int AbschlussAuswerten(List<string> reihenRot)
    {
        var funde = 0;
        var zuLang = Messwerte.ErstesOeffnen.Where(e => e.Value > Abbruch).Select(e => e.Key).ToList();
        foreach (var (reihe, ms) in Messwerte.Hoechstwerte().Where(r => r.Value > Abbruch))
        {
            zuLang.Add(reihe);
        }
        // Sammelliste 64 (nur Text): die Zeile nennt auch einmalige Messpunkte ueber 1 000 ms; das Urteil
        // faellt wie bisher ueber ihre FEHLER-Zeilen (dort schon gezaehlt, hier nicht noch einmal)
        var zuLangText = zuLang.Concat(UeberAbbruch.Where(t => !zuLang.Contains(t))).Distinct().ToList();
        Messwerte.Notiere(zuLangText.Count == 0
            ? "Zeitgrenze CI: alle Werte unter 1000 ms"
            : "Zeitgrenze CI ueberschritten (ueber 1000 ms): " + string.Join(", ", zuLangText));
        // Weisung P4-12 B5: Messpunkt, Wert, Grenze und Einzelmessungen; erstes Oeffnen und der
        // Seitenwechsel (MainView) sind einmalige Messungen und werden nicht wiederholt
        foreach (var (seite, ms) in Messwerte.ErstesOeffnen.Where(e => e.Value > Abbruch))
        {
            Messwerte.Notiere("  FEHLER: " + Messwiederholung.Einmal(ms, Abbruch).Fehlertext("Erstes Oeffnen '" + seite + "'"));
        }
        foreach (var text in reihenRot)
        {
            Messwerte.Notiere("  FEHLER: " + text);
        }
        foreach (var (reihe, ms) in Messwerte.Hoechstwerte().Where(r => r.Value > Abbruch && !reihenRot.Any(t => t.StartsWith(r.Key + ":", StringComparison.Ordinal))))
        {
            Messwerte.Notiere("  FEHLER: " + Messwiederholung.Einmal(ms, Abbruch).Fehlertext(reihe + " (Hoechstwert)"));
        }
        funde += zuLang.Count;
        funde += _w2Fehlt;   // Phase 5 W2: fehlender Diagnose-Bereich (Gegenpruefung Fund 11)
        funde += _w3Fehlt;   // Phase 5 W3: fehlender Update-Bereich
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
        // Fix A 0.14.2 (Weisung P8-9): je Fachbereich die Karte mit dem laengsten Wort (Frage, Antworten,
        // Musterloesung) nach dem Pruefen mit Multiple Choice, damit kein Fund vom Datum (der gezeigten Karte) abhaengt
        foreach (var (fachbereich, frage) in KartenMitLaengstemWort(inhalte))
        {
            karten.Probe(1, frage);
            await ansicht.BildFertigAsync();
            yield return ("cards/laengstes_wort/" + fachbereich, 3);
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
            // Phase 8 W5: Rueckfrage am Bett (Feierabend ueber das Bett), beide Texte
            ("spiel/bett_feierabend", () => Dialog.Zeigen(Core.Spiel.Wohnung.SchlafenTitel, Core.Spiel.Wohnung.SchlafenFertig,
                Core.Spiel.Wohnung.SchlafenNein, Core.Spiel.Wohnung.SchlafenJa)),
            ("spiel/bett_offen", () => Dialog.Zeigen(Core.Spiel.Wohnung.SchlafenTitel, Core.Spiel.Wohnung.SchlafenOffen(12), Dialog.Ok)),
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

    /// <summary>
    /// Fix A 0.14.2: je Fachbereich (Reihenfolge wie die Seitenleiste) die Frage der Karteikarte mit dem laengsten
    /// Wort in Frage, Antworten und Musterloesung (Zeichen bis zum naechsten Leerraum; bei Gleichstand die erste Karte).
    /// </summary>
    internal static List<(string Fachbereich, string Frage)> KartenMitLaengstemWort(Lerninhalte inhalte)
    {
        static int Laengstes(Lerninhalt k) =>
            new[] { k.Frage, k.Antwort, k.Text("a_full") }.Concat(k.Optionen)
                .SelectMany(t => (t ?? "").Split([' ', '\u00A0', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
                .Select(w => w.Length).DefaultIfEmpty(0).Max();
        var liste = new List<(string, string)>();
        foreach (var fachbereich in Core.Inhalte.Fachbereiche.Alle)
        {
            if (inhalte.Karteikarten.Where(k => k.Fachbereich == fachbereich && k.Frage is not null).MaxBy(Laengstes) is { } karte)
            {
                liste.Add((fachbereich, karte.Frage!));
            }
        }
        return liste;
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

    // Phase 4, W2: Balken "Nach Themen" (rot/gruen) auf card_hi; alle Textfarben der Klausur stehen schon oben
    private static readonly (string Text, string Flaeche)[] KlausurPaare =
    [
        ("red", "card_hi"), ("green", "card_hi"),
    ];

    // Phase 4, W3: Rahmen des Kurzhinweises (yellow auf card_hi); Summe rot und Warnungen gelb auf card stehen schon oben
    private static readonly (string Text, string Flaeche)[] ProjektPaare =
    [
        ("yellow", "card_hi"),
    ];

    // Phase 4, W1: Bereichsnamen im Notizblock (accent, purple, blue, accent2, orange) und Status auf card_alt
    private static readonly (string Text, string Flaeche)[] W1Paare =
    [
        ("blue", "card_alt"), ("purple", "card_alt"), ("accent2", "card_alt"), ("orange", "card_alt"), ("yellow", "card_alt"),
        ("text_soft", "card_alt"), ("text", "card_hi"), ("text_dim", "card_hi"),
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
        // Weisung P4-12 B6: mit zurueck (Zustand davor) oder wiederholbar (gleiche Rechnung) wird
        // ueber 1 000 ms wiederholt; ohne beides streng
        async Task Messpunkt(string titel, Action schritt, bool ohneFehler = true, Action? zurueck = null, bool wiederholbar = false)
        {
            var reihe = await BildMessenAsync(ansicht, schritt, zurueck, wiederholbar);
            var fehler = s.Aktuell.Fehler.IsVisible ? s.Aktuell.Fehler.Text : null;
            var rot = Ergebnis(titel, reihe);
            funde += rot;
            if (ohneFehler && fehler is not null)
            {
                Messwerte.Notiere("  FEHLER: " + fehler);
                funde += 1 - rot;
            }
        }
        rechner.Waehle("subnetting");
        s.ModusSetzen("ipv4");
        await ansicht.BildFertigAsync();
        await Messpunkt("Rechner Wechsel IPv4 -> IPv6", () => s.ModusSetzen("ipv6"), zurueck: () => s.ModusSetzen("ipv4"));
        s.AnsichtSetzen("ipv6", "aufteilen");
        await ansicht.BildFertigAsync();
        foreach (var (netz, anzahl) in new[] { ("2001:db8::/32", "4294967296"), ("2001:db8::/64", "18446744073709551616") })
        {
            s.Aktuell.Netz.Text = netz;
            s.Aktuell.Zweites!.Text = anzahl;
            await Messpunkt($"Rechner {netz} in {anzahl} Subnetze, erste Seite", s.Aktuell.Rechnen, wiederholbar: true);
            await Messpunkt($"Rechner {netz} in {anzahl} Subnetze, letzte Seite", () => s.Aktuell.Blaettern("last"), zurueck: () => s.Aktuell.Blaettern("first"));
            Messwerte.Notiere("  " + s.Aktuell.SeiteText);
        }
        s.ModusSetzen("ipv4");
        s.AnsichtSetzen("ipv4", "bedarf");
        await ansicht.BildFertigAsync();
        s.Aktuell.Netz.Text = "10.0.0.0/16";
        s.Aktuell.Zweites!.Text = string.Join("\n", Hostbedarf12);
        await Messpunkt("Rechner Hostbedarf mit 12 Zeilen", s.Aktuell.Rechnen, wiederholbar: true);
        await Messpunkt("Rechner Rechenweg (Hostbedarf) oeffnen", () => s.RechenwegZeigen(true), zurueck: () => s.RechenwegZeigen(false));
        s.RechenwegZeigen(false);
        await ansicht.BildFertigAsync();
        // W4a (Weisung P3-17): Auswahl-Pillen, Datenmengen und Uebertragung mit Rechenweg
        var d = rechner.Daten;
        async Task DatenMesspunkt(string titel, Action schritt, Func<string> fehler, Action? zurueck = null, bool wiederholbar = false)
        {
            var rot = Ergebnis(titel, await BildMessenAsync(ansicht, schritt, zurueck, wiederholbar));
            funde += rot;
            if (fehler().Length > 0)
            {
                Messwerte.Notiere("  FEHLER: " + fehler());
                funde += 1 - rot;
            }
        }
        await DatenMesspunkt("Rechner Auswahl Datenmengen", () => rechner.Waehle("datenmengen"), () => "", Auswahl(rechner, rechner.Gewaehlt));
        d.DatenEingabe("1.000.000.000.000", "TiB");
        await DatenMesspunkt("Datenmengen 10^12 TiB berechnen", d.DatenRechnen, () => d.DatenFehler, wiederholbar: true);
        await DatenMesspunkt("Rechenweg (Datenmengen) oeffnen", () => d.DatenWeg.Zeigen(true), () => d.DatenFehler, () => d.DatenWeg.Zeigen(false));
        d.DatenWeg.Zeigen(false);
        await DatenMesspunkt("Rechner Auswahl Uebertragung", () => rechner.Waehle("uebertragung"), () => "", Auswahl(rechner, rechner.Gewaehlt));
        d.DauerEingabe("1.000.000.000.000", "TiB", "0,001", "kbit/s", "0,1");
        await DatenMesspunkt("Uebertragung 10^12 TiB mit 0,001 kbit/s bei 0,1 % berechnen", d.DauerRechnen, () => d.DauerFehler, wiederholbar: true);
        await DatenMesspunkt("Rechenweg (Uebertragung) oeffnen", () => d.DauerWeg.Zeigen(true), () => d.DauerFehler, () => d.DauerWeg.Zeigen(false));
        d.DauerWeg.Zeigen(false);
        d.DatenEingabe(RechnerTexte.DataDefault, RechnerTexte.DataDefaultUnit);
        d.DauerEingabe(RechnerTexte.TransferDefaultSize, RechnerTexte.DataDefaultUnit, RechnerTexte.TransferDefaultRate, "Mbit/s", RechnerTexte.TransferDefaultPercent);
        funde += await VideoMesspunkteAsync(ansicht, rechner, DatenMesspunkt);
        funde += await WisoMesspunkteAsync(ansicht, rechner, DatenMesspunkt);
        rechner.Waehle("subnetting");
        await ansicht.BildFertigAsync();
        // W3 (R7): Reiter Trainer (erstes Zeigen liest die Statistik), Runde und Pruefen ohne Speichern
        await Messpunkt("Rechner Reiter Trainer (erstes Zeigen)", () => rechner.Tab("trainer"), ohneFehler: false);
        // Probe baut jede Runde neu mit festem Startwert (wiederholbar); Pruefen und Naechste
        // Aufgabe stellen vorher denselben Zustand wieder her (gleiche Folge wie beim ersten Mal)
        var t = rechner.Trainer;
        await Messpunkt("Trainer Runde starten (ohne Speichern)", () => t.Probe("bedarf", 3, pruefen: false), ohneFehler: false, wiederholbar: true);
        await Messpunkt("Trainer Pruefen (ohne Speichern)", t.Druecken, ohneFehler: false, zurueck: () => t.Probe("bedarf", 3, pruefen: false));
        await Messpunkt("Trainer Naechste Aufgabe (ohne Speichern)", t.Druecken, ohneFehler: false, zurueck: () => { t.Probe("bedarf", 3, pruefen: false); t.Druecken(); });
        await Messpunkt("Trainer Runde Uebertragungsdauer Schwer (ohne Speichern)", () => t.Probe("dauer", 3, pruefen: false), ohneFehler: false, wiederholbar: true);
        await Messpunkt("Trainer Pruefen Uebertragungsdauer (ohne Speichern)", () => t.Probe("dauer", 3, pruefen: true), ohneFehler: false, wiederholbar: true);
        await Messpunkt("Trainer Runde Bild und Video Schwer (ohne Speichern)", () => t.Probe("video", 3, pruefen: false), ohneFehler: false, wiederholbar: true);
        await Messpunkt("Trainer Pruefen Bild und Video (ohne Speichern)", () => t.Probe("video", 3, pruefen: true), ohneFehler: false, wiederholbar: true);
        // W4c (Weisung P3-23): Trainer WiSo
        await Messpunkt("Trainer Runde Verkaufskalkulation Schwer (ohne Speichern)", () => t.Probe("verkauf", 3, pruefen: false), ohneFehler: false, wiederholbar: true);
        await Messpunkt("Trainer Pruefen Verkaufskalkulation richtig (ohne Speichern)", () => t.Probe("verkauf", 3, pruefen: true, richtig: true), ohneFehler: false, wiederholbar: true);
        await Messpunkt("Trainer Runde Amortisation Schwer (ohne Speichern)", () => t.Probe("amort", 3, pruefen: false), ohneFehler: false, wiederholbar: true);
        await Messpunkt("Trainer Pruefen Amortisation falsch (ohne Speichern)", () => t.Probe("amort", 3, pruefen: true), ohneFehler: false, wiederholbar: true);
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
    /// <summary>Messpunkt der Rechner-Karten (Weisung P4-12 B6: zurueck bzw. wiederholbar wie bei <c>BildMessenAsync</c>).</summary>
    private delegate Task DatenPunkt(string titel, Action schritt, Func<string> fehler, Action? zurueck = null, bool wiederholbar = false);

    /// <summary>Zurueck auf die Karte, die vor dem Messpunkt gewaehlt war (Wert jetzt gelesen).</summary>
    private static Action Auswahl(RechnerSeite rechner, string vorher) => () => rechner.Waehle(vorher);

    private static async Task<int> VideoMesspunkteAsync(MainView ansicht, RechnerSeite rechner,
        DatenPunkt messpunkt)
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
        await messpunkt("Rechner Auswahl Bild und Video", () => rechner.Waehle("bild"), () => "", Auswahl(rechner, rechner.Gewaehlt));
        Pruefe(!v.KnopfAktiv, "Uebernahme-Knopf vor der ersten Berechnung aktiv");
        v.Rechnen();
        Pruefe(!v.KnopfAktiv, "Uebernahme-Knopf ohne Dauer aktiv");
        v.Eingabe("1920", "1080", "25", "24", "2", "50");
        await messpunkt("Bild und Video mit Video berechnen", v.Rechnen, () => v.Fehler, wiederholbar: true);
        Pruefe(v.KnopfAktiv, "Uebernahme-Knopf mit Video-Ergebnis gesperrt");
        await messpunkt("Rechenweg (Bild und Video) oeffnen", () => v.Weg.Zeigen(true), () => v.Fehler, () => v.Weg.Zeigen(false));
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
        await messpunkt("Uebernahme in Uebertragung (Wechsel und Rechnen)", v.Uebernehmen, () => d.DauerFehler, Auswahl(rechner, "bild"));
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
        ("verkauf", ["999.999.999,99", "99,99", "99,99", "99,99", "99,99", "100"], null),
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
        DatenPunkt messpunkt)
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
        await messpunkt("Rechner Auswahl WiSo", () => rechner.Waehle("wiso"), () => "", Auswahl(rechner, rechner.Gewaehlt));
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
            var vorher = w.Gewaehlt;
            await messpunkt("WiSo Auswahl " + name, () => w.Waehle(r), () => "", vorher == r ? null : () => w.Waehle(vorher));
            await messpunkt("WiSo " + name + " berechnen", () => w.Rechnen(r), () => w.Fehler(r), wiederholbar: true);
            Pruefe(w.Kasten(r).Roh.Split('\n').Any(z => z.StartsWith(letzte[r], StringComparison.Ordinal) && z.EndsWith(": " + soll[r], StringComparison.Ordinal)),
                name + ": Ergebnis ohne \"" + letzte[r] + " : " + soll[r] + "\"");
            await messpunkt("Rechenweg (WiSo " + name + ") oeffnen", () => w.Weg(r).Zeigen(true), () => w.Fehler(r), () => w.Weg(r).Zeigen(false));
            w.Weg(r).Zeigen(false);
        }
        w.Waehle("bezug");
        w.Eingabe("bezug", ["1.200,001", "15", "2", "25"]);
        w.Rechnen("bezug");
        Pruefe(w.Fehler("bezug") == "Bitte höchstens 2 Nachkommastellen (volle Cent) eingeben: Listeneinkaufspreis (€)." && w.Kasten("bezug").Roh.Length == 0,
            "Fehler zu viele Nachkommastellen fehlt oder Ergebnis nicht leer");
        w.Waehle("verkauf");
        w.Eingabe("verkauf", ["1.024,60", "99,999", "10", "2", "10", "19"]);
        w.Rechnen("verkauf");
        Pruefe(w.Fehler("verkauf") == "Bitte höchstens 2 Nachkommastellen eingeben: Handlungskosten (%)." && w.Kasten("verkauf").Roh.Length == 0,
            "Fehler Prozent mit 3 Nachkommastellen fehlt oder Ergebnis nicht leer");
        w.Waehle("zinsen");
        w.Eingabe("zinsen", ["5.000", "4,5", "3"], "monate");
        await messpunkt("WiSo Zinsen 3 Monate berechnen", () => w.Rechnen("zinsen"), () => w.Fehler("zinsen"), wiederholbar: true);
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
        w.Waehle("verkauf");
        w.Eingabe("verkauf", ["1.024,60", "99,999", "10", "2", "10", "19"]);
        w.Rechnen("verkauf");
        await ansicht.BildFertigAsync();
        yield return "calc/wiso-fehler-prozent";
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
        // einmalige Messpunkte (erstes Laden, erste Suche) bleiben streng
        void Messpunkt(string titel, double? ms) => funde += Ergebnis(titel, Messwiederholung.Einmal(ms, Abbruch));
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
            // reine Rechnung mit neuen Objekten je Messung: wiederholbar (Weisung P4-12 B6)
            var teile = new List<(double Buch, double Plan)>();
            double Rechnen()
            {
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
                teile.Add((msBuch, msPlan));
                return uhr.Elapsed.TotalMilliseconds;
            }
            var reihe = await RechnungMessenAsync(Rechnen);
            funde += Ergebnis("Faelligkeit bei 100 000 synthetischen Antworten (Buch, Plan, erste Sitzung)", reihe);
            var beste = Enumerable.Range(0, reihe.Werte.Count).MinBy(i => reihe.Werte[i]);
            var msGesamt = reihe.Werte[beste]!.Value;
            var (msBuchBest, msPlanBest) = teile[beste];
            Messwerte.Notiere(string.Create(inv, $"  davon Antworten einlesen {msBuchBest:F0} ms, Plan {msPlanBest:F0} ms, erste Sitzung {msGesamt - msBuchBest - msPlanBest:F0} ms"));
        }
        return funde;
    }

    /// <summary>
    /// Phase 4, W1: Ansichten der Lernseiten nacheinander zeigen (je nach dem Zeigen pruefen),
    /// ohne zu speichern: AP1, AP2 und Testprojekt je laengste Aufgabe mit aufgedeckter Loesung und
    /// "Wusstest du es?" sowie letzte Seite der Liste; Notizblock mit Status Offen und allen
    /// Musterantworten, Suche ohne Treffer, Suche "netz". Danach wieder der Anfang.
    /// </summary>
    private static async IAsyncEnumerable<string> W1Ansichten(MainView ansicht)
    {
        foreach (var id in (string[])[Seiten.Ap1Szenarien, Seiten.Ap2Szenarien, Seiten.Testprojekt])
        {
            if (!ansicht.GebauteSeiten.TryGetValue(id, out var gebaut) || gebaut is not SzenarioSeite seite)
            {
                continue;
            }
            await seite.Geladen;
            await ansicht.ZeigeAsync(id);
            foreach (var (art, name) in new[] { (0, "/aufgedeckt"), (1, "/letzte-seite") })
            {
                seite.Probe(art);
                await ansicht.BildFertigAsync();
                yield return id + name;
            }
            seite.Probe(-1);
        }
        if (ansicht.GebauteSeiten.TryGetValue(Seiten.Notizblock, out var nb) && nb is NotizblockSeite notiz)
        {
            await notiz.Geladen;
            await ansicht.ZeigeAsync(Seiten.Notizblock);
            foreach (var (art, name) in new[] { (0, "/offen-alle-antworten"), (1, "/suche-ohne-treffer"), (2, "/suche-netz") })
            {
                notiz.Probe(art);
                await ansicht.BildFertigAsync();
                yield return Seiten.Notizblock + name;
            }
            notiz.Probe(-1);
        }
        await ansicht.BildFertigAsync();
    }

    /// <summary>Rueckfragen und Hinweise der Klausursimulation (Wortliste W2 Abschnitt 4), laengster Bereichsname.</summary>
    private static readonly (string Name, Func<Task<int>> Zeigen)[] KlausurFenster =
    [
        ("pruefung/frage-starten", () => Dialog.Zeigen(PruefungTexte.StartTitel, Beschriftungen.Einsetzen(Geraetetexte.PruefungStartFrage(true),
            Core.Pruefung.Klausur.Arten.MaxBy(a => a.Name.Length)!.Name, 90), TrainerTexte.Ja, TrainerTexte.Nein)),
        ("pruefung/frage-abbrechen", () => Dialog.Zeigen(PruefungTexte.AbbrechenTitel, PruefungTexte.AbbrechenFrage, TrainerTexte.Ja, TrainerTexte.Nein)),
        ("pruefung/frage-abgeben", () => Dialog.Zeigen(PruefungTexte.AbgebenTitel, PruefungTexte.AbgebenFrage, TrainerTexte.Ja, TrainerTexte.Nein)),
        ("pruefung/zeit-wiso", () => Dialog.Zeigen(PruefungTexte.ZeitTitel, PruefungTexte.ZeitWiso, Dialog.Ok)),
        ("pruefung/zeit-aufgaben", () => Dialog.Zeigen(PruefungTexte.ZeitTitel, PruefungTexte.ZeitOffen, Dialog.Ok)),
        ("pruefung/frage-abschliessen", () => Dialog.Zeigen(PruefungTexte.BewertungAbschliessen, PruefungTexte.AbschliessenFrage, TrainerTexte.Ja, TrainerTexte.Nein)),
        ("pruefung/laeuft", () => Dialog.Zeigen(PruefungTexte.LaeuftTitel, PruefungTexte.LaeuftText, Dialog.Ok)),
    ];

    // Phase 4, W3: langer Titel fuer Rueckfrage und Kurzhinweis
    private const string LangerTitel = "Einführung einer zentralen Benutzer- und Rechteverwaltung mit Active Directory für 120 Arbeitsplätze";

    /// <summary>Rueckfrage, Fehler beim Export und Kurzhinweis des Abschlussprojekts (Wortliste W3 Nr 16, 19, 28).</summary>
    private static readonly (string Name, Action Zeigen, Action Schliessen)[] ProjektFenster =
    [
        ("abschluss/rueckfrage-loeschen", () => _ = Dialog.Zeigen(ProjektTexte.RueckfrageTitel,
            Abschlussprojekt.Rueckfrage(new Dictionary<string, string> { [Abschlussprojekt.TitelFeld] = LangerTitel }, true), TrainerTexte.Ja, TrainerTexte.Nein),
            Dialog.AlleSchliessen),
        ("abschluss/export-fehler", () => _ = Dialog.Zeigen(ProjektTexte.SpeichernFehlgeschlagen, ProjektTexte.DateiNichtGespeichert, Dialog.Ok),
            Dialog.AlleSchliessen),
        ("abschluss/kurzhinweis", () => Kurzhinweis.Zeigen(Beschriftungen.Einsetzen(ProjektTexte.Gespeichert,
            Abschlussprojekt.ExportName(new Dictionary<string, string> { [Abschlussprojekt.TitelFeld] = LangerTitel }, "pdf"))), Kurzhinweis.Schliessen),
    ];

    /// <summary>
    /// Phase 4, W3: die 6 Reiter des Abschlussprojekts nacheinander zeigen (je nach dem Zeigen
    /// pruefen), dann Fachgespraech mit Hinweis und "Wusstest du es?", die Fenster. Nichts wird
    /// gespeichert (es gibt keine offenen Eingaben); danach wieder Reiter "Übersicht".
    /// </summary>
    private static async IAsyncEnumerable<string> ProjektAnsichten(MainView ansicht)
    {
        if (!ansicht.GebauteSeiten.TryGetValue(Seiten.Abschluss, out var gebaut) || gebaut is not AbschlussprojektSeite seite)
        {
            yield break;
        }
        await ansicht.ZeigeAsync(Seiten.Abschluss);
        foreach (var (reiter, _) in Abschlussprojekt.Reiter)
        {
            seite.Probe(reiter);
            await ansicht.BildFertigAsync();
            yield return "abschluss/" + reiter;
        }
        seite.ProbeHinweis(true);
        await ansicht.BildFertigAsync();
        yield return "abschluss/fachgespraech-hinweis";
        seite.ProbeHinweis(false);
        foreach (var (name, zeigen, schliessen) in ProjektFenster)
        {
            zeigen();
            await ansicht.BildFertigAsync();
            yield return name;
            schliessen();
        }
        seite.Probe(Abschlussprojekt.Reiter[0].Schluessel);
        await ansicht.BildFertigAsync();
    }

    /// <summary>
    /// Phase 4, W3 (Plan P4-8 Abschnitt 6, Messpunkte): erstes Laden des Projekts, jeder Reiter
    /// (erstes Bauen und Wiederverwenden), Projekt wechseln, PDF und Text eines Projekts mit
    /// 8 vollen Kapiteln erzeugen. Grenze 400 ms, Fund ab 1 000 ms. Nichts wird gespeichert.
    /// </summary>
    private static async Task<int> ProjektMesspunkteAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        if (!ansicht.GebauteSeiten.TryGetValue(Seiten.Abschluss, out var gebaut) || gebaut is not AbschlussprojektSeite seite)
        {
            Messwerte.Notiere("Abschlussprojekt: FEHLER, Seite nicht gebaut");
            return 1;
        }
        async Task Messpunkt(string titel, Action schritt, Action? zurueck = null) => funde += Ergebnis(titel, await BildMessenAsync(ansicht, schritt, zurueck));
        // erstes Laden und erstes Bauen der Reiter sind einmalig (streng); "wiederverwendet"
        // zeigt vor einer Wiederholung den Reiter davor (Weisung P4-12 B6)
        funde += Ergebnis("Abschlussprojekt erstes Laden des Projekts (Reiter Übersicht gebaut)", Messwiederholung.Einmal(seite.LadenMs, Abbruch));
        await ansicht.ZeigeAsync(Seiten.Abschluss);
        var davor = Abschlussprojekt.Reiter[0].Item1;
        foreach (var runde in (string[])["erstes Bauen", "wiederverwendet"])
        {
            foreach (var (reiter, name) in Abschlussprojekt.Reiter.Skip(1).Append(Abschlussprojekt.Reiter[0]))
            {
                var zurueckReiter = davor;
                await Messpunkt($"Abschlussprojekt Reiter \"{name}\" ({runde}, bis zum Bild)", () => seite.Probe(reiter),
                    runde == "erstes Bauen" ? null : () => seite.Probe(zurueckReiter));
                davor = reiter;
            }
        }
        var vorher = seite.Ablauf.Projekt;
        var neu = "messung-" + Abschlussprojekt.NeueKennung();
        await Messpunkt("Abschlussprojekt Projekt wechseln (Reiter neu, bis zum Bild)", () => seite.ProbeLaden(neu), () => seite.ProbeLaden(vorher));
        seite.ProbeLaden(vorher);
        await ansicht.BildFertigAsync();
        // Projekt mit 8 vollen Kapiteln (je etwa 2 Seiten), vollem Antrag und Sachkosten
        var felder = new Dictionary<string, string>(StringComparer.Ordinal) { [Abschlussprojekt.TitelFeld] = LangerTitel };
        var absatz = string.Concat(Enumerable.Repeat("Die Umsetzung erfolgt schrittweise nach Plan, mit Tests und Abnahme durch den Auftraggeber. ", 60));
        foreach (var k in Abschlussprojekt.Kapitel)
        {
            felder["kapitel_" + k.Schluessel] = absatz + "\n\n" + absatz;
        }
        foreach (var a in Abschlussprojekt.Antrag)
        {
            felder[a.Schluessel] = absatz[..600];
        }
        for (var n = 0; n < Abschlussprojekt.Sachkostenzeilen; n++)
        {
            felder["material_name_" + n] = "Server mit Lizenz " + n;
            felder["material_betrag_" + n] = "1.234,50";
        }
        // reine Rechnungen: wiederholbar
        var pdf = Array.Empty<byte>();
        var text = Array.Empty<byte>();
        var reihePdf = await RechnungMessenAsync(() =>
        {
            var uhr = Stopwatch.StartNew();
            pdf = Abschlussprojekt.ExportPdf(felder, 40, DateTime.Now);
            return uhr.Elapsed.TotalMilliseconds;
        });
        funde += Ergebnis(string.Create(inv, $"Abschlussprojekt PDF erzeugen (8 volle Kapitel, {pdf.Length / 1024} KB)"), reihePdf);
        var reiheText = await RechnungMessenAsync(() =>
        {
            var uhr = Stopwatch.StartNew();
            text = Abschlussprojekt.TextDatei(Abschlussprojekt.ExportText(felder, 40), false);
            return uhr.Elapsed.TotalMilliseconds;
        });
        funde += Ergebnis(string.Create(inv, $"Abschlussprojekt Text erzeugen (8 volle Kapitel, {text.Length / 1024} KB)"), reiheText);
        return funde;
    }

    /// <summary>
    /// Phase 4, W2: Ansichten der Klausursimulation nacheinander zeigen (je nach dem Zeigen
    /// pruefen), ohne zu speichern (KlausurTeil.Probe), dann die Rueckfragen. Danach wieder
    /// wie gespeichert und Art "Übung".
    /// </summary>
    private static async IAsyncEnumerable<string> KlausurAnsichten(MainView ansicht)
    {
        if (!ansicht.GebauteSeiten.TryGetValue(Seiten.Trainer, out var gebaut) || gebaut is not TrainerSeite trainer)
        {
            yield break;
        }
        await trainer.Geladen;
        await ansicht.ZeigeAsync(Seiten.Trainer);
        trainer.ArtWaehlen(TrainerTexte.Pruefung);
        var klausur = trainer.Klausur!;
        for (var art = 0; art < KlausurTeil.ProbeNamen.Length; art++)
        {
            klausur.Probe(art);
            await ansicht.BildFertigAsync();
            yield return KlausurTeil.ProbeNamen[art];
        }
        klausur.Probe(-1);
        await ansicht.BildFertigAsync();
        foreach (var (name, zeigen) in KlausurFenster)
        {
            _ = zeigen();
            await ansicht.BildFertigAsync();
            yield return name;
            Dialog.AlleSchliessen();
        }
        trainer.ArtWaehlen(TrainerTexte.Uebung);
        await ansicht.BildFertigAsync();
    }

    /// <summary>
    /// Phase 4, W2 (Weisung P4-6, Messpunkte): Umschalter auf "Prüfung" (Auswahl mit Lernstand),
    /// laufende Pruefung zeigen (Aufgaben, WiSo), Aufgabe wechseln, WiSo blaettern, Bewertung zeigen
    /// und Aufgabe wechseln, Ergebnis zeigen, je bis zum Bild und ohne Speichern. Grenze 400 ms,
    /// Fund ab 1 000 ms.
    /// </summary>
    private static async Task<int> KlausurMesspunkteAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        if (!ansicht.GebauteSeiten.TryGetValue(Seiten.Trainer, out var gebaut) || gebaut is not TrainerSeite trainer)
        {
            Messwerte.Notiere("Klausursimulation: FEHLER, Pruefungstrainer nicht gebaut");
            return 1;
        }
        await trainer.Geladen;
        await ansicht.ZeigeAsync(Seiten.Trainer);
        async Task Messpunkt(string titel, Action schritt, Action? zurueck = null, bool wiederholbar = false) =>
            funde += Ergebnis(titel, await BildMessenAsync(ansicht, schritt, zurueck, wiederholbar));
        // Wechsel zu "Prüfung" baut den Teil zum ersten Mal (streng); Probe setzt jede Ansicht
        // vollstaendig neu (wiederholbar), Wechseln und Blaettern stellen vorher den Zustand davor her
        await Messpunkt("Pruefungstrainer Wechsel zu \"Prüfung\" (Auswahl mit Lernstand, bis zum Bild)", () => trainer.ArtWaehlen(TrainerTexte.Pruefung));
        var k = trainer.Klausur!;
        await Messpunkt("Pruefung Auswahl mit 9 Pruefungen und Gesamtergebnis (bis zum Bild)", () => k.Probe(0), wiederholbar: true);
        await Messpunkt("Laufende Pruefung zeigen, Aufgaben mit den laengsten Texten (bis zum Bild)", () => k.Probe(2), wiederholbar: true);
        await Messpunkt("Laufende Pruefung Aufgabe wechseln (bis zum Bild)", () => k.ProbeAufgabe(1), () => k.ProbeAufgabe(0));
        await Messpunkt("Laufende Pruefung WiSo zeigen (bis zum Bild)", () => k.Probe(3), wiederholbar: true);
        await Messpunkt("Laufende Pruefung WiSo blaettern (bis zum Bild)", k.ProbeBlaettern, () => k.Probe(3));
        await Messpunkt("Bewertung zeigen (bis zum Bild)", () => k.Probe(4), wiederholbar: true);
        await Messpunkt("Bewertung Aufgabe wechseln (bis zum Bild)", () => k.ProbeAufgabe(2), () => k.ProbeAufgabe(0));
        await Messpunkt("Ergebnis mit \"Nach Themen\" zeigen (bis zum Bild)", () => k.Probe(5), wiederholbar: true);
        k.Probe(-1);
        trainer.ArtWaehlen(TrainerTexte.Uebung);
        await ansicht.BildFertigAsync();
        return funde;
    }

    /// <summary>
    /// Phase 4, W1 (Plan Abschnitt 7): erstes Oeffnen der Lernseiten bis der erste Eintrag steht,
    /// AP2-Liste (330) blaettern, Szenario wechseln, Musterloesung aufdecken (ohne Speichern),
    /// Notizblock Filter und Suche, dazu Notizblock-Kern mit 100 000 synthetischen Antworten
    /// (wie test_grosse_datenmengen). Grenze 400 ms, Fund ab 1 000 ms.
    /// </summary>
    private static async Task<int> W1MesspunkteAsync(MainView ansicht, Lerninhalte? inhalte)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        // einmalige Messpunkte (erstes Laden, erste Suche) bleiben streng
        void Messpunkt(string titel, double? ms) => funde += Ergebnis(titel, Messwiederholung.Einmal(ms, Abbruch));
        // Weisung P4-12 B6: vor einer Wiederholung der Zustand davor (zurueck)
        async Task Bild(string titel, Action schritt, Action zurueck) => funde += Ergebnis(titel, await BildMessenAsync(ansicht, schritt, zurueck));
        // reine Rechnung mit denselben Daten (wiederholbar)
        async Task Rechnung(string titel, Action rechnung) => funde += Ergebnis(titel, await RechnungMessenAsync(() =>
        {
            var uhr = Stopwatch.StartNew();
            rechnung();
            return uhr.Elapsed.TotalMilliseconds;
        }));
        foreach (var id in (string[])[Seiten.Ap1Szenarien, Seiten.Ap2Szenarien, Seiten.Testprojekt])
        {
            if (ansicht.GebauteSeiten.TryGetValue(id, out var gebaut) && gebaut is SzenarioSeite seite)
            {
                await seite.Geladen;
                Messpunkt(Seiten.Titel(id) + " bis erster Eintrag", seite.InhaltMs);
            }
        }
        if (ansicht.GebauteSeiten.TryGetValue(Seiten.Notizblock, out var nbSeite) && nbSeite is NotizblockSeite notiz)
        {
            await notiz.Geladen;
            Messpunkt("Notizblock bis Liste", notiz.InhaltMs);
            await ansicht.ZeigeAsync(Seiten.Notizblock);
            await Bild("Notizblock Filter Status Offen mit allen Musterantworten (bis zum Bild)", () => notiz.Probe(0), () => notiz.Probe(-1));
            await Bild("Notizblock Suche \"netz\" (bis zum Bild)", () => notiz.Probe(2), () => { notiz.Probe(-1); notiz.Probe(0); });
            notiz.Probe(-1);
        }
        if (ansicht.GebauteSeiten.TryGetValue(Seiten.Ap2Szenarien, out var ap2) && ap2 is SzenarioSeite szenarien)
        {
            await ansicht.ZeigeAsync(Seiten.Ap2Szenarien);
            var liste = szenarien.Liste;
            Action ZurSeite(int seite) => () => liste.Blaettern(seite - liste.Seite);
            await Bild("AP2-Szenarien Liste blaettern (bis zum Bild)", () => liste.Blaettern(1), ZurSeite(liste.Seite));
            await Bild("AP2-Szenarien Liste letzte Seite (bis zum Bild)", () => szenarien.Probe(1), ZurSeite(liste.Seite));
            await Bild("AP2-Szenarien Szenario wechseln (bis zum Bild)", () => szenarien.Oeffnen(200), () => szenarien.Probe(-1));
            await Bild("AP2-Szenarien Musterloesung aufdecken, laengste (ohne Speichern, bis zum Bild)", () => szenarien.Probe(0), () => szenarien.Oeffnen(200));
            szenarien.Probe(-1);
        }
        if (inhalte is not null)
        {
            // 100 000 Antworten ueber alle fuenf Quellen, feste Zufallsfolge
            var zufall = new Core.Zufall.PythonZufall(100001);
            var schluessel = inhalte.Karteikarten.Select(k => (Core.Lernen.Quellen.Karte, (object)k.Frage!))
                .Concat(inhalte.Quizfragen.Select(q => (Core.Lernen.Quellen.Quiz, (object)q.Frage!)))
                .Concat(inhalte.Ap1Szenarien.Select((_, i) => (Core.Lernen.Quellen.Ap1, (object)(long)i)))
                .Concat(inhalte.Ap2Szenarien.Select((_, i) => (Core.Lernen.Quellen.Ap2, (object)(long)i)))
                .Concat(inhalte.Testprojekte.Select((_, i) => (Core.Lernen.Quellen.Projekt, (object)(long)i))).ToList();
            var buch = new Core.Lernen.Antwortbuch();
            for (var i = 0; i < 100000; i++)
            {
                var (quelle, frage) = schluessel[(int)zufall.RandRange(schluessel.Count)];
                buch.Hinzufuegen(quelle, frage, "2026-10-" + (1 + (int)zufall.RandRange(20)).ToString("00", inv) + " 12:00:00", (int)zufall.RandRange(3) != 0);
            }
            var status = new Core.Lernen.StatusBuch(buch);
            var kern = new Core.Lernen.Notizblock(inhalte);
            var eintraege = kern.Eintraege(status);
            var reihe = await RechnungMessenAsync(() =>
            {
                var uhr = Stopwatch.StartNew();
                eintraege = kern.Eintraege(status);
                _ = kern.Uebersicht(status);
                return uhr.Elapsed.TotalMilliseconds;
            });
            funde += Ergebnis(string.Create(inv, $"Notizblock-Kern bei 100 000 Antworten: Eintraege und Uebersicht ({eintraege.Count} zu ueben)"), reihe);
            var alle = kern.Eintraege(status, Core.Lernen.FragenStatus.Alle);
            var uhrSuche = Stopwatch.StartNew();
            _ = kern.Suchen(alle, "netz");
            Messpunkt("Notizblock-Kern erste Suche ueber alle Fragen (baut den Suchtext)", uhrSuche.Elapsed.TotalMilliseconds);
            await Rechnung("Notizblock-Kern weitere Suche ueber alle Fragen", () => _ = kern.Suchen(alle, "subnetz"));
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
        var einzel = new List<string>();
        for (var wert = 0; wert <= 359; wert++)
        {
            var ziel = wert;
            async Task<double?> Schritt()
            {
                var uhr = Stopwatch.StartNew();
                regler.Value = ziel;
                await ansicht.BildFertigAsync();
                return uhr.Elapsed.TotalMilliseconds;
            }
            var erste = await Schritt();
            if (wert == 0)
            {
                // Schritt 1 traegt das erste Zeichnen: einmalig, streng (nicht wiederholt)
                zeiten.Add(erste!.Value);
                einzel.Add(Messwiederholung.Einmal(erste, Farbreglerregel.GrenzeErsterSchritt).Einzelwerte("F1") + " (nicht wiederholt)");
                continue;
            }
            // Weisung P4-12 B6: ueber der Grenze derselbe Schritt erneut (vorher Wert davor, nicht gemessen)
            var reihe = await Messwiederholung.MessenAsync(erste, Farbreglerregel.GrenzeWeitereSchritte, Schritt,
                async () => { regler.Value = ziel - 1; await ansicht.BildFertigAsync(); }, Task.Delay);
            zeiten.Add(reihe.Bester ?? double.PositiveInfinity);
            einzel.Add(reihe.Einzelwerte("F1"));
            if (reihe.Wiederholt)
            {
                Messwerte.Notiere(string.Create(inv, $"Farbregler Schritt {wert + 1} wiederholt: Einzelmessungen {reihe.Einzelwerte("F1")} ({(reihe.Rot ? "rot" : "gruen")})"));
            }
        }
        regler.Value = alt;
        await ansicht.BildFertigAsync();
        var weitere = zeiten.Skip(1).ToList();
        Messwerte.Notiere(string.Create(inv, $"Farbregler Neuzeichnen (Farbton Akzent 1, {zeiten.Count} Schritte bis zum Bild): erster Schritt {zeiten[0]:F1} ms (Grenze {Farbreglerregel.GrenzeErsterSchritt:F0} ms); ab Schritt 2: Mittel {weitere.Average():F1} ms, Median {weitere.Order().ElementAt(weitere.Count / 2):F1} ms, Hoechstwert {weitere.Max():F1} ms (Grenze {Farbreglerregel.GrenzeWeitereSchritte:F0} ms)"));
        var funde = Farbreglerregel.Funde(zeiten);
        foreach (var fund in funde)
        {
            // "Schritt n: ..." (Farbreglerregel), dazu alle Einzelmessungen dieses Schritts
            var nr = int.Parse(fund["Schritt ".Length..fund.IndexOf(':', StringComparison.Ordinal)], inv);
            Messwerte.Notiere("  FEHLER: Farbregler " + fund + "; Einzelmessungen " + einzel[nr - 1]);
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
                    await foreach (var name in W1Ansichten(ansicht))
                    {
                        foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                        {
                            layoutFunde.Add($"{welt} {name}: {fund}");
                        }
                    }
                    await foreach (var name in KlausurAnsichten(ansicht))
                    {
                        foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                        {
                            layoutFunde.Add($"{welt} {name}: {fund}");
                        }
                    }
                    await foreach (var name in ProjektAnsichten(ansicht))
                    {
                        foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                        {
                            layoutFunde.Add($"{welt} {name}: {fund}");
                        }
                    }
                    await foreach (var name in W4Ansichten(ansicht))
                    {
                        foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                        {
                            layoutFunde.Add($"{welt} {name}: {fund}");
                        }
                    }
                    await foreach (var name in P5Ansichten(ansicht))
                    {
                        foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                        {
                            layoutFunde.Add($"{welt} {name}: {fund}");
                        }
                    }
                    await foreach (var name in P5W2Ansichten(ansicht))
                    {
                        foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                        {
                            layoutFunde.Add($"{welt} {name}: {fund}");
                        }
                    }
                    await foreach (var name in P5W3Ansichten(ansicht))
                    {
                        foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                        {
                            layoutFunde.Add($"{welt} {name}: {fund}");
                        }
                    }
                    await foreach (var name in P6W2Ansichten(ansicht))
                    {
                        foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                        {
                            layoutFunde.Add($"{welt} {name}: {fund}");
                        }
                    }
                    await foreach (var name in P8W2Ansichten(ansicht))
                    {
                        foreach (var fund in Layoutpruefung.Pruefe(ansicht))
                        {
                            layoutFunde.Add($"{welt} {name}: {fund}");
                        }
                    }
                    await foreach (var name in P8W2bAnsichten(ansicht))
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
                    foreach (var (text, flaeche) in W2Paare.Concat(W3Paare).Concat(RechnerPaare).Concat(W1Paare).Concat(KlausurPaare).Concat(ProjektPaare).Concat(W4Paare))
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
        Messwerte.Notiere(string.Create(inv, $"Farbwelten mit den Ansichten der Wellen 2 und 3, der Rechner-Seite, der Lernseiten (Phase 4, W1), der Klausursimulation (Phase 4, W2), des Abschlussprojekts (Phase 4, W3) und der Hilfe mit Rundgang (Phase 4, W4) geprueft: {anzahl}"));
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
