using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.21.0 / A 0.52.0 (Weisung P12-89, Nico 08.11.2026 14:06 MEZ „Stellwerk Talheim-Nord bitte.“): Name eines Orts im
/// Werkzeug aendern. Ein Schritt, Rueckgaengig geht; leer, doppelt oder zu lang geht nicht und ist in der Pruefung rot.
/// Das Spiel liest den Namen aus der Karte der Fassung (Kennung, Kunde, Aufgaben und Innenraum bleiben).
/// </summary>
public class OrtsnameTests
{
    private const string Neu = "Stellwerk Talheim-Nord";

    private static Projekt Stadt1()
    {
        var p = Projekt.Neu("Ortsname", Daten.Spiel, Daten.NeueAblage());
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        return p;
    }

    private static List<StadtbauFund> Rot(Projekt p) => [.. p.PruefenAussen().Where(f => f.Rot)];

    private static string Name(Projekt p, string ort) => Py.Str(AussenBearbeitung.Ort(p.AussenStand, ort)!["name"]);

    [Fact]
    public void Umbenennen_EinSchritt_RueckgaengigGeht_ExportUndSpielZeigenDenNamen()
    {
        var p = Stadt1();
        var schritte = p.Verlauf.Count;
        Assert.Null(p.AussenAendern("Ort umbenannt", a => AussenBearbeitung.OrtUmbenennen(a, "talheim_nord", "  " + Neu + " ")));
        Assert.Equal(schritte + 1, p.Verlauf.Count);
        Assert.Equal(Neu, Name(p, "talheim_nord"));
        Assert.Equal(Neu, p.OrtName("talheim_nord"));
        Assert.Empty(Rot(p));
        // Export und Ladepruefung des Spiels; die Stadt des Spiels zeigt den neuen Namen, die Kennung bleibt
        var st = StadtFassung.Lesen(p.Exportieren(DateTimeOffset.Now));
        Assert.Equal([], StadtbauPruefung.Inhalt(st, Daten.Spiel.Inhalt));
        Assert.Equal([], SpielPruefung.Welt(st.MitKarte(Daten.Spiel.Inhalt)));
        var stadt = st.StadtFuer(st.MitKarte(Daten.Spiel.Inhalt));
        var spielStand = new Spielstand([], Daten.Spiel.Inhalt) { TageFertig = 29 };
        var ort = stadt.Orte(spielStand).Single(o => Py.Gleich(o["id"], "talheim_nord"));
        Assert.Equal(Neu, ort["name"]);
        Assert.Equal("kunde:talheim_nord", ort["ansicht"]);
        // Rueckgaengig bringt den alten Namen
        p.Rueckgaengig();
        Assert.Equal("Talheim-Nord", Name(p, "talheim_nord"));
    }

    [Theory]
    [InlineData("", "leer")]
    [InlineData("   ", "leer")]
    [InlineData("bahnhof talheim", "schon ein anderer Ort")]
    [InlineData("Ein sehr langer Name für den Bahnhof", "höchstens 28")]
    public void Ungueltig_NichtsGeaendert(string name, string grund)
    {
        var p = Stadt1();
        var vorher = PythonJson.Standard(p.AussenStand);
        var schritte = p.Verlauf.Count;
        var r = p.AussenAendern("Ort umbenannt", a => AussenBearbeitung.OrtUmbenennen(a, "talheim_nord", name));
        Assert.NotNull(r);
        Assert.Contains(grund, r, StringComparison.Ordinal);
        Assert.Equal(vorher, PythonJson.Standard(p.AussenStand));
        Assert.Equal(schritte, p.Verlauf.Count);
    }

    [Fact]
    public void GleicherName_BleibtErlaubt_NichtDoppeltMitSichSelbst()
    {
        var p = Stadt1();
        Assert.Null(p.AussenAendern("Ort umbenannt", a => AussenBearbeitung.OrtUmbenennen(a, "talheim_nord", "Talheim-Nord")));
    }

    /// <summary>Pruefung (gemeinsamer Teil): von Hand geschriebene Fassung mit leerem, doppeltem oder zu langem Namen = rot; Gegenprobe gruen.</summary>
    [Theory]
    [InlineData("", "leer")]
    [InlineData("Bahnhof Talheim", "schon ein anderer Ort")]
    [InlineData("Ein sehr langer Name für den Bahnhof", "höchstens 28")]
    public void Pruefung_LeerDoppeltZuLang_Rot_Gegenprobe(string name, string grund)
    {
        var p = Stadt1();
        p.AussenAendern("von Hand", a => Py.Setzen(AussenBearbeitung.Ort(a, "talheim_nord")!, "name", name));
        Assert.Contains(Rot(p), f => f.Text.Contains(grund, StringComparison.Ordinal));
        var g = Stadt1();
        g.AussenAendern("von Hand", a => Py.Setzen(AussenBearbeitung.Ort(a, "talheim_nord")!, "name", Neu));
        Assert.DoesNotContain(Rot(g), f => f.Text.Contains(grund, StringComparison.Ordinal));
        Assert.Empty(Rot(g));
    }

    [Fact]
    public void Fassung0_HatKeinenNamensfund()
    {
        Assert.DoesNotContain(Stadt1().PruefenAussen(), f => f.Text.StartsWith("Ort „", StringComparison.Ordinal));
    }
}
