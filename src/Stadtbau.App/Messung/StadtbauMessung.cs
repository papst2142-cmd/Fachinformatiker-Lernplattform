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
public static partial class StadtbauMessung
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
        // Patch S 0.23.1 (selbst gefunden, Lauf 688): voller Pfad. In der Windows-CI kommt der Datenordner gemischt an
        // ("D:\a\_temp/daten_werkzeug1"); die Liste der Projekte nennt volle Pfade (nur "\"), die Vergleiche der Probe
        // "Projekte loeschen" fanden deshalb keinen Kasten. Die App selbst vergleicht schon mit Path.GetFullPath.
        StadtbauApp.Ordner = new Werkzeugordner(Path.GetFullPath(ordner));
        Hauptansicht.Automatisch = true;
        Hauptansicht.HandyRegelImmer = true;
        // Nachbesserung S 0.5.1 (Weisung P12-42): Testquelle fuer "Nach Updates suchen" (beim Start still, weil aktuell)
        Aktualisierung.Pruefen = () => Task.FromResult(UpdateTestErgebnis);
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
                BeschriftungPruefen(a, seite + " " + name + " " + (dunkel ? "dunkel" : "hell"));
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
            a.NeuAnlegen("Starttest Elektromarkt prüfen", "elektromarkt");
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
        a.NeuAnlegen("Starttest Möbelhaus", "moebelhaus");
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

        // 4. Stadt draussen (Welle 3): setzen, schieben, drehen, loeschen, Pruefung, 3D-Ansicht
        await DraussenAsync(a, f);
        await KatalogAsync(a, f);
        await LeereStadtAsync(a, f);
        await BlueprintsAsync(a, f);
        await BedienungAsync(a, f);
        await OrdnungAsync(a, f);
        await FingerAsync(a, f);
        await Bauen3DAsync(a, f);
        await WasserAsync(a, f);
        await EntwurfAsync(a, f);
        await VollbildAsync(a, f);
        await BildschirmWahlAsync(a, f);
        await RueckgaengigKnopfAsync(a, f);
        await BewegenAsync(a, f);
        await UpdateKnopfAsync(a, f);
        await AbwaehlenAsync(a, f);
        await KurvenAsync(a, f);
        await KreisverkehrAsync(a, f);
        await StreckenAsync(a, f);
        await EinrastenAsync(a, f);
        await DrehenAsync(a, f);
        await ZiehenAsync(a, f);
        await FusswegTuerAsync(a, f);
        await BahnAsync(a, f);
        await EtagenAsync(a, f);
        await ObergeschosseAsync(a, f);
        await AussehenAsync(a, f);
        await SpielgeschichteAsync(a, f);
        await ProjekteLoeschenAsync(a, f);
        await StadtDateiAsync(a, f);
        await TuerAsync(a, f);
        await TuerFreiAsync(a, f);
        await ZebrastreifenAsync(a, f);
        await WerkzeugKleinAsync(a, f);
        await LeereStadtOeffnenAsync(a);
        if (Funde.Any(x => x.StartsWith("Draussen: Ausnahme", StringComparison.Ordinal)))
        {
            return Ende();
        }

        // 5. Handy: nur der Hinweis
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

    /// <summary>Der eine bekannte rote Fund in Stadt 1 (Fassung 0, Weisung P12-30): Nico verschiebt Laterne 20 in seinem Projekt.</summary>
    internal const string Laterne20Fund = "Laterne Nr. 20 muss um 0,4 verschoben werden, sobald alle Orte zu sehen sind (erlaubt 0,2).";

    private static async Task DraussenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Straßen und Grün", null);
        await a.AussenGeprueft;
        var p = a.Projekt!;
        // Weisung P12-30 (Sammelliste 220): in Stadt 1 (Fassung 0) ist genau Laterne 20 im spaeten Spielstand rot, sonst nichts
        if (a.AussenFunde.Count(x => x.Rot) != 1 || !a.AussenFunde.Any(x => x.Rot && x.Text == Laterne20Fund))
        {
            Fund("Draussen Stadt 1: rote Funde (erwartet genau Laterne 20): " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot).Select(x => x.Text)));
        }
        Messwerte.Notiere("Draussen Stadt 1: " + a.AussenFunde.Count.ToString(Inv) + " Funde (gelb)");
        a.ReiterZeigen(innen: false);
        // Text ohne Auswahl draussen (Weisung P12-25): "auf der Karte", nie "im Grundriss"
        if (!a.AussenAuswahlTexte.Contains(Texte.NichtsGewaehltDraussen) || a.AussenAuswahlTexte.Contains(Texte.NichtsGewaehlt))
        {
            Fund("Draussen: Karte \"" + Texte.Auswahl + "\" zeigt \"" + string.Join(" ", a.AussenAuswahlTexte) + "\" statt \"" + Texte.NichtsGewaehltDraussen + "\"");
        }
        var vorher = PythonJson.Text(p.AussenStand);
        var k = a.Karte;
        await MessenAsync("Draussen setzen (Laterne)", a, () =>
        {
            k.SetzenArt = AussenArt.Laterne;
            if (k.SetzenAn(12, 20.3) is null)
            {
                Fund("Draussen setzen: Laterne an der Strasse nicht gesetzt");
            }
            k.SetzenArt = null;
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        a.Rueckgaengig();
        var baum = AussenBearbeitung.Alle(p.AussenStand).First(x => x.Stelle.Art == AussenArt.Baum).Stelle;
        k.Waehlen(baum);
        await MessenAsync("Draussen verschieben (Baum)", a, () =>
        {
            k.Verschieben(0.5, 0);
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            k.Waehlen(baum);
            return Task.CompletedTask;
        });
        a.Rueckgaengig();
        var park = AussenBearbeitung.Alle(p.AussenStand).First(x => x.Stelle.Art == AussenArt.Park).Stelle;
        k.Waehlen(park);
        await MessenAsync("Draussen drehen (Park)", a, () =>
        {
            k.Drehen();
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            k.Waehlen(park);
            return Task.CompletedTask;
        });
        a.Rueckgaengig();
        k.Waehlen(baum);
        await MessenAsync("Draussen loeschen (Baum)", a, () =>
        {
            k.Loeschen();
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            k.Waehlen(baum);
            return Task.CompletedTask;
        });
        a.Rueckgaengig();
        if (PythonJson.Text(p.AussenStand) != vorher)
        {
            Fund("Draussen: nach Setzen, Schieben, Drehen, Loeschen und Rueckgaengig nicht wie vorher");
        }
        var uhr = Stopwatch.StartNew();
        await a.AussenGeprueft;
        Messwerte.Notiere(string.Create(Inv, $"Pruefung draussen bis fertig (Angabe): {uhr.Elapsed.TotalMilliseconds:F0} ms"));
        // Liste der Funde: nach der Pruefung nie mehr "Pruefung laeuft", ein roter Fund steht in der Liste
        k.SetzenArt = AussenArt.Laterne;
        k.SetzenAn(12, 8);
        k.SetzenArt = null;
        await a.AussenGeprueft;
        await BildAsync(a);
        ListePruefen(a, rotErwartet: true);
        a.Rueckgaengig();
        await a.AussenGeprueft;
        await BildAsync(a);
        ListePruefen(a, rotErwartet: false);
        k.Waehlen(new AussenStelle(AussenArt.Laterne, 0));
        await AlleGroessenAsync(a, f, "Draussen");

        // 3D: erstes Oeffnen (einmalig), nach einer Aenderung (wiederholbar), Grenzen der Gesamtansicht
        a.DreiZeigen();
        await a.DreiFertig;
        DreiGrenze("3D-Ansicht oeffnen (bis zum Bild)", [a.DreiZeitMs], einmalig: true);
        if (!a.Drei.GlBereit)
        {
            Messwerte.Notiere("3D-Ansicht: kein OpenGL auf diesem Rechner, Hinweis gezeigt (keine Messung der Grenzen)");
        }
        var werte = new List<double>();
        for (var i = 0; i < 3; i++)
        {
            k.Waehlen(baum);
            k.Verschieben(i % 2 == 0 ? 0.5 : -0.5, 0);
            await a.DreiFertig;
            werte.Add(a.DreiZeitMs);
            if (a.DreiZeitMs <= Grenze)
            {
                break;
            }
            await Task.Delay(200);
        }
        DreiGrenze("3D nach Aenderung (bis zum Bild)", werte, einmalig: false);
        var nacht = new Ansicht3D.Einstellung(true, Jahreszeit.Winter, Festzeit.Weihnachten);
        a.EinstellungSetzen(nacht);
        await a.DreiFertig;
        if (a.DreiWahlText != "Nacht / Winter / Weihnachten")
        {
            Fund("3D-Ansicht: Auswahl zeigt \"" + a.DreiWahlText + "\" statt \"Nacht / Winter / Weihnachten\"");
        }
        a.EinstellungSetzen(Ansicht3D.Einstellung.Standard);
        await a.DreiFertig;
        if (a.Drei.GlBereit)
        {
            await a.GrenzenMessenAsync();
            if (a.GrenzenDraussen is { } gz)
            {
                Messwerte.Notiere(string.Create(Inv, $"Grenzen Gesamtansicht Stadt 1 (Nacht, Winter, Weihnachten): {gz.Dreiecke} Dreiecke, {gz.Aufrufe} Aufrufe"));
                if (gz.Dreiecke > StadtbauGrenzen.Dreiecke || gz.Aufrufe > StadtbauGrenzen.Aufrufe)
                {
                    Fund(string.Create(Inv, $"Grenzen Gesamtansicht: {gz.Dreiecke} Dreiecke / {gz.Aufrufe} Aufrufe ueber {StadtbauGrenzen.Dreiecke} / {StadtbauGrenzen.Aufrufe}"));
                }
            }
            else
            {
                Fund("Grenzen Gesamtansicht: keine Messung");
            }
        }
        await AlleGroessenAsync(a, f, "3D-Ansicht");
        a.ReiterZeigen(innen: false);
    }

    /// <summary>
    /// Phase 13 Welle 3b (Weisung P12-19 Abschnitt 2, P12-24): Beschriftungen der Draufsicht liegen ganz in der Karte
    /// (Fund Chat: "TALHEIM" ragte am Tablet ueber den Rand). Gilt nur, wenn die Draufsicht zu sehen ist.
    /// </summary>
    private static int BeschriftungPruefen(Hauptansicht a, string wo, bool melden = true)
    {
        var k = a.Karte;
        if (!k.IsEffectivelyVisible || k.Bounds.Width < 10)
        {
            return 0;
        }
        var r = k.KartenRand;
        var draussen = 0;
        foreach (var (text, b) in k.Beschriftungen)
        {
            if (!Beschriftung.Innen(b.X, b.Y, b.Width, b.Height, r.Left, r.Top, r.Right, r.Bottom))
            {
                draussen++;
                if (melden)
                {
                    Fund(string.Create(Inv, $"Layout {wo}: Beschriftung \"{text}\" ragt ueber den Kartenrand ({b.X:F0},{b.Y:F0} {b.Width:F0}x{b.Height:F0}, Karte {r.Left:F0},{r.Top:F0} bis {r.Right:F0},{r.Bottom:F0})"));
                }
            }
        }
        return draussen;
    }

    /// <summary>Form der Effekte in der Draufsicht: genau eine, verschieden von der Laterne (Weisung P12-25).</summary>
    private static string? EffektFormPruefen(Hauptansicht a, string wo)
    {
        var effekte = a.Karte.Formen.Where(x => x.Effekt).Select(x => x.Form).Distinct().ToList();
        var laterne = a.Karte.Formen.Where(x => !x.Effekt).Select(x => x.Form).FirstOrDefault();
        if (effekte.Count != 1)
        {
            Fund("Effekt-Zeichen " + wo + ": " + effekte.Count.ToString(Inv) + " Formen statt einer");
            return effekte.FirstOrDefault();
        }
        if (laterne is null)
        {
            Fund("Effekt-Zeichen " + wo + ": keine Laterne zum Vergleich gezeichnet");
        }
        else if (laterne == effekte[0])
        {
            Fund("Effekt-Zeichen " + wo + ": Effekt sieht aus wie eine Laterne (" + laterne + ")");
        }
        return effekte[0];
    }

    /// <summary>
    /// Weisung P12-25 Abschnitt 3 Punkt 1: alle Kacheln des Katalogs gleich breit und gleich hoch, die Kacheln einer Reihe
    /// mit gleicher Ober- und Unterkante, das Bild in jeder Kachel an derselben Stelle. Gibt die Zahl der Funde zurueck.
    /// </summary>
    private const string DreiZeilenProbe = "Vorderrad, Kleinwagen, Polizei, Feuerwehr, Taxi";

    private static void NamenPruefen(Katalog kat, string wo)
    {
        var soll = Lernplattform.Design.Schrift.Grundgroessen["Klein"] * Lernplattform.Design.Schrift.Faktor;
        var zuLang = kat.Eintraege.Where(e => !KatalogAnsicht.NamensGroesse(e.Anzeige, soll).Passt).Select(e => e.Anzeige).ToList();
        var kleiner = kat.Eintraege.Count(e => KatalogAnsicht.NamensGroesse(e.Anzeige, soll).Groesse < soll);
        Messwerte.Notiere("Katalog-Namen " + wo + ": " + kat.Eintraege.Count.ToString(Inv) + " geprueft, " + kleiner.ToString(Inv) + " verkleinert, " + zuLang.Count.ToString(Inv) + " passen nicht");
        foreach (var n in zuLang)
        {
            Fund("Katalog-Name passt nicht in die Kachel (" + wo + "): " + n);
        }
        if (KatalogAnsicht.NamensGroesse("Hochgeschwindigkeitszugwagen", soll).Passt)
        {
            Fund("Katalog-Namen Gegenprobe (" + wo + "): zu langes Wort passt (Probe taugt nicht)");
        }
        if (KatalogAnsicht.NamensGroesse(DreiZeilenProbe, soll).Passt)
        {
            Fund("Katalog-Namen Gegenprobe (" + wo + "): Name mit drei Zeilen passt (Probe taugt nicht)");
        }
        var probe = KatalogAnsicht.NamensText(DreiZeilenProbe);
        probe.Measure(new Size(KatalogAnsicht.NamenPlatz + 1, double.PositiveInfinity));
        probe.Arrange(new Rect(probe.DesiredSize));
        if (!KatalogAnsicht.NameGekuerzt(probe))
        {
            Fund("Katalog-Namen Gegenprobe (" + wo + "): Kachel zeigt einen Namen mit drei Zeilen nicht als gekuerzt (Probe taugt nicht)");
        }
    }

    private static int RasterPruefen(KatalogAnsicht ansicht, string wo, bool melden = true)
    {
        var funde = new List<string>();
        Size? mass = null;
        Point? bildLage = null;
        foreach (var gruppe in ansicht.KachelGruppen)
        {
            // Reihen: eine neue Reihe beginnt, wenn eine Kachel nicht rechts der vorigen steht
            var reihe = new List<Button>();
            void ReiheAbschliessen()
            {
                if (reihe.Count > 1)
                {
                    var oben = reihe.Select(k => Math.Round(k.Bounds.Top, 1)).Distinct().Count();
                    var unten = reihe.Select(k => Math.Round(k.Bounds.Bottom, 1)).Distinct().Count();
                    if (oben > 1 || unten > 1)
                    {
                        funde.Add(string.Create(Inv, $"Reihe ab \"{((KatalogEintrag)reihe[0].Tag!).Anzeige}\": {oben} Oberkanten, {unten} Unterkanten ({string.Join(" ", reihe.Select(k => k.Bounds.Top.ToString("F1", Inv) + "-" + k.Bounds.Bottom.ToString("F1", Inv)))})"));
                    }
                }
                reihe.Clear();
            }
            foreach (var k in gruppe)
            {
                if (reihe.Count > 0 && k.Bounds.Left <= reihe[^1].Bounds.Left)
                {
                    ReiheAbschliessen();
                }
                reihe.Add(k);
                var groesse = new Size(Math.Round(k.Bounds.Width, 1), Math.Round(k.Bounds.Height, 1));
                mass ??= groesse;
                if (groesse != mass)
                {
                    funde.Add(string.Create(Inv, $"Kachel \"{((KatalogEintrag)k.Tag!).Anzeige}\" {groesse.Width:F1}x{groesse.Height:F1} statt {mass.Value.Width:F1}x{mass.Value.Height:F1}"));
                }
                // Weisung P12-79: der gezeichnete Name steht ganz da (kein "…", kein Wort mitten getrennt)
                if (KatalogAnsicht.KachelName(k) is { } nameText && KatalogAnsicht.NameGekuerzt(nameText))
                {
                    funde.Add("Kachel \"" + ((KatalogEintrag)k.Tag!).Anzeige + "\": Name gekuerzt oder Wort getrennt");
                }
                if (KatalogAnsicht.KachelBild(k)?.TranslatePoint(default, k) is { } lage)
                {
                    lage = new Point(Math.Round(lage.X, 1), Math.Round(lage.Y, 1));
                    bildLage ??= lage;
                    if (lage != bildLage)
                    {
                        funde.Add(string.Create(Inv, $"Kachel \"{((KatalogEintrag)k.Tag!).Anzeige}\": Bild bei {lage.X:F1},{lage.Y:F1} statt {bildLage.Value.X:F1},{bildLage.Value.Y:F1}"));
                    }
                }
            }
            ReiheAbschliessen();
        }
        if (mass is null)
        {
            funde.Add("keine Kachel gezeigt");
        }
        if (melden)
        {
            foreach (var x in funde.Take(5))
            {
                Fund("Layout " + wo + ": Katalog-Raster: " + x);
            }
        }
        return funde.Count;
    }

    /// <summary>Phase 13 Welle 3b: voller Katalog (oeffnen, Gruppe, Suche, setzen innen und draussen, Grenzen, Layout).</summary>
    private static async Task KatalogAsync(Hauptansicht a, Window? f)
    {
        var kat = Katalog.Aktiv;
        if (kat is null)
        {
            Fund("Katalog: voller Katalog nicht geladen");
            return;
        }
        Messwerte.Notiere("Katalog: " + kat.Eintraege.Count.ToString(Inv) + " Stuecke in " + kat.PaketTitel.Count.ToString(Inv) + " Paketen, " + kat.Gruppen.Count.ToString(Inv) + " Gruppen");

        // Gegenprobe Beschriftung: ohne Rand-Regel ragt am Tablet mindestens eine ueber den Rand (sonst taugt die Probe nicht)
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[1].B, Groessen[1].H);
        a.Karte.Einpassen();
        Beschriftung.OhneRandFuerTests = true;
        a.Karte.InvalidateVisual();
        await BildAsync(a);
        var ohne = BeschriftungPruefen(a, "Gegenprobe", melden: false);
        Beschriftung.OhneRandFuerTests = false;
        a.Karte.InvalidateVisual();
        await BildAsync(a);
        var mit = BeschriftungPruefen(a, "Tablet nach Gegenprobe");
        Messwerte.Notiere("Beschriftung Gegenprobe Tablet: ohne Rand-Regel " + ohne.ToString(Inv) + " ueber dem Rand, mit Regel " + mit.ToString(Inv));
        if (ohne == 0)
        {
            Fund("Beschriftung Gegenprobe: ohne Rand-Regel ragt keine Beschriftung ueber den Kartenrand (Probe taugt nicht)");
        }
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // draussen: erstes Oeffnen (einmalig), dann nur gelesen, was gezeigt wird
        KatalogAnsicht? ansicht = null;
        await MessenAsync("Katalog oeffnen (erstes Oeffnen, bis zum Bild)", a, () =>
        {
            ansicht = a.KatalogOeffnen(innen: false);
            return Task.CompletedTask;
        }, einmalig: true);
        var gezeigtePakete = ansicht!.GezeigteStuecke.Select(e => e.Paket).ToHashSet(StringComparer.Ordinal);
        if (kat.GeleseneModellPakete.Count > 0 || kat.GeleseneVorschauPakete.Any(p => !gezeigtePakete.Contains(p)))
        {
            Fund("Katalog laedt mehr als er zeigt: Modelle " + string.Join(", ", kat.GeleseneModellPakete) + "; Vorschau " + string.Join(", ", kat.GeleseneVorschauPakete));
        }
        Messwerte.Notiere("Katalog nach dem Oeffnen gelesen: Vorschaubilder " + string.Join(", ", kat.GeleseneVorschauPakete) + "; Modelle " + kat.GeleseneModellPakete.Count.ToString(Inv));
        var gruppe = kat.Gruppen.FirstOrDefault(g => g != ansicht.Gruppe && kat.InGruppe(g).All(e => !kat.GeleseneVorschauPakete.Contains(e.Paket)))
            ?? kat.Gruppen.First(g => g != ansicht.Gruppe);
        await MessenAsync("Katalog Gruppe oeffnen (erstes Oeffnen, bis zum Bild)", a, () =>
        {
            ansicht.GruppeZeigen(gruppe);
            return Task.CompletedTask;
        }, einmalig: true);
        Messwerte.Notiere("Katalog Gruppe " + gruppe + ": " + ansicht.Treffer.ToString(Inv) + " Stuecke, gezeigt " + ansicht.Gezeigt.ToString(Inv));
        await MessenAsync("Katalog Suche (bis zum Bild)", a, () =>
        {
            ansicht.Suchen("baum");
            return Task.CompletedTask;
        }, () =>
        {
            ansicht.Suchen("");
            return Task.CompletedTask;
        });
        foreach (var wort in new[] { "Baum", "tree", "Stuhl", "chair", "Kürbis", "Auto" })
        {
            ansicht.Suchen(wort);
            if (ansicht.Treffer == 0)
            {
                Fund("Katalog Suche: \"" + wort + "\" findet nichts");
            }
        }
        // Sammelliste 222, Weisung P12-79: jeder deutsche Name passt ganz in seine Kachel (hoechstens 2 Zeilen, kein "…",
        // kein Wort getrennt), alle 1 521 in jeder geprueften Groesse; Gegenproben: ein zu langes Wort und ein Name mit drei
        // Zeilen passen auch verkleinert nicht, und die Kachel zeigt ihn gekuerzt
        foreach (var (_, b, h) in Groessen)
        {
            await GroesseAsync(a, f, b, h);
            NamenPruefen(kat, string.Create(Inv, $"{b}x{h}"));
        }
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // Raster (Weisung P12-25): Gegenprobe mit den alten Kacheln, dann jede Gruppe in PC- und Tablet-Groesse
        var mitNamen = kat.Gruppen.First(g => kat.InGruppe(g).Any(e => e.Art == KatalogArt.Modell));
        KatalogAnsicht.UngleichFuerTests = true;
        ansicht.GruppeZeigen(kat.Gruppen.First(g => kat.InGruppe(g).Any(e => e.Art == KatalogArt.Effekt)));
        await BildAsync(a);
        var ungleich = RasterPruefen(ansicht, "Gegenprobe Effekte", melden: false);
        ansicht.GruppeZeigen(mitNamen);
        await BildAsync(a);
        ungleich += RasterPruefen(ansicht, "Gegenprobe " + mitNamen, melden: false);
        KatalogAnsicht.UngleichFuerTests = false;
        Messwerte.Notiere("Katalog-Raster Gegenprobe (alte Kacheln): " + ungleich.ToString(Inv) + " Funde");
        if (ungleich == 0)
        {
            Fund("Katalog-Raster Gegenprobe: alte Kacheln ohne Fund (Probe taugt nicht)");
        }
        var rasterFunde = 0;
        foreach (var (_, b, h) in Groessen)
        {
            await GroesseAsync(a, f, b, h);
            foreach (var g in kat.Gruppen)
            {
                ansicht.GruppeZeigen(g);
                await BildAsync(a);
                rasterFunde += RasterPruefen(ansicht, string.Create(Inv, $"{b}x{h} {g}"));
            }
        }
        Messwerte.Notiere("Katalog-Raster: " + kat.Gruppen.Count.ToString(Inv) + " Gruppen in " + Groessen.Length.ToString(Inv) + " Groessen, " + rasterFunde.ToString(Inv) + " Funde");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        ansicht.Suchen("baum");
        await AlleGroessenAsync(a, f, "Katalog draussen");

        // draussen setzen: ein Baum aus dem Katalog und ein Effekt, Grenzen mit echten Dreiecken
        var p = a.Projekt!;
        var baum = ansicht.GezeigteStuecke.First(e => e.Art == KatalogArt.Modell);
        ansicht.Waehlen(baum);
        if (ansicht.IsVisible)
        {
            Fund("Katalog: nach der Wahl eines Stuecks bleibt der Katalog offen");
        }
        if (a.Karte.SetzenAn(2, 7) is not { Art: AussenArt.Katalog })
        {
            Fund("Katalog draussen: " + baum.Anzeige + " nicht gesetzt");
        }
        var effekt = kat.Eintraege.First(e => e.Art == KatalogArt.Effekt);
        a.KatalogOeffnen(innen: false).Waehlen(effekt);
        a.Karte.SetzenAn(2, 12);
        a.Karte.SetzenArt = null;
        await a.AussenGeprueft;
        if (a.AussenFunde.Any(x => x.Rot && x.Text != Laterne20Fund))
        {
            Fund("Katalog draussen: rote Funde: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot).Select(x => x.Text)));
        }
        var dreiecke = KatalogPruefung.DreieckeAussen(kat, p.AussenStand);
        if (dreiecke != baum.Dreiecke + KatalogPruefung.EffektDreiecke)
        {
            Fund(string.Create(Inv, $"Katalog draussen: Grenzen zaehlen {dreiecke} Dreiecke statt {baum.Dreiecke + KatalogPruefung.EffektDreiecke}"));
        }
        await BildAsync(a);
        BeschriftungPruefen(a, "Katalog draussen gesetzt");
        // Effekt-Zeichen (Weisung P12-25): eigene Form, nie wie eine Laterne; Gegenprobe mit dem alten Punkt
        var formDraussen = EffektFormPruefen(a, "Draufsicht");
        EffektZeichen.AltFuerTests = true;
        a.Karte.InvalidateVisual();
        await BildAsync(a);
        var alteGleich = a.Karte.Formen.Where(x => x.Effekt).All(x => a.Karte.Formen.Any(l => !l.Effekt && l.Form == x.Form));
        EffektZeichen.AltFuerTests = false;
        a.Karte.InvalidateVisual();
        await BildAsync(a);
        Messwerte.Notiere("Effekt-Zeichen: Draufsicht " + (formDraussen ?? "-") + ", Gegenprobe alter Punkt wie Laterne: " + (alteGleich ? "ja" : "nein"));
        if (!alteGleich)
        {
            Fund("Effekt-Zeichen Gegenprobe: der alte Punkt gilt nicht als gleich der Laterne (Probe taugt nicht)");
        }

        // innen: Stuhl aus dem Katalog im Moebelhaus, gilt als Teil aus dem Katalog
        a.NeuAnlegen("Starttest Katalog innen", "moebelhaus");
        a.OrtOeffnen("moebelhaus");
        var innen = a.KatalogOeffnen(innen: true);
        innen.Suchen("Stuhl");
        await BildAsync(a);
        await AlleGroessenAsync(a, f, "Katalog innen");
        var stuhl = innen.GezeigteStuecke.FirstOrDefault(e => e.Art == KatalogArt.Modell);
        if (stuhl is null)
        {
            Fund("Katalog innen: kein Stuhl gefunden");
            return;
        }
        innen.Waehlen(stuhl);
        if (a.Flaeche.SetzenAn(2, 12) is null)
        {
            Fund("Katalog innen: " + stuhl.Anzeige + " nicht gesetzt");
        }
        a.Flaeche.SetzenTyp = null;
        await a.Geprueft;
        var falsch = a.Funde.Where(x => x.Text.Contains(Katalog.Praefix, StringComparison.Ordinal) || x.Text.Contains("kein Teil aus dem Katalog", StringComparison.Ordinal)).ToList();
        foreach (var x in falsch)
        {
            Fund("Katalog innen: " + x.Text);
        }
        Messwerte.Notiere(string.Create(Inv, $"Katalog innen: {stuhl.Anzeige} gesetzt, {a.Funde.Count} Funde, Grenzen {a.GrenzenStand.Dreiecke} Dreiecke / {a.GrenzenStand.Aufrufe} Aufrufe"));
        // Effekt im Grundriss: dasselbe Zeichen wie in der Draufsicht
        a.KatalogOeffnen(innen: true).Waehlen(effekt);
        if (a.Flaeche.SetzenAn(3, 12) is null)
        {
            Fund("Katalog innen: Effekt " + effekt.Anzeige + " nicht gesetzt");
        }
        a.Flaeche.SetzenTyp = null;
        a.Flaeche.InvalidateVisual();
        await BildAsync(a);
        var innenFormen = a.Flaeche.EffektFormen.Distinct().ToList();
        if (innenFormen.Count != 1 || innenFormen[0] != formDraussen)
        {
            Fund("Effekt-Zeichen: Grundriss zeigt " + (innenFormen.Count == 0 ? "keinen Effekt" : string.Join(", ", innenFormen)) + ", Draufsicht " + (formDraussen ?? "keinen"));
        }
    }

    private static async Task LeereStadtAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Leere Stadt", null, (16, 12));
        await a.AussenGeprueft;
        Messwerte.Notiere("Leere Stadt: " + a.AussenFunde.Count.ToString(Inv) + " Funde (" + string.Join(" | ", a.AussenFunde.Select(x => x.Text)) + ")");
        if (a.AussenFunde.Any(x => x.Rot))
        {
            Fund("Leere Stadt: rote Funde: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot).Select(x => x.Text)));
        }
        await AlleGroessenAsync(a, f, "Leere Stadt Draufsicht");
        a.DreiZeigen();
        await a.DreiFertig;
        if (a.Drei.Spiel is null)
        {
            Fund("Leere Stadt: 3D-Ansicht nicht gebaut");
        }
        Messwerte.Reihe("3D-Ansicht leere Stadt (Angabe)", a.DreiZeitMs);
        a.Karte.SetzenArt = AussenArt.Strasse;
        a.Karte.SetzenAn(8, 6);
        a.Karte.SetzenArt = null;
        await a.DreiFertig;
        await a.AussenGeprueft;
        if (a.Drei.Spiel is null)
        {
            Fund("Leere Stadt mit Strasse: 3D-Ansicht nicht gebaut");
        }
        await AlleGroessenAsync(a, f, "Leere Stadt 3D");
        a.ReiterZeigen(innen: false);
    }

    /// <summary>
    /// Phase 13 Welle 4c (Weisung P12-32): Moebelhaus aus Stadt 1 als Blueprint speichern, eine kaputte Datei steht mit
    /// Hinweis in der Liste, Blueprint in einer leeren Stadt setzen (Messpunkt, 400 ms), Pruefung gruen, Layout und
    /// Namen in allen Groessen, Umbenennen und Loeschen.
    /// </summary>
    /// <summary>Knoepfe eines Eintrags der Liste "Meine Blueprints", die nicht ganz in seinem Kasten liegen.</summary>
    private static List<string> KaestenPruefen(Hauptansicht a)
    {
        var funde = new List<string>();
        foreach (var (kasten, knoepfe) in a.BlueprintKaesten)
        {
            var innen = new Rect(kasten.Bounds.Size);
            foreach (var k in knoepfe)
            {
                var name = k.Content is TextBlock t ? t.Text : "?";
                if (k.TranslatePoint(new Point(0, 0), kasten) is not { } p || !k.IsEffectivelyVisible
                    || !innen.Contains(new Rect(p, k.Bounds.Size).Deflate(0.5)))
                {
                    funde.Add("Knopf \"" + name + "\" liegt nicht im Kasten seines Eintrags");
                }
            }
        }
        return funde;
    }

    private static async Task BlueprintsAsync(Hauptansicht a, Window? f)
    {
        const string Name = "Starttest Möbelhaus";
        a.NeuAnlegen("Starttest Blueprint Quelle", null);
        await a.AussenGeprueft;
        var haus = new AussenStelle(AussenArt.Haus, -1, 0, "moebelhaus");
        a.Karte.Waehlen(haus);
        if (!a.AussenAuswahlKnoepfe.Contains(Texte.AlsBlueprintSpeichern))
        {
            Fund("Blueprint: Knopf \"" + Texte.AlsBlueprintSpeichern + "\" fehlt am Moebelhaus");
        }
        if (a.BlueprintSpeichern(haus, Name) is { } grund)
        {
            Fund("Blueprint: Moebelhaus nicht gespeichert: " + grund);
            return;
        }
        if (a.BlueprintSpeichern(haus, Name) is null)
        {
            Fund("Blueprint: gleicher Name ein zweites Mal gespeichert");
        }
        File.WriteAllText(Path.Combine(a.Ablage.Blueprints, "Starttest kaputt" + Blueprint.Endung), "{");
        a.BlueprintsZeigen();
        if (a.Blueprints.Count != 2 || a.Blueprints.Count(e => e.Fehler is not null) != 1)
        {
            Fund("Blueprint: Liste zeigt " + a.Blueprints.Count.ToString(Inv) + " Eintraege (erwartet 2, einer kaputt)");
        }
        // Weisung P12-33: jeder Knopf liegt im Kasten seines Eintrags (Gegenprobe: alte Liste ohne Kaesten um die Knoepfe)
        // Welle Q: die Liste steht in der Gruppe „Gebäude“ (ein Tippen auf den Kopf oeffnet sie)
        a.GruppeOeffnenFuer(a.BlueprintListe);
        await BildAsync(a);
        foreach (var fund in KaestenPruefen(a))
        {
            Fund("Blueprint-Liste: " + fund);
        }
        Hauptansicht.BlueprintListeAltFuerTests = true;
        a.BlueprintsZeigen();
        await BildAsync(a);
        if (KaestenPruefen(a).Count == 0)
        {
            Fund("Blueprint-Liste: Gegenprobe (Knoepfe ausserhalb der Kaesten) ohne Fund");
        }
        Hauptansicht.BlueprintListeAltFuerTests = false;
        a.BlueprintsZeigen();
        var b = a.Blueprints.First(e => e.Vorlage is not null).Vorlage!;

        // leere Stadt mit einer Strasse, Blueprint davor setzen (Fussweg wird bis zur Strasse verlaengert)
        a.NeuAnlegen("Starttest Blueprint leer", null, (24, 18));
        a.Karte.SetzenArt = AussenArt.Strasse;
        a.Karte.SetzenAn(12, 8);
        await a.AussenGeprueft;
        var ende = SpielDaten.Liste(b.Weg[^1]);
        var richtung = Math.Sign(Py.F(ende[1]) - Py.F(b.Tuer![1]));
        var x = 12.0;
        var y = 8 - Py.F(ende[1]) + b.Mitte().Y - richtung;
        AussenStelle? gesetzt = null;
        await MessenAsync("Blueprint setzen", a, () =>
        {
            a.BlueprintSetzenWaehlen(b);
            gesetzt = a.Karte.SetzenAn(x, y);
            if (gesetzt is not { Art: AussenArt.Gebaeude })
            {
                Fund("Blueprint setzen: nicht gesetzt");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        await a.AussenGeprueft;
        if (a.AussenFunde.Any(z => z.Rot))
        {
            Fund("Blueprint gesetzt: rote Funde: " + string.Join(" | ", a.AussenFunde.Where(z => z.Rot).Select(z => z.Text)));
        }
        // ein zweites Mal: rot (das Spiel zeichnet die Tuer nur am ersten Gebaeude dieses Modells)
        a.BlueprintSetzenWaehlen(b);
        a.Karte.SetzenAn(x + 6, y);
        await a.AussenGeprueft;
        if (!a.AussenFunde.Any(z => z.Rot && z.Text.StartsWith(Texte.BlueprintSchonDa, StringComparison.Ordinal)))
        {
            Fund("Blueprint zweimal gesetzt: kein roter Fund \"" + Texte.BlueprintSchonDa + "\"");
        }
        a.Rueckgaengig();
        await a.AussenGeprueft;
        a.Karte.Waehlen(gesetzt);
        await BildAsync(a);
        await AlleGroessenAsync(a, f, "Blueprints");
        a.DreiZeigen();
        await a.DreiFertig;
        if (a.Drei.Spiel is null)
        {
            Fund("Blueprint: 3D-Ansicht nicht gebaut");
        }
        a.ReiterZeigen(innen: false);

        // Innenraum des neuen Gebaeudes (Meldung P12-23 Frage 1 a): kommt mit, gelber Hinweis draussen, Reiter Innenraeume
        const string bau = Projekt.BauPraefix + "moebelhaus";
        if (!a.Projekt!.Innenorte.Contains(bau))
        {
            Fund("Blueprint: Innenraum des neuen Gebaeudes fehlt im Projekt");
        }
        else
        {
            if (!a.AussenFunde.Any(z => !z.Rot && z.Text == Texte.BauInnenNochNicht(Name)))
            {
                Fund("Blueprint: gelber Hinweis \"" + Texte.BauInnenNochNicht(Name) + "\" fehlt");
            }
            a.OrtOeffnen(bau);
            await a.Geprueft;
            if (a.Funde.Any(z => z.Rot))
            {
                Fund("Neues Gebaeude innen: rote Funde: " + string.Join(" | ", a.Funde.Where(z => z.Rot).Select(z => z.Text)));
            }
            await BildAsync(a);
            await AlleGroessenAsync(a, f, "Neues Gebaeude innen");
            a.ReiterZeigen(innen: false);
        }

        // Umbenennen und Loeschen (Rueckfrage ist ein Fenster; hier der Schritt danach)
        var eintrag = a.Blueprints.First(e => e.Vorlage is not null);
        if (a.BlueprintUmbenennen(eintrag, "Starttest Möbelhaus Süd") is { } nicht)
        {
            Fund("Blueprint umbenennen: " + nicht);
        }
        if (!a.Blueprints.Any(e => e.Name == "Starttest Möbelhaus Süd"))
        {
            Fund("Blueprint umbenennen: neuer Name fehlt in der Liste");
        }
        foreach (var e in a.Blueprints.ToList())
        {
            a.BlueprintLoeschen(e);
        }
        if (a.Blueprints.Count != 0)
        {
            Fund("Blueprint loeschen: Liste nicht leer");
        }
        Messwerte.Notiere("Blueprints: gespeichert, gesetzt, umbenannt, geloescht; Sicherungen " + Directory.GetFiles(Path.Combine(a.Ablage.Sicherungen, Sicherungen.BlueprintOrdner), "*", SearchOption.AllDirectories).Length.ToString(Inv));
    }

    /// <summary>
    /// Patch S 0.5.1 (Weisung P12-35): Knopf "Werkzeug beenden" ganz rechts in der Kopfleiste mit Namen und Tipp; jeder
    /// Knopf der Kopfleiste und jede Gruppe links hat einen Tipp; langer Druck zeigt nur den Tipp; Rueckfrage beim Beenden
    /// nur bei einer nicht gespeicherten Aenderung, mit "Abbrechen" bleibt das Werkzeug offen.
    /// </summary>
    private static List<string> BeendenKnopfPruefen(Hauptansicht a)
    {
        var funde = new List<string>();
        var k = a.BeendenKnopfElement;
        if (!k.IsEffectivelyVisible || k.TranslatePoint(new Point(0, 0), a) is not { } p)
        {
            funde.Add("Knopf \"" + Texte.WerkzeugBeenden + "\" fehlt in der Kopfleiste");
            return funde;
        }
        var rechts = p.X + k.Bounds.Width;
        if (rechts < a.Bounds.Width - 40 || rechts > a.Bounds.Width)
        {
            funde.Add(string.Create(Inv, $"Knopf \"{Texte.WerkzeugBeenden}\" nicht ganz rechts (rechter Rand {rechts:F0} von {a.Bounds.Width:F0})"));
        }
        foreach (var (text, knopf) in a.KopfKnoepfe)
        {
            if (knopf != k && knopf.IsEffectivelyVisible && knopf.TranslatePoint(new Point(knopf.Bounds.Width, 0), a) is { } r && r.X > rechts)
            {
                funde.Add("Knopf \"" + text + "\" liegt rechts von \"" + Texte.WerkzeugBeenden + "\"");
            }
        }
        if (Avalonia.Automation.AutomationProperties.GetName(k) is not { Length: > 0 } && k.Content is not TextBlock { Text: Texte.WerkzeugBeenden })
        {
            funde.Add("Knopf \"" + Texte.WerkzeugBeenden + "\" ohne Namen fuer den Screenreader");
        }
        return funde;
    }

    private static async Task BedienungAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Beenden", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        foreach (var (name, b, h) in Groessen)
        {
            await GroesseAsync(a, f, b, h);
            foreach (var x in BeendenKnopfPruefen(a))
            {
                Fund("Beenden " + name + ": " + x);
            }
        }
        // Gegenprobe: Kopfleiste ohne den Knopf (wie S 0.5.0) muss auffallen
        a.BeendenKnopfElement.IsVisible = false;
        await BildAsync(a);
        var ohne = BeendenKnopfPruefen(a).Count;
        a.BeendenKnopfElement.IsVisible = true;
        await BildAsync(a);
        Messwerte.Notiere("Beenden-Knopf Gegenprobe (ohne Knopf): " + ohne.ToString(Inv) + " Funde");
        if (ohne == 0)
        {
            Fund("Beenden-Knopf Gegenprobe: Kopfleiste ohne Knopf ohne Fund (Probe taugt nicht)");
        }

        // Tipps: jeder Knopf der Kopfleiste, jede Gruppe links (beide Reiter gebaut)
        foreach (var (text, knopf) in a.KopfKnoepfe)
        {
            if (Tipp.Von(knopf) is not { Length: > 0 } t || !Texte.KopfTipps.TryGetValue(text, out var soll) || t != soll)
            {
                Fund("Tipp fehlt am Knopf \"" + text + "\"");
            }
        }
        var gruppen = a.GruppenMitTipp.Where(g => Tipp.Von(g) is { Length: > 0 }).Count();
        Messwerte.Notiere("Tipps: Kopfleiste " + a.KopfKnoepfe.Count().ToString(Inv) + " Knoepfe, Gruppen links " + gruppen.ToString(Inv));
        if (gruppen < 7 || gruppen != a.GruppenMitTipp.Count)
        {
            Fund("Tipps: Gruppen links mit Tipp " + gruppen.ToString(Inv) + " von " + a.GruppenMitTipp.Count.ToString(Inv) + " (erwartet mindestens 7)");
        }
        // langer Druck mit dem Finger: Tipp, kein Klick; Gegenprobe: ein normaler Klick schaltet die Farben um
        var farbe = a.KopfKnoepfe.First(k => k.Text == Texte.Dunkel).Knopf;
        var vorher = a.Dunkel;
        Tipp.Halten(farbe);
        farbe.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        if (a.Dunkel != vorher)
        {
            Fund("Tipp: langer Druck auf \"" + Texte.Dunkel + "\" hat den Knopf ausgeloest");
        }
        farbe.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        if (a.Dunkel == vorher)
        {
            Fund("Tipp Gegenprobe: normaler Klick auf \"" + Texte.Dunkel + "\" schaltet nicht um");
        }
        if (a.Dunkel != vorher)
        {
            a.FarbeWechseln();
        }
        ToolTip.SetIsOpen(farbe, false);

        // Rueckfrage nur bei nicht gespeicherter Aenderung
        if (a.Ungespeichert)
        {
            Fund("Beenden: frisches Projekt gilt als nicht gespeichert");
        }
        var ohneFrage = a.BeendenPruefenAsync();
        await BildAsync(a);
        if (Lernplattform.Views.Dialog.Offen is not null || !ohneFrage.IsCompleted || !await ohneFrage)
        {
            Fund("Beenden: ohne offene Aenderung kommt eine Rueckfrage oder das Werkzeug bleibt offen");
            Waehle(Texte.Abbrechen);
        }
        Hauptansicht.UngespeichertFuerTests = true;
        try
        {
            foreach (var (wahl, erwartet) in new[] { (Texte.Abbrechen, false), (Texte.OhneSpeichernBeenden, true) })
            {
                var frage = a.BeendenPruefenAsync();
                await BildAsync(a);
                var knoepfe = DialogKnoepfe();
                if (!knoepfe.SequenceEqual([Texte.SpeichernUndBeenden, Texte.OhneSpeichernBeenden, Texte.Abbrechen]))
                {
                    Fund("Beenden: Rueckfrage zeigt \"" + string.Join(" | ", knoepfe) + "\"");
                }
                if (wahl == Texte.Abbrechen)
                {
                    foreach (var (name, b, h) in Groessen)
                    {
                        await GroesseAsync(a, f, b, h);
                        Layout(a, "Rueckfrage Beenden " + name);
                    }
                }
                Waehle(wahl);
                await BildAsync(a);
                if (!frage.IsCompleted || await frage != erwartet)
                {
                    Fund("Beenden: \"" + wahl + "\" " + (erwartet ? "beendet nicht" : "beendet trotzdem"));
                }
            }
        }
        finally
        {
            Hauptansicht.UngespeichertFuerTests = false;
        }
        Messwerte.Notiere("Beenden: Knopf rechts, Rueckfrage nur bei nicht gespeicherter Aenderung, Abbrechen haelt offen");

        static List<string> DialogKnoepfe() => Lernplattform.Views.Dialog.Offen is { } d
            ? [.. Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(d).OfType<Button>().Select(k => k.Content is TextBlock t ? t.Text ?? "" : "")]
            : [];

        static void Waehle(string text)
        {
            if (Lernplattform.Views.Dialog.Offen is { } d
                && Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(d).OfType<Button>().FirstOrDefault(k => k.Content is TextBlock { } t && t.Text == text) is { } k)
            {
                k.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            }
        }
    }

    private static void ListePruefen(Hauptansicht a, bool rotErwartet)
    {
        var text = a.AussenPruefungText;
        Messwerte.Notiere("Liste Pruefungen draussen: " + text);
        if (text.Contains(Texte.PruefungLaeuft, StringComparison.Ordinal))
        {
            Fund("Pruefungen draussen: Liste zeigt nach der Pruefung noch \"" + Texte.PruefungLaeuft + "\"");
        }
        // Weisung P12-30: in Stadt 1 (Fassung 0) steht immer der rote Fund Laterne 20; gezaehlt werden die anderen
        var rotAndere = a.AussenFunde.Any(x => x.Rot && x.Text != Laterne20Fund);
        if (rotErwartet != rotAndere)
        {
            Fund("Pruefungen draussen: Liste " + (rotErwartet ? "ohne" : "mit") + " rotem Fund: " + text);
        }
        if (a.AussenFunde.Any(x => x.Rot) != text.Contains("Rot: ", StringComparison.Ordinal))
        {
            Fund("Pruefungen draussen: Liste zeigt die roten Funde nicht richtig: " + text);
        }
    }

    private static void DreiGrenze(string name, List<double> werte, bool einmalig)
    {
        Messwerte.Reihe(name, werte[^1]);
        if (werte.Count > 0 && werte.All(w => w > Grenze) && (einmalig || werte.Count == 3))
        {
            Fund(string.Create(Inv, $"{name}: {werte.Min():F0} ms ueber Grenze {Grenze:F0} ms; Einzelmessungen {string.Join(", ", werte.Select(w => w.ToString("F0", Inv)))} ms")
                + (einmalig ? " (einmaliger Messpunkt, nicht wiederholt)" : ""));
        }
    }

    private static int Ende()
    {
        Messwerte.Notiere(Funde.Count == 0 ? "URTEIL: gruen" : "URTEIL: rot (" + Funde.Count.ToString(Inv) + " Funde)");
        return Funde.Count == 0 ? 0 : 3;
    }
}
