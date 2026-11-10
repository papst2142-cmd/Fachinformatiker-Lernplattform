using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Lernplattform.Core.Daten;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle W-B S 0.30.0 (Weisung P12-136): Mehrfachauswahl und Reihe in einer leeren Stadt mit einer Strasse (2; 8)–(30; 8)
/// und drei Baeumen bei y = 12.
/// <list type="bullet">
/// <item>Maus: Umschalt+Klick nimmt dazu (Gegenprobe: Klick ohne Umschalt waehlt nur ein Stueck), Umschalt+Ziehen ueber freie
/// Flaeche waehlt im Rahmen.</item>
/// <item>Finger mit „Mehrere wählen“: Tipp dazu, Tipp heraus; ein Finger zieht die Gruppe = ein Schritt (Messpunkt „Mehrere
/// verschieben (bis zum Bild)“, wiederholbar); Alle drehen, Alle kopieren, Alle löschen je ein Schritt.</item>
/// <item>3D: Rahmen mit dem Finger, Gruppe mit dem Finger ziehen = ein Schritt.</item>
/// <item>Reihe: Busch als Reihe, Tipp Anfang, Tipp Ende = ein Schritt (Messpunkte „Reihe setzen (bis zum Bild)“ und „3D Reihe
/// setzen (bis zum Bild)“); ueber die Strasse ausgelassene rote Stellen; Gegenprobe ohne Schalter = ein Stueck.</item>
/// <item>Layout PC und Tablet, hell und dunkel: „Mehrfachauswahl“, „Mehrfachauswahl 3D“, „Reihe Vorschau“, „Reihe 3D“.</item>
/// </list>
/// </summary>
public static partial class StadtbauMessung
{
    private const string ReiheBusch = "nature/plant_bush";

    private static void MausMit(Control c, Point von, Point bis, KeyModifiers mod)
    {
        var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        c.RaiseEvent(new PointerPressedEventArgs(c, maus, Wurzel(c), c.TranslatePoint(von, Wurzel(c)) ?? von, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), mod));
        for (var i = 1; i <= 8 && von != bis; i++)
        {
            var q = von + ((bis - von) * (i / 8.0));
            c.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, c, maus, Wurzel(c), c.TranslatePoint(q, Wurzel(c)) ?? q, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), mod));
        }
        c.RaiseEvent(new PointerReleasedEventArgs(c, maus, Wurzel(c), c.TranslatePoint(bis, Wurzel(c)) ?? bis, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), mod, MouseButton.Left));
    }

    private static async Task MehrfachAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Mehrfach", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        if (!a.MehrereKnopf.IsEffectivelyVisible || a.KlicksBis(a.MehrereKnopf) is not 0)
        {
            Fund("Mehrfach: Knopf \"" + Texte.MehrereWaehlen + "\" nicht sichtbar oben in der Draufsicht");
            return;
        }
        p.AussenAendern("Straße", aa => Aussen.Landschaft(aa).Add(new PyObjekt
        {
            { "typ", "strasse" },
            { "punkte", new List<object?> { new List<object?> { 2.0, 8.0 }, new List<object?> { 30.0, 8.0 } } },
            { "gehweg", 0.35 },
        }));
        var baeume = new[] { 6.0, 8.0, 10.0 }.Select(x => p.AussenAendern("Baum", aa => AussenBearbeitung.Setzen(aa, AussenArt.Baum, x, 12))!).ToList();
        await a.AussenGeprueft;
        k.Einpassen();
        await BildAsync(a);
        Point B(double x, double y) => k.BildPunktFuerTests(x, y);

        // ---- Maus: Umschalt+Klick, Gegenprobe Klick ohne Umschalt ----
        MausMit(k, B(6, 12), B(6, 12), KeyModifiers.None);
        MausMit(k, B(8, 12), B(8, 12), KeyModifiers.Shift);
        if (!k.GruppeAktiv || k.Gruppe.Count != 2)
        {
            Fund("Mehrfach: Umschalt+Klick nimmt nicht dazu (" + k.Gewaehlt.Count.ToString(Inv) + " gewaehlt)");
        }
        MausMit(k, B(10, 12), B(10, 12), KeyModifiers.None);
        if (k.GruppeAktiv || k.Auswahl != baeume[2])
        {
            Fund("Mehrfach Gegenprobe: ein Klick ohne Umschalt waehlt nicht nur das eine Stueck");
        }
        // Umschalt+Ziehen ueber freie Flaeche: Rahmen um alle drei Baeume
        MausMit(k, B(5, 10.5), B(11, 13.5), KeyModifiers.Shift);
        if (k.Gruppe.Count != 3 || !a.AussenAuswahlTexte.Contains(Texte.GruppeGewaehlt(3)))
        {
            Fund("Mehrfach: Umschalt+Rahmen waehlt nicht alle drei Baeume oder die Karte rechts nennt sie nicht");
        }
        await AlleGroessenAsync(a, f, "Mehrfachauswahl");
        k.Taste(Key.Escape, KeyModifiers.None);

        // ---- Finger mit „Mehrere wählen“ ----
        a.MehrereKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!k.MehrereWaehlen)
        {
            Fund("Mehrfach: Knopf schaltet „Mehrere wählen“ nicht an");
        }
        FingerTipp(k, B(6, 12));
        FingerTipp(k, B(8, 12));
        FingerTipp(k, B(10, 12));
        FingerTipp(k, B(10, 12));
        if (k.Gruppe.Count != 2)
        {
            Fund("Mehrfach Finger: Tipp nimmt nicht dazu bzw. wieder heraus (" + k.Gewaehlt.Count.ToString(Inv) + " gewaehlt)");
        }
        FingerZug(k, B(4, 14), B(12, 10.5));
        if (k.Gruppe.Count != 3)
        {
            Fund("Mehrfach Finger: Rahmen mit einem Finger waehlt nicht alle drei Baeume");
        }
        var gruppe = k.Gruppe.ToList();
        var schritte = p.SchritteZurueck;
        await MessenAsync("Mehrere verschieben (bis zum Bild)", a, () =>
        {
            FingerZug(k, B(8, 12), B(8, 14));
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            k.GruppeSetzen(gruppe);
            return Task.CompletedTask;
        });
        if (p.SchritteZurueck != schritte + 1 || baeume.Any(b => Math.Abs(AussenBearbeitung.Lage(p.AussenStand, b).Y - 14) > 0.001))
        {
            Fund("Mehrfach: Gruppe ziehen ist nicht genau ein Schritt oder nicht alle drei 2 weiter");
        }
        a.Rueckgaengig();
        if (baeume.Any(b => Math.Abs(AussenBearbeitung.Lage(p.AussenStand, b).Y - 12) > 0.001))
        {
            Fund("Mehrfach: ein Rueckgaengig stellt nicht alle drei wieder her");
        }
        k.GruppeSetzen(gruppe);
        await BildAsync(a);
        foreach (var (knopf, name, soll) in new[] { (a.GruppeDrehenKnopf, "drehen", 3), (a.GruppeKopierenKnopf, "kopieren", 6), (a.GruppeLoeschenKnopf, "löschen", 3) })
        {
            schritte = p.SchritteZurueck;
            if (knopf is null)
            {
                Fund("Mehrfach: Knopf \"Alle " + name + "\" fehlt");
                continue;
            }
            knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await BildAsync(a);
            var zahl = AussenBearbeitung.Alle(p.AussenStand).Count(z => z.Stelle.Art == AussenArt.Baum);
            if (p.SchritteZurueck != schritte + 1 || zahl != (name == "löschen" ? 0 : soll))
            {
                Fund("Mehrfach: \"Alle " + name + "\" ist nicht genau ein Schritt (" + zahl.ToString(Inv) + " Baeume)");
            }
            a.Rueckgaengig();
            k.GruppeSetzen(gruppe);
        }
        a.MehrereKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // ---- 3D ----
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (a.Drei.GlBereit && a.Drei.AktuelleKamera is not null)
        {
            b3.GanzeStadt();
            await BildAsync(b3);
            k.Waehlen(null);
            a.MehrereKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            FingerZug(b3, Bild(a, 4, 10), Bild(a, 12, 14));
            if (k.Gruppe.Count != 3)
            {
                Fund("Mehrfach 3D: Rahmen mit dem Finger waehlt " + k.Gewaehlt.Count.ToString(Inv) + " statt 3 Baeume");
            }
            await AlleGroessenAsync(a, f, "Mehrfachauswahl 3D");
            schritte = p.SchritteZurueck;
            FingerZug(b3, Bild(a, 8, 12), Bild(a, 8, 15));
            if (p.SchritteZurueck != schritte + 1 || baeume.Any(b => AussenBearbeitung.Lage(p.AussenStand, b).Y < 13.9))
            {
                Fund("Mehrfach 3D: Gruppe mit dem Finger ziehen ist nicht genau ein Schritt");
            }
            a.Rueckgaengig();
            a.MehrereKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            k.Waehlen(null);
        }
        else
        {
            Messwerte.Notiere("Mehrfach 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);

        // ---- Reihe ----
        int Buesche() => AussenBearbeitung.Alle(p.AussenStand).Count(z => z.Stelle.Art == AussenArt.Katalog);
        a.AussenSetzenWaehlenFuerTests(AussenArt.Katalog, ReiheBusch);
        if (a.ReiheSchalter is null)
        {
            Fund("Reihe: Schalter \"" + Texte.ReiheSetzen + "\" fehlt beim Setzen eines Katalog-Stuecks");
            return;
        }
        // Gegenprobe: ohne Schalter setzt ein Tipp genau ein Stueck
        FingerTipp(k, B(4, 16));
        if (Buesche() != 1)
        {
            Fund("Reihe Gegenprobe: ohne Schalter nicht genau ein Stueck gesetzt");
        }
        a.Rueckgaengig();
        a.AussenSetzenWaehlenFuerTests(AussenArt.Katalog, ReiheBusch);
        a.ReiheSchalter!.IsChecked = true;
        schritte = p.SchritteZurueck;
        var gesetzt = 0;
        await MessenAsync("Reihe setzen (bis zum Bild)", a, () =>
        {
            FingerTipp(k, B(4, 16));
            FingerTipp(k, B(12, 16));
            gesetzt = Buesche();
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        if (p.SchritteZurueck != schritte + 1 || gesetzt < 5 || k.Gruppe.Count != gesetzt)
        {
            Fund("Reihe: nicht genau ein Schritt oder die neuen Stuecke nicht gewaehlt (" + gesetzt.ToString(Inv) + ")");
        }
        a.Rueckgaengig();
        // Vorschau quer ueber die Strasse: rote Stellen, Infozeile mit Dreiecken
        k.ReiheAnfangSetzen(20, 4);
        k.ReiheZielSetzen(20, 12);
        await BildAsync(a);
        if (k.ReiheVorschau is not { Ausgelassen: > 0, Frei: > 0 } || !a.ReiheInfoText.Contains("Dreiecke", StringComparison.Ordinal))
        {
            Fund("Reihe: Vorschau ueber die Strasse ohne rote Stellen oder ohne \"+n Dreiecke\" (\"" + a.ReiheInfoText + "\")");
        }
        await AlleGroessenAsync(a, f, "Reihe Vorschau");
        schritte = p.SchritteZurueck;
        var vorschau = k.ReiheVorschau!;
        k.ReiheBis(20, 12);
        if (p.SchritteZurueck != schritte + 1 || Buesche() != vorschau.Frei)
        {
            Fund("Reihe: gesetzt " + Buesche().ToString(Inv) + " statt " + vorschau.Frei.ToString(Inv) + " (Vorschau = Schritt)");
        }
        a.Rueckgaengig();

        // ---- Reihe in 3D ----
        a.DreiZeigen();
        await a.DreiFertig;
        if (a.Drei.GlBereit && a.Drei.AktuelleKamera is not null)
        {
            b3.GanzeStadt();
            await BildAsync(b3);
            if (!k.ReiheAktiv)
            {
                a.AussenSetzenWaehlenFuerTests(AussenArt.Katalog, ReiheBusch);
                a.ReiheSchalter!.IsChecked = true;
            }
            schritte = p.SchritteZurueck;
            await MessenAsync("3D Reihe setzen (bis zum Bild)", a, () =>
            {
                b3.ReiheTippenFuerTests(Bild(a, 4, 16));
                b3.ReiheTippenFuerTests(Bild(a, 12, 16));
                gesetzt = Buesche();
                return Task.CompletedTask;
            }, () =>
            {
                a.Rueckgaengig();
                return Task.CompletedTask;
            });
            if (p.SchritteZurueck != schritte + 1 || gesetzt < 5)
            {
                Fund("Reihe 3D: nicht genau ein Schritt (" + gesetzt.ToString(Inv) + " gesetzt)");
            }
            a.Rueckgaengig();
            b3.ReiheTippenFuerTests(Bild(a, 20, 4));
            b3.ReiheSchwebenFuerTests(Bild(a, 20, 12));
            await BildAsync(b3);
            if (b3.ReiheRahmen.Width < 1)
            {
                Fund("Reihe 3D: keine Vorschau am Boden");
            }
            await AlleGroessenAsync(a, f, "Reihe 3D");
            k.ReiheAbbrechen();
        }
        k.ReiheAn = false;
        a.SetzenBeenden();
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }
}
