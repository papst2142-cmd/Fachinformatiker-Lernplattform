using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Texte;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3, R2: berechnete Rechenwege "Netz aufteilen" (split_explain) und "Nach Hostbedarf"
/// (hosts_explain) genau wie Python v0.63.2 (fisi_core 5120-5627), PC (75 Zeichen) und Handy
/// (schmal, 28 Zeichen). Die festen Rechenwege stehen in RechnerTexte (CALC_EXPLAIN_*).
/// Vergleichswerte G25.
/// </summary>
public static class Rechenweg
{
    private static string I(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static string D(BigInteger wert) => Zahlen.DeGanz(wert);

    private static int L(string text) => PythonZeichen.Laenge(text);

    private static string Links(string text, int breite) => PythonZeichen.Links(text, breite);

    /// <summary>format(wert, "0nb").</summary>
    private static string Binaer(BigInteger wert, int stellen) => Bin(wert).PadLeft(stellen, '0');

    private static string Bin(BigInteger wert)
    {
        if (wert.IsZero)
        {
            return "0";
        }
        var ziffern = new List<char>();
        while (wert > 0)
        {
            ziffern.Add(wert.IsEven ? '0' : '1');
            wert >>= 1;
        }
        ziffern.Reverse();
        return new string([.. ziffern]);
    }

    /// <summary>_explain_list_indexes: alle oder 4 vorne, 2 hinten.</summary>
    private static (List<BigInteger?> Indizes, (BigInteger, BigInteger)? Luecke) Liste(BigInteger gesamt)
    {
        if (gesamt <= SUBNET_EXPLAIN_ALL)
        {
            var alle = new List<BigInteger?>();
            for (BigInteger i = 0; i < gesamt; i++)
            {
                alle.Add(i);
            }
            return (alle, null);
        }
        return ([0, 1, 2, 3, null, gesamt - 2, gesamt - 1], (5, gesamt - 2));
    }

    private static List<string> K(Aufteilung a, string pad, bool schmal)
    {
        var zeilen = new List<string>();
        for (var potenz = Math.Max(0, a.K - 1); potenz <= a.K; potenz++)
        {
            var wert = D(BigInteger.One << potenz);
            string zeile, rest;
            if (potenz == a.K)
            {
                zeile = pad + I($"2^{potenz} = ") + wert + " >= " + D(a.Gewuenscht) + I($" -> k = {a.K}");
                rest = pad + " >= " + D(a.Gewuenscht) + I($" -> k = {a.K}");
            }
            else
            {
                zeile = pad + I($"2^{potenz} = ") + wert + " < " + D(a.Gewuenscht) + " (zu wenig)";
                rest = pad + " < " + D(a.Gewuenscht) + " (zu wenig)";
            }
            if (schmal && L(zeile) > Zeilen.HandyBreite)
            {
                zeilen.Add(pad + I($"2^{potenz} = ") + wert);
                zeilen.Add(rest);
            }
            else
            {
                zeilen.Add(zeile);
            }
        }
        if (a.K == 0)
        {
            zeilen.AddRange(Zeilen.Absatz(SUBNET_NO_SPLIT, pad, schmal ? Zeilen.HandyBreite : Zeilen.PcBreite));
        }
        return zeilen;
    }

    private static List<string> Reserve(Aufteilung a, bool schmal)
    {
        var gewuenscht = a.Gewuenscht;
        var gesamt = a.Anzahl;
        if (gesamt == gewuenscht)
        {
            return [];
        }
        if (schmal)
        {
            return [" " + D(gewuenscht) + " ist keine Zweierpotenz:", I($" Es entstehen 2^{a.K} = ") + D(gesamt),
                " Subnetze. Die ersten " + D(gewuenscht), " werden verwendet, der Rest", " bleibt frei (Reserve)."];
        }
        var frei = gesamt - gewuenscht;
        string rest, verb;
        if (frei == 1)
        {
            rest = "Nr. " + D(gesamt);
            verb = "bleibt";
        }
        else
        {
            rest = "Nr. " + D(gewuenscht + 1) + (frei == 2 ? " und " : " bis ") + D(gesamt);
            verb = "bleiben";
        }
        var zeilen = new List<string> { "   " + D(gewuenscht) + I($" ist keine Zweierpotenz: Es entstehen 2^{a.K} = ") + D(gesamt) + " Subnetze." };
        var benutzt = "Die ersten " + D(gewuenscht) + " werden verwendet (Nr. 1 bis " + D(gewuenscht) + "), " + rest;
        if (L("   " + benutzt) <= Zeilen.PcBreite)
        {
            zeilen.AddRange(["   " + benutzt, "   " + verb + " frei (Reserve)."]);
            return zeilen;
        }
        zeilen.AddRange(Zeilen.Absatz(benutzt + " " + verb + " frei (Reserve).", "   ", Zeilen.PcBreite));
        return zeilen;
    }

    private static List<string> Kopf(Aufteilung a, string titel, bool schmal)
    {
        var version = a.Version;
        var basis = Subnetting.NetzText(version, a.Netz, a.Praefix);
        var breite = schmal ? Zeilen.HandyBreite : Zeilen.PcBreite;
        List<string> zeilen;
        if (schmal)
        {
            zeilen = [titel, I($"(IPv{version})"), "Ausgangsnetz", .. Zeilen.HandyAdresse(" ", basis)];
            if (a.Gewuenscht == 1)
            {
                zeilen.Add("gesucht: 1 Subnetz");
            }
            else
            {
                zeilen.AddRange(["gesucht: " + D(a.Gewuenscht) + " gleich große", "Subnetze"]);
            }
        }
        else
        {
            zeilen = [titel + I($" (IPv{version})"),
                "Ausgangsnetz " + basis + ", gesucht: " + (a.Gewuenscht == 1 ? "1 Subnetz" : D(a.Gewuenscht) + " gleich große Subnetze")];
        }
        if (a.Basis.Hinweis is { } hinweis)
        {
            zeilen.AddRange(Zeilen.Absatz(hinweis, "", breite));
        }
        zeilen.Add("");
        if (schmal)
        {
            zeilen.AddRange(["SCHRITT 1: Bits für die", "Subnetze", " Gesucht: kleinstes k mit", " 2^k >= " + D(a.Gewuenscht)]);
            zeilen.AddRange(K(a, " ", true));
            zeilen.AddRange(["", "SCHRITT 2: Neue Präfixlänge", " alte Präfixlänge + k", I($" = {a.Praefix} + {a.K} = {a.Neu} -> /{a.Neu}")]);
        }
        else
        {
            zeilen.AddRange(["SCHRITT 1: Benötigte Bits für die Subnetze", "   Gesucht ist das kleinste k mit 2^k >= " + D(a.Gewuenscht) + "."]);
            zeilen.AddRange(K(a, "   ", false));
            zeilen.AddRange(["", "SCHRITT 2: Neue Präfixlänge",
                I($"   neue Präfixlänge = alte Präfixlänge + k = {a.Praefix} + {a.K} = {a.Neu} -> /{a.Neu}")]);
        }
        if (a.Nibble is int nibble)
        {
            zeilen.AddRange(Zeilen.Absatz(Beschriftungen.Einsetzen(SUBNET_EXPLAIN_NIBBLE, nibble, D(a.NibbleAnzahl!.Value), a.Neu),
                schmal ? " " : "   ", breite));
        }
        if (version == 6 && a.Neu > 64)
        {
            zeilen.AddRange(Zeilen.Absatz(Beschriftungen.Einsetzen(SUBNET_EXPLAIN_HINT_64, a.Neu), schmal ? " " : "   ", breite));
        }
        zeilen.Add("");
        return zeilen;
    }

    private static (List<string> Lage, List<string> Uebertrag) Uebertrag(Aufteilung a, int einheit, string name, string schritt, bool schmal)
    {
        var praefix = a.Praefix;
        var neu = a.Neu;
        var teile = new List<(int Bits, int Nr)>();
        for (var index = Div(praefix, einheit); index < Div(neu - 1, einheit) + 1; index++)
        {
            int tief = Math.Max(praefix + 1, index * einheit + 1), hoch = Math.Min(neu, (index + 1) * einheit);
            if (hoch >= tief)
            {
                teile.Add((hoch - tief + 1, index + 1));
            }
        }
        if (teile.Count < 2)
        {
            return ([], []);
        }
        var breite = schmal ? Zeilen.HandyBreite : Zeilen.PcBreite;
        var pad = schmal ? " " : "   ";
        var eintraege = teile.Select(t => I($"{t.Bits} im {t.Nr}.")).ToList();
        var wo = string.Join(", ", eintraege.Take(eintraege.Count - 1)) + " und " + eintraege[^1] + " " + name;
        var lage = Zeilen.Absatz(Beschriftungen.Einsetzen(SUBNET_EXPLAIN_BITS, neu - praefix, praefix + 1, neu, wo), pad, breite);
        var grenze = teile[^1].Nr;
        var uebertrag = Zeilen.Absatz(Beschriftungen.Einsetzen(SUBNET_EXPLAIN_CARRY, grenze, name, einheit == 8 ? "256" : "hex 10000", grenze - 1, name), pad, breite);
        var nummer = BigInteger.One << teile[^1].Bits;
        var vorher = Subnetting.AdressText(a.Version, Subnetting.Eintrag(a, nummer - 1).Netz);
        var nachher = Subnetting.AdressText(a.Version, Subnetting.Eintrag(a, nummer).Netz);
        if (schmal)
        {
            uebertrag.Add(" Beispiel:");
            uebertrag.AddRange(Zeilen.HandyAdresse(" Nr. " + D(nummer) + ": ", vorher));
            uebertrag.Add(" + " + schritt);
            uebertrag.AddRange(Zeilen.HandyAdresse(" = Nr. " + D(nummer + 1) + ": ", nachher));
        }
        else
        {
            uebertrag.AddRange(Zeilen.Absatz(Beschriftungen.Einsetzen(SUBNET_EXPLAIN_CARRY_EXAMPLE, D(nummer), vorher, schritt, D(nummer + 1), nachher), pad, breite));
        }
        return (lage, uebertrag);
    }

    /// <summary>Pythons a // b (abgerundet, auch negativ).</summary>
    private static int Div(int a, int b) => (int)Math.Floor((double)a / b);

    private static List<string> AufteilenV4(Aufteilung a, bool schmal)
    {
        var neu = a.Neu;
        var groesse = a.Groesse;
        var hostbits = 32 - neu;
        var maske = a.Maske!.Value;
        var oktett = Math.Max(0, Div(neu - 1, 8));
        var maskenOktett = (int)((maske >> (8 * (3 - oktett))) & 255);
        var schritt = 256 - maskenOktett;
        List<string> hosts;
        if (neu == 31)
        {
            hosts = [SUBNET_HINT_31];
        }
        else if (neu == 32)
        {
            hosts = [SUBNET_HINT_32];
        }
        else if (schmal)
        {
            hosts = [" Nutzbare Hosts je Subnetz", I($" = 2^{hostbits} - 2 = ") + D(groesse - 2)];
        }
        else
        {
            hosts = [I($"   Nutzbare Hosts je Subnetz = 2^{hostbits} - 2 = ") + D(groesse - 2)];
        }
        var zeilen = Kopf(a, "RECHENWEG NETZ AUFTEILEN", schmal);
        var (lage, uebertrag) = Uebertrag(a, 8, "Oktett", schritt.ToString(CultureInfo.InvariantCulture), schmal);
        if (schmal)
        {
            if (neu >= 31)
            {
                hosts = Zeilen.Absatz(hosts[0], " ", Zeilen.HandyBreite);
            }
            zeilen.AddRange(["SCHRITT 3: Maske, Hostbits,", "Schrittweite", I($" /{neu}, Oktett für Oktett:")]);
            for (var nummer = 0; nummer < 4; nummer++)
            {
                var wert = (int)((maske >> (8 * (3 - nummer))) & 255);
                zeilen.Add(I($" {nummer + 1}. Oktett ") + Binaer(wert, 8) + " = " + wert.ToString(CultureInfo.InvariantCulture).PadLeft(3));
            }
            zeilen.AddRange([" -> Subnetzmaske:", "    " + PythonIp.V4Text(maske), I($" Hostbits = 32 - {neu} = {hostbits}"),
                " Adressen je Subnetz", I($" = 2^{hostbits} = ") + D(groesse), I($" Schrittweite im {oktett + 1}. Oktett"),
                I($" = 256 - {maskenOktett} = {schritt}")]);
            zeilen.AddRange(lage);
            zeilen.AddRange(hosts);
            zeilen.AddRange(["", "SCHRITT 4: Subnetze", "auflisten", " Netzadresse + Schrittweite", " ergibt das nächste Subnetz."]);
            zeilen.AddRange(uebertrag);
        }
        else
        {
            if (neu >= 31)
            {
                hosts = ["   " + hosts[0]];
            }
            var bits = string.Join(".", new[] { 24, 16, 8, 0 }.Select(s => Binaer((maske >> s) & 255, 8)));
            zeilen.AddRange(["SCHRITT 3: Subnetzmaske, Hostbits und Schrittweite", I($"   /{neu} = ") + bits,
                "   -> Subnetzmaske: " + PythonIp.V4Text(maske), I($"   Hostbits = 32 - {neu} = {hostbits}"),
                I($"   Adressen je Subnetz = 2^{hostbits} = ") + D(groesse),
                I($"   Schrittweite im {oktett + 1}. Oktett = 256 - {maskenOktett} = {schritt}")]);
            zeilen.AddRange(lage);
            zeilen.AddRange(hosts);
            zeilen.AddRange(["", "SCHRITT 4: Subnetze auflisten", "   (Netzadresse + Schrittweite = nächstes Subnetz)"]);
            zeilen.AddRange(uebertrag);
        }
        var (indizes, luecke) = Liste(a.Anzahl);
        var reihen = new List<IReadOnlyList<string>?>();
        foreach (var index in indizes)
        {
            if (index is null)
            {
                if (schmal)
                {
                    zeilen.AddRange(Zeilen.Absatz("Subnetz " + D(luecke!.Value.Item1) + " bis " + D(luecke.Value.Item2)
                        + ": je + Schrittweite (alle im Ergebnis)", "", Zeilen.HandyBreite));
                }
                else
                {
                    reihen.Add(null);
                }
                continue;
            }
            var e = Subnetting.Eintrag(a, index.Value);
            var broadcast = e.Broadcast is { } b ? PythonIp.V4Text(b) : "keine";
            var netz = Subnetting.NetzText(4, e.Netz, neu);
            if (schmal)
            {
                zeilen.AddRange(["Subnetz " + D(e.Nr) + ": " + e.Status, " Netz: " + netz, " Erste: " + PythonIp.V4Text(e.Erste),
                    " Letzte: " + PythonIp.V4Text(e.Letzte), " Broadcast: " + broadcast]);
            }
            else
            {
                reihen.Add([D(e.Nr), netz, PythonIp.V4Text(e.Erste), PythonIp.V4Text(e.Letzte), broadcast]);
            }
        }
        if (!schmal)
        {
            var gefuellt = reihen.Where(r => r is not null).Select(r => r!).ToList();
            var breite = Math.Max(2, gefuellt.Max(r => L(r[0])));
            var tabelle = Zeilen.PcTabelle(["Nr", "Netzadresse", "Erster Host", "Letzter Host", "Broadcast"], gefuellt, [breite, 18, 15, 15]);
            if (luecke is { } l)
            {
                tabelle.Insert(5, "  " + Links("..", breite) + " Nr. " + D(l.Item1) + " bis " + D(l.Item2) + ": je + Schrittweite (alle im Ergebnis)");
            }
            zeilen.AddRange(tabelle);
        }
        var gesamt = a.Anzahl;
        var praefix = a.Praefix;
        zeilen.AddRange(["", "SCHRITT 5: Kontrolle"]);
        if (schmal)
        {
            zeilen.AddRange([" " + D(gesamt) + " x " + D(groesse), " = " + D(gesamt * groesse) + " Adressen", I($" = Größe von /{praefix}"),
                I($" (2^{32 - praefix} = ") + D(BigInteger.One << (32 - praefix)) + ")"]);
        }
        else
        {
            zeilen.Add("   " + D(gesamt) + " x " + D(groesse) + " = " + D(gesamt * groesse) + I($" Adressen = Größe von /{praefix} (2^{32 - praefix} = ")
                + D(BigInteger.One << (32 - praefix)) + ")");
        }
        zeilen.AddRange(Reserve(a, schmal));
        if (!schmal && gesamt == a.Gewuenscht)
        {
            zeilen.Add(gesamt == 1 ? "   Das eine Subnetz wird verwendet." : "   Alle " + D(gesamt) + " Subnetze werden verwendet.");
        }
        if (a.K != 0)
        {
            zeilen.AddRange(Zeilen.Absatz(SUBNET_EXPLAIN_BOOKS, schmal ? " " : "   ", schmal ? Zeilen.HandyBreite : Zeilen.PcBreite));
        }
        return zeilen;
    }

    private static List<string> AufteilenV6(Aufteilung a, bool schmal)
    {
        var neu = a.Neu;
        var groesse = a.Groesse;
        var praefix = a.Praefix;
        var hextett = Math.Max(0, Div(neu - 1, 16));
        int tief = 16 * hextett + 1, hoch = 16 * (hextett + 1);
        var schritt = BigInteger.One << (hoch - neu);
        var imHextett = neu - 16 * hextett;
        var einheit = imHextett == 1 ? "Bit" : "Bits";
        var zeilen = Kopf(a, "RECHENWEG NETZ AUFTEILEN", schmal);
        var (lage, uebertrag) = Uebertrag(a, 16, "Hextett", "hex " + PythonIp.Hex(schritt), schmal);
        var anycast = neu < 127;
        if (schmal)
        {
            zeilen.AddRange(["SCHRITT 3: Wo liegt die", "Grenze?", I($" /{neu} endet im {hextett + 1}. Hextett"),
                I($" (Bits {tief} bis {hoch})."), " Davon Netzanteil:", I($" {neu} - {16 * hextett} = {imHextett} ") + einheit]);
            zeilen.AddRange(lage);
            zeilen.AddRange([I($" Schrittweite im {hextett + 1}. Hextett"), I($" = 2^({hoch} - {neu}) = 2^{hoch - neu}"),
                " = " + D(schritt) + " = hex " + PythonIp.Hex(schritt), " Adressen je Subnetz:", I($" 2^(128 - {neu}) = 2^{128 - neu}"),
                " = " + D(groesse), " Kein Abzug von 2: IPv6", " hat keine Broadcast-", " Adresse."]);
            if (anycast)
            {
                zeilen.AddRange(Zeilen.Absatz(SUBNET_EXPLAIN_ANYCAST, " ", Zeilen.HandyBreite));
            }
            zeilen.AddRange(["", "SCHRITT 4: Subnetze", "auflisten", I($" {hextett + 1}. Hextett um die"), " Schrittweite erhöhen:"]);
            zeilen.AddRange(uebertrag);
        }
        else
        {
            zeilen.AddRange(["SCHRITT 3: Wo liegt die Grenze? Schrittweite und Größe",
                I($"   /{neu} endet im {hextett + 1}. Hextett (Bits {tief} bis {hoch}). Davon gehören"),
                I($"   {neu} - {16 * hextett} = {imHextett} ") + einheit + " zum Netzanteil."]);
            zeilen.AddRange(lage);
            zeilen.AddRange([I($"   Schrittweite im {hextett + 1}. Hextett = 2^({hoch} - {neu}) = 2^{hoch - neu} = ") + D(schritt) + " = hex " + PythonIp.Hex(schritt),
                I($"   Adressen je Subnetz = 2^(128 - {neu}) = 2^{128 - neu}"), "      = " + D(groesse),
                "   Kein Abzug von 2: IPv6 hat keine Broadcast-Adresse."]);
            if (anycast)
            {
                zeilen.AddRange(Zeilen.Absatz(SUBNET_EXPLAIN_ANYCAST, "   ", Zeilen.PcBreite));
            }
            zeilen.AddRange(["", I($"SCHRITT 4: Subnetze auflisten ({hextett + 1}. Hextett um die Schrittweite erhöhen)")]);
            zeilen.AddRange(uebertrag);
        }
        var bits = a.K;
        var zeigeBits = bits <= 16;
        var innen = bits >= 1 && praefix >= 16 * hextett;
        var (indizes, luecke) = Liste(a.Anzahl);
        var eintraege = indizes.Select(i => i is null ? null : Subnetting.Eintrag(a, i.Value)).ToList();
        var nummern = eintraege.Where(e => e is not null).Select(e => D(e!.Nr)).ToList();
        var breite = Math.Max(2, nummern.Max(L));
        var bitBreite = Math.Max(4, bits);
        if (!schmal)
        {
            var kopf = "  " + Links("Nr", breite) + " ";
            if (zeigeBits)
            {
                kopf += Links("Bits", bitBreite) + " ";
            }
            zeilen.Add(kopf + Links(I($"{hextett + 1}. Hextett binär"), 24) + "hex   Subnetz");
        }
        foreach (var e in eintraege)
        {
            if (e is null)
            {
                var text = "Subnetz " + D(luecke!.Value.Item1) + " bis " + D(luecke.Value.Item2) + ": je + Schrittweite (alle im Ergebnis)";
                if (schmal)
                {
                    zeilen.AddRange(Zeilen.Absatz(text, "", Zeilen.HandyBreite));
                }
                else
                {
                    zeilen.Add("  " + Links("..", breite) + " Nr. " + D(luecke.Value.Item1) + " bis " + D(luecke.Value.Item2) + ": je + Schrittweite (alle im Ergebnis)");
                }
                continue;
            }
            var gruppe = PythonIp.Hextette(e.Netz)[hextett];
            var binaer = Binaer(gruppe, 16);
            binaer = string.Join(" ", Enumerable.Range(0, 4).Select(i => binaer.Substring(4 * i, 4)));
            var teilBits = bits != 0 ? Binaer(e.Nr - 1, bits) : "-";
            var netz = Subnetting.NetzText(6, e.Netz, neu);
            var hex4 = gruppe.ToString("x4", CultureInfo.InvariantCulture);
            if (schmal)
            {
                zeilen.Add("Subnetz " + D(e.Nr) + ": " + e.Status);
                var beschriftung = " Bits " + teilBits + I($" im {hextett + 1}. Hextett:");
                if (innen && L(beschriftung) <= Zeilen.HandyBreite)
                {
                    zeilen.Add(beschriftung);
                }
                else
                {
                    if (zeigeBits && bits != 0)
                    {
                        zeilen.AddRange(Zeilen.HandyEinpassen([" Subnetz-Bits: " + teilBits]));
                    }
                    zeilen.Add(I($" {hextett + 1}. Hextett:"));
                }
                zeilen.Add("  " + binaer + " = " + hex4);
                zeilen.AddRange(Zeilen.HandyAdresse(" Netz: ", netz));
                zeilen.AddRange(Zeilen.HandyAdresse(" bis: ", PythonIp.V6Text(e.Ende)));
            }
            else
            {
                var reihe = "  " + Links(D(e.Nr), breite) + " ";
                if (zeigeBits)
                {
                    reihe += Links(teilBits, bitBreite) + " ";
                }
                zeilen.Add(reihe + binaer + " = " + hex4 + "  " + netz);
            }
        }
        if (!schmal)
        {
            var erstes = Subnetting.Eintrag(a, 0);
            zeilen.AddRange(["", "   Bereich je Subnetz: von der Netzadresse bis zur letzten Adresse,",
                "   z. B. Subnetz 1: " + PythonIp.V6Text(erstes.Netz) + " bis", "                    " + PythonIp.V6Text(erstes.Ende)]);
        }
        zeilen.AddRange(["", "SCHRITT 5: Kontrolle"]);
        var gesamt = a.Anzahl;
        if (schmal)
        {
            zeilen.AddRange([" " + D(gesamt) + I($" x 2^{128 - neu} = 2^{128 - praefix}"), I($" = Größe von /{praefix}")]);
        }
        else
        {
            zeilen.Add("   " + D(gesamt) + I($" x 2^{128 - neu} = 2^{128 - praefix} Adressen = Größe von /{praefix}"));
        }
        zeilen.AddRange(Reserve(a, schmal));
        return zeilen;
    }

    /// <summary>split_explain(result, schmal): Rechenweg "Netz aufteilen".</summary>
    public static string Aufteilen(Aufteilung a, bool schmal = false)
    {
        var zeilen = a.Version == 4 ? AufteilenV4(a, schmal) : AufteilenV6(a, schmal);
        return Zeilen.Text(Zeilen.Einpassen(zeilen, schmal ? Zeilen.HandyBreite : Zeilen.PcBreite));
    }

    /// <summary>hosts_explain(result, schmal): Rechenweg "Nach Hostbedarf" (VLSM, IPv4).</summary>
    public static string Bedarf(Hostbedarf r, bool schmal = false)
    {
        var breite = schmal ? Zeilen.HandyBreite : Zeilen.PcBreite;
        var pad = schmal ? " " : "   ";
        var basis = Subnetting.NetzText(4, r.Netz, r.Praefix);
        var netze = r.Subnetze;
        var folge = r.Reihenfolge;
        var hostBits = 32 - r.Praefix;
        List<string> zeilen;
        if (schmal)
        {
            zeilen = ["RECHENWEG NACH HOSTBEDARF", "(VLSM, IPv4)", "Ausgangsnetz", " " + basis, "Bedarf:"];
            zeilen.AddRange(netze.Select(n => " " + n.Name + ": " + Subnetting.HostsText(n.Bedarf)));
        }
        else
        {
            zeilen = ["RECHENWEG NACH HOSTBEDARF (VLSM, IPv4)", "Ausgangsnetz " + basis];
            zeilen.AddRange(Zeilen.Aufzaehlen("Bedarf: ", [.. netze.Select(n => n.Name + ": " + Subnetting.HostsText(n.Bedarf))], ", ", "        ", breite));
        }
        if (r.Basis.Hinweis is { } hinweis)
        {
            zeilen.AddRange(Zeilen.Absatz(hinweis, "", breite));
        }
        zeilen.Add("");
        var sortiert = folge.Select(i => netze[i].Name + ": " + D(netze[i].Bedarf)).ToList();
        if (schmal)
        {
            zeilen.AddRange(["SCHRITT 1: Sortieren,", "größter Bedarf zuerst"]);
            zeilen.AddRange(Zeilen.Aufzaehlen(" ", sortiert, ", ", " ", breite));
            zeilen.AddRange([" So liegt jedes Subnetz an", " seiner Größe ausgerichtet,", " es entstehen keine Lücken.", "",
                "SCHRITT 2: Größe je Subnetz", " kleinstes h mit", " 2^h - 2 >= Bedarf"]);
        }
        else
        {
            zeilen.Add("SCHRITT 1: Nach Hostbedarf sortieren, größter Bedarf zuerst");
            zeilen.AddRange(Zeilen.Aufzaehlen("   ", sortiert, ", ", "   ", breite));
            zeilen.AddRange(["   Begründung: So beginnt jedes Subnetz an einer Grenze, die zu",
                "   seiner Größe passt, und es entstehen keine Lücken.", "",
                "SCHRITT 2: Größe je Subnetz (kleinstes h mit 2^h - 2 >= Bedarf)"]);
        }
        foreach (var index in folge)
        {
            var n = netze[index];
            var bits = n.H;
            var bedarf = D(n.Bedarf);
            var bedarfText = Subnetting.HostsText(n.Bedarf);
            var darunter = bits > 2 ? I($"2^{bits - 1} - 2 = ") + D((BigInteger.One << (bits - 1)) - 2) + " < " + bedarf : "";
            var genug = I($"2^{bits} - 2 = ") + D((BigInteger.One << bits) - 2) + " >= " + bedarf;
            if (schmal)
            {
                zeilen.Add(" " + n.Name + ": " + bedarfText);
                if (darunter.Length > 0)
                {
                    zeilen.Add("  " + darunter);
                }
                zeilen.AddRange(["  " + genug, I($"  h = {bits} -> /{32 - bits} (32 - {bits})"), "  Größe " + D(n.Groesse) + " Adressen"]);
            }
            else
            {
                var zeile = "   " + n.Name + " (" + bedarfText + "): " + (darunter.Length > 0 ? darunter + ", " : "") + genug;
                if (L(zeile) > breite && darunter.Length > 0)
                {
                    zeilen.AddRange(["   " + n.Name + " (" + bedarfText + "): " + darunter + ",", "      " + genug]);
                }
                else
                {
                    zeilen.Add(zeile);
                }
                zeilen.Add(I($"      -> h = {bits}, /{32 - bits} (32 - {bits}), Maske ") + PythonIp.V4Text(n.Maske) + ", Größe " + D(n.Groesse));
            }
        }
        var groessen = folge.Select(i => D(netze[i].Groesse)).ToList();
        var urteil = r.Passt ? "passt" : "passt nicht";
        zeilen.AddRange(["", "SCHRITT 3: Passt es?"]);
        if (schmal)
        {
            if (groessen.Count > 1)
            {
                zeilen.AddRange(Zeilen.Aufzaehlen(" Summe: ", groessen, " + ", "  ", breite));
            }
            else
            {
                zeilen.Add(" Größe:");
            }
            zeilen.AddRange([" = " + D(r.Summe) + " Adressen", I($" Ausgangsnetz /{r.Praefix}: 2^{hostBits}"), " = " + D(r.Kapazitaet) + " Adressen",
                " " + D(r.Summe) + (r.Passt ? " <= " : " > ") + D(r.Kapazitaet) + " -> " + urteil]);
        }
        else
        {
            if (groessen.Count > 1)
            {
                var summe = Zeilen.Aufzaehlen("   Summe der Größen: ", groessen, " + ", "      ", breite);
                summe[^1] += " = " + D(r.Summe) + " Adressen";
                zeilen.AddRange(summe);
            }
            else
            {
                zeilen.Add("   Größe: " + D(r.Summe) + " Adressen");
            }
            zeilen.Add(I($"   Ausgangsnetz /{r.Praefix}: 2^{hostBits} = ") + D(r.Kapazitaet) + " Adressen -> " + urteil);
        }
        if (!r.Passt)
        {
            var potenz = Subnetting.Bits(r.Summe - 1);
            zeilen.AddRange(Zeilen.Absatz(r.Meldung!, pad, breite));
            zeilen.AddRange(Zeilen.Absatz(potenz <= 32
                ? "Nötige Größe: die nächste Zweierpotenz ab " + D(r.Summe) + I($" ist 2^{potenz} = ") + D(BigInteger.One << potenz)
                    + I($" Adressen, also /{32 - potenz} (32 - {potenz}).")
                : "Nötige Größe: mehr als 2^32 Adressen.", pad, breite));
            return Zeilen.Text(Zeilen.Einpassen(zeilen, breite));
        }
        zeilen.Add("");
        var reihen = new List<IReadOnlyList<string>>();
        if (schmal)
        {
            zeilen.AddRange(["SCHRITT 4: Vergabe", "nacheinander", " Jedes Subnetz beginnt", " direkt hinter dem vorigen."]);
        }
        else
        {
            zeilen.Add("SCHRITT 4: Vergabe nacheinander ab der ersten Adresse");
        }
        var nummer = 0;
        foreach (var index in folge)
        {
            nummer++;
            var n = netze[index];
            var netz = PythonIp.V4Text(n.Netz) + I($"/{n.Praefix}");
            if (schmal)
            {
                zeilen.AddRange([I($"{nummer}. ") + n.Name + " (" + Subnetting.HostsText(n.Bedarf) + ")", " Netz: " + netz,
                    " Maske: " + PythonIp.V4Text(n.Maske), " Erste: " + PythonIp.V4Text(n.Erste), " Letzte: " + PythonIp.V4Text(n.Letzte),
                    " Broadcast: " + PythonIp.V4Text(n.Broadcast)]);
            }
            else
            {
                reihen.Add([nummer.ToString(CultureInfo.InvariantCulture), n.Name, netz, PythonIp.V4Text(n.Erste),
                    PythonIp.V4Text(n.Letzte), PythonIp.V4Text(n.Broadcast)]);
            }
        }
        if (!schmal)
        {
            var namensBreite = Math.Max(4, netze.Max(n => L(n.Name)));
            zeilen.AddRange(Zeilen.PcTabelle(["Nr", "Name", "Netzadresse", "Erster Host", "Letzter Host", "Broadcast"], reihen, [2, namensBreite, 18, 15, 15]));
        }
        zeilen.Add("");
        var bloecke = r.Bloecke.Select(b => PythonIp.V4Text(b.Netz) + I($"/{b.Praefix}")).ToList();
        if (r.Rest is null || r.Rest.Value.IsZero)
        {
            zeilen.Add(schmal ? "SCHRITT 5: Rest" : "SCHRITT 5: Freier Rest");
            zeilen.AddRange(Zeilen.Absatz("Kein Rest: Das Ausgangsnetz ist genau voll.", pad, breite));
        }
        else if (schmal)
        {
            zeilen.AddRange(["SCHRITT 5: Rest", " frei: " + PythonIp.V4Text(r.RestVon!.Value), " bis " + PythonIp.V4Text(r.RestBis!.Value),
                " = " + D(r.Rest.Value) + " Adressen", "", " als Blöcke:"]);
            zeilen.AddRange(bloecke.Select(b => "  " + b));
        }
        else
        {
            zeilen.AddRange(["SCHRITT 5: Freier Rest", "   " + PythonIp.V4Text(r.RestVon!.Value) + " bis " + PythonIp.V4Text(r.RestBis!.Value)
                + " = " + D(r.Rest.Value) + " Adressen", ""]);
            zeilen.AddRange(Zeilen.Aufzaehlen("   als ausgerichtete Blöcke: ", bloecke, ", ", "   ", breite));
        }
        zeilen.Add("");
        if (schmal)
        {
            zeilen.Add("Reihenfolge der Eingabe:");
            zeilen.AddRange(netze.Select(n => " " + n.Name + ": " + PythonIp.V4Text(n.Netz) + I($"/{n.Praefix}")));
        }
        else
        {
            zeilen.Add("Ergebnis in der Reihenfolge der Eingabe:");
            zeilen.AddRange(netze.Select(n => "   " + n.Name + ": " + PythonIp.V4Text(n.Netz) + I($"/{n.Praefix}")));
        }
        return Zeilen.Text(Zeilen.Einpassen(zeilen, breite));
    }
}
