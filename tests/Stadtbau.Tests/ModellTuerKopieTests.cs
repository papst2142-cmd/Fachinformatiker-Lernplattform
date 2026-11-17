using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Sammelliste 367 (Weisung P12-291 Teil B): ein Spielort, dessen eigene Modelltuer die Tuer ist (<c>darstellung.modell_tuer</c>,
/// z. B. Betriebswerkstatt und Stellwerk Talheim-Nord), hat nach dem Einfuegen aus der Zwischenablage und nach „Stadtteil
/// einsetzen“ eine gleich gueltige Tuer wie im Original (kein roter Tuer-Fund), als Spielort und als Gebaeude ohne Ort.
/// </summary>
public class ModellTuerKopieTests
{
    private static Spieldaten D => Daten.Spiel;

    public static readonly TheoryData<string> OrteMitModelltuer = ["betriebswerkstatt", "talheim_nord", "moebelhaus", "elektromarkt"];

    private static List<string> TuerRote(Projekt p) =>
        [.. p.PruefenAussen().Where(f => f.Rot && f.Text.Contains("Tür", StringComparison.Ordinal)).Select(f => f.Text)];

    private static List<string> Einfuegen2Mal(string ort)
    {
        var q = Projekt.Neu("Quelle", D, Daten.NeueAblage());
        var z = AussenBearbeitung.InZwischenablage(q, [new AussenStelle(AussenArt.Haus, -1, 0, ort)], out _)!;
        var ziel = Projekt.Neu("Ziel", D, Daten.NeueAblage(), null, (40, 30));
        var e1 = ziel.AussenAendern(Texte.EinfuegenSchritt, a => AussenBearbeitung.Einfuegen(a, z, 12, 10, true, D.Fassung));
        Assert.Null(e1.Grund);
        Assert.Equal(AussenArt.Haus, e1.Auswahl.Single().Art);
        var e2 = ziel.AussenAendern(Texte.EinfuegenSchritt, a => AussenBearbeitung.Einfuegen(a, z, 28, 20, true, D.Fassung));
        Assert.Null(e2.Grund);
        Assert.Equal(AussenArt.Gebaeude, e2.Auswahl.Single().Art);
        return TuerRote(ziel);
    }

    [Theory]
    [MemberData(nameof(OrteMitModelltuer))]
    public void Einfuegen_AlsSpielortUndAlsGebaeudeOhneOrt_TuerGueltig(string ort)
    {
        var rot = Einfuegen2Mal(ort);
        Assert.True(rot.Count == 0, string.Join("\n", rot));
    }

    [Fact]
    public void Einfuegen_MerkmalGehtMit_SpielortVersatzNull_ZierwegModelltuer()
    {
        var q = Projekt.Neu("Quelle", D, Daten.NeueAblage());
        var z = AussenBearbeitung.InZwischenablage(q, [new AussenStelle(AussenArt.Haus, -1, 0, "talheim_nord")], out _)!;
        Assert.True(Py.Wahr(Get((PyObjekt)z.Stuecke.Single()!, "modell_tuer")));
        // die Zwischenablage als Datei behaelt das Merkmal (Format 1 bleibt)
        var wieder = Zwischenablage.Lesen(z.Schreiben())!;
        Assert.True(Py.Wahr(Get((PyObjekt)wieder.Stuecke.Single()!, "modell_tuer")));
        var ziel = Projekt.Neu("Ziel", D, Daten.NeueAblage(), null, (40, 30));
        ziel.AussenAendern(Texte.EinfuegenSchritt, a => AussenBearbeitung.Einfuegen(a, wieder, 12, 10, true, D.Fassung));
        var e2 = ziel.AussenAendern(Texte.EinfuegenSchritt, a => AussenBearbeitung.Einfuegen(a, wieder, 28, 20, true, D.Fassung));
        var sd = Aussen.Stadtdaten(ziel.AussenStand);
        Assert.Equal(0L, Get(Objekt(Get(Objekt(sd["darstellung"]), "modell_tuer")), "talheim_nord"));
        var zier = Blueprint.ZierwegVon(ziel.AussenStand, e2.Auswahl.Single())!;
        Assert.True(Py.Wahr(Get(zier, "modell_tuer")));
        // ein Gebaeude mit Zierweg auf der Modelltuer gibt das Merkmal beim Kopieren weiter
        var z2 = AussenBearbeitung.InZwischenablage(ziel, [e2.Auswahl.Single()], out _)!;
        Assert.True(Py.Wahr(Get((PyObjekt)z2.Stuecke.Single()!, "modell_tuer")));
        // ein Haus ohne Modelltuer bekommt das Merkmal nicht
        var z3 = AussenBearbeitung.InZwischenablage(q, [new AussenStelle(AussenArt.Haus, -1, 0, "kaufhaus")], out _)!;
        Assert.Null(Get((PyObjekt)z3.Stuecke.Single()!, "modell_tuer"));
    }

    [Fact]
    public void Gegenprobe_OhneMerkmal_TuerenRot_WieBisS0560()
    {
        AussenBearbeitung.ModellTuerKopieAltFuerTests = true;
        try
        {
            Assert.NotEmpty(Einfuegen2Mal("betriebswerkstatt"));
            Assert.NotEmpty(Einfuegen2Mal("talheim_nord"));
            var ablage = Daten.NeueAblage();
            var quelle = Projekt.Neu("Teil", D, ablage);
            var ziel = Projekt.Neu("Ziel", D, ablage);
            Assert.Null(ziel.KarteVergroessern(128, 64, 1));
            var vorher = TuerRote(ziel);
            Assert.True(ziel.StadtteilEinsetzen(quelle, 2).Geht);
            Assert.NotEmpty(TuerRote(ziel).Except(vorher));
        }
        finally
        {
            AussenBearbeitung.ModellTuerKopieAltFuerTests = false;
        }
    }

    [Fact]
    public void Einsetzen_DoppelteSpielorte_TuerenGueltig()
    {
        var ablage = Daten.NeueAblage();
        var quelle = Projekt.Neu("Teil", D, ablage);
        var ziel = Projekt.Neu("Ziel", D, ablage);
        Assert.Null(ziel.KarteVergroessern(128, 64, 1));
        var vorher = TuerRote(ziel);
        var e = ziel.StadtteilEinsetzen(quelle, 2);
        Assert.True(e.Geht, e.Grund);
        var rot = TuerRote(ziel).Except(vorher).ToList();
        Assert.True(rot.Count == 0, string.Join("\n", rot));
    }
}
