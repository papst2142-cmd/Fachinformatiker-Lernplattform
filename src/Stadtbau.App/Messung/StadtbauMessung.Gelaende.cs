using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.42.0 (Weisung P12-192): Gelaende in einer leeren Stadt 32 × 20 mit einer Strasse (2; 3)–(30; 3). Knopf „Hügel“
/// in „Natur und Licht“ setzen bis zum Bild (Messpunkt „Huegel setzen (bis zum Bild)“, wiederholbar): ohne roten Fund, die
/// Draufsicht zeichnet Hoehenlinien mit Beschriftung (Ebene „Gelände“ aus: keine, Gegenprobe). Ein Hügel an der Strasse ist
/// rot (Gegenprobe Rand 1,5). Pinsel „Heben“ mit einem Finger ueber die Wiese (Messpunkt „Pinselzug (bis zum Bild)“,
/// wiederholbar): ein Schritt im Verlauf; ein Zug „Senken“ auf flacher Wiese aendert nichts und ist kein Schritt (Gegenprobe).
/// In 3D mit einem Tipp des Fingers (Messpunkt „3D Huegel setzen (bis zum Bild)“). Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task GelaendeAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Gelaende", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        if (a.HuegelKnopf is not { } huegelKnopf || a.KlicksBis(huegelKnopf) is not (0 or 1) || a.PinselKnoepfe.Count != 3)
        {
            Fund("Gelaende: der Knopf „Hügel“ oder der Pinsel fehlt links");
            return;
        }
        p.AussenAendern("Straße", aa => Aussen.Landschaft(aa).Add(new PyObjekt
        {
            { "typ", "strasse" },
            { "punkte", new List<object?> { new List<object?> { 2.0, 3.0 }, new List<object?> { 30.0, 3.0 } } },
            { "gehweg", 0.35 },
        }));
        await a.AussenGeprueft;

        await MessenAsync("Huegel setzen (bis zum Bild)", k, async () =>
        {
            HuegelWaehlen(a, huegelKnopf);
            if (k.SetzenArt != AussenArt.Huegel || k.SetzenAn(16, 12) is null)
            {
                Fund("Huegel: nicht gesetzt");
            }
            await BildAsync(k);
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        a.Rueckgaengig(); // der gemessene wieder weg (Zuruecksetzen gilt nur zwischen den Versuchen)
        HuegelWaehlen(a, huegelKnopf);
        var s = k.SetzenAn(16, 12);
        k.SetzenBeenden();
        await a.AussenGeprueft;
        await BildAsync(k);
        if (s is null || AussenBearbeitung.HuegelListe(p.AussenStand).Count != 1)
        {
            Fund("Huegel: nicht genau einer gesetzt");
            return;
        }
        if (a.AussenFunde.Any(x => x.Rot))
        {
            Fund("Huegel: frisch gesetzt rot: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot).Select(x => x.Text)));
        }
        if (k.HoehenlinienGezeichnet == 0 || k.HoehenSchilder.Count == 0)
        {
            Fund(string.Create(Inv, $"Huegel: Draufsicht ohne Hoehenlinien ({k.HoehenlinienGezeichnet}) oder ohne Beschriftung ({k.HoehenSchilder.Count})"));
        }
        // Gegenprobe Ebene „Gelände“ aus: keine Hoehenlinien
        var ebenen = k.EbenenAus;
        k.EbenenAus = new HashSet<Ebene>(ebenen) { Ebene.Gelaende };
        await BildAsync(k);
        if (k.HoehenlinienGezeichnet != 0)
        {
            Fund("Huegel: Ebene „Gelände“ aus, trotzdem Hoehenlinien");
        }
        k.EbenenAus = ebenen;
        k.Waehlen(s);
        await AlleGroessenAsync(a, f, "Huegel gewaehlt");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        if (a.HuegelKnoepfe is not { } hk)
        {
            Fund("Huegel: Karte rechts ohne Radius und Hoehe");
        }
        else
        {
            hk.HoeheMehr.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await a.AussenGeprueft;
            if (Math.Abs(AussenBearbeitung.HuegelWerte(p.AussenStand, s).Hoehe - (AussenBearbeitung.HuegelHoeheNeu + 0.1)) > 0.001)
            {
                Fund(string.Create(Inv, $"Huegel: „+“ der Hoehe gab {AussenBearbeitung.HuegelWerte(p.AussenStand, s).Hoehe}"));
            }
            a.Rueckgaengig();
        }
        // Gegenprobe Rand: an die Strasse geschoben ist der Huegel rot
        k.Waehlen(s);
        k.Verschieben(0, -7);
        await a.AussenGeprueft;
        if (!a.AussenFunde.Any(x => x.Rot && x.Text.Contains("einer Straße", StringComparison.Ordinal)))
        {
            Fund("Huegel: an der Strasse nicht rot (Gegenprobe Rand 1,5)");
        }
        a.Rueckgaengig();
        await a.AussenGeprueft;

        // Pinsel: ein Finger zieht einen Zug ueber die Wiese, ein Schritt im Verlauf
        a.PinselWaehlen(PinselArt.Heben);
        if (k.GelaendePinsel != PinselArt.Heben)
        {
            Fund("Pinsel: „Heben“ nicht gewaehlt");
            return;
        }
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        await MessenAsync("Pinselzug (bis zum Bild)", k, async () =>
        {
            var schritte = p.SchritteZurueck;
            Druck(k, finger, k.Bildpunkt(6, 12));
            for (var x = 6.5; x <= 10; x += 0.5)
            {
                Bewegung(k, finger, k.Bildpunkt(x, 12));
            }
            Los(k, finger, k.Bildpunkt(10, 12));
            await BildAsync(k);
            if (p.SchritteZurueck != schritte + 1)
            {
                Fund(string.Create(Inv, $"Pinsel: ein Zug gab {p.SchritteZurueck - schritte} Schritte statt 1"));
            }
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        if (AussenBearbeitung.GemaltListe(p.AussenStand).Count != 0)
        {
            a.Rueckgaengig();
        }
        // ein Zug, der bleibt (fuer das Layout), dann Gegenprobe „Senken“ auf flacher Wiese: nichts, kein Schritt
        k.PinselZugAnwenden([new(6, 12), new(10, 12)]);
        a.PinselWaehlen(PinselArt.Senken);
        var vorher = p.SchritteZurueck;
        if (k.PinselZugAnwenden([new(25, 15), new(27, 15)]) || p.SchritteZurueck != vorher)
        {
            Fund("Pinsel: „Senken“ auf flacher Wiese hat einen Schritt gemacht (Gegenprobe)");
        }
        a.PinselWaehlen(PinselArt.Heben);
        await AlleGroessenAsync(a, f, "Pinsel");
        a.PinselWaehlen(PinselArt.Heben); // noch einmal = aus
        if (k.GelaendePinsel is not null)
        {
            Fund("Pinsel: zweites Antippen beendet ihn nicht");
        }
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // 3D: den Huegel weg, dann mit einem Tipp des Fingers setzen bis zum Bild
        p.AussenAendern("Hügel", aa => AussenBearbeitung.Loeschen(aa, new AussenStelle(AussenArt.Huegel, 0)));
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Huegel 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b3.GanzeStadt();
        await BildAsync(b3);
        var finger3 = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        await MessenAsync("3D Huegel setzen (bis zum Bild)", b3, async () =>
        {
            HuegelWaehlen(a, huegelKnopf);
            var bild = Bild(a, 16, 12);
            Druck(b3, finger3, bild);
            Los(b3, finger3, bild);
            await a.DreiFertig;
            if (AussenBearbeitung.HuegelListe(p.AussenStand).Count != 1)
            {
                Fund("Huegel 3D: ein Tipp hat keinen Huegel gesetzt");
            }
        }, async () =>
        {
            a.Rueckgaengig();
            await a.DreiFertig;
        });
        await AlleGroessenAsync(a, f, "Huegel 3D");
        a.Karte.SetzenBeenden();
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    private static void HuegelWaehlen(Hauptansicht a, Button knopf)
    {
        if (a.Karte.SetzenArt != AussenArt.Huegel || !ReferenceEquals(a.SetzenKnopfMarkiert, knopf))
        {
            a.SetzenKnopfDrueckenFuerTests(knopf);
        }
    }
}
