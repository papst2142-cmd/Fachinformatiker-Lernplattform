using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle Q1 (S 0.40.0, Weisung P12-186, Kurzplan Meldung P12-111 Abschnitt 4, Nico 4a): Questgeber im Abschnitt
/// <c>stadtdaten.questgeber</c> (Liste).
/// <list type="bullet">
/// <item>Eigener Questgeber: <c>{id "qg-n", name, rolle, aussehen {haut, frisur, haarfarbe, oberteil}, platz}</c>.</item>
/// <item>Bestand-Questgeber (die 17 Auftraggeber: 12 Kollegen, 5 Personen beim Kunden): nur verschiebbar, nie loeschbar
/// (sonst fehlt alten Spielstaenden die Person); ein Eintrag <c>{id, bestand: true, platz}</c> merkt den neuen Platz.</item>
/// </list>
/// Platz drinnen <c>{ort, etage, x, y, blick}</c> (Ort = Innenraum des Werkzeugs, Punkt im Grundriss), draussen
/// <c>{draussen: true, x, y, blick}</c> (Punkt auf der Karte, hoechstens <see cref="DraussenHoechstens"/>). Blick in Grad
/// 0/90/180/270 wie die Ankunft der Etagen (0 = +y).
/// </summary>
public static class QuestgeberDaten
{
    public const string Abschnitt = "questgeber";

    public const string Praefix = "qg-";

    /// <summary>Hoechstens so viele Questgeber draussen (Nico 4a).</summary>
    public const int DraussenHoechstens = 12;

    public const int NameHoechstens = 28;

    /// <summary>Teile des Aussehens, die der Questgeber-Editor waehlt (wie <c>aussehen</c> der Kollegen).</summary>
    public static readonly string[] AussehenTeile = ["haut", "frisur", "haarfarbe", "oberteil"];

    public static readonly long[] Blicke = [0, 90, 180, 270];

    public sealed record Platz(bool Draussen, string Ort, long Etage, double X, double Y, long Blick)
    {
        /// <summary>Schluessel des Innenraums (Ort oder „ort@k“ fuer eine Etage).</summary>
        public string InnenSchluessel => Lernplattform.Core.Stadtbau.Etagen.Schluessel(Ort, (int)Etage);

        public PyObjekt AlsObjekt() => Draussen
            ? new PyObjekt { { "draussen", true }, { "x", Bearbeitung.Zahl(X) }, { "y", Bearbeitung.Zahl(Y) }, { "blick", Blick } }
            : new PyObjekt { { "ort", Ort }, { "etage", Etage }, { "x", Bearbeitung.Zahl(X) }, { "y", Bearbeitung.Zahl(Y) }, { "blick", Blick } };

        public static Platz? Aus(object? wert)
        {
            if (wert is not PyObjekt p || Get(p, "x") is not (long or double) || Get(p, "y") is not (long or double))
            {
                return null;
            }
            var blick = Get(p, "blick") is long b ? b : 0;
            return Py.Wahr(Get(p, "draussen"))
                ? new Platz(true, "", 0, Py.F(p["x"]), Py.F(p["y"]), blick)
                : Get(p, "ort") is string o ? new Platz(false, o, Get(p, "etage") is long e ? e : 0, Py.F(p["x"]), Py.F(p["y"]), blick) : null;
        }
    }

    /// <summary>Ein Questgeber, wie das Werkzeug ihn zeigt. <see cref="Platz"/> null = Bestand an seinem Platz im Spiel.</summary>
    public sealed record Questgeber(string Id, bool Bestand, string Name, string Rolle, PyObjekt Aussehen, Platz? Platz, string? Stelle,
        PyObjekt? Eintrag);

    public static List<object?> Liste(PyObjekt aussen) => Get(Aussen.Stadtdaten(aussen), Abschnitt) as List<object?> ?? [];

    private static List<object?> Anlegen(PyObjekt aussen)
    {
        if (Get(Aussen.Stadtdaten(aussen), Abschnitt) is not List<object?> l)
        {
            l = [];
            Py.Setzen(Aussen.Stadtdaten(aussen), Abschnitt, l);
        }
        return l;
    }

    private static void Aufraeumen(PyObjekt aussen)
    {
        if (Get(Aussen.Stadtdaten(aussen), Abschnitt) is List<object?> { Count: 0 })
        {
            Aussen.TeilSetzen(aussen, "stadtdaten/" + Abschnitt, null, false);
        }
    }

    /// <summary>Die 17 Auftraggeber des Spiels (Kollegen, dann Personen beim Kunden), mit der Stelle im Spiel (buero, Kunden-Ort).</summary>
    public static List<(string Id, string Name, string Rolle, PyObjekt Aussehen, string Stelle)> Bestand(PyObjekt inhalt)
    {
        var auftraggeber = QuestDaten.BestandQuests(inhalt).Select(a => Py.Str(a["auftraggeber"])).ToHashSet(StringComparer.Ordinal);
        var liste = new List<(string, string, string, PyObjekt, string)>();
        foreach (var p in AllePersonen(inhalt))
        {
            var id = Py.Str(p["id"]);
            if (!auftraggeber.Contains(id))
            {
                continue;
            }
            liste.Add((id, Py.Str(Get(p, "name", id)), Py.Str(Get(p, "rolle", "")), Get(p, "aussehen") as PyObjekt ?? new PyObjekt(),
                Py.Hat(p, "ort") ? Py.Str(p["ort"]) : OrtBuero));
        }
        return liste;
    }

    /// <summary>Alle Questgeber: zuerst der Bestand (mit verschobenem Platz), dann die eigenen.</summary>
    public static List<Questgeber> Alle(PyObjekt inhalt, PyObjekt aussen)
    {
        var eintraege = Liste(aussen).OfType<PyObjekt>().Where(e => e["id"] is string).ToList();
        var liste = new List<Questgeber>();
        var bestand = Bestand(inhalt);
        var bestandIds = bestand.Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (id, name, rolle, aussehen, stelle) in bestand)
        {
            var e = eintraege.FirstOrDefault(x => Py.Gleich(x["id"], id));
            liste.Add(new Questgeber(id, true, name, rolle, aussehen, e is null ? null : Platz.Aus(Get(e, "platz")), stelle, e));
        }
        foreach (var e in eintraege.Where(x => !bestandIds.Contains(Py.Str(x["id"]))))
        {
            liste.Add(new Questgeber(Py.Str(e["id"]), false, Py.Str(Get(e, "name", "")), Py.Str(Get(e, "rolle", "")),
                Get(e, "aussehen") as PyObjekt ?? new PyObjekt(), Platz.Aus(Get(e, "platz")), null, e));
        }
        return liste;
    }

    /// <summary>Neuer eigener Questgeber ohne Platz (ein Schritt). Rueckgabe: Kennung.</summary>
    public static string Neu(PyObjekt inhalt, PyObjekt aussen, string name)
    {
        var belegt = Alle(inhalt, aussen).Select(q => q.Id).ToHashSet(StringComparer.Ordinal);
        var n = 1;
        while (belegt.Contains(Praefix + n.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        {
            n++;
        }
        var id = Praefix + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var aussehen = new PyObjekt();
        foreach (var teil in AussehenTeile)
        {
            aussehen.Add(teil, Lernplattform.Core.Spiel.Aussehen.Teile.First(t => t.Teil == teil).Wahl[0].Kennung);
        }
        Anlegen(aussen).Add(new PyObjekt { { "id", id }, { "name", name }, { "rolle", "" }, { "aussehen", aussehen } });
        return id;
    }

    private static PyObjekt? Eigener(PyObjekt aussen, string id) =>
        Liste(aussen).OfType<PyObjekt>().FirstOrDefault(e => Py.Gleich(e["id"], id) && !Py.Wahr(Get(e, "bestand")));

    /// <summary>Name oder Rolle eines eigenen Questgebers setzen. false = Bestand (Name und Rolle bleiben wie im Spiel).</summary>
    public static bool FeldSetzen(PyObjekt aussen, string id, string feld, string wert)
    {
        if (Eigener(aussen, id) is not { } e || feld is not ("name" or "rolle"))
        {
            return false;
        }
        Py.Setzen(e, feld, wert);
        return true;
    }

    /// <summary>Teil des Aussehens eines eigenen Questgebers setzen. false = Bestand.</summary>
    public static bool AussehenSetzen(PyObjekt aussen, string id, string teil, string wert)
    {
        if (Eigener(aussen, id) is not { } e)
        {
            return false;
        }
        if (Get(e, "aussehen") is not PyObjekt a)
        {
            a = new PyObjekt();
            Py.Setzen(e, "aussehen", a);
        }
        Py.Setzen(a, teil, wert);
        return true;
    }

    /// <summary>Platz setzen (eigener und Bestand: verschieben, Quests wandern mit).</summary>
    public static void PlatzSetzen(PyObjekt inhalt, PyObjekt aussen, string id, Platz platz)
    {
        if (Eigener(aussen, id) is { } e)
        {
            Py.Setzen(e, "platz", platz.AlsObjekt());
            return;
        }
        if (!Bestand(inhalt).Any(b => b.Id == id))
        {
            throw new ArgumentException("Questgeber " + id);
        }
        var liste = Anlegen(aussen);
        if (liste.OfType<PyObjekt>().FirstOrDefault(x => Py.Gleich(x["id"], id)) is { } b)
        {
            Py.Setzen(b, "platz", platz.AlsObjekt());
        }
        else
        {
            liste.Add(new PyObjekt { { "id", id }, { "bestand", true }, { "platz", platz.AlsObjekt() } });
        }
    }

    /// <summary>Bestand-Questgeber zurueck an seinen Platz im Spiel (Eintrag weg).</summary>
    public static bool PlatzZurueck(PyObjekt aussen, string id)
    {
        var weg = Liste(aussen).RemoveAll(e => e is PyObjekt o && Py.Gleich(o["id"], id) && Py.Wahr(Get(o, "bestand"))) > 0;
        Aufraeumen(aussen);
        return weg;
    }

    /// <summary>
    /// Eigenen Questgeber loeschen: seine Quests bleiben, stehen danach „ohne Questgeber“ (rot, bis sie neu zugewiesen sind).
    /// false = Bestand (nie loeschbar).
    /// </summary>
    public static bool Loeschen(PyObjekt inhalt, PyObjekt aussen, string id)
    {
        if (Eigener(aussen, id) is null)
        {
            return false;
        }
        Liste(aussen).RemoveAll(e => e is PyObjekt o && Py.Gleich(o["id"], id));
        foreach (var q in QuestDaten.Alle(inhalt, aussen).Where(q => q.Questgeber == id))
        {
            QuestDaten.Umhaengen(inhalt, aussen, q.Id, null);
        }
        Aufraeumen(aussen);
        return true;
    }

    /// <summary>Zahl der Questgeber draussen.</summary>
    public static int Draussen(PyObjekt inhalt, PyObjekt aussen) => Alle(inhalt, aussen).Count(q => q.Platz is { Draussen: true });

    /// <summary>Die Stelle im Spiel (buero, Kunden-Ort, Ort eines Geschaefts) eines Questgebers; null = draussen oder ohne Platz.</summary>
    public static string? Stelle(Questgeber q) => q.Platz is { } p
        ? p.Draussen ? null : Lernplattform.Core.Spiel.Probe.Innenraum.OrtFuer(Lernplattform.Core.Stadtbau.StadtbauPruefung.Ansicht(p.Ort))
        : q.Stelle;
}
