using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Zufall;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3 W4c, W4 (Weisung P3-23): Trainer-Aufgabenarten im Bereich "WiSo" (bezug, verkauf,
/// deckung, afa, zinsen, amort), je drei Stufen, nach der freigegebenen Wortliste W4c Abschnitt 6.
/// Kein Python-Vorbild; Wertebereiche und Reihenfolge der Zufallsaufrufe stehen gleich im
/// unabhaengigen Skript werkzeuge/vergleich/erzeuge_w4_rechentrainer.py (Vergleichswerte G31).
/// Geld rechnet in decimal (<see cref="Geld"/>), erst mal, dann geteilt, jeder Schritt auf Cent.
/// Liegt ein gerundeter Schritt genau auf einem halben Cent, wird die ganze Aufgabe neu gezogen
/// (F5); dann gibt es nur eine richtige Zahl, und die Pruefung ist immer genau (Geld.Wert, nie
/// ueber double). Zufall wie R4: random.Random("art-stufe-startwert").
/// </summary>
public static class WisoTrainer
{
    private static readonly long[] Rabatte = [5, 10, 15, 20, 25, 30];
    private static readonly long[] Skonti = [1, 2, 3];
    private static readonly long[] Handlung = [10, 15, 20, 25, 30, 40, 50];
    private static readonly long[] Gewinne = [5, 8, 10, 12, 15, 20, 25];
    private static readonly long[] KundenSkonti = [2, 3];
    private static readonly long[] KundenRabatte = [5, 10, 15, 20];
    private static readonly long[] Steuern = [7, 19];
    private static readonly decimal[] Zinssaetze = [1m, 1.5m, 2m, 2.5m, 3m, 3.5m, 4m, 4.5m, 5m, 6m];

    private static string G(decimal x) => Geld.Text(x);

    private static string P(decimal x) => Geld.Kurz(x);

    private static string Z(BigInteger n) => Zahlen.DeGanz(n);

    private static string S(long n) => n.ToString(CultureInfo.InvariantCulture);

    private static decimal R(decimal x) => Geld.Runden(x);

    /// <summary>zaehler / nenner liegt genau auf einem halben Cent (exakt: zaehler * 200 / nenner ist eine ungerade ganze Zahl).</summary>
    internal static bool Halb(decimal zaehler, decimal nenner)
    {
        var zweihundert = zaehler * 200;
        return zweihundert % nenner == 0 && (zweihundert / nenner) % 2 != 0;
    }

    private static TrainerAufgabe Aufgabe(string art, int stufe, string text, string loesung, IReadOnlyList<string> schritte, string feld = FeldErgebnisEuro) =>
        new(art, stufe, text, [("ergebnis", feld)], new Dictionary<string, string>(StringComparer.Ordinal) { ["ergebnis"] = loesung }, schritte,
            new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal) { ["ergebnis"] = GenauPruefer(loesung) }, zahlenfelder: true);

    // ---------------------------------------------------------------- bezug

    internal static TrainerAufgabe Bezug(PythonZufall rng, int stufe)
    {
        decimal lep, rab, zep, sk = 0, bep = 0, bk = 0;
        long r, s = 0;
        while (true)
        {
            if (stufe == 1)
            {
                lep = rng.RandInt(10, 500) * 10;
                r = rng.Choice(Rabatte);
            }
            else if (stufe == 2)
            {
                lep = rng.RandInt(100, 5000);
                r = rng.Choice(Rabatte);
                s = rng.Choice(Skonti);
            }
            else
            {
                lep = rng.RandInt(10000, 500000) / 100m;
                r = rng.Choice(Rabatte);
                s = rng.Choice(Skonti);
                bk = rng.RandInt(1, 20) * 5;
            }
            if (Halb(lep * r, 100))
            {
                continue;
            }
            rab = R(lep * r / 100);
            zep = lep - rab;
            if (stufe == 1)
            {
                break;
            }
            if (Halb(zep * s, 100))
            {
                continue;
            }
            sk = R(zep * s / 100);
            bep = zep - sk;
            break;
        }
        var schritte = new List<string>
        {
            "Rabatt: " + G(lep) + " x " + S(r) + " / 100 = " + G(rab) + " €",
            "Zieleinkaufspreis: " + G(lep) + " - " + G(rab) + " = " + G(zep) + " €",
        };
        if (stufe == 1)
        {
            return Aufgabe("bezug", 1, Beschriftungen.Einsetzen(AufgabeBezugLeicht, G(lep), S(r)), G(zep), schritte);
        }
        schritte.Add("Skonto: " + G(zep) + " x " + S(s) + " / 100 = " + G(sk) + " €");
        schritte.Add("Bareinkaufspreis: " + G(zep) + " - " + G(sk) + " = " + G(bep) + " €");
        if (stufe == 2)
        {
            return Aufgabe("bezug", 2, Beschriftungen.Einsetzen(AufgabeBezugMittel, G(lep), S(r), S(s)) + RundeJeden, G(bep), schritte);
        }
        var bzp = bep + bk;
        schritte.Add("Bezugspreis: " + G(bep) + " + " + G(bk) + " = " + G(bzp) + " €");
        return Aufgabe("bezug", 3, Beschriftungen.Einsetzen(AufgabeBezugSchwer, G(lep), S(r), S(s), G(bk)) + RundeJeden, G(bzp), schritte);
    }

    // ---------------------------------------------------------------- verkauf

    internal static TrainerAufgabe Verkauf(PythonZufall rng, int stufe)
    {
        decimal bp, hk, sk, gw = 0, bvp = 0, ksk = 0, zvp = 0, krb = 0, lvp = 0, ust = 0;
        long h, g = 0, ks = 0, kr = 0, u = 0;
        while (true)
        {
            bp = stufe == 3 ? rng.RandInt(10000, 500000) / 100m : rng.RandInt(100, 5000);
            h = rng.Choice(Handlung);
            if (stufe >= 2)
            {
                g = rng.Choice(Gewinne);
            }
            if (stufe == 3)
            {
                ks = rng.Choice(KundenSkonti);
                kr = rng.Choice(KundenRabatte);
                u = rng.Choice(Steuern);
            }
            if (Halb(bp * h, 100))
            {
                continue;
            }
            hk = R(bp * h / 100);
            sk = bp + hk;
            if (stufe == 1)
            {
                break;
            }
            if (Halb(sk * g, 100))
            {
                continue;
            }
            gw = R(sk * g / 100);
            bvp = sk + gw;
            if (stufe == 2)
            {
                break;
            }
            if (Halb(bvp * ks, 100 - ks))
            {
                continue;
            }
            ksk = R(bvp * ks / (100 - ks));
            zvp = bvp + ksk;
            if (Halb(zvp * kr, 100 - kr))
            {
                continue;
            }
            krb = R(zvp * kr / (100 - kr));
            lvp = zvp + krb;
            if (Halb(lvp * u, 100))
            {
                continue;
            }
            ust = R(lvp * u / 100);
            break;
        }
        var schritte = new List<string>
        {
            "Handlungskosten: " + G(bp) + " x " + S(h) + " / 100 = " + G(hk) + " €",
            "Selbstkosten: " + G(bp) + " + " + G(hk) + " = " + G(sk) + " €",
        };
        if (stufe == 1)
        {
            return Aufgabe("verkauf", 1, Beschriftungen.Einsetzen(AufgabeVerkaufLeicht, G(bp), S(h)), G(sk), schritte);
        }
        schritte.Add("Gewinn: " + G(sk) + " x " + S(g) + " / 100 = " + G(gw) + " €");
        schritte.Add("Barverkaufspreis: " + G(sk) + " + " + G(gw) + " = " + G(bvp) + " €");
        if (stufe == 2)
        {
            return Aufgabe("verkauf", 2, Beschriftungen.Einsetzen(AufgabeVerkaufMittel, G(bp), S(h), S(g)) + RundeJeden, G(bvp), schritte);
        }
        var brutto = lvp + ust;
        schritte.Add("Kundenskonto: " + G(bvp) + " x " + S(ks) + " / " + S(100 - ks) + " = " + G(ksk) + " €");
        schritte.Add("Zielverkaufspreis: " + G(bvp) + " + " + G(ksk) + " = " + G(zvp) + " €");
        schritte.Add("Kundenrabatt: " + G(zvp) + " x " + S(kr) + " / " + S(100 - kr) + " = " + G(krb) + " €");
        schritte.Add("Listenverkaufspreis netto: " + G(zvp) + " + " + G(krb) + " = " + G(lvp) + " €");
        schritte.Add("Umsatzsteuer: " + G(lvp) + " x " + S(u) + " / 100 = " + G(ust) + " €");
        schritte.Add("Bruttoverkaufspreis: " + G(lvp) + " + " + G(ust) + " = " + G(brutto) + " €");
        return Aufgabe("verkauf", 3, Beschriftungen.Einsetzen(AufgabeVerkaufSchwer, G(bp), S(h), S(g), S(ks), S(kr), S(u)) + RundeJeden,
            G(brutto), schritte);
    }

    // ---------------------------------------------------------------- deckung

    internal static TrainerAufgabe Deckung(PythonZufall rng, int stufe)
    {
        decimal p, vk, db, fix = 0, ges = 0, erg = 0;
        long m = 0, gs = 0;
        while (true)
        {
            p = rng.RandInt(20, 500);
            vk = rng.RandInt(5, (long)p - 5);
            db = p - vk;
            if (stufe == 1)
            {
                break;
            }
            if (stufe == 2)
            {
                m = rng.RandInt(10, 2000);
                fix = rng.RandInt(1, 200) * 500;
                ges = db * m;
                erg = ges - fix;
                if (erg <= 0)
                {
                    continue;
                }
                break;
            }
            fix = rng.RandInt(1, 200) * 500;
            if (fix % db == 0 || Halb(fix, db))
            {
                continue;
            }
            gs = Geld.Aufrunden(fix, db);
            break;
        }
        var schritte = new List<string> { "Deckungsbeitrag: " + G(p) + " - " + G(vk) + " = " + G(db) + " €" };
        if (stufe == 1)
        {
            return Aufgabe("deckung", 1, Beschriftungen.Einsetzen(AufgabeDeckungLeicht, G(p), G(vk)), G(db), schritte);
        }
        if (stufe == 2)
        {
            schritte.Add("Gesamt: " + G(db) + " x " + Z(m) + " = " + G(ges) + " €");
            schritte.Add("Betriebsergebnis: " + G(ges) + " - " + G(fix) + " = " + G(erg) + " €");
            return Aufgabe("deckung", 2, Beschriftungen.Einsetzen(AufgabeDeckungMittel, G(p), G(vk), G(fix), Z(m)), G(erg), schritte);
        }
        var q = G(R(fix / db));
        var wert = q.EndsWith(",00", StringComparison.Ordinal) ? Beschriftungen.Einsetzen(MehrAls, Z(gs - 1)) : q;
        schritte.Add("Gewinnschwelle: " + G(fix) + " / " + G(db) + " = " + wert + ", aufgerundet " + Z(gs) + " " + Stueck);
        return Aufgabe("deckung", 3, Beschriftungen.Einsetzen(AufgabeDeckungSchwer, G(p), G(vk), G(fix)), Z(gs), schritte, FeldErgebnisStueck);
    }

    // ---------------------------------------------------------------- afa

    internal static TrainerAufgabe Abschreibung(PythonZufall rng, int stufe)
    {
        decimal j, ak, e1 = 0;
        long nd, n = 0, m = 0;
        while (true)
        {
            j = rng.RandInt(10, 500) * 10;      // Wortliste W4c Abschnitt 6: jaehrlich 100 bis 5.000 in Zehnern (alle Stufen)
            nd = rng.RandInt(3, 10);
            if (stufe == 2)
            {
                n = rng.RandInt(1, nd - 1);
            }
            ak = j * nd;
            if (stufe == 3)
            {
                m = rng.RandInt(2, 12);
                if (Halb(j * (13 - m), 12))
                {
                    continue;
                }
                e1 = R(j * (13 - m) / 12);
            }
            break;
        }
        var schritte = new List<string> { "Jährlich: " + G(ak) + " / " + S(nd) + " = " + G(j) + " €" };
        if (stufe == 1)
        {
            return Aufgabe("afa", 1, Beschriftungen.Einsetzen(AufgabeAfaLeicht, G(ak), S(nd)), G(j), schritte);
        }
        if (stufe == 2)
        {
            var ab = j * n;
            schritte.Add("Abgeschrieben: " + G(j) + " x " + S(n) + " = " + G(ab) + " €");
            schritte.Add("Restbuchwert: " + G(ak) + " - " + G(ab) + " = " + G(ak - ab) + " €");
            return Aufgabe("afa", 2, Beschriftungen.Einsetzen(AufgabeAfaMittel, G(ak), S(nd), S(n)), G(ak - ab), schritte);
        }
        schritte.Add("Monate: 13 - " + S(m) + " = " + S(13 - m));
        schritte.Add("Erstes Jahr: " + G(j) + " x " + S(13 - m) + " / 12 = " + G(e1) + " €");
        return Aufgabe("afa", 3, Beschriftungen.Einsetzen(AufgabeAfaSchwer, G(ak), S(nd), Monatsnamen[(int)m - 1]) + RundeCent, G(e1), schritte);
    }

    // ---------------------------------------------------------------- zinsen

    internal static TrainerAufgabe Zinsen(PythonZufall rng, int stufe)
    {
        decimal k, p;
        long n = 0, t;
        while (true)
        {
            k = rng.RandInt(10, 500) * 100;      // Wortliste W4c Abschnitt 6: Kapital 1.000 bis 50.000 in Hundertern (alle Stufen)
            p = rng.Choice(Zinssaetze);
            if (stufe == 1)
            {
                n = rng.RandInt(1, 5);
                t = n * 360;
            }
            else if (stufe == 2)
            {
                n = rng.RandInt(1, 11);
                t = n * 30;
            }
            else
            {
                t = rng.RandInt(10, 359);
            }
            if (Halb(k * p * t, 36000))
            {
                continue;
            }
            break;
        }
        var z = R(k * p * t / 36000);
        var schritte = new List<string>();
        string dauer;
        if (stufe == 1)
        {
            dauer = Zahlen.Mehrzahl(n, Jahr, JahreText);
            schritte.Add("Tage: " + S(n) + " x 360 = " + Z(t));
        }
        else if (stufe == 2)
        {
            dauer = Zahlen.Mehrzahl(n, Monat, MonateText);
            schritte.Add("Tage: " + S(n) + " x 30 = " + Z(t));
        }
        else
        {
            dauer = S(t) + " " + Tage;
        }
        schritte.Add("Zinsen: " + G(k) + " x " + P(p) + " x " + Z(t) + " / 36.000 = " + G(z) + " €");
        var text = Beschriftungen.Einsetzen(AufgabeZinsen, G(k), dauer, P(p)) + (stufe == 3 ? AufgabeZinsenSchwer : "") + AufgabeZinsenFrage
            + (stufe > 1 ? RundeCent : "");
        return Aufgabe("zinsen", stufe, text, G(z), schritte);
    }

    // ---------------------------------------------------------------- amort

    internal static TrainerAufgabe Amortisation(PythonZufall rng, int stufe)
    {
        if (stufe == 1)
        {
            decimal e = rng.RandInt(5, 100) * 100;
            var n = rng.RandInt(2, 8);
            var i = e * n;
            return Aufgabe("amort", 1, Beschriftungen.Einsetzen(AufgabeAmortLeicht, G(i), G(e)), S(n),
                ["Amortisation: " + G(i) + " / " + G(e) + " = " + S(n) + " " + JahreText], FeldErgebnisJahre);
        }
        if (stufe == 2)
        {
            decimal r = rng.RandInt(5, 100) * 100;
            decimal l = rng.RandInt(1, 20) * 100;
            var n = rng.RandInt(2, 8);
            var e = r + l;
            var i = r * n;
            return Aufgabe("amort", 2, Beschriftungen.Einsetzen(AufgabeAmortMittel, G(i), G(e), G(l)), S(n),
                ["Rückfluss: " + G(e) + " - " + G(l) + " = " + G(r) + " €", "Amortisation: " + G(i) + " / " + G(r) + " = " + S(n) + " " + JahreText],
                FeldErgebnisJahre);
        }
        decimal inv, rf, lk;
        long mon;
        while (true)
        {
            inv = rng.RandInt(10, 500) * 100;
            rf = rng.RandInt(5, 100) * 100;
            lk = rng.RandInt(1, 20) * 100;
            mon = Geld.Aufrunden(inv * 12, rf);
            if (mon % 12 == 0 || mon > 240 || Halb(inv * 12, rf))
            {
                continue;
            }
            break;
        }
        var ein = rf + lk;
        string wert;
        if (inv * 12 % rf == 0)
        {
            wert = Z(mon);
        }
        else
        {
            var q = G(R(inv * 12 / rf));
            wert = (q.EndsWith(",00", StringComparison.Ordinal) ? Beschriftungen.Einsetzen(MehrAls, Z(mon - 1)) : q) + ", aufgerundet " + Z(mon);
        }
        var loesung = new Dictionary<string, string>(StringComparer.Ordinal) { ["jahre"] = S(mon / 12), ["monate"] = S(mon % 12) };
        return new TrainerAufgabe("amort", 3, Beschriftungen.Einsetzen(AufgabeAmortSchwer, G(inv), G(ein), G(lk)),
            [("jahre", FeldJahre), ("monate", FeldMonate)], loesung,
            [
                "Rückfluss: " + G(ein) + " - " + G(lk) + " = " + G(rf) + " €",
                "Monate: " + G(inv) + " x 12 / " + G(rf) + " = " + wert,
                "Ergebnis: " + Z(mon) + " " + MonateText + " = " + WisoBerichte.JahreUndMonate(mon),
            ],
            new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal)
            {
                ["jahre"] = GenauPruefer(loesung["jahre"]),
                ["monate"] = GenauPruefer(loesung["monate"]),
            },
            zahlenfelder: true);
    }

    // ---------------------------------------------------------------- Pruefer

    /// <summary>Genau gleich, gelesen als decimal nach der Leseregel (Geld.Wert, nie ueber double); "630" = "630,00".</summary>
    internal static Func<string, bool> GenauPruefer(string loesung)
    {
        if (!Geld.Wert(loesung, out var soll))
        {
            throw new ArgumentException("Loesung ist keine Zahl: " + loesung, nameof(loesung));
        }
        return text => Geld.Wert(text, out var w) && w == soll;
    }
}
