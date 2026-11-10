using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.33.0 „Tür frei setzen“ (Weisung P12-148, Nico 1a 2a 3a 4a; P12-149: Nicos Funde 01–10; Sammelliste 272, 274).
/// Geprueft: die Tuer kommt auf die gezeichnete Wand (1 = a, Gegenprobe Rechteck der Daten), bleibt hinter dem Wandende an
/// ihrer Wand (3 = a, Gegenprobe Nachbarwand), rastet auf die naechste glatte Stelle oder eine Tuer des Modells ein und ist
/// dann ohne roten Fund (4 = a, Gegenprobe alte Regel rot), ein Fensterteil eines eigenen Gebaeudes wird zum Tuerteil
/// (2 = a, ein Schritt, Rueckgaengig), die Figur kommt im Spiel durch die neue Tuer, und ein Fund nennt den Ort mit dem
/// Namen der Karte (274).
/// </summary>
public class TuerFreiSetzenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Stadt1()
    {
        var p = Projekt.Neu("Tuer frei", D, Daten.NeueAblage());
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        return p;
    }

    private static AussenStelle Ort(string id) => new(AussenArt.Haus, -1, 0, id);

    private static string Name(Projekt p, string id) => Py.Str(AussenBearbeitung.Ort(p.AussenStand, id)!["name"]);

    private static List<string> TuerFunde(Projekt p, string name) =>
        [.. p.PruefenAussen().Where(f => f.Rot && f.Text.StartsWith("Die Tür von „" + name + "“", StringComparison.Ordinal)).Select(f => f.Text)];

    private static StadtSzene Szene(Projekt p) => Ansicht3D.Bauen(p.Stand(), D, Ansicht3D.Einstellung.Standard).Szene;

    /// <summary>
    /// Nicos Funde 02/03 (Moebelhaus), 06/07 (Kaufhaus), 08/10 (Elektromarkt), set_2/1 (Gewerbehof): Tippen vor ein Schaufenster
    /// oder eine Nische. Bisher (Rechteck der Daten, ohne glatte Stelle) rot, jetzt mit „Einrasten“ an der naechsten glatten Stelle
    /// oder auf der Tuer des Modells, ohne Fund, mit gelbem Hinweis. Regel 262 ist dabei unveraendert streng.
    /// </summary>
    [Theory]
    [InlineData("moebelhaus", 14.0f, 21.1f)]
    [InlineData("kaufhaus", 29.6f, 21.1f)]
    [InlineData("kaufhaus", 27.6f, 21.1f)]
    [InlineData("elektromarkt", 23.8f, 21.1f)]
    [InlineData("gewerbehof", 21.4f, 15.1f)]
    public void Schaufenster_RastetAufGlatteStelle_OhneFund_GegenprobeAlteRegelRot(string id, float x, float y)
    {
        var tipp = new Vector2(x, y);
        var alt = Stadt1();
        alt.AussenAendern("Tuer alt", a => TuerSetzen.Setzen(a, Ort(id), tipp));
        Assert.NotEmpty(TuerFunde(alt, Name(alt, id)));

        var p = Stadt1();
        var f = TuerFassade.Rechnen(p, p.AussenStand, Ort(id))!;
        var platz = f.Finden(tipp, null, einrasten: true);
        Assert.True(platz.Art is TuerFassade.Stelle.Glatt or TuerFassade.Stelle.Modelltuer, $"{platz}");
        var hinweis = p.AussenAendern("Tuer frei", a => TuerSetzen.SetzenAn(a, Ort(id), platz));
        Assert.True(platz.Verschoben);
        Assert.Equal(Texte.TuerEingerastetGlatt, hinweis);
        Assert.Empty(TuerFunde(p, Name(p, id)));
        // die Tuer liegt auf der Wand, auf die getippt wurde (gleiche Richtung hinaus)
        var wand = f.Waende[f.WandFuer(tipp, null)];
        Assert.Equal(wand.Aussen, platz.Aussen);
        // ohne „Einrasten“ bleibt sie, wo getippt wurde, und Regel 262 ist rot
        var roh = Stadt1();
        var platzRoh = TuerFassade.Rechnen(roh, roh.AussenStand, Ort(id))!.Finden(tipp, null, einrasten: false);
        Assert.Equal(TuerFassade.Stelle.Nicht, platzRoh.Art);
        roh.AussenAendern("Tuer roh", a => TuerSetzen.SetzenAn(a, Ort(id), platzRoh));
        Assert.NotEmpty(TuerFunde(roh, Name(roh, id)));
    }

    /// <summary>
    /// Vorhersage = Pruefung: an allen vier Waenden von fuenf Haeusern ist jede Stelle, die die Fassade glatt oder Tuer des Modells
    /// nennt, nach dem Setzen ohne Fund, und jede Stelle „nicht glatt“ ist rot (die Fassade rechnet wie das Spiel).
    /// </summary>
    [Theory]
    [InlineData("moebelhaus")]
    [InlineData("elektromarkt")]
    [InlineData("kaufhaus")]
    [InlineData("zuhause")]
    [InlineData("filiale")]
    public void Vorhersage_GleichDerPruefung(string id)
    {
        var p0 = Stadt1();
        var f = TuerFassade.Rechnen(p0, p0.AussenStand, Ort(id))!;
        var name = Name(p0, id);
        for (var w = 0; w < 4; w++)
        {
            var wand = f.Waende[w];
            foreach (var anteil in new[] { 0.25f, 0.75f })
            {
                var platz = f.Finden(wand.Bei(wand.Laenge * anteil) + (wand.Aussen * 0.2f), w, einrasten: false);
                var p = Stadt1();
                p.AussenAendern("Tuer", a => TuerSetzen.SetzenAn(a, Ort(id), platz));
                var funde = TuerFunde(p, name);
                if (platz.Art == TuerFassade.Stelle.Nicht)
                {
                    Assert.NotEmpty(funde);
                }
                else
                {
                    Assert.True(funde.Count == 0, $"{id} Wand {w} {anteil}: {platz.Art}, aber {string.Join(" ", funde)}");
                }
            }
        }
    }

    /// <summary>
    /// 1 = a (Ursache 1): das Zuhause ist schmaler gezeichnet als das Rechteck der Daten (0,3 im Westen). Die Tuer kommt auf die
    /// Wand des gezeichneten Blocks, das Spiel setzt sie genau dort hin (gleiche Wand, gleiche Stelle laengs).
    /// Gegenprobe: die alte Regel legt sie auf das Rechteck der Daten, 0,3 vor die Wand.
    /// </summary>
    [Fact]
    public void GezeichneteWand_StattRechteckDerDaten()
    {
        var p = Stadt1();
        var s = Ort("zuhause");
        var f = TuerFassade.Rechnen(p, p.AussenStand, s)!;
        var r = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        var b = Assert.Single(f.Bloecke);
        Assert.True(Math.Abs(b.X1 - r.X) > 0.1, "gezeichnet = Daten, Test sinnlos");
        var tipp = new Vector2(b.X1 - 0.3f, (b.Z1 + b.Z2) / 2);
        var platz = f.Finden(tipp, null, einrasten: true);
        Assert.Equal(b.X1, platz.Punkt.X, 0.002f);
        p.AussenAendern("Tuer", a => TuerSetzen.SetzenAn(a, s, platz));
        var tuer = Assert.Single(Szene(p).Tueren, t => t.Ort == "zuhause");
        Assert.Equal(new Vector2(-1, 0), tuer.Aussen);
        Assert.Equal(platz.Punkt.Y, tuer.Mitte.Y, 0.002f);
        // Gegenprobe: das Rechteck der Daten
        var alt = TuerSetzen.AufWand(r, tipp).Punkt;
        Assert.True(MathF.Abs(alt.X - b.X1) > 0.1f, $"alte Regel {alt}, Block {b}");
    }

    /// <summary>
    /// 3 = a (Ursache 2, Nicos Bild 01): Tippen knapp hinter dem Ende der Wand, an der die Tuer steht (Kaufhaus Nordwand, hinter der
    /// Ostecke), laesst sie an DIESER Wand, 0,3 vor der Ecke. Gegenprobe: die alte Regel nimmt die Ostwand.
    /// </summary>
    [Fact]
    public void HinterDemWandende_BleibtAnDieserWand()
    {
        var p = Stadt1();
        var s = Ort("kaufhaus");
        var f = TuerFassade.Rechnen(p, p.AussenStand, s)!;
        var tuer = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
        var w = f.WandVon(tuer)!.Value;
        var wand = f.Waende[w];
        Assert.Equal(new Vector2(0, -1), wand.Aussen);
        // 0,2 hinter dem oestlichen Ende, 0,1 schon neben der Ostwand
        var tipp = wand.B + new Vector2(0.2f, 0.1f);
        var platz = f.Finden(tipp, w, einrasten: false);
        Assert.Equal(w, platz.Wand);
        Assert.Equal(wand.Laenge - TuerSetzen.EckAbstand, Vector2.Distance(wand.A, platz.Punkt), 0.002f);
        // ohne bisherige Wand (erster Tipp) hinter dem Ende: die Wand, ueber deren Ende der Tipp weiter hinaus liegt
        Assert.Equal(w, f.Finden(wand.B + new Vector2(0.2f, -0.3f), null, einrasten: false).Wand);
        // Gegenprobe: die alte Regel springt auf die Ostwand
        var alt = TuerSetzen.AufWand(AussenBearbeitung.HausFlaeche(p.AussenStand, s), tipp);
        Assert.Equal(new Vector2(1, 0), alt.Aussen);
    }

    /// <summary>
    /// Ziehen: ein Zug ueber das Wandende hinaus bleibt an der Wand; erst wenn der Finger klar vor der Nachbarwand steht
    /// (ausserhalb der Ecke), wechselt die Wand (die Wand unter dem Finger zaehlt).
    /// </summary>
    [Fact]
    public void Ziehen_UeberDasEnde_BleibtBisDerFingerVorDerNachbarwandSteht()
    {
        var p = Stadt1();
        var f = TuerFassade.Rechnen(p, p.AussenStand, Ort("kaufhaus"))!;
        var sued = Enumerable.Range(0, 4).First(i => f.Waende[i].Aussen == new Vector2(0, 1));
        var wand = f.Waende[sued];
        var w = f.Finden(wand.Bei(wand.Laenge - 0.5f) + new Vector2(0, 0.3f), sued, false).Wand;
        Assert.Equal(sued, w);
        w = f.Finden(wand.B + new Vector2(0.4f, 0.3f), w, false).Wand;
        Assert.Equal(sued, w);
        // vor der Ostwand, mitten in ihrer Spanne
        var ost = f.Finden(wand.B + new Vector2(0.3f, -0.9f), w, false);
        Assert.Equal(new Vector2(1, 0), ost.Aussen);
    }

    /// <summary>
    /// 2 = a (Nicos Fund 05): an einem eigenen Gebaeude wird das Fensterteil unter dem Finger zum Tuerteil, das bisherige Tuerteil
    /// zu diesem Fensterteil; Eingang und Block folgen, die Tuer ist die eigene des Gebaeudes (gruen), alles ein Schritt,
    /// Rueckgaengig stellt beides zurueck. Gegenprobe: die alte Regel stellt eine Kastentuer vor das Fenster (rot).
    /// </summary>
    [Fact]
    public void EigenesGebaeude_FensterWirdTuer_EinSchritt()
    {
        var (p, s) = EigenesHaus();
        var modell = Py.Str(Objekt(Aussen.Landschaft(p.AussenStand)[s.Nummer])["modell"]);
        var vorher = PythonJson.Standard(Get(Bauteile.Gebaeude(Aussen.Stadtdaten(p.AussenStand)), modell));
        var f = TuerFassade.Rechnen(p, p.AussenStand, s)!;
        var sued = Enumerable.Range(0, 4).First(i => f.Waende[i].Aussen == new Vector2(0, 1));
        var wand = f.Waende[sued];
        // das linke Fenster der Vorderseite (Feld 0 von 3)
        var tipp = wand.Bei(wand.Laenge / 6) + new Vector2(0, 0.3f);
        var platz = f.Finden(tipp, null, einrasten: false);
        Assert.Equal(TuerFassade.Stelle.Fenster, platz.Art);
        Assert.NotNull(platz.Fensterteil);
        p.AussenAendern("Tuer", a => TuerSetzen.SetzenAn(a, s, platz));
        var e = (PyObjekt)Get(Bauteile.Gebaeude(Aussen.Stadtdaten(p.AussenStand)), modell)!;
        var teile = Bauteile.TeileLesen(e["teile"]);
        Assert.Equal(Bauteile.Praefix + "building-door", teile[platz.Fensterteil!.Value].Id);
        Assert.Single(teile, t => t.Stockwerk == 0 && t.Art is { Tuer: true });
        var eingang = Assert.Single(Bauteile.EingaengeLesen(e["eingang"]));
        Assert.Equal((teile[platz.Fensterteil.Value].X, teile[platz.Fensterteil.Value].Y), (eingang.X, eingang.Y));
        var sz = Szene(p);
        var t = Assert.Single(sz.Tueren);
        Assert.True(t.Modell, "die Tuer ist nicht das Tuerteil");
        Assert.DoesNotContain(p.PruefenAussen(), x => x.Rot);
        // ein Schritt: Rueckgaengig stellt Teile und Tuer zurueck
        p.Rueckgaengig();
        Assert.Equal(vorher, PythonJson.Standard(Get(Bauteile.Gebaeude(Aussen.Stadtdaten(p.AussenStand)), modell)));
        // Gegenprobe: alte Regel, Kastentuer vor dem Fenster
        p.AussenAendern("Tuer alt", a => TuerSetzen.Setzen(a, s, tipp));
        Assert.Contains(p.PruefenAussen(), x => x.Rot && x.Text.StartsWith("Die Tür von", StringComparison.Ordinal));
    }

    private static (Projekt, AussenStelle) EigenesHaus()
    {
        var p = Projekt.Neu("Eigenes Haus", D, Daten.NeueAblage(), leer: (40, 32));
        p.AussenAendern("Straße", a =>
        {
            var st = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 4, 16)!;
            AussenBearbeitung.LaengeSetzen(a, st, 34);
        });
        var e = new Entwurf();
        for (var x = 0; x < 3; x++)
        {
            for (var y = 0; y < 2; y++)
            {
                var vorne = y == 1;
                var id = vorne && x == 1 ? "building-door" : "building-window";
                Assert.True(e.Setzen(Bauteile.Praefix + id, x, y, 0, vorne ? 0 : 2) >= 0);
                e.Setzen(Bauteile.Praefix + "roof-flat-center", x, y, 1, 0);
            }
        }
        Assert.True(e.EingangFestlegen(e.Teile.FindIndex(t => t.Id == Bauteile.Praefix + "building-door")));
        var b = e.AlsBlueprint("Eigenes Haus", Katalog.Bauteil, p.Name, p.Basis.StadtId, out _);
        string? grund = null;
        var s = p.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, 18, 12, out grund));
        Assert.Null(grund);
        return (p, Assert.IsType<AussenStelle>(s));
    }

    /// <summary>
    /// Die Figur geht im Spiel durch die neue Tuer: Moebelhaus (Ort mit Innenraum) an der glatten Stelle der Westwand.
    /// </summary>
    [Fact]
    public void Figur_KommtDurchDieNeueTuer()
    {
        var p = Stadt1();
        var s = Ort("moebelhaus");
        var f = TuerFassade.Rechnen(p, p.AussenStand, s)!;
        var west = Enumerable.Range(0, 4).First(i => f.Waende[i].Aussen == new Vector2(-1, 0));
        var platz = f.Finden(f.Waende[west].Bei(f.Waende[west].Laenge / 2) + new Vector2(-0.3f, 0), null, true);
        p.AussenAendern("Tuer", a => TuerSetzen.SetzenAn(a, s, platz));
        Assert.Empty(p.PruefenAussen().Where(x => x.Rot).Select(x => x.Text));
        var szene = Szene(p);
        var tuer = Assert.Single(szene.Tueren, t => t.Ort == "moebelhaus");
        Assert.Equal(new Vector2(-1, 0), tuer.Aussen);
        Assert.True(Vector2.Distance(tuer.WegAnfang, platz.Punkt) <= 0.01f);
        var z = AussenBearbeitung.Zugang(p.AussenStand, "moebelhaus")!;
        var ende = ((List<object?>)z["weg"]!).OfType<List<object?>>().Select(q => new Vector2((float)Py.F(q[0]), (float)Py.F(q[1]))).Last();
        var vor = tuer.WegAnfang + (tuer.Aussen * 0.1f);
        var pfad = szene.Laufraster.Weg((ende.X, ende.Y), (vor.X, vor.Y));
        Assert.NotEmpty(pfad);
        Assert.True(Vector2.Distance(new Vector2((float)pfad[^1].X, (float)pfad[^1].Y), vor) <= 0.3f);
        var o = szene.Ort("moebelhaus")!;
        Assert.True(Vector2.Distance(new Vector2(o.TuerX, o.TuerZ), tuer.WegAnfang) <= 0.6f);
        // der Versatz des Modells ist weg
        Assert.Null(Get(Objekt(Get(Objekt(Get(Aussen.Stadtdaten(p.AussenStand), "darstellung"))!, "modell_tuer") ?? new PyObjekt()), "moebelhaus"));
    }

    /// <summary>
    /// Sammelliste 274: in der Pruefung heisst ein Ort der Firma im Probestand wie die Firma („Probe“). Der Fund nennt jetzt den
    /// Namen aus der Karte (Gewerbehof).
    /// </summary>
    [Fact]
    public void Fund_NenntDenNamenDerKarte()
    {
        var p = Stadt1();
        var s = Ort("gewerbehof");
        var f = TuerFassade.Rechnen(p, p.AussenStand, s)!;
        var w = f.WandVon(TuerSetzen.Tuer(p.AussenStand, s))!.Value;
        var probe = f.ProbenVon(w).First(q => q.Art == TuerFassade.Stelle.Nicht && q.T > 0.5f);
        var platz = f.Finden(f.Waende[w].Bei(probe.T), w, einrasten: false);
        p.AussenAendern("Tuer", a => TuerSetzen.SetzenAn(a, s, platz));
        var rot = p.PruefenAussen().Where(x => x.Rot && x.Text.StartsWith("Die Tür von", StringComparison.Ordinal)).Select(x => x.Text).ToList();
        Assert.Contains(rot, t => t.StartsWith("Die Tür von „" + Name(p, "gewerbehof") + "“", StringComparison.Ordinal));
        Assert.DoesNotContain(rot, t => t.Contains("„Probe“", StringComparison.Ordinal));
    }
}
