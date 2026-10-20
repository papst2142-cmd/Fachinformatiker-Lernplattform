namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 2, L7: Texte des Fortschritts zeichengleich aus Python v0.63.2 (PC-Wortlaut:
/// app_gui.ProgressView/CoverageCard, fisi_lernen, fisi_rahmenplan, fisi_pruefung,
/// fisi_widgets.ShareBars, fisi_rechtliches). Vergleichswerte G18.
/// </summary>
public static class FortschrittTexte
{
    // Kacheln (app_gui.ProgressView)
    public const string Sessions = "Test-Sessions";                          // app_gui 6575
    public const string Durchschnitt = "Durchschnitt";                       // 6576
    public const string Bestes = "Bestes Ergebnis";                          // 6578
    public const string SessionsUnter = "abgeschlossene Sessions";           // 6695
    public const string ProzentMuster = "%s %%";                             // 6697
    public const string DurchschnittUnter = "über alle Sessions";            // 6698
    public const string OhneWert = "-";                                      // 6705
    public const string BestesLeer = "noch keine Session";                   // 6706

    // Tabellen
    public const string SessionsTitel = "Historie der Prüfungssessions";     // 6615
    public const string SpalteDatum = "Datum & Uhrzeit";                     // 6624
    public const string SpalteErgebnis = "Ergebnis";                         // 6624
    public const string SpalteQuote = "Erfolgsquote";                        // 6625
    public const string SpalteNote = "Note (Orientierung)";                  // fisi_rechtliches NOTE_KURZ
    public const string SpalteDauer = "Dauer";                               // 6626
    public const string PruefungenTitel = "Prüfungen (Klausursimulation)";   // 6639
    public const string PruefungenUnter = "aus dem Prüfungstrainer";         // 6640
    public const string SpaltePruefung = "Prüfung";                          // 6646
    public const string SpaltePunkte = "Punkte";                             // 6646
    public const string Aktualisieren = "Aktualisieren";                     // 6657
    public const string ErgebnisMuster = "%d / %d";                          // 6728
    public const string DauerMuster = "%02d:%02d min";                       // 6731

    // Diagramme (fisi_lernen 446-541, fisi_widgets 1997)
    public const string TageTitel = "Aufgaben pro Tag";
    public const string TageUnter = "bewertete Aufgaben, Tagesziel als Linie";
    public static readonly IReadOnlyList<(int Tage, string Text)> Zeitraeume = [(7, "7 Tage"), (30, "30 Tage")];
    public const string TageReihe = "Aufgaben";
    public const string AnteilTitel = "Richtig und falsch pro Tag";
    public const string AnteilRichtig = "richtig";
    public const string AnteilFalsch = "falsch";
    public const string AnteilLeerTag = "–";
    public const string BalkenLegende = "Zahl über dem Balken: % richtig";
    public const string ErgebnisTitel = "Ergebnisse im Prüfungstrainer";
    public const string ErgebnisUnter = "% richtig je abgeschlossener Session";
    public const string ErgebnisReihe = "richtig in %";
    public const string ErgebnisLeer = "Noch keine Ergebnisse. Hier erscheint nach jeder abgeschlossenen "
        + "Session im Prüfungstrainer (Runde bis zur Auswertung gespielt), wie viel Prozent der Fragen du "
        + "richtig beantwortet hast – mit Datum.";
    public static readonly IReadOnlyList<string> Wochentage = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];

    // Schwaechen und "Jetzt ueben" (fisi_lernen 613-615, fisi_pruefung 405)
    public const string SchwachTitel = "Üben (letzte Prüfung: %s%s)";
    public const string Ueben = "Jetzt üben";
    public const string UebenKeine = "Zu diesem Thema gibt es keine Übungsfragen.";
    public const string WeiterTitel = "Weiter üben";

    // Rahmenplan-Abdeckung (fisi_rahmenplan 285-298)
    public const string AbdeckungTitel = "Rahmenplan-Abdeckung";
    public const string AbdeckungUnter = "Wissensstand je Punkt";
    public const string AnsichtPlan = "rahmenplan";
    public const string AnsichtFelder = "lernfeld";
    public static readonly IReadOnlyList<(string Wert, string Text)> Ansichten = [(AnsichtPlan, "Rahmenplan"), (AnsichtFelder, "Lernfeld")];
    public const string FilterAlle = "Alle";
    public const string FilterAp1 = "AP1";
    public static readonly IReadOnlyList<(string Wert, string Text)> Filter =
        [(FilterAlle, "Alle"), (FilterAp1, "AP1"), ("Konzeption", "Konzeption"), ("Netzwerke", "Netzwerke"), ("Projekt", "Projekt"), ("WiSo", "WiSo")];
    public const string KeinInhalt = "kein Lerninhalt vorhanden";
    public const string KeineAntworten = "Noch keine Antworten. Starte mit Karteikarten oder dem Prüfungstrainer.";
    public const string AktuellesLernfeld = "Dein aktuelles Lernfeld";
    public const string FilterLeer = "Zu diesem Filter gibt es in den eingeschalteten Abschnitten keine Punkte.";
    public const string OhneProzent = "–";                                   // percent_text(None)
    public const string Jahr = "%d. Ausbildungsjahr";                        // year_label

    /// <summary>practice_next_text: Rueckfrage vor dem zweiten Teil von "Jetzt ueben".</summary>
    public static string WeiterText(string quelle, int anzahl) =>
        "Zum selben Thema gibt es noch "
        + (quelle == Lernen.Quellen.Karte
            ? Dashboard.DashboardTexte.Plural(anzahl, "Karteikarte", "Karteikarten")
            : Dashboard.DashboardTexte.Plural(anzahl, "Prüfungsfrage", "Prüfungsfragen"))
        + ". Jetzt weiterüben?";

    /// <summary>fisi_pruefung.EXAMS: Art -> (Name, Kurzname).</summary>
    public static readonly IReadOnlyDictionary<string, (string Name, string Kurz)> Pruefungen = new Dictionary<string, (string, string)>
    {
        ["ap1"] = ("AP1 · Einrichten eines IT-gestützten Arbeitsplatzes", "AP1"),
        ["konzeption"] = ("AP2 · Konzeption und Administration von IT-Systemen", "Konzeption"),
        ["netzwerke"] = ("AP2 · Analyse und Entwicklung von Netzwerken", "Netzwerke"),
        ["wiso"] = ("AP2 · Wirtschafts- und Sozialkunde", "WiSo"),
    };
}
