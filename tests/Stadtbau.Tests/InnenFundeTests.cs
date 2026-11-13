using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.49.0 (Weisung P12-231 Antwort 2c, Sammelliste 328): die Funde aller Innenraeume ohne offenen Raum. Die Liste
/// „Innenräume – m rot“ zaehlt genau die Funde, die Speichern und Senden sperren (gleicher Weg wie <see cref="Projekt.RoteFunde"/>),
/// die Rechnung liest nur Kopien (Aenderungen danach wirken nicht), und je Raum kommt dasselbe heraus wie die Pruefung des
/// offenen Raums.
/// </summary>
public class InnenFundeTests
{
    private static Spieldaten D => Daten.Spiel;

    [Fact]
    public void UnveraenderterRaum_SperrtNicht_GeaendertMitFund216_AchtRot()
    {
        var p = Projekt.Neu("Innen", D, Daten.NeueAblage(), "elektromarkt");
        Assert.Empty(InnenFunde.Schluessel(p));
        Assert.Empty(InnenFunde.Rechnen(InnenFunde.Vorbereiten(p)));

        p.Aendern("elektromarkt", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich"));
        var orte = InnenFunde.Rechnen(InnenFunde.Vorbereiten(p));
        var o = Assert.Single(orte);
        Assert.Equal("elektromarkt", o.Schluessel);
        Assert.Equal("Elektromarkt", o.Name);
        Assert.Equal(8, o.Rot);
        Assert.Equal(8, p.RoteFunde().Count(r => r.Ort == "elektromarkt"));
        Assert.Single(InnenFunde.NurRot(orte));
    }

    [Fact]
    public void JeRaum_GleichDerPruefungDesOffenenRaums()
    {
        var p = Projekt.Neu("Gleich", D, Daten.NeueAblage(), "elektromarkt");
        p.Aendern("elektromarkt", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich"));
        p.Aendern("bank", "Boden", g => Bearbeitung.Boden(g, "flur", "beton"));
        var orte = InnenFunde.Rechnen(InnenFunde.Vorbereiten(p));
        Assert.Equal(["elektromarkt", "bank"], orte.Select(x => x.Schluessel).Order().Reverse());
        foreach (var o in orte)
        {
            // Rechnung bis S 0.48.0 (Projekt.PruefenMitGrenzen auf dem gelesenen Stand), hier ausgeschrieben
            var (basis, etage) = Etagen.Teilen(o.Schluessel);
            var (alt, _) = KatalogPruefung.Innen(Katalog.Aktiv, basis, p.Grundriss(o.Schluessel), p.Personen(o.Schluessel), D.Grafik, D.Geschaefte, p.Basis.Innenraeume, etage);
            alt.AddRange(p.EtagenEntwurfFunde(basis).Where(f => f.Etage == etage).Select(f => f.Fund));
            Assert.Equal(alt.Select(f => (f.Text, f.Rot)), o.Funde.Select(f => (f.Text, f.Rot)));
            Assert.Equal(alt.Select(f => (f.Text, f.Rot)), p.PruefenMitGrenzen(o.Schluessel).Funde.Select(f => (f.Text, f.Rot)));
        }
    }

    [Fact]
    public void Rechnung_LiestNurKopien_AenderungDanachWirktNicht()
    {
        var p = Projekt.Neu("Kopie", D, Daten.NeueAblage(), "elektromarkt");
        p.Aendern("elektromarkt", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich"));
        var auftrag = InnenFunde.Vorbereiten(p);
        // danach alle Waren des Raums weg: die Pruefung des Auftrags bleibt bei 8 roten Funden
        p.Aendern("elektromarkt", "Leeren", g =>
        {
            foreach (var s in Bearbeitung.Alle(g).Where(s => Py.Hat(s.Deko, "ware")).Select(s => s.Stelle).Reverse().ToList())
            {
                Bearbeitung.Loeschen(g, s);
            }
        });
        Assert.Equal(8, Assert.Single(InnenFunde.Rechnen(auftrag)).Rot);
        Assert.NotEqual(8, Assert.Single(InnenFunde.Rechnen(InnenFunde.Vorbereiten(p))).Rot);
    }

    [Fact]
    public void Titel_NennenDieZahl()
    {
        Assert.Equal("Stadt – 3 rot", Texte.FundeStadtTitel(3));
        Assert.Equal("Innenräume – alles in Ordnung", Texte.FundeInnenTitel(0));
    }
}
