using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Suche;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Zufall;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3 W4a, D6 (Weisung P3-17): Trainer-Aufgabenarten "einheiten" (Datenmengen) und "dauer"
/// (Uebertragungsdauer), je drei Stufen. Kein Python-Vorbild; die Wertebereiche und die
/// Reihenfolge der Zufallsaufrufe stehen gleich im unabhaengigen Skript
/// werkzeuge/vergleich/erzeuge_w4_rechentrainer.py (Vergleichswerte G31). Zufall wie R4:
/// random.Random("art-stufe-startwert"). Rundungsgrenzfaelle zieht der Bau neu (3. Nachkommastelle
/// 5, Minutenueberlauf ab 59,995 s; Auflage P3-17 4c). Eingabe: Zahl nach der vorhandenen Leseregel;
/// keine Zahl, leer oder mit Einheit ist ein Eingabefehler (roter Hinweis, nichts gespeichert,
/// Auflage 4d). Pruefung genau; nur die Sekunden in "dauer - Schwer" mit +-0,01 (F5a).
/// W4b (Weisung P3-21): Art "video" (Bild und Video) nach Wortliste W4b Abschnitt 6, ebenfalls
/// gegen G31; Pruefung immer genau.
/// </summary>
public static class DatenTrainer
{
    private static readonly (string Von, string Nach)[] Leicht =
        [("kB", "Byte"), ("MB", "kB"), ("GB", "MB"), ("TB", "GB"), ("Byte", "kB"), ("kB", "MB"), ("MB", "GB"), ("GB", "TB")];

    private static readonly (string Von, string Nach)[] LeichtBit = [("Byte", "Bit"), ("Bit", "Byte")];

    private static readonly (string Von, string Nach)[] Mittel =
    [
        ("KiB", "Byte"), ("MiB", "KiB"), ("GiB", "MiB"), ("TiB", "GiB"), ("MiB", "Byte"), ("GiB", "KiB"),
        ("TiB", "MiB"), ("Byte", "KiB"), ("KiB", "MiB"), ("MiB", "GiB"), ("GiB", "TiB"),
    ];

    private static readonly (string Von, string Nach)[] Schwer =
        [("GB", "GiB"), ("TB", "TiB"), ("MB", "MiB"), ("GiB", "GB"), ("TiB", "TB"), ("MiB", "MB"), ("TB", "GiB"), ("GB", "MiB")];

    private static readonly long[] RatenLeicht = [2, 4, 5, 6, 8, 10, 16, 20, 25, 50, 100];
    private static readonly long[] RatenMittel = [10, 16, 20, 25, 40, 50, 80, 100, 200, 250, 500];
    private static readonly long[] RatenSchwer = [16, 25, 50, 100, 250, 500, 1000];
    private static readonly long[] ProzentSchwer = [50, 60, 70, 75, 80, 85, 90, 95];

    private static string D(double wert) => Zahlen.DeZahl(wert, 0);

    private static string S(long wert) => wert.ToString(CultureInfo.InvariantCulture);

    private static string System(string einheit) => Datenmenge.Dezimal.Contains(einheit) ? "Dezimal" : "Binär";

    /// <summary>3. Nachkommastelle des kuerzesten Dezimaltexts ist 5 (int(Decimal(repr(x)) * 1000) % 10 == 5).</summary>
    internal static bool DritteStelleFuenf(double x)
    {
        var (_, ziffern, exponent) = Zahlen.Kuerzest(x);
        var schieben = exponent + 3;
        var tausendstel = schieben >= 0 ? ziffern * BigInteger.Pow(10, schieben) : ziffern / BigInteger.Pow(10, -schieben);
        return tausendstel % 10 == 5;
    }

    /// <summary>Decimal(repr(t)) % 60 &lt; 59,995: die Sekunden laufen beim Runden nicht in die naechste Minute.</summary>
    internal static bool OhneMinutenUeberlauf(double t)
    {
        var (_, ziffern, exponent) = Zahlen.Kuerzest(t);
        if (exponent >= 0)
        {
            return true;    // ganze Sekunden: Rest hoechstens 59
        }
        var zehner = BigInteger.Pow(10, -exponent);
        var rest = ziffern % (60 * zehner);
        return rest * 1000 < 59995 * zehner;
    }

    // ---------------------------------------------------------------- einheiten

    /// <summary>Art "einheiten": Leicht dezimal oder Bit/Byte, Mittel binaer, Schwer dezimal &lt;-&gt; binaer.</summary>
    internal static TrainerAufgabe Einheiten(PythonZufall rng, int stufe)
    {
        var bit = false;
        string von, nach;
        long w;
        double bytes, x;
        if (stufe == 1)
        {
            bit = rng.Choice(new[] { "dez", "bit" }) == "bit";
            if (!bit)
            {
                (von, nach) = rng.Choice(Leicht);
                w = Datenmenge.Faktor(von) > Datenmenge.Faktor(nach) ? rng.RandInt(2, 999) : rng.RandInt(2, 999) * 1000;
            }
            else
            {
                (von, nach) = rng.Choice(LeichtBit);
                w = von == "Byte" ? rng.RandInt(2, 4096) : rng.RandInt(2, 4096) * 8;
            }
            (bytes, x) = Umrechnen(w, von, nach);
        }
        else if (stufe == 2)
        {
            (von, nach) = rng.Choice(Mittel);
            w = Datenmenge.Faktor(von) > Datenmenge.Faktor(nach) ? rng.RandInt(2, 64) : rng.RandInt(1, 64) * 1024;
            (bytes, x) = Umrechnen(w, von, nach);
        }
        else
        {
            while (true)
            {
                (von, nach) = rng.Choice(Schwer);
                w = rng.RandInt(1, 2000);
                (bytes, x) = Umrechnen(w, von, nach);
                if (x >= 0.01 && !DritteStelleFuenf(x))
                {
                    break;
                }
            }
        }
        var loesung = stufe == 3 ? Zahlen.DeZahl(x) : D(x);
        string text;
        var schritte = new List<string>();
        if (bit)
        {
            text = Beschriftungen.Einsetzen(von == "Byte" ? AufgabeBitAusByte : AufgabeByteAusBit, D(w));
            schritte.Add(von == "Byte"
                ? "Byte -> Bit: " + D(w) + " x 8 = " + loesung + " Bit"
                : "Bit -> Byte: " + D(w) + " / 8 = " + loesung + " Byte");
        }
        else
        {
            text = Beschriftungen.Einsetzen(AufgabeUmrechnen, D(w), von, nach) + (stufe == 3 ? AufgabeRunden2 : "");
            if (von != "Byte")
            {
                schritte.Add(System(von) + " -> Byte: " + D(w) + " x " + DatenBerichte.Potenz(von) + " = " + D(bytes) + " Byte");
            }
            if (nach != "Byte")
            {
                schritte.Add("Byte -> " + PythonKlein.Klein(System(nach)) + ": " + D(bytes) + " / " + DatenBerichte.Potenz(nach) + " = " + loesung + " " + nach);
            }
        }
        var feld = Beschriftungen.Einsetzen(FeldErgebnis, nach);
        return new TrainerAufgabe("einheiten", stufe, text, [("ergebnis", feld)],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["ergebnis"] = loesung }, schritte,
            new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal)
            {
                ["ergebnis"] = GenauPruefer(loesung),
            },
            zahlenfelder: true);
    }

    /// <summary>byte = w * F (Bit: w / 8), x = byte / F (Bit: byte * 8), genau wie das Skript.</summary>
    private static (double Byte, double X) Umrechnen(long w, string von, string nach)
    {
        var bytes = von == "Bit" ? w / 8.0 : w * Datenmenge.Faktor(von);
        var x = nach == "Bit" ? bytes * 8 : bytes / Datenmenge.Faktor(nach);
        return (bytes, x);
    }

    // ---------------------------------------------------------------- dauer

    /// <summary>Art "dauer": Leicht MB ganze Sekunden, Mittel GB Minuten/Sekunden, Schwer GB/GiB mit Prozent.</summary>
    internal static TrainerAufgabe Dauer(PythonZufall rng, int stufe)
    {
        long g, r, p = 100;
        var e = stufe == 1 ? "MB" : "GB";
        UebertragungErgebnis t;
        if (stufe == 1)
        {
            long s;
            while (true)
            {
                r = rng.Choice(RatenLeicht);
                s = rng.RandInt(10, 900);
                if (s * r % 8 == 0)
                {
                    break;
                }
            }
            g = s * r / 8;
            t = Rechnen(g, e, r, p);
        }
        else if (stufe == 2)
        {
            while (true)
            {
                g = rng.RandInt(1, 50);
                r = rng.Choice(RatenMittel);
                if (g * 8000 % r == 0 && g * 8000 / r >= 60 && g * 8000 / r < 3600)
                {
                    break;
                }
            }
            t = Rechnen(g, e, r, p);
        }
        else
        {
            while (true)
            {
                e = rng.Choice(new[] { "GB", "GiB" });
                g = rng.RandInt(1, 100);
                r = rng.Choice(RatenSchwer);
                p = rng.Choice(ProzentSchwer);
                t = Rechnen(g, e, r, p);
                if (t.Sekunden >= 60 && t.Sekunden < 360000 && !DritteStelleFuenf(t.Sekunden) && OhneMinutenUeberlauf(t.Sekunden))
                {
                    break;
                }
            }
        }
        var (std, min, sek) = t.Aufgeteilt;
        var minText = min.ToString(CultureInfo.InvariantCulture);
        var stdText = std.ToString(CultureInfo.InvariantCulture);
        string text;
        List<(string, string)> felder;
        Dictionary<string, string> loesung;
        var pruefer = new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal);
        if (stufe == 1)
        {
            text = Beschriftungen.Einsetzen(AufgabeDauerLeicht, D(g), D(r));
            felder = [("sekunden", FeldSekunden)];
            loesung = new(StringComparer.Ordinal) { ["sekunden"] = D(t.Sekunden) };
        }
        else if (stufe == 2)
        {
            text = Beschriftungen.Einsetzen(AufgabeDauerMittel, D(g), D(r));
            felder = [("minuten", FeldMinuten), ("sekunden", FeldSekunden)];
            loesung = new(StringComparer.Ordinal) { ["minuten"] = minText, ["sekunden"] = sek[..sek.IndexOf(',', StringComparison.Ordinal)] };
        }
        else
        {
            text = Beschriftungen.Einsetzen(AufgabeDauerSchwer, D(g), e, D(r), S(p));
            felder = [("stunden", FeldStunden), ("minuten", FeldMinuten), ("sekunden", FeldSekunden)];
            loesung = new(StringComparer.Ordinal) { ["stunden"] = stdText, ["minuten"] = minText, ["sekunden"] = sek };
        }
        foreach (var (schluessel, _) in felder)
        {
            pruefer[schluessel] = stufe == 3 && schluessel == "sekunden" ? NahePruefer(loesung[schluessel]) : GenauPruefer(loesung[schluessel]);
        }
        var bitText = Datenmenge.GanzOderKurz(t.Bit);
        var rateText = Datenmenge.GanzOderKurz(t.Rate);
        var schritte = new List<string>
        {
            "Bit: " + D(g) + " x " + DatenBerichte.Potenz(e) + " x 8 = " + bitText + " Bit",
            "Wirksame Datenrate: " + D(r) + " x 1.000.000 x " + S(p) + " / 100 = " + rateText + " bit/s",
            "Dauer: " + bitText + " / " + rateText + " = " + Zahlen.DeZahl(t.Sekunden) + " " + Sekunden,
        };
        if (stufe > 1)
        {
            schritte.Add("Aufteilen: " + Zahlen.DeZahl(t.Sekunden) + " Sek. = " + stdText + " Std. " + minText + " Min. " + sek + " Sek.");
        }
        return new TrainerAufgabe("dauer", stufe, text, felder, loesung, schritte, pruefer, zahlenfelder: true);
    }

    /// <summary>bit = g * F * 8, rate = r * 10^6 * p / 100, t = bit / rate (wie das Skript, Ganzzahlen exakt).</summary>
    private static UebertragungErgebnis Rechnen(long g, string e, long r, long p)
    {
        var bit = g * Datenmenge.Faktor(e) * 8;
        var rate = (double)(r * 1_000_000 * p) / 100;
        return new UebertragungErgebnis(UebertragungFehler.Keiner, bit, rate, bit / rate);
    }

    // ---------------------------------------------------------------- video

    private static readonly (long W, long H)[] Aufloesungen =
        [(640, 480), (800, 600), (1024, 768), (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160)];

    private static readonly long[] Farbtiefen = [8, 16, 24, 32];
    private static readonly long[] Bildraten = [24, 25, 30, 50, 60];
    private static readonly long[] Kompressionen = [10, 20, 25, 40, 50, 100, 200];

    /// <summary>
    /// Art "video": Leicht Byte je Bild, Mittel Datenrate unkomprimiert (Mbit/s), Schwer Videodatei
    /// mit Kompression (unter 1 GiB in MiB, sonst GiB, P3-21 F4). Neu gezogen wird bei einer 5 in
    /// der 3. Nachkommastelle. Byte je Bild ist immer ganz (Farbtiefe Vielfaches von 8).
    /// </summary>
    internal static TrainerAufgabe Video(PythonZufall rng, int stufe)
    {
        long w, h, d, fps = 0, m = 0, y = 0, bytes;
        double x;
        var einheit = "Byte";
        var schritte = new List<string>();
        string text, loesung;
        if (stufe == 1)
        {
            (w, h) = rng.Choice(Aufloesungen);
            d = rng.Choice(Farbtiefen);
            var bit = w * h * d;
            bytes = bit / 8;
            loesung = D(bytes);
            text = Beschriftungen.Einsetzen(AufgabeVideoLeicht, S(w), S(h), S(d));
            schritte.Add("Bit je Bild: " + S(w) + " x " + S(h) + " x " + S(d) + " = " + D(bit) + " Bit");
            schritte.Add("Byte je Bild: " + D(bit) + " / 8 = " + loesung + " Byte");
        }
        else if (stufe == 2)
        {
            while (true)
            {
                (w, h) = rng.Choice(Aufloesungen);
                d = rng.Choice(Farbtiefen);
                fps = rng.Choice(Bildraten);
                bytes = w * h * d / 8;
                x = (double)(bytes * fps * 8) / 1_000_000;
                if (!DritteStelleFuenf(x))
                {
                    break;
                }
            }
            einheit = "Mbit/s";
            loesung = Zahlen.DeZahl(x);
            text = Beschriftungen.Einsetzen(AufgabeVideoMittel, S(w), S(h), S(d), S(fps));
            schritte.Add("Byte je Bild: " + S(w) + " x " + S(h) + " x " + S(d) + " / 8 = " + D(bytes) + " Byte");
            schritte.Add("Datenrate: " + D(bytes) + " x " + S(fps) + " x 8 / 1.000.000 = " + loesung + " Mbit/s");
        }
        else
        {
            long unkomp;
            double komp;
            string potenz;
            while (true)
            {
                (w, h) = rng.Choice(Aufloesungen);
                d = rng.Choice(Farbtiefen);
                fps = rng.Choice(Bildraten);
                m = rng.RandInt(1, 10);
                y = rng.Choice(Kompressionen);
                bytes = w * h * d / 8;
                unkomp = bytes * fps * m * 60;
                komp = (double)unkomp / y;
                (einheit, potenz, x) = komp < 1024.0 * 1024 * 1024
                    ? ("MiB", "1024^2", komp / (1024 * 1024))
                    : ("GiB", "1024^3", komp / (1024.0 * 1024 * 1024));
                if (!DritteStelleFuenf(x))
                {
                    break;
                }
            }
            loesung = Zahlen.DeZahl(x);
            var dauer = m == 1 ? "1 " + Minute : S(m) + " " + Minuten;
            text = Beschriftungen.Einsetzen(AufgabeVideoSchwer, S(w), S(h), S(d), S(fps), dauer, S(y));
            var kompText = Datenmenge.GanzOderKurz(komp);
            schritte.Add("Byte je Bild: " + S(w) + " x " + S(h) + " x " + S(d) + " / 8 = " + D(bytes) + " Byte");
            schritte.Add("Datenmenge: " + D(bytes) + " x " + S(fps) + " x " + S(m) + " x 60 = " + D(unkomp) + " Byte");
            schritte.Add("Komprimiert: " + D(unkomp) + " / " + S(y) + " = " + kompText + " Byte");
            schritte.Add("In " + einheit + ": " + kompText + " / " + potenz + " = " + loesung + " " + einheit);
        }
        return new TrainerAufgabe("video", stufe, text, [("ergebnis", Beschriftungen.Einsetzen(FeldErgebnis, einheit))],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["ergebnis"] = loesung }, schritte,
            new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal) { ["ergebnis"] = GenauPruefer(loesung) },
            zahlenfelder: true);
    }

    // ---------------------------------------------------------------- Pruefer

    /// <summary>
    /// Wert nach der Leseregel oder null (keine Zahl, leer, mit Einheit). Die Leseregel laesst
    /// hoechstens 20 Zeichen zu (NUMBER_MAX_CHARS), also hoechstens rund 10^20: das passt immer in decimal.
    /// </summary>
    private static decimal? Wert(string text)
    {
        try
        {
            return (decimal)Zahlen.Lesen(text, "");
        }
        catch (RechnerFehler)
        {
            return null;
        }
    }

    private static decimal Loesungswert(string loesung) =>
        decimal.Parse(loesung.Replace(".", "", StringComparison.Ordinal).Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    /// <summary>Genau gleich (auch "2.000" oder "620,120" fuer 2000 bzw. 620,12).</summary>
    internal static Func<string, bool> GenauPruefer(string loesung)
    {
        var soll = Loesungswert(loesung);
        return text => Wert(text) is decimal w && w == soll;
    }

    /// <summary>Hoechstens 0,01 daneben (nur Sekunden in "dauer - Schwer", F5a).</summary>
    internal static Func<string, bool> NahePruefer(string loesung)
    {
        var soll = Loesungswert(loesung);
        return text => Wert(text) is decimal w && Math.Abs(w - soll) <= 0.01m;
    }
}
