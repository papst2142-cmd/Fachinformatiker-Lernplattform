using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Sammelliste 264 (Weisung P12-130 Frage 3 = c): Gebaeude faerben (Fassade, Dach, Rahmen; Tuer in vier Farben, Nico
/// 10.11.2026). Gegenprobe: nur den Hauptton ersetzen (<see cref="Gebaeudefarben.NurHaupttonFuerTests"/>) laesst Toene der
/// Familie stehen (fleckig).
/// </summary>
public class GebaeudeFarbenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static StadtSzene Szene(Projekt p) => Ansicht3D.Bauen(p.Stand(), D, Ansicht3D.Einstellung.Standard).Szene;

    private static Projekt Stadt1(string name = "Farben") => Projekt.Neu(name, D, Daten.NeueAblage());

    private static StadtHaus Haus(StadtSzene sz, string id) => Assert.Single(sz.Haeuser, h => h.Id == id);

    private const string Ziel = "#3D5F8F";

    [Fact]
    public void Fassade_GanzeFamilie_HelligkeitBleibt_GlasBleibt_GegenprobeFleckig()
    {
        var p = Stadt1();
        var vorher = Szene(p);
        Assert.True(p.AussenAendern("Farbe", a => AussenBearbeitung.FarbeSetzen(a, "moebelhaus", "fassade", Ziel)));
        var nachher = Szene(p);
        var haus = Haus(nachher, "moebelhaus");
        var geaendert = 0;
        var familieAlt = 0;
        for (var i = haus.StueckeVon; i < haus.StueckeBis; i++)
        {
            var alt = vorher.ModellAn(i)!;
            var neu = nachher.ModellAn(i)!;
            // die Farbe der Dreiecke (Erkennen von Glas, Licht, Pflanzen, Schnee) bleibt immer
            Assert.Equal(alt.Farben, neu.Farben);
            for (var d = 0; d < neu.Dreiecke; d++)
            {
                var f = alt.Farbe(d);
                if (StadtTueren.IstGlas(f) || Jahreszeitfarben.IstPflanze(f))
                {
                    for (var k = 0; k < 3; k++)
                    {
                        Assert.Equal(alt.EckFarbe(d, k), neu.EckFarbe(d, k));
                    }
                }
                if (alt.EckFarbe(d, 0) != neu.EckFarbe(d, 0))
                {
                    geaendert++;
                    // nie eine Leuchtmarke (Werte bis 1)
                    Assert.False(Nachtlicht.Leuchtet(neu.EckFarbe(d, 0)));
                }
            }
        }
        Assert.True(geaendert > 50, "Fassade nicht umgefaerbt: " + geaendert);
        // andere Haeuser unveraendert
        var bank = Haus(nachher, "kaufhaus");
        for (var i = bank.StueckeVon; i < bank.StueckeBis; i++)
        {
            Assert.Equal(vorher.ModellAn(i)!.EckFarben, nachher.ModellAn(i)!.EckFarben);
        }
        // Gegenprobe: nur der Hauptton, die anderen Toene der Familie bleiben stehen
        Gebaeudefarben.NurHaupttonFuerTests = true;
        try
        {
            var fleckig = Szene(p);
            for (var i = haus.StueckeVon; i < haus.StueckeBis; i++)
            {
                var alt = vorher.ModellAn(i)!;
                var neu = fleckig.ModellAn(i)!;
                var mitFamilie = nachher.ModellAn(i)!;
                for (var d = 0; d < neu.Dreiecke; d++)
                {
                    if (mitFamilie.EckFarbe(d, 0) != alt.EckFarbe(d, 0) && neu.EckFarbe(d, 0) == alt.EckFarbe(d, 0))
                    {
                        familieAlt++;
                    }
                }
            }
        }
        finally
        {
            Gebaeudefarben.NurHaupttonFuerTests = false;
        }
        Assert.True(familieAlt > 0, "Gegenprobe: nur der Hauptton sollte Toene der Familie stehen lassen");
    }

    [Fact]
    public void Umgefaerbt_BehaeltHelligkeitsverhaeltnis()
    {
        var haupt = new Vector3(0.8f, 0.7f, 0.6f);
        var dunkler = haupt * 0.8f;
        var ziel = Netz.Farbe(Ziel);
        var a = Gebaeudefarben.Umgefaerbt(haupt, haupt, ziel);
        var b = Gebaeudefarben.Umgefaerbt(dunkler, haupt, ziel);
        Assert.Equal(ziel, a);
        Assert.True(Vector3.Distance(b, ziel * 0.8f) < 0.01f);
    }

    [Fact]
    public void Hsv_HinUndZurueck()
    {
        foreach (var (_, wert) in Gebaeudefarben.Feste)
        {
            var (h, s, v) = Gebaeudefarben.Hsv(Netz.Farbe(wert));
            Assert.Equal(wert, Gebaeudefarben.Wert(Gebaeudefarben.AusHsv(h, s, v)));
        }
        Assert.Equal(12, Gebaeudefarben.Feste.Length);
        Assert.Equal(12, Gebaeudefarben.Feste.Select(f => f.Wert).Distinct().Count());
    }

    [Fact]
    public void Pruefung_FalscheWerte_Rot_GuteGruen()
    {
        var p = Stadt1();
        p.AussenAendern("Farbe", a =>
        {
            AussenBearbeitung.FarbeSetzen(a, "moebelhaus", "fassade", "#A8452E");
            AussenBearbeitung.FarbeSetzen(a, "moebelhaus", "tuer", "weiss");
        });
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot && f.Text.Contains("Farbe", StringComparison.Ordinal));
        // ungueltige Werte geht das Werkzeug gar nicht erst
        Assert.False(AussenBearbeitung.FarbeSetzen(p.AussenStand, "moebelhaus", "dach", "#12345"));
        Assert.False(AussenBearbeitung.FarbeSetzen(p.AussenStand, "moebelhaus", "tuer", "gruen"));
        // von Hand kaputt geschrieben: rot
        var nr = 0;
        foreach (var (teil, wert, text) in new[]
        {
            ("dach", (object?)"#GGGGGG", "außerhalb des Bereichs"),
            ("dach", 5L, "außerhalb des Bereichs"),
            ("tuer", "gruen", "Tür-Farbe"),
            ("fenster", "#FFFFFF", "unbekannten Teil"),
        })
        {
            var q = Stadt1("Kaputt " + nr++);
            q.AussenAendern("kaputt", a => Py.Setzen(Aussen.Stadtdaten(a), Gebaeudefarben.Schluessel, new PyObjekt { { "moebelhaus", new PyObjekt { { teil, wert } } } }));
            var fq = q.PruefenAussen();
            Assert.True(fq.Any(f => f.Rot && f.Text.Contains(text, StringComparison.Ordinal)), teil + ": " + string.Join(" / ", fq.Select(f => f.Text)));
        }
        // eine unbekannte Kennung raeumt das Werkzeug nach jedem Schritt weg; in einer Datei von Hand: rot
        var stand = Stadt1("Unbekannt").Stand();
        Py.Setzen(stand.Stadtdaten, Gebaeudefarben.Schluessel, new PyObjekt { { "gibtsnicht", new PyObjekt { { "dach", "#FFFFFF" } } } });
        Assert.Contains(Projekt.PruefenAussen(stand, D), f => f.Rot && f.Text.Contains("gibt es nicht", StringComparison.Ordinal));
    }

    [Fact]
    public void Werkzeug_EinSchritt_Rueckgaengig_WieVorher_Aufraeumen()
    {
        var p = Stadt1();
        Assert.False(AussenBearbeitung.HatFarben(p.AussenStand, "moebelhaus"));
        p.AussenAendern("Farbe", a => AussenBearbeitung.FarbeSetzen(a, "moebelhaus", "dach", "#4a4e54"));
        Assert.Equal("#4A4E54", AussenBearbeitung.FarbeVon(p.AussenStand, "moebelhaus", "dach"));
        Assert.True(p.Rueckgaengig());
        Assert.Null(AussenBearbeitung.FarbeVon(p.AussenStand, "moebelhaus", "dach"));
        Assert.False(Lernplattform.Core.Spiel.Py.Hat(Aussen.Stadtdaten(p.AussenStand), Gebaeudefarben.Schluessel));
        p.AussenAendern("Farbe", a => AussenBearbeitung.FarbeSetzen(a, "moebelhaus", "dach", "#4A4E54"));
        p.AussenAendern("Farbe", a => AussenBearbeitung.FarbeSetzen(a, "moebelhaus", "dach", null));
        Assert.False(Lernplattform.Core.Spiel.Py.Hat(Aussen.Stadtdaten(p.AussenStand), Gebaeudefarben.Schluessel));
        // Farben eines Gebaeudes ohne Ort fallen mit dem Gebaeude weg
        var stellwerk = Aussen.Landschaft(p.AussenStand).FindIndex(t => t is PyObjekt g && Lernplattform.Core.Spiel.Py.Gleich(g["typ"], "gebaeude"));
        var s = new AussenStelle(AussenArt.Gebaeude, stellwerk);
        var kennung = AussenBearbeitung.FarbKennung(p.AussenStand, s)!;
        p.AussenAendern("Farbe", a => AussenBearbeitung.FarbeSetzen(a, kennung, "fassade", "#F2EFE8"));
        p.AussenAendern("Löschen", a => AussenBearbeitung.Loeschen(a, s));
        Assert.False(AussenBearbeitung.HatFarben(p.AussenStand, kennung));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot && f.Text.Contains("gibt es nicht", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("braun")]
    [InlineData("schwarz")]
    [InlineData("weiss")]
    [InlineData("creme")]
    public void TuerVarianten_KastentuerRahmen(string id)
    {
        var p = Stadt1("Tuer " + id);
        var vorher = Szene(p);
        p.AussenAendern("Tür", a => AussenBearbeitung.FarbeSetzen(a, "moebelhaus", "tuer", id));
        var nachher = Szene(p);
        var farbe = Netz.Farbe(Gebaeudefarben.TuerVarianten.Single(v => v.Id == id).Wert);
        var t = Assert.Single(nachher.Tueren, x => x.Ort == "moebelhaus");
        if (!t.Modell)
        {
            Assert.Contains(nachher.Teile, k => k.Farbe == farbe && Vector2.Distance(new((k.X1 + k.X2) / 2, (k.Z1 + k.Z2) / 2), t.Mitte) <= 0.5f);
        }
        // andere Tueren unveraendert
        Assert.Equal(vorher.Teile.Count(k => k.Farbe == StadtTueren.Rahmen) - (t.Modell ? 0 : 1), nachher.Teile.Count(k => k.Farbe == StadtTueren.Rahmen));
    }

    [Fact]
    public void Editor_Farben_Verlauf_Blueprint_Format3_UndSetzen()
    {
        var e = Entwurf.Probehaus();
        Assert.True(e.FarbeSetzen("fassade", "#a8452e"));
        Assert.True(e.FarbeSetzen("tuer", "creme"));
        Assert.False(e.FarbeSetzen("dach", "rot"));
        Assert.Equal("#A8452E", Lernplattform.Core.Spiel.SpielDaten.Get(e.Farben, "fassade"));
        e.Zurueck();
        Assert.Null(Lernplattform.Core.Spiel.SpielDaten.Get(e.Farben, "tuer"));
        e.Wiederholen();
        var p = Projekt.Neu("Editor Farben", D, Daten.NeueAblage(), leer: (24, 24));
        var b = e.AlsBlueprint("Rotes Haus", Katalog.Bauteil, p.Name, p.Basis.StadtId, out _);
        var gelesen = Blueprint.Lesen(b.Schreiben());
        Assert.Equal(3L, Blueprint.FormatVersion);
        Assert.Equal(PythonJson.Standard(e.Farben), PythonJson.Standard(gelesen.Farben));
        Assert.Equal(PythonJson.Standard(e.Farben), PythonJson.Standard(Entwurf.Aus(gelesen)!.Farben));
        string? grund = null;
        p.AussenAendern("Setzen", a => gelesen.Setzen(a, 12, 12, out grund));
        Assert.Null(grund);
        Assert.Equal("#A8452E", AussenBearbeitung.FarbeVon(p.AussenStand, b.Modell, "fassade"));
        Assert.Equal("creme", AussenBearbeitung.FarbeVon(p.AussenStand, b.Modell, "tuer"));
        // falsche Farbe in der Datei = kaputt
        var kaputt = System.Text.Encoding.UTF8.GetString(b.Schreiben()).Replace("#A8452E", "#A8452", StringComparison.Ordinal);
        Assert.Throws<StadtbauFehler>(() => Blueprint.Lesen(System.Text.Encoding.UTF8.GetBytes(kaputt)));
    }

    [Fact]
    public void Editor_Tuerblatt_InDerGewaehltenFarbe()
    {
        var e = Entwurf.Probehaus();
        e.FarbeSetzen("tuer", "weiss");
        var p = Projekt.Neu("Weisse Tuer", D, Daten.NeueAblage(), leer: (24, 24));
        p.AussenAendern("Straße", a =>
        {
            var st = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 2, 18)!;
            AussenBearbeitung.LaengeSetzen(a, st, 20);
        });
        var b = e.AlsBlueprint("Weisse Tuer", Katalog.Bauteil, p.Name, p.Basis.StadtId, out _);
        p.AussenAendern("Setzen", a => b.Setzen(a, 12, 12, out _));
        var sz = Szene(p);
        var t = Assert.Single(sz.Tueren);
        Assert.True(t.Modell);
        var haus = Assert.Single(sz.Haeuser, h => h.Id.StartsWith(Bauteile.ModellPraefix, StringComparison.Ordinal));
        var eigene = StadtTueren.ModellTueren(sz.Stuecke, haus.StueckeVon, haus.StueckeBis, sz.ModellAn, t.Mitte, t.Aussen, weite: 0.1f, glas: true)
            .First(g => g.A1 <= 0.001f && g.A2 >= -0.001f);
        var weiss = Netz.Farbe(Gebaeudefarben.TuerVarianten.Single(v => v.Id == "weiss").Wert);
        Assert.All(eigene.Dreiecke, d => Assert.True(Vector3.Distance(sz.ModellAn(eigene.Stueck)!.Farbe(d), weiss) < 0.01f));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot);
    }
}
