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
/// Welle S 0.27.0 (Weisung P12-118 Teil B): Massband, Ebenen ein/aus und Notizen in einer leeren Stadt mit einer Strasse
/// (2; 8)–(30; 8), einem Haus und einem Baum. Die Knoepfe stehen oben links in der Werkzeugleiste.
/// <list type="bullet">
/// <item>Massband mit dem Finger: Tipp nahe der einen Hausecke, Tipp nahe der anderen = genau die Breite des Hauses, kein Schritt
/// im Verlauf, keine Auswahl (Gegenprobe: ohne Massband waehlt derselbe Tipp das Haus). Messpunkt „Massband messen (bis zum
/// Bild)“, wiederholbar. In 3D am Boden mit dem Finger (Schild mit der Laenge).</item>
/// <item>Notiz: setzen (Messpunkt „Notiz setzen (bis zum Bild)“, wiederholbar), Text aendern, verschieben, loeschen je ein
/// Schritt; nie in der Stadt-Datei.</item>
/// <item>Ebenen: Liste oeffnen, „Bäume und Grün“ aus = Baum nicht anwaehlbar (Gegenprobe an), Hinweis „1 Ebene aus“, gemerkt
/// in einstellungen.json, „Alle einblenden“.</item>
/// <item>Layout PC und Tablet, hell und dunkel, je Zustand.</item>
/// </list>
/// </summary>
public static partial class StadtbauMessung
{
    private const string NotizProbeText = "Starttest: hier kommt der Spielplatz hin";

    private static async Task WerkzeugKleinAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Werkzeug klein", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        foreach (var knopf in new[] { a.MassbandKnopf, a.EbenenKnopf, a.NotizKnopf })
        {
            if (!knopf.IsEffectivelyVisible || a.KlicksBis(knopf) is not 0)
            {
                Fund("Werkzeug klein: Knopf \"" + Avalonia.Automation.AutomationProperties.GetName(knopf) + "\" nicht sichtbar oben in der Draufsicht");
                return;
            }
        }
        p.AussenAendern("Straße", aa => Aussen.Landschaft(aa).Add(new PyObjekt
        {
            { "typ", "strasse" },
            { "punkte", new List<object?> { new List<object?> { 2.0, 8.0 }, new List<object?> { 30.0, 8.0 } } },
            { "gehweg", 0.35 },
        }));
        var haus = p.AussenAendern("Haus", aa => AussenBearbeitung.Setzen(aa, AussenArt.Gebaeude, 12, 13, modell: "filiale"))!;
        var baum = p.AussenAendern("Baum", aa => AussenBearbeitung.Setzen(aa, AussenArt.Baum, 22, 14))!;
        await a.AussenGeprueft;
        k.Einpassen();
        await BildAsync(a);
        var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(p.AussenStand, haus);
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        void Tipp(Control c, Point bild)
        {
            Druck(c, finger, bild);
            Los(c, finger, bild);
        }
        // knapp neben den beiden oberen Hausecken (3 Bildpunkte), das Massband rastet genau auf die Ecken
        var ecke1 = k.BildPunktFuerTests(hx, hy) + new Point(3, -3);
        var ecke2 = k.BildPunktFuerTests(hx + hw, hy) + new Point(-3, 2);

        // Gegenprobe: ohne Massband waehlt der Tipp das Haus
        Tipp(k, k.BildPunktFuerTests(hx + (hw / 2), hy + (hh / 2)));
        if (k.Auswahl != haus)
        {
            Fund("Massband Gegenprobe: ohne Massband waehlt der Tipp das Haus nicht");
        }
        k.Waehlen(null);

        // ---- Massband in der Draufsicht ----
        var schritte = p.SchritteZurueck;
        var soll = Massband.Text(hw);
        await MessenAsync("Massband messen (bis zum Bild)", a, () =>
        {
            if (!k.MassbandAn)
            {
                a.MassbandKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            Tipp(k, ecke1);
            Tipp(k, ecke2);
            return Task.CompletedTask;
        }, () =>
        {
            k.MassbandAn = false;
            return Task.CompletedTask;
        });
        await BildAsync(a);
        if (k.MassbandText != soll || k.Massband.Anfang is not { } m1 || Math.Abs(m1.X - hx) > 0.001 || Math.Abs(m1.Y - hy) > 0.001)
        {
            Fund("Massband: \"" + k.MassbandText + "\" statt \"" + soll + "\" oder nicht an der Hausecke eingerastet");
        }
        if (k.Auswahl is not null || p.SchritteZurueck != schritte)
        {
            Fund("Massband: der Tipp hat etwas gewaehlt oder einen Schritt im Verlauf angelegt");
        }
        if (k.MassbandSchild.Width < 1 || !a.AussenAuswahlTexte.Contains(soll))
        {
            Fund("Massband: keine Laenge an der Linie oder rechts in der Karte");
        }
        await AlleGroessenAsync(a, f, "Massband");
        k.Taste(Key.Escape, KeyModifiers.None);
        k.Taste(Key.Escape, KeyModifiers.None);
        if (k.MassbandAn)
        {
            Fund("Massband: Esc beendet es nicht");
        }

        // ---- Massband in 3D am Boden ----
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (a.Drei.GlBereit && a.Drei.AktuelleKamera is not null)
        {
            b3.GanzeStadt();
            await BildAsync(b3);
            a.MassbandKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Tipp(b3, Bild(a, 4, 16));
            Tipp(b3, Bild(a, 9, 16));
            await BildAsync(b3);
            if (k.MassbandText != Massband.Text(5) || b3.MassbandSchild.Width < 1 || p.SchritteZurueck != schritte)
            {
                Fund("Massband 3D: \"" + k.MassbandText + "\" statt \"" + Massband.Text(5) + "\" oder ohne Schild am Boden");
            }
            await AlleGroessenAsync(a, f, "Massband 3D");
            if (a.NotizKnopf.IsEffectivelyVisible || a.EbenenKnopf.IsEffectivelyVisible)
            {
                Fund("3D-Ansicht: Notiz und Ebenen gehoeren nur zur Draufsicht");
            }
            k.MassbandAn = false;
        }
        else
        {
            Messwerte.Notiere("Massband 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);

        // ---- Notizen ----
        var anzahl = AussenBearbeitung.NotizListe(p.AussenStand).Count;
        await MessenAsync("Notiz setzen (bis zum Bild)", a, () =>
        {
            if (k.SetzenArt != AussenArt.Notiz)
            {
                a.SetzenKnopfDrueckenFuerTests(a.NotizKnopf);
            }
            if (k.SetzenArt != AussenArt.Notiz || k.SetzenAn(20, 4) is null)
            {
                Fund("Notiz: nicht gesetzt");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        if (AussenBearbeitung.NotizListe(p.AussenStand).Count != anzahl + 1 || k.Auswahl is not { Art: AussenArt.Notiz } notiz)
        {
            Fund("Notiz: nach dem Setzen nicht genau eine neue, gewaehlte Notiz");
            return;
        }
        schritte = p.SchritteZurueck;
        if (a.NotizFeld is null || a.NotizTextAendern(notiz, NotizProbeText) is not null || p.SchritteZurueck != schritte + 1)
        {
            Fund("Notiz: Text aendern geht nicht oder ist nicht genau ein Schritt");
        }
        await BildAsync(a);
        if (!k.NotizFlaechen.Any(n => n.Text == NotizProbeText && n.Flaeche.Width > 20))
        {
            Fund("Notiz: der Text steht nicht auf der Karte");
        }
        if (a.NotizTextAendern(notiz, " ") != Texte.NotizLeer || p.SchritteZurueck != schritte + 1)
        {
            Fund("Notiz: leerer Text angenommen oder als Schritt gezaehlt");
        }
        await AlleGroessenAsync(a, f, "Notiz gewaehlt");
        k.Waehlen(notiz);
        k.Taste(Key.Right, KeyModifiers.None);
        if (p.SchritteZurueck != schritte + 2 || AussenBearbeitung.Lage(p.AussenStand, notiz).X <= 20)
        {
            Fund("Notiz: verschieben ist nicht genau ein Schritt");
        }
        if (Lernplattform.Core.Daten.PythonJson.Standard(p.Stand().Stadtdaten).Contains(NotizProbeText, StringComparison.Ordinal) || p.AussenGeaendert != true)
        {
            Fund("Notiz: steht in der Stadt-Datei");
        }
        // zum Ansehen bleibt eine Notiz stehen; die Kopie wird geloescht (ein Schritt)
        k.Waehlen(notiz);
        k.Kopieren();
        var kopie = k.Auswahl;
        k.Loeschen();
        if (kopie is not { Art: AussenArt.Notiz } || p.SchritteZurueck != schritte + 4 || AussenBearbeitung.NotizListe(p.AussenStand).Count != anzahl + 1)
        {
            Fund("Notiz: kopieren und loeschen sind nicht je ein Schritt");
        }
        k.Waehlen(null);

        // ---- Ebenen ----
        a.EbenenKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        if (!a.EbenenListe.IsEffectivelyVisible || a.EbenenSchalter.Count != Ebenen.Alle.Count)
        {
            Fund("Ebenen: Liste nicht offen oder nicht alle " + Ebenen.Alle.Count.ToString(Inv) + " Ebenen");
        }
        await AlleGroessenAsync(a, f, "Ebenen Liste");
        var baumBild = k.BildPunktFuerTests(22, 14);
        Tipp(k, baumBild);
        if (k.Auswahl != baum)
        {
            Fund("Ebenen Gegenprobe: der Baum laesst sich mit allen Ebenen nicht waehlen");
        }
        k.Waehlen(null);
        a.EbenenSchalter[Ebene.Gruen].IsChecked = false;
        await BildAsync(a);
        Tipp(k, baumBild);
        if (k.Auswahl is { Art: AussenArt.Baum })
        {
            Fund("Ebenen: ausgeblendeter Baum ist anwaehlbar");
        }
        k.Waehlen(null);
        if (a.EbenenHinweisText != Texte.EbenenAusHinweis(1) || !WerkzeugEinstellungen.EbenenAus(StadtbauApp.Ordner).SetEquals([Ebene.Gruen]))
        {
            Fund("Ebenen: Hinweis \"" + a.EbenenHinweisText + "\" oder nicht in einstellungen.json gemerkt");
        }
        a.EbenenSchalter[Ebene.Notizen].IsChecked = false;
        await BildAsync(a);
        if (k.NotizFlaechen.Count > 0 && k.Zeigt(Ebene.Notizen))
        {
            Fund("Ebenen: Notizen nicht ausgeblendet");
        }
        a.EbenenKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await AlleGroessenAsync(a, f, "Ebenen aus");
        a.EbenenSetzen(new HashSet<Ebene>());
        if (k.EbenenAus.Count != 0 || WerkzeugEinstellungen.EbenenAus(StadtbauApp.Ordner).Count != 0 || a.EbenenHinweisText.Length != 0)
        {
            Fund("Ebenen: „Alle einblenden“ blendet nicht alles ein");
        }
        await BildAsync(a);
    }
}
