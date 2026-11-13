using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.52.0 (Weisung P12-231 Punkt 5, Kurzplan P12-131 Punkt 5): „Alle beheben, die sicher gehen“. Nicos Ziel: ein
/// Knopf behebt alle roten Funde, fuer die es einen sicheren Vorschlag gibt (Laterne versetzen, Tuer auf die naechste glatte
/// Stelle, Weg neu an die Strasse), nur wenn kein neuer roter Fund entsteht, alles in EINEM Schritt, danach „n behoben, m
/// bleiben“. Gegenproben: ohne Nachpruefung entstuende ein neuer roter Fund; ein veraltetes Ergebnis legt keinen Schritt an.
/// </summary>
public class AlleBehebenTests
{
    private static Spieldaten D => Daten.Spiel;

    internal static AlleBeheben.Ergebnis Suchen(Projekt p) =>
        AlleBeheben.Suchen((PyObjekt)Py.Kopie(p.AussenStand)!, p.Basis, p.Innenraeume(), D, p.SzeneOhneLaternen, p.LaternenFuerVorschau(),
            (a, s) => TuerFassade.Rechnen(p, a, s), TestContext.Current.CancellationToken);

    internal static List<StadtbauFund> Rot(Projekt p) => [.. p.PruefenAussen().Where(f => f.Rot)];

    /// <summary>Stadt 1 (Fassung 0) mit Laterne 20 auf der Fahrbahn und dem Weg des Amts kurz vor der Strasse abgeschnitten.</summary>
    private static Projekt ZweiFunde()
    {
        var p = Projekt.Neu("Alle beheben", D, Daten.NeueAblage());
        p.AussenAendern("Weg kaputt", a =>
        {
            var sd = Aussen.Stadtdaten(a);
            if (sd["zugaenge_neu"] is not PyObjekt neu)
            {
                neu = [];
                Py.Setzen(sd, "zugaenge_neu", neu);
            }
            Py.Setzen(neu, "amt", new PyObjekt
            {
                { "tuer", new List<object?> { 10.6, 13.2 } },
                { "weg", new List<object?> { new List<object?> { 10.6, 13.2 }, new List<object?> { 10.6, 12.95 } } },
            });
        });
        return p;
    }

    [Fact]
    public void LaterneUndWeg_EinSchritt_DanachOhneDieseFunde_RestBleibt()
    {
        var p = ZweiFunde();
        var vorher = Rot(p);
        Assert.Contains(vorher, f => f.Text == "Der Fußweg von „Amt“ erreicht keine Straße.");
        Assert.Contains(vorher, f => LaterneVersetzen.Nummer(p.AussenStand, f) == 19);
        Assert.Equal(AlleBeheben.Art.Weg, AlleBeheben.ArtVon(p.AussenStand, vorher.First(f => f.Text.Contains("Amt", StringComparison.Ordinal))));

        var e = Suchen(p);
        Assert.Equal(2, e.Behoben);
        Assert.Contains(e.Behebungen, b => b.Art == AlleBeheben.Art.Laterne);
        Assert.Contains(e.Behebungen, b => b.Art == AlleBeheben.Art.Weg);

        var schritte = p.SchritteZurueck;
        Assert.True(p.AussenAendern(Texte.AlleBehebenSchritt, a => AlleBeheben.Anwenden(a, e)));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        var nachher = Rot(p);
        Assert.DoesNotContain(nachher, f => f.Text.Contains("„Amt“ erreicht keine Straße", StringComparison.Ordinal));
        Assert.DoesNotContain(nachher, f => LaterneVersetzen.Nummer(p.AussenStand, f) is not null);
        Assert.Equal(e.Bleiben, nachher.Count);
        Assert.Null(Bauen3D.NeuRot(vorher, nachher));

        // einmal rueckgaengig = beide Funde wieder da
        Assert.True(p.Rueckgaengig());
        Assert.Equal(vorher.Select(f => f.Text).Order(), Rot(p).Select(f => f.Text).Order());

        // veraltet (die Stadt hat sich seit der Suche geaendert): kein Schritt
        Assert.False(p.AussenAendern(Texte.AlleBehebenSchritt, a => AlleBeheben.Anwenden(a, Suchen(ZweiFunde()) with { Stand = [] })));
        Assert.Equal(schritte, p.SchritteZurueck);
    }

    [Fact]
    public void OhneSichereBehebung_NichtsGeaendert()
    {
        // Fassung 0 mit freier Laterne 20: kein roter Fund, also nichts zu tun, kein Schritt
        var p = Projekt.Neu("Gruen", D, Daten.NeueAblage());
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        var e = Suchen(p);
        Assert.Equal(0, e.Behoben);
        var schritte = p.SchritteZurueck;
        Assert.False(p.AussenAendern(Texte.AlleBehebenSchritt, a => AlleBeheben.Anwenden(a, e)));
        Assert.Equal(schritte, p.SchritteZurueck);
    }

    [Fact]
    public void WegNeu_Gegenprobe_OhneStrasse_KeineBehebung()
    {
        // Weg eines Orts ohne erreichbare Strasse: WegNeu findet keine, die Behebung gilt nicht (der Fund bleibt rot)
        var p = Projekt.Neu("Ohne Strasse", D, Daten.NeueAblage(), null, (30, 20));
        Assert.False(AlleBeheben.WegNeu(p.AussenStand, new AussenStelle(AussenArt.Haus, -1, 0, "gibtesnicht")));
    }

    /// <summary>
    /// Nie ein neuer roter Fund: mit einer Stoerung, die jede Behebung zusaetzlich eine Laterne auf die Fahrbahn stellen laesst,
    /// wird keine uebernommen. Gegenprobe: ohne Nachpruefung kaemen beide durch und die Stadt haette neue rote Funde.
    /// </summary>
    [Fact]
    public void NieNeuerRoterFund_GegenprobeOhneNachpruefung()
    {
        static void Laterne(PyObjekt a)
        {
            var m = AussenBearbeitung.Strassen(a).First(l => l.Count >= 2);
            var q = (m[0] + m[1]) / 2;
            Aussen.Laternen(a).Add(new List<object?> { Math.Round((double)q.X, 2), Math.Round((double)q.Y, 2), false });
        }
        var p = ZweiFunde();
        var vorher = Rot(p);
        AlleBeheben.StoerungFuerTests.Value = Laterne;
        try
        {
            Assert.Equal(0, Suchen(p).Behoben);
            AlleBeheben.OhneNachpruefungFuerTests = true;
            var e = Suchen(p);
            Assert.Equal(2, e.Behoben);
            // die zwei behobenen Funde sind durch zwei neue rote (Laternen auf der Fahrbahn) ersetzt
            Assert.Equal(vorher.Count, e.Bleiben);
        }
        finally
        {
            AlleBeheben.StoerungFuerTests.Value = null;
            AlleBeheben.OhneNachpruefungFuerTests = false;
        }
    }
}
