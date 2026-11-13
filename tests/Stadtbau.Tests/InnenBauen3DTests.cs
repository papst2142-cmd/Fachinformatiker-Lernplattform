using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.50.0 (Weisung P12-234 mit P12-231 Antwort 1b): Moebel im Innenraum in 3D bauen. Geprueft wird Nicos Ziel: in 3D
/// dieselben Schritte, derselbe Verlauf und dieselbe Pruefung wie im Grundriss; der Treffer nimmt das Stueck unter dem Finger;
/// der Schatten ist rot, wenn der Schritt einen neuen roten Fund braechte, sonst gruen. Jede Pruefung mit Gegenprobe.
/// </summary>
public class InnenBauen3DTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Neu(string ort = "elektromarkt") => Projekt.Neu("Innen bauen 3D", D, Daten.NeueAblage(), ort);

    private static (double X, double Y) Mitte(PyObjekt d) =>
        (Py.F(d["x"]) + (Py.F(d["w"]) / 2), Py.F(d["y"]) + (Py.F(d["h"]) / 2));

    [Fact]
    public void Treffer_NimmtDenKasten_SonstDenBoden_SonstNichts()
    {
        var p = Neu();
        var g = p.Grundriss("elektromarkt");
        var (stelle, d) = Bearbeitung.Alle(g).First();
        var (mx, my) = Mitte(d);
        var kaesten = InnenBauen3D.Kaesten(g, null);
        Assert.Equal(Bearbeitung.Alle(g).Count(), kaesten.Count);
        Assert.All(kaesten, k => Assert.Equal(InnenBauen3D.HoeheOhneBild, k.Max.Y));

        // von oben auf die Mitte: der Kasten des Stuecks
        Assert.Equal(stelle, InnenBauen3D.Treffer(g, kaesten, new Vector3((float)mx, 5, (float)my), -Vector3.UnitY));
        // waagerecht in halber Hoehe von weit her: nur der Kasten kann treffen (der Boden wird nie erreicht)
        var waagerecht = InnenBauen3D.Treffer(g, kaesten, new Vector3(-50, 0.3f, (float)my), Vector3.UnitX);
        Assert.NotNull(waagerecht);
        // Gegenprobe: dieselbe Linie ueber allen Kaesten trifft nichts (kein Kasten, kein Boden)
        Assert.Null(InnenBauen3D.Treffer(g, kaesten, new Vector3(-50, InnenBauen3D.HoeheOhneBild + 0.5f, (float)my), Vector3.UnitX));
        // Gegenprobe: ohne Kaesten faellt der Strahl von oben auf den Boden und nimmt das Stueck wie der Grundriss
        Assert.Equal(Bearbeitung.StueckBei(g, mx, my), InnenBauen3D.Treffer(g, [], new Vector3((float)mx, 5, (float)my), -Vector3.UnitY));
        // weit draussen: nichts
        Assert.Null(InnenBauen3D.Treffer(g, kaesten, new Vector3(-500, 5, -500), -Vector3.UnitY));
    }

    [Fact]
    public void Treffer_VordersterKasten_GewinntVorDemDahinter()
    {
        var p = Neu();
        var g = p.Grundriss("elektromarkt");
        var a = new InnenBauen3D.Kasten(new Stelle("flur", 0), new Vector3(0, 0, 0), new Vector3(1, 1, 1));
        var b = new InnenBauen3D.Kasten(new Stelle("flur", 1), new Vector3(3, 0, 0), new Vector3(4, 1, 1));
        Assert.Equal(a.Stelle, InnenBauen3D.Treffer(g, [b, a], new Vector3(-5, 0.5f, 0.5f), Vector3.UnitX));
        // Gegenprobe: aus der Gegenrichtung ist b vorne
        Assert.Equal(b.Stelle, InnenBauen3D.Treffer(g, [a, b], new Vector3(9, 0.5f, 0.5f), -Vector3.UnitX));
    }

    [Fact]
    public void Vorschau_GleichDemEchtenSchritt_UndLaesstDenStandUnberuehrt()
    {
        var p = Neu();
        var g = p.Grundriss("elektromarkt");
        var vorher = PythonJson.Text(g);
        var (stelle, d) = Bearbeitung.Alle(g).First();
        var (mx, my) = Mitte(d);

        var setzen = InnenBauen3D.Setzen(g, "aktenregal", mx, my, true);
        var verschieben = InnenBauen3D.Verschieben(g, stelle, 0.5, -0.5, true);
        Assert.Equal(vorher, PythonJson.Text(g));   // nur Kopien

        var echtSetzen = (PyObjekt)Py.Kopie(g)!;
        var s = Bearbeitung.Setzen(echtSetzen, "aktenregal", mx, my, true);
        Assert.Equal(PythonJson.Text(echtSetzen), PythonJson.Text(setzen.Gebaeude));
        Assert.Equal(s, setzen.Stelle);
        Assert.Equal(4, setzen.Umriss.Length);
        var echtVerschieben = (PyObjekt)Py.Kopie(g)!;
        Assert.Equal(Bearbeitung.Verschieben(echtVerschieben, stelle, 0.5, -0.5, true), verschieben.Stelle);
        Assert.Equal(PythonJson.Text(echtVerschieben), PythonJson.Text(verschieben.Gebaeude));
        // Gegenprobe: ein anderer Schritt ergibt einen anderen Stand
        Assert.NotEqual(PythonJson.Text(setzen.Gebaeude), PythonJson.Text(verschieben.Gebaeude));

        // ausserhalb jedes Raums: kein Stueck, mit Grund
        var draussen = InnenBauen3D.Setzen(g, "aktenregal", -500, -500, true);
        Assert.Null(draussen.Stelle);
        Assert.Equal(Texte.InnenBauen3DAusserhalb, draussen.Grund);
        Assert.Empty(draussen.Umriss);
    }

    [Theory]
    [InlineData("elektromarkt")]
    [InlineData("bank")]
    public void Pruefen_GleichDerPruefungDesGrundrisses(string ort)
    {
        var p = Neu(ort);
        var (stelle, _) = Bearbeitung.Alle(p.Grundriss(ort)).First();
        p.Aendern(ort, "Verschieben", g => Bearbeitung.Verschieben(g, stelle, 1.5, 0.5, true));
        var erwartet = p.Pruefen(ort).Select(f => (f.Text, f.Rot)).ToList();
        var (funde, _) = InnenBauen3D.Pruefen(InnenBauen3D.Vorbereiten(p, ort), (PyObjekt)Py.Kopie(p.Grundriss(ort))!);
        Assert.Equal(erwartet, funde.Select(f => (f.Text, f.Rot)).ToList());
    }

    [Fact]
    public void Schatten_Rot_AufEinemStueck_Gruen_AnSeinemAltenPlatz()
    {
        var p = Neu();
        const string ort = "elektromarkt";
        var auftrag = InnenBauen3D.Vorbereiten(p, ort);
        var g = p.Grundriss(ort);
        var basis = InnenBauen3D.Pruefen(auftrag, g).Funde;

        // ein Stueck desselben Typs genau auf ein vorhandenes Stueck: Ueberschneidung, neuer roter Fund
        var (stelle, d) = Bearbeitung.Alle(g).First(x => Bearbeitung.Teil(Py.Str(x.Deko["typ"])) is not null);
        var typ = Py.Str(d["typ"]);
        var (mx, my) = Mitte(d);
        var rot = InnenBauen3D.Setzen(g, typ, mx, my, true);
        Assert.NotNull(rot.Stelle);
        Assert.NotNull(Bauen3D.NeuRot(basis, InnenBauen3D.Pruefen(auftrag, rot.Gebaeude).Funde));

        // Gegenprobe: dasselbe Stueck geloescht und an seiner alten Mitte neu gesetzt = derselbe Raum, kein neuer roter Fund
        var ohne = (PyObjekt)Py.Kopie(g)!;
        Bearbeitung.Loeschen(ohne, stelle);
        var gruen = InnenBauen3D.Setzen(ohne, typ, mx, my, true);
        Assert.NotNull(gruen.Stelle);
        Assert.Null(Bauen3D.NeuRot(basis, InnenBauen3D.Pruefen(auftrag, gruen.Gebaeude).Funde));
    }

    [Fact]
    public void Verschieben_InDreiD_EinSchrittImVerlauf_WieImGrundriss()
    {
        var p = Neu();
        const string ort = "elektromarkt";
        var vorher = PythonJson.Text(p.Grundriss(ort));
        var (stelle, _) = Bearbeitung.Alle(p.Grundriss(ort)).First();
        var vorschau = InnenBauen3D.Verschieben(p.Grundriss(ort), stelle, 1, 0, true);
        var zurueck = p.SchritteZurueck;
        // derselbe Schritt, den die Oberflaeche nach dem Loslassen macht (GrundrissFlaeche.VerschiebenUm)
        p.Aendern(ort, "Verschieben", g => Bearbeitung.Verschieben(g, stelle, 1, 0, true));
        Assert.Equal(zurueck + 1, p.SchritteZurueck);
        Assert.Equal(PythonJson.Text(vorschau.Gebaeude), PythonJson.Text(p.Grundriss(ort)));
        Assert.True(p.Rueckgaengig());
        Assert.Equal(vorher, PythonJson.Text(p.Grundriss(ort)));
    }
}
