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
/// Weisung P12-214 (Nico 12.11.2026): (1) die Bruecke umrahmt auch den Gehweg; (2) Talheim-Nord wird das Stellwerk
/// („Stellwerk Talheim-Nord“, kein Gleis noetig) und das Geruest des alten Bahnhof-Modells ist an Gebaeuden ohne Ort weg.
/// </summary>
public class BrueckeTalheimTests
{
    private static Spieldaten D => Daten.Spiel;

    /// <summary>Stadt 1 mit Gehweg an der Strasse der ersten Bruecke; Rueckgabe Projekt, Bruecke, Gehwegbreite.</summary>
    private static (Projekt P, Vector2 Mitte, float Gehweg) Stadt1MitGehwegAnDerBruecke(string name)
    {
        var p = Projekt.Neu(name, D, Daten.NeueAblage());
        var vorher = p.SzeneOhneLaternen(p.AussenStand)!;
        var mitte = vorher.Bruecken[0];
        p.AussenAendern("Gehweg", a =>
        {
            var strasse = Aussen.Landschaft(a).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "strasse"))
                .OrderBy(t => Kurve.Abstand(mitte, Vektoren(t["punkte"]))).First();
            Py.Setzen(strasse, "gehweg", Bearbeitung.Zahl(AussenBearbeitung.NeuerGehweg));
        });
        return (p, mitte, (float)AussenBearbeitung.NeuerGehweg);
    }

    private static List<Vector2> Vektoren(object? punkte) =>
        [.. ((List<object?>)punkte!).OfType<List<object?>>().Select(q => new Vector2((float)Py.F(q[0]), (float)Py.F(q[1])))];

    [Fact]
    public void Bruecke_UmrahmtGehweg_BegehbarOhneLaterneDarauf_Gegenprobe()
    {
        var (p, mitte, gw) = Stadt1MitGehwegAnDerBruecke("Bruecke");
        var sz = p.SzeneOhneLaternen(p.AussenStand)!;
        var kasten = sz.BrueckenKaesten.First(b => Vector2.Distance(b.Mitte, mitte) < 0.01f);
        Assert.Equal((StadtSzene.StrassenBreite / 2) + gw + 0.1f, kasten.HalbBreite, 0.0001f);
        // Mitte des Gehwegs auf der Bruecke: begehbar (der Gehweg liegt jetzt zwischen den Bruestungen)
        var quer = new Vector2(-kasten.Richtung.Y, kasten.Richtung.X);
        foreach (var seite in (float[])[-1f, 1f])
        {
            var aufGehweg = mitte + (quer * seite * ((StadtSzene.StrassenBreite / 2) + (gw / 2)));
            Assert.True(sz.Laufraster.KannStehen(aufGehweg.X, aufGehweg.Y), "Gehweg auf der Bruecke nicht begehbar, Seite " + seite);
        }
        // keine Laterne (auch keine automatische) auf der breiteren Bruecke samt Bruestung
        p.LaternenVerteilen();
        Assert.DoesNotContain(Aussen.LaternenWerte(p.AussenStand), l =>
        {
            var r = new Vector2(l.X, l.Z) - kasten.Mitte;
            return MathF.Abs(Vector2.Dot(r, kasten.Richtung)) <= kasten.HalbLaenge && MathF.Abs(Vector2.Dot(r, quer)) <= kasten.HalbBreite;
        });
        // Gegenprobe: alte schmale Bruecke (Bruestung an der Fahrbahnkante, Gehweg aussen vorbei)
        StadtSzene.BrueckeSchmalAltFuerTests = true;
        try
        {
            var (q, _, _) = Stadt1MitGehwegAnDerBruecke("Bruecke alt");
            var alt = q.SzeneOhneLaternen(q.AussenStand)!.BrueckenKaesten.First(b => Vector2.Distance(b.Mitte, mitte) < 0.01f);
            Assert.Equal((StadtSzene.StrassenBreite / 2) + 0.1f, alt.HalbBreite, 0.0001f);
        }
        finally
        {
            StadtSzene.BrueckeSchmalAltFuerTests = false;
        }
    }

    [Fact]
    public void Bruecke_OhneGehweg_WieBisher()
    {
        var p = Projekt.Neu("Stadt 1", D, Daten.NeueAblage());
        var sz = p.SzeneOhneLaternen(p.AussenStand)!;
        Assert.NotEmpty(sz.BrueckenKaesten);
        Assert.All(sz.BrueckenKaesten, b => Assert.Equal((StadtSzene.StrassenBreite / 2) + 0.1f, b.HalbBreite));
    }

    [Fact]
    public void Stellwerk_WirdStellwerkTalheimNord_VorschlagZuerst_Gegenprobe()
    {
        var p = TorEingangTests.Nico();
        var s = TorEingangTests.Gebaeude(p, "stellwerk");
        Assert.True(s.Nummer >= 0);
        if (Blueprint.ZierwegVon(p.AussenStand, s) is null)
        {
            TorEingangTests.TuerDazu(p, TorEingangTests.Gebaeude(p, "stellwerk"));
        }
        s = TorEingangTests.Gebaeude(p, "stellwerk");
        var liste = Spielort.FehlendeFuer(p.AussenStand, s, D.Fassung);
        Assert.Equal("talheim_nord", Py.Str(liste[0]["id"]));
        Assert.True(Spielort.Vorschlag(p.AussenStand, s, liste[0]));
        Assert.Equal("Stellwerk Talheim-Nord", Spielort.Name(liste[0]));
        // Gegenprobe: am Moebelhaus kein Vorschlag, Reihenfolge der Stadt 1
        var m = TorEingangTests.Gebaeude(p, "moebelhaus");
        Assert.DoesNotContain(Spielort.FehlendeFuer(p.AussenStand, m, D.Fassung), o => Spielort.Vorschlag(p.AussenStand, m, o));
        Assert.Equal(Spielort.Fehlende(p.AussenStand, D.Fassung).Select(o => Py.Str(o["id"])),
            Spielort.FehlendeFuer(p.AussenStand, m, D.Fassung).Select(o => Py.Str(o["id"])));

        string? grund = null;
        p.AussenAendern("Spielort", a => grund = Spielort.Festlegen(a, s, "talheim_nord", D.Fassung));
        Assert.Null(grund);
        var ort = AussenBearbeitung.Ort(p.AussenStand, "talheim_nord")!;
        Assert.Equal("Stellwerk Talheim-Nord", Py.Str(ort["name"]));
        Assert.Equal("stellwerk", Py.Str(ort["modell"]));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Text.Contains("Gleis", StringComparison.Ordinal) && f.Text.Contains("Talheim", StringComparison.Ordinal));
    }

    /// <summary>Gebaeude ohne Ort mit einer Kopie des Talheim-Nord-Modells (wie Nicos „Talheim-Nord 2“) weit weg vom Gleis.</summary>
    private static Projekt MitTalheimKopie(string name)
    {
        var p = TorEingangTests.Nico(name);
        var vorlage = Py.Kopie(Objekt(Objekt(D.Inhalt["orte"])["modelle"])["bahnhof_neu"]);
        p.AussenAendern("Talheim-Nord 2", a =>
        {
            var karte = Aussen.Karte(a);
            if (Get(karte, "modelle") is not PyObjekt modelle)
            {
                modelle = new PyObjekt();
                Py.Setzen(karte, "modelle", modelle);
            }
            Py.Setzen(modelle, "bahnhof_neu_2", vorlage);
            Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "gebaeude" }, { "modell", "bahnhof_neu_2" }, { "x", 20L }, { "y", 20L } });
        });
        return p;
    }

    private static int HausTeile(Projekt p)
    {
        var sz = p.SzeneOhneLaternen((PyObjekt)Py.Kopie(p.AussenStand)!)!;
        var h = sz.Haeuser.First(x => x.Id.StartsWith("bahnhof_neu_2#", StringComparison.Ordinal));
        return h.TeileBis - h.TeileVon;
    }

    [Fact]
    public void TalheimNordKopie_OhneGeruest_OhneGleisFund_Gegenprobe()
    {
        var p = MitTalheimKopie("Talheim-Nord Kopie");
        var modelle = AussenBearbeitung.Modelle(p.AussenStand);
        var k = StadtModelle.Kontext(null, D.Inhalt);
        var alle = StadtModelle.Teile(modelle, "bahnhof_neu_2", k);
        var sichtbar = StadtSzene.OhneGeruest(alle);
        Assert.DoesNotContain(sichtbar, t => Py.Gleich(t.Teil["art"], "geruest"));
        // nur das Geruest faellt weg, sonst gleiches Aussehen (keine Teile der fertigen Form)
        Assert.Equal(alle.Count - 1, sichtbar.Count);
        Assert.DoesNotContain(sichtbar, t => Py.Hat(t.Teil, "erledigt"));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Text.Contains("steht nicht an einem Gleis", StringComparison.Ordinal));
        var neu = HausTeile(p);

        // Gegenprobe: bis S 0.44.1 mit Geruest (Aufgabe noch offen) und gelbem Fund am Gleis
        StadtSzene.GeruestAltFuerTests = true;
        ModellNamen.TalheimAltFuerTests = true;
        try
        {
            Assert.Contains(StadtSzene.OhneGeruest(StadtModelle.Teile(modelle, "bahnhof_neu_2", k)), t => Py.Gleich(t.Teil["art"], "geruest"));
            var q = MitTalheimKopie("Talheim-Nord Kopie alt");
            Assert.Contains(q.PruefenAussen(), f => f.Text.Contains("steht nicht an einem Gleis", StringComparison.Ordinal));
            Assert.NotEqual(neu, HausTeile(q));
        }
        finally
        {
            StadtSzene.GeruestAltFuerTests = false;
            ModellNamen.TalheimAltFuerTests = false;
        }
    }

    [Fact]
    public void Stadt1_OrtTalheimNord_BehaeltGeruest()
    {
        // der Ort Talheim-Nord der Fassung 0 bleibt wie bisher (Geruest bis zur Abnahme); nur mit Nicos OK aendern
        var modelle = StadtModelle.Modelle(D.Inhalt, null);
        var k = StadtModelle.Kontext(null, D.Inhalt);
        Assert.Contains(StadtModelle.Teile(modelle, "bahnhof_neu", k), t => Py.Gleich(t.Teil["art"], "geruest"));
    }
}
