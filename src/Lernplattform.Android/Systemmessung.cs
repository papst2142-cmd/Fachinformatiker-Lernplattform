using System.Diagnostics;
using System.Globalization;
using Android.OS;
using Android.Views;

namespace Lernplattform.Android;

/// <summary>
/// Weisung P7-16 (Haenger "ausserhalb"): Werte von Android fuer langsame Bilder in spiel_bilder.log, nur
/// gemessen. Je Bildtakt des Systems (Choreographer) zaehlt sie ausgelassene Takte, alle 50 ms merkt sie
/// sich die Zaehler der Speicherbereinigung von Java (Debug.getRuntimeStat), den Waermezustand und die
/// Beruehrungen. Fuer ein langsames Bild liefert sie die Aenderung seit dem Ende des Bildes davor.
/// Alles laeuft im Hauptfaden (Bildtakt, Beruehrungen und das Spielbild), darum ohne Sperre.
/// </summary>
internal sealed class Systemmessung : Java.Lang.Object, Choreographer.IFrameCallback
{
    private readonly record struct Stand(long Zeit, long GcAnzahl, long GcMs, long BlockAnzahl, long BlockMs, long Ausgelassen, long Beruehrungen);

    private const int Plaetze = 256;
    private static readonly long Abstand = Stopwatch.Frequency / 20;   // 50 ms

    private readonly Stand[] _ring = new Stand[Plaetze];
    private readonly Func<int> _waerme;
    private readonly double _taktNs;
    private int _naechster;
    private int _anzahl;
    private long _letzterTakt;
    private long _ausgelassen;
    private bool _an;

    public Systemmessung(Func<int> waerme, double hertz)
    {
        _waerme = waerme;
        _taktNs = 1e9 / (hertz > 1 ? hertz : 60);
        Hertz = hertz;
    }

    public double Hertz { get; }

    /// <summary>Beruehrungen (Finger aufgesetzt), gezaehlt von der MainActivity.</summary>
    public long Beruehrungen { get; set; }

    public void Setzen(bool an)
    {
        if (an == _an)
        {
            return;
        }
        _an = an;
        _letzterTakt = 0;
        if (an)
        {
            Choreographer.Instance?.PostFrameCallback(this);
        }
        else
        {
            Choreographer.Instance?.RemoveFrameCallback(this);
        }
    }

    public void DoFrame(long frameTimeNanos)
    {
        if (!_an)
        {
            return;
        }
        if (_letzterTakt > 0)
        {
            var abstand = frameTimeNanos - _letzterTakt;
            if (abstand > _taktNs * 1.5)
            {
                _ausgelassen += (long)Math.Round(abstand / _taktNs) - 1;
            }
        }
        _letzterTakt = frameTimeNanos;
        var jetzt = Stopwatch.GetTimestamp();
        if (_anzahl == 0 || jetzt - _ring[(_naechster + Plaetze - 1) % Plaetze].Zeit >= Abstand)
        {
            _ring[_naechster] = Lesen(jetzt);
            _naechster = (_naechster + 1) % Plaetze;
            _anzahl = Math.Min(_anzahl + 1, Plaetze);
        }
        Choreographer.Instance?.PostFrameCallback(this);
    }

    private static long Wert(string name) => long.TryParse(global::Android.OS.Debug.GetRuntimeStat(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var wert) ? wert : 0;

    private Stand Lesen(long zeit) => new(zeit, Wert("art.gc.gc-count"), Wert("art.gc.gc-time"), Wert("art.gc.blocking-gc-count"),
        Wert("art.gc.blocking-gc-time"), _ausgelassen, Beruehrungen);

    private static readonly string[] Waermenamen = ["keine", "leicht", "mittel", "stark", "kritisch", "Notfall", "Abschaltung"];

    /// <summary>Text fuer ein langsames Bild: Aenderungen seit <paramref name="von"/> (Stopwatch-Zeitstempel).</summary>
    public string? Seit(long von)
    {
        if (_anzahl == 0)
        {
            return null;
        }
        // der letzte gemerkte Stand vor dem Zeitraum (sonst der aelteste)
        var vorher = _ring[(_naechster + Plaetze - _anzahl) % Plaetze];
        for (var i = 1; i <= _anzahl; i++)
        {
            var s = _ring[(_naechster + Plaetze - i) % Plaetze];
            if (s.Zeit <= von)
            {
                vorher = s;
                break;
            }
        }
        var jetzt = Lesen(Stopwatch.GetTimestamp());
        var waerme = _waerme();
        var name = waerme >= 0 && waerme < Waermenamen.Length ? Waermenamen[waerme] : waerme.ToString(CultureInfo.InvariantCulture);
        var ab = (jetzt.Zeit - vorher.Zeit) * 1000.0 / Stopwatch.Frequency;
        long gc = jetzt.GcAnzahl - vorher.GcAnzahl, gcMs = jetzt.GcMs - vorher.GcMs;
        long block = jetzt.BlockAnzahl - vorher.BlockAnzahl, blockMs = jetzt.BlockMs - vorher.BlockMs;
        long takte = jetzt.Ausgelassen - vorher.Ausgelassen, finger = jetzt.Beruehrungen - vorher.Beruehrungen;
        return string.Create(CultureInfo.InvariantCulture,
            $"Android (seit {ab:0} ms): GC {gc} in {gcMs} ms, blockierend {block} in {blockMs} ms, ausgelassene Takte {takte}, Berührungen {finger}, Wärme {name}");
    }
}
