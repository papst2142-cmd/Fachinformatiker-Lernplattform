using System.Text.Json;
using System.Text.RegularExpressions;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L11 (Sammelliste 13, Wortliste W5, Weisung P2-14): PC und Handy zeigen dieselben
/// Woerter. P1: jedes Textpaar aus <see cref="Geraetetexte"/> ist gleich oder steht in der
/// festen Ausnahmetabelle. P2: Abstaende und Umbrueche zaehlen nicht. P3: keine tote Ausnahme.
/// A1: in der App waehlt keine Ansicht einen Anzeigetext am Geraet vorbei an Geraetetexte.
/// Kontrollfehler: ein Handy-Text weicht ab bzw. wird in einer Ansicht direkt gewaehlt.
/// </summary>
public partial class GeraetetexteTests
{
    private static readonly JsonElement G13 = LadeG13();

    private static JsonElement LadeG13()
    {
        using var f = File.OpenRead(Pfade.Vergleich("dashboard.json"));
        return JsonDocument.Parse(f).RootElement.Clone();
    }

    /// <summary>P2: Folgen von Leerzeichen, Zeilenumbruch und U+00A0 zaehlen als ein Leerzeichen; U+200B (Umbruchpunkt) zaehlt nicht.</summary>
    internal static string Einheitlich(string text) => Leerraum().Replace(text.Replace("\u200B", "", StringComparison.Ordinal), " ").Trim();

    [GeneratedRegex("[ \n\u00A0]+")]
    private static partial Regex Leerraum();

    private static string S(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private static IEnumerable<DashboardWerteAuszug> DashboardFaelle()
    {
        foreach (var fall in G13.GetProperty("faelle").EnumerateArray())
        {
            var w = fall.GetProperty("werte");
            var b = w.GetProperty("banner");
            var banner = new DashboardBanner(S(b, "titel"), S(b, "text_pc"), S(b, "text_handy"), S(b, "gross"), S(b, "gross_unter"));
            var aktivitaeten = w.GetProperty("aktivitaeten").EnumerateArray().Select(a => new DashboardAktivitaet(
                S(a, "zeit"), S(a, "art"), S(a, "detail"), S(a, "extra"), S(a, "pc1"), S(a, "pc2"), S(a, "handy1"), S(a, "handy2"))).ToList();
            var fach = w.GetProperty("fachbereiche").EnumerateArray().Select(f => new DashboardFachbereich(
                S(f, "name"), S(f, "kurz"), f.GetProperty("abdeckung").GetDouble(), S(f, "ring_text"), S(f, "ring_text_handy"),
                f.GetProperty("antworten").GetInt64(), f.GetProperty("richtig").GetDouble(), S(f, "detail"))).ToList();
            yield return new DashboardWerteAuszug(S(fall, "name"), banner, aktivitaeten, fach);
        }
        // B8 (nur bei kaputten Daten verschieden): Abdeckung 120 % wie DashboardWerte.Fachbereiche rechnet
        yield return new DashboardWerteAuszug("kaputt_abdeckung_120", null, [],
            [new DashboardFachbereich("Netzwerk & Protokolle", "Netzwerk", 120.0, DashboardTexte.Prozent(Math.Max(0.0, Math.Min(100.0, 120.0)), true),
                DashboardTexte.Prozent(120.0, true), 0, 0, "")]);
    }

    internal sealed record DashboardWerteAuszug(string Name, DashboardBanner? Banner, IReadOnlyList<DashboardAktivitaet> Aktivitaeten,
        IReadOnlyList<DashboardFachbereich> Fachbereiche);

    private static string Liste(IEnumerable<string> teile) => string.Join(" | ", teile);

    private static string Navigation(bool pc) =>
        Liste(Geraetetexte.Navigation(pc).SelectMany(e => e.Unterpunkte.Prepend(e)).Select(e => e.Titel));

    /// <summary>Alle Textpaare (Stelle, Fall, PC, Handy), die die Oberflaeche ueber Geraetetexte zeigt.</summary>
    internal static List<(string Stelle, string Fall, string Pc, string Handy)> Paare()
    {
        var paare = new List<(string, string, string, string)>
        {
            ("Navigation", "-", Navigation(true), Navigation(false)),
            ("Kopfzeilen", "-", Liste(Geraetetexte.Kopfzeilen(true).Select(k => k.Id + ":" + k.Bereich + ">" + k.Seite)),
                Liste(Geraetetexte.Kopfzeilen(false).Select(k => k.Id + ":" + k.Bereich + ">" + k.Seite))),
            ("Dashboard.AktivitaetenLeer", "-", Geraetetexte.AktivitaetenLeer(true), Geraetetexte.AktivitaetenLeer(false)),
            ("Optionen.Bereiche", "-", Liste(Geraetetexte.OptionenBereiche(true).Select(b => b.Titel)),
                Liste(Geraetetexte.OptionenBereiche(false).Select(b => b.Titel))),
            ("Tagesziel.Hinweis", "-", Geraetetexte.TageszielHinweis(true), Geraetetexte.TageszielHinweis(false)),
            ("Diagnose.Knoepfe", "-", Liste(Geraetetexte.DiagnoseKnoepfe(true)), Liste(Geraetetexte.DiagnoseKnoepfe(false))),
            ("Uebernahme.Dateifilter", "-", Liste(Geraetetexte.UebernahmeDateifilter(true).Select(f => f.Name + " " + f.Muster)),
                Liste(Geraetetexte.UebernahmeDateifilter(false).Select(f => f.Name + " " + f.Muster))),
            ("Uebernahme.KopieName", "-", Geraetetexte.KopieName("vor_uebernahme_2026-10-21_0612.db", true),
                Geraetetexte.KopieName("vor_uebernahme_2026-10-21_0612.db", false)),
            ("Abgleich.Geraet", "-", Geraetetexte.Geraet(true, "Rechner"), Geraetetexte.Geraet(false, "Rechner")),
            ("Pruefung.StartFrage", "-", Geraetetexte.PruefungStartFrage(true), Geraetetexte.PruefungStartFrage(false)),
            ("Rundgang.Tasten", "-", Geraetetexte.RundgangTasten(true) ?? "", Geraetetexte.RundgangTasten(false) ?? ""),
        };
        foreach (var installierbar in new[] { true, false })
        {
            paare.Add(("Update.Knopf", "installierbar " + installierbar, Geraetetexte.UpdateKnopf(installierbar, true), Geraetetexte.UpdateKnopf(installierbar, false)));
            foreach (var mitDatei in new[] { true, false })
            {
                paare.Add(("Update.Hinweis", $"installierbar {installierbar}, Datei {mitDatei}", Geraetetexte.UpdateHinweis(installierbar, mitDatei, true),
                    Geraetetexte.UpdateHinweis(installierbar, mitDatei, false)));
            }
        }
        foreach (var bereich in Optionen.AlleFuer(true))
        {
            paare.Add(("Optionen.Untertitel", bereich.Id, Geraetetexte.OptionenUntertitel(bereich, true) ?? "",
                Geraetetexte.OptionenUntertitel(bereich, false) ?? ""));
            paare.Add(("Optionen.Abschnitte", bereich.Id, Liste(Geraetetexte.OptionenAbschnitte(bereich, true).Select(a => a.Titel)),
                Liste(Geraetetexte.OptionenAbschnitte(bereich, false).Select(a => a.Titel))));
            paare.Add(("Optionen.Suchwoerter", bereich.Id, Liste(Geraetetexte.OptionenSuchwoerter(bereich, true)),
                Liste(Geraetetexte.OptionenSuchwoerter(bereich, false))));
        }
        foreach (var fall in DashboardFaelle())
        {
            if (fall.Banner is { } banner)
            {
                paare.Add(("Dashboard.Banner", fall.Name, Geraetetexte.Banner(banner, true), Geraetetexte.Banner(banner, false)));
            }
            foreach (var a in fall.Aktivitaeten)
            {
                paare.Add(("Dashboard.Aktivitaet1", fall.Name, Geraetetexte.AktivitaetZeile1(a, true), Geraetetexte.AktivitaetZeile1(a, false)));
                paare.Add(("Dashboard.Aktivitaet2", fall.Name, Geraetetexte.AktivitaetZeile2(a, true), Geraetetexte.AktivitaetZeile2(a, false)));
            }
            foreach (var f in fall.Fachbereiche)
            {
                paare.Add(("Dashboard.Ring", fall.Name + "/" + f.Kurz, Geraetetexte.Ring(f, true), Geraetetexte.Ring(f, false)));
            }
        }
        return paare;
    }

    /// <summary>Unterschiede, die nicht in der Ausnahmetabelle stehen.</summary>
    internal static List<string> Verstoesse(IEnumerable<(string Stelle, string Fall, string Pc, string Handy)> paare,
        IReadOnlyList<GeraeteAusnahme> ausnahmen)
    {
        var erlaubt = ausnahmen.Select(a => a.Stelle).ToHashSet(StringComparer.Ordinal);
        return [.. paare.Where(p => Einheitlich(p.Pc) != Einheitlich(p.Handy) && !erlaubt.Contains(p.Stelle))
            .Select(p => $"{p.Stelle} ({p.Fall}): PC \"{p.Pc}\" / Handy \"{p.Handy}\"")];
    }

    [Fact]
    public void NurErlaubteUnterschiede()
    {
        var verstoesse = Verstoesse(Paare(), Geraetetexte.Ausnahmen);
        Assert.True(verstoesse.Count == 0, string.Join("\n", verstoesse));
    }

    [Fact]
    public void KeineToteAusnahme()
    {
        var paare = Paare();
        foreach (var ausnahme in Geraetetexte.Ausnahmen)
        {
            Assert.True(paare.Any(p => p.Stelle == ausnahme.Stelle && Einheitlich(p.Pc) != Einheitlich(p.Handy)),
                $"{ausnahme.Nr} {ausnahme.Stelle}: kein Unterschied mehr");
        }
        Assert.Equal(["B1", "B3", "B4", "B5", "B8", "B9", "B10", "B11", "B12", "B13", "B14"], Geraetetexte.Ausnahmen.Select(a => a.Nr).Distinct());
    }

    [Fact]
    public void WortlisteT1BisT3()
    {
        var aktivitaeten = DashboardFaelle().SelectMany(f => f.Aktivitaeten).ToList();
        Assert.NotEmpty(aktivitaeten);
        foreach (var a in aktivitaeten)
        {
            // T1: deutsches Datum "20.10.2026 06:40  ·  richtig" auf beiden Geraeten
            Assert.Matches(@"^\d\d\.\d\d\.\d{4} \d\d:\d\d  ·  ", Geraetetexte.AktivitaetZeile2(a, true));
            // T2: Detail ungekuerzt
            Assert.Equal(a.Art + " · " + a.Detail, Geraetetexte.AktivitaetZeile1(a, true));
        }
        Assert.Contains(aktivitaeten, a => a.Pc1 != a.Handy1);   // die Kuerzung gab es in den Daten wirklich
        // T3
        var abgleich = Optionen.Bereiche.Single(b => b.Id == Optionen.Abgleich);
        Assert.Equal("über ein privates GitHub-Repository", Geraetetexte.OptionenUntertitel(abgleich, false));
        Assert.Equal("über ein privates GitHub-Repository", Geraetetexte.OptionenUntertitel(abgleich, true));
    }

    [Fact]
    public void Kontrollfehler_HandyTextWeichtAb()
    {
        var abgleich = Optionen.Bereiche.Single(b => b.Id == Optionen.Abgleich);
        var paare = Paare();
        paare.Add(("Optionen.Untertitel", abgleich.Id, abgleich.UntertitelPc!, abgleich.UntertitelHandy!));
        Assert.Single(Verstoesse(paare, Geraetetexte.Ausnahmen));
        // Abstand allein ist kein Unterschied (B6/B7), ein anderes Wort schon
        Assert.Empty(Verstoesse([("X", "-", "a   ·   b", "a  ·\u00A0b")], []));
        Assert.Single(Verstoesse([("X", "-", "a · b", "a - b")], []));
    }

    [Fact]
    public void SuchwoerterWieInDerSuche()
    {
        foreach (var bereich in Optionen.AlleFuer(true))
        {
            foreach (var pc in new[] { true, false })
            {
                Assert.Equal(bereich.StichwoerterFuer(pc), Geraetetexte.OptionenSuchwoerter(bereich, pc));
            }
        }
    }

    // -- A1: Quelltext der App ------------------------------------------------

    /// <summary>
    /// Erlaubte Geraete- oder Aufbauwahl in der App ausserhalb von Geraetetexte (Datei, Zeile ohne
    /// Einrueckung, Grund). Jede andere Zeile mit IsAndroid() oder dem Schalter Handy/Android/Pc
    /// oder einem *Handy/*Pc-Mitglied macht den Test rot.
    /// </summary>
    internal static readonly IReadOnlyList<(string Datei, string Zeile, string Grund)> Erlaubt =
    [
        ("App.axaml.cs", "if (OperatingSystem.IsAndroid() && this.TryGetFeature<IActivatableLifetime>() is { } leben)", "Technik: Android-Lebenszyklus (Verlassen/Zurueckholen)"),
        ("Messung/AutoMessung.cs", "var ids = Texte.NavigationPc.SelectMany(p => p.Unter.Prepend(p)).Select(p => p.Id)", "Messung ueber alle Seiten, keine Anzeige"),
        ("Messung/AutoMessung.cs", ".Concat(Texte.NavigationHandy.Select(p => p.Id))", "Messung ueber alle Seiten, keine Anzeige"),
        ("Programm/AbgleichDienst.cs", "private static readonly bool Android = OperatingSystem.IsAndroid();", "Schalter"),
        ("Programm/AbgleichDienst.cs", "if (Android)", "Technik: Rechnername nur am PC lesen; Text aus Geraetetexte.Geraet (B11)"),
        ("Programm/AbgleichDienst.cs", "Android ? AbgleichSteuerung.Handy : AbgleichSteuerung.Pc,", "Verhalten: Ausloeser und Zeiten des Abgleichs (L10, P2-12)"),
        ("Views/DashboardSeite.cs", "private static readonly bool Pc = !OperatingSystem.IsAndroid();", "Schalter"),
        ("Views/DashboardSeite.cs", "if (Pc || w.Faellig.Wiederholen)", "Verhalten: \"Jetzt wiederholen\" am Handy nur mit Faelligem (R3, P2-6)"),
        ("Views/Navigationsleisten.cs", "foreach (var punkt in Texte.NavigationPc)", "Aufbau: Seitenleiste (B1, B5)"),
        ("Views/Navigationsleisten.cs", "var raster = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(\",\", Enumerable.Repeat(\"*\", Texte.NavigationHandy.Count))) };", "Aufbau: untere Leiste (B1, B5)"),
        ("Views/Navigationsleisten.cs", "foreach (var punkt in Texte.NavigationHandy)", "Aufbau: untere Leiste (B1, B5)"),
        ("Views/Navigationsleisten.cs", "var punkt = Texte.NavigationPc.First(p => p.Id == eltern);", "Aufbau: Unterpunkt der Seitenleiste markieren"),
        ("Views/KlausurTeil.cs", "private static readonly bool Pc = !OperatingSystem.IsAndroid();", "Schalter (B12)"),
        ("Views/AbschlussprojektSeite.cs", "private static readonly bool Handy = OperatingSystem.IsAndroid();", "Schalter"),
        ("Views/AbschlussprojektSeite.cs", "var daten = _a.ExportDaten(endung, Handy, DateTime.Now);", "Technik: Textdatei PC CRLF mit BOM, Handy LF ohne BOM (app_gui 4736, main.py 2388), kein Anzeigetext"),
        ("Views/OptionenSeite.cs", "private static readonly bool Pc = !OperatingSystem.IsAndroid();", "Schalter"),
        ("Views/OptionenSeite.cs", "if (!Pc) // Termin beim Tippen", "Verhalten: Termin am Handy beim Tippen speichern, sobald gueltig (main.py 4484-4490, W4)"),
        ("Views/RundgangEbene.cs", "private static readonly bool Pc = !OperatingSystem.IsAndroid();", "Schalter"),
        ("Views/RundgangEbene.cs", "var reihe = new KnopfReihe { EineZeile = Pc, Margin = new Thickness(0, 10, 0, 0) };", "Aufbau: Knopfzeile PC eine Zeile, wenn sie passt, sonst wie Handy Hauptknopf oben (main.py 5059-5066, W4)"),
        ("Views/EinspielenTeil.cs", "private static readonly bool Android = OperatingSystem.IsAndroid();", "Schalter (Phase 5 BE3; nur als Argument fuer Geraetetexte)"),
        ("Views/RuecksicherungTeil.cs", "private static readonly bool Android = OperatingSystem.IsAndroid();", "Schalter"),
        ("Views/RuecksicherungTeil.cs", "if (!Android && pfad is not null)", "Technik: Speicherweg (Android ueber Datenstrom)"),
        ("Views/Seiten.cs", "foreach (var punkt in Texte.NavigationPc.SelectMany(p => p.Unter.Prepend(p)))", "Seitentitel: PC-Wortlaut auf beiden Geraeten"),
        ("Views/Seiten.cs", "Lernen => Texte.NavigationHandy.First(p => p.Id == Lernen).Titel,", "Seitentitel der Sammelseite \"Lernen\" (nur schmaler Aufbau, B5)"),
        ("Views/Seiten.cs", "Suche => Navigation.SucheHandy,", "Seitentitel \"Suchen\" auf beiden Geraeten gleich"),
        ("Views/SucheSeite.cs", "private static readonly bool Pc = !OperatingSystem.IsAndroid();", "Schalter"),
        ("Views/SucheSeite.cs", "ergebnis = await Task.Run(() => GlobaleSuche.Suchen(text, anfrage, Pc, abbruch.Token), abbruch.Token);", "B4: Suchwoerter (Optionen.StichwoerterFuer = Geraetetexte.OptionenSuchwoerter, Test SuchwoerterWieInDerSuche)"),
        ("Views/SucheSeite.cs", "ergebnis ??= new SuchErgebnis(anfrage, GlobaleSuche.SchnelleTreffer(anfrage, Pc));", "B4: wie oben (Hilfe und Optionen, Diff A)"),
        ("Views/Texte.cs", "public static IReadOnlyList<NavPunkt> NavigationPc { get; } = Navigation.Pc.Select(Punkt).ToList();", "Daten der Seitenleiste"),
        ("Views/Texte.cs", "public static IReadOnlyList<NavPunkt> NavigationHandy { get; } = Navigation.Handy.Select(Punkt).ToList();", "Daten der unteren Leiste"),
        ("Views/UebernahmeTeil.cs", "private static readonly bool Android = OperatingSystem.IsAndroid();", "Schalter"),
        // Phase 5 W2 (BE6 bis BE8)
        ("Programm/Diagnosedienst.cs", "private static readonly bool Pc = !OperatingSystem.IsAndroid();", "Schalter"),
        ("Programm/Diagnosedienst.cs", "var messung = new Leistungsmessung(Update.Anzeige, Pc ? \"PC\" : \"Handy\", pfad, aktiv, Pc ? Toolkit() : \"\");", "Daten: Spalten geraet und toolkit der Messdatei wie Recorder(APP_VERSION, \"PC\"/\"Handy\") (G39), kein Anzeigetext"),
        ("Programm/Diagnosedienst.cs", "if (Pc)", "Verhalten: Spalte vorgang nur am PC (Haenger-Diagnose nur PC, app_gui 8481); Zeile groesse PC wie PerfMonitor, Handy wie MobilePerf"),
        ("Programm/Diagnosedienst.cs", "Messung.Aufzeichnen(Leistungsmessung.EreignisSeite, von, nach, ms, elemente: Elemente(), messart: Pc ? Leistungsmessung.MessartGezeichnet : null);", "Daten: messart 2 nur am PC (PerfMonitor.page), Handy leer (MobilePerf.page), kein Anzeigetext"),
        ("Views/DiagnoseTeil.cs", "private static readonly bool Pc = !OperatingSystem.IsAndroid();", "Schalter"),
        ("Views/DiagnoseTeil.cs", "foreach (var text in DiagnoseTexte.Knoepfe(Pc))", "Verhalten: \"Datenordner öffnen\" nur am PC (app_gui 7624, main.py 4108); Liste in DiagnoseTexte (Phase 1)"),
        ("Views/DiagnoseTeil.cs", "Problembericht.Bauen(Zustand.Lernstand, Update.Anzeige, Pc ? \"PC\" : \"Handy\", Zustand.Werte, DateTime.Now);", "Daten: Geraet im Bericht wie build_report(db, APP_VERSION, \"PC\"/\"Handy\") (G38)"),
        ("Views/DiagnoseTeil.cs", "if (Pc && pfad is not null)", "Technik: Speicherweg (Handy ueber Datenstrom mit Zuruecklesen)"),
        ("Views/DiagnoseTeil.cs", "if (Pc)", "Verhalten: Messdatei zeigen PC Datenordner, Handy Datei speichern (app_gui 8566, main.py 4585)"),
        // Phase 5 W3 (BE10); Texte des Fensters ueber Geraetetexte.UpdateKnopf/UpdateHinweis (B14)
        ("Programm/UpdateDienst.cs", "public static readonly bool Pc = !OperatingSystem.IsAndroid();", "Schalter"),
        ("Programm/UpdateDienst.cs", "if (Pc)", "Verhalten: PC prueft nach 1 s das letzte Update und sucht nach 3 s (app_gui 8675), Handy sucht nach 3 s (main.py 6077)"),
        ("Programm/UpdateDienst.cs", "if (!Pc)", "Verhalten: Handy sucht automatisch hoechstens alle 30 Minuten (main.py AUTO_INTERVAL)"),
        ("Programm/UpdateDienst.cs", "_ = SuchenAsync(false, verzoegerung, nochmal: !Pc);", "Verhalten: Handy versucht es nach einem Netzfehler beim Start nach 30 s noch einmal (main.py AUTO_RETRY)"),
        ("Programm/UpdateDienst.cs", "if (!vonHand && !Pc)", "Verhalten: Zeitpunkt der letzten automatischen Suche nur am Handy (main.py check_updates)"),
        ("Views/UpdateFenster.cs", "private readonly bool Pc;", "Schalter"),
        ("Views/UpdateFenster.cs", "Pc = pc ?? UpdateDienst.Pc;", "Schalter (der Start ohne Bedienung zeigt auch die Ansicht des Handys)"),
        ("Views/UpdateFenster.cs", "var farbe = Pc ? \"Pinsel.text_dim\" : \"Pinsel.accent\";", "Optik: Hinweiszeile am Handy in accent (main.py 6158), PC text_dim, kein Text"),
        ("Views/UpdateFenster.cs", "if (!Pc)", "Verhalten: \"Später\" gilt am Handy bis zum naechsten Start (main.py dismissed_version)"),
        ("Views/UpdateFenster.cs", "return Pc;", "Verhalten: PC-Fenster bleibt nach \"Zur Download-Seite\" offen (app_gui 9416), Handy schliesst nach \"Herunterladen\" (main.py 6163)"),
    ];

    [GeneratedRegex(@"IsAndroid\(\)|(?<![\w.])(Handy|Android|Pc)\b(?!\s*\()|\.\w*(Handy|Pc)\d*\b")]
    private static partial Regex Geraetewahl();

    [GeneratedRegex(@"Geraetetexte\.\w+\(")]
    private static partial Regex UeberGeraetetexte();

    [GeneratedRegex("\"(?:[^\"\\\\]|\\\\.)*\"")]
    private static partial Regex Zeichenkette();

    /// <summary>Zeilen mit Geraetewahl, die weder ueber Geraetetexte laufen noch erlaubt sind.</summary>
    internal static List<string> Quelltextfunde(IEnumerable<(string Datei, string Text)> dateien)
    {
        var erlaubt = Erlaubt.Select(e => (e.Datei, e.Zeile)).ToHashSet();
        var funde = new List<string>();
        foreach (var (datei, text) in dateien)
        {
            var nr = 0;
            foreach (var roh in text.Split('\n'))
            {
                nr++;
                var zeile = roh.Trim();
                if (zeile.StartsWith("//", StringComparison.Ordinal) || zeile.StartsWith('*'))
                {
                    continue;
                }
                var code = Zeichenkette().Replace(zeile, "\"\"");
                var kommentar = code.IndexOf("//", StringComparison.Ordinal);
                if (kommentar >= 0)
                {
                    code = code[..kommentar];
                }
                if (!Geraetewahl().IsMatch(code) || erlaubt.Contains((datei, zeile)))
                {
                    continue;
                }
                // Erlaubt: der Schalter geht nur in Geraetetexte, sonst steht kein *Handy/*Pc-Mitglied in der Zeile
                var ohneSchalterArgument = Regex.Replace(code, @"(?<=[(,])\s*!?(Pc|Android|Handy)\s*(?=[),])", "");
                if (UeberGeraetetexte().IsMatch(code) && !Geraetewahl().IsMatch(ohneSchalterArgument))
                {
                    continue;
                }
                funde.Add($"{datei}:{nr}: {zeile}");
            }
        }
        return funde;
    }

    private static string AppOrdner()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !File.Exists(Path.Combine(ordner.FullName, "global.json")))
        {
            ordner = ordner.Parent;
        }
        Assert.NotNull(ordner);
        return Path.Combine(ordner.FullName, "src", "Lernplattform.App");
    }

    private static IEnumerable<(string, string)> AppDateien()
    {
        var wurzel = AppOrdner();
        foreach (var pfad in Directory.EnumerateFiles(wurzel, "*.cs", SearchOption.AllDirectories)
                     .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                                 && !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            yield return (Path.GetRelativePath(wurzel, pfad).Replace('\\', '/'), File.ReadAllText(pfad).Replace("\r\n", "\n"));
        }
    }

    [Fact]
    public void AppWaehltKeinenTextAmGeraet()
    {
        var dateien = AppDateien().ToList();
        Assert.True(dateien.Count > 20);
        var funde = Quelltextfunde(dateien);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        // Jeder erlaubte Eintrag steht noch im Quelltext (keine tote Erlaubnis)
        foreach (var (datei, zeile, _) in Erlaubt)
        {
            Assert.Contains(dateien, d => d.Item1 == datei && d.Item2.Split('\n').Any(z => z.Trim() == zeile));
        }
    }

    [Fact]
    public void Kontrollfehler_HandyTextInDerAnsicht()
    {
        var dashboard = AppDateien().Single(d => d.Item1 == "Views/DashboardSeite.cs");
        var falsch = dashboard.Item2.Replace("Schrift.Text(Geraetetexte.AktivitaetZeile1(a, Pc)),", "Schrift.Text(Pc ? a.Pc1 : a.Handy1),", StringComparison.Ordinal);
        Assert.NotEqual(dashboard.Item2, falsch);
        Assert.Single(Quelltextfunde([(dashboard.Item1, falsch)]));
        Assert.Single(Quelltextfunde([("Views/X.cs", "var t = OperatingSystem.IsAndroid() ? \"Start\" : \"Dashboard\";")]));
        Assert.Single(Quelltextfunde([("Views/X.cs", "Schrift.Text(bereich.UntertitelHandy);")]));
        Assert.Empty(Quelltextfunde([("Views/X.cs", "Schrift.Text(Geraetetexte.Banner(w.Banner, Pc));")]));
        // Weisung P4-16 b: der Erlaubt-Eintrag gilt nur fuer die Zeile mit dem Kommentar
        // "Termin beim Tippen"; ein weiteres "if (!Pc)" in derselben Datei ist ein Fund
        Assert.Empty(Quelltextfunde([("Views/OptionenSeite.cs", "            if (!Pc) // Termin beim Tippen")]));
        Assert.Single(Quelltextfunde([("Views/OptionenSeite.cs", "            if (!Pc)")]));
        Assert.Single(Quelltextfunde([("Views/OptionenSeite.cs", "            if (!Pc) // anderer Grund")]));
    }
}
