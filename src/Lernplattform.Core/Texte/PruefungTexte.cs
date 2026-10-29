namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 4, W2 (Wortliste W2, Weisung P4-6): Texte der Klausursimulation. Die SI-Texte sind
/// zeichengleich zu Python v0.63.2 (app_gui ExamPanel, fisi_pruefung); der Test vergleicht jeden
/// Namen mit dem Teil "texte" von G32 (tests/Vergleich/pruefung.json, mit ast aus Python gelesen).
/// Neu (Abschnitt 6 und 7 der Wortliste): Fachrichtungen und Optionen &gt; Ausbildung.
/// Nr 81 (Hinweis auf die Projektnote unter "Übersicht") kommt mit W3 (Weisung P4-8).
/// </summary>
public static class PruefungTexte
{
    public const string Uebung = "Übung";                                                                       // 3375
    public const string Pruefung = "Prüfung";                                                                   // 3375
    public const string AuswahlTitel = "Prüfung nach IHK-Vorbild";                                              // 3438
    public const string AuswahlUntertitel = "Aufbau nach der Ausbildungsverordnung 2020";                       // 3439
    public const string Einleitung = "Aufgebaut wie die echte Prüfung: feste Zeit ohne Pause, alle Aufgaben sind Pflicht, "
        + "keine Musterlösung während der Prüfung. Offene Aufgaben bewertest du nach der Abgabe selbst anhand der "
        + "Musterlösung, WiSo wird automatisch ausgewertet. %s: ab 92 Punkten sehr gut, ab 81 gut, ab 67 befriedigend, "
        + "ab 50 ausreichend, ab 30 mangelhaft.";                                                               // 3442
    public const string AngabeWiso = "%d Minuten · %d Auswahlfragen · 100 Punkte";                              // 3462
    public const string AngabeOffen = "%d Minuten · 4 Aufgaben zu je 25 Punkten";                               // 3465
    public const string Starten = "%s starten";                                                                 // 162
    public const string LetztePruefungen = "Letzte Prüfungen";                                                  // 3532
    public const string Insgesamt = "%d insgesamt";                                                             // 3533
    public const string KeinePruefung = "Noch keine Prüfung abgelegt.";                                         // 3536
    public const string PunkteMuster = "%s Punkte";                                                             // 3546
    public const string StartTitel = "Prüfung starten";                                                         // 3562
    public const string StartFrage = "%s\n\nDie Zeit (%d Minuten) läuft sofort und lässt sich nicht anhalten - "
        + "auch nicht, wenn du das Programm schließt. Jetzt starten?";                                          // 3563
    public const string StartFrageHandy = "%s\n\nDie Zeit (%d Minuten) läuft sofort und lässt sich nicht anhalten - "
        + "auch nicht, wenn du die App schließt. Jetzt starten?";                                               // main.py 1541 (B12)
    public const string Verbleibend = "verbleibend";                                                            // 3588
    public const string Abbrechen = "Abbrechen";                                                                // 3590
    public const string Abgeben = "Abgeben";                                                                    // 3591
    public const string FragenBeantwortet = "%d von %d Fragen beantwortet";                                     // 3622
    public const string TeileBearbeitet = "%d von %d Teilaufgaben bearbeitet";                                  // 3626
    public const string AufgabeNummer = "Aufgabe %d";                                                           // 3600
    public const string AufgabeUntertitel = "%s · 25 Punkte";                                                   // 3638
    public const string Teilaufgabe = "%s) %s  (%d Punkte)";                                                    // 3646
    public const string WisoTitel = "Wirtschafts- und Sozialkunde";                                             // 3662
    public const string Seite = "Seite %d von %d";                                                              // 3663
    public const string FrageNummer = "%d. %s";                                                                 // 3668
    public const string Zurueck = "Zurück";                                                                     // 3684
    public const string Weiter = "Weiter";                                                                      // 3687
    public const string LaeuftTitel = "Prüfung läuft";                                                          // 3190
    public const string LaeuftText = "Es läuft gerade eine Prüfung. Gib sie zuerst ab oder brich sie ab.";      // 3190
    public const string AbbrechenTitel = "Prüfung abbrechen";                                                   // 3730
    public const string AbbrechenFrage = "Die Prüfung wirklich abbrechen? Deine Antworten werden verworfen und es gibt kein Ergebnis."; // 3731
    public const string AbgebenTitel = "Abgeben";                                                               // 3740
    public const string AbgebenFrage = "Prüfung jetzt abgeben? Danach kannst du nichts mehr ändern.";           // 3740
    public const string ZeitTitel = "Zeit abgelaufen";                                                          // 3750
    public const string ZeitWiso = "Die Zeit ist um, die Prüfung wurde abgegeben und ausgewertet.";             // 3750
    public const string ZeitOffen = "Die Zeit ist um, die Prüfung wurde abgegeben. Jetzt folgt die Bewertung."; // 3755
    public const string BewertungTitel = "Bewertung";                                                           // 3767
    public const string BewertungAnleitung = "Vergleiche deine Antwort mit der Musterlösung und hake ab, was du hattest. "
        + "Die Punkte werden vorgeschlagen, du kannst sie wie ein Prüfer anpassen.";                            // 3770
    public const string BewertungAbschliessen = "Bewertung abschließen";                                        // 3778
    public const string Zwischenstand = "Zwischenstand: %d von 100 Punkten";                                    // 3798
    public const string TitelThema = "%s · %s";                                                                 // 3805
    public const string DeineAntwort = "DEINE ANTWORT";                                                         // 3814
    public const string KeineAntwort = "(keine Antwort)";                                                       // 3818
    public const string Musterloesung = "MUSTERLÖSUNG · KRITERIEN";                                             // 3820
    public const string Punkte = "Punkte";                                                                      // 3843
    public const string VonMax = "von %d";                                                                      // 3855
    public const string AbschliessenFrage = "Bewertung abschließen und das Ergebnis speichern?";                // 3862
    public const string ErgebnisTitel = "Ergebnis";                                                             // 3877
    public const string NoteDauer = "%s %s  ·  Dauer %s";                                                       // 3883
    public const string FragenRichtig = "%d von %d Fragen richtig";                                             // 3889
    public const string AufgabeErgebnis = "Aufgabe %d · %s: %d von %d Punkten";                                 // 3894
    public const string NachThemen = "Nach Themen";                                                             // 3898
    public const string Schwachstellen = "Schwachstellen rot";                                                  // 3899
    public const string BalkenWert = "%s von %s";                                                               // 3907
    public const string Ueben = "Üben:";                                                                        // 3910
    public const string ZurAuswahl = "Zur Prüfungsauswahl";                                                     // 3913
    public const string Gesamtergebnis = "Gesamtergebnis";                                                      // 4118
    public const string Gewichtung = "Gewichtung 20 / 50 / 10 / 10 / 10 %";                                     // 4119
    public const string BereichZeile = "%s (%d %%): %s";                                                        // 4124
    public const string NochOffen = "noch offen";                                                               // 4126
    public const string FehltNoch = "Für das Gesamtergebnis fehlt noch: %s.";                                   // 4131
    public const string ProjektHinweisW3 = " Die Projektnote schätzt du im Abschlussprojekt unter „Übersicht“.";// 4133, Nr 81 (W3)
    public const string GesamtZeile = "Gesamt %s Punkte · %s %s · %s";                                          // 4137
    public const string Bestanden = "bestanden";                                                                // 4139
    public const string NichtBestanden = "nicht bestanden";                                                     // 4139
    public const string Erfuellt = "erfüllt:  ";                                                                // 4143
    public const string Offen = "offen:  ";                                                                     // 4143

    // -- fisi_pruefung (Kern) ----------------------------------------------------------------

    /// <summary>Themen der WiSo-Auswertung (evaluate).</summary>
    public const string ThemaRecht = "Recht & Verträge";
    public const string ThemaArbeitswelt = "Arbeitswelt";

    /// <summary>_rules (fp 473-477), gleich fuer alle Fachrichtungen.</summary>
    public const string Regel1 = "Gesamtergebnis Teil 1 und 2 mindestens ausreichend (50)";
    public const string Regel2 = "Ergebnis Teil 2 mindestens ausreichend (50)";
    public const string Regel3 = "Mindestens drei Bereiche von Teil 2 ausreichend (%d von 4)";
    public const string Regel4 = "Kein Bereich von Teil 2 ungenügend (unter 30)";

    /// <summary>
    /// _supplement_hint (fp 501-506). Nr 91 verallgemeinert (Wortliste W2 Abschnitt 6): die beiden
    /// Kurznamen der Fachrichtung statt "Konzeption, Netzwerke"; fuer SI zeichengleich zu Python.
    /// </summary>
    public const string ErgaenzungNein = "Eine mündliche Ergänzungsprüfung würde hier nicht reichen (nur in %s, %s oder WiSo möglich, Gewichtung 2:1).";
    public const string ErgaenzungJa = "Eine mündliche Ergänzungsprüfung in %s könnte reichen: Dafür wären dort mindestens %s nötig (alt zu mündlich 2:1).";
    public const string PunktEinzahl = "Punkt";
    public const string PunktMehrzahl = "Punkte";

    // -- NEU: Optionen > Ausbildung (Wortliste W2 Abschnitt 7) --------------------------------

    public const string AusbildungReiter = "Ausbildung";
    public const string AusbildungTitel = "Fachrichtung";
    public const string AusbildungUntertitel = "Wird mit dem Abgleich auf deine anderen Geräte übertragen.";
    // Phase 9 W2 (Weisung P9-5 Punkt 6): zweiter Satz neu, die Inhalte richten sich nach der Wahl
    public const string AusbildungHinweis = "AP1, WiSo und das Abschlussprojekt sind für alle Fachrichtungen gleich. "
        + "Karteikarten, Prüfungsfragen, Szenarien und Testprojekte zeigen die gemeinsamen Inhalte und die deiner Fachrichtung.";

    /// <summary>Auswahl in Optionen &gt; Ausbildung, Reihenfolge wie Pruefung.Ausbildung.Werte.</summary>
    public static readonly IReadOnlyList<(string Wert, string Text)> Fachrichtungen =
    [
        ("si", "Systemintegration (SI)"),
        ("ae", "Anwendungsentwicklung (AE)"),
        ("dpa", "Daten- und Prozessanalyse (DPA)"),
        ("dv", "Digitale Vernetzung (DV)"),
    ];

    public static readonly IReadOnlyList<string> AusbildungSuchwoerter =
        ["Ausbildung", "Fachrichtung", "Systemintegration", "Anwendungsentwicklung", "Datenanalyse", "Vernetzung"];

    /// <summary>
    /// Feste Ausnahmen der Pruefung "nur Einfachleerzeichen" (Wortliste W2 Abschnitt 9, vier Eintraege).
    /// </summary>
    public static readonly IReadOnlyList<string> DoppelleerzeichenAusnahmen = [Erfuellt, Offen, NoteDauer, Teilaufgabe];
}
