using System.Globalization;
using Avalonia.Controls;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Messung;

/// <summary>
/// Welle K2 (S 0.15.0 / A 0.49.0, Weisung P12-72): die Bahn in einer leeren Stadt. Draufsicht: „Bahn ziehen“ vom Kartenrand
/// mit zwei Tipps bis zum Bild (Messpunkt „Bahn ziehen (bis zum Bild)“, wiederholbar), das Gleis biegt mit Radius 4,5 oder 3
/// und hat keinen roten Fund; Knopf „Bahn“ setzt ein gerades Gleis, Knopf „Radius 4,5“ am Eckpunkt (die Knoepfe 2 und 5 des
/// Flusses fehlen), eine gerade Strasse quert das Gleis mit Bahnuebergang. Sammelliste 236: solange die Vorschau rot ist,
/// steht ihr Grund im Hinweis links (Gegenprobe: altes Verhalten ohne Grund). 3D: „Bahn ziehen“ mit zwei Tipps des Fingers
/// bis zum Bild (Messpunkt „3D Bahn ziehen (bis zum Bild)“). Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task BahnAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Bahn", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        int Bahnen() => Aussen.Landschaft(p.AussenStand).OfType<Lernplattform.Core.Daten.PyObjekt>().Count(l => Py.Gleich(l["typ"], "bahn"));
        if (a.ZiehKnoepfe.Count != 4 || a.BahnKnopf is not { IsEffectivelyVisible: true })
        {
            Fund("Bahn: die Knoepfe „Bahn“ und „Bahn ziehen“ fehlen links");
            return;
        }

        // 1: Messpunkt, zwei Tipps in der Draufsicht vom Kartenrand in einen Bogen
        await MessenAsync("Bahn ziehen (bis zum Bild)", a, () =>
        {
            ZiehWaehlen(a, 3);
            k.ZiehAnfangSetzen(0.3, 6);
            if (k.ZiehenBis(22, 19.7) is null)
            {
                Fund("Bahn: nicht gezogen (" + k.ZiehGrund + ")");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        a.Rueckgaengig(); // das gemessene Gleis wieder weg (Zuruecksetzen gilt nur zwischen den Versuchen)
        ZiehWaehlen(a, 3);
        FingerTipp(k, k.BildPunktFuerTests(0.3, 6));
        FingerTipp(k, k.BildPunktFuerTests(22, 19.7));
        await a.AussenGeprueft;
        if (Bahnen() != 1)
        {
            Fund(string.Create(CultureInfo.InvariantCulture, $"Bahn: zwei Tipps mit dem Finger ergeben {Bahnen()} statt 1 Gleis"));
            return;
        }
        var gezogen = new AussenStelle(AussenArt.Bahn, Aussen.Landschaft(p.AussenStand).Count - 1, -1);
        var radien = Liste(Objekt(Aussen.Landschaft(p.AussenStand)[gezogen.Nummer])[Strassenkurve.RadienFeld]).Select(Py.F).ToList();
        Messwerte.Notiere("Bahn: gezogen mit Radien " + string.Join(", ", radien.Select(r => r.ToString("0.##", CultureInfo.InvariantCulture))));
        if (!radien.Contains(4.5))
        {
            Fund("Bahn: der Bogen hat nicht den Radius 4,5");
        }
        if (a.AussenFunde.FirstOrDefault(x => x.Rot) is { } rot)
        {
            Fund("Bahn: roter Fund nach dem Ziehen: " + rot.Text);
        }

        // Sammelliste 236: Grund der roten Vorschau steht sofort im Hinweis links (Ende hinter dem Anfang)
        k.ZiehAnfangSetzen(22, 19.7);
        k.ZiehZielSetzen(21.6, 19.2);
        if (k.ZiehGrund is not { } grund || !a.AussenHinweisText.Contains(grund, StringComparison.Ordinal))
        {
            Fund("Bahn: der Grund der roten Vorschau steht nicht im Hinweis links (" + a.AussenHinweisText + ")");
        }
        await AlleGroessenAsync(a, f, "Bahn ziehen Vorschau rot");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        Hauptansicht.GrundErstNachTippFuerTests = true;
        try
        {
            k.ZiehZielSetzen(21.5, 19.1);
            if (k.ZiehGrund is { } g2 && a.AussenHinweisText.Contains(g2, StringComparison.Ordinal))
            {
                Fund("Bahn: Gegenprobe Sammelliste 236 zeigt den Grund trotzdem");
            }
        }
        finally
        {
            Hauptansicht.GrundErstNachTippFuerTests = false;
        }
        k.ZiehAbbrechen();
        a.SetzenBeenden();

        // Knopf „Bahn“: gerades Gleis, Radius 4,5 am Eckpunkt ueber den Knopf rechts, gerade Strasse mit Bahnuebergang
        a.SetzenKnopfDrueckenFuerTests(a.BahnKnopf);
        var bahn = k.SetzenAn(10, 15);
        await a.AussenGeprueft;
        if (bahn is not { Art: AussenArt.Bahn })
        {
            Fund("Bahn: Knopf „Bahn“ setzt kein Gleis");
            return;
        }
        a.SetzenBeenden();
        a.Projekt!.AussenAendern("Verschieben", st => AussenBearbeitung.Verschieben(st, bahn with { Punkt = 1 }, 0, 3));
        k.Waehlen(bahn with { Punkt = 1 });
        await BildAsync(a);
        Button? Radius(string text) => a.KnoepfeMit(text).FirstOrDefault(b => b.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetName(b) == Texte.RadiusHier + " " + text);
        if (Radius("2") is not null || Radius("5") is not null)
        {
            Fund("Bahn: am Eckpunkt der Bahn stehen die Radien des Flusses (2, 5)");
        }
        if (Radius("4,5") is not { } knopf)
        {
            Fund("Bahn: Knopf „Radius 4,5“ am Eckpunkt der Bahn fehlt");
        }
        else
        {
            knopf.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            if (AussenBearbeitung.FlussRadius(p.AussenStand, bahn with { Punkt = 1 }) != 4.5)
            {
                Fund("Bahn: Knopf „Radius 4,5“ setzt den Radius nicht");
            }
        }
        await AlleGroessenAsync(a, f, "Bahn Radius");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        ZiehWaehlen(a, 0);
        k.ZiehAnfangSetzen(6, 0.5);
        k.ZiehenBis(6, 12);
        await a.AussenGeprueft;
        a.SetzenBeenden();
        if (a.AussenFunde.FirstOrDefault(x => x.Rot) is { } rotUebergang)
        {
            Fund("Bahn: roter Fund am Bahnuebergang: " + rotUebergang.Text);
        }

        // 3D: zwei Tipps mit dem Finger ziehen ein Gleis bis zum Bild
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Bahn 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b3.GanzeStadt();
        await BildAsync(b3);
        var vorher = Bahnen();
        await MessenAsync("3D Bahn ziehen (bis zum Bild)", b3, async () =>
        {
            ZiehWaehlen(a, 3);
            FingerTipp(b3, Bild(a, 31.7, 3));
            FingerTipp(b3, Bild(a, 25, 3));
            await a.DreiFertig;
            if (Bahnen() != vorher + 1)
            {
                Fund("Bahn 3D: zwei Tipps haben kein Gleis gezogen");
            }
        }, async () =>
        {
            a.Rueckgaengig();
            await a.DreiFertig;
        });
        a.SetzenBeenden();
        await AlleGroessenAsync(a, f, "Bahn 3D");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        Messwerte.Notiere(string.Create(CultureInfo.InvariantCulture, $"Bahn: fertig, {Bahnen()} Gleise"));
    }
}
