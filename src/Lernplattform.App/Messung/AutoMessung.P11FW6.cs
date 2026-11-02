using System.Globalization;
using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Design;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// FW6 (Weisung P11-12, Sammelliste 177 Teil 2): eine breite Tabelle (SI-Szenario "Nutzwertanalyse zur Auswahl einer
/// Firewall") steht am Handy in jeder Schriftstufe in einem eigenen Bereich mit fester Zeichenbreite, ohne Umbruch;
/// ist sie breiter als der Bereich, laesst sie sich seitlich wischen und darunter steht "→ wischen". In der groessten
/// Stufe muss sie wischbar sein (sonst prueft der Punkt nichts).
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Titel des Szenarios mit der breitesten Tabelle der Aufgabenstellung (SI).</summary>
    public const string Fw6TabellenSzenario = "Nutzwertanalyse zur Auswahl einer Firewall";

    private static async Task<int> P11Fw6TabellenAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("Tabellen am Handy (FW6): FEHLER, " + text);
            funde++;
        }
        if (TopLevel.GetTopLevel(ansicht) is not Window fenster)
        {
            Messwerte.Notiere("Tabellen am Handy (FW6): ohne Fenster nicht geprueft");
            return 0;
        }
        var vorher = Zustand.Sicht;
        var (breiteVorher, hoeheVorher) = (fenster.Width, fenster.Height);
        if (vorher != Fachrichtungen.Systemintegration)
        {
            ansicht.SichtAnwenden(Fachrichtungen.Systemintegration);
            await Zustand.Inhalte;
        }
        await ansicht.ZeigeAsync(Seiten.Ap2Szenarien);
        if (ansicht.GebauteSeiten[Seiten.Ap2Szenarien] is not SzenarioSeite seite)
        {
            Fund("Seite AP2-Szenarien fehlt");
            return funde;
        }
        await seite.Geladen;
        seite.Springen(Fw6TabellenSzenario);
        var (hb, hh) = Hoechstgroesse(fenster);
        fenster.Width = Math.Min(SchmalBreite, hb);
        fenster.Height = Math.Min(SchmalHoehe, hh);
        var erwartet = Tabellenzeilen.Teile(seite.Aufgabe.Text).Where(t => t.Tabelle).Select(t => t.Text).ToList();
        var zeilen = new List<string>();
        foreach (var stufe in Einstellungen.Schriftgroessen)
        {
            Schrift.Anwenden(Zustand.SchriftFaktor(stufe));
            await ansicht.BildFertigAsync();
            await ansicht.BildFertigAsync();
            var tabellen = seite.Aufgabe.Tabellen.ToList();
            if (erwartet.Count == 0 || tabellen.Count != erwartet.Count)
            {
                Fund(string.Create(inv, $"{stufe}: {tabellen.Count} Tabellenbereiche statt {erwartet.Count}"));
                continue;
            }
            for (var i = 0; i < tabellen.Count; i++)
            {
                var t = tabellen[i];
                var anzahl = erwartet[i].Split('\n').Length;
                var gesetzt = t.Zeilen.TextLayout.TextLines.Count;
                var breiter = t.Rollen.Extent.Width > t.Rollen.Viewport.Width + 0.5;
                zeilen.Add(string.Create(inv, $"{stufe} {t.Zeilen.FontSize:0.0} pt, {t.Rollen.Extent.Width:F0}/{t.Rollen.Viewport.Width:F0}{(t.WischHinweis ? " wischen" : "")}"));
                if (t.Zeilen.Text != erwartet[i] || gesetzt != anzahl)
                {
                    Fund(string.Create(inv, $"{stufe}: Tabelle {i + 1} mit {gesetzt} Zeilen statt {anzahl} oder anderem Text"));
                }
                if (breiter != t.WischHinweis)
                {
                    Fund(string.Create(inv, $"{stufe}: Tabelle {i + 1} {(breiter ? "ohne" : "mit")} Hinweis wischen"));
                }
                if (stufe == Einstellungen.Schriftgroessen[^1] && !breiter)
                {
                    Fund(string.Create(inv, $"{stufe}: Tabelle {i + 1} ist nicht wischbar, der Punkt prueft nichts"));
                }
            }
        }
        Messwerte.Notiere("Tabellen am Handy (FW6, " + Fw6TabellenSzenario + "): " + string.Join("; ", zeilen) + (funde == 0 ? "" : " FEHLER"));
        Schrift.Anwenden(Zustand.SchriftFaktor(Einstellungen.Schriftgroesse(Zustand.Werte)));
        fenster.Width = breiteVorher;
        fenster.Height = hoeheVorher;
        if (vorher != Fachrichtungen.Systemintegration)
        {
            ansicht.SichtAnwenden(vorher);
            await Zustand.Inhalte;
        }
        await ansicht.BildFertigAsync();
        return funde;
    }
}
