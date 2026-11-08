using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.22.0 (Weisung P12-91, Kurzplan P12-59): welche Aufgaben an welchem Ort der Stadt haengen. Jede Aufgabe der
/// Spielgeschichte genau einem Ort, Zaehlung je Ort gleich der Liste, Gegenprobe Aufgabe ohne Ort.
/// </summary>
public class SpielgeschichteTests
{
    private static Projekt Stadt1() => Projekt.Neu("Spielgeschichte", Daten.Spiel, Daten.NeueAblage());

    private static Spielgeschichte Von(Projekt p) => Spielgeschichte.Aus(Daten.Spiel.Inhalt, p.AussenStand);

    [Fact]
    public void Stadt1_JedeAufgabeGenauEinemOrt_ZahlenWieImKurzplan()
    {
        var g = Von(Stadt1());
        Assert.Equal(150, g.Aufgaben.Count);
        Assert.All(g.Aufgaben, e => Assert.NotNull(e.Ort));
        Assert.Equal(g.Aufgaben.Count, g.Aufgaben.Select(e => e.Id).Distinct().Count());
        var erwartet = new Dictionary<string, int>
        {
            ["bitweiche"] = 76, ["talheim"] = 19, ["betriebswerkstatt"] = 18, ["lindenau"] = 16, ["birkenhain"] = 15, ["talheim_nord"] = 6,
        };
        Assert.Equal(erwartet.OrderBy(p => p.Key), g.Zahlen().OrderBy(p => p.Key));
        Assert.Equal(150, g.Zahlen().Values.Sum());
        // die Stelle des Spiels heisst wie der Innenraum, der Ort der Karte wie auf der Karte
        Assert.Equal("bitweiche", g.OrtJeStelle[SpielDaten.OrtBuero]);
        Assert.Equal("talheim_nord", g.OrtJeStelle["talheim_nord"]);
    }

    [Fact]
    public void ZaehlungJeOrt_GleichDerListe_UndDerRechtenSpalte()
    {
        var g = Von(Stadt1());
        foreach (var (ort, zahl) in g.Zahlen())
        {
            Assert.Equal(zahl, g.Liste(ort: ort).Count);
            Assert.Equal(zahl, g.AnOrt(ort, Spielgeschichte.Art.Aufgabe).Count);
        }
        Assert.Equal(150, g.Liste().Count);
        // Liste nach Tag sortiert
        var tage = g.Liste().Select(e => e.AbTag).ToList();
        Assert.Equal(tage.Order(), tage);
    }

    [Fact]
    public void AuftraegeUndZwischenfaelle_ZaehlenNichtAmHaus()
    {
        var p = Stadt1();
        var g = Von(p);
        var inhalt = Daten.Spiel.Inhalt;
        var vorlagen = SpielDaten.Liste(inhalt["aufgaben"]).Count - 150;
        Assert.True(vorlagen > 0);
        Assert.Equal(vorlagen, g.Alle.Count(e => e.Sorte == Spielgeschichte.Art.Auftrag));
        Assert.Equal(50, g.Alle.Count(e => e.Sorte == Spielgeschichte.Art.Zwischenfall));
        // Gegenprobe: zaehlten Auftraege und Zwischenfaelle mit, waere die Zahl am Haus groesser
        Assert.True(g.Alle.Count(e => e.Ort == "talheim_nord") > g.Zahl("talheim_nord"));
        Assert.Equal(6, g.Zahl("talheim_nord"));
    }

    [Fact]
    public void FreiAb_TagDesKunden_ImmerTag1_SonstVomSpiel()
    {
        var g = Von(Stadt1());
        Assert.Equal(1, g.FreiAbTag("bitweiche"));
        Assert.Equal(12, g.FreiAbTag("talheim_nord"));
        Assert.Equal(22, g.FreiAbTag("betriebswerkstatt"));
        Assert.Equal(45, g.FreiAbTag("lindenau"));
        Assert.Equal(66, g.FreiAbTag("birkenhain"));
        Assert.Null(g.FreiAbTag("gewerbehof"));
        Assert.Null(g.FreiAbTag("gibt_es_nicht"));
    }

    [Fact]
    public void ErstNach_ZwischenOrten_LaufenUeberDasBuero()
    {
        var g = Von(Stadt1());
        var nachIds = g.Aufgaben.Where(e => e.Nach is not null).ToList();
        Assert.Equal(68, nachIds.Count);
        var jeId = g.Alle.ToDictionary(e => e.Id);
        foreach (var e in nachIds)
        {
            Assert.NotNull(e.NachTitel);
            var vorher = jeId[e.Nach!];
            if (vorher.Ort != e.Ort)
            {
                Assert.True(vorher.Ort == "bitweiche" || e.Ort == "bitweiche", e.Id + " nach " + vorher.Id);
            }
        }
    }

    [Fact]
    public void Gegenprobe_AufgabeOhneOrt_ZaehltNirgends()
    {
        // eine Aufgabe, deren Ort es in der Stadt nicht gibt, steht ohne Ort in der Liste und zaehlt an keinem Haus
        var inhalt = Py.Flach(Daten.Spiel.Inhalt);
        var aufgaben = SpielDaten.Liste(inhalt["aufgaben"]).ToList();
        var erste = Py.Flach(SpielDaten.Objekt(aufgaben[0]));
        Py.Setzen(erste, "ort", "gibt_es_nicht");
        aufgaben[0] = erste;
        Py.Setzen(inhalt, "aufgaben", aufgaben);
        var g = Spielgeschichte.Aus(inhalt, Stadt1().AussenStand);
        Assert.Null(g.Aufgaben[0].Ort);
        Assert.Equal(149, g.Zahlen().Values.Sum());
        Assert.Equal(75, g.Zahl("bitweiche"));
    }

    [Fact]
    public void LeereStadt_KeinOrt_KeineZahl()
    {
        var p = Projekt.Neu("Leer", Daten.Spiel, Daten.NeueAblage(), leer: (16, 16));
        var g = Von(p);
        Assert.Empty(g.Zahlen());
        Assert.All(g.Aufgaben, e => Assert.Null(e.Ort));
    }

    [Fact]
    public void Umbenennen_Verschieben_Tauschen_AendernDieZuordnungNicht()
    {
        var p = Stadt1();
        var vorher = Von(p).Zahlen();
        Assert.Null(p.AussenAendern("Ort umbenannt", a => AussenBearbeitung.OrtUmbenennen(a, "talheim_nord", "Stellwerk Talheim-Nord")));
        Assert.Equal(vorher, Von(p).Zahlen());
    }

    [Fact]
    public void Filter_OrtTagPersonSuche()
    {
        var g = Von(Stadt1());
        Assert.All(g.Liste(abTag: 12), e => Assert.True(e.AbTag >= 12));
        Assert.True(g.Liste(abTag: 12).Count < 150);
        var person = g.Personen()[0];
        Assert.All(g.Liste(person: person), e => Assert.Equal(person, e.Auftraggeber));
        var treffer = g.Liste(suche: "talheim-nord");
        Assert.NotEmpty(treffer);
        Assert.All(treffer, e => Assert.Contains("talheim-nord", Katalog.Normal(e.Titel + " " + e.Auftraggeber)));
        // Suche nach dem Ortsnamen ueber die Namensfunktion
        Assert.Equal(15, g.Liste(suche: "Birkenhain", ortName: o => o == "birkenhain" ? "Birkenhain" : o).Count(e => e.Ort == "birkenhain"));
    }
}
