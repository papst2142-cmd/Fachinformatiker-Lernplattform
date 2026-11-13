using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.49.0 (Weisung P12-231 Antwort 1a): Innenraum in 3D ansehen. Knopf „In 3D ansehen“ im Grundriss des Elektromarkts
/// (Messpunkt „Innenraum 3D oeffnen (bis zum Bild)“, einmalig), Aenderung im Grundriss bis zum neuen Bild (Messpunkt
/// „Innenraum 3D nach Aenderung (bis zum Bild)“, wiederholbar, 400 ms), Nacht, Etage des Moebelhauses an der Ankunft,
/// Bauen in 3D (S 0.50.0, <see cref="InnenBauen3DAsync"/>), „Figur laufen lassen“ mit Gegenprobe (aus: Tippen bewegt nichts),
/// zurueck zum Grundriss. Layout je Groesse hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task Innen3DAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Innen 3D", "elektromarkt");
        a.OrtOeffnen("elektromarkt");
        await a.Geprueft;
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        if (a.Innen3DKnopf is not { } knopf || !knopf.IsEffectivelyVisible)
        {
            Fund("Innenraum 3D: Knopf „" + Texte.Innen3DKnopf + "“ fehlt in der Kamera-Karte des Grundrisses");
            return;
        }
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        knopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await a.Innen3DFertig;
        await BildAsync(a);
        if (!a.ZeigtInnen3D)
        {
            Fund("Innenraum 3D: Knopf zeigt den Innenraum nicht in 3D");
            return;
        }
        if (!a.Innen3DFlaeche.GlBereit)
        {
            Messwerte.Notiere("Innenraum 3D: kein OpenGL auf diesem Rechner, Hinweis gezeigt (" + a.Innen3DStatusText + ")");
            await AlleGroessenAsync(a, f, "Innenraum 3D");
            a.Innen3DZeigen(false);
            return;
        }
        DreiGrenze("Innenraum 3D oeffnen (bis zum Bild)", [uhr.Elapsed.TotalMilliseconds], einmalig: true);
        var spiel = a.Innen3DFlaeche.Spiel;
        if (spiel?.Innen?.Ansicht != "geschaeft:elektromarkt" || a.Innen3DStatusText.Length > 0)
        {
            Fund("Innenraum 3D: zeigt \"" + spiel?.Innen?.Ansicht + "\" statt des Elektromarkts (" + a.Innen3DStatusText + ")");
            return;
        }

        // Aenderung im Grundriss bis zum neuen Bild (der Innenraum wird neu gebaut, das Spiel bleibt)
        var p = a.Projekt!;
        var werte = new List<double>();
        for (var i = 0; i < 3; i++)
        {
            var innenVorher = a.Innen3DFlaeche.Spiel?.Innen;
            p.Aendern("elektromarkt", "Boden", g => Bearbeitung.Boden(g, "flur", i % 2 == 0 ? "teppich" : "beton"));
            await a.Innen3DFertig;
            werte.Add(a.Innen3DMs);
            if (ReferenceEquals(innenVorher, a.Innen3DFlaeche.Spiel?.Innen) || !ReferenceEquals(spiel, a.Innen3DFlaeche.Spiel))
            {
                Fund("Innenraum 3D: nach einer Aenderung im Grundriss kein neuer Innenraum (oder das Spiel neu gebaut)");
            }
            if (a.Innen3DMs <= Grenze)
            {
                break;
            }
            await Task.Delay(200);
        }
        DreiGrenze("Innenraum 3D nach Aenderung (bis zum Bild)", werte, einmalig: false);
        await AlleGroessenAsync(a, f, "Innenraum 3D");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // Nacht
        a.Innen3DNacht = true;
        await a.Innen3DFertig;
        await BildAsync(a);
        if (Tageslicht.FesteUhr != 22 * 60)
        {
            Fund("Innenraum 3D: „Nacht“ stellt die Uhr nicht auf den Abend");
        }
        await AlleGroessenAsync(a, f, "Innenraum 3D Nacht");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.Innen3DNacht = false;
        await a.Innen3DFertig;

        // S 0.50.0 (Weisung P12-234): Moebel in 3D setzen, schieben, drehen, loeschen
        await InnenBauen3DAsync(a, f);

        // Figur laufen lassen: aus = Tippen bewegt nichts (Gegenprobe), an = die Figur geht los
        var mitte = new Point(a.Innen3DFlaeche.Bounds.Width / 2, a.Innen3DFlaeche.Bounds.Height / 2);
        a.Innen3DFigurLaufen = false;
        a.Innen3DFlaeche.Tippen(mitte);
        if (a.Innen3DFlaeche.Spiel?.Ziel is not null)
        {
            Fund("Innenraum 3D: ohne „Figur laufen lassen“ laeuft die Figur nach einem Tipp los (Gegenprobe)");
        }
        a.Innen3DFigurLaufen = true;
        a.Innen3DFlaeche.Tippen(mitte);
        if (a.Innen3DFlaeche.Spiel?.Ziel is null)
        {
            Fund("Innenraum 3D: mit „Figur laufen lassen“ laeuft die Figur nach einem Tipp nicht los");
        }
        a.Innen3DFlaeche.Spiel?.SofortAnkommen();
        a.Innen3DFigurLaufen = false;

        // Etage: Moebelhaus mit 1. OG, die 3D-Ansicht zeigt die Etage, die Figur steht an der Ankunft der Treppe
        p.Aendern("moebelhaus", "Etage", g =>
        {
            EtagenBearbeitung.Hinzufuegen(g);
            var s = Bearbeitung.Setzen(g, Etagen.Treppe, 9, 13)!;
            var d = Bearbeitung.Deko(g, s);
            Py.Setzen(d, "x", 8.0);
            Py.Setzen(d, "y", 12.2);
            Py.Setzen(d, "w", 3.0);
            Py.Setzen(d, "h", 1.6);
            Py.Setzen(d, "dreh", 3L);
            EtagenBearbeitung.ZielSetzen(g, 0, s, 1);
            EtagenBearbeitung.Aufraeumen(g);
        });
        a.OrtOeffnen("moebelhaus@1");
        await a.Innen3DFertig;
        await BildAsync(a);
        var halt = Etagen.Uebergaenge(p.Grundriss("moebelhaus"), "moebelhaus").Select(u => u.Auf(1)).FirstOrDefault(h => h is not null);
        if (a.Innen3DFlaeche.Spiel is not { Innen.Ansicht: "geschaeft:moebelhaus@1" } oben || halt is null
            || Math.Abs(oben.FigurX - halt.X) > 0.01 || Math.Abs(oben.FigurZ - halt.Y) > 0.01)
        {
            Fund("Innenraum 3D: das 1. OG des Moebelhauses steht nicht mit der Figur an der Ankunft da");
        }
        // Gegenprobe zu „Kamera bleibt beim Bauen“ (S 0.50.0): ein anderer Raum beginnt mit der Kamera an der Figur
        if (a.Innen3DFlaeche is { AktuelleKamera: { } kam, Spiel: { } sp }
            && (Math.Abs(kam.ZielX - sp.FigurX) > 0.01 || Math.Abs(kam.ZielZ - sp.FigurZ) > 0.01))
        {
            Fund("Innenraum 3D: nach dem Wechsel der Etage steht die Kamera nicht an der Figur");
        }
        if (!a.Innen3DEtage.IsEffectivelyVisible || a.Innen3DEtage.ItemCount != 2)
        {
            Fund("Innenraum 3D: die Wahl der Etage fehlt (" + a.Innen3DEtage.ItemCount + " Eintraege)");
        }
        await AlleGroessenAsync(a, f, "Innenraum 3D Etage");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // zurueck zum Grundriss
        a.Innen3DZurueckKnopf?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BildAsync(a);
        if (a.ZeigtInnen3D || !a.Flaeche.IsEffectivelyVisible)
        {
            Fund("Innenraum 3D: „" + Texte.Innen3DZurueck + "“ zeigt den Grundriss nicht wieder");
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }
}
