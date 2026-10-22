using System.Reflection;
using Lernplattform.Core.Projekt;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W3: Texte der Seite "Abschlussprojekt" gegen eine feste Tabelle nach der freigegebenen
/// Wortliste W3 (Anlage P4-8_Wortliste_W3.md, Nr 1 bis 20, 28; Stundentexte mit %d je Ausbildung,
/// Weisung P4-8 Frage 1 b). Jede Konstante von <see cref="ProjektTexte"/> steht genau einmal in der
/// Tabelle. Dazu "nur Einfachleerzeichen" mit der festen Ausnahmeliste Nr 30.
/// </summary>
public class ProjektTexteTests
{
    private static readonly Dictionary<string, string> Wortliste = new(StringComparer.Ordinal)
    {
        // Nr 1
        ["Projekt"] = "Projekt", ["NeuesProjekt"] = "Neues Projekt", ["Loeschen"] = "Löschen", ["AlsPdf"] = "Als PDF", ["AlsText"] = "Als Text",
        // Nr 3, 4
        ["Eckdaten"] = "Eckdaten", ["EckdatenUntertitel"] = "Fristen und Seitenzahl legt deine IHK fest",
        ["Fachbereich"] = "Fachbereich (für das Fachgespräch)", ["KeinFachbereich"] = "Kein Fachbereich",
        // Nr 5
        ["Stand"] = "Stand", ["StandUntertitel"] = "Vom Antrag bis zum Fachgespräch", ["Erledigt"] = "%d von %d Schritten erledigt",
        ["AntragOffen"] = "Im Antrag noch offen: %s", ["AntragVoll"] = "Alle Felder des Antrags sind ausgefüllt.",
        // Nr 6, 7
        ["Projektnote"] = "Geschätzte Projektnote", ["ProjektnoteUntertitel"] = "Fließt ins Gesamtergebnis unter Prüfungstrainer ein",
        ["ProjektnoteHinweis"] = "Die Projektarbeit zählt 50 % der Abschlussprüfung. Trage hier ein, wie viele Punkte (0 bis 100) du für "
            + "Dokumentation, Präsentation und Fachgespräch zusammen erwartest.",
        ["ErwartetePunkte"] = "Erwartete Punkte (0-100)",
        // Nr 8
        ["Vorlage"] = "Testprojekt als Vorlage", ["VorlageUntertitel"] = "Füllt nur leere Felder",
        ["VorlageHinweis"] = "Übernimmt Titel, Ausgangssituation, Auftrag und Rahmenbedingungen eines Testprojekts als Anregung. "
            + "Bereits ausgefüllte Felder bleiben unverändert.",
        ["Uebernehmen"] = "Übernehmen",
        // Nr 9
        ["Antrag"] = "Projektantrag", ["AntragUntertitel"] = "Gliederung wie bei den IHK-Vorlagen", ["AntragPruefen"] = "Prüfe vor dem Einreichen",
        // Nr 10 (%d je Ausbildung; die zwei fuehrenden Leerzeichen sind Abstand, Nr 30)
        ["Zeitplanung"] = "Zeitplanung", ["ZeitplanUntertitel"] = "Höchstens %d Stunden inklusive Dokumentation",
        ["WeiterePhase"] = "(weitere Phase, frei benennbar)", ["ZeitplanSumme"] = "Summe: %s von %d Stunden",
        // Nr 11
        ["Kostenrechnung"] = "Kostenrechnung", ["KostenUntertitel"] = "Stunden kommen aus der Zeitplanung", ["Sachkosten"] = "Sachkosten",
        ["SachkostenUntertitel"] = "Hardware, Lizenzen, Material", ["Bezeichnung"] = "BEZEICHNUNG", ["Betrag"] = "BETRAG", ["Euro"] = "€",
        ["Ergebnis"] = "Ergebnis",
        // Nr 12
        ["Dokumentation"] = "Dokumentation", ["DokuUntertitel"] = "Kapitelgerüst nach üblicher IHK-Gliederung",
        ["Seitenvorgabe"] = "Seitenvorgabe deiner IHK",
        ["SeitenHinweis"] = "Grobe Schätzung: etwa %d Zeichen pro Seite (11 pt, 1,5-zeilig). Deckblatt, Verzeichnisse und Anhang zählen meist nicht mit.",
        ["Seiten"] = "Etwa %s von %d Seiten",
        // Nr 13, 14
        ["Praesentation"] = "Präsentation", ["PraesentationUntertitel"] = "Höchstens 15 Minuten, danach das Fachgespräch",
        ["FachgespraechUeben"] = "Fachgespräch üben", ["FragenUntertitel"] = "%d Fragen · allgemeine plus die deines Fachbereichs",
        ["FragenStand"] = "Frage %d von %d · %d gewusst · %d nicht gewusst · %d offen", ["Gewusst"] = "Gewusst",
        ["NichtGewusst"] = "Nicht gewusst", ["NichtBewertet"] = "Noch nicht bewertet", ["HinweisZeigen"] = "Hinweis zeigen",
        ["HinweisAusblenden"] = "Hinweis ausblenden", ["NaechsteFrage"] = "Nächste Frage", ["WusstestDu"] = "Wusstest du es?",
        // Nr 15
        ["DialogTitel"] = "Abschlussprojekt speichern", ["PdfTyp"] = "PDF-Dokument", ["TextTyp"] = "Textdatei",
        // Nr 16
        ["Gespeichert"] = "Gespeichert: %s", ["VorlageUebernommen"] = "%s aus dem Testprojekt übernommen", ["Feld"] = "Feld",
        ["Felder"] = "Felder", ["VorlageVoll"] = "Alle passenden Felder waren schon ausgefüllt",
        // Nr 17
        ["AlsVorlage"] = "Als Vorlage fürs Abschlussprojekt",
        // Nr 28 (NEU)
        ["SpeichernFehlgeschlagen"] = "Speichern fehlgeschlagen", ["DateiNichtGespeichert"] = "Die Datei konnte nicht gespeichert werden.",
        // Nr 18 (Kern; Exportsumme und Pruefliste mit %d je Ausbildung)
        ["OhneTitel"] = "Projekt ohne Titel", ["Position"] = "Position %d", ["Weitere"] = "Weitere Phase", ["NochOffen"] = "(noch offen)",
        ["Checkliste"] = "Checkliste", ["ExportSumme"] = "Summe: %s (höchstens %d Stunden)", ["ExportFeld"] = "%s: %s",
        ["Haken"] = "[%s] %s", ["Stunden"] = " Std.", ["Dateiname"] = "Abschlussprojekt_%s.%s", ["DateinameVorgabe"] = "Projekt",
        ["WarnungSumme"] = "Die Summe von %s liegt über den erlaubten %d Stunden.",
        ["WarnungDoku"] = "Die Dokumentation nimmt %d %% der Zeit ein (üblich höchstens 15 bis 20 %%).",
        ["KostenStunden"] = "Projektstunden laut Zeitplan: %s", ["KostenPersonal"] = "Personalkosten: %s", ["KostenSach"] = "Sachkosten: %s",
        ["KostenGesamt"] = "Projektkosten gesamt: %s", ["KeineAmortisation"] = "Amortisation: keine Einsparung eingetragen",
        ["Amortisation"] = "Amortisation nach %s Monaten (%s Einsparung netto pro Monat)",
        ["ChecklisteStunden"] = "Summe höchstens %d Stunden inklusive Dokumentation",
        // Nr 19, 20
        ["RueckfrageTitel"] = "Projekt löschen",
        ["RueckfrageAbgleich"] = "„%s“ löschen? Das Projekt und alle seine Eingaben werden gelöscht, nach dem Abgleich auch auf deinen "
            + "anderen Geräten. Das lässt sich in der App nicht rückgängig machen.\n\nHinweis: Ältere Sicherungsdateien und frühere Stände "
            + "im Verlauf des Abgleich-Repositorys auf GitHub enthalten die Eingaben weiterhin.",
        ["RueckfrageLokal"] = "„%s“ löschen? Das Projekt und alle seine Eingaben werden gelöscht. Das lässt sich in der App nicht "
            + "rückgängig machen.\n\nHinweis: Ältere Sicherungsdateien enthalten die Eingaben weiterhin. Falls du den Abgleich früher "
            + "genutzt hast, enthalten frühere Stände im Abgleich-Repository auf GitHub die Eingaben weiterhin.",
    };

    private static IEnumerable<(string Name, string Text)> Konstanten() =>
        typeof(ProjektTexte).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetRawConstantValue()!));

    [Fact]
    public void JedeKonstanteWieDieWortliste()
    {
        var ist = Konstanten().ToList();
        Assert.Equal(Wortliste.Keys.Order(StringComparer.Ordinal), ist.Select(k => k.Name).Order(StringComparer.Ordinal));
        foreach (var (name, text) in ist)
        {
            Assert.True(Wortliste[name] == text, name + ": \"" + text + "\"");
        }
    }

    /// <summary>
    /// Nr 30: nur Einfachleerzeichen in Seite, Kern und Export, ausser der festen Ausnahmeliste
    /// (a) PDF-Fusszeile "%s  ·  Seite %d von %d  ·  %s", (b) Listenzeile "  - %s" im Textexport,
    /// (c) geschuetztes Leerzeichen in euro ("1.234,50 €").
    /// </summary>
    [Fact]
    public void NurEinfachleerzeichen()
    {
        static void Pruefe(string wo, string text)
        {
            Assert.False(text.Contains("  ", StringComparison.Ordinal), wo + ": Doppelleerzeichen in \"" + text + "\"");
            Assert.False(text.Contains(' '), wo + ": geschuetztes Leerzeichen in \"" + text + "\"");
            Assert.False(text.Contains('\t'), wo + ": Tabulator in \"" + text + "\"");
        }
        foreach (var (name, text) in Konstanten())
        {
            Pruefe(name, text);
        }
        foreach (var (s, n) in Abschlussprojekt.Reiter.Concat(Abschlussprojekt.Uebersicht))
        {
            Pruefe(s, n);
        }
        foreach (var f in Abschlussprojekt.Antrag.Concat(Abschlussprojekt.Kostenfelder).Concat(Abschlussprojekt.Kapitel))
        {
            Pruefe(f.Schluessel, f.Name + "|" + f.Zusatz);
        }
        foreach (var p in Abschlussprojekt.Phasen)
        {
            Pruefe(p.Schluessel, p.Name);
        }
        foreach (var (s, t) in Abschlussprojekt.ChecklisteUebersicht.Concat(Abschlussprojekt.ChecklisteAntrag(80)).Concat(Abschlussprojekt.ChecklistePraesentation))
        {
            Pruefe(s, t);
        }
        // Export und Kostenzeilen aller Faelle: nur (b) und (c)
        var ausnahmeB = 0;
        var ausnahmeC = 0;
        foreach (var (name, felder) in AbschlussprojektTests.Faelle())
        {
            foreach (var zeile in Abschlussprojekt.ExportText(felder, 40).Split('\n').Concat(Abschlussprojekt.Kostenzeilen(felder)))
            {
                var kern = zeile;
                if (kern.StartsWith("  - ", StringComparison.Ordinal))
                {
                    ausnahmeB++;
                    kern = kern[4..];
                }
                ausnahmeC += kern.Split(" €").Length - 1;
                kern = kern.Replace(" €", " €", StringComparison.Ordinal);
                // Eingaben der Faelle duerfen Leerraum enthalten; geprueft werden nur Zeilen ohne Eingabe
                if (!felder.Values.SelectMany(w => w.Split('\n')).Any(w => w.Trim().Length > 0 && zeile.Contains(w.Trim(), StringComparison.Ordinal)))
                {
                    Pruefe(name + " Export", kern);
                }
            }
        }
        Assert.True(ausnahmeB > 0 && ausnahmeC > 0);
        Assert.Equal("1.234,50 €", Abschlussprojekt.Euro(1234.5));
        // (a): die Fusszeile steht nur im PDF; dort zweimal "  ·  "
        var pdf = AbschlussprojektPdfTests.Zerlegen(Abschlussprojekt.ExportPdf(AbschlussprojektTests.Faelle().First().Felder, 40,
            new DateTime(2026, 10, 14))).Inhalte[0];
        Assert.Matches(@"\(Projekt ohne Titel  ·  Seite 1 von \d+  ·  14\.10\.2026\) Tj", pdf);
    }
}
