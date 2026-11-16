using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.55.0 (Weisung P12-282 Punkt 2): Stadtteil-Namen bearbeiten. Nico zieht in der Draufsicht ein Rechteck und gibt
/// ihm einen Namen (nur Anzeige im Spiel: Karte im Handy, Schild beim Betreten; keine Spielregel, kein Fingerabdruck).
/// Liste <c>stadtdaten.stadtteile</c> (<see cref="Stadtteile.Bereich"/>). Jede Aenderung laesst nur einen Stand zu, den die
/// Regeln <see cref="Stadtteile.Fehler"/> gruen finden; sonst kommt der Grund zurueck und nichts aendert sich (nie still).
/// Ohne Namen fehlt der Abschnitt ganz (die Datei bleibt bitgleich wie vorher, Formatnummer alt).
/// </summary>
public static class StadtteilNamen
{
    /// <summary>Die gueltigen Bereiche draussen in ihrer Reihenfolge (wie das Spiel sie zeigt).</summary>
    public static IReadOnlyList<Stadtteile.Bereich> Alle(PyObjekt aussen) => Stadtteile.Bereiche(Aussen.Stadtdaten(aussen));

    /// <summary>
    /// Jeder Eintrag der Liste an seiner Stelle (Nummer = Stelle, auch fuer die Aenderungen); ein unlesbarer Eintrag ist null
    /// (die Liste im Werkzeug zeigt ihn mit <see cref="Texte.StadtteilKaputt"/>, Loeschen geht).
    /// </summary>
    public static IReadOnlyList<Stadtteile.Bereich?> Eintraege(PyObjekt aussen) =>
        [.. Liste(aussen).Select(e => Stadtteile.Bereiche(new PyObjekt { { Stadtteile.AbschnittNamen, new List<object?> { e } } }).FirstOrDefault())];

    /// <summary>Ecke auf das Raster 0,5 runden.</summary>
    public static double Rund(double v) => Math.Round(v / Stadtteile.BereichRaster, MidpointRounding.AwayFromZero) * Stadtteile.BereichRaster;

    /// <summary>Ein gezogenes Rechteck (zwei beliebige Ecken) auf Raster und Karte gebracht.</summary>
    public static (double X1, double Z1, double X2, double Z2) Rechteck(double ax, double az, double bx, double bz, double breite, double tiefe)
    {
        double K(double v, double max) => Math.Clamp(Rund(v), 0, max);
        var (x1, x2) = (K(Math.Min(ax, bx), breite), K(Math.Max(ax, bx), breite));
        var (z1, z2) = (K(Math.Min(az, bz), tiefe), K(Math.Max(az, bz), tiefe));
        return (x1, z1, x2, z2);
    }

    /// <summary>Ein Vorschlag fuer einen neuen Namen („Stadtteil 1“, „Stadtteil 2“ …, der erste freie).</summary>
    public static string Vorschlag(PyObjekt aussen)
    {
        var namen = Alle(aussen).Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; ; i++)
        {
            var n = Texte.StadtteilVorschlag(i);
            if (!namen.Contains(n))
            {
                return n;
            }
        }
    }

    /// <summary>
    /// Prueft den Stand mit <paramref name="neu"/> an der Stelle <paramref name="nummer"/> (-1 = angehaengt, null = geloescht);
    /// Rueckgabe: der erste Fund der Regeln oder null (gruen).
    /// </summary>
    public static string? Pruefen(PyObjekt aussen, int nummer, Stadtteile.Bereich? neu)
    {
        var liste = Liste(aussen).Select(Py.Kopie).ToList();
        if (nummer < 0 && neu is not null)
        {
            liste.Add(neu.AlsDaten());
        }
        else if (nummer >= 0 && nummer < liste.Count)
        {
            if (neu is null)
            {
                liste.RemoveAt(nummer);
            }
            else
            {
                liste[nummer] = neu.AlsDaten();
            }
        }
        var daten = new PyObjekt { { Stadtteile.AbschnittNamen, liste } };
        var (b, h) = Aussen.Groesse(aussen);
        return Stadtteile.Fehler(daten, b, h).Select(f => f.Text).FirstOrDefault();
    }

    /// <summary>Neuer Stadtteil (angehaengt). Rueckgabe: Grund, warum es nicht geht, sonst null.</summary>
    public static string? Neu(PyObjekt aussen, Stadtteile.Bereich b) => Setzen(aussen, -1, b);

    /// <summary>Stadtteil Nr. <paramref name="nummer"/> (ab 0) umbenennen.</summary>
    public static string? Umbenennen(PyObjekt aussen, int nummer, string name) =>
        Eintraege(aussen).ElementAtOrDefault(nummer) is { } b ? Setzen(aussen, nummer, b with { Name = name }) : Texte.StadtteilFehlt;

    /// <summary>Stadtteil Nr. <paramref name="nummer"/> an eine neue Stelle oder auf eine neue Groesse setzen.</summary>
    public static string? Bereich(PyObjekt aussen, int nummer, double x1, double z1, double x2, double z2) =>
        Eintraege(aussen).ElementAtOrDefault(nummer) is { } b ? Setzen(aussen, nummer, b with { X1 = x1, Z1 = z1, X2 = x2, Z2 = z2 }) : Texte.StadtteilFehlt;

    /// <summary>Stadtteil Nr. <paramref name="nummer"/> loeschen; der letzte nimmt den Abschnitt mit (bitgleich wie ohne Namen).</summary>
    public static string? Loeschen(PyObjekt aussen, int nummer)
    {
        var liste = Liste(aussen);
        if (nummer < 0 || nummer >= liste.Count)
        {
            return Texte.StadtteilFehlt;
        }
        liste.RemoveAt(nummer);
        if (liste.Count == 0)
        {
            Aussen.Stadtdaten(aussen).RemoveAll(p => p.Key == Stadtteile.AbschnittNamen);
        }
        return null;
    }

    private static string? Setzen(PyObjekt aussen, int nummer, Stadtteile.Bereich b)
    {
        var bereich = b with { X1 = Rund(b.X1), Z1 = Rund(b.Z1), X2 = Rund(b.X2), Z2 = Rund(b.Z2) };
        if (Pruefen(aussen, nummer, bereich) is { } grund)
        {
            return grund;
        }
        var sd = Aussen.Stadtdaten(aussen);
        if (sd[Stadtteile.AbschnittNamen] is not List<object?> liste)
        {
            liste = [];
            Py.Setzen(sd, Stadtteile.AbschnittNamen, liste);
        }
        if (nummer < 0)
        {
            liste.Add(bereich.AlsDaten());
        }
        else
        {
            liste[nummer] = bereich.AlsDaten();
        }
        return null;
    }

    private static List<object?> Liste(PyObjekt aussen) => Aussen.Stadtdaten(aussen)[Stadtteile.AbschnittNamen] as List<object?> ?? [];
}
