using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;

namespace Stadtbau.Tests;

/// <summary>
/// Weisung P12-212 Punkt 5 (= a): das Werkzeug zeigt die Laufzeiten zwischen den Orten genau wie das Spiel sie rechnet
/// (2 Spielminuten je Schritt) und warnt gelb ab 60 Minuten; nie rot.
/// </summary>
public class LaufzeitenTests
{
    private static Spieldaten D => Daten.Spiel;

    [Fact]
    public void Stadt1_GleichDemSpiel_JedesPaarEinmal_LaengsteZuerst()
    {
        var p = Projekt.Neu("Laufzeiten", D, Daten.NeueAblage());
        var st = p.Stand();
        var wege = Laufzeiten.Rechnen(st, D.Inhalt);
        var orte = Spielort.Stadt1Orte(D.Fassung).Count;
        Assert.Equal(orte * (orte - 1) / 2, wege.Count);
        Assert.Equal(wege.Count, wege.Select(w => (w.Von, w.Nach)).Distinct().Count());
        for (var i = 1; i < wege.Count; i++)
        {
            Assert.True((wege[i - 1].Minuten ?? long.MaxValue) >= (wege[i].Minuten ?? long.MaxValue));
        }
        // wie das Spiel: Schritte der Breitensuche mal 2 Minuten
        var stadt = (Stadt)SpielKern.Regeln.Stadt(st, st.MitKarte(D.Inhalt), nachsichtig: true)!;
        Assert.Equal(2, stadt.MinutenJeSchritt);
        foreach (var w in wege)
        {
            Assert.NotNull(w.Minuten);
            Assert.Equal(stadt.Weg(w.Von, w.Nach)!.Schritte * 2, w.Minuten);
        }
    }

    [Fact]
    public void Stadt1_EinGelberHinweis_ZahlUndLaengsterWeg_NieRot()
    {
        var p = Projekt.Neu("Laufzeiten", D, Daten.NeueAblage());
        var wege = Laufzeiten.Rechnen(p.Stand(), D.Inhalt);
        var lang = wege.Where(w => w.Minuten >= Laufzeiten.GelbAb).ToList();
        Assert.NotEmpty(lang);
        var funde = p.PruefenAussen().Where(f => f.Text.Contains("Spielminuten oder länger", StringComparison.Ordinal)).ToList();
        var f = Assert.Single(funde);
        Assert.False(f.Rot);
        Assert.Equal(Texte.LaufzeitLang(lang.Count, lang[0].VonName, lang[0].NachName, lang[0].Minuten!.Value), f.Text);
        Assert.NotNull(f.X);
        // genau ab 60: ein Weg mit 58 Minuten zaehlt nicht, einer mit 60 schon
        Assert.DoesNotContain(wege.Where(w => w.Minuten < 60), w => lang.Contains(w));
        Assert.All(wege.Where(w => w.Minuten == 60), w => Assert.Contains(w, lang));
    }

    [Fact]
    public void Gegenprobe_AlleUnterSechzig_KeinHinweis()
    {
        // Nicos Teststadt hat keine Spielorte: keine Wege, kein Hinweis
        var p = TorEingangTests.Nico();
        Assert.Empty(Laufzeiten.Pruefen(p.Stand(), D.Inhalt, p.AussenStand));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Text.Contains("Spielminuten", StringComparison.Ordinal));
    }

    [Fact]
    public void Grenze_GenauSechzig()
    {
        Assert.Equal(60, Laufzeiten.GelbAb);
    }

    [Fact]
    public void WenigerAlsZweiOrte_Leer()
    {
        var p = TorEingangTests.Nico();
        Assert.Empty(Laufzeiten.Rechnen(p.Stand(), D.Inhalt));
        Assert.Null(SpielortTests.Festlegen(p, "wohnung", "zuhause"));
        Assert.Empty(Laufzeiten.Rechnen(p.Stand(), D.Inhalt));
    }
}
