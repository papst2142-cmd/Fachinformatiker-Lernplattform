using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17): Start-Test-Teil "stadtbau" (Start ohne Bedienung, LERNPLATTFORM_MESSUNG_TEIL=stadtbau).
/// Legt Projekte an, prueft Fund 216 im Elektromarkt (rot, Export gesperrt), setzt, schiebt, dreht und nimmt zurueck,
/// speichert eine gruene Datei aus dem Moebelhaus und prueft sie wie beim Einbau; Layoutpruefung mit Screenreader-Namen
/// in PC-Groesse 1440 × 810 und Tablet-Groesse 1280 × 800, hell und dunkel, dazu der Hinweis in Handy-Groesse.
/// Messpunkte: Grenze 400 ms (Abbruch ab 1000 ms); ein wiederholbarer Punkt zaehlt erst rot, wenn drei Messungen darueber
/// liegen. Ergebnis wie beim Start-Test der Lernplattform (Zeilen "FEHLER: ...", Rueckgabe 0 oder 3).
/// </summary>
public static class StadtbauMessung
{
    public const double Grenze = 400;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly List<string> Funde = [];

    /// <summary>Groessen (Name, Breite, Hoehe) fuer die Layoutpruefung.</summary>
    public static readonly (string Name, double B, double H)[] Groessen = [("PC", 1440, 810), ("Tablet", 1280, 800)];

    public static void Einrichten()
    {
        var ordner = Environment.GetEnvironmentVariable(Werkzeugordner.Umgebung) is { Length: > 0 } s ? s
            : Environment.GetEnvironmentVariable("LERNPLATTFORM_DATEN") is { Length: > 0 } l ? Path.Combine(l, "stadtbau")
            : Path.Combine(Path.GetTempPath(), "stadtbau_starttest_" + Environment.ProcessId.ToString(Inv));
        StadtbauApp.Ordner = new Werkzeugordner(ordner);
        Hauptansicht.Automatisch = true;
        Hauptansicht.HandyRegelImmer = true;
        StadtbauApp.NachStart = (ansicht, fenster) => Dispatcher.UIThread.Post(() => _ = AusfuehrenAsync(ansicht, fenster));
    }

    private static void Fund(string text)
    {
        Funde.Add(text);
        Messwerte.Notiere("  FEHLER: " + text);
    }

    private static async Task BildAsync(Control c)
    {
        if (TopLevel.GetTopLevel(c) is not { } top)
        {
            await Task.Delay(50);
            return;
        }
        for (var i = 0; i < 2; i++)
        {
            var fertig = new TaskCompletionSource();
            top.RequestAnimationFrame(_ => fertig.TrySetResult());
            await fertig.Task;
        }
    }

    /// <summary>Wiederholbarer Messpunkt: bis zu 3 Messungen, rot nur, wenn alle ueber der Grenze liegen.</summary>
    private static async Task MessenAsync(string name, Control c, Func<Task> schritt, Func<Task>? zuruecksetzen = null, bool einmalig = false)
    {
        var werte = new List<double>();
        for (var versuch = 0; versuch < (einmalig ? 1 : 3); versuch++)
        {
            if (versuch > 0)
            {
                if (zuruecksetzen is not null)
                {
                    await zuruecksetzen();
                }
                await Task.Delay(200);
            }
            var uhr = Stopwatch.StartNew();
            await schritt();
            await BildAsync(c);
            werte.Add(uhr.Elapsed.TotalMilliseconds);
            Messwerte.Reihe(name, werte[^1]);
            if (werte[^1] <= Grenze)
            {
                return;
            }
        }
        Fund(string.Create(Inv, $"{name}: {werte.Min():F0} ms ueber Grenze {Grenze:F0} ms; Einzelmessungen {string.Join(", ", werte.Select(w => w.ToString("F0", Inv)))} ms")
            + (einmalig ? " (einmaliger Messpunkt, nicht wiederholt)" : ""));
    }

    private static async Task GroesseAsync(Hauptansicht a, Window? f, double b, double h)
    {
        if (f is not null)
        {
            f.MinWidth = 0;
            f.MinHeight = 0;
            f.Width = b;
            f.Height = h;
        }
        else
        {
            a.Width = b;
            a.Height = h;
        }
        await Task.Delay(150);
        await BildAsync(a);
        if (Math.Abs(a.Bounds.Width - b) > 1 || Math.Abs(a.Bounds.Height - h) > 1)
        {
            Fund(string.Create(Inv, $"Layout: Fenster {a.Bounds.Width:F0} x {a.Bounds.Height:F0} statt {b:F0} x {h:F0} (Bildschirm zu klein?)"));
        }
    }

    private static void Layout(Hauptansicht a, string wo)
    {
        var funde = Layoutpruefung.Pruefe(a);
        foreach (var z in funde)
        {
            Fund("Layout " + wo + ": " + z);
        }
        Messwerte.Notiere("Layoutpruefung " + wo + ": " + funde.Count.ToString(Inv) + " Funde");
    }

    private static async Task AlleGroessenAsync(Hauptansicht a, Window? f, string seite)
    {
        foreach (var dunkel in new[] { false, true })
        {
            if (a.Dunkel != dunkel)
            {
                a.FarbeWechseln();
            }
            foreach (var (name, b, h) in Groessen)
            {
                await GroesseAsync(a, f, b, h);
                Layout(a, seite + " " + name + " " + (dunkel ? "dunkel" : "hell"));
            }
        }
        if (a.Dunkel)
        {
            a.FarbeWechseln();
        }
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
    }

    private static async Task AusfuehrenAsync(Hauptansicht a, Window? f)
    {
        int rc;
        try
        {
            rc = await LaufAsync(a, f);
        }
        catch (Exception e)
        {
            StadtbauApp.Ordner.Fehler(e.ToString());
            Messwerte.Notiere("URTEIL: rot (Ausnahme " + e.GetType().Name + ": " + e.Message + ")");
            rc = 1;
        }
        var text = Messwerte.Bericht(Werkzeug.Kennzeile).Replace("Programm: A ", "Programm (Oberflaeche der Lernplattform): A ", StringComparison.Ordinal);
        var datei = Environment.GetEnvironmentVariable("LERNPLATTFORM_MESSUNG_DATEI") is { Length: > 0 } d ? d : "messung_stadtbau.txt";
        await File.WriteAllTextAsync(datei, text);
        Console.WriteLine(text);
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(rc == 0 ? 0 : 3);
        }
    }

    private static async Task<int> LaufAsync(Hauptansicht a, Window? f)
    {
        Messwerte.Notiere("Start-Test-Teil stadtbau: " + Werkzeug.Kennzeile + ", Ordner " + StadtbauApp.Ordner.Wurzel);
        var start = Stopwatch.StartNew();
        await a.Geladen;
        Messwerte.Notiere(string.Create(Inv, $"Start bis Spieldaten geladen (Angabe): {start.Elapsed.TotalMilliseconds:F0} ms"));
        if (a.Daten is null)
        {
            Fund("Start: Spieldaten nicht geladen");
            return Ende();
        }
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        await AlleGroessenAsync(a, f, "Start");

        // 1. Elektromarkt: Fund 216 rot, Export gesperrt
        await MessenAsync("Projekt anlegen (Elektromarkt)", a, () =>
        {
            a.NeuAnlegen("Starttest Elektromarkt", "elektromarkt");
            return Task.CompletedTask;
        }, einmalig: true);
        await a.Geprueft;
        var rot = a.Funde.Where(x => x.Rot).ToList();
        Messwerte.Notiere("Elektromarkt: " + rot.Count.ToString(Inv) + " rote, " + (a.Funde.Count - rot.Count).ToString(Inv) + " gelbe Funde");
        if (rot.Count != 8 || !rot.All(x => x.Text.Contains("erreich", StringComparison.OrdinalIgnoreCase)))
        {
            Fund("Elektromarkt: Fund 216 erwartet 8 rote Funde zur Erreichbarkeit, ist " + string.Join(" | ", rot.Select(x => x.Text)));
        }
        Messwerte.Notiere(string.Create(Inv, $"Elektromarkt Grenzen: {a.GrenzenStand.Dreiecke} Dreiecke, {a.GrenzenStand.Aufrufe} Aufrufe"));
        await AlleGroessenAsync(a, f, "Innenraeume");

        var p = a.Projekt!;
        var g = p.Grundriss("elektromarkt");
        var regal = Bearbeitung.Alle(g).First(s => Py.Str(s.Deko["typ"]) == "regal" && s.Stelle.Bereich != "flur").Stelle;
        a.Flaeche.Waehlen(regal);
        var vorher = PythonJson.Text(g);
        await MessenAsync("Stueck drehen", a, () =>
        {
            a.Flaeche.Drehen();
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            a.Flaeche.Waehlen(regal);
            return Task.CompletedTask;
        });
        await MessenAsync("Rueckgaengig", a, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        }, () =>
        {
            a.Flaeche.Waehlen(regal);
            a.Flaeche.Drehen();
            return Task.CompletedTask;
        });
        if (PythonJson.Text(p.Grundriss("elektromarkt")) != vorher)
        {
            Fund("Rueckgaengig: Grundriss nach Drehen und Rueckgaengig nicht wie vorher");
        }
        a.Flaeche.Waehlen(regal);
        await MessenAsync("Stueck verschieben (Taste)", a, () =>
        {
            a.Flaeche.Taste(Key.Down, KeyModifiers.None);
            return Task.CompletedTask;
        });
        await MessenAsync("Stueck setzen (Katalog)", a, () =>
        {
            a.Flaeche.SetzenTyp = "pflanze";
            if (a.Flaeche.SetzenAn(2, 12) is null)
            {
                Fund("Stueck setzen: Pflanze im Flur nicht gesetzt");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        await a.Geprueft;
        try
        {
            p.Exportieren(DateTimeOffset.Now);
            Fund("Export: Elektromarkt mit Fund 216 ist nicht gesperrt");
        }
        catch (StadtbauFehler e)
        {
            Messwerte.Notiere("Export Elektromarkt gesperrt: " + e.Funde.Count.ToString(Inv) + " Gruende");
        }

        // 2. Draufsicht
        await MessenAsync("Reiter Draufsicht", a, () =>
        {
            a.ReiterZeigen(innen: false);
            return Task.CompletedTask;
        }, () =>
        {
            a.ReiterZeigen(innen: true);
            return Task.CompletedTask;
        });
        await AlleGroessenAsync(a, f, "Draufsicht");

        // 3. Moebelhaus: kleine Aenderung, Datei speichern, wie beim Einbau pruefen
        a.NeuAnlegen("Starttest Moebelhaus", "moebelhaus");
        await MessenAsync("Innenraum oeffnen", a, () =>
        {
            a.OrtOeffnen("bank");
            return Task.CompletedTask;
        }, () =>
        {
            a.OrtOeffnen("moebelhaus");
            return Task.CompletedTask;
        });
        a.OrtOeffnen("moebelhaus");
        a.Projekt!.Aendern("moebelhaus", "Boden", gg => Bearbeitung.Boden(gg, "flur", "teppich"));
        await a.Geprueft;
        if (a.Funde.Any(x => x.Rot))
        {
            Fund("Moebelhaus: rote Funde nach Bodenwechsel: " + string.Join(" | ", a.Funde.Where(x => x.Rot).Select(x => x.Text)));
        }
        var uhr = Stopwatch.StartNew();
        string pfad;
        try
        {
            pfad = a.Projekt.ExportSpeichern(DateTimeOffset.Now);
        }
        catch (StadtbauFehler e)
        {
            Fund("Export Moebelhaus gesperrt: " + string.Join(" | ", e.Funde));
            return Ende();
        }
        Messwerte.Notiere(string.Create(Inv, $"Export pruefen und speichern (Angabe): {uhr.Elapsed.TotalMilliseconds:F0} ms"));
        var datei = File.ReadAllBytes(pfad);
        var format = StadtbauPruefung.Format(Py.Lesen(datei));
        var fassung = StadtFassung.Lesen(datei);
        var inhalt = StadtbauPruefung.Inhalt(fassung, a.Daten.Inhalt);
        foreach (var z in format.Concat(inhalt))
        {
            Fund("Export Moebelhaus: " + z);
        }
        Messwerte.Notiere("Export Moebelhaus: " + Path.GetFileName(pfad) + ", Fassung " + fassung.Nummer.ToString(Inv) + " auf Basis " + (fassung.Basis ?? -1).ToString(Inv));

        // 4. Handy: nur der Hinweis
        await GroesseAsync(a, f, 412, 915);
        if (!a.HandyHinweisSichtbar)
        {
            Fund("Handy: Hinweis \"" + Texte.NurHandy + "\" fehlt in Handy-Groesse");
        }
        Layout(a, "Handy 412x915");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        if (a.HandyHinweisSichtbar)
        {
            Fund("Handy: Hinweis bleibt in PC-Groesse sichtbar");
        }
        return Ende();
    }

    private static int Ende()
    {
        Messwerte.Notiere(Funde.Count == 0 ? "URTEIL: gruen" : "URTEIL: rot (" + Funde.Count.ToString(Inv) + " Funde)");
        return Funde.Count == 0 ? 0 : 3;
    }
}
