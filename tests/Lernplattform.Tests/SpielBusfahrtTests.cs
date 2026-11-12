using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.67.0 (Weisung P12-204, Sammelliste 302, Nico 4a 5a): mit dem Bus fahren. Probe-Stadt = Fassung 0 mit Gehweg an
/// jeder Strasse und drei Haltestellen (Fassung 0 selbst hat keine: dort gibt es keinen Bus, alles bleibt wie bisher).
/// </summary>
public sealed class SpielBusfahrtTests
{
    private static StadtFassung Fassung0 => StadtbauFassungTests.Fassung0.Value;

    /// <summary>Haltestellen der Probe: an der langen Strasse unten links und rechts, an der linken Strasse oben.</summary>
    internal static readonly (double X, double Y)[] Halte = [(2.5, 20.3), (26.0, 20.3), (7.0, 4.0)];

    /// <summary>Zebrastreifen der Probe an jeder Strasse (sonst kommt die Figur nicht ueber die Fahrbahn).</summary>
    internal static readonly (double X, double Y)[] Zebras = [(7.5, 17.5), (18.4, 17.5), (29.8, 17.5), (12.0, 20.8), (7.5, 9.0), (12.0, 12.6)];

    /// <summary>Inhalt mit Karte der Fassung 0, jede Strasse mit Gehweg (Kopie).</summary>
    internal static PyObjekt MitGehweg()
    {
        var inhalt = (PyObjekt)Py.Lesen(PythonJson.Text(Fassung0.MitKarte(SpielInhaltTests.Inhalt)))!;
        var karte = SpielDaten.Objekt(inhalt["orte"]);
        foreach (var teil in SpielDaten.Liste(SpielDaten.Objekt(karte["welt"])["landschaft"]).Select(SpielDaten.Objekt))
        {
            if (Py.Gleich(teil["typ"], "strasse"))
            {
                Py.Setzen(teil, "gehweg", 0.35);
            }
        }
        return inhalt;
    }

    internal static PyObjekt Stadtdaten(bool mitHalte = true)
    {
        var s = (PyObjekt)Py.Lesen(PythonJson.Text(Fassung0.Stadtdaten))!;
        if (mitHalte)
        {
            Py.Setzen(s, Haltestellen.Abschnitt, Halte.Select(h => (object?)new PyObjekt { { "x", h.X }, { "y", h.Y } }).ToList());
            // die Fahrbahn einer Strasse mit Gehweg quert die Figur nur am Zebrastreifen
            Py.Setzen(s, Zebrastreifen.Abschnitt, Zebras.Select(h => (object?)new PyObjekt { { "x", h.X }, { "y", h.Y } }).ToList());
        }
        return s;
    }

    internal static StadtSpiel Spiel(bool mitHalte = true, Spielablauf? ablauf = null)
    {
        var inhalt = MitGehweg();
        var stadt = new Stadt(inhalt, Stadtdaten(mitHalte)) { Fassung = Fassung0 };
        return new StadtSpiel(stadt, ablauf ?? SpielQuestTests.AllesErledigt(inhalt), null, SpielInnenraumTests.Innenraeume.Value);
    }

    [Fact]
    public void ProbeStadt_HatDreiHaltestellen_UndEineLinie()
    {
        var spiel = Spiel();
        Assert.Equal(3, spiel.Szene.Haltestellen.Count);
        var linie = Assert.IsType<Buslinie.Linie>(spiel.Linie);
        Assert.Empty(linie.NichtErreicht);
        Assert.Equal(3, linie.Halte.Count);
    }

    [Fact]
    public void Zeiten_JeOrt_BusNurWennSchneller()
    {
        var spiel = Spiel();
        var zeilen = new List<string>();
        foreach (var o in spiel.Szene.Orte)
        {
            var fuss = spiel.MinutenZuFuss(o.Id);
            var plan = spiel.BusPlan(o.Id);
            var lohnt = spiel.BusLohnt(o.Id);
            zeilen.Add(string.Create(CultureInfo.InvariantCulture,
                $"{o.Id} ({o.StehX:0.0},{o.StehZ:0.0}): zu Fuss {fuss}, Bus {plan?.Minuten} ({plan?.MinutenZuFuss}+{plan?.Warten}+{plan?.MinutenFahrt}) lohnt {lohnt}"));
            if (lohnt is { } l)
            {
                Assert.True(l.Bus < l.ZuFuss, zeilen[^1]);
                Assert.Equal(plan!.Minuten, l.Bus);
            }
        }
        TestContext.Current.SendDiagnosticMessage(string.Join("\n", zeilen));
        Assert.Contains(spiel.Szene.Orte, o => spiel.BusLohnt(o.Id) is not null);
    }

    private static int StadtZeilen(StadtSpiel spiel) => spiel.Ablauf.Stand.Verlauf.Count(e => e.Art == Stadt.Gegangen);

    [Fact]
    public void MitDemBus_EinEreignisMitBus_UhrUmDieMinuten_ZeitrafferHoechstensAchtSekunden()
    {
        var spiel = Spiel();
        var lohnt = Assert.IsType<(long ZuFuss, long Bus)>(spiel.BusLohnt("amt"));
        var plan = spiel.BusPlan("amt")!;
        var vorher = StadtZeilen(spiel);
        var uhr = spiel.Jetzt.Uhr;
        Assert.Equal(StadtGrund.Ok, spiel.MitBusZu("amt"));
        // genau eine Zeile stadt_gegangen mit "mit": "bus" und den Minuten des Plans (keine neue Ereignisart, kein Geld)
        Assert.Equal(vorher + 1, StadtZeilen(spiel));
        var e = spiel.Ablauf.Stand.Verlauf.Last();
        Assert.Equal(Stadt.Gegangen, e.Art);
        Assert.Equal(Stadt.MitBus, e.Daten[Stadt.Mit]);
        Assert.Equal(lohnt.Bus, Py.Int(e.Daten["minuten"]));
        Assert.Equal(plan.Schritte, Py.Int(e.Daten["schritte"]));
        Assert.Equal("amt", spiel.Jetzt.Ort);
        Assert.Equal(uhr + lohnt.Bus, spiel.Jetzt.Uhr);
        // erst zu Fuss zur Haltestelle, dann im Bus (Zeitraffer), dann zu Fuss zum Ziel
        Assert.False(spiel.ImBus);
        var sekunden = 0.0;
        var imBus = 0.0;
        while (spiel.Fortschreiten(1.0 / 60))
        {
            sekunden += 1.0 / 60;
            if (spiel.ImBus)
            {
                imBus += 1.0 / 60;
                Assert.Equal(-1, spiel.SchrittBild);
            }
            Assert.True(sekunden < 600, "kommt nicht an");
        }
        Assert.InRange(imBus, 1.0, Busfahrt.ZeitrafferSekunden + 0.1);
        Assert.False(spiel.Geht);
        var amt = spiel.Szene.Ort("amt")!;
        Assert.Equal(amt.StehX, spiel.FigurX, 3);
        Assert.Equal(amt.StehZ, spiel.FigurZ, 3);
        // die Uhr laeuft beim Gehen nicht weiter (alles steht im einen Ereignis)
        Assert.Equal(uhr + lohnt.Bus, spiel.Jetzt.Uhr);
    }

    [Fact]
    public void Antippen_UeberspringtDieFahrt()
    {
        var spiel = Spiel();
        Assert.Equal(StadtGrund.Ok, spiel.MitBusZu("amt"));
        var schritte = 0;
        while (!spiel.ImBus && spiel.Fortschreiten(1.0 / 60))
        {
            Assert.True(++schritte < 60 * 600);
        }
        Assert.True(spiel.ImBus);
        var zeilen = spiel.Ablauf.Stand.Verlauf.Count;
        // Tippen irgendwohin: kein neuer Weg, die Fahrt ist sofort vorbei, die Figur steigt aus und geht weiter zum Ziel
        Assert.True(spiel.GeheZu(spiel.FigurX + 3, spiel.FigurZ));
        Assert.False(spiel.ImBus);
        Assert.Equal(zeilen, spiel.Ablauf.Stand.Verlauf.Count);
        spiel.SofortAnkommen();
        Assert.Equal("amt", spiel.Jetzt.Ort);
        Assert.Equal(spiel.Szene.Ort("amt")!.StehX, spiel.FigurX, 3);
    }

    [Fact]
    public void LohntNicht_OderOhneHaltestellen_SchreibtNichts()
    {
        // Kaufhaus liegt neben Zuhause: zu Fuss schneller
        var spiel = Spiel();
        Assert.Null(spiel.BusLohnt("kaufhaus"));
        var vorher = spiel.Ablauf.Stand.Verlauf.Count;
        Assert.Equal(StadtGrund.KeinWeg, spiel.MitBusZu("kaufhaus"));
        Assert.Equal(Core.Texte.SpielProbeTexte.BusLohntNicht, spiel.Zeile);
        Assert.Equal(vorher, spiel.Ablauf.Stand.Verlauf.Count);
        // Gegenprobe Fassung 0 ohne Haltestellen: keine Linie, kein Bus, nichts geschrieben
        var ohne = Spiel(mitHalte: false);
        Assert.Null(ohne.Linie);
        Assert.All(ohne.Szene.Orte, o => Assert.Null(ohne.BusLohnt(o.Id)));
        vorher = ohne.Ablauf.Stand.Verlauf.Count;
        Assert.Equal(StadtGrund.KeinWeg, ohne.MitBusZu("amt"));
        Assert.Equal(vorher, ohne.Ablauf.Stand.Verlauf.Count);
    }

    [Fact]
    public void Takt_HoechstensFuenfzehnMinuten_WartenNieLaenger()
    {
        var spiel = Spiel();
        foreach (var o in spiel.Szene.Orte)
        {
            if (spiel.BusPlan(o.Id) is { } plan)
            {
                Assert.InRange(plan.Warten, 0, (long)Busfahrt.TaktHoechstens);
                Assert.Equal(plan.MinutenZuFuss + plan.Warten + plan.MinutenFahrt, plan.Minuten);
            }
        }
    }

    [Fact]
    public void Busfahrt_NachNeuaufbau_GleicheUhr_UndAlteZeilenOhneMitBleibenGleich()
    {
        var spiel = Spiel();
        Assert.Equal(StadtGrund.Ok, spiel.MitBusZu("amt"));
        var jetzt = spiel.Jetzt;
        // gleiche Uhr nach dem Neuaufbau aus dem Protokoll (Neustart, anderes Geraet)
        spiel.Ablauf.Neuaufbau();
        Assert.Equal(jetzt, spiel.Stadt.Stand(spiel.Ablauf.Stand));
    }
}
