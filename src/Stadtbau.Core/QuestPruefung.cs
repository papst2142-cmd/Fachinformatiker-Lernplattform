using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle Q1 (S 0.40.0, Weisung P12-186, Kurzplan Meldung P12-111 Abschnitte 3–5): Pruefregeln fuer Quests, Questgeber und
/// Verkettungen, nur im Werkzeug (das Spiel liest die Abschnitte erst mit Q2). Rote Funde sperren Speichern und Senden der
/// Stadt-Datei wie jeder rote Fund draussen; gelbe sind Hinweise. Jede Regel hat in <c>QuestTests</c> einen roten und einen
/// gruenen Fall.
/// <para>E1 (Widerspruch im Kurzplan: Abschnitt 3 nennt „ohne Questgeber“ gelb, Abschnitte 4/5 und die Bilder 2/3 rot):
/// rot, wie die juengeren Abschnitte und die Bilder; sonst stuende die Quest im Spiel ohne Person, die sie stellt.</para>
/// </summary>
public static class QuestPruefung
{
    /// <summary>Ein Fund mit der Quest oder dem Questgeber, zu dem er gehoert (zum Antippen in den Reitern).</summary>
    public sealed record Fund(StadtbauFund Inhalt, string? Quest = null, string? Questgeber = null, bool Draussen = false)
    {
        public bool Rot => Inhalt.Rot;
        public string Text => Inhalt.Text;

        /// <summary>Fuer die Liste der Stadt draussen: Stelle nur bei einem Questgeber draussen (drinnen gilt sie im Grundriss).</summary>
        public StadtbauFund FuerStadt => Draussen ? Inhalt : Inhalt with { X = null, Y = null };
    }

    /// <summary>Verneinung in Grossschrift in einer Antwort (Regel 248, gleiche Woerter wie <c>LerninhalteTests</c>).</summary>
    public static readonly Regex VerneinungGross = new(@"\b(NICHT|KEINEN|KEINEM|KEINER|KEINES|KEINE|KEIN|NIE|NIEMALS|NUR|OHNE)\b");

    /// <summary>Was die Pruefung vom Werkzeug braucht.</summary>
    public sealed class Umgebung
    {
        public required PyObjekt Inhalt { get; init; }

        public required PyObjekt Aussen { get; init; }

        /// <summary>Innenraum (Schluessel „ort“ oder „ort@k“): Grundriss und Personen, null = gibt es nicht.</summary>
        public required Func<string, (PyObjekt Grundriss, List<PyObjekt> Personen)?> Innen { get; init; }

        /// <summary>Funde eines Innenraums mit zusaetzlichen Personen (wie die Pruefung des Innenraums).</summary>
        public required Func<string, List<PyObjekt>, List<StadtbauFund>> InnenPruefen { get; init; }

        /// <summary>Szene der Stadt draussen mit Startpunkt (nur gebaut, wenn ein Questgeber draussen steht).</summary>
        public required Func<(StadtSzene Szene, string StartOrt)?> Szene { get; init; }

        /// <summary>Ab welchem Spieltag ein Ort der Karte da ist (null = haengt vom Spiel ab), Ort der Karte je Stelle.</summary>
        public required Func<Spielgeschichte> GeschichteBauen { get; init; }

        private Spielgeschichte? _geschichte;

        /// <summary>Erst beim ersten Lesen gebaut (im Hintergrund).</summary>
        public Spielgeschichte Geschichte => _geschichte ??= GeschichteBauen();

        public Func<string, string> OrtName { get; init; } = o => o;
    }

    /// <summary>Alle Funde (rot zuerst nicht sortiert: Reihenfolge Quests, Verkettungen, Questgeber, Hinweis).</summary>
    public static List<Fund> Pruefen(Umgebung u)
    {
        var funde = new List<Fund>();
        if (QuestDaten.Liste(u.Aussen).Count == 0 && QuestgeberDaten.Liste(u.Aussen).Count == 0
            && Get(Aussen.Stadtdaten(u.Aussen), QuestDaten.Abschnitt) is null && Get(Aussen.Stadtdaten(u.Aussen), QuestgeberDaten.Abschnitt) is null)
        {
            return funde;
        }
        var questgeber = QuestgeberDaten.Alle(u.Inhalt, u.Aussen);
        Daten(u, funde);
        Quests(u, questgeber, funde);
        Verkettungen(u, questgeber, funde);
        Questgeber(u, questgeber, funde);
        funde.Add(new Fund(new StadtbauFund(Texte.QuestEinbauHinweis, Texte.QuestEinbauTipp, false)));
        return funde;
    }

    private static StadtbauFund Rot(string text, string tipp, double? x = null, double? y = null) => new(text, tipp, true, x, y);

    private static StadtbauFund Gelb(string text, string tipp) => new(text, tipp, false);

    /// <summary>Daten lesbar: Abschnitte sind Listen von Objekten mit Kennung, Kennungen einmalig.</summary>
    private static void Daten(Umgebung u, List<Fund> funde)
    {
        var sd = Aussen.Stadtdaten(u.Aussen);
        foreach (var abschnitt in (string[])[QuestDaten.Abschnitt, QuestgeberDaten.Abschnitt])
        {
            if (Get(sd, abschnitt) is { } w && (w is not List<object?> l || l.Any(e => e is not PyObjekt o || o["id"] is not string { Length: > 0 })))
            {
                funde.Add(new Fund(Rot(Texte.QuestDatenKaputt(abschnitt), Texte.QuestDatenKaputtTipp)));
            }
        }
        var bestand = QuestDaten.BestandQuests(u.Inhalt).Select(a => Py.Str(a["id"])).ToHashSet(StringComparer.Ordinal);
        var spiel = QuestDaten.SpielKennungen(u.Inhalt);
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in QuestDaten.Liste(u.Aussen).OfType<PyObjekt>())
        {
            if (e["id"] is not string id)
            {
                continue;
            }
            if (!gesehen.Add(id))
            {
                funde.Add(new Fund(Rot(Texte.QuestKennungDoppelt(id), Texte.QuestKennungDoppeltTipp), id));
            }
            if (bestand.Contains(id))
            {
                // Umhaengen (1a): nur id und questgeber, Texte der alten Quest bleiben
                if (e.Any(p => p.Key is not ("id" or "questgeber")))
                {
                    funde.Add(new Fund(Rot(Texte.QuestBestandGeaendert(id), Texte.QuestBestandGeaendertTipp), id));
                }
            }
            else if (spiel.Contains(id))
            {
                funde.Add(new Fund(Rot(Texte.QuestKennungDoppelt(id), Texte.QuestKennungDoppeltTipp), id));
            }
            else if (Get(e, "typ") is null)
            {
                funde.Add(new Fund(Rot(Texte.QuestUmhaengenUnbekannt(id), Texte.QuestUmhaengenUnbekanntTipp), id));
            }
        }
        var qg = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in QuestgeberDaten.Liste(u.Aussen).OfType<PyObjekt>())
        {
            if (e["id"] is string id && !qg.Add(id))
            {
                funde.Add(new Fund(Rot(Texte.QgKennungDoppelt(id), Texte.QuestKennungDoppeltTipp), null, id));
            }
        }
    }

    private static string Name(QuestDaten.Quest q) => q.Titel.Length > 0 ? q.Titel : q.Id;

    private static string Text(object? wert) => wert is List<object?> l ? string.Join(" ", l.Select(Py.Str)) : wert as string ?? "";

    private static bool Leer(object? wert) => Text(wert).Trim().Length == 0;

    /// <summary>Regeln je neuer Quest (Abschnitt 3) und je umgehaengter Quest (Questgeber).</summary>
    private static void Quests(Umgebung u, List<QuestgeberDaten.Questgeber> questgeber, List<Fund> funde)
    {
        var qgIds = questgeber.Select(q => q.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var q in QuestDaten.Alle(u.Inhalt, u.Aussen))
        {
            if (q.Bestand && !q.Umgehaengt)
            {
                continue;
            }
            void R(string text, string tipp) => funde.Add(new Fund(Rot(text, tipp), q.Id));
            void G(string text, string tipp) => funde.Add(new Fund(Gelb(text, tipp), q.Id));
            var n = Name(q);
            if (q.Questgeber is null)
            {
                R(Texte.QuestOhneQuestgeber(n), Texte.QuestOhneQuestgeberTipp);
            }
            else if (!qgIds.Contains(q.Questgeber))
            {
                R(Texte.QuestQuestgeberUnbekannt(n), Texte.QuestQuestgeberUnbekanntTipp);
            }
            if (q.Bestand)
            {
                continue;
            }
            var d = q.Daten;
            if (!QuestDaten.Typen.Contains(q.Typ))
            {
                R(Texte.QuestTypUnbekannt(n), Texte.QuestTypUnbekanntTipp);
                continue;
            }
            foreach (var (feld, name) in (ReadOnlySpan<(string, string)>)[("titel", Texte.QuestFeldTitel), ("ticket", Texte.QuestFeldTicket),
                ("frage", Texte.QuestFeldFrage), ("hilfe", Texte.QuestFeldHilfe), ("erklaerung", Texte.QuestFeldErklaerung)])
            {
                if (Leer(Get(d, feld)))
                {
                    R(Texte.QuestPflichtLeer(n, name), Texte.QuestPflichtLeerTipp);
                }
                else if (Text(Get(d, feld)).Length > QuestDaten.TextHoechstens)
                {
                    G(Texte.QuestTextLang(n, name), Texte.QuestTextLangTipp);
                }
            }
            if (Get(d, "cat") is not string cat || !QuestDaten.Themen.Contains(cat))
            {
                R(Texte.QuestThemaUnbekannt(n), Texte.QuestThemaUnbekanntTipp);
            }
            var b = Get(d, "belohnung") as PyObjekt;
            if (b is null || Get(b, "geld") is not long geld || geld < 0 || geld > QuestDaten.GeldHoechstens
                || Get(b, "reputation") is not long ruf || ruf < 0 || ruf > QuestDaten.RufHoechstens)
            {
                R(Texte.QuestBelohnung(n), Texte.QuestBelohnungTipp);
            }
            if (Get(d, "ab_tag") is not long tag || tag < 1 || tag > QuestDaten.AbTagHoechstens)
            {
                R(Texte.QuestAbTag(n), Texte.QuestAbTagTipp);
            }
            if (q.Typ == "auswahl")
            {
                Auswahl(d, n, R, G);
            }
            else
            {
                Zuordnung(d, n, R);
            }
        }
    }

    private static void Auswahl(PyObjekt d, string n, Action<string, string> rot, Action<string, string> gelb)
    {
        var optionen = Get(d, "optionen") as List<object?> ?? [];
        var texte = optionen.Select(o => (o as string ?? "").Trim()).ToList();
        if (texte.Count < 2 || texte.Count > QuestDaten.OptionenHoechstens)
        {
            rot(Texte.QuestOptionenZahl(n), Texte.QuestOptionenZahlTipp);
        }
        if (texte.Any(t => t.Length == 0))
        {
            rot(Texte.QuestPflichtLeer(n, Texte.QuestFeldOption), Texte.QuestPflichtLeerTipp);
        }
        var antwort = (Get(d, "antwort") as string ?? "").Trim();
        if (antwort.Length == 0)
        {
            rot(Texte.QuestPflichtLeer(n, Texte.QuestFeldAntwort), Texte.QuestAntwortTipp);
        }
        else if (!texte.Contains(antwort, StringComparer.Ordinal))
        {
            rot(Texte.QuestAntwortFehlt(n), Texte.QuestAntwortTipp);
        }
        if (texte.Where(t => t.Length > 0).GroupBy(t => Katalog.Normal(t)).Any(g => g.Count() > 1))
        {
            rot(Texte.QuestOptionenDoppelt(n), Texte.QuestOptionenDoppeltTipp);
        }
        foreach (var t in texte.Where(t => VerneinungGross.IsMatch(t)))
        {
            rot(Texte.QuestVerneinung(n, VerneinungGross.Match(t).Value), Texte.QuestVerneinungTipp);
        }
        if (antwort.Length > 0 && texte.Count > 1 && texte.Contains(antwort, StringComparer.Ordinal)
            && texte.Where(t => t != antwort).All(t => t.Length < antwort.Length))
        {
            gelb(Texte.QuestLaengsteRichtig(n), Texte.QuestLaengsteRichtigTipp);
        }
    }

    private static void Zuordnung(PyObjekt d, string n, Action<string, string> rot)
    {
        var paare = (Get(d, "paare") as List<object?> ?? []).Select(p => p as List<object?>).ToList();
        if (paare.Count < 2 || paare.Count > QuestDaten.PaareHoechstens || paare.Any(p => p is not { Count: 2 }))
        {
            rot(Texte.QuestPaareZahl(n), Texte.QuestPaareZahlTipp);
            return;
        }
        var links = paare.Select(p => (p![0] as string ?? "").Trim()).ToList();
        var rechts = paare.Select(p => (p![1] as string ?? "").Trim()).ToList();
        if (links.Concat(rechts).Any(t => t.Length == 0))
        {
            rot(Texte.QuestPflichtLeer(n, Texte.QuestFeldPaar), Texte.QuestPflichtLeerTipp);
        }
        if (links.Where(t => t.Length > 0).GroupBy(Katalog.Normal).Any(g => g.Count() > 1)
            || rechts.Where(t => t.Length > 0).GroupBy(Katalog.Normal).Any(g => g.Count() > 1))
        {
            rot(Texte.QuestPaareDoppelt(n), Texte.QuestOptionenDoppeltTipp);
        }
        foreach (var t in rechts.Where(t => VerneinungGross.IsMatch(t)))
        {
            rot(Texte.QuestVerneinung(n, VerneinungGross.Match(t).Value), Texte.QuestVerneinungTipp);
        }
    }

    /// <summary>Verkettungen (Abschnitt 5): Bedingung unbekannt, Kreis, Quest frueher frei als ihr Ort, Kette laenger als 10.</summary>
    private static void Verkettungen(Umgebung u, List<QuestgeberDaten.Questgeber> questgeber, List<Fund> funde)
    {
        var alle = QuestDaten.Alle(u.Inhalt, u.Aussen);
        var nach = alle.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First().Nach, StringComparer.Ordinal);
        var titel = alle.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => Name(g.First()), StringComparer.Ordinal);
        var spiel = QuestDaten.SpielKennungen(u.Inhalt);
        var kreise = new HashSet<string>(StringComparer.Ordinal);
        foreach (var q in alle.Where(q => !q.Bestand))
        {
            var n = Name(q);
            if (q.Nach is { } v && !nach.ContainsKey(v) && !spiel.Contains(v))
            {
                funde.Add(new Fund(Rot(Texte.QuestNachUnbekannt(n), Texte.QuestNachUnbekanntTipp), q.Id));
            }
            // Kette rueckwaerts verfolgen: Kreis oder Laenge
            var weg = new List<string> { q.Id };
            var jetzt = q.Nach;
            var kreis = false;
            while (jetzt is not null && nach.ContainsKey(jetzt))
            {
                if (weg.Contains(jetzt))
                {
                    kreis = jetzt == q.Id;
                    break;
                }
                weg.Add(jetzt);
                jetzt = nach[jetzt];
            }
            if (kreis)
            {
                var ring = string.Join("|", weg.Order(StringComparer.Ordinal));
                if (kreise.Add(ring))
                {
                    funde.Add(new Fund(Rot(Texte.QuestKreis([.. weg.Select(k => titel[k])]), Texte.QuestKreisTipp), q.Id));
                }
            }
            else if (weg.Count > QuestDaten.KetteHoechstens)
            {
                funde.Add(new Fund(Gelb(Texte.QuestKetteLang(n, weg.Count), Texte.QuestKetteLangTipp), q.Id));
            }
        }
        // frueher frei als ihr Ort: ab Tag der Quest vor dem Tag, an dem der Ort ihres Questgebers ins Spiel kommt
        var qg = questgeber.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var q in alle.Where(q => !q.Bestand || q.Umgehaengt))
        {
            if (q.Questgeber is null || !qg.TryGetValue(q.Questgeber, out var g) || QuestgeberDaten.Stelle(g) is not { } stelle
                || !u.Geschichte.OrtJeStelle.TryGetValue(stelle, out var ort) || u.Geschichte.FreiAbTag(ort) is not { } frei || q.AbTag >= frei)
            {
                continue;
            }
            funde.Add(new Fund(Rot(Texte.QuestZuFrueh(Name(q), u.OrtName(ort), frei, q.AbTag), Texte.QuestZuFruehTipp), q.Id));
        }
    }

    /// <summary>Regeln je Questgeber (Abschnitt 4): Name, Aussehen, Platz frei und erreichbar, hoechstens 12 draussen.</summary>
    private static void Questgeber(Umgebung u, List<QuestgeberDaten.Questgeber> questgeber, List<Fund> funde)
    {
        var namen = questgeber.GroupBy(q => Katalog.Normal(q.Name.Trim())).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        (StadtSzene Szene, string StartOrt)? szene = null;
        var szeneGebaut = false;
        var innenBasis = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var draussen = 0;
        foreach (var q in questgeber)
        {
            void R(string text, string tipp, double? x = null, double? y = null) =>
                funde.Add(new Fund(Rot(text, tipp, x, y), null, q.Id, q.Platz is { Draussen: true }));
            var n = q.Name.Trim().Length > 0 ? q.Name.Trim() : q.Id;
            if (!q.Bestand)
            {
                if (q.Name.Trim().Length == 0)
                {
                    R(Texte.QgNameLeer, Texte.QgNameTipp);
                }
                else if (q.Name.Trim().Length > QuestgeberDaten.NameHoechstens)
                {
                    R(Texte.QgNameLang(n), Texte.QgNameTipp);
                }
                if (namen.Contains(Katalog.Normal(q.Name.Trim())))
                {
                    R(Texte.QgNameDoppelt(n), Texte.QgNameTipp);
                }
                foreach (var teil in QuestgeberDaten.AussehenTeile)
                {
                    var wahl = Lernplattform.Core.Spiel.Aussehen.Teile.First(t => t.Teil == teil).Wahl;
                    if (Get(q.Aussehen, teil) is not string w || !wahl.Any(x => x.Kennung == w))
                    {
                        R(Texte.QgAussehenUnbekannt(n), Texte.QgAussehenTipp);
                        break;
                    }
                }
                if (q.Platz is null)
                {
                    R(Texte.QgOhnePlatz(n), Texte.QgOhnePlatzTipp);
                }
            }
            if (q.Eintrag is { } e && Get(e, "platz") is not null && q.Platz is null)
            {
                R(Texte.QgPlatzKaputt(n), Texte.QgOhnePlatzTipp);
            }
            if (q.Platz is not { } p)
            {
                continue;
            }
            if (!QuestgeberDaten.Blicke.Contains(p.Blick))
            {
                R(Texte.QgBlick(n), Texte.QgBlickTipp);
            }
            if (p.Draussen)
            {
                draussen++;
                if (draussen == QuestgeberDaten.DraussenHoechstens + 1)
                {
                    R(Texte.QgDraussenZuViele(QuestgeberDaten.Draussen(u.Inhalt, u.Aussen)), Texte.QgDraussenZuVieleTipp, p.X, p.Y);
                }
                var (breite, hoehe) = Aussen.Groesse(u.Aussen);
                if (p.X < 0 || p.Y < 0 || p.X > breite || p.Y > hoehe)
                {
                    R(Texte.QgNichtAufKarte(n), Texte.QgNichtFreiTipp, p.X, p.Y);
                    continue;
                }
                if (!szeneGebaut)
                {
                    szene = u.Szene();
                    szeneGebaut = true;
                }
                if (szene is not { } sz)
                {
                    continue;
                }
                if (!sz.Szene.Laufraster.KannStehen(p.X, p.Y))
                {
                    R(Texte.QgDraussenNichtFrei(n), Texte.QgNichtFreiTipp, p.X, p.Y);
                }
                else if (sz.Szene.Ort(sz.StartOrt) is { } start && sz.Szene.Laufraster.Weg((start.StehX, start.StehZ), (p.X, p.Y)).Count == 0
                    && sz.Szene.Laufraster.NaechsteFreie(start.StehX, start.StehZ) != sz.Szene.Laufraster.NaechsteFreie(p.X, p.Y))
                {
                    R(Texte.QgNichtErreichbar(n), Texte.QgNichtErreichbarTipp, p.X, p.Y);
                }
                continue;
            }
            var schluessel = p.InnenSchluessel;
            if (u.Innen(schluessel) is not { } innen)
            {
                R(Texte.QgOrtFehlt(n, p.Etage > 0 ? u.OrtName(p.Ort) + ", Etage " + p.Etage : u.OrtName(p.Ort)), Texte.QgOrtFehltTipp);
                continue;
            }
            var g = innen.Grundriss;
            if (p.X < 0 || p.Y < 0 || p.X > Py.F(g["breite"]) || p.Y > Py.F(g["hoehe"]))
            {
                R(Texte.QgInnenAussen(n), Texte.QgNichtFreiTipp, p.X, p.Y);
                continue;
            }
            if (Bearbeitung.StueckBei(g, p.X, p.Y) is not null || Wege.Wandstuecke(g).Any(w => p.X >= w.Item1 - 0.2 && p.X <= w.Item3 + 0.2 && p.Y >= w.Item2 - 0.2 && p.Y <= w.Item4 + 0.2))
            {
                R(Texte.QgInnenNichtFrei(n), Texte.QgNichtFreiTipp, p.X, p.Y);
                continue;
            }
            if (!innenBasis.TryGetValue(schluessel, out var basis))
            {
                innenBasis[schluessel] = basis = [.. u.InnenPruefen(schluessel, innen.Personen).Where(f => f.Rot).Select(f => f.Text)];
            }
            var person = new PyObjekt
            {
                { "id", q.Id },
                { "name", n },
                { "platz", new List<object?> { Bearbeitung.Zahl(p.X), Bearbeitung.Zahl(p.Y) } },
                { "aussehen", Py.Kopie(q.Aussehen) },
            };
            var mit = u.InnenPruefen(schluessel, [.. innen.Personen, person]);
            if (mit.Any(f => f.Rot && !basis.Contains(f.Text) && f.Text.Contains(n, StringComparison.Ordinal)
                && f.Text.Contains("nicht erreichbar", StringComparison.Ordinal)))
            {
                R(Texte.QgNichtErreichbar(n), Texte.QgNichtErreichbarTipp, p.X, p.Y);
            }
        }
    }
}
