using System.Text.Json;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 3 W4c (Weisung P3-23 Punkt 3a/3c): Karte "WiSo-Rechner" nach der freigegebenen Wortliste
/// (Fi-Austausch anlagen/P3-22_Wortliste_W4c.md): Ergebniszeilen, Rechenwege, Fehler in der
/// Pruefreihenfolge, Monatsregel (F1), Laufzeit-Pillen (F2), Zeilen hoechstens 28 am Handy mit nur
/// Einfachleerzeichen im Zeilenkern und Pruefaufgabe 14 im zweiten Fall.
/// </summary>
public class WisoBerichteTests
{
    private static readonly JsonElement G31 = VergleichsHilfe.Lade("rechentrainer_w4.json");

    private static Dictionary<string, string?> Vorgaben(string rechner) =>
        WisoFelder[rechner].ToDictionary(f => f.Schluessel, f => (string?)f.Vorgabe);

    private static WisoEingabe Lesen(string rechner, string einheit, params string[] werte)
    {
        var felder = WisoFelder[rechner];
        Assert.Equal(felder.Count, werte.Length);
        return WisoBerichte.Lesen(rechner, felder.Select((f, i) => (f.Schluessel, (string?)werte[i])).ToDictionary(p => p.Schluessel, p => p.Item2), einheit);
    }

    private static string Fehler(string rechner, string einheit, params string[] werte) =>
        Assert.Throws<RechnerFehler>(() => Lesen(rechner, einheit, werte)).Message;

    private static string L(params string[] zeilen) => string.Join("\n", zeilen);

    // ---------------------------------------------------------------- Karte

    [Fact]
    public void KarteUndAuswahlWieWortliste()
    {
        Assert.Equal(("wiso", "WiSo"), RechnerAuswahl[^1]);
        Assert.Equal(["Subnetting", "Datenmengen", "Übertragung", "Bild und Video", "RAID", "USV", "WiSo"], RechnerAuswahl.Select(a => a.Text));
        Assert.Equal("WiSo-Rechner", WisoTitle);
        Assert.Equal("Kalkulation, Kosten, Abschreibung, Zinsen", WisoSubtitle);
        Assert.Equal("Rechenhilfe zum Lernen, keine Steuer- oder Rechtsberatung.", WisoHinweis);
        Assert.Equal(["Bezug", "Verkauf", "Deckungsbeitrag", "Abschreibung", "Zinsen", "Amortisation"], WisoAuswahl.Select(a => a.Text));
        Assert.Equal(["bezug", "verkauf", "deckung", "afa", "zinsen", "amort"], WisoAuswahl.Select(a => a.Wert));
        Assert.Equal(["Tage", "Monate", "Jahre"], LaufzeitEinheiten.Select(a => a.Text));
        Assert.Equal(["Listeneinkaufspreis (€)", "Rabatt (%)", "Skonto (%)", "Bezugskosten (€)"], WisoFelder["bezug"].Select(f => f.Beschriftung));
        Assert.Equal(["Bezugspreis (€)", "Handlungskosten (%)", "Gewinn (%)", "Kundenskonto (%)", "Kundenrabatt (%)", "Umsatzsteuer (%)"],
            WisoFelder["verkauf"].Select(f => f.Beschriftung));
        Assert.Equal(["Verkaufspreis je Stück (€)", "Variable Kosten je Stück (€)", "Fixkosten (€)", "Menge (Stück)"], WisoFelder["deckung"].Select(f => f.Beschriftung));
        Assert.Equal(["Anschaffungskosten (€)", "Nutzungsdauer (Jahre)", "Monat der Anschaffung"], WisoFelder["afa"].Select(f => f.Beschriftung));
        Assert.Equal(["Kapital (€)", "Zinssatz (%)", "Laufzeit"], WisoFelder["zinsen"].Select(f => f.Beschriftung));
        Assert.Equal(["Investition (€)", "Einsparung pro Jahr (€)", "Laufende Kosten pro Jahr (€)"], WisoFelder["amort"].Select(f => f.Beschriftung));
        Assert.Equal(["1.200", "15", "2", "25"], WisoFelder["bezug"].Select(f => f.Vorgabe));
        Assert.Equal(["1.024,60", "30", "10", "2", "10", "19"], WisoFelder["verkauf"].Select(f => f.Vorgabe));
        Assert.Equal(["250", "150", "40.000", "600"], WisoFelder["deckung"].Select(f => f.Vorgabe));
        Assert.Equal(["1.800", "3", "4"], WisoFelder["afa"].Select(f => f.Vorgabe));
        Assert.Equal(["5.000", "4,5", "90"], WisoFelder["zinsen"].Select(f => f.Vorgabe));
        Assert.Equal(["12.000", "4.800", "800"], WisoFelder["amort"].Select(f => f.Vorgabe));
    }

    // ---------------------------------------------------------------- Ergebniszeilen (Abschnitt 4)

    [Fact]
    public void ErgebniszeilenWieWortliste()
    {
        const string Punkt = "Punkt als Tausender gelesen.";
        Assert.Equal(L(
            "Listeneinkaufspreis        : 1.200,00 €",
            "- Rabatt (15 %)            : 180,00 €",
            "= Zieleinkaufspreis        : 1.020,00 €",
            "- Skonto (2 %)             : 20,40 €",
            "= Bareinkaufspreis         : 999,60 €",
            "+ Bezugskosten             : 25,00 €",
            "= Bezugspreis              : 1.024,60 €",
            Punkt), WisoBerichte.Bericht(WisoBerichte.Lesen("bezug", Vorgaben("bezug")), false));
        Assert.Equal(L(
            "Bezugspreis                : 1.024,60 €",
            "+ Handlungskosten (30 %)   : 307,38 €",
            "= Selbstkosten             : 1.331,98 €",
            "+ Gewinn (10 %)            : 133,20 €",
            "= Barverkaufspreis         : 1.465,18 €",
            "+ Kundenskonto (2 %)       : 29,90 €",
            "= Zielverkaufspreis        : 1.495,08 €",
            "+ Kundenrabatt (10 %)      : 166,12 €",
            "= Listenverkaufspreis netto: 1.661,20 €",
            "+ Umsatzsteuer (19 %)      : 315,63 €",
            "= Bruttoverkaufspreis      : 1.976,83 €",
            Punkt), WisoBerichte.Bericht(WisoBerichte.Lesen("verkauf", Vorgaben("verkauf")), false));
        Assert.Equal(L(
            "Verkaufspreis je Stück     : 250,00 €",
            "- Variable Kosten je Stück : 150,00 €",
            "= Deckungsbeitrag je Stück : 100,00 €",
            "x Menge                    : 600 Stück",
            "= Deckungsbeitrag gesamt   : 60.000,00 €",
            "- Fixkosten                : 40.000,00 €",
            "= Betriebsergebnis         : 20.000,00 € (Gewinn)",
            "Gewinnschwelle             : 400 Stück",
            Punkt), WisoBerichte.Bericht(WisoBerichte.Lesen("deckung", Vorgaben("deckung")), false));
        Assert.Equal(L(
            "Anschaffungskosten         : 1.800,00 €",
            "Nutzungsdauer              : 3 Jahre",
            "Anschaffung im Monat       : 4 (April)",
            "Jährlicher Betrag          : 600,00 €",
            "Jahr 1 (9 Monate)          : 450,00 €, Restbuchwert 1.350,00 €",
            "Jahr 2                     : 600,00 €, Restbuchwert 750,00 €",
            "Jahr 3                     : 600,00 €, Restbuchwert 150,00 €",
            "Jahr 4 (3 Monate)          : 150,00 €, Restbuchwert 0,00 €",
            Punkt), WisoBerichte.Bericht(WisoBerichte.Lesen("afa", Vorgaben("afa")), false));
        Assert.Equal(L(
            "Kapital                    : 5.000,00 €",
            "Zinssatz                   : 4,5 %",
            "Laufzeit                   : 90 Tage",
            "Zinsen                     : 56,25 €",
            "Kapital mit Zinsen         : 5.056,25 €",
            Punkt), WisoBerichte.Bericht(WisoBerichte.Lesen("zinsen", Vorgaben("zinsen")), false));
        Assert.Equal(L(
            "Investition                : 12.000,00 €",
            "Einsparung pro Jahr        : 4.800,00 €",
            "- Laufende Kosten pro Jahr : 800,00 €",
            "= Rückfluss pro Jahr       : 4.000,00 €",
            "Amortisationsdauer         : 3,00 Jahre",
            "In Jahren und Monaten      : 3 Jahre",
            Punkt), WisoBerichte.Bericht(WisoBerichte.Lesen("amort", Vorgaben("amort")), false));
        // ohne Tausenderpunkt keine Hinweiszeile; am Handy Beschriftung auf ihre eigene Laenge
        Assert.Equal(L(
            "Investition : 20.000,00 €",
            "Einsparung pro Jahr : 6.000,00 €",
            "- Laufende Kosten pro Jahr : 500,00 €",
            "= Rückfluss pro Jahr : 5.500,00 €",
            "Amortisationsdauer : 3,64 Jahre",
            "In Jahren und Monaten : 3 Jahre und 8 Monate"), WisoBerichte.Bericht(Lesen("amort", "tage", "20000", "6000", "500"), true));
    }

    [Fact]
    public void SonderfaelleDerErgebniszeilen()
    {
        static string Zeile(string bericht, string anfang) => bericht.Split('\n').Single(z => z.StartsWith(anfang, StringComparison.Ordinal));
        // Betriebsergebnis: Verlust, 0 ohne Zusatz; Gewinnschwelle 1 Stueck, Fixkosten 0 -> 0 Stueck
        var b = WisoBerichte.Bericht(Lesen("deckung", "tage", "250", "150", "40000", "360"), false);
        Assert.Equal("= Betriebsergebnis         : -4.000,00 € (Verlust)", Zeile(b, "= Betriebsergebnis"));
        b = WisoBerichte.Bericht(Lesen("deckung", "tage", "250", "150", "40000", "400"), false);
        Assert.Equal("= Betriebsergebnis         : 0,00 €", Zeile(b, "= Betriebsergebnis"));
        b = WisoBerichte.Bericht(Lesen("deckung", "tage", "250", "150", "50", "1"), false);
        Assert.Equal("Gewinnschwelle             : 1 Stück", Zeile(b, "Gewinnschwelle"));
        b = WisoBerichte.Bericht(Lesen("deckung", "tage", "250", "150", "0", "1"), false);
        Assert.Equal("Gewinnschwelle             : 0 Stück", Zeile(b, "Gewinnschwelle"));
        // AfA: Nutzungsdauer 1 "1 Jahr", Januar ohne "(n Monate)", "1 Monat" in der Einzahl
        b = WisoBerichte.Bericht(Lesen("afa", "tage", "1200", "1", "1"), false);
        Assert.Equal(L(
            "Anschaffungskosten         : 1.200,00 €",
            "Nutzungsdauer              : 1 Jahr",
            "Anschaffung im Monat       : 1 (Januar)",
            "Jährlicher Betrag          : 1.200,00 €",
            "Jahr 1                     : 1.200,00 €, Restbuchwert 0,00 €"), b);
        b = WisoBerichte.Bericht(Lesen("afa", "tage", "1200", "1", "12"), false);
        Assert.Equal("Jahr 1 (1 Monat)           : 100,00 €, Restbuchwert 1.100,00 €", Zeile(b, "Jahr 1"));
        Assert.Equal("Jahr 2 (11 Monate)         : 1.100,00 €, Restbuchwert 0,00 €", Zeile(b, "Jahr 2"));
        Assert.Equal("Anschaffung im Monat       : 12 (Dezember)", Zeile(b, "Anschaffung im"));
        // Zinsen: Laufzeit-Pillen (F2), Einzahl
        Assert.Equal("Laufzeit                   : 3 Monate = 90 Tage", Zeile(WisoBerichte.Bericht(Lesen("zinsen", "monate", "5000", "4,5", "3"), false), "Laufzeit"));
        Assert.Equal("Laufzeit                   : 2 Jahre = 720 Tage", Zeile(WisoBerichte.Bericht(Lesen("zinsen", "jahre", "5000", "4,5", "2"), false), "Laufzeit"));
        Assert.Equal("Laufzeit                   : 1 Tag", Zeile(WisoBerichte.Bericht(Lesen("zinsen", "tage", "5000", "4,5", "1"), false), "Laufzeit"));
        Assert.Equal("Laufzeit                   : 1 Monat = 30 Tage", Zeile(WisoBerichte.Bericht(Lesen("zinsen", "monate", "5000", "4,5", "1"), false), "Laufzeit"));
        Assert.Equal("Laufzeit                   : 1 Jahr = 360 Tage", Zeile(WisoBerichte.Bericht(Lesen("zinsen", "jahre", "5000", "4,5", "1"), false), "Laufzeit"));
        // 2 Jahre = 720 Tage: 5.000 x 4,5 x 720 / 36.000 = 450,00
        Assert.Equal("Zinsen                     : 450,00 €", Zeile(WisoBerichte.Bericht(Lesen("zinsen", "jahre", "5000", "4,5", "2"), false), "Zinsen "));
        // Prozentsaetze ohne Nullen am Ende
        b = WisoBerichte.Bericht(Lesen("bezug", "tage", "100", "2,5", "99,999", "0"), false);
        Assert.StartsWith("- Rabatt (2,5 %)", Zeile(b, "- Rabatt"), StringComparison.Ordinal);
        Assert.StartsWith("- Skonto (99,999 %)", Zeile(b, "- Skonto"), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- Monatsregel (F1), Pruefaufgabe 14

    [Fact]
    public void Pruefaufgabe14ZweiterFall()
    {
        // 20.000 / (6.000 - 500) = 3,64 Jahre; 20.000 x 12 / 5.500 = 43,64 -> 44 Monate = 3 Jahre und 8 Monate
        var e = Lesen("amort", "tage", "20.000", "6.000", "500");
        var a = Assert.IsType<AmortWerte>(e.Ergebnis);
        Assert.Equal(5500m, a.Rueckfluss);
        Assert.Equal(3.64m, a.Jahre);
        Assert.Equal(44, a.Monate);
        Assert.Equal("3 Jahre und 8 Monate", WisoBerichte.JahreUndMonate(a.Monate));
        Assert.Equal(44, Wiso.Amortisation(20000m, 6000m, 500m).Werte!.Monate);
    }

    [Fact]
    public void MonatsregelExaktUeberDenRest()
    {
        // 1.000 / 600: genau 20 Monate (ueber 1,67 Jahre waeren es falsch 21)
        Assert.Equal(20, Wiso.Amortisation(1000m, 600m, 0m).Werte!.Monate);
        Assert.Equal(1.67m, Wiso.Amortisation(1000m, 600m, 0m).Werte!.Jahre);
        // 11,x Monate werden 12 = 1 Jahr; angefangene Monate zaehlen voll
        Assert.Equal(12, Wiso.Amortisation(1000m, 1050m, 0m).Werte!.Monate);
        Assert.Equal(37, Wiso.Amortisation(12000.01m, 4000m, 0m).Werte!.Monate);
        Assert.Equal(1, Wiso.Amortisation(1m, 1000000000m, 0m).Werte!.Monate);
        Assert.Equal("3 Jahre", WisoBerichte.JahreUndMonate(36));
        Assert.Equal("1 Jahr und 1 Monat", WisoBerichte.JahreUndMonate(13));
        Assert.Equal("8 Monate", WisoBerichte.JahreUndMonate(8));
        Assert.Equal("1 Monat", WisoBerichte.JahreUndMonate(1));
        Assert.Equal("1 Jahr", WisoBerichte.JahreUndMonate(12));
        Assert.Equal("2 Jahre und 11 Monate", WisoBerichte.JahreUndMonate(35));
    }

    // ---------------------------------------------------------------- Rechenwege (Abschnitt 5)

    [Fact]
    public void RechenwegeWieWortliste()
    {
        const string Cent = "   Jeder Schritt wird kaufmännisch auf volle Cent gerundet.";
        Assert.Equal(L(
            "RECHENWEG BEZUGSKALKULATION",
            "SCHRITT 1: Rabatt abziehen",
            "   Rabatt = Listeneinkaufspreis x Rabatt / 100",
            "   1.200,00 x 15 / 100 = 180,00 €",
            "   Zieleinkaufspreis = 1.200,00 - 180,00 = 1.020,00 €",
            "SCHRITT 2: Skonto abziehen",
            "   Skonto = Zieleinkaufspreis x Skonto / 100",
            "   1.020,00 x 2 / 100 = 20,40 €",
            "   Bareinkaufspreis = 1.020,00 - 20,40 = 999,60 €",
            "SCHRITT 3: Bezugskosten addieren",
            "   Bezugspreis = 999,60 + 25,00 = 1.024,60 €",
            Cent), WisoBerichte.Rechenweg(WisoBerichte.Lesen("bezug", Vorgaben("bezug"))));
        Assert.Equal(L(
            "RECHENWEG VERKAUFSKALKULATION",
            "SCHRITT 1: Selbstkosten",
            "   Handlungskosten = 1.024,60 x 30 / 100 = 307,38 €",
            "   Selbstkosten = 1.024,60 + 307,38 = 1.331,98 €",
            "SCHRITT 2: Gewinn",
            "   Gewinn = 1.331,98 x 10 / 100 = 133,20 €",
            "   Barverkaufspreis = 1.331,98 + 133,20 = 1.465,18 €",
            "SCHRITT 3: Kundenskonto und Kundenrabatt (im Hundert)",
            "   Skonto und Rabatt werden vom Endpreis abgezogen. Deshalb",
            "   wird mit 100 minus Prozentsatz geteilt.",
            "   Kundenskonto = 1.465,18 x 2 / 98 = 29,90 €",
            "   Zielverkaufspreis = 1.465,18 + 29,90 = 1.495,08 €",
            "   Kundenrabatt = 1.495,08 x 10 / 90 = 166,12 €",
            "   Listenverkaufspreis netto = 1.495,08 + 166,12 = 1.661,20 €",
            "SCHRITT 4: Umsatzsteuer",
            "   Umsatzsteuer = 1.661,20 x 19 / 100 = 315,63 €",
            "   Bruttoverkaufspreis = 1.661,20 + 315,63 = 1.976,83 €",
            Cent), WisoBerichte.Rechenweg(WisoBerichte.Lesen("verkauf", Vorgaben("verkauf"))));
        Assert.Equal(L(
            "RECHENWEG DECKUNGSBEITRAG",
            "SCHRITT 1: Deckungsbeitrag je Stück",
            "   250,00 - 150,00 = 100,00 €",
            "SCHRITT 2: Betriebsergebnis",
            "   Deckungsbeitrag gesamt = 100,00 x 600 = 60.000,00 €",
            "   Betriebsergebnis = 60.000,00 - 40.000,00 = 20.000,00 €",
            "SCHRITT 3: Gewinnschwelle",
            "   Gewinnschwelle = Fixkosten / Deckungsbeitrag je Stück",
            "   40.000,00 / 100,00 = 400 Stück",
            "   Die Gewinnschwelle wird immer auf ganze Stück aufgerundet."), WisoBerichte.Rechenweg(WisoBerichte.Lesen("deckung", Vorgaben("deckung"))));
        Assert.Equal(L(
            "RECHENWEG ABSCHREIBUNG",
            "SCHRITT 1: Jährlicher Betrag",
            "   1.800,00 / 3 = 600,00 €",
            "SCHRITT 2: Erstes Jahr monatsgenau",
            "   Ab dem Monat der Anschaffung zählen 13 - 4 = 9 Monate.",
            "   600,00 x 9 / 12 = 450,00 €",
            "SCHRITT 3: Folgejahre",
            "   Jedes Jahr 600,00 €, bis der Restbuchwert 0 ist.",
            "   Im letzten Jahr wird nur der Rest abgeschrieben, 150,00 €.",
            Cent), WisoBerichte.Rechenweg(WisoBerichte.Lesen("afa", Vorgaben("afa"))));
        Assert.Equal(L(
            "RECHENWEG ZINSEN",
            "SCHRITT 1: Laufzeit in Tagen (1 Monat = 30 Tage, 1 Jahr = 360 Tage)",
            "   90 Tage",
            "SCHRITT 2: Zinsen berechnen",
            "   Zinsen = Kapital x Zinssatz x Tage / (100 x 360)",
            "   5.000,00 x 4,5 x 90 / 36.000 = 56,25 €",
            "   Kapital mit Zinsen = 5.000,00 + 56,25 = 5.056,25 €",
            Cent), WisoBerichte.Rechenweg(WisoBerichte.Lesen("zinsen", Vorgaben("zinsen"))));
        Assert.Equal(L(
            "RECHENWEG AMORTISATION",
            "SCHRITT 1: Rückfluss pro Jahr",
            "   4.800,00 - 800,00 = 4.000,00 €",
            "SCHRITT 2: Amortisationsdauer",
            "   Amortisationsdauer = Investition / Rückfluss pro Jahr",
            "   12.000,00 / 4.000,00 = 3,00 Jahre",
            "SCHRITT 3: In Jahren und Monaten",
            "   12.000,00 x 12 / 4.000,00 = 36 Monate = 3 Jahre"), WisoBerichte.Rechenweg(WisoBerichte.Lesen("amort", Vorgaben("amort"))));
    }

    [Fact]
    public void RechenwegSonderfaelle()
    {
        static string Zeile(string weg, string anfang) => weg.Split('\n').Single(z => z.StartsWith(anfang, StringComparison.Ordinal));
        Assert.Equal("   20.000,00 / 28,00 = 714,29, aufgerundet 715 Stück",
            Zeile(WisoBerichte.Rechenweg(Lesen("deckung", "tage", "80", "52", "20000", "900")), "   20.000,00 / "));
        Assert.Equal("   40.000,01 / 100,00 = mehr als 400, aufgerundet 401 Stück",
            Zeile(WisoBerichte.Rechenweg(Lesen("deckung", "tage", "250", "150", "40000,01", "600")), "   40.000,01 / "));
        Assert.Equal("   20.000,00 x 12 / 5.500,00 = 43,64 Monate, aufgerundet 44 Monate = 3 Jahre und 8 Monate",
            Zeile(WisoBerichte.Rechenweg(Lesen("amort", "tage", "20000", "6000", "500")), "   20.000,00 x 12"));
        // Januar: Schritt 2 entfaellt, dann "SCHRITT 2: Folgejahre"
        var afa = WisoBerichte.Rechenweg(Lesen("afa", "tage", "1800", "3", "1"));
        Assert.DoesNotContain("Erstes Jahr monatsgenau", afa, StringComparison.Ordinal);
        Assert.Contains("SCHRITT 2: Folgejahre", afa, StringComparison.Ordinal);
        Assert.Equal("   3 Monate x 30 = 90 Tage", WisoBerichte.Rechenweg(Lesen("zinsen", "monate", "5000", "4,5", "3")).Split('\n')[2]);
        Assert.Equal("   2 Jahre x 360 = 720 Tage", WisoBerichte.Rechenweg(Lesen("zinsen", "jahre", "5000", "4,5", "2")).Split('\n')[2]);
        // Deckungsbeitrag und Amortisation ohne Cent-Zeile
        Assert.DoesNotContain("volle Cent", WisoBerichte.Rechenweg(WisoBerichte.Lesen("deckung", Vorgaben("deckung"))), StringComparison.Ordinal);
        Assert.DoesNotContain("volle Cent", WisoBerichte.Rechenweg(WisoBerichte.Lesen("amort", Vorgaben("amort"))), StringComparison.Ordinal);
        // die letzte AfA-Zeile hat kein ": " (fit_lines laese sie sonst als Beschriftung)
        Assert.DoesNotContain(": ", Zeile(afa, "   Im letzten Jahr"), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- Fehler (Abschnitt 2)

    [Fact]
    public void FehlerWieWortliste()
    {
        // Leseregel unveraendert; Ganzzahlfelder mit "Zum Beispiel 1.000."
        Assert.Equal("Bitte einen Wert eingeben: Listeneinkaufspreis (€).", Fehler("bezug", "tage", "", "15", "2", "25"));
        Assert.Equal("Bitte eine Zahl eingeben: Rabatt (%). Zum Beispiel 1.000 oder 1,5.", Fehler("bezug", "tage", "1200", "abc", "2", "25"));
        Assert.Equal("Bitte eine Zahl eingeben: Menge (Stück). Zum Beispiel 1.000.", Fehler("deckung", "tage", "250", "150", "40000", "abc"));
        Assert.Equal("Bitte einen Wert eingeben: Laufzeit.", Fehler("zinsen", "tage", "5000", "4,5", ""));
        // Geldbetraege
        Assert.Equal("Bitte höchstens 2 Nachkommastellen (volle Cent) eingeben: Listeneinkaufspreis (€).", Fehler("bezug", "tage", "1200,001", "15", "2", "25"));
        Assert.Equal("Der Betrag darf nicht negativ sein: Bezugskosten (€).", Fehler("bezug", "tage", "1200", "15", "2", "-1"));
        Assert.Equal("Der Betrag muss größer als 0 sein: Anschaffungskosten (€).", Fehler("afa", "tage", "0", "3", "4"));
        Assert.Equal("Der Betrag muss größer als 0 sein: Investition (€).", Fehler("amort", "tage", "0", "4800", "800"));
        Assert.Equal("Der Betrag ist zu groß (höchstens 1.000.000.000 €): Kapital (€).", Fehler("zinsen", "tage", "1000000000,01", "4,5", "90"));
        // Prozentsaetze
        Assert.Equal("Der Prozentsatz muss kleiner als 100 sein: Rabatt (%).", Fehler("bezug", "tage", "1200", "100", "2", "25"));
        Assert.Equal("Der Prozentsatz muss kleiner als 100 sein: Kundenrabatt (%).", Fehler("verkauf", "tage", "1000", "30", "10", "2", "100", "19"));
        Assert.Equal("Der Prozentsatz darf höchstens 100 sein: Umsatzsteuer (%).", Fehler("verkauf", "tage", "1000", "30", "10", "2", "10", "100,001"));
        Assert.Equal("Der Prozentsatz darf höchstens 100 sein: Zinssatz (%).", Fehler("zinsen", "tage", "5000", "101", "90"));
        Assert.Equal("Der Prozentsatz darf nicht negativ sein: Gewinn (%).", Fehler("verkauf", "tage", "1000", "30", "-1", "2", "10", "19"));
        // 99,999 ist erlaubt, 100 bei Umsatzsteuer und Zinssatz auch
        Lesen("bezug", "tage", "1200", "99,999", "99,999", "25");
        Lesen("verkauf", "tage", "1000", "30", "10", "2", "10", "100");
        Lesen("zinsen", "tage", "5000", "100", "90");
        // ganze Zahlen mit Grenzen
        Assert.Equal("Bitte eine ganze Zahl eingeben: Menge (Stück).", Fehler("deckung", "tage", "250", "150", "40000", "1,5"));
        Assert.Equal("Bitte eine ganze Zahl von 1 bis 10.000.000 eingeben: Menge (Stück).", Fehler("deckung", "tage", "250", "150", "40000", "10000001"));
        Assert.Equal("Bitte eine ganze Zahl von 1 bis 50 eingeben: Nutzungsdauer (Jahre).", Fehler("afa", "tage", "1800", "51", "4"));
        Assert.Equal("Bitte eine ganze Zahl von 1 bis 12 eingeben: Monat der Anschaffung.", Fehler("afa", "tage", "1800", "3", "13"));
        Assert.Equal("Bitte eine ganze Zahl von 1 bis 12 eingeben: Monat der Anschaffung.", Fehler("afa", "tage", "1800", "3", "0"));
        Assert.Equal("Bitte eine ganze Zahl von 1 bis 360 eingeben: Laufzeit.", Fehler("zinsen", "tage", "5000", "4,5", "361"));
        Assert.Equal("Bitte eine ganze Zahl von 1 bis 600 eingeben: Laufzeit.", Fehler("zinsen", "monate", "5000", "4,5", "601"));
        Assert.Equal("Bitte eine ganze Zahl von 1 bis 50 eingeben: Laufzeit.", Fehler("zinsen", "jahre", "5000", "4,5", "51"));
        Assert.Equal("Bitte eine ganze Zahl von 1 bis 360 eingeben: Laufzeit.", Fehler("zinsen", "tage", "5000", "4,5", "0"));
        Assert.Equal("Bitte eine ganze Zahl eingeben: Laufzeit.", Fehler("zinsen", "monate", "5000", "4,5", "2,5"));
        Lesen("zinsen", "tage", "5000", "4,5", "360");
        Lesen("zinsen", "monate", "5000", "4,5", "600");
        Lesen("zinsen", "jahre", "5000", "4,5", "50");
        // rechnerweit
        Assert.Equal("Kein positiver Deckungsbeitrag: Es gibt keine Gewinnschwelle.", Fehler("deckung", "tage", "150", "150", "40000", "600"));
        Assert.Equal("Kein positiver Deckungsbeitrag: Es gibt keine Gewinnschwelle.", Fehler("deckung", "tage", "100", "150", "40000", "600"));
        Assert.Equal("Die Investition amortisiert sich nie.", Fehler("amort", "tage", "12000", "800", "800"));
        Assert.Equal("Die Investition amortisiert sich nie.", Fehler("amort", "tage", "12000", "700", "800"));
    }

    [Fact]
    public void ErsterFehlerGewinnt()
    {
        // erst alle Felder lesen: der Lesefehler unten schlaegt den Bereichsfehler oben
        Assert.Equal("Bitte eine Zahl eingeben: Bezugskosten (€). Zum Beispiel 1.000 oder 1,5.", Fehler("bezug", "tage", "-5", "15", "2", "x"));
        // dann je Feld von oben nach unten Form vor Bereich
        Assert.Equal("Bitte höchstens 2 Nachkommastellen (volle Cent) eingeben: Listeneinkaufspreis (€).", Fehler("bezug", "tage", "-1,001", "100", "2", "25"));
        Assert.Equal("Der Betrag darf nicht negativ sein: Listeneinkaufspreis (€).", Fehler("bezug", "tage", "-1", "100", "2", "25"));
        // zuletzt die rechnerweiten Fehler
        Assert.Equal("Bitte eine ganze Zahl von 1 bis 10.000.000 eingeben: Menge (Stück).", Fehler("deckung", "tage", "100", "150", "40000", "0"));
        // Laufzeit ist das letzte Feld: Kapital und Zinssatz zuerst
        Assert.Equal("Der Prozentsatz darf höchstens 100 sein: Zinssatz (%).", Fehler("zinsen", "tage", "5000", "101", "999"));
    }

    // ---------------------------------------------------------------- Zeilen hoechstens 28 (Punkt 3c)

    private static void Handybreite(string name, string text)
    {
        Assert.True(!string.Join("\n", text.Split('\n').Select(z => z.TrimStart(' '))).Contains("  ", StringComparison.Ordinal), name + ": Doppelleerzeichen");
        foreach (var zeile in Zeilen.Kasten(text, 28, einheitlich: true).Split('\n'))
        {
            Assert.True(zeile.Length <= 28 || !zeile.Trim().Contains(' ', StringComparison.Ordinal), name + ": \"" + zeile + "\"");
        }
    }

    [Fact]
    public void ZeilenHoechstens28AmHandy()
    {
        var faelle = new List<(string Rechner, string Einheit, string[] Werte)>
        {
            ("bezug", "tage", ["999999999,99", "99,999", "99,999", "999999999,99"]),
            ("bezug", "tage", ["1", "0", "0", "0"]),
            // Handlungskosten mit 6 Zeichen ("99,999") ergaeben am Handy 29 Zeichen: offen als Frage in Meldung P3-23
            ("verkauf", "tage", ["999999999,99", "99,99", "99,999", "99,999", "99,999", "100"]),
            ("verkauf", "tage", ["999999999,99", "9,999", "99,999", "99,999", "99,999", "99,999"]),
            ("verkauf", "tage", ["0", "0", "0", "0", "0", "0"]),
            ("deckung", "tage", ["1000000000", "0", "1000000000", "10000000"]),
            ("deckung", "tage", ["1000000000", "999999999,99", "1000000000", "1"]),
            ("deckung", "tage", ["80", "52", "20000", "900"]),
            ("deckung", "tage", ["250", "150", "40000,01", "600"]),
            ("afa", "tage", ["1000000000", "50", "12"]),
            ("afa", "tage", ["0,01", "1", "1"]),
            ("zinsen", "monate", ["1000000000", "100", "600"]),
            ("zinsen", "jahre", ["1000000000", "99,999", "50"]),
            ("zinsen", "tage", ["0,01", "0", "1"]),
            ("amort", "tage", ["1000000000", "1000000000", "999999999,99"]),
            ("amort", "tage", ["20000", "6000", "500"]),
            ("amort", "tage", ["1", "1000000000", "0"]),
        };
        foreach (var (rechner, _, _) in WisoAuswahl.Select(a => (a.Wert, "", Array.Empty<string>())))
        {
            faelle.Add((rechner, "tage", [.. WisoFelder[rechner].Select(f => f.Vorgabe)]));
        }
        foreach (var (rechner, einheit, werte) in faelle)
        {
            var e = Lesen(rechner, einheit, werte);
            var name = rechner + " " + string.Join("/", werte);
            Handybreite(name + " Ergebnis", WisoBerichte.Bericht(e, true));
            Handybreite(name + " Rechenweg", WisoBerichte.Rechenweg(e));
            // Handy-Ergebnis ohne Umbruch, wo die Zeile passt: jede Beschriftung hoechstens 27 Zeichen
            foreach (var zeile in WisoBerichte.Bericht(e, false).Split('\n').Where(z => z.Contains(": ", StringComparison.Ordinal)))
            {
                Assert.True(zeile.IndexOf(": ", StringComparison.Ordinal) <= 27, name + ": " + zeile);
            }
        }
        // alle WiSo-Aufgaben aus G31: Aufgabentext und Schritte
        var wiso = RechenTrainer.WisoArten.Select(a => a.Art).ToHashSet();
        var anzahl = 0;
        foreach (var soll in G31.GetProperty("aufgaben").EnumerateArray().Where(a => wiso.Contains(a.GetProperty("art").GetString()!)))
        {
            var a = RechenTrainer.Aufgabe(soll.GetProperty("art").GetString()!, soll.GetProperty("stufe").GetInt32(), soll.GetProperty("startwert").GetString()!);
            var name = $"{a.Art}-{a.Stufe}-{soll.GetProperty("startwert").GetString()}";
            Handybreite(name + " Text", a.Text);
            Handybreite(name + " Schritte", string.Join("\n", a.HandySchritte));
            Assert.All(a.Felder, f => Assert.True(f.Item2.Length <= 28, name + " " + f.Item2));
            anzahl++;
        }
        Assert.Equal(720, anzahl);
    }

    // ---------------------------------------------------------------- Trainer (Abschnitt 6, F5)

    [Fact]
    public void TrainerPrueftGenau()
    {
        var genau = WisoTrainer.GenauPruefer("1.125,79");
        Assert.True(genau("1.125,79"));
        Assert.True(genau("1125,79"));
        Assert.True(genau("1125,790"));
        Assert.False(genau("1125,78"));
        Assert.False(genau("1125,80"));
        Assert.False(genau("1125,7901"));
        Assert.False(genau("abc"));
        var ganz = WisoTrainer.GenauPruefer("630");
        Assert.True(ganz("630,00"));
        Assert.True(ganz("630"));
        Assert.False(ganz("630,01"));
        // Ganzzahl-Ergebnisse (Stueck, Jahre, Monate)
        var stueck = WisoTrainer.GenauPruefer("715");
        Assert.True(stueck("715"));
        Assert.False(stueck("714"));
        Assert.False(stueck("714,29"));
    }

    [Fact]
    public void TrainerFelderUndTexteWieWortliste()
    {
        foreach (var (art, _) in RechenTrainer.WisoArten)
        {
            for (var stufe = 1; stufe <= 3; stufe++)
            {
                foreach (var a in RechenTrainer.RundeBauen(art, stufe, "4711", 10))
                {
                    Assert.True(a.Zahlenfelder);
                    var felder = a.Felder.Select(f => f.Beschriftung).ToList();
                    var soll = (art, stufe) switch
                    {
                        ("deckung", 3) => (List<string>)["Ergebnis (Stück)"],
                        ("amort", 1) or ("amort", 2) => ["Ergebnis (Jahre)"],
                        ("amort", 3) => ["Jahre", "Monate"],
                        _ => ["Ergebnis (€)"],
                    };
                    Assert.Equal(soll, felder);
                    // mehr als ein gerundeter Schritt -> "Runde jeden Zwischenschritt auf volle Cent."
                    if ((art, stufe) is ("bezug", 2) or ("bezug", 3) or ("verkauf", 2) or ("verkauf", 3))
                    {
                        Assert.EndsWith(" Runde jeden Zwischenschritt auf volle Cent.", a.Text, StringComparison.Ordinal);
                    }
                    Assert.DoesNotContain("  ", a.Text, StringComparison.Ordinal);
                }
            }
        }
        // Beispiele der Wortliste: Wortlaut der Aufgabentexte (Muster)
        Assert.Equal("Ein Kapital von 8.000,00 € wird 2 Jahre lang zu 3,5 % verzinst. Wie hoch sind die Zinsen?",
            Beschriftungen.Einsetzen(AufgabeZinsen, "8.000,00", "2 Jahre", "3,5") + AufgabeZinsenFrage);
    }

    [Fact]
    public void KeinSchrittAufEinemHalbenCent()
    {
        // Neuziehen (einheitlich): kein Schritt "a x b / c = r" liegt genau auf einem halben Cent, und r ist
        // der kaufmaennisch gerundete genaue Wert; gerechnet exakt mit decimal aus den angezeigten Zahlen
        var muster = new System.Text.RegularExpressions.Regex(@": (Z(?: x Z)*) / (Z) = (Z)".Replace("Z", "[0-9.]+(?:,[0-9]+)?", StringComparison.Ordinal));
        var geprueft = 0;
        foreach (var (art, _) in RechenTrainer.WisoArten)
        {
            for (var stufe = 1; stufe <= 3; stufe++)
            {
                for (var s = 0; s < 50; s++)
                {
                    foreach (var a in RechenTrainer.RundeBauen(art, stufe, "halb-" + s, 10))
                    {
                        foreach (var schritt in a.Schritte)
                        {
                            var m = muster.Match(schritt);
                            if (!m.Success)
                            {
                                continue;
                            }
                            var zaehler = m.Groups[1].Value.Split(" x ").Aggregate(1m, (p, t) => p * Wert(t));
                            var nenner = Wert(m.Groups[2].Value);
                            // eigene Rechnung, nicht WisoTrainer.Halb: zaehler x 200 / nenner ist eine ungerade ganze Zahl
                            Assert.False(zaehler * 200 % nenner == 0 && zaehler * 200 / nenner % 2 == 1, a.Text + " | " + schritt);
                            Assert.Equal(Geld.Runden(zaehler / nenner), Wert(m.Groups[3].Value));
                            geprueft++;
                        }
                    }
                }
            }
        }
        Assert.True(geprueft > 10000, geprueft.ToString(System.Globalization.CultureInfo.InvariantCulture));

        static decimal Wert(string text) => Geld.Wert(text, out var w) ? w : throw new FormatException(text);
    }

    [Fact]
    public void GeldAnzeigeWieF3()
    {
        var b = WisoBerichte.Bericht(Lesen("deckung", "tage", "250", "150", "40000", "360"), false);
        Assert.Contains("-4.000,00 €", b, StringComparison.Ordinal);
        Assert.Contains("1.024,60 €", WisoBerichte.Bericht(WisoBerichte.Lesen("bezug", Vorgaben("bezug")), false), StringComparison.Ordinal);
        Assert.Contains(": 0,00 €", WisoBerichte.Bericht(Lesen("bezug", "tage", "1", "0", "0", "0"), false), StringComparison.Ordinal);
    }
}
