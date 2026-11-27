using Lernplattform.Core.Pruefung.Echt;

namespace Lernplattform.Tests;

/// <summary>
/// Patch A 0.84.1 (Sammelliste 363, Weisung P12-286): der Umschalter "Anlagen (n)" nennt fuer jede Aufgabe genau so viele
/// Anlagen, wie neben ihr gezeigt werden; bis A 0.84.0 nannte er die Zahl aller Anlagen des Satzes.
/// </summary>
public class EchtAnlagenAnzeigeTests
{
    private static readonly string Datei = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen", EchtSaetze.Datei);

    private static EchtBestand Bestand => EchtSaetze.Laden(EchtTestDatei.Vorher);

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

    /// <summary>
    /// A 1.11.0 (Sammelliste 456): volle Breite genau fuer Aufgaben mit einer Zeichenaufgabe (Baukasten), feste Stichprobe
    /// (Klasse ae_b1_07 Aufgabe 2, Use-Case ae_b1_06 Aufgabe 4, ER si_b1_01 Aufgabe 3) und Gesamtzahl im Bestand.
    /// </summary>
    [Fact]
    public void VolleBreite_GenauAufgabenMitBaukasten()
    {
        var bestand = Bestand;
        bool[] Breit(string kennung) => [.. bestand.Satz(kennung)!.Aufgaben.Select(EchtAnlagenAnzeige.VolleBreite)];
        Assert.Equal([false, false, true, true], Breit("ae_b1_06"));
        Assert.Equal([false, true, true, false], Breit("ae_b1_07"));
        Assert.Equal([false, false, true, false], Breit("si_b1_01"));
        Assert.Equal([false, false, false, false], Breit("alle_ap1_01"));
        var alle = bestand.Saetze.SelectMany(s => s.Aufgaben).ToList();
        Assert.All(alle, a => Assert.Equal(a.Teile.Any(t => t.Antwort.Form == EchtSaetze.FormZeichnen), EchtAnlagenAnzeige.VolleBreite(a)));
        // A 1.12.0 (Weisung P12-390): dazu die Struktogramm-Aufgabe ae_b2_08 Aufgabe 2 (ae_b2_02 heraus, P12-393)
        Assert.Equal(32, alle.Count(EchtAnlagenAnzeige.VolleBreite));
    }

    /// <summary>Gegenprobe 456: mit der bisherigen Rechnung steht keine Aufgabe in voller Breite, der Test oben waere rot.</summary>
    [Fact]
    public void Gegenprobe_VolleBreiteAlt_KeineAufgabe()
    {
        var bestand = Bestand;
        EchtAnlagenAnzeige.VolleBreiteAltFuerTests = true;
        try
        {
            Assert.DoesNotContain(bestand.Saetze.SelectMany(s => s.Aufgaben), EchtAnlagenAnzeige.VolleBreite);
        }
        finally
        {
            EchtAnlagenAnzeige.VolleBreiteAltFuerTests = false;
        }
    }
}
