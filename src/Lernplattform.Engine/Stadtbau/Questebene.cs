using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle Q2 (A 0.65.0 / S 0.41.0, Weisung P12-190, Kurzplan Meldung P12-111 Abschnitte 6–7, Nico 1a 2a 3a 4a): das Spiel
/// liest die Quest-Ebene einer Stadt-Fassung, die das Werkzeug seit S 0.40.0 schreibt:
/// <list type="bullet">
/// <item><c>stadtdaten.questgeber</c>: eigene Questgeber (Kennung <c>qg-n</c>, Name, Rolle, Aussehen, Platz) und
/// verschobene Auftraggeber des Spiels (<c>{id, bestand: true, platz}</c>). Platz drinnen in einem Geschaeft oder an einem
/// Schalter (auch auf einer Etage), oder draussen auf der Karte (hoechstens <see cref="DraussenHoechstens"/>, 4a).</item>
/// <item><c>stadtdaten.quests</c>: neue Quests (nur Auswahl und Zuordnung, 2a), hinten an die Aufgabenliste angehaengt (3a),
/// und vorhandene Quests mit anderem Questgeber (nur <c>{id, questgeber}</c>, die Texte bleiben, 1a).</item>
/// </list>
/// <para>Regeln: ohne beide Abschnitte gibt <see cref="Anwenden"/> genau dieselben Spielinhalte zurueck (dasselbe Objekt;
/// Fassung 0 und alle Fingerabdruecke bleiben bitgleich). Mit Abschnitten entstehen neue Listen (Kopien), die Inhalte aus
/// den Dateien bleiben unveraendert. Jeder Fehler ist ein roter Fund und haelt das Laden an (<see cref="StadtbauFehler"/>),
/// nie still weg. Nur das Werkzeug zeigt eine halb fertige Ebene nachsichtig (fehlerhafte Eintraege fallen dort nur fuer die
/// Ansicht weg, seine eigene Pruefung meldet sie rot).</para>
/// <para>Im Spiel: ein Questgeber ist eine Person in <c>kollegen</c> mit <c>ort</c> (Geschaeft, Schalter oder
/// <see cref="OrtDraussen"/>), <c>platz</c>, <c>etage</c> und <c>blick</c>; jede Quest, deren Questgeber einen neuen Platz
/// hat, traegt dessen Ort (<see cref="SpielDaten.AuftragOrt"/>). Tickets, Geld und Ruf laufen ueber den heutigen Ablauf
/// (Spielstand, Bewertung); keine neue Ereignisart, kein neues Feld im Spielstand.</para>
/// </summary>
public static class Questebene
{
    public const string AbschnittQuests = "quests";

    public const string AbschnittQuestgeber = "questgeber";

    /// <summary>Ort einer Quest, deren Questgeber draussen steht (wie <see cref="SpielDaten.OrtBuero"/>).</summary>
    public const string OrtDraussen = "draussen";

    /// <summary>Merkmal an Personen und Auftraegen aus der Quest-Ebene (nur zum Erkennen, das Spiel rechnet nicht damit).</summary>
    public const string Merkmal = "questebene";

    public const int DraussenHoechstens = 12;

    public const int NameHoechstens = 28;

    public const long GeldHoechstens = 200;

    public const long RufHoechstens = 10;

    public const long AbTagHoechstens = 365;

    public const int OptionenHoechstens = 6;

    public const int PaareHoechstens = 8;

    /// <summary>Typen neuer Quests (Nico 2a).</summary>
    public static readonly IReadOnlyList<string> Typen = ["auswahl", "zuordnung"];

    /// <summary>Lernbezug (Feld <c>cat</c>) wie die vorhandenen Aufgaben.</summary>
    public static readonly IReadOnlyList<string> Themen = ["systeme", "netzwerk", "wirtschaft", "sicherheit", "datenbanken"];

    /// <summary>Teile des Aussehens eines eigenen Questgebers.</summary>
    public static readonly IReadOnlyList<string> AussehenTeile = ["haut", "frisur", "haarfarbe", "oberteil"];

    public static readonly IReadOnlyList<long> Blicke = [0, 90, 180, 270];

    /// <summary>Praefix der Innenraeume eigener Gebaeude im Werkzeug: im Spiel erst mit dem Einbau (W4b).</summary>
    public const string BauPraefix = "bau:";

    /// <summary>Hat die Stadt eine Quest-Ebene (mindestens einen der beiden Abschnitte)?</summary>
    public static bool Hat(PyObjekt? stadtdaten) =>
        stadtdaten is not null && (Py.Hat(stadtdaten, AbschnittQuests) || Py.Hat(stadtdaten, AbschnittQuestgeber));

    /// <summary>Ein Questgeber, der draussen steht (fuer Darstellung und Ansprechen).</summary>
    public sealed record Draussen(string Id, string Name, double X, double Z, long Blick, PyObjekt? Aussehen, PyObjekt Daten);

    /// <summary>Platz eines Questgebers (gelesen und geprueft).</summary>
    private sealed record Platz(bool Aussen, string Ort, int Etage, double X, double Y, long Blick);

    /// <summary>Die roten Funde der Quest-Ebene (leer = laedt). Texte in einfachen Worten fuer Nico.</summary>
    public static List<string> Pruefen(PyObjekt inhalt, PyObjekt? stadtdaten, PyObjekt? innenraeume) =>
        Lesen(inhalt, stadtdaten, innenraeume).Funde;

    /// <summary>
    /// Spielinhalte mit der Quest-Ebene. Ohne Abschnitte: <paramref name="inhalt"/> selbst. Mit Funden: <see cref="StadtbauFehler"/>
    /// (nie still weg), ausser <paramref name="nachsichtig"/> oder <see cref="Nachsicht.An"/> (nur Werkzeug: fehlerhafte Eintraege
    /// fallen fuer die Ansicht weg; die Pruefung des Werkzeugs meldet sie selbst).
    /// </summary>
    public static PyObjekt Anwenden(PyObjekt inhalt, PyObjekt? stadtdaten, PyObjekt? innenraeume, bool nachsichtig = false)
    {
        if (!Hat(stadtdaten))
        {
            return inhalt;
        }
        var gelesen = Lesen(inhalt, stadtdaten, innenraeume);
        if (gelesen.Funde.Count > 0 && !nachsichtig && !Nachsicht.An)
        {
            throw new StadtbauFehler(gelesen.Funde);
        }
        return Einbauen(inhalt, gelesen);
    }

    /// <summary>Die Questgeber, die draussen stehen (Personen mit <c>ort</c> = <see cref="OrtDraussen"/>), in der Reihenfolge der Daten.</summary>
    public static List<Draussen> DraussenListe(PyObjekt inhalt)
    {
        var liste = new List<Draussen>();
        if (inhalt["kollegen"] is not List<object?> kollegen)
        {
            return liste;
        }
        foreach (var p in kollegen.OfType<PyObjekt>())
        {
            if (!Py.Gleich(Get(p, "ort"), OrtDraussen) || Get(p, "platz") is not List<object?> { Count: 2 } platz)
            {
                continue;
            }
            liste.Add(new Draussen(Py.Str(p["id"]), Py.Str(Get(p, "name", "")), Py.F(platz[0]), Py.F(platz[1]),
                Get(p, "blick") is long b ? b : 0, Get(p, "aussehen") as PyObjekt, p));
        }
        return liste;
    }

    /// <summary>Etage, auf der eine Person der Quest-Ebene steht (0 = Erdgeschoss oder keine Angabe).</summary>
    public static int Etage(PyObjekt person) => Get(person, "etage") is long e ? (int)e : 0;

    /// <summary>Stammt die Person aus der Quest-Ebene (eigener oder verschobener Questgeber)?</summary>
    public static bool IstQuestgeber(PyObjekt person) => Py.Wahr(Get(person, Merkmal));

    // ---------------------------------------------------------------- Lesen und Pruefen

    private sealed class Gelesen
    {
        public List<string> Funde { get; } = [];

        /// <summary>Gueltige Questgeber: Kennung → (Eintrag, Platz, eigener?).</summary>
        public Dictionary<string, (PyObjekt Eintrag, Platz Platz, bool Eigen)> Questgeber { get; } = new(StringComparer.Ordinal);

        /// <summary>Gueltige neue Quests in der Reihenfolge des Abschnitts.</summary>
        public List<PyObjekt> Neu { get; } = [];

        /// <summary>Gueltiges Umhaengen: Kennung der vorhandenen Quest → Questgeber.</summary>
        public Dictionary<string, string> Umhaengen { get; } = new(StringComparer.Ordinal);
    }

    private static Gelesen Lesen(PyObjekt inhalt, PyObjekt? stadtdaten, PyObjekt? innenraeume)
    {
        var g = new Gelesen();
        if (!Hat(stadtdaten))
        {
            return g;
        }
        var personen = AllePersonen(inhalt).Select(p => Py.Str(p["id"])).ToHashSet(StringComparer.Ordinal);
        QuestgeberLesen(g, inhalt, stadtdaten!, innenraeume, personen);
        QuestsLesen(g, inhalt, stadtdaten!, personen);
        return g;
    }

    private static List<PyObjekt>? Eintraege(PyObjekt stadtdaten, string abschnitt, List<string> funde)
    {
        if (!Py.Hat(stadtdaten, abschnitt))
        {
            return [];
        }
        if (stadtdaten[abschnitt] is not List<object?> l || l.Any(e => e is not PyObjekt o || o["id"] is not string { Length: > 0 }))
        {
            funde.Add($"Quests: der Abschnitt „{abschnitt}“ ist nicht lesbar (jeder Eintrag braucht eine Kennung).");
            return null;
        }
        return [.. l.Cast<PyObjekt>()];
    }

    private static void QuestgeberLesen(Gelesen g, PyObjekt inhalt, PyObjekt stadtdaten, PyObjekt? innenraeume, HashSet<string> personen)
    {
        if (Eintraege(stadtdaten, AbschnittQuestgeber, g.Funde) is not { } eintraege)
        {
            return;
        }
        var welt = Get(inhalt, "orte") is PyObjekt orte && Get(orte, "welt") is PyObjekt w ? w : null;
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        var namen = new HashSet<string>(StringComparer.Ordinal);
        var draussen = 0;
        foreach (var e in eintraege)
        {
            var id = Py.Str(e["id"]);
            if (!gesehen.Add(id))
            {
                g.Funde.Add($"Questgeber „{id}“ steht zweimal in der Stadt-Datei.");
                continue;
            }
            var eigen = !Py.Wahr(Get(e, "bestand"));
            var name = eigen ? (Get(e, "name") as string ?? "").Trim() : id;
            var vorher = g.Funde.Count;
            if (eigen)
            {
                if (personen.Contains(id))
                {
                    g.Funde.Add($"Questgeber „{id}“: die Kennung gehört schon einer Person im Spiel.");
                }
                if (name.Length == 0 || name.Length > NameHoechstens)
                {
                    g.Funde.Add($"Questgeber „{id}“: der Name fehlt oder ist länger als {NameHoechstens} Zeichen.");
                }
                else if (!namen.Add(name.ToUpperInvariant()))
                {
                    g.Funde.Add($"Questgeber „{name}“: der Name kommt zweimal vor.");
                }
                if (Get(e, "rolle") is not (null or string))
                {
                    g.Funde.Add($"Questgeber „{name}“: die Rolle ist kein Text.");
                }
                if (Get(e, "aussehen") is not PyObjekt aussehen || AussehenTeile.Any(t => Get(aussehen, t) is not string wert
                    || !Aussehen.Teile.First(x => x.Teil == t).Wahl.Any(x => x.Kennung == wert)))
                {
                    g.Funde.Add($"Questgeber „{name}“: das Aussehen ist unbekannt.");
                }
            }
            else if (!personen.Contains(id))
            {
                g.Funde.Add($"Questgeber „{id}“: diese Person gibt es im Spiel nicht.");
            }
            var platz = PlatzLesen(Get(e, "platz"), name, welt, innenraeume, g.Funde);
            if (platz is { Aussen: true })
            {
                draussen++;
            }
            if (platz is not null && g.Funde.Count == vorher)
            {
                g.Questgeber[id] = (e, platz, eigen);
            }
        }
        if (draussen > DraussenHoechstens)
        {
            g.Funde.Add($"Draußen stehen {draussen} Questgeber, höchstens {DraussenHoechstens} sind erlaubt.");
        }
    }

    private static Platz? PlatzLesen(object? wert, string name, PyObjekt? welt, PyObjekt? innenraeume, List<string> funde)
    {
        if (wert is not PyObjekt p || Get(p, "x") is not (long or double) || Get(p, "y") is not (long or double))
        {
            funde.Add($"Questgeber „{name}“: der Platz fehlt oder ist nicht lesbar.");
            return null;
        }
        var blick = Get(p, "blick", 0L);
        if (blick is not long b || !Blicke.Contains(b))
        {
            funde.Add($"Questgeber „{name}“: die Blickrichtung ist unbekannt.");
            return null;
        }
        double x = Py.F(p["x"]), y = Py.F(p["y"]);
        if (Py.Wahr(Get(p, "draussen")))
        {
            if (welt is not null && (x < 0 || y < 0 || x > Py.F(welt["breite"]) || y > Py.F(welt["hoehe"])))
            {
                funde.Add($"Questgeber „{name}“ steht draußen nicht auf der Karte.");
                return null;
            }
            return new Platz(true, "", 0, x, y, b);
        }
        if (Get(p, "ort") is not string ort || Get(p, "etage", 0L) is not long etage || etage < 0)
        {
            funde.Add($"Questgeber „{name}“: der Ort fehlt oder ist nicht lesbar.");
            return null;
        }
        if (ort.StartsWith(BauPraefix, StringComparison.Ordinal))
        {
            funde.Add($"Questgeber „{name}“ steht in einem selbst gebauten Gebäude; das kommt erst mit dem Einbau ins Spiel.");
            return null;
        }
        if (innenraeume is null || !InnenGrundriss.Orte(innenraeume).Contains(ort))
        {
            funde.Add($"Questgeber „{name}“: den Innenraum „{ort}“ gibt es im Spiel nicht.");
            return null;
        }
        var eg = Objekt(Objekt(innenraeume[ort])["gebaeude"]);
        if (!Etagen.Gibt(eg, (int)etage))
        {
            funde.Add($"Questgeber „{name}“: die Etage {etage} gibt es in „{ort}“ nicht.");
            return null;
        }
        var grundriss = etage == 0 ? eg : Etagen.Gebaeude(eg, (int)etage);
        if (x < 0 || y < 0 || x > Py.F(grundriss["breite"]) || y > Py.F(grundriss["hoehe"]))
        {
            funde.Add($"Questgeber „{name}“ steht außerhalb des Raums.");
            return null;
        }
        return new Platz(false, ort, (int)etage, x, y, b);
    }

    private static void QuestsLesen(Gelesen g, PyObjekt inhalt, PyObjekt stadtdaten, HashSet<string> personen)
    {
        if (Eintraege(stadtdaten, AbschnittQuests, g.Funde) is not { } eintraege)
        {
            return;
        }
        var aufgaben = Liste(inhalt["aufgaben"]).Select(Objekt).ToList();
        var bestand = aufgaben.Where(a => !Py.Wahr(Get(a, "vorlage"))).Select(a => Py.Str(a["id"])).ToHashSet(StringComparer.Ordinal);
        var spiel = aufgaben.Concat(ListeOderLeer(inhalt, "zwischenfaelle").Select(Objekt)).Select(a => Py.Str(a["id"])).ToHashSet(StringComparer.Ordinal);
        var neueIds = eintraege.Select(e => Py.Str(e["id"])).Where(id => !spiel.Contains(id)).ToHashSet(StringComparer.Ordinal);
        bool Bekannt(string qg) => g.Questgeber.ContainsKey(qg) || personen.Contains(qg);
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        var nach = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var e in eintraege)
        {
            var id = Py.Str(e["id"]);
            if (!gesehen.Add(id))
            {
                g.Funde.Add($"Quest „{id}“ steht zweimal in der Stadt-Datei.");
                continue;
            }
            if (bestand.Contains(id))
            {
                // Umhaengen (1a): nur Kennung und Questgeber, die Texte der alten Quest bleiben
                if (e.Any(p => p.Key is not ("id" or "questgeber")) || Get(e, "questgeber") is not string qgAlt)
                {
                    g.Funde.Add($"Quest „{id}“: bei einer vorhandenen Quest darf nur der Questgeber geändert werden.");
                }
                else if (!Bekannt(qgAlt))
                {
                    g.Funde.Add($"Quest „{id}“: den Questgeber „{qgAlt}“ gibt es nicht.");
                }
                else
                {
                    g.Umhaengen[id] = qgAlt;
                }
                continue;
            }
            if (spiel.Contains(id))
            {
                g.Funde.Add($"Quest „{id}“: die Kennung gehört schon einem Auftrag im Spiel.");
                continue;
            }
            var titel = Get(e, "titel") as string ?? "";
            var n = titel.Trim().Length > 0 ? titel.Trim() : id;
            var vorher = g.Funde.Count;
            void R(string text) => g.Funde.Add($"Quest „{n}“: {text}");
            if (Get(e, "typ") is not string typ || !Typen.Contains(typ))
            {
                R("der Typ ist im Spiel nur Auswahl oder Zuordnung.");
                continue;
            }
            if (Get(e, "questgeber") is not string qg)
            {
                R("sie hat keinen Questgeber.");
            }
            else if (!Bekannt(qg))
            {
                R($"den Questgeber „{qg}“ gibt es nicht.");
            }
            if (titel.Trim().Length == 0 || Get(e, "frage") is not string { Length: > 0 } frage || frage.Trim().Length == 0)
            {
                R("Titel oder Frage fehlt.");
            }
            foreach (var feld in (string[])["ticket", "hilfe", "erklaerung"])
            {
                // Zeilenliste wie in aufgaben.json oder ein Text mit Zeilenumbruechen (Kopie einer geladenen Quest im Werkzeug)
                var ok = Get(e, feld) switch
                {
                    string text => text.Trim().Length > 0,
                    List<object?> zeilen => zeilen.Count > 0 && zeilen.All(z => z is string s && s.Trim().Length > 0),
                    _ => false,
                };
                if (!ok)
                {
                    R($"das Feld „{feld}“ fehlt oder hat leere Zeilen.");
                }
            }
            if (Get(e, "cat") is not string cat || !Themen.Contains(cat))
            {
                R("das Thema ist unbekannt.");
            }
            if (Get(e, "belohnung") is not PyObjekt b || Get(b, "geld") is not long geld || geld < 0 || geld > GeldHoechstens
                || Get(b, "reputation") is not long ruf || ruf < 0 || ruf > RufHoechstens)
            {
                R($"die Belohnung muss 0 bis {GeldHoechstens} Euro und 0 bis {RufHoechstens} Ruf sein.");
            }
            if (Get(e, "ab_tag", 1L) is not long tag || tag < 1 || tag > AbTagHoechstens)
            {
                R($"„ab Tag“ muss zwischen 1 und {AbTagHoechstens} liegen.");
            }
            if (typ == "auswahl")
            {
                var optionen = Get(e, "optionen") as List<object?> ?? [];
                var texte = optionen.Select(o => (o as string ?? "").Trim()).ToList();
                if (texte.Count < 2 || texte.Count > OptionenHoechstens || texte.Any(t => t.Length == 0)
                    || texte.Distinct(StringComparer.Ordinal).Count() != texte.Count)
                {
                    R($"sie braucht 2 bis {OptionenHoechstens} verschiedene, nicht leere Antworten.");
                }
                else if (Get(e, "antwort") is not string antwort || !optionen.Contains(antwort))
                {
                    R("die richtige Antwort steht nicht unter den Antworten.");
                }
            }
            else
            {
                var paare = Get(e, "paare") as List<object?> ?? [];
                if (paare.Count < 2 || paare.Count > PaareHoechstens
                    || paare.Any(p => p is not List<object?> { Count: 2 } pl || pl.Any(t => t is not string s || s.Trim().Length == 0)))
                {
                    R($"sie braucht 2 bis {PaareHoechstens} vollständige Paare.");
                }
                else
                {
                    var links = paare.Cast<List<object?>>().Select(p => Py.Str(p[0])).ToList();
                    var rechts = paare.Cast<List<object?>>().Select(p => Py.Str(p[1])).ToList();
                    if (links.Distinct(StringComparer.Ordinal).Count() != links.Count || rechts.Distinct(StringComparer.Ordinal).Count() != rechts.Count)
                    {
                        R("zwei Paare sind gleich.");
                    }
                }
            }
            var v = Get(e, "nach");
            if (v is not null && (v is not string vs || (!spiel.Contains(vs) && !neueIds.Contains(vs))))
            {
                R("„erst nach“ nennt eine Quest, die es nicht gibt.");
            }
            nach[id] = v as string;
            if (g.Funde.Count == vorher)
            {
                g.Neu.Add(e);
            }
        }
        // Kreis in „erst nach“: die Quests kaemen nie dran (rot, nie still weg)
        foreach (var id in nach.Keys)
        {
            var weg = new HashSet<string>(StringComparer.Ordinal) { id };
            var jetzt = nach[id];
            while (jetzt is not null && nach.TryGetValue(jetzt, out var weiter))
            {
                if (!weg.Add(jetzt))
                {
                    break;
                }
                jetzt = weiter;
            }
            if (jetzt == id)
            {
                g.Funde.Add($"Quest „{id}“: „erst nach“ bildet einen Kreis, die Quest käme nie dran.");
                g.Neu.RemoveAll(e => Py.Gleich(e["id"], id));
            }
        }
    }

    // ---------------------------------------------------------------- Einbauen

    private static PyObjekt Einbauen(PyObjekt inhalt, Gelesen g)
    {
        var neu = Py.Flach(inhalt);
        var kollegen = Liste(inhalt["kollegen"]).ToList();
        var kunden = inhalt["kunden"] as PyObjekt;
        var kundenNeu = kunden is null ? null : Py.Flach(kunden);
        var orteNeu = kunden is not null && Py.Hat(kunden, "orte") ? Liste(kunden["orte"]).ToList() : null;
        // Ort der Quest je Questgeber mit eigenem Platz (Geschaeft, Schalter oder draussen)
        var ortVon = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, (eintrag, platz, eigen)) in g.Questgeber)
        {
            var ort = platz.Aussen ? OrtDraussen : platz.Ort;
            ortVon[id] = ort;
            PyObjekt person;
            var stelle = -1;
            if (eigen)
            {
                person = new PyObjekt
                {
                    { "id", id },
                    { "name", ((string)eintrag["name"]!).Trim() },
                    { "rolle", Get(eintrag, "rolle") as string ?? "" },
                    { "aussehen", Py.Kopie(eintrag["aussehen"]) },
                };
            }
            else if (kollegen.FindIndex(p => Py.Gleich(Objekt(p)["id"], id)) is var i and >= 0)
            {
                // an derselben Stelle der Liste (die Reihenfolge der Personen bleibt)
                person = Py.Flach(Objekt(kollegen[i]));
                stelle = i;
                Py.Setzen(person, "raum", null);
            }
            else
            {
                person = BeimKundenHerausnehmen(orteNeu, id);
            }
            Py.Setzen(person, "ort", ort);
            Py.Setzen(person, "platz", new List<object?> { platz.X, platz.Y });
            Py.Setzen(person, "etage", (long)platz.Etage);
            Py.Setzen(person, "blick", platz.Blick);
            Py.Setzen(person, Merkmal, true);
            if (stelle >= 0)
            {
                kollegen[stelle] = person;
            }
            else
            {
                kollegen.Add(person);
            }
        }
        var aufgaben = Liste(inhalt["aufgaben"]).ToList();
        for (var i = 0; i < aufgaben.Count; i++)
        {
            var a = Objekt(aufgaben[i]);
            var aid = Py.Str(a["id"]);
            var qg = g.Umhaengen.TryGetValue(aid, out var um) ? um : Py.Str(a["auftraggeber"]);
            var umgehaengt = g.Umhaengen.ContainsKey(aid) && qg != Py.Str(a["auftraggeber"]);
            // wie die Spielgeschichte des Werkzeugs: eine Quest mit eigenem Ort (beim Kunden) bleibt dort, wenn nur ihr
            // Questgeber einen neuen Platz hat; umgehaengt oder ohne eigenen Ort wandert sie mit dem Questgeber
            if (!umgehaengt && (!ortVon.ContainsKey(qg) || Py.Hat(a, "ort")))
            {
                continue;
            }
            var kopie = Py.Flach(a);
            Py.Setzen(kopie, "auftraggeber", qg);
            KopieOhne(kopie, "stelle");
            if (ortVon.TryGetValue(qg, out var o))
            {
                Py.Setzen(kopie, "ort", o);
                Py.Setzen(kopie, "raum", "");
            }
            else
            {
                // zu einer Person des Spiels an ihrem alten Platz: deren Ort gilt (AuftragOrt)
                KopieOhne(kopie, "ort");
            }
            Py.Setzen(kopie, Merkmal, true);
            aufgaben[i] = kopie;
        }
        foreach (var e in g.Neu)
        {
            aufgaben.Add(NeueAufgabe(e, ortVon));
        }
        Py.Setzen(neu, "kollegen", kollegen);
        Py.Setzen(neu, "aufgaben", aufgaben);
        if (kundenNeu is not null && orteNeu is not null)
        {
            Py.Setzen(kundenNeu, "orte", orteNeu);
            Py.Setzen(neu, "kunden", kundenNeu);
        }
        return neu;
    }

    private static void KopieOhne(PyObjekt d, string feld)
    {
        if (!Py.Hat(d, feld))
        {
            return;
        }
        var rest = d.Where(p => p.Key != feld).ToList();
        d.Clear();
        d.AddRange(rest);
    }

    /// <summary>Person beim Kunden aus der Kopie ihres Kundenorts nehmen (sonst stuende sie an zwei Plaetzen).</summary>
    private static PyObjekt BeimKundenHerausnehmen(List<object?>? orte, string id)
    {
        for (var i = 0; orte is not null && i < orte.Count; i++)
        {
            var ort = Objekt(orte[i]);
            var leute = ListeOderLeer(ort, "personen").ToList();
            var k = leute.FindIndex(p => Py.Gleich(Objekt(p)["id"], id));
            if (k < 0)
            {
                continue;
            }
            var person = Py.Flach(Objekt(leute[k]));
            leute.RemoveAt(k);
            var ortKopie = Py.Flach(ort);
            Py.Setzen(ortKopie, "personen", leute);
            orte[i] = ortKopie;
            return person;
        }
        throw new InvalidDataException("Person " + id + " fehlt");
    }

    private static PyObjekt NeueAufgabe(PyObjekt e, Dictionary<string, string> ortVon)
    {
        var qg = (string)e["questgeber"]!;
        var typ = (string)e["typ"]!;
        var b = Objekt(e["belohnung"]);
        var a = new PyObjekt
        {
            { "id", e["id"] },
            { "typ", typ },
            { "titel", ((string)e["titel"]!).Trim() },
            { "auftraggeber", qg },
            { "raum", "" },
            { "prioritaet", "normal" },
            { "cat", e["cat"] },
            { "anforderungen", new PyObjekt() },
            { "achsen", new List<object?> { "fachkompetenz" } },
            { "belohnung", new PyObjekt { { "geld", b["geld"] }, { "reputation", b["reputation"] } } },
            { "ab_tag", Get(e, "ab_tag", 1L) },
            { "ticket", SpielInhalt.Zeilen(Py.Kopie(e["ticket"])) },
            { "frage", ((string)e["frage"]!).Trim() },
        };
        if (typ == "auswahl")
        {
            a.Add("optionen", Py.Kopie(e["optionen"]));
            a.Add("antwort", e["antwort"]);
        }
        else
        {
            a.Add("paare", Py.Kopie(e["paare"]));
        }
        a.Add("hilfe", SpielInhalt.Zeilen(Py.Kopie(e["hilfe"])));
        a.Add("erklaerung", SpielInhalt.Zeilen(Py.Kopie(e["erklaerung"])));
        a.Add("suchbegriffe", new List<object?>());
        if (Get(e, "nach") is string nach)
        {
            a.Add("nach", nach);
        }
        if (ortVon.TryGetValue(qg, out var ort))
        {
            a.Add("ort", ort);
        }
        a.Add(Merkmal, true);
        return a;
    }

    /// <summary>Wo eine Quest in einem Geschaeft oder an einem Schalter wartet (Liste der Tickets).</summary>
    public static readonly IReadOnlyDictionary<string, string> OrtNamen = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["moebelhaus"] = "im Möbelhaus", ["elektromarkt"] = "im Elektromarkt", ["kaufhaus"] = "im Kaufhaus", ["bank"] = "in der Bank", ["amt"] = "im Amt",
    };

    /// <summary>Text fuer die Liste der Tickets: wo die Quest wartet (Geschaeft, Schalter, draussen), sonst null.</summary>
    public static string? Wo(object? ort) => ort switch
    {
        OrtDraussen => "draußen in der Stadt",
        string o when OrtNamen.TryGetValue(o, out var wo) => wo,
        _ => null,
    };
}
