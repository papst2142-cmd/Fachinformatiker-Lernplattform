using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Weisung P12-212 Punkt 4 (318 = a): in Nicos erster Stadt (leere Stadt) wird ein Gebaeude mit „Als Spielort festlegen“ zu
/// einem Ort der Stadt 1: Daten (Name, Ansicht, Oeffnungszeit, Innenraum) ueber die Kennung, das Aussehen bleibt Nicos Gebaeude.
/// </summary>
public class SpielortTests
{
    private static Spieldaten D => Daten.Spiel;

    /// <summary>Nicos Gebaeude (Modellname) → Ort der Stadt 1. Talheim-Nord hat in Nicos Teststadt kein Gebaeude.</summary>
    internal static readonly (string Modell, string Ort)[] Zuordnung =
    [
        ("wohnung", "zuhause"), ("bitweiche", "bitweiche"), ("bahnhof_2", "talheim"), ("werkstatt", "betriebswerkstatt"),
        ("verwaltung", "lindenau"), ("haltepunkt", "birkenhain"), ("gewerbehof", "gewerbehof"), ("filiale", "filiale"),
        ("lokschuppen", "lokschuppen"), ("moebelhaus", "moebelhaus"), ("elektromarkt", "elektromarkt"), ("amt", "amt"),
        ("bank", "bank"), ("kaufhaus", "kaufhaus"),
    ];

    internal static string? Festlegen(Projekt p, string modell, string ort)
    {
        var s = TorEingangTests.Gebaeude(p, modell);
        if (Blueprint.ZierwegVon(p.AussenStand, s) is null)
        {
            TorEingangTests.TuerDazu(p, s);
        }
        string? grund = null;
        p.AussenAendern(Texte.SpielortSchritt(ort), a => grund = Spielort.Festlegen(a, TorEingangTests.Gebaeude(p, modell), ort, D.Fassung));
        return grund;
    }

    private static List<StadtbauFund> Rote(Projekt p) => [.. p.PruefenAussen().Where(f => f.Rot)];

    private static string Text(object? o) => PythonJson.Standard(o);

    [Fact]
    public void AlleGebaeude_WerdenSpielorte_ReihenfolgeWieStadt1_NurTalheimNordFehlt()
    {
        var p = TorEingangTests.Nico();
        Assert.Empty(Rote(p));
        foreach (var (modell, ort) in Zuordnung)
        {
            Assert.Null(Festlegen(p, modell, ort));
        }
        var orte = Aussen.Karte(p.AussenStand)["orte"] as List<object?>;
        Assert.Equal(
            ["zuhause", "bitweiche", "talheim", "betriebswerkstatt", "lindenau", "birkenhain", "gewerbehof", "filiale", "lokschuppen"],
            orte!.OfType<PyObjekt>().Select(o => Py.Str(o["id"])).ToList());
        var neue = Aussen.Stadtdaten(p.AussenStand)["neue_orte"] as List<object?>;
        Assert.Equal(["moebelhaus", "elektromarkt", "amt", "bank", "kaufhaus"], neue!.OfType<PyObjekt>().Select(o => Py.Str(o["id"])).ToList());
        // Daten der Stadt 1 (Name, Ansicht), Aussehen von Nico
        var zuhause = AussenBearbeitung.Ort(p.AussenStand, "zuhause")!;
        Assert.Equal("Zuhause", Py.Str(zuhause["name"]));
        Assert.Equal("wohnung", Py.Str(zuhause["modell"]));
        // nur Talheim-Nord fehlt: genau ein roter Fund, der ihn nennt
        Assert.Equal(["talheim_nord"], Spielort.Fehlende(p.AussenStand, D.Fassung).Select(o => Py.Str(o["id"])).ToList());
        // mit Spielorten gelten die Regeln des Spiels: Nicos Teststadt hat Wege ohne Zebrastreifen und einen Paketplatz im Haus
        // (Funde seiner Stadt, Meldung P12-126); vom Festlegen selbst kommt nur „Spielort fehlt“
        var rot = Rote(p).Where(f => !f.Text.Contains("zu Fuß nicht erreichbar", StringComparison.Ordinal) && !f.Text.StartsWith("Der Paketplatz", StringComparison.Ordinal)).ToList();
        Assert.True(rot.Count == 1, string.Join(" | ", rot.Select(f => f.Text)));
        Assert.Contains("Talheim-Nord", rot[0].Text, StringComparison.Ordinal);
        // Innenraeume der Stadt 1 ueber die Kennung (auch der Lokschuppen ueber seine Ansicht „serverfarm“)
        foreach (var o in new[] { "moebelhaus", "elektromarkt", "kaufhaus", "bank", "amt" })
        {
            Assert.Contains(o, p.Orte);
        }
        var innen = p.Innenraeume();
        Assert.True(Get(innen, "serverfarm") is PyObjekt);
        // Buero der Bitweiche bzw. der eigenen Firma aus den Zusatz-Abschnitten
        Assert.Contains(innen, e => e.Key.StartsWith("zusatz", StringComparison.Ordinal) && Get(Objekt(e.Value), "buero") is not null);
        // die Stadt laedt im Spiel
        Assert.Null(BelastungsprobenTests.SpielLaedt(p.Stand()));
    }

    [Theory]
    [InlineData("moebelhaus", "moebelhaus")]
    [InlineData("wohnung", "zuhause")]
    [InlineData("gewerbehof", "gewerbehof")]
    [InlineData("werkstatt", "betriebswerkstatt")]
    [InlineData("lokschuppen", "lokschuppen")]
    [InlineData("haltepunkt", "birkenhain")]
    public void Festlegen_TuerUndHausBleibenImSpielGleich(string modell, string ort)
    {
        var p = TorEingangTests.Nico();
        var s = TorEingangTests.Gebaeude(p, modell);
        if (Blueprint.ZierwegVon(p.AussenStand, s) is null)
        {
            TorEingangTests.TuerDazu(p, s);
        }
        var vorher = p.SzeneOhneLaternen((PyObjekt)Py.Kopie(p.AussenStand)!, true)!;
        var tv = vorher.Tueren.Single(t => t.Ort == modell);
        var hv = vorher.Haeuser.First(h => h.Id.StartsWith(modell + "#", StringComparison.Ordinal));
        Assert.Null(Festlegen(p, modell, ort));
        // eigenes Projekt fuer nachher (der Vorrat des Teilbaus uebernaehme sonst Hausnetze)
        var q = Projekt.AusStadtDatei(p.Stand().Schreiben(), D, Daten.NeueAblage(), "Nachher");
        var nachher = q.SzeneOhneLaternen((PyObjekt)Py.Kopie(q.AussenStand)!, true)!;
        var tn = nachher.Tueren.Single(t => t.Ort == ort);
        var hn = nachher.Haeuser.Single(h => h.Id == ort);
        Assert.True(Vector2.Distance(tv.Mitte, tn.Mitte) < 0.001f, $"{tv.Mitte} {tn.Mitte}");
        Assert.Equal(tv.Aussen, tn.Aussen);
        Assert.Equal(tv.Modell, tn.Modell);
        Assert.True(Vector3.Distance(hv.Min, hn.Min) < 0.001f && Vector3.Distance(hv.Max, hn.Max) < 0.001f, $"{hv.Min}-{hv.Max} {hn.Min}-{hn.Max}");
        Assert.Equal(hv.StueckeBis - hv.StueckeVon, hn.StueckeBis - hn.StueckeVon);
    }

    [Fact]
    public void Gruende_Bahnsteig_OhneTuer_SchonDa_Unbekannt()
    {
        var p = TorEingangTests.Nico();
        var a = p.AussenStand;
        Assert.Equal(Texte.SpielortNurGebaeude, Spielort.FestlegenNicht(a, TorEingangTests.Gebaeude(p, "bahnsteig_bahnhof_2"), D.Fassung));
        var bank = TorEingangTests.Gebaeude(p, "bank");
        if (Blueprint.ZierwegVon(a, bank) is not null)
        {
            p.AussenAendern(Texte.TuerDazu, x => GebaeudeTuer.Entfernen(x, bank));
        }
        Assert.Equal(Texte.SpielortOhneTuer, Spielort.FestlegenNicht(p.AussenStand, TorEingangTests.Gebaeude(p, "bank"), D.Fassung));
        Assert.Null(Festlegen(p, "amt", "amt"));
        string? grund = null;
        TorEingangTests.TuerDazu(p, TorEingangTests.Gebaeude(p, "kaufhaus"));
        p.AussenAendern("x", x => grund = Spielort.Festlegen(x, TorEingangTests.Gebaeude(p, "kaufhaus"), "amt", D.Fassung));
        Assert.Equal(Texte.SpielortSchonDa, grund);
        p.AussenAendern("x", x => grund = Spielort.Festlegen(x, TorEingangTests.Gebaeude(p, "kaufhaus"), "gibtsnicht", D.Fassung));
        Assert.Equal(Texte.SpielortUnbekannt, grund);
    }

    [Fact]
    public void Rueckgaengig_NimmtOrtUndInnenraumZurueck()
    {
        var p = TorEingangTests.Nico();
        var s = TorEingangTests.Gebaeude(p, "moebelhaus");
        if (Blueprint.ZierwegVon(p.AussenStand, s) is null)
        {
            TorEingangTests.TuerDazu(p, s);
        }
        var aussen = Text(p.AussenStand);
        Assert.DoesNotContain("moebelhaus", p.Orte);
        Assert.Null(Festlegen(p, "moebelhaus", "moebelhaus"));
        Assert.Contains("moebelhaus", p.Orte);
        Assert.True(p.Rueckgaengig());
        Assert.Equal(aussen, Text(p.AussenStand));
        Assert.DoesNotContain("moebelhaus", p.Orte);
    }

    [Fact]
    public void OhneSpielorte_KeinFund_Stadt1_KeinKnopf()
    {
        // Nicos Teststadt ohne Spielorte: keine Pruefung „Spielort fehlt“ (jede andere leere Stadt bleibt ohne Fund)
        var p = TorEingangTests.Nico();
        Assert.False(Spielort.HatSpielorte(p.AussenStand));
        Assert.Empty(Spielort.Pruefen(p.AussenStand, D.Fassung));
        // Stadt 1 hat alle Orte: kein Knopf
        var s1 = Projekt.Neu("Stadt 1 Probe", D, Daten.NeueAblage());
        Assert.Empty(Spielort.Fehlende(s1.AussenStand, D.Fassung));
        var stellwerk = TorEingangTests.Gebaeude(s1, "stellwerk");
        Assert.False(Spielort.KnopfZeigen(s1.AussenStand, stellwerk, D.Fassung));
        Assert.DoesNotContain(Projekt.PruefenAussen(s1.Stand(), D), f => f.Text.Contains("Spielort", StringComparison.Ordinal));
    }

    [Fact]
    public void Gegenprobe_FehlenderOrt_IstRot()
    {
        var p = TorEingangTests.Nico();
        Assert.Null(Festlegen(p, "wohnung", "zuhause"));
        var rot = Rote(p);
        Assert.Contains(rot, f => f.Text.Contains("Kaufhaus", StringComparison.Ordinal) && f.Text.Contains("Talheim-Nord", StringComparison.Ordinal));
    }
}
