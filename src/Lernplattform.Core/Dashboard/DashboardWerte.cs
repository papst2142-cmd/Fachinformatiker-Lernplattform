using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Mathe;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Dashboard;

/// <summary>Zustand der Lernstand-Datenbank, aus der das Dashboard gelesen hat.</summary>
public enum DashboardZustand
{
    /// <summary>Datenbank lesbar, aber ohne Lern-Ereignisse.</summary>
    Leer,

    /// <summary>Datenbank lesbar, mit Lern-Ereignissen.</summary>
    Daten,

    /// <summary>Keine Datei vorhanden (es wird keine angelegt).</summary>
    Fehlt,

    /// <summary>Datei nicht lesbar oder mindestens eine Abfrage gescheitert (Werte dann wie Python: Vorgabewerte je Abfrage).</summary>
    Kaputt,
}

/// <summary>Banner oben (PC: GradientPanel, Handy: HeroPanel).</summary>
public sealed record DashboardBanner(string Titel, string TextPc, string TextHandy, string Gross, string GrossUnter);

/// <summary>Kachel "Heute": Tagesziel (DailyGoal).</summary>
public sealed record DashboardTagesziel(bool Sichtbar, long Erledigt, int Ziel, bool Erreicht, double Anteil, string Text, string RingText);

/// <summary>Kachel "Heute": faellige Wiederholungen (ReviewPlan, due_text).</summary>
public sealed record DashboardFaellig(int Karten, int Fragen, string Text, bool Wiederholen, int AltbestandWartet, string? ErsteQuelle);

/// <summary>Ring (Reihe 1) und Balken (Abdeckung) einer Inhaltsart.</summary>
public sealed record DashboardKennzahl(string Titel, long Wert, int Gesamt, double Anteil, string Gross, string Unter, double BalkenProzent, string BalkenText);

/// <summary>Kachel "Erfolgsquote" (quiz_success_rate).</summary>
public sealed record DashboardQuote(double Wert, double Richtig, long Antworten, string Text, string Unter);

/// <summary>Ein Tag im Lernverlauf (daily_counts).</summary>
public sealed record DashboardVerlaufTag(DateOnly Tag, string Beschriftung, long Anzahl);

/// <summary>Ring eines Fachbereichs (category_coverage, category_stats).</summary>
public sealed record DashboardFachbereich(string Name, string Kurz, double Abdeckung, string RingText, string RingTextHandy, long Antworten, double Richtig, string Detail);

/// <summary>Eine Zeile "Aktivitaeten" (recent_activities) mit den Zeilen am PC und am Handy.</summary>
public sealed record DashboardAktivitaet(string Zeit, string Art, string Detail, string Extra, string Pc1, string Pc2, string Handy1, string Handy2);

/// <summary>Monat im Kalender "Lerntage": Wochen ab Montag, 0 = kein Tag dieses Monats.</summary>
public sealed record DashboardKalender(int Jahr, int Monat, string Titel, IReadOnlyList<int> Lerntage, IReadOnlyList<IReadOnlyList<int>> Wochen);

/// <summary>Ring eines Pruefungsthemas (theme_progress).</summary>
public sealed record DashboardThema(string Name, double Prozent, string RingText);

/// <summary>Ein Thema im Reinzoom: Heatmap der letzten Tage, Wissensstand (fisi_game.topic_knowledge), Antworten.</summary>
public sealed record DashboardZoomThema(string Id, string Name, string Kurz, IReadOnlyList<long> Heatmap, double Wissen, long Antworten, double Richtig, string Notiz);

/// <summary>Reinzoom in die Themen eines Fachbereichs.</summary>
public sealed record DashboardReinzoom(string Fachbereich, string Titel, IReadOnlyList<DashboardZoomThema> Themen);

/// <summary>Feste Texte des Dashboards, genau wie Python (fisi_lernen DASH_*, app_gui.DashboardView, mobile DashboardScreen).</summary>
public static class DashboardTexte
{
    /// <summary>
    /// Karte oben bei <see cref="DashboardZustand.Kaputt"/> (Weisung P1-4, N1; neu in C#,
    /// Python bricht hier ab). Satz 2 gilt, weil das Dashboard nur lesend oeffnet.
    /// </summary>
    public const string LernstandNichtLesbar = "Der Lernstand konnte nicht gelesen werden. Er wurde nicht verändert.";

    /// <summary>Hinweiskarte zum Zustand: nur bei Kaputt, nie bei Fehlt, Leer oder Daten.</summary>
    public static string? Hinweis(DashboardZustand zustand) => zustand == DashboardZustand.Kaputt ? LernstandNichtLesbar : null;

    public const string Gelernt = "%s von %s Karten und Fragen bearbeitet";        // DASH_LEARNED
    public const string Rate = "Prüfungstrainer %d %% richtig";                     // DASH_RATE
    public const string Gesamt = "Gesamtfortschritt";                               // DASH_TOTAL
    public const string RingKarten = "Karteikarten";                                // DASH_RING_CARDS
    public const string RingQuiz = "Prüfungstrainer";                               // DASH_RING_QUIZ
    public const string RingAp1 = "AP1-Szenarien";                                  // DASH_RING_AP1
    public const string RingAp2 = "AP2-Szenarien";                                  // DASH_RING_AP2
    public const string KartenUnter = "von %s Karten bearbeitet";                   // DASH_CARDS_SUB
    public const string QuizUnter = "von %s Fragen beantwortet";                    // DASH_QUIZ_SUB
    public const string SzenarienUnter = "von %s bearbeitet";                       // DASH_SCEN_SUB
    public const string QuoteTitel = "Erfolgsquote";                                // DASH_QUOTE_TITLE
    public const string QuoteUntertitel = "Prüfungstrainer gesamt";                 // DASH_QUOTE_SUBTITLE
    public const string QuoteLeer = "noch keine Antworten erfasst";                 // DASH_QUOTE_EMPTY
    public const string SeitenleisteBalken = "Karten und Fragen bearbeitet";        // DASH_SIDEBAR_BAR
    public const string VerlaufSerie = "Lernaktivitäten pro Tag";                   // LEARN_CHART_SERIES

    public const string HeuteTitel = "Heute";
    public const string HeuteUnter = "Tagesziel und Wiederholung";
    public const string Wiederholen = "Jetzt wiederholen";
    public const string VerlaufTitel = "Lernverlauf";
    public const string VerlaufUnter = "letzte %d Tage";
    public const string AbdeckungTitel = "Abdeckung";
    public const string AbdeckungUnter = "Material";
    public const string FachTitel = "Fortschritt je Fachbereich";
    public const string FachUnter = "Auswahl zeigt die Themen";
    public const string FachDetail = "%s\n%d %% richtig";
    public const string FachDetailLeer = "noch nicht\nbearbeitet";
    public const string AktivitaetenTitel = "Aktivitäten";
    public const string AktivitaetenUnter = "zuletzt";
    public const string AktivitaetenLeerPc = "Noch keine Aktivitäten.\nStarte mit den Karteikarten\noder dem Prüfungstrainer.";
    public const string AktivitaetenLeerHandy = "Noch keine Aktivitäten. Starte mit den Karteikarten oder dem Prüfungstrainer.";
    public const string KalenderTitel = "Lerntage";
    public const string KalenderUnter = "Monatsübersicht";
    public const string Ap1Titel = "AP1 Prüfungsthemen";
    public const string Ap1Unter = "bearbeitete Grundlagenaufgaben";
    public const string Ap2Titel = "AP2 Prüfungsthemen";
    public const string Ap2Unter = "bearbeitete Szenarien";
    public const string ReinzoomTitel = "Themen · %s";
    public const string ReinzoomUnter = "Aktivität und Wissen";
    public const string ReinzoomAktivitaet = "Aktivität je Thema (letzte %d Tage)";
    public const string ReinzoomWissen = "Wissensstand je Thema (zählt für die Aufträge im Spiel)";
    public const string ReinzoomSchliessen = "Schließen";
    public const string ReinzoomNotiz = "Wissen %d %%  ·  %s";
    public const string ReinzoomLeer = "noch nicht bearbeitet";
    public const string BannerOhneName = "Dein Lernstand";
    public const string LernseriePc = "Lernserie: %s   ·   ";
    public const string LernserieHandy = "Lernserie: %s  ·  ";
    public const string Begruessung = "Hallo %s";                                   // fisi_hilfe.GREETING
    public const int NameMax = 40;                                                  // fisi_hilfe.NAME_MAX

    public static readonly IReadOnlyList<string> Monate =
        ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"];

    /// <summary>fisi_core.plural ohne Zahlwort: "1 Tag", "2 Tage", "1.234 Antworten".</summary>
    public static string Plural(long anzahl, string einzahl, string mehrzahl) =>
        DeInt(anzahl) + " " + (anzahl == 1 ? einzahl : mehrzahl);

    /// <summary>fisi_core.de_int: ganze Zahl mit Tausenderpunkten.</summary>
    public static string DeInt(long wert) => wert.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

    /// <summary>fisi_hilfe.greeting: "Hallo &lt;Name&gt;", "" ohne Namen (Name ohne doppelten Leerraum, hoechstens 40 Zeichen).</summary>
    public static string Gruss(string? name)
    {
        var sauber = PyText.Teil(string.Join(' ', PyText.Woerter(name ?? "")), 0, NameMax);
        return sauber.Length > 0 ? Begruessung.Replace("%s", sauber, StringComparison.Ordinal) : "";
    }

    /// <summary>fisi_lernen.due_text.</summary>
    public static string FaelligText(int karten, int fragen)
    {
        if (karten == 0 && fragen == 0)
        {
            return "Heute ist nichts fällig";
        }
        var teile = new List<string>();
        if (karten != 0)
        {
            teile.Add(Inv($"{karten} Karteikarte{(karten == 1 ? "" : "n")}"));
        }
        if (fragen != 0)
        {
            teile.Add(Inv($"{fragen} Prüfungsfrage{(fragen == 1 ? "" : "n")}"));
        }
        return "Heute fällig: " + string.Join(" · ", teile);
    }

    /// <summary>fisi_lernen.DailyGoal.text.</summary>
    public static string ZielText(long erledigt, int ziel, bool erreicht) => erreicht
        ? Inv($"Tagesziel erreicht: {erledigt} von {ziel} Aufgaben")
        : Inv($"Tagesziel: {erledigt} von {ziel} Aufgaben");

    /// <summary>fisi_lernen.dash_quote_text.</summary>
    public static string QuoteText(double richtig, long antworten) => antworten == 0
        ? QuoteLeer
        : Inv($"{PyText.D(richtig)} von {Plural(antworten, "Antwort", "Antworten")} richtig");

    /// <summary>mobile/src/ui.keep_last_words: letztes Wort (mit Zeichen wie "%") per geschuetztem Leerzeichen an das davor.</summary>
    public static string LetzteWorteZusammen(string text)
    {
        var woerter = text.Split(' ');
        if (woerter.Length < 3)
        {
            return text;
        }
        var halten = 2;
        while (halten < woerter.Length - 1 && woerter[^halten].Length > 0 && !PyText.BeginntAlnum(woerter[^halten]))
        {
            halten++;
        }
        return string.Join(' ', woerter[..^halten]) + " " + string.Join(' ', woerter[^halten..]);
    }

    /// <summary>mobile/src/main.german_time: '2026-09-27 21:40:05' -> '27.09.2026 21:40'.</summary>
    public static string DeutscheZeit(object? zeitstempel)
    {
        var wert = PyText.Wahr(zeitstempel) ? PyText.Str(zeitstempel) : "";
        if (PyText.Laenge(wert) < 16)
        {
            return wert;
        }
        return PyText.Teil(wert, 8, 10) + "." + PyText.Teil(wert, 5, 7) + "." + PyText.Teil(wert, 0, 4) + " " + PyText.Teil(wert, 11, 16);
    }

    /// <summary>"%d %%" bzw. "%d %%" mit Pythons round().</summary>
    public static string Prozent(double wert, bool geschuetzt) =>
        PyText.D(Math.Round(wert, MidpointRounding.ToEven)) + (geschuetzt ? " %" : " %");

    internal static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Gesamtzahlen der Inhalte, die das Dashboard braucht (einmal aus den
/// Lerninhalten berechnet): content_totals, theme_totals, ap1_theme_totals,
/// Frage -> Thema (CARD_TOPIC, QUIZ_TOPIC), gueltige Fragen fuer die
/// Wiederholung, Inhalte fuer den Wissensstand im Spiel (game_items) und die
/// Parameter dafuer (spiel/balancing.json "wissen" + "wissen_thema").
/// </summary>
public sealed class DashboardInhalte
{
    public DashboardInhalte(Lerninhalte inhalte)
        : this(inhalte, File.ReadAllBytes(Path.Combine(inhalte.Ordner, "spiel", "balancing.json")))
    {
    }

    /// <summary>Wie oben, spiel/balancing.json kommt als Inhalt (App: aus dem Programm selbst).</summary>
    public DashboardInhalte(Lerninhalte inhalte, byte[] balancingJson)
    {
        Inhalte = inhalte;
        JeFachbereich = inhalte.InhalteJeFachbereich();
        Summe = JeFachbereich.Values.Sum();
        Ap2JeBlock = inhalte.Ap2SzenarienJeBlock();
        Ap1JeBlock = inhalte.Ap1SzenarienJeBlock();
        foreach (var karte in inhalte.Karteikarten)
        {
            KartenThema[karte.Frage!] = karte.Thema;
        }
        foreach (var frage in inhalte.Quizfragen)
        {
            FragenThema[frage.Frage!] = frage.Thema;
        }
        var balancing = JsonNode.Parse(balancingJson)!;
        var parameter = new Dictionary<string, JsonNode?>();
        foreach (var (name, wert) in balancing["wissen"]!.AsObject())
        {
            parameter[name] = wert;
        }
        if (balancing["wissen_thema"] is JsonObject thema)
        {
            foreach (var (name, wert) in thema)
            {
                if (!name.StartsWith('_'))
                {
                    parameter[name] = wert;
                }
            }
        }
        WissenFenster = (int)Zahl(parameter["letzte_antworten"]);
        WissenMindest = (int)Zahl(parameter["mindest_antworten"]);
        WissenVoll = Zahl(parameter["volle_abdeckung_prozent"]);
        WissenExponent = Zahl(parameter["abdeckung_exponent"]);
    }

    public Lerninhalte Inhalte { get; }

    public OrderedDictionary<string, int> JeFachbereich { get; }

    public int Summe { get; }

    public OrderedDictionary<string, int> Ap2JeBlock { get; }

    public OrderedDictionary<string, int> Ap1JeBlock { get; }

    internal Dictionary<string, string?> KartenThema { get; } = new(StringComparer.Ordinal);

    internal Dictionary<string, string?> FragenThema { get; } = new(StringComparer.Ordinal);

    internal int WissenFenster { get; }

    internal int WissenMindest { get; }

    internal double WissenVoll { get; }

    internal double WissenExponent { get; }

    private static double Zahl(JsonNode? wert) => wert!.GetValue<double>();
}

/// <summary>
/// Alle Zahlen und Texte des Dashboards (PC: app_gui.DashboardView, Handy:
/// mobile/src/main.DashboardScreen), nur lesend aus der Lernstand-Datenbank
/// berechnet, genau wie Python v0.63.2 (Vergleichswerte G13).
/// Die Datenbank wird nur lesend geoeffnet (Mode=ReadOnly, PRAGMA query_only);
/// eine fehlende Datei wird nicht angelegt. Fehlt die Datei oder ist sie nicht
/// lesbar, gibt es keine Ausnahme: Zustand Fehlt bzw. Kaputt, Werte wie bei
/// leerer Datenbank. Scheitert nur ein Teil der Abfragen (z. B. eine Tabelle
/// fehlt), liefert jede gescheiterte Abfrage wie in Python ihren Vorgabewert.
/// </summary>
public sealed record DashboardWerte(
    DashboardZustand Zustand,
    DashboardBanner Banner,
    long Bearbeitet,
    int InhalteGesamt,
    long Lernserie,
    DashboardTagesziel Tagesziel,
    DashboardFaellig Faellig,
    IReadOnlyList<DashboardKennzahl> Kennzahlen,
    DashboardQuote Quote,
    IReadOnlyList<DashboardVerlaufTag> Verlauf,
    IReadOnlyList<DashboardFachbereich> Fachbereiche,
    IReadOnlyList<DashboardAktivitaet> Aktivitaeten,
    DashboardKalender Kalender,
    IReadOnlyList<DashboardThema> Ap1Themen,
    IReadOnlyList<DashboardThema> Ap2Themen,
    IReadOnlyList<DashboardReinzoom> Reinzoom)
{
    /// <summary>Tage im Lernverlauf und im Reinzoom (DAYS).</summary>
    public const int Tage = 14;

    /// <summary>Zeilen "Aktivitaeten" (recent_activities(7), ACTIVITIES).</summary>
    public const int AnzahlAktivitaeten = 7;

    /// <summary>Beginn des Wiederholungssystems (fisi_lernen.SR_START).</summary>
    public static readonly DateOnly WiederholungStart = new(2026, 10, 1);

    private static readonly int[] Abstaende = [1, 3, 7, 14, 30];      // SR_INTERVALS
    private const int AltbestandJeTag = 20;                           // SR_LEGACY_PER_DAY
    internal const string QuelleKarte = "karte";                      // SRC_CARD
    internal const string QuelleQuiz = "quiz";                        // SRC_QUIZ

    // Tabellen mit Lern-Ereignissen (Zustand Leer, wenn alle leer sind)
    private static readonly string[] Ereignistabellen =
        ["card_events", "quiz_answers", "scenario_events", "project_events", "ap1_events", "trainer_aufgaben", "test_results"];

    /// <summary>
    /// Dashboard lesen. heute: Stichtag in Ortszeit (Python: datetime.date.today()),
    /// ziel: Tagesziel-Einstellungen (Einstellungen.Ziel), nutzername: Name aus
    /// "Jetzt einrichten" (leer = "Dein Lernstand").
    /// </summary>
    public static DashboardWerte Lesen(string dbPfad, DateOnly heute, DashboardInhalte inhalte,
        Einstellungen.Tagesziel ziel, string? nutzername = null)
    {
        if (!File.Exists(dbPfad))
        {
            return Rechne(new Leser(null), heute, inhalte, ziel, nutzername, DashboardZustand.Fehlt);
        }
        try
        {
            using var leser = Leser.Oeffnen(dbPfad);
            return Rechne(leser, heute, inhalte, ziel, nutzername, null);
        }
        catch (Exception)
        {
            // Unlesbare Datei oder unerwartete Werte (Python braeche hier ab): wie leer
            return Rechne(new Leser(null), heute, inhalte, ziel, nutzername, DashboardZustand.Kaputt);
        }
    }

    /// <summary>Kalender "Lerntage" fuer einen anderen Monat (Blaettern); bei fehlender oder kaputter Datei ohne Lerntage.</summary>
    public static DashboardKalender Monat(string dbPfad, int jahr, int monat)
    {
        if (File.Exists(dbPfad))
        {
            try
            {
                using var leser = Leser.Oeffnen(dbPfad);
                return LeseKalender(leser, jahr, monat);
            }
            catch (Exception)
            {
                // wie leer
            }
        }
        return LeseKalender(new Leser(null), jahr, monat);
    }

    private static DashboardWerte Rechne(Leser db, DateOnly heute, DashboardInhalte inhalte,
        Einstellungen.Tagesziel ziel, string? nutzername, DashboardZustand? zustand)
    {
        var lern = inhalte.Inhalte;
        int gesamtKarten = lern.Karteikarten.Count, gesamtQuiz = lern.Quizfragen.Count;
        int gesamtAp1 = lern.Ap1Szenarien.Count, gesamtAp2 = lern.Ap2Szenarien.Count;

        var karten = Anzahl(db, "SELECT COUNT(DISTINCT question) FROM card_events");
        var fragen = Anzahl(db, "SELECT COUNT(DISTINCT question) FROM quiz_answers");
        var ap1 = Anzahl(db, "SELECT COUNT(DISTINCT scenario_index) FROM ap1_events");
        var ap2 = Anzahl(db, "SELECT COUNT(DISTINCT scenario_index) FROM scenario_events");
        var (rate, richtig, beantwortet) = Erfolgsquote(db);
        var gelernt = karten + fragen;

        // Kachel "Heute"
        var tage = AktivitaetTage(db);
        var erledigt = tage.GetValueOrDefault(Iso(heute));
        var erreicht = erledigt >= ziel.ZielAnzahl;
        var anteil = ziel.ZielAnzahl != 0 ? Math.Min(1.0, (double)erledigt / ziel.ZielAnzahl) : 1.0;
        var tagesziel = new DashboardTagesziel(ziel.ZielAn, erledigt, ziel.ZielAnzahl, erreicht, anteil,
            DashboardTexte.ZielText(erledigt, ziel.ZielAnzahl, erreicht), DashboardTexte.Prozent(anteil * 100, true));
        var faellig = Wiederholung(db, heute, inhalte);

        // Banner
        var serie = Serie(tage, heute);
        string SerieText(string muster) => ziel.SerieAn
            ? muster.Replace("%s", DashboardTexte.Plural(serie, "Tag", "Tage"), StringComparison.Ordinal) : "";
        string BannerText(string serieText, string trenner) => serieText
            + DashboardTexte.Gelernt.Replace("%s von %s", DashboardTexte.DeInt(gelernt) + " von " + DashboardTexte.DeInt(inhalte.Summe), StringComparison.Ordinal)
            + trenner + DashboardTexte.Rate.Replace("%d %%", DashboardTexte.Prozent(rate, false), StringComparison.Ordinal);
        var name = DashboardTexte.Gruss(nutzername);
        var banner = new DashboardBanner(name.Length > 0 ? name : DashboardTexte.BannerOhneName,
            BannerText(SerieText(DashboardTexte.LernseriePc), "   ·   "),
            DashboardTexte.LetzteWorteZusammen(BannerText(SerieText(DashboardTexte.LernserieHandy), "  ·  ")),
            DashboardTexte.Prozent((double)gelernt / Math.Max(1, inhalte.Summe) * 100, false), DashboardTexte.Gesamt);

        var kennzahlen = new[]
        {
            Kennzahl(DashboardTexte.RingKarten, karten, gesamtKarten, DashboardTexte.KartenUnter),
            Kennzahl(DashboardTexte.RingQuiz, fragen, gesamtQuiz, DashboardTexte.QuizUnter),
            Kennzahl(DashboardTexte.RingAp1, ap1, gesamtAp1, DashboardTexte.SzenarienUnter),
            Kennzahl(DashboardTexte.RingAp2, ap2, gesamtAp2, DashboardTexte.SzenarienUnter),
        };
        var quote = new DashboardQuote(rate, richtig, beantwortet, DashboardTexte.Prozent(rate, false),
            DashboardTexte.QuoteText(richtig, beantwortet));

        var werte = new DashboardWerte(
            DashboardZustand.Daten, banner, gelernt, inhalte.Summe, serie, tagesziel, faellig, kennzahlen, quote,
            LeseVerlauf(db, heute), LeseFachbereiche(db, inhalte), LeseAktivitaeten(db), LeseKalender(db, heute.Year, heute.Month),
            LeseThemen(db, "ap1_events", inhalte.Ap1JeBlock, Inhalte.Fachbereiche.Ap1Themen),
            LeseThemen(db, "scenario_events", inhalte.Ap2JeBlock, Inhalte.Fachbereiche.Ap2Themen),
            LeseReinzoom(db, heute, inhalte));
        return werte with { Zustand = zustand ?? (db.Fehler ? DashboardZustand.Kaputt : IstLeer(db) ? DashboardZustand.Leer : DashboardZustand.Daten) };
    }

    private static bool IstLeer(Leser db)
    {
        var sql = "SELECT " + string.Join(" + ", Ereignistabellen.Select(t => $"(SELECT COUNT(*) FROM {t})"));
        var zeilen = db.Abfrage(sql);
        return zeilen is not null && Convert.ToInt64(zeilen[0][0], CultureInfo.InvariantCulture) == 0;
    }

    private static DashboardKennzahl Kennzahl(string titel, long wert, int gesamt, string unter) => new(
        titel, wert, gesamt, (double)wert / Math.Max(1, gesamt), DashboardTexte.DeInt(wert),
        unter.Replace("%s", DashboardTexte.DeInt(gesamt), StringComparison.Ordinal),
        (double)wert / Math.Max(1, gesamt) * 100, DashboardTexte.DeInt(wert) + " / " + DashboardTexte.DeInt(gesamt));

    // -- Abfragen wie fisi_core.DBManager (gleiche SQL-Texte) ----------------

    private static long Anzahl(Leser db, string sql)
    {
        var zeilen = db.Abfrage(sql);
        return zeilen is { Count: > 0 } && zeilen[0][0] is long n ? n : 0;
    }

    private static (double Rate, double Richtig, long Gesamt) Erfolgsquote(Leser db)
    {
        var zeilen = db.Abfrage("SELECT COUNT(*), SUM(correct) FROM quiz_answers");
        if (zeilen is not { Count: > 0 } || zeilen[0][0] is not long gesamt || gesamt == 0)
        {
            return (0.0, 0, 0);
        }
        var richtig = PyText.Zahl(zeilen[0][1]);
        return (richtig / gesamt * 100.0, richtig, gesamt);
    }

    /// <summary>DBManager.activity_days: {tag: bewertete Aufgaben}.</summary>
    private static Dictionary<string, long> AktivitaetTage(Leser db)
    {
        var zeilen = db.Abfrage(
            "SELECT substr(timestamp, 1, 10) AS tag, COUNT(*) FROM ("
            + "  SELECT timestamp FROM card_events WHERE correct IS NOT NULL"
            + "  UNION ALL SELECT timestamp FROM quiz_answers"
            + "  UNION ALL SELECT timestamp FROM scenario_events WHERE correct IS NOT NULL"
            + "  UNION ALL SELECT timestamp FROM project_events WHERE correct IS NOT NULL"
            + "  UNION ALL SELECT timestamp FROM ap1_events WHERE correct IS NOT NULL"
            + "  UNION ALL SELECT timestamp FROM trainer_aufgaben"
            + ") GROUP BY tag") ?? [];
        var tage = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var zeile in zeilen)
        {
            if (zeile[0] is string tag)
            {
                tage[tag] = (long)zeile[1]!;
            }
        }
        return tage;
    }

    /// <summary>fisi_core.learning_streak (DBManager.streak).</summary>
    private static long Serie(Dictionary<string, long> tage, DateOnly heute)
    {
        bool Gelernt(DateOnly tag) => tage.GetValueOrDefault(Iso(tag)) != 0;
        var tag = Gelernt(heute) ? heute : heute.AddDays(-1);
        long anzahl = 0;
        while (Gelernt(tag))
        {
            anzahl++;
            tag = tag.AddDays(-1);
        }
        return anzahl;
    }

    private static List<DashboardVerlaufTag> LeseVerlauf(Leser db, DateOnly heute)
    {
        var start = heute.AddDays(-(Tage - 1));
        var zeilen = db.Abfrage(
            "SELECT substr(timestamp, 1, 10) AS tag, COUNT(*) FROM ("
            + "  SELECT timestamp FROM card_events"
            + "  UNION ALL SELECT timestamp FROM quiz_answers"
            + "  UNION ALL SELECT timestamp FROM scenario_events"
            + "  UNION ALL SELECT timestamp FROM project_events"
            + "  UNION ALL SELECT timestamp FROM ap1_events"
            + "  UNION ALL SELECT timestamp FROM trainer_aufgaben"
            + ") WHERE substr(timestamp, 1, 10) >= ?1 GROUP BY tag", Iso(start)) ?? [];
        var nach = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var zeile in zeilen)
        {
            if (zeile[0] is string tag)
            {
                nach[tag] = (long)zeile[1]!;
            }
        }
        return Enumerable.Range(0, Tage)
            .Select(i => start.AddDays(i))
            .Select(tag => new DashboardVerlaufTag(tag, tag.ToString("dd.MM", CultureInfo.InvariantCulture), nach.GetValueOrDefault(Iso(tag))))
            .ToList();
    }

    private static List<DashboardFachbereich> LeseFachbereiche(Leser db, DashboardInhalte inhalte)
    {
        // category_coverage
        var gesehen = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var zeile in db.Abfrage(
            "SELECT category, COUNT(DISTINCT question) FROM ("
            + "  SELECT category, question FROM quiz_answers"
            + "  UNION"
            + "  SELECT category, question FROM card_events"
            + ") GROUP BY category") ?? [])
        {
            if (zeile[0] is string kategorie)
            {
                gesehen[kategorie] = (long)zeile[1]!;
            }
        }
        // category_stats
        var werte = new Dictionary<string, (long Antworten, double Richtig)>(StringComparer.Ordinal);
        foreach (var zeile in db.Abfrage(
            "SELECT category, COUNT(*), SUM(correct) FROM ("
            + "  SELECT category, correct FROM quiz_answers"
            + "  UNION ALL"
            + "  SELECT category, correct FROM card_events WHERE correct IS NOT NULL"
            + ") GROUP BY category") ?? [])
        {
            if (zeile[0] is string kategorie)
            {
                werte[kategorie] = (zeile[1] is long n ? n : 0, PyText.Zahl(zeile[2]));
            }
        }
        var ergebnis = new List<DashboardFachbereich>();
        foreach (var fachbereich in Inhalte.Fachbereiche.Alle)
        {
            var gesamt = Math.Max(1, inhalte.JeFachbereich.TryGetValue(fachbereich, out var t) ? t : 1);
            var abdeckung = Math.Min(100.0, (double)gesehen.GetValueOrDefault(fachbereich) / gesamt * 100.0);
            var (antworten, richtig) = werte.GetValueOrDefault(fachbereich);
            var detail = antworten != 0
                ? DashboardTexte.Plural(antworten, "Antwort", "Antworten") + "\n"
                    + PyText.D(Math.Round(richtig / antworten * 100, MidpointRounding.ToEven)) + " % richtig"
                : DashboardTexte.FachDetailLeer;
            ergebnis.Add(new DashboardFachbereich(fachbereich, Inhalte.Fachbereiche.Kurzname[fachbereich], abdeckung,
                DashboardTexte.Prozent(Math.Max(0.0, Math.Min(100.0, abdeckung)), true),
                DashboardTexte.Prozent(abdeckung, true), antworten, richtig, detail));
        }
        return ergebnis;
    }

    private static List<DashboardAktivitaet> LeseAktivitaeten(Leser db)
    {
        var zeilen = db.Abfrage(
            "SELECT timestamp, art, detail, extra FROM ("
            + "  SELECT timestamp, 'Karteikarte' AS art, category AS detail,"
            + "         CASE WHEN correct IS NULL THEN 'angesehen'"
            + "              WHEN correct = 1 THEN 'richtig' ELSE 'falsch' END AS extra"
            + "  FROM card_events"
            + "  UNION ALL"
            + "  SELECT timestamp, 'Prüfungsfrage', category,"
            + "         CASE WHEN correct = 1 THEN 'richtig' ELSE 'falsch' END"
            + "  FROM quiz_answers"
            + "  UNION ALL"
            + "  SELECT timestamp, 'AP2-Szenario', theme, 'bearbeitet'"
            + "  FROM scenario_events"
            + "  UNION ALL"
            + "  SELECT timestamp, 'Testprojekt', category, 'bearbeitet'"
            + "  FROM project_events"
            + "  UNION ALL"
            + "  SELECT timestamp, 'AP1-Szenario', theme, 'bearbeitet'"
            + "  FROM ap1_events"
            + "  UNION ALL"
            + "  SELECT timestamp, 'Test-Session', note,"
            + "         CAST(score AS TEXT) || ' / ' || CAST(total AS TEXT)"
            + "  FROM test_results"
            + ") ORDER BY timestamp DESC LIMIT ?1", (long)AnzahlAktivitaeten) ?? [];
        var ergebnis = new List<DashboardAktivitaet>();
        foreach (var zeile in zeilen)
        {
            var zeit = PyText.Str(zeile[0]);
            var art = PyText.Str(zeile[1]);
            var detail = PyText.Str(zeile[2]);
            var extra = PyText.Str(zeile[3]);
            // PC: DashboardView._activity_row (Detail auf 34 Zeichen gekuerzt)
            var kurz = PyText.Laenge(detail) <= 34 ? detail : PyText.Teil(detail, 0, 32) + "…";
            ergebnis.Add(new DashboardAktivitaet(zeit, art, detail, extra,
                art + " · " + kurz,
                PyText.Teil(zeit, 0, 16).Replace('-', '.') + "  ·  " + extra,
                art + " · " + detail,
                DashboardTexte.DeutscheZeit(zeile[0]) + "  ·  " + extra));
        }
        return ergebnis;
    }

    private static DashboardKalender LeseKalender(Leser db, int jahr, int monat)
    {
        // DBManager.month_activity
        var zeilen = db.Abfrage(
            "SELECT DISTINCT substr(timestamp, 1, 10) FROM ("
            + "  SELECT timestamp FROM card_events"
            + "  UNION ALL SELECT timestamp FROM quiz_answers"
            + "  UNION ALL SELECT timestamp FROM scenario_events"
            + "  UNION ALL SELECT timestamp FROM project_events"
            + "  UNION ALL SELECT timestamp FROM ap1_events"
            + "  UNION ALL SELECT timestamp FROM test_results"
            + ") WHERE substr(timestamp, 1, 7) = ?1",
            jahr.ToString("0000", CultureInfo.InvariantCulture) + "-" + monat.ToString("00", CultureInfo.InvariantCulture)) ?? [];
        var tage = new SortedSet<int>();
        foreach (var zeile in zeilen)
        {
            if (zeile[0] is string wert && Einstellungen.PythonIntText(PyText.Teil(wert, 8, 10)) is { } tag)
            {
                tage.Add((int)tag);
            }
        }
        // calendar.Calendar(firstweekday=0).monthdayscalendar
        var wochen = new List<IReadOnlyList<int>>();
        var erster = new DateOnly(jahr, monat, 1);
        var woche = new int[7];
        var spalte = ((int)erster.DayOfWeek + 6) % 7;
        for (var tag = 1; tag <= DateTime.DaysInMonth(jahr, monat); tag++)
        {
            woche[spalte] = tag;
            if (++spalte == 7)
            {
                wochen.Add(woche);
                woche = new int[7];
                spalte = 0;
            }
        }
        if (spalte > 0)
        {
            wochen.Add(woche);
        }
        return new DashboardKalender(jahr, monat, DashboardTexte.Monate[monat - 1] + " " + jahr.ToString(CultureInfo.InvariantCulture),
            tage.ToList(), wochen);
    }

    /// <summary>DBManager.theme_progress (AP2: scenario_events, AP1: ap1_events).</summary>
    private static List<DashboardThema> LeseThemen(Leser db, string tabelle, OrderedDictionary<string, int> gesamt, IReadOnlyList<string> themen)
    {
        var erledigt = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var zeile in db.Abfrage($"SELECT theme, COUNT(DISTINCT scenario_index) FROM {tabelle} GROUP BY theme") ?? [])
        {
            if (zeile[0] is string thema)
            {
                var block = Inhalte.Fachbereiche.Block(thema)!;
                erledigt[block] = erledigt.GetValueOrDefault(block) + (long)zeile[1]!;
            }
        }
        return themen.Select(thema =>
        {
            var summe = Math.Max(1, gesamt.TryGetValue(thema, out var t) ? t : 1);
            var prozent = Math.Min(100.0, (double)erledigt.GetValueOrDefault(thema) / summe * 100.0);
            return new DashboardThema(thema, prozent, DashboardTexte.Prozent(prozent, true));
        }).ToList();
    }

    // -- Reinzoom: topic_daily, topic_stats, fisi_game.topic_knowledge -------

    private static List<DashboardReinzoom> LeseReinzoom(Leser db, DateOnly heute, DashboardInhalte inhalte)
    {
        var lern = inhalte.Inhalte;
        string? ThemaVon(object? quelle, object? frage) =>
            frage is string text && (PyText.Wahr(quelle) ? inhalte.KartenThema : inhalte.FragenThema).TryGetValue(text, out var thema) ? thema : null;

        // topic_stats
        var statistik = lern.ThemenReihenfolge.ToDictionary(t => t, _ => (Antworten: 0L, Richtig: 0.0));
        foreach (var zeile in db.Abfrage(
            "SELECT src, question, COUNT(*), SUM(correct) FROM ("
            + "  SELECT 0 AS src, question, correct FROM quiz_answers"
            + "  UNION ALL"
            + "  SELECT 1 AS src, question, correct FROM card_events WHERE correct IS NOT NULL"
            + ") GROUP BY src, question") ?? [])
        {
            if (ThemaVon(zeile[0], zeile[1]) is { } thema && statistik.TryGetValue(thema, out var bisher))
            {
                statistik[thema] = (bisher.Antworten + (long)zeile[2]!, bisher.Richtig + PyText.Zahl(zeile[3]));
            }
        }
        var wissen = Wissensstand(db, inhalte, ThemaVon);

        var start = heute.AddDays(-(Tage - 1));
        var ergebnis = new List<DashboardReinzoom>();
        foreach (var fachbereich in Inhalte.Fachbereiche.Alle)
        {
            var themen = lern.Themen.TryGetValue(fachbereich, out var liste) ? liste : [];
            var matrix = themen.ToDictionary(t => t.Id, _ => new long[Tage]);
            foreach (var zeile in db.Abfrage(
                "SELECT src, question, substr(timestamp, 1, 10) AS tag, COUNT(*) FROM ("
                + "  SELECT 1 AS src, question, category, timestamp FROM card_events"
                + "  UNION ALL SELECT 0, question, category, timestamp FROM quiz_answers"
                + ") WHERE category = ?1 AND substr(timestamp, 1, 10) >= ?2"
                + " GROUP BY src, question, tag", fachbereich, Iso(start)) ?? [])
            {
                if (ThemaVon(zeile[0], zeile[1]) is not { } thema || !matrix.TryGetValue(thema, out var reihe)
                    || zeile[2] is not string tag || !PyText.IsoTag(tag, out var datum))
                {
                    continue;
                }
                var index = datum.DayNumber - start.DayNumber;
                if (index is >= 0 and < Tage)
                {
                    reihe[index] += (long)zeile[3]!;
                }
            }
            ergebnis.Add(new DashboardReinzoom(fachbereich,
                DashboardTexte.ReinzoomTitel.Replace("%s", fachbereich, StringComparison.Ordinal),
                themen.Select(t =>
                {
                    var (antworten, richtig) = statistik[t.Id];
                    var stand = wissen[t.Id];
                    var notiz = antworten != 0
                        ? "Wissen " + PyText.D(stand) + " %  ·  " + DashboardTexte.Plural(antworten, "Antwort", "Antworten")
                        : DashboardTexte.ReinzoomLeer;
                    return new DashboardZoomThema(t.Id, t.Name, t.Kurz, matrix[t.Id], stand, antworten, richtig, notiz);
                }).ToList()));
        }
        return ergebnis;
    }

    /// <summary>fisi_game.topic_knowledge: topic_answers, topic_coverage(game_topic_totals), knowledge_from_answers.</summary>
    private static Dictionary<string, double> Wissensstand(Leser db, DashboardInhalte inhalte, Func<object?, object?, string?> themaVon)
    {
        var lern = inhalte.Inhalte;
        var fenster = Math.Max(1, inhalte.WissenFenster);

        // topic_answers (neueste zuerst)
        var antworten = new Dictionary<string, List<bool>>(StringComparer.Ordinal);
        foreach (var zeile in db.Abfrage(
            "SELECT src, question, correct FROM ("
            + "  SELECT timestamp, id, 0 AS src, question, correct FROM quiz_answers"
            + "  UNION ALL"
            + "  SELECT timestamp, id, 1 AS src, question, correct FROM card_events"
            + "  WHERE correct IS NOT NULL"
            + ") ORDER BY timestamp DESC, id DESC") ?? [])
        {
            if (themaVon(zeile[0], zeile[1]) is not { } thema)
            {
                continue;
            }
            if (!antworten.TryGetValue(thema, out var eimer))
            {
                antworten[thema] = eimer = [];
            }
            if (eimer.Count < fenster)
            {
                eimer.Add(PyText.Wahr(zeile[2]));
            }
        }

        // game_items / game_topic_totals: neue Inhalte (neu_ab) erst, wenn bearbeitet
        HashSet<string> Bearbeitet(string tabelle, IReadOnlyList<Lerninhalt> eintraege)
        {
            var menge = new HashSet<string>(StringComparer.Ordinal);
            if (eintraege.Any(Neu))
            {
                foreach (var zeile in db.Abfrage($"SELECT DISTINCT question FROM {tabelle}") ?? [])
                {
                    if (zeile[0] is string frage)
                    {
                        menge.Add(frage);
                    }
                }
            }
            return menge;
        }
        var karten = Bearbeitet("card_events", lern.Karteikarten);
        var fragen = Bearbeitet("quiz_answers", lern.Quizfragen);
        var summen = lern.ThemenReihenfolge.ToDictionary(t => t, _ => 0);
        foreach (var eintrag in lern.Karteikarten.Where(k => !Neu(k) || karten.Contains(k.Frage!))
                     .Concat(lern.Quizfragen.Where(f => !Neu(f) || fragen.Contains(f.Frage!))))
        {
            if (eintrag.Thema is { } thema && summen.ContainsKey(thema))
            {
                summen[thema]++;
            }
        }

        // topic_coverage
        var gesehen = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var zeile in db.Abfrage(
            "SELECT DISTINCT 0, question FROM quiz_answers"
            + " UNION SELECT DISTINCT 1, question FROM card_events") ?? [])
        {
            if (themaVon(zeile[0], zeile[1]) is { } thema)
            {
                gesehen[thema] = gesehen.GetValueOrDefault(thema) + 1;
            }
        }

        // knowledge_from_answers
        var mindest = Math.Max(1, inhalte.WissenMindest);
        var voll = Math.Max(1.0, inhalte.WissenVoll);
        var ergebnis = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var thema in lern.ThemenReihenfolge)
        {
            var abdeckung = Math.Min(100.0, gesehen.GetValueOrDefault(thema) / (double)Math.Max(1, summen.GetValueOrDefault(thema, 1)) * 100.0);
            var juengste = antworten.TryGetValue(thema, out var liste) ? liste : [];
            double quote = 0.0;
            if (juengste.Count > 0)
            {
                long gewichtRichtig = 0, gewichtSumme = 0;
                for (var i = 0; i < juengste.Count; i++)
                {
                    var gewicht = juengste.Count - i;
                    gewichtSumme += gewicht;
                    if (juengste[i])
                    {
                        gewichtRichtig += gewicht;
                    }
                }
                quote = gewichtRichtig / (double)gewichtSumme;
                quote *= Math.Min(1.0, juengste.Count / (double)mindest);
            }
            var anteil = Math.Min(1.0, Math.Max(0.0, abdeckung) / voll);
            ergebnis[thema] = PyText.Runde1(100.0 * quote * PythonPow.Pow(anteil, inhalte.WissenExponent));
        }
        return ergebnis;
    }

    private static bool Neu(Lerninhalt eintrag) => Einstellungen.PythonBool(eintrag.Daten["neu_ab"]);

    // -- Wiederholung (fisi_lernen.ReviewPlan, review_info, review_state) ----

    private static DashboardFaellig Wiederholung(Leser db, DateOnly heute, DashboardInhalte inhalte)
    {
        var lern = inhalte.Inhalte;
        var gueltig = new Dictionary<string, HashSet<string>>
        {
            [QuelleKarte] = lern.Karteikarten.Select(k => k.Frage!).ToHashSet(StringComparer.Ordinal),
            [QuelleQuiz] = lern.Quizfragen.Select(f => f.Frage!).ToHashSet(StringComparer.Ordinal),
        };
        // question_results (nur Karteikarten und Pruefungstrainer), aelteste zuerst
        var ergebnisse = new List<(string Quelle, string Frage, List<(object? Zeit, bool? Wert)> Eintraege)>();
        foreach (var (quelle, tabelle) in new[] { (QuelleKarte, "card_events"), (QuelleQuiz, "quiz_answers") })
        {
            var nach = new Dictionary<string, List<(object?, bool?)>>(StringComparer.Ordinal);
            foreach (var zeile in db.Abfrage($"SELECT timestamp, question, correct FROM {tabelle} ORDER BY timestamp, id") ?? [])
            {
                if (zeile[1] is not string frage || !gueltig[quelle].Contains(frage))
                {
                    continue;
                }
                if (!nach.TryGetValue(frage, out var eintraege))
                {
                    nach[frage] = eintraege = [];
                    ergebnisse.Add((quelle, frage, eintraege));
                }
                eintraege.Add((zeile[0], zeile[2] is null ? null : PyText.Wahr(zeile[2])));
            }
        }

        var start = WiederholungStart;
        var regulaer = new List<string>();
        var altbestand = new List<(string Quelle, string Frage, List<(object? Zeit, bool? Wert)> Eintraege)>();
        var heuteUmgestellt = 0;
        foreach (var eintrag in ergebnisse)
        {
            var bewertet = eintrag.Eintraege.Where(e => e.Wert is not null).ToList();
            if (bewertet.Count == 0)
            {
                continue;
            }
            int stufe = 0;
            DateOnly? faellig = null;
            var tage = new List<DateOnly>();
            foreach (var (zeit, wert) in bewertet)
            {
                var tag = Datum(zeit);
                tage.Add(tag);
                if (wert == false)
                {
                    (stufe, faellig) = (0, tag.AddDays(1));
                }
                else if (faellig is null || tag >= faellig)
                {
                    stufe = Math.Min(stufe + 1, Abstaende.Length);
                    faellig = tag.AddDays(Abstaende[stufe - 1]);
                }
            }
            var alt = tage.Any(t => t < start);
            DateOnly? ersterNeuer = tage.Where(t => t >= start).Select(t => (DateOnly?)t).FirstOrDefault();
            if (alt && ersterNeuer is null)
            {
                altbestand.Add(eintrag);
                continue;
            }
            if (alt && ersterNeuer == heute)
            {
                heuteUmgestellt++;
            }
            if (faellig <= heute)
            {
                regulaer.Add(eintrag.Quelle);
            }
        }
        var freigegeben = new List<string>();
        if (heute >= start)
        {
            freigegeben = altbestand
                .Select(e => (e.Quelle, Rang: AltbestandRang(e.Eintraege), Md5: StabilerRang(e.Quelle, e.Frage)))
                .OrderBy(e => e.Rang).ThenBy(e => e.Md5, StringComparer.Ordinal)
                .Take(Math.Max(0, AltbestandJeTag - heuteUmgestellt))
                .Select(e => e.Quelle)
                .ToList();
        }
        var alle = regulaer.Concat(freigegeben).ToList();
        int karten = alle.Count(q => q == QuelleKarte), fragen = alle.Count(q => q == QuelleQuiz);
        return new DashboardFaellig(karten, fragen, DashboardTexte.FaelligText(karten, fragen), karten + fragen > 0,
            altbestand.Count - freigegeben.Count, karten > 0 ? QuelleKarte : fragen > 0 ? QuelleQuiz : null);
    }

    /// <summary>legacy_rank: zu ueben rot 0, zu ueben gelb 1, offen 2, abgeschlossen 3 (fisi_core.question_status).</summary>
    private static int AltbestandRang(List<(object? Zeit, bool? Wert)> eintraege)
    {
        var werte = eintraege.Where(e => e.Wert is not null).Select(e => e.Wert!.Value).ToList();
        if (werte.Count >= 2 && werte[^1] && werte[^2])
        {
            return 3;
        }
        if (werte.Count > 0 && !werte[^1])
        {
            return 0;
        }
        return werte.Contains(false) ? 1 : 2;
    }

    /// <summary>fisi_lernen._stable_rank: MD5 nur fuer eine feste Reihenfolge, nicht fuer Sicherheit.</summary>
#pragma warning disable CA5351 // MD5 nur als feste Reihenfolge wie in Python, keine Sicherheitsfunktion
    private static string StabilerRang(string quelle, string frage) =>
        Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(quelle + "|" + frage)));
#pragma warning restore CA5351

    /// <summary>fisi_lernen._date: date.fromisoformat(str(timestamp)[:10]); anderes als JJJJ-MM-TT bricht ab (Python ebenso, siehe Lesen).</summary>
    private static DateOnly Datum(object? zeit) =>
        PyText.IsoTag(PyText.Teil(PyText.Str(zeit), 0, 10), out var tag) ? tag : throw new FormatException("Zeitstempel");

    private static string Iso(DateOnly tag) => tag.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // -- Lese-Verbindung -------------------------------------------------------

    /// <summary>Nur lesende Verbindung; jede gescheiterte Abfrage liefert null (Python: Vorgabewert) und merkt sich den Fehler.</summary>
    private sealed class Leser(SqliteConnection? verbindung) : IDisposable
    {
        public bool Fehler { get; private set; }

        public static Leser Oeffnen(string pfad)
        {
            var verbindung = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = pfad,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = Lernstand.Wartezeit,
            }.ToString());
            try
            {
                verbindung.Open();
                using var befehl = verbindung.CreateCommand();
                befehl.CommandText = "PRAGMA query_only = ON";
                befehl.ExecuteNonQuery();
            }
            catch
            {
                verbindung.Dispose();
                throw;
            }
            return new Leser(verbindung);
        }

        public List<object?[]>? Abfrage(string sql, params object[] werte)
        {
            if (verbindung is null)
            {
                return null;
            }
            try
            {
                using var befehl = verbindung.CreateCommand();
                befehl.CommandText = sql;
                for (var i = 0; i < werte.Length; i++)
                {
                    // Platzhalter ?1, ?2 ... (wie Pythons ?, Microsoft.Data.Sqlite braucht Namen)
                    var parameter = befehl.CreateParameter();
                    parameter.ParameterName = "?" + (i + 1).ToString(CultureInfo.InvariantCulture);
                    parameter.Value = werte[i];
                    befehl.Parameters.Add(parameter);
                }
                using var leser = befehl.ExecuteReader();
                var zeilen = new List<object?[]>();
                while (leser.Read())
                {
                    var zeile = new object?[leser.FieldCount];
                    for (var i = 0; i < leser.FieldCount; i++)
                    {
                        zeile[i] = leser.IsDBNull(i) ? null : leser.GetValue(i);
                    }
                    zeilen.Add(zeile);
                }
                return zeilen;
            }
            catch (SqliteException)
            {
                Fehler = true;
                return null;
            }
        }

        public void Dispose() => verbindung?.Dispose();
    }
}

/// <summary>Nachbildung der Python-Umwandlungen fuer Werte aus SQLite (str(), bool(), Slicing nach Zeichen, round(x, 1)).</summary>
internal static class PyText
{
    /// <summary>Wie Pythons bool(x) fuer SQLite-Werte.</summary>
    public static bool Wahr(object? wert) => wert switch
    {
        null => false,
        long n => n != 0,
        double d => d != 0,
        string s => s.Length > 0,
        byte[] b => b.Length > 0,
        _ => true,
    };

    /// <summary>Zahlwert einer Summe (None/Text -> 0 wie "x or 0").</summary>
    public static double Zahl(object? wert) => wert switch
    {
        long n => n,
        double d => d,
        _ => 0,
    };

    /// <summary>"%d" einer Zahl: ganzzahliger Teil (Python int()).</summary>
    public static string D(double wert) => Math.Truncate(wert).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>Wie Pythons str(x) fuer SQLite-Werte.</summary>
    public static string Str(object? wert) => wert switch
    {
        null => "None",
        string s => s,
        long n => n.ToString(CultureInfo.InvariantCulture),
        double d => Float(d),
        byte[] b => Bytes(b),
        _ => Convert.ToString(wert, CultureInfo.InvariantCulture) ?? "",
    };

    /// <summary>Anzahl Zeichen wie Pythons len() (Unicode-Zeichen, nicht UTF-16-Einheiten).</summary>
    public static int Laenge(string text) => text.EnumerateRunes().Count();

    /// <summary>text[von:bis] wie in Python, gezaehlt in Unicode-Zeichen.</summary>
    public static string Teil(string text, int von, int bis)
    {
        var zeichen = text.EnumerateRunes().ToList();
        von = Math.Clamp(von, 0, zeichen.Count);
        bis = Math.Clamp(bis, von, zeichen.Count);
        var ergebnis = new StringBuilder();
        for (var i = von; i < bis; i++)
        {
            ergebnis.Append(zeichen[i].ToString());
        }
        return ergebnis.ToString();
    }

    /// <summary>Wie Pythons str.split() ohne Argument (Leerraum nach Python).</summary>
    public static List<string> Woerter(string text)
    {
        var woerter = new List<string>();
        var aktuell = new StringBuilder();
        foreach (var zeichen in text.EnumerateRunes())
        {
            if (Leerraum(zeichen.Value))
            {
                if (aktuell.Length > 0)
                {
                    woerter.Add(aktuell.ToString());
                    aktuell.Clear();
                }
            }
            else
            {
                aktuell.Append(zeichen.ToString());
            }
        }
        if (aktuell.Length > 0)
        {
            woerter.Add(aktuell.ToString());
        }
        return woerter;
    }

    /// <summary>Pythons str.isspace fuer ein Zeichen.</summary>
    private static bool Leerraum(int c) =>
        c is >= 0x09 and <= 0x0D or >= 0x1C and <= 0x20 or 0x85 or 0xA0 or 0x1680 or >= 0x2000 and <= 0x200A
            or 0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000;

    /// <summary>Erstes Zeichen ist Buchstabe oder Ziffer (Pythons isalnum).</summary>
    public static bool BeginntAlnum(string wort)
    {
        var zeichen = Rune.GetRuneAt(wort, 0);
        return Rune.IsLetter(zeichen) || Rune.IsNumber(zeichen);
    }

    /// <summary>Genau JJJJ-MM-TT (date.fromisoformat fuer die Form, die die Datenbank schreibt).</summary>
    public static bool IsoTag(string text, out DateOnly tag) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out tag);

    /// <summary>Pythons round(x, 1): exakter Wert der Gleitkommazahl, bei Gleichstand zur geraden Ziffer.</summary>
    public static double Runde1(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || x == 0)
        {
            return x;
        }
        var bits = BitConverter.DoubleToInt64Bits(x);
        var negativ = bits < 0;
        var exponent = (int)((bits >> 52) & 0x7FF);
        var mantisse = bits & 0xFFFFFFFFFFFFFL;
        if (exponent == 0)
        {
            exponent = 1;
        }
        else
        {
            mantisse |= 1L << 52;
        }
        exponent -= 1075;   // x = mantisse * 2^exponent
        var zaehler = new BigInteger(mantisse) * 10;
        var nenner = BigInteger.One;
        if (exponent > 0)
        {
            zaehler <<= exponent;
        }
        else
        {
            nenner <<= -exponent;
        }
        var ganz = BigInteger.DivRem(zaehler, nenner, out var rest);
        var doppelt = rest * 2;
        if (doppelt > nenner || (doppelt == nenner && !ganz.IsEven))
        {
            ganz += 1;
        }
        var ergebnis = (double)ganz / 10.0;
        return negativ ? -ergebnis : ergebnis;
    }

    /// <summary>Pythons repr(float).</summary>
    private static string Float(double d)
    {
        if (double.IsNaN(d))
        {
            return "nan";
        }
        if (double.IsInfinity(d))
        {
            return d > 0 ? "inf" : "-inf";
        }
        // kuerzeste eindeutige Ziffernfolge und Zehnerexponent
        var kurz = d.ToString("R", CultureInfo.InvariantCulture);
        var vorzeichen = kurz.StartsWith('-') ? "-" : "";
        kurz = kurz.TrimStart('-');
        var e = kurz.IndexOfAny(['E', 'e']);
        var exp = e >= 0 ? int.Parse(kurz[(e + 1)..], CultureInfo.InvariantCulture) : 0;
        var mantisse = e >= 0 ? kurz[..e] : kurz;
        var punkt = mantisse.IndexOf('.', StringComparison.Ordinal);
        var ziffern = mantisse.Replace(".", "", StringComparison.Ordinal);
        var stellen = (punkt >= 0 ? punkt : mantisse.Length) + exp;   // Stellen vor dem Komma
        var fuehrend = ziffern.Length - ziffern.TrimStart('0').Length;
        ziffern = ziffern.Trim('0');
        if (ziffern.Length == 0)
        {
            return vorzeichen + "0.0";
        }
        stellen -= fuehrend;
        var exp10 = stellen - 1;
        if (exp10 is < -4 or >= 16)
        {
            var rest = ziffern.Length > 1 ? "." + ziffern[1..] : "";
            return vorzeichen + ziffern[0] + rest + "e" + (exp10 < 0 ? "-" : "+") + Math.Abs(exp10).ToString("00", CultureInfo.InvariantCulture);
        }
        if (stellen <= 0)
        {
            return vorzeichen + "0." + new string('0', -stellen) + ziffern;
        }
        if (stellen >= ziffern.Length)
        {
            return vorzeichen + ziffern + new string('0', stellen - ziffern.Length) + ".0";
        }
        return vorzeichen + ziffern[..stellen] + "." + ziffern[stellen..];
    }

    /// <summary>Pythons repr(bytes).</summary>
    private static string Bytes(byte[] daten)
    {
        var anfuehrung = daten.Contains((byte)'\'') && !daten.Contains((byte)'"') ? '"' : '\'';
        var text = new StringBuilder("b").Append(anfuehrung);
        foreach (var b in daten)
        {
            text.Append(b switch
            {
                (byte)'\\' => "\\\\",
                (byte)'\t' => "\\t",
                (byte)'\n' => "\\n",
                (byte)'\r' => "\\r",
                _ when b == anfuehrung => "\\" + (char)b,
                >= 0x20 and < 0x7F => ((char)b).ToString(),
                _ => "\\x" + b.ToString("x2", CultureInfo.InvariantCulture),
            });
        }
        return text.Append(anfuehrung).ToString();
    }
}
