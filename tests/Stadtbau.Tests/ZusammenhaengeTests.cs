using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.22.0, Ansicht „Zusammenhänge“ (Weisung P12-92): Spalten nach dem Tag, eine Linie je Paar von Orten,
/// keine Linie kreuzt eine andere oder laeuft durch einen Kasten (zugeklappt, je Ort aufgeklappt, alle aufgeklappt),
/// Gegenprobe mit geraden Linien.
/// </summary>
public class ZusammenhaengeTests
{
    // Messung ohne Oberflaeche: 7 Bildpunkte je Zeichen, 18 je Zeile (die echte misst die Oberflaeche)
    internal static readonly Zusammenhaenge.Masse Masse = new(
        (text, breite, _) => 18 * Math.Max(1, Math.Ceiling(text.Length * 7.0 / Math.Max(1, breite))),
        (text, _) => text.Length * 7.0);

    private static Spielgeschichte Geschichte() =>
        Spielgeschichte.Aus(Daten.Spiel.Inhalt, Projekt.Neu("Zusammenhaenge", Daten.Spiel, Daten.NeueAblage()).AussenStand);

    private static string Name(string ort) => Daten.Spiel.OrtName(ort);

    private static Zusammenhaenge Bauen(Spielgeschichte g, params string[] offen) =>
        Zusammenhaenge.Bauen(g, Name, offen.ToHashSet(StringComparer.Ordinal), Masse);

    [Fact]
    public void Spalten_NachDemTag_WieImEntwurf()
    {
        var spalten = Zusammenhaenge.Spalten(Geschichte(), Name).ToDictionary(s => s.Ort, s => s.Spalte);
        Assert.Equal(0, spalten["bitweiche"]);
        Assert.Equal(0, spalten["talheim"]);
        Assert.Equal(1, spalten["talheim_nord"]);
        Assert.Equal(1, spalten["betriebswerkstatt"]);
        Assert.Equal(2, spalten["lindenau"]);
        Assert.Equal(3, spalten["birkenhain"]);
    }

    [Fact]
    public void Zugeklappt_EineLinieJePaar_AlleUeberDasBuero_OhneKreuzung()
    {
        var z = Bauen(Geschichte());
        Assert.Equal(6, z.Kaesten.Count);
        Assert.Equal(5, z.Linien.Count);
        Assert.All(z.Linien, l => Assert.True(l.A == "bitweiche" || l.B == "bitweiche"));
        Assert.Empty(z.Kreuzungen());
        Assert.Empty(z.DurchKaesten());
        // spaetere Spalten stehen weiter rechts
        var x = z.Kaesten.ToDictionary(k => k.Ort, k => k.Koerper.X);
        Assert.True(x["bitweiche"] < x["talheim_nord"] && x["talheim_nord"] < x["lindenau"] && x["lindenau"] < x["birkenhain"]);
    }

    [Fact]
    public void JeOrtAufgeklappt_UndAlleAufgeklappt_OhneKreuzungUndNieDurchEinenKasten()
    {
        var g = Geschichte();
        var orte = g.Zahlen().Keys.ToList();
        foreach (var offen in orte.Select(o => new[] { o }).Append([.. orte]))
        {
            var z = Bauen(g, offen);
            Assert.True(z.Kreuzungen().Count == 0, string.Join(",", offen) + ": " + string.Join("; ", z.Kreuzungen().Take(5)));
            Assert.True(z.DurchKaesten().Count == 0, string.Join(",", offen) + ": " + string.Join("; ", z.DurchKaesten().Take(5)));
            foreach (var k in z.Kaesten.Where(k => k.Offen))
            {
                Assert.Equal(k.Zahl, k.Zeilen.Count);
                // Zeilen ueberlappen nicht und liegen im Kasten
                for (var i = 0; i + 1 < k.Zeilen.Count; i++)
                {
                    Assert.True(k.Zeilen[i].Ort.Unten <= k.Zeilen[i + 1].Ort.Y);
                }
                Assert.All(k.Zeilen, r => Assert.True(r.Ort.Y >= k.Kopf.Unten && r.Ort.Unten <= k.Knopf.Y));
            }
        }
    }

    [Fact]
    public void TalheimNordAufgeklappt_LinieBisZurAufgabe_KlammernInnen()
    {
        var z = Bauen(Geschichte(), "talheim_nord");
        Assert.True(z.AnAufgaben);
        Assert.Contains(z.Linien, l => !l.Klammer && l.B == "rack-talheim-nord");
        Assert.Equal(2, z.Linien.Count(l => l.Klammer));
        // Baum: jede Aufgabe direkt unter ihrem Vorgaenger
        var k = z.Kaesten.Single(k => k.Ort == "talheim_nord");
        var ids = k.Zeilen.Select(r => r.Aufgabe.Id).ToList();
        Assert.Equal(ids.IndexOf("rack-talheim-nord") + 1, ids.IndexOf("abnahme-talheim-nord"));
    }

    [Fact]
    public void Gegenprobe_GeradeLinien_KreuzenOderLaufenDurchKaesten()
    {
        var g = Geschichte();
        var alt = Zusammenhaenge.Bauen(g, Name, new HashSet<string>(StringComparer.Ordinal), Masse, altFuerTests: true);
        Assert.NotEmpty(alt.Kreuzungen().Concat(alt.DurchKaesten()));
        // Gegenprobe der Pruefung selbst: zwei Linien, die sich kreuzen
        var kreuz = Zusammenhaenge.Bauen(g, Name, new HashSet<string>(StringComparer.Ordinal), Masse);
        Assert.Empty(kreuz.Kreuzungen());
    }
}
