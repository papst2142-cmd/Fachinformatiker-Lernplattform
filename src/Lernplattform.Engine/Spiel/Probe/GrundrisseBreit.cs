using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 1.2.0 (Weisung P12-334, Nico: Weg B „Neue Grundrisse als Daten“): die breiten Grundrisse (Diele 1,32 m, Flure 1,80 m,
/// Innentueren 0,90 m) stehen als Daten in <see cref="Datei"/>, nur angehaengt in <c>inhalte_csharp/</c>; <c>inhalte/</c> bleibt
/// byte-gleich mit Python v0.63.2. Die Datei ist eine Liste von Ersatzstuecken: an einem Pfad im Spielinhalt bzw. in den
/// Innenraeumen steht ein neuer Wert, der den alten ersetzt, aber nur, wenn der alte genau dem erwarteten entspricht
/// (<see cref="Pruefsumme"/>). Baut Nico einen Raum im Werkzeug neu, gilt seiner; ein zweites Einsetzen aendert nichts (kein Flur
/// wird doppelt breiter). Spiel-Kern, Innenraum und Werkzeug lesen denselben eingesetzten Inhalt: keine Umrechnung zur Laufzeit.
/// Erzeugt wird die Datei einmal aus den alten Grundrissen (<see cref="Erzeugen"/>, Rechnung <see cref="Flurbreite"/>); ein Test
/// prueft, dass sie genau diesem Ergebnis entspricht. Fuer die Spielstaende (gespeicherte Moebelplaetze der Wohnungen) steht
/// je Wohnung die Umrechnung mit in der Datei (<see cref="Wohnungen"/>).
/// </summary>
public static class GrundrisseBreit
{
    /// <summary>Pfad der Datei (relativ zum Repo bzw. Name der eingebetteten Datei).</summary>
    public const string Datei = "inhalte_csharp/spiel/grundrisse_breit.json";

    /// <summary>Bereich „Spielinhalt“ (Ergebnis von SpielInhalt.Laden).</summary>
    public const string Inhalt = "inhalt";

    /// <summary>Bereich „Innenraeume“ (innenraeume.json bzw. der Abschnitt der Stadt-Fassung).</summary>
    public const string Innenraeume = "innenraeume";

    /// <summary>Formatnummer der Datei; eine neuere Nummer ist ein klarer Fehler.</summary>
    public const long Format = 1;

    /// <summary>
    /// Ein Ersatzstueck: im <paramref name="Bereich"/> am <paramref name="Pfad"/> (Schluessel eines Objekts, Zahl = Stelle in
    /// einer Liste, „@id:x“ = Eintrag einer Liste mit dieser id) steht <paramref name="Neu"/> statt eines Werts mit der Pruefsumme
    /// <paramref name="Alt"/>.
    /// </summary>
    public sealed record Ersatz(string Bereich, IReadOnlyList<object> Pfad, string Alt, object? Neu);

    /// <summary>Daten der Datei: Ersatzstuecke und je Wohnung die Umrechnung des Flurs (fuer die Spielstaende).</summary>
    public sealed record Daten(IReadOnlyList<Ersatz> Ersatz, IReadOnlyDictionary<string, Flurbreite.Umrechnung> Wohnungen)
    {
        /// <summary>Keine Datei: nichts wird ersetzt.</summary>
        public static readonly Daten Leer = new([], new Dictionary<string, Flurbreite.Umrechnung>());
    }

    /// <summary>Pruefsumme „sha256:&lt;hex&gt;“ eines Werts (json.dumps kompakt, sortiert, ohne \u-Umschreibung).</summary>
    public static string Pruefsumme(object? wert) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(PythonJson.Utf8(PythonJson.Text(wert, sortiert: true))));

    /// <summary>Liest die Datei (null = keine Datei, dann <see cref="Daten.Leer"/>).</summary>
    public static Daten Lesen(byte[]? datei)
    {
        if (datei is null)
        {
            return Daten.Leer;
        }
        var d = Py.Lesen(datei) as PyObjekt ?? throw new InvalidDataException(Datei + " ist kein Objekt");
        var format = Py.Int(d["format"]);
        if (format > Format)
        {
            throw new InvalidDataException($"{Datei}: Format {format} ist neuer als dieses Programm ({Format})");
        }
        var ersatz = Liste(d["ersatz"]).Select(Objekt).Select(e => new Ersatz(
            Py.Str(e["bereich"]), [.. Liste(e["pfad"]).Select(p => p is long n ? (object)n : Py.Str(p))], Py.Str(e["alt"]), e["neu"])).ToList();
        var wohnungen = new Dictionary<string, Flurbreite.Umrechnung>();
        foreach (var (id, u) in Objekt(d["wohnungen"]))
        {
            var o = Objekt(u);
            wohnungen[id] = new Flurbreite.Umrechnung(Py.F(o["oben"]), Py.F(o["unten"]), Py.F(o["zuwachs"]));
        }
        return new Daten(ersatz, wohnungen);
    }

    /// <summary>
    /// Setzt die Ersatzstuecke eines Bereichs ein (Kopie; das Original bleibt). Ein Stueck, dessen alter Wert fehlt oder nicht
    /// mehr passt (z. B. von Nico im Werkzeug neu gebaut), bleibt wie es ist.
    /// </summary>
    public static PyObjekt Einsetzen(PyObjekt wurzel, string bereich, Daten daten)
    {
        var stuecke = daten.Ersatz.Where(e => e.Bereich == bereich).ToList();
        if (stuecke.Count == 0)
        {
            return wurzel;
        }
        var neu = (PyObjekt)Py.Kopie(wurzel)!;
        foreach (var e in stuecke)
        {
            if (Finden(neu, e.Pfad) is { } stelle && Pruefsumme(stelle.Wert) == e.Alt)
            {
                stelle.Setzen(Py.Kopie(e.Neu));
            }
        }
        return neu;
    }

    /// <summary>Stadt-Fassung mit den breiten Grundrissen in ihren Innenraeumen (Kopie; Nicos Datei bleibt bytegleich).</summary>
    public static Stadtbau.StadtFassung Fassung(Stadtbau.StadtFassung fassung, Daten daten) =>
        new()
        {
            StadtId = fassung.StadtId, Nummer = fassung.Nummer, Basis = fassung.Basis, Werkzeug = fassung.Werkzeug,
            Erstellt = fassung.Erstellt, Nachricht = fassung.Nachricht, Karte = fassung.Karte, Stadtdaten = fassung.Stadtdaten,
            Innenraeume = Einsetzen(fassung.Innenraeume, Innenraeume, daten), Laternen = fassung.Laternen, Beete = fassung.Beete,
        };

    /// <summary>Wie viele Ersatzstuecke eines Bereichs passen (alter Wert gleich)? Fuer Tests und Fundlisten.</summary>
    public static int Passend(PyObjekt wurzel, string bereich, Daten daten) =>
        daten.Ersatz.Count(e => e.Bereich == bereich && Finden(wurzel, e.Pfad) is { } s && Pruefsumme(s.Wert) == e.Alt);

    private sealed record Stelle(object? Wert, Action<object?> Setzen);

    private static Stelle? Finden(PyObjekt wurzel, IReadOnlyList<object> pfad)
    {
        object? halter = wurzel;
        Stelle? stelle = null;
        foreach (var teil in pfad)
        {
            var h = halter;
            switch (teil)
            {
                case long n when h is List<object?> l && n >= 0 && n < l.Count:
                    stelle = new Stelle(l[(int)n], w => l[(int)n] = w);
                    break;
                case string s when s.StartsWith("@id:", StringComparison.Ordinal) && h is List<object?> l:
                    var i = l.FindIndex(x => x is PyObjekt o && Py.Hat(o, "id") && Py.Str(o["id"]) == s[4..]);
                    if (i < 0)
                    {
                        return null;
                    }
                    stelle = new Stelle(l[i], w => l[i] = w);
                    break;
                case string s when h is PyObjekt o && Py.Hat(o, s):
                    stelle = new Stelle(o[s], w => Py.Setzen(o, s, w));
                    break;
                default:
                    return null;
            }
            halter = stelle.Wert;
        }
        return stelle;
    }

    // ---- Erzeugen (einmal, aus den alten Grundrissen; Test prueft die Datei dagegen) ------------------------------------------

    /// <summary>
    /// Erzeugt die Daten aus dem alten Spielinhalt (wie Python v0.63.2) und den alten Innenraeumen: je Ort mit Flur der breite
    /// Grundriss (<see cref="Flurbreite.Anwenden"/>, <see cref="Flurbreite.Tueren"/>), dazu alles, was Stellen in diesem
    /// Grundriss nennt: Plaetze der Kollegen und Kunden-Personen, Arbeitsplaetze der Firmenstufen, Stellen der Auftraege,
    /// Vorgabe-Einrichtung und Kartons der Wohnungen, Zusaetze der Innenraeume. Nur Stuecke, die sich aendern.
    /// </summary>
    public static Daten Erzeugen(PyObjekt inhalt, PyObjekt innenraeume)
    {
        var ersatz = new List<Ersatz>();
        var wohnungen = new Dictionary<string, Flurbreite.Umrechnung>();
        // Raum-id -> Umrechnung des Grundrisses, in dem er liegt (fuer Stellen der Auftraege); doppelte ids mit anderer Umrechnung = Fehler
        var raumOrt = new Dictionary<string, (Flurbreite.Umrechnung U, PyObjekt Alt)>();

        void Dazu(string bereich, object[] pfad, object? alt, object? neu)
        {
            if (Pruefsumme(alt) != Pruefsumme(neu))
            {
                ersatz.Add(new Ersatz(bereich, pfad, Pruefsumme(alt), neu));
            }
        }
        (Flurbreite.Umrechnung U, PyObjekt Neu) Grundriss(string ort, PyObjekt alt)
        {
            var u = Flurbreite.Fuer(ort, alt);
            foreach (var r in ListeOderLeer(alt, "raeume").Select(Objekt))
            {
                var id = Py.Str(r["id"]);
                if (raumOrt.TryGetValue(id, out var vorher) && vorher.U != u && !ReferenceEquals(vorher.Alt, alt))
                {
                    raumOrt[id] = (Flurbreite.Umrechnung.Keine, alt); // mehrdeutig: Stellen dort duerfen sich nicht verschieben
                }
                else
                {
                    raumOrt[id] = (u, alt);
                }
            }
            return (u, Flurbreite.Tueren(Flurbreite.Anwenden(alt, u)));
        }
        object? Punkt(object? platz, Flurbreite.Umrechnung u)
        {
            if (platz is not List<object?> { Count: >= 2 } p)
            {
                return platz;
            }
            var neu = (List<object?>)Py.Kopie(p)!;
            neu[1] = Flurbreite.Zahl(u.Hin(Py.F(p[1])), p[1]);
            return neu;
        }
        List<object?> Leute(List<object?> leute, Flurbreite.Umrechnung u) => [.. leute.Select(x =>
        {
            if (x is not PyObjekt o || Get(o, "platz") is null)
            {
                return Py.Kopie(x);
            }
            var n = (PyObjekt)Py.Kopie(o)!;
            Py.Setzen(n, "platz", Punkt(o["platz"], u));
            return n;
        })];

        // Bitweiche mit den Kollegen
        var (ub, gb) = Grundriss(OrtBuero, Objekt(inhalt["gebaeude"]));
        Dazu(Inhalt, ["gebaeude"], inhalt["gebaeude"], gb);
        Dazu(Inhalt, ["kollegen"], inhalt["kollegen"], Leute(Liste(inhalt["kollegen"]), ub));

        // Wohnungen: Grundriss, Kartons (Rechteck), Vorgabe-Einrichtung (Moebel an ihrer Wand)
        var wohnDaten = Objekt(inhalt["wohnungen"]);
        foreach (var w in Liste(wohnDaten["wohnungen"]).Select(Objekt))
        {
            var id = Py.Str(w["id"]);
            var (u, g) = Grundriss(OrtZuhause, Objekt(w["gebaeude"]));
            wohnungen[id] = u;
            var neu = (PyObjekt)Py.Kopie(w)!;
            Py.Setzen(neu, "gebaeude", g);
            if (Get(w, "kartons") is List<object?> { Count: 4 } k)
            {
                var kn = (List<object?>)Py.Kopie(k)!;
                kn[1] = Flurbreite.Zahl(Py.F(k[1]) + u.Verschiebung(Py.F(k[1]), Py.F(k[3])), k[1]);
                Py.Setzen(neu, "kartons", kn);
            }
            if (Get(w, "einrichtung") is PyObjekt e && Get(e, "moebel") is PyObjekt m)
            {
                var en = (PyObjekt)Py.Kopie(e)!;
                Py.Setzen(en, "moebel", MoebelUmrechnen(m, u, wohnDaten, null));
                Py.Setzen(neu, "einrichtung", en);
            }
            Dazu(Inhalt, ["wohnungen", "wohnungen", "@id:" + id], w, neu);
        }

        // Kunden mit ihren Personen
        foreach (var k in Kundenorte(inhalt))
        {
            var id = Py.Str(k["id"]);
            var (u, g) = Grundriss(id, Objekt(k["gebaeude"]));
            var neu = (PyObjekt)Py.Kopie(k)!;
            Py.Setzen(neu, "gebaeude", g);
            if (Get(k, "personen") is List<object?> leute)
            {
                Py.Setzen(neu, "personen", Leute(leute, u));
            }
            Dazu(Inhalt, ["kunden", "orte", "@id:" + id], k, neu);
        }

        // Firma und Filiale: jede Stufe mit Grundriss und Arbeitsplaetzen [x, y, raum]
        var firma = Objekt(inhalt["firma"]);
        foreach (var (teil, ort) in new[] { ("gebaeude", OrtBuero), ("filiale", OrtFiliale) })
        {
            if (Get(firma, teil) is not PyObjekt f)
            {
                continue;
            }
            var stufen = ListeOderLeer(f, "stufen");
            for (var i = 0; i < stufen.Count; i++)
            {
                var s = Objekt(stufen[i]);
                if (Get(s, "gebaeude") is not PyObjekt alt)
                {
                    continue;
                }
                var (u, g) = Grundriss(ort, alt);
                var neu = (PyObjekt)Py.Kopie(s)!;
                Py.Setzen(neu, "gebaeude", g);
                if (Get(s, "plaetze") is List<object?> pl)
                {
                    Py.Setzen(neu, "plaetze", pl.Select(p => Punkt(p, u)).ToList());
                }
                Dazu(Inhalt, ["firma", teil, "stufen", (long)i], s, neu);
            }
        }

        // Serverfarm (Innenraeume) mit ihren Personen
        if (Get(innenraeume, Innenraum.Serverfarm) is PyObjekt farm && Get(farm, "gebaeude") is PyObjekt fg)
        {
            var (u, g) = Grundriss(Innenraum.Serverfarm, fg);
            var neu = (PyObjekt)Py.Kopie(farm)!;
            Py.Setzen(neu, "gebaeude", g);
            if (Get(farm, "personen") is List<object?> leute)
            {
                Py.Setzen(neu, "personen", Leute(leute, u));
            }
            if (Get(farm, "plaetze") is PyObjekt plaetze)
            {
                var np = new PyObjekt();
                foreach (var (id, platz) in plaetze)
                {
                    np.Add(id, Punkt(platz, u));
                }
                Py.Setzen(neu, "plaetze", np);
            }
            Dazu(Innenraeume, [Innenraum.Serverfarm], farm, neu);
        }

        // Zusaetze der Innenraeume je Ort: Einrichtung in den Raeumen rueckt mit ihrem Raum, Plaetze wie Punkte
        var orte = new Dictionary<string, PyObjekt> { [OrtBuero] = Objekt(inhalt["gebaeude"]) };
        foreach (var k in Kundenorte(inhalt))
        {
            orte[Py.Str(k["id"])] = Objekt(k["gebaeude"]);
        }
        if (Get(innenraeume, Innenraum.Serverfarm) is PyObjekt sf && Get(sf, "gebaeude") is PyObjekt sfg)
        {
            orte[Innenraum.Serverfarm] = sfg;
        }
        foreach (var abschnitt in new[] { "zusatz", "zusatz_kunden", "zusatz_nachtrag", "zusatz_moebel", "zusatz_schraenke" })
        {
            if (Get(innenraeume, abschnitt) is not PyObjekt z)
            {
                continue;
            }
            foreach (var (ort, zo) in z)
            {
                if (!orte.TryGetValue(ort, out var alt) || zo is not PyObjekt zusatz)
                {
                    continue;
                }
                Dazu(Innenraeume, [abschnitt, ort], zusatz, ZusatzUmrechnen(zusatz, alt, Flurbreite.Fuer(ort, alt)));
            }
        }

        // Stellen der Auftraege (Aufgaben, Vorlagen, Zwischenfaelle) im Raum, in dem sie liegen
        foreach (var liste in new[] { "aufgaben", "zwischenfaelle" })
        {
            var eintraege = ListeOderLeer(inhalt, liste);
            for (var i = 0; i < eintraege.Count; i++)
            {
                if (eintraege[i] is not PyObjekt a || Get(a, "stelle") is null || Get(a, "raum") is not string raum
                    || !raumOrt.TryGetValue(raum, out var ro))
                {
                    continue;
                }
                Dazu(Inhalt, [liste, (long)i, "stelle"], a["stelle"], Punkt(a["stelle"], ro.U));
            }
        }
        return new Daten(ersatz, wohnungen);
    }

    /// <summary>
    /// Moebelplaetze einer Wohnung {stueck: [x, y, dreh]} von altem auf breiten Grundriss: jedes Stueck rueckt wie ein Rechteck
    /// (<see cref="Flurbreite.Umrechnung.Verschiebung"/>: unter der Diele um den Zuwachs, in der Diele an seiner Wand). Die Groesse
    /// kommt aus dem Moebelkatalog (<paramref name="moebelVonStueck"/>: Stueck -> Moebel-id, sonst die Startmoebel der Wohnungen).
    /// </summary>
    public static PyObjekt MoebelUmrechnen(PyObjekt moebel, Flurbreite.Umrechnung u, PyObjekt wohnDaten, Func<string, object?>? moebelVonStueck)
    {
        var katalog = ListeOderLeer(wohnDaten, "moebel").Select(Objekt).Where(m => Py.Hat(m, "id")).ToDictionary(m => Py.Str(m["id"]));
        var start = Get(wohnDaten, "start_moebel") as PyObjekt;
        var neu = new PyObjekt();
        foreach (var (stueck, wert) in moebel)
        {
            if (wert is not List<object?> { Count: 3 } p)
            {
                Py.Setzen(neu, stueck, Py.Kopie(wert));
                continue;
            }
            var id = moebelVonStueck?.Invoke(stueck) ?? (start is not null ? Get(start, stueck) : null);
            var h = 0.0;
            if (id is string mid && katalog.TryGetValue(mid, out var m))
            {
                var dreh = Py.Int(p[2]);
                h = Py.F(((dreh % 2) + 2) % 2 != 0 ? m["w"] : m["h"]);
            }
            var n = (List<object?>)Py.Kopie(p)!;
            n[1] = Flurbreite.Zahl(Py.F(p[1]) + u.Verschiebung(Py.F(p[1]), h), p[1]);
            Py.Setzen(neu, stueck, n);
        }
        return neu;
    }

    private static PyObjekt ZusatzUmrechnen(PyObjekt zusatz, PyObjekt alt, Flurbreite.Umrechnung u)
    {
        var neu = (PyObjekt)Py.Kopie(zusatz)!;
        if (u.Zuwachs <= 0)
        {
            return neu;
        }
        void Deko(object? liste, Func<double, double, double> v)
        {
            foreach (var d in (liste as List<object?> ?? []).OfType<PyObjekt>())
            {
                if (Get(d, "y") is { } y)
                {
                    Py.Setzen(d, "y", Flurbreite.Zahl(Py.F(y) + v(Py.F(y), Get(d, "h") is { } h ? Py.F(h) : 0), y));
                }
            }
        }
        if (Get(neu, "flur") is PyObjekt fl)
        {
            Deko(Get(fl, "deko"), u.Verschiebung);
        }
        if (Get(neu, "raeume") is PyObjekt raeume)
        {
            var alleRaeume = ListeOderLeer(alt, "raeume").Select(Objekt).ToDictionary(r => Py.Str(r["id"]));
            foreach (var (id, rz) in raeume)
            {
                if (rz is not PyObjekt r || !alleRaeume.TryGetValue(id, out var raum))
                {
                    continue;
                }
                var ry = Py.F(raum["y"]);
                var quert = ry < u.Unten - 1e-6 && ry + Py.F(raum["h"]) > u.Oben + 1e-6;
                Func<double, double, double> v = ry >= u.Unten - 1e-6 ? (_, _) => u.Zuwachs : quert ? u.Verschiebung : (_, _) => 0;
                Deko(Get(r, "deko"), v);
                if (Get(r, "plaetze") is PyObjekt pl)
                {
                    foreach (var (person, platz) in pl.ToList())
                    {
                        if (platz is List<object?> { Count: >= 2 } p)
                        {
                            p[1] = Flurbreite.Zahl(u.Hin(Py.F(p[1])), p[1]);
                        }
                    }
                }
            }
        }
        return neu;
    }

    /// <summary>Text der Datei (ein Ersatzstueck je Zeile, json wie Python mit ensure_ascii=False).</summary>
    public static string Schreiben(Daten daten)
    {
        var t = new StringBuilder();
        t.Append("{\"_hinweis\": [\"Welle A 1.2.0 (Weisung P12-334, Nico: Neue Grundrisse als Daten): breite Grundrisse (Diele 1,32 m, Flure 1,80 m, Innentueren 0,90 m).\", ");
        t.Append("\"Jedes Ersatzstueck gilt nur, wenn der alte Wert die Pruefsumme alt hat; erzeugt von GrundrisseBreit.Erzeugen, nie von Hand aendern.\"],\n");
        t.Append(CultureInfo.InvariantCulture, $"\"format\": {Format},\n\"wohnungen\": {{");
        t.Append(string.Join(", ", daten.Wohnungen.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => PythonJson.Standard(p.Key) + ": " + PythonJson.Standard(new PyObjekt
        {
            { "oben", Flurbreite.Zahl(p.Value.Oben, 0.0) }, { "unten", Flurbreite.Zahl(p.Value.Unten, 0.0) }, { "zuwachs", Flurbreite.Zahl(p.Value.Zuwachs, 0.0) },
        }))));
        t.Append("},\n\"ersatz\": [\n");
        t.Append(string.Join(",\n", daten.Ersatz.Select(e => PythonJson.Standard(new PyObjekt
        {
            { "bereich", e.Bereich }, { "pfad", e.Pfad.Select(p => (object?)p).ToList() }, { "alt", e.Alt }, { "neu", e.Neu },
        }))));
        t.Append("\n]}\n");
        return t.ToString();
    }
}
