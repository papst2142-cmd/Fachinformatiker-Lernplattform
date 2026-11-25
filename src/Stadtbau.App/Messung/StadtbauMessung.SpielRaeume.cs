using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
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
        // Wahl ueber die Liste links: ein Tipp auf „Bitweiche“ oeffnet sie (die Liste zeigt jeden Raum mit ganzem Namen)
        var knoepfe = a.GetLogicalDescendants().OfType<RadioButton>().Where(k => k.Tag is string t && SpielRaeume.Ist(t)).ToList();
        if (knoepfe.Count != liste.Count)
        {
            Fund("Räume des Spiels: die Liste links zeigt " + knoepfe.Count + " statt " + liste.Count + " Räume");
        }
        if (knoepfe.FirstOrDefault(k => (string)k.Tag! == "spiel:buero:bitweiche") is { } bitweiche)
        {
            bitweiche.IsChecked = true;
            await BildAsync(a);
            if (a.SpielRaumOffen != "spiel:buero:bitweiche")
            {
                Fund("Räume des Spiels: ein Tipp in der Liste öffnet die Bitweiche nicht");
            }
        }
        await SchilderAsync(a, f, liste.Select(r => (r.Schluessel, r.Name)).ToList());
        a.SpielRaumOeffnen(liste[0].Schluessel);
        await AlleGroessenAsync(a, f, "Räume des Spiels 2D");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // 3D: Wohnung, Bitweiche, Serverfarm
        if (a.SpielRaum3DKnopf is not { } drei || !drei.IsEffectivelyVisible)
        {
            Fund("Räume des Spiels: Knopf „" + Texte.SpielRaum3DKnopf + "“ fehlt");
            return;
        }
        drei.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await a.SpielRaum3DFertig;
        await BildAsync(a);
        if (!a.ZeigtSpielRaum3D)
        {
            Fund("Räume des Spiels: „" + Texte.SpielRaum3DKnopf + "“ zeigt den Raum nicht in 3D");
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

    /// <summary>
    /// Welle S 0.64.0 (Weisung P12-380 Punkt 415 B1/B2, P12-381 Zusatz): in allen Raeumen des Spiels, PC und Tablet, bei
    /// „Ganzen Raum zeigen“ steht jeder Name einer Person und jede Beschriftung eines Stuecks ganz im sichtbaren Bereich
    /// (nie gekuerzt) und kein Schild ueberdeckt ein anderes, einen Raumnamen oder einen Punkt einer Person. Dazu nach
    /// Vergroessern und Verschieben (B2). Gegenprobe: mit der alten Zeichnung sind Serverfarm (Tablet) und Bitweiche rot.
    /// </summary>
    private static async Task SchilderAsync(Hauptansicht a, Window? f, List<(string Schluessel, string Name)> raeume)
    {
        var flaeche = a.SpielRaumFlaeche;
        int Pruefen(string wo)
        {
            var schilder = flaeche.SchilderJetzt();
            var funde = 0;
            for (var i = 0; i < schilder.Count; i++)
            {
                var (art, s, _) = schilder[i];
                if (!s.Frei)
                {
                    funde++;
                    if (!Schilder.AltFuerTests.Value)
                    {
                        Fund("Schilder " + wo + ": " + art + " „" + s.Text + "“ abgeschnitten oder überdeckt");
                    }
                }
                for (var j = i + 1; j < schilder.Count; j++)
                {
                    if (s.Lage.Ueberdeckung(schilder[j].Schild.Lage) > 0)
                    {
                        funde++;
                        if (!Schilder.AltFuerTests.Value)
                        {
                            Fund("Schilder " + wo + ": „" + s.Text + "“ und „" + schilder[j].Schild.Text + "“ überdecken sich");
                        }
                    }
                }
            }
            return funde;
        }
        int personen = 0, stuecke = 0, raeumeGeprueft = 0;
        foreach (var (name, b, h) in Groessen)
        {
            await GroesseAsync(a, f, b, h);
            foreach (var (schluessel, raum) in raeume)
            {
                a.SpielRaumOeffnen(schluessel);
                flaeche.Einpassen();
                await BildAsync(a);
                var alle = flaeche.SchilderJetzt();
                personen += alle.Count(t => t.Art == GrundrissFlaeche.SchildArt.Person);
                stuecke += alle.Count(t => t.Art == GrundrissFlaeche.SchildArt.Stueck);
                raeumeGeprueft++;
                Pruefen(raum + " " + name);
            }
        }
        Messwerte.Notiere(string.Create(Inv, $"Schilder im Grundriss (415): {raeumeGeprueft} Räume x Größe, {personen} Namen und {stuecke} Beschriftungen geprüft (ganz sichtbar, ohne Überdeckung)"));

        // B2: nach Vergroessern und Verschieben (Maus wie Finger nutzen dieselben Schritte der Flaeche)
        await GroesseAsync(a, f, Groessen[1].B, Groessen[1].H);
        a.SpielRaumOeffnen("spiel:buero:bitweiche");
        flaeche.Einpassen();
        flaeche.Vergroessern();
        flaeche.Vergroessern();
        await BildAsync(a);
        Pruefen("Bitweiche Tablet vergrößert");
        flaeche.AnsichtSchieben(new Avalonia.Vector(120, -80));
        await BildAsync(a);
        Pruefen("Bitweiche Tablet verschoben");

        // Gegenprobe: alte Zeichnung (gleich am Punkt bzw. im Stueck gekuerzt) muss in diesen Raeumen Funde haben
        Schilder.AltFuerTests.Value = true;
        try
        {
            var gegen = new List<string>();
            foreach (var (schluessel, wo) in new[] { ("spiel:serverfarm", "Serverfarm"), ("spiel:buero:bitweiche", "Bitweiche") })
            {
                a.SpielRaumOeffnen(schluessel);
                flaeche.Einpassen();
                await BildAsync(a);
                gegen.Add(wo + " " + Pruefen(wo + " alt").ToString(Inv));
            }
            Messwerte.Notiere("Schilder Gegenprobe Tablet mit alter Zeichnung: " + string.Join(", ", gegen) + " Funde");
            if (gegen.Any(g => g.EndsWith(" 0", StringComparison.Ordinal)))
            {
                Fund("Schilder: Gegenprobe mit alter Zeichnung ohne Fund (" + string.Join(", ", gegen) + "), die Probe taugt nicht");
            }
        }
        finally
        {
            Schilder.AltFuerTests.Value = false;
        }
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
    }

    private static bool PythonGleich(object? a, object? b) =>
        Lernplattform.Core.Daten.PythonJson.Text(a) == Lernplattform.Core.Daten.PythonJson.Text(b);
}
