using System.Globalization;
using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Phase 13 Welle 1 (Weisung P12-16): die gemeinsame Pruefung einer Stadt-Fassung fuer Spiel, CI, Einbau und spaeter das
/// Werkzeug (Plan Abschnitt 4). Jeder Fund ist ein Satz in einfachen Worten; ein Fund sperrt den Einbau und den Export.
/// Welle 1: <see cref="Format"/> (Aufbau, Formatnummer, Felder, Pruefsumme) und <see cref="Inhalt"/> (die Stadt laesst
/// sich laden, jeder Ort hat einen Zugang, Laternen und Beete liegen auf der Karte, jeder Ort mit Grundriss hat ihn).
/// Die Regeln der Darstellung (Tueren auf der Wand, Erreichbarkeit, Abstaende) pruefen bis Welle 2 die Tests des Spiels;
/// sie ziehen mit dem Werkzeug hierher um.
/// </summary>
public static partial class StadtbauPruefung
{
    [GeneratedRegex("^[a-z][a-z0-9_]{0,31}$")]
    private static partial Regex StadtIdMuster();

    [GeneratedRegex("^sha256:[0-9a-f]{64}$")]
    private static partial Regex PruefsummeMuster();

    /// <summary>Aufbau der Datei (ohne Spielinhalte): leer = in Ordnung.</summary>
    public static List<string> Format(object? roh)
    {
        var funde = new List<string>();
        if (roh is not PyObjekt d)
        {
            funde.Add("Die Datei enthält kein Objekt.");
            return funde;
        }
        foreach (var (k, _) in d)
        {
            if (!StadtFassung.Felder.Contains(k))
            {
                funde.Add($"Unbekanntes Feld „{k}“.");
            }
        }
        foreach (var k in StadtFassung.Felder)
        {
            if (!d.Any(p => p.Key == k))
            {
                funde.Add($"Feld „{k}“ fehlt.");
            }
        }
        if (funde.Count > 0)
        {
            return funde;
        }
        if (d["format"] is not string f || f != StadtFassung.Format)
        {
            funde.Add("Das ist keine Stadt-Datei (Feld „format“ ist nicht „fistadt“).");
        }
        if (!Py.IstNurInt(d["formatversion"]))
        {
            funde.Add("Feld „formatversion“ ist keine ganze Zahl.");
        }
        else if (Py.Int(d["formatversion"]) > StadtFassung.FormatVersion)
        {
            funde.Add(string.Create(CultureInfo.InvariantCulture, $"Die Datei stammt aus einer neueren Fassung des Formats ({Py.Int(d["formatversion"])}); dieses Programm kennt nur {StadtFassung.FormatVersion}."));
        }
        else if (Py.Int(d["formatversion"]) < 1)
        {
            funde.Add("Feld „formatversion“ ist kleiner als 1.");
        }
        if (d["stadt"] is not PyObjekt stadt)
        {
            funde.Add("Feld „stadt“ ist kein Objekt.");
        }
        else
        {
            foreach (var (k, _) in stadt)
            {
                if (!StadtFassung.StadtFelder.Contains(k))
                {
                    funde.Add($"Unbekanntes Feld „stadt.{k}“.");
                }
            }
            if (stadt["id"] is not string id || !StadtIdMuster().IsMatch(id))
            {
                funde.Add("„stadt.id“ fehlt oder ist nicht aus Kleinbuchstaben, Ziffern und _ (höchstens 32 Zeichen).");
            }
            if (!Py.IstNurInt(stadt["fassung"]) || Py.Int(stadt["fassung"]) < 0)
            {
                funde.Add("„stadt.fassung“ ist keine ganze Zahl ab 0.");
            }
            if (!stadt.Any(p => p.Key == "basis") || (stadt["basis"] is not null && (!Py.IstNurInt(stadt["basis"]) || Py.Int(stadt["basis"]) < 0)))
            {
                funde.Add("„stadt.basis“ fehlt oder ist weder leer noch eine ganze Zahl ab 0.");
            }
        }
        foreach (var k in new[] { "werkzeug", "erstellt", "nachricht" })
        {
            if (d[k] is not string)
            {
                funde.Add($"Feld „{k}“ ist kein Text.");
            }
        }
        if (d["nachricht"] is string n && n.Length > 2000)
        {
            funde.Add("Die Nachricht ist länger als 2000 Zeichen.");
        }
        foreach (var k in new[] { "karte", "stadtdaten", "innenraeume" })
        {
            if (d[k] is not PyObjekt)
            {
                funde.Add($"Feld „{k}“ ist kein Objekt.");
            }
        }
        if (d["karte"] is PyObjekt karte)
        {
            if (karte["welt"] is not PyObjekt welt || !IstZahl(welt["breite"]) || !IstZahl(welt["hoehe"]) || Py.F(welt["breite"]) <= 0 || Py.F(welt["hoehe"]) <= 0)
            {
                funde.Add("„karte.welt“ braucht Breite und Höhe größer als 0.");
            }
            if (karte["orte"] is not List<object?>)
            {
                funde.Add("„karte.orte“ ist keine Liste.");
            }
        }
        Punkte(d["laternen"], "laternen", 3, funde);
        Punkte(d["beete"], "beete", 2, funde);
        if (d["pruefsumme"] is not string summe || !PruefsummeMuster().IsMatch(summe))
        {
            funde.Add("Feld „pruefsumme“ fehlt oder hat nicht die Form sha256:<64 Zeichen>.");
        }
        else if (funde.Count == 0)
        {
            var ohne = new PyObjekt();
            foreach (var (k, v) in d)
            {
                if (k != "pruefsumme")
                {
                    ohne.Add(k, v);
                }
            }
            if (StadtFassung.Pruefsumme(ohne) != summe)
            {
                funde.Add("Die Prüfsumme stimmt nicht: Die Datei wurde nach dem Speichern verändert oder ist beschädigt.");
            }
        }
        return funde;
    }

    private static bool IstZahl(object? w) => w is long or int or double;

    private static void Punkte(object? wert, string name, int laenge, List<string> funde)
    {
        if (wert is not List<object?> liste)
        {
            funde.Add($"Feld „{name}“ ist keine Liste.");
            return;
        }
        for (var i = 0; i < liste.Count; i++)
        {
            var ok = liste[i] is List<object?> p && p.Count == laenge && IstZahl(p[0]) && IstZahl(p[1]) && (laenge == 2 || p[2] is bool);
            if (!ok)
            {
                funde.Add(string.Create(CultureInfo.InvariantCulture, $"„{name}“ Nr. {i + 1} hat nicht die Form [x, z{(laenge == 3 ? ", doppelt" : "")}]."));
            }
        }
    }

    /// <summary>
    /// Inhalt einer gelesenen Fassung zusammen mit den Spielinhalten (<paramref name="inhalt"/>, Abschnitt "orte" wird durch
    /// die Karte der Fassung ersetzt): leer = in Ordnung.
    /// </summary>
    public static List<string> Inhalt(StadtFassung fassung, PyObjekt inhalt)
    {
        var funde = new List<string>();
        Stadt stadt;
        try
        {
            stadt = fassung.StadtFuer(fassung.MitKarte(inhalt));
        }
        catch (Exception fehler) when (fehler is InvalidDataException or IOException or KeyNotFoundException or InvalidCastException or FormatException or OverflowException or SpielFehler)
        {
            funde.Add("Die Stadt lässt sich nicht laden (" + fehler.GetType().Name + ").");
            return funde;
        }
        var welt = SpielDaten.Objekt(fassung.Karte["welt"]);
        var (breite, hoehe) = (Py.F(welt["breite"]), Py.F(welt["hoehe"]));
        foreach (var regel in stadt.Regeln)
        {
            var id = Py.Str(regel["id"]);
            if (!stadt.Zugaenge.Any(p => p.Key == id))
            {
                funde.Add($"Ort „{id}“ hat keinen Zugang (Tür und Fußweg).");
            }
        }
        var nr = 0;
        foreach (var (x, z, _) in fassung.Laternen)
        {
            nr++;
            if (x < 0 || z < 0 || x > breite || z > hoehe)
            {
                funde.Add(string.Create(CultureInfo.InvariantCulture, $"Laterne Nr. {nr} liegt außerhalb der Karte ({x:0.##}; {z:0.##})."));
            }
        }
        nr = 0;
        foreach (var b in fassung.Beete)
        {
            nr++;
            if (b.X < 0 || b.Y < 0 || b.X > breite || b.Y > hoehe)
            {
                funde.Add(string.Create(CultureInfo.InvariantCulture, $"Beet Nr. {nr} liegt außerhalb der Karte ({b.X:0.##}; {b.Y:0.##})."));
            }
        }
        foreach (var (ort, gebaeude) in fassung.Innenraeume)
        {
            if (ort.StartsWith('_') || ort.StartsWith("zusatz", StringComparison.Ordinal))
            {
                continue;
            }
            if (gebaeude is not PyObjekt g || g["gebaeude"] is not PyObjekt)
            {
                funde.Add($"Innenraum „{ort}“ hat keinen Grundriss.");
            }
        }
        return funde;
    }
}
