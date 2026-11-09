using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.25.0 (Weisung P12-104, Sammelliste 246 und 247): Stadt 1, Kiste aus dem Katalog vor der Tuer des Stellwerks =
/// roter Fund „… steht vor der Tür von „Stellwerk“ …“ in der Pruefliste (Layout PC und Tablet, hell und dunkel);
/// Gegenprobe ohne Regel: kein Fund; Kiste daneben: kein Fund. Beschriftung jedes Gebaeudes ohne Ort deutsch, nie die
/// Kennung (Gegenprobe alte Beschriftung = Kennung).
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task TuerFreiAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Tuer frei", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;

        // 247: Beschriftung
        var land = Aussen.Landschaft(p.AussenStand);
        var gebaeude = AussenBearbeitung.Alle(p.AussenStand).Where(x => x.Stelle.Art == AussenArt.Gebaeude).Select(x => x.Stelle).ToList();
        if (gebaeude.Count == 0)
        {
            Fund("Tuer frei: Stadt 1 ohne Gebaeude ohne Ort");
        }
        foreach (var s in gebaeude)
        {
            var modell = Py.Str(Objekt(land[s.Nummer])["modell"]);
            if (ModellNamen.Beschriftung(p.AussenStand, s) == modell)
            {
                Fund("Tuer frei: Draufsicht beschriftet „" + modell + "“ mit der Kennung");
            }
            ModellNamen.BeschriftungAltFuerTests.Value = true;
            var alt = ModellNamen.Beschriftung(p.AussenStand, s);
            ModellNamen.BeschriftungAltFuerTests.Value = false;
            if (alt != modell)
            {
                Fund("Tuer frei: Gegenprobe alte Beschriftung zeigt nicht die Kennung „" + modell + "“");
            }
        }

        // 246: Kiste vor der Tuer des Stellwerks
        var kiste = Katalog.Aktiv!.Eintraege.First(k => k.Art == KatalogArt.Modell && k.Aussen[0] is >= 0.3 and <= 0.5 && k.Aussen[1] is >= 0.3 and <= 0.5);
        p.AussenAendern("Kiste", aa => AussenBearbeitung.Setzen(aa, AussenArt.Katalog, 17.3, 14.1, raster: false, modell: kiste.Id));
        await a.AussenGeprueft;
        var erwartet = Texte.TuerVerstellt(kiste.Anzeige, "Stellwerk");
        if (!a.AussenFunde.Any(x => x.Rot && x.Text == erwartet))
        {
            Fund("Tuer frei: Kiste vor der Tuer des Stellwerks ohne roten Fund");
        }
        TuerFrei.AusFuerTests.Value = true;
        var ohne = TuerFrei.Pruefen(p.AussenStand);
        TuerFrei.AusFuerTests.Value = false;
        if (ohne.Count != 0)
        {
            Fund("Tuer frei: Gegenprobe ohne Regel meldet trotzdem");
        }
        await BildAsync(a);
        await AlleGroessenAsync(a, f, "Kiste vor der Tuer");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.Rueckgaengig();
        await a.AussenGeprueft;

        // Kiste neben dem Weg: kein Fund
        p.AussenAendern("Kiste daneben", aa => AussenBearbeitung.Setzen(aa, AussenArt.Katalog, 16.5, 14.2, raster: false, modell: kiste.Id));
        await a.AussenGeprueft;
        if (a.AussenFunde.Any(x => x.Text.EndsWith(": die Figur kommt nicht hinein.", StringComparison.Ordinal)))
        {
            Fund("Tuer frei: Kiste neben dem Weg meldet „vor der Tür“");
        }
        a.Rueckgaengig();
        await a.AussenGeprueft;
        await BildAsync(a);
    }
}
