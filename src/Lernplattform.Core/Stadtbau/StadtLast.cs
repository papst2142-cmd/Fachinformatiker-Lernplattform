using System.Diagnostics;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Engine festigen Stufe 2 Teil 1 (Weisung P12-176, Kurzplan Meldung P12-106 Punkt 4a): was eine Stadt beim Bauen kostet.
/// Gemessen werden die Zeit fuer <see cref="StadtSzene.Bauen"/> plus <see cref="StadtSzene.Vorbereiten"/> (alle Stuecke und
/// alle Netze) und die Menge der Netzdaten (Ecken und Indizes aller Netze, die das Spiel in die Grafikkarte laedt: Stadt,
/// Haeuser, Kleingruen, Paket-Netz, Fernformen, Saetze des Instancing, Ampel-Lichter). Start-Test des Spiels und des Werkzeugs
/// messen damit die teuerste Lage (Nacht, Winter, Weihnachten, alle Orte sichtbar); damit faellt jede spaetere grosse Stadt
/// sofort auf. Nur Messung, aendert nichts an der Szene.
/// </summary>
public static class StadtLast
{
    /// <summary>
    /// Grenze fuer „Stadt bauen“ in ms (einmaliger Messpunkt, streng). Vorschlag aus den Messwerten von Stadt 1 (Meldung
    /// P12-107, voller Lauf 819: Spiel 130 ms, Werkzeug bis 809 ms unter Windows, waehrend im Hintergrund Pruefung und Grenzen
    /// laufen) mit deutlichem Abstand; Grenzwerte entscheidet Nico.
    /// </summary>
    public const double BauenGrenzeMs = 2000;

    /// <summary>Grenze fuer „Netzdaten“ in MB (Vorschlag aus Stadt 1: rund 16 MB, dreifacher Abstand; Grenzwerte = Nico).</summary>
    public const double NetzdatenGrenzeMb = 48;

    /// <summary>Ergebnis einer Messung: Zeit fuer Bauen und Vorbereiten, Netzdaten in Byte, Zahl der Netze.</summary>
    public sealed record Ergebnis(double BauenMs, double VorbereitenMs, long Bytes, int Netze)
    {
        /// <summary>Zeit fuer „Stadt bauen“ (Bauen plus Vorbereiten).</summary>
        public double GesamtMs => BauenMs + VorbereitenMs;

        /// <summary>Netzdaten in MB (1 MB = 1 048 576 Byte).</summary>
        public double Mb => Bytes / 1024.0 / 1024.0;
    }

    /// <summary>Baut die Szene ueber <paramref name="bauen"/> neu (blockiert), bereitet die Netze vor und misst beides.</summary>
    public static Ergebnis Messen(Func<StadtSzene> bauen)
    {
        var uhr = Stopwatch.StartNew();
        var szene = bauen();
        var bauenMs = uhr.Elapsed.TotalMilliseconds;
        uhr.Restart();
        var fertig = szene.Vorbereiten();
        var vorbereitenMs = uhr.Elapsed.TotalMilliseconds;
        var netze = Netze(fertig).ToList();
        return new Ergebnis(bauenMs, vorbereitenMs, netze.Sum(Bytes), netze.Count);
    }

    /// <summary>Byte eines Netzes: je Ecke <see cref="Netz.Zahlen"/> float, je Index ein uint (je 4 Byte).</summary>
    public static long Bytes(Netz netz) => (long)netz.Ecken.Count * 4 + (long)netz.Indizes.Count * 4;

    /// <summary>Alle Netze der fertigen Szene, die das Spiel hochlaedt (jedes genau einmal).</summary>
    public static IEnumerable<Netz> Netze(StadtSzene.Fertig f)
    {
        var alle = new List<Netz?> { f.Stadt, f.StadtFern, f.Gruen, f.PaketNah, f.PaketFern };
        alle.AddRange(f.Haeuser.Values);
        alle.AddRange(f.HaeuserFern?.Values ?? Enumerable.Empty<Netz>());
        foreach (var satz in f.Instanzen?.Saetze ?? [])
        {
            alle.Add(satz.Nah);
            alle.Add(satz.Fern);
        }
        alle.AddRange(f.AmpelLichter ?? []);
        return alle.OfType<Netz>().Distinct(ReferenceEqualityComparer.Instance).Cast<Netz>();
    }

    /// <summary>Zeile fuer die Ausgabe des Start-Tests (Angabe aller Teilwerte).</summary>
    public static string Text(Ergebnis e) => string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"{e.GesamtMs:F0} ms (Bauen {e.BauenMs:F0} ms, Vorbereiten {e.VorbereitenMs:F0} ms), Netzdaten {e.Mb:F1} MB in {e.Netze} Netzen");
}
