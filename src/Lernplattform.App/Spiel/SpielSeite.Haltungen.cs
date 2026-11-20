using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle A 0.93.0 „Haltungen“ (Weisung P12-303, Sammelliste 352): Springen, Ducken und Kriechen ueber die Tasten der
/// Tastenbelegung (Leertaste, C, X; frei belegbar), am Handy und Tablet ueber die runden Knoepfe am rechten Daumen (A 0.99.0),
/// mit Ansagen (gelungen oder warum nicht). Welle A 1.0.0 (Sammelliste 404, Nico: „Tastatur und Tastenbelegung reichen“): die
/// drei Eintraege im aufgeklappten Menue gibt es nicht mehr, auch nicht am PC.
/// </summary>
public sealed partial class SpielSeite
{
    /// <summary>Taste oder runder Knopf: springen, ducken oder kriechen; die Ansage sagt, was geschah. true = die Aufgabe gehoert hierher.</summary>
    public bool HaltungAusfuehren(string aufgabe)
    {
        if (Spiel is not { } spiel || aufgabe is not (Tastenbelegung.Springen or Tastenbelegung.Ducken or Tastenbelegung.Kriechen))
        {
            return false;
        }
        if (!Flaeche.GlBereit || _ersatz.IsVisible)
        {
            return true;
        }
        // in der Ich-Sicht schaut der Koerper in die Blickrichtung: von dort aus Platz suchen
        float? blick = Flaeche.AktuelleKamera is { } k && StadtFlaeche.IchNah(k) ? IchSicht.FigurDrehung(k, spiel) : null;
        _ = aufgabe switch
        {
            Tastenbelegung.Springen => spiel.Springen(),
            Tastenbelegung.Ducken => spiel.Ducken(blick),
            _ => spiel.Kriechen(blick),
        };
        if (spiel.HaltungHinweis is { } hinweis)
        {
            Views.Ansage.Sagen(hinweis);
        }
        HaltungKnoepfeSetzen();
        Flaeche.AnfordernBild();
        return true;
    }

    /// <summary>Runde Knoepfe am rechten Daumen nach dem Zustand setzen (Handy und Tablet).</summary>
    private void HaltungKnoepfeSetzen() => DaumenSetzen();
}
