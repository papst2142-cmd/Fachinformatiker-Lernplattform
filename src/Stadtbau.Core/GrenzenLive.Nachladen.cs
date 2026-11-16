using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.55.0 (Weisung P12-282 Punkt 3): Grenzen live fuer grosse Karten (groesser als
/// <see cref="Stadtteile.AltesFormatBis"/>). Das Spiel zeigt sie nur mit Nachladen (feste Kamera, nur die Stadtteile um die
/// Figur), darum rechnet das Werkzeug dieselben Bilder: die Figur in der Mitte jedes Stadtteils, Kamera wie im Spiel am
/// weitesten weg und am flachsten (<see cref="SpielKamera.AbstandMax"/>, <see cref="SpielKamera.NeigungMin"/>), vier
/// Blickrichtungen, geladen genau die noetigen Stadtteile (<see cref="Ladeplan.Noetig"/>), gezaehlt mit
/// <see cref="StadtbauGrenzen.Nachladen"/> (dieselben Regeln wie das Zeichnen, im Start-Test mit dem Bild verglichen).
/// Jeder Stadtteil bekommt seine Kosten (<see cref="Kacheln"/>); ist einer ueber der Grenze, nennt der rote Fund ihn mit
/// Namen. Gleichzeitig geladen zaehlt die Grundlage plus jeder Stadtteil im Kreis <see cref="Ladeplan.Vor"/> (geladen und
/// vorbereitet, wie das Spiel im Stehen), Grenze <see cref="StadtLast.NetzdatenGrenzeMb"/>.
/// </summary>
public sealed partial record GrenzenLive
{
    /// <summary>Kosten eines Stadtteils: teuerstes Bild (Mittel) mit der Figur in seiner Mitte, Netzdaten gleichzeitig geladen.</summary>
    public sealed record KachelKosten(Stadtteile.Kachel Kachel, long Dreiecke, int Aufrufe, float Drehung, long GeladenBytes)
    {
        public bool Ueber => Dreiecke > StadtbauGrenzen.Dreiecke || Aufrufe > StadtbauGrenzen.Aufrufe;
    }

    /// <summary>Kosten je Stadtteil (Reihe fuer Reihe); null = kleine Karte ohne Nachladen (Gesamt- und Nahansicht wie bisher).</summary>
    public IReadOnlyList<KachelKosten>? Kacheln { get; init; }

    /// <summary>Hoechstens gleichzeitig geladene Netzdaten in Byte (nur mit Nachladen).</summary>
    public long GeladenBytes { get; init; }

    /// <summary>Stadtteil mit dem hoechsten Wert an Netzdaten (nur mit Nachladen).</summary>
    public Stadtteile.Kachel? GeladenBei { get; init; }

    public bool GeladenUeber => Kacheln is not null && GeladenBytes / 1048576.0 > StadtLast.NetzdatenGrenzeMb;

    /// <summary>Der teuerste Stadtteil (nur mit Nachladen).</summary>
    public KachelKosten? Teuerste => Kacheln?.OrderByDescending(k => StadtbauGrenzen.Anteil(k.Dreiecke, k.Aufrufe)).FirstOrDefault();

    /// <summary>Werkzeug laedt nach (3D-Ansicht und Grenzen) bei Karten groesser als <see cref="Stadtteile.AltesFormatBis"/>.</summary>
    public static bool MitNachladenFuer(float breite, float tiefe) =>
        breite > Stadtteile.AltesFormatBis || tiefe > Stadtteile.AltesFormatBis;

    /// <summary>Alle Stadtteile packen: mit Kleingruen (Mittel, Hoch) und ohne (Niedrig), aus EINEM Bau je Stadtteil.</summary>
    public static (Nachlader.Paket[] Mit, Nachlader.Paket[] Ohne) Packen(StadtSzene szene, IReadOnlyList<Stadtteile.Kachel> kacheln)
    {
        _ = szene.GrundlageVorbereiten();
        _ = szene.Laternen;
        var mit = new Nachlader.Paket[kacheln.Count];
        var ohne = new Nachlader.Paket[kacheln.Count];
        Parallel.For(0, kacheln.Count, i =>
        {
            var n = szene.KachelVorbereiten(kacheln[i]);
            mit[i] = Nachlader.Packen(n, mitGruen: true);
            ohne[i] = Nachlader.Packen(n, mitGruen: false);
        });
        return (mit, ohne);
    }

    /// <summary>Kamera eines Bilds mit Nachladen wie im Spiel (Figur in der Mitte, am weitesten weg und am flachsten).</summary>
    public static Kamera KameraNachladen(float x, float z, float drehung) =>
        new(x, z, SpielKamera.AbstandMax) { Neigung = SpielKamera.NeigungMin, Drehung = drehung };

    /// <summary>Die vier Blickrichtungen je Stadtteil (wie der Start-Test des Spiels mit der Testkarte).</summary>
    public static readonly float[] NachladenDrehungen = [0f, 90f, 180f, 270f];

    private static GrenzenLive RechnenNachladen(StadtSpiel spiel, long uhr, long questgeber)
    {
        var szene = spiel.Szene;
        var kacheln = Stadtteile.Alle(szene.Breite, szene.Tiefe).ToList();
        var grundlage = szene.GrundlageVorbereiten();
        var (mit, ohne) = Packen(szene, kacheln);
        var zMit = new StadtbauGrenzen.Nachladen(szene, grundlage, mit);
        var zOhne = new StadtbauGrenzen.Nachladen(szene, grundlage, ohne);
        var bytes = mit.ToDictionary(p => p.Kachel, p => p.Bytes);
        var ohneSparen = OhneSparenFuerTests;
        var abstand = SpielKamera.AbstandMax;
        // Stufen: Mittel (geprueft), Niedrig und Hoch (Info); Kleingruen in der Kachel wie das Spiel beim Laden
        var stufen = new[] { Grafikstufen.Mittel, Grafikstufen.Niedrig, Grafikstufen.Hoch };
        var bewegt = stufen.Select(s => StadtbauSpiel.Bewegt(spiel, s.Fern(abstand), uhr: uhr, halb: s.AutosHalb)).ToArray();
        var kosten = new KachelKosten[kacheln.Count];
        var hoechst = new (long Dreiecke, int Aufrufe)[kacheln.Count, stufen.Length];
        Parallel.For(0, kacheln.Count, i =>
        {
            var k = kacheln[i];
            var m = k.Mitte;
            var geladen = Ladeplan.Noetig(m.X, m.Y, szene.Breite, szene.Tiefe).ToHashSet();
            (long Dreiecke, int Aufrufe, float Dreh) teuer = (-1, 0, 0);
            var aufrufeMax = 0;
            foreach (var dreh in NachladenDrehungen)
            {
                var kamera = KameraNachladen(m.X, m.Y, dreh);
                var sp = kamera.Sicht * kamera.Projektion(BildBreite, BildHoehe);
                for (var s = 0; s < stufen.Length; s++)
                {
                    var z = stufen[s].Gruen(abstand) ? zMit : zOhne;
                    var b = z.Bild(sp, abstand, geladen, stufen[s], ohneSparen);
                    var summe = (b.Dreiecke + bewegt[s].Dreiecke + questgeber, b.Aufrufe + bewegt[s].Aufrufe);
                    hoechst[i, s] = (Math.Max(hoechst[i, s].Dreiecke, summe.Item1), Math.Max(hoechst[i, s].Aufrufe, summe.Item2));
                    if (s == 0)
                    {
                        aufrufeMax = Math.Max(aufrufeMax, summe.Item2);
                        if (summe.Item1 > teuer.Dreiecke)
                        {
                            teuer = (summe.Item1, summe.Item2, dreh);
                        }
                    }
                }
            }
            var geladenBytes = grundlage.Bytes + Ladeplan.Vorbereiten(m.X, m.Y, szene.Breite, szene.Tiefe).Sum(x => bytes.GetValueOrDefault(x));
            kosten[i] = new KachelKosten(k, teuer.Dreiecke, aufrufeMax, teuer.Dreh, geladenBytes);
        });
        var teuerste = kosten.OrderByDescending(k => StadtbauGrenzen.Anteil(k.Dreiecke, k.Aufrufe)).ThenBy(k => k.Kachel.Reihe).ThenBy(k => k.Kachel.Spalte).First();
        var meist = kosten.OrderByDescending(k => k.GeladenBytes).First();
        double AnteilVon(int s, Grafikstufe stufe)
        {
            var g = Grafikstufen.Grenze(stufe);
            var d = Enumerable.Range(0, kacheln.Count).Max(i => hoechst[i, s].Dreiecke);
            var a = Enumerable.Range(0, kacheln.Count).Max(i => hoechst[i, s].Aufrufe);
            return Math.Max((double)d / g.Dreiecke, (double)a / g.Aufrufe);
        }
        var (breite, tiefe) = ((int)MathF.Ceiling(szene.Breite), (int)MathF.Ceiling(szene.Tiefe));
        var mitte = teuerste.Kachel.Mitte;
        return new GrenzenLive((0, 0), (teuerste.Dreiecke, teuerste.Aufrufe), mitte.X, mitte.Y, abstand, teuerste.Drehung, breite, tiefe,
            WaermeNachladen(grundlage, mit, breite, tiefe), kacheln.Count * NachladenDrehungen.Length)
        {
            AnteilNiedrig = AnteilVon(1, Grafikstufe.Niedrig),
            AnteilHoch = AnteilVon(2, Grafikstufe.Hoch),
            QuestgeberDreiecke = questgeber,
            Kacheln = kosten,
            GeladenBytes = meist.GeladenBytes,
            GeladenBei = meist.Kachel,
        };
    }

    /// <summary>Roter Fund mit Nachladen: der teuerste Stadtteil mit Namen (und die weiteren), sonst zu viel gleichzeitig geladen.</summary>
    private StadtbauFund? FundNachladen()
    {
        var ueber = Kacheln!.Where(k => k.Ueber).OrderByDescending(k => StadtbauGrenzen.Anteil(k.Dreiecke, k.Aufrufe)).ToList();
        if (ueber.Count > 0)
        {
            var t = ueber[0];
            var m = t.Kachel.Mitte;
            return new StadtbauFund(Texte.StadtteilZuTeuer(t.Kachel.Name, t.Dreiecke, t.Aufrufe, [.. ueber.Skip(1).Select(k => k.Kachel.Name)]),
                Texte.StadtZuTeuerTipp, true, m.X, m.Y);
        }
        if (GeladenUeber && GeladenBei is { } bei)
        {
            var m = bei.Mitte;
            return new StadtbauFund(Texte.StadtGeladenZuViel(GeladenBytes / 1048576.0, bei.Name), Texte.StadtZuTeuerTipp, true, m.X, m.Y);
        }
        return null;
    }

    /// <summary>Waermekarte mit Nachladen: dieselben Dreiecke wie der ganze Bau, aus den Stadtteilen (mit Kleingruen) und den Saetzen.</summary>
    private static long[] WaermeNachladen(StadtSzene.Grundlage g, IEnumerable<Nachlader.Paket> pakete, int breite, int tiefe)
    {
        var felder = new long[breite * tiefe];
        void Zaehlen(Netz netz, Matrix4x4? lage)
        {
            var e = netz.EckenFeld;
            var idx = netz.IndizesFeld;
            for (var i = 0; i + 2 < idx.Length; i += 3)
            {
                var mm = Vector3.Zero;
                for (var k = 0; k < 3; k++)
                {
                    var o = (int)idx[i + k] * Netz.Zahlen;
                    mm += new Vector3(e[o], e[o + 1], e[o + 2]);
                }
                mm /= 3;
                if (lage is { } l)
                {
                    mm = Vector3.Transform(mm, l);
                }
                int x = (int)MathF.Floor(mm.X), y = (int)MathF.Floor(mm.Z);
                if (x >= 0 && y >= 0 && x < breite && y < tiefe)
                {
                    felder[(y * breite) + x]++;
                }
            }
        }
        Zaehlen(g.Boden, null);
        foreach (var p in pakete)
        {
            Zaehlen(p.Fest, null);
            foreach (var (_, n) in p.Haeuser)
            {
                Zaehlen(n, null);
            }
        }
        foreach (var s in g.Instanzen?.Saetze ?? [])
        {
            foreach (var l in s.Lagen)
            {
                Zaehlen(s.Nah, l);
            }
        }
        return felder;
    }
}
