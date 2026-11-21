using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.60.0 (Weisung P12-337): „Räume des Spiels ansehen“. Knopf in den Innenraeumen oeffnet die Ansicht, jeder Raum
/// des Spiels oeffnet sich im Grundriss (gesperrt, kein Schritt im Verlauf), Wohnung, Bitweiche und Serverfarm auch in 3D (die
/// 3D-Ansicht zeigt genau den Raum, den das Projekt wie das Spiel baut), „Zurück zu den Innenräumen“. Layout je Groesse hell
/// und dunkel in 2D und 3D.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task SpielRaeumeAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Spiel-Raeume", "elektromarkt");
        a.OrtOeffnen("elektromarkt");
        await a.Geprueft;
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        if (a.SpielRaeumeKnopf is not { } knopf || !knopf.IsEffectivelyVisible)
        {
            Fund("Räume des Spiels: Knopf „" + Texte.SpielRaeumeKnopf + "“ fehlt in den Innenräumen");
            return;
        }
        knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        var p = a.Projekt!;
        var liste = p.SpielRaeumeListe();
        if (!a.ZeigtSpielRaeume || liste.Count == 0 || a.SpielRaumOffen != liste[0].Schluessel)
        {
            Fund("Räume des Spiels: der Knopf öffnet die Ansicht nicht mit dem ersten Raum (" + a.SpielRaumOffen + ")");
            return;
        }
        var schritte = p.SchritteZurueck;
        foreach (var r in liste)
        {
            a.SpielRaumOeffnen(r.Schluessel);
            await BildAsync(a);
            if (a.SpielRaumOffen != r.Schluessel || a.SpielRaumFlaeche.Ort != r.Schluessel || !a.SpielRaumFlaeche.Gesperrt
                || !a.SpielRaumFlaeche.IsEffectivelyVisible || p.SpielRaum(r.Schluessel) is null)
            {
                Fund("Räume des Spiels: " + r.Name + " öffnet sich nicht gesperrt im Grundriss");
            }
            // gesperrt: ein Stueck waehlen geht, Loeschen und Drehen ueber die Flaeche bewirken nichts
            if (a.SpielRaumFlaeche.GebaeudeJetzt is { } g && Bearbeitung.Alle(g).Select(t => t.Item1).FirstOrDefault() is { } stelle)
            {
                a.SpielRaumFlaeche.Waehlen(stelle);
            }
            a.SpielRaumFlaeche.Loeschen();
            a.SpielRaumFlaeche.Drehen();
        }
        if (p.SchritteZurueck != schritte)
        {
            Fund("Räume des Spiels: Ansehen hat Schritte im Verlauf angelegt (" + schritte + " → " + p.SchritteZurueck + ")");
        }
        a.SpielRaumOeffnen(liste[0].Schluessel);
        await AlleGroessenAsync(a, f, "Räume des Spiels 2D");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // 3D: Wohnung, Bitweiche, Serverfarm
        if (a.SpielRaum3DKnopf is not { } drei || !drei.IsEffectivelyVisible)
        {
            Fund("Räume des Spiels: Knopf „" + Texte.Innen3DKnopf + "“ fehlt");
            return;
        }
        drei.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await a.SpielRaum3DFertig;
        await BildAsync(a);
        if (!a.ZeigtSpielRaum3D)
        {
            Fund("Räume des Spiels: „" + Texte.Innen3DKnopf + "“ zeigt den Raum nicht in 3D");
            return;
        }
        if (!a.SpielRaum3DFlaeche.GlBereit)
        {
            Messwerte.Notiere("Räume des Spiels 3D: kein OpenGL auf diesem Rechner, Hinweis gezeigt (" + a.SpielRaum3DStatusText + ")");
        }
        else
        {
            foreach (var (schluessel, ansicht) in new[] { (liste[0].Schluessel, "zuhause"), ("spiel:buero:bitweiche", "buero"), ("spiel:serverfarm", Innenraum.Serverfarm) })
            {
                a.SpielRaumOeffnen(schluessel);
                await a.SpielRaum3DFertig;
                await BildAsync(a);
                var innen = a.SpielRaum3DFlaeche.Spiel?.Innen;
                if (innen?.Ansicht != ansicht || a.SpielRaum3DStatusText.Length > 0 || !ReferenceEquals(innen, a.SpielRaum3DInnen)
                    || p.SpielRaum(schluessel) is not { } werkzeug || PythonGleich(werkzeug.Gebaeude, innen.Gebaeude) is false)
                {
                    Fund("Räume des Spiels 3D: " + schluessel + " zeigt \"" + innen?.Ansicht + "\" oder einen anderen Grundriss (" + a.SpielRaum3DStatusText + ")");
                }
            }
        }
        await AlleGroessenAsync(a, f, "Räume des Spiels 3D");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.SpielRaum2DKnopf?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        if (a.ZeigtSpielRaum3D || !a.SpielRaumFlaeche.IsEffectivelyVisible)
        {
            Fund("Räume des Spiels: „" + Texte.Innen3DZurueck + "“ zeigt den Grundriss nicht wieder");
        }

        // zurueck zu den Innenraeumen der Stadt
        a.ReiterZeigen(innen: true);
        await BildAsync(a);
        if (a.ZeigtSpielRaeume || !a.Flaeche.IsEffectivelyVisible)
        {
            Fund("Räume des Spiels: „" + Texte.SpielRaeumeZurueck + "“ zeigt die Innenräume nicht wieder");
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    private static bool PythonGleich(object? a, object? b) =>
        Lernplattform.Core.Daten.PythonJson.Text(a) == Lernplattform.Core.Daten.PythonJson.Text(b);
}
