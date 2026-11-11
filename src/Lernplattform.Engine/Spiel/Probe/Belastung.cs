using System.Diagnostics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle Grafik G1 (Weisung P12-182, Plan P12-109 Abschnitt 4): wie stark das Geraet in der Stadt arbeitet. Einmal je
/// Sekunde ein Messwert (Bilder je Sekunde, Rechenzeit je Bild, Arbeitsspeicher und Prozessor des Programms, GPU-Zeit je
/// Bild aus der Zeitabfrage der Grafik, GPU-Auslastung nur wo das System sie liefert); gezeigt wird das Mittel der letzten
/// <see cref="Sekunden"/> Sekunden. <see cref="Letzte"/> behaelt die Werte des letzten Aufenthalts in der Stadt fuer die
/// Optionen-Karte „Grafik“. Was ein System nicht liefert, bleibt null („nicht verfuegbar“, nie geschaetzt). Kosten: je Bild
/// eine Addition; je Sekunde einmal Prozessorzeit und Arbeitsspeicher des eigenen Prozesses lesen.
/// </summary>
public sealed class Belastung
{
    /// <summary>Gemittelt ueber so viele Sekunden.</summary>
    public const int Sekunden = 5;

    /// <summary>Ein Messwert (gemittelt). Null = vom System nicht geliefert.</summary>
    public sealed record Werte(double BilderJeSekunde, double ZeitJeBildMs, long RamBytes, double? CpuProzent, double? GpuMs, double? GpuProzent);

    /// <summary>Werte des letzten Aufenthalts in der Stadt (null = noch nie gemessen in diesem Programmlauf).</summary>
    public static Werte? Letzte { get; private set; }

    /// <summary>GPU-Auslastung in Prozent, nur wo das System sie liefert (Windows: Leistungszaehler); sonst null.</summary>
    public static Func<double?>? GpuProzentQuelle { get; set; }

    private readonly Queue<Werte> _sekunden = new();
    private double _sekundeBeginn = -1;
    private int _bilder;
    private double _zeitSumme;
    private double _gpuSumme;
    private int _gpuZahl;
    private TimeSpan _cpuBeginn;
    private bool _cpuDa = true;

    /// <summary>Aktueller Mittelwert (null, solange noch keine volle Sekunde gemessen ist).</summary>
    public Werte? Aktuell { get; private set; }

    /// <summary>Ein gezeichnetes Bild mit seiner Rechenzeit; <paramref name="jetzt"/> in Sekunden (fortlaufende Uhr).</summary>
    public void Bild(double jetzt, double zeitMs)
    {
        if (_sekundeBeginn < 0 || jetzt - _sekundeBeginn > 3)
        {
            // erster Wert oder nach einer Pause (Haus, Hintergrund): neu anfangen, alte Sekunden zaehlen nicht mit
            Neu(jetzt);
            return;
        }
        _bilder++;
        _zeitSumme += zeitMs;
        if (jetzt - _sekundeBeginn >= 1)
        {
            Abschliessen(jetzt);
        }
    }

    /// <summary>GPU-Zeit eines Bilds aus der Zeitabfrage (kommt ein oder zwei Bilder spaeter).</summary>
    public void GpuZeit(double ms)
    {
        _gpuSumme += ms;
        _gpuZahl++;
    }

    private void Neu(double jetzt)
    {
        _sekunden.Clear();
        _sekundeBeginn = jetzt;
        _bilder = 0;
        _zeitSumme = 0;
        _gpuSumme = 0;
        _gpuZahl = 0;
        _cpuBeginn = CpuZeit();
    }

    private TimeSpan CpuZeit()
    {
        if (!_cpuDa)
        {
            return TimeSpan.Zero;
        }
        try
        {
            using var p = Process.GetCurrentProcess();
            return p.TotalProcessorTime;
        }
        catch (Exception e) when (e is PlatformNotSupportedException or NotSupportedException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _cpuDa = false;
            return TimeSpan.Zero;
        }
    }

    private void Abschliessen(double jetzt)
    {
        var dauer = jetzt - _sekundeBeginn;
        var cpu = CpuZeit();
        double? cpuProzent = _cpuDa ? Math.Clamp((cpu - _cpuBeginn).TotalSeconds / dauer / Environment.ProcessorCount * 100, 0, 100) : null;
        double? gpuProzent = null;
        try
        {
            gpuProzent = GpuProzentQuelle?.Invoke();
        }
        catch (InvalidOperationException)
        {
            gpuProzent = null;
        }
        var w = new Werte(_bilder / dauer, _bilder > 0 ? _zeitSumme / _bilder : 0, Environment.WorkingSet, cpuProzent,
            _gpuZahl > 0 ? _gpuSumme / _gpuZahl : null, gpuProzent);
        _sekunden.Enqueue(w);
        while (_sekunden.Count > Sekunden)
        {
            _sekunden.Dequeue();
        }
        Aktuell = Mittel(_sekunden);
        Letzte = Aktuell;
        _sekundeBeginn = jetzt;
        _bilder = 0;
        _zeitSumme = 0;
        _gpuSumme = 0;
        _gpuZahl = 0;
        _cpuBeginn = cpu;
    }

    /// <summary>Mittel mehrerer Sekunden; ein Wert ist nur da, wenn ihn jede Sekunde hatte.</summary>
    public static Werte Mittel(IReadOnlyCollection<Werte> liste)
    {
        double? Schnitt(Func<Werte, double?> f) => liste.All(w => f(w) is not null) ? liste.Average(w => f(w)!.Value) : null;
        return new Werte(liste.Average(w => w.BilderJeSekunde), liste.Average(w => w.ZeitJeBildMs), liste.Last().RamBytes,
            Schnitt(w => w.CpuProzent), Schnitt(w => w.GpuMs), Schnitt(w => w.GpuProzent));
    }
}
