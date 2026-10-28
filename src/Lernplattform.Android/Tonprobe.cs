#if PROBE
using System.Diagnostics;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;

namespace Lernplattform.Android;

/// <summary>
/// Phase 8 W8 Vorarbeit (Weisung P8-16 a): spielt mit SoundFlow (miniaudio) einen selbst erzeugten
/// Ton (kein Fremdton): zwei Sinustöne 440 Hz und 660 Hz je 0,4 s mit weichem Ein- und Ausklang,
/// dazwischen 0,2 s Stille, als Schleife. Nur in der Probe-APK; Fehler (kein Gerät) werden als Zeile
/// gezeigt, nie geworfen.
/// </summary>
public sealed class Tonprobe : Lernplattform.Spiel.ITonProbe
{
    private const int Rate = 44100;
    private MiniAudioEngine? _engine;
    private AudioPlaybackDevice? _geraet;
    private SoundPlayer? _spieler;

    public static float[] Erzeugen()
    {
        var ton = (int)(Rate * 0.4);
        var pause = (int)(Rate * 0.2);
        var daten = new float[2 * (ton + pause)];
        var rand = (int)(Rate * 0.02);
        for (var t = 0; t < 2; t++)
        {
            var hz = t == 0 ? 440f : 660f;
            var start = t * (ton + pause);
            for (var i = 0; i < ton; i++)
            {
                var huelle = Math.Min(1f, Math.Min(i, ton - 1 - i) / (float)rand);
                daten[start + i] = 0.35f * huelle * MathF.Sin(2 * MathF.PI * hz * i / Rate);
            }
        }
        return daten;
    }

    public string Starten(float lautstaerke)
    {
        var uhr = Stopwatch.StartNew();
        try
        {
            if (_spieler is null)
            {
                var format = new AudioFormat { SampleRate = Rate, Channels = 1, Format = SampleFormat.F32 };
                _engine = new MiniAudioEngine();
                _geraet = _engine.InitializePlaybackDevice(null, format);
                _spieler = new SoundPlayer(_engine, format, new RawDataProvider(Erzeugen(), Rate)) { IsLooping = true };
                _geraet.MasterMixer.AddComponent(_spieler);
                _geraet.Start();
            }
            _spieler.Volume = lautstaerke;
            _spieler.Play();
            return $"Ton läuft (Gerät: {_geraet?.Info?.Name ?? "Standard"}, geöffnet in {uhr.ElapsedMilliseconds} ms).";
        }
        catch (Exception e)
        {
            Aufraeumen();
            return $"Kein Ton: {e.GetType().Name}: {e.Message} (nach {uhr.ElapsedMilliseconds} ms).";
        }
    }

    public void Anhalten()
    {
        try
        {
            _spieler?.Stop();
        }
        catch (Exception)
        {
            Aufraeumen();
        }
    }

    public void Lautstaerke(float wert)
    {
        if (_spieler is { } spieler)
        {
            spieler.Volume = wert;
        }
    }

    /// <summary>Beim Wechsel in den Hintergrund: Gerät schließen (beim nächsten Start neu öffnen).</summary>
    public void Aufraeumen()
    {
        try
        {
            _spieler?.Stop();
            _geraet?.Stop();
            _geraet?.Dispose();
            _engine?.Dispose();
        }
        catch (Exception)
        {
            // Probe: Fehler beim Schliessen nur verwerfen, die Anzeige meldet den naechsten Start
        }
        _spieler = null;
        _geraet = null;
        _engine = null;
    }
}
#endif
