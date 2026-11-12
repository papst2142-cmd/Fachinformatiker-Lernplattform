using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17): Pruefregeln der Innenraeume im gemeinsamen Teil (Core/Stadtbau). Fassung 0 hat
/// genau Fund 216 als Rot (Elektromarkt, obere Reihe nicht erreichbar); jede Regel wird mit einer Gegenprobe rot.
/// </summary>
public class PruefungTests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<StadtbauFund> Pruefen(string ort, PyObjekt g) =>
        StadtbauPruefung.Innen(ort, g, InnenGrundriss.Personen(D.Fassung.Innenraeume, ort), D.Grafik, D.Geschaefte, D.Fassung.Innenraeume);

    private static PyObjekt Grundriss(string ort) => InnenGrundriss.Grundriss(D.Fassung.Innenraeume, ort);

    [Fact]
    public void Orte_SindGeschaefteUndSchalter()
    {
        Assert.Equal(["moebelhaus", "elektromarkt", "bank", "amt", "kaufhaus"], D.Orte);
    }

    [Fact]
    public void Fassung0_NurFund216Rot()
    {
        foreach (var ort in D.Orte)
        {
            var rot = Pruefen(ort, Grundriss(ort)).Where(f => f.Rot).ToList();
            if (ort == "elektromarkt")
            {
                // Sammelliste 216: die obere Regalreihe ist zu Fuss nicht erreichbar (acht Waren der Teile)
                Assert.Equal(8, rot.Count);
                Assert.All(rot, f => Assert.Contains("zu Fuß nicht erreichbar", f.Text, StringComparison.Ordinal));
                Assert.All(rot, f => Assert.NotNull(f.X));
                // Sammelliste 219 Punkt 2: der Name wie im Spiel im Text, die Kennung nur klein im Tipp
                var kennungen = rot.Select(f => f.Tipp.Split("(Kennung: ")[1].TrimEnd(')')).Order(StringComparer.Ordinal).ToList();
                Assert.Equal(["teile:cpu", "teile:gehaeuse", "teile:gpu", "teile:mainboard", "teile:netzteil", "teile:notebook", "teile:ram", "teile:ssd"], kennungen);
                var waren = rot.Select(f => f.Text.Split('„', '“')[1]).Order(StringComparer.Ordinal).ToList();
                Assert.Equal(["Arbeitsspeicher", "CPU", "Gehäuse", "Grafikkarte", "Mainboard", "Netzteil", "Notebook", "SSD"], waren);
                Assert.All(rot, f => Assert.DoesNotContain("teile:", f.Text, StringComparison.Ordinal));
            }
            else
            {
                Assert.True(rot.Count == 0, ort + ": " + string.Join(" | ", rot.Select(f => f.Text)));
            }
        }
    }

    [Fact]
    public void Ueberschneidung_ZweiRegaleAufeinander_Rot()
    {
        var g = Grundriss("moebelhaus");
        var (stelle, d) = Bearbeitung.Alle(g).First(s => Py.Str(s.Deko["typ"]) == "regal" && s.Stelle.Bereich != "flur");
        var kopie = Daten.Kopie(d);
        Liste(Bearbeitung.Bereich(g, stelle.Bereich)["deko"]).Add(kopie);
        Assert.Contains(Pruefen("moebelhaus", g), f => f.Rot && f.Text.Contains("überschneiden sich", StringComparison.Ordinal));
    }

    [Fact]
    public void Aufliegen_KasseAufTresen_Erlaubt_KasseAufRegal_Rot()
    {
        Assert.True(InnenKatalog.DarfAufliegen("tresen", "kasse"));
        Assert.True(InnenKatalog.DarfAufliegen("kasse", "tresen"));
        Assert.False(InnenKatalog.DarfAufliegen("regal", "kasse"));
        var g = Grundriss("moebelhaus");
        var (stelle, regal) = Bearbeitung.Alle(g).First(s => Py.Str(s.Deko["typ"]) == "regal" && s.Stelle.Bereich != "flur");
        Liste(Bearbeitung.Bereich(g, stelle.Bereich)["deko"]).Add(new PyObjekt
        {
            { "typ", "kasse" }, { "x", regal["x"] }, { "y", regal["y"] }, { "w", 0.5 }, { "h", 0.4 },
        });
        Assert.Contains(Pruefen("moebelhaus", g), f => f.Rot && f.Text.Contains("Kasse", StringComparison.Ordinal) && f.Text.Contains("überschneiden", StringComparison.Ordinal));
    }

    [Fact]
    public void Erreichbarkeit_WareHinterWand_Rot_GegenprobeOhneWand()
    {
        // Gegenprobe zu Fund 216: im Moebelhaus ist alles erreichbar; ein Regal vor der Tuer des Verkaufsraums sperrt ab
        var g = Grundriss("moebelhaus");
        var raum = Liste(g["raeume"]).OfType<PyObjekt>().First();
        var tuer = Objekt(raum["tuer"]);
        var y = Py.F(raum["y"]) + Py.F(raum["h"]) - 0.8;
        Liste(raum["deko"]).Add(new PyObjekt
        {
            { "typ", "regal" }, { "x", Py.F(raum["x"]) }, { "y", y }, { "w", Py.F(raum["w"]) }, { "h", 0.3 },
        });
        Assert.True(Py.F(tuer["bis"]) > Py.F(tuer["von"]));
        var funde = Pruefen("moebelhaus", g);
        Assert.Contains(funde, f => f.Rot && f.Text.Contains("zu Fuß nicht erreichbar", StringComparison.Ordinal));
    }

    [Fact]
    public void Grundriss_RaumAusserhalb_TuerFalsch_UnbekannterTyp_Rot()
    {
        var g = Grundriss("bank");
        var raum = Liste(g["raeume"]).OfType<PyObjekt>().First();
        Py.Setzen(raum, "w", Py.F(g["breite"]) + 3);
        Assert.Contains(Pruefen("bank", g), f => f.Rot && f.Text.Contains("liegt nicht ganz im Haus", StringComparison.Ordinal));

        g = Grundriss("bank");
        raum = Liste(g["raeume"]).OfType<PyObjekt>().First();
        Py.Setzen(raum, "tuer", new PyObjekt { { "von", 1.0 }, { "bis", 1.2 } });
        Assert.Contains(Pruefen("bank", g), f => f.Rot && f.Text.Contains("liegt nicht auf der Wand zum Flur", StringComparison.Ordinal));

        g = Grundriss("bank");
        Liste(Liste(g["raeume"]).OfType<PyObjekt>().First()["deko"]).Add(new PyObjekt { { "typ", "raumschiff" }, { "x", 1.0 }, { "y", 1.0 }, { "w", 1.0 }, { "h", 1.0 } });
        Assert.Contains(Pruefen("bank", g), f => f.Rot && f.Text.Contains("kein Teil aus dem Katalog", StringComparison.Ordinal));
    }

    [Fact]
    public void Grundriss_StueckRagtAusDemRaum_Rot()
    {
        var g = Grundriss("amt");
        var raum = Liste(g["raeume"]).OfType<PyObjekt>().First();
        Liste(raum["deko"]).Add(new PyObjekt { { "typ", "pflanze" }, { "x", Py.F(raum["x"]) - 0.5 }, { "y", Py.F(raum["y"]) + 1 }, { "w", 0.8 }, { "h", 0.8 } });
        Assert.Contains(Pruefen("amt", g), f => f.Rot && f.Text.Contains("ragt aus dem Raum", StringComparison.Ordinal));
    }

    [Fact]
    public void Sortiment_WareOhneRegal_Rot()
    {
        var g = Grundriss("moebelhaus");
        var (stelle, _) = Bearbeitung.Alle(g).First(s => Py.Hat(s.Deko, "ware"));
        Bearbeitung.Ware(g, stelle, null);
        var rot = Pruefen("moebelhaus", g).Where(f => f.Rot).ToList();
        Assert.NotEmpty(rot);
    }

    [Fact]
    public void Sortiment_ZeigtNamenWieImSpiel_KennungNurImTipp()
    {
        // Sammelliste 219 Punkt 2: Moebel ohne Auslage (Moebelhaus) und Teile-Typ ohne Regal (Elektromarkt)
        var g = Grundriss("moebelhaus");
        var (stelle, d) = Bearbeitung.Alle(g).First(s => Py.Hat(s.Deko, "ware") && !Py.Str(s.Deko["ware"]).Contains(':', StringComparison.Ordinal));
        var kennung = Py.Str(d["ware"]);
        var name = D.Geschaefte.Finden("moebelhaus", Geschaefte.ArtMoebel, kennung)!.Name;
        Assert.NotEqual(kennung, name);
        foreach (var andere in Bearbeitung.Alle(g).Where(s => Py.Gleich(s.Deko["ware"], kennung)).Select(s => s.Stelle).Reverse().ToList())
        {
            Bearbeitung.Ware(g, andere, null);
        }
        var fund = Assert.Single(Pruefen("moebelhaus", g), f => f.Text.Contains("steht nirgends aus", StringComparison.Ordinal));
        Assert.Contains("„" + name + "“", fund.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(kennung, fund.Text, StringComparison.Ordinal);
        Assert.EndsWith("(Kennung: " + kennung + ")", fund.Tipp, StringComparison.Ordinal);

        var e = Grundriss("elektromarkt");
        foreach (var s in Bearbeitung.Alle(e).Where(s => Py.Gleich(s.Deko["ware"], "teile:mainboard")).Select(s => s.Stelle).Reverse().ToList())
        {
            Bearbeitung.Ware(e, s, null);
        }
        var regal = Assert.Single(Pruefen("elektromarkt", e), f => f.Text.Contains("kein Regal", StringComparison.Ordinal));
        Assert.Equal("Für „Mainboard“ gibt es kein Regal.", regal.Text);
        Assert.EndsWith("(Kennung: teile:mainboard)", regal.Tipp, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("elektromarkt", "teile:ram", "Arbeitsspeicher")]
    [InlineData("kaufhaus", "kleidung:kopf", "Kappen und Mützen")]
    [InlineData("moebelhaus", "gibt_es_nicht", "gibt_es_nicht")]
    public void Warennamen_WieImSpiel(string ort, string kennung, string erwartet)
    {
        var name = Warennamen.Name(D.Geschaefte, ort, kennung);
        Assert.Equal(erwartet, name);
        Assert.Equal(name == kennung ? "" : " (Kennung: " + kennung + ")", Warennamen.Kennung(name, kennung));
    }

    [Fact]
    public void Grenzen_GelbAb80Prozent_RotDarueber()
    {
        var funde = new List<StadtbauFund>();
        StadtbauPruefung.Grenzen(127_999, 31, funde);
        Assert.Empty(funde);
        StadtbauPruefung.Grenzen(128_000, 12, funde);
        Assert.Single(funde);
        Assert.False(funde[0].Rot);
        funde.Clear();
        StadtbauPruefung.Grenzen(10_000, 32, funde);
        Assert.False(Assert.Single(funde).Rot);
        funde.Clear();
        StadtbauPruefung.Grenzen(160_001, 12, funde);
        Assert.True(Assert.Single(funde).Rot);
        funde.Clear();
        StadtbauPruefung.Grenzen(1_000, 41, funde);
        Assert.True(Assert.Single(funde).Rot);
        funde.Clear();
        StadtbauPruefung.Grenzen(160_000, 40, funde);
        Assert.False(Assert.Single(funde).Rot);
    }

    /// <summary>Zaehlung wie das Spiel (StadtFlaeche zeichnet Netz ohne Waende, jede Wand, Figur, jede Person): feste Werte in Fassung 0.</summary>
    [Fact]
    public void Grenzen_Fassung0_FesteWerte()
    {
        var soll = new Dictionary<string, (long, int)>
        {
            // A 0.68.0 (Weisung P12-206): der begehbare Schrank in der Auslage ist jetzt Entwurf B (vorher 10160)
            ["moebelhaus"] = (10184, 12),
            ["elektromarkt"] = (15282, 12),
            ["bank"] = (7468, 12),
            ["amt"] = (9259, 12),
            ["kaufhaus"] = (17796, 12),
        };
        foreach (var ort in D.Orte)
        {
            var innen = Innenraum.AusGrundrissMitPersonen(StadtbauPruefung.Ansicht(ort), Grundriss(ort), InnenGrundriss.Personen(D.Fassung.Innenraeume, ort), D.Grafik);
            Assert.Equal(soll[ort], StadtbauGrenzen.Innen(innen));
        }
    }

    /// <summary>
    /// Das Spiel baut aus dem flach geschriebenen Grundriss (Export) denselben Innenraum wie aus den Schichten der
    /// Fassung 0 (zusatz, zusatz_nachtrag, zusatz_moebel, zusatz_schraenke): Netze, Waende, Deko, Personen.
    /// </summary>
    [Fact]
    public void Flachschreiben_GleicherInnenraum()
    {
        var innen = D.Fassung.Innenraeume;
        foreach (var ort in D.Orte)
        {
            var flach = InnenGrundriss.MitGrundriss(innen, ort, Grundriss(ort));
            Assert.Equal(PythonJson.Text(Innenraum.LadenGebaeude(innen, ort)), PythonJson.Text(Innenraum.LadenGebaeude(flach, ort)));
            Assert.Equal(PythonJson.Text(InnenGrundriss.Personen(innen, ort)), PythonJson.Text(InnenGrundriss.Personen(flach, ort)));
            foreach (var anderer in D.Orte.Where(o => o != ort))
            {
                Assert.Equal(PythonJson.Text(Innenraum.LadenGebaeude(innen, anderer)), PythonJson.Text(Innenraum.LadenGebaeude(flach, anderer)));
            }
            var vorher = Innenraum.AusGrundrissMitPersonen(StadtbauPruefung.Ansicht(ort), Grundriss(ort), InnenGrundriss.Personen(innen, ort), D.Grafik);
            var nachher = Innenraum.AusGrundrissMitPersonen(StadtbauPruefung.Ansicht(ort), InnenGrundriss.Grundriss(flach, ort), InnenGrundriss.Personen(flach, ort), D.Grafik);
            Assert.Equal(StadtbauGrenzen.Innen(vorher), StadtbauGrenzen.Innen(nachher));
        }
        // das Original bleibt
        Assert.True(Py.Hat(innen, "zusatz_schraenke"));
    }

    [Fact]
    public void Katalog_KenntJedenTypDerFassung0()
    {
        foreach (var ort in D.Orte)
        {
            foreach (var (_, d) in InnenGrundriss.Einrichtung(Grundriss(ort)))
            {
                Assert.True(InnenKatalog.Bekannt(Py.Str(d["typ"])), ort + ": " + Py.Str(d["typ"]));
            }
        }
        Assert.Equal(InnenKatalog.Teile.Count, InnenKatalog.Teile.Select(t => t.Typ).Distinct().Count());
        Assert.All(InnenKatalog.Teile, t => Assert.True(t.Breite > 0 && t.Tiefe > 0 && t.Name.Length > 0));
    }
}
