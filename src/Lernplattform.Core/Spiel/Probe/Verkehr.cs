using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Fahrende Autos (Sammelliste 126, Phase 8 W7, sechs Autos: Nico 2b, 27.10.2026; Nachlauf 3a), nur Darstellung: kein Ereignis, keine Spielregel, kein
/// Einfluss auf die Wege der Figur. Feste Strecken auf den Strassen (rechte Spur), Rundlauf (am Rand der Karte
/// hinaus, am Anfang wieder herein), feste Abstaende. Ein Auto haelt, wenn die Figur vor ihm auf der Fahrbahn steht
/// oder geht, wenn das Auto vor ihm zu nah ist und vor einem Kreuzungsfeld, das ein anderes Auto belegt. Keine Uhr,
/// kein Zufall: der Stand ergibt sich nur aus der Summe der Schritte <see cref="Weiter"/> und der Lage der Figur.
/// </summary>
public sealed class Verkehr
{
    /// <summary>Tempo in Karteneinheiten je Sekunde (2,2 = 11 m/s, rund 40 km/h).</summary>
    public const float Tempo = 2.2f;

    /// <summary>Abstand der Spurmitte von der Strassenmitte.</summary>
    public const float Spur = 0.17f;

    /// <summary>Wie weit vor dem Auto die Figur ein Halt ist (Weite in Fahrtrichtung, quer = ganze Strasse).</summary>
    public const float FigurVoraus = 0.9f;

    /// <summary>Kleinster Abstand zum Auto davor in derselben Richtung.</summary>
    public const float Abstand = 0.75f;

    /// <summary>Halbmesser eines Kreuzungsfelds (halbe Strassenbreite und etwas Rand).</summary>
    public const float FeldRadius = (StadtSzene.StrassenBreite / 2) + 0.12f;

    /// <summary>So weit vor dem Kreuzungsfeld wartet ein Auto, wenn das Feld belegt ist.</summary>
    public const float FeldVorlauf = 0.35f;

    /// <summary>Die Autos fahren nach dem letzten Bedienen oder Gehen noch so viele Sekunden (Nico 3a).</summary>
    public const double Nachlauf = 10;

    /// <summary>Abstand von der Mitte des Autos zu Front und Heck.</summary>
    private const float Front = 0.3f;

    public sealed class Auto(string modell, List<Vector2> strecke, float start)
    {
        public string Modell { get; } = modell;
        public List<Vector2> Strecke { get; } = strecke;
        public float Laenge { get; } = Enumerable.Range(1, strecke.Count - 1).Sum(i => Vector2.Distance(strecke[i - 1], strecke[i]));
        public float Weg { get; set; } = start;
        public bool Haelt { get; set; }

        /// <summary>Phase 8 W11: Tempo dieses Fahrzeugs bzw. dieser Festgestalt (Karteneinheiten je Sekunde).</summary>
        public float Tempo { get; init; } = Verkehr.Tempo;

        /// <summary>Phase 8 W11: Nummer der Strecke in <see cref="Strecken"/>.</summary>
        public int Nummer { get; init; }

        /// <summary>Welle K3b: Lage ueber die Summe der Stuecklaengen suchen (lange automatische Strecken); die festen Strecken rechnen wie bisher.</summary>
        public bool Schnell { get; init; }

        private float[]? _summen;

        /// <summary>Ort und Fahrtrichtung bei Weg <paramref name="weg"/> (Rundlauf).</summary>
        public (Vector2 Ort, Vector2 Richtung) Lage(float weg)
        {
            weg = ((weg % Laenge) + Laenge) % Laenge;
            if (Schnell)
            {
                return LageSchnell(weg);
            }
            for (var i = 1; i < Strecke.Count; i++)
            {
                var d = Vector2.Distance(Strecke[i - 1], Strecke[i]);
                if (weg <= d || i == Strecke.Count - 1)
                {
                    var r = Vector2.Normalize(Strecke[i] - Strecke[i - 1]);
                    return (Strecke[i - 1] + (r * Math.Min(weg, d)), r);
                }
                weg -= d;
            }
            return (Strecke[0], Vector2.UnitX);
        }

        public (Vector2 Ort, Vector2 Richtung) Lage() => Lage(Weg);

        private (Vector2 Ort, Vector2 Richtung) LageSchnell(float weg)
        {
            var summen = _summen;
            if (summen is null)
            {
                summen = new float[Strecke.Count];
                for (var k = 1; k < Strecke.Count; k++)
                {
                    summen[k] = summen[k - 1] + Vector2.Distance(Strecke[k - 1], Strecke[k]);
                }
                _summen = summen;
            }
            var i = Array.BinarySearch(summen, weg);
            i = Math.Clamp(i < 0 ? ~i : i, 1, Strecke.Count - 1);
            var r = Vector2.Normalize(Strecke[i] - Strecke[i - 1]);
            return (Strecke[i - 1] + (r * MathF.Min(weg - summen[i - 1], summen[i] - summen[i - 1])), r);
        }
    }

    public List<Auto> Autos { get; } = [];

    /// <summary>Phase 8 W11: kleinster Abstand zum Vordermann (zu den Festzeiten 0,85 statt <see cref="Abstand"/>).</summary>
    public float AbstandVorne { get; init; } = Abstand;

    /// <summary>Mitten der Kreuzungsfelder (Stellen, an denen sich Strassen treffen).</summary>
    public List<Vector2> Kreuzungen { get; } = [];

    /// <summary>Welle K3b: die Strecken sind geschlossene Rundwege (<see cref="Autostrecken"/>), kein Sprung an den Anfang.</summary>
    public bool Rundweg { get; init; }

    /// <summary>Welle K3b: Ringe der Kreisverkehre (Mitte, Radius der Mittellinie); wer im Ring faehrt, hat Vorfahrt.</summary>
    public List<(Vector2 Mitte, float Radius)> Ringe { get; } = [];

    /// <summary>Welle K3b: die Strassen, fuer die dieser Verkehr gebaut ist (null = unbekannt, z. B. im Uebergang der Festzeiten ohne Angabe).</summary>
    public IReadOnlyList<IReadOnlyList<Vector2>>? FuerStrassen { get; init; }

    /// <summary>
    /// Welle K3b: passt der Verkehr zu den Strassen der Szene? Nur dann behalten die Autos beim Neubau der Szene ihre
    /// Lage (im Spiel aendern sich die Strassen einer Stadt nie; im Werkzeug nach jeder Aenderung).
    /// </summary>
    public bool Passt(StadtSzene szene) => (FuerStrassen is not { } eigene
        || (eigene.Count == szene.Strassen.Count && eigene.Zip(szene.Strassen).All(p => p.First.SequenceEqual(p.Second))))
        && (FuerTag is not { } tag || tag == szene.Spieltag)
        && Zebras.SequenceEqual(szene.Zebras);

    /// <summary>
    /// Welle A 0.54.0 (Weisung P12-115): Zebrastreifen der Szene; ein Auto haelt davor, solange die Figur darauf steht.
    /// </summary>
    public List<Zebra> Zebras { get; } = [];

    /// <summary>
    /// Welle A 0.54.0 (Sammelliste 254): steht die Figur auf der Fahrbahn (Mitte der Figur naeher als die halbe
    /// Strassenbreite an der Mitte einer Strasse)? Auf dem Gehweg nicht. Ohne bekannte Strassen gilt die Regel bis A 0.53.2.
    /// </summary>
    public bool AufFahrbahn(Vector2 figur) => FuerStrassen is null || FuerStrassen.Any(s => Kurve.Abstand(figur, s) < StadtSzene.StrassenBreite / 2);

    /// <summary>Gegenprobe Welle A 0.54.0: Anhalten wie bis A 0.53.2 (Figur bis 0,45 quer zur Spur, auch auf dem Gehweg).</summary>
    public static bool AnhaltenAltFuerTests
    {
        get => AnhaltenAlt.Value;
        set => AnhaltenAlt.Value = value;
    }

    private static readonly AsyncLocal<bool> AnhaltenAlt = new();

    /// <summary>
    /// Welle A 0.51.0: Spieltag, fuer den die automatischen Strecken gelten (null = feste Strecken, gleich an jedem Tag).
    /// Ein anderer Tag der Szene baut den Verkehr neu (<see cref="Passt"/>).
    /// </summary>
    public long? FuerTag { get; set; }

    /// <summary>Welle K3b: so weit vor einem Feld am Ring hat ein Auto im Ring Vorfahrt vor einem, das einfahren will.</summary>
    public const float VorfahrtWeite = 1.2f;

    /// <summary>Bis zu dieser Zeit (Sekunden der Bildschleife) fahren die Autos (Nico 3a).</summary>
    public double FahrenBis { get; private set; } = Nachlauf;

    /// <summary>Bedienen oder Gehen zur Zeit <paramref name="jetzt"/>: die Autos fahren noch <see cref="Nachlauf"/> Sekunden.</summary>
    public void Bedient(double jetzt) => FahrenBis = Math.Max(FahrenBis, jetzt + Nachlauf);

    /// <summary>Fahren die Autos zur Zeit <paramref name="jetzt"/>?</summary>
    public bool Faehrt(double jetzt) => jetzt < FahrenBis;

    /// <summary>Strecken der Mitte der Strasse (vor dem Versatz auf die rechte Spur), Modell und Start auf der Strecke.</summary>
    public static readonly (string Modell, (float X, float Z)[] Punkte, float Start)[] Strecken =
    [
        // Hauptstrasse in beide Richtungen, Nord-Sued-Strasse nach Sueden und weiter nach Osten,
        // Runde ueber die Strasse am Rand des Parks und die Querstrasse nach Norden
        ("car-taxi", [(-1.5f, 20.8f), (33.5f, 20.8f)], 4f),
        ("car-van", [(33.5f, 20.8f), (-1.5f, 20.8f)], 10f),
        ("car-sedan", [(7.5f, -1.5f), (7.5f, 20.8f), (33.5f, 20.8f)], 2f),
        ("car-police", [(33.5f, 20.8f), (18.4f, 20.8f), (18.4f, 12.6f), (7.5f, 12.6f), (7.5f, -1.5f)], 20f),
        // Nico 2b (sechs Autos): Krankenwagen die Runde andersherum und weiter nach Westen, Muellwagen von Westen
        // auf der Hauptstrasse und die Nord-Sued-Strasse hinauf
        ("car-ambulance", [(7.5f, -1.5f), (7.5f, 12.6f), (18.4f, 12.6f), (18.4f, 20.8f), (-1.5f, 20.8f)], 6f),
        ("car-garbage-truck", [(-1.5f, 20.8f), (7.5f, 20.8f), (7.5f, -1.5f)], 14f),
    ];

    /// <summary>
    /// Verkehr fuer die Szene: jede Strecke, deren Punkte innerhalb der Karte auf einer Strasse liegen (andere Staedte
    /// ohne passende Strassen bekommen so keine falschen Autos); Kreuzungen aus den Strassen der Szene.
    /// </summary>
    public static Verkehr Fuer(StadtSzene szene) => Fuer(szene, null, Abstand);

    /// <summary>
    /// Phase 8 W11 (Festzeiten-Verkehr): nur die Strecken aus <paramref name="tempo"/> (Nummer -> Tempo), alle anderen
    /// bleiben leer; null = alle Strecken im normalen Tempo. <paramref name="abstand"/> = kleinster Abstand zum Vordermann,
    /// <paramref name="bogen"/> = Halbmesser der Kurven an den Knicken.
    /// </summary>
    public static Verkehr Fuer(StadtSzene szene, IReadOnlyDictionary<int, float>? tempo, float abstand, float bogen = 0.6f)
    {
        // Welle K3b (Weisung P12-61): Stadt mit Kurve oder Kreisverkehr oder ohne passende feste Strecke = automatische
        // Strecken aus dem Strassennetz; Fassung 0 (Stadt 1) faehrt wie bisher
        if (Autostrecken.Automatisch(szene))
        {
            var v0 = Autostrecken.VerkehrAus(Autostrecken.Anlegen(szene.Strassen, bogen, szene.Spieltag), tempo, abstand, szene.Strassen);
            v0.FuerTag = szene.Spieltag;
            v0.Zebras.AddRange(szene.Zebras);
            return v0;
        }
        var v = new Verkehr { AbstandVorne = abstand, FuerStrassen = szene.Strassen };
        v.Kreuzungen.AddRange(KreuzungenAus(szene.Strassen));
        v.Zebras.AddRange(szene.Zebras);
        for (var nr = 0; nr < Strecken.Length; nr++)
        {
            var (modell, punkte, start) = Strecken[nr];
            var mitte = punkte.Select(q => new Vector2(q.X, q.Z)).ToList();
            if (!AufStrassen(mitte, szene) || (tempo is not null && !tempo.ContainsKey(nr)))
            {
                continue;
            }
            v.Autos.Add(new Auto(modell, Kurve.Versetzt(Kurve.Glatt(mitte, bogen), Spur), start) { Nummer = nr, Tempo = tempo?[nr] ?? Tempo });
        }
        return v;
    }

    /// <summary>Liegt jeder Abschnitt der Strecke (innerhalb der Karte) auf einer Strasse der Szene?</summary>
    public static bool AufStrassen(IReadOnlyList<Vector2> mitte, StadtSzene szene)
    {
        for (var i = 1; i < mitte.Count; i++)
        {
            for (var t = 0f; t <= 1f; t += 0.05f)
            {
                var p = Vector2.Lerp(mitte[i - 1], mitte[i], t);
                if (p.X < 0 || p.Y < 0 || p.X > szene.Breite || p.Y > szene.Tiefe)
                {
                    continue;
                }
                if (!szene.Strassen.Any(s => Kurve.Abstand(p, s) <= 0.01f))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>Treffpunkte der Strassen: Enden auf einer anderen Strasse und echte Schnitte, doppelte zusammengefasst.</summary>
    public static List<Vector2> KreuzungenAus(IReadOnlyList<IReadOnlyList<Vector2>> strassen)
    {
        var punkte = new List<Vector2>();
        void Dazu(Vector2 p)
        {
            if (punkte.All(q => Vector2.Distance(p, q) > 0.5f))
            {
                punkte.Add(p);
            }
        }
        for (var a = 0; a < strassen.Count; a++)
        {
            for (var b = 0; b < strassen.Count; b++)
            {
                if (a == b)
                {
                    continue;
                }
                foreach (var ende in (Vector2[])[strassen[a][0], strassen[a][^1]])
                {
                    if (Kurve.Abstand(ende, strassen[b]) <= 0.05f)
                    {
                        Dazu(ende);
                    }
                }
                if (a < b)
                {
                    for (var i = 1; i < strassen[a].Count; i++)
                    {
                        for (var j = 1; j < strassen[b].Count; j++)
                        {
                            if (Schnitt(strassen[a][i - 1], strassen[a][i], strassen[b][j - 1], strassen[b][j]) is { } s)
                            {
                                Dazu(s);
                            }
                        }
                    }
                }
            }
        }
        return punkte;
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
        return t is >= 0 and <= 1 && u is >= 0 and <= 1 ? a1 + (r * t) : null;
    }

    /// <summary>Index des Kreuzungsfelds, in dem <paramref name="p"/> liegt, sonst -1.</summary>
    private int Feld(Vector2 p)
    {
        for (var k = 0; k < Kreuzungen.Count; k++)
        {
            if (Vector2.Distance(p, Kreuzungen[k]) <= FeldRadius)
            {
                return k;
            }
        }
        return -1;
    }

    /// <summary>
    /// Alle Autos um <paramref name="sekunden"/> weiter; die Figur steht bei (fx, fz). Reihenfolge fest (die der
    /// Strecken): wer zuerst in ein freies Feld faehrt, belegt es fuer die folgenden.
    /// </summary>
    public void Weiter(double sekunden, float fx, float fz)
    {
        var figurOrt = new Vector2(fx, fz);
        // Welle A 0.54.0 (Sammelliste 254): nur eine Figur auf der Fahrbahn haelt die Autos an (auf dem Gehweg fahren sie
        // weiter); steht sie auf einem Zebrastreifen, haelt jedes Auto davor
        var alt = AnhaltenAltFuerTests;
        var aufFahrbahn = alt || AufFahrbahn(figurOrt);
        var zebra = alt ? null : Zebras.FirstOrDefault(z => z.Drauf(figurOrt, 0.05f, 0.1f));
        // belegt: Felder, in denen ein Auto steht (Heck, Mitte oder Front)
        var belegt = new Dictionary<int, Auto>();
        foreach (var a in Autos)
        {
            foreach (var p in (Vector2[])[a.Lage(a.Weg - Front).Ort, a.Lage().Ort, a.Lage(a.Weg + Front).Ort])
            {
                if (Feld(p) is var f and >= 0)
                {
                    belegt.TryAdd(f, a);
                }
            }
        }
        foreach (var a in Autos)
        {
            var schritt = (float)(sekunden * a.Tempo);
            var (ort, richtung) = a.Lage(a.Weg + Front);
            // Figur auf der Fahrbahn vor dem Auto (bis FigurVoraus, quer bis 0,45 = ganze Strasse)?
            var zurFigur = figurOrt - ort;
            var voraus = Vector2.Dot(zurFigur, richtung);
            var quer = MathF.Abs((zurFigur.X * richtung.Y) - (zurFigur.Y * richtung.X));
            var figur = aufFahrbahn && voraus > -0.1f && voraus < FigurVoraus && quer < 0.45f;
            if (zebra is not null && !figur)
            {
                var zuZebra = zebra.Mitte - ort;
                var vz = Vector2.Dot(zuZebra, richtung);
                var qz = MathF.Abs((zuZebra.X * richtung.Y) - (zuZebra.Y * richtung.X));
                figur = vz + (Zebrastreifen.Breite / 2) > -0.1f && vz - (Zebrastreifen.Breite / 2) < FigurVoraus && qz < 0.45f
                    && MathF.Abs(Vector2.Dot(zebra.Richtung, richtung)) > 0.7f;
            }
            // anderes Auto zu nah voraus (gleiche Richtung)?
            var vorne = Autos.Any(b => b != a && b.Lage() is var (bo, br) && Vector2.Dot(bo - ort, richtung) is var d && d > 0 && d < AbstandVorne
                && Vector2.Distance(bo, ort) < AbstandVorne + 0.05f && Vector2.Dot(br, richtung) > 0.5f);
            // Kreuzung: das Feld kurz voraus ist von einem anderen Auto belegt (und das Auto steht noch nicht darin)
            var feldJetzt = Feld(ort);
            var feldVoraus = Feld(a.Lage(a.Weg + Front + FeldVorlauf).Ort);
            var kreuzung = feldVoraus >= 0 && feldVoraus != feldJetzt && belegt.TryGetValue(feldVoraus, out var anderes) && anderes != a;
            // Rundlauf: am Ende der Strecke wieder an den Anfang nur, wenn dort kein anderes Auto steht. Patch A 0.29.2
            // (Sammelliste 182): ein Fahrzeug, das selbst am Ende seiner Strecke steht und dort hinausfaehrt (Gegenrichtung
            // zum Anfang), zaehlt nicht; sonst warten zwei am Kartenrand fuer immer aufeinander (Ende der einen Strecke
            // = Anfang der anderen).
            var anfang = !Rundweg && AmEnde(a, sekunden) && Autos.Any(b => b != a && Vector2.Distance(b.Lage().Ort, a.Lage(0).Ort) < AbstandVorne + (2 * Front)
                && !(AmEnde(b, sekunden) && Vector2.Dot(b.Lage().Richtung, a.Lage(0).Richtung) < 0.5f));
            // Welle K3b: vor einem Feld am Ring wartet, wer noch nicht im Ring ist, solange ein Auto im Ring darauf zufaehrt
            var vorfahrt = Ringe.Count > 0 && !kreuzung && feldVoraus >= 0 && feldVoraus != feldJetzt && AmRing(Kreuzungen[feldVoraus]) && !ImRing(ort)
                && Autos.Any(b => b != a && b.Lage() is var (bo, br) && ImRing(bo) && Vector2.Distance(bo, Kreuzungen[feldVoraus]) < FeldRadius + VorfahrtWeite
                    && Vector2.Dot(br, Kreuzungen[feldVoraus] - bo) > 0);
            a.Haelt = figur || vorne || kreuzung || anfang || vorfahrt;
            if (a.Haelt)
            {
                continue;
            }
            a.Weg = (a.Weg + schritt) % a.Laenge;
            if (Feld(a.Lage(a.Weg + Front).Ort) is var neu and >= 0)
            {
                belegt.TryAdd(neu, a);
            }
            // Patch A 0.29.2: wer auf ein Feld zufaehrt, belegt es schon fuer die folgenden (sonst fahren zwei, die im
            // selben Schritt frei sehen, gleichzeitig hinein)
            if (Feld(a.Lage(a.Weg + Front + FeldVorlauf).Ort) is var feldNeu and >= 0)
            {
                belegt.TryAdd(feldNeu, a);
            }
        }
    }

    /// <summary>Welle K3b: liegt der Punkt auf der Fahrbahn eines Rings?</summary>
    private bool ImRing(Vector2 p) => Ringe.Any(r => MathF.Abs(Vector2.Distance(p, r.Mitte) - r.Radius) < StadtSzene.StrassenBreite / 2);

    /// <summary>Welle K3b: liegt das Feld auf einem Ring?</summary>
    private bool AmRing(Vector2 feld) => Ringe.Any(r => MathF.Abs(Vector2.Distance(feld, r.Mitte) - r.Radius) < 0.1f);

    /// <summary>Faehrt das Auto im naechsten Schritt (<paramref name="sekunden"/>) ueber das Ende seiner Strecke?</summary>
    private static bool AmEnde(Auto a, double sekunden) => a.Weg + (float)(sekunden * a.Tempo) + Front >= a.Laenge;

    /// <summary>Start-Test und Tests: fester Stand nach <paramref name="sekunden"/> Fahrzeit in Schritten von 1/60 s.</summary>
    public void Probe(double sekunden, float fx, float fz)
    {
        for (var t = 0; t < (int)Math.Round(sekunden * 60); t++)
        {
            Weiter(1.0 / 60, fx, fz);
        }
    }
}
