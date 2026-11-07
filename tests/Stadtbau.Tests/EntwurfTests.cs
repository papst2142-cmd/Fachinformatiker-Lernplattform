using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.9.0 (Weisung P12-48, Plan P12-26b Abschnitt 4): Regeln des Gebaeude-Editors, je Regel ein roter und ein gruener
/// Fall, dazu die Schritte (setzen, drehen, verschieben, Ebene, kopieren, loeschen, Eingang, Rueckgaengig) und der
/// Vorschlag fuer den Grundriss im Erdgeschoss.
/// </summary>
public class EntwurfTests
{
    private const string Tuer = Bauteile.Praefix + "building-door";
    private const string Fenster = Bauteile.Praefix + "building-window";
    private const string Wand = Bauteile.Praefix + "building-block";
    private const string Dach = Bauteile.Praefix + "roof-flat-center";
    private const string Markise = Bauteile.Praefix + "building-window-awnings";
    private const string FensterDetail = Bauteile.Praefix + "window-white";
    private const string Klima = Bauteile.Praefix + "detail-ac-a";

    /// <summary>Kleines gueltiges Haus: Tuer und Fenster nebeneinander (Fassade Sued), Flachdach darauf, Eingang an der Tuer.</summary>
    private static Entwurf Haus()
    {
        var e = new Entwurf();
        e.Setzen(Tuer, 0, 0, 0, 0);
        e.Setzen(Fenster, 1, 0, 0, 0);
        e.Setzen(Dach, 0, 0, 1, 0);
        e.Setzen(Dach, 1, 0, 1, 0);
        Assert.True(e.EingangFestlegen(0));
        return e;
    }

    private static List<StadtbauFund> P(Entwurf e) => e.Pruefen(Katalog.Bauteil);

    private static bool Rot(Entwurf e, string text) => P(e).Any(f => f.Rot && f.Text.EndsWith(text, StringComparison.Ordinal));

    private static bool Gelb(Entwurf e, string text) => P(e).Any(f => !f.Rot && f.Text.EndsWith(text, StringComparison.Ordinal));

    [Fact]
    public void Haus_OhneFund()
    {
        Assert.Empty(P(Haus()));
        Assert.Equal(Bauteile.Dreiecke(Haus().Teile, Katalog.Bauteil), Haus().Teile.Sum(t => Katalog.Bauteil(t.Id)!.Dreiecke));
    }

    [Fact]
    public void Leer_Rot_MitTeilImEG_Gruen()
    {
        Assert.True(Rot(new Entwurf(), Texte.EntwurfLeer));
        var nurOben = new Entwurf();
        nurOben.Setzen(Wand, 0, 0, 1, 0);
        Assert.True(Rot(nurOben, Texte.EntwurfLeer));
        Assert.False(Rot(Haus(), Texte.EntwurfLeer));
    }

    [Fact]
    public void Unbekannt_Rot_Bekannt_Gruen()
    {
        var e = Haus();
        e.Teile.Add(new Bauteile.Teil(Bauteile.Praefix + "gibt-es-nicht", 5, 5, 0, 0));
        Assert.True(Rot(e, Texte.EntwurfUnbekannt("gibt-es-nicht")));
        Assert.DoesNotContain(P(Haus()), f => f.Text.Contains("Unbekannt", StringComparison.Ordinal));
        // ein unbekanntes Teil laesst sich nicht setzen (nichts geschieht)
        Assert.Equal(-1, new Entwurf().Setzen(Bauteile.Praefix + "gibt-es-nicht", 0, 0, 0, 0));
    }

    [Fact]
    public void Doppelt_Rot_FassadenTeilAnAndererSeite_Gruen()
    {
        var e = Haus();
        e.Setzen(Fenster, 1, 0, 0, 2);
        Assert.True(Rot(e, Texte.EntwurfDoppelt));
        // Fenster vorne und hinten an derselben Wand: zwei Plaetze, kein Fund
        var g = Haus();
        g.Setzen(FensterDetail, 1, 0, 0, 0);
        g.Setzen(FensterDetail, 1, 0, 0, 2);
        Assert.False(Rot(g, Texte.EntwurfDoppelt));
        g.Setzen(FensterDetail, 1, 0, 0, 0);
        Assert.True(Rot(g, Texte.EntwurfDoppelt));
    }

    [Fact]
    public void DachOhneWand_Rot_DachAufWand_Gruen()
    {
        var e = Haus();
        e.Setzen(Dach, 5, 5, 0, 0);
        Assert.True(Rot(e, Texte.EntwurfDachOhneWand));
        var hoch = Haus();
        hoch.Setzen(Dach, 0, 0, 3, 0);
        Assert.True(Rot(hoch, Texte.EntwurfDachOhneWand));
        Assert.False(Rot(Haus(), Texte.EntwurfDachOhneWand));
    }

    [Fact]
    public void InDerLuft_Rot_AufgelegtGruen()
    {
        // Wand im 2. OG ohne Wand im 1. OG
        var e = Haus();
        e.Setzen(Wand, 0, 0, 2, 0);
        Assert.True(Rot(e, Texte.EntwurfInDerLuft));
        // Fenster an einem Feld ohne Wand
        var f = Haus();
        f.Setzen(FensterDetail, 6, 6, 0, 0);
        Assert.True(Rot(f, Texte.EntwurfInDerLuft));
        // Klimageraet ohne Dach darunter
        var k = Haus();
        k.Setzen(Klima, 6, 6, 1, 0);
        Assert.True(Rot(k, Texte.EntwurfInDerLuft));
        // gruen: Fenster an der Wand, Klimageraet auf dem Dach, Wand auf Wand
        var g = Haus();
        g.Setzen(FensterDetail, 1, 0, 0, 0);
        g.Setzen(Klima, 1, 0, 1, 0);
        Assert.False(Rot(g, Texte.EntwurfInDerLuft));
        var turm = new Entwurf();
        for (var s = 0; s < 3; s++)
        {
            turm.Setzen(Wand, 0, 0, s, 0);
        }
        Assert.False(Rot(turm, Texte.EntwurfInDerLuft));
    }

    [Fact]
    public void ZuHoch_Rot_Bis4OG_Gruen()
    {
        var e = new Entwurf();
        for (var s = 0; s <= Bauteile.HoechsteWandEbene; s++)
        {
            e.Setzen(Wand, 0, 0, s, 0);
        }
        Assert.False(Rot(e, Texte.EntwurfZuHoch));
        e.Setzen(Wand, 0, 0, Bauteile.HoechsteWandEbene + 1, 0);
        Assert.True(Rot(e, Texte.EntwurfZuHoch));
        // ein Dach auf der obersten Ebene ist erlaubt
        var d = new Entwurf();
        for (var s = 0; s <= Bauteile.HoechsteWandEbene; s++)
        {
            d.Setzen(Wand, 0, 0, s, 0);
        }
        d.Setzen(Dach, 0, 0, Bauteile.HoechsteWandEbene + 1, 0);
        Assert.False(Rot(d, Texte.EntwurfZuHoch));
        Assert.False(Rot(d, Texte.EntwurfDachOhneWand));
    }

    [Fact]
    public void KeinEingang_Rot_MitEingang_Gruen()
    {
        var e = Haus();
        e.Eingaenge.Clear();
        Assert.True(Rot(e, Texte.EntwurfKeinEingang));
        Assert.False(Rot(Haus(), Texte.EntwurfKeinEingang));
    }

    [Fact]
    public void MehrEingaenge_Rot_EinEingang_Gruen()
    {
        var e = Haus();
        e.Setzen(Tuer, 2, 0, 0, 0);
        e.Setzen(Dach, 2, 0, 1, 0);
        e.Eingaenge.Add(new Bauteile.Eingang(2, 0, Bauteile.Seiten.Sued));
        Assert.True(Rot(e, Texte.EntwurfMehrEingaenge));
        // Eingang festlegen ersetzt den alten: wieder genau einer
        Assert.True(e.EingangFestlegen(e.Teile.Count - 2));
        Assert.Single(e.Eingaenge);
        Assert.False(Rot(e, Texte.EntwurfMehrEingaenge));
    }

    [Fact]
    public void EingangOhneTuer_Rot_AnDerTuer_Gruen()
    {
        var e = Haus();
        e.Eingaenge[0] = new Bauteile.Eingang(1, 0, Bauteile.Seiten.Sued);
        Assert.True(Rot(e, Texte.EntwurfEingangOhneTuer));
        // falsche Seite der Tuer
        var s = Haus();
        s.Eingaenge[0] = new Bauteile.Eingang(0, 0, Bauteile.Seiten.Nord);
        Assert.True(Rot(s, Texte.EntwurfEingangOhneTuer));
        // Tuer als Teil an einer Wand: geht
        var d = new Entwurf();
        d.Setzen(Wand, 0, 0, 0, 0);
        d.Setzen(Dach, 0, 0, 1, 0);
        var i = d.Setzen(Bauteile.Praefix + "door-white", 0, 0, 0, 0);
        Assert.True(d.EingangFestlegen(i));
        Assert.Empty(P(d));
        // eine Wand mit Fenster kann kein Eingang sein
        Assert.False(Haus().EingangFestlegen(1));
    }

    [Fact]
    public void VorEingangBelegt_Rot_Frei_Gruen()
    {
        var e = Haus();
        e.Setzen(Wand, 0, 1, 0, 0);
        Assert.True(Rot(e, Texte.EntwurfVorEingangBelegt));
        Assert.False(Rot(Haus(), Texte.EntwurfVorEingangBelegt));
    }

    [Fact]
    public void ZuVieleDreiecke_Rot_UnterDerGrenze_Gruen()
    {
        var e = new Entwurf();
        var n = (Bauteile.GrenzeDreiecke / Katalog.Bauteil(Markise)!.Dreiecke) + 1;
        for (var i = 0; i < n; i++)
        {
            e.Setzen(Markise, i % Entwurf.Raster, i / Entwurf.Raster, 0, 0);
        }
        Assert.True(Bauteile.Dreiecke(e.Teile, Katalog.Bauteil) > Bauteile.GrenzeDreiecke);
        Assert.Contains(P(e), f => f.Rot && f.Text == Texte.EntwurfZuVieleDreiecke(Bauteile.Dreiecke(e.Teile, Katalog.Bauteil)));
        e.Loeschen(0);
        Assert.True(Bauteile.Dreiecke(e.Teile, Katalog.Bauteil) <= Bauteile.GrenzeDreiecke);
        Assert.DoesNotContain(P(e), f => f.Text.StartsWith("Das Gebäude hat ", StringComparison.Ordinal) && f.Text.Contains("Dreiecke", StringComparison.Ordinal));
    }

    [Fact]
    public void Zerfaellt_Rot_Zusammenhaengend_Gruen()
    {
        var e = Haus();
        e.Setzen(Wand, 5, 5, 0, 0);
        Assert.True(Rot(e, Texte.EntwurfZerfaellt));
        // nur ueber Eck beruehrt zaehlt nicht als zusammen
        var eck = Haus();
        eck.Setzen(Wand, 2, 1, 0, 0);
        Assert.True(Rot(eck, Texte.EntwurfZerfaellt));
        var g = Haus();
        g.Setzen(Wand, 2, 0, 0, 0);
        Assert.False(Rot(g, Texte.EntwurfZerfaellt));
    }

    [Fact]
    public void ObenOffen_Gelb_JeEbeneEinFund_MitDach_KeinFund()
    {
        var e = new Entwurf();
        e.Setzen(Tuer, 0, 0, 0, 0);
        e.Setzen(Wand, 1, 0, 0, 0);
        e.Setzen(Wand, 1, 0, 1, 0);
        e.EingangFestlegen(0);
        var gelb = P(e).Where(f => !f.Rot).ToList();
        // EG: Tuer offen; 1. OG: Wand offen -> zwei Funde (je Ebene einer), nie rot
        Assert.Equal(2, gelb.Count(f => f.Text.EndsWith(Texte.EntwurfObenOffen(1), StringComparison.Ordinal)));
        Assert.DoesNotContain(P(e), f => f.Rot);
        Assert.False(Gelb(Haus(), Texte.EntwurfObenOffen(1)));
    }

    [Fact]
    public void FensterZuNachbar_Gelb_FensterNachAussen_KeinFund()
    {
        var e = Haus();
        e.Setzen(Wand, 1, 1, 0, 0);
        e.Setzen(Dach, 1, 1, 1, 0);
        Assert.True(Gelb(e, Texte.EntwurfFensterZuNachbar));
        Assert.DoesNotContain(P(e), f => f.Rot);
        Assert.False(Gelb(Haus(), Texte.EntwurfFensterZuNachbar));
    }

    [Fact]
    public void Fund_NenntEbeneUndFeld()
    {
        var e = Haus();
        e.Setzen(Dach, 2, 0, 0, 0);
        var f = P(e).Single(f => f.Rot);
        Assert.Equal(Texte.EntwurfStelle(0, 2, 0) + Texte.EntwurfDachOhneWand, f.Text);
        Assert.Equal(2.5, f.X);
        Assert.Equal(0.5, f.Y);
    }

    // ---- Schritte ---------------------------------------------------------------------------------

    [Fact]
    public void Setzen_AusserhalbOderFalscheEbene_NichtsGeschieht()
    {
        var e = new Entwurf();
        Assert.Equal(-1, e.Setzen(Wand, -1, 0, 0, 0));
        Assert.Equal(-1, e.Setzen(Wand, 0, Entwurf.Raster, 0, 0));
        Assert.Equal(-1, e.Setzen(Wand, 0, 0, Bauteile.Ebenen, 0));
        Assert.Empty(e.Teile);
        Assert.False(e.KannZurueck);
        Assert.Equal(0, e.Setzen(Wand, 0, 0, 0, 7));
        Assert.Equal(3, e.Teile[0].Dreh);
    }

    [Fact]
    public void Drehen_Verschieben_Ebene_NehmenDenEingangMit()
    {
        var e = Haus();
        e.Drehen(0);
        Assert.Equal(new Bauteile.Eingang(0, 0, Bauteile.Seiten.West), Assert.Single(e.Eingaenge));
        Assert.True(e.Verschieben(0, 0, 3));
        Assert.Equal(new Bauteile.Eingang(0, 3, Bauteile.Seiten.West), Assert.Single(e.Eingaenge));
        Assert.False(e.Verschieben(0, -1, 0));
        Assert.Equal(0, e.Teile[0].X);
        // eine Ebene hoeher: kein Eingang mehr (nur im Erdgeschoss)
        Assert.True(e.EbeneWechseln(0, 1));
        Assert.Empty(e.Eingaenge);
        Assert.False(e.EbeneWechseln(0, Bauteile.Ebenen));
    }

    [Fact]
    public void Kopieren_InsNaechsteFreieFeld_Loeschen_NimmtEingangMit()
    {
        var e = Haus();
        var i = e.Kopieren(1);
        Assert.Equal((2, 0, 0), (e.Teile[i].X, e.Teile[i].Y, e.Teile[i].Stockwerk));
        var j = e.Kopieren(0);
        Assert.Equal((0, 1), (e.Teile[j].X, e.Teile[j].Y));
        e.Loeschen(0);
        Assert.Empty(e.Eingaenge);
    }

    [Fact]
    public void Rueckgaengig_Wiederholen_JederSchrittEinzeln()
    {
        var e = Haus();
        var vorher = PythonJsonText(e);
        e.Setzen(Wand, 3, 0, 0, 0);
        e.Drehen(0);
        Assert.True(e.KannZurueck);
        e.Zurueck();
        e.Zurueck();
        Assert.Equal(vorher, PythonJsonText(e));
        Assert.True(e.KannWiederholen);
        e.Wiederholen();
        Assert.Equal(5, e.Teile.Count);
        // ein neuer Schritt verwirft das Wiederholen
        e.Loeschen(4);
        Assert.False(e.KannWiederholen);
    }

    private static string PythonJsonText(Entwurf e) => PythonJson.Standard(e.Daten());

    // ---- Grundriss-Vorschlag (Frage 4 = a, nur Erdgeschoss) ----------------------------------------

    public static TheoryData<int> Drehungen() => [0, 1, 2, 3];

    [Theory]
    [MemberData(nameof(Drehungen))]
    public void GrundrissVorschlag_GueltigerInnenraum_TuerAmEingang(int dreh)
    {
        // Haus 3 x 2 Felder, Tuer in der Mitte einer Seite (je nach Drehung Sued, West, Nord, Ost)
        var e = new Entwurf();
        for (var x = 0; x < 3; x++)
        {
            for (var y = 0; y < 2; y++)
            {
                e.Setzen(Wand, x, y, 0, 0);
                e.Setzen(Dach, x, y, 1, 0);
            }
        }
        var (tx, ty) = dreh switch { 0 => (1, 1), 1 => (0, 0), 2 => (1, 0), _ => (2, 1) };
        var i = e.Setzen(Bauteile.Praefix + "door-white", tx, ty, 0, dreh);
        Assert.True(e.EingangFestlegen(i));
        Assert.DoesNotContain(P(e), f => f.Rot);
        var g = e.GrundrissVorschlag()!;
        var quer = dreh is 1 or 3;
        // die Fassade mit dem Eingang ist die linke Wand: Hoehe des Plans = Breite der Fassade
        Assert.Equal((quer ? 3 : 2) * Bauteile.InnenJeFeld, Py.F(g["breite"]));
        Assert.Equal((quer ? 2 : 3) * Bauteile.InnenJeFeld, Py.F(g["hoehe"]));
        var funde = StadtbauPruefung.Innen("bau:probe", g, [], Daten.Spiel.Grafik);
        Assert.DoesNotContain(funde, f => f.Rot);
        // die Tuer im Flur liegt an der Stelle des Eingangs (von draussen gesehen, je Feld 4)
        var ein = Objekt(Objekt(g["flur"])!["eingang"])!;
        var mitte = (Py.F(ein["von"]) + Py.F(ein["bis"])) / 2;
        Assert.Equal(dreh is 0 or 2 ? 6.0 : 2.0, mitte);
    }

    [Fact]
    public void GrundrissVorschlag_EinFeldBreit_FlurUndRaumPassen()
    {
        var e = new Entwurf();
        e.Setzen(Tuer, 0, 0, 0, 0);
        e.Setzen(Dach, 0, 0, 1, 0);
        e.EingangFestlegen(0);
        var g = e.GrundrissVorschlag()!;
        Assert.Equal(4.0, Py.F(g["breite"]));
        Assert.Equal(4.0, Py.F(g["hoehe"]));
        Assert.DoesNotContain(StadtbauPruefung.Innen("bau:probe", g, [], Daten.Spiel.Grafik), f => f.Rot);
        // Gegenprobe: ohne Raum ist der Innenraum rot (jedes Haus braucht einen Raum)
        Py.Setzen(g, "raeume", new List<object?>());
        Assert.Contains(StadtbauPruefung.Innen("bau:probe", g, [], Daten.Spiel.Grafik), f => f.Rot);
    }

    [Fact]
    public void GrundrissVorschlag_OhneEingangOderLeer_Null()
    {
        Assert.Null(new Entwurf().GrundrissVorschlag());
        var e = Haus();
        e.Eingaenge.Clear();
        Assert.Null(e.GrundrissVorschlag());
    }
}
