using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Patch S 0.5.1 Nachbesserung (Weisung P12-40 Punkt 5, Nico: "Manchmal ist die Rueckgaengig Taste ausgegraut obwohl
/// was gesetzt wurde"): nach jeder Art von Schritt (setzen, schieben, drehen, kopieren, loeschen, Katalog, Blueprint,
/// draussen und innen, mit Finger und ohne) muessen "Rueckgaengig" und "Wiederholen" sofort richtig an oder aus sein,
/// ebenso nach jedem Druck auf diese Knoepfe. Gegenprobe: mit dem Verhalten vor der Nachbesserung ist ein Knopf falsch.
/// </summary>
public static partial class StadtbauMessung
{
    private static Button KopfKnopf(Hauptansicht a, string text) => a.KopfKnoepfe.First(k => k.Text == text).Knopf;

    /// <summary>Stehen beide Knoepfe so, wie es der Verlauf sagt? Sonst Text des Fehlers.</summary>
    private static string? KnoepfeFalsch(Hauptansicht a)
    {
        var p = a.Projekt!;
        var zurueck = KopfKnopf(a, Texte.Rueckgaengig).IsEnabled;
        var vor = KopfKnopf(a, Texte.Wiederholen).IsEnabled;
        if (zurueck == p.SchritteZurueck > 0 && vor == p.SchritteVor > 0)
        {
            return null;
        }
        return string.Create(Inv, $"\"{Texte.Rueckgaengig}\" {(zurueck ? "an" : "aus")} bei {p.SchritteZurueck} Schritten zurueck, \"{Texte.Wiederholen}\" {(vor ? "an" : "aus")} bei {p.SchritteVor} vor");
    }

    private static async Task<int> SchrittPruefenAsync(Hauptansicht a, string name, Func<Task> schritt, bool melden = true)
    {
        var vorher = a.Projekt!.SchritteZurueck + a.Projekt.SchritteVor * 1000;
        await schritt();
        var gemacht = a.Projekt.SchritteZurueck + a.Projekt.SchritteVor * 1000 != vorher;
        if (!gemacht && melden)
        {
            Fund("Rueckgaengig-Knopf: Schritt \"" + name + "\" hat nichts geaendert (Probe taugt nicht)");
        }
        if (KnoepfeFalsch(a) is { } falsch)
        {
            if (melden)
            {
                Fund("Rueckgaengig-Knopf nach \"" + name + "\": " + falsch);
            }
            return 1;
        }
        return 0;
    }

    private static void Klicken(Button k) => k.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>Ein Finger tippt kurz (ohne Bewegung) an die Stelle.</summary>
    private static async Task TippenAsync(Control c, Point p)
    {
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        Druck(c, finger, p);
        Los(c, finger, p);
        await BildAsync(c);
    }

    /// <summary>Ein Finger zieht von start um (dx, dy) Bildpunkte.</summary>
    private static async Task ZiehenAsync(Control c, Point start, double dx, double dy)
    {
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        Druck(c, finger, start);
        for (var i = 1; i <= 6; i++)
        {
            Bewegung(c, finger, start + new Point(dx * i / 6, dy * i / 6));
        }
        Los(c, finger, start + new Point(dx, dy));
        await BildAsync(c);
    }

    private static async Task RueckgaengigKnopfAsync(Hauptansicht a, Window? f)
    {
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var funde = await SchritteAsync(a, melden: true);
        Messwerte.Notiere("Rueckgaengig-Knopf: " + funde.ToString(Inv) + " falsche Knoepfe nach allen Schritten");
        // Speichern scheitert (an der Stelle der Projektdatei liegt ein Ordner, wie eine kurz gesperrte Datei): der Schritt
        // bleibt, die Knoepfe stimmen, der Fehler steht unten. Gegenprobe: wie vorher blieb "Rueckgaengig" grau.
        if (await GesperrtAsync(a, alt: false) is { } fund)
        {
            Fund("Rueckgaengig-Knopf bei gesperrter Projektdatei: " + fund);
        }
        Projekt.SpeichernAltFuerTests = true;
        try
        {
            if (await GesperrtAsync(a, alt: true) is null)
            {
                Fund("Rueckgaengig-Knopf Gegenprobe: mit dem alten Verhalten stimmen die Knoepfe auch bei gesperrter Projektdatei (Probe taugt nicht)");
            }
        }
        finally
        {
            Projekt.SpeichernAltFuerTests = false;
        }
    }

    /// <summary>Ein Schritt, waehrend die Projektdatei nicht geschrieben werden kann; Rueckgabe: Fehler oder null.</summary>
    private static async Task<string?> GesperrtAsync(Hauptansicht a, bool alt)
    {
        a.NeuAnlegen(alt ? "Starttest gesperrt Gegenprobe" : "Starttest gesperrt", null, (24, 18));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        var p = a.Projekt!;
        File.Delete(p.Datei);
        Directory.CreateDirectory(p.Datei);
        try
        {
            a.Karte.SetzenArt = AussenArt.Baum;
            try
            {
                a.Karte.SetzenAn(4, 4);
            }
            catch (Exception e) when (alt && e is IOException or UnauthorizedAccessException)
            {
                // so kam der Fehler vorher bei der Oberflaeche an (fehler.log, sonst nichts)
            }
            await BildAsync(a);
            if (p.SchritteZurueck != 1)
            {
                return "Schritt nicht im Verlauf";
            }
            if (KnoepfeFalsch(a) is { } falsch)
            {
                return falsch;
            }
            if (!alt && !a.StatusText.Contains(Texte.AutoSpeichernFehler, StringComparison.Ordinal))
            {
                return "Hinweis \"" + Texte.AutoSpeichernFehler + "\" fehlt, unten steht \"" + a.StatusText + "\"";
            }
            return null;
        }
        finally
        {
            Directory.Delete(p.Datei);
            p.Speichern(vonHand: true);
            a.Karte.SetzenArt = null;
        }
    }

    /// <summary>Alle Arten von Schritten der Reihe nach, dann alles zurueck und wieder vor; Rueckgabe: Zahl falscher Knoepfe.</summary>
    private static async Task<int> SchritteAsync(Hauptansicht a, bool melden)
    {
        var falsch = 0;
        Task<int> S(string name, Func<Task> schritt) => SchrittPruefenAsync(a, name, schritt, melden);
        Task<int> Sofort(string name, Action schritt) => S(name, () =>
        {
            schritt();
            return Task.CompletedTask;
        });

        // draussen in einer leeren Stadt (Strasse, Baum, Katalog, Blueprint)
        a.NeuAnlegen(melden ? "Starttest Rueckgaengig" : "Starttest Rueckgaengig Gegenprobe", null, (24, 18));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        var k = a.Karte;
        var p = a.Projekt!;
        if (KnoepfeFalsch(a) is { } start)
        {
            falsch++;
            if (melden)
            {
                Fund("Rueckgaengig-Knopf im neuen Projekt: " + start);
            }
        }
        falsch += await Sofort("Strasse setzen", () =>
        {
            k.SetzenArt = AussenArt.Strasse;
            k.SetzenAn(12, 8);
        });
        falsch += await Sofort("Baum setzen", () =>
        {
            k.SetzenArt = AussenArt.Baum;
            k.SetzenAn(4, 4);
        });
        var baum = AussenBearbeitung.Alle(p.AussenStand).First(x => x.Stelle.Art == AussenArt.Baum).Stelle;
        falsch += await Sofort("Baum verschieben", () =>
        {
            k.Waehlen(baum);
            k.Verschieben(1, 0);
        });
        var strasse = AussenBearbeitung.Alle(p.AussenStand).First(x => x.Stelle.Art == AussenArt.Strasse).Stelle;
        falsch += await Sofort("Strasse drehen", () =>
        {
            k.Waehlen(strasse);
            k.Drehen();
        });
        falsch += await Sofort("Baum kopieren", () =>
        {
            k.Waehlen(AussenBearbeitung.Alle(p.AussenStand).First(x => x.Stelle.Art == AussenArt.Baum).Stelle);
            k.Kopieren();
        });
        falsch += await Sofort("Baum loeschen", () =>
        {
            k.Waehlen(AussenBearbeitung.Alle(p.AussenStand).First(x => x.Stelle.Art == AussenArt.Baum).Stelle);
            k.Loeschen();
        });
        if (Katalog.Aktiv is { } kat)
        {
            falsch += await Sofort("Katalog-Stueck setzen", () =>
            {
                a.KatalogOeffnen(innen: false).Waehlen(kat.Eintraege.First(e => e.Art == KatalogArt.Modell));
                k.SetzenAn(20, 14);
            });
        }
        if (a.Blueprints.FirstOrDefault(e => e.Vorlage is not null)?.Vorlage is { } b)
        {
            falsch += await Sofort("Blueprint setzen", () =>
            {
                a.BlueprintSetzenWaehlen(b);
                k.SetzenAn(6, 13);
            });
        }
        // mit dem Finger: tippen setzt, ziehen auf einem Stueck schiebt es
        k.Waehlen(null);
        falsch += await S("Baum mit dem Finger setzen", async () =>
        {
            k.SetzenArt = AussenArt.Baum;
            await TippenAsync(k, MitteVon(k) + new Point(-120, -90));
        });
        var aufStueck = Suchen(k, k.StueckAnFuerTests);
        if (aufStueck is { } punkt)
        {
            falsch += await S("Stueck mit dem Finger schieben", () => ZiehenAsync(k, punkt, 40, 0));
        }

        // innen: Moebelhaus von Stadt 1 (ein eigenes Projekt)
        a.NeuAnlegen(melden ? "Starttest Rueckgaengig innen" : "Starttest Rueckgaengig innen Gegenprobe", "moebelhaus");
        await a.Geprueft;
        a.ReiterZeigen(innen: true);
        a.OrtOeffnen("moebelhaus");
        await BildAsync(a);
        var fl = a.Flaeche;
        falsch += await Sofort("innen setzen", () =>
        {
            fl.SetzenTyp = "pflanze";
            fl.SetzenAn(2, 12);
        });
        var regal = Bearbeitung.Alle(a.Projekt!.Grundriss("moebelhaus")).First(s => Py.Str(s.Deko["typ"]) == "regal").Stelle;
        falsch += await Sofort("innen drehen", () =>
        {
            fl.Waehlen(regal);
            fl.Drehen();
        });
        falsch += await Sofort("innen verschieben (Taste)", () =>
        {
            fl.Waehlen(regal);
            fl.Taste(Key.Down, KeyModifiers.None);
        });
        falsch += await Sofort("innen kopieren", () =>
        {
            fl.Waehlen(Bearbeitung.Alle(a.Projekt!.Grundriss("moebelhaus")).First(s => Py.Str(s.Deko["typ"]) == "pflanze").Stelle);
            fl.Kopieren();
        });
        falsch += await Sofort("innen loeschen", () =>
        {
            fl.Waehlen(Bearbeitung.Alle(a.Projekt!.Grundriss("moebelhaus")).First(s => Py.Str(s.Deko["typ"]) == "pflanze").Stelle);
            fl.Loeschen();
        });
        if (Suchen(fl, fl.StueckAnFuerTests) is { } innenPunkt)
        {
            falsch += await S("innen mit dem Finger schieben", () => ZiehenAsync(fl, innenPunkt, 0, 40));
        }

        // alles zurueck mit dem Knopf, dann alles wieder vor; nach jedem Druck stimmen beide Knoepfe
        var zurueck = KopfKnopf(a, Texte.Rueckgaengig);
        for (var i = 0; i < 40 && zurueck.IsEnabled; i++)
        {
            falsch += await Sofort("Knopf " + Texte.Rueckgaengig, () => Klicken(zurueck));
        }
        var vor = KopfKnopf(a, Texte.Wiederholen);
        for (var i = 0; i < 40 && vor.IsEnabled; i++)
        {
            falsch += await Sofort("Knopf " + Texte.Wiederholen, () => Klicken(vor));
        }
        if (melden && (a.Projekt!.SchritteZurueck == 0 || a.Projekt.SchritteVor != 0))
        {
            Fund(string.Create(Inv, $"Rueckgaengig-Knopf: nach allem Wiederholen {a.Projekt.SchritteZurueck} zurueck / {a.Projekt.SchritteVor} vor"));
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        return falsch;
    }
}
