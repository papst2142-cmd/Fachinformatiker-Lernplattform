using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 12 Welle 2 Kleidung (Weisung P12-206, Kurzplan P12-118 Abschnitte 1 und 2, Nico 2b 3a): neue Kleidung in vier
/// angehaengten Stellen (Kleid oder Rock, Jacke oder Mantel, Brille, Tasche), Frisur und Bart an der 3D-Figur, Bart nur beim
/// Anlegen. Alte Spielstaende, Zeilen und Fingerabdruecke bleiben gleich.
/// </summary>
public class SpielKleidungWelle2Tests
{
    private static PyObjekt Inhalt => SpielCharakterTests.InhaltMitNeuen.Value;

    private static PyObjekt Daten => SpielGeschaefteTests.Daten.Value;

    private static Geschaefte Neu(PyObjekt? daten = null) => new(Inhalt, daten ?? Daten);

    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static Spielablauf NeuesSpiel(PyObjekt? aussehen = null)
    {
        var zaehler = 0;
        var spiel = new Spielablauf(Inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 11, 12).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        spiel.Profil("Nico", aussehen ?? new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" } });
        spiel.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", 2000L } });
        return spiel;
    }

    /// <summary>Ein Stueck ohne Laden in den Stand bringen (Kaufzeile wie im Kaufhaus).</summary>
    private static void Gekauft(Spielablauf spiel, string id) =>
        spiel.Log(Kleidung.Gekauft, new PyObjekt { { "stueck", id }, { "geld", -Kleidung.Finden(Daten, id)!.Preis }, { "tag", 1L }, { "laden", Kleidung.Laden } });

    [Fact]
    public void Sortiment_NeueStellenUndStueckeNurAngehaengt()
    {
        Assert.Empty(Kleidung.Pruefen(SpielDaten.Objekt(Daten[Kleidung.Laden])));
        Assert.Empty(Neu().Pruefen(SpielInnenraumTests.Innenraeume.Value));
        var stellen = Kleidung.Stellen(Daten);
        // die vier alten Stellen vorne unveraendert, die neuen dahinter mit ihrer Auslage
        Assert.Equal(["oberteil", "hose", "schuhe", "kopf", "kleid", "jacke", "brille", "tasche"], stellen.Select(s => s.Id));
        Assert.All(stellen.Take(4), s => Assert.Null(s.Auslage));
        Assert.Equal(["oberteil", "oberteil", "kopf", "kopf"], stellen.Skip(4).Select(s => s.Auslage));
        Assert.Equal(["Kleider und Röcke", "Jacken und Mäntel", "Brillen", "Handtaschen"], stellen.Skip(4).Select(s => s.Abteilung));
        var alle = Kleidung.Alle(Daten);
        Assert.Equal(28 + 46, alle.Count);
        // die 28 Stuecke von vorher stehen vorne in derselben Reihenfolge
        Assert.Equal("fruehlingsshirt", alle[27].Id);
        var neue = alle.Skip(28).ToList();
        Assert.All(neue, k => Assert.Null(k.Festzeit));
        // Kurzplan P12-118: Preise je Stueck, 4 bis 6 Farben
        var preise = new Dictionary<string, long>
        {
            ["tshirt"] = 15, ["hemd_lang"] = 35, ["kurz"] = 25, ["rock"] = 35, ["kleid"] = 60, ["jacke"] = 70, ["mantel"] = 120, ["brille"] = 40, ["handtasche"] = 50,
        };
        Assert.Equal(preise.Keys.Order(), neue.Select(k => k.Form).Distinct().Order());
        Assert.All(neue, k => Assert.Equal(preise[k.Form], k.Preis));
        Assert.All(neue.GroupBy(k => k.Form), g => Assert.InRange(g.Count(), 4, 6));
        // jede Stelle hat Ware, jede neue Ware ist im Laden zu kaufen
        Assert.All(stellen, s => Assert.Contains(Kleidung.Sortiment(Daten), k => k.Slot == s.Id));
        Assert.All(neue, k => Assert.Contains(Neu().Waren(Kleidung.Laden), w => w.Id == k.Id && w.Typ == k.Slot));
    }

    /// <summary>Eine angehaengte Stelle braucht eine Auslage: eigene oder die ihrer Stelle "auslage" (Gegenprobe ohne beides).</summary>
    [Fact]
    public void Auslage_UeberDieAndereStelle_GegenprobeRot()
    {
        PyObjekt Kaputt(Action<List<object?>> aendern)
        {
            var d = (PyObjekt)Py.Lesen(PythonJson.Text(Daten))!;
            aendern((List<object?>)SpielDaten.Objekt(d[Kleidung.Laden])["slots"]!);
            return d;
        }
        PyObjekt Stelle(List<object?> s, string id) => s.OfType<PyObjekt>().Single(x => Py.Gleich(x["id"], id));
        // auf eine unbekannte Stelle
        Assert.Contains("kaufhaus: brille: Auslage ungueltig", Neu(Kaputt(s => Py.Setzen(Stelle(s, "brille"), "auslage", "guertel"))).Pruefen(SpielInnenraumTests.Innenraeume.Value));
        // auf eine Stelle, die selbst nur eine geliehene Auslage hat
        Assert.Contains("kaufhaus: brille: Auslage ungueltig", Neu(Kaputt(s => Py.Setzen(Stelle(s, "brille"), "auslage", "tasche"))).Pruefen(SpielInnenraumTests.Innenraeume.Value));
        // ohne Feld: die Stelle hat keine Auslage im Kaufhaus
        var ohne = Kaputt(s => Stelle(s, "jacke").Remove(Stelle(s, "jacke").First(p => p.Key == "auslage")));
        Assert.Contains("kaufhaus: Kleidung ohne Auslage: jacke", Neu(ohne).Pruefen(SpielInnenraumTests.Innenraeume.Value));
        Assert.Equal("oberteil", Kleidung.AuslageVon(Daten, "jacke"));
        Assert.Equal("hose", Kleidung.AuslageVon(Daten, "hose"));
    }

    /// <summary>
    /// Die Zeile kleidung_getragen nennt die neuen Stellen nur, wenn dort etwas getragen wird oder sie schon gespeichert waren:
    /// ein Spielstand ohne neue Kleidung schreibt dieselben Zeilen wie vorher (Fingerabdruck Kaufhaus bleibt).
    /// </summary>
    [Fact]
    public void Getragen_NeueStellenNurWennBelegt()
    {
        var spiel = NeuesSpiel();
        var g = Neu();
        Gekauft(spiel, "hoodie_gruen");
        spiel.KleidungAnziehen(g, "oberteil", "hoodie_gruen");
        Assert.Equal(["oberteil", "hose", "schuhe", "kopf"], SpielDaten.Objekt(spiel.Stand.Verlauf[^1].Daten["getragen"]).Select(p => p.Key));
        Gekauft(spiel, "kleid_rot");
        spiel.KleidungAnziehen(g, "kleid", "kleid_rot");
        var zeile = SpielDaten.Objekt(spiel.Stand.Verlauf[^1].Daten["getragen"]);
        Assert.Equal(["oberteil", "hose", "schuhe", "kopf", "kleid"], zeile.Select(p => p.Key));
        Assert.Equal("kleid_rot", Kleidung.Angezogen(spiel.Stand, Daten)["kleid"].Id);
        // ablegen: die Stelle bleibt mit null in der Zeile (sonst waere unklar, ob sie abgelegt ist)
        spiel.KleidungAnziehen(g, "kleid", null);
        zeile = SpielDaten.Objekt(spiel.Stand.Verlauf[^1].Daten["getragen"]);
        Assert.True(Py.Hat(zeile, "kleid"));
        Assert.Null(zeile["kleid"]);
        Assert.False(Kleidung.Angezogen(spiel.Stand, Daten).ContainsKey("kleid"));
        // ein anderes Stueck tragen haelt die anderen fest
        Gekauft(spiel, "brille_rot");
        spiel.KleidungAnziehen(g, "brille", "brille_rot");
        Assert.Equal(["hoodie_gruen", "brille_rot"], Kleidung.Angezogen(spiel.Stand, Daten).Values.Select(k => k.Id));
        Assert.Empty(spiel.Stand.Unterschiede(spiel.Neuaufbau()));
    }

    /// <summary>Jede neue Form zeigt sich an der Figur: eigene Teile oder umgefaerbte Dreiecke (Gegenprobe: ohne Stueck gleich).</summary>
    [Theory]
    [InlineData("oberteil", "tshirt")]
    [InlineData("oberteil", "hemd_lang")]
    [InlineData("hose", "kurz")]
    [InlineData("kleid", "kleid")]
    [InlineData("kleid", "rock")]
    [InlineData("jacke", "jacke")]
    [InlineData("jacke", "mantel")]
    [InlineData("brille", "brille")]
    [InlineData("tasche", "handtasche")]
    public void Figur_NeueFormSichtbar(string slot, string form)
    {
        var grund = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck(slot, slot is "oberteil" ? "shirt" : slot is "hose" ? "hose" : form, "#DC2626")]);
        var ohne = slot is "oberteil" or "hose" ? grund : Figur.Netz(Grafik, null, 1f, []);
        var mit = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck(slot, form, "#DC2626")]);
        Assert.True(mit.AnzahlEcken > ohne.AnzahlEcken || !Farben(mit).SequenceEqual(Farben(ohne)), form + " zeigt sich nicht");
        // gleicher Aufruf, gleiches Netz
        Assert.Equal(Farben(mit), Farben(Figur.Netz(Grafik, null, 1f, [new Figur.Stueck(slot, form, "#DC2626")])));
    }

    /// <summary>Kurze Aermel und kurze Hose zeigen Haut, das Kleid faerbt Rumpf und Beine oben in seiner Farbe.</summary>
    [Fact]
    public void Figur_KurzeAermelUndKleidFaerben()
    {
        var haut = Netz.Farbe(Figur.Haut["hell"]);
        int Hautnah(Netz n) => Farbton(n, haut);
        var lang = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck("oberteil", "shirt", "#DC2626")]);
        var kurz = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck("oberteil", "tshirt", "#DC2626")]);
        Assert.True(Hautnah(kurz) > Hautnah(lang), "T-Shirt kurzarm zeigt keine Arme");
        var hose = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck("hose", "hose", "#1E3A8A")]);
        var kurzeHose = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck("hose", "kurz", "#1E3A8A")]);
        Assert.True(Hautnah(kurzeHose) > Hautnah(hose), "kurze Hose zeigt keine Beine");
        // das Kleid deckt Oberteil und Hose: mit Kleid ist ein getragenes Oberteil nicht mehr zu sehen
        var kleid = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck("kleid", "kleid", "#16A34A")]);
        var kleidUndHemd = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck("oberteil", "shirt", "#DC2626"), new Figur.Stueck("kleid", "kleid", "#16A34A")]);
        Assert.Equal(Farben(kleid), Farben(kleidUndHemd));
    }

    /// <summary>Farben aller Ecken eines Netzes.</summary>
    private static List<System.Numerics.Vector3> Farben(Netz n) => [.. Enumerable.Range(0, n.AnzahlEcken).Select(n.FarbeAn)];

    /// <summary>Ecken mit dem Farbton von <paramref name="farbe"/> (gleiche Richtung, Helligkeit egal: die Vorlage schattiert).</summary>
    private static int Farbton(Netz n, System.Numerics.Vector3 farbe)
    {
        static System.Numerics.Vector3 Richtung(System.Numerics.Vector3 v) => v / MathF.Max(v.Length(), 1e-6f);
        return Farben(n).Count(f => System.Numerics.Vector3.Distance(Richtung(f), Richtung(farbe)) < 0.03f);
    }

    /// <summary>Nico 3a: die Frisur ist auch an der 3D-Figur zu sehen; „kurz“ bleibt die Figur von vorher (Gegenprobe).</summary>
    [Fact]
    public void Figur_FrisurUndBartSichtbar()
    {
        PyObjekt Mit(string teil, string wert)
        {
            var a = Aussehen.Voreinstellung();
            Py.Setzen(a, teil, wert);
            return a;
        }
        var kurz = Figur.Netz(Grafik, Aussehen.Voreinstellung(), 1f);
        Assert.Equal(Farben(kurz), Farben(Figur.Netz(Grafik, null, 1f)));
        foreach (var frisur in new[] { "lang", "zopf", "locken" })
        {
            Assert.True(Figur.Netz(Grafik, Mit("frisur", frisur), 1f).AnzahlEcken > kurz.AnzahlEcken, frisur);
        }
        var glatze = Figur.Netz(Grafik, Mit("frisur", "glatze"), 1f);
        Assert.Equal(kurz.AnzahlEcken, glatze.AnzahlEcken);
        Assert.NotEqual(Farben(kurz), Farben(glatze));
        foreach (var bart in new[] { "vollbart", "schnurrbart" })
        {
            Assert.True(Figur.Netz(Grafik, Mit("bart", bart), 1f).AnzahlEcken > kurz.AnzahlEcken, bart);
        }
        // Locken unter einer Kappe fallen weg (sie wuerden durch die Kappe stechen), lange Haare bleiben
        var kappe = new Figur.Stueck("kopf", "kappe", "#DC2626");
        Assert.Equal(Figur.Netz(Grafik, null, 1f, [kappe]).AnzahlEcken, Figur.Netz(Grafik, Mit("frisur", "locken"), 1f, [kappe]).AnzahlEcken);
        Assert.True(Figur.Netz(Grafik, Mit("frisur", "lang"), 1f, [kappe]).AnzahlEcken > Figur.Netz(Grafik, null, 1f, [kappe]).AnzahlEcken);
    }

    /// <summary>
    /// Der Bart ist ein Teil nur der C#-Linie: die Python-Teile (zufaellige Personen, G-Dateien) bleiben, im Aussehen steht er nur
    /// mit einer Wahl ausser „Keiner“, unbekannte Werte fallen weg.
    /// </summary>
    [Fact]
    public void Aussehen_BartNurWennGewaehlt()
    {
        Assert.Equal(["haut", "frisur", "haarfarbe", "oberteil", "extra", "kreis"], Aussehen.Teile.Select(t => t.Teil));
        Assert.Equal(["haut", "frisur", "haarfarbe", "oberteil", "extra", "kreis"], Aussehen.Voreinstellung().Select(p => p.Key));
        Assert.Equal(["keiner", "vollbart", "schnurrbart"], Aussehen.WahlFuer("bart").Select(w => w.Kennung));
        PyObjekt Mit(object? bart) => new() { { "haut", "hell" }, { "bart", bart } };
        Assert.Equal("vollbart", Aussehen.Normalisieren(Mit("vollbart"))["bart"]);
        Assert.False(Py.Hat(Aussehen.Normalisieren(Mit("keiner")), "bart"));
        Assert.False(Py.Hat(Aussehen.Normalisieren(Mit("ziegenbart")), "bart"));
        Assert.False(Py.Hat(Aussehen.Normalisieren(Mit(1L)), "bart"));
        Assert.Equal("keiner", Aussehen.Wert(Aussehen.Voreinstellung(), "bart"));
        // die Zeile profil_gesetzt traegt den Bart nur, wenn einer gewaehlt ist
        var mit = NeuesSpiel(new PyObjekt { { "haut", "hell" }, { "bart", "schnurrbart" } });
        Assert.Equal("schnurrbart", SpielDaten.Objekt(mit.Stand.Profil!["aussehen"])["bart"]);
        var ohne = NeuesSpiel(new PyObjekt { { "haut", "hell" }, { "bart", "keiner" } });
        Assert.False(Py.Hat(SpielDaten.Objekt(ohne.Stand.Profil!["aussehen"]), "bart"));
        Assert.Empty(mit.Stand.Unterschiede(mit.Neuaufbau()));
    }

    /// <summary>
    /// Jede Frisur, jeder Bart und jedes neue Stueck (auch alles zusammen) bleibt im festen Halbmesser der Figur
    /// (<see cref="StadtSpiel.FigurHalbmesser"/>, Weisung P12-197): beim Ansprechen durchdringen sich zwei Figuren nicht.
    /// Gegenprobe: ein Teil eine Kopfbreite neben dem Kopf liegt ausserhalb.
    /// </summary>
    [Fact]
    public void Figur_AllesImHalbmesser()
    {
        static float Halbmesser(Netz netz)
        {
            var e = netz.Ecken;
            var r = 0f;
            for (var i = 0; i < e.Count / Netz.Zahlen; i++)
            {
                r = MathF.Max(r, new System.Numerics.Vector2(e[i * Netz.Zahlen], e[(i * Netz.Zahlen) + 2]).Length());
            }
            return r * Innenraum.FigurHoehe;
        }
        var stuecke = new Figur.Stueck[]
        {
            new("oberteil", "hemd_lang", "#DC2626"), new("oberteil", "tshirt", "#DC2626"), new("hose", "kurz", "#DC2626"),
            new("kleid", "kleid", "#DC2626"), new("kleid", "rock", "#DC2626"), new("jacke", "jacke", "#DC2626"),
            new("jacke", "mantel", "#DC2626"), new("brille", "brille", "#DC2626"), new("tasche", "handtasche", "#DC2626"),
        };
        var geprueft = 0;
        foreach (var frisur in Aussehen.Teile.First(t => t.Teil == "frisur").Wahl.Select(w => w.Kennung))
        {
            foreach (var bart in Aussehen.WahlFuer("bart").Select(w => w.Kennung))
            {
                var a = new PyObjekt { { "frisur", frisur }, { "bart", bart } };
                foreach (var kleidung in stuecke.Select(s => (Figur.Stueck[])[s]).Append([]).Append(
                    [new("kleid", "kleid", "#DC2626"), new("jacke", "mantel", "#DC2626"), new("brille", "brille", "#DC2626"), new("tasche", "handtasche", "#DC2626")]))
                {
                    var r = Halbmesser(Figur.Netz(Grafik, a, 1f, kleidung));
                    Assert.True(r <= StadtSpiel.FigurHalbmesser, $"{frisur} {bart} {string.Join(",", kleidung.Select(k => k.Form))}: {r}");
                    geprueft++;
                }
            }
        }
        Assert.True(geprueft >= 5 * 3 * 11, "geprueft: " + geprueft);
        // Gegenprobe: ein Teil eine Kopfbreite neben dem Kopf
        var kopf = Figur.Kopf(Grafik.Modell(Figur.Modell)!)!.Value;
        var breit = kopf.Max.X - kopf.Min.X;
        var zuWeit = Figur.Frisur(kopf.Min + new System.Numerics.Vector3(breit, 0, 0), kopf.Max + new System.Numerics.Vector3(breit, 0, 0), "lang", System.Numerics.Vector3.One, null);
        var gm = Grafik.Modell(Figur.Modell)!;
        var s1 = 1f / (gm.Max.Y - gm.Min.Y);
        var mitte = (gm.Min + gm.Max) / 2;
        Assert.True(zuWeit.Max(t => MathF.Abs(t.X2 - mitte.X) * s1 * Innenraum.FigurHoehe) > StadtSpiel.FigurHalbmesser);
    }

    /// <summary>Der 2D-Avatar zeigt den Bart; ohne Bart sind die Formen wie in Python (Gegenprobe).</summary>
    [Fact]
    public void Avatar_BartNurMitBart()
    {
        var ohne = SpielBilder.Avatar(Aussehen.Voreinstellung());
        var keiner = Aussehen.Voreinstellung();
        keiner.Add("bart", "keiner");
        Assert.Equal(ohne, SpielBilder.Avatar(keiner));
        var voll = Aussehen.Voreinstellung();
        voll.Add("bart", "vollbart");
        Assert.Equal(ohne.Count + 2, SpielBilder.Avatar(voll).Count);
        var schnurr = Aussehen.Voreinstellung();
        schnurr.Add("bart", "schnurrbart");
        Assert.Equal(ohne.Count + 1, SpielBilder.Avatar(schnurr).Count);
    }
}
