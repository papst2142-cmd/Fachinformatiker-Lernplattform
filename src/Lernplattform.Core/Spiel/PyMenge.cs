namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: eine Python-Menge (set) von Zellen (spalte, zeile) mit genau der Reihenfolge,
/// in der CPython 3.12 sie durchlaeuft. Python-Code wie <c>fisi_game._nearest_free</c>
/// entscheidet bei gleichem Abstand nach dieser Reihenfolge, darum wird die Hashtabelle von
/// CPython (Objects/setobject.c 3.12: set_add_entry, set_insert_clean, set_table_resize,
/// set_merge, set_difference, set_discard_entry) Schritt fuer Schritt nachgebaut, mit dem
/// Tupel-Hash von Objects/tupleobject.c (xxHash, 64 Bit). Vergleichswerte G48
/// (<c>spiel_wege.json</c>: Reihenfolge aller Raster, hash() einzelner Zellen).
/// </summary>
public sealed class PyMenge : IEnumerable<(long Spalte, long Zeile)>
{
    private const int Mindestgroesse = 8;   // PySet_MINSIZE
    private const int LineareProben = 9;    // LINEAR_PROBES
    private const int Schub = 5;            // PERTURB_SHIFT

    private const ulong Prime1 = 11400714785074694791UL;
    private const ulong Prime2 = 14029467366897019727UL;
    private const ulong Prime5 = 2870177450012600261UL;

    // Platz: 0 leer, 1 belegt, 2 geloescht (dummy)
    private byte[] zustand = new byte[Mindestgroesse];
    private (long, long)[] schluessel = new (long, long)[Mindestgroesse];
    private ulong[] hashwert = new ulong[Mindestgroesse];
    private readonly Dictionary<(long, long), int> platz = [];
    private ulong maske = Mindestgroesse - 1;
    private long gefuellt;   // fill (belegt + geloescht)

    public int Count => platz.Count;

    public bool Contains((long, long) zelle) => platz.ContainsKey(zelle);

    /// <summary>hash() eines Python-Tupels aus zwei ganzen Zahlen.</summary>
    public static long Hash((long, long) zelle)
    {
        // Weisung P8-6 (109): beide Teile nacheinander ohne neues Feld je Aufruf, dieselbe Rechnung
        var acc = Runde(Prime5, zelle.Item1);
        acc = Runde(acc, zelle.Item2);
        acc += 2UL ^ (Prime5 ^ 3527539UL);
        return acc == ulong.MaxValue ? 1546275796 : unchecked((long)acc);
    }

    /// <summary>Eine Runde von xxHash fuer ein Element des Tupels (tuplehash).</summary>
    private static ulong Runde(ulong acc, long teil)
    {
        acc += unchecked((ulong)IntHash(teil) * Prime2);
        acc = (acc << 31) | (acc >> 33);
        return unchecked(acc * Prime1);
    }

    /// <summary>hash(int) fuer |n| &lt; 2**61 - 1 (hash(-1) ist -2).</summary>
    private static long IntHash(long n)
    {
        const long Modul = (1L << 61) - 1;
        if (n >= Modul || n <= -Modul)
        {
            throw new NotSupportedException("hash() fuer so grosse Zahlen nicht nachgebaut");
        }
        return n == -1 ? -2 : n;
    }

    /// <summary>set.add (set_add_entry)</summary>
    public void Add((long, long) zelle)
    {
        if (platz.ContainsKey(zelle))
        {
            return;
        }
        var hash = unchecked((ulong)Hash(zelle));
        var i = hash & maske;
        var schub = hash;
        long freierPlatz = -1;
        while (true)
        {
            var proben = i + LineareProben <= maske ? LineareProben : 0;
            var j = i;
            do
            {
                if (zustand[j] == 0)
                {
                    if (freierPlatz >= 0)
                    {
                        Setzen((int)freierPlatz, zelle, hash);
                        return;
                    }
                    Setzen((int)j, zelle, hash);
                    gefuellt++;
                    if ((ulong)gefuellt * 5 >= maske * 3)
                    {
                        Umbauen(Count > 50000 ? Count * 2L : Count * 4L);
                    }
                    return;
                }
                if (zustand[j] == 2 && freierPlatz < 0)
                {
                    freierPlatz = (long)j;
                }
                j++;
            }
            while (proben-- > 0);
            schub >>= Schub;
            i = (i * 5 + 1 + schub) & maske;
        }
    }

    /// <summary>set.discard (set_discard_entry): der Platz wird ein dummy.</summary>
    public void Discard((long, long) zelle)
    {
        if (platz.Remove(zelle, out var p))
        {
            zustand[p] = 2;
        }
    }

    private void Setzen(int p, (long, long) zelle, ulong hash)
    {
        zustand[p] = 1;
        schluessel[p] = zelle;
        hashwert[p] = hash;
        platz[zelle] = p;
    }

    /// <summary>set_table_resize: kleinste Zweierpotenz &gt; mindestens, Eintraege in Platzfolge neu.</summary>
    private void Umbauen(long mindestens)
    {
        long groesse = Mindestgroesse;
        while (groesse <= mindestens)
        {
            groesse <<= 1;
        }
        var alt = Eintraege().ToList();
        Leeren(groesse);
        foreach (var (zelle, hash) in alt)
        {
            SauberEinfuegen(zelle, hash);
        }
        gefuellt = Count;
    }

    private void Leeren(long groesse)
    {
        zustand = new byte[groesse];
        schluessel = new (long, long)[groesse];
        hashwert = new ulong[groesse];
        platz.Clear();
        maske = (ulong)groesse - 1;
        gefuellt = 0;
    }

    /// <summary>set_insert_clean (Tabelle ohne dummies, Schluessel sicher neu).</summary>
    private void SauberEinfuegen((long, long) zelle, ulong hash)
    {
        var schub = hash;
        var i = hash & maske;
        while (true)
        {
            if (zustand[i] == 0)
            {
                Setzen((int)i, zelle, hash);
                return;
            }
            if (i + LineareProben <= maske)
            {
                for (ulong j = 1; j <= LineareProben; j++)
                {
                    if (zustand[i + j] == 0)
                    {
                        Setzen((int)(i + j), zelle, hash);
                        return;
                    }
                }
            }
            schub >>= Schub;
            i = (i * 5 + 1 + schub) & maske;
        }
    }

    private IEnumerable<((long, long) Zelle, ulong Hash)> Eintraege()
    {
        for (var p = 0; p < zustand.Length; p++)
        {
            if (zustand[p] == 1)
            {
                yield return (schluessel[p], hashwert[p]);
            }
        }
    }

    /// <summary>set(menge) bzw. set.copy (make_new_set + set_merge in eine leere Menge).</summary>
    public PyMenge Kopie()
    {
        var neu = new PyMenge();
        if (Count == 0)
        {
            return neu;
        }
        if ((ulong)(neu.gefuellt + Count) * 5 >= neu.maske * 3)
        {
            neu.Umbauen((neu.Count + Count) * 2L);
        }
        if (neu.maske == maske && gefuellt == Count)
        {
            // gleiche Groesse ohne dummies: Tabelle wird Platz fuer Platz kopiert
            for (var p = 0; p < zustand.Length; p++)
            {
                if (zustand[p] == 1)
                {
                    neu.Setzen(p, schluessel[p], hashwert[p]);
                }
            }
        }
        else
        {
            foreach (var (zelle, hash) in Eintraege())
            {
                neu.SauberEinfuegen(zelle, hash);
            }
        }
        neu.gefuellt = Count;
        return neu;
    }

    /// <summary>menge - andere (set_difference); andere ist eine Menge mit <paramref name="anzahl"/> Eintraegen.</summary>
    public PyMenge Ohne(IReadOnlySet<(long, long)> andere)
    {
        if ((Count >> 2) > andere.Count)
        {
            // set_copy_and_difference: kopieren, dann die anderen entfernen (nur dummies)
            var kopie = Kopie();
            foreach (var zelle in andere)
            {
                kopie.Discard(zelle);
            }
            return kopie;
        }
        var ergebnis = new PyMenge();
        foreach (var zelle in this)
        {
            if (!andere.Contains(zelle))
            {
                ergebnis.Add(zelle);
            }
        }
        return ergebnis;
    }

    /// <summary>Durchlauf wie Python: Plaetze der Tabelle der Reihe nach.</summary>
    public IEnumerator<(long Spalte, long Zeile)> GetEnumerator()
    {
        for (var p = 0; p < zustand.Length; p++)
        {
            if (zustand[p] == 1)
            {
                yield return schluessel[p];
            }
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
