using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W8 (Weisungen P8-16, P8-17 Nico 4b): Tastenbelegung (PC und Handy gleich, nur Abweichungen in
/// einstellungen.json, keine Taste doppelt, Esc und Steuertasten gesperrt) und Maus an der Kamera (links ziehen
/// schiebt, rechts ziehen dreht, Tippen nur links ohne Ziehen).
/// </summary>
public sealed class SpielBedienungTests
{
    // -- Tastenbelegung ---------------------------------------------------------------

    [Fact]
    public void StandardWieBisher()
    {
        var tasten = new Tastenbelegung();
        Assert.True(tasten.IstStandard);
        Assert.Equal(Tastenbelegung.Links, tasten.AufgabeFuer("Left"));
        Assert.Equal(Tastenbelegung.DrehLinks, tasten.AufgabeFuer("Q"));
        Assert.Equal(Tastenbelegung.Naeher, tasten.AufgabeFuer("Add"));
        Assert.Equal(Tastenbelegung.Naeher, tasten.AufgabeFuer("OemPlus"));
        Assert.Equal(Tastenbelegung.Vollbild, tasten.AufgabeFuer("F11"));
        Assert.Null(tasten.AufgabeFuer("Escape"));
        Assert.Equal("+", tasten.AnzeigeFuer(Tastenbelegung.Naeher));
        Assert.Equal(SpielProbeTexte.Hinweis, SpielProbeTexte.HinweisFuer(tasten));
        // jede Standardtaste genau einer Aufgabe
        var alle = Tastenbelegung.Aufgaben.SelectMany(a => a.Standard).ToList();
        Assert.Equal(alle.Count, alle.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(alle, Tastenbelegung.Gesperrt.Contains);
    }

    [Fact]
    public void NeueTasteErsetztDieStandardtasten()
    {
        var tasten = new Tastenbelegung();
        Assert.Equal(Tastenbelegung.Ergebnis.Ok, tasten.Setzen(Tastenbelegung.Naeher, "W", out var andere));
        Assert.Null(andere);
        Assert.Equal(["W"], tasten.Tasten(Tastenbelegung.Naeher));
        Assert.Null(tasten.AufgabeFuer("Add"));
        Assert.Equal(Tastenbelegung.Naeher, tasten.AufgabeFuer("W"));
        // zurueck auf die einzige Standardtaste = keine Abweichung mehr
        Assert.Equal(Tastenbelegung.Ergebnis.Ok, tasten.Setzen(Tastenbelegung.DrehLinks, "A", out _));
        Assert.Equal(Tastenbelegung.Ergebnis.Ok, tasten.Setzen(Tastenbelegung.DrehLinks, "Q", out _));
        Assert.Equal([Tastenbelegung.Naeher], tasten.Eigen.Keys);
        Assert.Equal("Tippe auf ein Haus, seinen Namen oder einen freien Platz, um hinzugehen. Ziehen verschiebt die Ansicht, zwei Finger zoomen und drehen. "
            + "Am PC: Mausrad zoomt, Pfeiltasten verschieben, rechte Maustaste oder Q und E drehen.", SpielProbeTexte.HinweisFuer(tasten));
        tasten.Setzen(Tastenbelegung.Links, "J", out _);
        Assert.Contains("J, Pfeil rechts, Pfeil hoch und Pfeil runter verschieben", SpielProbeTexte.HinweisFuer(tasten), StringComparison.Ordinal);
    }

    [Fact]
    public void DoppeltUndGesperrtAendernNichts()
    {
        var tasten = new Tastenbelegung();
        Assert.Equal(Tastenbelegung.Ergebnis.Doppelt, tasten.Setzen(Tastenbelegung.Hinein, "Q", out var andere));
        Assert.Equal(Tastenbelegung.DrehLinks, andere);
        Assert.Equal("„Q“ ist schon für „Nach links drehen“ belegt. Bitte eine andere Taste wählen.", Tastenbelegung.DoppeltText("Q", andere!));
        Assert.Equal(Tastenbelegung.Ergebnis.Gesperrt, tasten.Setzen(Tastenbelegung.Hinein, "Escape", out _));
        Assert.Equal(Tastenbelegung.Ergebnis.Gesperrt, tasten.Setzen(Tastenbelegung.Hinein, "Tab", out _));
        Assert.True(tasten.IstStandard);
        // dieselbe Aufgabe darf ihre eigene Taste behalten
        Assert.Equal(Tastenbelegung.Ergebnis.Ok, tasten.Setzen(Tastenbelegung.Hinein, "Enter", out _));
        Assert.True(tasten.IstStandard);
    }

    [Fact]
    public void LesenUndSpeichern()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "lp_tasten_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ordner);
        try
        {
            var einstellungen = new Einstellungen(ordner);
            var tasten = new Tastenbelegung();
            tasten.Setzen(Tastenbelegung.Hinein, "Space", out _);
            tasten.Setzen(Tastenbelegung.Vollbild, "F", out _);
            Assert.True(tasten.Speichern(einstellungen));
            var werte = JsonNode.Parse(File.ReadAllText(einstellungen.Pfad))!.AsObject();
            Assert.Equal("{\"hinein\":\"Space\",\"vollbild\":\"F\"}", werte[Tastenbelegung.Schluessel]!.ToJsonString());
            var gelesen = Tastenbelegung.Lesen(werte);
            Assert.Equal(tasten.Eigen, gelesen.Eigen);
            Assert.Equal("Leertaste", gelesen.AnzeigeFuer(Tastenbelegung.Hinein));
            // Standard = Schluessel weg
            Assert.True(new Tastenbelegung().Speichern(einstellungen));
            Assert.Null(JsonNode.Parse(File.ReadAllText(einstellungen.Pfad))!.AsObject()[Tastenbelegung.Schluessel]);
        }
        finally
        {
            Directory.Delete(ordner, true);
        }
    }

    [Fact]
    public void KaputteBelegungGiltAlsStandard()
    {
        Assert.True(Tastenbelegung.Lesen(new JsonObject { [Tastenbelegung.Schluessel] = "Q" }).IstStandard);
        var gelesen = Tastenbelegung.Lesen(new JsonObject
        {
            [Tastenbelegung.Schluessel] = new JsonObject
            {
                ["unbekannt"] = "W",
                [Tastenbelegung.Hinein] = "Escape",
                [Tastenbelegung.Links] = 3,
                [Tastenbelegung.Rechts] = "",
                // E hat schon "Nach rechts drehen": gilt nicht
                [Tastenbelegung.Hoch] = "E",
                [Tastenbelegung.Runter] = "S",
            },
        });
        Assert.Equal(new Dictionary<string, string> { [Tastenbelegung.Runter] = "S" }, gelesen.Eigen);
    }

    [Fact]
    public void KopieIstUnabhaengig()
    {
        var tasten = new Tastenbelegung();
        tasten.Setzen(Tastenbelegung.Links, "A", out _);
        var kopie = tasten.Kopie();
        kopie.Setzen(Tastenbelegung.Links, "Left", out _);
        Assert.Equal(["A"], tasten.Tasten(Tastenbelegung.Links));
        Assert.True(kopie.IstStandard);
    }

    [Fact]
    public void TastennamenFuerDieKarte()
    {
        Assert.Equal("Pfeil links", Tastenbelegung.Anzeige("Left"));
        Assert.Equal("−", Tastenbelegung.Anzeige("Subtract"));
        Assert.Equal("7", Tastenbelegung.Anzeige("D7"));
        Assert.Equal("Ziffernblock 4", Tastenbelegung.Anzeige("NumPad4"));
        Assert.Equal("F11", Tastenbelegung.Anzeige("F11"));
        Assert.Equal("Eingabe", Tastenbelegung.Anzeige("Enter"));
    }

    // -- Maus an der Kamera -----------------------------------------------------------

    [Fact]
    public void LinksTippenUndZiehen()
    {
        var griff = new Kameragriff(8);
        griff.Druecken(false, 100, 100);
        Assert.Equal(Kameragriff.Art.Nichts, griff.Bewegen(103, 102).Art);
        Assert.True(griff.Loslassen(false));
        Assert.False(griff.Gedrueckt);
        griff.Druecken(false, 100, 100);
        Assert.Equal(Kameragriff.Art.Nichts, griff.Bewegen(104, 100).Art);
        var schritt = griff.Bewegen(110, 101);
        Assert.Equal(Kameragriff.Art.Ziehen, schritt.Art);
        Assert.Equal(6, schritt.Dx);
        Assert.Equal(1, schritt.Dy);
        Assert.False(griff.Loslassen(false));
    }

    [Fact]
    public void RechtsDrehtUndTipptNie()
    {
        var griff = new Kameragriff(8);
        griff.Druecken(true, 50, 50);
        var schritt = griff.Bewegen(60, 70);
        Assert.Equal(Kameragriff.Art.Drehen, schritt.Art);
        Assert.Equal(10 * Kameragriff.GradJePunkt, schritt.Grad);
        Assert.Equal(Kameragriff.Art.Nichts, griff.Bewegen(60, 90).Art);
        Assert.False(griff.Loslassen(true));
        // ohne Bewegung losgelassen: auch kein Tippen
        griff.Druecken(true, 50, 50);
        Assert.False(griff.Loslassen(true));
    }

    [Fact]
    public void BeideTastenKeinTippen()
    {
        var griff = new Kameragriff(8);
        griff.Druecken(false, 10, 10);
        griff.Druecken(true, 10, 10);
        Assert.Equal(Kameragriff.Art.Drehen, griff.Bewegen(20, 10).Art);
        Assert.False(griff.Loslassen(true));
        Assert.False(griff.Loslassen(false));
        griff.Druecken(false, 10, 10);
        griff.Abbrechen();
        Assert.False(griff.Gedrueckt);
        Assert.Equal(Kameragriff.Art.Nichts, griff.Bewegen(40, 40).Art);
        Assert.False(griff.Loslassen(false));
    }
}
