using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.6.0 (Sammelliste 442, Weisung P12-359, Fund Nico 23.11.2026: „guckt man wenn man in das Gebaeude rein geht, immer
/// zur rechten Wand anstatt in den Flur direkt“). Abnahmesatz: nach dem Betreten schaut man in den Flur, weg von der
/// Eingangstuer, in jedem betretbaren Gebaeude und egal, wohin man draussen zuletzt geschaut hat. Gepruefte Rechnung: dieselbe
/// wie die Spielflaeche (<see cref="IchSicht.Takt"/> mit der Innenkamera <see cref="SpielKamera.Drinnen"/>).
/// </summary>
public sealed class SpielBetretenBlickTests
{
    private static readonly float[] DrehungenDraussen = [0f, 90f, 180f, 270f, 37f];

    /// <summary>Waagrechte Blickrichtung (x, z) der Ich-Sicht.</summary>
    private static Vector2 Blick(Kamera k) => Vector2.Normalize(new Vector2(k.IchBlick.X, k.IchBlick.Z));

    /// <summary>
    /// Je Ort und Drehung draussen: in der Ich-Sicht betreten. Fund, wenn der Blick nicht von der Eingangswand (x = 0) weg zeigt
    /// oder 2 Einheiten vor der Figur keine begehbare Stelle liegt (dann schaute man auf eine Wand).
    /// </summary>
    private static (int Orte, List<string> Funde) Pruefen()
    {
        var s = SpielMitfahrtTests.StadtNachFassung(2);
        var funde = new List<string>();
        var orte = 0;
        foreach (var ort in s.Szene.Orte.Where(o => s.HatInnenraum(o.Id)))
        {
            // jeder Ort mit Innenraum muss betretbar sein (sonst fiele er still aus der Pruefung)
            Assert.Contains(s.GeheZu(ort.Id), new[] { StadtGrund.Ok, StadtGrund.SchonDa });
            s.SofortAnkommen();
            Assert.True(s.KannBetreten, "nicht betretbar: " + ort.Id);
            orte++;
            foreach (var drehung in DrehungenDraussen)
            {
                var regeln = new IchSicht();
                var draussen = SpielKamera.Draussen(s.FigurX, s.FigurZ, s.Szene.Breite, s.Szene.Tiefe);
                Assert.True(SpielKamera.IchUmschalten(draussen));
                for (var i = 0; i < 120; i++)
                {
                    regeln.Takt(draussen, s, 1f / 60);
                }
                draussen.Drehung = drehung;
                Assert.True(s.Betreten(), "Betreten " + ort.Id);
                var innen = s.Innen!;
                var k = SpielKamera.Drinnen(innen.Breite, innen.Tiefe, true);
                regeln.Takt(k, s, 1f / 60);
                var b = Blick(k);
                var vorn = new Vector2(s.FigurX, s.FigurZ) + (b * 2f);
                if (b.X < 0.99f || !Wege.KannStehen(vorn.X, vorn.Y, innen.Lauf))
                {
                    funde.Add($"{ort.Id} (draussen {drehung:F0} Grad): Blick ({b.X:F2}, {b.Y:F2}), vorn frei {Wege.KannStehen(vorn.X, vorn.Y, innen.Lauf)}");
                }
                Assert.True(k.Ich, "Ich-Sicht bleibt beim Betreten an: " + ort.Id);
                s.Verlassen();
            }
        }
        return (orte, funde);
    }

    [Fact]
    public void Betreten_InDerIchSicht_BlickInDenFlur_JederOrt_GegenprobeA150()
    {
        var (orte, funde) = Pruefen();
        Assert.True(orte >= 8, "zu wenige betretbare Orte: " + orte);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        IchSicht.BetretenAltFuerTests = true;
        try
        {
            var (_, alt) = Pruefen();
            Assert.True(alt.Count > 0, "Gegenprobe greift nicht: auch die Drehung von draussen schaute in den Flur");
        }
        finally
        {
            IchSicht.BetretenAltFuerTests = false;
        }
    }
}
