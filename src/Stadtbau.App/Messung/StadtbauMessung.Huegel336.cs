using System.Numerics;
using Avalonia.Controls;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.52.0 (Weisung P12-242 Punkt 3, Sammelliste 336): ein Huegel auf Gelaende, das dort schon 1,5 hoch gemalt ist,
/// steht in den Daten, man sieht ihn aber nicht. Mit dem Knopf „Hügel“ und einem Tippen auf die Karte (PC und Tablet gleicher
/// Weg) sagt die Statuszeile das jetzt (<see cref="Texte.HuegelVerdeckt"/>); an freier Stelle bleibt es „Hügel gesetzt“.
/// Gegenprobe: wie bis S 0.51.0 steht nur „Hügel gesetzt“ (stumm).
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task HuegelVerdecktAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Huegel verdeckt", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        var p = a.Projekt!;
        var k = a.Karte;
        if (a.HuegelKnopf is not { } knopf)
        {
            Fund("Huegel verdeckt: Knopf „Hügel“ fehlt");
            return;
        }
        for (var i = 0; i < 15; i++)
        {
            p.AussenAendern("Malen", aa => AussenBearbeitung.Pinselzug(aa, PinselArt.Heben, [new Vector2(10, 10)], 2f));
        }
        await a.AussenGeprueft;
        var texte = new List<string>();
        foreach (var alt in new[] { false, true })
        {
            AussenBearbeitung.HuegelVerdecktAltFuerTests = alt;
            try
            {
                foreach (var (x, y, verdeckt) in new[] { (10.0, 10.0, true), (24.0, 10.0, false) })
                {
                    HuegelWaehlen(a, knopf);
                    var s = k.SetzenAn(x, y);
                    await a.AussenGeprueft;
                    await BildAsync(a);
                    texte.Add((alt ? "alt " : "") + (verdeckt ? "gemalt: " : "frei: ") + a.StatusText);
                    var soll = verdeckt && !alt ? Texte.HuegelVerdeckt : Texte.Huegel + " gesetzt";
                    if (s is null || a.StatusText != soll)
                    {
                        Fund($"Huegel verdeckt{(alt ? " Gegenprobe" : "")}: bei ({x}; {y}) steht „{a.StatusText}“ statt „{soll}“");
                    }
                    if (verdeckt && !alt)
                    {
                        await AlleGroessenAsync(a, f, "Huegel verdeckt Hinweis");
                        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
                    }
                    a.Rueckgaengig();
                    await a.AussenGeprueft;
                }
            }
            finally
            {
                AussenBearbeitung.HuegelVerdecktAltFuerTests = false;
            }
        }
        Messwerte.Notiere("Huegel verdeckt (Sammelliste 336): " + string.Join("; ", texte));
    }
}
