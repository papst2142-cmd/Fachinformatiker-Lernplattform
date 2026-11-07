using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.8.0 / A 0.43.4 (Weisung P12-47, Plan P12-26b Abschnitt 10): Teiche, Stege und Eis im gemeinsamen Teil (Szene,
/// Netz, Laufraster) und das Bearbeiten im Werkzeug (setzen, Eckpunkte ziehen, dazu und weg, drehen, kopieren, loeschen).
/// </summary>
public class WasserTests
{
    private static Spieldaten D => Daten.Spiel;

    private static List<object?> P(double x, double y) => [x, y];

    /// <summary>Stadt 1 mit Probeteich (22,5 bis 25,5 / 1,5 bis 3,5) und Steg vom Ufer hinein; optional Eis.</summary>
    private static PyObjekt MitTeich(bool eis = false)
    {
        var a = Aussen.Aus(D.Fassung);
        var w = Aussen.WasserAnlegen(a);
        Liste(w["teiche"]).Add(new PyObjekt { { "punkte", new List<object?> { P(22.5, 1.5), P(25.5, 1.5), P(25.5, 3.5), P(22.5, 3.5) } } });
        Liste(w["stege"]).Add(new PyObjekt { { "x", 23.2 }, { "y", 2.5 }, { "w", 1.5 }, { "t", 0.5 }, { "dreh", 0L } });
        if (eis)
        {
            Py.Setzen(w, "eis", true);
        }
        return a;
    }

    private static StadtSzene Szene(PyObjekt a, Jahreszeit j = Jahreszeit.Sommer)
    {
        var f = Aussen.Fassung(a, D.Fassung, D.Fassung.Innenraeume, 1, 0, "test", "", "");
        var stadt = f.StadtFuer(f.MitKarte(D.Inhalt));
        return StadtSzene.Bauen(stadt, null, D.Grafik, jahreszeit: j);
    }

    private static bool HatFarbe(Netz n, Vector3 farbe)
    {
        for (var e = 0; e < n.AnzahlEcken; e++)
        {
            if (Vector3.DistanceSquared(n.FarbeAn(e), farbe) < 1e-6f)
            {
                return true;
            }
        }
        return false;
    }

    [Fact]
    public void Fassung0_OhneWasser_SzeneUnveraendert()
    {
        var s = Szene(Aussen.Aus(D.Fassung));
        Assert.Empty(s.Teiche);
        Assert.Empty(s.Stege);
        Assert.Empty(s.Flaechen);
        Assert.False(s.Eis);
    }

    [Fact]
    public void Teich_Rund_GegenDenUhrzeigersinn_InDenEckpunkten_DreieckeVollstaendig()
    {
        var t = new Teich([new(0, 0), new(0, 2), new(3, 2), new(3, 0)]);   // im Uhrzeigersinn gesetzt
        Assert.True(Wasser.Flaeche2(t.Rund) > 0);
        Assert.All(t.Rund, p => Assert.True(p.X is >= -1e-4f and <= 3.0001f && p.Y is >= -1e-4f and <= 2.0001f));
        // gerundete Ecken: die Ecke (0, 0) selbst liegt nicht mehr im Wasser, die Mitte schon
        Assert.False(Wasser.Drin(t.Rund, new Vector2(0.02f, 0.02f)));
        Assert.True(Wasser.Drin(t.Rund, new Vector2(1.5f, 1f)));
        // Ufer 0,15 rundum
        Assert.True(Wasser.Drin(t.MitUfer, new Vector2(1.5f, -0.1f)));
        Assert.False(Wasser.Drin(t.MitUfer, new Vector2(1.5f, -0.2f)));
        // Dreiecke decken die Flaeche ganz ab (n - 2 Dreiecke, Summe der Flaechen = Flaeche)
        var d = Wasser.Dreiecke(t.Rund);
        Assert.Equal(t.Rund.Count - 2, d.Count);
        var summe = d.Sum(x => Wasser.Flaeche2([t.Rund[x.A], t.Rund[x.B], t.Rund[x.C]]));
        Assert.Equal(Wasser.Flaeche2(t.Rund), summe, 3);
        // feste Formen als Hilfe (Frage 7 = b bliebe klein): Punkte fuer eckig und rund
        Assert.Equal(4, Wasser.FormPunkte(Wasser.Form.Eckig, 5, 5, 3, 2).Count);
        Assert.Equal(8, Wasser.FormPunkte(Wasser.Form.Rund, 5, 5, 3, 2).Count);
    }

    [Fact]
    public void Laufraster_TeichUndUferGesperrt_StegFrei()
    {
        var mit = Szene(MitTeich()).Laufraster;
        var ohne = Szene(Aussen.Aus(D.Fassung)).Laufraster;
        // Mitte des Teichs und das Ufer: vorher frei, jetzt gesperrt
        Assert.True(ohne.KannStehen(24.6, 2.5));
        Assert.False(mit.KannStehen(24.6, 2.5));
        Assert.False(mit.KannStehen(25.6, 2.0));
        // auf dem Steg frei, auch sein Ende im Wasser; neben dem Teich frei
        Assert.True(mit.KannStehen(23.6, 2.5));
        Assert.True(mit.KannStehen(26.2, 2.5));
        // die Figur kommt bis ans Ende des Stegs (vom Land ueber den Steg)
        Assert.NotEmpty(mit.Weg((20.5, 2.5), (23.8, 2.5)));
    }

    [Fact]
    public void Netz_TeichUndSteg_ImNetzDerStadt_KeinNeuesNetz()
    {
        var ohne = Szene(Aussen.Aus(D.Fassung)).Vorbereiten();
        var mit = Szene(MitTeich()).Vorbereiten();
        var mehr = (mit.Stadt.Indizes.Count - ohne.Stadt.Indizes.Count) / 3;
        // Ufer und Wasser je rund 18 Dreiecke, Steg 2 Pfostenreihen und eine Platte (Plan: Teich rund 200, Steg rund 60)
        Assert.InRange(mehr, 30, 400);
        Assert.Equal(ohne.Haeuser.Count, mit.Haeuser.Count);
        Assert.Equal(ohne.Gruen is null, mit.Gruen is null);
        Assert.Equal(ohne.PaketNah is null, mit.PaketNah is null);
        Assert.Equal(ohne.PaketFern is null, mit.PaketFern is null);
        Assert.True(HatFarbe(mit.Stadt, Wasser.StegHolz));
        Assert.False(HatFarbe(ohne.Stadt, Wasser.StegHolz));
    }

    [Fact]
    public void Eis_NurImWinter_NurMitSchalter()
    {
        Assert.True(HatFarbe(Szene(MitTeich(eis: true), Jahreszeit.Winter).NetzBauen(), StadtSzene.EisFarbe));
        // Gegenproben: Winter ohne Schalter, Sommer mit Schalter, Fassung 0 im Winter
        Assert.False(HatFarbe(Szene(MitTeich(), Jahreszeit.Winter).NetzBauen(), StadtSzene.EisFarbe));
        Assert.False(HatFarbe(Szene(MitTeich(eis: true)).NetzBauen(), StadtSzene.EisFarbe));
        Assert.False(HatFarbe(Szene(Aussen.Aus(D.Fassung), Jahreszeit.Winter).NetzBauen(), StadtSzene.EisFarbe));
        // auch der Fluss friert zu: Stadt 1 ohne Teich, nur mit dem Schalter
        var nurEis = Aussen.Aus(D.Fassung);
        AussenBearbeitung.EisSetzen(nurEis, true);
        Assert.True(HatFarbe(Szene(nurEis, Jahreszeit.Winter).NetzBauen(), StadtSzene.EisFarbe));
        AussenBearbeitung.EisSetzen(nurEis, false);
        Assert.False(Py.Hat(Objekt(Aussen.Stadtdaten(nurEis)[Wasser.Abschnitt]), "eis"));
    }

    [Fact]
    public void Werkzeug_SetzenZiehenEckpunkteDrehenKopierenLoeschen()
    {
        var a = Aussen.Aus(Aussen.Leer(D.Fassung, "neu_wasser", 24, 16));
        var t = AussenBearbeitung.Setzen(a, AussenArt.Teich, 6.2, 5.9)!;
        Assert.Equal(new AussenStelle(AussenArt.Teich, 0, -1), t);
        Assert.Equal(4, AussenBearbeitung.EckpunkteWerte(a, t).Count);
        Assert.Equal(t, AussenBearbeitung.StueckBei(a, 6, 6));
        // Eckpunkt greifen und ziehen: nur er bewegt sich
        var ecke = AussenBearbeitung.StueckBei(a, 4.5, 5.0)!;
        Assert.Equal(new AussenStelle(AussenArt.Teich, 0, 0), ecke);
        AussenBearbeitung.Verschieben(a, ecke, -1, -1);
        var p = AussenBearbeitung.EckpunkteWerte(a, t);
        Assert.Equal(new Vector2(3.5f, 4f), p[0]);
        Assert.Equal(new Vector2(7.5f, 5f), p[1]);
        // dazu und weg
        var neu = AussenBearbeitung.EckpunktDazu(a, ecke)!;
        Assert.Equal(1, neu.Punkt);
        Assert.Equal(5, AussenBearbeitung.EckpunkteWerte(a, t).Count);
        Assert.True(AussenBearbeitung.EckpunktWeg(a, neu));
        Assert.True(AussenBearbeitung.EckpunktWeg(a, ecke));
        Assert.Equal(3, AussenBearbeitung.EckpunkteWerte(a, t).Count);
        Assert.NotNull(AussenBearbeitung.EckpunktWegNicht(a, ecke));
        Assert.False(AussenBearbeitung.EckpunktWeg(a, ecke));
        // runder Teich (feste Form als Hilfe), Steg, Fluss
        var rund = AussenBearbeitung.Setzen(a, AussenArt.Teich, 18, 4, true, "rund")!;
        Assert.Equal(8, AussenBearbeitung.EckpunkteWerte(a, rund).Count);
        var steg = AussenBearbeitung.Setzen(a, AussenArt.Steg, 12, 12)!;
        Assert.Equal(steg, AussenBearbeitung.StueckBei(a, 12.8, 12.1));
        Assert.True(AussenBearbeitung.Drehen(a, steg));
        Assert.True(Wasser.Steg(Aussen.Stege(a)[0])!.Quer);
        var fluss = AussenBearbeitung.Setzen(a, AussenArt.Fluss, 12, 8)!;
        Assert.Equal(3, AussenBearbeitung.EckpunkteWerte(a, fluss).Count);
        Assert.Equal(fluss, AussenBearbeitung.StueckBei(a, 10, 8.3));
        Assert.Equal(fluss with { Punkt = 1 }, AussenBearbeitung.StueckBei(a, 12, 8.1));
        // kopieren und loeschen
        var kopie = AussenBearbeitung.Kopieren(a, ecke)!;
        Assert.Equal(new AussenStelle(AussenArt.Teich, 2, -1), kopie);
        Assert.True(AussenBearbeitung.Loeschen(a, kopie));
        Assert.Equal(2, Aussen.Teiche(a).Count);
        Assert.False(AussenBearbeitung.Gueltig(a, kopie));
        // Beschreibung und Umriss (3D-Schatten) fuer jede Art
        foreach (var s in new[] { t, ecke, steg, fluss, fluss with { Punkt = 2 } })
        {
            Assert.NotEmpty(Bauen3D.Umriss(a, s));
            Assert.False(string.IsNullOrEmpty(AussenBearbeitung.Beschreibung(a, s, x => x)));
        }
    }

    [Fact]
    public void Vorschau3D_GleichDemEchtenSchritt()
    {
        var p = Projekt.Neu("Wasser3D", D, Daten.NeueAblage(), leer: (24, 16));
        var vorher = PythonJson.Text(p.AussenStand);
        var v = Bauen3D.Setzen(p.AussenStand, AussenArt.Teich, 8, 8, true, null, 1);
        Assert.Equal(vorher, PythonJson.Text(p.AussenStand));
        Assert.NotEmpty(v.Umriss);
        p.AussenAendern("Setzen", a =>
        {
            var s = AussenBearbeitung.Setzen(a, AussenArt.Teich, 8, 8, true, null);
            AussenBearbeitung.Drehen(a, s!);
        });
        Assert.Equal(PythonJson.Text(p.AussenStand), PythonJson.Text(v.Stand));
        var ecke = AussenBearbeitung.StueckBei(p.AussenStand, 7, 6.5)!;
        Assert.True(ecke.Punkt >= 0);
        var w = Bauen3D.Verschieben(p.AussenStand, ecke, 0.5, -0.5, true);
        p.AussenAendern("Verschieben", a => AussenBearbeitung.Verschieben(a, ecke, 0.5, -0.5, true));
        Assert.Equal(PythonJson.Text(p.AussenStand), PythonJson.Text(w.Stand));
    }
}
