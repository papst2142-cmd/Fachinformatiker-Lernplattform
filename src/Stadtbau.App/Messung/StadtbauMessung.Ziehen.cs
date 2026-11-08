using System.Globalization;
using Avalonia;
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
/// Welle Z (S 0.14.0, Weisung P12-71): Ziehen von Punkt zu Punkt in einer leeren Stadt. Draufsicht: „Straße ziehen“ mit
/// zwei Tipps bis zum Bild (Messpunkt „Strasse ziehen (bis zum Bild)“, wiederholbar), dann mit einem Finger vom
/// Strassenende in einem Zug abbiegen (Boegen, ein Schritt, Anschluss wie im Spiel, kein Rot); Gegenprobe zweiter Finger
/// (nichts gesetzt, der Anfang faellt weg), Escape bricht erst den Anfang, dann das Ziehen ab; rote Vorschau (Ende hinter
/// dem Anfang) setzt nichts; „Fußweg ziehen“ von der Gehwegkante. 3D: zwei Tipps mit dem Finger bis zum Bild (Messpunkt
/// „3D Strasse ziehen (bis zum Bild)“), Schatten folgt der Maus gruen, Rechtsklick ohne Bewegung bricht ab. Layout PC und
/// Tablet, hell und dunkel, mit Vorschau.
/// </summary>
public static partial class StadtbauMessung
{
    private static readonly CultureInfo ZiehInv = CultureInfo.InvariantCulture;

    private static void ZiehWaehlen(Hauptansicht a, int knopf)
    {
        if (!ReferenceEquals(a.SetzenKnopfMarkiert, a.ZiehKnoepfe[knopf]))
        {
            a.SetzenKnopfDrueckenFuerTests(a.ZiehKnoepfe[knopf]);
        }
    }

    private static void FingerZug(Control c, Point von, Point bis)
    {
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        Druck(c, finger, von);
        for (var i = 1; i <= 8; i++)
        {
            Bewegung(c, finger, von + ((bis - von) * (i / 8.0)));
        }
        Los(c, finger, bis);
    }

    private static void FingerTipp(Control c, Point p)
    {
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        Druck(c, finger, p);
        Los(c, finger, p);
    }

    private static async Task ZiehenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Ziehen", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        if (a.ZiehKnoepfe.Count != 4 || a.ZiehKnoepfe.Any(b => !b.IsEffectivelyVisible))
        {
            Fund("Ziehen: die Knoepfe „Straße ziehen“, „Fußweg ziehen“, „Fluss ziehen“, „Bahn ziehen“ fehlen links");
            return;
        }
        var strassen = 0;
        int Strassen() => AussenBearbeitung.Strassen(p.AussenStand).Count;

        // 1: Messpunkt, zwei Tipps in der Draufsicht (Anfang, Ende)
        await MessenAsync("Strasse ziehen (bis zum Bild)", a, () =>
        {
            ZiehWaehlen(a, 0);
            k.ZiehAnfangSetzen(4, 6);
            if (k.ZiehenBis(14, 6) is null)
            {
                Fund("Ziehen: Strasse nicht gezogen (" + k.ZiehGrund + ")");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        a.Rueckgaengig(); // die gemessene Strasse wieder weg (Zuruecksetzen gilt nur zwischen den Versuchen)
        ZiehWaehlen(a, 0);
        FingerTipp(k, k.BildPunktFuerTests(4, 6));
        FingerTipp(k, k.BildPunktFuerTests(14, 6));
        await a.AussenGeprueft;
        strassen = Strassen();
        if (strassen != 1)
        {
            Fund(string.Create(ZiehInv, $"Ziehen: zwei Tipps mit dem Finger ergeben {strassen} statt 1 Strasse"));
            return;
        }

        // 2: mit einem Finger vom Strassenende in einem Zug abbiegen: Boegen, ein Schritt, Anschluss, kein Rot
        var schritte = p.SchritteZurueck;
        FingerZug(k, k.BildPunktFuerTests(14.2, 6.1), k.BildPunktFuerTests(22, 14));
        await a.AussenGeprueft;
        var linien = AussenBearbeitung.Strassen(p.AussenStand);
        Messwerte.Notiere(string.Create(ZiehInv, $"Ziehen: Finger-Zug ergibt {linien.Count} Strassen, Schritte +{p.SchritteZurueck - schritte}"));
        if (p.SchritteZurueck != schritte + 1 || linien.Count < 2 || !StadtSzene.MittellinienZuege(linien).Any(z => z.Strassen.Count == linien.Count))
        {
            Fund("Ziehen: Finger-Zug vom Strassenende ist nicht genau ein Schritt mit durchgehendem Anschluss");
        }
        if (a.AussenFunde.FirstOrDefault(x => x.Rot) is { } rot)
        {
            Fund("Ziehen: roter Fund nach dem Abbiegen: " + rot.Text);
        }
        a.Rueckgaengig();
        await a.AussenGeprueft;

        // Gegenprobe zweiter Finger: der eben begonnene Anfang faellt weg, nichts gesetzt
        schritte = p.SchritteZurueck;
        var eins = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var zwei = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, false);
        var s0 = k.BildPunktFuerTests(14, 6);
        Druck(k, eins, s0);
        Bewegung(k, eins, s0 + new Point(30, 30));
        Druck(k, zwei, s0 + new Point(150, 0));
        Bewegung(k, zwei, s0 + new Point(180, 0));
        Los(k, zwei, s0 + new Point(180, 0));
        Los(k, eins, s0 + new Point(30, 30));
        await BildAsync(k);
        if (p.SchritteZurueck != schritte || k.ZiehAnfang is not null)
        {
            Fund("Ziehen: zweiter Finger hat doch gezogen oder den Anfang behalten");
        }

        // Escape: erst der Anfang, dann das Ziehen
        k.ZiehAnfangSetzen(14, 6);
        k.Taste(Key.Escape, KeyModifiers.None);
        if (k.ZiehAnfang is not null || k.ZiehArtAktiv is null)
        {
            Fund("Ziehen: erstes Escape verwirft nicht nur den Anfang");
        }
        k.Taste(Key.Escape, KeyModifiers.None);
        if (k.SetzenArt is not null)
        {
            Fund("Ziehen: zweites Escape beendet das Ziehen nicht");
        }

        // rote Vorschau: Ende hinter dem Anfang, nichts gesetzt, Grund sichtbar
        ZiehWaehlen(a, 0);
        k.ZiehAnfangSetzen(14, 6);
        k.ZiehZielSetzen(13.6, 6.3);
        if (k.ZiehGrund is null)
        {
            Fund("Ziehen: Vorschau hinter dem Anfang ist nicht rot");
        }
        await AlleGroessenAsync(a, f, "Ziehen Vorschau rot");
        if (k.ZiehenBis(13.6, 6.3) is not null || p.SchritteZurueck != schritte)
        {
            Fund("Ziehen: rote Vorschau hat doch gesetzt");
        }
        k.ZiehAbbrechen();

        // Fusswege: von der Gehwegkante bis frei, keine roten Funde
        ZiehWaehlen(a, 1);
        k.ZiehAnfangSetzen(8, 6.7);
        k.ZiehZielSetzen(12, 14);
        await AlleGroessenAsync(a, f, "Fussweg ziehen Vorschau");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var weg = k.ZiehenBis(12, 14);
        await a.AussenGeprueft;
        if (weg is not { Art: AussenArt.Fussweg } || Fusswege.Alle(Objekt(p.AussenStand["stadtdaten"])).Count != 1)
        {
            Fund("Ziehen: Fussweg nicht gezogen (" + k.ZiehGrund + ")");
        }
        if (a.AussenFunde.FirstOrDefault(x => x.Rot) is { } rotWeg)
        {
            Fund("Ziehen: roter Fund nach dem Fussweg: " + rotWeg.Text);
        }
        k.Waehlen(weg);
        await AlleGroessenAsync(a, f, "Fussweg gewaehlt");
        a.SetzenBeenden();

        // 3D: zwei Tipps mit dem Finger, Schatten der Maus, Rechtsklick bricht ab
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Ziehen 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b3.GanzeStadt();
        await BildAsync(b3);
        var vorher = Strassen();
        await MessenAsync("3D Strasse ziehen (bis zum Bild)", b3, async () =>
        {
            ZiehWaehlen(a, 0);
            FingerTipp(b3, Bild(a, 4, 14));
            FingerTipp(b3, Bild(a, 14, 14));
            await a.DreiFertig;
            if (Strassen() != vorher + 1)
            {
                Fund("Ziehen 3D: zwei Tipps haben keine Strasse gezogen");
            }
        }, async () =>
        {
            a.Rueckgaengig();
            await a.DreiFertig;
        });
        a.Rueckgaengig();
        await a.DreiFertig;

        // Schatten folgt der Maus, gruen
        ZiehWaehlen(a, 0);
        FingerTipp(b3, Bild(a, 4, 14));
        var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        Bewegung(b3, maus, Bild(a, 14, 14));
        var gruen = await GeprueftAsync(b3);
        if (!b3.SchattenDa || gruen != true)
        {
            Fund("Ziehen 3D: kein gruener Schatten vom Anfang bis zur Maus (" + b3.SchattenText + ")");
        }
        await AlleGroessenAsync(a, f, "Ziehen 3D Vorschau");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        // Rechtsklick ohne Bewegung: der Anfang faellt weg, das Ziehen bleibt
        var rechts = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var rp = Bild(a, 10, 10);
        b3.RaiseEvent(new PointerPressedEventArgs(b3, rechts, b3, rp, 0, new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed), KeyModifiers.None));
        b3.RaiseEvent(new PointerReleasedEventArgs(b3, rechts, b3, rp, 0, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.RightButtonReleased), KeyModifiers.None, MouseButton.Right));
        await BildAsync(b3);
        if (k.ZiehAnfang is not null || k.ZiehArtAktiv is null || b3.SchattenDa)
        {
            Fund("Ziehen 3D: Rechtsklick ohne Bewegung bricht den Anfang nicht ab");
        }
        a.SetzenBeenden();
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        Messwerte.Notiere(string.Create(ZiehInv, $"Ziehen: fertig, {Strassen()} Strassen, {Fusswege.Alle(Objekt(p.AussenStand["stadtdaten"])).Count} Fussweg"));
    }
}
