using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle E (S 0.13.0, Weisungen P12-63, P12-67 Fragen 1 = a und 2 = a): Drehgriff mit dem Finger in der Draufsicht und
/// der 3D-Ansicht (Linie in 15-Grad-Rasten, Haus in Vierteln, ein Schritt im Verlauf), Gegenprobe: derselbe Zug neben dem
/// Griff dreht nichts. Schalter „Einrasten“ (Taste E) wirkt auf die Draufsicht. Messpunkt „Drehgriff (bis zum Bild)“
/// (wiederholbar, 400 ms). Layout mit dem Feld „Winkel“ in allen Groessen.
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Punkt auf dem Kreis des Griffs um die Mitte, <paramref name="grad"/> im Uhrzeigersinn vom Griff aus.</summary>
    private static Point AufKreis(Point mitte, double grad)
    {
        var w = grad * Math.PI / 180;
        return new Point(mitte.X + (Drehgriff.Abstand * Math.Sin(w)), mitte.Y - (Drehgriff.Abstand * Math.Cos(w)));
    }

    /// <summary>Finger auf (start) legen, auf einem Bogen um die Mitte bis <paramref name="grad"/> ziehen, loslassen.</summary>
    private static void GriffZug(Control c, Point mitte, Point start, double grad)
    {
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        Druck(c, finger, start);
        var r = Math.Sqrt(((start.X - mitte.X) * (start.X - mitte.X)) + ((start.Y - mitte.Y) * (start.Y - mitte.Y)));
        var w0 = Math.Atan2(start.X - mitte.X, -(start.Y - mitte.Y));
        var ziel = start;
        for (var i = 1; i <= 8; i++)
        {
            var w = w0 + (grad * Math.PI / 180 * i / 8);
            ziel = new Point(mitte.X + (r * Math.Sin(w)), mitte.Y - (r * Math.Cos(w)));
            Bewegung(c, finger, ziel);
        }
        Los(c, finger, ziel);
    }

    private static async Task DrehenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Drehen", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        AussenStelle? strasse = null, haus = null;
        p.AussenAendern("Starttest", aa =>
        {
            strasse = AussenBearbeitung.Setzen(aa, AussenArt.Strasse, 16, 10);
            haus = AussenBearbeitung.Setzen(aa, AussenArt.Gebaeude, 10, 5, modell: AussenBearbeitung.HausModelle(aa)[0]);
            return true;
        });
        await a.AussenGeprueft;
        if (strasse is not { } st || haus is not { } hs)
        {
            Fund("Drehen: Strasse oder Gebaeude nicht gesetzt");
            return;
        }
        int? Winkel() => AussenBearbeitung.Winkel(p.AussenStand, st);

        // 1: Draufsicht, Strasse waehlen, Griff mit dem Finger 30 Grad im Uhrzeigersinn ziehen
        a.Karte.Waehlen(st);
        await BildAsync(a);
        if (a.Karte.GriffMitte is not { } m || a.Karte.Griff is not { } g)
        {
            Fund("Drehgriff 2D: kein Griff an der gewaehlten Strasse");
            return;
        }
        var vor = p.SchritteZurueck;
        GriffZug(a.Karte, m, g, 31);
        await a.AussenGeprueft;
        Messwerte.Notiere(string.Create(CultureInfo.InvariantCulture, $"Drehgriff 2D: Winkel {Winkel()}, Schritte +{p.SchritteZurueck - vor}"));
        if (Winkel() != 30 || p.SchritteZurueck != vor + 1)
        {
            Fund($"Drehgriff 2D: Strasse dreht nicht in 15-Grad-Rasten (Winkel {Winkel()}, soll 30, Schritte +{p.SchritteZurueck - vor})");
        }
        // Gegenprobe: derselbe Zug 60 Bildpunkte neben dem Griff dreht nichts
        a.Rueckgaengig();
        await a.AussenGeprueft;
        a.Karte.Waehlen(st);
        await BildAsync(a);
        var daneben = new Point(g.X + 60, g.Y);
        GriffZug(a.Karte, m, daneben, 31);
        await a.AussenGeprueft;
        if (Winkel() != 0)
        {
            Fund($"Drehgriff 2D Gegenprobe: Zug neben dem Griff dreht auch (Winkel {Winkel()})");
        }
        if (p.SchritteZurueck != vor)
        {
            a.Rueckgaengig();
            await a.AussenGeprueft;
        }

        // Messpunkt: Drehgriff bis zum Bild (wiederholbar)
        a.Karte.Waehlen(st);
        await BildAsync(a);
        await MessenAsync("Drehgriff (bis zum Bild)", a, async () =>
        {
            var mm = a.Karte.GriffMitte ?? m;
            a.Karte.GriffZiehenFuerTests(AufKreis(mm, 15).X, AufKreis(mm, 15).Y);
            await a.AussenGeprueft;
        }, async () =>
        {
            a.Rueckgaengig();
            await a.AussenGeprueft;
            a.Karte.Waehlen(st);
        });
        a.Rueckgaengig();
        await a.AussenGeprueft;

        // 2: Haus mit dem Griff eine Vierteldrehung im Uhrzeigersinn; 30 Grad bleiben ohne Wirkung
        a.Karte.Waehlen(hs);
        await BildAsync(a);
        if (a.Karte.GriffMitte is { } hm && a.Karte.Griff is { } hg)
        {
            var hvor = p.SchritteZurueck;
            GriffZug(a.Karte, hm, hg, 30);
            await a.AussenGeprueft;
            if (AussenBearbeitung.HausDreh(p.AussenStand, hs) != 0 || p.SchritteZurueck != hvor)
            {
                Fund("Drehgriff 2D: Haus dreht schon bei 30 Grad");
            }
            a.Karte.Waehlen(hs);
            await BildAsync(a);
            GriffZug(a.Karte, hm, hg, 88);
            await a.AussenGeprueft;
            if (AussenBearbeitung.HausDreh(p.AussenStand, hs) != 1)
            {
                Fund($"Drehgriff 2D: Haus dreht nicht eine Viertel weiter (Drehung {AussenBearbeitung.HausDreh(p.AussenStand, hs)})");
            }
        }
        else
        {
            Fund("Drehgriff 2D: kein Griff am gewaehlten Haus");
        }

        // 3: Schalter Einrasten (Taste E) und Layout mit dem Feld „Winkel“
        var schalter = a.EinrastenSchalter;
        if (schalter.IsChecked != true || !a.Karte.Raster)
        {
            Fund("Einrasten: Schalter ist zu Beginn nicht an");
        }
        schalter.IsChecked = false;
        if (a.Karte.Raster)
        {
            Fund("Einrasten: Schalter aus wirkt nicht auf die Draufsicht");
        }
        schalter.IsChecked = true;
        a.Karte.Waehlen(st);
        await BildAsync(a);
        await AlleGroessenAsync(a, f, "Drehen (Winkel)");

        // 4: 3D-Ansicht: derselbe Griff ueber der Strasse
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Drehgriff 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b3.GanzeStadt();
        await BildAsync(b3);
        a.Karte.Waehlen(st);
        await BildAsync(b3);
        if (b3.GriffMitte is not { } m3 || b3.Griff3D is not { } g3)
        {
            Fund("Drehgriff 3D: kein Griff an der gewaehlten Strasse");
        }
        else
        {
            var w0 = Winkel();
            var v3 = p.SchritteZurueck;
            GriffZug(b3, m3, g3, -46);
            await a.DreiFertig;
            await a.AussenGeprueft;
            var soll = ((w0 ?? 0) + 360 - 45) % 360;
            Messwerte.Notiere(string.Create(CultureInfo.InvariantCulture, $"Drehgriff 3D: Winkel {Winkel()}, Schritte +{p.SchritteZurueck - v3}"));
            if (Winkel() != soll || p.SchritteZurueck != v3 + 1)
            {
                Fund($"Drehgriff 3D: Strasse dreht nicht in 15-Grad-Rasten (Winkel {Winkel()}, soll {soll})");
            }
            a.Rueckgaengig();
            await a.DreiFertig;
            a.Karte.Waehlen(st);
            await BildAsync(b3);
            // Gegenprobe: neben dem Griff dreht derselbe Finger die Strasse nicht
            GriffZug(b3, m3, new Point(g3.X + 60, g3.Y), -46);
            await a.DreiFertig;
            if (Winkel() != w0)
            {
                Fund($"Drehgriff 3D Gegenprobe: Zug neben dem Griff dreht auch (Winkel {Winkel()})");
            }
        }
        await AlleGroessenAsync(a, f, "Drehen 3D");
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }
}
