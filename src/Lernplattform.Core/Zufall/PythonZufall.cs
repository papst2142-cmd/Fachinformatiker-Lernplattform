using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Lernplattform.Core.Zufall;

/// <summary>
/// Nachbau von Pythons <c>random.Random</c> (Mersenne Twister MT19937) mit
/// festem Startwert. Gleicher Startwert ergibt exakt dieselben Werte wie in
/// der Python-Linie (Vergleichswerte G2, tests/Vergleich/zufall.json).
/// Nur die Funktionen, die die Python-Linie nutzt: random, getrandbits,
/// randrange, randint, choice, shuffle, sample.
/// </summary>
public sealed class PythonZufall
{
    private const int N = 624;
    private const int M = 397;
    private const uint MatrixA = 0x9908b0dfU;
    private const uint UpperMask = 0x80000000U;
    private const uint LowerMask = 0x7fffffffU;

    private readonly uint[] _mt = new uint[N];
    private int _mti = N + 1;

    /// <summary>Startwert als Text (Python: Text wird mit SHA-512 verlaengert).</summary>
    public PythonZufall(string startwert)
    {
        var text = Encoding.UTF8.GetBytes(startwert);
        var bytes = new byte[text.Length + 64];
        text.CopyTo(bytes, 0);
        SHA512.HashData(text).CopyTo(bytes, text.Length);
        InitAusZahl(new BigInteger(bytes, isUnsigned: true, isBigEndian: true));
    }

    /// <summary>Startwert als ganze Zahl (Python nimmt den Betrag).</summary>
    public PythonZufall(BigInteger startwert)
    {
        InitAusZahl(BigInteger.Abs(startwert));
    }

    public PythonZufall(long startwert) : this(new BigInteger(startwert))
    {
    }

    private void InitAusZahl(BigInteger n)
    {
        // Zahl in 32-Bit-Woerter zerlegen, niederwertigstes zuerst (Python: init_by_array).
        var schluessel = new List<uint>();
        if (n.IsZero)
        {
            schluessel.Add(0);
        }
        while (!n.IsZero)
        {
            schluessel.Add((uint)(n & uint.MaxValue));
            n >>= 32;
        }
        InitNachArray(schluessel);
    }

    private void InitGenrand(uint s)
    {
        _mt[0] = s;
        for (_mti = 1; _mti < N; _mti++)
        {
            _mt[_mti] = 1812433253U * (_mt[_mti - 1] ^ (_mt[_mti - 1] >> 30)) + (uint)_mti;
        }
    }

    private void InitNachArray(List<uint> schluessel)
    {
        InitGenrand(19650218U);
        int i = 1, j = 0;
        int laenge = schluessel.Count;
        for (int k = Math.Max(N, laenge); k > 0; k--)
        {
            _mt[i] = (_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1664525U)) + schluessel[j] + (uint)j;
            i++;
            j++;
            if (i >= N)
            {
                _mt[0] = _mt[N - 1];
                i = 1;
            }
            if (j >= laenge)
            {
                j = 0;
            }
        }
        for (int k = N - 1; k > 0; k--)
        {
            _mt[i] = (_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1566083941U)) - (uint)i;
            i++;
            if (i >= N)
            {
                _mt[0] = _mt[N - 1];
                i = 1;
            }
        }
        _mt[0] = 0x80000000U;
    }

    private uint Genrand()
    {
        uint y;
        if (_mti >= N)
        {
            int kk;
            for (kk = 0; kk < N - M; kk++)
            {
                y = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                _mt[kk] = _mt[kk + M] ^ (y >> 1) ^ ((y & 1U) != 0 ? MatrixA : 0U);
            }
            for (; kk < N - 1; kk++)
            {
                y = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                _mt[kk] = _mt[kk + (M - N)] ^ (y >> 1) ^ ((y & 1U) != 0 ? MatrixA : 0U);
            }
            y = (_mt[N - 1] & UpperMask) | (_mt[0] & LowerMask);
            _mt[N - 1] = _mt[M - 1] ^ (y >> 1) ^ ((y & 1U) != 0 ? MatrixA : 0U);
            _mti = 0;
        }
        y = _mt[_mti++];
        y ^= y >> 11;
        y ^= (y << 7) & 0x9d2c5680U;
        y ^= (y << 15) & 0xefc60000U;
        y ^= y >> 18;
        return y;
    }

    /// <summary>Python: random() - Kommazahl in [0, 1) mit 53 Bit.</summary>
    public double Random()
    {
        uint a = Genrand() >> 5, b = Genrand() >> 6;
        return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
    }

    /// <summary>Python: getrandbits(k).</summary>
    public BigInteger GetRandBits(int k)
    {
        if (k < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k));
        }
        if (k == 0)
        {
            return BigInteger.Zero;
        }
        if (k <= 32)
        {
            return Genrand() >> (32 - k);
        }
        // Mehrere Woerter, niederwertigstes zuerst.
        var ergebnis = BigInteger.Zero;
        int verschiebung = 0;
        while (k > 0)
        {
            uint r = Genrand();
            if (k < 32)
            {
                r >>= 32 - k;
            }
            ergebnis |= new BigInteger(r) << verschiebung;
            verschiebung += 32;
            k -= 32;
        }
        return ergebnis;
    }

    /// <summary>Python: _randbelow_with_getrandbits(n), 0 &lt;= r &lt; n.</summary>
    private long RandBelow(long n)
    {
        if (n <= 0)
        {
            return 0;
        }
        int k = 64 - System.Numerics.BitOperations.LeadingZeroCount((ulong)n);
        var r = (long)GetRandBits(k);
        while (r >= n)
        {
            r = (long)GetRandBits(k);
        }
        return r;
    }

    /// <summary>Python: randrange(stop).</summary>
    public long RandRange(long stop)
    {
        if (stop <= 0)
        {
            throw new ArgumentException("leerer Bereich fuer RandRange");
        }
        return RandBelow(stop);
    }

    /// <summary>Python: randrange(start, stop, step=1).</summary>
    public long RandRange(long start, long stop, long step = 1)
    {
        long breite = stop - start;
        if (step == 1)
        {
            if (breite <= 0)
            {
                throw new ArgumentException("leerer Bereich fuer RandRange");
            }
            return start + RandBelow(breite);
        }
        if (step == 0)
        {
            throw new ArgumentException("Schrittweite 0");
        }
        long anzahl = step > 0 ? (breite + step - 1) / step : (breite + step + 1) / step;
        if (anzahl <= 0)
        {
            throw new ArgumentException("leerer Bereich fuer RandRange");
        }
        return start + step * RandBelow(anzahl);
    }

    /// <summary>Python: randint(a, b), beide Grenzen eingeschlossen.</summary>
    public long RandInt(long a, long b) => RandRange(a, b + 1);

    /// <summary>Python: choice(seq).</summary>
    public T Choice<T>(IReadOnlyList<T> folge)
    {
        if (folge.Count == 0)
        {
            throw new ArgumentException("leere Folge");
        }
        return folge[(int)RandBelow(folge.Count)];
    }

    /// <summary>Python: shuffle(x), mischt die Liste an Ort und Stelle.</summary>
    public void Shuffle<T>(IList<T> liste)
    {
        for (int i = liste.Count - 1; i > 0; i--)
        {
            int j = (int)RandBelow(i + 1);
            (liste[i], liste[j]) = (liste[j], liste[i]);
        }
    }

    /// <summary>Python: sample(population, k), Auswahl ohne Zuruecklegen.</summary>
    public List<T> Sample<T>(IReadOnlyList<T> menge, int k)
    {
        int n = menge.Count;
        if (k < 0 || k > n)
        {
            throw new ArgumentException("Stichprobe groesser als Menge oder negativ");
        }
        var ergebnis = new T[k];
        int setgroesse = 21;
        if (k > 5)
        {
            setgroesse += (int)Math.Pow(4, Math.Ceiling(Math.Log(k * 3) / Math.Log(4)));
        }
        if (n <= setgroesse)
        {
            var pool = menge.ToList();
            for (int i = 0; i < k; i++)
            {
                int j = (int)RandBelow(n - i);
                ergebnis[i] = pool[j];
                pool[j] = pool[n - i - 1];
            }
        }
        else
        {
            var gewaehlt = new HashSet<int>();
            for (int i = 0; i < k; i++)
            {
                int j = (int)RandBelow(n);
                while (gewaehlt.Contains(j))
                {
                    j = (int)RandBelow(n);
                }
                gewaehlt.Add(j);
                ergebnis[i] = menge[j];
            }
        }
        return ergebnis.ToList();
    }
}
