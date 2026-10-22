using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Fortschritt;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Projekt;

/// <summary>Ein Feld mit Beschriftung und Hinweis (PROPOSAL_FIELDS, CHAPTERS) bzw. Vorgabe (COST_FIELDS).</summary>
public sealed record ProjektFeld(string Schluessel, string Name, string Zusatz);

/// <summary>Eine Phase des Zeitplans mit Vorgabestunden (PHASES).</summary>
public sealed record Phase(string Schluessel, string Name, int Vorgabe);

/// <summary>Eine Zeile des Zeitplans (schedule): Name (bei freien Phasen das Feld) und Stunden.</summary>
public sealed record Zeitplanzeile(string Schluessel, string Name, double Stunden);

/// <summary>Kostenrechnung (costs), alle Werte in EUR bzw. Monaten.</summary>
public sealed record Kosten(double Stunden, double Personal, IReadOnlyList<(string Name, double Betrag)> Material,
    double MaterialSumme, double Gesamt, double NettoMonat, double? Amortisation);

/// <summary>Ein Block des Exports (export_blocks): Art h1, h2, h3, p oder li.</summary>
public sealed record ExportBlock(string Art, string Text);

/// <summary>
/// Phase 4, W3 (PB8): Arbeitsbereich Abschlussprojekt ohne Oberflaeche wie fisi_projekt v0.63.2
/// (Spezifikation G34 P4-8 Teil D mit Nachtrag P4-9). Jedes Feld steht einzeln in der Tabelle
/// abschlussprojekt (neueste Fassung gilt, LernstandSchreiber.ProjektFeldSpeichern), mehrere
/// Projekte, eines je Geraet aktiv (einstellungen.json "aktives_projekt"). Die reservierte Zeile
/// der Ausbildung (<see cref="Ausbildung.Zeile"/>) ist nie ein Projekt. Pflichtregeln: Laenge,
/// Abschneiden und strip in Codepunkten mit Pythons Leerraum (PythonZeichen), "%.1f"/"%.2f" aus dem
/// exakten Binaerwert halb gerade (<see cref="Fest"/>), round halb gerade (PythonRunden), Summen mit
/// PythonSumme, Sortieren nach Codepunkten und stabil. Die Obergrenze der Projektstunden kommt aus der
/// Ausbildung (Weisung P4-8, Frage 1 b: AE 80, sonst 40; Python kennt nur 40).
/// </summary>
public static partial class Abschlussprojekt
{
    public const int DokuAnteilMax = 20;
    public const int ZeichenJeSeite = 2800;
    public const int SeitenVorgabe = 15;
    public const int Sachkostenzeilen = 5;
    public const int SpeicherVerzug = 800;      // PROJECT_SAVE_DELAY (ms nach dem letzten Tastendruck)

    public const string SchaetzungFeld = Gesamtstand.SchaetzungFeld;
    public const string SeitenFeld = "seitenziel";
    public const string TitelFeld = "titel";
    public const string FachbereichFeld = "fachbereich";
    public const string GeloeschtFeld = Gesamtstand.GeloeschtFeld;
    public const string AktivSchluessel = Gesamtstand.AktivSchluessel;

    /// <summary>MAX_HOURS je Ausbildung (Fachrichtung.ProjektStunden, G36): AE 80, SI/DPA/DV 40.</summary>
    public static int MaxStunden(string fachrichtung) => Fachrichtung.Nach(fachrichtung).ProjektStunden;

    public static readonly IReadOnlyList<(string Schluessel, string Name)> Reiter =
    [
        ("uebersicht", "Übersicht"), ("antrag", "Antrag"), ("zeitplan", "Zeitplan"),
        ("kosten", "Kosten"), ("doku", "Dokumentation"), ("fachgespraech", "Fachgespräch"),
    ];

    /// <summary>OVERVIEW_FIELDS (ohne das immer falsche "mehrzeilig").</summary>
    public static readonly IReadOnlyList<(string Schluessel, string Name)> Uebersicht =
    [
        ("titel", "Projekttitel"),
        ("betrieb", "Ausbildungsbetrieb / Auftraggeber"),
        ("zeitraum", "Durchführungszeitraum"),
        ("frist_antrag", "Abgabe Projektantrag bis"),
        ("frist_doku", "Abgabe Dokumentation bis"),
    ];

    /// <summary>PROPOSAL_FIELDS (Schluessel, Name, Hinweis).</summary>
    public static readonly IReadOnlyList<ProjektFeld> Antrag =
    [
        new("bezeichnung", "Projektbezeichnung", "Kurzer, aussagekräftiger Titel: Was wird für wen gemacht?"),
        new("ausgangssituation", "Ausgangssituation", "Ist-Zustand und Problem beim Auftraggeber, warum das Projekt nötig ist."),
        new("ziel", "Projektziel", "Sachziel, Kostenziel, Zeitziel und Qualitätsziel - möglichst messbar."),
        new("umfeld", "Auftraggeber und Projektumfeld", "Wer beauftragt, wer ist beteiligt, Schnittstellen, Rahmenbedingungen."),
        new("alternativen", "Lösungsalternativen", "Mindestens zwei Möglichkeiten und nach welchen Kriterien entschieden wird."),
        new("systeme", "Technische Systeme und Ressourcen", "Hard- und Software, Netz, Werkzeuge, Personal."),
        new("einfuehrung", "Einführung und Pflege", "Übergabe, Einweisung, Betrieb und Wartung nach dem Projekt."),
        new("sicherheit", "IT-Sicherheit und Datenschutz", "Schutzbedarf, Schwachstellen, geplante Maßnahmen."),
        new("praesentationsmittel", "Dokumentation und Präsentationsmittel", "Welche Unterlagen entstehen, womit wird präsentiert."),
    ];

    /// <summary>PHASES: feste Phasen plus zwei freie.</summary>
    public static readonly IReadOnlyList<Phase> Phasen =
    [
        new("analyse", "Analyse (Ist-Aufnahme, Anforderungen)", 5),
        new("planung", "Planung und Entwurf (Soll-Konzept, Wirtschaftlichkeit)", 7),
        new("durchfuehrung", "Durchführung / Umsetzung", 14),
        new("test", "Test und Qualitätssicherung", 4),
        new("uebergabe", "Übergabe, Einweisung, Abnahme", 2),
        new("dokumentation", "Dokumentation", 8),
        new("frei1", "", 0),
        new("frei2", "", 0),
    ];

    /// <summary>COST_FIELDS (Schluessel, Beschriftung, Vorgabe).</summary>
    public static readonly IReadOnlyList<ProjektFeld> Kostenfelder =
    [
        new("satz_azubi", "Stundensatz Auszubildende/r (EUR)", "25"),
        new("satz_ma", "Stundensatz Mitarbeiter/in (EUR)", "60"),
        new("stunden_ma", "Stunden Mitarbeiter/in (Abnahme, Einweisung)", "4"),
        new("einsparung", "Einsparung pro Monat (EUR)", "0"),
        new("laufend", "Laufende Kosten pro Monat (EUR)", "0"),
    ];

    /// <summary>CHAPTERS (Schluessel, Titel, Hinweis).</summary>
    public static readonly IReadOnlyList<ProjektFeld> Kapitel =
    [
        new("einleitung", "1. Einleitung", "Projektumfeld, Projektziel, Projektbegründung, Projektschnittstellen, Abgrenzung."),
        new("ist", "2. Ist-Analyse", "Bestandsaufnahme und Schwachstellen."),
        new("soll", "3. Soll-Konzept und Lösungsauswahl", "Anforderungen, Alternativen, Entscheidung (z. B. Nutzwertanalyse)."),
        new("wirtschaft", "4. Wirtschaftlichkeit", "Projektkosten, Make-or-Buy, Amortisation."),
        new("durchfuehrung", "5. Durchführung", "Umsetzung Schritt für Schritt, Abweichungen vom Plan."),
        new("test", "6. Test und Qualitätssicherung", "Testfälle, Testergebnisse, Abnahme."),
        new("uebergabe", "7. Übergabe und Einführung", "Einweisung, Dokumentation für Anwender und Administration."),
        new("fazit", "8. Fazit und Ausblick", "Soll-Ist-Vergleich, Lessons Learned, Ausblick."),
    ];

    public static readonly IReadOnlyList<(string Schluessel, string Text)> ChecklisteUebersicht =
    [
        ("antrag_entwurf", "Projektantrag geschrieben"),
        ("antrag_betrieb", "Antrag vom Ausbildungsbetrieb geprüft"),
        ("antrag_eingereicht", "Antrag bei der IHK eingereicht"),
        ("antrag_genehmigt", "Antrag genehmigt"),
        ("durchgefuehrt", "Projekt durchgeführt"),
        ("doku_fertig", "Dokumentation fertig und korrekturgelesen"),
        ("doku_abgegeben", "Dokumentation abgegeben"),
        ("praesentation", "Präsentation erstellt (höchstens 15 Minuten)"),
        ("geprobt", "Präsentation mit Zeitmessung geprobt"),
        ("fachgespraech", "Fachgespräch geübt"),
    ];

    /// <summary>CHECKLIST_PROPOSAL; der Text von "max40" nennt die Projektstunden der Ausbildung.</summary>
    public static IReadOnlyList<(string Schluessel, string Text)> ChecklisteAntrag(int maxStunden) =>
    [
        ("phasen", "Zeitplan in Phasen mit Stunden"),
        ("max40", Beschriftungen.Einsetzen(ProjektTexte.ChecklisteStunden, maxStunden)),
        ("eigenleistung", "Eigene Leistung klar erkennbar"),
        ("messbar", "Projektziel messbar beschrieben"),
        ("ki", "Einsatz von KI-Hilfsmitteln gekennzeichnet (frag bei deiner IHK nach)"),
    ];

    public static readonly IReadOnlyList<(string Schluessel, string Text)> ChecklistePraesentation =
    [
        ("p_dauer", "Dauer 12 bis 15 Minuten"),
        ("p_gliederung", "Gliederung: Ausgangslage, Ziel, Vorgehen, Ergebnis, Fazit"),
        ("p_wirtschaft", "Wirtschaftlichkeit kurz dargestellt"),
        ("p_folien", "Folien gut lesbar, wenig Text"),
        ("p_technik", "Technik und Ersatz (USB-Stick, PDF) vorbereitet"),
    ];
}
