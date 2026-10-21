using System.Globalization;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 4, W1: Texte der Listen (PagedList), AP1-/AP2-Szenarien und des Testprojekts zeichengleich
/// aus Python v0.63.2, PC-Wortlaut auf PC und Handy (Wortliste P4-2, H1-H10, Weisung P4-3).
/// Zeilenangaben app_gui.py.
/// </summary>
public static class SzenarienTexte
{
    // PagedList (500-760)
    public const string SucheHinweis = "Titel oder Nummer suchen";                 // 527
    public const string Fachbereich = "FACHBEREICH";                               // 535
    public const string Thema = "Thema";                                           // 512 group_label (Anzeige GROSS)
    public const string Schwierigkeit = "Schwierigkeit";                           // 4410 group_label
    public const string Status = "STATUS";                                         // 540
    public const string KeineTreffer = "Keine Treffer für diese Auswahl.";         // 561
    public const string Zaehler = "%s von %s %s";                                  // 714
    public const string Seite = "Seite %d / %d";                                   // 717
    public const string Fertig = " ✓";                                             // 700
    public const string StatusTrenner = "  ·  ";                                   // 706 (Ausnahme Einfachleerzeichen Nr. 3)
    public const string Aufgaben = "Aufgaben";                                     // 513 count_noun
    public const string Kundenauftraegen = "Kundenaufträgen";                      // 4411
    public const string SeiteZurueck = "Vorherige Seite";                          // 160 PAGE_PREV
    public const string SeiteWeiter = "Nächste Seite";                             // 160 PAGE_NEXT

    // ScenarioViewBase (4154-4370)
    public const string Ap1Titel = "AP1-Szenarien";                                // 4363
    public const string Ap2Titel = "AP2-Szenarien";                                // 4353
    public const string AnzahlAufgaben = "%d Aufgaben";                            // 4188
    public const string Aufgabenstellung = "Aufgabenstellung";                     // 4212
    public const string DeineLoesung = "Deine Lösung";                             // 4226
    public const string DeineLoesungUnter = "wächst automatisch mit dem Text";     // 4227
    public const string DeineLoesungHinweis = "Löse die Aufgabe hier schriftlich, bevor du die Musterlösung aufdeckst."; // 4232
    public const string Musterloesung = "Musterlösung";                            // 4244
    public const string Anzeigen = "Musterlösung anzeigen";                        // 4252
    public const string Ausblenden = "Musterlösung ausblenden";                    // 4335
    public const string Naechstes = "Nächstes Szenario";                           // 4255
    public const string NochAusgeblendet = "Die Musterlösung ist noch ausgeblendet.\n\nBearbeite die Aufgabe zuerst selbst und decke die Lösung anschließend auf."; // 4316
    public const string Ausgeblendet = "Die Musterlösung ist ausgeblendet.";       // 4330

    /// <summary>Listeneintrag "%d. %s%s" (703, PC-Wortlaut mit einfachem Leerzeichen).</summary>
    public static string Eintrag(int position, string titel, bool fertig) =>
        (position + 1).ToString(CultureInfo.InvariantCulture) + ". " + titel + (fertig ? Fertig : "");

    /// <summary>Untertitel eines Listeneintrags, ab "offen" mit Status (704-706).</summary>
    public static string Unterzeile(string grund, string status) =>
        status == FragenStatus.Offen ? grund : grund + StatusTrenner + Listen.StatusName[status];

    /// <summary>"%s von %s %s" (714).</summary>
    public static string ZaehlerText(int treffer, int gesamt, string nomen) =>
        DashboardTexte.DeInt(treffer) + " von " + DashboardTexte.DeInt(gesamt) + " " + nomen;

    public static string SeiteText(int seite, int seiten) => Beschriftungen.Einsetzen(Seite, seite + 1, seiten);
}

/// <summary>Phase 4, W1: Texte des Testprojekts (ProjectView, app_gui 4375-4585), PC-Wortlaut.</summary>
public static class TestprojektTexte
{
    public const string ListeTitel = "Testprojekte";                               // 4394
    public const string AnzahlAuftraege = "%s Kundenaufträge";                     // 4395
    public const string Kundenauftrag = "Kundenauftrag";                           // 4436
    public const string Loesungsansaetze = "Lösungsansätze";                       // 4443
    public const string Anzeigen = "Lösungsansätze anzeigen";                      // 4451
    public const string Ausblenden = "Lösungsansätze ausblenden";                  // 4564
    public const string Naechstes = "Nächstes Projekt";                            // 4454
    public const string NochAusgeblendet = "Die Lösungsansätze sind noch ausgeblendet.\n\nBearbeite den Projektantrag zuerst selbst - Ist-Analyse, Konzept, Zeit- und Kostenplanung, Risiken - und decke die Lösungsansätze anschließend zum Vergleich auf."; // 4546
    public const string Ausgeblendet = "Die Lösungsansätze sind ausgeblendet.";    // 4559
    public const string HinweisVorweg = "Diese Hinweise ersetzen keine eigene Bearbeitung - nutze sie zum Vergleich, nachdem du deinen eigenen Projektantrag geschrieben hast."; // 4522

    /// <summary>Listen-Untertitel "%s · %s" (Schwierigkeit · Fachbereich kurz, 4406).</summary>
    public static string Unterzeile(Lerninhalt projekt) =>
        (projekt.Text("schwierigkeit") ?? "") + " · " + Fachbereiche.Kurzname.GetValueOrDefault(projekt.Fachbereich ?? "", "");

    /// <summary>"%s  ·  Schwierigkeit: %s  ·  Fachbereich: %s" (4540, Ausnahme Einfachleerzeichen Nr. 1).</summary>
    public static string Kopfzeile(Lerninhalt projekt) =>
        (projekt.Text("branche") ?? "") + "  ·  Schwierigkeit: " + (projekt.Text("schwierigkeit") ?? "") + "  ·  Fachbereich: "
        + Fachbereiche.Kurzname.GetValueOrDefault(projekt.Fachbereich ?? "", "");

    /// <summary>_task_text (4507-4517).</summary>
    public static string Aufgabe(Lerninhalt projekt)
    {
        var zeilen = new List<string>
        {
            "AUSGANGSSITUATION", projekt.Text("ausgangssituation") ?? "", "",
            "AUFTRAG", projekt.Text("auftrag") ?? "", "",
            "RAHMENBEDINGUNGEN",
        };
        zeilen.AddRange(projekt.Liste("rahmenbedingungen").Select(r => "  - " + r));
        zeilen.Add("");
        zeilen.Add("IHRE AUFGABEN (Projektantrag)");
        var aufgaben = projekt.Liste("aufgaben");
        for (var i = 0; i < aufgaben.Count; i++)
        {
            zeilen.Add("  " + (i + 1).ToString(CultureInfo.InvariantCulture) + ". " + aufgaben[i]);
        }
        return string.Join("\n", zeilen);
    }

    /// <summary>_hint_text (4519-4531): Hinweis, dann je Aufgabe Nummer, Aufgabe, Loesungsansatz, Leerzeile; rstrip.</summary>
    public static string Hinweise(Lerninhalt projekt)
    {
        var zeilen = new List<string> { HinweisVorweg, "" };
        var aufgaben = projekt.Liste("aufgaben");
        var hinweise = projekt.Liste("hinweise");
        for (var i = 0; i < Math.Min(aufgaben.Count, hinweise.Count); i++)
        {
            zeilen.Add((i + 1).ToString(CultureInfo.InvariantCulture) + ". " + aufgaben[i]);
            zeilen.Add("   Lösungsansatz: " + hinweise[i]);
            zeilen.Add("");
        }
        return Rechner.PythonZeichen.RStrip(string.Join("\n", zeilen));
    }
}

/// <summary>Phase 4, W1: Texte des Notizblocks (NotebookView, app_gui 5142-5489; fisi_core 2272-2530), PC-Wortlaut.</summary>
public static class NotizblockTexte
{
    public const string UebersichtTitel = "Lernstand je Bereich";                  // 5156
    public const string UebersichtUnter = "Abgeschlossen = zweimal hintereinander richtig"; // 5158
    public const string Suche = "SUCHE";                                           // 5173
    public const string SucheHinweis = "Stichwort suchen (Frage, Antwort, Thema)"; // fisi_core NOTEBOOK_SEARCH_HINT
    public const string KeineTrefferMuster = "Keine Treffer für „%s“. Versuch ein anderes Stichwort oder setze die Filter zurück."; // NOTEBOOK_NO_HITS
    public const string Fachbereich = "FACHBEREICH";                               // 5179
    public const string Bereich = "BEREICH";                                       // 5183
    public const string Thema = "THEMA";                                           // 5192
    public const string Status = "STATUS";                                         // 5199
    public const string Musterantworten = "MUSTERANTWORTEN";                       // 5208
    public const string Alle = "Alle";
    public const string Aus = "aus";
    public const string An = "an";
    public const string AusText = "Ausblenden";                                    // 5209
    public const string AnText = "Einblenden";                                     // 5209

    /// <summary>Status-Pillen (5199-5201): Zu ueben, Abgeschlossen, Offen.</summary>
    public static readonly IReadOnlyList<(string Wert, string Text)> StatusPillen =
        [(FragenStatus.Ueben, "Zu üben"), (FragenStatus.Fertig, "Abgeschlossen"), (FragenStatus.Offen, "Offen")];

    /// <summary>SOURCE_NAME (fisi_core 2272).</summary>
    public static readonly IReadOnlyDictionary<string, string> QuelleName = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Quellen.Karte] = "Karteikarte", [Quellen.Quiz] = "Prüfungsfrage", [Quellen.Ap1] = "AP1-Szenario",
        [Quellen.Ap2] = "AP2-Szenario", [Quellen.Projekt] = "Testprojekt",
    };

    /// <summary>SOURCE_PLURAL (fisi_core 2274).</summary>
    public static readonly IReadOnlyDictionary<string, string> QuelleMehrzahl = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Quellen.Karte] = "Karteikarten", [Quellen.Quiz] = "Prüfungsfragen", [Quellen.Ap1] = "AP1-Szenarien",
        [Quellen.Ap2] = "AP2-Szenarien", [Quellen.Projekt] = "Testprojekte",
    };

    /// <summary>SOURCE_COLOR_KEY (app_gui 5127).</summary>
    public static readonly IReadOnlyDictionary<string, string> QuelleFarbe = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Quellen.Karte] = "accent", [Quellen.Quiz] = "purple", [Quellen.Ap1] = "blue", [Quellen.Ap2] = "accent2", [Quellen.Projekt] = "orange",
    };

    /// <summary>NOTEBOOK_PAGE (app_gui 5129).</summary>
    public const int Seitengroesse = 12;

    /// <summary>Gekuerzte Musterantwort (5460: bis 1 200 Zeichen, PC-Wert auf beiden Geraeten, H7).</summary>
    public const int AntwortHoechstens = 1200;

    public static string ZuUeben(int anzahl) => DashboardTexte.DeInt(anzahl) + " zu üben";                          // 5348
    public static string Abgeschlossen(int fertig, int gesamt) => "Abgeschlossen " + DashboardTexte.DeInt(fertig) + " von " + DashboardTexte.DeInt(gesamt); // 5351
    public static string OffenText(int anzahl) => "Offen " + DashboardTexte.DeInt(anzahl);                         // 5358

    /// <summary>Verb der Uebungsknoepfe (5368): je Status-Pille.</summary>
    public static string UebenVerb(string status) => status switch
    {
        FragenStatus.Ueben => "üben",
        FragenStatus.Fertig => "wiederholen",
        _ => "lernen",
    };

    public static string KartenKnopf(int anzahl, string status) => DashboardTexte.Plural(anzahl, "Karteikarte", "Karteikarten") + " " + UebenVerb(status);     // 5379
    public static string FragenKnopf(int anzahl, string status) => DashboardTexte.Plural(anzahl, "Prüfungsfrage", "Prüfungsfragen") + " " + UebenVerb(status); // 5384
    public const string UebenHinweis = "Startet eine Übungsrunde nur mit den Fragen der Liste (Prüfungstrainer: höchstens 50)."; // 5387
    public const string HinweisTitel = "Notizblock";                               // 5393
    public const string KeineFragenDazu = "In dieser Auswahl gibt es dazu keine Fragen."; // 5393 (H9: Hinweisfenster auf beiden Geraeten)

    /// <summary>Titel der Liste: Q_STATUS_NAME in Grossschrift (5414).</summary>
    public static string ListeTitel(string status) => Beschriftungen.Gross(Listen.StatusName[status]);

    public static string ListeUnter(int anzahl) => DashboardTexte.Plural(anzahl, "Frage", "Fragen");                  // 5419
    public const string NichtsZuUeben = "Nichts zu üben – sehr gut! Falsch beantwortete Fragen landen automatisch hier."; // 5421
    public const string KeineFragen = "Keine Fragen in dieser Auswahl.";           // 5423

    /// <summary>Leertext der Liste (5420-5425).</summary>
    public static string Leer(string status, string anfrage, bool vorFilternDa)
    {
        var text = status == FragenStatus.Ueben ? NichtsZuUeben : KeineFragen;
        var stich = Rechner.PythonZeichen.Strip(anfrage);
        return stich.Length > 0 && vorFilternDa ? KeineTrefferMuster.Replace("%s", stich, StringComparison.Ordinal) : text;
    }

    /// <summary>Kopf eines Eintrags "%s  ·  %s%s" (5448, Ausnahme Einfachleerzeichen Nr. 2).</summary>
    public static string Kopf(string quelle, Lerninhalt eintrag, string themaKurz) =>
        QuelleName[quelle] + "  ·  " + Fachbereiche.Kurzname.GetValueOrDefault(eintrag.Fachbereich ?? "", "")
        + (themaKurz.Length > 0 ? "  ·  " + themaKurz : "");

    /// <summary>Titel eines Eintrags; Szenarien und Testprojekte mit Nummer (5452-5454).</summary>
    public static string EintragTitel(NotizEintrag eintrag) =>
        eintrag.Quelle is Quellen.Ap1 or Quellen.Ap2 or Quellen.Projekt
            ? (Convert.ToInt64(eintrag.Schluessel, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture) + ". " + eintrag.Titel
            : eintrag.Titel;

    /// <summary>Musterantwort, ab 1 200 Zeichen am letzten Leerzeichen gekuerzt mit " …" (5460-5461).</summary>
    public static string Gekuerzt(string antwort)
    {
        if (Rechner.PythonZeichen.Laenge(antwort) <= AntwortHoechstens)
        {
            return antwort;
        }
        var teil = Rechner.PythonZeichen.Teil(antwort, 0, AntwortHoechstens);
        var leer = teil.LastIndexOf(' ');
        return (leer >= 0 ? teil[..leer] : teil) + " …";
    }

    public const string AntwortZeigen = "Musterantwort zeigen";                    // 5467
    public const string AntwortAusblenden = "Musterantwort ausblenden";            // 5466
    public const string JetztUeben = "Jetzt üben";                                 // 5469
}
