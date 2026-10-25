using System.Globalization;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>Phase 7, W2: ein Python-Tupel (unveraenderlich, gleich nach Inhalt wie Python).</summary>
public sealed class PyTupel(params object?[] werte) : IEquatable<PyTupel>
{
    public object?[] Werte { get; } = werte;

    public object? this[int i] => Werte[i];

    public int Count => Werte.Length;

    public bool Equals(PyTupel? anderes) =>
        anderes is not null && Werte.Length == anderes.Werte.Length && Werte.Zip(anderes.Werte).All(p => PyVergleich.Gleich.Equals(p.First, p.Second));

    public override bool Equals(object? obj) => Equals(obj as PyTupel);

    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (var w in Werte)
        {
            h.Add(PyVergleich.Gleich.GetHashCode(w));
        }
        return h.ToHashCode();
    }
}

/// <summary>Gleichheit wie Python == fuer Schluessel von dict und set (1 == 1.0 == True).</summary>
public sealed class PyVergleich : IEqualityComparer<object?>
{
    public static readonly PyVergleich Gleich = new();

    public new bool Equals(object? a, object? b) => a is PyTupel t ? t.Equals(b as PyTupel) : Py.Gleich(a, b);

    public int GetHashCode(object? wert) => wert switch
    {
        null => 0,
        bool b => (b ? 1L : 0L).GetHashCode(),
        int n => ((long)n).GetHashCode(),
        long n => n.GetHashCode(),
        double d when d == Math.Floor(d) && Math.Abs(d) < 9.2e18 => ((long)d).GetHashCode(),
        double d => d.GetHashCode(),
        string s => StringComparer.Ordinal.GetHashCode(s),
        PyTupel t => t.GetHashCode(),
        _ => throw new NotSupportedException("nicht hashbar: " + wert.GetType().Name),
    };
}

/// <summary>
/// Phase 7, W2: ein Python-dict mit beliebigen hashbaren Schluesseln (auch None, Zahlen,
/// Tupel) und Pythons Reihenfolge: neue Schluessel hinten, Ueberschreiben behaelt den Platz,
/// pop und erneutes Setzen kommt hinten an.
/// </summary>
public sealed class PyDict : IEnumerable<KeyValuePair<object?, object?>>
{
    private static readonly object Keins = new();
    private readonly OrderedDictionary<object, object?> werte = new(new Kasten());

    public int Count => werte.Count;

    public object? this[object? schluessel]
    {
        get => werte[schluessel ?? Keins];
        set => werte[schluessel ?? Keins] = value;
    }

    public bool ContainsKey(object? schluessel) => werte.ContainsKey(schluessel ?? Keins);

    /// <summary>d.get(k, standard)</summary>
    public object? Get(object? schluessel, object? standard = null) =>
        werte.TryGetValue(schluessel ?? Keins, out var w) ? w : standard;

    /// <summary>d.pop(k, None): true, wenn der Schluessel da war.</summary>
    public bool Pop(object? schluessel) => werte.Remove(schluessel ?? Keins);

    /// <summary>d.setdefault(k, standard)</summary>
    public object? SetDefault(object? schluessel, object? standard)
    {
        if (!ContainsKey(schluessel))
        {
            this[schluessel] = standard;
        }
        return this[schluessel];
    }

    /// <summary>d.clear()</summary>
    public void Clear() => werte.Clear();

    public IEnumerable<object?> Keys => werte.Keys.Select(k => ReferenceEquals(k, Keins) ? null : k);

    public IEnumerable<object?> Values => werte.Values;

    public PyDict Kopie()
    {
        var neu = new PyDict();
        foreach (var (k, v) in this)
        {
            neu[k] = v;
        }
        return neu;
    }

    public IEnumerator<KeyValuePair<object?, object?>> GetEnumerator() =>
        werte.Select(p => new KeyValuePair<object?, object?>(ReferenceEquals(p.Key, Keins) ? null : p.Key, p.Value)).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class Kasten : IEqualityComparer<object>
    {
        public new bool Equals(object? a, object? b) =>
            ReferenceEquals(a, b) || (!ReferenceEquals(a, Keins) && !ReferenceEquals(b, Keins) && PyVergleich.Gleich.Equals(a, b));

        public int GetHashCode(object wert) => ReferenceEquals(wert, Keins) ? -1 : PyVergleich.Gleich.GetHashCode(wert);
    }
}

/// <summary>Eine Python-Menge, deren Reihenfolge nie zaehlt (nur Mitgliedschaft und Anzahl).</summary>
public sealed class PySet : HashSet<object?>
{
    public PySet() : base(PyVergleich.Gleich)
    {
    }

    public PySet(IEnumerable<object?> werte) : base(werte, PyVergleich.Gleich)
    {
    }
}

/// <summary>Werte fuer die Vergleichswerte: Python-Werte als JSON-Text wie die Erzeuger sie schreiben.</summary>
public static class PyAusgabe
{
    /// <summary>
    /// Wie <c>wert()</c> in den Erzeugern G44/G45: set = {"menge": nach json.dumps(ensure_ascii=True)
    /// sortiert}, Tupel = Liste, dict mit nur Text-Schluesseln = dict, sonst {"paare": [[k, v], ...]}.
    /// </summary>
    public static object? Wert(object? wert) => wert switch
    {
        null or bool or long or int or double or string => wert,
        PyObjekt o => Objekt(o),
        PyDict d when d.Keys.All(k => k is string) => Objekt(d.Select(p => new KeyValuePair<string, object?>((string)p.Key!, p.Value))),
        PyDict d => new PyObjekt { { "paare", d.Select(p => (object?)new List<object?> { Wert(p.Key), Wert(p.Value) }).ToList() } },
        PySet s => new PyObjekt { { "menge", s.Select(Wert).OrderBy(Ascii, StringComparer.Ordinal).ToList() } },
        PyTupel t => t.Werte.Select(Wert).ToList(),
        System.Collections.IEnumerable l => l.Cast<object?>().Select(Wert).ToList(),
        _ => throw new NotSupportedException("Ausgabe fuer " + wert.GetType().Name),
    };

    private static PyObjekt Objekt(IEnumerable<KeyValuePair<string, object?>> paare)
    {
        var neu = new PyObjekt();
        foreach (var (k, v) in paare)
        {
            neu.Add(k, Wert(v));
        }
        return neu;
    }

    /// <summary>json.dumps(wert) mit ensure_ascii=True (Sortierschluessel).</summary>
    public static string Ascii(object? wert)
    {
        var text = PythonJson.Text(wert);
        var b = new System.Text.StringBuilder();
        foreach (var c in text)
        {
            if (c < 0x80)
            {
                b.Append(c);
            }
            else
            {
                b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            }
        }
        return b.ToString();
    }
}
