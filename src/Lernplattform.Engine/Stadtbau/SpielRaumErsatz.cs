using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle A 1.10.0 / S 0.64.0 (Weisungen P12-380 bis P12-382, Nico 1a 2c 3c): Raeume des Spiels, die Nico im Werkzeug
/// geaendert hat (Wohnungen, Buero der Bitweiche, eigene Firma je Stufe, Zweigstelle je Stufe, Serverfarm, Kunden). Sie stehen
/// in der Stadt-Datei im Abschnitt <see cref="Abschnitt"/> der Stadtdaten (nur dann Formatnummer 5, sonst bleibt jede Datei
/// bitgleich) als Ersatzstuecke wie die breiten Grundrisse (<see cref="GrundrisseBreit"/>): an der Stelle des Grundrisses in
/// den Spielinhalten bzw. den Innenraeumen steht der neue Grundriss, aber nur, wenn der alte genau die Pruefsumme
/// <see cref="Stueck.Alt"/> hat. Passt ein Stueck nicht, ist das ein klarer Fehler (nie still). Der neue Grundriss enthaelt
/// schon alle angehaengten Zusaetze des Orts (Markierung <see cref="Eingebaut"/>); das Spiel mischt sie dann nicht noch einmal
/// ein (nur die Plaetze der Personen aus <c>zusatz_moebel</c> gelten weiter). Spiel-Kern, Innenraum und Werkzeug lesen
/// denselben eingesetzten Inhalt.
/// </summary>
public static class SpielRaumErsatz
{
    /// <summary>Abschnitt der Stadtdaten (Liste der Ersatzstuecke).</summary>
    public const string Abschnitt = "spielraeume";

    /// <summary>Markierung im Grundriss: die Zusaetze des Orts sind schon eingebaut (nicht noch einmal einmischen).</summary>
    public const string Eingebaut = "zusaetze_eingebaut";

    /// <summary>Felder eines Ersatzstuecks in ihrer festen Reihenfolge.</summary>
    public static readonly string[] Felder = ["raum", "bereich", "pfad", "alt", "neu"];

    /// <summary>
    /// Ein Ersatzstueck: der Raum des Spiels <paramref name="Raum"/> (z. B. „zuhause:apartment“, „buero:bitweiche“,
    /// „buero:firma2“, „filiale1“, „serverfarm“, „kunde:talheim“) mit seinem Grundriss <paramref name="Neu"/>.
    /// </summary>
    public sealed record Stueck(string Raum, string Bereich, IReadOnlyList<object> Pfad, string Alt, PyObjekt Neu);

    /// <summary>Hat die Stadt geaenderte Raeume des Spiels?</summary>
    public static bool Hat(PyObjekt? stadtdaten) =>
        stadtdaten is not null && Py.Hat(stadtdaten, Abschnitt) && stadtdaten[Abschnitt] is List<object?> { Count: > 0 };

    /// <summary>Ort des Spiels zum Raum (fuer die Zusaetze in innenraeume.json); null = unbekannter Raum.</summary>
    public static string? Ort(string raum) =>
        raum.StartsWith("buero:", StringComparison.Ordinal) ? OrtBuero
        : raum.StartsWith("zuhause:", StringComparison.Ordinal) ? OrtZuhause
        : raum.StartsWith(OrtFiliale, StringComparison.Ordinal) ? OrtFiliale
        : raum == Innenraum.Serverfarm ? Innenraum.Serverfarm
        : raum.StartsWith("kunde:", StringComparison.Ordinal) ? raum["kunde:".Length..]
        : null;

    /// <summary>Bereich und Pfad des Grundrisses eines Raums (wie in <see cref="GrundrisseBreit"/>); null = unbekannter Raum.</summary>
    public static (string Bereich, object[] Pfad)? Quelle(string raum)
    {
        static long? Nummer(string text) => long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : null;
        if (raum == "buero:bitweiche")
        {
            return (GrundrisseBreit.Inhalt, ["gebaeude"]);
        }
        if (raum.StartsWith("buero:firma", StringComparison.Ordinal) && Nummer(raum["buero:firma".Length..]) is { } f)
        {
            return (GrundrisseBreit.Inhalt, ["firma", "gebaeude", "stufen", f - 1, "gebaeude"]);
        }
        if (raum.StartsWith(OrtFiliale, StringComparison.Ordinal) && Nummer(raum[OrtFiliale.Length..]) is { } z)
        {
            return (GrundrisseBreit.Inhalt, ["firma", "filiale", "stufen", z - 1, "gebaeude"]);
        }
        if (raum == Innenraum.Serverfarm)
        {
            return (GrundrisseBreit.Innenraeume, [Innenraum.Serverfarm, "gebaeude"]);
        }
        if (raum.StartsWith("zuhause:", StringComparison.Ordinal) && raum.Length > "zuhause:".Length)
        {
            return (GrundrisseBreit.Inhalt, ["wohnungen", "wohnungen", "@id:" + raum["zuhause:".Length..], "gebaeude"]);
        }
        if (raum.StartsWith("kunde:", StringComparison.Ordinal) && raum.Length > "kunde:".Length)
        {
            return (GrundrisseBreit.Inhalt, ["kunden", "orte", "@id:" + raum["kunde:".Length..], "gebaeude"]);
        }
        return null;
    }

    /// <summary>Der heutige Grundriss eines Raums an seiner Quelle (ohne Zusaetze); null = gibt es nicht.</summary>
    public static PyObjekt? Alt(string raum, PyObjekt inhalt, PyObjekt? innenraeume)
    {
        if (Quelle(raum) is not { } q)
        {
            return null;
        }
        object? halter = q.Bereich == GrundrisseBreit.Inhalt ? inhalt : innenraeume;
        foreach (var teil in q.Pfad)
        {
            halter = teil switch
            {
                long n when halter is List<object?> l && n >= 0 && n < l.Count => l[(int)n],
                string s when s.StartsWith("@id:", StringComparison.Ordinal) && halter is List<object?> l
                    => l.FirstOrDefault(x => x is PyObjekt o && Py.Hat(o, "id") && Py.Str(o["id"]) == s[4..]),
                string s when halter is PyObjekt o && Py.Hat(o, s) => o[s],
                _ => null,
            };
            if (halter is null)
            {
                return null;
            }
        }
        return halter as PyObjekt;
    }

    /// <summary>
    /// Der Grundriss eines Raums so, wie das Werkzeug ihn bearbeitet: die Quelle mit allen angehaengten Zusaetzen des Orts
    /// (<c>zusatz_kunden</c>, Einrichtung aus <c>zusatz_moebel</c>, <c>zusatz_schraenke</c>), ohne Zustand eines Spielstands
    /// (keine Moebel der Spielerin/des Spielers, kein Leerstand, auch Einrichtung, die erst nach einer Aufgabe kommt). Eine Kopie;
    /// null = gibt es nicht.
    /// </summary>
    public static PyObjekt? Grundlage(string raum, PyObjekt inhalt, PyObjekt? innenraeume)
    {
        if (Alt(raum, inhalt, innenraeume) is not { } alt || Ort(raum) is not { } ort)
        {
            return null;
        }
        var kopie = (PyObjekt)Py.Kopie(alt)!;
        if (Py.Hat(kopie, Eingebaut))
        {
            kopie.RemoveAll(p => p.Key == Eingebaut);
            return kopie;
        }
        return ort == Innenraum.Serverfarm ? kopie : (PyObjekt)Py.Kopie(Innenraum.MitZusaetzen(ort, kopie, innenraeume))!;
    }

    /// <summary>Ein Ersatzstueck fuer den Raum mit dem bearbeiteten Grundriss <paramref name="neu"/> (Pruefsumme der heutigen Quelle).</summary>
    public static Stueck Bauen(string raum, PyObjekt neu, PyObjekt inhalt, PyObjekt? innenraeume)
    {
        var q = Quelle(raum) ?? throw new ArgumentException("Unbekannter Raum des Spiels: " + raum, nameof(raum));
        var alt = Alt(raum, inhalt, innenraeume) ?? throw new ArgumentException("Den Raum gibt es nicht: " + raum, nameof(raum));
        var mit = (PyObjekt)Py.Kopie(neu)!;
        Py.Setzen(mit, Eingebaut, true);
        return new Stueck(raum, q.Bereich, q.Pfad, GrundrisseBreit.Pruefsumme(alt), mit);
    }

    /// <summary>Das Stueck als Objekt der Datei.</summary>
    public static PyObjekt AlsObjekt(Stueck s) => new()
    {
        { "raum", s.Raum }, { "bereich", s.Bereich }, { "pfad", s.Pfad.Select(p => (object?)p).ToList() }, { "alt", s.Alt }, { "neu", s.Neu },
    };

    /// <summary>Funde der Formatpruefung des Abschnitts (leer = in Ordnung).</summary>
    public static List<string> Format(object? abschnitt)
    {
        var funde = new List<string>();
        if (abschnitt is not List<object?> liste)
        {
            funde.Add($"Abschnitt „{Abschnitt}“ ist keine Liste.");
            return funde;
        }
        var raeume = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in liste)
        {
            if (e is not PyObjekt o || o.Count != Felder.Length || !Felder.All(f => Py.Hat(o, f)))
            {
                funde.Add($"Ein Eintrag in „{Abschnitt}“ hat nicht genau die Felder {string.Join(", ", Felder)}.");
                continue;
            }
            if (o["raum"] is not string raum || Quelle(raum) is not { } q)
            {
                funde.Add($"„{Abschnitt}“: unbekannter Raum des Spiels „{Py.Str(o["raum"])}“.");
                continue;
            }
            if (!raeume.Add(raum))
            {
                funde.Add($"„{Abschnitt}“: der Raum „{raum}“ steht doppelt.");
            }
            if (!Py.Gleich(o["bereich"], q.Bereich) || o["pfad"] is not List<object?> pfad || !PfadGleich(pfad, q.Pfad))
            {
                funde.Add($"„{Abschnitt}“: Bereich oder Pfad von „{raum}“ passen nicht zum Raum.");
            }
            if (o["alt"] is not string a || !a.StartsWith("sha256:", StringComparison.Ordinal) || a.Length != 71)
            {
                funde.Add($"„{Abschnitt}“: die Prüfsumme des alten Grundrisses von „{raum}“ fehlt.");
            }
            if (o["neu"] is not PyObjekt neu || !Py.Hat(neu, Eingebaut) || neu[Eingebaut] is not true
                || !Py.Hat(neu, "breite") || !Py.Hat(neu, "hoehe") || !Py.Hat(neu, "flur") || !Py.Hat(neu, "raeume")
                || neu["breite"] is not (long or double) || neu["hoehe"] is not (long or double) || neu["flur"] is not PyObjekt || neu["raeume"] is not List<object?>)
            {
                funde.Add($"„{Abschnitt}“: der neue Grundriss von „{raum}“ ist unvollständig.");
            }
        }
        return funde;
    }

    private static bool PfadGleich(List<object?> pfad, object[] soll) =>
        pfad.Count == soll.Length && pfad.Zip(soll).All(p => p.First is long a ? p.Second is long b && a == b : Py.Gleich(p.First, p.Second));

    /// <summary>Die Stuecke einer Stadt (leer = keine). Das Format ist vorher geprueft (<see cref="StadtbauPruefung.Format"/>).</summary>
    public static List<Stueck> Lesen(PyObjekt? stadtdaten)
    {
        if (!Hat(stadtdaten))
        {
            return [];
        }
        var funde = Format(stadtdaten![Abschnitt]);
        if (funde.Count > 0)
        {
            throw new StadtbauFehler(funde);
        }
        return [.. ((List<object?>)stadtdaten[Abschnitt]!).Cast<PyObjekt>().Select(o => new Stueck(Py.Str(o["raum"]), Py.Str(o["bereich"]),
            [.. ((List<object?>)o["pfad"]!).Select(p => p is long n ? (object)n : Py.Str(p))], Py.Str(o["alt"]), (PyObjekt)o["neu"]!))];
    }

    private static GrundrisseBreit.Daten Daten(IEnumerable<Stueck> stuecke) =>
        new([.. stuecke.Select(s => new GrundrisseBreit.Ersatz(s.Bereich, s.Pfad, s.Alt, s.Neu))], new Dictionary<string, Flurbreite.Umrechnung>());

    /// <summary>
    /// Raeume, deren Ersatzstueck nicht passt (der Grundriss an der Quelle hat eine andere Pruefsumme oder fehlt); leer = alle
    /// passen. <paramref name="innenraeume"/> null: nur Stuecke im Bereich der Spielinhalte.
    /// </summary>
    public static List<string> Unpassend(IEnumerable<Stueck> stuecke, PyObjekt inhalt, PyObjekt? innenraeume) =>
        [.. stuecke.Where(s => !Passt(s, s.Bereich == GrundrisseBreit.Inhalt ? inhalt : innenraeume)).Select(s => s.Raum)];

    /// <summary>Passt das Stueck (alter Wert gleich) oder steht der neue Grundriss schon dort (zweites Einsetzen aendert nichts)?</summary>
    private static bool Passt(Stueck s, PyObjekt? wurzel)
    {
        if (wurzel is null)
        {
            return true;
        }
        if (GrundrisseBreit.Passend(wurzel, s.Bereich, Daten([s])) == 1)
        {
            return true;
        }
        var raum = s.Bereich == GrundrisseBreit.Inhalt ? Alt(s.Raum, wurzel, null) : Alt(s.Raum, [], wurzel);
        return raum is not null && GrundrisseBreit.Pruefsumme(raum) == GrundrisseBreit.Pruefsumme(s.Neu);
    }

    private static StadtbauFehler Fehler(List<string> raeume) => new([.. raeume.Select(r =>
        $"Raum des Spiels „{r}“: die Änderung aus dem Werkzeug passt nicht mehr zum Stand des Spiels (bitte im Werkzeug neu exportieren).")]);

    /// <summary>Spielinhalte mit den geaenderten Raeumen (Kopie; ohne Stuecke dasselbe Objekt). Passt ein Stueck nicht: Fehler.</summary>
    public static PyObjekt Inhalt(PyObjekt inhalt, IReadOnlyList<Stueck> stuecke)
    {
        if (!stuecke.Any(s => s.Bereich == GrundrisseBreit.Inhalt))
        {
            return inhalt;
        }
        var falsch = Unpassend(stuecke.Where(s => s.Bereich == GrundrisseBreit.Inhalt), inhalt, null);
        return falsch.Count > 0 ? throw Fehler(falsch) : GrundrisseBreit.Einsetzen(inhalt, GrundrisseBreit.Inhalt, Daten(stuecke));
    }

    /// <summary>Innenraeume mit den geaenderten Raeumen (Kopie; ohne Stuecke dasselbe Objekt). Passt ein Stueck nicht: Fehler.</summary>
    public static PyObjekt Innenraeume(PyObjekt innenraeume, IReadOnlyList<Stueck> stuecke)
    {
        var hier = stuecke.Where(s => s.Bereich == GrundrisseBreit.Innenraeume).ToList();
        if (hier.Count == 0)
        {
            return innenraeume;
        }
        var falsch = hier.Where(s => !Passt(s, innenraeume)).Select(s => s.Raum).ToList();
        return falsch.Count > 0 ? throw Fehler(falsch) : GrundrisseBreit.Einsetzen(innenraeume, GrundrisseBreit.Innenraeume, Daten(hier));
    }

    /// <summary>Spielinhalte mit den geaenderten Raeumen der Fassung.</summary>
    public static PyObjekt Inhalt(PyObjekt inhalt, StadtFassung fassung) => Inhalt(inhalt, Lesen(fassung.Stadtdaten));

    /// <summary>Die Fassung mit den geaenderten Raeumen in ihren Innenraeumen (Serverfarm); ohne Stuecke dieselbe Fassung.</summary>
    public static StadtFassung Fassung(StadtFassung fassung)
    {
        var stuecke = Lesen(fassung.Stadtdaten);
        if (!stuecke.Any(s => s.Bereich == GrundrisseBreit.Innenraeume))
        {
            return fassung;
        }
        return new StadtFassung
        {
            StadtId = fassung.StadtId, Nummer = fassung.Nummer, Basis = fassung.Basis, Werkzeug = fassung.Werkzeug,
            Erstellt = fassung.Erstellt, Nachricht = fassung.Nachricht, Karte = fassung.Karte, Stadtdaten = fassung.Stadtdaten,
            Innenraeume = Innenraeume(fassung.Innenraeume, stuecke), Laternen = fassung.Laternen, Beete = fassung.Beete,
        };
    }
}
