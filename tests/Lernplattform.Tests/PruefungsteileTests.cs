using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.15.0 (Weisungen P12-406/P12-407, Variante B): Abnahmesaetze der Navigation nach Pruefungsteilen.
/// Namen der Teile wie auf dem Deckblatt der IHK je Fachrichtung (feste Tabelle, nicht aus dem Code), jede bisherige Seite in
/// hoechstens zwei Klicks (Liste alt -> neu), Fragen und Szenarien je Teil nur aus vorhandenen Zuordnungen, Stand je Teil.
/// Gegenproben: falscher Name, fehlende Seite, falsche Zuordnung machen den jeweiligen Test rot.
/// </summary>
public class PruefungsteileTests
{
    private static readonly Lazy<Lerninhalte> Alle = new(() => Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte),
        new LernenOrdner(n => Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen", n + ".json") is var pfad && File.Exists(pfad) ? File.ReadAllBytes(pfad) : null),
        null));

    /// <summary>Deckblatt der IHK (FIAusbV 2020): Teil 1, Bereich 1, Bereich 2, WiSo je Fachrichtung.</summary>
    public static TheoryData<string, string, string, string, string> Deckblatt => new()
    {
        { "si", "Einrichten eines IT-gestützten Arbeitsplatzes", "Konzeption und Administration von IT-Systemen", "Analyse und Entwicklung von Netzwerken", "Wirtschafts- und Sozialkunde" },
        { "ae", "Einrichten eines IT-gestützten Arbeitsplatzes", "Planen eines Softwareproduktes", "Entwicklung und Umsetzung von Algorithmen", "Wirtschafts- und Sozialkunde" },
        { "dpa", "Einrichten eines IT-gestützten Arbeitsplatzes", "Durchführen einer Prozessanalyse", "Sicherstellen der Datenqualität", "Wirtschafts- und Sozialkunde" },
        { "dv", "Einrichten eines IT-gestützten Arbeitsplatzes", "Diagnose und Störungsbeseitigung in vernetzten Systemen", "Betrieb und Erweiterung von vernetzten Systemen", "Wirtschafts- und Sozialkunde" },
    };

    [Theory]
    [MemberData(nameof(Deckblatt))]
    public void NamenWieDasDeckblatt(string fr, string ap1, string b1, string b2, string wiso)
    {
        var f = Fachrichtung.Nach(fr);
        Assert.Equal(fr, f.Id);
        Assert.Equal([ap1, b1, b2, wiso], new[] { "ap1", "b1", "b2", "wiso" }.Select(r => Pruefungsteile.Deckblattname(f, r)));
        var leiste = Pruefungsteile.Seitenleiste(f);
        Assert.Equal("ABSCHLUSSPRÜFUNG " + FachrichtungTexte.KuerzelFuer(fr), leiste.Single(z => z.Art == LeistenArt.Gruppe && z.Titel.StartsWith("ABSCHLUSS", StringComparison.Ordinal)).Titel);
        // Seitenleiste: nur FISI Bereich 1 kuerzer wie im Entwurfsbild, sonst der Name vom Deckblatt
        Assert.Equal(fr == "si" ? "Konzeption und Administration" : b1, leiste.Single(z => z.Id == "b1").Titel);
        Assert.Equal(b2, leiste.Single(z => z.Id == "b2").Titel);
        Assert.Equal(wiso, leiste.Single(z => z.Id == "wiso").Titel);
        // Brotkrumen am PC mit dem vollen Namen, am Handy kurz
        Assert.Equal(Beschriftungen.Gross(b1), Pruefungsteile.Kopf("b1", f, true)!.Seite);
        Assert.Equal("BEREICH 1", Pruefungsteile.Kopf("b1", f, false)!.Seite);
    }

    /// <summary>Alle bisherigen Seiten (Seitenleiste vor A 1.15.0 und Sammelseite „Lernen“ am Handy) und wie man sie erreicht.</summary>
    public static readonly IReadOnlyList<string> BisherigeSeiten =
        ["dashboard", "cards", "quiz", "ap1scenarios", "scenarios", "testproject", "abschluss", "notebook", "calc", "game", "progress", "help", "settings", "learn"];

    /// <summary>Wie viele Klicks bis zu einer Seite (Klick 1 = Eintrag der Leiste, danach Knoepfe der Seite, <see cref="Pruefungsteile.Ziele"/>).</summary>
    private static int Klicks(IReadOnlyList<string> leiste, string seite, Func<string, bool> mitSzenarien)
    {
        var stufe = leiste.Distinct().ToList();
        var gesehen = new HashSet<string>(stufe, StringComparer.Ordinal);
        for (var klicks = 1; klicks <= 4; klicks++)
        {
            if (stufe.Contains(seite))
            {
                return klicks;
            }
            stufe = [.. stufe.SelectMany(s => Pruefungsteile.Ziele(s, mitSzenarien(s))).Where(gesehen.Add)];
        }
        return int.MaxValue;
    }

    [Theory]
    [InlineData("si")]
    [InlineData("ae")]
    [InlineData("dpa")]
    [InlineData("dv")]
    public void JedeBisherigeSeiteInHoechstensZweiKlicks(string fr)
    {
        var f = Fachrichtung.Nach(fr);
        var sicht = Alle.Value.Fuer(fr);
        var klausur = new KlausurInhalte(sicht);
        var gewicht = new RahmenplanGewicht(sicht);
        // A 1.19.0: AP2 zeigt die Szenarien je Bereich (Bereich 1, Bereich 2, WiSo); die Zeile gibt es, wenn ein Bereich welche hat
        bool MitSzenarien(string seite) => (seite == Pruefungsteile.Pruefung ? Pruefungsteile.Teil1 : seite) switch
        {
            Pruefungsteile.Teil1 => TeilUeben.Inhalte(sicht, gewicht, klausur, f, "ap1").Szenarien.Count > 0,
            Pruefungsteile.Teil2 or Pruefungsteile.B1 or Pruefungsteile.B2 or Pruefungsteile.Wiso => new[] { Pruefungsteile.B1, Pruefungsteile.B2, Pruefungsteile.Wiso }
                .Any(b => TeilUeben.Inhalte(sicht, gewicht, klausur, f, b).Szenarien.Count > 0),
            _ => false,
        };
        var pc = Pruefungsteile.Seitenleiste(f).Concat(Pruefungsteile.SeitenleisteUnten).Where(z => z.Id is not null).Select(z => z.Id!).ToList();
        var handy = Navigation.Handy.Select(h => h.Id).ToList();
        var funde = new List<string>();
        foreach (var (geraet, leiste) in new[] { ("PC", pc), ("Handy", handy) })
        {
            foreach (var seite in BisherigeSeiten.Where(s => geraet == "Handy" || s != "learn"))
            {
                // Weisung P12-432 (Nico 2b): ueberall wieder hoechstens zwei Klicks (Testprojekte und Mein Projekt auch unter „Mehr“).
                // Ausnahme A 1.19.0 (Weisung P12-440, Nico 1a, Anlage P12-278 Zeile „AP2-Szenarien fällt weg“): die ganze Liste
                // AP2-Szenarien unter „Mehr“ entfaellt; am Handy sind sie Prüfung, AP2, Szenarien = drei Klicks, am PC zwei (Bereich-Unterpunkt)
                var grenze = geraet == "Handy" && seite == "scenarios" ? 3 : 2;
                var ist = Klicks(leiste, seite, MitSzenarien);
                if (ist > grenze)
                {
                    funde.Add(geraet + ": " + seite + " in " + ist + " statt hoechstens " + grenze + " Klicks");
                }
            }
        }
        Assert.Empty(funde);
    }

    [Fact]
    public void PruefungZeigtAp1UndAp2PerSegment()
    {
        // Gegenprobe: ohne Segment (AP2 nicht von AP1 aus erreichbar) waeren die AP2-Szenarien ueber „Prüfung“ nicht erreichbar;
        // mit Segment drei Klicks (Prüfung, AP2, Szenarien); Testprojekte und Mein Projekt ueber „Mehr“ zwei (Weisung P12-432)
        Assert.Contains(Pruefungsteile.Teil2, Pruefungsteile.Ziele(Pruefungsteile.Pruefung));
        Assert.Contains(Pruefungsteile.Teil1, Pruefungsteile.Ziele(Pruefungsteile.Teil2));
        // A 1.19.0 (Nico 2a, 3a): die Projekt-Karte unter AP2 ist nur ein Hinweis ohne Knoepfe; Testprojekte und Mein Projekt nur unter „Mehr“
        Assert.Equal(int.MaxValue, Klicks([Pruefungsteile.Pruefung], "abschluss", _ => true));
        Assert.Equal(int.MaxValue, Klicks([Pruefungsteile.Pruefung], "testproject", _ => true));
        // AP2-Szenarien: Prüfung, AP2, Szenarien (eingebettet, die eigene Seite unter „Mehr“ faellt weg)
        Assert.Equal(3, Klicks([Pruefungsteile.Pruefung], "scenarios", _ => true));
        Assert.Equal(int.MaxValue, Klicks([Pruefungsteile.Mehr], "scenarios", _ => true));
        Assert.Equal(2, Klicks([Pruefungsteile.Pruefung], "quiz", _ => true));
        Assert.Equal(int.MaxValue, Klicks([Pruefungsteile.Pruefung], "settings", _ => true));
        Assert.Equal(2, Klicks([Pruefungsteile.Mehr], "abschluss", _ => true));
        Assert.Equal(2, Klicks([Pruefungsteile.Mehr], "testproject", _ => true));
    }

    [Fact]
    public void GegenprobeFehlendeSeiteWirdGefunden()
    {
        // ohne „Mehr“ in der unteren Leiste waeren Fortschritt, Optionen und Hilfe am Handy nicht in zwei Klicks erreichbar
        var handy = Navigation.Handy.Select(h => h.Id).Where(id => id != Pruefungsteile.Mehr).ToList();
        Assert.DoesNotContain(handy, l => Pruefungsteile.Ziele(l).Contains("progress"));
        Assert.Contains("progress", Pruefungsteile.Ziele(Pruefungsteile.Mehr));
    }

    [Fact]
    public void FragenUndSzenarienNurAusVorhandenenZuordnungen()
    {
        var sicht = Alle.Value.Fuer("si");
        var f = Fachrichtung.Nach("si");
        var klausur = new KlausurInhalte(sicht);
        var gewicht = new RahmenplanGewicht(sicht);
        foreach (var rolle in new[] { "ap1", "b1", "b2", "wiso" })
        {
            var teil = TeilUeben.Inhalte(sicht, gewicht, klausur, f, rolle);
            Assert.NotEmpty(teil.Fragen);
            Assert.Equal(teil.Fragen.Count, teil.Fragen.Distinct().Count());
            var punkte = TeilUeben.PunkteFuer(sicht, f.Bereich(rolle));
            var art = klausur.Art(f.Bereich(rolle).Art);
            foreach (var frage in teil.Fragen)
            {
                var eintrag = sicht.Quizfragen.First(q => q.Frage == frage);
                var wiso = art?.QuizThemen is { } themen && eintrag.Thema is { } t && themen.Contains(t);
                Assert.True(wiso || gewicht.Punkte(Quellen.Quiz, frage).Any(punkte.Contains), rolle + ": " + frage);
            }
            // Szenarien: AP1 alle AP1-Szenarien der Sicht (A 1.19.0, auch die zur WiSo, aus denen die Probeklausur AP1 nicht zieht),
            // AP2 genau die, aus denen die Probeklausur des Bereichs zieht
            if (rolle == "ap1")
            {
                Assert.Equal(sicht.Ap1Szenarien, teil.Szenarien);
            }
            else if (rolle != "wiso")
            {
                Assert.NotEmpty(teil.Szenarien);
                Assert.All(teil.Szenarien, s => Assert.Contains(s.SzenarioThema, art!.Themen?.SelectMany(w => w) ?? art.Pool ?? []));
            }
            else
            {
                Assert.Empty(teil.Szenarien);
            }
        }
        // Teil 1 zieht aus den AP1-Szenarien, Teil 2 aus den AP2-Szenarien
        Assert.Equal(Quellen.Ap1, TeilUeben.Inhalte(sicht, gewicht, klausur, f, "ap1").SzenarienQuelle);
        Assert.Equal(Quellen.Ap2, TeilUeben.Inhalte(sicht, gewicht, klausur, f, "b1").SzenarienQuelle);
    }

    /// <summary>
    /// A 1.19.0 (Weisung P12-440, Fund 1 von Chat): die Szenarien je Bereich ergeben zusammen genau alle AP2-Szenarien der Sicht
    /// (keine Aufgabe geht verloren); FISI hat 46 Szenarien in Bereich 1 und Bereich 2 (beide Probeklausuren ziehen aus ihnen),
    /// die anderen Fachrichtungen keine. Der Lernstand zaehlt je Szenario-Nummer, also jedes nur einmal. AP1 zeigt alle AP1-Szenarien.
    /// </summary>
    [Theory]
    [InlineData("si", 241, 135, 46)]
    [InlineData("ae", 165, 165, 0)]
    [InlineData("dpa", 165, 165, 0)]
    [InlineData("dv", 165, 165, 0)]
    public void SzenarienJeBereichOhneVerlust(string fr, int b1, int b2, int beide)
    {
        var sicht = Alle.Value.Fuer(fr);
        var f = Fachrichtung.Nach(fr);
        var klausur = new KlausurInhalte(sicht);
        var gewicht = new RahmenplanGewicht(sicht);
        List<int> Nummern(string rolle) => [.. TeilUeben.Inhalte(sicht, gewicht, klausur, f, rolle).Szenarien.Select(s => s.Nummer)];
        var eins = Nummern("b1");
        var zwei = Nummern("b2");
        Assert.Equal((b1, b2, beide), (eins.Count, zwei.Count, eins.Intersect(zwei).Count()));
        Assert.Empty(Nummern("wiso"));
        Assert.Equal(sicht.Ap2Szenarien.Select(s => s.Nummer).Order(), eins.Union(zwei).Order());
        Assert.Equal(sicht.Ap1Szenarien.Select(s => s.Nummer).Order(), Nummern("ap1").Order());
        // Stand: ein bearbeitetes Szenario aus beiden Bereichen zaehlt im Lernstand einmal (Nummer), in jeder Bereich-Zeile einmal
        Assert.Equal(eins.Count + zwei.Count - beide, sicht.Ap2Szenarien.Count);
    }

    [Fact]
    public void GegenprobeOhneZuordnungKeineFragen()
    {
        // ohne Rahmenplan-Zuordnung bleiben nur die WiSo-Fragen der Probeklausur (Themen recht und arbeitswelt)
        var sicht = Alle.Value.Fuer("si");
        var f = Fachrichtung.Nach("si");
        var klausur = new KlausurInhalte(sicht);
        Assert.Empty(TeilUeben.Inhalte(sicht, null, klausur, f, "b1").Fragen);
        Assert.NotEmpty(TeilUeben.Inhalte(sicht, null, klausur, f, "wiso").Fragen);
    }

    [Fact]
    public void StandZaehltFragenUndSzenarien()
    {
        var teil = new TeilInhalte(["a", "b", "c"], Quellen.Ap2, []);
        var leer = TeilUeben.Stand(teil, new StatusBuch(new Antwortbuch()));
        Assert.Equal(new TeilStand(3, 0, 0, 0, 0), leer);
    }
}
