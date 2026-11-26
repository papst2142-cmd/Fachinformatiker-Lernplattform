using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="StadtSzene"/> mit Strassen, Mittellinien,
/// Stegen, Gleisen, Kreuzungen und Bruecken. Reiner Umzug aus StadtSzene.cs, nichts geaendert.
/// </summary>
public sealed partial class StadtSzene
{
    /// <summary>
    /// Welle K1: so weit reicht eine stumpf angesetzte Kurve in die anschliessende Strasse (auf gleicher Hoehe und in
    /// gleicher Farbe). Mehr als der Versatz der Gehrung am ersten Punkt (halbe Breite samt Gehweg mal tan 2,5 Grad, unter
    /// 0,04), sonst ueberschlaegt sich das erste Viereck.
    /// </summary>
    public const float StumpfZugabe = 0.05f;

    /// <summary>So weit duerfen die Richtungen zweier aneinander stossender Strassen abweichen (Grad, <see cref="Anschluss"/>).</summary>
    public const float AnschlussGrad = 8f;

    /// <summary>
    /// Welle K1: trifft ein Strassenende genau das Ende einer anderen Strasse in gleicher Richtung (Kurve an gerade Strasse,
    /// so setzt das Werkzeug sie), stossen beide Baender stumpf aneinander (keine Verlaengerung um die halbe Breite, die
    /// sonst als Ecke ueber den Bogen ragt). Nur an Kurven (mehr als zwei Punkte auf einer der beiden Seiten).
    /// </summary>
    /// <param name="richtung">Richtung aus der eigenen Strasse heraus (Laenge 1).</param>
    public static bool Fortgesetzt(Vector2 ende, Vector2 richtung, IReadOnlyList<Vector2> eigene, IEnumerable<List<Vector2>> alle) =>
        Weiter(ende, richtung, eigene, alle) is not null;

    /// <summary>
    /// Die Richtung, in der die anschliessende Strasse vom gemeinsamen Ende weg laeuft (<see cref="Fortgesetzt"/>), sonst
    /// null. Erlaubt sind bis <see cref="AnschlussGrad"/> Grad Unterschied: das erste und das letzte Stueck einer Kurve
    /// sind Sehnen von 7,5 Grad und weichen um die Haelfte von der Tangente ab; treffen zwei Kurven mit gleicher
    /// Drehrichtung aufeinander, liegen ihre Sehnen am Stoss 7,5 Grad auseinander (Welle K3, vorher 5 Grad: dort fehlte
    /// der Anschluss, die Baender hatten Endkappen und die Mittellinie eine Luecke).
    /// </summary>
    public static Vector2? Weiter(Vector2 ende, Vector2 richtung, IReadOnlyList<Vector2> eigene, IEnumerable<List<Vector2>> alle) =>
        Anschluss(ende, richtung, eigene, [.. alle]) is { } t ? t.Richtung : null;

    /// <summary>
    /// Welle K3: wie <see cref="Weiter"/>, dazu welche Strasse (Stelle in <paramref name="alle"/>) und ob sie mit ihrem
    /// Anfang (true) oder ihrem Ende anschliesst. Gleiche Regeln, darum gibt <see cref="Weiter"/> genau diese Richtung.
    /// </summary>
    public static (int Strasse, bool AmAnfang, Vector2 Richtung)? Anschluss(Vector2 ende, Vector2 richtung, IReadOnlyList<Vector2> eigene,
        IReadOnlyList<List<Vector2>> alle, Func<int, bool>? erlaubt = null)
    {
        for (var k = 0; k < alle.Count; k++)
        {
            var a = alle[k];
            if (a.Count < 2 || ReferenceEquals(a, eigene) || a.SequenceEqual(eigene) || (!AlsBand(a) && !AlsBand(eigene))
                || (erlaubt is not null && !erlaubt(k)))
            {
                continue;
            }
            foreach (var (p, weiter, anfang) in new[] { (a[0], a[1] - a[0], true), (a[^1], a[^2] - a[^1], false) })
            {
                if (Vector2.Distance(p, ende) <= 0.02f && weiter.LengthSquared() > 1e-10f
                    && Vector2.Dot(Vector2.Normalize(weiter), richtung) >= MathF.Cos(AnschlussGrad * MathF.PI / 180))
                {
                    return (k, anfang, Vector2.Normalize(weiter));
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Die Striche der Mittellinie aller Strassen (Reihenfolge wie gezeichnet). Kein Strich, dessen Mitte bis zur halben
    /// Strassenbreite + 0,05 an einer Strasse ausserhalb der eigenen Kette liegt (sonst lag auf Kreuzungen ein "+").
    /// <paramref name="ohneKette"/> = Stand vor K3 (jede Strasse eigener Takt), nur fuer die Gegenprobe in Tests.
    /// </summary>
    public static List<(float X1, float Z1, float X2, float Z2)> MittellinienStriche(IReadOnlyList<List<Vector2>> strassen, bool ohneKette = false)
    {
        var ergebnis = new List<(float, float, float, float)>();
        // Welle Nachladen (grosse Karten), gleiches Ergebnis wie vorher: gleiche Linienzuege einmal als Gruppe bestimmt (statt
        // jede Strasse gegen jede zu vergleichen), und eine andere Strasse prueft den Abstand nur, wenn die Mitte im Rahmen der
        // Strasse plus Breite liegt (ausserhalb ist der Abstand sicher groesser)
        var gruppe = new int[strassen.Count];
        var gruppen = new Dictionary<List<Vector2>, int>(new GleicheLinie());
        for (var k = 0; k < strassen.Count; k++)
        {
            gruppe[k] = gruppen.TryGetValue(strassen[k], out var g) ? g : gruppen[strassen[k]] = k;
        }
        var rahmen = strassen.Select(Rahmen).ToList();
        const float weite = (StrassenBreite / 2) + 0.05f;
        foreach (var zug in MittellinienZuege(strassen, ohneKette))
        {
            var eigeneGruppen = zug.Strassen.Select(k => gruppe[k]).ToHashSet();
            var andere = Enumerable.Range(0, strassen.Count).Where(k => !zug.Strassen.Contains(k) && !eigeneGruppen.Contains(gruppe[k])).ToList();
            foreach (var (x1, z1, x2, z2) in Kreisverkehr.Geschlossen([.. zug.Linie.Select(q => new Vector2(q.X, q.Z))])
                ? RingStriche(zug.Linie) : Striche(zug.Linie, 0.2f, 0.25f))
            {
                var mitte = new Vector2((x1 + x2) / 2, (z1 + z2) / 2);
                if (andere.Any(a => mitte.X >= rahmen[a].Min.X - weite - 0.01f && mitte.X <= rahmen[a].Max.X + weite + 0.01f
                    && mitte.Y >= rahmen[a].Min.Y - weite - 0.01f && mitte.Y <= rahmen[a].Max.Y + weite + 0.01f
                    && Kurve.Abstand(mitte, strassen[a]) <= weite))
                {
                    continue;
                }
                ergebnis.Add((x1, z1, x2, z2));
            }
        }
        return ergebnis;
    }

    /// <summary>
    /// Welle K3 (A 0.45.0, Sammelliste 231): die Linienzuege der Mittellinie. Strassen, die nach <see cref="Anschluss"/>
    /// aneinander anschliessen, bilden eine Kette in Reihenfolge und Richtung (gemeinsamer Punkt nur einmal); jede Kette
    /// kommt an der Stelle ihrer ersten Strasse (Reihenfolge der Landschaft). Eine Strasse ohne Anschluss ist eine Kette
    /// mit ihren eigenen Punkten, unveraendert (Fassung 0 hat nur solche, darum bytegleich).
    /// </summary>
    public static List<(List<int> Strassen, List<(float X, float Z)> Linie)> MittellinienZuege(IReadOnlyList<List<Vector2>> strassen, bool ohneKette = false)
    {
        var ergebnis = new List<(List<int>, List<(float X, float Z)>)>();
        var vergeben = new bool[strassen.Count];
        for (var i = 0; i < strassen.Count; i++)
        {
            if (vergeben[i])
            {
                continue;
            }
            vergeben[i] = true;
            // Kette als Liste (Strasse, umgedreht)
            var kette = new LinkedList<(int K, bool Um)>();
            kette.AddFirst((i, false));
            var s0 = strassen[i];
            if (!ohneKette && s0.Count >= 2)
            {
                // nach vorn: vom Ende der letzten Strasse weiter
                while (true)
                {
                    var (k, um) = kette.Last!.Value;
                    var p = Ausgerichtet(strassen[k], um);
                    if (p.Count < 2 || Vector2.DistanceSquared(p[^1], p[^2]) < 1e-12f
                        || Anschluss(p[^1], Vector2.Normalize(p[^1] - p[^2]), strassen[k], strassen, j => !vergeben[j]) is not { } t)
                    {
                        break;
                    }
                    vergeben[t.Strasse] = true;
                    kette.AddLast((t.Strasse, !t.AmAnfang));
                }
                // nach hinten: vom Anfang der ersten Strasse zurueck
                while (true)
                {
                    var (k, um) = kette.First!.Value;
                    var p = Ausgerichtet(strassen[k], um);
                    if (p.Count < 2 || Vector2.DistanceSquared(p[0], p[1]) < 1e-12f
                        || Anschluss(p[0], Vector2.Normalize(p[0] - p[1]), strassen[k], strassen, j => !vergeben[j]) is not { } t)
                    {
                        break;
                    }
                    vergeben[t.Strasse] = true;
                    // schliesst mit dem Ende an: in eigener Richtung davor; mit dem Anfang: umgedreht davor
                    kette.AddFirst((t.Strasse, t.AmAnfang));
                }
            }
            if (kette.Count == 1)
            {
                ergebnis.Add(([i], [.. s0.Select(q => (q.X, q.Y))]));
                continue;
            }
            var linie = new List<(float X, float Z)>();
            foreach (var (k, um) in kette)
            {
                var p = Ausgerichtet(strassen[k], um);
                // der gemeinsame Punkt (bis 0,02 auseinander) nur einmal
                linie.AddRange(p.Skip(linie.Count == 0 ? 0 : 1).Select(q => (q.X, q.Y)));
            }
            ergebnis.Add(([.. kette.Select(e => e.K)], linie));
        }
        return ergebnis;
    }

    /// <summary>
    /// Welle K3: Striche auf einem geschlossenen Ring im Takt 0,45 (Luecke so angepasst, dass eine ganze Zahl Striche
    /// rundum passt; sonst laege am Stoss eine kurze Luecke oder zwei Striche aneinander).
    /// </summary>
    public static List<(float X1, float Z1, float X2, float Z2)> RingStriche(List<(float X, float Z)> ring)
    {
        var laenge = 0f;
        for (var i = 0; i + 1 < ring.Count; i++)
        {
            laenge += MathF.Sqrt(((ring[i + 1].X - ring[i].X) * (ring[i + 1].X - ring[i].X)) + ((ring[i + 1].Z - ring[i].Z) * (ring[i + 1].Z - ring[i].Z)));
        }
        var anzahl = Math.Max(1, (int)MathF.Round(laenge / 0.45f));
        return [.. Striche(ring, 0.2f, (laenge / anzahl) - 0.2f).Take(anzahl)];
    }

    private static List<Vector2> Ausgerichtet(List<Vector2> p, bool umgedreht) => umgedreht ? [.. Enumerable.Reverse(p)] : p;

    internal static List<Vector2> Vektoren(object? punkte) => [.. Punkte(punkte).Select(p => new Vector2(p.X, p.Z))];

    /// <summary>Stehplatz vor der Tuer: 0,3 den Fussweg (Zugang aus stadt.json) entlang, hoechstens bis zu seinem ersten Knick.</summary>
    private static (float X, float Z) Stehplatz(IStadtSicht stadt, string id, float tx, float tz)
    {
        if (Get(stadt.Zugaenge, id) is not PyObjekt zugang)
        {
            return (tx, tz);
        }
        var weg = Vektoren(zugang["weg"]);
        if (weg.Count < 2)
        {
            return (tx, tz);
        }
        var d = weg[1] - weg[0];
        var l = d.Length();
        if (l < 1e-4f)
        {
            return (tx, tz);
        }
        var p = weg[0] + (d / l * MathF.Min(0.3f, l));
        return (p.X, p.Y);
    }

    /// <summary>Engine-Welle A 1.4.0: die Striche der Mittellinien nebenher (eigene Methode, nichts vom grossen Bau eingefangen).</summary>
    private static Spaeter<List<Teil>> MittellinienSpaeter(List<List<Vector2>> strassen, List<Zebra> zebras, List<Zebra> furten)
    {
        return new Spaeter<List<Teil>>(() =>
        {
            var striche = new List<Teil>();
            foreach (var (x1, z1, x2, z2) in MittellinienStriche(strassen))
            {
                // Welle A 0.54.0: kein Strich auf einem Zebrastreifen
                if (zebras.Any(z => z.Drauf(new Vector2(x1, z1), 0.02f) || z.Drauf(new Vector2(x2, z2), 0.02f) || z.Drauf(new Vector2((x1 + x2) / 2, (z1 + z2) / 2), 0.02f))
                    || furten.Any(z => z.Drauf(new Vector2(x1, z1), 0.02f) || z.Drauf(new Vector2(x2, z2), 0.02f) || z.Drauf(new Vector2((x1 + x2) / 2, (z1 + z2) / 2), 0.02f)))
                {
                    continue;
                }
                striche.Add(Band(x1, z1, x2, z2, Hoehe.Mittellinie, 0.04f, Farben.Mittellinie));
            }
            return striche;
        });
    }

    /// <summary>Engine-Welle A 1.4.0: das Laufraster nebenher (eigene Methode, nichts vom grossen Bau eingefangen).</summary>
    private static Spaeter<Wegsuche> LaufrasterSpaeter(float breite, float tiefe, List<(double, double, double, double)> hindernisse,
        List<List<Vector2>> wasser, List<List<Vector2>> gleise, List<List<Vector2>> strassen, List<List<Vector2>> wege, List<StadtOrt> orte,
        List<(List<Vector2> Linie, float Halb)> gehwege, List<Teich> teiche, List<Steg> stege, List<List<Vector2>> inseln, List<Zebra> zebras,
        Gelaende? gelaende) =>
        new(() => LaufrasterBauen(breite, tiefe, hindernisse, wasser, gleise, strassen, wege, orte, gehwege, teiche, stege, inseln, zebras, gelaende), sofort: true);

    /// <summary>
    /// Welle A 1.0.0 (Sammelliste 403): liegt das Ende <paramref name="a"/> (sonst das andere) eines Stegs am Land? Ein Stueck
    /// hinter dem Ende (<see cref="StegLandProbe"/>) liegt in keinem Teich.
    /// </summary>
    public static bool StegEndeAmLand(Steg steg, IReadOnlyList<Teich>? teiche, bool a)
    {
        var (ea, eb) = steg.Enden;
        var (ende, anderes) = a ? (ea, eb) : (eb, ea);
        var raus = ende - anderes;
        if (raus.LengthSquared() <= 0)
        {
            return true;
        }
        var probe = ende + (Vector2.Normalize(raus) * StegLandProbe);
        return !(teiche ?? []).Any(t => Wasser.Drin(t.Rund, probe));
    }

    /// <summary>Abstand hinter einem Stegende, an dem geprueft wird, ob dort Land ist.</summary>
    public const float StegLandProbe = 0.05f;

    /// <summary>
    /// Welle A 1.0.0 (Sammelliste 403): feine Sperre um das Wasser ergaenzt: die Mitte der Figur darf nie im Wasser eines Teichs
    /// stehen, ausser auf einem Steg (Rechteck des Stegs). Ohne Teiche bleibt die feine Sperre, wie sie war.
    /// </summary>
    private static Func<double, double, bool>? MitWasserSperre(Func<double, double, bool>? fein, List<Teich>? teiche, List<Steg>? stege)
    {
        if (teiche is not { Count: > 0 })
        {
            return fein;
        }
        var flaechen = teiche.Select(t => (Rund: t.Rund, X1: t.Rund.Min(p => p.X), Z1: t.Rund.Min(p => p.Y), X2: t.Rund.Max(p => p.X), Z2: t.Rund.Max(p => p.Y))).ToList();
        var rechtecke = (stege ?? []).Select(st => st.Rechteck).ToList();
        bool ImWasser(double x, double z)
        {
            if (StegAltFuerTests)
            {
                return false;
            }
            var p = new Vector2((float)x, (float)z);
            foreach (var f in flaechen)
            {
                if (p.X < f.X1 || p.X > f.X2 || p.Y < f.Z1 || p.Y > f.Z2 || !Wasser.Drin(f.Rund, p))
                {
                    continue;
                }
                foreach (var (x1, z1, x2, z2) in rechtecke)
                {
                    if (p.X >= x1 && p.X <= x2 && p.Y >= z1 && p.Y <= z2)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
        return fein is null ? ImWasser : (x, z) => fein(x, z) || ImWasser(x, z);
    }

    /// <summary>Gegenprobe Welle A 1.0.0 (Sammelliste 403): Steg wie bis A 0.99.0 (beide Enden verlaengert, Wasser nicht fein gesperrt; nur Tests, je Ablauf).</summary>
    public static bool StegAltFuerTests { get => StegAlt.Value; set => StegAlt.Value = value; }
    private static readonly AsyncLocal<bool> StegAlt = new();

    /// <summary>Gegenprobe Welle A 0.54.0: Laufraster wie bis A 0.53.2 (Fahrbahn jeder Strasse frei zum Gehen).</summary>
    public static bool FahrbahnFreiAltFuerTests
    {
        get => FahrbahnAlt.Value;
        set => FahrbahnAlt.Value = value;
    }

    private static readonly AsyncLocal<bool> FahrbahnAlt = new();

    /// <summary>Gegenprobe Welle A 0.55.0: Teil „schild“ wie bis A 0.54.0 zeichnen (Platte in der Farbe der Rolle).</summary>
    public static bool SchildAltFuerTests
    {
        get => SchildAlt.Value;
        set => SchildAlt.Value = value;
    }

    private static readonly AsyncLocal<bool> SchildAlt = new();

    /// <summary>Gegenprobe Sammelliste 315: Teil „kreis“ auch ganz ueber dem Block zeichnen (wie bis A 0.68.0, nur Tests, je Ablauf).</summary>
    public static bool KreisAltFuerTests { get => KreisAlt.Value; set => KreisAlt.Value = value; }
    private static readonly AsyncLocal<bool> KreisAlt = new();

    /// <summary>Gegenprobe Weisung P12-218 Punkt 1: Feld „windfang“ nicht beachten (wie bis A 0.69.0, nur Tests, je Ablauf).</summary>
    public static bool WindfangAltFuerTests { get => WindfangAlt.Value; set => WindfangAlt.Value = value; }
    private static readonly AsyncLocal<bool> WindfangAlt = new();

    /// <summary>Gegenprobe Welle A 0.70.0: Tuer eines Zierwegs immer auf dem Kasten des Hauses (wie bis A 0.69.0, nur Tests, je Ablauf).</summary>
    public static bool ZierKastenAltFuerTests { get => ZierKastenAlt.Value; set => ZierKastenAlt.Value = value; }
    private static readonly AsyncLocal<bool> ZierKastenAlt = new();

    /// <summary>Punkt liegt mehr als <see cref="ZierInnen"/> innerhalb aller vier Waende des Kastens.</summary>
    private static bool ImKastenInnen(Vector3 min, Vector3 max, Vector2 p) =>
        p.X > min.X + ZierInnen && p.X < max.X - ZierInnen && p.Y > min.Z + ZierInnen && p.Y < max.Z - ZierInnen;

    /// <summary>Abstand zur Wand des Kastens, ab dem eine Zierweg-Tuer als „innen“ gilt.</summary>
    public const float ZierInnen = 0.05f;

    /// <summary>Gegenprobe Sammelliste 312: Feld <c>modell_tuer</c> eines Zierwegs nicht beachten (wie bis A 0.68.0, nur Tests, je Ablauf).</summary>
    public static bool ZierModellTuerAltFuerTests { get => ZierModellTuerAlt.Value; set => ZierModellTuerAlt.Value = value; }
    private static readonly AsyncLocal<bool> ZierModellTuerAlt = new();

    /// <summary>Gegenprobe Sammelliste 292: Rueckseite eines Gebaeudes ohne Ort bis zur Vorderwand absuchen (wie bis A 0.65.0, nur Tests, je Ablauf).</summary>
    public static bool RueckseiteAltFuerTests { get => RueckseiteAlt.Value; set => RueckseiteAlt.Value = value; }
    private static readonly AsyncLocal<bool> RueckseiteAlt = new();

    private static void Linie(List<Teil> teile, List<(float X, float Z)> p, float hoehe, float breite, Vector3 farbe)
    {
        for (var i = 0; i + 1 < p.Count; i++)
        {
            // um die halbe Breite verlaengert, damit Knicke und Kreuzungen geschlossen sind
            var (x1, z1) = p[i];
            var (x2, z2) = p[i + 1];
            var d = new Vector2(x2 - x1, z2 - z1);
            if (d.LengthSquared() < 1e-12f)
            {
                continue;
            }
            var e = Vector2.Normalize(d) * (breite / 2);
            teile.Add(Band(x1 - e.X, z1 - e.Y, x2 + e.X, z2 + e.Y, hoehe, breite, farbe));
        }
    }

    /// <summary>_along/_dashes: Striche der Laenge <paramref name="strich"/> mit Luecke entlang der Linie.</summary>
    public static List<(float X1, float Z1, float X2, float Z2)> Striche(List<(float X, float Z)> p, float strich, float luecke)
    {
        var liste = new List<(float, float, float, float)>();
        var rest = 0f;
        for (var i = 0; i + 1 < p.Count; i++)
        {
            var (x1, z1) = p[i];
            var (x2, z2) = p[i + 1];
            var laenge = MathF.Sqrt(((x2 - x1) * (x2 - x1)) + ((z2 - z1) * (z2 - z1)));
            if (laenge <= 0)
            {
                continue;
            }
            float dx = (x2 - x1) / laenge, dz = (z2 - z1) / laenge;
            var pos = rest;
            while (pos <= laenge)
            {
                var x = x1 + (dx * pos);
                var z = z1 + (dz * pos);
                liste.Add((x, z, x + (dx * strich), z + (dz * strich)));
                pos += strich + luecke;
            }
            rest = pos - laenge;
        }
        return liste;
    }

    /// <summary>Breite des Gleises in der Welt; die Schienen des Train Kit (1 breit, 4 lang) werden darauf skaliert.</summary>
    public const float GleisBreite = 0.42f;

    /// <summary>Welle A 0.70.0: ein Gleisende so nah am Kartenrand oder an einem anderen Gleis bekommt keinen Prellbock.</summary>
    public const float PrellbockRand = 0.05f;

    /// <summary>Welle A 0.70.0: ein Gleisende so nah an einem Haus (Abstellgleis bis ins Tor) bekommt keinen Prellbock.</summary>
    public const float PrellbockHaus = 0.1f;

    /// <summary>Welle A 0.70.0: Rot des Prellbocks (Pufferbohle).</summary>
    public static readonly Vector3 PrellbockRot = Netz.Farbe("#B3261E");

    /// <summary>Gegenprobe Weisung P12-218 Punkt 3: kein Prellbock (wie bis A 0.69.0, nur Tests, je Ablauf).</summary>
    public static bool PrellbockAltFuerTests { get => PrellbockAlt.Value; set => PrellbockAlt.Value = value; }
    private static readonly AsyncLocal<bool> PrellbockAlt = new();

    /// <summary>Beide Enden eines Gleises mit der Richtung aus dem Gleis hinaus.</summary>
    private static IEnumerable<(Vector2 Ende, Vector2 Hinaus)> GleisEnden(List<Vector2> p)
    {
        var a = p.FindIndex(q => Vector2.DistanceSquared(q, p[0]) > 1e-8f);
        var b = p.FindLastIndex(q => Vector2.DistanceSquared(q, p[^1]) > 1e-8f);
        if (a < 0 || b < 0)
        {
            yield break;
        }
        yield return (p[0], Vector2.Normalize(p[0] - p[a]));
        yield return (p[^1], Vector2.Normalize(p[^1] - p[b]));
    }

    /// <summary>
    /// Welle A 0.70.0: Prellbock am Gleisende (Pufferbohle rot-weiss quer ueber beiden Schienen, dahinter zwei Stuetzen aus
    /// Stahl); im Rahmen x = quer, z = laengs hinaus, gedreht um das Ende (<see cref="Teil.Dreh"/>).
    /// </summary>
    public static IEnumerable<Teil> Prellbock(Vector2 ende, Vector2 hinaus)
    {
        var grad = (float)(Math.Atan2(-hinaus.X, hinaus.Y) * 180 / Math.PI);
        var dreh = new Vector3(ende.X, ende.Y, grad);
        Teil T(float x1, float y1, float z1, float x2, float y2, float z2, Vector3 farbe) =>
            new(TeilArt.Quader, ende.X + x1, y1, ende.Y + z1, ende.X + x2, y2, ende.Y + z2, farbe) { Dreh = dreh };
        var unten = Hoehe.Schiene;
        // Pufferbohle: aussen rot, in der Mitte weiss
        yield return T(-0.24f, unten + 0.08f, -0.06f, -0.08f, unten + 0.17f, 0f, PrellbockRot);
        yield return T(-0.08f, unten + 0.08f, -0.06f, 0.08f, unten + 0.17f, 0f, Weiss);
        yield return T(0.08f, unten + 0.08f, -0.06f, 0.24f, unten + 0.17f, 0f, PrellbockRot);
        // Stuetzen ueber den Schienen, schraeg nach hinten als Stufe angedeutet
        foreach (var x in (float[])[-SpurHalb, SpurHalb])
        {
            yield return T(x - 0.025f, Hoehe.Gleisbett, 0f, x + 0.025f, unten + 0.17f, 0.05f, Farben.Stahl);
            yield return T(x - 0.025f, Hoehe.Gleisbett, 0.05f, x + 0.025f, unten + 0.1f, 0.12f, Farben.Stahl);
        }
    }

    /// <summary>
    /// Lage eines Modells: Kasten des Modells (min) auf <paramref name="ort"/>, skaliert mit
    /// <paramref name="mass"/>, Vorderseite (+Z) in Richtung <paramref name="richtung"/> (Karte x/z).
    /// </summary>
    public static Matrix4x4 Lage(GrafikModell m, Vector3 anker, Vector3 mass, Vector3 ort, Vector2 richtung)
    {
        var drehung = Drehung(richtung);
        return Matrix4x4.CreateTranslation(-anker) * Matrix4x4.CreateScale(mass) * drehung * Matrix4x4.CreateTranslation(ort);
    }

    /// <summary>Drehung um die Hochachse, die +Z auf die Richtung (x, z) legt.</summary>
    public static Matrix4x4 Drehung(Vector2 richtung)
    {
        if (richtung.LengthSquared() < 1e-12f)
        {
            return Matrix4x4.Identity;
        }
        var d = Vector2.Normalize(richtung);
        var winkel = MathF.Atan2(d.X, d.Y);
        var m = Matrix4x4.CreateRotationY(winkel);
        var probe = Vector3.TransformNormal(Vector3.UnitZ, m);
        // Drehsinn von System.Numerics nicht voraussetzen: sonst die Gegenrichtung
        return MathF.Abs(probe.X - d.X) + MathF.Abs(probe.Z - d.Y) < 1e-3f ? m : Matrix4x4.CreateRotationY(-winkel);
    }

    /// <summary>
    /// Gleis als glatte Baender (Nachtrag 1, Wunsch 1): Schotterbett, Schwellen alle 0,14 quer zur
    /// Fahrtrichtung, zwei Schienen im festen Abstand; die Boegen kommen aus <see cref="Kurve.Glatt"/>,
    /// darum ohne Knick und Versatz. Bahnuebergaenge (Wunsch Nico 25.10.2026): wo eine Strasse oder ein
    /// Fussweg das Gleis kreuzt, liegt ein Belag in Richtung der Strasse (so breit wie sie) ueber dem
    /// Schotter, die Schienen schauen heraus; an Strassen stehen davor Warnkreuze.
    /// </summary>
    private static List<(Vector2 Mitte, bool Strasse)> Gleis(List<Teil> teile, List<Streifen> streifen, List<Bahnuebergang.Kreuz> andreaskreuze, List<Vector2> p, List<List<Vector2>> strassen, List<List<Vector2>> wege)
    {
        streifen.Add(new Streifen(p, GleisBreite, 0.004f, Hoehe.Gleisbett, Farben.Gleisbett));
        var uebergaenge = new List<(Vector2 Mitte, Vector2 Richtung, float Breite)>();
        foreach (var (linien, breite, farbe, kreuze) in (IEnumerable<(List<List<Vector2>>, float, Vector3, bool)>)
            [(strassen, StrassenBreite, Farben.Strasse, true), (wege, WegBreite, Mischen(Farben.Strasse, Weiss, 0.45f), false)])
        {
            foreach (var linie in linien)
            {
                foreach (var (mitte, richtung, sinus) in Kreuzungen(p, linie))
                {
                    Uebergang(streifen, andreaskreuze, p, mitte, richtung, sinus, breite, farbe, kreuze);
                    uebergaenge.Add((mitte, richtung, breite));
                }
            }
        }
        bool AufUebergang(Vector2 q) => uebergaenge.Any(u =>
        {
            var d = q - u.Mitte;
            var quer = new Vector2(-u.Richtung.Y, u.Richtung.X);
            return MathF.Abs(Vector2.Dot(d, quer)) <= (u.Breite / 2) + 0.02f && MathF.Abs(Vector2.Dot(d, u.Richtung)) <= 0.6f;
        });
        foreach (var (ort, richtung) in Kurve.Entlang(p, 0.14f, 0.07f))
        {
            if (AufUebergang(ort))
            {
                continue;
            }
            var quer = new Vector2(-richtung.Y, richtung.X) * 0.17f;
            streifen.Add(new Streifen([ort - quer, ort + quer], 0.06f, Hoehe.Gleisbett, Hoehe.Schwelle, Farben.Schwelle));
        }
        foreach (var seite in (float[])[-SpurHalb, SpurHalb])
        {
            streifen.Add(new Streifen(Kurve.Versetzt(p, seite), 0.03f, Hoehe.Schwelle, Hoehe.Schiene, Farben.Stahl));
        }
        return [.. uebergaenge.Select(u => (u.Mitte, u.Breite == StrassenBreite))];
    }

    /// <summary>
    /// Schnittpunkte der Linie <paramref name="b"/> (Strasse, Weg) mit <paramref name="a"/> (Gleis, Fluss):
    /// Ort, Richtung von <paramref name="b"/> dort und |sin| des Winkels zwischen beiden.
    /// </summary>
    public static List<(Vector2 Mitte, Vector2 Richtung, float Sinus)> Kreuzungen(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
    {
        var liste = new List<(Vector2, Vector2, float)>();
        // Welle Nachladen (grosse Karten): Linienzuege, deren Rahmen sich nicht beruehren, kreuzen sich nicht (gleiches Ergebnis,
        // Zugabe 0,01 gegen Rundung)
        if (a.Count < 2 || b.Count < 2 || !RahmenBeruehren(a, b, 0.01f))
        {
            return liste;
        }
        for (var i = 0; i + 1 < a.Count; i++)
        {
            for (var j = 0; j + 1 < b.Count; j++)
            {
                var r = a[i + 1] - a[i];
                var s = b[j + 1] - b[j];
                var nenner = (r.X * s.Y) - (r.Y * s.X);
                if (MathF.Abs(nenner) < 1e-9f)
                {
                    continue;
                }
                var q = b[j] - a[i];
                var t = ((q.X * s.Y) - (q.Y * s.X)) / nenner;
                var u = ((q.X * r.Y) - (q.Y * r.X)) / nenner;
                if (t < 0 || t > 1 || u < 0 || u > 1)
                {
                    continue;
                }
                var mitte = a[i] + (r * t);
                // dieselbe Kreuzung nicht doppelt (Schnitt genau auf einer Ecke)
                if (liste.Any(k => Vector2.Distance(k.Item1, mitte) < 0.05f))
                {
                    continue;
                }
                var rn = Vector2.Normalize(r);
                var sn = Vector2.Normalize(s);
                liste.Add((mitte, sn, MathF.Max(0.2f, MathF.Abs((rn.X * sn.Y) - (rn.Y * sn.X)))));
            }
        }
        return liste;
    }

    /// <summary>Welle Nachladen: gleiche Linienzuege (gleich wie <c>SequenceEqual</c>), fuer Gruppen.</summary>
    private sealed class GleicheLinie : IEqualityComparer<List<Vector2>>
    {
        public bool Equals(List<Vector2>? x, List<Vector2>? y) => ReferenceEquals(x, y) || (x is not null && y is not null && x.SequenceEqual(y));

        public int GetHashCode(List<Vector2> l)
        {
            var h = new HashCode();
            h.Add(l.Count);
            foreach (var p in l)
            {
                h.Add(p);
            }
            return h.ToHashCode();
        }
    }

    /// <summary>Welle Nachladen: beruehren sich die Rahmen zweier Linienzuege (mit Zugabe <paramref name="rand"/>)?</summary>
    internal static bool RahmenBeruehren(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b, float rand)
    {
        var (a0, a1) = Rahmen(a);
        var (b0, b1) = Rahmen(b);
        return a1.X + rand >= b0.X && b1.X + rand >= a0.X && a1.Y + rand >= b0.Y && b1.Y + rand >= a0.Y;
    }

    /// <summary>Welle Nachladen: kleinste und groesste Ecke eines Linienzugs.</summary>
    internal static (Vector2 Min, Vector2 Max) Rahmen(IReadOnlyList<Vector2> l)
    {
        var (min, max) = (new Vector2(float.MaxValue), new Vector2(float.MinValue));
        for (var i = 0; i < l.Count; i++)
        {
            (min, max) = (Vector2.Min(min, l[i]), Vector2.Max(max, l[i]));
        }
        return (min, max);
    }

    /// <summary>Bahnuebergang: Belag in Richtung der Strasse ueber dem Gleisbett, an Strassen Warnkreuze davor.</summary>
    private static void Uebergang(List<Streifen> streifen, List<Bahnuebergang.Kreuz> kreuze, IReadOnlyList<Vector2> gleis, Vector2 mitte, Vector2 richtung, float sinus, float breite, Vector3 farbe, bool mitKreuzen)
    {
        var halb = ((GleisBreite / 2) + 0.12f) / sinus;
        streifen.Add(new Streifen([mitte - (richtung * halb), mitte + (richtung * halb)], breite, Hoehe.Gleisbett, Hoehe.Uebergang, farbe));
        if (!mitKreuzen)
        {
            return;
        }
        // P8-49 (Nico, Variante c): Andreaskreuz mit Lichtern und Halbschranke je Fahrtrichtung rechts vor dem Gleis (P8-50),
        // diagonal gegenueber, ausserhalb des Lichtraums (mit den Laternen im Paket-Netz, Bahnuebergang.Hinzu in PaketNetzBauen; im Netz der Stadt leuchtet nichts)
        kreuze.AddRange(Bahnuebergang.Plaetze(mitte, richtung, sinus, gleis));
    }

    /// <summary>
    /// Bruecke (Wunsch Nico 25.10.2026): wo eine Strasse den Fluss kreuzt, ein Steinbett unter der
    /// Fahrbahn bis ueber beide Ufer, Bruestungen an beiden Seiten und Pfosten an den Enden.
    /// </summary>
    private static float BrueckeHalb(float sinus) => (((FlussBreite + 0.3f) / 2) + 0.18f) / sinus;

    /// <summary>
    /// Weisung P12-214 (Nico: „das komische Geruest sollte bei dem alten Modell entfernt werden“): ein Gebaeude ohne Ort
    /// zeichnet keine Teile „geruest“ (Baugeruest des Orts Talheim-Nord, solange seine Abnahme offen ist). Sonst bleibt das
    /// Aussehen gleich (die fertige Form mit Glasfront wurde bewusst nicht gewaehlt, E1). Fassung 0 hat nur das Stellwerk
    /// ohne Geruest, darum bitgleich; der Ort Talheim-Nord selbst bleibt wie bisher.
    /// </summary>
    internal static List<(int Nr, PyObjekt Teil)> OhneGeruest(List<(int Nr, PyObjekt Teil)> teile) =>
        GeruestAltFuerTests ? teile : [.. teile.Where(t => !Py.Gleich(t.Teil["art"], "geruest"))];

    /// <summary>Gegenprobe P12-214: Gebaeude ohne Ort zeigen das Geruest wie bis A 0.68.1 (nur Tests, je Ablauf).</summary>
    public static bool GeruestAltFuerTests { get => GeruestAlt.Value; set => GeruestAlt.Value = value; }
    private static readonly AsyncLocal<bool> GeruestAlt = new();

    /// <summary>Gegenprobe Weisung P12-214: Bruecke nur so breit wie die Fahrbahn (wie bis A 0.68.1, Gehweg lief aussen am Gelaender vorbei; nur Tests, je Ablauf).</summary>
    public static bool BrueckeSchmalAltFuerTests { get => BrueckeSchmalAlt.Value; set => BrueckeSchmalAlt.Value = value; }
    private static readonly AsyncLocal<bool> BrueckeSchmalAlt = new();

    private static void Bruecke(List<Teil> teile, List<Streifen> streifen, Vector2 mitte, Vector2 richtung, float sinus, float gehweg)
    {
        var halb = BrueckeHalb(sinus);
        var a = mitte - (richtung * halb);
        var b = mitte + (richtung * halb);
        var stein = Netz.Farbe("#9A94A8");
        // Steinbett breiter als Fahrbahn und Gehweg: sein Rand zeigt die Bruecke, Fahrbahn und Gehweg liegen darauf
        streifen.Add(new Streifen([a, b], StrassenBreite + (2 * gehweg) + 0.2f, 0f, Hoehe.Strasse - 0.012f, stein));
        var quer = new Vector2(-richtung.Y, richtung.X);
        foreach (var seite in (float[])[-1f, 1f])
        {
            // Bruestung aussen an der Gehwegkante (P12-214), ohne Gehweg an der Fahrbahnkante
            var versatz = quer * seite * ((StrassenBreite / 2) + gehweg + 0.06f);
            streifen.Add(new Streifen([a + versatz, b + versatz], 0.07f, Hoehe.Strasse - 0.012f, Hoehe.Strasse + 0.16f, stein * 0.92f));
            foreach (var ende in (Vector2[])[a + versatz, b + versatz])
            {
                teile.Add(Quader(ende.X - 0.07f, 0f, ende.Y - 0.07f, ende.X + 0.07f, Hoehe.Strasse + 0.24f, ende.Y + 0.07f, stein * 0.85f));
            }
        }
    }

    /// <summary>Halber Abstand der Schienen (Normalspur 1,435 m).</summary>
    public const float SpurHalb = 1.435f / 2 * Meter;

    /// <summary>Tuer der eigenen Haeuser: 1,8 m breit (zweifluegelig), 2,2 m hoch.</summary>
    public const float TuerBreite = 1.8f * Meter;

    public const float TuerHoehe = 2.2f * Meter;

    /// <summary>Breite der Zuege (Wagenkasten 2,9 m).</summary>
    public const float ZugBreite = 2.9f * Meter;
}
