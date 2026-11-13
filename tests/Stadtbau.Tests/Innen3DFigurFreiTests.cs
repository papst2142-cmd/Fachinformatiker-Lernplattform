using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Stadtbau.Core;
using Xunit;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.52.0 (Sammelliste 334, Weisung P12-237 Punkt 5: in Bild 05 stand die Figur nach dem Verschieben im Tresen):
/// landet beim Bauen ein Moebel auf der Stelle der Figur, steht sie im Innenraum in 3D auf der naechsten Stelle, an der sie im
/// Spiel stehen kann. Gegenprobe: wie bis S 0.51.0 bleibt sie im Moebel.
/// </summary>
public class Innen3DFigurFreiTests
{
    private static Spieldaten D => Daten.Spiel;

    [Fact]
    public void MoebelAufDerFigur_FigurDanebenFrei_GegenprobeImMoebel()
    {
        const string ort = "elektromarkt";
        var p = Projekt.Neu("Figur frei", D, Daten.NeueAblage(), ort);
        var vorher = Innen3D.Bauen(Innen3D.Vorbereiten(p, ort), null);
        var (sx, sz) = (vorher.Innen.StartX, vorher.Innen.StartZ);
        Assert.True(Wege.KannStehen(sx, sz, vorher.Innen.Lauf));
        Assert.Equal(sx, vorher.Figur.X);

        // ein Stueck mit Umriss (kein begehbarer Teppich) genau auf die Stelle der Figur schieben
        var (stelle, d) = Bearbeitung.Alle(p.Grundriss(ort)).First(x => !Wege.Begehbar.Contains(Py.Str(x.Deko["typ"])) && Py.F(x.Deko["w"]) >= 0.8 && Py.F(x.Deko["h"]) >= 0.5);
        var (mx, my) = (Py.F(d["x"]) + (Py.F(d["w"]) / 2), Py.F(d["y"]) + (Py.F(d["h"]) / 2));
        p.Aendern(ort, "Verschieben", g => Bearbeitung.Verschieben(g, stelle, sx - mx, sz - my, false));

        var e = Innen3D.Bauen(Innen3D.Vorbereiten(p, ort), vorher);
        Assert.False(Wege.KannStehen(e.Innen.StartX, e.Innen.StartZ, e.Innen.Lauf));
        Assert.True(Wege.KannStehen(e.Figur.X, e.Figur.Y, e.Innen.Lauf));
        Assert.True(MathF.Sqrt(MathF.Pow(e.Figur.X - sx, 2) + MathF.Pow(e.Figur.Y - sz, 2)) < 2.5f);
        Assert.Equal(90, e.Figur.Z);

        // Gegenprobe: wie bis S 0.51.0 bleibt die Figur im Moebel
        Innen3D.FigurImMoebelAltFuerTests = true;
        try
        {
            var alt = Innen3D.Bauen(Innen3D.Vorbereiten(p, ort), vorher);
            Assert.False(Wege.KannStehen(alt.Figur.X, alt.Figur.Y, alt.Innen.Lauf));
        }
        finally
        {
            Innen3D.FigurImMoebelAltFuerTests = false;
        }
    }
}
