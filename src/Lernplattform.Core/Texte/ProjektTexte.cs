namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 4, W3 (Wortliste W3, Weisung P4-8): Texte der Seite "Abschlussprojekt", zeichengleich
/// zum PC-Python v0.63.2 (app_gui FinalProjectView 4586-5141, Zeilen am Ende). Die festen Listen
/// des Kerns (Reiter, Felder, Phasen, Kapitel, Checklisten) stehen in
/// <see cref="Projekt.Abschlussprojekt"/> und werden gegen G34 Teil "texte" geprueft. Neu (nicht
/// aus Python) sind nur Nr 28 (Export misslingt) und die Stundentexte je Ausbildung (Weisung P4-8,
/// Frage 1 b: Python-Text mit der Zahl der Ausbildung statt der festen 40).
/// </summary>
public static class ProjektTexte
{
    // Nr 1: Kopfzeile
    public const string Projekt = "Projekt";                                                                    // 4610
    public const string NeuesProjekt = "Neues Projekt";                                                         // 4614
    public const string Loeschen = "Löschen";                                                                   // 4616
    public const string AlsPdf = "Als PDF";                                                                     // 4620
    public const string AlsText = "Als Text";                                                                   // 4622

    // Nr 3, 4: Eckdaten
    public const string Eckdaten = "Eckdaten";                                                                  // 4873
    public const string EckdatenUntertitel = "Fristen und Seitenzahl legt deine IHK fest";                     // 4874
    public const string Fachbereich = "Fachbereich (für das Fachgespräch)";                                     // 4879
    public const string KeinFachbereich = "Kein Fachbereich";                                                   // 4583

    // Nr 5: Stand
    public const string Stand = "Stand";                                                                        // 4885
    public const string StandUntertitel = "Vom Antrag bis zum Fachgespräch";                                    // 4886
    public const string Erledigt = "%d von %d Schritten erledigt";                                              // 4933
    public const string AntragOffen = "Im Antrag noch offen: %s";                                               // 4935
    public const string AntragVoll = "Alle Felder des Antrags sind ausgefüllt.";                                // 4936

    // Nr 6, 7: Projektnote
    public const string Projektnote = "Geschätzte Projektnote";                                                 // 4895
    public const string ProjektnoteUntertitel = "Fließt ins Gesamtergebnis unter Prüfungstrainer ein";         // 4896
    public const string ProjektnoteHinweis = "Die Projektarbeit zählt 50 % der Abschlussprüfung. Trage "
        + "hier ein, wie viele Punkte (0 bis 100) du für Dokumentation, Präsentation und Fachgespräch zusammen "
        + "erwartest.";                                                                                         // 4898
    public const string ErwartetePunkte = "Erwartete Punkte (0-100)";                                           // 4901

    // Nr 8: Testprojekt als Vorlage
    public const string Vorlage = "Testprojekt als Vorlage";                                                    // 4904
    public const string VorlageUntertitel = "Füllt nur leere Felder";                                           // 4905
    public const string VorlageHinweis = "Übernimmt Titel, Ausgangssituation, Auftrag und Rahmenbedingungen "
        + "eines Testprojekts als Anregung. Bereits ausgefüllte Felder bleiben unverändert.";                   // 4907
    public const string Uebernehmen = "Übernehmen";                                                             // 4925

    // Nr 9: Antrag
    public const string Antrag = "Projektantrag";                                                               // 4948
    public const string AntragUntertitel = "Gliederung wie bei den IHK-Vorlagen";                               // 4949
    public const string AntragPruefen = "Prüfe vor dem Einreichen";                                             // 4956

    // Nr 10: Zeitplan (%d = Projektstunden der Ausbildung, Weisung P4-8)
    public const string Zeitplanung = "Zeitplanung";                                                            // 4961
    public const string ZeitplanUntertitel = "Höchstens %d Stunden inklusive Dokumentation";                    // 4962
    public const string WeiterePhase = "(weitere Phase, frei benennbar)";                                       // 4970 (ohne die zwei Leerzeichen, Nr 30)
    public const string ZeitplanSumme = "Summe: %s von %d Stunden";                                            // 4983

    // Nr 11: Kosten
    public const string Kostenrechnung = "Kostenrechnung";                                                      // 4992
    public const string KostenUntertitel = "Stunden kommen aus der Zeitplanung";                                // 4993
    public const string Sachkosten = "Sachkosten";                                                              // 4998
    public const string SachkostenUntertitel = "Hardware, Lizenzen, Material";                                  // 4999
    public const string Bezeichnung = "BEZEICHNUNG";                                                            // 5004
    public const string Betrag = "BETRAG";                                                                      // 5006
    public const string Euro = "€";                                                                             // 5015
    public const string Ergebnis = "Ergebnis";                                                                  // 5017

    // Nr 12: Dokumentation
    public const string Dokumentation = "Dokumentation";                                                        // 5028
    public const string DokuUntertitel = "Kapitelgerüst nach üblicher IHK-Gliederung";                          // 5029
    public const string Seitenvorgabe = "Seitenvorgabe deiner IHK";                                             // 5031
    public const string SeitenHinweis = "Grobe Schätzung: etwa %d Zeichen pro Seite (11 pt, 1,5-zeilig). "
        + "Deckblatt, Verzeichnisse und Anhang zählen meist nicht mit.";                                        // 5035
    public const string Seiten = "Etwa %s von %d Seiten";                                                       // 5047

    // Nr 13, 14: Fachgespraech
    public const string Praesentation = "Präsentation";                                                         // 5053
    public const string PraesentationUntertitel = "Höchstens 15 Minuten, danach das Fachgespräch";              // 5054
    public const string FachgespraechUeben = "Fachgespräch üben";                                               // 5059
    public const string FragenUntertitel = "%d Fragen · allgemeine plus die deines Fachbereichs";               // 5060
    public const string FragenStand = "Frage %d von %d · %d gewusst · %d nicht gewusst · %d offen";             // 5091
    public const string Gewusst = "Gewusst";                                                                    // 5079
    public const string NichtGewusst = "Nicht gewusst";                                                         // 5079
    public const string NichtBewertet = "Noch nicht bewertet";                                                  // 5080
    public const string HinweisZeigen = "Hinweis zeigen";                                                       // 5072
    public const string HinweisAusblenden = "Hinweis ausblenden";                                               // 5083
    public const string NaechsteFrage = "Nächste Frage";                                                        // 5075
    public const string WusstestDu = "Wusstest du es?";                                                         // fisi_widgets rate_box 491

    // Nr 15: Datei-Dialog
    public const string DialogTitel = "Abschlussprojekt speichern";                                             // 4723
    public const string PdfTyp = "PDF-Dokument";                                                                // 4725
    public const string TextTyp = "Textdatei";                                                                  // 4726

    // Nr 16: Kurzhinweise (toast)
    public const string Gespeichert = "Gespeichert: %s";                                                        // 4742
    public const string VorlageUebernommen = "%s aus dem Testprojekt übernommen";                               // 4749
    public const string Feld = "Feld";                                                                          // 4750
    public const string Felder = "Felder";                                                                      // 4750
    public const string VorlageVoll = "Alle passenden Felder waren schon ausgefüllt";                           // 4751

    // Nr 17: Knopf auf der Seite Testprojekt
    public const string AlsVorlage = "Als Vorlage fürs Abschlussprojekt";                                       // 4457

    // Nr 28 (NEU): Export misslingt; Einzelheiten nur ins Fehlerprotokoll
    public const string SpeichernFehlgeschlagen = "Speichern fehlgeschlagen";                                   // 4740
    public const string DateiNichtGespeichert = "Die Datei konnte nicht gespeichert werden.";                   // NEU (Nr 28)

    // Nr 18, 19, 20: Kern (fisi_projekt)
    public const string OhneTitel = "Projekt ohne Titel";                                                       // fpj 168
    public const string Position = "Position %d";                                                               // fpj 327
    public const string Weitere = "Weitere Phase";                                                              // fpj 412
    public const string NochOffen = "(noch offen)";                                                             // fpj 406
    public const string Checkliste = "Checkliste";                                                              // fpj 425
    public const string ExportSumme = "Summe: %s (höchstens %d Stunden)";                                      // fpj 413
    public const string ExportFeld = "%s: %s";                                                                  // fpj 401
    public const string Haken = "[%s] %s";                                                                      // fpj 427
    public const string Stunden = " Std.";                                                                      // fpj 286
    public const string Dateiname = "Abschlussprojekt_%s.%s";                                                   // fpj 453
    public const string DateinameVorgabe = "Projekt";                                                           // fpj 453
    public const string WarnungSumme = "Die Summe von %s liegt über den erlaubten %d Stunden.";                // fpj 304
    public const string WarnungDoku = "Die Dokumentation nimmt %d %% der Zeit ein (üblich höchstens 15 bis 20 %%).";  // fpj 308
    public const string KostenStunden = "Projektstunden laut Zeitplan: %s";                                     // fpj 342
    public const string KostenPersonal = "Personalkosten: %s";                                                  // fpj 343
    public const string KostenSach = "Sachkosten: %s";                                                          // fpj 344
    public const string KostenGesamt = "Projektkosten gesamt: %s";                                              // fpj 345
    public const string KeineAmortisation = "Amortisation: keine Einsparung eingetragen";                       // fpj 347
    public const string Amortisation = "Amortisation nach %s Monaten (%s Einsparung netto pro Monat)";          // fpj 349
    public const string ChecklisteStunden = "Summe höchstens %d Stunden inklusive Dokumentation";               // fpj 115 (max40)

    public const string RueckfrageTitel = "Projekt löschen";                                                    // fpj 198
    public const string RueckfrageAbgleich = "„%s“ löschen? Das Projekt und alle seine Eingaben werden gelöscht, "
        + "nach dem Abgleich auch auf deinen anderen Geräten. Das lässt sich in der App nicht rückgängig machen."
        + "\n\nHinweis: Ältere Sicherungsdateien und frühere Stände im Verlauf des Abgleich-Repositorys auf "
        + "GitHub enthalten die Eingaben weiterhin.";                                                           // fpj 199
    public const string RueckfrageLokal = "„%s“ löschen? Das Projekt und alle seine Eingaben werden gelöscht. "
        + "Das lässt sich in der App nicht rückgängig machen.\n\nHinweis: Ältere Sicherungsdateien enthalten die "
        + "Eingaben weiterhin. Falls du den Abgleich früher genutzt hast, enthalten frühere Stände im "
        + "Abgleich-Repository auf GitHub die Eingaben weiterhin.";                                             // fpj 204
}
