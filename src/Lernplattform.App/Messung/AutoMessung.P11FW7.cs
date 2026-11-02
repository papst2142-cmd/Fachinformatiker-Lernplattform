using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// FW7 (Weisung P11-13, Sammelliste 40, 41, 43): Rechner-Texte am Handy in jeder Schriftstufe.
/// (40) Im Aufgabentext des Trainers bricht keine Zeile zwischen Zahl und Einheit ("50 %"); damit
/// der Punkt etwas prueft, wird derselbe Text ohne Schutz in vielen Breiten gesetzt und muss dort
/// mindestens einmal so brechen. (41) Eine sehr lange Zahl (Uebertragung 10^12 TiB mit 0,001
/// kbit/s) bleibt ganz, keine Zeile beginnt mit einem Tausenderpunkt, der Kasten ist dann
/// wischbar und zeigt "→ wischen". (43) Im Rechenweg Verkauf steht keine Einheit allein und kein
/// Rechenzeichen am Zeilenende.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Eingabe fuer die sehr lange Zahl (Sammelliste 41).</summary>
    public static readonly (string Groesse, string Einheit, string Rate, string RateEinheit, string Prozent) Fw7Dauer =
        ("1000000000000", "TiB", "0,001", "kbit/s", "100");

    private static async Task<int> P11Fw7RechnerTexteAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("Rechner-Texte am Handy (FW7): FEHLER, " + text);
            funde++;
        }
        if (TopLevel.GetTopLevel(ansicht) is not Window fenster)
        {
            Messwerte.Notiere("Rechner-Texte am Handy (FW7): ohne Fenster nicht geprueft");
            return 0;
        }
        await ansicht.ZeigeAsync(Seiten.Rechner);
        if (ansicht.GebauteSeiten[Seiten.Rechner] is not RechnerSeite r)
        {
            Fund("Rechner-Seite fehlt");
            return funde;
        }
        var (breiteVorher, hoeheVorher) = (fenster.Width, fenster.Height);
        var (hb, hh) = Hoechstgroesse(fenster);
        fenster.Width = Math.Min(SchmalBreite, hb);
        fenster.Height = Math.Min(SchmalHoehe, hh);
        var karteVorher = r.Gewaehlt;
        var wisoVorher = r.Wiso.Gewaehlt;
        var felderVorher = r.Daten.DauerFelder;
        var ungeschuetztGebrochen = 0;
        var notizen = new List<string>();
        foreach (var stufe in Einstellungen.Schriftgroessen)
        {
            Schrift.Anwenden(Zustand.SchriftFaktor(stufe));
            await ansicht.BildFertigAsync();

            // (40) Aufgabentext des Trainers
            r.Tab("trainer");
            r.Trainer.Probe("dauer", 3, pruefen: false);
            await ansicht.BildFertigAsync();
            await ansicht.BildFertigAsync();
            var block = r.Trainer.AufgabeText;
            var gebunden = block.Text ?? "";
            var schutz = Enumerable.Range(0, gebunden.Length).Where(i => gebunden[i] == Zahlbindung.Geschuetzt).ToList();
            if (schutz.Count == 0)
            {
                Fund($"{stufe}: Aufgabentext ohne geschuetzte Leerzeichen: \"{gebunden}\"");
            }
            if (Bruch(block, schutz) is { } stelle)
            {
                Fund($"{stufe}: Aufgabentext bricht zwischen Zahl und Einheit bei {stelle}");
            }
            var ungeschuetzt = gebunden.Replace(Zahlbindung.Geschuetzt, ' ');
            for (var breite = 120.0; breite <= 420; breite += 3)
            {
                if (Bruch(Probe(block, gebunden, breite), schutz) is { } s)
                {
                    Fund(string.Create(inv, $"{stufe}: Breite {breite:F0} bricht zwischen Zahl und Einheit bei {s}"));
                }
                if (Bruch(Probe(block, ungeschuetzt, breite), schutz) is not null)
                {
                    ungeschuetztGebrochen++;
                }
            }
            r.Trainer.ProbeEnde();
            r.Tab("rechner");

            // (41) sehr lange Zahl
            r.Waehle("uebertragung");
            r.Daten.DauerEingabe(Fw7Dauer.Groesse, Fw7Dauer.Einheit, Fw7Dauer.Rate, Fw7Dauer.RateEinheit, Fw7Dauer.Prozent);
            r.Daten.DauerRechnen();
            await ansicht.BildFertigAsync();
            await ansicht.BildFertigAsync();
            var kasten = r.Daten.DauerKasten;
            var zeilen = kasten.Angezeigt.Split('\n');
            if (zeilen.FirstOrDefault(z => z.TrimStart().StartsWith('.')) is { } punkt)
            {
                Fund($"{stufe}: Zeile beginnt mit Tausenderpunkt: \"{punkt.Trim()}\"");
            }
            if (!zeilen.Any(z => z.Contains("8.796.093.022.208.000.000.000.000,00", StringComparison.Ordinal)))
            {
                Fund($"{stufe}: lange Zahl der Dauer nicht ganz in einer Zeile");
            }
            var breiter = kasten.Rollen.Extent.Width > kasten.Rollen.Viewport.Width + 0.5;
            if (breiter != kasten.WischHinweis)
            {
                Fund($"{stufe}: Uebertragung {(breiter ? "ohne" : "mit")} Hinweis wischen");
            }
            notizen.Add(string.Create(inv, $"{stufe} {kasten.Zeichen} Zeichen{(kasten.WischHinweis ? " wischen" : "")}"));

            // (43) Rechenweg Verkauf
            r.Waehle("wiso");
            r.Wiso.Waehle("verkauf");
            r.Wiso.Rechnen("verkauf");
            var weg = r.Wiso.Weg("verkauf");
            weg.Zeigen(true);
            await ansicht.BildFertigAsync();
            await ansicht.BildFertigAsync();
            foreach (var zeile in weg.Kasten.Angezeigt.Split('\n'))
            {
                var t = zeile.Trim();
                if (t is "€" or "%" || t.StartsWith("€ ", StringComparison.Ordinal))
                {
                    Fund($"{stufe}: Einheit allein in der Zeile im Rechenweg Verkauf");
                }
                if (Zeilen.AnzeigeRechenzeichen.Any(z => t.EndsWith(" " + z, StringComparison.Ordinal)))
                {
                    Fund($"{stufe}: Rechenzeichen am Zeilenende: \"{t}\"");
                }
            }
            weg.Zeigen(false);
        }
        if (ungeschuetztGebrochen == 0)
        {
            Fund("ohne Schutz bricht der Aufgabentext in keiner Breite zwischen Zahl und Einheit, der Punkt prueft nichts");
        }
        Messwerte.Notiere("Rechner-Texte am Handy (FW7): " + string.Join("; ", notizen)
            + string.Create(inv, $"; ohne Schutz {ungeschuetztGebrochen} Brueche") + (funde == 0 ? "" : " FEHLER"));
        r.Daten.DauerEingabe(felderVorher.Groesse, felderVorher.Einheit, felderVorher.Rate, felderVorher.RateEinheit, felderVorher.Prozent);
        r.Daten.DauerRechnen();
        r.Wiso.Waehle(wisoVorher);
        r.Waehle(karteVorher);
        Schrift.Anwenden(Zustand.SchriftFaktor(Einstellungen.Schriftgroesse(Zustand.Werte)));
        fenster.Width = breiteVorher;
        fenster.Height = hoeheVorher;
        await ansicht.BildFertigAsync();
        return funde;
    }

    /// <summary>Text wie der Block gesetzt, in fester Breite.</summary>
    private static TextBlock Probe(TextBlock vorlage, string text, double breite)
    {
        var t = new TextBlock
        {
            Text = text,
            FontFamily = vorlage.FontFamily,
            FontSize = vorlage.FontSize,
            FontWeight = vorlage.FontWeight,
            TextWrapping = TextWrapping.Wrap,
        };
        t.Measure(new Size(breite, double.PositiveInfinity));
        return t;
    }

    /// <summary>Endet eine Zeile an einer der Stellen (vor oder nach dem Leerzeichen)? Dann die Stelle.</summary>
    private static int? Bruch(TextBlock block, List<int> stellen)
    {
        var zeilen = block.TextLayout.TextLines;
        for (var i = 0; i < zeilen.Count - 1; i++)
        {
            var ende = zeilen[i].FirstTextSourceIndex + zeilen[i].Length;
            if (stellen.FirstOrDefault(s => ende == s || ende == s + 1, -1) is var s && s >= 0)
            {
                return s;
            }
        }
        return null;
    }
}
