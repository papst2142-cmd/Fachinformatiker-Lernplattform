using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.26.0 (Weisung P12-115, Meldung P12-75 Abschnitt 2, Nico Fragen 1 = a, 2 = a, Sammelliste 253): Laternen
/// automatisch entlang der Strassen, nur im Werkzeug.
/// <list type="bullet">
/// <item>Nach jeder Aenderung, die eine Strasse (auch Kurve, Zufahrt) setzt, zieht, schiebt, dreht oder loescht, verteilt das
/// Werkzeug die automatischen Laternen dieser Strassen neu (im selben Schritt wie die Aenderung, Rueckgaengig nimmt beides
/// zurueck); der Knopf „Laternen neu verteilen“ macht es fuer alle Strassen.</item>
/// <item>Abstand <see cref="Abstand"/> entlang der Strassenmitte, abwechselnd links und rechts, in Kurven nur aussen; jede
/// Laterne <see cref="Nachtlicht.Rand"/> neben der Mitte (wie heute). Nicht naeher als <see cref="ZuKreuzung"/> an einer
/// Kreuzung oder Einmuendung, nicht am Kreisverkehr (dort bleiben seine vier Laternen), nicht im Lichtraum des Gleises, nicht
/// auf einem Zebrastreifen; sonst die Regeln des Spiels (<see cref="Nachtlicht.Regeln"/>: Tuer, Weg zur Tuer, Haus, Baum,
/// Bruecke, Fluss …). Ist der Platz belegt, rueckt sie laengs der Strasse um hoechstens den halben Abstand, sonst faellt sie weg.</item>
/// <item>Selbst gesetzte Laternen bleiben immer; eine automatische haelt <see cref="ZuHand"/> Abstand zu ihnen. Eine
/// automatische, die Nico loescht oder verschiebt, kommt an ihrer alten Stelle nicht wieder (Teil <see cref="Teil"/> der
/// Stadt draussen, nur in der Projektdatei, nie in der Stadt-Datei).</item>
/// </list>
/// Das Spiel bleibt unveraendert: die Laternen stehen wie jede andere in der Liste <c>laternen</c>.
/// </summary>
public static class LaternenAutomatik
{
    /// <summary>Teil der Stadt draussen (nur Werkzeug): {gesetzt: [[x, z], …], weg: [[x, z], …]}.</summary>
    public const string Teil = "laternen_auto";

    /// <summary>Abstand entlang der Strassenmitte (Nico Frage 1 = a, wie Stadt 1 heute); je Seite also alle 7.</summary>
    public const float Abstand = 3.5f;

    /// <summary>Kleinster Abstand zu einer Kreuzung oder Einmuendung.</summary>
    public const float ZuKreuzung = 0.8f;

    /// <summary>Kleinster Abstand einer automatischen Laterne zu einer selbst gesetzten.</summary>
    public const float ZuHand = 1.5f;

    /// <summary>Gleiche Stelle (Laterne noch da, nicht verschoben).</summary>
    private const float Gleich = 0.005f;

    /// <summary>Schritt beim Ausweichen laengs der Strasse.</summary>
    private const float Ruecken = 0.05f;

    /// <summary>Gegenprobe: ohne Automatik (wie bis S 0.25.1).</summary>
    public static bool AusFuerTests
    {
        get => Aus.Value;
        set => Aus.Value = value;
    }

    private static readonly AsyncLocal<bool> Aus = new();

    /// <summary>Der Teil der Stadt draussen, bei Bedarf angelegt.</summary>
    public static PyObjekt Daten(PyObjekt aussen)
    {
        if (Get(aussen, Teil) is not PyObjekt d)
        {
            d = new PyObjekt();
            Py.Setzen(aussen, Teil, d);
        }
        // nach Rueckgaengig bis vor die erste automatische Laterne ist der Teil leer ({}): beide Listen anlegen
        foreach (var k in (string[])["gesetzt", "weg"])
        {
            if (Get(d, k) is not List<object?>)
            {
                Py.Setzen(d, k, new List<object?>());
            }
        }
        return d;
    }

    private static List<object?> Liste(PyObjekt aussen, string schluessel) => SpielDaten.Liste(Daten(aussen)[schluessel]);

    private static List<Vector2> Punkte(PyObjekt aussen, string schluessel) =>
        [.. Liste(aussen, schluessel).OfType<List<object?>>().Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1])))];

    /// <summary>Stellen der automatischen Laternen.</summary>
    public static List<Vector2> Gesetzt(PyObjekt aussen) => Get(aussen, Teil) is PyObjekt d && Get(d, "gesetzt") is List<object?> ? Punkte(aussen, "gesetzt") : [];

    /// <summary>Stellen geloeschter oder verschobener automatischer Laternen (dort kommt keine wieder).</summary>
    public static List<Vector2> Weg(PyObjekt aussen) => Get(aussen, Teil) is PyObjekt d && Get(d, "weg") is List<object?> ? Punkte(aussen, "weg") : [];

    /// <summary>Ist die Laterne Nr. (0-basiert) eine automatische?</summary>
    public static bool IstAutomatisch(PyObjekt aussen, int nummer)
    {
        var l = Aussen.LaternenWerte(aussen);
        return nummer >= 0 && nummer < l.Count && Gesetzt(aussen).Any(g => Vector2.Distance(g, new Vector2(l[nummer].X, l[nummer].Z)) < Gleich);
    }

    /// <summary>Strassen (ohne Ring eines Kreisverkehrs) als Linien, fuer den Vergleich vorher/nachher.</summary>
    public static List<List<Vector2>> Strassen(PyObjekt aussen) =>
        [.. Aussen.Landschaft(aussen).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "strasse") && !Kreisverkehr.IstRing(t)).Select(t => Linie(t["punkte"]))];

    private static List<Vector2> Linie(object? punkte) =>
        [.. SpielDaten.Liste(punkte).Select(p => SpielDaten.Liste(p)).Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1])))];

    /// <summary>
    /// Nach jeder Aenderung (vor dem Neuverteilen): eine automatische Laterne, die nicht mehr an ihrer Stelle steht (geloescht
    /// oder verschoben), merkt sich das Projekt als „weg“; verschoben ist sie danach eine selbst gesetzte.
    /// </summary>
    public static void Abgleichen(PyObjekt aussen)
    {
        if (Get(aussen, Teil) is not PyObjekt)
        {
            return;
        }
        var stehen = Aussen.LaternenWerte(aussen).Select(l => new Vector2(l.X, l.Z)).ToList();
        var gesetzt = Liste(aussen, "gesetzt");
        var weg = Liste(aussen, "weg");
        for (var i = gesetzt.Count - 1; i >= 0; i--)
        {
            var p = SpielDaten.Liste(gesetzt[i]);
            var g = new Vector2((float)Py.F(p[0]), (float)Py.F(p[1]));
            if (!stehen.Any(s => Vector2.Distance(s, g) < Gleich))
            {
                gesetzt.RemoveAt(i);
                weg.Add(p);
            }
        }
    }

    /// <summary>
    /// Nach einer Aenderung: automatische Laternen an weggefallenen oder geaenderten Strassen entfernen, die uebrigen
    /// nachpruefen (steht jetzt etwas an ihrer Stelle, etwa eine Tuer, ein Weg oder ein Zebrastreifen, ruecken sie laengs der
    /// Strasse bis zum halben Abstand aus, sonst fallen sie weg, ohne als „weg“ gemerkt zu werden) und an den neuen oder
    /// geaenderten Strassen neu verteilen. <paramref name="sonstGeaendert"/> = ausser den Laternen hat sich etwas geaendert
    /// (nur dann wird nachgeprueft). <paramref name="szene"/> baut die Stadt ohne Laternen (fuer die Regeln des Spiels).
    /// Ergebnis: aufgestellt, entfernt.
    /// </summary>
    public static (int Dazu, int Weg) NachAenderung(PyObjekt aussen, List<List<Vector2>> vorher, Func<PyObjekt, StadtSzene?> szene,
        bool sonstGeaendert = true)
    {
        var jetzt = Strassen(aussen);
        var neu = jetzt.Where(l => !vorher.Any(v => v.SequenceEqual(l))).ToList();
        var alt = vorher.Where(v => !jetzt.Any(l => l.SequenceEqual(v))).ToList();
        if (neu.Count == 0 && alt.Count == 0 && (!sonstGeaendert || Gesetzt(aussen).Count == 0))
        {
            return (0, 0);
        }
        // automatische Laternen, deren Strasse (vorher) weggefallen oder geaendert ist
        var entfernt = Entfernen(aussen, g => vorher.Count > 0 && alt.Any(a => ReferenceEquals(a, Naechste(vorher, g))));
        if (Platz.Fuer(aussen, szene) is not { } platz)
        {
            return (0, entfernt);
        }
        var (dazu, weg) = platz.Nachpruefen();
        return (dazu + platz.Aufstellen(neu.Where(l => l.Count >= 2).ToList()), entfernt + weg);
    }

    /// <summary>Knopf „Laternen neu verteilen“: alle automatischen weg, an allen Strassen neu (geloeschte bleiben weg).</summary>
    public static (int Dazu, int Weg) Verteilen(PyObjekt aussen, Func<PyObjekt, StadtSzene?> szene)
    {
        var entfernt = Entfernen(aussen, _ => true);
        return (Platz.Fuer(aussen, szene)?.Aufstellen([.. Strassen(aussen).Where(l => l.Count >= 2)]) ?? 0, entfernt);
    }

    private static List<Vector2> Naechste(List<List<Vector2>> strassen, Vector2 p) =>
        strassen.OrderBy(s => s.Count >= 2 ? Kurve.Abstand(p, s) : float.MaxValue).First();

    /// <summary>Automatische Laternen entfernen, auf die <paramref name="welche"/> zutrifft (nicht als „weg“ gemerkt).</summary>
    private static int Entfernen(PyObjekt aussen, Func<Vector2, bool> welche)
    {
        if (Get(aussen, Teil) is not PyObjekt)
        {
            return 0;
        }
        var gesetzt = Liste(aussen, "gesetzt");
        var laternen = Aussen.Laternen(aussen);
        var zahl = 0;
        for (var i = gesetzt.Count - 1; i >= 0; i--)
        {
            var p = SpielDaten.Liste(gesetzt[i]);
            var g = new Vector2((float)Py.F(p[0]), (float)Py.F(p[1]));
            if (!welche(g))
            {
                continue;
            }
            gesetzt.RemoveAt(i);
            var nr = laternen.FindIndex(l => l is List<object?> q && Vector2.Distance(new Vector2((float)Py.F(q[0]), (float)Py.F(q[1])), g) < Gleich);
            if (nr >= 0)
            {
                laternen.RemoveAt(nr);
                zahl++;
            }
        }
        return zahl;
    }

    /// <summary>Die Regeln fuer den Platz einer automatischen Laterne an der gebauten Stadt ohne Laternen.</summary>
    private sealed class Platz
    {
        private readonly PyObjekt _aussen;
        private readonly StadtSzene _szene;
        private readonly Nachtlicht.Regeln _regeln;
        private readonly List<IReadOnlyList<Vector2>> _alleStrassen;
        private readonly List<Vector2> _kreuzungen;
        private readonly List<(Vector2 Mitte, float Rand)> _ringe;
        private readonly List<Vector2> _hand;
        private readonly List<Vector2> _auto;
        private readonly List<Vector2> _weg;

        private Platz(PyObjekt aussen, StadtSzene szene)
        {
            _aussen = aussen;
            _szene = szene;
            _regeln = new Nachtlicht.Regeln(szene);
            var landschaft = Aussen.Landschaft(aussen).OfType<PyObjekt>().ToList();
            _alleStrassen = landschaft.Where(t => Py.Gleich(t["typ"], "strasse")).Select(t => (IReadOnlyList<Vector2>)Linie(t["punkte"])).ToList();
            _kreuzungen = Verkehr.KreuzungenAus(_alleStrassen);
            _ringe = Kreisverkehr.Alle(landschaft.Cast<object?>()).Select(r => (Kreisverkehr.Mitte(r.Ring), (float)r.Radius + (StadtSzene.StrassenBreite / 2)
                + StadtSzene.GehwegBreite(landschaft.First(t => Kreisverkehr.IstRing(t) && Linie(t["punkte"]).SequenceEqual(r.Ring))))).ToList();
            var gesetzt = Gesetzt(aussen);
            var stehen = Aussen.LaternenWerte(aussen).Select(l => new Vector2(l.X, l.Z)).ToList();
            _hand = stehen.Where(p => !gesetzt.Any(g => Vector2.Distance(g, p) < Gleich)).ToList();
            _auto = stehen.Where(p => gesetzt.Any(g => Vector2.Distance(g, p) < Gleich)).ToList();
            _weg = Weg(aussen);
        }

        /// <summary>Null = die Stadt laesst sich nicht bauen (dann keine automatischen Laternen).</summary>
        public static Platz? Fuer(PyObjekt aussen, Func<PyObjekt, StadtSzene?> szeneBauen)
        {
            var kopie = (PyObjekt)Py.Kopie(aussen)!;
            Aussen.Laternen(kopie).Clear();
            return szeneBauen(kopie) is { } szene ? new Platz(aussen, szene) : null;
        }

        private bool Frei(Vector2 q, IReadOnlyList<Vector2> eigene)
        {
            if (!_regeln.Pruefen(q).Frei
                || _kreuzungen.Any(k => Vector2.Distance(q, k) < ZuKreuzung)
                || _ringe.Any(r => Vector2.Distance(q, r.Mitte) < r.Rand + ZuKreuzung)
                || _szene.Gleise.Any(g => Kurve.Abstand(q, g) < Bahnuebergang.LichtraumHalb + Nachtlicht.AbstandModell + 0.05f)
                || _szene.Zebras.Any(z => z.Drauf(q, StadtbauPruefung.ZebraFreiLaengs + 0.05f, StadtbauPruefung.ZebraFreiQuer + 0.05f))
                || _szene.Ampeln.Any(a => a.Masten.Any(m => Vector2.Distance(q, m.Fuss) < Ampeln.MastGrundriss + 0.05f))
                || _szene.Haltestellen.Any(h => Vector2.Distance(q, h.Schild) < StadtbauPruefung.HaltLaterneAbstand)
                || _szene.Parkplaetze.Any(pp => Kurve.Abstand(q, [pp.Einfahrt.A, pp.Einfahrt.B]) < (Parkplaetze.Gasse / 2) + 0.15f)
                || _hand.Any(h => Vector2.Distance(q, h) < ZuHand)
                || _auto.Any(a => Vector2.Distance(q, a) < Nachtlicht.Zwischen))
            {
                return false;
            }
            // das Spiel muss sie wieder an genau diese Strasse und Stelle legen (Nachtlicht.StrasseVon)
            var (strasse, _, _) = Nachtlicht.StrasseVon(_alleStrassen, q);
            return strasse.SequenceEqual(eigene) && MathF.Abs(Kurve.Abstand(q, eigene) - Nachtlicht.Rand) < 0.01f;
        }

        /// <summary>
        /// Die naechste freie Stelle bei <paramref name="s0"/> (laengs der Strasse, in Schritten von <see cref="Ruecken"/> bis zum
        /// halben Abstand); null = keine.
        /// </summary>
        private Vector2? Suchen(List<Vector2> linie, float laenge, float s0, float seite)
        {
            for (var j = 0; j * Ruecken <= Abstand / 2; j++)
            {
                foreach (var s in j == 0 ? (float[])[s0] : [s0 + (j * Ruecken), s0 - (j * Ruecken)])
                {
                    if (s < 0 || s > laenge)
                    {
                        continue;
                    }
                    var (p, d) = Punkt(linie, s);
                    var q = p + (new Vector2(-d.Y, d.X) * (Aussenseite(linie, s) ?? seite) * Nachtlicht.Rand);
                    q = new Vector2(MathF.Round(q.X, 3), MathF.Round(q.Y, 3));
                    if (Frei(q, linie))
                    {
                        return q;
                    }
                }
            }
            return null;
        }

        private void Hinzu(Vector2 neu)
        {
            Aussen.Laternen(_aussen).Add(new List<object?> { Aussen.Zahl(neu.X), Aussen.Zahl(neu.Y), false });
            SpielDaten.Liste(Daten(_aussen)["gesetzt"]).Add(new List<object?> { Aussen.Zahl(neu.X), Aussen.Zahl(neu.Y) });
            _auto.Add(neu);
        }

        /// <summary>Laternen an den Strassen <paramref name="strassen"/> aufstellen; Ergebnis = Zahl der neuen.</summary>
        public int Aufstellen(List<List<Vector2>> strassen)
        {
            var zahl = 0;
            foreach (var linie in strassen)
            {
                var laenge = Laenge(linie);
                for (var k = 0; (Abstand / 2) + (k * Abstand) <= laenge; k++)
                {
                    var s0 = (Abstand / 2) + (k * Abstand);
                    var (p0, d0) = Punkt(linie, s0);
                    var seite = Aussenseite(linie, s0) ?? (k % 2 == 0 ? 1f : -1f);
                    var wunsch = p0 + (new Vector2(-d0.Y, d0.X) * seite * Nachtlicht.Rand);
                    if (_weg.Any(w => Vector2.Distance(w, wunsch) < Abstand / 2))
                    {
                        continue;
                    }
                    if (Suchen(linie, laenge, s0, seite) is { } neu)
                    {
                        Hinzu(neu);
                        zahl++;
                    }
                }
            }
            return zahl;
        }

        /// <summary>
        /// Jede automatische Laterne, deren Platz nicht mehr frei ist, rueckt laengs ihrer Strasse aus (gleiche Seite, bis zum
        /// halben Abstand) oder faellt weg. Ergebnis: neu gestellt, entfernt.
        /// </summary>
        public (int Dazu, int Weg) Nachpruefen()
        {
            var strassen = Strassen(_aussen).Where(l => l.Count >= 2).ToList();
            var (dazu, weg) = (0, 0);
            foreach (var g in _auto.ToList())
            {
                _auto.Remove(g);
                var (strasse, _, _) = Nachtlicht.StrasseVon(_alleStrassen, g);
                var linie = strassen.FirstOrDefault(l => l.SequenceEqual(strasse));
                if (linie is not null && Frei(g, linie))
                {
                    _auto.Add(g);
                    continue;
                }
                Entfernen(_aussen, q => Vector2.Distance(q, g) < Gleich);
                weg++;
                if (linie is null)
                {
                    continue;
                }
                var (s0, seite) = Lot(linie, g);
                if (Suchen(linie, Laenge(linie), s0, seite) is { } neu)
                {
                    Hinzu(neu);
                    dazu++;
                }
            }
            return (dazu, weg);
        }
    }

    /// <summary>Lage eines Punkts an der Linie: Weg vom Anfang bis zum Lotfuss und Seite (+1 links, -1 rechts der Richtung).</summary>
    private static (float S, float Seite) Lot(List<Vector2> linie, Vector2 q)
    {
        var (best, s, seite, summe) = (float.MaxValue, 0f, 1f, 0f);
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var l = Vector2.Distance(linie[i], linie[i + 1]);
            if (l < 1e-4f)
            {
                continue;
            }
            var d = (linie[i + 1] - linie[i]) / l;
            var t = Math.Clamp(Vector2.Dot(q - linie[i], d), 0, l);
            var fuss = linie[i] + (d * t);
            var a = Vector2.Distance(q, fuss);
            if (a < best)
            {
                (best, s) = (a, summe + t);
                seite = Vector2.Dot(q - fuss, new Vector2(-d.Y, d.X)) >= 0 ? 1f : -1f;
            }
            summe += l;
        }
        return (s, seite);
    }

    /// <summary>In einer Kurve die Aussenseite (+1 links, -1 rechts der Richtung); auf einem geraden Stueck null.</summary>
    private static float? Aussenseite(List<Vector2> linie, float s)
    {
        if (linie.Count <= 2)
        {
            return null;
        }
        var (_, a) = Punkt(linie, MathF.Max(0, s - 0.3f));
        var (_, b) = Punkt(linie, s + 0.3f);
        var kreuz = (a.X * b.Y) - (a.Y * b.X);
        return MathF.Abs(kreuz) < 0.01f ? null : kreuz > 0 ? -1f : 1f;
    }

    private static float Laenge(List<Vector2> linie)
    {
        var summe = 0f;
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            summe += Vector2.Distance(linie[i], linie[i + 1]);
        }
        return summe;
    }

    private static (Vector2 Punkt, Vector2 Richtung) Punkt(List<Vector2> linie, float s)
    {
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var l = Vector2.Distance(linie[i], linie[i + 1]);
            if (l < 1e-4f)
            {
                continue;
            }
            var d = (linie[i + 1] - linie[i]) / l;
            if (s <= l || i + 2 == linie.Count)
            {
                return (linie[i] + (d * MathF.Min(s, l)), d);
            }
            s -= l;
        }
        return (linie[0], Vector2.UnitX);
    }
}
