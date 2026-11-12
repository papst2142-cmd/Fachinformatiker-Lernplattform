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
/// Weisung P12-212 Punkt 1 (Nico: „sieht doof aus, würde keiner so machen“): „Tür dazu“ legt den Eingang eines Gebäudes mit
/// Toren (Betriebswerkstatt, Lokschuppen) auf eine glatte Wand ohne Tor, nie in ein Tor. Von Hand bleibt die Tür frei.
/// </summary>
public class TorEingangTests
{
    private static Spieldaten D => Daten.Spiel;

    private static readonly Lazy<byte[]> Datei = new(() =>
        File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "nico", "teststadt_S0.43.0.fistadt")));

    internal static Projekt Nico(string name = "Nico Teststadt") => Projekt.AusStadtDatei(Datei.Value, D, Daten.NeueAblage(), name);

    internal static AussenStelle Gebaeude(Projekt p, string modell) =>
        new(AussenArt.Gebaeude, Aussen.Landschaft(p.AussenStand).FindIndex(t => t is PyObjekt g && Py.Gleich(g["typ"], "gebaeude") && Py.Gleich(g["modell"], modell)));

    private static List<string> RoteFunde(Projekt p) => [.. p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text)];

    internal static string? TuerDazu(Projekt p, AussenStelle s)
    {
        var platz = GebaeudeTuer.DazuPlatz(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s, einrasten: true);
        string? hinweis = null;
        p.AussenAendern(Texte.TuerDazu, a => hinweis = GebaeudeTuer.DazuAn(a, s, true, platz));
        return hinweis;
    }

    /// <summary>Wand der gespeicherten Tuer und die Waende mit Tor.</summary>
    private static (int Wand, HashSet<int> Tore, TuerFassade F) Lage(Projekt p, AussenStelle s)
    {
        var z = Blueprint.ZierwegVon(p.AussenStand, s)!;
        var t = (List<object?>)z["tuer"]!;
        var f = TuerFassade.Rechnen(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s)!;
        var w = f.WandFuer(new Vector2((float)Py.F(t[0]), (float)Py.F(t[1])), null);
        return (w, GebaeudeTuer.TorWaende(p.AussenStand, s, f), f);
    }

    [Fact]
    public void TuerDazu_Lokschuppen_EingangAufWandOhneTor_OhneRotenFund()
    {
        var p = Nico();
        var s = Gebaeude(p, "lokschuppen");
        Assert.Null(TuerDazu(p, s));
        var (w, tore, f) = Lage(p, s);
        Assert.NotEmpty(tore);
        Assert.DoesNotContain(w, tore);
        var z = Blueprint.ZierwegVon(p.AussenStand, s)!;
        Assert.False(Py.Wahr(Get(z, "modell_tuer")));
        Assert.DoesNotContain(RoteFunde(p), x => x.Contains("Tür", StringComparison.Ordinal));
        // die Tuer steht auf einer glatten Stelle ihrer Wand
        var t = (List<object?>)z["tuer"]!;
        var platz = f.Finden(new Vector2((float)Py.F(t[0]), (float)Py.F(t[1])) + (f.Waende[w].Aussen * 0.01f), w, einrasten: false);
        Assert.Equal(TuerFassade.Stelle.Glatt, platz.Art);
    }

    /// <summary>
    /// Die Betriebswerkstatt (CC0 low-detail-building-wide-a) hat an JEDER Wand in Tuerhoehe ein Band (0,3 bis 0,66 hoch,
    /// 0,24 vor dem Sockel): keine Wand ohne Tor hat eine glatte Stelle. Dann bleibt die Tuer im Rolltor (kein roter Fund) und
    /// „Tür dazu“ sagt es (Frage an Nico in Meldung P12-126).
    /// </summary>
    [Fact]
    public void TuerDazu_Werkstatt_OhneGlatteSeitenwand_BleibtImTor_MitHinweis()
    {
        var p = Nico();
        var s = Gebaeude(p, "werkstatt");
        var f = TuerFassade.Rechnen(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s)!;
        var tore = GebaeudeTuer.TorWaende(p.AussenStand, s, f);
        Assert.NotEmpty(tore);
        foreach (var w in Enumerable.Range(0, f.Waende.Count).Where(w => !tore.Contains(w)))
        {
            Assert.DoesNotContain(f.ProbenVon(w), q => q.Art == TuerFassade.Stelle.Glatt);
        }
        Assert.Equal(Texte.TuerImTorKeineSeite, TuerDazu(p, s));
        Assert.Contains(Lage(p, s).Wand, tore);
        Assert.DoesNotContain(RoteFunde(p), x => x.Contains("Tür", StringComparison.Ordinal));
    }

    [Fact]
    public void Gegenprobe_TorAlt_WerkstattTuerImRolltor()
    {
        GebaeudeTuer.TorAltFuerTests = true;
        try
        {
            var p = Nico("Alt");
            var s = Gebaeude(p, "werkstatt");
            Assert.Null(TuerDazu(p, s));
            var (w, tore, _) = Lage(p, s);
            Assert.Contains(w, tore);
            Assert.True(Py.Wahr(Get(Blueprint.ZierwegVon(p.AussenStand, s)!, "modell_tuer")));
        }
        finally
        {
            GebaeudeTuer.TorAltFuerTests = false;
        }
    }

    [Fact]
    public void Werkstatt_TorWandGedreht_FolgtDerDrehung()
    {
        var p = Nico();
        var s = Gebaeude(p, "werkstatt");
        var f0 = TuerFassade.Rechnen(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s)!;
        var vorher = GebaeudeTuer.TorWaende(p.AussenStand, s, f0).Select(w => f0.Waende[w].Aussen).ToList();
        Assert.Contains(new Vector2(0, 1), vorher);
        p.AussenAendern(Texte.Drehen, a => AussenBearbeitung.Drehen(a, s, 90));
        var f1 = TuerFassade.Rechnen(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s)!;
        var nachher = GebaeudeTuer.TorWaende(p.AussenStand, s, f1).Select(w => f1.Waende[w].Aussen).ToList();
        Assert.Contains(new Vector2(-1, 0), nachher);
        Assert.DoesNotContain(new Vector2(0, 1), nachher);
    }

    [Fact]
    public void GebaeudeOhneTor_TuerDazu_WieBisher()
    {
        var p = Nico();
        var s = Gebaeude(p, "wohnung");
        var f = TuerFassade.Rechnen(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s)!;
        Assert.Empty(GebaeudeTuer.TorWaende(p.AussenStand, s, f));
        var neu = GebaeudeTuer.DazuPlatz(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s, true);
        GebaeudeTuer.TorAltFuerTests = true;
        try
        {
            var alt = GebaeudeTuer.DazuPlatz(p, (PyObjekt)Py.Kopie(p.AussenStand)!, s, true);
            Assert.Equal(alt, neu);
        }
        finally
        {
            GebaeudeTuer.TorAltFuerTests = false;
        }
    }
}
