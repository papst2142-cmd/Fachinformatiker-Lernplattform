using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle K3b (A 0.46.0 / S 0.12.0, Weisungen P12-59/P12-61, Nico 3 = b): Autostrecken automatisch aus dem Strassennetz,
/// nur Darstellung wie <see cref="Verkehr"/> (keine Ereignisart, kein Spielstand-Feld, kein Zufall, kein neuer
/// Zeichenaufruf, hoechstens <see cref="HoechstensAutos"/> Autos wie in Stadt 1).
/// <para>
/// Regel (<see cref="Automatisch"/>): automatische Strecken gibt es genau dann, wenn eine Strasse der Stadt mehr als zwei
/// Punkte hat (Kurve oder Kreisverkehr) oder keine der festen Strecken (<see cref="Verkehr.Strecken"/>) auf den
/// Strassen der Karte liegt (<see cref="FestPasst"/>). Fassung 0 (Stadt 1) hat nur Strassen mit zwei Punkten, alle festen Strecken passen: sie faehrt wie
/// bisher.
/// </para>
/// <para>
/// Netz: Knoten an Strassenenden, an Enden auf einer anderen Strasse und an Schnitten; jede Strasse zerfaellt dort in
/// Kanten. Gerade Strassen und Kurven in beide Richtungen (Rechtsverkehr, rechte Spur <see cref="Verkehr.Spur"/>), der
/// Ring eines Kreisverkehrs nur in eine Richtung (gegen den Uhrzeigersinn: die Insel liegt links vom Auto). Am Ende einer
/// Sackgasse (auch am Kartenrand) wendet das Auto (E1: so kommt jedes Auto ueberall wieder heraus, kein Sprung, keine
/// Sackgasse ohne Wendeweg); ist das Stueck dafuer zu kurz (<see cref="WendeMindest"/>), bekommt das Auto keine Strecke
/// und die Pruefung des Werkzeugs meldet rot. Jedes Auto faehrt einen festen Rundweg: an jeder Abzweigung waehlt es
/// reihum rechts, geradeaus, links (je Auto um eins versetzt), bis sich sein Weg wiederholt.
/// </para>
/// </summary>
public static class Autostrecken
{
    /// <summary>Punkte naeher als das sind ein Knoten.</summary>
    public const float KnotenNah = 0.12f;

    /// <summary>Ein Strassenende bis zu diesem Abstand auf einer anderen Strasse ist eine Einmuendung (wie <see cref="Verkehr.KreuzungenAus"/>).</summary>
    public const float EndeAufStrasse = 0.05f;

    /// <summary>An einem Knick ueber <see cref="EntlastenAbGrad"/> fallen Zwischenpunkte bis zu diesem Abstand weg (Platz fuer den Bogen).</summary>
    public const float Entlasten = 0.7f;

    /// <summary>Ab diesem Knick (Grad) wird entlastet.</summary>
    public const float EntlastenAbGrad = 20f;

    /// <summary>Wendebogen: so weit faehrt das Auto beim Wenden ueber die Spur hinaus nach vorn (halbe Ellipse).</summary>
    public const float WendeLang = 0.35f;

    /// <summary>So weit bleibt die Spitze des Wendebogens vor dem Strassenende (die Front des Autos bleibt auf der Strasse).</summary>
    public const float WendeRand = 0.3f;

    /// <summary>Kuerzestes Stueck zwischen zwei Wendestellen, auf dem ein Auto noch wenden und fahren kann.</summary>
    public const float WendeMindest = (2 * (WendeLang + WendeRand)) + 0.2f;

    /// <summary>Je so viel Strassenlaenge ein Auto.</summary>
    public const float StreckeJeAuto = 10f;

    /// <summary>Hoechstens so viele Autos (wie Stadt 1).</summary>
    public const int HoechstensAutos = 6;

    /// <summary>Gerichtete Kante: von Knoten, nach Knoten, Mittellinie in Fahrtrichtung, Teil = Stelle der ungerichteten Kante, Strasse = Stelle in der Strassenliste.</summary>
    public sealed record Kante(int Von, int Nach, List<Vector2> Linie, int Teil, bool Vorwaerts, bool Ring, int Strasse)
    {
        public float Laenge { get; } = Enumerable.Range(1, Linie.Count - 1).Sum(i => Vector2.Distance(Linie[i - 1], Linie[i]));
    }

    /// <summary>Das Netz: Knoten, gerichtete Kanten, Grad je Knoten (ungerichtete Kanten), Ringe der Kreisverkehre.</summary>
    public sealed record Netz(List<Vector2> Knoten, List<Kante> Kanten, int[] Grad, List<(Vector2 Mitte, float Radius)> Ringe)
    {
        /// <summary>Gesamtlaenge aller Strassenstuecke (jedes einmal).</summary>
        public float Laenge => Kanten.GroupBy(k => k.Teil).Sum(g => g.First().Laenge);
    }

    /// <summary>Ergebnis: Strecken (Spur, schon versetzt und geglaettet, geschlossen), Kreuzungsfelder, Ringe, zu kurze Sackgassen.</summary>
    public sealed record Ergebnis(List<(int Nummer, List<Vector2> Spur)> Strecken, List<Vector2> Kreuzungen, List<(Vector2 Mitte, float Radius)> Ringe,
        List<Vector2> ZuKurz, Netz Netz);

    /// <summary>Gilt fuer diese Strassen der automatische Verkehr (Regel siehe Klasse)?</summary>
    public static bool Automatisch(StadtSzene szene) =>
        szene.Strassen.Any(s => s.Count > 2) || !Verkehr.Strecken.Any(f => FestPasst([.. f.Punkte.Select(q => new Vector2(q.X, q.Z))], szene));

    /// <summary>
    /// Liegt die feste Strecke auf den Strassen (<see cref="Verkehr.AufStrassen"/>) und dabei wenigstens zum Teil auf der
    /// Karte? (Eine Strecke ganz ausserhalb der Karte zaehlt nicht als passend.)
    /// </summary>
    public static bool FestPasst(IReadOnlyList<Vector2> mitte, StadtSzene szene)
    {
        var innen = false;
        for (var i = 1; i < mitte.Count && !innen; i++)
        {
            for (var t = 0f; t <= 1f && !innen; t += 0.05f)
            {
                var p = Vector2.Lerp(mitte[i - 1], mitte[i], t);
                innen = p.X >= 0 && p.Y >= 0 && p.X <= szene.Breite && p.Y <= szene.Tiefe;
            }
        }
        return innen && Verkehr.AufStrassen(mitte, szene);
    }

    /// <summary>Rechte Seite zur Fahrtrichtung (die Seite, zu der <see cref="Kurve.Versetzt"/> mit positivem Abstand rueckt).</summary>
    public static Vector2 Rechts(Vector2 d) => new(-d.Y, d.X);

    /// <summary>Wie stark biegt <paramref name="raus"/> gegen <paramref name="rein"/> nach rechts ab (Bogenmass, rechts positiv)?</summary>
    public static float Abbiegen(Vector2 rein, Vector2 raus) => MathF.Atan2(Vector2.Dot(raus, Rechts(rein)), Vector2.Dot(raus, rein));

    /// <summary>Das Netz der Strassen (Mittellinien wie <see cref="StadtSzene.Strassen"/>).</summary>
    public static Netz NetzAus(IReadOnlyList<IReadOnlyList<Vector2>> strassen)
    {
        var knoten = new List<Vector2>();
        int KnotenBei(Vector2 p)
        {
            for (var i = 0; i < knoten.Count; i++)
            {
                if (Vector2.Distance(knoten[i], p) <= KnotenNah)
                {
                    return i;
                }
            }
            knoten.Add(p);
            return knoten.Count - 1;
        }
        // Knoten zuerst an allen Teilungsstellen (Einmuendung: Punkt auf der durchgehenden Strasse), damit die Enden der
        // einmuendenden Strasse denselben Knoten finden
        var teilungen = new List<List<(float U, Vector2 P)>>();
        var ringe = new List<(Vector2, float)>();
        for (var i = 0; i < strassen.Count; i++)
        {
            var lin = strassen[i];
            var liste = new List<(float U, Vector2 P)>();
            teilungen.Add(liste);
            if (lin.Count < 2)
            {
                continue;
            }
            var ring = Kreisverkehr.Geschlossen(lin);
            var laenge = Laenge(lin);
            for (var j = 0; j < strassen.Count; j++)
            {
                var anders = strassen[j];
                if (j == i || anders.Count < 2)
                {
                    continue;
                }
                if (!Kreisverkehr.Geschlossen(anders))
                {
                    foreach (var ende in (Vector2[])[anders[0], anders[^1]])
                    {
                        if (Kurve.Abstand(ende, lin) <= EndeAufStrasse)
                        {
                            var (u, p) = Projektion(lin, ende);
                            liste.Add((u, p));
                        }
                    }
                }
                for (var a = 1; a < lin.Count; a++)
                {
                    for (var b = 1; b < anders.Count; b++)
                    {
                        if (Schnitt(lin[a - 1], lin[a], anders[b - 1], anders[b]) is { } s)
                        {
                            liste.Add((Bogen(lin, a - 1) + Vector2.Distance(lin[a - 1], s), s));
                        }
                    }
                }
            }
            if (!ring)
            {
                liste.Add((0, lin[0]));
                liste.Add((laenge, lin[^1]));
            }
            else
            {
                var m = Kreisverkehr.Mitte(lin);
                ringe.Add((m, lin.Take(lin.Count - 1).Average(q => Vector2.Distance(q, m))));
                // u auf dem Ring: 0 und Laenge sind dieselbe Stelle
                for (var k = 0; k < liste.Count; k++)
                {
                    if (liste[k].U >= laenge - 0.05f)
                    {
                        liste[k] = (0, liste[k].P);
                    }
                }
            }
            liste.Sort((x, y) => x.U.CompareTo(y.U));
            for (var k = liste.Count - 1; k > 0; k--)
            {
                if (liste[k].U - liste[k - 1].U < 0.05f)
                {
                    liste.RemoveAt(k);
                }
            }
            foreach (var (_, p) in liste)
            {
                KnotenBei(p);
            }
        }
        var kanten = new List<Kante>();
        void Teil(int von, int nach, List<Vector2> linie, bool ring, bool vorwaertsErlaubt, int strasse)
        {
            linie[0] = knoten[von];
            linie[^1] = knoten[nach];
            if (Laenge(linie) < 1e-3f)
            {
                return;
            }
            var teil = kanten.Count == 0 ? 0 : kanten.Max(k => k.Teil) + 1;
            if (!ring || vorwaertsErlaubt)
            {
                kanten.Add(new Kante(von, nach, linie, teil, true, ring, strasse));
            }
            if (!ring || !vorwaertsErlaubt)
            {
                var r = Enumerable.Reverse(linie).ToList();
                kanten.Add(new Kante(nach, von, r, teil, false, ring, strasse));
            }
        }
        for (var i = 0; i < strassen.Count; i++)
        {
            var lin = strassen[i];
            if (lin.Count < 2)
            {
                continue;
            }
            var liste = teilungen[i];
            if (!Kreisverkehr.Geschlossen(lin))
            {
                for (var k = 1; k < liste.Count; k++)
                {
                    Teil(KnotenBei(liste[k - 1].P), KnotenBei(liste[k].P), Stueck(lin, liste[k - 1].U, liste[k].U), false, true, i);
                }
                continue;
            }
            // Ring: Fahrtrichtung so, dass die Mitte links vom Auto liegt (rechte Seite zeigt nach aussen)
            var m = Kreisverkehr.Mitte(lin);
            var vorwaerts = Vector2.Dot(Rechts(Vector2.Normalize(lin[1] - lin[0])), lin[0] - m) > 0;
            var laenge = Laenge(lin);
            if (liste.Count == 0)
            {
                var n = KnotenBei(lin[0]);
                Teil(n, n, [.. lin], true, vorwaerts, i);
                continue;
            }
            for (var k = 0; k < liste.Count; k++)
            {
                var (ua, pa) = liste[k];
                var (ub, pb) = liste[(k + 1) % liste.Count];
                var linie = ub > ua ? Stueck(lin, ua, ub) : Stueck(lin, ua, laenge).Concat(Stueck(lin, 0, ub).Skip(1)).ToList();
                Teil(KnotenBei(pa), KnotenBei(pb), linie, true, vorwaerts, i);
            }
        }
        var grade = new int[knoten.Count];
        foreach (var g in kanten.GroupBy(k => k.Teil))
        {
            var k = g.First();
            grade[k.Von]++;
            grade[k.Nach]++;
        }
        return new Netz(knoten, kanten, grade, ringe);
    }

    /// <summary>Die Wege, die an einer Kante weitergehen (ohne die Gegenrichtung derselben Kante), rechts zuerst.</summary>
    public static List<int> Weiter(Netz netz, int kante)
    {
        var e = netz.Kanten[kante];
        var rein = Vector2.Normalize(e.Linie[^1] - e.Linie[^2]);
        return [.. Enumerable.Range(0, netz.Kanten.Count)
            .Where(f => netz.Kanten[f].Von == e.Nach && netz.Kanten[f].Teil != e.Teil)
            .OrderByDescending(f => Abbiegen(rein, Vector2.Normalize(netz.Kanten[f].Linie[1] - netz.Kanten[f].Linie[0])))
            .ThenBy(f => f)];
    }

    /// <summary>Die Gegenrichtung einer Kante (auf dem Ring keine: -1).</summary>
    public static int Gegen(Netz netz, int kante)
    {
        var e = netz.Kanten[kante];
        return netz.Kanten.FindIndex(f => f.Teil == e.Teil && f.Vorwaerts != e.Vorwaerts);
    }

    /// <summary>
    /// Der Rundweg von Auto <paramref name="auto"/> ab Kante <paramref name="start"/>: an jeder Abzweigung reihum
    /// rechts, geradeaus, links (Beginn je Auto versetzt), an einer Sackgasse wenden. Ergebnis: Kanten des Kreises mit
    /// der Angabe, ob danach gewendet wird.
    /// </summary>
    public static List<(int Kante, bool Wende)> Rundweg(Netz netz, int start, int auto)
    {
        var gesehen = new Dictionary<(int, int), int>();
        var weg = new List<(int Kante, bool Wende)>();
        var (kante, z) = (start, 0);
        while (!gesehen.ContainsKey((kante, z)))
        {
            gesehen[(kante, z)] = weg.Count;
            var weiter = Weiter(netz, kante);
            int naechste;
            var wende = false;
            if (weiter.Count == 0 && netz.Kanten[kante].Ring)
            {
                naechste = kante;   // Ring ohne Zufahrt: weiter im Kreis
            }
            else if (weiter.Count == 0)
            {
                naechste = Gegen(netz, kante);
                wende = true;
                if (naechste < 0)
                {
                    return [];
                }
            }
            else if (weiter.Count == 1)
            {
                naechste = weiter[0];
            }
            else
            {
                var rein = Vector2.Normalize(netz.Kanten[kante].Linie[^1] - netz.Kanten[kante].Linie[^2]);
                naechste = ((auto + z) % 3) switch
                {
                    0 => weiter[0],
                    2 => weiter[^1],
                    _ => weiter.OrderBy(f => MathF.Abs(Abbiegen(rein, Vector2.Normalize(netz.Kanten[f].Linie[1] - netz.Kanten[f].Linie[0])))).ThenBy(f => f).First(),
                };
                z = (z + 1) % 3;
            }
            weg.Add((kante, wende));
            kante = naechste;
        }
        return weg.Skip(gesehen[(kante, z)]).ToList();
    }

    /// <summary>Alle Strecken der Strassen (Regel siehe Klasse), <paramref name="bogen"/> = Halbmesser der Boegen an Knicken.</summary>
    public static Ergebnis Anlegen(IReadOnlyList<IReadOnlyList<Vector2>> strassen, float bogen = 0.6f)
    {
        var netz = NetzAus(strassen);
        var kreuzungen = Enumerable.Range(0, netz.Knoten.Count).Where(k => netz.Grad[k] >= 3).Select(k => netz.Knoten[k]).ToList();
        var zuKurz = new List<Vector2>();
        var liste = new List<(int, List<Vector2>)>();
        // jede Sackgasse, auf der ein Auto nicht wenden kann, einmal nennen
        foreach (var e in netz.Kanten)
        {
            if (!e.Ring && netz.Grad[e.Nach] == 1 && netz.Grad[e.Von] == 1 && e.Vorwaerts && e.Laenge < WendeMindest)
            {
                zuKurz.Add(Vector2.Lerp(e.Linie[0], e.Linie[^1], 0.5f));
            }
        }
        if (netz.Kanten.Count == 0)
        {
            return new Ergebnis(liste, kreuzungen, netz.Ringe, zuKurz, netz);
        }
        var autos = Math.Clamp((int)(netz.Laenge / StreckeJeAuto), 1, HoechstensAutos);
        var reihe = Enumerable.Range(0, netz.Kanten.Count).OrderByDescending(k => netz.Kanten[k].Laenge).ThenBy(k => k).ToList();
        for (var nr = 0; nr < autos; nr++)
        {
            var weg = Rundweg(netz, reihe[nr * reihe.Count / autos], nr);
            if (weg.Count == 0 || Spur(netz, weg, bogen, zuKurz) is not { } spur)
            {
                continue;
            }
            liste.Add((nr, OhneDoppelte(spur)));
        }
        return new Ergebnis(liste, kreuzungen, netz.Ringe, zuKurz, netz);
    }

    /// <summary>Die Spur (rechte Fahrspur, geglaettet, geschlossen) eines Rundwegs; null, wenn ein Stueck zum Wenden zu kurz ist.</summary>
    private static List<Vector2>? Spur(Netz netz, List<(int Kante, bool Wende)> weg, float bogen, List<Vector2> zuKurz)
    {
        if (weg.Any(w => w.Wende))
        {
            // so drehen, dass nach dem letzten Stueck gewendet wird; dann ist jedes Stueck von Wende zu Wende
            var letzte = weg.FindLastIndex(w => w.Wende);
            weg = [.. weg.Skip(letzte + 1), .. weg.Take(letzte + 1)];
            var spur = new List<Vector2>();
            var stueck = new List<int>();
            foreach (var (k, wende) in weg)
            {
                stueck.Add(k);
                if (!wende)
                {
                    continue;
                }
                var (mitte, gelenke) = Verbinden(netz, stueck);
                stueck.Clear();
                var lang = WendeLang + WendeRand;
                if (Laenge(mitte) < WendeMindest)
                {
                    var ort = Vector2.Lerp(mitte[0], mitte[^1], 0.5f);
                    if (zuKurz.All(q => Vector2.Distance(q, ort) > 0.1f))
                    {
                        zuKurz.Add(ort);
                    }
                    return null;
                }
                var gekuerzt = Kuerzen(mitte, gelenke, lang);
                var linie = Kurve.Versetzt(Kurve.Glatt(Entlastet(gekuerzt.Linie, gekuerzt.Gelenke), bogen), Verkehr.Spur);
                spur.AddRange(spur.Count > 0 && Vector2.Distance(spur[^1], linie[0]) < 1e-4f ? linie.Skip(1) : linie);
                // Wendebogen: halbe Ellipse um das Ende der Mittellinie, nach links herum auf die Gegenspur
                var ende = gekuerzt.Linie[^1];
                var d = Vector2.Normalize(gekuerzt.Linie[^1] - gekuerzt.Linie[^2]);
                var s = Rechts(d);
                for (var i = 1; i <= 12; i++)
                {
                    var phi = MathF.PI * i / 12;
                    spur.Add(ende + (s * (Verkehr.Spur * MathF.Cos(phi))) + (d * (WendeLang * MathF.Sin(phi))));
                }
            }
            // geschlossen: der letzte Punkt des letzten Wendebogens ist der Anfang
            if (spur.Count > 2 && Vector2.Distance(spur[^1], spur[0]) < 1e-3f)
            {
                spur[^1] = spur[0];
            }
            return spur;
        }
        // ohne Wende: Beginn in der Mitte der laengsten Kante (dort ist es gerade), dann geschlossen
        var laengste = Enumerable.Range(0, weg.Count).OrderByDescending(i => netz.Kanten[weg[i].Kante].Laenge).ThenBy(i => i).First();
        weg = [.. weg.Skip(laengste), .. weg.Take(laengste)];
        var (rund, gel) = Verbinden(netz, [.. weg.Select(w => w.Kante), weg[0].Kante]);
        var erste = netz.Kanten[weg[0].Kante].Laenge;
        var halb = erste / 2;
        var gesamt = Laenge(rund);
        var anfang = Ab(rund, gel, halb);
        var (linieRund, gelRund) = Bis(anfang.Linie, anfang.Gelenke, gesamt - halb - halb);
        var fertig = Kurve.Versetzt(Kurve.Glatt(Entlastet(linieRund, gelRund), bogen), Verkehr.Spur);
        fertig[^1] = fertig[0];
        return fertig;
    }

    /// <summary>Mittellinien der Kanten aneinander, mit den Stellen der Gelenke (Knoten zwischen zwei Kanten).</summary>
    private static (List<Vector2> Linie, List<int> Gelenke) Verbinden(Netz netz, List<int> kanten)
    {
        var linie = new List<Vector2>();
        var gelenke = new List<int>();
        foreach (var k in kanten)
        {
            var l = netz.Kanten[k].Linie;
            if (linie.Count > 0)
            {
                gelenke.Add(linie.Count - 1);
                linie.AddRange(l.Skip(1));
            }
            else
            {
                linie.AddRange(l);
            }
        }
        return (linie, gelenke);
    }

    /// <summary>Am Anfang und am Ende je <paramref name="laenge"/> abschneiden (Platz fuer den Wendebogen).</summary>
    private static (List<Vector2> Linie, List<int> Gelenke) Kuerzen(List<Vector2> linie, List<int> gelenke, float laenge)
    {
        var a = Ab(linie, gelenke, laenge);
        return Bis(a.Linie, a.Gelenke, Laenge(a.Linie) - laenge);
    }

    /// <summary>Linie ab Bogenlaenge <paramref name="u"/>.</summary>
    private static (List<Vector2> Linie, List<int> Gelenke) Ab(List<Vector2> linie, List<int> gelenke, float u)
    {
        var lauf = 0f;
        for (var i = 1; i < linie.Count; i++)
        {
            var d = Vector2.Distance(linie[i - 1], linie[i]);
            if (lauf + d > u)
            {
                var p = Vector2.Lerp(linie[i - 1], linie[i], (u - lauf) / d);
                var neu = new List<Vector2> { p };
                neu.AddRange(linie.Skip(i));
                return (neu, [.. gelenke.Where(g => g >= i).Select(g => g - i + 1)]);
            }
            lauf += d;
        }
        return ([linie[^1]], []);
    }

    /// <summary>Linie bis Bogenlaenge <paramref name="u"/>.</summary>
    private static (List<Vector2> Linie, List<int> Gelenke) Bis(List<Vector2> linie, List<int> gelenke, float u)
    {
        var lauf = 0f;
        for (var i = 1; i < linie.Count; i++)
        {
            var d = Vector2.Distance(linie[i - 1], linie[i]);
            if (lauf + d >= u)
            {
                var neu = linie.Take(i).ToList();
                neu.Add(Vector2.Lerp(linie[i - 1], linie[i], d < 1e-6f ? 0 : (u - lauf) / d));
                return (neu, [.. gelenke.Where(g => g < i && g > 0)]);
            }
            lauf += d;
        }
        return (linie, gelenke);
    }

    /// <summary>An Gelenken mit Knick ueber <see cref="EntlastenAbGrad"/> fallen Zwischenpunkte bis <see cref="Entlasten"/> weg.</summary>
    public static List<Vector2> Entlastet(List<Vector2> linie, List<int> gelenke)
    {
        var weg = new HashSet<int>();
        var feste = gelenke.ToHashSet();
        foreach (var g in gelenke)
        {
            if (g <= 0 || g >= linie.Count - 1)
            {
                continue;
            }
            var rein = Vector2.Normalize(linie[g] - linie[g - 1]);
            var raus = Vector2.Normalize(linie[g + 1] - linie[g]);
            if (MathF.Abs(Abbiegen(rein, raus)) * 180 / MathF.PI <= EntlastenAbGrad)
            {
                continue;
            }
            for (var i = g - 1; i > 0 && !feste.Contains(i) && Vector2.Distance(linie[i], linie[g]) < Entlasten; i--)
            {
                weg.Add(i);
            }
            for (var i = g + 1; i < linie.Count - 1 && !feste.Contains(i) && Vector2.Distance(linie[i], linie[g]) < Entlasten; i++)
            {
                weg.Add(i);
            }
        }
        return [.. linie.Where((_, i) => !weg.Contains(i))];
    }

    /// <summary>Verkehr aus den automatischen Strecken (Modelle der festen Strecken nach Nummer).</summary>
    public static Verkehr VerkehrAus(Ergebnis e, IReadOnlyDictionary<int, float>? tempo, float abstand, IReadOnlyList<IReadOnlyList<Vector2>>? strassen = null)
    {
        var v = new Verkehr { AbstandVorne = abstand, Rundweg = true, FuerStrassen = strassen };
        v.Kreuzungen.AddRange(e.Kreuzungen);
        v.Ringe.AddRange(e.Ringe);
        var gesetzt = new List<Vector2>();
        foreach (var (nr, spur) in e.Strecken)
        {
            if (tempo is not null && !tempo.ContainsKey(nr))
            {
                continue;
            }
            var auto = new Verkehr.Auto(Verkehr.Strecken[nr].Modell, spur, 0) { Nummer = nr, Tempo = tempo?[nr] ?? Verkehr.Tempo, Schnell = true };
            // Start: erste Stelle (alle 0,5) mit Abstand zu den schon gesetzten Autos und ausserhalb der Kreuzungsfelder
            for (var s = 0f; s < auto.Laenge; s += 0.5f)
            {
                var ort = auto.Lage(s).Ort;
                if (gesetzt.All(q => Vector2.Distance(q, ort) >= 1.5f) && e.Kreuzungen.All(k => Vector2.Distance(k, ort) > Verkehr.FeldRadius + 0.4f))
                {
                    auto.Weg = s;
                    break;
                }
            }
            gesetzt.Add(auto.Lage().Ort);
            v.Autos.Add(auto);
        }
        return v;
    }

    /// <summary>Stellen der Autos, die beim Fahren laenger als <paramref name="grenze"/> Sekunden am Stueck stehen (Figur weit weg).</summary>
    public static List<Vector2> Stecken(Verkehr v, double sekunden = 90, double grenze = 20)
    {
        var steht = new double[v.Autos.Count];
        var stellen = new List<Vector2>();
        var gemeldet = new bool[v.Autos.Count];
        const double schritt = 1.0 / 20;
        for (var t = 0; t < (int)Math.Round(sekunden / schritt); t++)
        {
            v.Weiter(schritt, -1000, -1000);
            for (var i = 0; i < v.Autos.Count; i++)
            {
                steht[i] = v.Autos[i].Haelt ? steht[i] + schritt : 0;
                if (steht[i] > grenze && !gemeldet[i])
                {
                    gemeldet[i] = true;
                    stellen.Add(v.Autos[i].Lage().Ort);
                }
            }
        }
        return stellen;
    }

    /// <summary>Punkte, die auf dem vorigen liegen, fallen weg (kein Stueck der Laenge 0); Anfang und Ende bleiben gleich.</summary>
    private static List<Vector2> OhneDoppelte(List<Vector2> p)
    {
        var liste = new List<Vector2> { p[0] };
        for (var i = 1; i < p.Count; i++)
        {
            if (Vector2.Distance(liste[^1], p[i]) > 1e-4f)
            {
                liste.Add(p[i]);
            }
        }
        liste[^1] = p[^1];
        return liste;
    }

    private static float Laenge(IReadOnlyList<Vector2> p)
    {
        var l = 0f;
        for (var i = 1; i < p.Count; i++)
        {
            l += Vector2.Distance(p[i - 1], p[i]);
        }
        return l;
    }

    /// <summary>Bogenlaenge bis Punkt <paramref name="i"/>.</summary>
    private static float Bogen(IReadOnlyList<Vector2> p, int i)
    {
        var l = 0f;
        for (var k = 1; k <= i; k++)
        {
            l += Vector2.Distance(p[k - 1], p[k]);
        }
        return l;
    }

    /// <summary>Naechster Punkt der Linie zu <paramref name="q"/> mit seiner Bogenlaenge.</summary>
    private static (float U, Vector2 P) Projektion(IReadOnlyList<Vector2> p, Vector2 q)
    {
        var (bestU, bestP, bestD) = (0f, p[0], float.MaxValue);
        var lauf = 0f;
        for (var i = 0; i + 1 < p.Count; i++)
        {
            var d = p[i + 1] - p[i];
            var l2 = d.LengthSquared();
            var t = l2 < 1e-12f ? 0 : Math.Clamp(Vector2.Dot(q - p[i], d) / l2, 0f, 1f);
            var punkt = p[i] + (d * t);
            var abstand = Vector2.Distance(q, punkt);
            if (abstand < bestD)
            {
                (bestU, bestP, bestD) = (lauf + (MathF.Sqrt(l2) * t), punkt, abstand);
            }
            lauf += MathF.Sqrt(l2);
        }
        return (bestU, bestP);
    }

    /// <summary>Stueck der Linie zwischen den Bogenlaengen <paramref name="ua"/> und <paramref name="ub"/>.</summary>
    private static List<Vector2> Stueck(IReadOnlyList<Vector2> p, float ua, float ub)
    {
        var liste = new List<Vector2> { Punkt(p, ua) };
        var lauf = 0f;
        for (var i = 0; i < p.Count; i++)
        {
            if (i > 0)
            {
                lauf += Vector2.Distance(p[i - 1], p[i]);
            }
            if (lauf > ua + 1e-4f && lauf < ub - 1e-4f)
            {
                liste.Add(p[i]);
            }
        }
        liste.Add(Punkt(p, ub));
        return liste;
    }

    private static Vector2 Punkt(IReadOnlyList<Vector2> p, float u)
    {
        var lauf = 0f;
        for (var i = 1; i < p.Count; i++)
        {
            var d = Vector2.Distance(p[i - 1], p[i]);
            if (lauf + d >= u)
            {
                return Vector2.Lerp(p[i - 1], p[i], d < 1e-6f ? 0 : (u - lauf) / d);
            }
            lauf += d;
        }
        return p[^1];
    }

    private static Vector2? Schnitt(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        var r = a2 - a1;
        var s = b2 - b1;
        var nenner = (r.X * s.Y) - (r.Y * s.X);
        if (MathF.Abs(nenner) < 1e-6f)
        {
            return null;
        }
        var q = b1 - a1;
        var t = ((q.X * s.Y) - (q.Y * s.X)) / nenner;
        var u = ((q.X * r.Y) - (q.Y * r.X)) / nenner;
        // nur echte Schnitte in der Mitte (Enden zaehlen als Einmuendung)
        return t is > 1e-3f and < 1 - 1e-3f && u is > 1e-3f and < 1 - 1e-3f ? a1 + (r * t) : null;
    }
}
