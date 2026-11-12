namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.67.0 (Weisung P12-204, Sammelliste 302, Kurzplan P12-118 Abschnitt 4, Nico 4a 5a): Busfahrt fuer den Spieler.
/// Rechnet ohne Oberflaeche und ohne Zufall, auf allen Geraeten gleich:
/// <list type="bullet">
/// <item>zu Fuss zur Einstiegs-Haltestelle und von der Ausstiegs-Haltestelle zum Ziel ueber das Laufraster der Stadt, je
/// angefangene Zelle ein Schritt (wie jeder freie Weg), je Schritt <c>minutenJeSchritt</c>;</item>
/// <item>fahren: Weg der Front auf der Spur der Buslinie von Haltestelle zu Haltestelle, je Zelle
/// <see cref="MinutenJeZelleBus"/> (4 × so schnell wie zu Fuss);</item>
/// <item>warten: der Takt ist der Umlauf geteilt durch die Zahl der Busse (<see cref="Buslinie.Busse"/>), die Abfahrt an
/// einer Haltestelle folgt aus der Lage des ersten Busses beim Tagesbeginn; gewartet wird von der Ankunft an der Haltestelle
/// bis zur naechsten Abfahrt.</item>
/// </list>
/// Gewaehlt wird das Paar aus Ein- und Ausstieg mit den wenigsten Minuten (gleich: die kleinere Nummer zuerst). Lohnt die
/// Fahrt nicht (nicht weniger Minuten als zu Fuss), entscheidet der Aufrufer.
/// </summary>
public static class Busfahrt
{
    /// <summary>Minuten je Zelle im Bus (zu Fuss 2 Minuten je Schritt, der Bus 4 × so schnell).</summary>
    public const double MinutenJeZelleBus = 0.5;

    /// <summary>Hoechstens so lange dauert die Fahrt im Zeitraffer (Nico 5a); Antippen ueberspringt.</summary>
    public const double ZeitrafferSekunden = 8;

    /// <summary>
    /// Laengster Takt in Minuten (Kurzplan P12-118: hoechstens 15 Spielminuten). Die Busse der Darstellung fahren langsamer als
    /// die Spielzeit; wie lange man wartet, rechnet nur dieser Takt.
    /// </summary>
    public const double TaktHoechstens = 15;

    /// <summary>Abstand der Punkte der Fahrt auf der Spur (Feldmass).</summary>
    public const float PunktAbstand = 0.25f;

    /// <summary>Plan einer Fahrt; die Wege sind Punkte ohne den jeweiligen Startpunkt.</summary>
    public sealed record Plan(
        int Ein, int Aus,
        IReadOnlyList<(double X, double Y)> ZurHaltestelle,
        IReadOnlyList<(double X, double Y)> Fahrt,
        IReadOnlyList<(double X, double Y)> ZumZiel,
        long Schritte, long MinutenZuFuss, long Warten, long MinutenFahrt)
    {
        /// <summary>Alle Minuten der Fahrt (zu Fuss, warten, fahren).</summary>
        public long Minuten => checked(MinutenZuFuss + Warten + MinutenFahrt);
    }

    /// <summary>
    /// Die beste Fahrt von <paramref name="start"/> zu <paramref name="ziel"/>; null ohne Linie, ohne zwei erreichbare
    /// Haltestellen oder ohne Weg zu Fuss. <paramref name="uhr"/> = Uhr beim Losgehen, <paramref name="beginn"/> = Beginn
    /// des Tages (Minuten).
    /// </summary>
    public static Plan? Planen(Buslinie.Linie? linie, IReadOnlyList<Haltestelle> haltestellen, Wegsuche raster,
        (double X, double Y) start, (double X, double Y) ziel, double zelle, long minutenJeSchritt, long uhr, long beginn)
    {
        if (linie is null || linie.Spur.Count < 3 || linie.Halte.Count < 2 || zelle <= 0)
        {
            return null;
        }
        var busse = Buslinie.Busse(linie, haltestellen.Count);
        if (busse.Count == 0)
        {
            return null;
        }
        var laenge = busse[0].Laenge;
        var umlauf = laenge / zelle * MinutenJeZelleBus;
        var takt = Math.Min(TaktHoechstens, umlauf / busse.Count);
        var startWeg = busse[0].Weg;
        // je Haltestelle der Linie: Weg zu Fuss hin und zurueck (einmal gerechnet)
        var halte = linie.Halte.Select(h => (h.Front, Nr: h.Haltestelle, Punkt: Wartepunkt(haltestellen, h.Haltestelle))).ToList();
        var hin = halte.Select(h => h.Punkt is { } p ? Fussweg(raster, start, p, zelle) : null).ToList();
        var weg = halte.Select(h => h.Punkt is { } p ? Fussweg(raster, p, ziel, zelle) : null).ToList();
        Plan? bester = null;
        for (var a = 0; a < halte.Count; a++)
        {
            if (hin[a] is not { } zuFussHin)
            {
                continue;
            }
            for (var b = 0; b < halte.Count; b++)
            {
                if (a == b || halte[a].Nr == halte[b].Nr || weg[b] is not { } zuFussWeg)
                {
                    continue;
                }
                var schritte = checked(zuFussHin.Schritte + zuFussWeg.Schritte);
                var minutenFuss = checked(schritte * minutenJeSchritt);
                // Abfahrt: der erste Bus steht bei Tagesbeginn auf startWeg; die Front erreicht die Haltestelle nach
                // (Front - Weg) mod Laenge, danach alle takt Minuten ein Bus
                var ankunft = (double)checked(uhr + (zuFussHin.Schritte * minutenJeSchritt) - beginn);
                var erste = Mod(halte[a].Front - startWeg, laenge) / zelle * MinutenJeZelleBus;
                var warten = (long)Math.Ceiling(Mod(erste - ankunft, takt) - 1e-9);
                var strecke = Mod(halte[b].Front - halte[a].Front, laenge);
                var fahrt = (long)Math.Ceiling((strecke / zelle * MinutenJeZelleBus) - 1e-9);
                var minuten = checked(minutenFuss + warten + fahrt);
                if (bester is null || minuten < bester.Minuten)
                {
                    bester = new Plan(halte[a].Nr, halte[b].Nr, zuFussHin.Punkte, FahrtPunkte(busse[0], halte[a].Front, strecke),
                        zuFussWeg.Punkte, schritte, minutenFuss, warten, fahrt);
                }
            }
        }
        return bester;
    }

    /// <summary>Zeitraffer: so schnell faehrt die Figur im Bus, damit die Fahrt hoechstens <see cref="ZeitrafferSekunden"/> dauert.</summary>
    public static double TempoImBus(IReadOnlyList<(double X, double Y)> fahrt, (double X, double Y) von, double tempo)
    {
        var laenge = Wegsuche.Laenge(von, fahrt);
        return Math.Max(tempo, laenge / ZeitrafferSekunden);
    }

    /// <summary>Stelle, an der die Figur auf den Bus wartet (vor dem Haeuschen auf dem Gehweg).</summary>
    public static (double X, double Y)? Wartepunkt(IReadOnlyList<Haltestelle> haltestellen, int nr) =>
        haltestellen.FirstOrDefault(h => h.Nr == nr) is { } h ? (h.Wartend.X, h.Wartend.Y) : null;

    private sealed record Fuss(IReadOnlyList<(double X, double Y)> Punkte, long Schritte);

    private static Fuss? Fussweg(Wegsuche raster, (double X, double Y) von, (double X, double Y) nach, double zelle)
    {
        var punkte = raster.Weg(von, nach);
        if (punkte.Count == 0 && (Math.Abs(von.X - nach.X) > 0.5 || Math.Abs(von.Y - nach.Y) > 0.5))
        {
            return null;
        }
        return new Fuss(punkte, (long)Math.Ceiling((Wegsuche.Laenge(von, punkte) / zelle) - 1e-9));
    }

    /// <summary>Punkte der Fahrt: Lage der Front alle <see cref="PunktAbstand"/> von <paramref name="ab"/> bis ab + strecke.</summary>
    private static List<(double X, double Y)> FahrtPunkte(Verkehr.Auto bus, float ab, float strecke)
    {
        var punkte = new List<(double X, double Y)>();
        for (var s = PunktAbstand; s < strecke; s += PunktAbstand)
        {
            var (ort, _) = bus.Lage(Mod(ab + s, bus.Laenge));
            punkte.Add((ort.X, ort.Y));
        }
        var (ende, _) = bus.Lage(Mod(ab + strecke, bus.Laenge));
        punkte.Add((ende.X, ende.Y));
        return punkte;
    }

    private static float Mod(float a, float m) => ((a % m) + m) % m;

    private static double Mod(double a, double m) => ((a % m) + m) % m;
}
