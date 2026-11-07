using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.7.0 (Weisung P12-45, Plan P12-26b Abschnitt 9): Bauen in der 3D-Ansicht ohne Oberflaeche. Treffer mit dem
/// Strahl (Fassade vor Boden), Umrisse fuer den Schatten, Vorschau auf einer Kopie (gleich dem echten Schritt, das
/// Original bleibt), gruen/rot nach denselben Funden wie die Liste rechts, Kippen und „Ganze Stadt zeigen“.
/// </summary>
public class Bauen3DTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Leer(string name)
    {
        var p = Projekt.Neu(name, D, Daten.NeueAblage(), leer: (24, 16));
        p.AussenAendern("Straße", a => AussenBearbeitung.Setzen(a, AussenArt.Strasse, 12, 8));
        return p;
    }

    [Fact]
    public void Kippen_BleibtInDenGrenzen_SpielNeigungGleichDerKamera()
    {
        Assert.Equal(Bauen3D.NeigungSpiel, new Kamera(0, 0, 5).Neigung);
        Assert.Equal(60f, Bauen3D.Kippen(52, 8));
        Assert.Equal(Bauen3D.NeigungMax, Bauen3D.Kippen(80, 20));
        Assert.Equal(Bauen3D.NeigungMin, Bauen3D.Kippen(30, -20));
        // die Kamera rechnet mit der neuen Neigung (Auge hoeher bei steilerem Blick)
        var k = new Kamera(10, 10, 10);
        var flach = k.Auge.Y;
        k.Neigung = Bauen3D.Kippen(k.Neigung, 20);
        Assert.True(k.Auge.Y > flach);
    }

    [Fact]
    public void Strahl_Schnitt_UndBoden()
    {
        var start = new Vector3(0, 5, 0);
        var runter = Vector3.Normalize(new Vector3(1, -1, 0));
        Assert.Equal(new Vector2(5, 0), Bauen3D.Boden(start, runter));
        Assert.Null(Bauen3D.Boden(start, Vector3.Normalize(new Vector3(1, 1, 0))));
        var t = Bauen3D.Schnitt(start, runter, new Vector3(2, 0, -1), new Vector3(3, 2, 1));
        Assert.NotNull(t);
        var p = start + (runter * t!.Value);
        Assert.Equal(2f, p.Y, 3);
        Assert.Equal(3f, p.X, 3);
        Assert.Null(Bauen3D.Schnitt(start, runter, new Vector3(2, 0, 5), new Vector3(3, 2, 6)));
    }

    [Fact]
    public void Treffer_FassadeVorBoden_GegenprobeOhneHaeuser()
    {
        var p = Projekt.Neu("Treffer", D, Daten.NeueAblage());
        var a = p.AussenStand;
        var haus = AussenBearbeitung.Alle(a).First(z => z.Stelle.Art == AussenArt.Haus).Stelle;
        var f = AussenBearbeitung.HausFlaeche(a, haus);
        var haeuser = Bauen3D.Haeuser(a);
        // schraeger Blick von vorn auf die Wand: der Strahl trifft die Fassade, der Bodenpunkt liegt hinter dem Haus
        var ziel = new Vector3((float)(f.X + (f.W / 2)), 0.5f, (float)(f.Y + f.H));
        var start = ziel + new Vector3(0, 1, 1.5f);
        var richtung = Vector3.Normalize(ziel - start);
        var boden = Bauen3D.Boden(start, richtung)!.Value;
        Assert.True(boden.Y < f.Y + f.H, "Bodenpunkt liegt nicht hinter der Hauswand (Probe taugt nicht)");
        Assert.Equal(haus, Bauen3D.Treffer(a, start, richtung, haeuser, 0.3));
        // Gegenprobe: ohne Quader (nur Boden wie die Draufsicht) trifft derselbe Strahl das Haus nicht ueber die Wand
        var nurBoden = Bauen3D.Treffer(a, start, richtung, [], 0.3);
        Assert.Equal(AussenBearbeitung.StueckBei(a, boden.X, boden.Y, 0.3), nurBoden);
    }

    [Fact]
    public void Treffer_VonObenWieDieDraufsicht()
    {
        var p = Leer("Oben");
        p.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 4, 4));
        var a = p.AussenStand;
        var baum = AussenBearbeitung.StueckBei(a, 4, 4, 0.3);
        Assert.NotNull(baum);
        Assert.Equal(baum, Bauen3D.Treffer(a, new Vector3(4, 20, 4), -Vector3.UnitY, Bauen3D.Haeuser(a), 0.3));
        Assert.Null(Bauen3D.Treffer(a, new Vector3(20, 20, 2), -Vector3.UnitY, Bauen3D.Haeuser(a), 0.3));
        // Blick in den Himmel trifft nichts
        Assert.Null(Bauen3D.Treffer(a, new Vector3(4, 2, 4), Vector3.UnitY, Bauen3D.Haeuser(a), 0.3));
    }

    [Fact]
    public void Vorschau_GleichDemEchtenSchritt_OriginalBleibt()
    {
        var p = Leer("Vorschau");
        var vorher = PythonJson.Text(p.AussenStand);
        var v = Bauen3D.Setzen(p.AussenStand, AussenArt.Baum, 4.2, 3.9, true, null, 0);
        Assert.Equal(vorher, PythonJson.Text(p.AussenStand));
        Assert.NotNull(v.Stelle);
        Assert.Single(v.Umriss);
        p.AussenAendern("Setzen", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 4.2, 3.9, true, null));
        Assert.Equal(PythonJson.Text(p.AussenStand), PythonJson.Text(v.Stand));

        // Verschieben: dasselbe
        var baum = v.Stelle!;
        var vorher2 = PythonJson.Text(p.AussenStand);
        var w = Bauen3D.Verschieben(p.AussenStand, baum, 2, 1, true);
        Assert.Equal(vorher2, PythonJson.Text(p.AussenStand));
        p.AussenAendern("Verschieben", a => AussenBearbeitung.Verschieben(a, baum, 2, 1, true));
        Assert.Equal(PythonJson.Text(p.AussenStand), PythonJson.Text(w.Stand));

        // Strasse gedreht gesetzt: Umriss senkrecht (wie zwei Vierteldrehungen = waagerecht)
        var s = Bauen3D.Setzen(p.AussenStand, AussenArt.Strasse, 20, 8, true, null, 1);
        Assert.NotEmpty(s.Umriss);
        var eck = s.Umriss.SelectMany(u => u).ToList();
        Assert.True(eck.Max(e => e.Y) - eck.Min(e => e.Y) > eck.Max(e => e.X) - eck.Min(e => e.X));
    }

    [Fact]
    public void Umriss_HausWieDieGrundflaeche_StrasseJeAbschnitt()
    {
        var p = Projekt.Neu("Umriss", D, Daten.NeueAblage());
        var a = p.AussenStand;
        var haus = AussenBearbeitung.Alle(a).First(z => z.Stelle.Art == AussenArt.Haus).Stelle;
        var f = AussenBearbeitung.HausFlaeche(a, haus);
        var u = Assert.Single(Bauen3D.Umriss(a, haus));
        Assert.Equal(new Vector2((float)f.X, (float)f.Y), u[0]);
        Assert.Equal(new Vector2((float)(f.X + f.W), (float)(f.Y + f.H)), u[2]);
        var strasse = AussenBearbeitung.Alle(a).First(z => z.Stelle.Art == AussenArt.Strasse).Stelle;
        var punkte = (List<object?>)((Lernplattform.Core.Daten.PyObjekt)Aussen.Landschaft(a)[strasse.Nummer]!)["punkte"]!;
        Assert.Equal(punkte.Count - 1, Bauen3D.Umriss(a, strasse).Count);
        // ein Stueck, das es nicht gibt, hat keinen Umriss (geloeschte Auswahl)
        Assert.Empty(Bauen3D.Umriss(a, new AussenStelle(AussenArt.Baum, 99999)));
    }

    [Fact]
    public void NeuRot_ZaehltGleicheTexteEinzeln()
    {
        StadtbauFund R(string t) => new(t, "", true);
        StadtbauFund G(string t) => new(t, "", false);
        Assert.Null(Bauen3D.NeuRot([R("a"), R("a")], [R("a"), R("a"), G("b")]));
        Assert.Equal("a", Bauen3D.NeuRot([R("a")], [R("a"), R("a")])?.Text);
        Assert.Equal("c", Bauen3D.NeuRot([R("a")], [R("c")])?.Text);
        // gelb zaehlt nie als rot
        Assert.Null(Bauen3D.NeuRot([], [G("x")]));
    }

    [Fact]
    public void Schatten_RotAufDerStrasse_GruenAufFreierFlaeche()
    {
        var p = Leer("Schatten");
        var innen = p.Innenraeume();
        var vorher = Bauen3D.Pruefen((Lernplattform.Core.Daten.PyObjekt)Py.Kopie(p.AussenStand)!, p.Basis, innen, p.Daten);
        Assert.DoesNotContain(vorher, f => f.Rot);
        // eine zweite Strasse fast genau auf der ersten: rot (gleiche Regel wie die Liste rechts)
        var rot = Bauen3D.Setzen(p.AussenStand, AussenArt.Strasse, 12, 8.3, false, null, 0);
        Assert.NotNull(rot.Stelle);
        var fund = Bauen3D.NeuRot(vorher, Bauen3D.Pruefen(rot.Stand, p.Basis, innen, p.Daten));
        Assert.NotNull(fund);
        // derselbe Schritt wirklich gemacht: die Liste zeigt denselben Fund
        p.AussenAendern("Setzen", a => AussenBearbeitung.Setzen(a, AussenArt.Strasse, 12, 8.3, false, null));
        Assert.Contains(Projekt.PruefenAussen(p.Stand(), D, Aussen.Bauten(p.AussenStand)), f => f.Rot && f.Text == fund!.Text);
        p.Rueckgaengig();

        var gruen = Bauen3D.Setzen(p.AussenStand, AussenArt.Baum, 3, 14, true, null, 0);
        Assert.NotNull(gruen.Stelle);
        Assert.Null(Bauen3D.NeuRot(vorher, Bauen3D.Pruefen(gruen.Stand, p.Basis, innen, p.Daten)));
    }

    [Fact]
    public void GanzeStadt_MitteUndAbstandInDenGrenzen()
    {
        var p = Leer("Ganz");
        var (x, z, abstand) = Bauen3D.GanzeStadt(p.AussenStand);
        Assert.Equal(12f, x);
        Assert.Equal(8f, z);
        Assert.InRange(abstand, Kamera.AbstandMin, Kamera.AbstandMax);
        Assert.Equal(24 * 1.05f, abstand, 3);
        var gross = Projekt.Neu("Gross", D, Daten.NeueAblage(), leer: (64, 64));
        Assert.Equal(Kamera.AbstandMax, Bauen3D.GanzeStadt(gross.AussenStand).Abstand);
    }
}
