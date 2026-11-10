using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle A 0.61.0 / S 0.36.0 (Weisung P12-171): Haltestellen, Bus und Parkplatz in einer leeren Stadt mit einem Ring aus vier
/// Strassen mit Gehweg (2; 4)–(30; 4)–(30; 16)–(2; 16). Messpunkte (wiederholbar, 400 ms): „Haltestelle setzen (bis zum
/// Bild)“, „Parkplatz setzen (bis zum Bild)“, „3D Haltestelle setzen (bis zum Bild)“ und „Buslinie anlegen“ (Linie und
/// Busse aus Strassen und Haltestellen, wie die 3D-Ansicht sie im Hintergrund baut). Geprueft: Haltestelle an der Strasse,
/// Seite wie getippt, kein roter Fund; Parkplatz mit 12 Plaetzen in zwei Reihen; der Bus faehrt alle Haltestellen an und
/// haelt; die wartende Figur verschwindet beim Halt und kommt wieder (Gegenprobe: ohne Bus bleibt sie); Gegenprobe Setzen
/// fern einer Strasse mit Gehweg setzt nichts. Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task HaltestellenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Haltestellen", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        if (a.HaltestelleKnopf is not { } haltKnopf || a.ParkplatzKnopf is not { } parkKnopf
            || a.KlicksBis(haltKnopf) is not (0 or 1) || a.KlicksBis(parkKnopf) is not (0 or 1))
        {
            Fund("Haltestellen: die Knöpfe „Haltestelle“ und „Parkplatz“ fehlen links");
            return;
        }
        static PyObjekt Strasse(double x1, double y1, double x2, double y2) => new()
        {
            { "typ", "strasse" },
            { "punkte", new List<object?> { new List<object?> { x1, y1 }, new List<object?> { x2, y2 } } },
            { "gehweg", 0.35 },
        };
        p.AussenAendern("Straße", aa =>
        {
            Aussen.Landschaft(aa).Add(Strasse(2, 4, 30, 4));
            Aussen.Landschaft(aa).Add(Strasse(30, 4, 30, 16));
            Aussen.Landschaft(aa).Add(Strasse(30, 16, 2, 16));
            Aussen.Landschaft(aa).Add(Strasse(2, 16, 2, 4));
        });
        await a.AussenGeprueft;

        await MessenAsync("Haltestelle setzen (bis zum Bild)", a, () =>
        {
            Waehlen(a, haltKnopf, AussenArt.Haltestelle);
            if (k.SetzenAn(10, 4.9) is null)
            {
                Fund("Haltestelle: nicht gesetzt");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        a.Rueckgaengig();
        Waehlen(a, haltKnopf, AussenArt.Haltestelle);
        var h1 = k.SetzenAn(10, 4.9);
        Waehlen(a, haltKnopf, AussenArt.Haltestelle);
        var h2 = k.SetzenAn(20, 15.1);
        await a.AussenGeprueft;
        if (h1 is null || h2 is null || AussenBearbeitung.HaltestelleVon(p.AussenStand, h1) is not { } halt
            || Vector2.Distance(halt.Mitte, new Vector2(10, 4)) > 0.001f || halt.Aussen.Y < 0.99f)
        {
            Fund("Haltestelle: liegt nicht bei (10; 4) auf der getippten Seite");
            return;
        }
        // Gegenprobe: fern jeder Strasse mit Gehweg kommt keine, nichts im Verlauf
        var schritte = p.SchritteZurueck;
        Waehlen(a, haltKnopf, AussenArt.Haltestelle);
        if (k.SetzenAn(16, 10) is not null || p.SchritteZurueck != schritte)
        {
            Fund("Haltestelle: mitten in der Stadt gesetzt (Gegenprobe)");
        }
        k.SetzenBeenden();

        await MessenAsync("Parkplatz setzen (bis zum Bild)", a, () =>
        {
            Waehlen(a, parkKnopf, AussenArt.Parkplatz);
            if (k.SetzenAn(22, 4.9) is null)
            {
                Fund("Parkplatz: nicht gesetzt");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        a.Rueckgaengig();
        Waehlen(a, parkKnopf, AussenArt.Parkplatz);
        var pp = k.SetzenAn(22, 4.9);
        k.SetzenBeenden();
        if (pp is null)
        {
            Fund("Parkplatz: nicht gesetzt");
            return;
        }
        k.Waehlen(pp);
        await BildAsync(a);
        if (a.ParkplatzKnoepfe is not { } pk)
        {
            Fund("Parkplatz: Knöpfe für Plätze und Reihen fehlen rechts");
            return;
        }
        // auf 12 Plaetze in zwei Reihen (Nico P12-170: voller Parkplatz mit 12 Plaetzen)
        a.ParkplatzSetzen(pp, 12, 2);
        await a.AussenGeprueft;
        if (AussenBearbeitung.ParkplatzVon(p.AussenStand, pp) is not { Plaetze: 12, Reihen: 2 })
        {
            Fund("Parkplatz: 12 Plätze in zwei Reihen nicht übernommen");
        }
        if (a.AussenFunde.Any(x => x.Rot))
        {
            Fund("Haltestellen: rot: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot).Select(x => x.Text)));
        }
        await AlleGroessenAsync(a, f, "Parkplatz gewaehlt");
        k.Waehlen(h1);
        await AlleGroessenAsync(a, f, "Haltestelle gewaehlt");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // Buslinie und Busse aus den Strassen und Haltestellen (wie im Hintergrund der 3D-Ansicht)
        var (stand, daten) = (p.Stand(), p.Daten);
        var szene = await Task.Run(() => Ansicht3D.Bauen(stand, daten, Ansicht3D.Einstellung.Standard).Szene);
        Verkehr? verkehr = null;
        await MessenAsync("Buslinie anlegen", a, async () =>
        {
            verkehr = await Task.Run(() => Verkehr.Fuer(szene));
        });
        var bus = verkehr?.Autos.FirstOrDefault(x => x.Nummer == Buslinie.ErsteNummer);
        if (verkehr is null || bus is null || bus.Halte.Count != 2 || verkehr.Wartende.Count != 2)
        {
            Fund("Bus: kein Bus mit zwei Halten und zwei wartenden Figuren");
            return;
        }
        var (weg, wieder, ohneWeg) = (false, false, false);
        var w = verkehr.Wartende[0];
        for (var t = 0; t < 120 * 20 && !wieder; t++)
        {
            verkehr.Weiter(1.0 / 20, -1000, -1000);
            weg |= !verkehr.Sichtbar(w);
            wieder = weg && verkehr.Sichtbar(w);
        }
        // Gegenprobe: ohne Bus verschwindet die Figur nie
        var ohne = Verkehr.Fuer(szene);
        ohne.Autos.RemoveAll(x => x.Nummer >= Buslinie.ErsteNummer);
        for (var t = 0; t < 60 * 20; t++)
        {
            ohne.Weiter(1.0 / 20, -1000, -1000);
            ohneWeg |= !ohne.Sichtbar(ohne.Wartende[0]);
        }
        if (!weg || !wieder || ohneWeg)
        {
            Fund($"Bus: wartende Figur verschwindet {(weg ? "ja" : "nein")}, kommt wieder {(wieder ? "ja" : "nein")}, Gegenprobe ohne Bus verschwindet {(ohneWeg ? "ja" : "nein")}");
        }
        Messwerte.Notiere($"  Bus: {bus.Halte.Count} Halte, Rundweg {bus.Laenge:0.0}, Figur weg und wieder: {weg && wieder}, Gegenprobe ohne Bus: {!ohneWeg}");

        // 3D: die zweite Haltestelle weg, dann mit einem Tipp des Fingers setzen bis zum Bild
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Haltestelle 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b3.GanzeStadt();
        await BildAsync(b3);
        var zahl = AussenBearbeitung.HaltListe(p.AussenStand, AussenArt.Haltestelle).Count;
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        await MessenAsync("3D Haltestelle setzen (bis zum Bild)", b3, async () =>
        {
            Waehlen(a, haltKnopf, AussenArt.Haltestelle);
            var bild = Bild(a, 6, 16.9);
            Druck(b3, finger, bild);
            Los(b3, finger, bild);
            await a.DreiFertig;
            if (AussenBearbeitung.HaltListe(p.AussenStand, AussenArt.Haltestelle).Count != zahl + 1)
            {
                Fund("Haltestelle 3D: ein Tipp hat keine Haltestelle gesetzt");
            }
        }, async () =>
        {
            a.Rueckgaengig();
            await a.DreiFertig;
        });
        await AlleGroessenAsync(a, f, "Haltestelle 3D");
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    private static void Waehlen(Hauptansicht a, Button knopf, AussenArt art)
    {
        if (a.Karte.SetzenArt != art || !ReferenceEquals(a.SetzenKnopfMarkiert, knopf))
        {
            a.SetzenKnopfDrueckenFuerTests(knopf);
        }
    }
}
