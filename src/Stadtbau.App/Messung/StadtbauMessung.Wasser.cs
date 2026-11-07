using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

using Point = Avalonia.Point;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.8.0 (Weisung P12-47, Plan P12-26b Abschnitt 10): Wasser in Stadt 1. Teich setzen bis zum Bild (Messpunkt „Teich
/// setzen (bis zum Bild)“, wiederholbar), Steg vom Ufer, kein neuer roter Fund; ein Eckpunkt mit dem Finger gezogen ist ein
/// Schritt; Haus im Teich ist rot; Schalter „Eis im Winter“; 3D nach einer Aenderung am Teich (Messpunkt „Wasser 3D nach
/// Aenderung“, wiederholbar) und Grenzen der Gesamtansicht mit Teich und Steg (hoechstens 160 000 Dreiecke, 40 Aufrufe).
/// Layout PC und Tablet, hell und dunkel, mit gewaehltem Eckpunkt.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task WasserAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Wasser", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        await BildAsync(a);
        var p = a.Projekt!;
        var k = a.Karte;

        // Teich setzen bis zum Bild (oben rechts auf der Wiese, 22,5 bis 25,5 / 1,5 bis 3,5)
        await MessenAsync("Teich setzen (bis zum Bild)", a, () =>
        {
            k.SetzenArt = AussenArt.Teich;
            if (k.SetzenAn(24, 2.5) is null)
            {
                Fund("Wasser: Teich nicht gesetzt");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        var teich = new AussenStelle(AussenArt.Teich, 0, -1);
        if (Aussen.Teiche(p.AussenStand).Count != 1 || AussenBearbeitung.EckpunkteWerte(p.AussenStand, teich) is not { Count: 4 } ecken || ecken[0] != new System.Numerics.Vector2(22.5f, 1.5f))
        {
            Fund("Wasser: Teich nicht wie erwartet (" + PythonJson.Text(Aussen.Stadtdaten(p.AussenStand)[Wasser.Abschnitt]) + ")");
            return;
        }
        k.SetzenArt = AussenArt.Steg;
        k.SetzenAn(23.5, 2.5);
        await a.AussenGeprueft;
        if (a.AussenFunde.Any(x => x.Rot && x.Text != Laterne20Fund))
        {
            Fund("Wasser: Teich mit Steg hat rote Funde: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot && x.Text != Laterne20Fund).Select(x => x.Text)));
        }

        // Eckpunkt mit dem Finger ziehen (ein Schritt), Rueckgaengig bringt ihn zurueck
        await BildAsync(a);
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var start = k.Bildpunkt(25.5, 1.5);
        var ziel = k.Bildpunkt(26, 1);
        Druck(k, finger, start);
        if (k.Auswahl != new AussenStelle(AussenArt.Teich, 0, 1))
        {
            Fund("Wasser: Antippen des Eckpunkts waehlt ihn nicht (" + k.Auswahl + ")");
        }
        for (var i = 1; i <= 6; i++)
        {
            Bewegung(k, finger, start + ((ziel - start) * (i / 6.0)));
        }
        Los(k, finger, ziel);
        await BildAsync(a);
        var gezogen = AussenBearbeitung.EckpunkteWerte(p.AussenStand, teich);
        if (gezogen[1] != new System.Numerics.Vector2(26f, 1f) || gezogen[0] != new System.Numerics.Vector2(22.5f, 1.5f))
        {
            Fund("Wasser: Eckpunkt mit dem Finger nicht gezogen (" + string.Join(" ", gezogen) + ")");
        }
        a.Rueckgaengig();
        if (AussenBearbeitung.EckpunkteWerte(p.AussenStand, teich)[1] != new System.Numerics.Vector2(25.5f, 1.5f))
        {
            Fund("Wasser: Rueckgaengig nach dem Ziehen bringt den Eckpunkt nicht zurueck (kein einzelner Schritt)");
        }

        // Haus im Teich: rot (Gegenprobe der Pruefung in der Oberflaeche)
        var stellwerk = AussenBearbeitung.Alle(p.AussenStand).First(x => x.Stelle.Art == AussenArt.Gebaeude).Stelle;
        k.Waehlen(stellwerk);
        var (sx, sy) = AussenBearbeitung.Lage(p.AussenStand, stellwerk);
        p.AussenAendern("Verschieben", aa => AussenBearbeitung.Platzieren(aa, stellwerk, 23.5, 2));
        await a.AussenGeprueft;
        if (!a.AussenFunde.Any(x => x.Rot && x.Text.Contains("steht im Wasser oder auf dem Ufer", StringComparison.Ordinal)))
        {
            Fund("Wasser: Haus im Teich ist nicht rot");
        }
        a.Rueckgaengig();
        await a.AussenGeprueft;
        if (AussenBearbeitung.Lage(p.AussenStand, stellwerk) != (sx, sy))
        {
            Fund("Wasser: Haus nach Rueckgaengig nicht zurueck");
        }

        // Eis im Winter: Schalter schreibt den Wert, Rueckgaengig nimmt ihn weg
        a.EisSchalterFuerTests.IsChecked = true;
        if (!Aussen.Eis(p.AussenStand))
        {
            Fund("Wasser: Schalter „Eis im Winter“ setzt nichts");
        }
        a.Rueckgaengig();
        await BildAsync(a);
        if (Aussen.Eis(p.AussenStand) || a.EisSchalterFuerTests.IsChecked == true)
        {
            Fund("Wasser: Rueckgaengig nimmt „Eis im Winter“ nicht zurueck");
        }

        // Layout mit gewaehltem Eckpunkt (Knoepfe Eckpunkt dazu und weg)
        k.Waehlen(new AussenStelle(AussenArt.Teich, 0, 2));
        await AlleGroessenAsync(a, f, "Wasser");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // 3D: nach einer Aenderung am Teich bis zum Bild, Grenzen mit Teich und Steg
        a.DreiZeigen();
        await a.DreiFertig;
        if (!a.Drei.GlBereit)
        {
            Messwerte.Notiere("Wasser 3D: kein OpenGL auf diesem Rechner, nicht gemessen");
            a.ReiterZeigen(innen: false);
            return;
        }
        var ecke = new AussenStelle(AussenArt.Teich, 0, 2);
        var werte = new List<double>();
        for (var i = 0; i < 3; i++)
        {
            k.Waehlen(ecke);
            k.Verschieben(i % 2 == 0 ? 0.5 : -0.5, 0);
            await a.DreiFertig;
            werte.Add(a.DreiZeitMs);
            if (a.DreiZeitMs <= Grenze)
            {
                break;
            }
            await Task.Delay(200);
        }
        DreiGrenze("Wasser 3D nach Aenderung", werte, einmalig: false);
        if (a.Drei.Spiel?.Szene is { } szene && (szene.Teiche.Count != 1 || szene.Stege.Count != 1))
        {
            Fund("Wasser 3D: die Szene zeigt nicht den Teich und den Steg");
        }
        await a.GrenzenMessenAsync();
        if (a.GrenzenDraussen is { } gz)
        {
            Messwerte.Notiere(string.Create(Inv, $"Grenzen Gesamtansicht Stadt 1 mit Teich und Steg (Nacht, Winter, Weihnachten): {gz.Dreiecke} Dreiecke, {gz.Aufrufe} Aufrufe"));
            if (gz.Dreiecke > StadtbauGrenzen.Dreiecke || gz.Aufrufe > StadtbauGrenzen.Aufrufe)
            {
                Fund(string.Create(Inv, $"Grenzen mit Teich und Steg: {gz.Dreiecke} Dreiecke / {gz.Aufrufe} Aufrufe ueber {StadtbauGrenzen.Dreiecke} / {StadtbauGrenzen.Aufrufe}"));
            }
        }
        else
        {
            Fund("Grenzen mit Teich und Steg: keine Messung");
        }
        await AlleGroessenAsync(a, f, "Wasser 3D");
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }
}
