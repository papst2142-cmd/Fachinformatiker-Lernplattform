namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3 W4c, W2 (Weisungen P3-16/P3-22): Rechenkern der sechs WiSo-Rechner (Plan P3-15
/// Abschnitt 4.4), ohne Texte. Kein Python-Vorbild; Vergleichswerte G30 aus dem unabhaengigen
/// Skript werkzeuge/vergleich/erzeuge_w4_wiso.py. Alles in decimal (<see cref="Geld"/>): jeder
/// Schritt mit Geldbetrag wird auf Cent gerundet, in jeder Formel erst multipliziert, dann
/// geteilt. Felder werden in fester Reihenfolge geprueft (erst Form, dann Bereich); der erste
/// Fehler gewinnt. Die Feldschluessel sind dieselben wie in G30.
/// </summary>
public static class Wiso
{
    /// <summary>Hoechster Prozentsatz fuer Rabatt, Skonto und Zuschlaege (unter 100, sonst Teilung durch 0).</summary>
    public const decimal HoechstProzent = 99.999m;

    /// <summary>Hoechste Menge (Stueck).</summary>
    public const long HoechstMenge = 10_000_000;

    /// <summary>Hoechste Nutzungsdauer in Jahren.</summary>
    public const int HoechstNutzungsdauer = 50;

    /// <summary>Hoechste Laufzeit in Tagen (50 Jahre zu 360 Tagen).</summary>
    public const int HoechstTage = 18_000;

    // ---------------------------------------------------------------- Pruefen

    private static WisoFehler? Betrag(string feld, decimal wert, bool mehrAlsNull = false)
    {
        if (Geld.MehrAlsCent(wert))
        {
            return new WisoFehler(feld, WisoFehlerArt.Nachkommastellen);
        }
        if (wert < 0 || (mehrAlsNull && wert == 0))
        {
            return new WisoFehler(feld, WisoFehlerArt.ZuKlein);
        }
        return wert > Geld.HoechstBetrag ? new WisoFehler(feld, WisoFehlerArt.ZuGross) : null;
    }

    private static WisoFehler? Prozent(string feld, decimal wert, decimal hoechst = HoechstProzent)
    {
        if (wert < 0)
        {
            return new WisoFehler(feld, WisoFehlerArt.ZuKlein);
        }
        return wert > hoechst ? new WisoFehler(feld, WisoFehlerArt.ZuGross) : null;
    }

    private static WisoFehler? Ganz(string feld, decimal wert, long kleinst, long hoechst)
    {
        if (!Geld.IstGanz(wert))
        {
            return new WisoFehler(feld, WisoFehlerArt.NichtGanz);
        }
        if (wert < kleinst)
        {
            return new WisoFehler(feld, WisoFehlerArt.ZuKlein);
        }
        return wert > hoechst ? new WisoFehler(feld, WisoFehlerArt.ZuGross) : null;
    }

    private static WisoFehler? Erster(params WisoFehler?[] fehler) => fehler.FirstOrDefault(f => f is not null);

    private static decimal R(decimal wert) => Geld.Runden(wert);

    // ---------------------------------------------------------------- Rechner

    /// <summary>Bezugskalkulation: Listeneinkaufspreis - Rabatt - Skonto + Bezugskosten.</summary>
    public static WisoErgebnis<BezugWerte> Bezug(decimal lep, decimal rabatt, decimal skonto, decimal bezugskosten)
    {
        var fehler = Erster(Betrag("lep", lep), Prozent("rabatt", rabatt), Prozent("skonto", skonto), Betrag("bezugskosten", bezugskosten));
        if (fehler is not null)
        {
            return new(fehler, null);
        }
        var rabattEur = R(lep * rabatt / 100);
        var zep = lep - rabattEur;
        var skontoEur = R(zep * skonto / 100);
        var bep = zep - skontoEur;
        return new(null, new BezugWerte(rabattEur, zep, skontoEur, bep, bep + bezugskosten));
    }

    /// <summary>
    /// Verkaufskalkulation bis brutto: Handlungskosten und Gewinn vom Hundert, Kundenskonto und
    /// Kundenrabatt im Hundert (x * s / (100 - s)), Umsatzsteuer vom Hundert.
    /// </summary>
    public static WisoErgebnis<VerkaufWerte> Verkauf(decimal bp, decimal hk, decimal gewinn, decimal kskonto, decimal krabatt, decimal ust)
    {
        var fehler = Erster(Betrag("bp", bp), Prozent("hk", hk), Prozent("gewinn", gewinn), Prozent("kskonto", kskonto),
            Prozent("krabatt", krabatt), Prozent("ust", ust, 100));
        if (fehler is not null)
        {
            return new(fehler, null);
        }
        var hkEur = R(bp * hk / 100);
        var sk = bp + hkEur;
        var gewinnEur = R(sk * gewinn / 100);
        var bvp = sk + gewinnEur;
        var kskontoEur = R(bvp * kskonto / (100 - kskonto));
        var zvp = bvp + kskontoEur;
        var krabattEur = R(zvp * krabatt / (100 - krabatt));
        var lvp = zvp + krabattEur;
        var ustEur = R(lvp * ust / 100);
        return new(null, new VerkaufWerte(hkEur, sk, gewinnEur, bvp, kskontoEur, zvp, krabattEur, lvp, ustEur, lvp + ustEur));
    }

    /// <summary>Deckungsbeitrag, Betriebsergebnis und Gewinnschwelle (aufgerundet auf ganze Stueck).</summary>
    public static WisoErgebnis<DeckungWerte> Deckung(decimal preis, decimal vk, decimal fix, decimal menge)
    {
        var fehler = Erster(Betrag("preis", preis), Betrag("vk", vk), Betrag("fix", fix), Ganz("menge", menge, 1, HoechstMenge));
        if (fehler is not null)
        {
            return new(fehler, null);
        }
        var db = preis - vk;
        if (db <= 0)
        {
            return new(new WisoFehler(null, WisoFehlerArt.KeinDeckungsbeitrag), null);
        }
        var gesamt = db * menge;
        return new(null, new DeckungWerte(db, gesamt, gesamt - fix, R(fix / db), Geld.Aufrunden(fix, db)));
    }

    /// <summary>
    /// Lineare Abschreibung: jaehrlich = AK / Nutzungsdauer; im ersten Jahr monatsgenau
    /// jaehrlich * (13 - Monat) / 12; im letzten Jahr der Rest; nie mehr als der Restbuchwert.
    /// </summary>
    public static WisoErgebnis<AfaWerte> Abschreibung(decimal ak, decimal nd, decimal monat)
    {
        var fehler = Erster(Betrag("ak", ak, mehrAlsNull: true), Ganz("nd", nd, 1, HoechstNutzungsdauer), Ganz("monat", monat, 1, 12));
        if (fehler is not null)
        {
            return new(fehler, null);
        }
        var jahre = (int)nd;
        var m = (int)monat;
        var jaehrlich = R(ak / jahre);
        var anzahl = m == 1 ? jahre : jahre + 1;
        var liste = new List<AfaJahr>();
        var rest = ak;
        for (var jahr = 1; jahr <= anzahl; jahr++)
        {
            decimal geplant;
            int monate;
            if (jahr == anzahl)
            {
                geplant = rest;
                monate = m == 1 ? 12 : (jahr == 1 ? 13 - m : m - 1);
            }
            else if (jahr == 1 && m > 1)
            {
                geplant = R(jaehrlich * (13 - m) / 12);
                monate = 13 - m;
            }
            else
            {
                geplant = jaehrlich;
                monate = 12;
            }
            var betrag = Math.Min(geplant, rest);
            rest -= betrag;
            liste.Add(new AfaJahr(jahr, monate, betrag, rest));
        }
        return new(null, new AfaWerte(jaehrlich, liste));
    }

    /// <summary>Kaufmaennische Zinsen (30/360): Z = K * p * t / (100 * 360), t in Tagen.</summary>
    public static WisoErgebnis<ZinsenWerte> Zinsen(decimal kapital, decimal zinssatz, decimal tage)
    {
        var fehler = Erster(Betrag("kapital", kapital), Prozent("zinssatz", zinssatz, 100), Ganz("tage", tage, 1, HoechstTage));
        if (fehler is not null)
        {
            return new(fehler, null);
        }
        var zinsen = R(kapital * zinssatz * tage / 36000);
        return new(null, new ZinsenWerte(zinsen, kapital + zinsen));
    }

    /// <summary>
    /// Statische Amortisation: Jahre = Investition / (Einsparung - laufende Kosten), auf 2 Stellen.
    /// Monatsregel (F1, Weisung P3-23): Monate gesamt = Investition x 12 / Rueckfluss, exakt ueber
    /// den Rest auf ganze Monate aufgerundet, nie ueber die gerundete Jahreszahl.
    /// </summary>
    public static WisoErgebnis<AmortWerte> Amortisation(decimal investition, decimal einsparung, decimal laufend)
    {
        var fehler = Erster(Betrag("investition", investition, mehrAlsNull: true), Betrag("einsparung", einsparung), Betrag("laufend", laufend));
        if (fehler is not null)
        {
            return new(fehler, null);
        }
        var rueckfluss = einsparung - laufend;
        if (rueckfluss <= 0)
        {
            return new(new WisoFehler(null, WisoFehlerArt.Nie), null);
        }
        return new(null, new AmortWerte(rueckfluss, R(investition / rueckfluss), Geld.Aufrunden(investition * 12, rueckfluss)));
    }
}

/// <summary>Art eines WiSo-Fehlers (Codes wie G30: zu_klein, zu_gross, nachkommastellen, nicht_ganz, kein_db, nie).</summary>
public enum WisoFehlerArt
{
    ZuKlein,
    ZuGross,
    Nachkommastellen,
    NichtGanz,
    KeinDeckungsbeitrag,
    Nie,
}

/// <summary>Erster Fehler: Feldschluessel (null bei kein_db und nie) und Art.</summary>
public sealed record WisoFehler(string? Feld, WisoFehlerArt Art);

/// <summary>Ergebnis eines WiSo-Rechners: entweder Fehler oder Werte.</summary>
public sealed record WisoErgebnis<T>(WisoFehler? Fehler, T? Werte) where T : class;

public sealed record BezugWerte(decimal Rabatt, decimal Zep, decimal Skonto, decimal Bep, decimal Bezugspreis);

public sealed record VerkaufWerte(decimal Hk, decimal Sk, decimal Gewinn, decimal Bvp, decimal Kskonto, decimal Zvp,
    decimal Krabatt, decimal Lvp, decimal Ust, decimal Brutto);

public sealed record DeckungWerte(decimal Db, decimal DbGesamt, decimal Ergebnis, decimal GsDezimal, long Gewinnschwelle);

public sealed record AfaJahr(int Jahr, int Monate, decimal Betrag, decimal Rest);

public sealed record AfaWerte(decimal Jaehrlich, IReadOnlyList<AfaJahr> Jahre);

public sealed record ZinsenWerte(decimal Zinsen, decimal Endkapital);

/// <summary>Rueckfluss, Jahre (2 Stellen) und Monate gesamt (aufgerundet); Jahre/Monate daraus mit / und % 12.</summary>
public sealed record AmortWerte(decimal Rueckfluss, decimal Jahre, long Monate);
