using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Patch A 0.29.2 (Weisung P10-4, Sammelliste 182): am Kartenrand ist das Ende der einen Strecke der Anfang einer
/// anderen. Warteten zwei Fahrzeuge dort am Ende ihrer Strecke gegenseitig auf den freien Anfang, standen sie fuer
/// immer, und nach einigen Minuten stand der ganze Verkehr. Je Satz eine Stunde Fahrt ohne Figur: kein Fahrzeug steht
/// laenger als <see cref="HoechsteStandzeit"/> Sekunden, Abstaende zum Vordermann, Kreuzungsfelder und Zahl der
/// Fahrzeuge gelten in jedem Schritt; der Uebergang aus A 0.29.1 ist in der Gesamtansicht zu jedem Zeitpunkt ohne
/// Hoechstzeit fertig. Nur Darstellung: die Fingerabdruecke pruefen die bestehenden Tests.
/// </summary>
public sealed class VerkehrStillstandTests
{
    /// <summary>Laenger darf kein Fahrzeug ohne Figur am Stueck stehen (Weisung P10-4).</summary>
    private const double HoechsteStandzeit = 30;

    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static StadtSzene Szene(string? fest, Jahreszeit j) =>
        StadtSzene.Bauen(new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value), null, Grafik, fest, FestDekoTests.Daten.Value, j);

    [Theory]
    [InlineData(FestVerkehr.Satz.Autos)]
    [InlineData(FestVerkehr.Satz.Halloween)]
    [InlineData(FestVerkehr.Satz.Weihnachten)]
    [InlineData(FestVerkehr.Satz.Ostern)]
    public void EineStundeOhneStillstand(FestVerkehr.Satz satz)
    {
        var v = FestVerkehr.VerkehrFuer(Szene(null, Jahreszeit.Sommer), satz);
        var zahl = satz == FestVerkehr.Satz.Autos ? 6 : FestVerkehr.Plaetze(satz).Select(p => p.Strecke).Distinct().Count();
        Assert.Equal(zahl, v.Autos.Count);
        var steht = new double[v.Autos.Count];
        var laengste = new double[v.Autos.Count];
        var gefahren = new float[v.Autos.Count];
        for (var bild = 0; bild < 3600 * 60; bild++)
        {
            var alt = v.Autos.Select(a => a.Weg).ToList();
            v.Weiter(1.0 / 60, -100, -100);
            Assert.Equal(zahl, v.Autos.Count);
            for (var i = 0; i < v.Autos.Count; i++)
            {
                var d = v.Autos[i].Weg - alt[i];
                gefahren[i] += d < 0 ? d + v.Autos[i].Laenge : d;
                steht[i] = d == 0 ? steht[i] + (1.0 / 60) : 0;
                laengste[i] = Math.Max(laengste[i], steht[i]);
                Assert.True(steht[i] <= HoechsteStandzeit, string.Create(CultureInfo.InvariantCulture,
                    $"{satz}: {v.Autos[i].Modell} (Strecke {v.Autos[i].Nummer}) steht seit {steht[i]:F1} s bei Weg {v.Autos[i].Weg:F2} von {v.Autos[i].Laenge:F2}, Bild {bild}"));
            }
            var lagen = v.Autos.Select(a => a.Lage()).ToList();
            for (var i = 0; i < lagen.Count; i++)
            {
                for (var k = i + 1; k < lagen.Count; k++)
                {
                    // gleiche Richtung: nie naeher als ein Auto lang (wie SpielVerkehrTests.KeinAuffahrenUndKreuzungenFrei)
                    if (Vector2.Dot(lagen[i].Richtung, lagen[k].Richtung) > 0.9f)
                    {
                        Assert.True(Vector2.Distance(lagen[i].Ort, lagen[k].Ort) > 0.6f,
                            $"{satz}: aufgefahren {v.Autos[i].Modell} und {v.Autos[k].Modell} in Bild {bild}");
                    }
                }
            }
            foreach (var kr in v.Kreuzungen)
            {
                Assert.True(lagen.Count(l => Vector2.Distance(l.Ort, kr) <= Verkehr.FeldRadius) <= 1, $"{satz}: Kreuzung {kr} doppelt belegt in Bild {bild}");
            }
        }
        // jedes Fahrzeug faehrt in der Stunde viele Runden
        for (var i = 0; i < v.Autos.Count; i++)
        {
            Assert.True(gefahren[i] > v.Autos[i].Laenge * 10, string.Create(CultureInfo.InvariantCulture, $"{satz}: {v.Autos[i].Modell} nur {gefahren[i]:F1}"));
        }
        TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture,
            $"{satz}: laengster Halt {laengste.Max():F1} s, Runden {string.Join(" ", gefahren.Select((g, i) => (g / v.Autos[i].Laenge).ToString("F0", CultureInfo.InvariantCulture)))}"));
    }

    /// <summary>Je Fest und Richtung, Beginn nach verschiedenen Fahrzeiten (auch nach mehreren Minuten, wo frueher alles stand).</summary>
    public static TheoryData<string, Jahreszeit, bool, double> Uebergaenge()
    {
        var daten = new TheoryData<string, Jahreszeit, bool, double>();
        foreach (var (fest, j) in (ValueTuple<string, Jahreszeit>[])[(Festzeit.Halloween, Jahreszeit.Herbst), (Festzeit.Weihnachten, Jahreszeit.Winter), (Festzeit.Ostern, Jahreszeit.Fruehling)])
        {
            foreach (var hin in (bool[])[true, false])
            {
                foreach (var vorlauf in (double[])[60, 240, 420, 900])
                {
                    daten.Add(fest, j, hin, vorlauf);
                }
            }
        }
        return daten;
    }

    [Theory]
    [MemberData(nameof(Uebergaenge))]
    public void UebergangInDerGesamtansichtOhneHoechstzeit(string fest, Jahreszeit j, bool hin, double vorlauf)
    {
        var szene = Szene(fest, j);
        var ab = Tageslicht.DaemmerungAb(j);
        var (vorher, nachher) = (FestVerkehr.Fuer(fest, ab - 1, j), FestVerkehr.Fuer(fest, ab, j));
        var (alt, neu) = hin ? (vorher, nachher) : (nachher, vorher);
        var altVerkehr = FestVerkehr.VerkehrFuer(szene, alt);
        altVerkehr.Probe(vorlauf, -100, -100);
        // der neue Satz faehrt ab seinen Startplaetzen (wie in StadtFlaeche)
        var u = new FestUebergang(alt, altVerkehr, neu, FestVerkehr.VerkehrFuer(szene, neu));
        while (!u.Fertig)
        {
            u.Schritt(1.0 / 60, -100, -100, (_, _) => true);
            Assert.True(u.Verkehr.Autos.Count <= 16, "hoechstens 16 Fahrzeuge");
        }
        Assert.True(u.Tausche.All(t => t.Grund == (t.Neu ? FestUebergang.Grund.Einfahrt : FestUebergang.Grund.Rundlauf)), string.Join("; ", u.Tausche));
        Assert.True(u.Fahrzeit < FestUebergang.Hoechstzeit);
        Assert.Equal(u.NeuVerkehr.Autos, u.Verkehr.Autos);
        TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture,
            $"{fest} {alt}->{neu} nach {vorlauf} s: fertig nach {u.Fahrzeit:F1} s, {u.Tausche.Count} Tausche"));
    }
}
