using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Was das Werkzeug aus dem Spiel braucht, ueber dieselbe Lesefunktion wie das Spiel (eingebettete Dateien der
/// Lernplattform; in Tests die Dateien des Repos): die geltende Fassung von Stadt 1, die Spielinhalte mit ihrer Karte,
/// das Sortiment (geschaefte.json) und die CC0-Modelle. So zeigt das Werkzeug nie andere Zahlen als das Spiel.
/// </summary>
public sealed class Spieldaten
{
    public required StadtFassung Fassung { get; init; }

    public required PyObjekt Inhalt { get; init; }

    public required Geschaefte Geschaefte { get; init; }

    public required GrafikBibliothek? Grafik { get; init; }

    /// <summary>Orte mit bearbeitbarem Innenraum (Geschaefte und Schalter).</summary>
    public IReadOnlyList<string> Orte => InnenGrundriss.Orte(Fassung.Innenraeume);

    public static Spieldaten Laden(Func<string, byte[]?> lesen)
    {
        var fassung = AktiveFassung.Laden(lesen);
        var karte = PythonJson.Utf8(PythonJson.Standard(fassung.Karte));
        var daten = Py.Lesen(lesen("inhalte_csharp/spiel/geschaefte.json") ?? throw new FileNotFoundException("geschaefte.json")) as PyObjekt
            ?? throw new InvalidDataException("geschaefte.json ist kein Objekt");
        var inhalt = Geschaefte.NeueMoebelEinfuegen(SpielInhalt.Laden(name => name == "orte" ? karte : lesen("inhalte/spiel/" + name + ".json"), null), daten);
        GrafikBibliothek? grafik = null;
        if (lesen("inhalte_csharp/grafik/modelle.bin") is { } bin && lesen("inhalte_csharp/grafik/modelle.json") is { } mj && lesen("inhalte_csharp/grafik/zuordnung.json") is { } zj)
        {
            grafik = GrafikBibliothek.Laden(bin, System.Text.Encoding.UTF8.GetString(mj), System.Text.Encoding.UTF8.GetString(zj));
        }
        return new Spieldaten { Fassung = fassung, Inhalt = inhalt, Geschaefte = new Geschaefte(inhalt, daten), Grafik = grafik };
    }

    /// <summary>Name eines Orts fuer die Anzeige (Karte der Fassung, sonst die Kennung).</summary>
    public string OrtName(string ort)
    {
        if (Fassung.Stadtdaten["neue_orte"] is List<object?> neue)
        {
            foreach (var o in neue.OfType<PyObjekt>())
            {
                if (Py.Gleich(o["id"], ort) && o["name"] is string n)
                {
                    return n;
                }
            }
        }
        if (Fassung.Karte["orte"] is List<object?> orte)
        {
            foreach (var o in orte.OfType<PyObjekt>())
            {
                if (Py.Gleich(o["id"], ort) && o["name"] is string n)
                {
                    return n;
                }
            }
        }
        return ort;
    }

    /// <summary>Waren, die sich im Innenraum zuordnen lassen (Kennung wie in innenraeume.json "ware", Name).</summary>
    public List<(string Kennung, string Name)> Waren(string ort)
    {
        var liste = new List<(string, string)>();
        if (!Lernplattform.Core.Spiel.Geschaefte.Laeden.Contains(ort))
        {
            return liste;
        }
        var typen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var w in Geschaefte.Waren(ort))
        {
            if (w.Art == Lernplattform.Core.Spiel.Geschaefte.ArtMoebel)
            {
                liste.Add((w.Id, w.Name));
            }
            else if (w.Art == Lernplattform.Core.Spiel.Geschaefte.ArtTeil && typen.Add("teile:" + w.Typ))
            {
                liste.Add(("teile:" + w.Typ, Lernplattform.Core.Spiel.Geschaefte.TypName(w.Typ, Inhalt)));
            }
            else if (w.Art == Kleidung.Art && typen.Add(Kleidung.Art + ":" + w.Typ))
            {
                liste.Add((Kleidung.Art + ":" + w.Typ, Warennamen.Name(Geschaefte, ort, Kleidung.Art + ":" + w.Typ)));
            }
        }
        return liste;
    }
}
