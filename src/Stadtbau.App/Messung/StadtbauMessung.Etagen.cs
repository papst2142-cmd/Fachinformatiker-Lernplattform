using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.16.0 Welle W5 „Etagen“ (Weisung P12-73): im Moebelhaus eine Etage anlegen (Messpunkt „Etage hinzufuegen (bis zum
/// Bild)“, wiederholbar, 400 ms), eine Treppe setzen und ihr Ziel waehlen (Gegenstueck oben, ohne roten Fund), die
/// Ankunft ziehen (ein Schritt), Etage mit Treppe nicht loeschbar. Layout PC und Tablet, hell und dunkel, mit gewaehlter
/// Treppe (Ziel-Auswahl) und im 1. Obergeschoss (Ankunftspfeil).
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task EtagenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Etagen", "moebelhaus");
        a.ReiterZeigen(innen: true);
        a.OrtOeffnen("moebelhaus");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        await BildAsync(a);
        var p = a.Projekt!;
        if (a.EtagenKnoepfe.Count != 1 || a.EtageHinzuKnopf is null)
        {
            Fund("Etagen: Liste zeigt nicht nur das Erdgeschoss mit „" + Texte.EtageHinzufuegen + "“ (" + a.EtagenKnoepfe.Count + " Knoepfe)");
        }

        // Etage hinzufuegen bis zum Bild (danach Rueckgaengig, gemessen wird dreimal)
        await MessenAsync("Etage hinzufuegen (bis zum Bild)", a, () =>
        {
            a.EtageHinzufuegenFuerTests();
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            a.OrtOeffnen("moebelhaus");
            return Task.CompletedTask;
        });
        if (Etagen.Anzahl(p.Grundriss("moebelhaus")) != 1 || a.Ort != "moebelhaus@1" || a.EtagenKnoepfe.Count != 2)
        {
            Fund("Etagen: nach „" + Texte.EtageHinzufuegen + "“ nicht im 1. Obergeschoss (" + a.Ort + ", " + a.EtagenKnoepfe.Count + " Knoepfe)");
            return;
        }

        // Treppe im Erdgeschoss setzen und waehlen, Ziel 1. Obergeschoss
        a.OrtOeffnen("moebelhaus");
        await BildAsync(a);
        Stelle? stelle = null;
        p.Aendern("moebelhaus", "Setzen", g =>
        {
            stelle = Bearbeitung.Setzen(g, Etagen.Treppe, 9, 13);
            var d = Bearbeitung.Deko(g, stelle!);
            Py.Setzen(d, "x", 8.0);
            Py.Setzen(d, "y", 12.2);
            Py.Setzen(d, "w", 3.0);
            Py.Setzen(d, "h", 1.6);
            Py.Setzen(d, "dreh", 3L);
        });
        a.Flaeche.Waehlen(stelle);
        await BildAsync(a);
        if (a.ZielWahl is null)
        {
            Fund("Etagen: gewaehlte Treppe zeigt keine Auswahl „" + Texte.Ziel + "“");
        }
        await AlleGroessenAsync(a, f, "Etagen Treppe ohne Ziel");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        if (!a.ZielSetzenFuerTests(1))
        {
            Fund("Etagen: Ziel 1. Obergeschoss nicht gesetzt");
        }
        await BildAsync(a);
        var eg = p.Grundriss("moebelhaus");
        if (Etagen.Von(eg, "moebelhaus", "u1") is not { } u || u.Auf(1) is null || Etagen.Stuecke(Etagen.Gebaeude(eg, 1), "u1").Count != 1)
        {
            Fund("Etagen: Treppe ohne Gegenstueck im 1. Obergeschoss (" + PythonJson.Text(Get(eg, Etagen.FeldUebergaenge)) + ")");
            return;
        }
        var rot = Etagen.Anzahl(eg) == 1 ? p.RoteFunde() : [];
        if (rot.Count > 0)
        {
            Fund("Etagen: Treppe mit Ziel hat rote Funde: " + string.Join(" | ", rot.Select(r => p.OrtName(r.Ort) + ": " + r.Fund.Text)));
        }
        await AlleGroessenAsync(a, f, "Etagen Treppe mit Ziel");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // 1. Obergeschoss: Ankunft ziehen = ein Schritt, Rueckgaengig bringt sie zurueck
        a.OrtOeffnen("moebelhaus@1");
        await BildAsync(a);
        var schritte = p.Verlauf.Count;
        a.Flaeche.AnkunftZiehenFuerTests("u1", 12, 13.5);
        await BildAsync(a);
        var h = Etagen.Von(p.Grundriss("moebelhaus"), "moebelhaus", "u1")!.Auf(1)!;
        if ((h.X, h.Y) != (12.0, 13.5) || p.Verlauf.Count != schritte + 1)
        {
            Fund($"Etagen: Ankunft nicht gezogen ({h.X} {h.Y}, Schritte {p.Verlauf.Count - schritte})");
        }
        a.Rueckgaengig();
        h = Etagen.Von(p.Grundriss("moebelhaus"), "moebelhaus", "u1")!.Auf(1)!;
        if ((h.X, h.Y) != (11.6, 13.0))
        {
            Fund($"Etagen: Rueckgaengig bringt die Ankunft nicht zurueck ({h.X} {h.Y})");
        }
        // Gegenprobe: eine Etage mit Treppe laesst sich nicht loeschen
        if (EtagenBearbeitung.Loeschen(Daten(p.Grundriss("moebelhaus")), 1))
        {
            Fund("Etagen: Etage mit Treppe liess sich loeschen");
        }
        await AlleGroessenAsync(a, f, "Etagen 1. Obergeschoss");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.OrtOeffnen("moebelhaus");
        await BildAsync(a);
    }

    private static PyObjekt Daten(PyObjekt g) => (PyObjekt)Py.Kopie(g)!;
}
