using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.45.0 (Weisung P12-212 Punkt 5, Nico a): Laufzeiten zwischen den Orten der Stadt, genau wie das Spiel sie rechnet
/// (<see cref="Stadt.Weg"/>: Breitensuche von Tuer zu Tuer auf dem Stadt-Raster, je Schritt <see cref="Stadt.MinutenJeSchritt"/>
/// Spielminuten, heute 2). Die Spielregel bleibt; das Werkzeug zeigt die Wege und warnt gelb ab <see cref="GelbAb"/> Minuten.
/// Nur Anzeige und gelber Hinweis, nie rot.
/// </summary>
public static class Laufzeiten
{
    /// <summary>Ab so vielen Spielminuten ist ein Weg gelb.</summary>
    public const long GelbAb = 60;

    /// <summary>Ein Weg zwischen zwei Orten (Kennungen, Namen, Minuten zu Fuss; null = kein Weg).</summary>
    public sealed record Weg(string Von, string Nach, string VonName, string NachName, long? Minuten);

    /// <summary>Alle Wege zwischen je zwei Orten (jedes Paar einmal), laengste zuerst, ohne Weg ganz vorn. Leer bei weniger als zwei Orten.</summary>
    public static List<Weg> Rechnen(StadtFassung stand, PyObjekt inhalt)
    {
        Stadt stadt;
        try
        {
            if (SpielKern.Regeln.Stadt(stand, stand.MitKarte(inhalt), nachsichtig: true) is not Stadt s)
            {
                return [];
            }
            stadt = s;
        }
        catch (Exception fehler) when (fehler is InvalidDataException or IOException or KeyNotFoundException or InvalidCastException or FormatException or OverflowException or SpielFehler)
        {
            // laedt die Stadt nicht, meldet das schon die Pruefung
            return [];
        }
        var orte = stadt.Regeln.Select(r => (Id: Py.Str(r["id"]), Name: Py.Str(r["name"]))).ToList();
        var wege = new List<Weg>();
        for (var i = 0; i < orte.Count; i++)
        {
            for (var j = i + 1; j < orte.Count; j++)
            {
                var w = stadt.Weg(orte[i].Id, orte[j].Id);
                wege.Add(new Weg(orte[i].Id, orte[j].Id, orte[i].Name, orte[j].Name, w is null ? null : checked(w.Schritte * stadt.MinutenJeSchritt)));
            }
        }
        return [.. wege.OrderByDescending(w => w.Minuten ?? long.MaxValue).ThenBy(w => w.VonName, StringComparer.Ordinal).ThenBy(w => w.NachName, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Gelber Hinweis: EINER fuer alle Wege ab <see cref="GelbAb"/> Minuten (Zahl und laengster Weg, an der Tuer seines ersten
    /// Orts); die einzelnen Wege zeigt die Karte „Laufzeiten zu Fuß“. Stadt 1 hat Wege bis ueber 250 Minuten, ein Hinweis je Weg
    /// waere eine lange gelbe Liste (E1).
    /// </summary>
    public static List<StadtbauFund> Pruefen(StadtFassung stand, PyObjekt inhalt, PyObjekt aussen)
    {
        var lang = Rechnen(stand, inhalt).Where(w => w.Minuten >= GelbAb).ToList();
        if (lang.Count == 0)
        {
            return [];
        }
        var w = lang[0];
        var (x, y) = TuerVon(aussen, w.Von);
        return [new StadtbauFund(Texte.LaufzeitLang(lang.Count, w.VonName, w.NachName, w.Minuten!.Value), Texte.LaufzeitLangTipp, false, x, y)];
    }

    private static (double?, double?) TuerVon(PyObjekt aussen, string ort)
    {
        var sd = Aussen.Stadtdaten(aussen);
        foreach (var abschnitt in new[] { "zugaenge_neu", "zugaenge" })
        {
            if (SpielDaten.Get(SpielDaten.Get(sd, abschnitt) as PyObjekt ?? [], ort) is PyObjekt z && z["tuer"] is List<object?> t)
            {
                return (Py.F(t[0]), Py.F(t[1]));
            }
        }
        return (null, null);
    }
}
