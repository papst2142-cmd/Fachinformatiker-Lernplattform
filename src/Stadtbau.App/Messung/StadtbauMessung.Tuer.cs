using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.24.0 (Weisung P12-100, Nico 1b 2a 3a 4a, Sammelliste 245): leere Stadt mit einer Strasse, eine Wohnung davor.
/// Knopf „Tür dazu“ unter „Ausgewähltes Stück“ bis zum Bild (Messpunkt „Tuer dazu (bis zum Bild)“, wiederholbar, 400 ms):
/// Tuer da, Haus an den Gehweg geruckt, Weg bis zur Strasse, kein Fund zur Tuer; Knopf heisst danach „Tür entfernen“.
/// Zweite Wohnung mit Tuer = Kopie „Wohnung 2“; Gegenproben: Wohnung fern der Strasse = gelber Hinweis, „Tür entfernen“
/// nimmt Tuer und Weg weg, Rueckgaengig bringt sie wieder. Layout PC und Tablet, hell und dunkel, mit beiden Knoepfen.
/// </summary>
public static partial class StadtbauMessung
{
    private static Button? TuerKnopfSuchen(Hauptansicht a, string text) =>
        a.KnoepfeMit(text).FirstOrDefault(k => k.IsEffectivelyVisible && k.IsEffectivelyEnabled);

    private static async Task TuerAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Tuer", null, (40, 40));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var s = p.AussenAendern("Wohnung", aa => AussenBearbeitung.Setzen(aa, AussenArt.Gebaeude, 20, 15, raster: true, modell: "wohnung")!);
        var vorher = AussenBearbeitung.HausFlaeche(p.AussenStand, s);
        // Strasse so, dass die Front 0,5 vor der Gehwegkante steht (Fangweite wie bei den Laeden)
        var strasseY = Math.Round(vorher.Y + vorher.H + (StadtSzene.StrassenBreite / 2) + 0.35 + 0.5, 2);
        p.AussenAendern("Strasse", aa => Aussen.Landschaft(aa).Add(new PyObjekt
        {
            { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { 2.0, strasseY }, new List<object?> { 38.0, strasseY } } }, { "gehweg", 0.35 },
        }));
        await a.AussenGeprueft;
        a.Karte.Waehlen(s);
        await BildAsync(a);
        if (TuerKnopfSuchen(a, Texte.TuerDazu) is null)
        {
            Fund("Tuer: Knopf „" + Texte.TuerDazu + "“ fehlt unter „Ausgewähltes Stück“");
            return;
        }
        await AlleGroessenAsync(a, f, "Tuer dazu Knopf");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // Messpunkt: echter Knopfdruck bis zum Bild
        await MessenAsync("Tuer dazu (bis zum Bild)", a, () =>
        {
            a.Karte.Waehlen(s);
            if (TuerKnopfSuchen(a, Texte.TuerDazu) is not { } k)
            {
                Fund("Tuer: Knopf „" + Texte.TuerDazu + "“ fehlt beim Messen");
                return Task.CompletedTask;
            }
            k.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        await a.AussenGeprueft;
        if (!GebaeudeTuer.HatTuer(p.AussenStand, s))
        {
            Fund("Tuer: nach „" + Texte.TuerDazu + "“ keine Tuer");
            return;
        }
        if (!(AussenBearbeitung.HausFlaeche(p.AussenStand, s).Y > vorher.Y))
        {
            Fund("Tuer: das Haus ist nicht an den Gehweg geruckt");
        }
        if (a.AussenFunde.FirstOrDefault(x => x.Text.Contains("Tür", StringComparison.Ordinal) || x.Text.StartsWith(Texte.TuerWegOhneStrasse, StringComparison.Ordinal)) is { } fund)
        {
            Fund("Tuer: Fund nach „" + Texte.TuerDazu + "“: " + fund.Text);
        }
        a.Karte.Waehlen(s);
        await BildAsync(a);
        if (TuerKnopfSuchen(a, Texte.TuerEntfernen) is null)
        {
            Fund("Tuer: nach „" + Texte.TuerDazu + "“ kein Knopf „" + Texte.TuerEntfernen + "“");
        }
        await AlleGroessenAsync(a, f, "Tuer entfernen Knopf");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // zweite Wohnung mit Tuer = Kopie „Wohnung 2“ (Nico 2a)
        var s2 = p.AussenAendern("Wohnung 2", aa => AussenBearbeitung.Setzen(aa, AussenArt.Gebaeude, 30, vorher.Y + (vorher.H / 2), raster: true, modell: "wohnung")!);
        if (a.TuerUmschalten(s2) is null)
        {
            Fund("Tuer: zweite Wohnung ohne Meldung");
        }
        await a.AussenGeprueft;
        if (Blueprint.ZierwegVon(p.AussenStand, s2) is not { } z2 || Py.Str(z2["name"]) != "Wohnung 2")
        {
            Fund("Tuer: zweite Wohnung heisst nicht „Wohnung 2“ oder hat keine Tuer");
        }

        // Gegenprobe: fern der Strasse (Weg laenger als GebaeudeTuer.WegHoechstens) = gelber Hinweis
        var s3 = p.AussenAendern("Wohnung fern", aa => AussenBearbeitung.Setzen(aa, AussenArt.Gebaeude, 20, 2, raster: true, modell: "wohnung")!);
        if (a.TuerUmschalten(s3) != Texte.TuerWegOhneStrasse)
        {
            Fund("Tuer: Wohnung fern der Strasse meldet nicht „" + Texte.TuerWegOhneStrasse + "“ (Gegenprobe)");
        }
        await a.AussenGeprueft;
        if (!a.AussenFunde.Any(x => !x.Rot && x.Text.StartsWith(Texte.TuerWegOhneStrasse, StringComparison.Ordinal)))
        {
            Fund("Tuer: kein gelber Hinweis fuer den Weg ohne Strasse (Gegenprobe)");
        }

        // „Tür entfernen“ und Rueckgaengig
        a.TuerUmschalten(s);
        if (GebaeudeTuer.HatTuer(p.AussenStand, s))
        {
            Fund("Tuer: „" + Texte.TuerEntfernen + "“ laesst die Tuer stehen");
        }
        a.Rueckgaengig();
        await a.AussenGeprueft;
        if (!GebaeudeTuer.HatTuer(p.AussenStand, s))
        {
            Fund("Tuer: Rueckgaengig nach „" + Texte.TuerEntfernen + "“ bringt die Tuer nicht wieder");
        }
        await BildAsync(a);
    }
}
