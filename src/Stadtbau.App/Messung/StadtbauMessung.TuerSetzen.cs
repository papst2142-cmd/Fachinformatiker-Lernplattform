using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.28.0 (Weisung P12-121, Nico 2a 3a, Sammelliste 260): leere Stadt mit einer Strasse, eine Wohnung davor.
/// Knopf „Tür setzen“, dann in der Draufsicht die Westwand antippen bis zum Bild (Messpunkt „Tuer setzen (bis zum Bild)“,
/// wiederholbar, 400 ms): Tuer auf der Westwand, ein Schritt. Danach die Tuer mit dem Finger um die Ecke auf die Nordwand
/// ziehen (ein Schritt), in 3D die Ostwand antippen. Gegenprobe: ein Tipp fern des Hauses setzt nichts.
/// Layout PC und Tablet, hell und dunkel, mit Tuer-Setzen an.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task TuerSetzenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Tuer setzen", null, (40, 40));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        var s = p.AussenAendern("Wohnung", aa => AussenBearbeitung.Setzen(aa, AussenArt.Gebaeude, 20, 15, raster: true, modell: "wohnung")!);
        p.AussenAendern("Strasse", aa => Aussen.Landschaft(aa).Add(new PyObjekt
        {
            { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { 2.0, 24.0 }, new List<object?> { 38.0, 24.0 } } }, { "gehweg", 0.35 },
        }));
        await a.AussenGeprueft;
        k.Waehlen(s);
        await BildAsync(a);
        if (TuerKnopfSuchen(a, Texte.TuerSetzenKnopf) is not { } knopf)
        {
            Fund("Tuer setzen: Knopf „" + Texte.TuerSetzenKnopf + "“ fehlt unter „Ausgewähltes Stück“");
            return;
        }
        knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        if (k.TuerFuer != s)
        {
            Fund("Tuer setzen: Knopf startet das Tuer-Setzen nicht");
            return;
        }
        if (TuerKnopfSuchen(a, Texte.TuerSetzenBeenden) is null)
        {
            Fund("Tuer setzen: kein Knopf „" + Texte.TuerSetzenBeenden + "“ waehrend des Setzens");
        }
        var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        var west = new Vector2((float)hx, (float)(hy + (hh / 2)));
        var nord = new Vector2((float)(hx + (hw / 2)), (float)hy);
        var ost = new Vector2((float)(hx + hw), (float)(hy + (hh / 2)));

        // Sammelliste 277: ein Tipp vor fertiger Fassade wird erst gesetzt, wenn sie da ist; der Messpunkt misst den Schritt
        await k.FassadeBereit;
        // Messpunkt: Finger tippt die Westwand, bis zum Bild
        await MessenAsync("Tuer setzen (bis zum Bild)", a, () =>
        {
            if (k.TuerFuer != s)
            {
                k.TuerSetzenStarten(s, still: true);
            }
            FingerTipp(k, k.Bildpunkt(west.X - 0.1, west.Y));
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        await a.AussenGeprueft;
        if (k.TuerFuer != s)
        {
            k.TuerSetzenStarten(s, still: true);
        }
        if (!AufWand(p, s, west))
        {
            Fund("Tuer setzen: nach dem Tipp liegt die Tuer nicht auf der Westwand");
            return;
        }
        if (a.AussenFunde.FirstOrDefault(x => x.Rot) is { } rot)
        {
            Fund("Tuer setzen: roter Fund nach dem Setzen: " + rot.Text);
        }
        await AlleGroessenAsync(a, f, "Tuer setzen");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        if (k.TuerFuer != s)
        {
            k.TuerSetzenStarten(s, still: true);
        }

        // Ziehen um die Ecke: ein Zug = ein Schritt
        var schritte = p.SchritteZurueck;
        var tuer = TuerSetzen.Tuer(p.AussenStand, s)!.Value;
        FingerZug(k, k.Bildpunkt(tuer.X, tuer.Y), k.Bildpunkt(nord.X, nord.Y - 0.1));
        await a.AussenGeprueft;
        if (!AufWand(p, s, nord))
        {
            Fund("Tuer setzen: Ziehen um die Ecke legt die Tuer nicht auf die Nordwand");
        }
        if (p.SchritteZurueck != schritte + 1)
        {
            Fund("Tuer setzen: Ziehen ist nicht genau ein Schritt (" + (p.SchritteZurueck - schritte) + ")");
        }

        // Gegenprobe: Tipp fern des Hauses setzt nichts
        schritte = p.SchritteZurueck;
        if (k.TuerFuer != s)
        {
            k.TuerSetzenStarten(s, still: true);
        }
        FingerTipp(k, k.Bildpunkt(5, 5));
        if (p.SchritteZurueck != schritte || !AufWand(p, s, nord))
        {
            Fund("Tuer setzen: Tipp fern des Hauses hat die Tuer versetzt (Gegenprobe)");
        }

        // 3D: Ostwand antippen
        if (k.TuerFuer != s)
        {
            k.TuerSetzenStarten(s, still: true);
        }
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Tuer setzen 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
        }
        else
        {
            b3.GanzeStadt();
            await BildAsync(b3);
            if (k.TuerFuer != s)
            {
                k.TuerSetzenStarten(s, still: true);
            }
            b3.TuerTippenFuerTests(Bild(a, ost.X + 0.1, ost.Y));
            await a.DreiFertig;
            if (!AufWand(p, s, ost))
            {
                Fund("Tuer setzen 3D: Tipp auf die Ostwand legt die Tuer nicht dorthin");
            }
            await AlleGroessenAsync(a, f, "Tuer setzen 3D");
            await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        }
        k.TuerSetzenBeenden();
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        await SchraegeTuerAsync(a, f, s);
    }

    /// <summary>
    /// S 0.49.0 (Weisung P12-231 Antwort 3b): das Haus um 30 Grad gedreht, Tuer setzen per Tipp vor die gedrehte Westwand:
    /// die Tuer liegt auf dieser Wand, der Weg geht gerade (senkrecht zur Wand) hinaus. Gegenprobe alte Regel: Knopf grau.
    /// </summary>
    private static async Task SchraegeTuerAsync(Hauptansicht a, Window? f, AussenStelle s)
    {
        var p = a.Projekt!;
        var k = a.Karte;
        p.AussenAendern("Drehen", aa => AussenBearbeitung.Drehen(aa, s, 30));
        await a.AussenGeprueft;
        var lage = AussenBearbeitung.HausLage(p.AussenStand, s);
        if (!lage.Aktiv)
        {
            Fund("Tuer schraeg: Haus ist nach dem Drehen um 30 Grad nicht schraeg");
            return;
        }
        k.Waehlen(s);
        await BildAsync(a);
        if (TuerSetzen.Nicht(p.AussenStand, s) is { } grund)
        {
            Fund("Tuer schraeg: Tuer setzen gesperrt: " + grund);
            return;
        }
        k.TuerSetzenStarten(s, still: true);
        await k.FassadeBereit;
        var (vx, vy, _, vh) = AussenBearbeitung.HausViertel(p.AussenStand, s);
        var westAussen = lage.Richtung(new Vector2(-1, 0));
        var tipp = lage.Punkt(new Vector2((float)vx - 0.15f, (float)(vy + (vh / 2))));
        var schritte = p.SchritteZurueck;
        FingerTipp(k, k.Bildpunkt(tipp.X, tipp.Y));
        await a.AussenGeprueft;
        await BildAsync(a);
        if (p.SchritteZurueck != schritte + 1 || TuerSetzen.Tuer(p.AussenStand, s) is not { } t
            || TuerFassade.Rechnen(p, p.AussenStand, s) is not { } fa)
        {
            Fund("Tuer schraeg: Tipp vor die gedrehte Westwand setzt keine Tuer");
            return;
        }
        var w = fa.Waende[fa.WandFuer(t, null)];
        if (Vector2.Dot(w.Aussen, westAussen) < 0.999f || Math.Abs(Vector2.Dot(t - w.A, w.Aussen)) > 0.05f)
        {
            Fund("Tuer schraeg: Tuer liegt nicht auf der gedrehten Westwand (" + t + ")");
        }
        if (Blueprint.ZierwegVon(p.AussenStand, s) is { } z && AussenBearbeitung.Linie(z["weg"]) is { Count: >= 2 } weg
            && Vector2.Dot(Vector2.Normalize(weg[1] - weg[0]), westAussen) < 0.999f)
        {
            Fund("Tuer schraeg: Weg geht nicht gerade (senkrecht zur Wand) aus der Tuer");
        }
        if (a.AussenFunde.FirstOrDefault(x => x.Rot) is { } rot)
        {
            Fund("Tuer schraeg: roter Fund nach dem Setzen: " + rot.Text);
        }
        await AlleGroessenAsync(a, f, "Tuer schraeg");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        k.TuerSetzenBeenden();

        // Gegenprobe: alte Regel sperrt die Tuer am schraegen Haus
        TuerFassade.SchraegAltFuerTests = true;
        try
        {
            if (TuerSetzen.Nicht(p.AussenStand, s) != Texte.TuerSchraegNicht)
            {
                Fund("Tuer schraeg Gegenprobe: alte Regel sperrt die Tuer nicht (Probe taugt nicht)");
            }
        }
        finally
        {
            TuerFassade.SchraegAltFuerTests = false;
        }
        await BildAsync(a);
    }

    /// <summary>Liegt die Tuer des Hauses auf derselben Wand wie <paramref name="wand"/> (bis 0,05 quer)?</summary>
    private static bool AufWand(Projekt p, AussenStelle s, Vector2 wand)
    {
        if (TuerSetzen.Tuer(p.AussenStand, s) is not { } t)
        {
            return false;
        }
        var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        // erwartete Seite aus der Wand des Daten-Rechtecks
        var aussen = Math.Abs(wand.X - hx) < 1e-3 ? new Vector2(-1, 0) : Math.Abs(wand.X - (hx + hw)) < 1e-3 ? new Vector2(1, 0)
            : Math.Abs(wand.Y - hy) < 1e-3 ? new Vector2(0, -1) : new Vector2(0, 1);
        // S 0.33.0 (Weisung P12-148, Nico 1a): die Tuer liegt auf der GEZEICHNETEN Wand dieser Seite (sie kann gegenueber dem
        // Rechteck der Daten eingerueckt sein), also auf einer Wand der Fassade mit dieser Richtung hinaus
        if (!TuerFassade.AltFuerTests && TuerFassade.Rechnen(p, p.AussenStand, s) is { } f)
        {
            var w = f.Waende[f.WandFuer(t, null)];
            var quer = Vector2.Dot(t - w.A, w.Aussen);
            return Vector2.Distance(w.Aussen, aussen) < 1e-3 && Math.Abs(quer) <= 0.05;
        }
        var waagrecht = Math.Abs(wand.Y - hy) < 1e-3 || Math.Abs(wand.Y - (hy + hh)) < 1e-3;
        return waagrecht ? Math.Abs(t.Y - wand.Y) <= 0.05 : Math.Abs(t.X - wand.X) <= 0.05;
    }
}
