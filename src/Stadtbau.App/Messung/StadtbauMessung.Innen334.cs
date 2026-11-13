using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.52.0 (Sammelliste 334, Weisung P12-237 Punkt 5): im Innenraum in 3D laesst sich die Kamera-Karte einklappen
/// (danach braucht sie hoechstens halb so viel Platz, ein Tipp auf „Kamera“ zeigt sie wieder), und ein Moebel, das beim
/// Bauen auf die Figur geschoben wird, laesst die Figur nicht mehr darin stehen (sie steht auf der naechsten freien Stelle).
/// Gegenprobe: wie bis S 0.51.0 steht sie im Moebel. Layout je Groesse hell und dunkel mit eingeklappter Karte.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task Innen334Async(Hauptansicht a, Window? f)
    {
        var p = a.Projekt!;
        if (a.Innen3DKarte is not Border karte || Kamerakarte.KlappKnopf(karte) is not { } klappe)
        {
            Fund("Innenraum 3D: Kamera-Karte ohne Knopf „" + Texte.KameraEinklappen + "“");
            return;
        }
        var offen = Kamerakarte.Hoehe(karte);
        klappe.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        var zu = Kamerakarte.Hoehe(karte);
        if (!Kamerakarte.Eingeklappt(karte) || zu > offen / 2)
        {
            Fund(string.Create(Inv, $"Innenraum 3D: Kamera-Karte nach „{Texte.KameraEinklappen}“ {zu:F0} statt hoechstens {offen / 2:F0} hoch"));
        }
        await AlleGroessenAsync(a, f, "Innenraum 3D Kamera eingeklappt");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        klappe.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        if (Kamerakarte.Eingeklappt(karte) || Math.Abs(Kamerakarte.Hoehe(karte) - offen) > 1)
        {
            Fund("Innenraum 3D: Kamera-Karte nach „" + Texte.KameraAufklappen + "“ nicht wieder ganz da");
        }
        Messwerte.Notiere(string.Create(Inv, $"Innenraum 3D Kamera-Karte: offen {offen:F0}, eingeklappt {zu:F0} px hoch"));

        // Figur: ein Moebel genau auf ihre Stelle schieben (wie Bild 05 der Welle S 0.50.0)
        var stand = PythonJson.Text(p.Grundriss(InnenOrt));
        if (a.Innen3DFlaeche.Spiel?.Innen is not { } innen)
        {
            Fund("Innenraum 3D: kein Innenraum fuer die Probe der Figur");
            return;
        }
        var (sx, sz) = (innen.StartX, innen.StartZ);
        var treffer = Bearbeitung.Alle(p.Grundriss(InnenOrt)).FirstOrDefault(x => !Wege.Begehbar.Contains(Py.Str(x.Deko["typ"])) && Py.F(x.Deko["w"]) >= 0.8 && Py.F(x.Deko["h"]) >= 0.5);
        if (treffer.Deko is null)
        {
            Fund("Innenraum 3D: kein Moebel fuer die Probe der Figur");
            return;
        }
        var d = treffer.Deko;
        var (mx, my) = (Py.F(d["x"]) + (Py.F(d["w"]) / 2), Py.F(d["y"]) + (Py.F(d["h"]) / 2));
        async Task<bool> FigurImMoebelAsync()
        {
            p.Aendern(InnenOrt, "Verschieben", g => Bearbeitung.Verschieben(g, treffer.Stelle, sx - mx, sz - my, false));
            await a.Innen3DFertig;
            await BildAsync(a);
            var s = a.Innen3DFlaeche.Spiel!;
            var drin = !Wege.KannStehen(s.FigurX, s.FigurZ, s.Innen!.Lauf);
            await ZurueckAsync(a, stand);
            return drin;
        }
        if (await FigurImMoebelAsync())
        {
            Fund("Innenraum 3D: nach dem Verschieben eines Moebels auf die Figur steht sie im Moebel");
        }
        Innen3D.FigurImMoebelAltFuerTests = true;
        try
        {
            if (!await FigurImMoebelAsync())
            {
                Fund("Innenraum 3D Gegenprobe: wie bis S 0.51.0 stand die Figur nicht im Moebel, die Probe prueft nichts");
            }
        }
        finally
        {
            Innen3D.FigurImMoebelAltFuerTests = false;
        }
        Messwerte.Notiere("Innenraum 3D: Moebel auf der Figur, Figur steht frei daneben; Gegenprobe alt im Moebel");
    }
}
