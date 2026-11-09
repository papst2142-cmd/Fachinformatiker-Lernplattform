using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.26.0 (Weisung P12-115): Zebrastreifen und automatische Laternen in einer leeren Stadt mit einer geraden Strasse
/// (2; 8)–(30; 8) mit Gehweg. An der Strasse stehen nach dem Anlegen automatische Laternen ohne roten Fund. Knopf
/// „Zebrastreifen“ links setzen bis zum Bild (Messpunkt „Zebrastreifen setzen (bis zum Bild)“, wiederholbar): der
/// Zebrastreifen liegt quer genau auf der Strassenmitte, ohne Fund; neben jeder Strasse gesetzt kommt keiner (Gegenprobe).
/// Knopf „Laternen neu verteilen“ (Messpunkt „Laternen verteilen“, wiederholbar). In 3D mit einem Tipp des Fingers (Messpunkt
/// „3D Zebrastreifen setzen (bis zum Bild)“). Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task ZebrastreifenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Zebrastreifen", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        if (a.ZebraKnopf is not { } zebraKnopf || a.LaternenVerteilenKnopf is not { } verteilenKnopf
            || a.KlicksBis(zebraKnopf) is not (0 or 1) || a.KlicksBis(verteilenKnopf) is not (0 or 1))
        {
            Fund("Zebrastreifen: die Knoepfe „Zebrastreifen“ und „Laternen neu verteilen“ fehlen links");
            return;
        }
        p.AussenAendern("Straße", aa => Aussen.Landschaft(aa).Add(new PyObjekt
        {
            { "typ", "strasse" },
            { "punkte", new List<object?> { new List<object?> { 2.0, 8.0 }, new List<object?> { 30.0, 8.0 } } },
            { "gehweg", 0.35 },
        }));
        await a.AussenGeprueft;
        var auto = LaternenAutomatik.Gesetzt(p.AussenStand).Count;
        if (auto != 8 || Aussen.Laternen(p.AussenStand).Count != 8)
        {
            Fund(string.Create(Inv, $"Laternen automatisch: an der Strasse stehen {Aussen.Laternen(p.AussenStand).Count} Laternen ({auto} automatisch) statt 8"));
        }
        if (a.AussenFunde.Any(x => x.Rot))
        {
            Fund("Laternen automatisch: rot: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot).Select(x => x.Text)));
        }

        // „Laternen neu verteilen“ stellt dieselben 8 wieder auf (dann kein Schritt im Verlauf); zurueck nur, wenn es einer war
        var basis = p.SchritteZurueck;
        void Zurueck()
        {
            while (p.SchritteZurueck > basis)
            {
                a.Rueckgaengig();
            }
        }
        await MessenAsync("Laternen verteilen", a, () =>
        {
            if (a.LaternenVerteilen() is null || LaternenAutomatik.Gesetzt(p.AussenStand).Count != 8)
            {
                Fund("Laternen verteilen: danach nicht 8 automatische Laternen");
            }
            return Task.CompletedTask;
        }, () =>
        {
            Zurueck();
            return Task.CompletedTask;
        });
        Zurueck();

        var vorher = AussenBearbeitung.ZebraListe(p.AussenStand).Count;
        await MessenAsync("Zebrastreifen setzen (bis zum Bild)", a, () =>
        {
            ZebraWaehlen(a, zebraKnopf);
            if (k.SetzenArt != AussenArt.Zebrastreifen || k.SetzenAn(12, 8.4) is null)
            {
                Fund("Zebrastreifen: nicht gesetzt");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        a.Rueckgaengig(); // der gemessene wieder weg (Zuruecksetzen gilt nur zwischen den Versuchen)
        ZebraWaehlen(a, zebraKnopf);
        var z = k.SetzenAn(12, 8.4);
        await a.AussenGeprueft;
        if (z is null || AussenBearbeitung.ZebraVon(p.AussenStand, z) is not { } zebra || MathF.Abs(zebra.Mitte.Y - 8) > 0.001f || MathF.Abs(zebra.Mitte.X - 12) > 0.001f)
        {
            Fund("Zebrastreifen: liegt nicht quer genau auf der Strassenmitte bei (12; 8)");
            return;
        }
        if (a.AussenFunde.Any(x => x.Rot))
        {
            Fund("Zebrastreifen: frisch gesetzt rot: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot).Select(x => x.Text)));
        }
        if (LaternenAutomatik.Gesetzt(p.AussenStand).Count != 8)
        {
            Fund("Zebrastreifen: die Laterne an seiner Stelle ist nicht ausgewichen");
        }
        await AlleGroessenAsync(a, f, "Zebrastreifen gewaehlt");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        // Gegenprobe: weit neben jeder Strasse kommt keiner, nichts im Verlauf
        var schritte = p.SchritteZurueck;
        ZebraWaehlen(a, zebraKnopf);
        if (k.SetzenAn(12, 15) is not null || p.SchritteZurueck != schritte || AussenBearbeitung.ZebraListe(p.AussenStand).Count != vorher + 1)
        {
            Fund("Zebrastreifen: neben der Strasse gesetzt (Gegenprobe)");
        }
        k.SetzenBeenden();
        // schieben nur laengs: Pfeil nach unten bewegt ihn nicht quer
        k.Waehlen(z);
        k.Taste(Key.Down, KeyModifiers.None);
        k.Taste(Key.Right, KeyModifiers.None);
        await a.AussenGeprueft;
        if (AussenBearbeitung.ZebraVon(p.AussenStand, z) is not { } geschoben || MathF.Abs(geschoben.Mitte.Y - 8) > 0.001f || geschoben.Mitte.X <= 12)
        {
            Fund("Zebrastreifen: schieben nicht nur laengs der Strasse");
        }

        // 3D: mit einem Tipp des Fingers setzen bis zum Bild
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Zebrastreifen 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b3.GanzeStadt();
        await BildAsync(b3);
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var anzahl = AussenBearbeitung.ZebraListe(p.AussenStand).Count;
        await MessenAsync("3D Zebrastreifen setzen (bis zum Bild)", b3, async () =>
        {
            ZebraWaehlen(a, zebraKnopf);
            var bild = Bild(a, 22, 8.3);
            Druck(b3, finger, bild);
            Los(b3, finger, bild);
            await a.DreiFertig;
            if (AussenBearbeitung.ZebraListe(p.AussenStand).Count != anzahl + 1)
            {
                Fund("Zebrastreifen 3D: ein Tipp hat keinen Zebrastreifen gesetzt");
            }
        }, async () =>
        {
            a.Rueckgaengig();
            await a.DreiFertig;
        });
        await AlleGroessenAsync(a, f, "Zebrastreifen 3D");
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    private static void ZebraWaehlen(Hauptansicht a, Button knopf)
    {
        if (a.Karte.SetzenArt != AussenArt.Zebrastreifen || !ReferenceEquals(a.SetzenKnopfMarkiert, knopf))
        {
            a.SetzenKnopfDrueckenFuerTests(knopf);
        }
    }
}
