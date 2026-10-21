using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Core.Rechner;

/// <summary>Ein Balken der Laufzeit im USV-Bild (_ups_bars).</summary>
public sealed record UsvBalken(string Beschriftung, double Minuten, string Art, double Anteil, string Text);

/// <summary>Bild des USV-Rechners (ups_calculate()["bild"]): Kaesten Last, USV, Akku und Balken.</summary>
public sealed record UsvBild(string Last, string LastDetail, string Usv, string UsvDetail, string Akku, string AkkuDetail,
    double? Auslastung, int Grenze, IReadOnlyList<UsvBalken> Balken, string? Urteil);

/// <summary>Ergebnis von ups_calculate: Text, Bild und Empfehlung (null ausser im Modus "empfehlung").</summary>
public sealed record UsvErgebnis(string Text, UsvBild Bild, bool? Passend);

/// <summary>Akku-Bedarf (ups_battery_need).</summary>
public sealed record UsvBedarf(double Stunden, double LastWh, double AkkuWh, double BedarfWh, double BedarfAh);

/// <summary>Zahlen einer Uebungsaufgabe (UPS_TASKS "werte"); fehlende Werte sind 0.</summary>
public sealed record UsvAufgabe(double S = 0, double Pf = 0, double W = 0, double Eta = 0, double Min = 0, double V = 0,
    double Alt = 0, double Ah = 0, double Reihe = 0, double Par = 0, double NennVa = 0, double NennW = 0);

/// <summary>
/// Phase 3, R3: USV-Kapazitaetsrechner genau wie Python v0.63.2 (fisi_core 4009-4452):
/// Akku, Laufzeit (neu und am Lebensende mit Alterungszuschlag), Empfehlung nach der
/// 80-%-Regel, Bild-Daten mit Vorlesetext und sechs Uebungsaufgaben mit Loesungsweg
/// (10 min = 1/6 h als Bruch). Vergleichswerte G26.
/// </summary>
public static class Usv
{
    private static string I(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static string N(double wert, int stellen = 2) => Zahlen.DeZahl(wert, stellen);

    private static string K(double wert, int stellen = 2) => Zahlen.DeKurz(wert, stellen);

    /// <summary>"%s" einer ganzen Python-Zahl (int).</summary>
    private static string G(double wert) => new BigInteger(wert).ToString(CultureInfo.InvariantCulture);

    /// <summary>UPS_LIMITS: kleinster Wert, groesster Wert, ganze Zahl, 0 erlaubt (Grenze eingeschlossen).</summary>
    public static readonly IReadOnlyDictionary<string, (double Klein, double Gross, bool Ganz, bool NullErlaubt)> Grenzen =
        new Dictionary<string, (double, double, bool, bool)>(StringComparer.Ordinal)
        {
            ["last"] = (0, 1000000, false, false),
            ["pf"] = (0.1, 1, false, true),
            ["eta"] = (10, 100, false, true),
            ["minuten"] = (0, 1440, false, false),
            ["alterung"] = (0, 100, false, true),
            ["block_v"] = (0, 1000, false, false),
            ["block_ah"] = (0, 10000, false, false),
            ["reihe"] = (1, 100, true, true),
            ["parallel"] = (1, 20, true, true),
            ["nenn_va"] = (0, 1000000, false, false),
            ["nenn_w"] = (0, 1000000, false, false),
        };

    /// <summary>UPS_TASKS "werte" (Texte: RechnerTexte.UPS_TASK_TEXTS).</summary>
    public static readonly IReadOnlyList<UsvAufgabe> Aufgaben =
    [
        new(S: 800, Pf: 0.9),
        new(W: 450, Eta: 84, Min: 480, V: 48, Alt: 0),
        new(W: 300, Eta: 85, V: 12, Ah: 9, Reihe: 2, Par: 1, Alt: 25),
        new(S: 1200, Pf: 0.9, NennVa: 1500, NennW: 1000),
        new(W: 1000, Eta: 90, Min: 10, V: 48, Alt: 25),
        new(W: 500, Eta: 80, Min: 30, Alt: 25, V: 12, Ah: 12, Reihe: 2),
    ];

    private static string Feldtext(IReadOnlyDictionary<string, string?> felder, string schluessel) =>
        felder.TryGetValue(schluessel, out var wert) ? wert ?? "" : "";

    /// <summary>ups_number(fields, key): Feld lesen und Grenzen pruefen (Meldung mit Feldbeschriftung).</summary>
    public static double Zahl(IReadOnlyDictionary<string, string?> felder, string schluessel)
    {
        var beschriftung = UPS_FIELD_CAPTIONS[schluessel];
        var (klein, gross, ganz, nullErlaubt) = Grenzen[schluessel];
        var text = Feldtext(felder, schluessel);
        var wert = ganz ? (double)Zahlen.LesenGanz(text, beschriftung) : Zahlen.Lesen(text, beschriftung);
        var zuKlein = nullErlaubt ? wert < klein : wert <= klein;
        if (zuKlein || wert > gross)
        {
            if (nullErlaubt)
            {
                throw new RechnerFehler($"{beschriftung} muss zwischen {K(klein)} und {K(gross)} liegen.");
            }
            throw new RechnerFehler($"{beschriftung} muss größer als {K(klein)} und höchstens {K(gross)} sein.");
        }
        return wert;
    }

    /// <summary>ups_load(load, unit, pf): Last in W und VA (P = S x Leistungsfaktor).</summary>
    public static (double Watt, double Va) Last(double last, string einheit, double pf) =>
        einheit == "VA" ? (last * pf, last) : (last, last / pf);

    /// <summary>ups_battery_need: noetige Akku-Energie und Kapazitaet je Strang.</summary>
    public static UsvBedarf AkkuBedarf(double watt, double etaProzent, double minuten, double alterungProzent, double strangVolt)
    {
        var stunden = minuten / 60.0;
        var lastWh = watt * stunden;
        var akkuWh = lastWh / (etaProzent / 100.0);
        var bedarfWh = akkuWh * (1 + alterungProzent / 100.0);
        return new UsvBedarf(stunden, lastWh, akkuWh, bedarfWh, bedarfWh / strangVolt);
    }

    /// <summary>ups_battery_energy: Energie (Wh) aus Bloecken in Reihe und Straengen.</summary>
    public static double AkkuEnergie(double blockV, double blockAh, double reihe, double parallel) => blockV * reihe * blockAh * parallel;

    /// <summary>ups_runtime: Laufzeit in Minuten, neu und am Lebensende.</summary>
    public static (double Neu, double Ende) Laufzeit(double watt, double etaProzent, double akkuWh, double alterungProzent)
    {
        var neu = akkuWh * (etaProzent / 100.0) / watt * 60.0;
        return (neu, neu / (1 + alterungProzent / 100.0));
    }

    /// <summary>ups_end_share: Restkapazitaet am Lebensende in Prozent (1,25 -> 80 %).</summary>
    public static double EndAnteil(double alterungProzent) => 100.0 / (1 + alterungProzent / 100.0);

    /// <summary>ups_check: Empfehlung nach UPS_RULE_TEXT, (passend, Pruefzeilen).</summary>
    public static (bool Passend, List<(bool Ok, string Text)> Pruefungen) Pruefen(double watt, double va, double nennVa,
        double? nennW, double minutenEnde, double minuten)
    {
        var pruefungen = new List<(bool, string)>();
        var anteilVa = va / nennVa * 100;
        pruefungen.Add((anteilVa <= UPS_LOAD_LIMIT,
            $"Auslastung Scheinleistung: {K(va)} VA von {N(nennVa, 0)} VA = {N(anteilVa, 1)} %"));
        if (nennW is { } w && w != 0)
        {
            var anteilW = watt / w * 100;
            pruefungen.Add((anteilW <= UPS_LOAD_LIMIT,
                $"Auslastung Wirkleistung: {N(watt, 0)} W von {N(w, 0)} W = {N(anteilW, 1)} %"));
        }
        pruefungen.Add((minutenEnde >= minuten, $"Laufzeit am Lebensende: {N(minutenEnde, 1)} min, gewünscht {K(minuten)} min"));
        return (pruefungen.All(p => p.Item1), pruefungen);
    }

    /// <summary>_ups_bars: Balken gewuenscht, Akku neu, Akku am Lebensende.</summary>
    public static List<UsvBalken> Balken(double minuten, (double Neu, double Ende)? laufzeit = null, double alterungProzent = 0)
    {
        var roh = new List<(string Beschriftung, double Minuten, string Art)> { ("Gewünschte Laufzeit", minuten, "ziel") };
        if (laufzeit is { } l)
        {
            roh.Add(("Akku neu", l.Neu, "neu"));
            roh.Add(($"Akku am Lebensende ({N(EndAnteil(alterungProzent), 0)} %)", l.Ende, l.Ende >= minuten ? "ok" : "knapp"));
        }
        var spitze = roh.Max(b => b.Minuten);
        if (spitze == 0)
        {
            spitze = 1;
        }
        return [.. roh.Select(b => new UsvBalken(b.Beschriftung, b.Minuten, b.Art, b.Minuten / spitze,
            N(b.Minuten, 1) + " min" + (b.Art == "knapp" ? ", zu kurz" : "")))];
    }

    /// <summary>ups_picture_summary: Vorlesetext des Bildes (gleiche Angaben wie das Bild).</summary>
    public static string Vorlesetext(UsvBild bild)
    {
        var teile = new List<string>
        {
            $"Last {bild.Last}, {bild.LastDetail}",
            $"USV {bild.Usv}, {bild.UsvDetail}",
            $"Akku {bild.Akku}, {bild.AkkuDetail}",
        };
        teile.AddRange(bild.Balken.Select(b => $"{b.Beschriftung} {b.Text}"));
        if (!string.IsNullOrEmpty(bild.Urteil))
        {
            teile.Add("Empfehlung: USV " + bild.Urteil);
        }
        return "Bild: " + string.Join("; ", teile);
    }

    /// <summary>ups_calculate(mode, fields, unit): "akku", "laufzeit" oder "empfehlung".</summary>
    public static UsvErgebnis Berechnen(string modus, IReadOnlyDictionary<string, string?> felder, string einheit = "W")
    {
        if (!UPS_MODES.Any(m => m.Item1 == modus))
        {
            throw new RechnerFehler("Unbekannte Berechnung.");
        }
        var last = Zahl(felder, "last");
        var pf = Zahl(felder, "pf");
        var eta = Zahl(felder, "eta");
        var minuten = Zahl(felder, "minuten");
        var alterung = Zahl(felder, "alterung");
        var blockV = Zahl(felder, "block_v");
        var reihe = Zahl(felder, "reihe");
        var strangV = blockV * reihe;
        var (watt, va) = Last(last, einheit, pf);

        var zeilen = new List<string>
        {
            $"Last                  : {N(watt, 0)} W / {N(va, 0)} VA (Leistungsfaktor {K(pf)})",
            $"Wirkungsgrad der USV  : {K(eta)} %",
            $"Strangspannung        : {G(reihe)} x {K(blockV)} V = {K(strangV)} V",
        };
        var lastText = N(watt, 0) + " W";
        var lastDetail = $"{N(va, 0)} VA, λ {K(pf)}";
        var usvText = $"η {K(eta)} %";
        var usvDetail = "Wirkungsgrad";
        string akku, akkuDetail;
        double? auslastung = null;
        string? urteil = null;
        bool? passend = null;
        List<UsvBalken> balken;

        if (modus == "akku")
        {
            var bedarf = AkkuBedarf(watt, eta, minuten, alterung, strangV);
            var laufzeit = (minuten * (1 + alterung / 100.0), minuten);
            zeilen.AddRange([
                $"Gewünschte Laufzeit   : {K(minuten)} min = {N(bedarf.Stunden, 3)} h",
                "",
                $"Energie für die Last  : {N(bedarf.LastWh)} Wh",
                $"Energie aus dem Akku  : {N(bedarf.AkkuWh)} Wh (geteilt durch Wirkungsgrad)",
                $"Mit Alterungszuschlag : {N(bedarf.BedarfWh)} Wh (+{K(alterung)} %)",
                $"Benötigte Kapazität   : {N(bedarf.BedarfAh)} Ah bei {K(strangV)} V",
            ]);
            akku = N(bedarf.BedarfWh, 0) + " Wh";
            akkuDetail = $"{N(bedarf.BedarfAh, 1)} Ah bei {K(strangV)} V";
            balken = Balken(minuten, laufzeit, alterung);
        }
        else
        {
            var blockAh = Zahl(felder, "block_ah");
            var parallel = Zahl(felder, "parallel");
            var akkuWh = AkkuEnergie(blockV, blockAh, reihe, parallel);
            var laufzeit = Laufzeit(watt, eta, akkuWh, alterung);
            var straenge = Zahlen.Mehrzahl(new BigInteger(parallel), "Strang", "Stränge");
            zeilen.AddRange([
                $"Akku                  : {G(reihe)} x {K(blockV)} V, {K(blockAh)} Ah, {straenge}",
                $"Akku-Energie          : {K(strangV)} V x {K(blockAh)} Ah x {straenge} = {N(akkuWh)} Wh",
                "",
                $"Laufzeit (Akku neu)   : {N(laufzeit.Neu, 1)} min",
                $"Laufzeit (Lebensende) : {N(laufzeit.Ende, 1)} min ({N(EndAnteil(alterung), 0)} % Kapazität)",
            ]);
            akku = N(akkuWh, 0) + " Wh";
            akkuDetail = $"{G(reihe * parallel)} x {K(blockV)} V, {K(blockAh)} Ah";
            balken = Balken(minuten, laufzeit, alterung);

            if (modus == "empfehlung")
            {
                var nennVa = Zahl(felder, "nenn_va");
                double? nennW = PythonZeichen.Strip(Feldtext(felder, "nenn_w")).Length > 0 ? Zahl(felder, "nenn_w") : null;
                var (ok, pruefungen) = Pruefen(watt, va, nennVa, nennW, laufzeit.Ende, minuten);
                passend = ok;
                var anteil = Math.Max(va / nennVa, nennW is { } w && w != 0 ? watt / w : 0) * 100;
                auslastung = anteil;
                usvDetail = $"Auslastung {N(anteil, 0)} %";
                urteil = ok ? "passend" : "zu klein";
                zeilen.AddRange(["", "Empfehlung            : USV " + urteil]);
                zeilen.AddRange(pruefungen.Select(p => $"  {(p.Ok ? "ja  " : "nein")}  {p.Text}"));
                if (nennW is null)
                {
                    zeilen.Add("  Hinweis: ohne Nennleistung in W nur VA geprüft.");
                }
            }
        }
        var benutzt = new List<string> { "last", "pf", "eta", "minuten", "alterung", "block_v", "reihe" };
        if (modus != "akku")
        {
            benutzt.AddRange(["block_ah", "parallel"]);
        }
        if (modus == "empfehlung")
        {
            benutzt.AddRange(["nenn_va", "nenn_w"]);
        }
        zeilen.AddRange(Zahlen.TausenderHinweis([.. benutzt.Select(s => felder.TryGetValue(s, out var t) ? t : null)]));
        zeilen.AddRange(["", UPS_SIMPLE_TEXT]);
        var bild = new UsvBild(lastText, lastDetail, usvText, usvDetail, akku, akkuDetail, auslastung, UPS_LOAD_LIMIT, balken, urteil);
        return new UsvErgebnis(string.Join("\n", zeilen), bild, passend);
    }

    /// <summary>str(Fraction(zaehler, nenner)): gekuerzt, "1/6" oder "8".</summary>
    private static string Bruch(long zaehler, long nenner, out bool ganz)
    {
        var teiler = (long)BigInteger.GreatestCommonDivisor(zaehler, nenner);
        zaehler /= teiler;
        nenner /= teiler;
        ganz = nenner == 1;
        return ganz ? I($"{zaehler}") : I($"{zaehler}/{nenner}");
    }

    /// <summary>ups_task_solution(index): Loesungsweg (gerechnet, nicht abgeschrieben).</summary>
    public static string AufgabeLoesung(int index)
    {
        var w = Aufgaben[index];
        switch (index)
        {
            case 0:
            {
                var (watt, _) = Last(w.S, "VA", w.Pf);
                var noetig = w.S / (UPS_LOAD_LIMIT / 100.0);
                return $"P = S x Leistungsfaktor = {N(w.S, 0)} VA x {K(w.Pf)} = {N(watt, 0)} W\n"
                    + $"Nennleistung = Last / 0,8 = {N(w.S, 0)} VA / 0,8 = {N(noetig, 0)} VA\n"
                    + $"Antwort: {N(watt, 0)} W Wirkleistung, USV mit mindestens {N(noetig, 0)} VA.";
            }
            case 1 or 4:
            {
                var bedarf = AkkuBedarf(w.W, w.Eta, w.Min, w.Alt, w.V);
                var stunden = K(bedarf.Stunden, 4);
                var stundenErst = stunden + " h";
                var bruch = Bruch((long)w.Min, 60, out var ganz);
                if (!ganz && PythonRundenStellen.Runde(bedarf.Stunden, 4) != bedarf.Stunden)
                {
                    // 10 min = 1/6 h, damit der Zwischenwert nachrechenbar ist
                    stundenErst = $"{bruch} h (≈ {stunden} h)";
                    stunden = bruch;
                }
                var schritte = new List<string>
                {
                    $"Zeit = {K(w.Min)} min / 60 = {stundenErst}",
                    $"Energie Last = {N(w.W, 0)} W x {stunden} h = {N(bedarf.LastWh)} Wh",
                    $"Energie Akku = {N(bedarf.LastWh)} Wh / {K(w.Eta / 100.0)} = {N(bedarf.AkkuWh)} Wh",
                };
                if (w.Alt != 0)
                {
                    schritte.Add($"Mit Alterungszuschlag = {N(bedarf.AkkuWh)} Wh x {K(1 + w.Alt / 100.0)} = {N(bedarf.BedarfWh)} Wh");
                }
                schritte.Add($"Kapazität = {N(bedarf.BedarfWh)} Wh / {G(w.V)} V = {N(bedarf.BedarfAh)} Ah");
                schritte.Add($"Antwort: {N(bedarf.BedarfWh)} Wh, also {N(bedarf.BedarfAh)} Ah bei {G(w.V)} V.");
                return string.Join("\n", schritte);
            }
            case 2:
            {
                var energie = AkkuEnergie(w.V, w.Ah, w.Reihe, w.Par);
                var laufzeit = Laufzeit(w.W, w.Eta, energie, w.Alt);
                return $"Akku-Energie = {G(w.Reihe)} x {G(w.V)} V x {G(w.Ah)} Ah = {N(energie, 0)} Wh\n"
                    + $"Laufzeit = ({N(energie, 0)} Wh x {K(w.Eta / 100.0)} / {G(w.W)} W) x 60 min/h = {N(laufzeit.Neu)} min\n"
                    + $"Lebensende = {N(laufzeit.Neu)} min x 0,8 = {N(laufzeit.Ende)} min\n"
                    + $"Antwort: neu etwa {N(laufzeit.Neu, 1)} min, am Lebensende etwa {N(laufzeit.Ende, 1)} min.";
            }
            case 3:
            {
                var (watt, va) = Last(w.S, "VA", w.Pf);
                var anteilVa = va / w.NennVa * 100;
                var anteilW = watt / w.NennW * 100;
                return $"P = {N(w.S, 0)} VA x {K(w.Pf)} = {N(watt, 0)} W\n"
                    + $"Auslastung VA = {N(va, 0)} / {N(w.NennVa, 0)} = {N(anteilVa, 0)} % (höchstens 80 %: {(anteilVa <= 80 ? "ja" : "nein")})\n"
                    + $"Auslastung W = {N(watt, 0)} / {N(w.NennW, 0)} = {N(anteilW, 0)} % (höchstens 80 %: {(anteilW <= 80 ? "ja" : "nein")})\n"
                    + "Antwort: Die USV ist zu klein, weil die Wirkleistung über 80 % liegt (sogar über 100 %).";
            }
            default:
            {
                // Aufgabe 6: Anzahl Straenge
                var bedarf = AkkuBedarf(w.W, w.Eta, w.Min, w.Alt, w.V * w.Reihe);
                var strangWh = AkkuEnergie(w.V, w.Ah, w.Reihe, 1);
                var anzahl = StraengeNoetig(bedarf.BedarfWh, strangWh);
                return $"Bedarf = {G(w.W)} W x {K(bedarf.Stunden)} h / {K(w.Eta / 100.0)} x {K(1 + w.Alt / 100.0)} = {N(bedarf.BedarfWh)} Wh\n"
                    + $"Ein Strang = {G(w.Reihe)} x {G(w.V)} V x {G(w.Ah)} Ah = {N(strangWh, 0)} Wh\n"
                    + $"Stränge = {N(bedarf.BedarfWh)} Wh / {N(strangWh, 0)} Wh = {N(bedarf.BedarfWh / strangWh)}, aufgerundet {G(anzahl)}\n"
                    + $"Antwort: {G(anzahl)} Stränge parallel ({N(strangWh * anzahl, 0)} Wh).";
            }
        }
    }

    /// <summary>ups_strings_needed: Anzahl paralleler Straenge (immer aufrunden, 1e-6 Spielraum).</summary>
    public static int StraengeNoetig(double bedarfWh, double strangWh) => Math.Max(1, (int)Math.Ceiling(bedarfWh / strangWh - 1e-6));

    /// <summary>ups_task_text(index, solution): Aufgabe und auf Wunsch der Loesungsweg.</summary>
    public static string AufgabeText(int index, bool loesung = false)
    {
        var anzahl = Aufgaben.Count;
        var nr = ((index % anzahl) + anzahl) % anzahl;
        var (titel, frage, annahmen) = UPS_TASK_TEXTS[nr];
        var text = I($"Aufgabe {nr + 1} von {anzahl}: {titel}\n\n{frage}\n\nAnnahmen: {annahmen}");
        if (loesung)
        {
            text += "\n\nLösungsweg:\n" + AufgabeLoesung(nr);
        }
        return text;
    }
}
