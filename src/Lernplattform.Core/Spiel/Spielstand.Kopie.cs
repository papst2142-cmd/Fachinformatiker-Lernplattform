using System.Reflection;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W8 (Sammelliste 86/87): Kopie eines Spielstands wie <c>copy.deepcopy(state,
/// {id(state.content): state.content})</c> und der Vergleich zweier Spielstaende wie
/// <c>GameState.differences</c>/<c>_same</c> (Python 0.63.2, Z. 3611-3637 und 5326-5329).
/// Stoppgrenze wie <c>Spielstand.cs</c>.
/// </summary>
public sealed partial class Spielstand
{
    private static readonly FieldInfo[] AlleFelder = typeof(Spielstand)
        .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    /// <summary>
    /// deepcopy: ein neues Spielstand-Objekt, das mit diesem nichts teilt ausser den Inhalten.
    /// Gemeinsame Objekte innerhalb des Stands bleiben gemeinsam (memo wie Python).
    /// </summary>
    public Spielstand Kopie()
    {
        var neu = (Spielstand)MemberwiseClone();
        var memo = new Dictionary<object, object>(ReferenceEqualityComparer.Instance) { [Inhalt] = Inhalt };
        var alt = VerlaufAlt.Value;
        foreach (var feld in AlleFelder)
        {
            if (feld.FieldType.IsValueType || feld.Name == "<Inhalt>k__BackingField" || (!alt && feld == VerlaufFeld))
            {
                continue;
            }
            feld.SetValue(neu, KopieWert(feld.GetValue(this), memo));
        }
        if (!alt)
        {
            neu.Verlauf = VerlaufKopie(Verlauf, memo);
        }
        return neu;
    }

    private static readonly FieldInfo VerlaufFeld = typeof(Spielstand).GetField(nameof(Verlauf))!;

    private static readonly AsyncLocal<bool> VerlaufAlt = new();

    /// <summary>Nur Gegenprobe (Patch A 1.20.1): jede Zeile des Verlaufs wird wie bis A 1.20.0 tief kopiert.</summary>
    public static bool KopieVerlaufAltFuerTests { get => VerlaufAlt.Value; set => VerlaufAlt.Value = value; }

    /// <summary>
    /// Patch A 1.20.1 (Sammelliste 473, Weisung P12-449): jede Spielaktion (auch jeder Halt beim freien Gehen) kopiert den Stand
    /// vor dem Anwenden. Bis A 1.20.0 wurde dabei jede Zeile des Verlaufs tief kopiert (bei 600 Spielereignissen rund 450 KB je
    /// Aktion, am Tablet ein Haenger von 60 bis 100 ms). Die Zeilen des Verlaufs sind fertig gelesene Ereignisse: kein Teil des
    /// Spiels aendert sie nach dem Anwenden (die Aktionen bauen neue Staende sogar mit denselben Zeilen, <c>SpielAktionen</c>).
    /// Darum teilen sich die Kopie und das Original jetzt jede Zeile, von der kein Teil an einem anderen Feld des Stands haengt
    /// (das Feld haette sie sonst in der Kopie als eigenes Objekt, wie deepcopy mit memo). Hat eine Zeile einen solchen Teil,
    /// wird sie wie bisher tief kopiert. Die Kopie ist danach Feld fuer Feld gleich der tiefen Kopie (Test mit Gegenprobe).
    /// </summary>
    private static List<SpielEreignis> VerlaufKopie(List<SpielEreignis> verlauf, Dictionary<object, object> memo)
    {
        var neu = new List<SpielEreignis>(verlauf.Count);
        memo[verlauf] = neu;
        foreach (var e in verlauf)
        {
            neu.Add(memo.ContainsKey(e) || !NurHier(e.Daten, memo) || !NurHier(e.Zeit, memo) ? (SpielEreignis)KopieWert(e, memo)! : e);
        }
        return neu;
    }

    /// <summary>Haengt kein Teil von <paramref name="wert"/> an einem schon kopierten Feld (memo)?</summary>
    private static bool NurHier(object? wert, Dictionary<object, object> memo)
    {
        if (wert is null or bool or int or long or double or string)
        {
            return true;
        }
        if (memo.ContainsKey(wert))
        {
            return false;
        }
        switch (wert)
        {
            case PyObjekt o:
                foreach (var (_, v) in o)
                {
                    if (!NurHier(v, memo))
                    {
                        return false;
                    }
                }
                return true;
            case PyDict d:
                foreach (var (k, v) in d)
                {
                    if (!NurHier(k, memo) || !NurHier(v, memo))
                    {
                        return false;
                    }
                }
                return true;
            case PySet m:
                foreach (var w in m)
                {
                    if (!NurHier(w, memo))
                    {
                        return false;
                    }
                }
                return true;
            case PyTupel t:
                foreach (var w in t.Werte)
                {
                    if (!NurHier(w, memo))
                    {
                        return false;
                    }
                }
                return true;
            case List<object?> l:
                foreach (var w in l)
                {
                    if (!NurHier(w, memo))
                    {
                        return false;
                    }
                }
                return true;
            default:
                // unbekannte Art: lieber tief kopieren
                return false;
        }
    }

    private static object? KopieWert(object? wert, Dictionary<object, object> memo)
    {
        if (wert is null or bool or int or long or double or string)
        {
            return wert;
        }
        if (memo.TryGetValue(wert, out var schon))
        {
            return schon;
        }
        switch (wert)
        {
            case PyObjekt o:
            {
                var neu = new PyObjekt();
                memo[wert] = neu;
                foreach (var (k, v) in o)
                {
                    neu.Add(k, KopieWert(v, memo));
                }
                return neu;
            }
            case PyDict d:
            {
                var neu = new PyDict();
                memo[wert] = neu;
                foreach (var (k, v) in d)
                {
                    neu[KopieWert(k, memo)] = KopieWert(v, memo);
                }
                return neu;
            }
            case PySet s:
            {
                var neu = new PySet(s.Select(w => KopieWert(w, memo)));
                memo[wert] = neu;
                return neu;
            }
            case PyTupel t:
            {
                var neu = new PyTupel([.. t.Werte.Select(w => KopieWert(w, memo))]);
                memo[wert] = neu;
                return neu;
            }
            case SpielEreignis e:
            {
                var neu = new SpielEreignis(KopieWert(e.Zeit, memo), e.Art, (PyObjekt)KopieWert(e.Daten, memo)!);
                memo[wert] = neu;
                return neu;
            }
            case Dictionary<object, PyObjekt> basis:
            {
                var neu = new Dictionary<object, PyObjekt>(basis.Comparer);
                memo[wert] = neu;
                foreach (var (k, v) in basis)
                {
                    neu[KopieWert(k, memo)!] = (PyObjekt)KopieWert(v, memo)!;
                }
                return neu;
            }
            case List<object?> l:
                return KopieListe(l, memo);
            case List<PyObjekt> l:
                return KopieListe(l, memo);
            case List<PyTupel> l:
                return KopieListe(l, memo);
            case List<SpielEreignis> l:
                return KopieListe(l, memo);
            default:
                throw new NotSupportedException("Kopie fuer " + wert.GetType().Name);
        }
    }

    private static List<T> KopieListe<T>(List<T> liste, Dictionary<object, object> memo)
    {
        var neu = new List<T>(liste.Count);
        memo[liste] = neu;
        foreach (var w in liste)
        {
            neu.Add((T)KopieWert(w, memo)!);
        }
        return neu;
    }

    /// <summary>
    /// differences: Namen der Felder (sortiert), in denen sich zwei Spielstaende unterscheiden.
    /// Nicht verglichen werden die Inhalte und _pressure; bei _topic_base muessen gemeinsame
    /// Eintraege gleich sein (dann steht "_topic_base" am Ende).
    /// </summary>
    public List<string> Unterschiede(Spielstand anderer)
    {
        var meine = Felder();
        var deine = anderer.Felder();
        var namen = new List<string>();
        foreach (var name in meine.Select(p => p.Key).Order(StringComparer.Ordinal))
        {
            if (!Gleich(meine[name], deine[name]))
            {
                namen.Add(name);
            }
        }
        foreach (var (k, v) in themenBasis)
        {
            if (anderer.themenBasis.TryGetValue(k, out var w) && !Gleich(v, w))
            {
                namen.Add("_topic_base");
                break;
            }
        }
        return namen;
    }

    /// <summary>_same: gleich samt Typ und Reihenfolge (1 ist nicht 1.0, ein Tupel keine Liste).</summary>
    public static bool Gleich(object? links, object? rechts)
    {
        var typ = PyTyp(links);
        if (typ != PyTyp(rechts))
        {
            return false;
        }
        switch (typ)
        {
            case "dict":
            {
                var a = Paare(links!);
                var b = Paare(rechts!);
                if (a.Count != b.Count || !a.Zip(b).All(p => PyVergleich.Gleich.Equals(p.First.Key, p.Second.Key)))
                {
                    return false;
                }
                // Wie Python right[key]: der Wert zum gleichen Schluessel
                return a.Zip(b).All(p => Gleich(p.First.Value, p.Second.Value));
            }
            case "list" or "tuple":
            {
                var a = Werte(links!);
                var b = Werte(rechts!);
                return a.Count == b.Count && a.Zip(b).All(p => Gleich(p.First, p.Second));
            }
            case "set":
                return ((PySet)links!).SetEquals((PySet)rechts!);
            default:
                return Py.Gleich(links, rechts);
        }
    }

    private static string PyTyp(object? wert) => wert switch
    {
        null => "NoneType",
        bool => "bool",
        int or long => "int",
        double => "float",
        string => "str",
        PyObjekt or PyDict or Dictionary<object, PyObjekt> => "dict",
        PyTupel => "tuple",
        PySet => "set",
        SpielEreignis => "tuple",
        System.Collections.IEnumerable => "list",
        _ => wert.GetType().FullName!,
    };

    private static List<KeyValuePair<object?, object?>> Paare(object wert) => wert switch
    {
        PyObjekt o => [.. o.Select(p => new KeyValuePair<object?, object?>(p.Key, p.Value))],
        PyDict d => [.. d],
        Dictionary<object, PyObjekt> b => [.. b.Select(p => new KeyValuePair<object?, object?>(p.Key, p.Value))],
        _ => throw new NotSupportedException(wert.GetType().Name),
    };

    private static List<object?> Werte(object wert) => wert switch
    {
        PyTupel t => [.. t.Werte],
        SpielEreignis e => [e.Zeit, e.Art, e.Daten],
        System.Collections.IEnumerable l => [.. l.Cast<object?>()],
        _ => throw new NotSupportedException(wert.GetType().Name),
    };
}
