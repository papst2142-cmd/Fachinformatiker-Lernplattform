using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle Q1 (S 0.40.0, Weisung P12-186, Kurzplan Meldung P12-111 Abschnitte 2–3, Nico 1a 2a 3a): Quests der Stadt im
/// Abschnitt <c>stadtdaten.quests</c> (Liste). Zwei Arten von Eintraegen:
/// <list type="bullet">
/// <item>Neue Quest: alle Felder wie eine Aufgabe in <c>inhalte/spiel/aufgaben.json</c> (Kennung <c>neu-n</c>, Typ nur
/// <c>auswahl</c> oder <c>zuordnung</c>, 2a) und das Feld <c>questgeber</c>. Das Spiel haengt sie erst mit Q2 hinten an die
/// Aufgabenliste an (3a).</item>
/// <item>Umhaengen einer vorhandenen Quest (1a): nur <c>{id, questgeber}</c>; Texte, Belohnung und „erst nach“ der alten
/// Quest bleiben (alte Spielstaende und Sollwerte bleiben sicher).</item>
/// </list>
/// Keine neue Formatnummer: Fassung 0 hat den Abschnitt nicht und bleibt bitgleich; das Spiel liest ihn noch nicht.
/// </summary>
public static class QuestDaten
{
    public const string Abschnitt = "quests";

    /// <summary>Praefix der Kennung neuer Quests (neu-1, neu-2, ...).</summary>
    public const string NeuPraefix = "neu-";

    /// <summary>Typen neuer Quests in Q1 (Nico 2a).</summary>
    public static readonly string[] Typen = ["auswahl", "zuordnung"];

    /// <summary>Lernbezug (Feld <c>cat</c>) wie die vorhandenen Aufgaben.</summary>
    public static readonly string[] Themen = ["systeme", "netzwerk", "wirtschaft", "sicherheit", "datenbanken"];

    public const long GeldHoechstens = 200;
    public const long RufHoechstens = 10;
    public const long AbTagHoechstens = 365;
    public const int TextHoechstens = 600;
    public const int KetteHoechstens = 10;
    public const int OptionenHoechstens = 6;
    public const int PaareHoechstens = 8;

    /// <summary>Eine Quest, wie das Werkzeug sie zeigt: vorhanden (Bestand) oder neu, mit dem geltenden Questgeber.</summary>
    public sealed record Quest(string Id, bool Bestand, string Typ, string Titel, string? Questgeber, long AbTag, string? Nach,
        PyObjekt Daten, bool Umgehaengt);

    /// <summary>Liste im Abschnitt (leer, wenn es keine gibt).</summary>
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

    /// <summary>Leere Liste: Abschnitt weg (wie die anderen Abschnitte, Fassung ohne Quests bleibt wie vorher).</summary>
    private static void Aufraeumen(PyObjekt aussen)
    {
        if (Get(Aussen.Stadtdaten(aussen), Abschnitt) is List<object?> { Count: 0 })
        {
            Aussen.TeilSetzen(aussen, "stadtdaten/" + Abschnitt, null, false);
        }
    }

    /// <summary>Die vorhandenen Quests der Spielgeschichte (aufgaben.json ohne wiederkehrende Auftraege).</summary>
    public static IEnumerable<PyObjekt> BestandQuests(PyObjekt inhalt) =>
        SpielDaten.Liste(inhalt["aufgaben"]).Select(Objekt).Where(a => !Py.Hat(a, "vorlage"));

    /// <summary>Kennungen aller Auftraege des Spiels (auch Vorlagen und Zwischenfaelle): neue Kennungen duerfen keine davon sein.</summary>
    public static HashSet<string> SpielKennungen(PyObjekt inhalt) =>
        [.. SpielDaten.Liste(inhalt["aufgaben"]).Concat(ListeOderLeer(inhalt, "zwischenfaelle")).Select(Objekt).Select(a => Py.Str(a["id"]))];

    /// <summary>Ist der Eintrag ein Umhaengen (nur id und questgeber) einer vorhandenen Quest?</summary>
    public static bool IstUmhaengen(PyObjekt e, ISet<string> bestand) => e["id"] is string id && bestand.Contains(id);

    /// <summary>Alle Quests: zuerst die vorhandenen in der Reihenfolge der Datei, dann die neuen in der Reihenfolge des Abschnitts.</summary>
    public static List<Quest> Alle(PyObjekt inhalt, PyObjekt aussen)
    {
        var bestand = BestandQuests(inhalt).ToList();
        var ids = bestand.Select(a => Py.Str(a["id"])).ToHashSet(StringComparer.Ordinal);
        var umhaengen = new Dictionary<string, object?>(StringComparer.Ordinal);
        var neu = new List<Quest>();
        foreach (var e in Liste(aussen).OfType<PyObjekt>())
        {
            if (e["id"] is not string id)
            {
                continue;
            }
            if (ids.Contains(id))
            {
                umhaengen[id] = Get(e, "questgeber");
                continue;
            }
            neu.Add(new Quest(id, false, Py.Str(Get(e, "typ", "")), Py.Str(Get(e, "titel", "")), Get(e, "questgeber") as string,
                Get(e, "ab_tag") is long t ? t : 1, Get(e, "nach") as string, e, false));
        }
        var liste = new List<Quest>();
        foreach (var a in bestand)
        {
            var id = Py.Str(a["id"]);
            var um = umhaengen.TryGetValue(id, out var qg);
            liste.Add(new Quest(id, true, Py.Str(a["typ"]), Py.Str(a["titel"]), um ? qg as string : Py.Str(a["auftraggeber"]),
                Py.Int(Get(a, "ab_tag", 1L) ?? 1L), Get(a, "nach") as string, a, um));
        }
        liste.AddRange(neu);
        return liste;
    }

    /// <summary>Naechste freie Kennung neu-n (n ab 1, keine Kennung des Spiels).</summary>
    public static string NeueKennung(PyObjekt inhalt, PyObjekt aussen)
    {
        var belegt = SpielKennungen(inhalt);
        belegt.UnionWith(Liste(aussen).OfType<PyObjekt>().Select(e => e["id"]).OfType<string>());
        for (var n = 1; ; n++)
        {
            var id = NeuPraefix + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!belegt.Contains(id))
            {
                return id;
            }
        }
    }

    /// <summary>Neue leere Quest (ein Schritt). Rueckgabe: Kennung.</summary>
    public static string Neu(PyObjekt inhalt, PyObjekt aussen, string typ, string? questgeber)
    {
        if (!Typen.Contains(typ))
        {
            throw new ArgumentException("Typ " + typ);
        }
        var id = NeueKennung(inhalt, aussen);
        var q = new PyObjekt
        {
            { "id", id },
            { "typ", typ },
            { "titel", Texte.QuestNeuTitel },
            { "ticket", new List<object?>() },
            { "frage", "" },
        };
        if (typ == "auswahl")
        {
            q.Add("optionen", new List<object?> { "", "" });
            q.Add("antwort", "");
        }
        else
        {
            q.Add("paare", new List<object?> { new List<object?> { "", "" }, new List<object?> { "", "" } });
        }
        q.Add("hilfe", new List<object?>());
        q.Add("erklaerung", new List<object?>());
        q.Add("cat", Themen[0]);
        q.Add("belohnung", new PyObjekt { { "geld", 30L }, { "reputation", 3L } });
        q.Add("ab_tag", 1L);
        q.Add("questgeber", questgeber);
        Anlegen(aussen).Add(q);
        return id;
    }

    /// <summary>Eintrag einer neuen Quest (null = keine neue Quest dieser Kennung).</summary>
    public static PyObjekt? NeueQuest(PyObjekt aussen, string id) =>
        Liste(aussen).OfType<PyObjekt>().FirstOrDefault(e => Py.Gleich(e["id"], id) && Get(e, "typ") is not null);

    /// <summary>
    /// „Als neue Quest kopieren“ (1a): alle Felder einer Quest vom Typ Auswahl oder Zuordnung als neue Quest mit neuer
    /// Kennung, Titel mit „(Kopie)“; die Quelle bleibt. null = Typ laesst sich in Q1 nicht bearbeiten (2a).
    /// </summary>
    public static string? Kopieren(PyObjekt inhalt, PyObjekt aussen, string quelle)
    {
        var q = Alle(inhalt, aussen).FirstOrDefault(x => x.Id == quelle);
        if (q is null || !Typen.Contains(q.Typ))
        {
            return null;
        }
        var id = NeueKennung(inhalt, aussen);
        var n = new PyObjekt { { "id", id }, { "typ", q.Typ }, { "titel", q.Titel + Texte.QuestKopieZusatz } };
        foreach (var feld in (string[])["ticket", "frage", "optionen", "antwort", "paare", "hilfe", "erklaerung", "cat"])
        {
            if (Get(q.Daten, feld) is { } w)
            {
                n.Add(feld, Py.Kopie(w));
            }
        }
        n.Add("belohnung", Get(q.Daten, "belohnung") is PyObjekt b
            ? new PyObjekt { { "geld", Get(b, "geld", 0L) }, { "reputation", Get(b, "reputation", 0L) } }
            : new PyObjekt { { "geld", 30L }, { "reputation", 3L } });
        n.Add("ab_tag", q.AbTag);
        if (q.Nach is { } nach)
        {
            n.Add("nach", nach);
        }
        n.Add("questgeber", q.Questgeber);
        Anlegen(aussen).Add(n);
        return id;
    }

    /// <summary>Ein Feld einer neuen Quest setzen (null = Feld weg, nur bei „nach“). false = keine neue Quest.</summary>
    public static bool FeldSetzen(PyObjekt aussen, string id, string feld, object? wert)
    {
        if (NeueQuest(aussen, id) is not { } q)
        {
            return false;
        }
        if (wert is null && feld == "nach")
        {
            var rest = q.Where(p => p.Key != "nach").ToList();
            q.Clear();
            q.AddRange(rest);
            return true;
        }
        if (feld == "typ" && wert is string typ && Py.Str(q["typ"]) != typ)
        {
            // Typ wechseln: die Felder des anderen Typs fallen weg
            var ohne = q.Where(p => p.Key is not ("optionen" or "antwort" or "paare")).ToList();
            q.Clear();
            q.AddRange(ohne);
            Py.Setzen(q, "typ", typ);
            if (typ == "auswahl")
            {
                Py.Setzen(q, "optionen", new List<object?> { "", "" });
                Py.Setzen(q, "antwort", "");
            }
            else
            {
                Py.Setzen(q, "paare", new List<object?> { new List<object?> { "", "" }, new List<object?> { "", "" } });
            }
            return true;
        }
        Py.Setzen(q, feld, wert);
        return true;
    }

    /// <summary>Questgeber einer Quest setzen (Umhaengen, 1a); null = ohne Questgeber.</summary>
    public static void Umhaengen(PyObjekt inhalt, PyObjekt aussen, string id, string? questgeber)
    {
        if (NeueQuest(aussen, id) is { } q)
        {
            Py.Setzen(q, "questgeber", questgeber);
            return;
        }
        var alt = BestandQuests(inhalt).FirstOrDefault(a => Py.Gleich(a["id"], id)) ?? throw new ArgumentException("Quest " + id);
        var liste = Anlegen(aussen);
        liste.RemoveAll(e => e is PyObjekt o && Py.Gleich(o["id"], id));
        if (questgeber != Py.Str(alt["auftraggeber"]))
        {
            liste.Add(new PyObjekt { { "id", id }, { "questgeber", questgeber } });
        }
        Aufraeumen(aussen);
    }

    /// <summary>„Erst nach“ einer neuen Quest setzen (null = ohne). false = Bestand (bleibt wie im Spiel, 1a).</summary>
    public static bool NachSetzen(PyObjekt aussen, string id, string? nach) => FeldSetzen(aussen, id, "nach", nach);

    /// <summary>Neue Quest loeschen. false = keine neue Quest (vorhandene lassen sich nicht loeschen).</summary>
    public static bool Loeschen(PyObjekt aussen, string id)
    {
        var liste = Liste(aussen);
        var weg = liste.RemoveAll(e => e is PyObjekt o && Py.Gleich(o["id"], id) && Get(o, "typ") is not null) > 0;
        Aufraeumen(aussen);
        return weg;
    }

    /// <summary>Text einer Liste von Zeilen (Ticket, Hilfe, Erklaerung) fuer ein Eingabefeld: eine Zeile je Absatz.</summary>
    public static string Zeilen(object? wert) => wert is List<object?> l ? string.Join("\n", l.Select(Py.Str)) : wert as string ?? "";

    /// <summary>Text aus einem Eingabefeld als Liste von Zeilen (leere Zeilen fallen weg).</summary>
    public static List<object?> AusZeilen(string text) =>
        [.. text.Replace("\r", "", StringComparison.Ordinal).Split('\n').Select(z => z.Trim()).Where(z => z.Length > 0)];
}
