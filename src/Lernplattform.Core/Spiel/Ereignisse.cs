namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Ereignistypen des Spielprotokolls und Status der Tickets wie <c>fisi_game.py</c>
/// (Python 0.63.2, Z. 69-142, 6160-6180, 15539-15552). Die Texte stehen so in der Datenbank
/// (Tabelle spiel_ereignisse) und duerfen sich nie aendern.
/// </summary>
public static class Ereignisse
{
    public const string Profil = "profil_gesetzt";               // EV_PROFILE
    public const string Erledigt = "ticket_erledigt";            // EV_SOLVED
    public const string Verschoben = "ticket_verschoben";        // EV_DEFERRED
    public const string Feierabend = "tag_beendet";              // EV_DAY_END
    public const string Umzug = "umzug";                         // EV_MOVE
    public const string MoebelGekauft = "moebel_gekauft";        // EV_BUY
    public const string MoebelVerkauft = "moebel_verkauft";      // EV_SELL
    public const string Einrichtung = "einrichtung_gesetzt";     // EV_LAYOUT
    public const string Ersatzteil = "ersatzteil_bestellt";      // EV_SPARE_ORDER
    public const string Gegruendet = "firma_gegruendet";         // EV_FOUNDED
    public const string Eingestellt = "mitarbeiter_eingestellt"; // EV_HIRED
    public const string Gekuendigt = "mitarbeiter_gekuendigt";   // EV_FIRED
    public const string Weiterbildung = "weiterbildung";         // EV_TRAINING
    public const string Erweitert = "gebaeude_erweitert";        // EV_EXPAND
    public const string AngebotGewonnen = "angebot_gewonnen";    // EV_OFFER_WON
    public const string AngebotVerloren = "angebot_verloren";    // EV_OFFER_LOST
    public const string Delegiert = "ticket_delegiert";          // EV_DELEGATED
    public const string ProjektGewonnen = "projekt_gewonnen";    // EV_PROJECT_WON
    public const string ProjektVerloren = "projekt_verloren";    // EV_PROJECT_LOST
    public const string ProjektTeam = "projekt_team";            // EV_PROJECT_TEAM
    public const string Raum = "raum_ausgebaut";                 // EV_ROOM
    public const string Kredit = "kredit_aufgenommen";           // EV_LOAN
    public const string KreditAbgeloest = "kredit_abgeloest";    // EV_LOAN_REPAID
    public const string Werbung = "werbung_gebucht";             // EV_ADS
    public const string WerbungEnde = "werbung_beendet";         // EV_ADS_STOP
    public const string Zertifizierung = "zertifizierung";       // EV_CERT
    public const string UrlaubJa = "urlaub_genehmigt";           // EV_VACATION_OK
    public const string UrlaubNein = "urlaub_abgelehnt";         // EV_VACATION_NO
    public const string Konflikt = "konflikt_ereignis";          // EV_CONFLICT
    public const string Rueckhol = "rueckhol_angebot";           // EV_RECALL
    public const string Filiale = "filiale_ausgebaut";           // EV_BRANCH
    public const string Versetzt = "mitarbeiter_versetzt";       // EV_TRANSFER
    public const string Erfolg = "erfolg_freigeschaltet";        // EV_ACHIEVEMENT
    public const string Schwierigkeit = "schwierigkeit_gewaehlt"; // EV_DIFFICULTY
    public const string FarmStart = "serverfarm_gestartet";      // EV_FARM_START
    public const string FarmAufgabe = "serverfarm_aufgabe";      // EV_FARM_TASK
    public const string FarmTeam = "serverfarm_team";            // EV_FARM_TEAM

    public const string Einfach = "einfach";                     // DIFF_EASY
    public const string Normal = "normal";                       // DIFF_NORMAL

    public const string Offen = "offen";                         // ST_OPEN
    public const string Richtig = "richtig";                     // ST_RIGHT
    public const string Falsch = "falsch";                       // ST_WRONG
    public const string StatusVerschoben = "verschoben";         // ST_DEFERRED
    public const string Wartet = "wartet";                       // ST_WAITING

    /// <summary>FIRM_EVENTS</summary>
    public static readonly IReadOnlySet<string> Firma = new HashSet<string>
    {
        Gegruendet, Eingestellt, Gekuendigt, Weiterbildung, Erweitert, AngebotGewonnen, AngebotVerloren, Delegiert,
        ProjektGewonnen, ProjektVerloren, ProjektTeam, Raum, Kredit, KreditAbgeloest, Werbung, WerbungEnde, Zertifizierung,
        UrlaubJa, UrlaubNein, Konflikt, Rueckhol, Filiale, Versetzt,
    };

    /// <summary>FARM_EVENTS</summary>
    public static readonly IReadOnlySet<string> Farm = new HashSet<string> { FarmStart, FarmAufgabe, FarmTeam };

    /// <summary>AXIS_KEYS: die vier Achsen der Reputation.</summary>
    public static readonly IReadOnlyList<string> Achsen = ["fachkompetenz", "zuverlaessigkeit", "kundenzufriedenheit", "sicherheit"];

    // Kassenbuch (BOOK_*)
    public const string BuchAuftraege = "Aufträge";
    public const string BuchGehalt = "Gehalt";
    public const string BuchWohnen = "Wohnen";
    public const string BuchWaren = "Wareneinkauf";
    public const string BuchGruendung = "Gründung";
    public const string BuchUmsatz = "Umsatz Mitarbeiter";
    public const string BuchGehaelter = "Gehälter";
    public const string BuchNebenkosten = "Nebenkosten";
    public const string BuchWeiterbildung = "Weiterbildung";
    public const string BuchAusbau = "Ausbau";
    public const string BuchAngebote = "Angebote";
    public const string BuchKundentickets = "Kundentickets";
    public const string BuchProjekte = "Projekte";
    public const string BuchMaterial = "Projektmaterial";
    public const string BuchKredit = "Kredite";
    public const string BuchZinsen = "Kreditzinsen";
    public const string BuchTilgung = "Kredittilgung";
    public const string BuchKreditGebuehren = "Kreditgebühren";
    public const string BuchSteuer = "Umsatzsteuer";
    public const string BuchWerbung = "Werbung";
    public const string BuchZertifikat = "Zertifizierung";
    public const string BuchNacharbeit = "Nacharbeit";
}
