using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Fund 262 (Weisung P12-122, Nico: an seinen Gebaeuden „Statdhaus 1“ und „Stadtgebäude 2“ glitchen die Tueren in der
/// Fassade). Ursache: jedes Tuer-Teil des Gebaeude-Editors (Kenney Modular Buildings) hat eine eigene Glastuer in einer
/// Nische 0,1 hinter der Fassade (Breite 0,25, „Tür“ 0,75). Das Spiel zeichnete davor seine Kastentuer (0,46 mit Rahmen):
/// schmaler als die Nische der „Tür“ (Glastuer und Laibung neben dem Rahmen sichtbar) bzw. breiter als die Nische der
/// anderen Teile (Rahmen steckt seitlich in der Fassade). Die Glastuer erkannte <see cref="StadtTueren.ModellTueren"/> nicht
/// (Glas zaehlte nie als Tuer), darum blieb sie stehen. Jetzt: die eigene Tuer des Gebaeudes ist die Tuer, keine zweite;
/// eine gezeichnete Tuer, die nicht buendig liegt, ist im Werkzeug rot (<see cref="TuerBuendig"/>). Gegenprobe je Art:
/// Verhalten bis A 0.54.0 (<see cref="StadtTueren.EigeneTuerAltFuerTests"/>) ist rot.
/// </summary>
public partial class TuerBuendigTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Leer(string name)
    {
        var p = Projekt.Neu(name, D, Daten.NeueAblage(), leer: (40, 32));
        p.AussenAendern("Straße", a =>
        {
            var s = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 4, 16)!;
            AussenBearbeitung.LaengeSetzen(a, s, 34);
        });
        return p;
    }

    private static StadtSzene Szene(Projekt p) => Ansicht3D.Bauen(p.Stand(), D, Ansicht3D.Einstellung.Standard).Szene;

    /// <summary>Haus b x t aus Wandteilen (vorne nach Sueden, hinten nach Norden), Tuer-Teil vorne in der Mitte, Flachdach.</summary>
    private static Entwurf Haus(string tuer, int b = 3, int t = 2, int geschosse = 1, int vorbau = -1)
    {
        var e = new Entwurf();
        for (var g = 0; g < geschosse; g++)
        {
            for (var x = 0; x < b; x++)
            {
                for (var y = 0; y < t; y++)
                {
                    var vorne = y == t - 1;
                    var id = g == 0 && vorne && x == b / 2 && vorbau < 0 ? tuer : "building-window";
                    Assert.True(e.Setzen(Bauteile.Praefix + id, x, y, g, vorne ? 0 : 2) >= 0, $"{id} {x},{y},{g}");
                }
            }
            if (vorbau >= 0)
            {
                Assert.True(e.Setzen(Bauteile.Praefix + (g == 0 ? tuer : "building-window"), vorbau, t, g, 0) >= 0);
            }
        }
        for (var x = 0; x < b; x++)
        {
            for (var y = 0; y < t; y++)
            {
                e.Setzen(Bauteile.Praefix + "roof-flat-center", x, y, geschosse, 0);
            }
        }
        if (vorbau >= 0)
        {
            e.Setzen(Bauteile.Praefix + "roof-flat-center", vorbau, t, geschosse, 0);
        }
        Assert.True(e.EingangFestlegen(e.Teile.FindIndex(x => x.Id == Bauteile.Praefix + tuer)));
        Assert.DoesNotContain(e.Pruefen(Katalog.Bauteil), f => f.Rot);
        return e;
    }

    private static Projekt Gesetzt(string name, Entwurf e) => GesetztMit(name, e).P;

    private static (Projekt P, AussenStelle S) GesetztMit(string name, Entwurf e)
    {
        var p = Leer(name);
        var b = e.AlsBlueprint(name, Katalog.Bauteil, p.Name, p.Basis.StadtId, out _);
        string? grund = null;
        var s = p.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, 18, 12, out grund));
        Assert.Null(grund);
        return (p, Assert.IsType<AussenStelle>(s));
    }

    /// <summary>Teile der gezeichneten Kastentuer (Rahmenfarbe) bis 0,5 um die Tuer.</summary>
    private static int Kastenteile(StadtSzene sz, StadtTuer t) =>
        sz.Teile.Count(k => k.Farbe == StadtTueren.Rahmen && Vector2.Distance(new Vector2((k.X1 + k.X2) / 2, (k.Z1 + k.Z2) / 2), t.Mitte) <= 0.5f);

    public static TheoryData<string> TuerTeile() =>
    [
        "building-door", "building-door-window", "building-door-window-narrow", "building-edges-door",
        "building-window-door-window", "building-window-door-window-round",
    ];

    private static void EigeneTuerOhneZweite(Projekt p, string was)
    {
        var sz = Szene(p);
        var t = Assert.Single(sz.Tueren);
        Assert.True(t.Modell, was + ": die Tuer ist nicht die eigene des Gebaeudes");
        Assert.Equal(0, Kastenteile(sz, t));
        // die Tuermitte liegt auf der Glastuer des Teils (quer innerhalb, in ihrer Tiefe)
        var haus = Assert.Single(sz.Haeuser, h => h.Id.StartsWith(Bauteile.ModellPraefix, StringComparison.Ordinal));
        var glas = StadtTueren.ModellTueren(sz.Stuecke, haus.StueckeVon, haus.StueckeBis, sz.ModellAn, t.Mitte, t.Aussen, weite: 0.1f, glas: true);
        Assert.Contains(glas, g => g.A1 <= 0.001f && g.A2 >= -0.001f && MathF.Abs(g.Tiefe) <= 0.01f);
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot);
        // Gegenprobe: bis A 0.54.0 Kastentuer davor, im Werkzeug jetzt rot
        StadtTueren.EigeneTuerAltFuerTests = true;
        try
        {
            var alt = Szene(p);
            var ta = Assert.Single(alt.Tueren);
            Assert.False(ta.Modell);
            Assert.True(Kastenteile(alt, ta) > 0);
            Assert.NotEmpty(StadtTueren.NichtBuendig(alt, ta));
            Assert.Contains(p.PruefenAussen(), f => f.Rot && f.Text.StartsWith("Die Tür von „" + p.Name + "“ sitzt nicht sauber in der Wand", StringComparison.Ordinal));
        }
        finally
        {
            StadtTueren.EigeneTuerAltFuerTests = false;
        }
    }

    [Theory]
    [MemberData(nameof(TuerTeile))]
    public void EditorTuerteil_EigeneGlastuer_KeineZweiteTuer_GegenprobeRot(string tuer) =>
        EigeneTuerOhneZweite(Gesetzt("Haus " + tuer, Haus(tuer)), tuer);

    /// <summary>Nicos Gebaeude von den Bildern nachgestellt (Bauplaene liegen nicht vor): langes Haus mit zwei Geschossen und
    /// „Tür“ in der Mitte; Haus mit Vorbau in der Mitte und schmaler Tuer.</summary>
    [Fact]
    public void NicosGebaeude_Nachgestellt_EigeneTuer_GegenprobeRot()
    {
        EigeneTuerOhneZweite(Gesetzt("Stadthaus 1", Haus("building-door", b: 7, t: 2, geschosse: 2)), "Stadthaus 1");
        EigeneTuerOhneZweite(Gesetzt("Stadtgebaeude 2", Haus("building-window-door-window", b: 5, t: 2, geschosse: 2, vorbau: 2)), "Stadtgebaeude 2");
    }

    /// <summary>Zierweg des Gebaeudes mit Tuer und Wegbeginn an <paramref name="p"/> (wie eine von Hand verlegte Tuer).</summary>
    private static void TuerNach(Projekt p, AussenStelle s, Vector2 q) =>
        p.AussenAendern("Tuer", a =>
        {
            var z = Blueprint.ZierwegVon(a, s)!;
            var weg = (List<object?>)z["weg"]!;
            weg[0] = new List<object?> { Math.Round((double)q.X, 2), Math.Round((double)q.Y, 2) };
            Py.Setzen(z, "tuer", new List<object?> { Math.Round((double)q.X, 2), Math.Round((double)q.Y, 2) });
        });

    /// <summary>Eine Tuer auf einem Fenster (Fensterbank davor, Nische dahinter) ist rot, wieder auf der eigenen Tuer gruen.</summary>
    [Fact]
    public void TuerAufFensterNische_Rot_AufDerEigenenTuer_Gruen()
    {
        var (p, s) = GesetztMit("Haus", Haus("building-edges-door", b: 5));
        var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        var eigene = Blueprint.ZierwegVon(p.AussenStand, s)!["tuer"] as List<object?>;
        // auf das Fenster ganz links vorne (Feldmitte)
        TuerNach(p, s, new Vector2((float)(hx + 0.5), (float)(hy + hh)));
        var sz = Szene(p);
        var t = Assert.Single(sz.Tueren);
        Assert.False(t.Modell);
        Assert.NotEmpty(StadtTueren.NichtBuendig(sz, t));
        Assert.Contains(p.PruefenAussen(), f => f.Rot && f.Text.Contains("sitzt nicht sauber in der Wand", StringComparison.Ordinal));
        // wieder auf die eigene Tuer: gruen
        TuerNach(p, s, new Vector2((float)Py.F(eigene![0]), (float)Py.F(eigene[1])));
        Assert.True(Assert.Single(Szene(p).Tueren).Modell);
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Text.Contains("sitzt nicht sauber in der Wand", StringComparison.Ordinal));
    }

    /// <summary>Einrasten findet die Glastuer des Editor-Teils (vorher nur dunkle CC0-Tueren).</summary>
    [Fact]
    public void Einrasten_FindetGlastuerDesEditorTeils()
    {
        var (p, s) = GesetztMit("Haus", Haus("building-door-window-narrow", b: 5));
        var r = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        var mitten = TuerSetzen.ModellTuerMitten(p.SzeneOhneLaternen(p.AussenStand, mitGebaeuden: true)!, D.Grafik, r);
        Assert.Contains(mitten, m => MathF.Abs(m.X - (float)(r.X + (r.W / 2))) <= 0.02f && MathF.Abs(m.Y - (float)(r.Y + r.H)) <= 0.15f);
        // Gegenprobe: die Szene ohne eigene Gebaeude kennt keine
        Assert.Empty(TuerSetzen.ModellTuerMitten(p.SzeneOhneLaternen(p.AussenStand)!, D.Grafik, r));
    }

    /// <summary>
    /// Stadt-Modell: eine auf eine CC0-Tuer eingerastete Tuer ist die Tuer des Modells, keine Kastentuer darueber. Ort ohne
    /// Versatz (erster Ort der Stadt 1 mit CC0-Tuer ausserhalb von darstellung.modell_tuer): ueber die neue Regel im Spiel,
    /// Gegenprobe bis A 0.54.0 Kastentuer auf der (umgefaerbten) CC0-Tuer. Ort mit Versatz (Moebelhaus, Tuer im Norden): der
    /// Versatz bleibt, sonst rueckte das Modell zur Seite (Gegenprobe: Punkt nicht eingerastet = Versatz weg).
    /// </summary>
    [Fact]
    public void StadtModell_EingerasteteCc0Tuer_KeineZweite_Gegenprobe()
    {
        var p = Projekt.Neu("Moebel", D, Daten.NeueAblage());
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        var szene = p.SzeneOhneLaternen(p.AussenStand)!;
        var gefunden = false;
        foreach (var ort in szene.Orte.Select(o => o.Id).Where(o => StadtTueren.ModellVersatz(Stadt(p), o) is null))
        {
            var s = new AussenStelle(AussenArt.Haus, -1, 0, ort);
            var r = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
            var mitten = TuerSetzen.ModellTuerMitten(szene, D.Grafik, r);
            if (mitten.Count == 0)
            {
                continue;
            }
            var m = mitten[0];
            Assert.Null(p.AussenAendern("Tuer", a => TuerSetzen.Setzen(a, s, m, [m])));
            var t = Assert.Single(Szene(p).Tueren, t => t.Ort == ort);
            Assert.True(t.Modell, ort);
            Assert.Equal(0, Kastenteile(Szene(p), t));
            StadtTueren.EigeneTuerAltFuerTests = true;
            try
            {
                var alt = Szene(p);
                var ta = Assert.Single(alt.Tueren, t => t.Ort == ort);
                Assert.False(ta.Modell);
                Assert.True(Kastenteile(alt, ta) > 0);
            }
            finally
            {
                StadtTueren.EigeneTuerAltFuerTests = false;
            }
            gefunden = true;
            break;
        }
        Assert.True(gefunden, "kein Ort mit CC0-Tuer ohne Versatz");
        // Moebelhaus mit Versatz
        var mh = new AussenStelle(AussenArt.Haus, -1, 0, "moebelhaus");
        var rm = AussenBearbeitung.HausFlaeche(p.AussenStand, mh);
        var nord = TuerSetzen.ModellTuerMitten(szene, D.Grafik, rm).First(t => MathF.Abs(t.Y - (float)rm.Y) <= 0.4f);
        Assert.Null(p.AussenAendern("Tuer", a => TuerSetzen.Setzen(a, mh, nord, [nord])));
        Assert.True(TuerSetzen.Tuer(p.AussenStand, mh) is { } tm && Vector2.Distance(tm, nord) <= 0.01f);
        Assert.True(StadtTueren.ModellVersatz(Stadt(p), "moebelhaus") is not null);
        var tmh = Assert.Single(Szene(p).Tueren, t => t.Ort == "moebelhaus");
        Assert.True(tmh.Modell);
        Assert.Equal(0, Kastenteile(Szene(p), tmh));
        // Gegenprobe: 0,3 daneben (nicht eingerastet) faellt der Versatz weg wie in P12-121
        Assert.Null(p.AussenAendern("Tuer", a => TuerSetzen.Setzen(a, mh, nord + new Vector2(0.3f, 0))));
        Assert.Null(StadtTueren.ModellVersatz(Stadt(p), "moebelhaus"));
    }

    private static Stadt Stadt(Projekt p)
    {
        var f = p.Stand();
        return f.StadtFuer(f.MitKarte(D.Inhalt));
    }

    /// <summary>
    /// Fassung 0 ohne neuen Fund: ihre gezeichneten Tueren bleiben ohne Fund, solange sie wie in der Ausgangsstadt liegen (E1).
    /// Gegenprobe: alle Tueren streng geprueft = genau diese 4 (Bahnhof Talheim und Gewerbehof (im Probestand „Probe“) mit
    /// Vorsprung am Rand, Kaufhaus mit Nische, Stellwerk mit Kante; gemeldet in P12-82).
    /// </summary>
    [Fact]
    public void Fassung0_OhneNeuenFund_GegenprobeStrengVier()
    {
        var p = Projekt.Neu("Stadt 1", D, Daten.NeueAblage());
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        var neu = p.PruefenAussen().Where(f => f.Text.Contains("sitzt nicht sauber", StringComparison.Ordinal)).Select(f => f.Text).ToList();
        Assert.True(neu.Count == 0, string.Join(" / ", neu));
        TuerBuendig.AuchAusgangFuerTests.Value = true;
        try
        {
            var streng = p.PruefenAussen().Where(f => f.Rot && f.Text.Contains("sitzt nicht sauber", StringComparison.Ordinal)).Select(f => f.Text).ToList();
            Assert.Equal("Bahnhof Talheim|Kaufhaus|Probe|stellwerk", string.Join("|", streng.Select(t => t[(t.IndexOf('„') + 1)..t.IndexOf('“')]).Order(StringComparer.Ordinal)));
        }
        finally
        {
            TuerBuendig.AuchAusgangFuerTests.Value = false;
        }
        // Spiel: Fassung 0 zeichnet dieselben Tueren wie vorher (keine wird zur Modell-Tuer)
        var sz = Szene(p);
        StadtTueren.EigeneTuerAltFuerTests = true;
        try
        {
            Assert.Equal(Szene(p).Tueren.Select(t => (t.Ort, t.Modell, t.Mitte)), sz.Tueren.Select(t => (t.Ort, t.Modell, t.Mitte)));
        }
        finally
        {
            StadtTueren.EigeneTuerAltFuerTests = false;
        }
    }

    private static List<string> TuerFunde(Projekt p) =>
        [.. p.PruefenAussen().Where(f => f.Rot && f.Text.Contains("sitzt nicht sauber", StringComparison.Ordinal)).Select(f => f.Text)];

    /// <summary>
    /// Start-Test S 0.28.0: ein Blueprint vom Moebelhaus (Ort mit Tuerversatz <c>modell_tuer</c>) setzte das Modell ohne
    /// Versatz, die Tuer lag 0,x neben der CC0-Tuer (Kastentuer in der Fassade). Jetzt rueckt die Tuer beim Speichern mit
    /// und liegt in der leeren Stadt auf der eigenen Tuer des Modells. Gegenprobe: Tuer ohne Verschieben ist rot.
    /// </summary>
    [Fact]
    public void BlueprintVomMoebelhaus_TuerAufDerModelltuer_GegenprobeOhneVersatzRot()
    {
        var quelle = Projekt.Neu("Quelle", D, Daten.NeueAblage());
        var v = Blueprint.TuerVersatz(quelle.AussenStand, "moebelhaus");
        Assert.NotNull(v);
        var b = Blueprint.Aus(quelle, new AussenStelle(AussenArt.Haus, -1, 0, "moebelhaus"), "Moebel")!;
        var p = Leer("Moebel leer");
        string? grund = null;
        Assert.NotNull(p.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, 18, 12, out grund)));
        Assert.Null(grund);
        Assert.True(Assert.Single(Szene(p).Tueren).Modell);
        Assert.Empty(TuerFunde(p));
        // Gegenprobe: Tuer an der Stelle ohne Versatz (wie bis S 0.27.0)
        b.Tuer![0] = Py.F(b.Tuer[0]) + v!.Value;
        var w0 = (List<object?>)b.Weg[0]!;
        w0[0] = Py.F(w0[0]) + v.Value;
        var q = Leer("Moebel alt");
        Assert.NotNull(q.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, 18, 12, out grund)));
        Assert.NotEmpty(TuerFunde(q));
    }

    /// <summary>
    /// Start-Test S 0.28.0: „Aussehen tauschen“ Talheim-Nord auf das Stellwerk setzt dessen Tuer an dieselbe Stelle wie in
    /// der Ausgangsstadt (eine der 4 streng nicht buendigen Tueren der Fassung 0): sieht aus wie dort, also ohne Fund (E1).
    /// Gegenprobe: streng (auch Ausgang) rot.
    /// </summary>
    [Fact]
    public void AussehenStellwerk_TuerWieInDerAusgangsstadt_OhneFund_GegenprobeStreng()
    {
        var p = Projekt.Neu("Aussehen", D, Daten.NeueAblage());
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        p.AussenAendern("Stellwerk weg", a => AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Gebaeude,
            Aussen.Landschaft(a).FindIndex(t => t is PyObjekt g && Py.Gleich(g["typ"], "gebaeude") && Py.Gleich(g["modell"], "stellwerk")))));
        var vorlage = OrtAussehen.Vorlagen(p.AussenStand, Aussen.Aus(p.Basis), "talheim_nord", []).Single(x => x.Blueprint.Modell == "stellwerk");
        var r = p.AussenAendern("Aussehen tauschen", a => OrtAussehen.Tauschen(a, "talheim_nord", vorlage.Blueprint, Aussen.Aus(p.Basis)));
        Assert.Null(r.Grund);
        Assert.DoesNotContain(TuerFunde(p), t => t.Contains("Talheim-Nord", StringComparison.Ordinal));
        TuerBuendig.AuchAusgangFuerTests.Value = true;
        try
        {
            Assert.Contains(TuerFunde(p), t => t.Contains("Talheim-Nord", StringComparison.Ordinal));
        }
        finally
        {
            TuerBuendig.AuchAusgangFuerTests.Value = false;
        }
    }
}
