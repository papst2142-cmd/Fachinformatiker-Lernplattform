using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle D2 (Weisung P12-157, Kurzplan P12-97, Hinweise P12-156): Leiste „Grenzen live“, Waermekarte und roter Fund
/// „Stadt zu teuer“ an Stadt 1.
/// <list type="bullet">
/// <item>Die Leiste steht nach dem Oeffnen unten links mit Prozent und Klammer (welcher Wert den Balken fuellt), Stadt 1 liegt
/// unter der Grenze. Messpunkt „Grenzen live (bis zur Anzeige)“ (Baum setzen bis zur neuen Leiste, wiederholbar, 400 ms).</item>
/// <item>Zaehlung ohne Grafikkarte = gezeichnetes Bild der 3D-Ansicht (Gesamtansicht und Nahansichten an der teuersten Stelle);
/// Gegenprobe: ohne Sichtkegel gerechnet liegt die Nahansicht hoeher.</item>
/// <item>Waermekarte (Knopf und Taste W): gefaerbte Felder nur auf der Karte, Legende in der Leiste, Layout je Groesse.</item>
/// <item>Gegenprobe „Stadt zu teuer“: 200 Geschenke auf einem Fleck = roter Fund mit Stelle, Export gesperrt.</item>
/// </list>
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task GrenzenLiveAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Grenzen live", null);
        await a.AussenGeprueft;
        await a.GrenzenLiveFertig;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        if (a.GrenzenLiveStand is not { } g)
        {
            Fund("Grenzen live: kein Ergebnis nach dem Oeffnen");
            return;
        }
        Messwerte.Notiere("Grenzen live Stadt 1 (Nacht, Winter, Weihnachten): " + g);
        if (g.Ueber)
        {
            Fund("Grenzen live: Stadt 1 liegt ueber der Grenze: " + g);
        }
        if (!a.GrenzLeiste.IsEffectivelyVisible || !a.GrenzLeisteKopf.Contains('%') || !a.GrenzLeisteKopf.Contains('('))
        {
            Fund("Grenzen live: Leiste nicht sichtbar oder ohne Anteil und Klammer: \"" + a.GrenzLeisteKopf + "\"");
        }

        // Messpunkt: Baum setzen bis zur neuen Leiste
        var vorher = a.GrenzenLiveStand;
        await MessenAsync("Grenzen live (bis zur Anzeige)", a, async () =>
        {
            p.AussenAendern("Baum", aa => AussenBearbeitung.Setzen(aa, AussenArt.Baum, 4.5, 21.5));
            await a.GrenzenLiveFertig;
        }, async () =>
        {
            a.Rueckgaengig();
            await a.GrenzenLiveFertig;
        });
        if (ReferenceEquals(a.GrenzenLiveStand, vorher))
        {
            Fund("Grenzen live: nach der Aenderung kein neues Ergebnis");
        }
        a.Rueckgaengig();
        await a.AussenGeprueft;
        await a.GrenzenLiveFertig;

        // Zaehlung = gezeichnetes Bild (3D-Ansicht, Szene der Grenzen)
        a.DreiZeigen();
        await a.DreiFertig;
        if (await a.ZaehlungVergleichenAsync() is { } v)
        {
            Messwerte.Notiere(string.Create(Inv, $"Grenzen live: Zaehlung ohne Grafikkarte gegen das Bild der 3D-Ansicht: {v.Bilder} Bilder, {v.Abweichungen.Count} Abweichungen"));
            foreach (var z in v.Abweichungen.Take(10))
            {
                Fund("Grenzen live: Zaehlung weicht vom gezeichneten Bild ab: " + z);
            }
        }
        else
        {
            Messwerte.Notiere("Grenzen live: Zaehlung nicht mit dem Bild verglichen (kein OpenGL oder kein Instancing auf diesem Rechner)");
        }
        // Gegenprobe der Zaehlung: ohne Sichtkegel liegt die teuerste Nahansicht hoeher
        GrenzenLive.OhneSparenFuerTests = true;
        try
        {
            var ohne = await Task.Run(() => GrenzenLive.Fuer(p.Stand(), p.Daten));
            Messwerte.Notiere("Grenzen live Gegenprobe ohne Sichtkegel: " + ohne);
            if (ohne.Nah.Dreiecke <= g.Nah.Dreiecke)
            {
                Fund("Grenzen live Gegenprobe: ohne Sichtkegel nicht teurer (" + ohne + " gegen " + g + ")");
            }
        }
        finally
        {
            GrenzenLive.OhneSparenFuerTests = false;
        }
        await AlleGroessenAsync(a, f, "Grenzen live 3D");
        a.ReiterZeigen(innen: false);

        // Waermekarte: Knopf, nur Felder auf der Karte, Legende
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.WaermeKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        var (breite, tiefe) = Aussen.Groesse(p.AussenStand);
        var aufKarte = 0;
        for (var y = 0; y < g.Tiefe; y++)
        {
            for (var x = 0; x < g.Breite; x++)
            {
                aufKarte += x < breite && y < tiefe && GrenzenLive.Stufe(g.Feld(x, y)) >= 0 ? 1 : 0;
            }
        }
        Messwerte.Notiere(string.Create(Inv, $"Waermekarte: {a.Karte.WaermeFelderGezeichnet} Felder gefaerbt, {aufKarte} Felder mit Dreiecken auf der Karte ({g.Breite} x {g.Tiefe})"));
        if (!a.Karte.WaermeAn || a.Karte.WaermeFelderGezeichnet != aufKarte || aufKarte == 0 || !a.WaermeLegende.IsEffectivelyVisible)
        {
            Fund("Waermekarte: nicht an, falsche Zahl gefaerbter Felder oder keine Legende");
        }
        await AlleGroessenAsync(a, f, "Waermekarte");
        a.TasteFuerTests(Key.W);
        await BildAsync(a);
        if (a.Karte.WaermeAn || a.Karte.WaermeFelderGezeichnet != 0)
        {
            Fund("Waermekarte: Taste W schaltet sie nicht aus");
        }

        // Gegenprobe: zu teuer = roter Fund mit Stelle, Export gesperrt (das Modell mit den meisten Dreiecken dicht an dicht)
        var gross = Katalog.Aktiv!.Eintraege.Where(e => e.Art == KatalogArt.Modell).MaxBy(e => e.Dreiecke)!;
        var zahl = (int)Math.Min(400, (StadtbauGrenzen.Dreiecke / Math.Max(1, gross.Dreiecke)) + 20);
        p.AussenAendern("Viele Stuecke", aa =>
        {
            for (var i = 0; i < zahl; i++)
            {
                AussenBearbeitung.Setzen(aa, AussenArt.Katalog, 2 + (i % 20 * 0.1), 20 + (i / 20 * 0.1), raster: false, modell: gross.Id);
            }
            return 0;
        });
        await a.AussenGeprueft;
        await a.GrenzenLiveFertig;
        var teuer = a.AussenFunde.FirstOrDefault(x => x.Rot && x.Text.StartsWith("Stadt zu teuer", StringComparison.Ordinal));
        Messwerte.Notiere(string.Create(Inv, $"Grenzen live mit {zahl} x {gross.Id} ({gross.Dreiecke} Dreiecke): {a.GrenzenLiveStand}"));
        if (teuer is null || teuer.X is null || p.RoteFunde().All(r => !r.Fund.Text.StartsWith("Stadt zu teuer", StringComparison.Ordinal)))
        {
            Fund("Grenzen live Gegenprobe: viele Stuecke auf einem Fleck ergeben keinen roten Fund \"Stadt zu teuer\" mit Stelle oder sperren den Export nicht");
        }
        else
        {
            await AlleGroessenAsync(a, f, "Stadt zu teuer");
        }
    }
}
