using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle E4 Nachtrag (Weisungen P12-248/P12-249, Sammelliste 339, Nico: „Es funktioniert nicht mit der Maus“): Gelaende-
/// Pinsel und Knopf „Hügel“ mit der <b>Maus</b> in „Stadt von oben“ und in 3D. Leere Stadt 32 × 20 mit einer Strasse
/// (2; 3)–(30; 3).
/// Draufsicht: linke Maustaste zieht einen Zug = genau 1 Schritt, gemalte Hoehe &gt; 0; rechte Maustaste zieht nur die
/// Ansicht, kein Schritt (Gegenprobe). Drei Staerken (Nico 14.11.2026), voreingestellt „Mittel“, je Stufe die Hoehe in der Mitte. „Hügel“ mit einem Mausklick gesetzt.
/// 3D: „Hügel“ mit einem Mausklick; Pinsel mit der linken Maustaste (Messpunkt „3D Pinselzug (bis zum Bild)“, wiederholbar,
/// 400 ms): 1 Schritt, Gelaende hoeher, der Pinselkreis ist waehrend des Zugs zu sehen und liegt unter dem Zeiger, der Zug
/// trifft die Stelle unter dem Zeiger auch auf schon gehobenem Gelaende (Gegenprobe: der Schnitt mit der flachen Ebene
/// laege daneben), Rueckgaengig nimmt den Zug ganz zurueck. Ein Finger = 1 Schritt; zwei Finger, rechte und mittlere
/// Maustaste = kein Schritt (Gegenproben). Ein Zug ueber die Strasse aendert nichts (dieselbe Rechnung wie in der Draufsicht).
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task PinselMausAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Pinsel Maus", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        if (a.HuegelKnopf is not { } huegelKnopf)
        {
            Fund("Pinsel Maus: Knopf „Hügel“ fehlt");
            return;
        }
        p.AussenAendern("Straße", aa => Aussen.Landschaft(aa).Add(new PyObjekt
        {
            { "typ", "strasse" },
            { "punkte", new List<object?> { new List<object?> { 2.0, 3.0 }, new List<object?> { 30.0, 3.0 } } },
            { "gehweg", 0.35 },
        }));
        await a.AussenGeprueft;
        string Gemalt() => PythonJson.Text(AussenBearbeitung.GemaltListe(p.AussenStand));
        float HoeheAn(float x, float y) => AussenBearbeitung.GelaendeVon(p.AussenStand)?.Hoehe(x, y) ?? 0f;

        // Draufsicht: linke Maustaste malt einen Zug, ein Schritt
        a.PinselWaehlen(PinselArt.Heben);
        var schritte = p.SchritteZurueck;
        var start = k.Bildpunkt(6, 12);
        await MausZiehenAsync(k, start, k.Bildpunkt(10, 12).X - start.X, 0, "links");
        if (p.SchritteZurueck != schritte + 1 || HoeheAn(8, 12) <= 0f)
        {
            Fund(string.Create(Inv, $"Pinsel Maus Draufsicht: {p.SchritteZurueck - schritte} Schritte, Hoehe {HoeheAn(8, 12):0.00} (erwartet 1 Schritt, Hoehe > 0)"));
        }
        // Gegenprobe rechte Maustaste: nur die Ansicht, kein Schritt
        var vorher = Gemalt();
        schritte = p.SchritteZurueck;
        var bildVorher = k.Bildpunkt(8, 12);
        await MausZiehenAsync(k, k.Bildpunkt(20, 12), 60, 0, "rechts");
        if (p.SchritteZurueck != schritte || Gemalt() != vorher || Math.Abs(k.Bildpunkt(8, 12).X - bildVorher.X) < 30)
        {
            Fund("Pinsel Maus Draufsicht: die rechte Maustaste hat gemalt oder die Ansicht nicht geschoben (Gegenprobe)");
        }
        a.PinselWaehlen(PinselArt.Heben); // aus
        a.Rueckgaengig();
        await a.AussenGeprueft;

        // Nico 14.11.2026: drei voreingestellte Staerken; voreingestellt „Mittel“, je Stufe die Hoehe in der Mitte des Zugs
        if (a.PinselStaerkeKnoepfe.Count != 3 || a.PinselStaerkeKnoepfe.Any(x => a.KlicksBis(x) is not (0 or 1)) || Math.Abs(k.PinselStaerke - AussenBearbeitung.PinselStaerken[1]) > 1e-4f)
        {
            Fund("Pinsel: nicht drei Staerken oder nicht „Mittel“ voreingestellt");
        }
        a.PinselWaehlen(PinselArt.Heben);
        for (var i = 0; i < a.PinselStaerkeKnoepfe.Count; i++)
        {
            a.SetzenKnopfDrueckenFuerTests(a.PinselStaerkeKnoepfe[i]);
            k.PinselZugAnwenden([new(6, 12), new(10, 12)]);
            var soll = AussenBearbeitung.PinselStaerken[i];
            if (Math.Abs(HoeheAn(8, 12) - soll) > 0.011f)
            {
                Fund(string.Create(Inv, $"Pinsel Staerke {i + 1}: Hoehe {HoeheAn(8, 12):0.00} statt {soll:0.00}"));
            }
            a.Rueckgaengig();
        }
        a.SetzenKnopfDrueckenFuerTests(a.PinselStaerkeKnoepfe[1]);
        a.PinselWaehlen(PinselArt.Heben); // aus
        await a.AussenGeprueft;

        // Draufsicht: „Hügel“ mit einem Mausklick
        HuegelWaehlen(a, huegelKnopf);
        Klick(k, k.Bildpunkt(24, 13));
        await a.AussenGeprueft;
        if (AussenBearbeitung.HuegelListe(p.AussenStand).Count != 1)
        {
            Fund("Huegel Maus Draufsicht: ein Klick hat keinen Huegel gesetzt");
        }
        k.SetzenBeenden();
        a.Rueckgaengig();
        await a.AussenGeprueft;

        // 3D
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Pinsel 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b3.GanzeStadt();
        await BildAsync(b3);

        // „Hügel“ mit einem Mausklick in 3D
        HuegelWaehlen(a, huegelKnopf);
        Klick(b3, Bild(a, 24, 13));
        await a.DreiFertig;
        if (AussenBearbeitung.HuegelListe(p.AussenStand).Count != 1)
        {
            Fund("Huegel Maus 3D: ein Klick hat keinen Huegel gesetzt");
        }
        a.Karte.SetzenBeenden();
        a.Rueckgaengig();
        await a.DreiFertig;

        // Pinsel mit der linken Maustaste in 3D bis zum Bild
        a.PinselWaehlen(PinselArt.Heben);
        if (k.GelaendePinsel != PinselArt.Heben)
        {
            Fund("Pinsel 3D: „Heben“ nicht gewaehlt");
            return;
        }
        await MessenAsync("3D Pinselzug (bis zum Bild)", b3, async () =>
        {
            var s0 = p.SchritteZurueck;
            await Zug3DAsync(a, b3, 6, 15, 10, 15, "links");
            await a.DreiFertig;
            if (p.SchritteZurueck != s0 + 1)
            {
                Fund(string.Create(Inv, $"Pinsel 3D: ein Zug mit der Maus gab {p.SchritteZurueck - s0} Schritte statt 1"));
            }
        }, async () =>
        {
            a.Rueckgaengig();
            await a.DreiFertig;
        });
        if (AussenBearbeitung.GemaltListe(p.AussenStand).Count != 0)
        {
            a.Rueckgaengig();
            await a.DreiFertig;
        }

        // Zug von Hand: Pinselkreis waehrend des Zugs unter dem Zeiger, danach hoeher, Rueckgaengig ganz zurueck
        var leer = Gemalt();
        var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var von = Bild(a, 6, 15);
        Maus(b3, maus, von, RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        for (var x = 6.5; x <= 10; x += 0.5)
        {
            Maus(b3, maus, Bild(a, x, 15), RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other);
        }
        b3.InvalidateVisual();
        await BildAsync(b3);
        var bis = Bild(a, 10, 15);
        if (b3.PinselKreisRahmen.Width <= 0 || !b3.PinselKreisRahmen.Contains(bis) || !b3.PinselKreisRahmen.Contains(von))
        {
            Fund($"Pinsel 3D: kein Pinselkreis unter dem Zeiger waehrend des Zugs ({b3.PinselKreisRahmen}, {von}, {bis})");
        }
        if (b3.PinselZugPunkte.Count == 0 || Vector2.Distance(b3.PinselZugPunkte[0], new Vector2(6, 15)) > 0.15f)
        {
            Fund("Pinsel 3D: der Zug beginnt nicht an der Stelle unter dem Zeiger");
        }
        Maus(b3, maus, bis, RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased, los: true);
        await a.DreiFertig;
        await BildAsync(b3);
        if (HoeheAn(8, 15) <= 0f || b3.PinselKreisRahmen.Width > 0)
        {
            Fund(string.Create(Inv, $"Pinsel 3D: nach dem Zug Hoehe {HoeheAn(8, 15):0.00}, Kreis {b3.PinselKreisRahmen} (erwartet Hoehe > 0, kein Kreis)"));
        }
        a.Rueckgaengig();
        await a.DreiFertig;
        if (Gemalt() != leer)
        {
            Fund("Pinsel 3D: Rueckgaengig nimmt den Zug nicht ganz zurueck");
        }

        // Stelle auf schon gehobenem Gelaende: fuenf Zuege heben (8; 15) deutlich, dann zeigt die Maus genau dorthin
        for (var i = 0; i < 5; i++)
        {
            k.PinselZugAnwenden([new(6, 15), new(10, 15)]);
        }
        await a.DreiFertig;
        var kam = a.Drei.AktuelleKamera!;
        var h = HoeheAn(8, 15);
        if (kam.Bildpunkt(new Vector3(8, h, 15), (float)b3.Bounds.Width, (float)b3.Bounds.Height) is { } hb)
        {
            var hp = new Point(hb.X, hb.Y);
            var treffer = b3.GelaendeAn(hp);
            var flach = kam.Boden((float)hp.X, (float)hp.Y, (float)b3.Bounds.Width, (float)b3.Bounds.Height);
            if (treffer is not { } t || Vector2.Distance(t, new Vector2(8, 15)) > 0.1f)
            {
                Fund(string.Create(Inv, $"Pinsel 3D: auf gehobenem Gelaende (Hoehe {h:0.00}) trifft der Zeiger {treffer} statt (8; 15)"));
            }
            if (flach is { } fl && Vector2.Distance(fl, new Vector2(8, 15)) < 0.2f)
            {
                Fund(string.Create(Inv, $"Pinsel 3D Gegenprobe: die flache Ebene laege nur {Vector2.Distance(fl, new Vector2(8, 15)):0.00} daneben (Probe zu schwach)"));
            }
        }
        for (var i = 0; i < 5; i++)
        {
            a.Rueckgaengig();
        }
        await a.DreiFertig;

        // ein Finger malt; zwei Finger, rechte und mittlere Maustaste bewegen nur die Kamera
        var s1 = p.SchritteZurueck;
        await Zug3DAsync(a, b3, 6, 15, 10, 15, "finger");
        await a.DreiFertig;
        if (p.SchritteZurueck != s1 + 1)
        {
            Fund(string.Create(Inv, $"Pinsel 3D: ein Finger gab {p.SchritteZurueck - s1} Schritte statt 1"));
        }
        a.Rueckgaengig();
        await a.DreiFertig;
        var drehung = a.Drei.AktuelleKamera!.Drehung;
        foreach (var taste in new[] { "rechts", "mitte", "zwei" })
        {
            var s2 = p.SchritteZurueck;
            var g2 = Gemalt();
            await Zug3DAsync(a, b3, 12, 14, 16, 14, taste);
            await BildAsync(b3);
            if (p.SchritteZurueck != s2 || Gemalt() != g2)
            {
                Fund($"Pinsel 3D: „{taste}“ hat gemalt (Gegenprobe: nur die Kamera)");
            }
            // die rechte Maustaste und zwei Finger drehen die Kamera: zurueck auf die Lage davor (GanzeStadt laesst die Drehung)
            a.Drei.AktuelleKamera!.Drehung = drehung;
            b3.GanzeStadt();
            await BildAsync(b3);
        }

        // ueber der Strasse: dieselbe Rechnung wie in der Draufsicht (AussenBearbeitung.Pinselzug laesst Strassen nicht aus,
        // die Pruefung meldet das gemalte Gelaende an der Strasse rot); der Zug in 3D ergibt dieselbe Hoehe wie derselbe Zug
        // in der Draufsicht (Gegenprobe gegen eine zweite Rechnung)
        var s3 = p.SchritteZurueck;
        await Zug3DAsync(a, b3, 8, 3, 14, 3, "links");
        await a.DreiFertig;
        await a.AussenGeprueft;
        var hoehe3D = HoeheAn(11, 3);
        var rotStrasse = a.AussenFunde.Any(x => x.Rot && x.Text.Contains("Gemaltes Gelände", StringComparison.Ordinal));
        a.Rueckgaengig();
        await a.DreiFertig;
        k.PinselZugAnwenden([new(8, 3), new(14, 3)]);
        var hoehe2D = HoeheAn(11, 3);
        a.Rueckgaengig();
        await a.AussenGeprueft;
        if (p.SchritteZurueck != s3 || Math.Abs(hoehe3D - hoehe2D) > 0.011f || !rotStrasse)
        {
            Fund(string.Create(Inv, $"Pinsel 3D an der Strasse: 3D {hoehe3D:0.00}, Draufsicht {hoehe2D:0.00}, rot gemeldet {rotStrasse} (erwartet gleich und rot)"));
        }
        Messwerte.Notiere(string.Create(Inv, $"Pinsel an der Strasse (Sammelliste 339): 3D {hoehe3D:0.00}, Draufsicht {hoehe2D:0.00}, Pruefung rot {rotStrasse}"));
        await AlleGroessenAsync(a, f, "Pinsel 3D");
        k.GelaendePinsel = null;
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    /// <summary>Einen Zug in 3D von (x1; y1) nach (x2; y2): "links", "rechts", "mitte" (Maus), "finger" oder "zwei" (Finger).</summary>
    private static async Task Zug3DAsync(Hauptansicht a, DreiBedienung b3, double x1, double y1, double x2, double y2, string art)
    {
        var von = Bild(a, x1, y1);
        var punkte = Enumerable.Range(1, 8).Select(i => Bild(a, x1 + ((x2 - x1) * i / 8), y1 + ((y2 - y1) * i / 8))).ToList();
        switch (art)
        {
            case "finger":
            {
                var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
                Druck(b3, finger, von);
                punkte.ForEach(q => Bewegung(b3, finger, q));
                Los(b3, finger, punkte[^1]);
                break;
            }
            case "zwei":
            {
                var eins = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
                var zwei = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, false);
                var d = new Point(80, 0);
                Druck(b3, eins, von);
                Druck(b3, zwei, von + d);
                foreach (var q in punkte)
                {
                    Bewegung(b3, eins, q);
                    Bewegung(b3, zwei, q + d);
                }
                Los(b3, zwei, punkte[^1] + d);
                Los(b3, eins, punkte[^1]);
                break;
            }
            default:
            {
                var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
                var (mod, druck, los) = art switch
                {
                    "rechts" => (RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed, PointerUpdateKind.RightButtonReleased),
                    "mitte" => (RawInputModifiers.MiddleMouseButton, PointerUpdateKind.MiddleButtonPressed, PointerUpdateKind.MiddleButtonReleased),
                    _ => (RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed, PointerUpdateKind.LeftButtonReleased),
                };
                Maus(b3, maus, von, mod, druck);
                punkte.ForEach(q => Maus(b3, maus, q, mod, PointerUpdateKind.Other));
                Maus(b3, maus, punkte[^1], RawInputModifiers.None, los, los: true);
                break;
            }
        }
        await BildAsync(b3);
    }
}
