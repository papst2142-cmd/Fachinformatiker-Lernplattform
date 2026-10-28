using Lernplattform.Core.Spiel.Ton;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Lernplattform.Spiel;

/// <summary>
/// Phase 8 W8 (E2 P8-16 Punkt 1): Ausgabe ueber SoundFlow 1.4.1 (miniaudio) auf Windows, Linux, macOS und
/// Android; eine einzige Komponente holt die Proben aus dem <see cref="Mischer"/>. Anhalten schliesst das
/// Geraet ganz (Hintergrund am Handy, wie die Ton-Probe aus P8-16), Starten oeffnet es neu.
/// </summary>
public sealed class SoundFlowAusgabe : ITonausgabe
{
    private MiniAudioEngine? _engine;
    private AudioPlaybackDevice? _geraet;

    public string Name => "SoundFlow/miniaudio (" + (_geraet?.Info?.Name ?? "Standardgerät") + ")";

    public void Starten(Mischer mischer)
    {
        if (_geraet is null)
        {
            var format = new AudioFormat { SampleRate = Mischer.Rate, Channels = 2, Format = SampleFormat.F32 };
            _engine = new MiniAudioEngine();
            _geraet = _engine.InitializePlaybackDevice(null, format);
            _geraet.MasterMixer.AddComponent(new Komponente(_engine, format, mischer));
            var engine = _engine;
            // MP3 und FLAC (eigene Lieder) liest miniaudio
            Tondekoder.Weitere = (stream, _) => new Strom(engine.CreateDecoder(stream, out var gelesen, null), gelesen, stream);
        }
        _geraet.Start();
    }

    public void Anhalten() => Schliessen();

    public void Dispose() => Schliessen();

    private void Schliessen()
    {
        try
        {
            _geraet?.Stop();
            _geraet?.Dispose();
            _engine?.Dispose();
        }
        catch (Exception)
        {
            // beim Schliessen nur verwerfen; das naechste Starten oeffnet neu
        }
        _geraet = null;
        _engine = null;
    }

    private sealed class Komponente(AudioEngine engine, AudioFormat format, Mischer mischer) : SoundComponent(engine, format)
    {
        public override string Name { get; set; } = "Lernplattform";

        protected override void GenerateAudio(Span<float> buffer, int channels) => mischer.Fuellen(buffer, channels);
    }

    /// <summary>Strom aus einem miniaudio-Decoder (eigene Lieder als MP3 oder FLAC).</summary>
    private sealed class Strom(ISoundDecoder decoder, AudioFormat format, Stream stream) : ITonStrom
    {
        public int Rate => format.SampleRate;

        public int Kanaele => Math.Max(1, format.Channels);

        public int Lesen(float[] puffer, int anzahl) => decoder.Decode(puffer.AsSpan(0, anzahl - (anzahl % Kanaele)));

        public void Dispose()
        {
            decoder.Dispose();
            stream.Dispose();
        }
    }
}
