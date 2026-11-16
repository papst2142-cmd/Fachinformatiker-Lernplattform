using System.Globalization;
using Lernplattform.Core.Pruefung.Echt;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.81.2, Sammelliste 346 (Weisung P12-278, Option b): In einem Zahlenfeld, dessen Musterloesung 1000 oder mehr ist,
/// wird "1.340" wie 1340 gewertet, weil nach dem Punkt genau Dreiergruppen folgen; alles andere wird gewertet wie
/// bisher. Geprueft wird jeder Loesungswert ab 1000 aus der eingebauten Datei (Rechenergebnis, Ersatzergebnis und
/// Lueckenfelder der Art "zahl"), jeweils mit Gegenprobe ueber den alten Weg.
/// </summary>
public class EchtTausenderpunktTests
{
    private static readonly string Datei = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen", EchtSaetze.Datei);

    private static EchtBestand Bestand => EchtSaetze.Laden(File.ReadAllBytes(Datei));

    /// <summary>Jedes Rechenfeld mit jedem seiner Werte (Ergebnis, Ersatzergebnis) ab 1000.</summary>
    private static List<(string Wo, EchtAntwort Antwort, double Wert)> Rechenwerte() =>
    [
        .. from satz in Bestand.Saetze
           from aufgabe in satz.Aufgaben.Select((a, i) => (a, i))
           from teil in aufgabe.a.Teile
           where teil.Antwort.Form == EchtSaetze.FormRechnen
           from wert in new[] { (double?)teil.Antwort.Ergebnis, teil.Antwort.ErsatzErgebnis }
           where wert is >= 1000
           select ($"{satz.Kennung} {aufgabe.i + 1}{teil.Nr}", teil.Antwort, wert!.Value),
    ];

    /// <summary>Jedes Lueckenfeld der Art "zahl" mit jeder seiner Loesungen ab 1000.</summary>
    private static List<(string Wo, EchtFeld Feld, double Wert)> Feldwerte() =>
    [
        .. from satz in Bestand.Saetze
           from aufgabe in satz.Aufgaben.Select((a, i) => (a, i))
           from teil in aufgabe.a.Teile
           from feld in teil.Antwort.Felder
           where feld.Art == "zahl"
           from loesung in feld.Loesung
           let wert = EchtBewertung.Dezimal(loesung)
           where wert is >= 1000
           select ($"{satz.Kennung} {aufgabe.i + 1}{teil.Nr} {feld.Name}", feld, wert!.Value),
    ];

    /// <summary>Deutsche Schreibweise mit Tausenderpunkt aus dem Loesungswert: 1340 = "1.340", 4821,07 = "4.821,07".</summary>
    private static string MitPunkt(double wert)
    {
        var ganz = Math.Truncate(wert).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
        var rest = Math.Round(wert - Math.Truncate(wert), 6);
        return rest == 0 ? ganz : ganz + "," + rest.ToString("0.######", CultureInfo.InvariantCulture)[2..];
    }

    /// <summary>"1,34"-artige falsche Werte aus der Schreibweise mit Punkt (keine Dreiergruppe hinter dem Punkt).</summary>
    private static string[] Falsch(string mitPunkt)
    {
        var ohneKomma = mitPunkt.Split(',')[0];
        return [ohneKomma.Replace('.', ','), ohneKomma + "0", ohneKomma[..^1]];
    }

    /// <summary>Rechenwerte ab 1000 (Ergebnis und Ersatzergebnis) in den zehn AE-Saetzen (A 0.84.0).</summary>
    private const int AeRechenwerte = 15;

    [Fact]
    public void T346_AlleVierundzwanzigRechenwerte_MitTausenderpunktRichtig_DezimalFalsch()
    {
        var werte = Rechenwerte();
        // Kurzplan P12-163: 24 Loesungswerte ab 1000 in den Saetzen bis A 0.83.0, davon 18 ganze Zahlen; seit A 0.84.0
        // kommen die Werte der AE-Saetze dazu und werden genauso geprueft (P12-285)
        var bisher = werte.Where(w => !w.Wo.StartsWith("ae_", StringComparison.Ordinal)).ToList();
        Assert.Equal(24, bisher.Count);
        Assert.Equal(18, bisher.Count(w => w.Wert == Math.Truncate(w.Wert)));
        Assert.Equal(AeRechenwerte, werte.Count - bisher.Count);
        foreach (var (wo, antwort, wert) in werte)
        {
            var punkt = MitPunkt(wert);
            Assert.True(EchtBewertung.ErgebnisRichtig(antwort, punkt), $"{wo}: {punkt} muss richtig sein");
            Assert.True(EchtBewertung.ErgebnisRichtig(antwort, wert.ToString("R", CultureInfo.InvariantCulture).Replace('.', ',')),
                $"{wo}: ohne Punkt muss richtig bleiben");
            if (wert == Math.Truncate(wert))
            {
                Assert.True(EchtBewertung.ErgebnisRichtig(antwort, punkt + ",00"), $"{wo}: {punkt},00 muss richtig bleiben");
                Assert.True(EchtBewertung.ErgebnisRichtig(antwort, " " + punkt + " "), $"{wo}: Leerraum aussen zaehlt nicht");
            }
            foreach (var falsch in Falsch(punkt))
            {
                Assert.False(EchtBewertung.ErgebnisRichtig(antwort, falsch), $"{wo}: {falsch} muss falsch sein");
            }
        }
    }

    [Fact]
    public void T346_Lueckenfelder_ZahlAb1000_MitTausenderpunktRichtig()
    {
        var werte = Feldwerte();
        Assert.NotEmpty(werte);
        foreach (var (wo, feld, wert) in werte)
        {
            var punkt = MitPunkt(wert);
            Assert.True(EchtBewertung.Passt(feld.Art, punkt, feld.Loesung), $"{wo}: {punkt} muss richtig sein");
            Assert.True(EchtBewertung.Passt(feld.Art, punkt + ",00", feld.Loesung), $"{wo}: {punkt},00 muss richtig sein");
            foreach (var falsch in Falsch(punkt))
            {
                Assert.False(EchtBewertung.Passt(feld.Art, falsch, feld.Loesung), $"{wo}: {falsch} muss falsch sein");
            }
        }
    }

    [Theory]
    [InlineData("1.340", 1340, 1340)]
    [InlineData("20.508", 20508, 20508)]
    [InlineData("1.340.000", 1340000, 1340000)]
    [InlineData("1.340", 1000, 1340)]           // Grenze: ab 1000
    [InlineData("1.340", 999.99, 1.34)]         // unter 1000 bleibt der Punkt Dezimalzeichen
    [InlineData("1.340", 13.34, 1.34)]
    [InlineData("1.34", 1340, 1.34)]            // keine Dreiergruppe
    [InlineData("1.3400", 1340, 1.34)]          // Vierergruppe
    [InlineData("1.340,00", 1340, 1340)]        // Punkt und Komma wie bisher
    [InlineData("1.340,5", 1340, 1340.5)]
    [InlineData("01.340", 1340, 1.34)]          // fuehrende Null: keine Tausendergruppe (Rechen-Trainer gleich)
    [InlineData("1234.567", 1340, 1234.567)]    // mehr als drei Ziffern vor dem Punkt
    [InlineData("-1.340", 1340, -1.34)]
    [InlineData("1340", 1340, 1340)]
    public void T346_Tausenderpunkt_Regel(string eingabe, double loesung, double erwartet) =>
        Assert.Equal(erwartet, EchtBewertung.Tausenderpunkt(eingabe, loesung));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1..340")]
    [InlineData("1.340.")]
    public void T346_Tausenderpunkt_KeineZahl(string eingabe) =>
        Assert.Null(EchtBewertung.Tausenderpunkt(eingabe, 1340));

    /// <summary>Gegenprobe: der alte Weg wertet jede ganze Zahl ab 1000 mit Tausenderpunkt falsch (der Fehler von 346).</summary>
    [Fact]
    public void T346_Gegenprobe_AlterWeg_ZeigtDieFehlwertungen()
    {
        var rechnen = Rechenwerte().Where(w => w.Wert == Math.Truncate(w.Wert)).ToList();
        var felder = Feldwerte();
        EchtBewertung.TausenderAltFuerTests(true);
        try
        {
            foreach (var (wo, antwort, wert) in rechnen)
            {
                Assert.False(EchtBewertung.ErgebnisRichtig(antwort, MitPunkt(wert)), $"{wo}: alter Weg muss falsch werten");
                Assert.True(EchtBewertung.ErgebnisRichtig(antwort, MitPunkt(wert) + ",00"), $"{wo}: mit Komma auch alt richtig");
            }
            foreach (var (wo, feld, wert) in felder)
            {
                Assert.False(EchtBewertung.Passt(feld.Art, MitPunkt(wert), feld.Loesung), $"{wo}: alter Weg muss falsch werten");
            }
            Assert.Equal(1.34, EchtBewertung.Tausenderpunkt("1.340", 1340));
        }
        finally
        {
            EchtBewertung.TausenderAltFuerTests(false);
        }
        Assert.Equal(1340, EchtBewertung.Tausenderpunkt("1.340", 1340));
    }
}
