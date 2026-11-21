using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 1.0.0 (Sammelliste 357, Werkstatt Teil 2, Nico F3 a): was die Werkbank-Ansicht und die Regal-Ansicht der Aufgabe
/// „PC zusammenbauen“ anzeigen. Nur Anzeige: die Ansicht fuellt dieselbe Antwort wie die Liste, die Bewertung
/// (<see cref="Bewertung"/>) bleibt unveraendert, nichts wird in den Spielstand geschrieben.
/// </summary>
public static class Werkbank
{
    /// <summary>
    /// RAM-Baenke des eingesetzten Mainboards: Zahl der Baenke, belegte Baenke (Module des eingesetzten RAM), freie Baenke und
    /// ob mehr Module als Baenke eingesetzt sind. Ohne Mainboard null (es gibt keine Baenke). Lehre aus dem Entwurf P12-194:
    /// „RAM frei: 3“ bei leerer Bank war falsch; frei = Baenke minus Module, ohne RAM also alle Baenke.
    /// </summary>
    public static (long Baenke, long Belegt, long Frei, bool ZuViele)? Ram(PyObjekt? mainboard, PyObjekt? ram)
    {
        if (mainboard is null)
        {
            return null;
        }
        var baenke = Math.Max(0, Py.Int(Get(mainboard, "ram_slots", 0L)));
        var module = ram is null ? 0 : Math.Max(0, Py.Int(Get(ram, "module", 1L)));
        return (baenke, Math.Min(module, baenke), Math.Max(0, checked(baenke - module)), module > baenke);
    }

    /// <summary>Text unter den RAM-Baenken.</summary>
    public static string RamText(PyObjekt? mainboard, PyObjekt? ram)
    {
        if (Ram(mainboard, ram) is not { } r)
        {
            return "RAM-Bänke: erst ein Mainboard einsetzen";
        }
        return r.ZuViele
            ? PyFormat.Prozent("RAM passt nicht: %d Module, nur %d Bänke", Py.Int(Get(ram!, "module", 1L)), r.Baenke)
            : PyFormat.Prozent("RAM frei: %d von %d", r.Frei, r.Baenke);
    }

    /// <summary>Steckplatz, in den ein Teil gehoert (sein Typ), wenn die Aufgabe ihn hat, sonst null.</summary>
    public static string? PlatzFuer(object? kennung, IEnumerable<string> plaetze, PyObjekt inhalt)
    {
        var typ = Py.Str(Teil(kennung, inhalt)?["typ"]);
        return plaetze.FirstOrDefault(p => p == typ);
    }

    /// <summary>Steckplaetze, die noch leer sind und nicht optional (in der Reihenfolge der Aufgabe).</summary>
    public static List<string> Fehlt(IEnumerable<string> plaetze, IReadOnlyDictionary<string, object?> antwort, IEnumerable<object?> optional) =>
        [.. plaetze.Where(p => !antwort.ContainsKey(p) && !optional.Any(o => Py.Gleich(o, p)))];

    /// <summary>Ein Fach des Regals: Typ, Name des Typs und die Teile darin (Kennung, Name, Anzahl).</summary>
    public sealed record Fach(string Typ, string Name, List<(object? Kennung, string Name, long Anzahl)> Teile);

    /// <summary>
    /// Inhalt des Regals aus dem Lager des Spielstands (<c>Spielstand.Lager</c>): je Typ ein Fach in der Reihenfolge der Typen
    /// der Hardware (danach Ersatzteil-Typen, dann unbekannte), darin die Teile in der Reihenfolge des Lagers. Unbekannte
    /// Kennungen bleiben weg.
    /// </summary>
    public static List<Fach> Regal(PyDict lager, PyObjekt inhalt)
    {
        var hardware = Objekt(inhalt["hardware"]);
        var typen = Get(hardware, "typen") as PyObjekt ?? [];
        var ersatz = Get(hardware, "ersatz_typen") as PyObjekt ?? [];
        var faecher = new List<Fach>();
        foreach (var (kennung, anzahl) in lager)
        {
            if (Teil(kennung, inhalt) is not { } teil)
            {
                continue;
            }
            var typ = Py.Str(teil["typ"]);
            if (faecher.FirstOrDefault(f => f.Typ == typ) is not { } fach)
            {
                var name = Py.Str(Get(typen, typ, Get(ersatz, typ, typ)));
                fach = new Fach(typ, name, []);
                faecher.Add(fach);
            }
            fach.Teile.Add((kennung, Py.Str(teil["name"]), Py.Int(anzahl)));
        }
        var reihe = typen.Select(p => p.Key).Concat(ersatz.Select(p => p.Key)).ToList();
        return [.. faecher.OrderBy(f => reihe.IndexOf(f.Typ) is var i && i < 0 ? int.MaxValue : i)];
    }
}
