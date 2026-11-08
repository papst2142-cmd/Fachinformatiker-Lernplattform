using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.22.0 „Spielgeschichte ansehen“ (Weisungen P12-91/P12-92): Zahl am Haus (nur Aufgaben, in der Karte), gelbe Zeile und
/// Karte rechts beim Ort mit Aufgaben (Gegenprobe Ort ohne Spielgeschichte), Messpunkt „Spielgeschichte oeffnen“ (einmalig,
/// 400 ms), Zusammenhaenge zugeklappt, je Ort aufgeklappt und alle aufgeklappt: keine Linie kreuzt eine andere oder laeuft
/// durch einen Kasten (Gegenprobe gerade Linien), kein Text in einem Kasten abgeschnitten (Gegenprobe Reiter mit altem
/// Innenabstand), „Alles zeigen“ zeigt alles und die Kamera-Karte verdeckt keinen Kasten; Kasten antippen = rechts die
/// Spielgeschichte des Orts; Liste mit Filtern und hervorgehobenem Ort. Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task SpielgeschichteAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Spielgeschichte", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var g = a.Geschichte;
        if (g is null)
        {
            Fund("Spielgeschichte: keine Zuordnung zum Projekt");
            return;
        }

        // Bild 1: Zahl am Haus nur an den Orten mit Aufgaben, ganz in der Karte
        await BildAsync(a);
        var zahlen = a.Karte.ZahlenAmHaus;
        var soll = g.Zahlen();
        if (zahlen.Count != soll.Count || zahlen.Any(x => !soll.TryGetValue(x.Ort, out var n) || n != x.Zahl))
        {
            Fund("Spielgeschichte: Zahlen am Haus " + string.Join(", ", zahlen.Select(x => x.Ort + "=" + x.Zahl)) + " statt "
                + string.Join(", ", soll.Select(x => x.Key + "=" + x.Value)));
        }
        var karte = new Rect(a.Karte.Bounds.Size);
        foreach (var zz in zahlen.Where(x => !karte.Contains(x.Flaeche)))
        {
            Fund("Spielgeschichte: Zahl am Haus " + zz.Ort + " ragt aus der Karte");
        }

        // Bild 4 (4a): gelbe Zeile und Karte rechts; Gegenprobe Ort ohne Spielgeschichte
        a.Karte.Waehlen(OrtStelle("talheim_nord"));
        await BildAsync(a);
        if (a.GeschichteGelbText != Texte.GeschichteGelb(6) || a.GeschichteOrtKarte?.IsVisible != true
            || !g.AnOrt("talheim_nord", Spielgeschichte.Art.Aufgabe).All(e => a.GeschichteOrtTexte.Contains(e.Titel)))
        {
            Fund("Spielgeschichte: Talheim-Nord gewaehlt ohne gelbe Zeile oder ohne die Aufgaben rechts");
        }
        await AlleGroessenAsync(a, f, "Spielgeschichte Ort gewaehlt");
        var ohne = AussenBearbeitung.Orte(a.Projekt!.AussenStand).Select(o => o["id"] as string).OfType<string>().FirstOrDefault(o => !g.HatEtwas(o));
        if (ohne is null)
        {
            Fund("Spielgeschichte: kein Ort ohne Spielgeschichte fuer die Gegenprobe");
        }
        else
        {
            a.Karte.Waehlen(OrtStelle(ohne));
            await BildAsync(a);
            if (a.GeschichteGelbText is not null || a.GeschichteOrtKarte?.IsVisible == true)
            {
                Fund("Spielgeschichte: Ort ohne Spielgeschichte zeigt gelbe Zeile oder Karte (Gegenprobe)");
            }
        }
        a.Karte.Waehlen(null);

        // Messpunkt: Reiter oeffnen bis zum Bild (erstes Mal, baut die Ansicht)
        await MessenAsync("Spielgeschichte oeffnen", a, () =>
        {
            a.GeschichteZeigen();
            return Task.CompletedTask;
        }, einmalig: true);
        var z = a.Zusammenhang;
        if (!a.GeschichteOffen || a.GeschichteListeOffen || z.Plan is null)
        {
            Fund("Spielgeschichte: der Reiter zeigt nicht die Zusammenhaenge");
            return;
        }

        // Linien in jedem Zustand: zu, je Ort auf, alle auf
        async Task LinienPruefen(string wann)
        {
            await BildAsync(a);
            var p = z.Plan!;
            foreach (var k in p.Kreuzungen())
            {
                Fund("Spielgeschichte " + wann + ": Linien kreuzen sich: " + k);
            }
            foreach (var k in p.DurchKaesten())
            {
                Fund("Spielgeschichte " + wann + ": Linie durch einen Kasten: " + k);
            }
        }
        z.OffenSetzen([]);
        await LinienPruefen("zu");
        foreach (var ort in g.Zahlen().Keys.Order(StringComparer.Ordinal))
        {
            z.OffenSetzen([ort]);
            await LinienPruefen(ort + " auf");
        }
        z.OffenSetzen(g.Zahlen().Keys);
        await LinienPruefen("alle auf");
        // Gegenprobe: gerade Linien wie ein einfacher Plan kreuzen sich oder laufen durch Kaesten
        z.AltFuerTests = true;
        z.OffenSetzen([]);
        await BildAsync(a);
        if (z.Plan!.Kreuzungen().Count + z.Plan.DurchKaesten().Count == 0)
        {
            Fund("Spielgeschichte: die Linien-Pruefung findet bei geraden Linien nichts (Gegenprobe)");
        }
        z.AltFuerTests = false;

        // Texte in den Kaesten nie abgeschnitten (jede Groesse); Gegenprobe Reiter mit altem Innenabstand
        async Task TexteGanz(string wann)
        {
            foreach (var (name, b, h) in Groessen)
            {
                await GroesseAsync(a, f, b, h);
                foreach (var t in z.KastenTexte.Select(Abgeschnitten).OfType<string>().Distinct())
                {
                    Fund("Spielgeschichte " + wann + " " + name + ": Text im Kasten abgeschnitten: " + t);
                }
                z.Einpassen();
                await BildAsync(a);
                if (!z.GanzSichtbar)
                {
                    Fund("Spielgeschichte " + wann + " " + name + ": „Alles zeigen“ zeigt nicht alles");
                }
                foreach (var o in z.UnterDerKamerakarte())
                {
                    Fund("Spielgeschichte " + wann + " " + name + ": Kamera-Karte verdeckt den Kasten " + o);
                }
            }
            await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        }
        z.OffenSetzen([]);
        await TexteGanz("zu");
        ZusammenhangAnsicht.ReiterAltFuerTests = true;
        z.OffenSetzen([]);
        await BildAsync(a);
        if (!z.KastenTexte.Any(t => Abgeschnitten(t) is not null))
        {
            Fund("Spielgeschichte: die Pruefung abgeschnittener Texte findet beim alten Reiter nichts (Gegenprobe)");
        }
        ZusammenhangAnsicht.ReiterAltFuerTests = false;
        z.OffenSetzen([]);
        await AlleGroessenAsync(a, f, "Spielgeschichte Zusammenhaenge zu");

        // „Alle zeigen“ und „Weniger zeigen“ am Kasten, Kasten antippen = rechts die Spielgeschichte des Orts
        z.ZeigenKnoepfe["talheim_nord"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        if (!z.Offen.Contains("talheim_nord") || z.Plan!.Kaesten.First(k => k.Ort == "talheim_nord").Zeilen.Count != 6)
        {
            Fund("Spielgeschichte: „Alle zeigen“ klappt Talheim-Nord nicht mit 6 Aufgaben auf");
        }
        z.Koepfe["talheim_nord"].Klick?.Invoke();
        await BildAsync(a);
        if (a.GeschichteSeiteRechts != (true, "talheim_nord")
            || !g.AnOrt("talheim_nord", Spielgeschichte.Art.Aufgabe).All(e => a.GeschichteSeiteTexte.Contains(e.Titel)))
        {
            Fund("Spielgeschichte: Kasten Talheim-Nord angetippt, rechts nicht seine Spielgeschichte");
        }
        await TexteGanz("Talheim-Nord auf");
        await AlleGroessenAsync(a, f, "Spielgeschichte Talheim-Nord auf");
        z.ZeigenKnoepfe["talheim_nord"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        if (z.Offen.Contains("talheim_nord"))
        {
            Fund("Spielgeschichte: „Weniger zeigen“ klappt Talheim-Nord nicht zu");
        }
        z.AlleUmschalten();
        await TexteGanz("alle auf");
        await AlleGroessenAsync(a, f, "Spielgeschichte alle auf");
        z.AlleUmschalten();
        if (z.Offen.Count != 0)
        {
            Fund("Spielgeschichte: „Alle zuklappen“ laesst Orte offen");
        }

        // Liste „Aufgabe → Ort“ (Bild 2): alle Aufgaben; ueber „Alle in der Spielgeschichte“ der Ort hervorgehoben
        a.GeschichteListeUmschalten(true);
        await BildAsync(a);
        if (a.GeschichteListe.Count != g.Aufgaben.Count)
        {
            Fund("Spielgeschichte: Liste mit " + a.GeschichteListe.Count + " statt " + g.Aufgaben.Count + " Aufgaben");
        }
        a.GeschichteFilter.Suche.Text = "Talheim-Nord";
        await BildAsync(a);
        if (a.GeschichteListe.Count == 0 || a.GeschichteListe.Count == g.Aufgaben.Count)
        {
            Fund("Spielgeschichte: Suche „Talheim-Nord“ filtert nicht");
        }
        a.GeschichteFilter.Suche.Text = "";
        await AlleGroessenAsync(a, f, "Spielgeschichte Liste");
        a.GeschichteZeigen("lindenau");
        await BildAsync(a);
        if (!a.GeschichteListeOffen || a.GeschichteListe.Count(x => x.Hervor) != g.Zahl("lindenau")
            || a.GeschichteListe.Count != g.Liste(abTag: g.FreiAbTag("lindenau")).Count)
        {
            Fund("Spielgeschichte: „Alle in der Spielgeschichte“ fuer Lindenau zeigt nicht ab Tag " + g.FreiAbTag("lindenau") + " mit hervorgehobenen Aufgaben");
        }
        await AlleGroessenAsync(a, f, "Spielgeschichte Liste hervorgehoben");
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        if (a.GeschichteOffen)
        {
            Fund("Spielgeschichte: „Stadt von oben“ schliesst den Reiter nicht");
        }
    }

    /// <summary>Ist der Text abgeschnitten (zu hoch fuer seinen Platz oder ein Wort getrennt)? null = ganz.</summary>
    private static string? Abgeschnitten(TextBlock t)
    {
        var text = t.Text ?? "";
        var w = t.Bounds.Width;
        if (text.Length == 0)
        {
            return null;
        }
        if (w <= 0 || !t.IsEffectivelyVisible)
        {
            return text + " (nicht gezeigt)";
        }
        TextBlock Probe(string s, TextWrapping umbruch) => new()
        {
            Text = s,
            FontFamily = t.FontFamily,
            FontSize = t.FontSize,
            FontWeight = t.FontWeight,
            TextWrapping = umbruch,
        };
        var ganz = Probe(text, t.TextWrapping);
        ganz.Measure(new Size(w, double.PositiveInfinity));
        if (ganz.DesiredSize.Height > t.Bounds.Height + 0.5)
        {
            return text + " (zu hoch)";
        }
        foreach (var wort in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = Probe(wort, TextWrapping.NoWrap);
            p.Measure(Size.Infinity);
            if (p.DesiredSize.Width > w + 0.01)
            {
                return text + " (Wort „" + wort + "“ getrennt)";
            }
        }
        return null;
    }
}
