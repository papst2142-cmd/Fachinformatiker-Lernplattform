using System.Buffers.Binary;
using NVorbis;

namespace Lernplattform.Core.Spiel.Ton;

/// <summary>
/// Phase 8 W8: Toene lesen. OGG Vorbis mit NVorbis (MIT, nur verwaltet), WAV (PCM 8/16/24/32 Bit oder
/// Gleitkomma) selbst; MP3 und FLAC liest die Ausgabe (miniaudio in SoundFlow) ueber <see cref="Weitere"/>.
/// Kaputte oder unbekannte Dateien werfen eine Ausnahme (der Aufrufer ueberspringt sie).
/// </summary>
public static class Tondekoder
{
    /// <summary>Endungen, die "Lied hinzufügen" annimmt (Weisung P8-17).</summary>
    public static readonly IReadOnlyList<string> Endungen = [".mp3", ".wav", ".flac", ".ogg"];

    /// <summary>Weitere Formate (MP3, FLAC), von der Oberflaeche gesetzt; null = nur OGG und WAV.</summary>
    public static Func<Stream, string, ITonStrom>? Weitere { get; set; }

    /// <summary>Ganzen Ton mono lesen (kurze Geraeusche und Schleifen des Spiels).</summary>
    public static TonDaten Lesen(Stream stream, string endung)
    {
        using var strom = Strom(stream, endung, weitere: false);
        var kanaele = Math.Max(1, strom.Kanaele);
        var puffer = new float[8192 * kanaele];
        var mono = new List<float>(1 << 16);
        int n;
        while ((n = strom.Lesen(puffer, puffer.Length)) > 0)
        {
            for (var i = 0; i + kanaele <= n; i += kanaele)
            {
                var summe = 0f;
                for (var k = 0; k < kanaele; k++)
                {
                    summe += puffer[i + k];
                }
                mono.Add(summe / kanaele);
            }
        }
        if (mono.Count == 0)
        {
            throw new InvalidDataException("Ton ohne Proben");
        }
        return new TonDaten([.. mono], strom.Rate);
    }

    /// <summary>Strom oeffnen (eigene Lieder); der Strom besitzt <paramref name="stream"/>.</summary>
    public static ITonStrom Strom(Stream stream, string endung, bool weitere = true)
    {
        switch (endung.ToLowerInvariant())
        {
            case ".ogg":
                return new VorbisStrom(stream);
            case ".wav":
                return new WavStrom(stream);
            default:
                if (weitere && Weitere is { } fremd)
                {
                    return fremd(stream, endung);
                }
                stream.Dispose();
                throw new NotSupportedException("Tonformat " + endung);
        }
    }

    private sealed class VorbisStrom : ITonStrom
    {
        private readonly VorbisReader _leser;

        public VorbisStrom(Stream stream)
        {
            try
            {
                _leser = new VorbisReader(stream, closeOnDispose: true);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
            if (_leser.Channels < 1 || _leser.SampleRate < 8000)
            {
                _leser.Dispose();
                throw new InvalidDataException("OGG ohne gueltiges Format");
            }
        }

        public int Rate => _leser.SampleRate;

        public int Kanaele => _leser.Channels;

        public int Lesen(float[] puffer, int anzahl) => _leser.ReadSamples(puffer, 0, anzahl - (anzahl % Kanaele));

        public void Dispose() => _leser.Dispose();
    }

    /// <summary>WAV: RIFF mit fmt und data (auch WAVE_FORMAT_EXTENSIBLE).</summary>
    private sealed class WavStrom : ITonStrom
    {
        private readonly Stream _stream;
        private readonly int _bits;
        private readonly bool _gleitkomma;
        private long _rest;
        private byte[] _roh = [];

        public WavStrom(Stream stream)
        {
            _stream = stream;
            try
            {
                Span<byte> kopf = stackalloc byte[12];
                stream.ReadExactly(kopf);
                if (!kopf[..4].SequenceEqual("RIFF"u8) || !kopf[8..12].SequenceEqual("WAVE"u8))
                {
                    throw new InvalidDataException("keine WAV-Datei");
                }
                var formatGelesen = false;
                Span<byte> teil = stackalloc byte[8];
                while (true)
                {
                    stream.ReadExactly(teil);
                    var laenge = BinaryPrimitives.ReadUInt32LittleEndian(teil[4..]);
                    if (teil[..4].SequenceEqual("fmt "u8))
                    {
                        var fmt = new byte[laenge];
                        stream.ReadExactly(fmt);
                        int art = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
                        Kanaele = BinaryPrimitives.ReadUInt16LittleEndian(fmt.AsSpan(2));
                        Rate = (int)BinaryPrimitives.ReadUInt32LittleEndian(fmt.AsSpan(4));
                        _bits = BinaryPrimitives.ReadUInt16LittleEndian(fmt.AsSpan(14));
                        if (art == 0xFFFE && fmt.Length >= 26)
                        {
                            art = BinaryPrimitives.ReadUInt16LittleEndian(fmt.AsSpan(24));
                        }
                        _gleitkomma = art == 3;
                        if ((art != 1 && art != 3) || Kanaele < 1 || Rate < 8000 || (_bits is not (8 or 16 or 24 or 32)) || (_gleitkomma && _bits != 32))
                        {
                            throw new InvalidDataException("WAV-Format nicht unterstuetzt");
                        }
                        formatGelesen = true;
                        if ((laenge & 1) == 1)
                        {
                            stream.ReadByte();
                        }
                    }
                    else if (teil[..4].SequenceEqual("data"u8))
                    {
                        if (!formatGelesen)
                        {
                            throw new InvalidDataException("WAV ohne fmt");
                        }
                        _rest = laenge;
                        break;
                    }
                    else
                    {
                        // anderen Abschnitt ueberspringen (Laenge gerade aufgerundet)
                        var weiter = laenge + (laenge & 1);
                        var leer = new byte[Math.Min(weiter, 65536)];
                        while (weiter > 0)
                        {
                            var n = (int)Math.Min(weiter, leer.Length);
                            stream.ReadExactly(leer, 0, n);
                            weiter -= (uint)n;
                        }
                    }
                }
            }
            catch (EndOfStreamException)
            {
                stream.Dispose();
                throw new InvalidDataException("WAV zu kurz");
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        public int Rate { get; }

        public int Kanaele { get; }

        public int Lesen(float[] puffer, int anzahl)
        {
            var breite = _bits / 8;
            anzahl -= anzahl % Kanaele;
            var bytes = (int)Math.Min(_rest, (long)anzahl * breite);
            bytes -= bytes % (breite * Kanaele);
            if (bytes <= 0)
            {
                return 0;
            }
            if (_roh.Length < bytes)
            {
                _roh = new byte[bytes];
            }
            var gelesen = 0;
            while (gelesen < bytes)
            {
                var n = _stream.Read(_roh, gelesen, bytes - gelesen);
                if (n <= 0)
                {
                    break;
                }
                gelesen += n;
            }
            gelesen -= gelesen % (breite * Kanaele);
            _rest -= gelesen;
            var proben = gelesen / breite;
            for (var i = 0; i < proben; i++)
            {
                var o = i * breite;
                puffer[i] = _bits switch
                {
                    8 => (_roh[o] - 128) / 128f,
                    16 => BinaryPrimitives.ReadInt16LittleEndian(_roh.AsSpan(o)) / 32768f,
                    24 => ((_roh[o] | (_roh[o + 1] << 8) | ((sbyte)_roh[o + 2] << 16)) / 8388608f),
                    _ => _gleitkomma
                        ? BinaryPrimitives.ReadSingleLittleEndian(_roh.AsSpan(o))
                        : BinaryPrimitives.ReadInt32LittleEndian(_roh.AsSpan(o)) / 2147483648f,
                };
            }
            if (gelesen == 0)
            {
                _rest = 0;
            }
            return proben;
        }

        public void Dispose() => _stream.Dispose();
    }

    /// <summary>Mono-WAV mit 16 Bit schreiben (Start-Test: Sinus als "eigenes Lied", kein fremder Ton).</summary>
    public static void WavSchreiben(Stream ziel, float[] proben, int rate)
    {
        var daten = proben.Length * 2;
        Span<byte> kopf = stackalloc byte[44];
        "RIFF"u8.CopyTo(kopf);
        BinaryPrimitives.WriteUInt32LittleEndian(kopf[4..], (uint)(36 + daten));
        "WAVEfmt "u8.CopyTo(kopf[8..]);
        BinaryPrimitives.WriteUInt32LittleEndian(kopf[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(kopf[20..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(kopf[22..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(kopf[24..], (uint)rate);
        BinaryPrimitives.WriteUInt32LittleEndian(kopf[28..], (uint)(rate * 2));
        BinaryPrimitives.WriteUInt16LittleEndian(kopf[32..], 2);
        BinaryPrimitives.WriteUInt16LittleEndian(kopf[34..], 16);
        "data"u8.CopyTo(kopf[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(kopf[40..], (uint)daten);
        ziel.Write(kopf);
        var roh = new byte[daten];
        for (var i = 0; i < proben.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(roh.AsSpan(i * 2), (short)Math.Clamp(MathF.Round(proben[i] * 32767f), -32768f, 32767f));
        }
        ziel.Write(roh);
    }

    /// <summary>Sinus (Start-Test und Tests), leise, mit kurzer Ein- und Ausblendung.</summary>
    public static float[] Sinus(double hertz, double sekunden, int rate, float laut = 0.3f)
    {
        var n = (int)(sekunden * rate);
        var blende = Math.Max(1, rate / 50);
        var proben = new float[n];
        for (var i = 0; i < n; i++)
        {
            var huelle = Math.Min(1f, Math.Min(i, n - 1 - i) / (float)blende);
            proben[i] = laut * huelle * MathF.Sin((float)(2 * Math.PI * hertz * i / rate));
        }
        return proben;
    }
}
