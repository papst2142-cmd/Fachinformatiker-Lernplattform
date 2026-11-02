using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 11 FW7 (Sammelliste 40, 41, 43): feste, benannte Ausnahme von Python fit_lines fuer die
/// Anzeige (<see cref="Zeilen.KastenAnzeige"/>). <see cref="Zeilen.Kasten"/> bleibt wie Python
/// (G24); hier steht je Regel, dass Python anders umbricht und die Anzeige nicht.
/// </summary>
public class ZeilenAnzeigeTests
{
    private static string Dauer(bool schmal) =>
        DatenBerichte.UebertragungBericht(DatenBerichte.UebertragungLesen("1000000000000", "TiB", "0,001", "kbit/s", "100"), schmal);

    private static string Verkauf() =>
        WisoBerichte.Rechenweg(WisoBerichte.Lesen("verkauf",
            RechnerTexte.WisoFelder["verkauf"].ToDictionary(f => f.Schluessel, f => (string?)f.Vorgabe)));

    private static string[] Z(string text) => text.Split('\n');

    [Fact]
    public void GrosseZahl_BleibtGanz_AusnahmeVonPython()
    {
        // Python zerteilt die Zahl vor einem Tausenderpunkt, die Folgezeile beginnt mit ".000"
        Assert.Contains(Z(Zeilen.Kasten(Dauer(true), 20, true)), z => z.TrimStart().StartsWith(".000", StringComparison.Ordinal));
        var anzeige = Z(Zeilen.KastenAnzeige(Dauer(true), 20, true));
        Assert.DoesNotContain(anzeige, z => z.TrimStart().StartsWith('.'));
        Assert.Contains("  8.796.093.022.208.000.000.000.000,00", anzeige);
        // die Zahl ist laenger als der Kasten: der Kasten zeigt "→ wischen"
        Assert.True(Wischhinweis.Noetig(string.Join("\n", anzeige), 20));
    }

    [Fact]
    public void EinheitBleibtBeiDerZahl_AusnahmeVonPython()
    {
        // Python: "= 29,90" am Zeilenende, "€" allein in der naechsten Zeile (Handy, Schrift Normal)
        Assert.Contains(Enumerable.Range(18, 40), breite => Z(Zeilen.Kasten(Verkauf(), breite, true)).Contains("   €"));
        foreach (var breite in Enumerable.Range(18, 40))
        {
            var anzeige = Z(Zeilen.KastenAnzeige(Verkauf(), breite, true));
            Assert.DoesNotContain(anzeige, z => z.Trim() is "€" or "%" || z.TrimStart().StartsWith("€ ", StringComparison.Ordinal));
        }
        // "20,00 Sek." bleibt zusammen
        Assert.DoesNotContain(Z(Zeilen.KastenAnzeige(Dauer(true), 20, true)), z => z.Trim() == "Sek.");
    }

    [Fact]
    public void RechenzeichenNieAmZeilenende_AusnahmeVonPython()
    {
        // Python (Breite 20): "= 1.024,60 x 30 /" und in der naechsten Zeile "100 = 307,38 €"
        Assert.Contains(Z(Zeilen.Kasten(Verkauf(), 20, true)), z => z.TrimEnd().EndsWith(" /", StringComparison.Ordinal));
        foreach (var breite in Enumerable.Range(18, 30))
        {
            foreach (var zeile in Z(Zeilen.KastenAnzeige(Verkauf(), breite, true)))
            {
                foreach (var zeichen in Zeilen.AnzeigeRechenzeichen)
                {
                    Assert.False(zeile.TrimEnd().EndsWith(" " + zeichen, StringComparison.Ordinal), $"Breite {breite}: \"{zeile}\"");
                }
            }
        }
    }

    [Fact]
    public void SatzUeberZweiZeilen_WirdZusammenUmbrochen_AusnahmeVonPython()
    {
        // Python bricht "... vom Endpreis abgezogen. Deshalb" um und laesst "wird mit ..." als eigene Zeile
        var python = Z(Zeilen.Kasten(Verkauf(), 40, true));
        Assert.Contains("   abgezogen. Deshalb", python);
        var anzeige = Z(Zeilen.KastenAnzeige(Verkauf(), 40, true));
        Assert.DoesNotContain("   abgezogen. Deshalb", anzeige);
        var satz = string.Join(" ", anzeige.SkipWhile(z => !z.Contains("Skonto und Rabatt", StringComparison.Ordinal)).Take(3).Select(z => z.Trim()));
        Assert.StartsWith("Skonto und Rabatt werden vom Endpreis abgezogen. Deshalb wird mit 100 minus Prozentsatz geteilt.", satz, StringComparison.Ordinal);
        // passt die erste Zeile (PC), bleibt alles wie Python
        Assert.Equal(Zeilen.Kasten(Verkauf(), 75), Zeilen.KastenAnzeige(Verkauf(), 75));
    }

    [Fact]
    public void KurzeKlammerBleibtZusammen()
    {
        var anzeige = Z(Zeilen.KastenAnzeige(Verkauf(), 20, true));
        Assert.Contains("  (im Hundert)", anzeige);
    }

    [Fact]
    public void Anzeige_WortlautBleibt()
    {
        // nur die Zeilenwechsel liegen anders: ohne Leerraum ist der Text gleich
        static string Ohne(string t) => string.Concat(t.Where(c => !char.IsWhiteSpace(c)));
        foreach (var text in new[] { Dauer(true), Dauer(false), Verkauf() })
        {
            foreach (var breite in new[] { 18, 20, 28, 40, 75 })
            {
                Assert.Equal(Ohne(Zeilen.Kasten(text, breite, true)), Ohne(Zeilen.KastenAnzeige(text, breite, true)));
            }
        }
    }

    [Fact]
    public void Zahlbindung_SchuetztNurZahlUndEinheit()
    {
        var text = Beschriftungen.Einsetzen(RechnerTexte.AufgabeDauerSchwer, "81", "GiB", "250", "50");
        var gebunden = Zahlbindung.Binden(text);
        Assert.Equal(text, gebunden.Replace(Zahlbindung.Geschuetzt, ' '));
        Assert.Contains("81 GiB", gebunden, StringComparison.Ordinal);
        Assert.Contains("250 Mbit/s", gebunden, StringComparison.Ordinal);
        Assert.Contains("50 %", gebunden, StringComparison.Ordinal);
        // "2 Nachkommastellen" ist keine Einheit, "100 MBit" kein Treffer im Wort
        Assert.Contains("2 Nachkommastellen", gebunden, StringComparison.Ordinal);
        Assert.Equal("100 MBx", Zahlbindung.Binden("100 MBx"));
        Assert.Equal("1 kB, 2 %.", Zahlbindung.Binden("1 kB, 2 %."));
    }
}
