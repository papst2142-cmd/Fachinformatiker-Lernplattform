using System.Numerics;
using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.47.0 W2 (Weisung P12-226, Liste P12-119 Punkt W2): „Laterne versetzen“ als Knopf an einem roten Fund einer
/// Laterne. Das Werkzeug schlaegt die naechste freie Stelle vor (<see cref="LaternenAutomatik.FreieStellen"/>, Regeln wie die
/// automatischen Laternen, <see cref="Nachtlicht.Regeln"/>) und prueft jeden Kandidaten mit der ganzen Pruefung draussen: ein
/// Vorschlag gilt nur, wenn danach kein roter Fund diese Laterne nennt und kein neuer roter Fund dazukommt
/// (<see cref="Bauen3D.NeuRot"/>). Die Oberflaeche zeigt ihn als Vorschau; ein Tipp uebernimmt ihn als EIN Schritt
/// (<see cref="Anwenden"/>). Gibt es keine freie Stelle, kommt ein Grund statt eines Vorschlags.
/// </summary>
public static partial class LaterneVersetzen
{
    /// <summary>So viele Kandidaten prueft die Suche hoechstens mit der ganzen Pruefung.</summary>
    public const int HoechstensGeprueft = 8;

    /// <summary>Ein Fund ohne Nummer gehoert zu der Laterne, die so nah an seiner Stelle steht.</summary>
    public const double FundNaehe = 0.35;

    /// <summary>Vorschlag: Laterne Nr. (0-basiert), alte und neue Stelle.</summary>
    public sealed record Vorschlag(int Nummer, Vector2 Von, Vector2 Nach);

    [GeneratedRegex(@"^Laterne Nr\. ([0-9]+) ")]
    private static partial Regex NummerMuster();

    /// <summary>Laterne (0-basiert), die der rote Fund <paramref name="f"/> meint; null = kein Fund einer Laterne.</summary>
    public static int? Nummer(PyObjekt aussen, StadtbauFund f)
    {
        if (!f.Rot)
        {
            return null;
        }
        var anzahl = Aussen.Laternen(aussen).Count;
        if (NummerMuster().Match(f.Text) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var n))
        {
            return n >= 1 && n <= anzahl ? n - 1 : null;
        }
        if (!f.Text.Contains("Laterne", StringComparison.Ordinal) || f.X is not { } x || f.Y is not { } y)
        {
            return null;
        }
        var werte = Aussen.LaternenWerte(aussen);
        var beste = Enumerable.Range(0, werte.Count).Select(i => (i, d: Math.Sqrt(Math.Pow(werte[i].X - x, 2) + Math.Pow(werte[i].Z - y, 2))))
            .Where(t => t.d <= FundNaehe).OrderBy(t => t.d).ToList();
        return beste.Count > 0 ? beste[0].i : null;
    }

    /// <summary>Nennt der Fund die Laterne <paramref name="nummer"/> (0-basiert) an der Stelle <paramref name="stelle"/>?</summary>
    private static bool Meint(StadtbauFund f, int nummer, Vector2 stelle) =>
        f.Rot && (f.Text.StartsWith($"Laterne Nr. {nummer + 1} ", StringComparison.Ordinal)
            || (!NummerMuster().IsMatch(f.Text) && f.Text.Contains("Laterne", StringComparison.Ordinal) && f.X is { } x && f.Y is { } y
                && Math.Sqrt(Math.Pow(stelle.X - x, 2) + Math.Pow(stelle.Y - y, 2)) <= FundNaehe));

    /// <summary>
    /// Vorschlag fuer Laterne <paramref name="nummer"/> suchen (blockiert, im Hintergrund rufen; <paramref name="aussen"/> ist
    /// eine Kopie des Stands). <paramref name="szene"/> baut die Stadt (wie <see cref="Projekt.SzeneOhneLaternen(PyObjekt)"/>).
    /// </summary>
    public static (Vorschlag? Vorschlag, string? Grund) Suchen(PyObjekt aussen, StadtFassung basis, PyObjekt innen, Spieldaten daten,
        Func<PyObjekt, StadtSzene?> szene, int nummer, CancellationToken abbruch = default)
    {
        var werte = Aussen.LaternenWerte(aussen);
        if (nummer < 0 || nummer >= werte.Count)
        {
            return (null, Texte.LaterneVersetzenWeg);
        }
        var von = new Vector2(werte[nummer].X, werte[nummer].Z);
        var kandidaten = LaternenAutomatik.FreieStellen(aussen, nummer, szene).Where(q => Vector2.Distance(q, von) >= 0.01f).Take(HoechstensGeprueft).ToList();
        abbruch.ThrowIfCancellationRequested();
        if (kandidaten.Count == 0)
        {
            return (null, Texte.LaterneKeinPlatz);
        }
        // Die Pruefungen sind voneinander unabhaengig (je eine Kopie): der Stand vorher und die Kandidaten laufen in Runden
        // nebeneinander (zwei je Runde: mehr bremsen sich gegenseitig, gemessen; Parallel.For rechnet auch auf dem eigenen Faden, also kein Warten auf freie Faeden
        // des Pools); gewaehlt wird immer der erste passende in der Reihenfolge der Suche
        const int breite = 2;
        var optionen = new ParallelOptions { CancellationToken = abbruch, MaxDegreeOfParallelism = breite };
        List<StadtbauFund>? vorher = null;
        for (var start = 0; start < kandidaten.Count;)
        {
            var mitVorher = vorher is null;
            var runde = kandidaten.Skip(start).Take(breite - (mitVorher ? 1 : 0)).ToList();
            start += runde.Count;
            var ergebnisse = new List<StadtbauFund>[runde.Count + 1];
            Parallel.For(mitVorher ? 0 : 1, runde.Count + 1, optionen, i =>
            {
                if (i == 0)
                {
                    ergebnisse[0] = Pruefen(aussen, basis, innen, daten);
                    return;
                }
                var k = (PyObjekt)Py.Kopie(aussen)!;
                Setzen(k, nummer, runde[i - 1]);
                ergebnisse[i] = Pruefen(k, basis, innen, daten);
            });
            vorher ??= ergebnisse[0];
            for (var i = 0; i < runde.Count; i++)
            {
                if (!ergebnisse[i + 1].Any(f => Meint(f, nummer, runde[i])) && Bauen3D.NeuRot(vorher, ergebnisse[i + 1]) is null)
                {
                    return (new Vorschlag(nummer, von, runde[i]), null);
                }
            }
        }
        return (null, Texte.LaterneKeinPlatz);
    }

    private static List<StadtbauFund> Pruefen(PyObjekt aussen, StadtFassung basis, PyObjekt innen, Spieldaten daten) =>
        Projekt.PruefenAussen(Aussen.Fassung(aussen, basis, innen, 1, 0, Werkzeug.Kennzeile, "", ""), daten, Aussen.Bauten(aussen), wege: []);

    private static void Setzen(PyObjekt aussen, int nummer, Vector2 q)
    {
        var l = SpielDaten.Liste(Aussen.Laternen(aussen)[nummer]);
        l[0] = Aussen.Zahl(q.X);
        l[1] = Aussen.Zahl(q.Y);
    }

    /// <summary>
    /// Vorschlag uebernehmen (in <c>Projekt.AussenAendern</c>, ein Schritt). Steht die Laterne nicht mehr an der alten Stelle
    /// (inzwischen geaendert), passiert nichts: false. Eine automatische Laterne gilt danach als selbst gesetzt (wie Schieben).
    /// </summary>
    public static bool Anwenden(PyObjekt aussen, Vorschlag v)
    {
        var werte = Aussen.LaternenWerte(aussen);
        if (v.Nummer >= werte.Count || Math.Abs(werte[v.Nummer].X - v.Von.X) > 0.001 || Math.Abs(werte[v.Nummer].Z - v.Von.Y) > 0.001)
        {
            return false;
        }
        Setzen(aussen, v.Nummer, v.Nach);
        return true;
    }
}
