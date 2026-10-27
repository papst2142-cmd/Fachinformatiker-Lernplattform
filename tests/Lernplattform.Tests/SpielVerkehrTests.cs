using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W7 (Weisung P8-14, Sammelliste 126, Nico 2b und 3a): Autos nur zur Darstellung. Sechs Autos auf der
/// rechten Spur der Strassen, Halt vor der Figur, kein Auffahren, kein Auto faehrt in ein belegtes Kreuzungsfeld,
/// gleicher Stand nach gleichen Schritten, Nachlauf 10 Sekunden nach dem Bedienen.
/// </summary>
public sealed class SpielVerkehrTests
{
    internal static readonly Lazy<StadtSzene> Szene = new(() =>
        StadtSzene.Bauen(new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value), null, SpielGrafikTests.Bibliothek.Value));

    private static float Abstand(Vector2 p, IReadOnlyList<IReadOnlyList<Vector2>> strassen) => strassen.Min(s => Kurve.Abstand(p, s));

    [Fact]
    public void SechsAutosAufDerRechtenSpur()
    {
        var szene = Szene.Value;
        var v = Verkehr.Fuer(szene);
        Assert.Equal(["car-taxi", "car-van", "car-sedan", "car-police", "car-ambulance", "car-garbage-truck"], v.Autos.Select(a => a.Modell));
        Assert.All(v.Autos, a => Assert.NotNull(SpielGrafikTests.Bibliothek.Value.Modell(a.Modell)));
        var seiten = new HashSet<int>();
        foreach (var a in v.Autos)
        {
            for (var w = 0f; w < a.Laenge; w += 0.25f)
            {
                var (ort, richtung) = a.Lage(w);
                if (ort.X < 0.5f || ort.Y < 0.5f || ort.X > szene.Breite - 0.5f || ort.Y > szene.Tiefe - 0.5f
                    || v.Kreuzungen.Any(k => Vector2.Distance(k, ort) < 1.2f))
                {
                    continue;
                }
                // auf der Spur: Abstand zur Strassenmitte = Spur, immer auf derselben Seite der Fahrtrichtung
                var naechste = szene.Strassen.MinBy(s => Kurve.Abstand(ort, s))!;
                Assert.InRange(Kurve.Abstand(ort, naechste), Verkehr.Spur - 0.01f, Verkehr.Spur + 0.01f);
                var mitte = ort - (Vector2.Normalize(new Vector2(-richtung.Y, richtung.X)) * Verkehr.Spur);
                var seite = Kurve.Abstand(mitte, naechste) < 0.01f ? 1 : -1;
                seiten.Add(seite);
            }
        }
        Assert.Equal([1], seiten);
    }

    [Fact]
    public void StreckenNurAufStrassenUndKreuzungenGefunden()
    {
        var szene = Szene.Value;
        foreach (var (modell, punkte, _) in Verkehr.Strecken)
        {
            Assert.True(Verkehr.AufStrassen([.. punkte.Select(p => new Vector2(p.X, p.Z))], szene), modell);
        }
        Assert.False(Verkehr.AufStrassen([new Vector2(3, 3), new Vector2(6, 3)], szene));
        var kreuzungen = Verkehr.KreuzungenAus(szene.Strassen).Select(k => (MathF.Round(k.X, 1), MathF.Round(k.Y, 1))).Order().ToList();
        Assert.Equal([(7.5f, 12.6f), (7.5f, 20.8f), (18.4f, 12.6f), (18.4f, 20.8f), (29.8f, 20.8f)], kreuzungen);
        Assert.All(Verkehr.Fuer(szene).Kreuzungen, k => Assert.True(Abstand(k, szene.Strassen) < 0.01f));
    }

    [Fact]
    public void HaeltVorDerFigur()
    {
        var v = Verkehr.Fuer(Szene.Value);
        var taxi = v.Autos[0];
        taxi.Weg = 8f;
        // Figur 0,5 vor der Front auf der Fahrbahn: das Taxi haelt, die anderen fahren
        var (vorne, richtung) = taxi.Lage(taxi.Weg + 0.8f);
        var andere = v.Autos.Skip(1).Select(a => a.Weg).ToList();
        v.Weiter(0.1, vorne.X, vorne.Y);
        Assert.True(taxi.Haelt);
        Assert.Equal(8f, taxi.Weg);
        // auf der Gegenspur genauso (ganze Strassenbreite)
        var gegen = vorne + (new Vector2(-richtung.Y, richtung.X) * 0.34f);
        v.Weiter(0.1, gegen.X, gegen.Y);
        Assert.True(taxi.Haelt);
        // Figur weit weg: es faehrt Tempo mal Zeit
        v.Weiter(0.1, -50, -50);
        Assert.False(taxi.Haelt);
        Assert.Equal(8f + (Verkehr.Tempo * 0.1f), taxi.Weg, 4);
        Assert.Contains(v.Autos.Skip(1), a => !andere.Contains(a.Weg));
    }

    /// <summary>
    /// Zehn Minuten Fahrt: nie zwei Autos derselben Richtung naeher als ein Auto lang hintereinander, nie zwei
    /// Mitten im selben Kreuzungsfeld, kein Stillstand (jedes Auto faehrt Runden), mit einer Figur auf der Strasse.
    /// </summary>
    [Fact]
    public void KeinAuffahrenUndKreuzungenFrei()
    {
        var v = Verkehr.Fuer(Szene.Value);
        var gefahren = new float[v.Autos.Count];
        for (var bild = 0; bild < 60 * 600; bild++)
        {
            var alt = v.Autos.Select(a => a.Weg).ToList();
            // die Figur steht eine Weile auf der Hauptstrasse, dann wieder auf der Wiese
            var (fx, fz) = bild % 3600 < 600 ? (12f, 20.8f) : (-50f, -50f);
            v.Weiter(1.0 / 60, fx, fz);
            for (var i = 0; i < v.Autos.Count; i++)
            {
                var d = v.Autos[i].Weg - alt[i];
                gefahren[i] += d < 0 ? d + v.Autos[i].Laenge : d;
            }
            var lagen = v.Autos.Select(a => a.Lage()).ToList();
            for (var i = 0; i < lagen.Count; i++)
            {
                for (var j = i + 1; j < lagen.Count; j++)
                {
                    if (Vector2.Dot(lagen[i].Richtung, lagen[j].Richtung) > 0.9f)
                    {
                        Assert.True(Vector2.Distance(lagen[i].Ort, lagen[j].Ort) > 0.6f, "aufgefahren: " + v.Autos[i].Modell + " und " + v.Autos[j].Modell + " in Bild " + bild);
                    }
                }
            }
            foreach (var k in v.Kreuzungen)
            {
                Assert.True(lagen.Count(l => Vector2.Distance(l.Ort, k) <= Verkehr.FeldRadius) <= 1, "Kreuzung " + k + " doppelt belegt in Bild " + bild);
            }
        }
        for (var i = 0; i < v.Autos.Count; i++)
        {
            Assert.True(gefahren[i] > v.Autos[i].Laenge * 2, v.Autos[i].Modell + " steht: " + gefahren[i]);
        }
    }

    [Fact]
    public void GleicherStandNachGleichenSchritten()
    {
        var a = Verkehr.Fuer(Szene.Value);
        var b = Verkehr.Fuer(Szene.Value);
        a.Probe(37.5, 12f, 20.8f);
        b.Probe(37.5, 12f, 20.8f);
        Assert.Equal(a.Autos.Select(x => x.Weg), b.Autos.Select(x => x.Weg));
        Assert.NotEqual(Verkehr.Fuer(Szene.Value).Autos.Select(x => x.Weg), a.Autos.Select(x => x.Weg));
    }

    [Fact]
    public void FahrenNurBisZehnSekundenNachDemBedienen()
    {
        var v = Verkehr.Fuer(Szene.Value);
        Assert.True(v.Faehrt(0));
        Assert.True(v.Faehrt(9.99));
        Assert.False(v.Faehrt(10));
        v.Bedient(30);
        Assert.True(v.Faehrt(39.99));
        Assert.False(v.Faehrt(40));
        // ein frueheres Bedienen verkuerzt nie
        v.Bedient(5);
        Assert.True(v.Faehrt(39.99));
        Assert.Equal(10, Verkehr.Nachlauf);
    }
}
