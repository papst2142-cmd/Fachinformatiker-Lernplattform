using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Lernen;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 2, L4: Texte der Karteikarten zeichengleich aus Python v0.63.2 (PC-Wortlaut,
/// app_gui.CardsView, fisi_core Q_STATUS_TABS/status_label, count_text). Vergleichswerte G22.
/// </summary>
public static class KartenTexte
{
    public const string Fachbereich = "FACHBEREICH";       // app_gui 2683
    public const string Status = "STATUS";                 // 2697
    public const string Thema = "THEMA";                   // 2703
    public const string Lernmodus = "LERNMODUS";           // 2712
    public const string Alle = "Alle";                     // FILTER_ALL, Pille "Alle" (2684)

    /// <summary>Q_STATUS_TABS (fisi_core 2284): Schluessel und Beschriftung.</summary>
    public static readonly IReadOnlyList<(string Wert, string Text)> StatusReiter =
    [
        (FragenStatus.Alle, "Alle"), (FragenStatus.Offen, "Offen"), (FragenStatus.Ueben, "Zu üben"),
        (FragenStatus.Fertig, "Abgeschlossen"),
    ];

    public const string Freitext = "freitext";
    public const string Mehrfachwahl = "mc";
    public const string Aufdecken = "reveal";

    /// <summary>CardsView.MODES (2660).</summary>
    public static readonly IReadOnlyList<(string Wert, string Text)> Modi =
        [(Freitext, "Freitext"), (Mehrfachwahl, "Multiple Choice"), (Aufdecken, "Aufdecken")];

    public const string FrageTitel = "Frage";                                                        // 2718
    public const string Untertitel = "%s · %s";                                                      // 2919
    public const string AntwortTitel = "Deine Antwort";                                              // 2731
    public const string FreitextHinweis = "Formuliere deine Antwort in eigenen Worten:";             // 2743
    public const string AufdeckenHinweis = "Überlege dir die Antwort und decke sie anschließend auf."; // 2755
    public const string Wusstest = "Wusstest du es?";                                                // 491
    public const string Gewusst = "Gewusst";                                                         // 493
    public const string NichtGewusst = "Nicht gewusst";                                              // 495
    public const string Zurueck = "Zurück";                                                          // 2773
    public const string Pruefen = "Antwort prüfen";                                                  // 2776
    public const string LoesungAufdecken = "Lösung aufdecken";                                       // 2935
    public const string Naechste = "Nächste Karte";                                                  // 2779
    public const string Zaehler = "Karte %s / %s";                                                   // 2916
    public const string ZaehlerLeer = "0 / 0";                                                       // 2908
    public const string Leer = "Für diese Auswahl gibt es keine Karteikarten.";                      // 2905
    public const string HinweisTitel = "Hinweis";                                                    // 2946
    public const string EingabeFehlt = "Bitte gib zuerst deine Antwort ein.";                        // 2946
    public const string AuswahlFehlt = "Bitte wähle eine Antwort aus.";                              // 2962
    public const string FreitextRichtig = "Sehr gut - deine Antwort enthält die Kernlösung.";        // 2950
    public const string FreitextVergleich = "Vergleiche deine Eingabe mit der Musterlösung:";        // 2954
    public const string MusterloesungVor = "Musterlösung: ";                                         // 2957
    public const string McRichtig = "Richtig beantwortet.";                                          // 2967
    public const string McFalschVor = "Leider falsch. Richtig wäre: ";                               // 2970
    public const string Musterloesung = "Musterlösung";                                              // 2975
    public const string WiederholungTitel = "Wiederholung";                                          // 11025
    public const string NichtsFaellig = "Heute ist nichts mehr fällig.";                             // 11025
    public const string Datenbankfehler = "Datenbankfehler";                                         // 10666 (_db_error)

    /// <summary>count_text (app_gui 479-484): zwei Leerzeichen um den Punkt, geschuetztes Leerzeichen vor der Zahl.</summary>
    public static string ZaehlText(IReadOnlyDictionary<string, int> zahlen) =>
        "Offen\u00a0" + DashboardTexte.DeInt(zahlen[FragenStatus.Offen]) + "  ·  Zu üben\u00a0"
        + DashboardTexte.DeInt(zahlen[FragenStatus.Ueben]) + "  ·  Abgeschlossen\u00a0"
        + DashboardTexte.DeInt(zahlen[FragenStatus.Fertig]);

    /// <summary>Farbschluessel der Statuszeile (status_label): offen, rot, gelb, fertig.</summary>
    public const string FarbeOffen = "offen";

    /// <summary>status_label (fisi_core 2395-2410): Text und Farbschluessel einer Frage.</summary>
    public static (string Text, string Farbe) StatusZeile(StatusBuch buch, string quelle, object? schluessel)
    {
        var (status, stufe) = buch.Status(quelle, schluessel);
        var folge = buch.Folge(quelle, schluessel);
        if (status == FragenStatus.Fertig)
        {
            return (Beschriftungen.Einsetzen("Abgeschlossen · %d-mal in Folge richtig", folge), FragenStatus.Fertig);
        }
        if (status == FragenStatus.Ueben)
        {
            return stufe == FragenStatus.Rot
                ? ("Zu üben · zuletzt falsch", FragenStatus.Rot)
                : (Beschriftungen.Einsetzen("Zu üben · %d von %d richtig", folge, FragenStatus.FertigFolge), FragenStatus.Gelb);
        }
        if (folge != 0)
        {
            return (Beschriftungen.Einsetzen("Offen · %d von %d richtig", folge, FragenStatus.FertigFolge), FarbeOffen);
        }
        return buch.Beruehrt(quelle, schluessel)
            ? ("Offen · angesehen, noch nicht bewertet", FarbeOffen)
            : ("Offen · noch nicht bearbeitet", FarbeOffen);
    }
}

/// <summary>
/// Phase 2, L6: Texte des Pruefungstrainers (Uebung) zeichengleich aus Python v0.63.2
/// (PC-Wortlaut, app_gui.QuizView, fisi_rechtliches). Vergleichswerte G22.
/// </summary>
public static class TrainerTexte
{
    public const string Uebung = "uebung";
    public const string Pruefung = "pruefung";

    /// <summary>QUIZ_MODES (app_gui 3375).</summary>
    public static readonly IReadOnlyList<(string Wert, string Text)> Arten = [(Uebung, "Übung"), (Pruefung, "Prüfung")];

    public const string SessionTitel = "Test-Session";                                                       // 3067
    public const string ImKatalog = "%s Aufgaben im Katalog";                                                // 3068
    public const string Fragenanzahl = "Fragenanzahl";                                                       // 3095
    public const string Weniger = "Weniger";                                                                 // 161 STEP_LESS (Name fuer Vorlesen)
    public const string Mehr = "Mehr";                                                                       // 161 STEP_MORE
    public const string Starten = "Session starten";                                                         // 3100
    public const string Verfuegbar = "%s verfügbar  ·  %s";                                                  // 3179
    public const string FrageEinzahl = "Frage";
    public const string FrageMehrzahl = "Fragen";
    public const string FortschrittLeer = "Frage 0 / 0";                                                     // 3111
    public const string Fortschritt = "Frage %d / %d";                                                       // 3262
    public const string UhrLeer = "00:00";                                                                   // 3114
    public const string Richtig = "%d richtig";                                                              // 3264
    public const string AufgabeTitel = "Prüfungsaufgabe";                                                    // 3121
    public const string StartHinweis = "Wähle Fachbereich und Fragenanzahl und starte die Session.";         // 3126
    public const string FrageNummer = "%d. %s";                                                              // 3257
    public const string Einreichen = "Antwort einreichen";                                                   // 3140
    public const string NaechsteFrage = "Nächste Frage";                                                     // 3294
    public const string Auswertung = "Auswertung anzeigen";                                                  // 3294
    public const string Abbrechen = "Session abbrechen";                                                     // 3144
    public const string KeineFragen = "Für diese Auswahl gibt es keine Fragen.";                             // 3210
    public const string ErklaerungRichtig = "Richtig. ";                                                     // 3286
    public const string ErklaerungFalsch = "Falsch. Richtig wäre: %s. ";                                     // 3286
    public const string AbbrechenFrage = "Die laufende Session wirklich abbrechen? Bereits beantwortete Fragen bleiben in der Statistik erhalten."; // 3307
    public const string Abgebrochen = "Session abgebrochen. Du kannst jederzeit eine neue starten.";          // 3314
    public const string Gespeichert = "Das Ergebnis wurde gespeichert.";                                     // 3331
    public const string NichtGespeichert = "Achtung: Das Ergebnis konnte nicht gespeichert werden.";         // 3332
    public const string NoteLang = "Note nach IHK-Schlüssel (Orientierung, keine amtliche Note)";            // fisi_rechtliches 60
    public const string HinweisKurz = "Übungsprüfung, keine offizielle IHK-Prüfung.";                        // fisi_rechtliches 47

    /// <summary>System-Knoepfe der Rueckfrage (askyesno am PC).</summary>
    public const string Ja = "Ja";
    public const string Nein = "Nein";

    /// <summary>"%s Aufgaben im Katalog" mit Tausenderpunkt (de_int).</summary>
    public static string Katalog(int anzahl) => Beschriftungen.Einsetzen(ImKatalog, DashboardTexte.DeInt(anzahl));

    /// <summary>_update_pool: "%s verfügbar  ·  %s" (plural Frage/Fragen, count_text).</summary>
    public static string Pool(int verfuegbar, IReadOnlyDictionary<string, int> zahlen) =>
        Beschriftungen.Einsetzen(Verfuegbar, DashboardTexte.Plural(verfuegbar, FrageEinzahl, FrageMehrzahl), KartenTexte.ZaehlText(zahlen));

    /// <summary>Uhr der Session: "%02d:%02d".</summary>
    public static string Uhr(int sekunden) => (sekunden / 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture)
        + ":" + (sekunden % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>finish_quiz (3334-3340): Ergebnisblock.</summary>
    public static string Ergebnis(int richtig, int gesamt, double prozent, string note, int sekunden, bool gespeichert) =>
        "Session beendet\n\n"
        + Beschriftungen.Einsetzen("Ergebnis: %d von %d richtig (%s %%)\n", richtig, gesamt, Trainer.DeZahl(prozent, 1))
        + NoteLang + ": " + note + "\n"
        + "Dauer: " + (sekunden / 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":"
        + (sekunden % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + " Minuten\n\n"
        + (gespeichert ? Gespeichert : NichtGespeichert);
}
