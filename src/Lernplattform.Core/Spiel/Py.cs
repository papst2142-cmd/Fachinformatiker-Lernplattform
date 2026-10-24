using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W2: Werte wie Pythons json.load sie liefert, damit der Spiel-Kern die Daten aus
/// <c>inhalte/spiel/</c> genau wie <c>fisi_game.py</c> behandelt. Ein Wert ist null, bool,
/// long (ganze Zahl), double (Zahl mit Punkt oder Exponent), string, <see cref="PyObjekt"/>
/// (dict mit Einfuegereihenfolge) oder <see cref="List{T}"/> von Werten. Geschrieben wird mit
/// <see cref="PythonJson"/> (json.dumps).
/// </summary>
public static class Py
{
    /// <summary>json.loads: doppelte Schluessel behalten ihren ersten Platz und den letzten Wert (wie dict).</summary>
    public static object? Lesen(ReadOnlySpan<byte> utf8)
    {
        var leser = new Utf8JsonReader(utf8, new JsonReaderOptions { MaxDepth = 256 });
        leser.Read();
        return Wert(ref leser);
    }

    public static object? Lesen(string text) => Lesen(Encoding.UTF8.GetBytes(text));

    private static object? Wert(ref Utf8JsonReader leser)
    {
        switch (leser.TokenType)
        {
            case JsonTokenType.StartObject:
                var objekt = new PyObjekt();
                while (leser.Read() && leser.TokenType != JsonTokenType.EndObject)
                {
                    var schluessel = leser.GetString()!;
                    leser.Read();
                    Setzen(objekt, schluessel, Wert(ref leser));
                }
                return objekt;
            case JsonTokenType.StartArray:
                var liste = new List<object?>();
                while (leser.Read() && leser.TokenType != JsonTokenType.EndArray)
                {
                    liste.Add(Wert(ref leser));
                }
                return liste;
            case JsonTokenType.String:
                return leser.GetString();
            case JsonTokenType.Number:
                var roh = Encoding.UTF8.GetString(leser.HasValueSequence ? leser.ValueSequence.ToArray() : leser.ValueSpan);
                // Achtung: ohne (object) machte der Bedingungsausdruck aus jedem long ein double
                return roh.IndexOfAny(['.', 'e', 'E']) < 0
                    ? (object)long.Parse(roh, CultureInfo.InvariantCulture)
                    : double.Parse(roh, NumberStyles.Float, CultureInfo.InvariantCulture);
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Null:
                return null;
            default:
                throw new FormatException("JSON: unerwartetes Zeichen " + leser.TokenType);
        }
    }

    /// <summary>d[key] = value: vorhandener Schluessel behaelt seinen Platz.</summary>
    public static void Setzen(PyObjekt d, string schluessel, object? wert)
    {
        for (var i = 0; i < d.Count; i++)
        {
            if (d[i].Key == schluessel)
            {
                d[i] = new KeyValuePair<string, object?>(schluessel, wert);
                return;
            }
        }
        d.Add(schluessel, wert);
    }

    /// <summary>key in d</summary>
    public static bool Hat(PyObjekt d, string schluessel)
    {
        foreach (var paar in d)
        {
            if (paar.Key == schluessel)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>d.get(key) (null, wenn der Schluessel fehlt).</summary>
    public static object? Get(object? d, string schluessel) => d is PyObjekt o ? o[schluessel] : null;

    /// <summary>copy.deepcopy fuer JSON-Werte.</summary>
    public static object? Kopie(object? wert) => wert switch
    {
        PyObjekt o => KopieDict(o),
        List<object?> l => l.Select(Kopie).ToList(),
        _ => wert,
    };

    private static PyObjekt KopieDict(PyObjekt o)
    {
        var neu = new PyObjekt();
        foreach (var (k, v) in o)
        {
            neu.Add(k, Kopie(v));
        }
        return neu;
    }

    /// <summary>dict(d): flache Kopie.</summary>
    public static PyObjekt Flach(PyObjekt o)
    {
        var neu = new PyObjekt();
        neu.AddRange(o);
        return neu;
    }

    /// <summary>Wahrheitswert wie Python (leere Liste, "", 0, None = falsch).</summary>
    public static bool Wahr(object? wert) => wert switch
    {
        null => false,
        bool b => b,
        long n => n != 0,
        int n => n != 0,
        double d => d != 0,
        string s => s.Length > 0,
        PyObjekt o => o.Count > 0,
        System.Collections.ICollection c => c.Count > 0,
        _ => true,
    };

    /// <summary>str(wert) wie Python fuer JSON-Werte (Zahlen mit repr).</summary>
    public static string Str(object? wert) => wert switch
    {
        null => "None",
        bool b => b ? "True" : "False",
        long n => n.ToString(CultureInfo.InvariantCulture),
        int n => n.ToString(CultureInfo.InvariantCulture),
        double d => PythonJson.Zahl(d),
        string s => s,
        _ => throw new NotSupportedException("str() fuer " + wert.GetType().Name),
    };

    /// <summary>len() fuer str, list, dict.</summary>
    public static int Len(object? wert) => wert switch
    {
        string s => s.Length,
        PyObjekt o => o.Count,
        System.Collections.ICollection c => c.Count,
        _ => throw new NotSupportedException("len() fuer " + (wert?.GetType().Name ?? "None")),
    };

    /// <summary>a == b wie Python fuer JSON-Werte (1 == 1.0 == True, Listen und dicts nach Inhalt).</summary>
    public static bool Gleich(object? a, object? b)
    {
        if (Zahl(a, out var x) && Zahl(b, out var y))
        {
            return x == y;
        }
        return (a, b) switch
        {
            (null, null) => true,
            (string s, string t) => s == t,
            (List<object?> l, List<object?> m) => l.Count == m.Count && l.Zip(m).All(p => Gleich(p.First, p.Second)),
            (PyObjekt o, PyObjekt p) => o.Count == p.Count && o.All(e => Hat(p, e.Key) && Gleich(e.Value, p[e.Key])),
            _ => false,
        };
    }

    /// <summary>wert in behaelter (Liste: Element, dict: Schluessel, str: Teiltext).</summary>
    public static bool Enthaelt(object? behaelter, object? wert) => behaelter switch
    {
        List<object?> l => l.Any(e => Gleich(e, wert)),
        PyObjekt o => wert is string s && Hat(o, s),
        string t => wert is string s ? t.Contains(s, StringComparison.Ordinal)
            : throw new InvalidOperationException("'in <string>' braucht einen Text"),
        _ => throw new NotSupportedException("in fuer " + (behaelter?.GetType().Name ?? "None")),
    };

    /// <summary>a + b fuer Zahlen (int bleibt int, sonst float).</summary>
    public static object Plus(object? a, object? b) => Rechnen(a, b, (x, y) => x + y, (x, y) => x + y);

    /// <summary>a * b fuer Zahlen (int bleibt int, sonst float).</summary>
    public static object Mal(object? a, object? b) => Rechnen(a, b, (x, y) => x * y, (x, y) => x * y);

    private static object Rechnen(object? a, object? b, Func<long, long, long> ganz, Func<double, double, double> komma)
    {
        if (Ganz(a, out var x) && Ganz(b, out var y))
        {
            return checked(ganz(x, y));
        }
        if (Zahl(a, out var p) && Zahl(b, out var q))
        {
            return komma(p, q);
        }
        throw new NotSupportedException("Rechnen mit " + (a?.GetType().Name ?? "None") + " und " + (b?.GetType().Name ?? "None"));
    }

    private static bool Ganz(object? wert, out long n)
    {
        switch (wert)
        {
            case long l:
                n = l;
                return true;
            case int i:
                n = i;
                return true;
            case bool b:
                n = b ? 1 : 0;
                return true;
            default:
                n = 0;
                return false;
        }
    }

    private static bool Zahl(object? wert, out double d)
    {
        if (Ganz(wert, out var n))
        {
            d = n;
            return true;
        }
        if (wert is double x)
        {
            d = x;
            return true;
        }
        d = 0;
        return false;
    }

    /// <summary>Ein System.Text.Json-Knoten (z. B. aus <c>Lerninhalte</c>) als Python-Wert.</summary>
    public static object? AusKnoten(System.Text.Json.Nodes.JsonNode? knoten) =>
        knoten is null ? null : Lesen(knoten.ToJsonString());

    /// <summary>SHA-256 (hex klein) von json.dumps(wert, sort_keys=True, ensure_ascii=False, separators=(",", ":")).</summary>
    public static string Pruefsumme(object? wert) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(PythonJson.Utf8(PythonJson.Text(wert, sortiert: true))));
}
