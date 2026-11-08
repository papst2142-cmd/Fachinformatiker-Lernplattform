using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using static Lernplattform.Core.Spiel.SpielDaten;

using Point = Avalonia.Point;

namespace Stadtbau.Messung;

/// <summary>
/// Welle K1 (S 0.10.0, Weisung P12-55): Kurven in einer leeren Stadt. Kurve 90° mit dem Knopf links setzen bis zum Bild
/// (Messpunkt „Kurve setzen (bis zum Bild)“, wiederholbar), ihr Anfang rastet am Ende einer geraden Strasse ein (0,5
/// daneben gesetzt); beim Setzen drehen und spiegeln (Knopf und Tasten R, M); gewaehlte Kurve drehen und spiegeln (je ein
/// Schritt); mit dem Finger ziehen bleibt die Kurve ganz (ein Schritt). In 3D: Kurve 45° mit einem Tipp des Fingers setzen
/// bis zum Bild (Messpunkt „3D Kurve setzen (bis zum Bild)“), einrasten, drehen, spiegeln. Radius am Eckpunkt eines
/// Flusses ueber die Knoepfe rechts. Layout PC und Tablet, hell und dunkel, mit gewaehlter Kurve und im Setzen.
/// </summary>
public static partial class StadtbauMessung
{
    private static List<Vector2> KurvenLinie(Projekt p, AussenStelle s) =>
        [.. Liste(Objekt(Aussen.Landschaft(p.AussenStand)[s.Nummer])["punkte"]).Select(q => new Vector2((float)Py.F(Liste(q)[0]), (float)Py.F(Liste(q)[1])))];

    /// <summary>Kurven-Knopf links waehlen, nur wenn er nicht schon gewaehlt ist (zweites Antippen waehlt ab, P12-53).</summary>
    private static void KurveWaehlen(Hauptansicht a, int knopf)
    {
        if (a.Karte.SetzenArt != AussenArt.Kurve || !ReferenceEquals(a.SetzenKnopfMarkiert, a.KurvenKnoepfe[knopf]))
        {
            a.SetzenKnopfDrueckenFuerTests(a.KurvenKnoepfe[knopf]);
        }
    }

    private static async Task KurvenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Kurven", null, (24, 16));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        // gerade Strasse (4; 8) bis (8; 8)
        k.SetzenArt = AussenArt.Strasse;
        k.SetzenAn(6, 8);
        await a.AussenGeprueft;
        await BildAsync(a);
        if (a.KurvenKnoepfe.Count != 3 || a.KurvenKnoepfe.Any(b => a.KlicksBis(b) is not (0 or 1)))
        {
            Fund("Kurven: die drei Knoepfe Kurve 90°, Kurve 45°, S-Kurve fehlen links");
            return;
        }
        a.KurvenRadiusWahl.SelectedIndex = 1;
        var anzahl = Aussen.Landschaft(p.AussenStand).Count;

        // Kurve 90° setzen bis zum Bild: Mitte (9,5; 9) legt den Anfang 0,5 neben das Strassenende, er rastet dort ein
        await MessenAsync("Kurve setzen (bis zum Bild)", a, () =>
        {
            KurveWaehlen(a, 0);
            if (k.SetzenArt != AussenArt.Kurve || k.SetzenAn(9.5, 9) is null)
            {
                Fund("Kurven: Kurve 90° nicht gesetzt");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        a.Rueckgaengig(); // die gemessene Kurve wieder weg (Zuruecksetzen gilt nur zwischen den Versuchen)
        KurveWaehlen(a, 0);
        var kurve = k.SetzenAn(9.5, 9);
        await a.AussenGeprueft;
        if (kurve is null || Aussen.Landschaft(p.AussenStand).Count != anzahl + 1 || !AussenBearbeitung.IstKurve(p.AussenStand, kurve))
        {
            Fund(string.Create(Inv, $"Kurven: nach dem Setzen keine Kurve in der Stadt (gesetzt {kurve is not null}, Art {k.SetzenArt}, Stuecke {Aussen.Landschaft(p.AussenStand).Count} statt {anzahl + 1})"));
            return;
        }
        var linie = KurvenLinie(p, kurve);
        if (Vector2.Distance(linie[0], new Vector2(8, 8)) > 0.011f)
        {
            Fund(string.Create(Inv, $"Kurven: der Anfang rastet nicht am Strassenende (8; 8) ein, liegt bei {linie[0].X:F2}; {linie[0].Y:F2}"));
        }
        if (a.AussenFunde.Any(x => x.Rot))
        {
            Fund("Kurven: Kurve am Strassenende hat rote Funde: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot).Select(x => x.Text)));
        }
        await AlleGroessenAsync(a, f, "Kurve gewaehlt");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // gewaehlte Kurve drehen (R) und spiegeln (M): je ein Schritt, Form bleibt
        var schritte = p.SchritteZurueck;
        k.Waehlen(kurve);
        k.Taste(Key.M, KeyModifiers.None);
        var gespiegelt = KurvenLinie(p, kurve);
        if (p.SchritteZurueck != schritte + 1 || (gespiegelt[^1].Y - gespiegelt[0].Y) * (linie[^1].Y - linie[0].Y) >= 0)
        {
            Fund("Kurven: Taste M spiegelt die gewaehlte Kurve nicht (ein Schritt)");
        }
        k.Taste(Key.R, KeyModifiers.None);
        var gedreht = KurvenLinie(p, kurve);
        if (p.SchritteZurueck != schritte + 2 || Math.Abs(Vector2.Distance(gedreht[0], gedreht[^1]) - Vector2.Distance(linie[0], linie[^1])) > 0.05)
        {
            Fund("Kurven: Taste R dreht die gewaehlte Kurve nicht starr (ein Schritt)");
        }
        a.Rueckgaengig();
        a.Rueckgaengig();
        if (KurvenLinie(p, kurve).Zip(linie).Any(z => Vector2.Distance(z.First, z.Second) > 1e-4f))
        {
            Fund("Kurven: zweimal Rueckgaengig bringt die Kurve nicht zurueck");
        }

        // mit dem Finger ziehen: die ganze Kurve geht mit (ein Schritt)
        await BildAsync(a);
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var start = k.Bildpunkt(linie[6].X, linie[6].Y);
        var ziel = k.Bildpunkt(linie[6].X + 2, linie[6].Y + 1);
        schritte = p.SchritteZurueck;
        Druck(k, finger, start);
        for (var i = 1; i <= 6; i++)
        {
            Bewegung(k, finger, start + ((ziel - start) * (i / 6.0)));
        }
        Los(k, finger, ziel);
        await BildAsync(a);
        var gezogen = KurvenLinie(p, kurve);
        if (p.SchritteZurueck != schritte + 1 || gezogen.Zip(linie).Any(z => Vector2.Distance(z.First - z.Second, new Vector2(2, 1)) > 0.02f))
        {
            Fund("Kurven: mit dem Finger gezogen bleibt die Kurve nicht ganz oder es sind mehrere Schritte (" + string.Join(" ", gezogen.Take(3)) + ")");
        }
        a.Rueckgaengig();

        // beim Setzen drehen und spiegeln: S-Kurve gedreht und gespiegelt an anderer Stelle
        KurveWaehlen(a, 2);
        k.Taste(Key.R, KeyModifiers.None);
        k.Taste(Key.M, KeyModifiers.None);
        if (!k.SetzenGespiegelt || k.SetzenDreh != 1)
        {
            Fund("Kurven: beim Setzen drehen (R) oder spiegeln (M) wirkt nicht");
        }
        await AlleGroessenAsync(a, f, "Kurve setzen");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var s = k.SetzenAn(18, 6);
        if (s is null || KurvenLinie(p, s) is not { Count: 13 } sk || MathF.Abs(sk[1].X - sk[0].X) > MathF.Abs(sk[1].Y - sk[0].Y))
        {
            Fund("Kurven: S-Kurve gedreht nicht senkrecht gesetzt");
        }
        k.SetzenDreh = 0;

        // Radius am Eckpunkt eines Flusses ueber die Knoepfe rechts
        k.SetzenArt = AussenArt.Fluss;
        var fluss = k.SetzenAn(12, 3);
        await a.AussenGeprueft;
        if (fluss is null)
        {
            Fund("Kurven: Fluss nicht gesetzt");
        }
        else
        {
            k.Waehlen(fluss with { Punkt = 1 });
            await BildAsync(a);
            var knopf = a.KnoepfeMit("5").FirstOrDefault(b => b.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetName(b) == Texte.RadiusHier + " 5");
            if (knopf is null)
            {
                Fund("Kurven: Knopf „Radius 5“ am Eckpunkt des Flusses fehlt");
            }
            else
            {
                knopf.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                if (AussenBearbeitung.FlussRadius(p.AussenStand, fluss with { Punkt = 1 }) != 5)
                {
                    Fund("Kurven: Knopf „Radius 5“ setzt den Radius nicht");
                }
            }
            await AlleGroessenAsync(a, f, "Fluss Radius");
            await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        }

        // 3D: Kurve 45° mit einem Tipp des Fingers setzen bis zum Bild, rastet am Strassenende ein
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Kurven 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b3.GanzeStadt();
        await BildAsync(b3);
        // Anfang links (4; 8) der geraden Strasse: gespiegelte Kurve 45° nach links oben, Mitte so, dass das Ende 0,4 neben (4; 8) liegt
        var fingerDrei = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var vorher = Aussen.Landschaft(p.AussenStand).Count;
        await MessenAsync("3D Kurve setzen (bis zum Bild)", b3, async () =>
        {
            KurveWaehlen(a, 1);
            var bild = Bild(a, 6, 12.5);
            Druck(b3, fingerDrei, bild);
            Los(b3, fingerDrei, bild);
            await a.DreiFertig;
            if (Aussen.Landschaft(p.AussenStand).Count != vorher + 1)
            {
                Fund("Kurven 3D: ein Tipp hat keine Kurve gesetzt");
            }
        }, async () =>
        {
            a.Rueckgaengig();
            await a.DreiFertig;
        });
        a.Rueckgaengig(); // die gemessene Kurve wieder weg
        await a.DreiFertig;
        // Einrasten in 3D: Kurve 45° gedreht, ein Ende 0,3 und 0,2 neben dem Ende (10; 10) der Kurve 90°
        var ende = linie[^1];
        KurveWaehlen(a, 1);
        k.SetzenDreh = 1;
        // gedreht um 90 Grad: aus (x, y) wird (-y, x); der ANFANG zeigt dann nach unten wie das Ende der Kurve 90° und
        // soll 0,3 / 0,2 neben diesem Ende liegen (S 0.12.1: vorher lag hier das Ende der Kurve 45° in Gegenrichtung, das
        // alte Einrasten schob es trotzdem auf das Ende, die Kurven lagen dann rueckwaerts aufeinander)
        var gedreht45 = Strassenkurve.Mittellinie(Strassenkurve.Bogen, 45, 2, k.SetzenGespiegelt).Select(q => new Vector2(-q.Y, q.X)).ToList();
        var mitte45 = new Vector2((gedreht45.Min(q => q.X) + gedreht45.Max(q => q.X)) / 2, (gedreht45.Min(q => q.Y) + gedreht45.Max(q => q.Y)) / 2);
        var setzen = ende - (gedreht45[0] - mitte45) + new Vector2(0.3f, 0.2f);
        var tipp = Bild(a, setzen.X, setzen.Y);
        Druck(b3, fingerDrei, tipp);
        Los(b3, fingerDrei, tipp);
        await a.DreiFertig;
        var neu = a.Karte.Auswahl;
        k.SetzenDreh = 0;
        if (neu is null || !AussenBearbeitung.IstKurve(p.AussenStand, neu))
        {
            Fund("Kurven 3D: gedrehte Kurve 45° nicht gesetzt");
        }
        else
        {
            var l3 = KurvenLinie(p, neu);
            var naechst = Vector2.Distance(l3[0], ende);
            Messwerte.Notiere(string.Create(Inv, $"Kurven 3D: Kurve 45° gesetzt, Anfang {naechst:F2} vom Ende der Kurve 90°"));
            if (naechst > 0.011f)
            {
                Fund(string.Create(Inv, $"Kurven 3D: Ende {naechst:F2} neben dem Strassenende rastet nicht ein"));
            }
            // drehen und spiegeln in 3D (Tasten wie in der Draufsicht)
            var vorDreh = p.SchritteZurueck;
            k.Waehlen(neu);
            k.Taste(Key.M, KeyModifiers.None);
            await a.DreiFertig;
            k.Taste(Key.R, KeyModifiers.None);
            await a.DreiFertig;
            if (p.SchritteZurueck != vorDreh + 2)
            {
                Fund("Kurven 3D: spiegeln und drehen sind nicht je ein Schritt");
            }
        }
        await AlleGroessenAsync(a, f, "Kurven 3D");
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }
}
