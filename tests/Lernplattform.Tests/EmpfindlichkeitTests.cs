using System.Text.Json.Nodes;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.6.0 (Sammelliste 445/446, Weisung P12-360 Punkte 6 und 7). Abnahmesaetze: „Mit dem Regler auf 50 % dreht die Maus
/// (bzw. der rechte Stick) bei gleicher Bewegung halb so weit wie mit 100 %, mit 200 % doppelt so weit; 100 % ist genau das
/// Verhalten ohne Regler.“ und „Mit dem Regler des linken Sticks auf 50 % geht die Figur bei gleichem Ausschlag halb so schnell,
/// mit 200 % doppelt so schnell (nie schneller als volles Tempo); 100 % wie ohne Regler.“ Gespeichert wird nur ein abweichender
/// Wert; eine alte Datei ohne Eintrag gilt als 100 %.
/// </summary>
public sealed class EmpfindlichkeitTests
{
    private static Kamera NeueKamera(bool ich)
    {
        var k = SpielKamera.Draussen(5, 5, 20, 20);
        k.Drehung = 30f;
        if (ich)
        {
            k.IchZiel = 1f;
            k.IchAnteil = 1f;
        }
        return k;
    }

    /// <summary>Drehung und Neigung nach einem Mausschritt (rechte Taste bzw. in der Ich-Sicht linke Taste).</summary>
    private static (float Drehung, float Neigung) Nach(int? prozent, bool ich, Kameragriff.Schritt s)
    {
        using var _ = prozent is { } p ? Empfindlichkeit.Maus.FuerTests(p) : null;
        var k = NeueKamera(ich);
        // in der Ich-Sicht neigt der Blick selbst (IchNeigung), sonst die Kamera
        float Neigung() => ich ? k.IchNeigung : k.Neigung;
        var (d0, n0) = (k.Drehung, Neigung());
        Kamerabedienung.Maus(k, true, s, 800);
        return ((((k.Drehung - d0) + 540f) % 360f) - 180f, Neigung() - n0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Maus_50Halb_200Doppelt_100WieOhneRegler(bool ich)
    {
        var schritte = new[]
        {
            new Kameragriff.Schritt(Kameragriff.Art.Drehen, Grad: 12f),
            new Kameragriff.Schritt(Kameragriff.Art.Drehen, Dy: 6),
        };
        if (ich)
        {
            schritte = [.. schritte, new Kameragriff.Schritt(Kameragriff.Art.Ziehen, Dx: 30)];
        }
        foreach (var s in schritte)
        {
            var ohne = Nach(null, ich, s);
            var voll = Nach(100, ich, s);
            var halb = Nach(50, ich, s);
            var doppelt = Nach(200, ich, s);
            // Gegenprobe: 100 % ist genau das Verhalten ohne Regler, und der Schritt bewegt die Kamera ueberhaupt
            Assert.Equal(ohne, voll);
            Assert.True(MathF.Abs(voll.Drehung) + MathF.Abs(voll.Neigung) > 0.5f, s.ToString());
            Assert.Equal(voll.Drehung / 2, halb.Drehung, 3);
            Assert.Equal(voll.Neigung / 2, halb.Neigung, 3);
            Assert.Equal(voll.Drehung * 2, doppelt.Drehung, 3);
            Assert.Equal(voll.Neigung * 2, doppelt.Neigung, 3);
        }
    }

    [Fact]
    public void StickRechts_50Halb_200Doppelt_100Gleich()
    {
        var weg = (Dx: 14.0, Dy: -6.0);
        Assert.Equal(weg, Empfindlichkeit.Umsehen(weg));
        using (Empfindlichkeit.StickRechts.FuerTests(50))
        {
            Assert.Equal((7.0, -3.0), Empfindlichkeit.Umsehen(weg));
            // die Maus bleibt davon unberuehrt (eigener Regler)
            Assert.Equal(1.0, Empfindlichkeit.Maus.Faktor);
        }
        using (Empfindlichkeit.StickRechts.FuerTests(200))
        {
            Assert.Equal((28.0, -12.0), Empfindlichkeit.Umsehen(weg));
        }
        Assert.Equal(weg, Empfindlichkeit.Umsehen(weg));
    }

    [Fact]
    public void StickLinks_50Halb_200Doppelt_NieSchnellerAlsVoll_100Gleich()
    {
        var eingabe = (Vor: 0.3, Seit: 0.4);
        Assert.Equal(eingabe, Empfindlichkeit.Gehen(eingabe));
        using (Empfindlichkeit.StickLinks.FuerTests(50))
        {
            var (v, s) = Empfindlichkeit.Gehen(eingabe);
            Assert.Equal(0.15, v, 9);
            Assert.Equal(0.2, s, 9);
        }
        using (Empfindlichkeit.StickLinks.FuerTests(200))
        {
            var (v, s) = Empfindlichkeit.Gehen(eingabe);
            Assert.Equal(0.6, v, 9);
            Assert.Equal(0.8, s, 9);
            // voller Ausschlag bleibt volles Tempo (Laenge 1), die Richtung bleibt
            var (v2, s2) = Empfindlichkeit.Gehen((0.6, 0.8));
            Assert.Equal(1.0, Math.Sqrt((v2 * v2) + (s2 * s2)), 9);
            Assert.Equal(0.8 / 0.6, s2 / v2, 9);
        }
        Assert.Equal(eingabe, Empfindlichkeit.Gehen(eingabe));
    }

    [Fact]
    public void Speichern_NurAbweichend_AlteDateiOhneEintrag100_KaputterWert100()
    {
        foreach (var e in Empfindlichkeit.Alle)
        {
            var werte = new JsonObject();
            Assert.Equal(Empfindlichkeit.Vorgabe, e.Lesen(werte));
            e.Wert(werte, 52);
            Assert.Equal(50, werte[e.Schluessel]!.GetValue<int>());
            Assert.Equal(50, e.Lesen(werte));
            e.Wert(werte, 100);
            Assert.False(werte.ContainsKey(e.Schluessel));
            e.Wert(werte, 999);
            Assert.Equal(Empfindlichkeit.Max, e.Lesen(werte));
            werte[e.Schluessel] = 300;
            Assert.Equal(Empfindlichkeit.Vorgabe, e.Lesen(werte));
            werte[e.Schluessel] = "schnell";
            Assert.Equal(Empfindlichkeit.Vorgabe, e.Lesen(werte));
            werte[e.Schluessel] = 1.5;
            Assert.Equal(Empfindlichkeit.Vorgabe, e.Lesen(werte));
        }
        Assert.Equal(3, Empfindlichkeit.Alle.Select(e => e.Schluessel).Distinct().Count());
    }
}
