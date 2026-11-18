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
        Assert.Equal(Tastenbelegung.Steiler, tasten.AufgabeFuer("R"));
        Assert.Equal(Tastenbelegung.Flacher, tasten.AufgabeFuer("F"));
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
        Assert.Equal("Tippe auf ein Haus, seinen Namen oder einen freien Platz, um hinzugehen. Zwei Finger drehen und neigen die Kamera und zoomen ein wenig. "
            + "Am PC: rechte Maustaste dreht und neigt, Mausrad zoomt ein wenig, Q und E drehen, R und F neigen. Ganz heranzoomen oder I führt in die Ich-Sicht. Die ganze Stadt zeigt die Karte im Handy.",
            SpielProbeTexte.HinweisFuer(tasten));
        // Welle A 0.80.0: die Pfeiltasten verschieben draussen nicht mehr, der Hinweis nennt sie nicht; eigene Neige-Tasten stehen darin
        tasten.Setzen(Tastenbelegung.Links, "J", out _);
        Assert.DoesNotContain("J", SpielProbeTexte.HinweisFuer(tasten), StringComparison.Ordinal);
        tasten.Setzen(Tastenbelegung.Steiler, "T", out _);
        Assert.Contains("T und F neigen", SpielProbeTexte.HinweisFuer(tasten), StringComparison.Ordinal);
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
    public void FruehereEigeneTaste_GewinntGegenNeueStandardtaste()
    {
        // Welle A 0.80.0: wer vorher F fuer Vollbild gewaehlt hatte, behaelt das; „Kamera flacher“ hat dann keine Taste
        var gelesen = Tastenbelegung.Lesen(JsonNode.Parse("{\"tasten\":{\"vollbild\":\"F\",\"hinein\":\"R\"}}")!.AsObject());
        Assert.Equal(Tastenbelegung.Vollbild, gelesen.AufgabeFuer("F"));
        Assert.Equal(Tastenbelegung.Hinein, gelesen.AufgabeFuer("R"));
        Assert.Equal("keine Taste", gelesen.AnzeigeFuer(Tastenbelegung.Flacher));
        Assert.Equal("keine Taste", gelesen.AnzeigeFuer(Tastenbelegung.Steiler));
        Assert.DoesNotContain("neigen.", SpielProbeTexte.HinweisFuer(gelesen), StringComparison.Ordinal);
        // gespeichert und wieder gelesen: dieselbe Belegung (die leere Taste gilt beim Lesen als nicht gesetzt, der Vorrang greift erneut)
        var wieder = Tastenbelegung.Lesen(new JsonObject { [Tastenbelegung.Schluessel] = gelesen.AlsJson() });
        Assert.Equal(Tastenbelegung.Vollbild, wieder.AufgabeFuer("F"));
        Assert.Equal("keine Taste", wieder.AnzeigeFuer(Tastenbelegung.Flacher));
        // Gegenprobe: eine Aufgabe von vorher behaelt die alte Regel (Zusammenstoss = Standard dieser Aufgabe)
        var alt = Tastenbelegung.Lesen(JsonNode.Parse("{\"tasten\":{\"hinein\":\"Q\"}}")!.AsObject());
        Assert.Equal(Tastenbelegung.DrehLinks, alt.AufgabeFuer("Q"));
        Assert.Equal(Tastenbelegung.Hinein, alt.AufgabeFuer("Enter"));
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
            tasten.Setzen(Tastenbelegung.Vollbild, "G", out _);    // A 0.80.0: F ist Standard fuer „Kamera flacher“
            Assert.True(tasten.Speichern(einstellungen));
            var werte = JsonNode.Parse(File.ReadAllText(einstellungen.Pfad))!.AsObject();
            Assert.Equal("{\"hinein\":\"Space\",\"vollbild\":\"G\"}", werte[Tastenbelegung.Schluessel]!.ToJsonString());
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

    // -- Hinweisfeld (Phase 8 W10, Nico 1b) -------------------------------------------

    [Fact]
    public void HinweisfeldZustandGemerkt()
    {
        Assert.False(HinweisWahl.Zugeklappt(new JsonObject()));
        Assert.False(HinweisWahl.Zugeklappt(new JsonObject { [HinweisWahl.Schluessel] = "Zu" }));
        Assert.False(HinweisWahl.Zugeklappt(new JsonObject { [HinweisWahl.Schluessel] = true }));
        Assert.True(HinweisWahl.Zugeklappt(new JsonObject { [HinweisWahl.Schluessel] = "zu" }));
        var ordner = Path.Combine(Path.GetTempPath(), "lp_hinweis_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ordner);
        try
        {
            var einstellungen = new Einstellungen(ordner);
            Assert.True(HinweisWahl.Setzen(einstellungen, true));
            var werte = JsonNode.Parse(File.ReadAllText(einstellungen.Pfad))!.AsObject();
            Assert.Equal("zu", (string?)werte[HinweisWahl.Schluessel]);
            Assert.True(HinweisWahl.Zugeklappt(werte));
            Assert.True(HinweisWahl.Setzen(einstellungen, false));
            werte = JsonNode.Parse(File.ReadAllText(einstellungen.Pfad))!.AsObject();
            Assert.Equal("offen", (string?)werte[HinweisWahl.Schluessel]);
            Assert.False(HinweisWahl.Zugeklappt(werte));
        }
        finally
        {
            Directory.Delete(ordner, true);
        }
    }

    [Fact]
    public void HinweisfeldKurzeMeldung()
    {
        var hinweis = SpielProbeTexte.HinweisFuer(new Tastenbelegung());
        Assert.Equal("", HinweisWahl.KurzeMeldung(hinweis, hinweis));
        Assert.Equal("", HinweisWahl.KurzeMeldung(SpielProbeTexte.ImFreien, hinweis));
        Assert.Equal("", HinweisWahl.KurzeMeldung("", hinweis));
        var spaet = StadtTexte.UhrSteht + "\n" + "Bitweiche: geschlossen";
        Assert.Equal(spaet, HinweisWahl.KurzeMeldung(spaet, hinweis));
        Assert.Equal(TimeSpan.FromSeconds(5), HinweisWahl.MeldungDauer);
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
        Assert.Equal(20, schritt.Dy);
        // Welle A 0.80.0: rein senkrecht = Drehen um 0 Grad mit Dy (die feste Spielkamera draussen neigt damit)
        var senkrecht = griff.Bewegen(60, 90);
        Assert.Equal((Kameragriff.Art.Drehen, 0f, 20d), (senkrecht.Art, senkrecht.Grad, senkrecht.Dy));
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
