using System.Numerics;
using Lernplattform.Core.Texte;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3 W4c, W3 (Weisung P3-23): Lesen, Ergebniszeilen und Rechenwege der Karte "WiSo-Rechner"
/// nach der freigegebenen Wortliste (Fi-Austausch anlagen/P3-22_Wortliste_W4c.md). Gerechnet wird
/// nur in <see cref="Wiso"/> (G30); hier stehen keine Rundungsregeln, nur Zahltexte ueber
/// <see cref="Geld.Text"/>. Erst werden alle Felder mit der Leseregel gelesen (Ganzzahl-Felder mit
/// dem Beispiel aus Zahlen.LesenGanz), danach pruefen Form und Bereich in der Reihenfolge der Felder.
/// Den Umbruch uebernimmt der Ergebniskasten (fit_lines, PC 75 / Handy 28 Zeichen); im Zeilenkern
/// der Rechenwege steht nur Einfachleerzeichen.
/// </summary>
public static class WisoBerichte
{
    /// <summary>Beschriftungen bis Spalte 27, dann ": " (Wortliste Abschnitt 4).</summary>
    private const int Spalte = 27;

    private static readonly HashSet<string> GanzFelder = new(StringComparer.Ordinal) { "menge", "nd", "monat", "laufzeit" };
    private static readonly HashSet<string> GeldFelder = new(StringComparer.Ordinal)
        { "lep", "bezugskosten", "bp", "preis", "vk", "fix", "ak", "kapital", "investition", "einsparung", "laufend" };

    /// <summary>Grenzen der Laufzeit je Pille (F2): Tage 1-360, Monate 1-600, Jahre 1-50; Tage je Einheit.</summary>
    public static (int Hoechst, int TageJe) Laufzeit(string einheit) => einheit switch
    {
        "tage" => (360, 1),
        "monate" => (600, 30),
        "jahre" => (50, 360),
        _ => throw new ArgumentException("unbekannte Laufzeit: " + einheit, nameof(einheit)),
    };

    private static string Beschriftung(string rechner, string feld) => WisoFelder[rechner].First(f => f.Schluessel == feld).Beschriftung;

    // ---------------------------------------------------------------- Lesen

    /// <summary>Liest die Felder eines Rechners und rechnet; RechnerFehler mit dem Wortlaut der Wortliste.</summary>
    public static WisoEingabe Lesen(string rechner, IReadOnlyDictionary<string, string?> texte, string laufzeitEinheit = "tage")
    {
        var felder = WisoFelder[rechner];
        var w = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (schluessel, beschriftung, _) in felder)
        {
            var text = texte.TryGetValue(schluessel, out var t) ? t : null;
            if (Geld.Wert(text, out var wert))
            {
                w[schluessel] = wert;
            }
            else if (GanzFelder.Contains(schluessel))
            {
                Zahlen.LesenGanz(text, beschriftung);   // wirft den Lesefehler mit "Zum Beispiel 1.000."
            }
            else
            {
                Geld.Lesen(text, beschriftung);         // wirft den Lesefehler der Leseregel
            }
        }
        var hinweis = Zahlen.TausenderHinweis([.. felder.Select(f => texte.TryGetValue(f.Schluessel, out var t) ? t : null)]);
        object? ergebnis;
        WisoFehler? fehler;
        switch (rechner)
        {
            case "bezug":
                (fehler, ergebnis) = Teile(Wiso.Bezug(w["lep"], w["rabatt"], w["skonto"], w["bezugskosten"]));
                break;
            case "verkauf":
                (fehler, ergebnis) = Teile(Wiso.Verkauf(w["bp"], w["hk"], w["gewinn"], w["kskonto"], w["krabatt"], w["ust"]));
                break;
            case "deckung":
                (fehler, ergebnis) = Teile(Wiso.Deckung(w["preis"], w["vk"], w["fix"], w["menge"]));
                break;
            case "afa":
                (fehler, ergebnis) = Teile(Wiso.Abschreibung(w["ak"], w["nd"], w["monat"]));
                break;
            case "zinsen":
                {
                    // Laufzeit ist das letzte Feld: erst Kapital und Zinssatz pruefen, dann die Laufzeit
                    var (hoechst, je) = Laufzeit(laufzeitEinheit);
                    var lz = w["laufzeit"];
                    string? lzFehler = !Geld.IstGanz(lz)
                        ? Beschriftungen.Einsetzen(FehlerGanz, FeldLaufzeit)
                        : lz < 1 || lz > hoechst
                            ? Beschriftungen.Einsetzen(FehlerGanzBereich, "1", Zahlen.DeGanz(new BigInteger(hoechst)), FeldLaufzeit)
                            : null;
                    (fehler, ergebnis) = Teile(Wiso.Zinsen(w["kapital"], w["zinssatz"], lzFehler is null ? lz * je : 1));
                    if (fehler is null && lzFehler is not null)
                    {
                        throw new RechnerFehler(lzFehler);
                    }
                    break;
                }
            case "amort":
                (fehler, ergebnis) = Teile(Wiso.Amortisation(w["investition"], w["einsparung"], w["laufend"]));
                break;
            default:
                throw new ArgumentException("unbekannter Rechner: " + rechner, nameof(rechner));
        }
        if (fehler is not null)
        {
            throw new RechnerFehler(Fehlertext(rechner, fehler, fehler.Feld is null ? 0 : w[fehler.Feld]));
        }
        return new WisoEingabe(rechner, w, laufzeitEinheit, ergebnis!, hinweis);
    }

    private static (WisoFehler?, object?) Teile<T>(WisoErgebnis<T> e) where T : class => (e.Fehler, e.Werte);

    /// <summary>Fehlertext der Wortliste (Abschnitt 2) zu einem Fehler aus <see cref="Wiso"/>.</summary>
    public static string Fehlertext(string rechner, WisoFehler f, decimal wert)
    {
        if (f.Feld is null)
        {
            return f.Art == WisoFehlerArt.KeinDeckungsbeitrag ? FehlerKeinDb : FehlerNie;
        }
        var b = Beschriftung(rechner, f.Feld);
        if (GanzFelder.Contains(f.Feld))
        {
            if (f.Art == WisoFehlerArt.NichtGanz)
            {
                return Beschriftungen.Einsetzen(FehlerGanz, b);
            }
            var hoechst = f.Feld switch { "menge" => Wiso.HoechstMenge, "nd" => Wiso.HoechstNutzungsdauer, _ => 12 };
            return Beschriftungen.Einsetzen(FehlerGanzBereich, "1", Zahlen.DeGanz(new BigInteger(hoechst)), b);
        }
        if (GeldFelder.Contains(f.Feld))
        {
            return Beschriftungen.Einsetzen(f.Art switch
            {
                WisoFehlerArt.Nachkommastellen => FehlerCent,
                WisoFehlerArt.ZuKlein => wert < 0 ? FehlerNegativ : FehlerNull,
                _ => FehlerBetragGross,
            }, b);
        }
        return Beschriftungen.Einsetzen(f.Art == WisoFehlerArt.ZuKlein ? FehlerProzentNegativ
            : f.Feld is "ust" or "zinssatz" ? FehlerProzentHoechst : FehlerProzent100, b);
    }

    // ---------------------------------------------------------------- Zahltexte

    private static string E(decimal x) => Geld.Text(x) + " " + Euro;

    private static string G(decimal x) => Geld.Text(x);

    private static string P(decimal x) => Geld.Kurz(x);

    private static string Z(BigInteger n) => Zahlen.DeGanz(n);

    private static string Mz(BigInteger n, string einzahl, string mehrzahl) => Zahlen.Mehrzahl(n, einzahl, mehrzahl);

    /// <summary>"3 Jahre", "1 Jahr und 1 Monat", "8 Monate" (F1, ohne "0 Jahre").</summary>
    public static string JahreUndMonate(long monate)
    {
        var jahre = monate / 12;
        var rest = monate % 12;
        if (jahre == 0)
        {
            return Mz(rest, Monat, MonateText);
        }
        var text = Mz(jahre, Jahr, JahreText);
        return rest == 0 ? text : text + Und + Mz(rest, Monat, MonateText);
    }

    /// <summary>Quotient auf 2 Stellen; ist er glatt, obwohl die Teilung nicht aufgeht: "mehr als N".</summary>
    private static string Quotient(decimal zaehler, decimal nenner, long aufgerundet, string einheit) =>
        G(Geld.Runden(zaehler / nenner)).EndsWith(",00", StringComparison.Ordinal)
            ? Beschriftungen.Einsetzen(MehrAls, Z(aufgerundet - 1) + einheit)
            : G(Geld.Runden(zaehler / nenner)) + einheit;

    private static string Zeile(string beschriftung, string wert, bool schmal) =>
        (schmal ? beschriftung + " " : beschriftung.PadRight(Spalte)) + ": " + wert;

    private static string Laufzeit(WisoEingabe e)
    {
        var n = (long)e.Werte["laufzeit"];
        var tage = Mz(n * Laufzeit(e.LaufzeitEinheit).TageJe, Tag, Tage);
        return e.LaufzeitEinheit switch
        {
            "monate" => Mz(n, Monat, MonateText),
            "jahre" => Mz(n, Jahr, JahreText),
            _ => tage,
        } + (e.LaufzeitEinheit == "tage" ? "" : " = " + tage);
    }

    // ---------------------------------------------------------------- Ergebnis

    /// <summary>Ergebniszeilen PC = Handy (Wortliste Abschnitt 4), danach ggf. "Punkt als Tausender gelesen.".</summary>
    public static string Bericht(WisoEingabe e, bool schmal)
    {
        var w = e.Werte;
        var z = new List<string>();
        void Add(string b, string wert) => z.Add(Zeile(b, wert, schmal));
        string Pz(string muster, string feld) => Beschriftungen.Einsetzen(muster, P(w[feld]));
        switch (e.Ergebnis)
        {
            case BezugWerte b:
                Add(ZeileLep, E(w["lep"]));
                Add(Pz(ZeileRabatt, "rabatt"), E(b.Rabatt));
                Add(ZeileZep, E(b.Zep));
                Add(Pz(ZeileSkonto, "skonto"), E(b.Skonto));
                Add(ZeileBep, E(b.Bep));
                Add(ZeileBezugskosten, E(w["bezugskosten"]));
                Add(ZeileBezugspreis, E(b.Bezugspreis));
                break;
            case VerkaufWerte v:
                Add(ZeileBp, E(w["bp"]));
                Add(Pz(ZeileHk, "hk"), E(v.Hk));
                Add(ZeileSk, E(v.Sk));
                Add(Pz(ZeileGewinn, "gewinn"), E(v.Gewinn));
                Add(ZeileBvp, E(v.Bvp));
                Add(Pz(ZeileKskonto, "kskonto"), E(v.Kskonto));
                Add(ZeileZvp, E(v.Zvp));
                Add(Pz(ZeileKrabatt, "krabatt"), E(v.Krabatt));
                Add(ZeileLvp, E(v.Lvp));
                Add(Pz(ZeileUst, "ust"), E(v.Ust));
                Add(ZeileBrutto, E(v.Brutto));
                break;
            case DeckungWerte d:
                Add(ZeilePreis, E(w["preis"]));
                Add(ZeileVariabel, E(w["vk"]));
                Add(ZeileDb, E(d.Db));
                Add(ZeileMenge, Z(new BigInteger(w["menge"])) + " " + Stueck);
                Add(ZeileDbGesamt, E(d.DbGesamt));
                Add(ZeileFix, E(w["fix"]));
                Add(ZeileErgebnis, E(d.Ergebnis) + (d.Ergebnis > 0 ? ZusatzGewinn : d.Ergebnis < 0 ? ZusatzVerlust : ""));
                Add(ZeileGewinnschwelle, Z(d.Gewinnschwelle) + " " + Stueck);
                break;
            case AfaWerte a:
                var monat = (int)w["monat"];
                Add(ZeileAk, E(w["ak"]));
                Add(ZeileNd, Mz((long)w["nd"], Jahr, JahreText));
                Add(ZeileMonat, monat + " (" + Monatsnamen[monat - 1] + ")");
                Add(ZeileJaehrlich, E(a.Jaehrlich));
                foreach (var j in a.Jahre)
                {
                    var b = j.Monate < 12
                        ? Beschriftungen.Einsetzen(ZeileJahrMonate, j.Jahr.ToString(System.Globalization.CultureInfo.InvariantCulture), Mz(j.Monate, Monat, MonateText))
                        : Beschriftungen.Einsetzen(ZeileJahr, j.Jahr.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Add(b, Beschriftungen.Einsetzen(ZeileRestbuchwert, E(j.Betrag), E(j.Rest)));
                }
                break;
            case ZinsenWerte zi:
                Add(ZeileKapital, E(w["kapital"]));
                Add(ZeileZinssatz, P(w["zinssatz"]) + " %");
                Add(ZeileLaufzeit, Laufzeit(e));
                Add(ZeileZinsen, E(zi.Zinsen));
                Add(ZeileEndkapital, E(zi.Endkapital));
                break;
            case AmortWerte am:
                Add(ZeileInvestition, E(w["investition"]));
                Add(ZeileEinsparung, E(w["einsparung"]));
                Add(ZeileLaufend, E(w["laufend"]));
                Add(ZeileRueckfluss, E(am.Rueckfluss));
                Add(ZeileAmortDauer, G(am.Jahre) + " " + JahreText);
                Add(ZeileJahreMonate, JahreUndMonate(am.Monate));
                break;
        }
        z.AddRange(e.Hinweis);
        return string.Join("\n", z);
    }

    // ---------------------------------------------------------------- Rechenweg

    /// <summary>Rechenweg mit den eingegebenen Zahlen (Wortliste Abschnitt 5).</summary>
    public static string Rechenweg(WisoEingabe e)
    {
        var w = e.Werte;
        var z = new List<string>();
        switch (e.Ergebnis)
        {
            case BezugWerte b:
                z.AddRange([BezugExplainTitle, BezugStep1, BezugFormelRabatt,
                    "   " + G(w["lep"]) + " x " + P(w["rabatt"]) + " / 100 = " + E(b.Rabatt),
                    "   Zieleinkaufspreis = " + G(w["lep"]) + " - " + G(b.Rabatt) + " = " + E(b.Zep),
                    BezugStep2, BezugFormelSkonto,
                    "   " + G(b.Zep) + " x " + P(w["skonto"]) + " / 100 = " + E(b.Skonto),
                    "   Bareinkaufspreis = " + G(b.Zep) + " - " + G(b.Skonto) + " = " + E(b.Bep),
                    BezugStep3,
                    "   Bezugspreis = " + G(b.Bep) + " + " + G(w["bezugskosten"]) + " = " + E(b.Bezugspreis),
                    WisoCent]);
                break;
            case VerkaufWerte v:
                z.AddRange([VerkaufExplainTitle, VerkaufStep1,
                    "   Handlungskosten = " + G(w["bp"]) + " x " + P(w["hk"]) + " / 100 = " + E(v.Hk),
                    "   Selbstkosten = " + G(w["bp"]) + " + " + G(v.Hk) + " = " + E(v.Sk),
                    VerkaufStep2,
                    "   Gewinn = " + G(v.Sk) + " x " + P(w["gewinn"]) + " / 100 = " + E(v.Gewinn),
                    "   Barverkaufspreis = " + G(v.Sk) + " + " + G(v.Gewinn) + " = " + E(v.Bvp),
                    VerkaufStep3, .. VerkaufImHundert,
                    "   Kundenskonto = " + G(v.Bvp) + " x " + P(w["kskonto"]) + " / " + P(100 - w["kskonto"]) + " = " + E(v.Kskonto),
                    "   Zielverkaufspreis = " + G(v.Bvp) + " + " + G(v.Kskonto) + " = " + E(v.Zvp),
                    "   Kundenrabatt = " + G(v.Zvp) + " x " + P(w["krabatt"]) + " / " + P(100 - w["krabatt"]) + " = " + E(v.Krabatt),
                    "   Listenverkaufspreis netto = " + G(v.Zvp) + " + " + G(v.Krabatt) + " = " + E(v.Lvp),
                    VerkaufStep4,
                    "   Umsatzsteuer = " + G(v.Lvp) + " x " + P(w["ust"]) + " / 100 = " + E(v.Ust),
                    "   Bruttoverkaufspreis = " + G(v.Lvp) + " + " + G(v.Ust) + " = " + E(v.Brutto),
                    WisoCent]);
                break;
            case DeckungWerte d:
                var glatt = w["fix"] % d.Db == 0;
                z.AddRange([DeckungExplainTitle, DeckungStep1,
                    "   " + G(w["preis"]) + " - " + G(w["vk"]) + " = " + E(d.Db),
                    DeckungStep2,
                    "   Deckungsbeitrag gesamt = " + G(d.Db) + " x " + Z(new BigInteger(w["menge"])) + " = " + E(d.DbGesamt),
                    "   Betriebsergebnis = " + G(d.DbGesamt) + " - " + G(w["fix"]) + " = " + E(d.Ergebnis),
                    DeckungStep3, DeckungFormel,
                    "   " + G(w["fix"]) + " / " + G(d.Db) + " = " + (glatt
                        ? Z(d.Gewinnschwelle) + " " + Stueck
                        : Beschriftungen.Einsetzen(Aufgerundet, Quotient(w["fix"], d.Db, d.Gewinnschwelle, ""), Z(d.Gewinnschwelle) + " " + Stueck)),
                    DeckungAufrunden]);
                break;
            case AfaWerte a:
                var monat = (int)w["monat"];
                z.AddRange([AfaExplainTitle, AfaStep1, "   " + G(w["ak"]) + " / " + Z(new BigInteger(w["nd"])) + " = " + E(a.Jaehrlich)]);
                var schritt = 2;
                if (monat > 1)
                {
                    z.AddRange([AfaStep2,
                        Beschriftungen.Einsetzen(AfaMonate, monat.ToString(System.Globalization.CultureInfo.InvariantCulture), Mz(13 - monat, Monat, MonateText)),
                        "   " + G(a.Jaehrlich) + " x " + (13 - monat).ToString(System.Globalization.CultureInfo.InvariantCulture) + " / 12 = " + E(a.Jahre[0].Betrag)]);
                    schritt = 3;
                }
                z.AddRange([Beschriftungen.Einsetzen(AfaStepFolge, schritt.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    Beschriftungen.Einsetzen(AfaJedesJahr, E(a.Jaehrlich)),
                    Beschriftungen.Einsetzen(AfaLetztes, E(a.Jahre[^1].Betrag)),
                    WisoCent]);
                break;
            case ZinsenWerte zi:
                var n = (long)w["laufzeit"];
                var tage = n * Laufzeit(e.LaufzeitEinheit).TageJe;
                z.AddRange([ZinsenExplainTitle, ZinsenStep1, e.LaufzeitEinheit switch
                {
                    "monate" => "   " + Mz(n, Monat, MonateText) + " x 30 = " + Mz(tage, Tag, Tage),
                    "jahre" => "   " + Mz(n, Jahr, JahreText) + " x 360 = " + Mz(tage, Tag, Tage),
                    _ => "   " + Mz(tage, Tag, Tage),
                }, ZinsenStep2, ZinsenFormel,
                    "   " + G(w["kapital"]) + " x " + P(w["zinssatz"]) + " x " + Z(tage) + " / 36.000 = " + E(zi.Zinsen),
                    "   Kapital mit Zinsen = " + G(w["kapital"]) + " + " + G(zi.Zinsen) + " = " + E(zi.Endkapital),
                    WisoCent]);
                break;
            case AmortWerte am:
                var i = w["investition"];
                var monate = Mz(am.Monate, Monat, MonateText);
                z.AddRange([AmortExplainTitle, AmortStep1,
                    "   " + G(w["einsparung"]) + " - " + G(w["laufend"]) + " = " + E(am.Rueckfluss),
                    AmortStep2, AmortFormel,
                    "   " + G(i) + " / " + G(am.Rueckfluss) + " = " + G(am.Jahre) + " " + JahreText,
                    AmortStep3,
                    "   " + G(i) + " x 12 / " + G(am.Rueckfluss) + " = " + (i * 12 % am.Rueckfluss == 0
                        ? monate
                        : Beschriftungen.Einsetzen(Aufgerundet, Quotient(i * 12, am.Rueckfluss, am.Monate, " " + MonateText), monate))
                    + (am.Monate < 12 ? "" : " = " + JahreUndMonate(am.Monate))]);   // unter 12 Monaten nicht doppelt "8 Monate = 8 Monate"
                break;
        }
        return string.Join("\n", z);
    }
}

/// <summary>Gelesene Eingabe eines WiSo-Rechners: Werte je Feld, Laufzeit-Einheit, Ergebnis (Werte-Record) und Hinweis.</summary>
public sealed record WisoEingabe(string Rechner, IReadOnlyDictionary<string, decimal> Werte, string LaufzeitEinheit, object Ergebnis,
    IReadOnlyList<string> Hinweis);
