using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.52.0 (Weisung P12-231 Punkt 5 = Kurzplan P12-131 Punkt 5, Weisung P12-242): Knopf „Alle beheben, die sicher
/// gehen“ oben in der Liste „Stadt – n rot“. Er behebt nur rote Funde, fuer die es einen sicheren Vorschlag gibt:
/// <list type="bullet">
/// <item>Laterne versetzen (derselbe Vorschlag wie der Knopf am Fund, <see cref="LaterneVersetzen.Suchen"/>),</item>
/// <item>Tür auf die naechste glatte Stelle (Tuer „sitzt nicht sauber in der Wand“, <see cref="TuerFassade.Finden"/> mit Einrasten),</item>
/// <item>Weg neu an die Strasse (Fussweg „erreicht keine Straße“: Tuer bleibt, der Weg wird neu gerechnet wie bei „Tür setzen“).</item>
/// </list>
/// Jede Behebung wird nur uebernommen, wenn danach ihr Fund weg ist und kein neuer roter Fund entsteht (gleiche Rechnung wie
/// „Laterne versetzen“: ganze Pruefung draussen mit den automatischen Laternen, die der echte Schritt aufstellen wuerde).
/// Alles zusammen ist EIN Schritt (<see cref="Anwenden"/>), danach „n behoben, m bleiben“.
/// </summary>
public static class AlleBeheben
{
    /// <summary>Was an einem roten Fund sicher behoben werden kann.</summary>
    public enum Art
    {
        Laterne,
        Tuer,
        Weg,
    }

    /// <summary>So nah muss die Tuer eines Hauses an der Stelle des Funds „Tür nicht sauber“ liegen.</summary>
    public const float TuerNaehe = 1.0f;

    /// <summary>So nah muss das Ende eines Fusswegs an der Stelle des Funds „erreicht keine Straße“ liegen.</summary>
    public const float WegNaehe = 0.05f;

    /// <summary>Eine gepruefte Behebung: Fund, Art und die Aenderung (auf dem Stand vor ihr wiederholbar).</summary>
    public sealed record Behebung(StadtbauFund Fund, Art Art, Func<PyObjekt, bool> Aendern);

    /// <summary>
    /// Ergebnis der Suche: die uebernommenen Behebungen (in dieser Reihenfolge anzuwenden), die roten Funde danach (bleiben) und
    /// der Stand, auf dem gerechnet wurde (als Text je Teil; hat sich die Stadt inzwischen geaendert, gilt das Ergebnis nicht).
    /// </summary>
    public sealed record Ergebnis(IReadOnlyList<Behebung> Behebungen, int Bleiben, Dictionary<string, string> Stand)
    {
        public int Behoben => Behebungen.Count;
    }

    /// <summary>Gegenprobe (nur Tests, je Ablauf): Behebungen ohne Nachpruefung uebernehmen (wie ein Knopf, der blind setzt).</summary>
    public static bool OhneNachpruefungFuerTests { get => OhneNachpruefung.Value; set => OhneNachpruefung.Value = value; }
    private static readonly AsyncLocal<bool> OhneNachpruefung = new();

    /// <summary>Nur Tests (je Ablauf): stoert jede Behebung zusaetzlich (z. B. eine Laterne auf die Fahrbahn), fuer die Gegenprobe der Nachpruefung.</summary>
    internal static readonly AsyncLocal<Action<PyObjekt>?> StoerungFuerTests = new();

    /// <summary>Art der sicheren Behebung fuer den Fund <paramref name="f"/>, null = keine.</summary>
    public static Art? ArtVon(PyObjekt aussen, StadtbauFund f)
    {
        if (!f.Rot)
        {
            return null;
        }
        if (LaterneVersetzen.Nummer(aussen, f) is not null)
        {
            return Art.Laterne;
        }
        if (IstTuerFund(f) && TuerHaus(aussen, f) is not null)
        {
            return Art.Tuer;
        }
        if (IstWegFund(f) && WegHaus(aussen, f) is not null)
        {
            return Art.Weg;
        }
        return null;
    }

    private static bool IstTuerFund(StadtbauFund f) =>
        f.Text.StartsWith("Die Tür von „", StringComparison.Ordinal) && f.Text.Contains("“ sitzt nicht sauber in der Wand", StringComparison.Ordinal);

    private static bool IstWegFund(StadtbauFund f) =>
        f.Text.StartsWith("Der Fußweg von „", StringComparison.Ordinal) && f.Text.EndsWith("“ erreicht keine Straße.", StringComparison.Ordinal);

    /// <summary>Alle Haeuser mit Tuer: Orte und Gebaeude ohne Ort mit Zierweg.</summary>
    private static IEnumerable<AussenStelle> HaeuserMitTuer(PyObjekt aussen)
    {
        foreach (var o in AussenBearbeitung.Orte(aussen))
        {
            if (o["id"] is string id)
            {
                yield return new AussenStelle(AussenArt.Haus, -1, 0, id);
            }
        }
        var land = Aussen.Landschaft(aussen);
        for (var i = 0; i < land.Count; i++)
        {
            if (land[i] is PyObjekt g && Py.Gleich(g["typ"], "gebaeude"))
            {
                var s = new AussenStelle(AussenArt.Gebaeude, i);
                if (Blueprint.ZierwegVon(aussen, s) is not null)
                {
                    yield return s;
                }
            }
        }
    }

    private static PyObjekt? Zugang(PyObjekt aussen, AussenStelle s) =>
        s.Art == AussenArt.Haus ? AussenBearbeitung.Zugang(aussen, s.Ort) : Blueprint.ZierwegVon(aussen, s);

    /// <summary>Haus, dessen Tuer der Fund „Tür nicht sauber“ meint (naechste Tuer bis <see cref="TuerNaehe"/>).</summary>
    internal static AussenStelle? TuerHaus(PyObjekt aussen, StadtbauFund f)
    {
        if (f.X is not { } x || f.Y is not { } y)
        {
            return null;
        }
        var p = new Vector2((float)x, (float)y);
        return HaeuserMitTuer(aussen).Select(s => (s, t: TuerSetzen.Tuer(aussen, s))).Where(z => z.t is { } t && Vector2.Distance(t, p) <= TuerNaehe)
            .OrderBy(z => Vector2.Distance(z.t!.Value, p)).Select(z => (AussenStelle?)z.s).FirstOrDefault();
    }

    /// <summary>Haus, dessen Fussweg der Fund „erreicht keine Straße“ meint (Wegende an der Stelle des Funds).</summary>
    internal static AussenStelle? WegHaus(PyObjekt aussen, StadtbauFund f)
    {
        if (f.X is not { } x || f.Y is not { } y)
        {
            return null;
        }
        var p = new Vector2((float)x, (float)y);
        foreach (var s in HaeuserMitTuer(aussen))
        {
            if (Zugang(aussen, s)?["weg"] is List<object?> { Count: >= 2 } weg && weg[^1] is List<object?> e
                && Vector2.Distance(new Vector2((float)Py.F(e[0]), (float)Py.F(e[1])), p) <= WegNaehe)
            {
                return s;
            }
        }
        return null;
    }

    /// <summary>
    /// „Weg neu an die Straße“: Tuer und das erste gerade Stueck bleiben, der Rest des Wegs wird neu zur naechsten Strasse
    /// gerechnet (dieselbe Rechnung wie „Tür setzen“). true = Strasse erreicht.
    /// </summary>
    public static bool WegNeu(PyObjekt aussen, AussenStelle s)
    {
        if (Zugang(aussen, s) is not { } z || z["weg"] is not List<object?> { Count: >= 2 } weg)
        {
            return false;
        }
        var neu = new List<object?> { Py.Kopie(weg[0]), Py.Kopie(weg[1]) };
        Py.Setzen(z, "weg", neu);
        z.RemoveAll(e => e.Key == "radien");
        return TuerSetzen.WegZurStrasse(aussen, s, z);
    }

    /// <summary>Schrittweite, in der <see cref="NaechsteGlatte"/> die Wand von der Tuer aus nach beiden Seiten absucht.</summary>
    public const float GlattSchritt = 0.1f;

    /// <summary>
    /// „Tür auf die naechste glatte Stelle“: auf derselben Wand von der Tuer aus abwechselnd nach beiden Seiten die naechste
    /// Stelle, an der <see cref="TuerFassade.Finden"/> (mit Einrasten) eine glatte Stelle oder eine Tuer des Modells liefert.
    /// Ein Fenster wird dabei nie zur Tuer (das aenderte das Gebaeude, nicht nur die Lage der Tuer). null = keine.
    /// </summary>
    public static TuerFassade.Platz? NaechsteGlatte(TuerFassade fa, Vector2 tuer)
    {
        var w = fa.WandFuer(tuer, null);
        var wand = fa.Waende[w];
        var t0 = Vector2.Dot(tuer - wand.A, wand.Richtung);
        for (var d = 0f; d <= wand.Laenge + GlattSchritt; d += GlattSchritt)
        {
            foreach (var t in d == 0 ? [t0] : new[] { t0 - d, t0 + d })
            {
                if (t < 0 || t > wand.Laenge)
                {
                    continue;
                }
                var platz = fa.Finden(wand.Bei(t) + (wand.Aussen * 0.01f), w, einrasten: true);
                if (platz.Wand == w && platz.Fensterteil is null && !platz.OhneGlatte
                    && platz.Art is TuerFassade.Stelle.Glatt or TuerFassade.Stelle.Modelltuer)
                {
                    return platz;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Sichere Behebungen suchen (blockiert, im Hintergrund rufen). <paramref name="stand"/> ist eine Kopie der Stadt draussen,
    /// <paramref name="laternenVorschau"/> setzt die automatischen Laternen wie der echte Schritt
    /// (<see cref="Projekt.LaternenFuerVorschau"/>), <paramref name="fassade"/> rechnet die Fassade eines Hauses
    /// (<see cref="TuerFassade.Rechnen"/>).
    /// </summary>
    public static Ergebnis Suchen(PyObjekt stand, StadtFassung basis, PyObjekt innen, Spieldaten daten, Func<PyObjekt, StadtSzene?> szene,
        Func<PyObjekt, PyObjekt> laternenVorschau, Func<PyObjekt, AussenStelle, TuerFassade?> fassade, CancellationToken abbruch = default)
    {
        var start = Aussen.TeileText(stand);
        var k = (PyObjekt)Py.Kopie(stand)!;
        List<StadtbauFund> Pruefen(PyObjekt a) => Bauen3D.Pruefen(laternenVorschau(a), basis, innen, daten);
        var funde = Pruefen(k);
        var behebungen = new List<Behebung>();
        foreach (var f in funde.Where(f => f.Rot).ToList())
        {
            abbruch.ThrowIfCancellationRequested();
            // ist der Fund schon durch eine fruehere Behebung weg (z. B. zwei Funde derselben Laterne), nichts tun
            if (!funde.Any(x => x.Rot && x.Text == f.Text))
            {
                continue;
            }
            Func<PyObjekt, bool>? aendern = null;
            Art? art = ArtVon(k, f);
            switch (art)
            {
                case Art.Laterne:
                    var nummer = LaterneVersetzen.Nummer(k, f)!.Value;
                    var (v, _) = LaterneVersetzen.Suchen((PyObjekt)Py.Kopie(k)!, basis, innen, daten, szene, nummer, abbruch);
                    if (v is not null)
                    {
                        aendern = a => LaterneVersetzen.Anwenden(a, v);
                    }
                    break;
                case Art.Tuer:
                    var s = TuerHaus(k, f)!;
                    if (TuerSetzen.Tuer(k, s) is { } tuer && fassade(k, s) is { Waende.Count: > 0 } fa && NaechsteGlatte(fa, tuer) is { } platz)
                    {
                        aendern = a => TuerSetzen.SetzenAn(a, s, platz) is null or Texte.TuerEingerastetGlatt;
                    }
                    break;
                case Art.Weg:
                    var h = WegHaus(k, f)!;
                    aendern = a => WegNeu(a, h);
                    break;
            }
            if (aendern is null)
            {
                continue;
            }
            var c = (PyObjekt)Py.Kopie(k)!;
            if (!aendern(c))
            {
                continue;
            }
            StoerungFuerTests.Value?.Invoke(c);
            var nachher = Pruefen(c);
            var vorherZahl = funde.Count(x => x.Rot && x.Text == f.Text);
            var nachherZahl = nachher.Count(x => x.Rot && x.Text == f.Text);
            if (!OhneNachpruefung.Value && (nachherZahl >= vorherZahl || Bauen3D.NeuRot(funde, nachher) is not null))
            {
                continue;
            }
            behebungen.Add(new Behebung(f, art!.Value, aendern));
            k = c;
            funde = nachher;
        }
        return new Ergebnis(behebungen, funde.Count(f => f.Rot), start);
    }

    /// <summary>
    /// Die Behebungen auf <paramref name="aussen"/> anwenden (in <c>Projekt.AussenAendern</c>, ein Schritt). Hat sich die Stadt
    /// seit der Suche geaendert, passiert nichts: false.
    /// </summary>
    public static bool Anwenden(PyObjekt aussen, Ergebnis e)
    {
        var jetzt = Aussen.TeileText(aussen);
        if (jetzt.Count != e.Stand.Count || jetzt.Any(kv => !e.Stand.TryGetValue(kv.Key, out var v) || v != kv.Value))
        {
            return false;
        }
        foreach (var b in e.Behebungen)
        {
            b.Aendern(aussen);
        }
        return e.Behoben > 0;
    }
}
