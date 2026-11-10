using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.33.0 „Tür frei setzen“ (Weisung P12-148, Nico 1a 2a 3a 4a; Sammelliste 272): die Tuer kommt auf die gezeichnete
/// Fassade, so wie das Spiel sie zeichnet.
/// <list type="bullet">
/// <item>1 = a: Waende sind die gezeichneten Bloecke des Hauses (<see cref="StadtSzene.OrtBloecke"/>, Gebaeude ohne Ort: sein
/// Kasten), nicht das Rechteck der Daten. Je Stelle rechnet die Fassade wie das Spiel (<see cref="StadtTueren.Wand"/>,
/// <see cref="StadtTueren.EigeneTuer"/>, <see cref="StadtTueren.Wandtiefe"/>) und prueft die Flaeche der Tuer wie Regel 262
/// (<see cref="StadtTueren.Flaeche"/>).</item>
/// <item>3 = a: die Wand unter dem Finger zaehlt; hinter dem Ende einer Wand bleibt die Tuer an DIESER Wand,
/// <see cref="TuerSetzen.EckAbstand"/> vor der Ecke (nie die Nachbarwand).</item>
/// <item>4 = a: mit „Einrasten“ springt eine Tuer auf eine nicht glatte Stelle (Schaufenster, Nische, Vorbau) an die naechste
/// glatte Stelle derselben Wand oder auf eine Tuer des Modells (gelber Hinweis). Gibt es keine, bleibt sie, wo getippt wurde;
/// Regel 262 bleibt dann rot.</item>
/// <item>2 = a: an einem eigenen Gebaeude aus dem Editor ist ein Fensterteil im Erdgeschoss eine erlaubte Stelle: die Tuer kommt
/// auf seine Mitte, das Setzen tauscht es gegen das Tuerteil (<see cref="TuerSetzen.SetzenAn"/>).</item>
/// </list>
/// Die Szene muss ohne den Versatz <c>darstellung.modell_tuer</c> dieses Orts gebaut sein (<see cref="StandOhneVersatz"/>): das
/// Setzen nimmt ihn immer weg. Nur Werkzeug; das Spiel liest nur die gespeicherte Tuer und bleibt unveraendert.
/// </summary>
public sealed class TuerFassade
{
    /// <summary>Abstand der Proben laengs einer Wand.</summary>
    public const float Schritt = 0.05f;

    /// <summary>Was an einer Stelle der Wand passiert, wenn die Tuer dort steht.</summary>
    public enum Stelle
    {
        /// <summary>gezeichnete Tuer liegt buendig (Regel 262 gruen)</summary>
        Glatt,

        /// <summary>die eigene Tuer des Modells ist die Tuer (keine zweite davor)</summary>
        Modelltuer,

        /// <summary>Fensterteil eines eigenen Gebaeudes: wird beim Setzen zum Tuerteil</summary>
        Fenster,

        /// <summary>nicht buendig (Fenster, Nische, Vorbau): Regel 262 waere rot</summary>
        Nicht,
    }

    /// <summary>Eine Wand eines gezeichneten Blocks: von A nach B, Richtung hinaus.</summary>
    public sealed record Wand(Vector2 A, Vector2 B, Vector2 Aussen, int Block)
    {
        public float Laenge => Vector2.Distance(A, B);

        public Vector2 Richtung => (B - A) / MathF.Max(Laenge, 1e-6f);

        public Vector2 Bei(float t) => A + (Richtung * t);
    }

    /// <summary>Ergebnis fuer einen Tipp: Tuerpunkt (auf der Wand, gespeichert), gezeichnete Mitte, Art und ob verschoben.</summary>
    public sealed record Platz(Vector2 Punkt, Vector2 Aussen, int Wand, Stelle Art, bool Verschoben, bool OhneGlatte, int? Fensterteil, Vector2 Gezeichnet);

    private sealed record Probe(float T, Stelle Art, Vector2 Gezeichnet, int? Teil);

    /// <summary>Gegenprobe: altes Setzen auf das Rechteck der Daten (nur in Tests, je Ablauf).</summary>
    public static bool AltFuerTests { get => Alt.Value; set => Alt.Value = value; }
    private static readonly AsyncLocal<bool> Alt = new();

    public IReadOnlyList<Wand> Waende => _waende;

    /// <summary>Gezeichnete Bloecke des Hauses (x1, z1, x2, z2).</summary>
    public IReadOnlyList<(float X1, float Z1, float X2, float Z2)> Bloecke => _bloecke;

    private readonly List<Wand> _waende = [];
    private readonly List<(float X1, float Z1, float X2, float Z2)> _bloecke = [];
    private readonly List<List<Probe>> _proben = [];
    private readonly List<List<(float T, Vector2 Gezeichnet)>> _modellTueren = [];
    private readonly List<List<(float T, int Teil, Vector2 Gezeichnet)>> _fenster = [];

    /// <summary>Kopie des Stands ohne den Versatz <c>darstellung.modell_tuer</c> des Orts (so steht das Modell nach dem Setzen).</summary>
    public static PyObjekt StandOhneVersatz(PyObjekt aussen, AussenStelle s)
    {
        var k = (PyObjekt)Py.Kopie(aussen)!;
        if (s.Art == AussenArt.Haus && Get(Objekt(Get(Aussen.Stadtdaten(k), "darstellung") ?? new PyObjekt()), "modell_tuer") is PyObjekt mt)
        {
            mt.RemoveAll(e => e.Key == s.Ort);
        }
        return k;
    }

    /// <summary>Fassade des Hauses <paramref name="s"/> aus dem Projekt (baut die Szene, darf im Hintergrund laufen); null ohne Haus.</summary>
    public static TuerFassade? Rechnen(Projekt p, PyObjekt aussen, AussenStelle s)
    {
        var stand = StandOhneVersatz(aussen, s);
        return p.SzeneOhneLaternen(stand, mitGebaeuden: true) is { } szene ? Aus(szene, stand, s, p.Daten) : null;
    }

    /// <summary>Fassade des Hauses <paramref name="s"/> in der Szene (gebaut aus <see cref="StandOhneVersatz"/>), null ohne Haus.</summary>
    public static TuerFassade? Aus(StadtSzene szene, PyObjekt aussen, AussenStelle s, Spieldaten? daten = null)
    {
        if (szene.Grafik is not { } grafik)
        {
            return null;
        }
        StadtHaus? haus;
        var f = new TuerFassade();
        float hoehe;
        string modell;
        if (s.Art == AussenArt.Haus)
        {
            haus = szene.Haeuser.FirstOrDefault(h => h.Id == s.Ort);
            if (haus is null || !szene.OrtBloecke.TryGetValue(s.Ort, out var bl) || bl.Count == 0)
            {
                return null;
            }
            f._bloecke.AddRange(bl.Select(b => ((float)Math.Min(b.X1, b.X2), (float)Math.Min(b.Z1, b.Z2), (float)Math.Max(b.X1, b.X2), (float)Math.Max(b.Z1, b.Z2))));
            hoehe = szene.Tueren.FirstOrDefault(t => t.Ort == s.Ort) is { Hoehe: > 0 } t0 ? t0.Hoehe : MathF.Min(StadtSzene.TuerHoehe, haus.Max.Y * 0.7f);
            modell = Py.Str(AussenBearbeitung.Ort(aussen, s.Ort)!["modell"]);
        }
        else
        {
            modell = Py.Str(Objekt(Aussen.Landschaft(aussen)[s.Nummer])["modell"]);
            var (x, y, w, hh) = AussenBearbeitung.HausFlaeche(aussen, s);
            var m = new Vector2((float)(x + (w / 2)), (float)(y + (hh / 2)));
            haus = szene.Haeuser.Where(h => h.Id.StartsWith(modell + "#", StringComparison.Ordinal))
                .OrderBy(h => Vector2.Distance(m, new Vector2((h.Min.X + h.Max.X) / 2, (h.Min.Z + h.Max.Z) / 2))).FirstOrDefault();
            if (haus is null)
            {
                return null;
            }
            f._bloecke.Add((haus.Min.X, haus.Min.Z, haus.Max.X, haus.Max.Z));
            hoehe = MathF.Min(StadtSzene.TuerHoehe, haus.Max.Y * 0.7f);
        }
        if (haus.StueckeBis <= haus.StueckeVon)
        {
            return null;
        }
        var glas = Enumerable.Range(haus.StueckeVon, haus.StueckeBis - haus.StueckeVon).Any(i => StadtTueren.MitGlasTuer(szene.Stuecke[i].Name));
        var fensterTeile = glas ? FensterTeile(szene, haus, aussen, modell) : [];
        // Tueren wie in der Ausgangsstadt (dort ohne Fund, auch wenn sie nicht ganz buendig sind)
        var ausgang = daten is null ? [] : TuerBuendig.AusgangFuer(szene, haus, daten)
            .Select(a => (Mitte: new Vector2(haus.Min.X + a.Lage.X, haus.Min.Z + a.Lage.Y), a.Aussen)).ToList();
        for (var b = 0; b < f._bloecke.Count; b++)
        {
            var (x1, z1, x2, z2) = f._bloecke[b];
            // gleiche Reihenfolge wie TuerSetzen.AufWand: Sued (+z), Ost, Nord, West
            f._waende.Add(new Wand(new(x1, z2), new(x2, z2), new(0, 1), b));
            f._waende.Add(new Wand(new(x2, z1), new(x2, z2), new(1, 0), b));
            f._waende.Add(new Wand(new(x1, z1), new(x2, z1), new(0, -1), b));
            f._waende.Add(new Wand(new(x1, z1), new(x1, z2), new(-1, 0), b));
        }
        foreach (var wand in f._waende)
        {
            var (stuecke, bib) = Ausschnitt(szene, grafik, haus, wand);
            Func<int, GrafikModell?> an = i => bib.Modell(stuecke[i].Name);
            var proben = new List<Probe>();
            var mtueren = new List<(float, Vector2)>();
            var fenster = new List<(float, int, Vector2)>();
            var l = wand.Laenge;
            var quer = new Vector2(-wand.Aussen.Y, wand.Aussen.X);
            var mitte = wand.Bei(l / 2);
            if (!glas)
            {
                // Tueren des Modells auf dieser Wand (dunkle CC0-Tuer, wie das Spiel sie als eigene Tuer erkennt)
                foreach (var mt in StadtTueren.ModellTueren(stuecke, 0, stuecke.Count, an, mitte, wand.Aussen, weite: l / 2))
                {
                    var tm = (l / 2) + (Vector2.Dot(quer, wand.Richtung) * mt.Mitte);
                    if (tm >= TuerSetzen.EckAbstand - 0.001f && tm <= l - TuerSetzen.EckAbstand + 0.001f && !Innen(f._bloecke, b: wand.Block, wand.Bei(tm) + (wand.Aussen * 0.05f)))
                    {
                        mtueren.Add((tm, wand.Bei(tm) - (wand.Aussen * mt.Tiefe)));
                    }
                }
            }
            foreach (var (am, an2) in ausgang)
            {
                var r = am - wand.A;
                var ta = Vector2.Dot(r, wand.Richtung);
                if (Vector2.Distance(an2, wand.Aussen) <= 0.01f && Vector2.Dot(r, wand.Aussen) is >= -0.6f and <= 0.01f && ta >= 0 && ta <= l)
                {
                    mtueren.Add((ta, am));
                }
            }
            foreach (var (teil, mitteTeil, n) in fensterTeile)
            {
                var r = mitteTeil - wand.A;
                var tt = Vector2.Dot(r, wand.Richtung);
                if (Vector2.Dot(n, wand.Aussen) > 0.9f && MathF.Abs(Vector2.Dot(r, wand.Aussen)) <= 0.35f && tt >= -0.01f && tt <= l + 0.01f)
                {
                    tt = Math.Clamp(tt, MathF.Min(TuerSetzen.EckAbstand, l / 2), MathF.Max(l - TuerSetzen.EckAbstand, l / 2));
                    fenster.Add((tt, teil, wand.Bei(tt)));
                }
            }
            foreach (var t in Proben(l))
            {
                var w = wand.Bei(t);
                proben.Add(Innen(f._bloecke, wand.Block, w + (wand.Aussen * 0.05f))
                    ? new Probe(t, Stelle.Nicht, w, null)
                    : Pruefen(t, stuecke, an, bib, w, wand.Aussen, hoehe, glas));
            }
            f._proben.Add(proben);
            f._modellTueren.Add(mtueren);
            f._fenster.Add(fenster);
        }
        return f;
    }

    /// <summary>Proben laengs einer Wand der Laenge l: alle <see cref="Schritt"/> von EckAbstand bis l - EckAbstand (kurz: Mitte).</summary>
    private static IEnumerable<float> Proben(float l)
    {
        var e = TuerSetzen.EckAbstand;
        if (l <= 2 * e)
        {
            yield return l / 2;
            yield break;
        }
        var n = (int)MathF.Floor(((l - (2 * e)) / Schritt) + 1e-4f);
        for (var k = 0; k <= n; k++)
        {
            yield return e + (k * Schritt);
        }
        if (e + (n * Schritt) < l - e - 1e-4f)
        {
            yield return l - e;
        }
    }

    /// <summary>Wie das Spiel die Tuer an dieser Stelle zeichnet und ob sie dort buendig ist.</summary>
    private static Probe Pruefen(float t, List<ModellStueck> stuecke, Func<int, GrafikModell?> an, GrafikBibliothek bib, Vector2 w, Vector2 aussen, float hoehe, bool glas)
    {
        if (StadtTueren.EigeneTuer(stuecke, 0, stuecke.Count, an, w, aussen, glas) is { } eigene)
        {
            var quer = new Vector2(-aussen.Y, aussen.X);
            return new Probe(t, Stelle.Modelltuer, w + (quer * Math.Clamp(0f, eigene.A1, eigene.A2)) - (aussen * eigene.Tiefe), null);
        }
        var (tiefe, hinten) = StadtTueren.Wandtiefe(stuecke, bib, w, aussen, hoehe) ?? (0f, 0f);
        var mitte = w - (aussen * tiefe);
        if (StadtTueren.Flaeche(stuecke, bib, mitte, aussen, hoehe).Count > 0)
        {
            return new Probe(t, Stelle.Nicht, mitte, null);
        }
        // eine Glastuer unter dem Rahmen, die das Spiel nicht umfaerbt (tiefer als die Tuerflaeche), bliebe sichtbar
        var halb = StadtTueren.HalbRahmen;
        var sichtbar = StadtTueren.ModellTueren(stuecke, 0, stuecke.Count, an, mitte, aussen, weite: halb + StadtTueren.GlasTuerBreit, glas: true)
            .Any(m => m.A1 < halb && m.A2 > -halb && m.Tiefe > hinten + StadtTueren.Rand && an(m.Stueck) is { } gm && m.Dreiecke.Count > 0 && StadtTueren.IstGlas(gm.Farbe(m.Dreiecke[0])));
        return new Probe(t, sichtbar ? Stelle.Nicht : Stelle.Glatt, mitte, null);
    }

    /// <summary>
    /// Nur die Dreiecke der Stuecke, die Strahlen an dieser Wand treffen koennen (in der Welt, je Stueck ein Modell): die Rechnungen
    /// des Spiels liefern damit genau dasselbe, nur schneller.
    /// </summary>
    private static (List<ModellStueck>, GrafikBibliothek) Ausschnitt(StadtSzene szene, GrafikBibliothek grafik, StadtHaus haus, Wand wand)
    {
        var stuecke = new List<ModellStueck>();
        var modelle = new List<GrafikModell>();
        var quer = new Vector2(-wand.Aussen.Y, wand.Aussen.X);
        var m = wand.Bei(wand.Laenge / 2);
        var halb = (wand.Laenge / 2) + StadtTueren.GlasTuerBreit + 1f;
        for (var i = haus.StueckeVon; i < haus.StueckeBis; i++)
        {
            if (grafik.Modell(szene.Stuecke[i].Name) is not { } gm)
            {
                continue;
            }
            var ecken = new List<float>();
            var farben = new List<byte>();
            for (var d = 0; d < gm.Dreiecke; d++)
            {
                var (a, b, c) = StadtTueren.Ecken(gm, d, szene.Stuecke[i].Lage);
                float qmin = float.MaxValue, qmax = float.MinValue, omin = float.MaxValue, omax = float.MinValue;
                foreach (var p in (ReadOnlySpan<Vector3>)[a, b, c])
                {
                    var r = new Vector2(p.X, p.Z) - m;
                    var q = Vector2.Dot(r, quer);
                    var o = Vector2.Dot(r, wand.Aussen);
                    (qmin, qmax, omin, omax) = (MathF.Min(qmin, q), MathF.Max(qmax, q), MathF.Min(omin, o), MathF.Max(omax, o));
                }
                if (qmax < -halb || qmin > halb || omax < -1.0f || omin > 0.6f || MathF.Min(a.Y, MathF.Min(b.Y, c.Y)) > 2.5f)
                {
                    continue;
                }
                ecken.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z]);
                farben.AddRange(gm.Farben.AsSpan(d * 3, 3).ToArray());
            }
            if (ecken.Count > 0)
            {
                var name = "fassade" + stuecke.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                modelle.Add(new GrafikModell(name, [.. ecken], [.. farben]));
                stuecke.Add(new ModellStueck(name, Matrix4x4.Identity));
            }
        }
        return (stuecke, GrafikBibliothek.Aus(modelle));
    }

    /// <summary>Liegt der Punkt in einem anderen Block des Hauses (innere Wand)?</summary>
    private static bool Innen(List<(float X1, float Z1, float X2, float Z2)> bloecke, int b, Vector2 p) =>
        bloecke.Where((_, i) => i != b).Any(k => p.X > k.X1 + 1e-4f && p.X < k.X2 - 1e-4f && p.Y > k.Z1 + 1e-4f && p.Y < k.Z2 - 1e-4f);

    /// <summary>
    /// Sammelliste 272 Frage 2 = a: Fensterteile im Erdgeschoss eines eigenen Gebaeudes (Wohnbau aus dem Editor): Nummer in
    /// <c>stadtdaten.bauteile[modell].teile</c>, Mitte der Fassade in der Welt und Richtung hinaus.
    /// </summary>
    private static List<(int Teil, Vector2 Mitte, Vector2 Aussen)> FensterTeile(StadtSzene szene, StadtHaus haus, PyObjekt aussen, string modell)
    {
        var ergebnis = new List<(int, Vector2, Vector2)>();
        if (Get(Bauteile.Gebaeude(Aussen.Stadtdaten(aussen)), modell) is not PyObjekt e || szene.Grafik?.Modell(modell) is null)
        {
            return ergebnis;
        }
        List<Bauteile.Teil> teile;
        try
        {
            teile = Bauteile.TeileLesen(Get(e, "teile"));
        }
        catch (InvalidDataException)
        {
            return ergebnis;
        }
        if (Bauteile.IstHalle(teile))
        {
            return ergebnis;
        }
        var stueck = Enumerable.Range(haus.StueckeVon, haus.StueckeBis - haus.StueckeVon).Select(i => szene.Stuecke[i]).FirstOrDefault(st => st.Name == modell);
        if (stueck.Name is null)
        {
            return ergebnis;
        }
        for (var i = 0; i < teile.Count; i++)
        {
            var t = teile[i];
            if (t.Stockwerk != 0 || t.Art is not { Art: Bauteile.Art.Wand, Fenster: true, Tuer: false } || !Bauteile.IstWohnbau(t.Id))
            {
                continue;
            }
            var (dx, dy) = Bauteile.Richtung(t.Fassade);
            var lokal = new Vector3(t.X + 0.5f + (dx * 0.5f), 0, t.Y + 0.5f + (dy * 0.5f));
            var welt = Vector3.Transform(lokal, stueck.Lage);
            var n = Vector3.TransformNormal(new Vector3(dx, 0, dy), stueck.Lage);
            var n2 = Vector2.Normalize(new Vector2(n.X, n.Z));
            ergebnis.Add((i, new Vector2(welt.X, welt.Z), n2));
        }
        return ergebnis;
    }

    /// <summary>
    /// Platz fuer einen Tipp bei <paramref name="p"/>. <paramref name="bisherWand"/>: Wand der Tuer bisher bzw. des letzten
    /// Zugschritts (Frage 3 = a: hinter dem Wandende bleibt die Tuer an ihr). <paramref name="einrasten"/>: Schalter „Einrasten“.
    /// </summary>
    public Platz Finden(Vector2 p, int? bisherWand, bool einrasten)
    {
        var w = WandFuer(p, bisherWand);
        var wand = _waende[w];
        var l = wand.Laenge;
        var e = TuerSetzen.EckAbstand;
        var t = l <= 2 * e ? l / 2 : Math.Clamp(Vector2.Dot(p - wand.A, wand.Richtung), e, l - e);
        // Frage 2 = a: ein Fensterteil des eigenen Gebaeudes unter dem Finger wird zur Tuer (immer, auch ohne Einrasten)
        if (_fenster[w].Where(f => MathF.Abs(f.T - t) <= 0.5f).OrderBy(f => MathF.Abs(f.T - t)).Select(f => ((float, int, Vector2)?)f).FirstOrDefault() is { } fe)
        {
            return Ergebnis(w, fe.Item1, Stelle.Fenster, false, false, fe.Item2, fe.Item3);
        }
        var hier = ProbeBei(w, t);
        if (!einrasten)
        {
            return Ergebnis(w, t, hier.Art, false, false, null, hier.Gezeichnet);
        }
        // auf eine Tuer des Modells bis FangWeite (wie bisher), sonst an die naechste glatte Stelle
        if (_modellTueren[w].Where(m => MathF.Abs(m.T - t) <= TuerSetzen.FangWeite).OrderBy(m => MathF.Abs(m.T - t)).Select(m => ((float, Vector2)?)m).FirstOrDefault() is { } mt)
        {
            return Ergebnis(w, mt.Item1, Stelle.Modelltuer, false, false, null, mt.Item2);
        }
        if (hier.Art is Stelle.Glatt or Stelle.Modelltuer)
        {
            return Ergebnis(w, t, hier.Art, false, false, null, hier.Gezeichnet);
        }
        var ziele = _proben[w].Where(q => q.Art is Stelle.Glatt or Stelle.Modelltuer).Select(q => (q.T, q.Art, q.Gezeichnet, Teil: (int?)null))
            .Concat(_modellTueren[w].Select(m => (m.T, Art: Stelle.Modelltuer, m.Gezeichnet, Teil: (int?)null)))
            .Concat(_fenster[w].Select(f => (f.T, Art: Stelle.Fenster, f.Gezeichnet, Teil: (int?)f.Teil)))
            .OrderBy(z => MathF.Abs(z.T - t)).ThenBy(z => z.T).ToList();
        if (ziele.Count == 0)
        {
            return Ergebnis(w, t, hier.Art, false, true, null, hier.Gezeichnet);
        }
        var z = ziele[0];
        return Ergebnis(w, z.T, z.Art, MathF.Abs(z.T - t) > 0.001f, false, z.Teil, z.Gezeichnet);
    }

    private Platz Ergebnis(int w, float t, Stelle art, bool verschoben, bool ohne, int? teil, Vector2 gezeichnet)
    {
        var wand = _waende[w];
        var q = wand.Bei(t);
        return new Platz(new Vector2(MathF.Round(q.X, 3), MathF.Round(q.Y, 3)), wand.Aussen, w, art, verschoben, ohne, teil, gezeichnet);
    }

    private Probe ProbeBei(int w, float t) => _proben[w].MinBy(q => MathF.Abs(q.T - t))!;

    /// <summary>
    /// Wand fuer den Punkt: der Block, der ihm am naechsten ist (wie das Spiel), darin die Wand, deren Spanne den Punkt enthaelt
    /// (die naechste). Hinter einem Wandende (keine Spanne, oder nur die Nachbarwand innerhalb von EckAbstand vor der Ecke) bleibt
    /// eine anliegende bisherige Wand; sonst die Wand, ueber deren Ende der Punkt weiter hinaus liegt.
    /// </summary>
    public int WandFuer(Vector2 p, int? bisherWand)
    {
        var block = Enumerable.Range(0, _bloecke.Count).MinBy(b => AbstandZumRand(_bloecke[b], p));
        var eigene = Enumerable.Range(0, _waende.Count).Where(i => _waende[i].Block == block).ToList();
        var e = TuerSetzen.EckAbstand;
        int? bisher = bisherWand is { } bw && bw >= 0 && bw < _waende.Count && _waende[bw].Block == block ? bw : null;
        var inSpanne = eigene.Select(i => (I: i, T: Vector2.Dot(p - _waende[i].A, _waende[i].Richtung), D: Vector2.Dot(p - _waende[i].A, _waende[i].Aussen)))
            .Where(x => x.T >= -0.01f && x.T <= _waende[x.I].Laenge + 0.01f).OrderBy(x => MathF.Abs(x.D)).ToList();
        if (inSpanne.Count > 0)
        {
            var best = inSpanne[0];
            // nur die Nachbarwand nah an der Ecke, die bisherige Wand endet dort: dann bleibt die Tuer an der bisherigen
            if (bisher is { } b0 && b0 != best.I && best.D > 0 && (best.T < e || best.T > _waende[best.I].Laenge - e) && Anliegend(b0, best.I))
            {
                return b0;
            }
            return best.I;
        }
        if (bisher is { } b1 && eigene.Where(i => i != b1).Any(i => Anliegend(b1, i)))
        {
            // hinter dem Ende: liegt der Punkt vor der bisherigen Wand, bleibt sie
            if (Vector2.Dot(p - _waende[b1].A, _waende[b1].Aussen) > 0)
            {
                return b1;
            }
        }
        // die Wand, vor der der Punkt am weitesten draussen liegt
        return eigene.MaxBy(i => Vector2.Dot(p - _waende[i].A, _waende[i].Aussen));
    }

    private bool Anliegend(int a, int b) =>
        Vector2.Distance(_waende[a].A, _waende[b].A) < 1e-3f || Vector2.Distance(_waende[a].A, _waende[b].B) < 1e-3f
        || Vector2.Distance(_waende[a].B, _waende[b].A) < 1e-3f || Vector2.Distance(_waende[a].B, _waende[b].B) < 1e-3f;

    private static float AbstandZumRand((float X1, float Z1, float X2, float Z2) b, Vector2 p) =>
        Kurve.Abstand(p, [new(b.X1, b.Z1), new(b.X2, b.Z1), new(b.X2, b.Z2), new(b.X1, b.Z2), new(b.X1, b.Z1)]);

    /// <summary>Wand und Stelle der Tuer, die gerade gespeichert ist (oder null), fuer die bisherige Wand beim Ziehen.</summary>
    public int? WandVon(Vector2? tuer) => tuer is { } t ? WandFuer(t, null) : null;

    /// <summary>Art jeder Probe einer Wand (nur Tests und Bilder): Stelle laengs der Wand und Art.</summary>
    public IReadOnlyList<(float T, Stelle Art)> ProbenVon(int wand) => [.. _proben[wand].Select(q => (q.T, q.Art))];
}
