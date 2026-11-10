using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle A 0.60.0 / S 0.35.0 (Weisung P12-166): Ampeln in einer leeren Stadt mit zwei Strassen mit Gehweg, die sich bei
/// (16; 8) kreuzen. Knopf „Ampel“ links setzen bis zum Bild (Messpunkt „Ampel setzen (bis zum Bild)“, wiederholbar): die
/// Ampel springt genau auf die Kreuzung, ohne roten Fund (auch nicht mit den automatischen Laternen); eine zweite an
/// derselben Kreuzung und eine fern jeder Kreuzung kommen nicht (Gegenprobe). In 3D mit einem Tipp des Fingers (Messpunkt
/// „3D Ampel setzen (bis zum Bild)“). Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task AmpelnAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Ampeln", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        if (a.AmpelKnopf is not { } ampelKnopf || a.KlicksBis(ampelKnopf) is not (0 or 1))
        {
            Fund("Ampeln: der Knopf „Ampel“ fehlt links");
            return;
        }
        static PyObjekt Strasse(double x1, double y1, double x2, double y2) => new()
        {
            { "typ", "strasse" },
            { "punkte", new List<object?> { new List<object?> { x1, y1 }, new List<object?> { x2, y2 } } },
            { "gehweg", 0.35 },
        };
        p.AussenAendern("Straße", aa => Aussen.Landschaft(aa).Add(Strasse(2, 8, 30, 8)));
        p.AussenAendern("Straße", aa => Aussen.Landschaft(aa).Add(Strasse(16, 2, 16, 18)));
        await a.AussenGeprueft;

        await MessenAsync("Ampel setzen (bis zum Bild)", a, () =>
        {
            AmpelWaehlen(a, ampelKnopf);
            if (k.SetzenArt != AussenArt.Ampel || k.SetzenAn(16.4, 8.3) is null)
            {
                Fund("Ampel: nicht gesetzt");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        a.Rueckgaengig(); // die gemessene wieder weg (Zuruecksetzen gilt nur zwischen den Versuchen)
        AmpelWaehlen(a, ampelKnopf);
        var s = k.SetzenAn(16.4, 8.3);
        await a.AussenGeprueft;
        if (s is null || AussenBearbeitung.AmpelVon(p.AussenStand, s) is not { } amp || MathF.Abs(amp.Mitte.X - 16) > 0.001f || MathF.Abs(amp.Mitte.Y - 8) > 0.001f
            || amp.Arme.Count != 4)
        {
            Fund("Ampel: liegt nicht genau auf der Kreuzung (16; 8) mit vier Armen");
            return;
        }
        if (a.AussenFunde.Any(x => x.Rot))
        {
            Fund("Ampel: frisch gesetzt rot: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot).Select(x => x.Text)));
        }
        await AlleGroessenAsync(a, f, "Ampel gewaehlt");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        // Gegenprobe: zweite an derselben Kreuzung und eine fern jeder Kreuzung kommen nicht, nichts im Verlauf
        var schritte = p.SchritteZurueck;
        foreach (var (x, y) in new[] { (16.0, 8.0), (8.0, 15.0) })
        {
            AmpelWaehlen(a, ampelKnopf);
            if (k.SetzenAn(x, y) is not null || p.SchritteZurueck != schritte || AussenBearbeitung.AmpelListe(p.AussenStand).Count != 1)
            {
                Fund(string.Create(Inv, $"Ampel: bei ({x}; {y}) gesetzt (Gegenprobe)"));
            }
        }
        k.SetzenBeenden();

        // 3D: die Ampel weg, dann mit einem Tipp des Fingers auf die Kreuzung setzen bis zum Bild
        a.Rueckgaengig();
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Ampel 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b3.GanzeStadt();
        await BildAsync(b3);
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        await MessenAsync("3D Ampel setzen (bis zum Bild)", b3, async () =>
        {
            AmpelWaehlen(a, ampelKnopf);
            var bild = Bild(a, 16.2, 8.2);
            Druck(b3, finger, bild);
            Los(b3, finger, bild);
            await a.DreiFertig;
            if (AussenBearbeitung.AmpelListe(p.AussenStand).Count != 1)
            {
                Fund("Ampel 3D: ein Tipp hat keine Ampel gesetzt");
            }
        }, async () =>
        {
            a.Rueckgaengig();
            await a.DreiFertig;
        });
        await AlleGroessenAsync(a, f, "Ampel 3D");
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    private static void AmpelWaehlen(Hauptansicht a, Button knopf)
    {
        if (a.Karte.SetzenArt != AussenArt.Ampel || !ReferenceEquals(a.SetzenKnopfMarkiert, knopf))
        {
            a.SetzenKnopfDrueckenFuerTests(knopf);
        }
    }
}
