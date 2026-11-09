using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Messung;

/// <summary>
/// Welle K3 (S 0.11.0, Weisungen P12-58/P12-59): Kreisverkehr in einer leeren Stadt. „Kreisverkehr klein“ links setzen bis
/// zum Bild (Messpunkt „Kreisverkehr setzen (bis zum Bild)“, wiederholbar): Ring, vier Zufahrten, vier Laternen, ohne
/// roten Fund; gewaehlt schieben (Taste Pfeil) und drehen (R) nehmen Zufahrten mit (je ein Schritt, danach ohne Fund);
/// eine Zufahrt loeschen bleibt ohne Fund, eine zweite gibt rot. In 3D: „Kreisverkehr groß“ mit einem Tipp des Fingers
/// setzen bis zum Bild (Messpunkt „3D Kreisverkehr setzen (bis zum Bild)“). Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static void KreisWaehlen(Hauptansicht a, int knopf)
    {
        if (a.Karte.SetzenArt != AussenArt.Kreisverkehr || !ReferenceEquals(a.SetzenKnopfMarkiert, a.KreisKnoepfe[knopf]))
        {
            a.SetzenKnopfDrueckenFuerTests(a.KreisKnoepfe[knopf]);
        }
    }

    private static async Task KreisverkehrAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Kreisverkehr", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        if (a.KreisKnoepfe.Count != 2 || a.KreisKnoepfe.Any(b => a.KlicksBis(b) is not (0 or 1)))
        {
            Fund("Kreisverkehr: die Knoepfe „Kreisverkehr klein“ und „Kreisverkehr groß“ fehlen links");
            return;
        }
        var anzahl = Aussen.Landschaft(p.AussenStand).Count;
        await MessenAsync("Kreisverkehr setzen (bis zum Bild)", a, () =>
        {
            KreisWaehlen(a, 0);
            if (k.SetzenArt != AussenArt.Kreisverkehr || k.SetzenAn(10, 10) is null)
            {
                Fund("Kreisverkehr: nicht gesetzt");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        a.Rueckgaengig(); // der gemessene Kreisverkehr wieder weg (Zuruecksetzen gilt nur zwischen den Versuchen)
        KreisWaehlen(a, 0);
        var ring = k.SetzenAn(10, 10);
        await a.AussenGeprueft;
        if (ring is null || Aussen.Landschaft(p.AussenStand).Count != anzahl + 5 || !AussenBearbeitung.IstKreisverkehr(p.AussenStand, ring)
            || Aussen.Laternen(p.AussenStand).Count != 4 + LaternenAutomatik.Gesetzt(p.AussenStand).Count)
        {
            Fund(string.Create(Inv, $"Kreisverkehr: nach dem Setzen kein Ring mit vier Zufahrten und vier Laternen am Ring (S 0.26.0: dazu die automatischen an den Zufahrten) (Stuecke {Aussen.Landschaft(p.AussenStand).Count} statt {anzahl + 5})"));
            return;
        }
        if (a.AussenFunde.Count > 0)
        {
            Fund("Kreisverkehr: frisch gesetzt mit Funden: " + string.Join(" | ", a.AussenFunde.Select(x => x.Text)));
        }
        await AlleGroessenAsync(a, f, "Kreisverkehr gewaehlt");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // gewaehlt schieben und drehen: je ein Schritt, Zufahrten gehen mit, kein Fund
        Vector2 Mitte() => Kreisverkehr.Mitte(Kreisverkehr.Linie(Objekt(Aussen.Landschaft(p.AussenStand)[ring.Nummer])["punkte"]));
        var schritte = p.SchritteZurueck;
        k.Waehlen(ring);
        k.Taste(Key.Right, KeyModifiers.None);
        k.Taste(Key.R, KeyModifiers.None);
        await a.AussenGeprueft;
        if (p.SchritteZurueck != schritte + 2 || Vector2.Distance(Mitte(), new Vector2(10.5f, 10)) > 0.02f)
        {
            Fund(string.Create(Inv, $"Kreisverkehr: Pfeil und R sind nicht je ein Schritt oder die Mitte stimmt nicht ({Mitte().X:F2}; {Mitte().Y:F2})"));
        }
        if (a.AussenFunde.Count > 0)
        {
            Fund("Kreisverkehr: nach Schieben und Drehen mit Funden: " + string.Join(" | ", a.AussenFunde.Select(x => x.Text)));
        }
        a.Rueckgaengig();
        a.Rueckgaengig();

        // eine Zufahrt weg: drei bleiben ohne Fund; zwei weg: rot
        k.Waehlen(new AussenStelle(AussenArt.Strasse, ring.Nummer + 1));
        k.Taste(Key.Delete, KeyModifiers.None);
        await a.AussenGeprueft;
        if (a.AussenFunde.Any(x => x.Rot))
        {
            Fund("Kreisverkehr: mit drei Zufahrten rot: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot).Select(x => x.Text)));
        }
        k.Waehlen(new AussenStelle(AussenArt.Strasse, ring.Nummer + 1));
        k.Taste(Key.Delete, KeyModifiers.None);
        await a.AussenGeprueft;
        if (!a.AussenFunde.Any(x => x.Rot && x.Text.Contains("hat 2 Zufahrten", StringComparison.Ordinal)))
        {
            Fund("Kreisverkehr: mit zwei Zufahrten kein roter Fund");
        }
        a.Rueckgaengig();
        a.Rueckgaengig();
        await a.AussenGeprueft;

        // 3D: Kreisverkehr gross mit einem Tipp des Fingers setzen bis zum Bild
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Kreisverkehr 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b3.GanzeStadt();
        await BildAsync(b3);
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var vorher = Aussen.Landschaft(p.AussenStand).Count;
        await MessenAsync("3D Kreisverkehr setzen (bis zum Bild)", b3, async () =>
        {
            KreisWaehlen(a, 1);
            var bild = Bild(a, 22, 10);
            Druck(b3, finger, bild);
            Los(b3, finger, bild);
            await a.DreiFertig;
            if (Aussen.Landschaft(p.AussenStand).Count != vorher + 5)
            {
                Fund("Kreisverkehr 3D: ein Tipp hat keinen Kreisverkehr gesetzt");
            }
        }, async () =>
        {
            a.Rueckgaengig();
            await a.DreiFertig;
        });
        await AlleGroessenAsync(a, f, "Kreisverkehr 3D");
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }
}
