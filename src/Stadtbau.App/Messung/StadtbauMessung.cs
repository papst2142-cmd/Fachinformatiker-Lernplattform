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

    private static async Task DraussenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Straßen und Grün", null);
        await a.AussenGeprueft;
        var p = a.Projekt!;
        if (a.AussenFunde.Any(x => x.Rot))
        {
            Fund("Draussen Stadt 1: rote Funde: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot).Select(x => x.Text)));
        }
        Messwerte.Notiere("Draussen Stadt 1: " + a.AussenFunde.Count.ToString(Inv) + " Funde (gelb)");
        a.ReiterZeigen(innen: false);
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
        if (a.Karte.SetzenAn(16, 12) is not { Art: AussenArt.Katalog })
        {
            Fund("Katalog draussen: " + baum.Anzeige + " nicht gesetzt");
        }
        var effekt = kat.Eintraege.First(e => e.Art == KatalogArt.Effekt);
        a.KatalogOeffnen(innen: false).Waehlen(effekt);
        a.Karte.SetzenAn(17, 12);
        a.Karte.SetzenArt = null;
        await a.AussenGeprueft;
        if (a.AussenFunde.Any(x => x.Rot))
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

    private static void ListePruefen(Hauptansicht a, bool rotErwartet)
    {
        var text = a.AussenPruefungText;
        Messwerte.Notiere("Liste Pruefungen draussen: " + text);
        if (text.Contains(Texte.PruefungLaeuft, StringComparison.Ordinal))
        {
            Fund("Pruefungen draussen: Liste zeigt nach der Pruefung noch \"" + Texte.PruefungLaeuft + "\"");
        }
        if (rotErwartet != text.Contains("Rot: ", StringComparison.Ordinal))
        {
            Fund("Pruefungen draussen: Liste " + (rotErwartet ? "ohne" : "mit") + " rotem Fund: " + text);
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
