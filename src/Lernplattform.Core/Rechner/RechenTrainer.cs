using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Suche;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Zufall;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Core.Rechner;

/// <summary>Eine Aufgabe des Rechen-Trainers (Python TrainerTask): Text, Felder, Loesung, Rechenweg.</summary>
public sealed class TrainerAufgabe
{
    private readonly IReadOnlyDictionary<string, Func<string, bool>> _pruefer;

    internal TrainerAufgabe(string art, int stufe, string text, IReadOnlyList<(string Schluessel, string Beschriftung)> felder,
        IReadOnlyDictionary<string, string> loesung, IReadOnlyList<string> schritte,
        IReadOnlyDictionary<string, Func<string, bool>> pruefer, IReadOnlyList<string>? schritteSchmal = null)
    {
        Art = art;
        Stufe = stufe;
        Text = text;
        Felder = felder;
        Loesung = loesung;
        Schritte = schritte;
        SchritteSchmal = schritteSchmal;
        _pruefer = pruefer;
    }

    public string Art { get; }

    public int Stufe { get; }

    public string Text { get; }

    /// <summary>fields: (Schluessel, Beschriftung) in Anzeigereihenfolge.</summary>
    public IReadOnlyList<(string Schluessel, string Beschriftung)> Felder { get; }

    /// <summary>solution: Schluessel -> Loesungstext ("richtig wäre: %s").</summary>
    public IReadOnlyDictionary<string, string> Loesung { get; }

    /// <summary>steps: Rechenweg (PC; IPv4 mit schmal = Handy-Schritt 3).</summary>
    public IReadOnlyList<string> Schritte { get; }

    /// <summary>steps_schmal: Handy-Fassung des Rechenwegs (nur "teilen" und "bedarf"), sonst null.</summary>
    public IReadOnlyList<string>? SchritteSchmal { get; }

    /// <summary>Was das Handy zeigt: steps_schmal oder steps (main.py 3053).</summary>
    public IReadOnlyList<string> HandySchritte => SchritteSchmal is { Count: > 0 } s ? s : Schritte;

    /// <summary>check(answers): je Feld richtig/falsch, fehlende Antwort = "".</summary>
    public List<(string Schluessel, bool Richtig)> Pruefen(IReadOnlyDictionary<string, string> antworten) =>
        [.. Felder.Select(f => (f.Schluessel, _pruefer[f.Schluessel](antworten.TryGetValue(f.Schluessel, out var a) ? a ?? "" : "")))];

    /// <summary>all_correct(answers).</summary>
    public bool AllesRichtig(IReadOnlyDictionary<string, string> antworten) => Pruefen(antworten).All(p => p.Richtig);
}

/// <summary>
/// Phase 3, R4: Rechen-Trainer genau wie Python v0.63.2 (fisi_lernen 712-1353): sechs
/// Aufgabenarten, drei Stufen, Runde zu 10 Aufgaben, Pruefer und Zusammenfassung. Jede
/// Aufgabe ist durch (Art, Stufe, Startwert-Text) festgelegt: Zufall wie Python
/// random.Random("art-stufe-startwert") (Text-Startwert, PythonZufall unveraendert), alle
/// Zufallsaufrufe in Pythons Reihenfolge (auch die Woerterbuecher, die alle Werte vorab
/// ziehen). Pruefer: Leerraum wie str.split(), Kleinschreibung nur mit PythonKlein,
/// Adressen als Adresse verglichen (PythonIp, nie als Text). Vergleichswerte G27.
/// </summary>
public static class RechenTrainer
{
    /// <summary>TRAINER_KINDS.</summary>
    public static readonly IReadOnlyList<(string Art, string Name)> Arten =
    [
        ("ipv4", "IPv4-Subnetz"),
        ("vlsm", "VLSM"),
        ("zahlen", "Binär/Hex/Dezimal"),
        ("ipv6", "IPv6-Schreibweise"),
        ("teilen", "Netz aufteilen"),
        ("bedarf", "Nach Hostbedarf"),
    ];

    /// <summary>TRAINER_LEVELS.</summary>
    public static readonly IReadOnlyList<(int Stufe, string Name)> Stufen = [(1, "Leicht"), (2, "Mittel"), (3, "Schwer")];

    /// <summary>TRAINER_ROUND.</summary>
    public const int Runde = 10;

    /// <summary>TRAINER_TITLE.</summary>
    public const string Titel = "Rechen-Trainer";

    /// <summary>TRAINER_NAMES (Netz aufteilen, Nach Hostbedarf).</summary>
    public static readonly IReadOnlyList<string> Namen =
        ["Verwaltung", "Produktion", "Vertrieb", "Lager", "Technik", "Gäste-WLAN", "Server", "Entwicklung", "Einkauf", "Buchhaltung"];

    private static readonly IReadOnlyList<string> VlsmNamen =
        ["Verwaltung", "Produktion", "Vertrieb", "Lager", "Technik", "Gäste-WLAN", "Server", "Entwicklung"];

    /// <summary>TRAINER_GAP_OLD / TRAINER_GAP_NEW.</summary>
    public const string LueckeAlt = "(alle im Ergebnis)";

    public const string LueckeNeu = "(nicht einzeln aufgelistet)";

    /// <summary>TRAINER_KIND_NAME.</summary>
    public static string ArtName(string art) => Arten.First(a => a.Art == art).Name;

    /// <summary>TRAINER_LEVEL_NAME.</summary>
    public static string StufeName(int stufe) => Stufen.First(s => s.Stufe == stufe).Name;

    private static string I(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static string D(BigInteger wert) => Zahlen.DeGanz(wert);

    private static int L(string text) => PythonZeichen.Laenge(text);

    // ---------------------------------------------------------------- Aufgabe, Runde, Zusammenfassung

    /// <summary>trainer_task(kind, level, seed, schmal): schmal nur fuer Schritt 3 der IPv4-Aufgabe.</summary>
    public static TrainerAufgabe Aufgabe(string art, int stufe, string startwert, bool schmal = false)
    {
        var rng = new PythonZufall(art + "-" + stufe.ToString(CultureInfo.InvariantCulture) + "-" + startwert);
        return art switch
        {
            "ipv4" => Ipv4(rng, stufe, schmal),
            "vlsm" => Vlsm(rng, stufe),
            "zahlen" => Zahlensysteme(rng, stufe),
            "ipv6" => Ipv6(rng, stufe),
            "teilen" => Teilen(rng, stufe),
            "bedarf" => Bedarf(rng, stufe),
            _ => throw new ArgumentOutOfRangeException(nameof(art), art, "unbekannte Aufgabenart"),
        };
    }

    /// <summary>trainer_round(kind, level, seed, count, schmal): Startwerte "seed-0" bis "seed-(count-1)".</summary>
    public static List<TrainerAufgabe> RundeBauen(string art, int stufe, string startwert, int anzahl = Runde, bool schmal = false) =>
        [.. Enumerable.Range(0, anzahl).Select(n => Aufgabe(art, stufe, startwert + "-" + n.ToString(CultureInfo.InvariantCulture), schmal))];

    /// <summary>trainer_round mit ganzzahligem Startwert wie app_gui/main.py ("%s" einer Zahl).</summary>
    public static List<TrainerAufgabe> RundeBauen(string art, int stufe, long startwert, int anzahl = Runde, bool schmal = false) =>
        RundeBauen(art, stufe, startwert.ToString(CultureInfo.InvariantCulture), anzahl, schmal);

    /// <summary>
    /// Startwert einer neuen Runde wie random.randrange(1 &lt;&lt; 30) (Python ohne festen Startwert:
    /// kein Sollwert, nur die Regel 0 &lt;= Startwert &lt; 2^30).
    /// </summary>
    public static long NeuerStartwert() => System.Random.Shared.NextInt64(1L << 30);

    /// <summary>trainer_summary(results).</summary>
    public static string Zusammenfassung(IReadOnlyList<bool> ergebnisse) =>
        I($"{ergebnisse.Count(e => e)} von {ergebnisse.Count} Aufgaben richtig");

    /// <summary>
    /// show_stats (app_gui 5552, main.py 2992): "bisher 12 Aufgaben, 9 richtig" ueber alle Arten,
    /// ohne geloeste Aufgabe leer. statistik wie LernstandSchreiber.TrainerStatistik.
    /// </summary>
    public static string StatistikZeile(IReadOnlyDictionary<string, (long Geloest, double Richtig)> statistik)
    {
        // "%d" % sum(...): erst summieren (Python 3.12 sum, Neumaier), dann abschneiden
        var anzahl = statistik.Values.Sum(s => s.Geloest);
        var richtig = (long)Math.Truncate(Mathe.PythonSumme.Summe(statistik.Values.Select(s => s.Richtig)));
        return anzahl == 0 ? "" : Beschriftungen.Einsetzen(Bisher, Zahlen.Mehrzahl(anzahl, RechnerTexte.Aufgabe, RechnerTexte.Aufgaben), richtig);
    }

    // ---------------------------------------------------------------- Pruefer

    /// <summary>_clean: allen Leerraum (str.split()) entfernen, dann str.lower() (PythonKlein).</summary>
    internal static string Bereinigen(string? text) => PythonKlein.Klein(string.Concat(PythonZeichen.Split(text ?? "")));

    /// <summary>_digits: nicht leer und nur die ASCII-Ziffern 0 bis 9.</summary>
    internal static bool Ziffern(string text) => text.Length > 0 && text.All(c => c is >= '0' and <= '9');

    private static BigInteger Ganz(string ziffern) => BigInteger.Parse(ziffern, NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>Gleiche Adresse wie Python: Version, Wert und Zone (scope_id) gleich.</summary>
    private static bool Gleich(IpAdresse a, IpAdresse b) =>
        a.Version == b.Version && a.Wert == b.Wert && (a.Zone ?? "") == (b.Zone ?? "");

    /// <summary>_ip_matcher: ip_address(_clean(text)) == erwartet.</summary>
    internal static Func<string, bool> IpPruefer(IpAdresse erwartet) =>
        text => PythonIp.Adresse(Bereinigen(text)) is { } a && Gleich(a, erwartet);

    /// <summary>_int_matcher: alle Punkte weg, dann nur ASCII-Ziffern.</summary>
    internal static Func<string, bool> ZahlPruefer(BigInteger erwartet) => text =>
    {
        var wert = Bereinigen(text).Replace(".", "", StringComparison.Ordinal);
        return Ziffern(wert) && Ganz(wert) == erwartet;
    };

    /// <summary>_base_matcher: Praefix "0b" bzw. "0x"/"h" (auch vorn) einmal weg, "_" weg, dann int(wert, basis).</summary>
    internal static Func<string, bool> BasisPruefer(BigInteger erwartet, int basis) => text =>
    {
        string[] marken = basis switch { 2 => ["0b"], 16 => ["0x", "h"], _ => [] };
        var wert = Bereinigen(text);
        foreach (var marke in marken)
        {
            if (wert.StartsWith(marke, StringComparison.Ordinal))
            {
                wert = wert[marke.Length..];
            }
            else if (wert.EndsWith(marke, StringComparison.Ordinal) && marke == "h")
            {
                wert = wert[..^1];
            }
        }
        wert = wert.Replace("_", "", StringComparison.Ordinal);
        return wert != "" && PythonInt(wert, basis, out var zahl) && zahl == erwartet;
    };

    /// <summary>_network_matcher: ip_network(_clean(text), strict=True) == erwartet.</summary>
    internal static Func<string, bool> NetzPruefer(IpNetz erwartet) =>
        text => PythonIp.Netz(Bereinigen(text), streng: true) is { } n
            && Gleich(n.Netzadresse, erwartet.Netzadresse) && n.Praefix == erwartet.Praefix;

    /// <summary>_ipv6_text_matcher: Text gleich (nur fuer die Schreibweise-Aufgabe).</summary>
    internal static Func<string, bool> Ipv6TextPruefer(string erwartet) =>
        text => Bereinigen(text) == PythonKlein.Klein(erwartet);

    /// <summary>_prefix_matcher: "/27" oder "27".</summary>
    internal static Func<string, bool> PraefixPruefer(int erwartet) => text =>
    {
        var wert = Bereinigen(text);
        wert = wert.StartsWith('/') ? wert[1..] : wert;
        return Ziffern(wert) && Ganz(wert) == erwartet;
    };

    /// <summary>_count_matcher: Tausenderpunkte nur in Dreiergruppen, mit potenz auch "2^n" (hoechstens 6 Zeichen).</summary>
    internal static Func<string, bool> AnzahlPruefer(BigInteger erwartet, bool potenz = false) => text =>
    {
        var wert = Bereinigen(text);
        if (potenz && wert.StartsWith("2^", StringComparison.Ordinal))
        {
            var hoch = wert[2..];
            return Ziffern(hoch) && L(wert) <= 6 && BigInteger.One << (int)Ganz(hoch) == erwartet;
        }
        var gruppen = wert.Split('.');
        if (gruppen.Length > 1 && !(L(gruppen[0]) is >= 1 and <= 3 && gruppen.Skip(1).All(g => L(g) == 3)))
        {
            return false;
        }
        wert = string.Concat(gruppen);
        return Ziffern(wert) && Ganz(wert) == erwartet;
    };

    /// <summary>
    /// int(text, basis) fuer basis 2 bis 36 wie CPython: Leerraum aussen (ASCII " \t\n\v\f\r",
    /// Unicode-Leerraum ab U+007F; \x1c bis \x1f nicht, anders als str.split()), ein Vorzeichen,
    /// Praefix 0b/0o/0x passend zur Basis (danach ein "_" erlaubt), Ziffern mit einzelnen "_"
    /// dazwischen; Dezimalziffern jeder Schrift zaehlen als 0 bis 9, Buchstaben nur ASCII.
    /// </summary>
    internal static bool PythonInt(string text, int basis, out BigInteger wert)
    {
        wert = BigInteger.Zero;
        var zeichen = new List<char>();
        foreach (var rune in text.EnumerateRunes())
        {
            // _PyUnicode_TransformDecimalAndSpaceToASCII: ASCII bleibt, sonst Leerraum -> " ",
            // Dezimalziffer -> ASCII-Ziffer, alles andere ungueltig
            var dez = PythonZeichen.Dezimalwert(rune.Value);
            if (rune.Value < 127)
            {
                zeichen.Add((char)rune.Value);
            }
            else if (PythonZeichen.IstLeer(rune.Value))
            {
                zeichen.Add(' ');
            }
            else
            {
                zeichen.Add(dez >= 0 ? (char)('0' + dez) : '?');
            }
        }
        var s = new string([.. zeichen]).Trim(' ', '\t', '\n', '\v', '\f', '\r');
        var i = 0;
        var negativ = false;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            negativ = s[i] == '-';
            i++;
        }
        var marke = basis switch { 16 => 'x', 8 => 'o', 2 => 'b', _ => '\0' };
        if (marke != '\0' && i + 1 < s.Length && s[i] == '0' && char.ToLowerInvariant(s[i + 1]) == marke)
        {
            i += 2;
            if (i < s.Length && s[i] == '_')
            {
                i++;
            }
        }
        if (i >= s.Length || s[i] == '_')
        {
            return false;
        }
        var ergebnis = BigInteger.Zero;
        var vorher = '_';
        for (; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '_')
            {
                if (vorher == '_')
                {
                    return false;
                }
                vorher = c;
                continue;
            }
            var ziffer = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'z' => c - 'a' + 10,
                >= 'A' and <= 'Z' => c - 'A' + 10,
                _ => 99,
            };
            if (ziffer >= basis)
            {
                return false;
            }
            ergebnis = ergebnis * basis + ziffer;
            vorher = c;
        }
        if (vorher == '_')
        {
            return false;
        }
        wert = negativ ? -ergebnis : ergebnis;
        return true;
    }

    // ---------------------------------------------------------------- Helfer

    private static IpAdresse V4(BigInteger wert) => new(4, wert);

    private static IpAdresse V6(BigInteger wert) => new(6, wert);

    private static string V4Text(BigInteger wert) => PythonIp.V4Text(wert);

    private static string NetzText(BigInteger netz, int praefix) => I($"{V4Text(netz)}/{praefix}");

    private static byte[] Oktette(BigInteger wert) => [.. new[] { 24, 16, 8, 0 }.Select(s => (byte)((wert >> s) & 255))];

    private static string Bin(BigInteger wert)
    {
        if (wert.IsZero)
        {
            return "0";
        }
        var ziffern = new List<char>();
        for (var w = wert; w > 0; w >>= 1)
        {
            ziffern.Add(w.IsEven ? '0' : '1');
        }
        ziffern.Reverse();
        return new string([.. ziffern]);
    }

    private static string Bin8(int wert) => Bin(wert).PadLeft(8, '0');

    /// <summary>_dotted_binary: vier Oktette binaer mit Punkten.</summary>
    private static string PunktBinaer(BigInteger wert) => string.Join(".", Oktette(wert).Select(o => Bin8(o)));

    private static T Waehle<T>(int stufe, T leicht, T mittel, T schwer) => stufe switch
    {
        1 => leicht,
        2 => mittel,
        3 => schwer,
        _ => throw new ArgumentOutOfRangeException(nameof(stufe), stufe, "unbekannte Stufe"),
    };

    // ---------------------------------------------------------------- IPv4-Subnetz

    /// <summary>_ipv4_and_narrow: Schritt 3 fuer das Handy, Oktett fuer Oktett.</summary>
    private static List<string> UndSchmal(BigInteger adresse, BigInteger maske, BigInteger netz)
    {
        var zeilen = new List<string> { "3. UND-Verknüpfung je Oktett" };
        byte[] a = Oktette(adresse), m = Oktette(maske), n = Oktette(netz);
        for (var i = 0; i < 4; i++)
        {
            zeilen.Add(I($"   Oktett {i + 1}:"));
            zeilen.Add("     Adresse " + Bin8(a[i]));
            zeilen.Add("     Maske   " + Bin8(m[i]));
            zeilen.Add(I($"     UND     {Bin8(n[i])} = {n[i]}"));
        }
        zeilen.Add("   Ergebnis: " + V4Text(netz));
        return zeilen;
    }

    /// <summary>_ipv4_task.</summary>
    private static TrainerAufgabe Ipv4(PythonZufall rng, int stufe, bool schmal)
    {
        // Python {1: randint(24, 30), 2: randint(17, 30), 3: randint(8, 30)}[level] zieht alle drei
        long p1 = rng.RandInt(24, 30), p2 = rng.RandInt(17, 30), p3 = rng.RandInt(8, 30);
        var praefix = (int)Waehle(stufe, p1, p2, p3);
        var erstes = rng.Choice(new long[] { 10, 172, 192 });
        long[] oktette = erstes switch
        {
            10 => [10, rng.RandInt(0, 255), rng.RandInt(0, 255), rng.RandInt(1, 254)],
            172 => [172, rng.RandInt(16, 31), rng.RandInt(0, 255), rng.RandInt(1, 254)],
            _ => [192, 168, rng.RandInt(0, 255), rng.RandInt(1, 254)],
        };
        var adresse = oktette.Aggregate(BigInteger.Zero, (w, o) => (w << 8) | o);
        var maske = Subnetting.V4Maske(praefix);
        var netz = adresse & maske;
        var broadcast = netz | (maske ^ uint.MaxValue);
        var hosts = (BigInteger.One << (32 - praefix)) - 2;
        var ersteHost = netz + 1;
        var letzteHost = broadcast - 1;
        var blockOktett = praefix / 8;
        List<(string, string)> felder =
        [
            ("netz", "Netzadresse"), ("maske", "Subnetzmaske"), ("broadcast", "Broadcast-Adresse"),
            ("erste", "Erste Host-Adresse"), ("letzte", "Letzte Host-Adresse"), ("hosts", "Nutzbare Hosts"),
        ];
        var loesung = new Dictionary<string, string>
        {
            ["netz"] = V4Text(netz), ["maske"] = V4Text(maske), ["broadcast"] = V4Text(broadcast),
            ["erste"] = V4Text(ersteHost), ["letzte"] = V4Text(letzteHost), ["hosts"] = hosts.ToString(CultureInfo.InvariantCulture),
        };
        var pruefer = new Dictionary<string, Func<string, bool>>
        {
            ["netz"] = IpPruefer(V4(netz)), ["maske"] = IpPruefer(V4(maske)), ["broadcast"] = IpPruefer(V4(broadcast)),
            ["erste"] = IpPruefer(V4(ersteHost)), ["letzte"] = IpPruefer(V4(letzteHost)), ["hosts"] = ZahlPruefer(hosts),
        };
        var schritte = new List<string>
        {
            I($"1. Präfix /{praefix}: {praefix} Bit Netzanteil, {32 - praefix} Bit Hostanteil."),
            I($"2. Maske: {praefix} Einsen = {V4Text(maske)}"),
        };
        if (schmal)
        {
            schritte.AddRange(UndSchmal(adresse, maske, netz));
        }
        else
        {
            schritte.Add("3. Adresse binär:   " + PunktBinaer(adresse));
            schritte.Add("   Maske binär:     " + PunktBinaer(maske));
            schritte.Add("   UND-Verknüpfung: " + PunktBinaer(netz) + " = " + V4Text(netz));
        }
        if (praefix % 8 != 0)
        {
            int maskenOktett = Oktette(maske)[blockOktett];
            schritte.Add(I($"   Kurzweg: Blockgröße im {blockOktett + 1}. Oktett = 256 - {maskenOktett} = {256 - maskenOktett}; ")
                + I($"{Oktette(adresse)[blockOktett]} liegt im Block ab {Oktette(netz)[blockOktett]}."));
            if (blockOktett < 3)
            {
                schritte.Add("   Die folgenden Oktette sind in der Netzadresse 0 und in der Broadcast-Adresse 255.");
            }
        }
        schritte.Add("4. Broadcast-Adresse: alle Hostbits auf 1 = " + V4Text(broadcast));
        schritte.Add("5. Hostbereich: " + V4Text(ersteHost) + " bis " + V4Text(letzteHost)
            + " (Netzadresse + 1 bis Broadcast-Adresse - 1)");
        schritte.Add(I($"6. Nutzbare Hosts: 2^{32 - praefix} - 2 = {hosts}"));
        var text = I($"Berechne zur Adresse {V4Text(adresse)}/{praefix} die Netzdaten (die Adresse kann auch selbst ")
            + "die Netz- oder Broadcast-Adresse sein).";
        return new TrainerAufgabe("ipv4", stufe, text, felder, loesung, schritte, pruefer);
    }

    // ---------------------------------------------------------------- VLSM

    /// <summary>_vlsm_task.</summary>
    private static TrainerAufgabe Vlsm(PythonZufall rng, int stufe)
    {
        var anzahl = Waehle(stufe, 2, 3, 4);
        // {1: 24, 2: 24, 3: rng.choice([22, 23])}[level] zieht die Auswahl immer
        var gross = (int)rng.Choice(new long[] { 22, 23 });
        var basisPraefix = Waehle(stufe, 24, 24, gross);
        var kapazitaet = 1 << (32 - basisPraefix);
        List<long> gewuenscht;
        List<long> groessen;
        var bereich = Enumerable.Range(5, kapazitaet / anzahl - 5).Select(z => (long)z).ToList();
        while (true)
        {
            gewuenscht = [.. rng.Sample(bereich, anzahl).OrderByDescending(z => z)];
            groessen = [];
            foreach (var bedarf in gewuenscht)
            {
                long groesse = 4;
                while (groesse - 2 < bedarf)
                {
                    groesse *= 2;
                }
                groessen.Add(groesse);
            }
            if (groessen.Sum() <= kapazitaet)
            {
                break;
            }
        }
        BigInteger basis;
        if (basisPraefix == 24)
        {
            basis = new BigInteger((192L << 24) | (168L << 16) | (rng.RandInt(0, 250) << 8));
        }
        else
        {
            var drittes = rng.RandInt(0, 60) * 4;
            var zweites = rng.RandInt(0, 255);
            basis = new BigInteger((10L << 24) | (zweites << 16) | (drittes << 8)) & Subnetting.V4Maske(basisPraefix);
        }
        var abteilungen = rng.Sample(VlsmNamen, anzahl);
        var reihenfolge = Enumerable.Range(0, anzahl).OrderBy(i => -gewuenscht[i]).ThenBy(i => i).ToList();
        var start = basis;
        var netze = new Dictionary<int, (BigInteger Netz, int Praefix)>();
        var schritte = new List<string> { "Vorgehen: größtes Netz zuerst, jedes Netz beginnt direkt hinter dem vorherigen." };
        var nummer = 1;
        foreach (var index in reihenfolge)
        {
            var groesse = groessen[index];
            var praefix = 32 - Subnetting.Bits(groesse) + 1;
            netze[index] = (start, praefix);
            var ende = start + groesse - 1;
            schritte.Add(I($"{nummer}. {abteilungen[index]}: {gewuenscht[index]} Hosts + 2 = {gewuenscht[index] + 2} Adressen nötig -> Block {groesse} (/{praefix}) -> ")
                + NetzText(start, praefix) + ", Broadcast-Adresse " + V4Text(ende));
            start += groesse;
            nummer++;
        }
        var felder = Enumerable.Range(0, anzahl)
            .Select(i => (I($"netz{i}"), I($"{abteilungen[i]} ({gewuenscht[i]} Hosts)"))).ToList();
        var loesung = Enumerable.Range(0, anzahl).ToDictionary(i => I($"netz{i}"), i => NetzText(netze[i].Netz, netze[i].Praefix));
        var pruefer = Enumerable.Range(0, anzahl).ToDictionary(i => I($"netz{i}"),
            i => NetzPruefer(new IpNetz(V4(netze[i].Netz), netze[i].Praefix)));
        var text = "Teile das Netz " + NetzText(basis, basisPraefix) + " nach VLSM auf (größtes Netz zuerst, lückenlos ab der "
            + "ersten Adresse). Gib je Abteilung Netzadresse/Präfix an, z. B. 192.168.1.0/26.";
        return new TrainerAufgabe("vlsm", stufe, text, felder, loesung, schritte, pruefer);
    }

    // ---------------------------------------------------------------- Zahlensysteme

    /// <summary>_number_task.</summary>
    private static TrainerAufgabe Zahlensysteme(PythonZufall rng, int stufe)
    {
        var hoechstens = Waehle(stufe, 255, 4095, 65535);
        var wert = rng.RandInt(stufe == 1 ? 1 : 16, hoechstens);
        var gegeben = rng.Choice(new[] { "dez", "bin", "hex" });
        var stellen = Waehle(stufe, 8, 12, 16);
        var binaer = Bin(wert);
        var hex = wert.ToString("X", CultureInfo.InvariantCulture);
        var gezeigt = new Dictionary<string, string>
        {
            ["dez"] = wert.ToString(CultureInfo.InvariantCulture), ["bin"] = binaer.PadLeft(stellen, '0'), ["hex"] = hex,
        };
        var namen = new Dictionary<string, string> { ["dez"] = "Dezimal", ["bin"] = "Binär", ["hex"] = "Hexadezimal" };
        var felder = new[] { "dez", "bin", "hex" }.Where(k => k != gegeben).Select(k => (k, namen[k])).ToList();
        var allePruefer = new Dictionary<string, Func<string, bool>>
        {
            ["dez"] = ZahlPruefer(wert), ["bin"] = BasisPruefer(wert, 2), ["hex"] = BasisPruefer(wert, 16),
        };
        var loesung = felder.ToDictionary(f => f.k, f => gezeigt[f.k]);
        // Vierergruppen von rechts; die erste Gruppe kann kuerzer sein
        var nibbles = new List<string>();
        for (var ende = binaer.Length; ende > 0; ende -= 4)
        {
            nibbles.Insert(0, binaer[Math.Max(0, ende - 4)..ende]);
        }
        var teile = binaer.Select((bit, i) => (bit, i)).Where(t => t.bit == '1')
            .Select(t => (BigInteger.One << (binaer.Length - 1 - t.i)).ToString(CultureInfo.InvariantCulture)).ToList();
        var schritte = new List<string>();
        if (gegeben == "dez")
        {
            var zeilen = new List<string>();
            for (var rest = wert; rest != 0; rest /= 2)
            {
                zeilen.Add(I($"{rest} : 2 = {rest / 2} Rest {rest % 2}"));
            }
            schritte.Add("Dezimal -> Binär (fortgesetzt durch 2 teilen, Reste von unten nach oben lesen):");
            schritte.AddRange(zeilen.Select(z => "   " + z));
        }
        else if (gegeben == "hex")
        {
            var terme = hex.Select((ziffer, i) => I($"{ziffer} x 16^{hex.Length - 1 - i}"));
            schritte.Add("Hex -> Dezimal: " + string.Join(" + ", terme) + I($" = {wert}"));
            var buchstaben = hex.Where(z => "ABCDEF".Contains(z)).Distinct().OrderBy(z => z).ToList();
            if (buchstaben.Count > 0)
            {
                schritte.Add("   Hex-Ziffern: " + string.Join(", ", buchstaben.Select(z => I($"{z} = {z - 'A' + 10}"))));
            }
        }
        else
        {
            schritte.Add("Stellenwerte der Einsen addieren: " + string.Join(" + ", teile) + I($" = {wert}"));
        }
        var gruppen = string.Join(" ", nibbles.Select(n => n.PadLeft(4, '0')));
        schritte.Add(gegeben == "hex"
            ? "Hex -> Binär: jede Hex-Ziffer als Vierergruppe (Nibble) schreiben: " + gruppen
            : "Binär -> Hex: in Vierergruppen (Nibbles) von rechts teilen: " + gruppen);
        schritte.Add("Jede Gruppe ist eine Hex-Ziffer: " + string.Join(" ",
            nibbles.Select(n => n.PadLeft(4, '0') + "=" + Convert.ToInt32(n, 2).ToString("X", CultureInfo.InvariantCulture))));
        var text = "Rechne " + namen[gegeben] + " " + gezeigt[gegeben] + " in die anderen Zahlensysteme um.";
        return new TrainerAufgabe("zahlen", stufe, text, felder, loesung, schritte,
            felder.ToDictionary(f => f.k, f => allePruefer[f.k]));
    }

    // ---------------------------------------------------------------- IPv6-Schreibweise

    /// <summary>_ipv6_task.</summary>
    private static TrainerAufgabe Ipv6(PythonZufall rng, int stufe)
    {
        var gruppen = new long[8];
        var nullfolgen = Waehle(stufe, 1, 2, 2);
        for (var i = 0; i < 8; i++)
        {
            // rng.choice([0, 0, randint(1, 0xF), randint(0x10, 0xFFF), randint(0x1000, 0xFFFF)])
            long a = rng.RandInt(1, 0xF), b = rng.RandInt(0x10, 0xFFF), c = rng.RandInt(0x1000, 0xFFFF);
            gruppen[i] = rng.Choice(new[] { 0, 0, a, b, c });
        }
        gruppen[0] = 0x2001;
        gruppen[1] = 0x0DB8;
        for (var n = 0; n < nullfolgen; n++)
        {
            var start = (int)rng.RandInt(2, 6);
            var laenge = (int)rng.RandInt(1, 8 - start);
            for (var i = start; i < start + laenge; i++)
            {
                gruppen[i] = 0;
            }
        }
        var adresse = V6(gruppen.Aggregate(BigInteger.Zero, (w, g) => (w << 16) | g));
        var voll = PythonIp.Ausgeschrieben(adresse);
        var kurz = PythonIp.Kurzform(adresse);
        List<(string, string)> felder;
        Dictionary<string, string> loesung;
        Dictionary<string, Func<string, bool>> pruefer;
        string text;
        List<string> schritte;
        if (stufe == 3 && rng.Random() < 0.5)
        {
            felder = [("lang", "Ausgeschrieben (8 Blöcke à 4 Stellen)")];
            loesung = new() { ["lang"] = voll };
            pruefer = new() { ["lang"] = Ipv6TextPruefer(voll) };
            text = "Schreibe die IPv6-Adresse " + kurz + " vollständig aus.";
            schritte =
            [
                "1. '::' steht für so viele Nullblöcke, dass es 8 Blöcke werden.",
                "2. Jeden Block mit führenden Nullen auf 4 Stellen auffüllen.",
                "Ergebnis: " + voll,
            ];
        }
        else
        {
            felder = [("kurz", "Kürzeste Schreibweise")];
            loesung = new() { ["kurz"] = kurz };
            pruefer = new() { ["kurz"] = Ipv6TextPruefer(kurz) };
            text = "Kürze die IPv6-Adresse " + voll + " so weit wie möglich.";
            schritte =
            [
                "1. Führende Nullen in jedem Block weglassen: " + string.Join(":", gruppen.Select(g => g.ToString("x", CultureInfo.InvariantCulture))),
                "2. Die längste Folge von Nullblöcken (mindestens zwei) durch '::' ersetzen, bei Gleichstand die erste.",
            ];
            if (!kurz.Contains("::", StringComparison.Ordinal))
            {
                schritte.Add("   Hier gibt es keine solche Folge, dieser Schritt entfällt.");
            }
            schritte.Add("3. '::' darf nur einmal vorkommen; Kleinbuchstaben verwenden.");
            schritte.Add("Ergebnis: " + kurz);
        }
        return new TrainerAufgabe("ipv6", stufe, text, felder, loesung, schritte, pruefer);
    }

    // ---------------------------------------------------------------- Netz aufteilen

    /// <summary>_trainer_v4_net: zufaelliges privates IPv4-Netz mit diesem Praefix.</summary>
    private static BigInteger ZufallsnetzV4(PythonZufall rng, int praefix)
    {
        var auswahl = new List<long> { 10 };
        if (praefix >= 12)
        {
            auswahl.Add(172);
        }
        if (praefix >= 16)
        {
            auswahl.Add(192);
        }
        var erstes = rng.Choice(auswahl);
        BigInteger wert;
        if (erstes == 10)
        {
            wert = (new BigInteger(10) << 24) | rng.GetRandBits(24);
        }
        else if (erstes == 172)
        {
            var zweites = 16 + rng.RandInt(0, 15);
            wert = (new BigInteger(172) << 24) | (new BigInteger(zweites) << 16) | rng.GetRandBits(16);
        }
        else
        {
            wert = (new BigInteger(192) << 24) | (new BigInteger(168) << 16) | rng.GetRandBits(16);
        }
        return wert >> (32 - praefix) << (32 - praefix);
    }

    /// <summary>_trainer_v6_net: zufaelliges Netz in 2001:db8::/32.</summary>
    private static BigInteger ZufallsnetzV6(PythonZufall rng, int praefix)
    {
        var wert = new BigInteger(0x20010DB8) << 96;
        if (praefix > 32)
        {
            wert |= rng.GetRandBits(praefix - 32) << (128 - praefix);
        }
        return wert;
    }

    /// <summary>_narrow_address: Adresse hinter der Beschriftung, sonst an einem Doppelpunkt geteilt.</summary>
    private static List<string> SchmaleAdresse(string beschriftung, string adresse)
    {
        if (L(beschriftung + adresse) <= CALC_EXPLAIN_HANDY_WIDTH)
        {
            return [beschriftung + adresse];
        }
        var schnitt = Zeilen.Finde(adresse, ":", L(adresse) / 2 - 3) + 1;
        return [beschriftung + PythonZeichen.Teil(adresse, 0, schnitt), "   " + PythonZeichen.Teil(adresse, schnitt)];
    }

    /// <summary>_split_nibble_lines: Vorueberlegung "an Nibble-Grenzen".</summary>
    private static List<string> NibbleZeilen(long gewuenscht, int praefix, int bits, int nibble, bool schmal)
    {
        var neu = praefix + bits;
        var gesamt = D(BigInteger.One << (nibble - praefix));
        var benutzt = D(gewuenscht);
        if (schmal)
        {
            return
            [
                "VORÜBERLEGUNG:", "NIBBLE-GRENZE",
                " " + benutzt + " Subnetze brauchen",
                I($" k = {bits} Bit: /{praefix} + {bits} = /{neu}"),
                I($" /{neu} liegt nicht an einer"), " 4-Bit-Grenze (Nibble).",
                I($" Die nächste ist /{nibble}."),
                I($" Dort entstehen 2^{nibble - praefix}"),
                " = " + gesamt + " Subnetze. Damit", " geht es weiter; verwendet",
                " werden die ersten " + benutzt + ".", "",
            ];
        }
        return
        [
            "VORÜBERLEGUNG: NIBBLE-GRENZE",
            "   " + benutzt + I($" Subnetze brauchen k = {bits} Bit: /{praefix} + {bits} = /{neu}."),
            I($"   /{neu} liegt nicht an einer 4-Bit-Grenze (Nibble). Die nächste ist /{nibble}."),
            I($"   Dort entstehen 2^{nibble - praefix} = ") + gesamt + " Subnetze. Damit geht es weiter;",
            "   verwendet werden die ersten " + benutzt + ".", "",
        ];
    }

    /// <summary>_split_pick_lines: letzter Schritt, das gefragte Subnetz Nr. nummer.</summary>
    private static List<string> AuswahlZeilen(Aufteilung a, long nummer, bool schmal)
    {
        var version = a.Version;
        var groesse = a.Groesse;
        var eintrag = Subnetting.Eintrag(a, nummer - 1);
        var basis = Subnetting.AdressText(version, a.Netz);
        var netz = Subnetting.AdressText(version, eintrag.Netz);
        var ende = Subnetting.AdressText(version, eintrag.Ende);
        var groesseText = version == 4 ? D(groesse) : I($"2^{a.Basis.Breite - a.Neu}");
        var endeName = version == 4 ? "Broadcast-Adresse" : "Letzte Adresse";
        var bits = a.K != 0 ? Bin(nummer - 1).PadLeft(a.K, '0') : "0";
        var schritt = D((nummer - 1) * groesse);
        if (schmal)
        {
            var zeilen = new List<string> { "", "SCHRITT 6: Gefragt ist", I($"Subnetz Nr. {nummer}") };
            if (version == 6)
            {
                zeilen.Add(I($" Subnetz-Bits: {nummer} - 1 = {nummer - 1}"));
                zeilen.Add(" = binär " + bits);
            }
            zeilen.Add(" Netzadresse = Nr. 1");
            zeilen.Add(I($" + ({nummer} - 1) x ") + groesseText);
            if (version == 4)
            {
                zeilen.Add(" = " + basis);
                zeilen.Add(" + " + schritt);
            }
            zeilen.AddRange(SchmaleAdresse(" = ", netz));
            zeilen.Add(" " + endeName);
            zeilen.Add(" = nächste Netzadresse - 1");
            zeilen.AddRange(SchmaleAdresse(" = ", ende));
            return zeilen;
        }
        var pc = new List<string> { "", I($"SCHRITT 6: Gefragt ist Subnetz Nr. {nummer}") };
        if (version == 6)
        {
            pc.Add(I($"   Subnetz-Bits: {nummer} - 1 = {nummer - 1} = binär ") + bits);
        }
        pc.Add(I($"   Netzadresse = Nr. 1 + ({nummer} - 1) x ") + groesseText + " Adressen");
        if (version == 4)
        {
            pc.Add("   = " + basis + " + " + schritt + " = " + netz);
            pc.Add("   " + endeName + " = nächste Netzadresse - 1 = " + ende);
        }
        else
        {
            pc.Add("   = " + netz);
            pc.Add("   " + endeName + " = nächste Netzadresse - 1");
            pc.Add("   = " + ende);
        }
        return pc;
    }

    /// <summary>_explain_list_indexes(total)[1]: Luecke der gelisteten Subnetze, sonst null.</summary>
    private static (BigInteger Von, BigInteger Bis)? Luecke(BigInteger gesamt) =>
        gesamt <= SUBNET_EXPLAIN_ALL ? null : (5, gesamt - 2);

    /// <summary>_explain_list_indexes(total)[0] ohne die Luecke (None).</summary>
    private static IEnumerable<BigInteger> Gelistet(BigInteger gesamt)
    {
        if (gesamt <= SUBNET_EXPLAIN_ALL)
        {
            for (BigInteger i = 0; i < gesamt; i++)
            {
                yield return i;
            }
            yield break;
        }
        foreach (var i in new BigInteger[] { 0, 1, 2, 3, gesamt - 2, gesamt - 1 })
        {
            yield return i;
        }
    }

    /// <summary>_split_gap_lines: Lueckenzeile im Trainer-Rechenweg ersetzen (PC in der Zeile, Handy den Absatz).</summary>
    private static (List<string> Pc, List<string> Schmal) LueckeErsetzen(Aufteilung a, List<string> pc, List<string> schmal)
    {
        if (Luecke(a.Anzahl) is not { } luecke)
        {
            return (pc, schmal);
        }
        pc = [.. pc.Select(z => z.Replace(LueckeAlt, LueckeNeu, StringComparison.Ordinal))];
        var text = "Subnetz " + D(luecke.Von) + " bis " + D(luecke.Bis) + ": je + Schrittweite ";
        var alt = Zeilen.Absatz(text + LueckeAlt, "", CALC_EXPLAIN_HANDY_WIDTH);
        var neu = Zeilen.Absatz(text + LueckeNeu, "", CALC_EXPLAIN_HANDY_WIDTH);
        for (var start = 0; start < schmal.Count - alt.Count + 1; start++)
        {
            if (schmal.Skip(start).Take(alt.Count).SequenceEqual(alt))
            {
                return (pc, [.. schmal.Take(start), .. neu, .. schmal.Skip(start + alt.Count)]);
            }
        }
        return (pc, schmal);
    }

    /// <summary>_split_reserve_lines: am Handy heissen gelistete Subnetze ab Nr. wanted + 1 "frei, Reserve".</summary>
    private static List<string> ReserveZeilen(Aufteilung a, long gewuenscht, List<string> schmal)
    {
        foreach (var index in Gelistet(a.Anzahl))
        {
            if (index >= gewuenscht)
            {
                var alt = "Subnetz " + D(index + 1) + ": " + SUBNET_STATE_USED;
                var neu = "Subnetz " + D(index + 1) + ": " + SUBNET_STATE_FREE;
                schmal = [.. schmal.Select(z => z == alt ? neu : z)];
            }
        }
        return schmal;
    }

    /// <summary>_split_task: N gleich grosse Subnetze (Stufe 3 auch IPv6, zur Haelfte an Nibble-Grenzen).</summary>
    private static TrainerAufgabe Teilen(PythonZufall rng, int stufe)
    {
        var version = stufe == 3 && rng.Random() < 0.5 ? 6 : 4;
        var nibble = version == 6 && rng.Random() < 0.5;
        int praefix, bits, neu;
        long gewuenscht;
        while (true)
        {
            if (version == 4)
            {
                praefix = stufe == 1 ? 24 : (int)(stufe == 2 ? rng.RandInt(16, 23) : rng.RandInt(8, 22));
                gewuenscht = rng.RandInt(2, Waehle(stufe, 8, 32, 300));
            }
            else
            {
                praefix = (int)rng.RandInt(32, 56);
                gewuenscht = rng.RandInt(2, 300);
            }
            bits = Subnetting.Bits(gewuenscht - 1);
            neu = praefix + bits;
            if (version == 4 && neu <= 30)
            {
                break;
            }
            if (version == 6 && (!nibble || neu % 4 != 0) && (neu + 3) / 4 * 4 <= 64)
            {
                break;
            }
        }
        var breite = version == 4 ? 32 : 128;
        var wert = version == 4 ? ZufallsnetzV4(rng, praefix) : ZufallsnetzV6(rng, praefix);
        var netzText = Subnetting.NetzText(version, wert, praefix);
        var nummer = rng.RandInt(2, gewuenscht);
        Aufteilung a;
        List<string> pc, schmal;
        if (nibble)
        {
            var ziel = (neu + 3) / 4 * 4;
            a = Subnetting.Aufteilen(netzText, BigInteger.One << (ziel - praefix), version);
            pc = NibbleZeilen(gewuenscht, praefix, bits, ziel, false);
            schmal = NibbleZeilen(gewuenscht, praefix, bits, ziel, true);
        }
        else
        {
            a = Subnetting.Aufteilen(netzText, gewuenscht, version);
            pc = [];
            schmal = [];
        }
        pc.AddRange(Rechenweg.Aufteilen(a).Split('\n'));
        pc.AddRange(AuswahlZeilen(a, nummer, false));
        schmal.AddRange(Rechenweg.Aufteilen(a, true).Split('\n'));
        schmal.AddRange(AuswahlZeilen(a, nummer, true));
        (pc, schmal) = LueckeErsetzen(a, pc, schmal);
        if (nibble)
        {
            schmal = ReserveZeilen(a, gewuenscht, schmal);
        }
        var eintrag = Subnetting.Eintrag(a, nummer - 1);
        var groesse = a.Groesse;
        List<(string, string)> felder;
        Dictionary<string, string> loesung;
        Dictionary<string, Func<string, bool>> pruefer;
        string gefragt;
        if (version == 4)
        {
            felder =
            [
                ("praefix", "Neue Präfixlänge"), ("maske", "Subnetzmaske"),
                ("adressen", "Adressen je Subnetz"), ("hosts", "Nutzbare Hosts je Subnetz"),
                ("netz", I($"Netzadresse des {nummer}. Subnetzes (ohne Präfix)")),
                ("broadcast", I($"Broadcast-Adresse des {nummer}. Subnetzes")),
            ];
            loesung = new()
            {
                ["maske"] = V4Text(a.Maske!.Value), ["adressen"] = D(groesse),
                ["hosts"] = D(a.Hosts!.Value), ["broadcast"] = V4Text(eintrag.Ende),
            };
            pruefer = new()
            {
                ["maske"] = IpPruefer(V4(a.Maske.Value)), ["adressen"] = AnzahlPruefer(groesse, potenz: true),
                ["hosts"] = AnzahlPruefer(a.Hosts.Value), ["broadcast"] = IpPruefer(V4(eintrag.Ende)),
            };
            gefragt = "neue Präfixlänge, Subnetzmaske, Adressen und nutzbare Hosts je Subnetz "
                + I($"sowie Netzadresse und Broadcast-Adresse des {nummer}. Subnetzes");
        }
        else
        {
            felder =
            [
                ("praefix", "Neue Präfixlänge"),
                ("adressen", "Adressen je Subnetz (als 2^n)"),
                ("netz", I($"Netzadresse des {nummer}. Subnetzes (ohne Präfix)")),
                ("letzte", I($"Letzte Adresse des {nummer}. Subnetzes")),
            ];
            loesung = new() { ["adressen"] = I($"2^{breite - a.Neu}"), ["letzte"] = Subnetting.AdressText(6, eintrag.Ende) };
            pruefer = new() { ["adressen"] = AnzahlPruefer(groesse, potenz: true), ["letzte"] = IpPruefer(V6(eintrag.Ende)) };
            gefragt = I($"neue Präfixlänge, Adressen je Subnetz sowie Netzadresse und letzte Adresse des {nummer}. Subnetzes");
        }
        loesung["praefix"] = I($"/{a.Neu}");
        loesung["netz"] = Subnetting.AdressText(version, eintrag.Netz);
        pruefer["praefix"] = PraefixPruefer(a.Neu);
        pruefer["netz"] = IpPruefer(new IpAdresse(version, eintrag.Netz));
        var text = "Das Netz " + netzText + " soll in " + D(gewuenscht) + " gleich große Subnetze "
            + (nibble ? "an Nibble-Grenzen (4-Bit-Grenzen) " : "") + "aufgeteilt werden. Gib an: " + gefragt + ".";
        return new TrainerAufgabe("teilen", stufe, text, felder, loesung, pc, pruefer, schmal);
    }

    // ---------------------------------------------------------------- Nach Hostbedarf

    /// <summary>_hosts_sizes: Netz an zufaelligen Bloecken halbieren, bis count (voll) bzw. count + 1 Bloecke da sind.</summary>
    private static List<long> Blockgroessen(PythonZufall rng, long kapazitaet, int anzahl, bool voll)
    {
        var bloecke = new List<long> { kapazitaet };
        while (bloecke.Count < anzahl + (voll ? 0 : 1))
        {
            var moeglich = Enumerable.Range(0, bloecke.Count).Where(i => bloecke[i] >= 8).ToList();
            var index = rng.Choice(moeglich);
            var haelfte = bloecke[index] / 2;
            bloecke.RemoveAt(index);
            bloecke.Add(haelfte);
            bloecke.Add(haelfte);
        }
        if (!voll)
        {
            bloecke.RemoveAt((int)rng.RandRange(bloecke.Count));
        }
        rng.Shuffle(bloecke);
        return bloecke;
    }

    /// <summary>_hosts_task: erst die Groessen, dann je Groesse ein passender Bedarf (immer loesbar).</summary>
    private static TrainerAufgabe Bedarf(PythonZufall rng, int stufe)
    {
        var (von, bis) = Waehle(stufe, (2, 3), (3, 4), (4, 6));
        var anzahl = (int)rng.RandInt(von, bis);
        var praefix = stufe == 1 ? 24 : (int)rng.Choice(stufe == 2 ? new long[] { 23, 24 } : new long[] { 22, 23 });
        // rng.random() < 1 / 3 wird als Argument vor _hosts_sizes gezogen
        var voll = rng.Random() < 1.0 / 3;
        var groessen = Blockgroessen(rng, 1L << (32 - praefix), anzahl, voll);
        var bedarfe = groessen.Select(g => rng.RandInt(Math.Max(1, g / 2 - 1), g - 2)).ToList();
        var namen = rng.Sample(Namen, anzahl);
        var netzText = Subnetting.NetzText(4, ZufallsnetzV4(rng, praefix), praefix);
        var r = Subnetting.NachBedarf(netzText, [.. namen.Zip(bedarfe, (n, b) => (n, new BigInteger(b)))], 4);
        var nummer = (int)rng.RandInt(1, anzahl);
        var felder = new List<(string, string)>();
        var loesung = new Dictionary<string, string>();
        var pruefer = new Dictionary<string, Func<string, bool>>();
        for (var index = 0; index < r.Subnetze.Count; index++)
        {
            var netz = r.Subnetze[index];
            felder.Add((I($"netz{index}"), netz.Name + " (" + Subnetting.HostsText(netz.Bedarf) + "): Netzadresse/Präfix"));
            felder.Add((I($"maske{index}"), netz.Name + ": Subnetzmaske"));
            loesung[I($"netz{index}")] = NetzText(netz.Netz, netz.Praefix);
            loesung[I($"maske{index}")] = V4Text(netz.Maske);
            pruefer[I($"netz{index}")] = NetzPruefer(new IpNetz(V4(netz.Netz), netz.Praefix));
            pruefer[I($"maske{index}")] = IpPruefer(V4(Subnetting.V4Maske(netz.Praefix)));
        }
        var gefragt = r.Subnetze[r.Reihenfolge[nummer - 1]].Broadcast;
        felder.Add(("broadcast", I($"Broadcast-Adresse des {nummer}. vergebenen Subnetzes")));
        felder.Add(("rest", "Freier Rest (Anzahl Adressen)"));
        loesung["broadcast"] = V4Text(gefragt);
        loesung["rest"] = D(r.Rest!.Value);
        pruefer["broadcast"] = IpPruefer(V4(gefragt));
        pruefer["rest"] = AnzahlPruefer(r.Rest.Value);
        var wuensche = string.Join(", ", r.Subnetze.Select(s => s.Name + ": " + Subnetting.HostsText(s.Bedarf)));
        var text = "Das Netz " + netzText + " soll nach Hostbedarf aufgeteilt werden: " + wuensche + ". Vergeben wird "
            + "lückenlos ab der ersten Adresse, das größte Subnetz zuerst. Gib je "
            + "Subnetz Netzadresse/Präfix und Subnetzmaske an, dazu die Broadcast-Adresse "
            + I($"des {nummer}. vergebenen Subnetzes und den freien Rest in Adressen.");
        return new TrainerAufgabe("bedarf", stufe, text, felder, loesung,
            Rechenweg.Bedarf(r).Split('\n'), pruefer, Rechenweg.Bedarf(r, true).Split('\n'));
    }
}
