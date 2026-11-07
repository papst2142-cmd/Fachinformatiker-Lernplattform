using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle K3 (A 0.45.0 / S 0.11.0, Weisung P12-58, Sammelliste 231): die Mittellinie laeuft ueber jeden Anschluss gerade
/// Strasse ↔ Kurve und Kurve ↔ Kurve im gleichen Takt weiter (kein fehlender, kein doppelter Strich). Gegenprobe: mit dem
/// Stand vor K3 (jede Strasse eigener Takt) fehlt am Anschluss ein Strich. Fassung 0 bleibt unveraendert.
/// </summary>
public class MittellinieTests
{
    private static Spieldaten D => Daten.Spiel;

    /// <summary>Schritt von Strichanfang zu Strichanfang (Strich 0,2 + Luecke 0,25).</summary>
    private const float Takt = 0.45f;

    private static PyObjekt Leer() => Aussen.Aus(Aussen.Leer(D.Fassung, "neu_mittellinie", 24, 16));

    private static void Strasse(PyObjekt a, IEnumerable<Vector2> punkte, PyObjekt? kurve = null)
    {
        var l = new PyObjekt
        {
            { "typ", "strasse" },
            { "punkte", new List<object?>(punkte.Select(q => (object?)new List<object?> { (double)q.X, (double)q.Y })) },
        };
        if (kurve is not null)
        {
            l.Add("gehweg", 0.35);
            l.Add(Strassenkurve.Feld, kurve);
        }
        Aussen.Landschaft(a).Add(l);
    }

    private static PyObjekt KurveFeld(string form, int winkel, double radius) =>
        new() { { "form", form }, { "winkel", (long)winkel }, { "radius", radius } };

    /// <summary>
    /// Stadt mit einem Linienzug aus Stuecken (gerade / Kurve) und die ganze Mittellinie als ein Linienzug. Die Stuecke
    /// werden in Fahrtrichtung aneinander gesetzt, <paramref name="umgedreht"/> speichert jedes zweite rueckwaerts (die
    /// Kette muss die Richtung selbst finden).
    /// </summary>
    private static (PyObjekt A, List<Vector2> Ganz) Zug(string aufbau, bool umgedreht = false)
    {
        var a = Leer();
        var ganz = new List<Vector2> { new(2, 3) };
        var richtung = 0f;
        var nr = 0;
        foreach (var teil in aufbau.Split(' '))
        {
            var start = ganz[^1];
            List<Vector2> p;
            PyObjekt? feld = null;
            if (teil.StartsWith('g'))
            {
                var laenge = float.Parse(teil[1..], System.Globalization.CultureInfo.InvariantCulture);
                p = [start, start + (new Vector2(MathF.Cos(richtung), MathF.Sin(richtung)) * laenge)];
            }
            else
            {
                var k = Strassenkurve.Lesen(teil)!.Value;
                var roh = Strassenkurve.Mittellinie(k.Form, k.Winkel, k.Radius, k.Gespiegelt);
                var dreh = Matrix3x2.CreateRotation(richtung);
                p = [.. roh.Select(q => start + Vector2.Transform(q, dreh))];
                feld = KurveFeld(k.Form, k.Winkel, k.Radius);
            }
            var ende = Vector2.Normalize(p[^1] - p[^2]);
            richtung = MathF.Atan2(ende.Y, ende.X);
            Strasse(a, umgedreht && nr % 2 == 1 ? Enumerable.Reverse(p) : p, feld);
            ganz.AddRange(p.Skip(1));
            nr++;
        }
        return (a, ganz);
    }

    private static List<List<Vector2>> Strassen(PyObjekt a) =>
        [.. Aussen.Landschaft(a).OfType<PyObjekt>().Where(l => Py.Gleich(l["typ"], "strasse"))
            .Select(l => Liste(l["punkte"]).Select(q => new Vector2((float)Py.F(Liste(q)[0]), (float)Py.F(Liste(q)[1]))).ToList())];

    /// <summary>Lage (Bogenlaenge) des Strichanfangs auf dem ganzen Linienzug.</summary>
    private static float Lage(Vector2 q, List<Vector2> ganz)
    {
        var (best, lage, s) = (float.MaxValue, 0f, 0f);
        for (var i = 0; i + 1 < ganz.Count; i++)
        {
            var d = ganz[i + 1] - ganz[i];
            var l = d.Length();
            var t = Math.Clamp(Vector2.Dot(q - ganz[i], d) / (l * l), 0f, 1f);
            var abstand = Vector2.Distance(q, ganz[i] + (d * t));
            if (abstand < best - 1e-5f)
            {
                (best, lage) = (abstand, s + (t * l));
            }
            s += l;
        }
        return lage;
    }

    /// <summary>Groesster und kleinster Abstand zweier aufeinander folgender Strichanfaenge entlang des Linienzugs.</summary>
    private static (float Groesster, float Kleinster, int Anzahl) Abstaende(List<(float X1, float Z1, float X2, float Z2)> striche, List<Vector2> ganz)
    {
        var lagen = striche.Select(s => Lage(new Vector2(s.X1, s.Z1), ganz)).Order().ToList();
        var schritte = lagen.Zip(lagen.Skip(1), (x, y) => y - x).ToList();
        return (schritte.Max(), schritte.Min(), lagen.Count);
    }

    public static TheoryData<string, bool> Anschluesse => new()
    {
        { "g4 bogen:90:1 g4", false },
        { "g4 bogen:90:2 g4", false },
        { "g4 bogen:90:3 g4", true },
        { "g3.3 bogen:45:2 g3", false },
        { "g4 s:45:2 g4", true },
        { "bogen:90:2 bogen:90:2:m", false },
        { "g3 bogen:45:2 bogen:45:2 g3", true },
        { "g2.7 s:45:2 bogen:90:3 g3", false },
    };

    [Theory]
    [MemberData(nameof(Anschluesse))]
    public void UeberJedenAnschluss_GleicherTakt_GegenprobeVorK3MitLuecke(string aufbau, bool umgedreht)
    {
        var (a, ganz) = Zug(aufbau, umgedreht);
        var strassen = Strassen(a);
        var zuege = StadtSzene.MittellinienZuege(strassen);
        Assert.Single(zuege);
        Assert.Equal(strassen.Count, zuege[0].Strassen.Count);
        var striche = StadtSzene.MittellinienStriche(strassen);
        var (groesster, kleinster, anzahl) = Abstaende(striche, ganz);
        // jeder Strich im Takt: kein fehlender (Abstand ~0,9) und kein doppelter (Abstand ~0)
        Assert.True(groesster <= Takt + 0.01f, $"{aufbau}: Luecke {groesster}");
        Assert.True(kleinster >= Takt - 0.01f, $"{aufbau}: doppelt {kleinster}");
        var laenge = ganz.Zip(ganz.Skip(1), Vector2.Distance).Sum();
        Assert.True(anzahl >= (int)(laenge / Takt), $"{aufbau}: nur {anzahl} Striche auf {laenge}");
        // Gegenprobe: Stand vor K3 (jede Strasse eigener Takt, Kreuzungsregel gegen die Nachbarstrasse) hat eine Luecke
        var alt = StadtSzene.MittellinienStriche(strassen, ohneKette: true);
        Assert.True(Abstaende(alt, ganz).Groesster > Takt + 0.1f, $"{aufbau}: Gegenprobe ohne Luecke");
    }

    [Fact]
    public void Spiel_ZeichnetGenauDieseStriche()
    {
        var (a, _) = Zug("g4 bogen:90:2 g4");
        var szene = Ansicht3D.Bauen(Aussen.Fassung(a, D.Fassung, new PyObjekt(), 1, null, "t", "", ""), D, Ansicht3D.Einstellung.Standard).Szene;
        var gezeichnet = szene.Teile.Where(t => t.Art == TeilArt.Band && Math.Abs(t.Y1 - StadtSzene.Hoehe.Mittellinie) < 1e-6)
            .Select(t => (t.X1, t.Z1, t.X2, t.Z2)).ToList();
        Assert.Equal(StadtSzene.MittellinienStriche(Strassen(a)), gezeichnet);
    }

    [Fact]
    public void Kreuzung_BleibtOhneStrich_AuchInDerKette()
    {
        // eine Querstrasse ueber das gerade Stueck nach der Kurve: dort kein Strich der Kette (kein "+")
        var (a, ganz) = Zug("g4 bogen:90:2 g6");
        var quer = ganz[^1] - new Vector2(0, 3);
        Strasse(a, [quer - new Vector2(2, 0), quer + new Vector2(2, 0)]);
        var striche = StadtSzene.MittellinienStriche(Strassen(a));
        Assert.DoesNotContain(striche.Where(s => MathF.Abs(s.X2 - s.X1) < 0.01f), s => Vector2.Distance(new Vector2((s.X1 + s.X2) / 2, (s.Z1 + s.Z2) / 2), quer) < StadtSzene.StrassenBreite / 2);
        // Gegenprobe: ohne Querstrasse liegt dort ein Strich
        var (b, ganz2) = Zug("g4 bogen:90:2 g6");
        Assert.Contains(StadtSzene.MittellinienStriche(Strassen(b)), s => Vector2.Distance(new Vector2((s.X1 + s.X2) / 2, (s.Z1 + s.Z2) / 2), ganz2[^1] - new Vector2(0, 3)) < StadtSzene.StrassenBreite / 2);
    }

    [Fact]
    public void Fassung0_GleicheStricheWieVorK3()
    {
        var strassen = Strassen(Aussen.Aus(D.Fassung));
        Assert.True(strassen.Count >= 5, $"nur {strassen.Count} Strassen");
        Assert.Equal(strassen.Count, StadtSzene.MittellinienZuege(strassen).Count);
        Assert.Equal(StadtSzene.MittellinienStriche(strassen, ohneKette: true), StadtSzene.MittellinienStriche(strassen));
    }
}
