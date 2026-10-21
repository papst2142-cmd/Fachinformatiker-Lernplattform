namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 3 W4c (Weisung P3-23): Texte der Karte "WiSo-Rechner" und des Trainer-Bereichs "WiSo",
/// PC und Handy gleich. Wortlaut genau wie die von Chat freigegebene Wortliste (Fi-Austausch
/// anlagen/P3-22_Wortliste_W4c.md, F1 bis F5 wie vorgeschlagen). Kein Python-Vorbild;
/// RechnerTexte.Python.cs (erzeugt) bleibt unveraendert.
/// </summary>
public static partial class RechnerTexte
{
    // -- Karte -------------------------------------------------------------------------------

    public const string WisoTitle = "WiSo-Rechner";
    public const string WisoSubtitle = "Kalkulation, Kosten, Abschreibung, Zinsen";
    public const string WisoHinweis = "Rechenhilfe zum Lernen, keine Steuer- oder Rechtsberatung.";

    /// <summary>Innere Pillenreihe: (Wert, Pillentext), Vorgabe der erste.</summary>
    public static readonly IReadOnlyList<(string Wert, string Text)> WisoAuswahl =
    [
        ("bezug", "Bezug"), ("verkauf", "Verkauf"), ("deckung", "Deckungsbeitrag"), ("afa", "Abschreibung"),
        ("zinsen", "Zinsen"), ("amort", "Amortisation"),
    ];

    // -- Felder (Beschriftung, Vorgabe) ------------------------------------------------------

    public const string FeldLep = "Listeneinkaufspreis (€)";
    public const string FeldRabatt = "Rabatt (%)";
    public const string FeldSkonto = "Skonto (%)";
    public const string FeldBezugskosten = "Bezugskosten (€)";
    public const string FeldBezugspreis = "Bezugspreis (€)";
    public const string FeldHandlungskosten = "Handlungskosten (%)";
    public const string FeldGewinn = "Gewinn (%)";
    public const string FeldKundenskonto = "Kundenskonto (%)";
    public const string FeldKundenrabatt = "Kundenrabatt (%)";
    public const string FeldUmsatzsteuer = "Umsatzsteuer (%)";
    public const string FeldPreis = "Verkaufspreis je Stück (€)";
    public const string FeldVariabel = "Variable Kosten je Stück (€)";
    public const string FeldFixkosten = "Fixkosten (€)";
    public const string FeldMenge = "Menge (Stück)";
    public const string FeldAk = "Anschaffungskosten (€)";
    public const string FeldNutzungsdauer = "Nutzungsdauer (Jahre)";
    public const string FeldMonat = "Monat der Anschaffung";
    public const string FeldKapital = "Kapital (€)";
    public const string FeldZinssatz = "Zinssatz (%)";
    public const string FeldLaufzeit = "Laufzeit";
    public const string FeldInvestition = "Investition (€)";
    public const string FeldEinsparung = "Einsparung pro Jahr (€)";
    public const string FeldLaufend = "Laufende Kosten pro Jahr (€)";

    /// <summary>Pillen der Laufzeit (F2): (Wert, Text), Vorgabe Tage.</summary>
    public static readonly IReadOnlyList<(string Wert, string Text)> LaufzeitEinheiten =
        [("tage", "Tage"), ("monate", "Monate"), ("jahre", "Jahre")];

    /// <summary>Felder je Rechner: (Schluessel wie G30, Beschriftung, Vorgabe) in Pruefreihenfolge.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<(string Schluessel, string Beschriftung, string Vorgabe)>> WisoFelder =
        new Dictionary<string, IReadOnlyList<(string, string, string)>>(StringComparer.Ordinal)
        {
            ["bezug"] = [("lep", FeldLep, "1.200"), ("rabatt", FeldRabatt, "15"), ("skonto", FeldSkonto, "2"), ("bezugskosten", FeldBezugskosten, "25")],
            ["verkauf"] =
            [
                ("bp", FeldBezugspreis, "1.024,60"), ("hk", FeldHandlungskosten, "30"), ("gewinn", FeldGewinn, "10"),
                ("kskonto", FeldKundenskonto, "2"), ("krabatt", FeldKundenrabatt, "10"), ("ust", FeldUmsatzsteuer, "19"),
            ],
            ["deckung"] = [("preis", FeldPreis, "250"), ("vk", FeldVariabel, "150"), ("fix", FeldFixkosten, "40.000"), ("menge", FeldMenge, "600")],
            ["afa"] = [("ak", FeldAk, "1.800"), ("nd", FeldNutzungsdauer, "3"), ("monat", FeldMonat, "4")],
            ["zinsen"] = [("kapital", FeldKapital, "5.000"), ("zinssatz", FeldZinssatz, "4,5"), ("laufzeit", FeldLaufzeit, "90")],
            ["amort"] = [("investition", FeldInvestition, "12.000"), ("einsparung", FeldEinsparung, "4.800"), ("laufend", FeldLaufend, "800")],
        };

    // -- Fehler ------------------------------------------------------------------------------

    public const string FehlerCent = "Bitte höchstens 2 Nachkommastellen (volle Cent) eingeben: %s.";
    public const string FehlerNegativ = "Der Betrag darf nicht negativ sein: %s.";
    public const string FehlerNull = "Der Betrag muss größer als 0 sein: %s.";
    public const string FehlerBetragGross = "Der Betrag ist zu groß (höchstens 1.000.000.000 €): %s.";
    public const string FehlerProzent100 = "Der Prozentsatz muss kleiner als 100 sein: %s.";
    public const string FehlerProzentHoechst = "Der Prozentsatz darf höchstens 100 sein: %s.";
    public const string FehlerProzentNegativ = "Der Prozentsatz darf nicht negativ sein: %s.";
    public const string FehlerGanz = "Bitte eine ganze Zahl eingeben: %s.";
    public const string FehlerGanzBereich = "Bitte eine ganze Zahl von %s bis %s eingeben: %s.";
    public const string FehlerKeinDb = "Kein positiver Deckungsbeitrag: Es gibt keine Gewinnschwelle.";
    public const string FehlerNie = "Die Investition amortisiert sich nie.";

    // -- Ergebniszeilen ----------------------------------------------------------------------

    public const string Euro = "€";
    public const string ZeileLep = "Listeneinkaufspreis";
    public const string ZeileRabatt = "- Rabatt (%s %%)";
    public const string ZeileZep = "= Zieleinkaufspreis";
    public const string ZeileSkonto = "- Skonto (%s %%)";
    public const string ZeileBep = "= Bareinkaufspreis";
    public const string ZeileBezugskosten = "+ Bezugskosten";
    public const string ZeileBezugspreis = "= Bezugspreis";
    public const string ZeileBp = "Bezugspreis";
    public const string ZeileHk = "+ Handlungskosten (%s %%)";
    public const string ZeileSk = "= Selbstkosten";
    public const string ZeileGewinn = "+ Gewinn (%s %%)";
    public const string ZeileBvp = "= Barverkaufspreis";
    public const string ZeileKskonto = "+ Kundenskonto (%s %%)";
    public const string ZeileZvp = "= Zielverkaufspreis";
    public const string ZeileKrabatt = "+ Kundenrabatt (%s %%)";
    public const string ZeileLvp = "= Listenverkaufspreis netto";
    public const string ZeileUst = "+ Umsatzsteuer (%s %%)";
    public const string ZeileBrutto = "= Bruttoverkaufspreis";
    public const string ZeilePreis = "Verkaufspreis je Stück";
    public const string ZeileVariabel = "- Variable Kosten je Stück";
    public const string ZeileDb = "= Deckungsbeitrag je Stück";
    public const string ZeileMenge = "x Menge";
    public const string ZeileDbGesamt = "= Deckungsbeitrag gesamt";
    public const string ZeileFix = "- Fixkosten";
    public const string ZeileErgebnis = "= Betriebsergebnis";
    public const string ZeileGewinnschwelle = "Gewinnschwelle";
    public const string Stueck = "Stück";
    public const string ZusatzGewinn = " (Gewinn)";
    public const string ZusatzVerlust = " (Verlust)";
    public const string ZeileAk = "Anschaffungskosten";
    public const string ZeileNd = "Nutzungsdauer";
    public const string ZeileMonat = "Anschaffung im Monat";
    public const string ZeileJaehrlich = "Jährlicher Betrag";
    public const string ZeileJahr = "Jahr %s";
    public const string ZeileJahrMonate = "Jahr %s (%s)";
    public const string ZeileRestbuchwert = "%s, Restbuchwert %s";
    public const string ZeileKapital = "Kapital";
    public const string ZeileZinssatz = "Zinssatz";
    public const string ZeileLaufzeit = "Laufzeit";
    public const string ZeileZinsen = "Zinsen";
    public const string ZeileEndkapital = "Kapital mit Zinsen";
    public const string ZeileInvestition = "Investition";
    public const string ZeileEinsparung = "Einsparung pro Jahr";
    public const string ZeileLaufend = "- Laufende Kosten pro Jahr";
    public const string ZeileRueckfluss = "= Rückfluss pro Jahr";
    public const string ZeileAmortDauer = "Amortisationsdauer";
    public const string ZeileJahreMonate = "In Jahren und Monaten";
    public const string Jahr = "Jahr";
    public const string JahreText = "Jahre";
    public const string Monat = "Monat";
    public const string MonateText = "Monate";
    public const string Tag = "Tag";
    public const string Tage = "Tage";
    public const string Und = " und ";

    public static readonly IReadOnlyList<string> Monatsnamen =
        ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"];

    // -- Rechenwege --------------------------------------------------------------------------

    public const string WisoCent = "   Jeder Schritt wird kaufmännisch auf volle Cent gerundet.";
    public const string BezugExplainTitle = "RECHENWEG BEZUGSKALKULATION";
    public const string BezugStep1 = "SCHRITT 1: Rabatt abziehen";
    public const string BezugFormelRabatt = "   Rabatt = Listeneinkaufspreis x Rabatt / 100";
    public const string BezugStep2 = "SCHRITT 2: Skonto abziehen";
    public const string BezugFormelSkonto = "   Skonto = Zieleinkaufspreis x Skonto / 100";
    public const string BezugStep3 = "SCHRITT 3: Bezugskosten addieren";
    public const string VerkaufExplainTitle = "RECHENWEG VERKAUFSKALKULATION";
    public const string VerkaufStep1 = "SCHRITT 1: Selbstkosten";
    public const string VerkaufStep2 = "SCHRITT 2: Gewinn";
    public const string VerkaufStep3 = "SCHRITT 3: Kundenskonto und Kundenrabatt (im Hundert)";
    public static readonly IReadOnlyList<string> VerkaufImHundert =
    [
        "   Skonto und Rabatt werden vom Endpreis abgezogen. Deshalb",
        "   wird mit 100 minus Prozentsatz geteilt.",
    ];
    public const string VerkaufStep4 = "SCHRITT 4: Umsatzsteuer";
    public const string DeckungExplainTitle = "RECHENWEG DECKUNGSBEITRAG";
    public const string DeckungStep1 = "SCHRITT 1: Deckungsbeitrag je Stück";
    public const string DeckungStep2 = "SCHRITT 2: Betriebsergebnis";
    public const string DeckungStep3 = "SCHRITT 3: Gewinnschwelle";
    public const string DeckungFormel = "   Gewinnschwelle = Fixkosten / Deckungsbeitrag je Stück";
    public const string DeckungAufrunden = "   Die Gewinnschwelle wird immer auf ganze Stück aufgerundet.";
    public const string MehrAls = "mehr als %s";
    public const string Aufgerundet = "%s, aufgerundet %s";
    public const string AfaExplainTitle = "RECHENWEG ABSCHREIBUNG";
    public const string AfaStep1 = "SCHRITT 1: Jährlicher Betrag";
    public const string AfaStep2 = "SCHRITT 2: Erstes Jahr monatsgenau";
    public const string AfaMonate = "   Ab dem Monat der Anschaffung zählen 13 - %s = %s.";
    public const string AfaStepFolge = "SCHRITT %s: Folgejahre";
    public const string AfaJedesJahr = "   Jedes Jahr %s, bis der Restbuchwert 0 ist.";
    public const string AfaLetztes = "   Im letzten Jahr wird nur der Rest abgeschrieben, %s.";
    public const string ZinsenExplainTitle = "RECHENWEG ZINSEN";
    public const string ZinsenStep1 = "SCHRITT 1: Laufzeit in Tagen (1 Monat = 30 Tage, 1 Jahr = 360 Tage)";
    public const string ZinsenStep2 = "SCHRITT 2: Zinsen berechnen";
    public const string ZinsenFormel = "   Zinsen = Kapital x Zinssatz x Tage / (100 x 360)";
    public const string AmortExplainTitle = "RECHENWEG AMORTISATION";
    public const string AmortStep1 = "SCHRITT 1: Rückfluss pro Jahr";
    public const string AmortStep2 = "SCHRITT 2: Amortisationsdauer";
    public const string AmortFormel = "   Amortisationsdauer = Investition / Rückfluss pro Jahr";
    public const string AmortStep3 = "SCHRITT 3: In Jahren und Monaten";

    // -- Trainer -----------------------------------------------------------------------------

    public const string BereichWiso = "WiSo";
    public const string ArtBezug = "Bezugskalkulation";
    public const string ArtVerkauf = "Verkaufskalkulation";
    public const string ArtDeckung = "Deckungsbeitrag";
    public const string ArtAfa = "Abschreibung";
    public const string ArtZinsen = "Zinsen";
    public const string ArtAmort = "Amortisation";
    public const string FeldErgebnisEuro = "Ergebnis (€)";
    public const string FeldErgebnisStueck = "Ergebnis (Stück)";
    public const string FeldErgebnisJahre = "Ergebnis (Jahre)";
    public const string FeldJahre = "Jahre";
    public const string FeldMonate = "Monate";
    public const string RundeCent = " Runde auf volle Cent.";
    public const string RundeJeden = " Runde jeden Zwischenschritt auf volle Cent.";
    public const string AufgabeBezugLeicht = "Ein Lieferant bietet eine Ware für %s € an und gewährt %s %% Rabatt. Wie hoch ist der Zieleinkaufspreis?";
    public const string AufgabeBezugMittel = "Listeneinkaufspreis %s €, Rabatt %s %%, Skonto %s %%. Wie hoch ist der Bareinkaufspreis?";
    public const string AufgabeBezugSchwer = "Listeneinkaufspreis %s €, Rabatt %s %%, Skonto %s %%, Bezugskosten %s €. Wie hoch ist der Bezugspreis?";
    public const string AufgabeVerkaufLeicht = "Der Bezugspreis einer Ware ist %s €. Die Handlungskosten betragen %s %%. Wie hoch sind die Selbstkosten?";
    public const string AufgabeVerkaufMittel = "Bezugspreis %s €, Handlungskosten %s %%, Gewinn %s %%. Wie hoch ist der Barverkaufspreis?";
    public const string AufgabeVerkaufSchwer = "Bezugspreis %s €, Handlungskosten %s %%, Gewinn %s %%, Kundenskonto %s %%, Kundenrabatt %s %%, Umsatzsteuer %s %%. Wie hoch ist der Bruttoverkaufspreis?";
    public const string AufgabeDeckungLeicht = "Ein Artikel wird für %s € je Stück verkauft. Die variablen Kosten betragen %s € je Stück. Wie hoch ist der Deckungsbeitrag je Stück?";
    public const string AufgabeDeckungMittel = "Preis %s € je Stück, variable Kosten %s € je Stück, Fixkosten %s €, Menge %s Stück. Wie hoch ist das Betriebsergebnis?";
    public const string AufgabeDeckungSchwer = "Preis %s € je Stück, variable Kosten %s € je Stück, Fixkosten %s €. Wie hoch ist die Gewinnschwelle? Runde auf ganze Stück auf.";
    public const string AufgabeAfaLeicht = "Ein Server kostet %s €. Die Nutzungsdauer beträgt %s Jahre. Wie hoch ist die jährliche lineare Abschreibung?";
    public const string AufgabeAfaMittel = "Ein Server kostet %s €, Nutzungsdauer %s Jahre, Anschaffung im Januar. Wie hoch ist der Restbuchwert am Ende von Jahr %s?";
    public const string AufgabeAfaSchwer = "Ein Server kostet %s €, Nutzungsdauer %s Jahre, Anschaffung im %s. Wie hoch ist die Abschreibung im ersten Jahr (monatsgenau)?";
    public const string AufgabeZinsen = "Ein Kapital von %s € wird %s lang zu %s %% verzinst.";
    public const string AufgabeZinsenSchwer = " Rechne kaufmännisch (1 Monat = 30 Tage, 1 Jahr = 360 Tage).";
    public const string AufgabeZinsenFrage = " Wie hoch sind die Zinsen?";
    public const string AufgabeAmortLeicht = "Eine neue Anlage kostet %s € und spart %s € pro Jahr. Nach wie vielen Jahren hat sie sich amortisiert?";
    public const string AufgabeAmortMittel = "Eine neue Anlage kostet %s €, spart %s € pro Jahr und hat laufende Kosten von %s € pro Jahr. Nach wie vielen Jahren hat sie sich amortisiert?";
    public const string AufgabeAmortSchwer = "Eine neue Anlage kostet %s €, spart %s € pro Jahr und hat laufende Kosten von %s € pro Jahr. Wie lange dauert die Amortisation? Angefangene Monate zählen voll.";
}
