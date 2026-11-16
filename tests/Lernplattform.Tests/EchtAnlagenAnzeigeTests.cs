using Lernplattform.Core.Pruefung.Echt;

namespace Lernplattform.Tests;

/// <summary>
/// Patch A 0.84.1 (Sammelliste 363, Weisung P12-286): der Umschalter "Anlagen (n)" nennt fuer jede Aufgabe genau so viele
/// Anlagen, wie neben ihr gezeigt werden; bis A 0.84.0 nannte er die Zahl aller Anlagen des Satzes.
/// </summary>
public class EchtAnlagenAnzeigeTests
{
    private static readonly string Datei = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen", EchtSaetze.Datei);

    private static EchtBestand Bestand => EchtSaetze.Laden(File.ReadAllBytes(Datei));

    [Fact]
    public void JedeAufgabe_ZahlImUmschalter_GleichDerGezeigtenAnlagen()
    {
        var bestand = Bestand;
        Assert.Empty(bestand.Fehler);
        Assert.NotEmpty(bestand.Saetze);
        foreach (var satz in bestand.Saetze)
        {
            foreach (var aufgabe in satz.Aufgaben)
            {
                var gezeigt = EchtAnlagenAnzeige.Von(satz, aufgabe);
                Assert.NotEmpty(gezeigt);
                Assert.Equal(gezeigt.Count, EchtAnlagenAnzeige.UmschalterZahl(satz, aufgabe));
                // jede gezeigte Anlage gehoert zum Satz, keine doppelt
                Assert.Equal(gezeigt.Count, gezeigt.Select(a => a.Nr).Distinct().Count());
                Assert.All(gezeigt, a => Assert.Same(a, satz.Anlage(a.Nr)));
            }
        }
    }

    [Fact]
    public void Zahl_GehtMitDerAufgabe_FesteStichprobe()
    {
        var bestand = Bestand;
        int[] Zahlen(string kennung)
        {
            var s = bestand.Satz(kennung)!;
            return [.. s.Aufgaben.Select(a => EchtAnlagenAnzeige.UmschalterZahl(s, a))];
        }
        Assert.Equal([1, 2, 5, 2], Zahlen("alle_ap1_01"));
        Assert.Equal([2, 1, 4, 3], Zahlen("si_b2_02"));
        Assert.Equal([5, 2, 1, 2], Zahlen("ae_b2_05"));
    }

    /// <summary>Gegenprobe: mit der alten Rechnung (alle Anlagen des Satzes) weicht die Zahl ab, der Test oben waere rot.</summary>
    [Fact]
    public void Gegenprobe_AlteZahl_WeichtAb()
    {
        var bestand = Bestand;
        var satz = bestand.Satz("alle_ap1_01")!;
        EchtAnlagenAnzeige.ZahlAltFuerTests = true;
        try
        {
            Assert.Equal(5, EchtAnlagenAnzeige.UmschalterZahl(satz, satz.Aufgaben[0]));
            var abweichend = bestand.Saetze.SelectMany(s => s.Aufgaben.Select(a => (s, a)))
                .Count(x => EchtAnlagenAnzeige.UmschalterZahl(x.s, x.a) != EchtAnlagenAnzeige.Von(x.s, x.a).Count);
            Assert.True(abweichend > 0);
        }
        finally
        {
            EchtAnlagenAnzeige.ZahlAltFuerTests = false;
        }
    }
}
